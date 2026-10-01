using IntroSkipper.Data;
using IntroSkipper.Db;
using IntroSkipper.Helper;
using IntroSkipper.Providers;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model;
using Moq;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class SeriesPlaybackSegmentTests
{
    [Fact]
    public async Task EpisodeFromSeries_IsSupportedAndReceivesItsIntroSegment()
    {
        var episodeId = Guid.NewGuid();
        var database = new Mock<IIntroSkipperDatabase>();
        database
            .Setup(db => db.GetServableSegmentsAsync(episodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DbSegment(episodeId, AnalysisMode.Introduction, 12 * TimeSpan.TicksPerSecond, 98 * TimeSpan.TicksPerSecond, SegmentSource.Chromaprint)]);
        var provider = new SegmentProvider(database.Object, new SegmentDtoFactory(database.Object));

        Assert.True(MediaItemHelper.IsSupported(new Episode { Id = episodeId }));
        Assert.True(await provider.Supports(new Episode { Id = episodeId }));

        var segments = await provider.GetMediaSegments(
            new MediaSegmentGenerationRequest { ItemId = episodeId, ExistingSegments = [] },
            TestContext.Current.CancellationToken);

        var segment = Assert.Single(segments);
        Assert.Equal(episodeId, segment.ItemId);
        Assert.Equal(12 * TimeSpan.TicksPerSecond, segment.StartTicks);
        Assert.Equal(98 * TimeSpan.TicksPerSecond, segment.EndTicks);
    }
}
