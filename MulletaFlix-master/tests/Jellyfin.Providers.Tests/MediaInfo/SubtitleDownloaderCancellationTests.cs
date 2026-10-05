using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Providers.MediaInfo;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.MediaInfo;

[Collection("LibraryManagerTests")]
public sealed class SubtitleDownloaderCancellationTests : IDisposable
{
    private readonly IRecordingsManager? _previousRecordingsManager = Video.RecordingsManager;

    public SubtitleDownloaderCancellationTests()
    {
        Video.RecordingsManager = Mock.Of<IRecordingsManager>();
    }

    public void Dispose()
    {
        Video.RecordingsManager = _previousRecordingsManager;
    }

    [Fact]
    public async Task DownloadSubtitles_WhenSearchIsCancelled_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var subtitleManager = new Mock<ISubtitleManager>();
        subtitleManager
            .Setup(manager => manager.SearchSubtitles(It.IsAny<SubtitleSearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubtitleSearchRequest _, CancellationToken token) =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled<RemoteSubtitleInfo[]>(token);
            });

        var downloader = new SubtitleDownloader(NullLogger.Instance, subtitleManager.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadSubtitles(
            new Movie { VideoType = VideoType.VideoFile },
            [],
            skipIfEmbeddedSubtitlesPresent: false,
            skipIfAudioTrackMatches: false,
            requirePerfectMatch: false,
            languages: ["pt-BR"],
            disabledSubtitleFetchers: [],
            subtitleFetcherOrder: [],
            isAutomated: true,
            cancellationToken: cancellationSource.Token));
    }
}
