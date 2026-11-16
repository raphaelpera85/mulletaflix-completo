// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Data;
using IntroSkipper.Db;
using IntroSkipper.FFmpeg;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class StrmInputResolverTests
{
    [Fact]
    public async Task FingerprintAsync_ReportsMissingStrmAsFingerprintFailure()
    {
        var missingStrmPath = Path.Combine(Path.GetTempPath(), "mfx-introskipper-strm", Guid.NewGuid().ToString("N"), "episode.strm");
        var cache = new DetectionCacheService(
            NullLogger<DetectionCacheService>.Instance,
            Mock.Of<IDetectionCacheDatabase>());
        var service = new FFmpegService(NullLogger<FFmpegService>.Instance, cache);
        var episode = new QueuedEpisode { Path = missingStrmPath };

        var exception = await Assert.ThrowsAsync<FingerprintException>(
            () => service.FingerprintAsync(episode, AnalysisMode.Introduction, TestContext.Current.CancellationToken));

        Assert.IsType<DirectoryNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task DetectBlackFramesAsync_ReportsMissingStrmAsFingerprintFailure()
    {
        var missingStrmPath = Path.Combine(Path.GetTempPath(), "mfx-introskipper-strm", Guid.NewGuid().ToString("N"), "episode.strm");
        var cache = new DetectionCacheService(
            NullLogger<DetectionCacheService>.Instance,
            Mock.Of<IDetectionCacheDatabase>());
        var service = new FFmpegService(NullLogger<FFmpegService>.Instance, cache);
        var episode = new QueuedEpisode { Path = missingStrmPath };

        var exception = await Assert.ThrowsAsync<FingerprintException>(
            () => service.DetectBlackFramesAsync(episode, threshold: 20, TestContext.Current.CancellationToken));

        Assert.IsType<DirectoryNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void Resolve_UsesAbsoluteMediaTargetFromStrmFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mfx-introskipper-strm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var strmPath = Path.Combine(directory, "episode.strm");
        const string target = "http://127.0.0.1:18967/media/episode.m4a";

        try
        {
            File.WriteAllText(strmPath, $"  {target}  {Environment.NewLine}");

            Assert.Equal(target, StrmInputResolver.Resolve(strmPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_RejectsStrmWithoutSupportedAbsoluteTarget()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mfx-introskipper-strm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var strmPath = Path.Combine(directory, "episode.strm");

        try
        {
            File.WriteAllText(strmPath, "relative/media.mkv");

            Assert.Throws<InvalidDataException>(() => StrmInputResolver.Resolve(strmPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolveArguments_ReplacesTheInputAfterFFmpegInputSwitch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mfx-introskipper-strm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var strmPath = Path.Combine(directory, "episode.strm");
        const string target = "http://127.0.0.1:18967/media/episode.m4a";

        try
        {
            File.WriteAllText(strmPath, target);

            var resolved = StrmInputResolver.ResolveArguments(["-ss", "0", "-i", strmPath, "-f", "null", "-"]);

            Assert.Equal(target, resolved[3]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_LeavesRegularMediaPathUnchanged()
    {
        const string mediaPath = "C:/media/episode.mkv";

        Assert.Equal(mediaPath, StrmInputResolver.Resolve(mediaPath));
    }

    [Fact]
    public void Resolve_ConvertsFileUriToNativePathForFfmpeg()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mfx-introskipper-strm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var strmPath = Path.Combine(directory, "episode.strm");
        var mediaPath = Path.Combine(directory, "media with spaces.wav");

        try
        {
            File.WriteAllText(strmPath, new Uri(mediaPath).AbsoluteUri);

            Assert.Equal(mediaPath, StrmInputResolver.Resolve(strmPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
