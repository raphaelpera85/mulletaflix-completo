using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class BackupControllerAuthorizationHttpTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;

    private static readonly (HttpMethod Method, string Path)[] BackupRoutes =
    [
        (HttpMethod.Post, "/Backup/Create"),
        (HttpMethod.Post, "/Backup/Restore"),
        (HttpMethod.Post, "/Backup/RestorePointInTime"),
        (HttpMethod.Get, "/Backup"),
        (HttpMethod.Get, "/Backup/Manifest?path=backup.zip"),
        (HttpMethod.Get, "/Backup/History"),
        (HttpMethod.Get, "/Backup/Validate?path=backup.zip")
    ];

    [Fact]
    public async Task AllBackupRoutes_RequireAdministratorRole()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(BackupController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy(
            Policies.RequiresElevation,
            policy => policy.AddAuthenticationSchemes(TestScheme)
                .RequireClaim(ClaimTypes.Role, UserRoles.Administrator)));

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        foreach (var (method, path) in BackupRoutes)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(401, (int)response.StatusCode);
        }

        client.DefaultRequestHeaders.Add("X-Test-Role", UserRoles.User);
        foreach (var (method, path) in BackupRoutes)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(403, (int)response.StatusCode);
        }
    }

    [Fact]
    public async Task RestoreRoute_AsAdministrator_SchedulesExistingArchive()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupPath);

        try
        {
            var archivePath = Path.Combine(backupPath, "restore.zip");
            await File.WriteAllTextAsync(archivePath, "test archive", TestContext.Current.CancellationToken);

            var applicationPaths = new Mock<IServerApplicationPaths>();
            applicationPaths.SetupGet(paths => paths.BackupPath).Returns(backupPath);
            var applicationHost = new Mock<IServerApplicationHost>();
            applicationHost.SetupProperty(host => host.RestoreBackupPath);
            applicationHost.SetupProperty(host => host.ShouldRestart);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(BackupController).Assembly);
            builder.Services.AddSingleton(applicationPaths.Object);
            builder.Services.AddSingleton<IApplicationPaths>(applicationPaths.Object);
            builder.Services.AddSingleton<IServerApplicationHost>(applicationHost.Object);
            builder.Services.AddSingleton(Mock.Of<IDbContextFactory<MulletaFlixDbContext>>());
            builder.Services.AddSingleton(Mock.Of<IMulletaFlixDatabaseProvider>());
            builder.Services.AddSingleton<IBackupService>(services => new BackupService(
                NullLogger<BackupService>.Instance,
                services.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>(),
                services.GetRequiredService<IServerApplicationHost>(),
                services.GetRequiredService<IServerApplicationPaths>(),
                services.GetRequiredService<IMulletaFlixDatabaseProvider>(),
                services.GetRequiredService<IHostApplicationLifetime>()));
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestScheme;
                    options.DefaultChallengeScheme = TestScheme;
                    options.DefaultForbidScheme = TestScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
            builder.Services.AddAuthorization(options => options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator)));

            await using var application = builder.Build();
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapControllers();
            await application.StartAsync(TestContext.Current.CancellationToken);

            using var client = application.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-Role", UserRoles.Administrator);
            using var content = new StringContent("{\"ArchiveFileName\":\"restore.zip\"}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/Backup/Restore", content, TestContext.Current.CancellationToken);

            Assert.Equal(204, (int)response.StatusCode);
            Assert.Equal(archivePath, applicationHost.Object.RestoreBackupPath);
            Assert.True(applicationHost.Object.ShouldRestart);
            applicationHost.Verify(host => host.NotifyPendingRestart(), Times.Once);
            await application.WaitForShutdownAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }

    private sealed class RoleHeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public RoleHeaderAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Role, role.ToString()) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
