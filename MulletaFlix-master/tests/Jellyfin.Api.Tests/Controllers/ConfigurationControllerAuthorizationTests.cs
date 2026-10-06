using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

/// <summary>
/// Tests for ConfigurationController authorization policies via HTTP pipeline.
/// Ensures all configuration endpoints require elevation (admin-only access).
/// Tests both lack of authentication (401) and insufficient role (403).
/// </summary>
public sealed class ConfigurationControllerAuthorizationTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task GetConfiguration_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ConfigurationController).Assembly);
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
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        using var response = await client.GetAsync("/System/Configuration", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task UpdateConfiguration_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ConfigurationController).Assembly);
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
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        var config = new { MetadataCountryCode = "US", PreferredMetadataLanguage = "en" };
        using var content = new StringContent(JsonSerializer.Serialize(config), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/System/Configuration", content, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task GetNamedConfiguration_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ConfigurationController).Assembly);
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
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        using var response = await client.GetAsync("/System/Configuration/test", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task UpdateNamedConfiguration_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ConfigurationController).Assembly);
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
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/System/Configuration/test", content, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task GetDefaultMetadataOptions_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ConfigurationController).Assembly);
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
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        using var response = await client.GetAsync("/System/Configuration/MetadataOptions/Default", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task UpdateBrandingConfiguration_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ConfigurationController).Assembly);
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
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        var branding = new { LoginDisclaimer = "", CustomCss = "", DefaultTheme = "" };
        using var content = new StringContent(JsonSerializer.Serialize(branding), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/System/Configuration/Branding", content, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
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
