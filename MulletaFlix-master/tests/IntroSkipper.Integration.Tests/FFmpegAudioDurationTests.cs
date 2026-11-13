using IntroSkipper.FFmpeg;
using IntroSkipper.ScheduledTasks;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class FFmpegAudioDurationTests
{
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
