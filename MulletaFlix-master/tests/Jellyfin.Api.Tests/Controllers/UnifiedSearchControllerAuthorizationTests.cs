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

public sealed class UnifiedSearchControllerAuthorizationTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid AnotherUserId = Guid.NewGuid();

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task GetUnifiedSearch_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UnifiedSearchController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

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
            client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId.ToString("D"));
        }

        using var response = await client.GetAsync($"/Search/Unified?searchTerm=test", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task GetSearchStats_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UnifiedSearchController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

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
            client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId.ToString("D"));
        }

        using var response = await client.GetAsync($"/Search/Stats?userId={TestUserId}", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Fact]
    public async Task GetUnifiedSearch_WithOtherUserIdAsNonAdmin_ShouldThrowSecurityException()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UnifiedSearchController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        // User role attempting to search for another user's content
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");
        client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId.ToString("D"));

        // Attempting to access another user's search results as a non-admin should fail
        using var response = await client.GetAsync($"/Search/Unified?searchTerm=test&userId={AnotherUserId}", TestContext.Current.CancellationToken);

        // Since controller requires elevation, non-admin users should get 403
        // The RequestHelpers.GetUserId() call will throw SecurityException on cross-user access
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest,
                    $"Expected 403 or 400, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetUnifiedSearch_WithAdminRole_CanAccessOtherUserSearch()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UnifiedSearchController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        // Administrator role
        client.DefaultRequestHeaders.Add("X-Test-Role", UserRoles.Administrator);
        client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId.ToString("D"));

        // Admin accessing another user's search
        using var response = await client.GetAsync($"/Search/Unified?searchTerm=test&userId={AnotherUserId}", TestContext.Current.CancellationToken);

        // Admin should be allowed by the policy (will fail on actual item lookup, but auth should pass)
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK ||
                    response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest,
                    $"Expected 200, 404, or 400, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetSearchStats_WithOtherUserIdAsNonAdmin_ShouldThrowSecurityException()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UnifiedSearchController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        // User role attempting to get stats for another user
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");
        client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId.ToString("D"));

        using var response = await client.GetAsync($"/Search/Stats?userId={AnotherUserId}", TestContext.Current.CancellationToken);

        // Since controller requires elevation, non-admin users should get 403
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest,
                    $"Expected 403 or 400, got {response.StatusCode}");
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

            // Extract user ID if provided
            if (Request.Headers.TryGetValue("X-Test-UserId", out var userId))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
