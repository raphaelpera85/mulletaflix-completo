using IntroSkipper.Analyzers;
using IntroSkipper.Configuration;
using IntroSkipper.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class ChromaprintAnalyzerTests
{
    [Fact]
    public void CompareEpisodes_FindsSharedOpeningAtDifferentOffsets()
    {
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();
        var leftPoints = Enumerable.Range(0, 80).Select(i => 0xF0000000u + (uint)i)
            .Concat(Enumerable.Range(0, 200).Select(i => 0x10000000u + ((uint)i * 0x101u)))
            .ToArray();
        var rightPoints = Enumerable.Range(0, 100).Select(i => 0xA0000000u + (uint)i)
            .Concat(Enumerable.Range(0, 200).Select(i => 0x10000000u + ((uint)i * 0x101u)))
            .ToArray();
        var analyzer = new ChromaprintAnalyzer(
            NullLogger<ChromaprintAnalyzer>.Instance,
            ffmpegService: null!,
            cacheService: null!,
            database: null!,
            new PluginConfiguration
            {
                MinimumIntroDuration = 1,
                MaximumFingerprintPointDifferences = 0,
                InvertedIndexShift = 0,
            });

        var (left, right) = analyzer.CompareEpisodes(leftId, leftPoints, rightId, rightPoints);

        Assert.True(left.Valid);
        Assert.True(right.Valid);
        Assert.InRange(Math.Abs(left.Start - (80 * ChromaprintConstants.SampleDuration)), 0, 0.01);
        Assert.InRange(Math.Abs(right.Start - (100 * ChromaprintConstants.SampleDuration)), 0, 0.01);
        Assert.InRange(Math.Abs(left.Duration - right.Duration), 0, 0.01);
    }
}
