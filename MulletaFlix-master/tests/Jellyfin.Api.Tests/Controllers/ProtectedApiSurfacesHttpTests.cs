using System.Collections.Generic;
using System.Net;
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

public sealed class ProtectedApiSurfacesHttpTests
{
    private const string TestScheme = MulletaFlix.Api.Constants.AuthenticationSchemes.CustomAuthentication;

    [Fact]
    public async Task AdminRoutes_RejectAnonymousAndRegularUsersThroughHttpPipeline()
    {
        string[] routes =
        [
            "POST /NebulaFtp/Uploads/1/Cancel",
            "POST /NebulaFtp/FailedUploads/1/Retry",
            "POST /NebulaFtp/PlaybackCache/Clear",
            "POST /NebulaFtp/PlaybackCache/Path",
            "POST /NebulaFtp/Supabase/Backup",
            "POST /NebulaFtp/Supabase/Users/Backup",
            "POST /NebulaFtp/Supabase/Users/Restore",
            "GET /NebulaFtp/Supabase/SqlScript",
            "GET /UserFeedback/MediaRequestCatalog",
            "GET /Backup",
            "GET /Backup/Manifest?path=backup.zip",
            "GET /Backup/History",
            "GET /Backup/Validate?path=backup.zip",
            "POST /Backup/Create",
            "POST /Backup/Restore",
            "POST /Backup/RestorePointInTime"
        ];

        await using var application = await CreateApplicationAsync();
        using var anonymousClient = application.GetTestClient();
        using var userClient = application.GetTestClient();
        userClient.DefaultRequestHeaders.Add("X-Test-Role", "User");

        foreach (var route in routes)
        {
            var separator = route.IndexOf(' ', System.StringComparison.Ordinal);
            var method = route[..separator];
            var path = route[(separator + 1)..];

            using var anonymousResponse = await anonymousClient.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), path),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            using var userResponse = await userClient.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), path),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);
        }
    }

    [Fact]
    public async Task UserFeedbackRoutes_RejectAnonymousRequestsThroughHttpPipeline()
    {
        (string Method, string Path)[] routes =
        [
            ("GET", "/UserFeedback/MediaSuggestions?query=ab"),
            ("GET", "/UserFeedback/MediaSuggestions/Status"),
            ("GET", "/UserFeedback/MediaRequests"),
            ("POST", "/UserFeedback/MediaRequests"),
            ("POST", "/UserFeedback/PlaybackIssues")
        ];

        await using var application = await CreateApplicationAsync();
        using var client = application.GetTestClient();

        foreach (var (method, path) in routes)
        {
            using var response = await client.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), path),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static async Task<WebApplication> CreateApplicationAsync()
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

        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
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
