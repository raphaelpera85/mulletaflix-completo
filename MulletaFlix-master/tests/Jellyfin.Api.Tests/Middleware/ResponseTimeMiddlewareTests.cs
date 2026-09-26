using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using MulletaFlix.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

/// <summary>
/// Covers the slow response warning.
/// </summary>
/// <remarks>
/// These tests exist because the warning was unreachable in production: the emit was gated behind
/// <c>_logger.IsEnabled(LogLevel.Debug)</c>, so an install with
/// <c>EnableSlowResponseWarning=true</c> and a 500 ms threshold logged nothing at all under the
/// production log level. The first test pins the behaviour that was broken — Debug disabled, and the
/// warning must still be emitted.
/// </remarks>
public sealed class ResponseTimeMiddlewareTests
{
    private const string SlowPath = "/Videos/abc/stream";

    [Fact]
    public async Task SlowResponse_IsLoggedAtInformationEvenWhenDebugIsDisabled()
    {
        var logger = CreateProductionLogger();
        var sut = CreateMiddleware(logger, TimeSpan.FromMilliseconds(5));

        await InvokeAsync(sut, SlowPath);

        VerifyLogged(logger, Times.Once());
    }

    [Fact]
    public async Task RepeatedSlowResponsesOnTheSamePath_AreSuppressedWithinTheWindow()
    {
        var logger = CreateProductionLogger();
        var sut = CreateMiddleware(logger, TimeSpan.FromMilliseconds(5));

        // A two hour HLS playback is roughly 1200 requests to the same segment path.
        await InvokeAsync(sut, SlowPath);
        await InvokeAsync(sut, SlowPath);
        await InvokeAsync(sut, SlowPath);

        VerifyLogged(logger, Times.Once());
    }

    [Fact]
    public async Task SlowResponsesOnDifferentPaths_AreEachLogged()
    {
        var logger = CreateProductionLogger();
        var sut = CreateMiddleware(logger, TimeSpan.FromMilliseconds(5));

        await InvokeAsync(sut, "/Videos/abc/stream");
        await InvokeAsync(sut, "/Videos/abc/master.m3u8");

        VerifyLogged(logger, Times.Exactly(2));
    }

    [Fact]
    public async Task WhenTheWarningIsDisabled_NothingIsLogged()
    {
        var logger = CreateProductionLogger();
        var sut = CreateMiddleware(logger, TimeSpan.FromMilliseconds(5), new ServerConfiguration
        {
            EnableSlowResponseWarning = false,
            SlowResponseThresholdMs = 1
        });

        await InvokeAsync(sut, SlowPath);

        VerifyLogged(logger, Times.Never());
    }

    [Fact]
    public async Task FastResponse_IsNotLogged()
    {
        var logger = CreateProductionLogger();
        var sut = CreateMiddleware(logger, TimeSpan.Zero, new ServerConfiguration
        {
            EnableSlowResponseWarning = true,
            SlowResponseThresholdMs = 60_000
        });

        await InvokeAsync(sut, SlowPath);

        VerifyLogged(logger, Times.Never());
    }

    [Fact]
    public async Task ResponseTimeHeader_IsSetOnFastAndSlowResponses()
    {
        var fast = new DefaultHttpContext();
        await InvokeAsync(CreateMiddleware(CreateProductionLogger(), TimeSpan.Zero), SlowPath, fast);
        Assert.True(fast.Response.Headers.ContainsKey("X-Response-Time-ms"));

        var slow = new DefaultHttpContext();
        await InvokeAsync(CreateMiddleware(CreateProductionLogger(), TimeSpan.FromMilliseconds(5)), SlowPath, slow);
        Assert.True(slow.Response.Headers.ContainsKey("X-Response-Time-ms"));
    }

    /// <summary>
    /// Builds a logger that reports the production log level, where Debug is off.
    /// </summary>
    /// <returns>The mocked logger.</returns>
    private static Mock<ILogger<ResponseTimeMiddleware>> CreateProductionLogger()
    {
        var logger = new Mock<ILogger<ResponseTimeMiddleware>>();
        logger
            .Setup(l => l.IsEnabled(It.IsAny<LogLevel>()))
            .Returns((LogLevel level) => level >= LogLevel.Information);
        return logger;
    }

    private static MiddlewareUnderTest CreateMiddleware(
        Mock<ILogger<ResponseTimeMiddleware>> logger,
        TimeSpan handlerDelay,
        ServerConfiguration? configuration = null)
    {
        configuration ??= new ServerConfiguration
        {
            EnableSlowResponseWarning = true,
            SlowResponseThresholdMs = 1
        };

        var manager = new Mock<IServerConfigurationManager>();
        manager.SetupGet(m => m.Configuration).Returns(configuration);

        var middleware = new ResponseTimeMiddleware(
            async context =>
            {
                if (handlerDelay > TimeSpan.Zero)
                {
                    await Task.Delay(handlerDelay).ConfigureAwait(false);
                }

                // OnStarting callbacks only run once the response actually starts.
                await context.Response.StartAsync().ConfigureAwait(false);
            },
            logger.Object);

        return new MiddlewareUnderTest(middleware, manager.Object);
    }

    private static async Task InvokeAsync(
        MiddlewareUnderTest sut,
        string path,
        HttpContext? context = null)
    {
        context ??= new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8096);
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.30");

        // DefaultHttpContext never runs its OnStarting callbacks — invoking them is the real
        // server's job — so the middleware under test would look like it never logs. Installing a
        // feature that does run them is what makes this test observe the production path.
        var responseFeature = new CallbackRunningResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Features.Set<IHttpResponseBodyFeature>(responseFeature);

        await sut.Middleware.Invoke(context, sut.Configuration).ConfigureAwait(false);
    }

    private static void VerifyLogged(Mock<ILogger<ResponseTimeMiddleware>> logger, Times times)
    {
        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times);
    }

    private sealed record MiddlewareUnderTest(
        ResponseTimeMiddleware Middleware,
        IServerConfigurationManager Configuration);

    /// <summary>
    /// A response feature pair that actually invokes the registered OnStarting callbacks.
    /// </summary>
    private sealed class CallbackRunningResponseFeature : IHttpResponseFeature, IHttpResponseBodyFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = new();

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted { get; private set; }

        public Stream Stream => Body;

        public PipeWriter Writer => PipeWriter.Create(Body);

        public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
            // The middleware under test never registers completion callbacks.
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => RunOnStartingAsync();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CompleteAsync() => Task.CompletedTask;

        public void DisableBuffering()
        {
        }

        private async Task RunOnStartingAsync()
        {
            if (HasStarted)
            {
                return;
            }

            HasStarted = true;
            foreach (var (callback, state) in _onStarting)
            {
                await callback(state).ConfigureAwait(false);
            }
        }
    }
}
