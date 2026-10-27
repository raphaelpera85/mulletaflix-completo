using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntroSkipper.Controllers;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MulletaFlix.Api.Auth.LocalAccessOrRequiresElevationPolicy;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Plugin.GetAvatar.Controllers;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline.Api;
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
            "POST /Backup/RestorePointInTime",
            "GET /System/ActivityLog/Entries",
            "GET /PlaybackReports/Entries",
            "GET /PlaybackReports/Stats",
            "GET /PlaybackReports/Export",
            "POST /Items/00000000000000000000000000000001/Images/Primary",
            "POST /Items/00000000000000000000000000000001/Images/Primary/0",
            "DELETE /Items/00000000000000000000000000000001/Images/Primary",
            "DELETE /Items/00000000000000000000000000000001/Images/Primary/0",
            "POST /Items/00000000000000000000000000000001/Images/Primary/0/Index?newIndex=1",
            "POST /Branding/Splashscreen",
            "DELETE /Branding/Splashscreen",
            "POST /MidiaStorageOnline/config",
            "GET /MidiaStorageOnline/status",
            "POST /MidiaStorageOnline/sync"
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
    public async Task EveryRequiresElevationRoute_RejectsAnonymousAndRegularUsersThroughHttpPipeline()
    {
        await using var application = await CreateApplicationAsync();
        using var anonymousClient = application.GetTestClient();
        using var userClient = application.GetTestClient();
        userClient.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var routes = application.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(attribute => attribute.Policy == Policies.RequiresElevation))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => (Method: method, Path: CreateRequestPath(endpoint.RoutePattern.RawText!))))
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.Contains(routes, route => route.Method == "POST"
            && route.Path.Contains("Intros/EraseTimestamps", StringComparison.Ordinal));
        foreach (var (method, path) in routes)
        {
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
    public async Task EveryDiscoveredMvcRouteDeclaresAuthorizationIntent()
    {
        await using var application = await CreateApplicationAsync();

        var routesWithoutAuthorizationIntent = application.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.True(
            routesWithoutAuthorizationIntent.Length == 0,
            $"MVC routes must declare [Authorize] or [AllowAnonymous]: {string.Join(", ", routesWithoutAuthorizationIntent)}");
    }

    [Fact]
    public async Task TimeSyncRouteAllowsAnonymousRequests()
    {
        await using var application = await CreateApplicationAsync();
        using var client = application.GetTestClient();

        using var response = await client.GetAsync("/GetUtcTime", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EveryLocalAccessOrElevationRoute_RejectsRemoteAnonymousAndRegularUsers()
    {
        await using var application = await CreateRemoteAccessApplicationAsync();
        using var anonymousClient = application.GetTestClient();
        using var userClient = application.GetTestClient();
        userClient.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var routes = application.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(attribute => attribute.Policy == Policies.LocalAccessOrRequiresElevation))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => (Method: method, Path: CreateRequestPath(endpoint.RoutePattern.RawText!))))
            .ToArray();

        Assert.NotEmpty(routes);
        foreach (var (method, path) in routes)
        {
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
    public async Task MidiaStorageAdminRoutes_RejectRegularUsersEvenFromLocalNetwork()
    {
        (string Method, string Path)[] routes =
        [
            ("POST", "/MidiaStorageOnline/config"),
            ("GET", "/MidiaStorageOnline/status"),
            ("POST", "/MidiaStorageOnline/sync")
        ];

        await using var application = await CreateRemoteAccessApplicationAsync(isLocalNetwork: true);
        using var anonymousClient = application.GetTestClient();
        using var userClient = application.GetTestClient();
        userClient.DefaultRequestHeaders.Add("X-Test-Role", "User");

        foreach (var (method, path) in routes)
        {
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

    private static string CreateRequestPath(string routeTemplate)
        => Regex.Replace(routeTemplate, "\\{([^}]+)\\}", match =>
        {
            var parameter = match.Groups[1].Value;
            var parts = parameter.TrimStart('*').TrimEnd('?').Split(':', 2);
            var value = parts.Length > 1 && parts[1].StartsWith("guid", System.StringComparison.OrdinalIgnoreCase)
                ? "00000000-0000-0000-0000-000000000001"
                : parts.Length > 1 && (parts[1].StartsWith("int", System.StringComparison.OrdinalIgnoreCase)
                    || parts[1].StartsWith("long", System.StringComparison.OrdinalIgnoreCase))
                    ? "1"
                    : parts[0].Contains("imageType", System.StringComparison.OrdinalIgnoreCase)
                        ? "Primary"
                        : "test";
            return Uri.EscapeDataString(value);
        });

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
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(NebulaFtpController).Assembly)
            .AddApplicationPart(typeof(AvatarController).Assembly)
            .AddApplicationPart(typeof(SkipIntroController).Assembly)
            .AddApplicationPart(typeof(MidiaStorageOnlineController).Assembly);
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
            // The test exercises elevation, so combined policies must not admit loopback users.
            options.AddPolicy(
                Policies.LocalAccessOrRequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }

    private static async Task<WebApplication> CreateRemoteAccessApplicationAsync(bool isLocalNetwork = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(NebulaFtpController).Assembly)
            .AddApplicationPart(typeof(AvatarController).Assembly)
            .AddApplicationPart(typeof(SkipIntroController).Assembly)
            .AddApplicationPart(typeof(MidiaStorageOnlineController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleHeaderAuthenticationHandler>(TestScheme, _ => { });
        var networkManager = new Mock<INetworkManager>();
        networkManager.Setup(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>())).Returns(isLocalNetwork);
        builder.Services.AddSingleton(networkManager.Object);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuthorizationHandler, LocalAccessOrRequiresElevationHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.LocalAccessOrRequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .AddRequirements(new LocalAccessOrRequiresElevationRequirement()));
            options.AddPolicy(
                Policies.RequiresElevation,
                policy => policy.AddAuthenticationSchemes(TestScheme)
                    .RequireClaim(ClaimTypes.Role, UserRoles.Administrator));
        });

        var application = builder.Build();
        application.UseRouting();
        application.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(isLocalNetwork ? "192.168.1.10" : "203.0.113.10");
            await next();
        });
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
