using System.Text.RegularExpressions;

namespace MediaBrowser.Providers.Helpers;

internal static class RemoteUrlLogRedactor
{
    private static readonly Regex RemoteUrlRegex = new(
        @"https?://[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    public static string Redact(string value)
        => RemoteUrlRegex.Replace(value, "[REDACTED_URL]");
}
