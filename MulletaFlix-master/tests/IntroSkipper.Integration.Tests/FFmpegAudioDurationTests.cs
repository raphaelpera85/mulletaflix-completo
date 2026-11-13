using IntroSkipper.FFmpeg;
using IntroSkipper.ScheduledTasks;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class FFmpegAudioDurationTests
{
    [Theory]
    [InlineData(300, true, 900_000)]
    [InlineData(1_200, true, 1_200_000)]
    [InlineData(0, true, -1)]
    [InlineData(300, false, 300_000)]
    public void ScanTimeoutMilliseconds_ExtendsOnlyStrmTimeouts(int seconds, bool isStrm, int expected)
    {
        Assert.Equal(expected, FFmpegService.ScanTimeoutMilliseconds(seconds, isStrm));
    }

    [Theory]
    [InlineData(300, true, 900_000)]
    [InlineData(0, true, -1)]
    [InlineData(300, false, 10_000)]
    public void ProbeTimeoutMilliseconds_PreservesLocalTimeout(int scanTimeoutSeconds, bool isStrm, int expected)
    {
        Assert.Equal(expected, FFmpegService.ProbeTimeoutMilliseconds(scanTimeoutSeconds, isStrm));
    }

    [Fact]
    public void ParseAudioDuration_UsesContainerDurationWhenStreamDurationIsUnavailable()
    {
        Assert.Equal(123.5, FFmpegService.ParseAudioDuration("N/A\r\n123.5"));
    }

    [Fact]
    public async Task ResolveDurationAsync_ProbesWhenTheLibraryDurationIsMissing()
    {
        var probeCalls = 0;

        var duration = await BaseItemAnalyzerTask.ResolveDurationAsync(0, () =>
        {
            probeCalls++;
            return Task.FromResult<double?>(123.5);
        });

        Assert.Equal(123.5, duration);
        Assert.Equal(1, probeCalls);
    }

    [Fact]
    public async Task ResolveDurationAsync_DoesNotProbeWhenTheLibraryDurationIsPresent()
    {
        var duration = await BaseItemAnalyzerTask.ResolveDurationAsync(240, () => throw new Xunit.Sdk.XunitException("A known duration must not trigger another probe."));

        Assert.Equal(240, duration);
    }
}
