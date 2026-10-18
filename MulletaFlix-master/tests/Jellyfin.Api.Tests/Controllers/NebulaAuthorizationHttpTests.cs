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
using MulletaFlix.Plugin.GetAvatar.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class NebulaAuthorizationHttpTests
{
    private const string TestScheme = AuthenticationSchemes.CustomAuthentication;

    [Theory]
    [InlineData(null, 401, "GET", "/NebulaFtp/Status")]
    [InlineData("User", 403, "GET", "/NebulaFtp/Status")]
    [InlineData(null, 401, "POST", "/NebulaFtp/Config/Secrets")]
    [InlineData("User", 403, "POST", "/NebulaFtp/Config/Secrets")]
    [InlineData(null, 401, "POST", "/NebulaFtp/Actions/StartDownloader")]
    [InlineData("User", 403, "POST", "/NebulaFtp/Actions/StartDownloader")]
    [InlineData(null, 401, "POST", "/NebulaFtp/Supabase/Restore")]
    [InlineData("User", 403, "POST", "/NebulaFtp/Supabase/Restore")]
    public async Task NebulaRoutes_EnforceElevationThroughHttpPipeline(
        string? role,
        int expectedStatusCode,
        string method,
        string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(NebulaFtpController).Assembly);
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

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(null, 401, "POST", "/GetAvatar/Upload")]
    [InlineData("User", 403, "POST", "/GetAvatar/Upload")]
    [InlineData(null, 401, "DELETE", "/GetAvatar/Delete/test-avatar")]
    [InlineData("User", 403, "DELETE", "/GetAvatar/Delete/test-avatar")]
    [InlineData(null, 401, "POST", "/GetAvatar/RemoveUserAvatar")]
    [InlineData("User", 403, "POST", "/GetAvatar/RemoveUserAvatar")]
    [InlineData(null, 401, "POST", "/GetAvatar/ValidateAvatars")]
    [InlineData("User", 403, "POST", "/GetAvatar/ValidateAvatars")]
    [InlineData(null, 401, "POST", "/GetAvatar/CleanupOrphans")]
    [InlineData("User", 403, "POST", "/GetAvatar/CleanupOrphans")]
    [InlineData(null, 401, "GET", "/GetAvatar/AvatarStatus")]
    [InlineData("User", 403, "GET", "/GetAvatar/AvatarStatus")]
    public async Task AvatarAdminRoutes_RejectAnonymousAndNonAdminThroughHttpPipeline(
        string? role,
        int expectedStatusCode,
        string method,
        string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AvatarController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization();

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

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

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
