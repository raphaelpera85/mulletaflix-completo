using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Middleware;

/// <summary>
/// Response time middleware.
/// </summary>
public class ResponseTimeMiddleware
{
    private const string ResponseHeaderResponseTime = "X-Response-Time-ms";

    /// <summary>
    /// How long one path stays quiet after it logged a slow response.
    /// </summary>
    /// <remarks>
    /// A single two hour HLS playback issues roughly 1200 segment requests. Without suppression the
    /// warning would bury the log as soon as a transcode is slow, which is why the original code
    /// was written defensively — and ended up never emitting at all.
    /// </remarks>
    private static readonly TimeSpan WarningSuppressionWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound on tracked paths before the map is pruned.
    /// </summary>
    /// <remarks>
    /// An unbounded per-path cache is the same resident-memory defect this audit keeps finding
    /// elsewhere, so the map is bounded by construction rather than by hope.
    /// </remarks>
    private const int MaxTrackedPaths = 512;

    private readonly ConcurrentDictionary<string, long> _lastWarningTimestamps = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseTimeMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseTimeMiddleware"/> class.
    /// </summary>
    /// <param name="next">Next request delegate.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ExceptionMiddleware}"/> interface.</param>
    public ResponseTimeMiddleware(
        RequestDelegate next,
        ILogger<ResponseTimeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invoke request.
    /// </summary>
    /// <param name="context">Request context.</param>
    /// <param name="serverConfigurationManager">Instance of the <see cref="IServerConfigurationManager"/> interface.</param>
    /// <returns>Task.</returns>
    public async Task Invoke(HttpContext context, IServerConfigurationManager serverConfigurationManager)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var enableWarning = serverConfigurationManager.Configuration.EnableSlowResponseWarning;
        var warningThreshold = serverConfigurationManager.Configuration.SlowResponseThresholdMs;
        context.Response.OnStarting(() =>
        {
            var responseTime = Stopwatch.GetElapsedTime(startTimestamp);
            var responseTimeMs = responseTime.TotalMilliseconds;
            if (enableWarning && responseTimeMs > warningThreshold)
            {
                LogSlowResponse(context, responseTime);
            }

            context.Response.Headers[ResponseHeaderResponseTime] = responseTimeMs.ToString(CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });

        // Call the next delegate/middleware in the pipeline
        await this._next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Logs one slow response, suppressing repeats from the same path inside the window.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="responseTime">The measured response time.</param>
    private void LogSlowResponse(HttpContext context, TimeSpan responseTime)
    {
        // Logged at Information deliberately. This used to be gated behind IsEnabled(LogLevel.Debug),
        // so an install configured with EnableSlowResponseWarning=true and a 500 ms threshold
        // produced no output whatsoever under the production log level and the setting looked like
        // it was working. The suppression below is what keeps that fix from flooding the log.
        var path = context.Request.Path.Value ?? "/";
        var now = Stopwatch.GetTimestamp();

        if (_lastWarningTimestamps.TryGetValue(path, out var previous)
            && Stopwatch.GetElapsedTime(previous, now) < WarningSuppressionWindow)
        {
            return;
        }

        // Read-then-write rather than atomic: two concurrent slow requests on the same path may both
        // log once. For a warning line that is an acceptable trade for not taking a lock per request.
        _lastWarningTimestamps[path] = now;

        if (_lastWarningTimestamps.Count > MaxTrackedPaths)
        {
            PruneWarningTimestamps(now);
        }

        _logger.LogInformation(
            "Slow HTTP Response from {Url} to {RemoteIP} in {Elapsed:g} with Status Code {StatusCode}",
            context.Request.GetDisplayUrl(),
            context.GetNormalizedRemoteIP(),
            responseTime,
            context.Response.StatusCode);
    }

    /// <summary>
    /// Drops paths whose suppression window has already elapsed.
    /// </summary>
    /// <param name="now">The current timestamp.</param>
    private void PruneWarningTimestamps(long now)
    {
        foreach (var entry in _lastWarningTimestamps)
        {
            if (Stopwatch.GetElapsedTime(entry.Value, now) >= WarningSuppressionWindow)
            {
                _lastWarningTimestamps.TryRemove(entry.Key, out _);
            }
        }
    }
}
