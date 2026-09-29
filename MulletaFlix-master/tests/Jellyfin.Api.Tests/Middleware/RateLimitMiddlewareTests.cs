using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

public sealed class RateLimitMiddlewareTests
{
    [Theory]
    [InlineData("/Users/Authenticate", true)]
    [InlineData("/Users/Authenticate/", true)]
    [InlineData("/Users/Register/reset", true)]
    [InlineData("/Users/AuthenticateFake", false)]
    [InlineData("/Users/Register2", false)]
    public void IsPathOrDescendant_RequiresRouteBoundary(string path, bool expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.IsPathOrDescendant(path, "/Users/Authenticate")
            || RateLimitMiddleware.IsPathOrDescendant(path, "/Users/Register"));
    }

    [Theory]
    [InlineData("/assets/index.js", true)]
    [InlineData("/web/assets/index.js", true)]
    [InlineData("/serviceworker.js", true)]
    [InlineData("/config.json", true)]
    [InlineData("/Users/Authenticate", false)]
    [InlineData("/web/ConfigurationPages", false)]
    public void IsStaticWebAssetPath_DoesNotIncludeApiRoutes(string path, bool expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.IsStaticWebAssetPath(path));
    }

    [Theory]
    [InlineData("/System/Info/Public", true)]
    [InlineData("/System/Info/Public/", true)]
    [InlineData("/Users/Public", true)]
    [InlineData("/System/Info/Publicity", false)]
    [InlineData("/System/Configuration", false)]
    public void PublicBootstrapPath_RequiresRouteBoundary(string path, bool expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.IsPublicBootstrapPath(path));
    }

    [Theory]
    [InlineData("/media", "/media", true)]
    [InlineData("/media/", "/media", true)]
    [InlineData("/media/items", "/media", true)]
    [InlineData("/mediaBackup", "/media", false)]
    public void BaseUrl_IsPathUnderBaseUrl_RequiresPrefixBoundary(string path, string prefix, bool expected)
    {
        Assert.Equal(expected, BaseUrlRedirectionMiddleware.IsPathUnderBaseUrl(path, prefix));
    }

    [Theory]
    [InlineData("/Search/Hints", "search")]
    [InlineData("/Items/RemoteSearch", "search")]
    [InlineData("/NebulaFtp/Download", "nebula")]
    [InlineData("/Backup/Create", "administration")]
    [InlineData("/System/Logs", "administration")]
    [InlineData("/Systematic/Logs", null)]
    public void SelectiveRateLimitCategory_RequiresRouteBoundary(string path, string? expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.GetSelectiveRateLimitCategory(path));
    }

    [Fact]
    public async Task AuthenticatedNebulaRequests_AreLimitedSeparatelyFromAnonymousTraffic()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity("test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.77");
            context.Request.Path = "/NebulaFtp/Download";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var blocked = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("test"))
        };
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.77");
        blocked.Request.Path = "/NebulaFtp/Download";
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task AnonymousRequests_AreRecordedAndBlockedAfterLimit()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 30; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.17");
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.17");
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_BlockedResponse_IncludesRetryAfterHeader()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 30; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
            await middleware.Invoke(context);
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
        var retryAfter = int.Parse(blocked.Response.Headers.RetryAfter.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        // The anonymous window is 10 seconds; the header must be a positive value no larger
        // than the full window (it reports the remaining wait, never the whole window itself
        // unless every request landed in the same instant).
        Assert.InRange(retryAfter, 1, 10);
    }

    [Fact]
    public async Task LoginAttempts_BlockedResponse_IncludesRetryAfterHeader()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.21");
            context.Request.Path = "/Users/Authenticate";
            await middleware.Invoke(context);
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.21");
        blocked.Request.Path = "/Users/Authenticate";
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
        var retryAfter = int.Parse(blocked.Response.Headers.RetryAfter.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        // The login window is 15 minutes (900s); the header must be positive and never exceed it.
        Assert.InRange(retryAfter, 1, 900);
    }

    [Fact]
    public async Task AnonymousStaticAssets_AreNotCountedAgainstRequestLimit()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 40; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.18");
            context.Request.Path = "/assets/index.js";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task AnonymousPublicBootstrapRequests_AreNotCountedAgainstRequestLimit()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 40; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.19");
            context.Request.Path = "/System/Info/Public";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task AnonymousLoopbackRequests_AreNotRateLimited()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 40; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Path = "/Users/test-id/Views";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }
}
