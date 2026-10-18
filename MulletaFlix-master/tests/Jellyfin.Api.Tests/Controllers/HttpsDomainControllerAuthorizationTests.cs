using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.HttpsDomain;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.HttpsDomain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class HttpsDomainControllerAuthorizationTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;
    private const string SentinelToken = "super-secret-duckdns-token-xyz789";

    /// <summary>
    /// Case 1: GET 'System/Https/Status' without credentials returns 401.
    /// </summary>
    [Fact]
    public async Task GetStatus_WithoutCredentials_ReturnsUnauthorized()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Default);
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Default);
        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        // No X-Test-Role header = no authentication

        using var response = await client.GetAsync("/System/Https/Status", TestContext.Current.CancellationToken);

        Assert.Equal(401, (int)response.StatusCode);
    }

    /// <summary>
    /// Case 2: GET 'System/Https/Status' with role 'User' (non-admin) returns 403.
    /// </summary>
    [Fact]
    public async Task GetStatus_WithUserRoleNonAdmin_ReturnsForbidden()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Default);
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Default);
        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        using var response = await client.GetAsync("/System/Https/Status", TestContext.Current.CancellationToken);

        Assert.Equal(403, (int)response.StatusCode);
    }

    /// <summary>
    /// Case 3: GET 'System/Https/Status' as administrator returns 200 and response body
    /// does NOT contain the sentinel token; contains 'maskedToken'.
    /// </summary>
    [Fact]
    public async Task GetStatus_AsAdministrator_Returns200WithNoSentinelToken()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>();
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>();

        // Setup mock to return configuration with sentinel token
        var config = new HttpsDomainConfiguration
        {
            DuckDnsSubdomain = "test",
            AcmeEmail = "admin@test.com",
            DuckDnsToken = SentinelToken
        };
        configManager.Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(config);

        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", UserRoles.Administrator);

        using var response = await client.GetAsync("/System/Https/Status", TestContext.Current.CancellationToken);

        Assert.Equal(200, (int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // Verify sentinel token is NOT in response
        Assert.DoesNotContain(SentinelToken, content, StringComparison.Ordinal);
        // Verify maskedToken is present (camelCase in JSON output)
        Assert.Contains("maskedtoken", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Case 4: POST 'System/Https/Configure' without credentials returns 401 AND
    /// SaveConfiguration is never called (verified with Moq).
    /// </summary>
    [Fact]
    public async Task Configure_WithoutCredentials_ReturnsUnauthorizedAndNeverSavesConfiguration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Default);
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Default);
        configManager.Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        // No X-Test-Role header = no authentication

        var requestBody = new
        {
            DuckDnsSubdomain = "test",
            AcmeEmail = "admin@test.com",
            DuckDnsToken = "some-token"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/System/Https/Configure", content, TestContext.Current.CancellationToken);

        Assert.Equal(401, (int)response.StatusCode);
        // Verify SaveConfiguration was never called
        configManager.Verify(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    /// <summary>
    /// Case 5: POST 'System/Https/Configure' with role 'User' returns 403 AND
    /// SaveConfiguration is never called (verified with Moq).
    /// </summary>
    [Fact]
    public async Task Configure_WithUserRoleNonAdmin_ReturnsForbiddenAndNeverSavesConfiguration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Default);
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Default);
        configManager.Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var requestBody = new
        {
            DuckDnsSubdomain = "test",
            AcmeEmail = "admin@test.com",
            DuckDnsToken = "some-token"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/System/Https/Configure", content, TestContext.Current.CancellationToken);

        Assert.Equal(403, (int)response.StatusCode);
        // Verify SaveConfiguration was never called
        configManager.Verify(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    /// <summary>
    /// Case 6: POST 'System/Https/Configure' as administrator with valid body returns 200
    /// and the HTTP response body (raw string) does NOT contain the sentinel token sent in the request.
    /// </summary>
    [Fact]
    public async Task Configure_AsAdministratorWithValidBody_Returns200AndDoesNotExposeTokenInResponse()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Default);
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Default);

        HttpsDomainConfiguration? savedConfig = null;
        configManager.Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        configManager.Setup(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, cfg) => savedConfig = cfg as HttpsDomainConfiguration);

        configManager.Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(() => savedConfig ?? new HttpsDomainConfiguration());

        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", UserRoles.Administrator);

        var requestBody = new
        {
            DuckDnsSubdomain = "mulletaflix",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = SentinelToken
        };
        var httpContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/System/Https/Configure", httpContent, TestContext.Current.CancellationToken);

        Assert.Equal(200, (int)response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Verify sentinel token is NOT in response body
        Assert.DoesNotContain(SentinelToken, responseBody, StringComparison.Ordinal);
        // Verify maskedToken is present (camelCase in JSON output)
        Assert.Contains("maskedToken", responseBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Case 7: POST 'System/Https/Configure' as administrator with invalid subdomain returns 400
    /// and the error response body does NOT contain the sentinel token sent in the request.
    /// This tests that tokens don't leak even in error responses.
    /// </summary>
    [Fact]
    public async Task Configure_AsAdministratorWithInvalidSubdomain_Returns400AndDoesNotExposeTokenInErrorResponse()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpsDomainController).Assembly);
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

        // Mock dependencies
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Default);
        var configManager = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Default);
        configManager.Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        builder.Services.AddSingleton(certificateInspector.Object);
        builder.Services.AddSingleton(configManager.Object);

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", UserRoles.Administrator);

        // Invalid subdomain: contains underscore (not allowed)
        var requestBody = new
        {
            DuckDnsSubdomain = "invalid_subdomain",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = SentinelToken
        };
        var httpContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/System/Https/Configure", httpContent, TestContext.Current.CancellationToken);

        Assert.Equal(400, (int)response.StatusCode);
        var errorResponseBody = await response.Content.ReadAsStringAsync();

        // Verify sentinel token is NOT in error response body
        Assert.DoesNotContain(SentinelToken, errorResponseBody, StringComparison.Ordinal);
        // Verify we got an error message (should be about subdomain, not exposing secrets)
        Assert.Contains("error", errorResponseBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Private authentication handler for testing role-based authorization via headers.
    /// Mirrors the pattern from PackageControllerAuthorizationTests.
    /// </summary>
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
