using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MediaBrowser.Model.Serialization;
using MulletaFlix.Plugin.GetAvatar.Configuration;
using MulletaFlix.Plugin.GetAvatar.Controllers;
using MulletaFlix.Plugin.GetAvatar.Services;
using Xunit;
using GetAvatarPlugin = MulletaFlix.Plugin.GetAvatar.Plugin;
using User = MulletaFlix.Database.Implementations.Entities.User;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection(GetAvatarPluginTestCollection.Name)]
public sealed class GetAvatarAdminAuthorizationTests
{
    private const string TestScheme = "GetAvatarAdminTest";

    [Theory]
    [InlineData("POST", "/GetAvatar/Upload", 400)]
    [InlineData("DELETE", "/GetAvatar/Delete/missing-avatar", 404)]
    [InlineData("POST", "/GetAvatar/RemoveUserAvatar", 400)]
    [InlineData("POST", "/GetAvatar/ValidateAvatars", 200)]
    [InlineData("POST", "/GetAvatar/CleanupOrphans", 200)]
    [InlineData("GET", "/GetAvatar/AvatarStatus", 200)]
    public async Task AdministratorCanReachAvatarAdminActionsThroughHttpPipeline(
        string method,
        string path,
        int expectedStatusCode)
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "GetAvatarAdminAuthorization", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        var previousPlugin = GetAvatarPlugin.Instance;

        try
        {
            var userManager = new Mock<IUserManager>();
            userManager.Setup(manager => manager.GetUsers()).Returns(Array.Empty<User>());

            var applicationPaths = new Mock<IServerApplicationPaths>();
            applicationPaths.SetupGet(paths => paths.PluginConfigurationsPath).Returns(dataDirectory);
            applicationPaths.SetupGet(paths => paths.PluginsPath).Returns(dataDirectory);
            applicationPaths.SetupGet(paths => paths.DataPath).Returns(dataDirectory);
            applicationPaths.SetupGet(paths => paths.UserConfigurationDirectoryPath).Returns(Path.Combine(dataDirectory, "users"));

            var serializer = new Mock<IXmlSerializer>();
            serializer
                .Setup(xml => xml.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            _ = new GetAvatarPlugin(applicationPaths.Object, serializer.Object, NullLogger<GetAvatarPlugin>.Instance);

            var serverConfigurationManager = new Mock<IServerConfigurationManager>();
            serverConfigurationManager.SetupGet(manager => manager.ApplicationPaths).Returns(applicationPaths.Object);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(AvatarController).Assembly);
            builder.Services.AddLogging();
            builder.Services.AddSingleton(userManager.Object);
            builder.Services.AddSingleton(Mock.Of<INetworkManager>());
            builder.Services.AddSingleton(Mock.Of<IDeviceManager>());
            builder.Services.AddSingleton<IProviderManager>(Mock.Of<IProviderManager>());
            builder.Services.AddSingleton<MediaBrowser.Common.Configuration.IApplicationPaths>(applicationPaths.Object);
            builder.Services.AddSingleton<IServerConfigurationManager>(serverConfigurationManager.Object);
            builder.Services.AddSingleton<IHttpClientFactory>(Mock.Of<IHttpClientFactory>());
            builder.Services.AddSingleton<AvatarService>();
            builder.Services.AddSingleton<OnlinePackService>();
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestScheme;
                    options.DefaultChallengeScheme = TestScheme;
                    options.DefaultForbidScheme = TestScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, AdministratorAuthenticationHandler>(TestScheme, _ => { });
            builder.Services.AddAuthorization();

            await using var application = builder.Build();
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapControllers();
            await application.StartAsync(TestContext.Current.CancellationToken);

            using var client = application.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");

            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (path == "/GetAvatar/RemoveUserAvatar")
            {
                request.Content = new StringContent("{\"userId\":\"invalid-guid\"}", Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(expectedStatusCode, (int)response.StatusCode);
        }
        finally
        {
            typeof(GetAvatarPlugin).GetProperty(nameof(GetAvatarPlugin.Instance))!.SetValue(null, previousPlugin);
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    private sealed class AdministratorAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AdministratorAuthenticationHandler(
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
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
