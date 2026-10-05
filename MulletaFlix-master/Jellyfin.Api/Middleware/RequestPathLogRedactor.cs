using System;

namespace MulletaFlix.Api.Middleware;

/// <summary>
/// Removes bearer capabilities from request paths before the paths are written to logs.
/// </summary>
internal static class RequestPathLogRedactor
{
    private const string RedactedValue = "[REDACTED]";

    /// <summary>
    /// Replaces the opaque ID in anonymous live-recording stream routes with a fixed marker.
    /// </summary>
    /// <param name="path">The request path without its query string.</param>
    /// <returns>A log-safe path.</returns>
    public static string RedactSensitiveSegments(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path ?? string.Empty;
        }

        var segments = path.Split('/');
        for (var index = 0; index + 3 < segments.Length; index++)
        {
            if (string.Equals(segments[index], "LiveTv", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[index + 1], "LiveRecordings", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(segments[index + 2])
                && string.Equals(segments[index + 3], "stream", StringComparison.OrdinalIgnoreCase))
            {
                segments[index + 2] = RedactedValue;
                return string.Join('/', segments);
            }
        }

        return path;
    }
}
