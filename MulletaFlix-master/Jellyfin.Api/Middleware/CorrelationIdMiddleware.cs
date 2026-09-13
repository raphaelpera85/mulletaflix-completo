using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MulletaFlix.Api.Middleware;

/// <summary>
/// Adds a bounded correlation identifier to every HTTP request and response.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    internal const string HeaderName = "X-Correlation-ID";
    private const int MaxCorrelationIdLength = 64;

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">Next request delegate.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Adds or preserves a safe correlation identifier for the current request.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Invoke(HttpContext context)
    {
        var correlationId = Normalize(context.Request.Headers[HeaderName].ToString());
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context).ConfigureAwait(false);
    }

    internal static string Normalize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxCorrelationIdLength)
        {
            return Guid.NewGuid().ToString("N");
        }

        foreach (var character in candidate)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        return candidate;
    }
}
