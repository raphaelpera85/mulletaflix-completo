// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using IntroSkipper.FFmpeg;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class FFmpegProcessRunnerTests
{
    [Fact]
    public async Task RunWithDiagnosticsAsync_CapturesHttp404WhenFingerprintOutputIsEmpty()
    {
        var (processPath, arguments) = CreateFailingProcess();
        var runner = new FFmpegProcessRunner(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var result = await runner.RunWithDiagnosticsAsync(
            processPath,
            arguments,
            timeout: 5_000,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Output);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("HTTP error 404 Not Found", result.StandardError, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(result.StandardError) <= FFmpegProcessRunner.MaxDiagnosticBytes);
    }

    [Fact]
    public void SanitizeDiagnostic_RedactsNetworkUrisAndMediaPathsButKeepsHttpStatus()
    {
        const string diagnostic = "HTTP error 404 Not Found for https://user:secret@media.example/private/episode.m3u8?token=private#part\n"
            + "Error opening input file C:\\Users\\Raphael\\Videos\\Sensitive Show\\S01E01.strm";

        var sanitized = FFmpegProcessRunner.SanitizeDiagnostic(diagnostic);

        Assert.Contains("HTTP error 404", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("media.example", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("episode.m3u8", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("user", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("token", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive Show", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("S01E01.strm", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("episodes/Private Show/S01E01.mkv", "Private Show")]
    [InlineData("./episodes/Private Show/S01E01.mkv", "Private Show")]
    [InlineData("..\\Private Show\\S01E01.mkv", "Private Show")]
    [InlineData("/name", "/name")]
    [InlineData("episode.mkv", "episode.mkv")]
    [InlineData("S01E01.strm", "S01E01.strm")]
    public void SanitizeDiagnostic_RedactsRelativeAndShortPosixPathsButKeepsHttpStatus(string path, string privateSegment)
    {
        var diagnostic = $"Error opening input file {path}\nHTTP error 404 Not Found";

        var sanitized = FFmpegProcessRunner.SanitizeDiagnostic(diagnostic);

        Assert.DoesNotContain(privateSegment, sanitized, StringComparison.Ordinal);
        Assert.Contains("HTTP error 404", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeDiagnostic_KeepsHttpStatusCodeButDropsUntrustedReasonAndAuthorizationText()
    {
        const string diagnostic = "HTTP error 404 Not Found - Bearer abc123 /media/episode.mkv";

        var sanitized = FFmpegProcessRunner.SanitizeDiagnostic(diagnostic);

        Assert.Contains("HTTP error 404", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Not Found", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("episode.mkv", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("episode.wmv")]
    [InlineData("episode.mpg")]
    [InlineData("episode.mpeg")]
    [InlineData("episode.m2ts")]
    [InlineData("episode.vob")]
    [InlineData("private-media-name-without-extension - Bearer abc123")]
    public void SanitizeDiagnostic_RedactsFailedInputOpenLinesForBareFileNames(string inputName)
    {
        var diagnostic = $"Error opening input file {inputName}";

        var sanitized = FFmpegProcessRunner.SanitizeDiagnostic(diagnostic);

        Assert.DoesNotContain(inputName, sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", sanitized, StringComparison.Ordinal);
        Assert.Equal("[path-bearing FFmpeg detail redacted]", sanitized);
    }

    [Fact]
    public void SanitizeDiagnostic_PreservesUnrelatedInformationalLines()
    {
        const string diagnostic = "Input stream #0:0 is audio: aac, 48000 Hz, stereo";

        Assert.Equal(diagnostic, FFmpegProcessRunner.SanitizeDiagnostic(diagnostic));
    }

    [Theory]
    [InlineData("Failed to open input file private-media-name")]
    [InlineData("Unable to open input file private-media-name")]
    public void SanitizeDiagnostic_RedactsAlternateInputOpenFailureGrammar(string diagnostic)
    {
        var sanitized = FFmpegProcessRunner.SanitizeDiagnostic(diagnostic);

        Assert.DoesNotContain("private-media-name", sanitized, StringComparison.Ordinal);
        Assert.Equal("[path-bearing FFmpeg detail redacted]", sanitized);
    }

    [Theory]
    [InlineData("HTTP error 404 Not Found - Bearer abc123", "HTTP error 404")]
    [InlineData("Authorization: Bearer abc123", "Authorization: [redacted]")]
    [InlineData("Retrying request with Bearer abc123; transient failure", "Retrying request with Bearer [redacted]; transient failure")]
    public void SanitizeDiagnostic_RedactsAuthorizationAnywhereAndKeepsOnlyHttpStatus(string diagnostic, string expected)
    {
        var sanitized = FFmpegProcessRunner.SanitizeDiagnostic(diagnostic);

        Assert.Equal(expected, sanitized);
        Assert.DoesNotContain("abc123", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactArgument_RemovesUriCredentialsQueryAndFragment()
    {
        const string argument = "https://user:secret@media.example/path/episode.m3u8?token=private#part";

        var redacted = FFmpegProcessRunner.RedactArgument(argument);

        Assert.Equal("[remote URI redacted]", redacted);
        Assert.DoesNotContain("user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("media.example", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("episode.m3u8", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("private", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("part", redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-i")]
    [InlineData("C:/media/episode.mkv")]
    [InlineData("file:///C:/media/episode.mkv")]
    public void RedactArgument_LeavesNonNetworkInputsUnchanged(string argument)
    {
        Assert.Equal(argument, FFmpegProcessRunner.RedactArgument(argument));
    }

    private static (string ProcessPath, IReadOnlyList<string> Arguments) CreateFailingProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            var commandProcessor = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
            return (commandProcessor, ["/d", "/c", "for /L %i in (1,1,500) do @echo 12345678901234567890 1>&2 & echo HTTP error 404 Not Found 1>&2 & exit /b 1"]);
        }

        return ("/bin/sh", ["-c", "i=0; while [ \"$i\" -lt 500 ]; do printf '%s\\n' '12345678901234567890' >&2; i=$((i + 1)); done; printf '%s\\n' 'HTTP error 404 Not Found' >&2; exit 1"]);
    }
}
