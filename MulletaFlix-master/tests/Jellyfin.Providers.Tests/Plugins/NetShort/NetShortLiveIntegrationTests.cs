using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.NetShort;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NetShort
{
    /// <summary>
    /// Live verification against the real NetShort API.
    /// </summary>
    /// <remarks>
    /// The type name deliberately contains "Integration" so the CI filter
    /// (FullyQualifiedName!~Integration) skips it: it reaches the public internet and must never gate
    /// a build. Run it on demand with
    /// <c>dotnet test --filter "FullyQualifiedName~NetShortLiveIntegration"</c>.
    /// It exists because a captured payload cannot prove that the encryption still matches what the
    /// site does today — and encryption is the one thing about this provider that can break silently
    /// into an HTTP 500 on every call.
    /// </remarks>
    public sealed class NetShortLiveIntegrationTests
    {
        /// <summary>
        /// The series the task was verified against: https://netshort.com/pt/hotseries/além-do-99-perdões-2082282581191360513.
        /// </summary>
        private const string SeriesId = "2082282581191360513";

        [Fact]
        public async Task Search_FindsTheSeriesForItsOwnTitleThroughTheEncryptedApi()
        {
            using var client = CreateClient();

            var matches = await client.SearchAsync("perdões", 12, CancellationToken.None);

            Assert.NotEmpty(matches);

            var expected = Assert.Single(matches, match => match.Series.SeriesId == SeriesId);
            Assert.Equal("Além do 99 Perdões", expected.Series.Name);
            Assert.StartsWith("Brooke Sterling amou", expected.Series.Overview, StringComparison.Ordinal);
            Assert.Contains("awscover.netshort.com", expected.Series.Cover, StringComparison.Ordinal);
            Assert.NotEmpty(expected.Series.Genres);
        }

        [Fact]
        public async Task GetSeries_ReturnsTheWholeSeriesWithItsRealEpisodeList()
        {
            using var client = CreateClient();

            var series = await client.GetSeriesAsync(SeriesId, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal("Além do 99 Perdões", series!.Name);
            Assert.Equal(36, series.EpisodeCount);
            Assert.Equal(36, series.Episodes.Count);
            Assert.Equal(36, series.Episodes.Max(episode => episode.Number));

            // NetShort numbers from one, and episode one is free while the tail is paywalled.
            Assert.Equal(1, series.Episodes.Min(episode => episode.Number));
            Assert.False(series.Episodes[0].Locked);
            Assert.True(series.Episodes[35].Locked);
            Assert.All(series.Episodes, episode => Assert.NotEmpty(episode.Cover));
            Assert.All(series.Episodes, episode => Assert.NotEmpty(episode.EpisodeId));

            Assert.NotNull(series.PremiereDate);
        }

        [Fact]
        public async Task GetSeries_ResolvesAPastedUrlAndFallsBackToTheRenderedPage()
        {
            using var client = CreateClient();

            // Ids are the only route that works for the fallback, and this is the URL shape a user
            // actually pastes.
            var id = NetShortClient.ResolveSeriesId(
                "https://netshort.com/pt/hotseries/além-do-99-perdões-2082282581191360513");

            Assert.Equal(SeriesId, id);

            var series = await client.GetSeriesAsync(id, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal(SeriesId, series!.SeriesId);
        }

        [Fact]
        public async Task MatchByTitle_IdentifiesTheSeriesFromTheTitleMulletaFlixStores()
        {
            using var client = CreateClient();

            var match = await client.MatchByTitleAsync("Além do 99 Perdões", 0.92, CancellationToken.None);

            Assert.NotNull(match);
            Assert.Equal(SeriesId, match!.Series.SeriesId);
        }

        private static NetShortClient CreateClient()
        {
            // A real HttpClient rather than a mocked handler: the point of this test is the wire. The
            // client's own pacing serializes the calls, so one instance is enough.
            var httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory
                .Setup(factory => factory.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            return new NetShortClient(httpClientFactory.Object, new Mock<ILogger<NetShortClient>>().Object);
        }
    }
}
