using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Middleware;

public class RateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _failedLogins = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _anonymousRequests = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);
    private const int MaxFailedLogins = 10;

    private static readonly TimeSpan AnonymousWindow = TimeSpan.FromSeconds(10);
    private const int MaxAnonymousRequests = 30;

    private static readonly string[] StaticWebPaths =
    [
        "/assets",
        "/apps",
        "/controllers",
        "/libraries",
        "/themes",
        "/branding",
        "/web/assets",
        "/web/apps",
        "/web/controllers",
        "/web/libraries",
        "/web/themes",
        "/web/branding",
        "/index.html",
        "/web/index.html",
        "/config.json",
        "/web/config.json",
        "/manifest.json",
        "/web/manifest.json",
        "/serviceworker.js",
        "/web/serviceworker.js",
        "/robots.txt",
        "/web/robots.txt",
        "/sitemap.xml",
        "/web/sitemap.xml"
    ];

    /// <summary>
    /// Maximum number of distinct IP entries retained per dictionary to prevent
    /// unbounded memory growth from IP rotation attacks.
    /// </summary>
    private const int MaxDistinctIps = 10000;

    /// <summary>
    /// Minimum interval between cleanup sweeps after the configured IP cap is reached.
    /// </summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private static DateTime _lastCleanup = DateTime.MinValue;

    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(ip))
        {
            await _next(context);
            return;
        }

        var isAuth = context.User?.Identity?.IsAuthenticated ?? false;
        var path = context.Request.Path.Value;
        var isStaticWebAsset = path is not null && IsStaticWebAssetPath(path);
        var isLoginAttempt = path is not null && (
            IsPathOrDescendant(path, "/Users/Authenticate")
            || IsPathOrDescendant(path, "/Users/Register"));

        if (isLoginAttempt)
        {
            if (IsBlocked(_failedLogins, ip, LoginWindow, MaxFailedLogins))
            {
                _logger.LogWarning("Rate limit exceeded for login from IP {IP}", ip);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                return;
            }
        }
        else if (!isAuth && !isStaticWebAsset)
        {
            if (IsBlocked(_anonymousRequests, ip, AnonymousWindow, MaxAnonymousRequests))
            {
                _logger.LogWarning("Rate limit exceeded for anonymous requests from IP {IP}", ip);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                return;
            }
        }

        await _next(context);

        if (isLoginAttempt && context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
        {
            RecordAttempt(_failedLogins, ip, LoginWindow);
        }
        else if (!isAuth && !isLoginAttempt && !isStaticWebAsset)
        {
            RecordAttempt(_anonymousRequests, ip, AnonymousWindow);
        }
    }

    internal static bool IsPathOrDescendant(string path, string route)
        => string.Equals(path, route, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(route + "/", StringComparison.OrdinalIgnoreCase);

    internal static bool IsStaticWebAssetPath(string path)
        => StaticWebPaths.Any(route => IsPathOrDescendant(path, route));

    private static bool IsBlocked(ConcurrentDictionary<string, RateLimitEntry> store, string key, TimeSpan window, int max)
    {
        var now = DateTime.UtcNow;
        if (store.TryGetValue(key, out var entry))
        {
            return entry.IsBlocked(now, window, max);
        }

        return false;
    }

    private static void RecordAttempt(ConcurrentDictionary<string, RateLimitEntry> store, string key, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var entry = store.GetOrAdd(key, _ => new RateLimitEntry());
        entry.Record(now, window);

        EvictStaleEntries(store, now);
    }

    private static void EvictStaleEntries(ConcurrentDictionary<string, RateLimitEntry> store, DateTime now)
    {
        if (store.Count <= MaxDistinctIps)
        {
            return;
        }

        if ((now - _lastCleanup) < CleanupInterval && store.Count <= MaxDistinctIps * 2)
        {
            return;
        }

        _lastCleanup = now;

        var keysToRemove = store
            .Select(kvp =>
            {
                return (Key: kvp.Key, Oldest: kvp.Value.GetOldestTimestamp());
            })
            .OrderBy(entry => entry.Oldest)
            .Take(Math.Max(1, store.Count - MaxDistinctIps))
            .Select(entry => entry.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            store.TryRemove(key, out _);
        }
    }

    private class RateLimitEntry
    {
        private readonly object _sync = new();
        public List<DateTime> Timestamps { get; } = new();

        public bool IsBlocked(DateTime now, TimeSpan window, int max)
        {
            lock (_sync)
            {
                Prune(now, window);
                return Timestamps.Count >= max;
            }
        }

        public void Record(DateTime now, TimeSpan window)
        {
            lock (_sync)
            {
                Prune(now, window);
                Timestamps.Add(now);
            }
        }

        public DateTime GetOldestTimestamp()
        {
            lock (_sync)
            {
                return Timestamps.Count == 0 ? DateTime.MinValue : Timestamps.Min();
            }
        }

        private void Prune(DateTime now, TimeSpan window)
        {
            var cutoff = now - window;
            Timestamps.RemoveAll(t => t < cutoff);
        }
    }
}
