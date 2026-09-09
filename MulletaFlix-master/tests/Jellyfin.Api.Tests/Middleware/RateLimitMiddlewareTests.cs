using System.Net;
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
    [InlineData("/media", "/media", true)]
    [InlineData("/media/", "/media", true)]
    [InlineData("/media/items", "/media", true)]
    [InlineData("/mediaBackup", "/media", false)]
    public void BaseUrl_IsPathUnderBaseUrl_RequiresPrefixBoundary(string path, string prefix, bool expected)
    {
        Assert.Equal(expected, BaseUrlRedirectionMiddleware.IsPathUnderBaseUrl(path, prefix));
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
}
