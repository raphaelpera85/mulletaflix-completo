using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Api.Middleware;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

public sealed class ExceptionMiddlewareTests
{
    [Fact]
    public async Task Invoke_IncludesCorrelationIdInRequestFailureLog()
    {
        const string correlationId = "request-42";
        var context = new DefaultHttpContext();
        context.TraceIdentifier = correlationId;
        context.Request.QueryString = new QueryString("?api_key=do-not-log");
        var logger = new CapturingLogger<ExceptionMiddleware>();
        var configuration = new Mock<IServerConfigurationManager>();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Production);
        var middleware = new ExceptionMiddleware(
            _ => throw new IOException("simulated stream failure"),
            logger,
            configuration.Object,
            environment.Object);

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains(logger.Messages, message => message.Contains(correlationId, StringComparison.Ordinal));
        Assert.DoesNotContain("do-not-log", string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
