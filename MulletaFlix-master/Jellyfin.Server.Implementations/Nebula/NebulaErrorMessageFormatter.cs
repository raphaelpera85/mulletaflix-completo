using System;

namespace Jellyfin.Server.Implementations.Nebula;

internal static class NebulaErrorMessageFormatter
{
    internal const int MaxDisplayedErrorLength = 512;

    internal static string FormatForDisplay(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var message = exception.Message;
        if (message.Length > MaxDisplayedErrorLength)
        {
            var rootCauseMessage = exception.GetBaseException().Message;
            if (!string.IsNullOrWhiteSpace(rootCauseMessage) && rootCauseMessage.Length < message.Length)
            {
                message = rootCauseMessage;
            }
        }

        message = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Falha sem mensagem detalhada; consulte o log do servidor.";
        }

        if (message.Length > MaxDisplayedErrorLength)
        {
            const string suffix = "… (detalhes completos no log do servidor)";
            message = message[..(MaxDisplayedErrorLength - suffix.Length)].TrimEnd() + suffix;
        }

        return message;
    }
}
