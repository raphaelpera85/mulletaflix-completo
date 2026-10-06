using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ActionLogControllerAuthorizationTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;

    /// <summary>
    /// Verifies that unauthenticated requests to ActionLog endpoints return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetEntries_WithoutAuthentication_Returns401Unauthorized()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ActionLogController).Assembly);
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
        // No role header - unauthenticated

        using var response = await client.GetAsync("/ActionLog/Entries", TestContext.Current.CancellationToken);

        Assert.Equal(401, (int)response.StatusCode);
    }

    /// <summary>
    /// Verifies that non-admin users are forbidden from accessing ActionLog endpoints.
    /// </summary>
    [Fact]
    public async Task GetEntries_WithUserRole_Returns403Forbidden()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ActionLogController).Assembly);
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
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        using var response = await client.GetAsync("/ActionLog/Entries", TestContext.Current.CancellationToken);

        Assert.Equal(403, (int)response.StatusCode);
    }

    /// <summary>
    /// Verifies that GetStats endpoint (which returns dashboard statistics) also requires elevation.
    /// </summary>
    [Fact]
    public async Task GetStats_WithoutAuthentication_Returns401Unauthorized()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ActionLogController).Assembly);
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
        // No role header - unauthenticated

        using var response = await client.GetAsync("/ActionLog/Stats", TestContext.Current.CancellationToken);

        Assert.Equal(401, (int)response.StatusCode);
    }

    /// <summary>
    /// Verifies that GetStats endpoint forbids non-admin users.
    /// </summary>
    [Fact]
    public async Task GetStats_WithUserRole_Returns403Forbidden()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ActionLogController).Assembly);
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
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        using var response = await client.GetAsync("/ActionLog/Stats", TestContext.Current.CancellationToken);

        Assert.Equal(403, (int)response.StatusCode);
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
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
