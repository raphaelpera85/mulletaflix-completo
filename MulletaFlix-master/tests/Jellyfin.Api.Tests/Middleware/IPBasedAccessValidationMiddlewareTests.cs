using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Api.Middleware;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

public sealed class IPBasedAccessValidationMiddlewareTests
{
    [Fact]
    public async Task Invoke_RedactsLiveRecordingCapabilityFromBlockedRequestLog()
    {
        const string capability = "capability-secret";
        var context = new DefaultHttpContext();
        context.Request.Path = $"/LiveTv/LiveRecordings/{capability}/stream";
        context.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");
        var networkManager = new Mock<INetworkManager>();
        networkManager
            .Setup(manager => manager.ShouldAllowServerAccess(It.IsAny<IPAddress>()))
            .Returns(RemoteAccessPolicyResult.RejectDueToRemoteAccessDisabled);
        var logger = new CapturingLogger<IPBasedAccessValidationMiddleware>();
        var middleware = new IPBasedAccessValidationMiddleware(_ => throw new InvalidOperationException("Blocked requests must not continue."), logger);

        await middleware.Invoke(context, networkManager.Object);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        var log = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("REDACTED", log, StringComparison.Ordinal);
        Assert.DoesNotContain(capability, log, StringComparison.Ordinal);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            where TState : notnull
            => Messages.Add(formatter(state, exception));
    }
}
