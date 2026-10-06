using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
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

public sealed class UserLicenseControllerAuthorizationTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid AnotherUserId = Guid.NewGuid();

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task SetUserLicense_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UserLicenseController).Assembly);
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
                Policies.LocalAccessOrRequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireAuthenticatedUser());
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

        var request = new { DurationHours = 24, AdminNotes = "Test license" };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/Users/{TestUserId}/License", content, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("User", 403)]
    public async Task RevokeUserLicense_EnforcesElevationThroughHttpPipeline(string? role, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UserLicenseController).Assembly);
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
                Policies.LocalAccessOrRequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireAuthenticatedUser());
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

        using var response = await client.DeleteAsync($"/Users/{TestUserId}/License", TestContext.Current.CancellationToken);

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
