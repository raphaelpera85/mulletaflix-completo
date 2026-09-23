using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.NartoDrama;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NartoDrama
{
    /// <summary>
    /// Live verification against the real NartoDrama site.
    /// </summary>
    /// <remarks>
    /// The type name deliberately contains "Integration" so the CI filter
    /// (FullyQualifiedName!~Integration) skips it: it reaches the public internet and must never
    /// gate a build. Run it on demand with
    /// <c>dotnet test --filter "FullyQualifiedName~NartoDramaLiveIntegration"</c>.
    /// It exists because every other test in this folder uses a captured page, and a captured page
    /// cannot prove that the parser still reads what the site serves today.
    /// </remarks>
    public sealed class NartoDramaLiveIntegrationTests
    {
        private const string Slug = "abandonei-o-rei-dos-deuses-no-altar";

        [Fact]
        public async Task GetSeries_ReadsTheRealPageEndToEnd()
        {
            var client = CreateClient();

            var series = await client.GetSeriesAsync(Slug, CancellationToken.None);

            Assert.NotNull(series);

            // The published title carries " - Streaming grátis" and the clean one does not.
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", series!.Name);

            // The synopsis is the real, multi-paragraph one, without the site's own pitch glued on.
            Assert.StartsWith("No décimo ano ao lado de Aetheon", series.Overview, StringComparison.Ordinal);
            Assert.DoesNotContain("Narto Drama -", series.Overview, StringComparison.Ordinal);

            Assert.Equal("https://img.nartodrama-api.online/poster/91437.jpg", series.Cover);
            Assert.Equal("91437", series.ContentId);
            Assert.Equal(new[] { "Distúrbio", "Lamento", "Casamento", "Nobreza" }, series.Tags);

            // The series had 37 episodes when this was written, and the page states the same total.
            Assert.Equal(series.EpisodeCount, series.Episodes.Count);
            Assert.Equal(37, series.Episodes.Count);
            Assert.True(series.EpisodesAreComplete);
            Assert.Equal(1, series.Episodes[0].Number);
            Assert.Equal("001", series.Episodes[0].Label);
            Assert.Equal(37, series.Episodes[36].Number);
            Assert.Equal("037", series.Episodes[36].Label);
        }

        [Fact]
        public async Task Search_FindsTheSeriesByTitle()
        {
            var client = CreateClient();

            var matches = await client.SearchAsync("Abandonei o Rei dos Deuses no Altar", 12, CancellationToken.None);

            Assert.NotEmpty(matches);

            // The exact title must be the top hit, not merely present in the list.
            Assert.Equal(Slug, matches[0].Series.Slug);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", matches[0].Series.Name);
            Assert.Equal(1.0, matches[0].Score);
        }

        [Fact]
        public async Task Search_FindsTheSeriesFromAnAccentedTerm()
        {
            var client = CreateClient();

            // The catalogue folds accents into its slugs, and the search page accepts the accented
            // term: "coração" comes back as "coracao-a-conquista".
            var matches = await client.SearchAsync("coração", 12, CancellationToken.None);

            Assert.NotEmpty(matches);
            Assert.Contains(matches, match => match.Series.Slug == "coracao-a-conquista");
        }

        [Fact]
        public async Task GetSeries_ReturnsNullForAnUnknownSlugWithoutGoingQuiet()
        {
            var client = CreateClient();

            Assert.Null(await client.GetSeriesAsync("isto-nao-existe-zzz", CancellationToken.None));

            // A 404 is the site's answer, not an outage.
            Assert.False(client.IsInFailureCooldown);
        }

        private static NartoDramaClient CreateClient()
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

            return new NartoDramaClient(
                httpClientFactory.Object,
                new Mock<ILogger<NartoDramaClient>>().Object);
        }
    }
}
