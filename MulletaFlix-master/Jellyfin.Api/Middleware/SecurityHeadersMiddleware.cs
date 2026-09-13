using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace MulletaFlix.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
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

        await _next(context);
    }
}

