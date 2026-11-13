// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.FFmpeg;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class FFmpegProcessRunnerTests
{
    [Fact]
    public void RedactArgument_RemovesUriCredentialsQueryAndFragment()
    {
        const string argument = "https://user:secret@media.example/path/episode.m3u8?token=private#part";

        var redacted = FFmpegProcessRunner.RedactArgument(argument);

        Assert.Equal("https://media.example/path/episode.m3u8", redacted);
        Assert.DoesNotContain("user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("private", redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-i")]
    [InlineData("C:/media/episode.mkv")]
    [InlineData("file:///C:/media/episode.mkv")]
    public void RedactArgument_LeavesNonNetworkInputsUnchanged(string argument)
    {
        Assert.Equal(argument, FFmpegProcessRunner.RedactArgument(argument));
    }
}
