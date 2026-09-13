using System.Threading.Tasks;
using MulletaFlix.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("request-123", "request-123")]
    [InlineData("abc.DEF_09", "abc.DEF_09")]
    [InlineData("", null)]
    [InlineData("bad value", null)]
    [InlineData("bad/value", null)]
    public void Normalize_AcceptsOnlyBoundedHeaderValues(string candidate, string? expected)
    {
        var normalized = CorrelationIdMiddleware.Normalize(candidate);

        if (expected is null)
        {
            Assert.Matches("^[a-f0-9]{32}$", normalized);
        }
        else
        {
            Assert.Equal(expected, normalized);
        }
    }

    [Fact]
    public async Task Invoke_UsesIncomingIdAndAddsResponseHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "client-request-42";
        var middleware = new CorrelationIdMiddleware(next: _ => Task.CompletedTask);

        await middleware.Invoke(context);
        await context.Response.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("client-request-42", context.TraceIdentifier);
        Assert.Equal("client-request-42", context.Response.Headers["X-Correlation-ID"].ToString());
    }
}
