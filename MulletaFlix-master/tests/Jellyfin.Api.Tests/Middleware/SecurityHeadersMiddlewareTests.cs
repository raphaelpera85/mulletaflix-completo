using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

/// <summary>
/// Covers H-12: security headers must not be attached to streaming/image responses, since a
/// two-hour HLS playback issues roughly 1200 segment requests and every one of them used to carry
/// the full ~900 byte CSP header for no security benefit (a media segment is never rendered as a
/// document). Documents (HTML/JSON/etc.) must keep receiving the headers unchanged.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData("video/mp4", true)]
    [InlineData("video/MP4", true)]
    [InlineData("audio/mpeg", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("application/x-mpegURL", true)]
    [InlineData("application/vnd.apple.mpegurl", true)]
    [InlineData("application/dash+xml", true)]
    [InlineData("text/html", false)]
    [InlineData("application/json", false)]
    [InlineData("application/javascript", false)]
    [InlineData(null, false)]
    public void IsStreamingOrImageContentType_ClassifiesCorrectly(string? contentType, bool expected)
    {
        Assert.Equal(expected, SecurityHeadersMiddleware.IsStreamingOrImageContentType(contentType));
    }

    [Fact]
    public async Task StreamingResponse_DoesNotReceiveSecurityHeaders()
    {
        var context = CreateContext();
        var middleware = new SecurityHeadersMiddleware(async ctx =>
        {
            ctx.Response.ContentType = "video/mp4";
            await ctx.Response.StartAsync();
        });

        await middleware.Invoke(context);

        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.False(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
    }

    [Fact]
    public async Task DocumentResponse_ReceivesSecurityHeaders()
    {
        var context = CreateContext();
        var middleware = new SecurityHeadersMiddleware(async ctx =>
        {
            ctx.Response.ContentType = "text/html";
            await ctx.Response.StartAsync();
        });

        await middleware.Invoke(context);

        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"]);
    }

    [Fact]
    public async Task ResponseWithoutContentType_ReceivesSecurityHeaders()
    {
        var context = CreateContext();
        var middleware = new SecurityHeadersMiddleware(async ctx =>
        {
            await ctx.Response.StartAsync();
        });

        await middleware.Invoke(context);

        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    /// <summary>
    /// Builds a context whose response feature actually runs registered OnStarting callbacks.
    /// <see cref="DefaultHttpContext"/> alone never does — that is the real server's job — so
    /// without this the middleware under test would look like it never sets any header.
    /// </summary>
    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        var responseFeature = new CallbackRunningResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Features.Set<IHttpResponseBodyFeature>(responseFeature);
        return context;
    }

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
