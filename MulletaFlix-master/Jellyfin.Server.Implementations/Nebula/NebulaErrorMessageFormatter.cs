using System;
using System.Linq;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Produces concise operational error text while the structured logger retains the full exception.
/// </summary>
internal static class NebulaErrorMessageFormatter
{
    internal const int MaximumMessageLength = 512;

    internal static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var message = Normalize(exception.Message);
        if (message.Length > MaximumMessageLength)
        {
            var rootMessage = Normalize(exception.GetBaseException().Message);
            if (rootMessage.Length > 0)
            {
                message = rootMessage;
            }
        }

        if (message.Length <= MaximumMessageLength)
        {
            return message;
        }

        return string.Concat(message.AsSpan(0, MaximumMessageLength - 1), "…");
    }

    private static string Normalize(string? message)
    {
        return string.Join(' ', (message ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
