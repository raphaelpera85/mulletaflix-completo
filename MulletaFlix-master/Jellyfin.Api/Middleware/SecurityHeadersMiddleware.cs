using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace MulletaFlix.Api.Middleware;

/// <summary>
/// Adds security-related response headers.
/// </summary>
/// <remarks>
/// These headers only matter for documents the browser renders or scripts (HTML, JSON, JS, CSS).
/// A media segment or an image response is never interpreted as a document, so setting ~900 bytes
/// of headers (CSP string included) on every one of the ~1200 HLS segment/image requests in a
/// two-hour playback is pure overhead with no security benefit. The check runs from
/// <see cref="HttpContext.Response.OnStarting"/> because the response's final Content-Type is only
/// known once the endpoint has set it, which happens after this middleware calls into <c>next</c>.
/// </remarks>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(static state => SetSecurityHeaders((HttpContext)state), context);

        await _next(context);
    }

    private static Task SetSecurityHeaders(HttpContext context)
    {
        if (IsStreamingOrImageContentType(context.Response.ContentType))
        {
            return Task.CompletedTask;
        }

        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["X-Application-Name"] = "Jellyfin";
        headers["Content-Security-Policy"] =
            "default-src 'self'; "
            + "script-src 'self' 'unsafe-inline' 'unsafe-eval' http://*.gstatic.com https://*.gstatic.com http://www.gstatic.com https://www.gstatic.com chrome-extension:; "
            + "script-src-elem 'self' 'unsafe-inline' 'unsafe-eval' http://*.gstatic.com https://*.gstatic.com http://www.gstatic.com https://www.gstatic.com chrome-extension:; "
            + "style-src 'self' 'unsafe-inline'; "
            + "img-src 'self' data: blob: https: http: chrome-extension:; "
            + "media-src 'self' data: blob: https: http:; "
            + "font-src 'self' data:; "
            + "connect-src 'self' blob: ws: wss: http: https: chrome-extension:; "
            + "frame-src 'self' http: https: chrome-extension:;";

        return Task.CompletedTask;
    }

    /// <summary>
    /// Detects the media/binary content types used by HLS/DASH segments, playlists and images —
    /// none of which a browser will ever interpret as an active document, so the CSP/frame/referrer
    /// headers add nothing there.
    /// </summary>
    /// <param name="contentType">The response's <c>Content-Type</c> header value.</param>
    /// <returns><see langword="true"/> if the security headers should be skipped.</returns>
    internal static bool IsStreamingOrImageContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var span = contentType.AsSpan();

        return span.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("application/x-mpegurl", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("application/vnd.apple.mpegurl", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("application/dash+xml", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("application/vnd.ms-sstr+xml", StringComparison.OrdinalIgnoreCase);
    }
}

