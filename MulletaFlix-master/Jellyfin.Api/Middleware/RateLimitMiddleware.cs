using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Middleware;

public class RateLimitMiddleware
{
    public const string MeterName = "MulletaFlix.Server.HttpAdmission";
    private static readonly Meter AdmissionMeter = new(MeterName);
    private static readonly Counter<long> RejectedRequestCounter = AdmissionMeter.CreateCounter<long>(
        "mulletaflix.http.admission.rejected",
        unit: "{request}",
        description: "HTTP requests rejected by rate limits or heavy-operation admission.");

    private static readonly ConcurrentDictionary<string, RateLimitEntry> _failedLogins = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _anonymousRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _searchRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _administrativeRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _nebulaRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim _heavyBackupOperations = new(1, 1);
    private static readonly SemaphoreSlim _catalogScans = new(1, 1);

    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);
    private const int MaxFailedLogins = 10;

    private static readonly TimeSpan AnonymousWindow = TimeSpan.FromSeconds(10);
    private const int MaxAnonymousRequests = 30;
    private static readonly TimeSpan SearchWindow = TimeSpan.FromSeconds(10);
    private const int MaxSearchRequests = 60;
    private static readonly TimeSpan AdministrativeWindow = TimeSpan.FromSeconds(10);
    private const int MaxAdministrativeRequests = 20;
    private static readonly TimeSpan NebulaWindow = TimeSpan.FromSeconds(10);
    private const int MaxNebulaRequests = 10;

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

    private static readonly string[] PublicBootstrapPaths =
    [
        "/System/Info/Public",
        "/QuickConnect/Enabled",
        "/Users/Public",
        "/Branding/Configuration"
    ];

    private static readonly string[] AdministrativePaths =
    [
        "/ActivityLog",
        "/Backup",
        "/Configuration",
        "/Dashboard",
        "/Environment",
        "/Plugins",
        "/ScheduledTasks",
        "/ServerHealth",
        "/System"
    ];

    /// <summary>
    /// Maximum number of distinct client keys retained per dictionary to prevent
    /// unbounded memory growth from rotating users or IP addresses.
    /// </summary>
    private const int MaxDistinctClients = 10000;

    /// <summary>
    /// Minimum interval between cleanup sweeps after the configured client-key cap is reached.
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
        var heavyOperationLimiter = GetHeavyOperationLimiter(context.Request.Method, context.Request.Path.Value);
        if (heavyOperationLimiter is not null && !heavyOperationLimiter.Wait(0))
        {
            RecordRejection("heavy_operation");
            _logger.LogWarning("Concurrent heavy operation rejected for path {Path}", context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.RetryAfter = "1";
            return;
        }

        try
        {
            await InvokeCore(context);
        }
        finally
        {
            heavyOperationLimiter?.Release();
        }
    }

    private async Task InvokeCore(HttpContext context)
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
        var isPublicBootstrap = path is not null && IsPublicBootstrapPath(path);
        var isLoginAttempt = path is not null && (
            IsPathOrDescendant(path, "/Users/Authenticate")
            || IsPathOrDescendant(path, "/Users/Register"));
        var selectiveCategory = path is not null ? GetSelectiveRateLimitCategory(path) : null;
        RateLimitEntry? loginEntry = null;

        if (isLoginAttempt)
        {
            loginEntry = _failedLogins.GetOrAdd(ip, _ => new RateLimitEntry());
            if (!loginEntry.TryEnterLogin(DateTime.UtcNow, LoginWindow, MaxFailedLogins, out var retryAfterLogin))
            {
                RecordRejection("login");
                _logger.LogWarning("Rate limit exceeded for login from IP {IP}", ip);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers.RetryAfter = retryAfterLogin.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;
            }
        }
        else if (selectiveCategory is not null && !IsLoopback(ip))
        {
            var selectiveStore = selectiveCategory switch
            {
                "search" => _searchRequests,
                "administration" => _administrativeRequests,
                "nebula" => _nebulaRequests,
                _ => throw new InvalidOperationException($"Unknown rate-limit category: {selectiveCategory}")
            };
            var selectiveWindow = selectiveCategory switch
            {
                "search" => SearchWindow,
                "administration" => AdministrativeWindow,
                "nebula" => NebulaWindow,
                _ => throw new InvalidOperationException($"Unknown rate-limit category: {selectiveCategory}")
            };
            var selectiveMax = selectiveCategory switch
            {
                "search" => MaxSearchRequests,
                "administration" => MaxAdministrativeRequests,
                "nebula" => MaxNebulaRequests,
                _ => throw new InvalidOperationException($"Unknown rate-limit category: {selectiveCategory}")
            };

            var clientKey = GetSelectiveClientKey(context, ip);
            if (!TryRecordAttempt(selectiveStore, clientKey, selectiveWindow, selectiveMax, out var retryAfterSelective))
            {
                RecordRejection(selectiveCategory);
                _logger.LogWarning("Rate limit exceeded for {Category} requests from IP {IP}", selectiveCategory, ip);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers.RetryAfter = retryAfterSelective.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;
            }
        }
        else if (!isAuth && !IsLoopback(ip) && !isStaticWebAsset && !isPublicBootstrap && !IsHermeticTestMode())
        {
            if (!TryRecordAttempt(_anonymousRequests, ip, AnonymousWindow, MaxAnonymousRequests, out var retryAfterAnonymous))
            {
                RecordRejection("anonymous");
                _logger.LogWarning("Rate limit exceeded for anonymous requests from IP {IP}", ip);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers.RetryAfter = retryAfterAnonymous.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;
            }
        }

        try
        {
            await _next(context);
        }
        finally
        {
            if (loginEntry is not null)
            {
                loginEntry.CompleteLogin(
                    DateTime.UtcNow,
                    LoginWindow,
                    context.Response.StatusCode == (int)HttpStatusCode.Unauthorized);
                EvictStaleEntries(_failedLogins, DateTime.UtcNow);
            }
        }
    }

    private static void RecordRejection(string category)
    {
        RejectedRequestCounter.Add(1, new KeyValuePair<string, object?>("category", category));
    }

    private static SemaphoreSlim? GetHeavyOperationLimiter(string method, string? path)
    {
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) || path is null)
        {
            return null;
        }

        var normalizedPath = path.TrimEnd('/');
        if (string.Equals(normalizedPath, "/Backup/Create", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/NebulaFtp/Supabase/Restore", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/NebulaFtp/Supabase/Users/Backup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "/NebulaFtp/Supabase/Users/Restore", StringComparison.OrdinalIgnoreCase))
        {
            return _heavyBackupOperations;
        }

        return string.Equals(normalizedPath, "/NebulaFtp/Actions/ScanNovelas", StringComparison.OrdinalIgnoreCase)
            ? _catalogScans
            : null;
    }

    private static string GetSelectiveClientKey(HttpContext context, string ip)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(context.User.FindFirst(InternalClaimTypes.UserId)?.Value, out var userId)
                && userId != Guid.Empty)
            {
                return "user:" + userId.ToString("N", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (bool.TryParse(context.User.FindFirst(InternalClaimTypes.IsApiKey)?.Value, out var isApiKey)
                && isApiKey
                && context.User.FindFirst(InternalClaimTypes.Token)?.Value is { Length: > 0 } token)
            {
                return "api-key:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            }
        }

        return "ip:" + ip;
    }

    internal static string? GetSelectiveRateLimitCategory(string path)
    {
        if (IsPublicBootstrapPath(path))
        {
            return null;
        }

        if (IsPathOrDescendant(path, "/Search")
            || IsPathOrDescendant(path, "/Items/RemoteSearch"))
        {
            return "search";
        }

        if (IsPathOrDescendant(path, "/NebulaFtp"))
        {
            return "nebula";
        }

        foreach (var route in AdministrativePaths)
        {
            if (IsPathOrDescendant(path, route))
            {
                return "administration";
            }
        }

        return null;
    }

    internal static bool IsPathOrDescendant(string path, string route)
    {
        var pathSpan = path.AsSpan();
        var routeSpan = route.AsSpan();

        if (pathSpan.Equals(routeSpan, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Avoids the `route + "/"` string allocation on every call: compare the route prefix and
        // the boundary separator as two spans instead of concatenating a throwaway string first.
        return pathSpan.Length > routeSpan.Length
            && pathSpan[routeSpan.Length] == '/'
            && pathSpan[..routeSpan.Length].Equals(routeSpan, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsStaticWebAssetPath(string path)
    {
        // A `foreach` avoids the per-request closure allocation that `Array.Any(route => ...)`
        // would create by capturing `path`.
        foreach (var route in StaticWebPaths)
        {
            if (IsPathOrDescendant(path, route))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsPublicBootstrapPath(string path)
    {
        foreach (var route in PublicBootstrapPaths)
        {
            if (IsPathOrDescendant(path, route))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsLoopback(string ip)
        => IPAddress.TryParse(ip, out var address) && IPAddress.IsLoopback(address);

    private static bool IsHermeticTestMode()
    {
        var value = Environment.GetEnvironmentVariable("MFLX_E2E_TEST_MODE");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryRecordAttempt(
        ConcurrentDictionary<string, RateLimitEntry> store,
        string key,
        TimeSpan window,
        int max,
        out int retryAfterSeconds)
    {
        var now = DateTime.UtcNow;
        var entry = store.GetOrAdd(key, _ => new RateLimitEntry());
        var admitted = entry.TryRecord(now, window, max, out retryAfterSeconds);
        if (admitted)
        {
            EvictStaleEntries(store, now);
        }

        return admitted;
    }

    private static void EvictStaleEntries(ConcurrentDictionary<string, RateLimitEntry> store, DateTime now)
    {
        if (store.Count <= MaxDistinctClients)
        {
            return;
        }

        if ((now - _lastCleanup) < CleanupInterval && store.Count <= MaxDistinctClients * 2)
        {
            return;
        }

        _lastCleanup = now;

        var keysToRemove = store
            .Select(kvp =>
            {
                return (Key: kvp.Key, Oldest: kvp.Value.GetEvictableTimestamp());
            })
            .Where(entry => entry.Oldest.HasValue)
            .OrderBy(entry => entry.Oldest)
            .Take(Math.Max(1, store.Count - MaxDistinctClients))
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
        private int _activeLogins;
        public List<DateTime> Timestamps { get; } = new();

        public bool TryEnterLogin(DateTime now, TimeSpan window, int max, out int retryAfterSeconds)
        {
            lock (_sync)
            {
                Prune(now, window);
                if (Timestamps.Count >= max)
                {
                    var oldest = Timestamps.Min();
                    var remaining = (oldest + window) - now;
                    retryAfterSeconds = remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : 1;
                    return false;
                }

                if (Timestamps.Count + _activeLogins >= max)
                {
                    retryAfterSeconds = 1;
                    return false;
                }

                _activeLogins++;
                retryAfterSeconds = 0;
                return true;
            }
        }

        public void CompleteLogin(DateTime now, TimeSpan window, bool failed)
        {
            lock (_sync)
            {
                _activeLogins--;
                if (failed)
                {
                    Prune(now, window);
                    Timestamps.Add(now);
                }
            }
        }

        public bool TryRecord(DateTime now, TimeSpan window, int max, out int retryAfterSeconds)
        {
            lock (_sync)
            {
                Prune(now, window);
                if (Timestamps.Count >= max)
                {
                    var remaining = (Timestamps.Min() + window) - now;
                    retryAfterSeconds = remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : 1;
                    return false;
                }

                Timestamps.Add(now);
                retryAfterSeconds = 0;
                return true;
            }
        }

        public DateTime? GetEvictableTimestamp()
        {
            lock (_sync)
            {
                if (_activeLogins > 0)
                {
                    return null;
                }

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
