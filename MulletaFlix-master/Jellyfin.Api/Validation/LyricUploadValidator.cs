using System;
using System.Linq;

namespace MulletaFlix.Api.Validation;

/// <summary>
/// Validates lyric files uploaded through <c>LyricsController</c> before they are
/// handed to <see cref="MediaBrowser.Controller.Lyrics.ILyricManager"/>. This complements the
/// existing 1 MB size limit (see <c>LyricsController.MaxLyricFileSize</c>) with content-type /
/// magic-byte sniffing and a check for obviously malicious embedded payloads, per the Medium
/// finding "File Upload Validation in Lyrics Endpoint" in SECURITY_AUDIT_REPORT.md.
/// </summary>
internal static class LyricUploadValidator
{
    /// <summary>
    /// The lyric file extensions actually understood by the registered <c>ILyricParser</c>
    /// implementations (<c>LrcLyricParser</c>, <c>TxtLyricParser</c>). Anything else is rejected
    /// up front instead of being written to disk and silently failing to parse later.
    /// </summary>
    internal static readonly string[] AllowedFormats = ["lrc", "elrc", "txt"];

    /// <summary>
    /// Case-insensitive substrings that have no legitimate reason to appear in a lyric file and
    /// are strong indicators of an attempt to smuggle a script/markup payload through the
    /// upload endpoint (e.g. for later XSS if the lyric is ever rendered unescaped, or for
    /// tricking a downstream tool that opens the "lyric" file).
    /// </summary>
    private static readonly string[] MaliciousPatterns =
    [
        "<script",
        "</script",
        "javascript:",
        "vbscript:",
        "<?php",
        "<%",
        "onerror=",
        "onload=",
        "<iframe",
        "<object",
        "<embed"
    ];

    /// <summary>
    /// Binary file signatures ("magic bytes") that must never appear in a lyric upload, since
    /// every supported lyric format is plain text.
    /// </summary>
    private static readonly byte[][] BinarySignatures =
    [
        [0x4D, 0x5A], // MZ - Windows PE/EXE/DLL
        [0x7F, 0x45, 0x4C, 0x46], // ELF
        [0x50, 0x4B, 0x03, 0x04], // ZIP / PK (also docx, jar, apk, etc.)
        [0x89, 0x50, 0x4E, 0x47], // PNG
        [0xFF, 0xD8, 0xFF], // JPEG
        [0x47, 0x49, 0x46, 0x38], // GIF8
        [0x25, 0x50, 0x44, 0x46], // %PDF
        [0x1F, 0x8B], // GZIP
    ];

    /// <summary>
    /// Returns whether <paramref name="format"/> (a file extension without the leading dot, as
    /// produced by <c>Path.GetExtension(...).RightPart('.')</c>) is one of the formats actually
    /// understood by the server's lyric parsers.
    /// </summary>
    /// <param name="format">The extension/format token to validate.</param>
    /// <returns><see langword="true"/> if the format is recognized.</returns>
    internal static bool IsAllowedFormat(string? format)
        => !string.IsNullOrEmpty(format) && AllowedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether <paramref name="content"/> matches a known binary file signature or
    /// contains a NUL byte, either of which is impossible in a genuine lyric text file.
    /// </summary>
    /// <param name="content">The raw uploaded bytes.</param>
    /// <returns><see langword="true"/> if the content looks like a binary file rather than text.</returns>
    internal static bool LooksLikeBinaryContent(ReadOnlySpan<byte> content)
    {
        foreach (var signature in BinarySignatures)
        {
            if (content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature))
            {
                return true;
            }
        }

        // A NUL byte never legitimately appears in UTF-8/ASCII lyric text; its presence is a
        // strong binary-content signal regardless of which magic-byte table above is current.
        foreach (var b in content)
        {
            if (b == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether <paramref name="text"/> contains one of the known malicious patterns
    /// (embedded script/markup tags, code fences for server-side languages, inline event
    /// handlers, etc.).
    /// </summary>
    /// <param name="text">The decoded lyric text.</param>
    /// <returns><see langword="true"/> if a malicious pattern was found.</returns>
    internal static bool ContainsMaliciousPattern(string text)
    {
        foreach (var pattern in MaliciousPatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Runs every lyric-upload check and returns a human readable rejection reason, or
    /// <see langword="null"/> when the upload is acceptable.
    /// </summary>
    /// <param name="format">The requested lyric format/extension token.</param>
    /// <param name="content">The raw uploaded bytes.</param>
    /// <returns>A rejection reason, or <see langword="null"/> if the upload passes validation.</returns>
    internal static string? Validate(string? format, ReadOnlySpan<byte> content)
    {
        if (!IsAllowedFormat(format))
        {
            return $"Unsupported lyric format '{format}'. Allowed formats: {string.Join(", ", AllowedFormats)}.";
        }

        if (LooksLikeBinaryContent(content))
        {
            return "Lyric file content does not appear to be valid text.";
        }

        var text = System.Text.Encoding.UTF8.GetString(content);
        if (ContainsMaliciousPattern(text))
        {
            return "Lyric file contains disallowed content.";
        }

        return null;
    }
}
