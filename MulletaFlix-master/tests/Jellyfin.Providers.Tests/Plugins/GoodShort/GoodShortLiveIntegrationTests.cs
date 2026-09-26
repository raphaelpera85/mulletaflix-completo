using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.GoodShort;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.GoodShort
{
    /// <summary>
    /// Live verification against the real GoodShort site.
    /// </summary>
    /// <remarks>
    /// The type name deliberately contains "Integration" so the CI filter
    /// (FullyQualifiedName!~Integration) skips it: it reaches the public internet and must never
    /// gate a build. Run it on demand with
    /// <c>dotnet test --filter "FullyQualifiedName~GoodShortLiveIntegration"</c>.
    /// It exists because every other test in this folder uses a captured payload, and a captured
    /// payload cannot prove that GoodShort still answers the way it did when the fixtures were taken.
    /// </remarks>
    public sealed class GoodShortLiveIntegrationTests
    {
        /// <summary>
        /// The first series URL reported by the user. Its page listed 11 episode links while
        /// GoodShort itself reports 37 episodes.
        /// </summary>
        private const string FirstSeriesUrl =
            "https://www.goodshort.com/drama/abandonei-o-rei-dos-deuses-no-altar-31001543625";

        /// <summary>
        /// The second series URL reported by the user. Its slug carries a literal accented "á".
        /// </summary>
        private const string SecondSeriesUrl =
            "https://www.goodshort.com/drama/dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548";

        [Fact]
        public async Task GetSeries_ReadsBothReportedSeriesWithTheirCompleteEpisodeList()
        {
            using var client = CreateClient();

            foreach (var url in new[] { FirstSeriesUrl, SecondSeriesUrl })
            {
                var seriesId = GoodShortClient.ResolveSeriesId(url);
                Assert.False(string.IsNullOrWhiteSpace(seriesId), $"Could not read a series id out of {url}.");

                var series = await client.GetSeriesAsync(
                    seriesId,
                    GoodShortTitleMatcher.ExtractSlug(url),
                    CancellationToken.None);

                Assert.NotNull(series);
                Assert.False(string.IsNullOrWhiteSpace(series!.Name), "The live series carried no name.");
                Assert.False(string.IsNullOrWhiteSpace(series.Overview), "The live series carried no synopsis.");
                Assert.StartsWith("https://acf.goodshort.com/", series.Cover, StringComparison.Ordinal);

                // The API path must enumerate every episode GoodShort reports, which is far more
                // than the truncated ItemList the page ships.
                Assert.True(series.EpisodeCount > 6, $"{series.Name} reported only {series.EpisodeCount} episodes.");
                Assert.True(
                    series.EpisodesAreComplete,
                    $"{series.Name} listed {series.Episodes.Count} of {series.EpisodeCount} episodes.");

                Assert.Equal(Enumerable.Range(1, series.EpisodeCount), series.Episodes.Select(episode => episode.Number));
                Assert.All(series.Episodes, episode => Assert.False(string.IsNullOrWhiteSpace(episode.EpisodeId)));

                var first = series.Episodes[0];
                Assert.StartsWith("https://acf.goodshort.com/", first.Thumbnail, StringComparison.Ordinal);
                Assert.True(first.DurationSeconds > 0, "The live episode carried no duration.");
            }
        }

        [Fact]
        public async Task Search_ReturnsTheSeriesForItsOwnTitle()
        {
            using var client = CreateClient();

            var matches = await client.SearchAsync(
                "Abandonada pelo Don, Coroada pela Máfia",
                10,
                CancellationToken.None);

            Assert.NotEmpty(matches);
            Assert.Contains(matches, match => match.Series.SeriesId == "31001499548");

            // The matcher must strip both the bracketed platform marker and the library style one.
            var best = matches[0];
            Assert.True(best.Score > 0.5, $"Expected a confident match but got {best.Score} for '{best.Series.Name}'.");
        }

        [Fact]
        public async Task GetSeries_ReturnsNothingForAnIdThatDoesNotExist()
        {
            using var client = CreateClient();

            var series = await client.GetSeriesAsync("99999999999999", null, CancellationToken.None);

            // GoodShort answers HTTP 200 with status 12000 for an unknown book, so this only passes
            // if the client reads the body envelope rather than the transport status.
            Assert.Null(series);
            Assert.False(client.IsInFailureCooldown);
        }

        private static GoodShortClient CreateClient()
        {
            // A real HttpClient rather than a mocked handler: the point of this test is the wire.
            var httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory
                .Setup(factory => factory.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            return new GoodShortClient(
                httpClientFactory.Object,
                new Mock<ILogger<GoodShortClient>>().Object);
        }
    }
}
