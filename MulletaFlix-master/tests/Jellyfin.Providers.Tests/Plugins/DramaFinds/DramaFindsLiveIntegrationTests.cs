using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.DramaFinds;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaFinds
{
    /// <summary>
    /// Live verification against the real DramaFinds API.
    /// </summary>
    /// <remarks>
    /// Tests are skipped by default and reach the public internet only when the explicit live-provider
    /// environment switch is set.
    /// It exists because every other test in this folder uses a captured payload, and a captured
    /// payload cannot prove that the client still speaks what the API serves today — in particular
    /// that the Guest-Id header is still accepted and that the cover still resolves unsigned.
    /// </remarks>
    public sealed class DramaFindsLiveIntegrationTests
    {
        [Fact]
        public async Task SearchAndDetail_WorkAgainstTheRealApi()
        {
            LiveProviderIntegrationTestEnvironment.SkipUnlessEnabled();
            using var client = CreateClient();

            // The endpoint is fuzzy by substring, so the exact title is the only query that is
            // guaranteed to return the drama and its duplicate edition.
            var matches = await client.SearchAsync("99 Amuletos, 99 Desilusões", 10, CancellationToken.None);

            Assert.True(
                matches.Count >= 2,
                $"Expected the two known editions of this drama but the search returned {matches.Count} usable candidate(s).");
            Assert.All(matches, match => Assert.DoesNotContain("auth_key", match.Drama.Cover, StringComparison.Ordinal));

            var drama = await client.GetDramaAsync("47011", CancellationToken.None);

            Assert.NotNull(drama);
            Assert.Equal("47011", drama!.DramaId);
            Assert.Equal("99 Amuletos, 99 Desilusões", drama.Title);
            Assert.False(string.IsNullOrWhiteSpace(drama.Overview), "The live payload carried no synopsis.");
            Assert.False(string.IsNullOrWhiteSpace(drama.Cover), "The live payload carried no cover.");

            // The signature expires within hours, so the client has to have dropped it.
            Assert.DoesNotContain("auth_key", drama.Cover, StringComparison.Ordinal);
            Assert.DoesNotContain("?", drama.Cover, StringComparison.Ordinal);

            // The API reports the episode count in totalCount and reports "episodes" as null, so the
            // list only exists if the client synthesized it.
            Assert.True(drama.EpisodeCount > 0, "The live payload reported no episode count.");
            Assert.Equal(drama.EpisodeCount, drama.Episodes.Count);
            Assert.Equal("Episódio 1", drama.Episodes[0].Name);
            Assert.Equal(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilus%C3%B5es/episode-{0}",
                    drama.EpisodeCount),
                drama.Episodes[drama.EpisodeCount - 1].Url);

            // The API may return fuzzy candidates even when they are not a usable match.
            // They must not arm the transport failure cooldown or be accepted automatically.
            var none = await client.SearchAsync("Drama Totalmente Inexistente Sem Correspondente 2026", 10, CancellationToken.None);
            Assert.DoesNotContain(none, match => match.Score >= 0.92);
            Assert.False(client.IsInFailureCooldown, "A valid search response with no strong candidate must not arm the failure cooldown.");

            // And the fuzzy rows the endpoint returns for a shared word must never be usable as an
            // automatic identification.
            var fuzzy = await client.SearchAsync("A Agência", 10, CancellationToken.None);
            Assert.DoesNotContain(fuzzy, match => match.Score >= 0.92);
        }

        private static DramaFindsClient CreateClient()
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

            return new DramaFindsClient(
                httpClientFactory.Object,
                new Mock<ILogger<DramaFindsClient>>().Object);
        }
    }
}
