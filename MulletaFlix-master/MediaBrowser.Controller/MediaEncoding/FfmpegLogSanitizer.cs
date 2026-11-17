using System;
using System.Text.RegularExpressions;

namespace MediaBrowser.Controller.MediaEncoding;

public static partial class FfmpegLogSanitizer
{
    [GeneratedRegex("(?i)(https?|rtsp|rtmp|ftps?|sftp)://[^\\s\\\"'<>@]+@")]
    private static partial Regex UrlCredentialsRegex();

    [GeneratedRegex("(?i)(?<url>(?:https?|rtsp|rtmp|ftps?|sftp)://[^\\s\\\"'<>?]+)\\?[^\\s\\\"'<>]*")]
    private static partial Regex UrlQueryRegex();

    [GeneratedRegex("(?i)(?<prefix>\\\"(?:ApiKey|X-Api-Key|OpenToken|Token|AccessToken|RefreshToken|Authorization|Password|Secret|Cookie)\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")")]
    private static partial Regex SensitiveJsonValueRegex();

    [GeneratedRegex("(?i)(?<prefix>\\b(?:Authorization|X-Emby-Token|X-Api-Key|ApiKey|Access-Token|Cookie|Set-Cookie)\\s*:\\s*(?:Bearer\\s+)?)[^\\s,;\\\"']+")]
    private static partial Regex SensitiveHeaderRegex();

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        value = UrlCredentialsRegex().Replace(value, "$1://[REDACTED]@");
        value = UrlQueryRegex().Replace(value, "${url}?[REDACTED]");
        value = SensitiveJsonValueRegex().Replace(value, "${prefix}[REDACTED]${suffix}");
        return SensitiveHeaderRegex().Replace(value, "${prefix}[REDACTED]");
    }
}
