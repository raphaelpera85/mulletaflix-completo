// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using IntroSkipper.Configuration;
using IntroSkipper.Data;
using IntroSkipper.FFmpeg;
using IntroSkipper.Manager;
using IntroSkipper.ScheduledTasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Testing;
using Xunit;

namespace IntroSkipper.Integration.Tests;

[Collection(MariaDbCollection.Name)]
public sealed class IntroSkipperMediaAnalysisE2ETests : IClassFixture<MariaDbFixture>
{
    private readonly MariaDbFixture _fixture;

    public IntroSkipperMediaAnalysisE2ETests(MariaDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var ffmpegPath = FindFfmpegPath();
        Assert.SkipUnless(ffmpegPath is not null, "Set MULLETAFLIX_TEST_FFMPEG_PATH or add ffmpeg.exe to PATH.");
        Assert.SkipUnless(File.Exists(Path.Combine(Path.GetDirectoryName(ffmpegPath!)!, "ffprobe.exe")), "ffprobe.exe must be beside the test FFmpeg.");

        await _fixture.Database.InitializeAsync();
        Assert.True(_fixture.Cache.TryInitialize());

        var previousPlugin = Plugin.Instance;
        var temp = Directory.CreateTempSubdirectory("mfx-introskipper-e2e-");
        try
        {
            var configuration = new PluginConfiguration
            {
                ScanIntroduction = true,
                ScanCredits = false,
                ScanRecap = false,
                ScanPreview = false,
                ScanCommercial = false,
                PreferChromaprint = true,
                MinimumIntroDuration = 5,
                MaxParallelism = 8,
                AdjustIntroBasedOnChapters = false,
                AdjustIntroBasedOnSilence = false,
                SnapToKeyframe = false,
            };
            var items = await CreateMediaAsync(temp.FullName, ffmpegPath!);
            var series = items.Select(item => item.Series).DistinctBy(item => item.Id).ToArray();
            var entities = series.Cast<BaseItem>().Concat(items.Select(item => item.Episode)).ToDictionary(item => item.Id);

            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.PluginsPath).Returns(temp.FullName);
            paths.Setup(value => value.PluginConfigurationsPath).Returns(temp.FullName);
            var serializer = new Mock<IXmlSerializer>();
            serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>())).Returns(configuration);

            var serverConfiguration = new Mock<IServerConfigurationManager>();
            serverConfiguration.Setup(value => value.GetConfiguration("encoding")).Returns(new EncodingOptions { EncoderAppPathDisplay = ffmpegPath });
            serverConfiguration.Setup(value => value.Configuration).Returns(new ServerConfiguration());

            var pluginManager = new Mock<IPluginManager>();
            pluginManager.Setup(value => value.Plugins).Returns(Array.Empty<LocalPlugin>());
            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(value => value.GetItemById(It.IsAny<Guid>())).Returns((Guid id) => entities.GetValueOrDefault(id));
            libraryManager.Setup(value => value.GetLibraryOptions(It.IsAny<BaseItem>())).Returns(new LibraryOptions());
            libraryManager.Setup(value => value.GetItemList(It.IsAny<InternalItemsQuery>(), false))
                .Returns((InternalItemsQuery query, bool _) =>
                {
                    IEnumerable<BaseItem> found = query.AncestorIds.Length > 0
                        ? items.Where(item => query.AncestorIds.Contains(item.Series.Id)).Select(item => (BaseItem)item.Episode)
                        : items.Where(item => query.ItemIds.Contains(item.Episode.Id)).Select(item => (BaseItem)item.Episode);
                    return found.DistinctBy(item => item.Id).ToArray();
                });
            _ = new Plugin(paths.Object, serializer.Object, serverConfiguration.Object, libraryManager.Object, Mock.Of<IChapterManager>(), pluginManager.Object);
            Assert.True(Plugin.Instance!.Configuration.ScanIntroduction, "The integration plugin did not load the requested introduction configuration.");

            var chapterManager = new Mock<IChapterManager>();
            chapterManager.Setup(value => value.GetChapters(It.IsAny<Guid>())).Returns([]);

            var cacheService = new DetectionCacheService(NullLogger<DetectionCacheService>.Instance, _fixture.Cache);
            var countingFfmpeg = new CountingFfmpegService(new FFmpegService(NullLogger<FFmpegService>.Instance, cacheService));
            var resolver = new SeasonResolver(NullLogger<SeasonResolver>.Instance, libraryManager.Object, serverConfiguration.Object);
            var task = new BaseItemAnalyzerTask(NullLoggerFactory.Instance, resolver, countingFfmpeg, cacheService, _fixture.Cache, _fixture.Database);
            var episodeIds = items.Select(item => item.Episode.Id).ToArray();
            var owners = resolver.OwnersOf(episodeIds);
            Assert.Equal(series.Length, owners.Count);

            List<ResolvedSeason> resolvedSeasons = [];
            foreach (var owner in owners)
            {
                Assert.True(resolver.TryResolve(owner, includeExcluded: false, out var ownerSeasons), $"Unable to resolve owner {owner.Id}.");
                resolvedSeasons.AddRange(ownerSeasons);
            }

            Assert.Equal(series.Length, resolvedSeasons.Count);
            Assert.All(resolvedSeasons, season => Assert.Equal(2, season.Episodes.Count));
            Assert.Equal(items.Length, resolvedSeasons.SelectMany(season => season.Episodes).Select(episode => episode.EpisodeId).Distinct().Count());

            var ffmpegAvailable = await countingFfmpeg.CheckFFmpegVersionAsync();
            var ffmpegCheck = countingFfmpeg.GetCheckResult();
            Assert.True(ffmpegAvailable, $"FFmpeg capability check failed: {ffmpegCheck.Status}; probes={string.Join(',', ffmpegCheck.Outputs.Select(output => output.Name))}.");

            foreach (var season in resolvedSeasons)
            {
                var candidates = season.Episodes;
                var verified = await task.VerifyQueueAsync(candidates, [AnalysisMode.Introduction], ffmpegAvailable);
                Assert.Equal(candidates.Count, verified.Count);
                Assert.All(verified, episode => Assert.True(episode.NeedsAnalysis(AnalysisMode.Introduction), $"Episode {episode.EpisodeId} was already settled before its first scan."));
            }

            await task.AnalyzeItemsAsync(new Progress<double>(), CancellationToken.None, episodeIds);

            Assert.Equal(items.Length, countingFfmpeg.FingerprintCalls);
            Assert.Equal(2, countingFfmpeg.MaximumConcurrentFingerprints);
            Assert.Equal(0, countingFfmpeg.EmptyFingerprints);
            Assert.InRange(countingFfmpeg.MaximumConcurrentFingerprints, 1, 2);
            foreach (var item in items)
            {
                var segments = await _fixture.Database.GetSegmentsAsync(item.Episode.Id);
                var intro = Assert.Single(segments.Where(segment => segment.Source == SegmentSource.Chromaprint));
                Assert.InRange(intro.StartTicks / (double)TimeSpan.TicksPerSecond, 0, 2);
                Assert.InRange(intro.EndTicks / (double)TimeSpan.TicksPerSecond, 12, 32);
            }
        }
        finally
        {
            typeof(Plugin).GetProperty(nameof(Plugin.Instance))!.GetSetMethod(nonPublic: true)!.Invoke(null, [previousPlugin]);
            Directory.Delete(temp.FullName, recursive: true);
        }
    }

    private static async Task<MediaEpisode[]> CreateMediaAsync(string directory, string ffmpegPath)
    {
        List<MediaEpisode> result = [];
        for (var seriesIndex = 0; seriesIndex < 2; seriesIndex++)
        {
            var series = new Series { Id = Guid.NewGuid(), Name = $"Synthetic Series {seriesIndex}" };
            var seasonId = Guid.NewGuid();
            var sharedAudioPath = Path.Combine(directory, $"series-{seriesIndex}-intro.wav");
            await GenerateSharedAudioAsync(ffmpegPath, sharedAudioPath);
            for (var episodeIndex = 0; episodeIndex < 2; episodeIndex++)
            {
                var number = (seriesIndex * 2) + episodeIndex;
                var audioPath = Path.Combine(directory, $"episode-{number}.wav");
                var strmPath = Path.Combine(directory, $"episode-{number}.strm");
                await GenerateAudioAsync(ffmpegPath, sharedAudioPath, audioPath, 500 + (number * 170));
                await File.WriteAllTextAsync(strmPath, new Uri(audioPath).AbsoluteUri);
                result.Add(new MediaEpisode(series, new Episode
                {
                    Id = Guid.NewGuid(),
                    SeriesId = series.Id,
                    SeasonId = seasonId,
                    ParentIndexNumber = 1,
                    IndexNumber = episodeIndex + 1,
                    Name = $"Episode {episodeIndex + 1}",
                    Path = strmPath,
                    RunTimeTicks = TimeSpan.FromSeconds(32).Ticks,
                    DateCreated = DateTime.UtcNow,
                }));
            }
        }

        return [.. result];
    }

    private static async Task GenerateSharedAudioAsync(string ffmpegPath, string outputPath)
    {
        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "anoisesrc=color=pink:amplitude=0.2:sample_rate=44100:duration=20",
            "-c:a", "pcm_s16le", "-y", outputPath,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        await EnsureFfmpegSucceededAsync(startInfo);
    }

    private static async Task GenerateAudioAsync(string ffmpegPath, string sharedAudioPath, string outputPath, int tailFrequency)
    {
        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error",
            "-i", sharedAudioPath,
            "-f", "lavfi", "-i", $"sine=frequency={tailFrequency}:sample_rate=44100:duration=12",
            "-filter_complex", "[0:a][1:a]concat=n=2:v=0:a=1[a]",
            "-map", "[a]", "-c:a", "pcm_s16le", "-y", outputPath,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        await EnsureFfmpegSucceededAsync(startInfo);
    }

    private static async Task EnsureFfmpegSucceededAsync(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the test FFmpeg.");
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await process.WaitForExitAsync(timeout.Token);
        var error = await standardError;
        Assert.True(process.ExitCode == 0, $"Test media generation failed: {error}");
    }

    private static string? FindFfmpegPath()
    {
        var configured = Environment.GetEnvironmentVariable("MULLETAFLIX_TEST_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path, "ffmpeg.exe"))
            .FirstOrDefault(File.Exists);
    }

    private sealed record MediaEpisode(Series Series, Episode Episode);

    private sealed class CountingFfmpegService(IFFmpegService inner) : IFFmpegService
    {
        private int _activeFingerprints;
        private int _maximumConcurrentFingerprints;
        private int _fingerprintCalls;
        private int _emptyFingerprints;

        public int FingerprintCalls => Volatile.Read(ref _fingerprintCalls);

        public int MaximumConcurrentFingerprints => Volatile.Read(ref _maximumConcurrentFingerprints);

        public int EmptyFingerprints => Volatile.Read(ref _emptyFingerprints);

        public async Task<uint[]> FingerprintAsync(QueuedEpisode episode, AnalysisMode mode, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _fingerprintCalls);
            var active = Interlocked.Increment(ref _activeFingerprints);
            UpdateMaximum(active);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                var fingerprint = await inner.FingerprintAsync(episode, mode, cancellationToken);
                if (fingerprint.Length == 0)
                {
                    Interlocked.Increment(ref _emptyFingerprints);
                }

                return fingerprint;
            }
            catch (FingerprintException)
            {
                Interlocked.Increment(ref _emptyFingerprints);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _activeFingerprints);
            }
        }

        public Task<bool> CheckFFmpegVersionAsync(CancellationToken cancellationToken = default) => inner.CheckFFmpegVersionAsync(cancellationToken);

        public Task<TimeRange[]> DetectSilenceAsync(QueuedEpisode episode, TimeRange range, AnalysisMode mode, CancellationToken cancellationToken = default) => inner.DetectSilenceAsync(episode, range, mode, cancellationToken);

        public Task<BlackFrame[]> DetectBlackFramesAsync(QueuedEpisode episode, TimeRange range, int minimum, int threshold, AnalysisMode mode, CancellationToken cancellationToken = default) => inner.DetectBlackFramesAsync(episode, range, minimum, threshold, mode, cancellationToken);

        public Task<BlackFrame[]> DetectBlackFramesAsync(QueuedEpisode episode, int threshold, CancellationToken cancellationToken = default) => inner.DetectBlackFramesAsync(episode, threshold, cancellationToken);

        public Task<KeyframeVisual[]> DetectKeyframeVisualsAsync(QueuedEpisode episode, CancellationToken cancellationToken = default) => inner.DetectKeyframeVisualsAsync(episode, cancellationToken);

        public Task<BlackInterval[]> DetectBlackIntervalsAsync(QueuedEpisode episode, TimeRange range, int threshold, int minimum, CancellationToken cancellationToken = default) => inner.DetectBlackIntervalsAsync(episode, range, threshold, minimum, cancellationToken);

        public Task<double[]> DetectKeyFramesAsync(QueuedEpisode episode, TimeRange range, AnalysisMode mode, CancellationToken cancellationToken = default) => inner.DetectKeyFramesAsync(episode, range, mode, cancellationToken);

        public Task<double?> ProbeAudioDurationAsync(string filePath, CancellationToken cancellationToken = default) => inner.ProbeAudioDurationAsync(filePath, cancellationToken);

        public FFmpegCheckResult GetCheckResult() => inner.GetCheckResult();

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var maximum = Volatile.Read(ref _maximumConcurrentFingerprints);
                if (maximum >= active || Interlocked.CompareExchange(ref _maximumConcurrentFingerprints, active, maximum) == maximum)
                {
                    return;
                }
            }
        }
    }
}
