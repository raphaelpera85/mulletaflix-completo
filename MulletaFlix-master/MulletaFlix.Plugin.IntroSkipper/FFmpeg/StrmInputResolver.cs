// SPDX-License-Identifier: GPL-3.0-only

namespace IntroSkipper.FFmpeg;

/// <summary>
/// Resolves Jellyfin STRM shortcut files to the media URI consumed by FFmpeg.
/// </summary>
internal static class StrmInputResolver
{
    private static readonly string[] SupportedSchemes =
    [
        Uri.UriSchemeFile,
        Uri.UriSchemeFtp,
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        "rtmp",
        "rtmps",
        "rtsp",
    ];

    /// <summary>
    /// Returns a regular media path unchanged, or reads the absolute media URI from a STRM file.
    /// </summary>
    /// <param name="mediaPath">Filesystem media path.</param>
    /// <returns>The FFmpeg input path.</returns>
    /// <exception cref="InvalidDataException">The STRM file does not contain a supported absolute media URI.</exception>
    public static string Resolve(string mediaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);

        if (!mediaPath.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            return mediaPath;
        }

        var target = File.ReadAllText(mediaPath).Trim();
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && SupportedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return uri.AbsoluteUri;
        }

        throw new InvalidDataException($"STRM file '{mediaPath}' does not contain a supported absolute media URI.");
    }

    /// <summary>
    /// Resolves every input path following an FFmpeg <c>-i</c> argument.
    /// </summary>
    /// <param name="arguments">FFmpeg arguments.</param>
    /// <returns>Arguments with STRM inputs replaced by their media URI.</returns>
    public static string[] ResolveArguments(IReadOnlyList<string> arguments)
    {
        var resolved = arguments.ToArray();
        for (var index = 0; index < resolved.Length - 1; index++)
        {
            if (string.Equals(resolved[index], "-i", StringComparison.Ordinal)
                && resolved[index + 1].EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                resolved[index + 1] = Resolve(resolved[index + 1]);
            }
        }

        return resolved;
    }
}
