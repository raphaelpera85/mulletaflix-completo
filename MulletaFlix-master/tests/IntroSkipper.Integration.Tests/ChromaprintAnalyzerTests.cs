using IntroSkipper.Analyzers;
using IntroSkipper.Configuration;
using IntroSkipper.Data;
using IntroSkipper.Db;
using IntroSkipper.FFmpeg;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class ChromaprintAnalyzerTests
{
    [Fact]
    public async Task FindCandidatesAsync_LogsSafeMessageWithoutFingerprintInnerException()
    {
        const string privatePath = "C:\\Users\\TestUser\\Private Show\\S01E01.strm";
        var ffmpegService = new Mock<IFFmpegService>();
        ffmpegService
            .Setup(service => service.FingerprintAsync(It.IsAny<QueuedEpisode>(), It.IsAny<AnalysisMode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FingerprintException("Unable to resolve STRM input.", new DirectoryNotFoundException(privatePath)));
        var logger = new RecordingLogger<ChromaprintAnalyzer>();
        var cacheService = new DetectionCacheService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DetectionCacheService>.Instance,
            Mock.Of<IDetectionCacheDatabase>());
        var analyzer = new ChromaprintAnalyzer(
            logger,
            ffmpegService.Object,
            cacheService,
            Mock.Of<IIntroSkipperDatabase>(),
            new PluginConfiguration());
        var episodes = new[]
        {
            new QueuedEpisode { EpisodeId = Guid.NewGuid(), Path = privatePath, EpisodeNumber = 1 },
            new QueuedEpisode { EpisodeId = Guid.NewGuid(), Path = "C:\\Users\\TestUser\\Private Show\\S01E02.strm", EpisodeNumber = 2 },
        };

        await analyzer.FindCandidatesAsync(episodes, AnalysisMode.Introduction, TestContext.Current.CancellationToken);

        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message => Assert.DoesNotContain("Private Show", message, StringComparison.Ordinal));
        Assert.All(logger.Exceptions, exception => Assert.Null(exception));
    }

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

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            where TState : notnull
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
