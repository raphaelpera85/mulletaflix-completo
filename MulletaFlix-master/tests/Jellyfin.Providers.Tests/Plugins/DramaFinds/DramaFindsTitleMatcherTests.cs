using MediaBrowser.Providers.Plugins.DramaFinds;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaFinds
{
    public class DramaFindsTitleMatcherTests
    {
        [Fact]
        public void Similarity_IdenticalAfterNormalization_IsOne()
        {
            Assert.Equal(1.0, DramaFindsTitleMatcher.Similarity("99 Amuletos, 99 Desilusões", "99 Amuletos, 99 Desilusões"));
        }

        [Theory]
        [InlineData("Cicatrizes da Traição", "Cicatrizes da Traicao")]
        [InlineData("Reencontro à Esquina da Nova Vida (Dublado)", "Reencontro à Esquina da Nova Vida")]
        [InlineData("A Rainha Não Ama, Só Conquista (2024)", "A Rainha Não Ama, Só Conquista")]
        public void Normalize_IgnoresDiacriticsDubbingMarkersAndYears(string left, string right)
        {
            Assert.Equal(DramaFindsTitleMatcher.Normalize(left), DramaFindsTitleMatcher.Normalize(right));
        }

        /// <summary>
        /// The regression this matcher exists for. The search endpoint is fuzzy by substring, so
        /// querying "A Agência" also returns "Traído e Caçado pela Própria Agência" and
        /// "Cazado por mi propia agencia". Taking the first API row would identify the wrong series;
        /// both of these measure below the candidate threshold, so they are discarded.
        /// </summary>
        [Theory]
        [InlineData("Traído e Caçado pela Própria Agência", 0.25)]
        [InlineData("Cazado por mi propia agencia", 0.2857)]
        public void Similarity_FuzzySearchRowsAreRejected(string candidate, double expected)
        {
            var score = DramaFindsTitleMatcher.Similarity(candidate, "A Agência");

            Assert.Equal(expected, score);
            Assert.True(
                score < DramaFindsTitleMatcher.MinimumCandidateScore,
                $"Expected below {DramaFindsTitleMatcher.MinimumCandidateScore} but got {score}.");
        }

        /// <summary>
        /// A library folder named after only part of the platform title must still clear the
        /// automatic threshold used by the scheduled task.
        /// </summary>
        [Fact]
        public void Similarity_PartialTitleClearsTheAutomaticThreshold()
        {
            var score = DramaFindsTitleMatcher.Similarity("Adeus e Ponto Final", "3.2.1, Adeus e Ponto Final");

            Assert.True(score >= 0.92, $"Expected at least 0.92 but got {score}.");
        }

        [Fact]
        public void Similarity_UnrelatedTitles_ScoresLow()
        {
            var score = DramaFindsTitleMatcher.Similarity("Chuva Negra", "Renascida das Cinzas: A Vingança contra Meus Irmãos");

            Assert.Equal(0, score);
        }

        /// <summary>
        /// The slugs below are the ones the platform itself publishes: the canonical URL of drama
        /// 47011 is /99-amuletos-99-desilus%C3%B5es/episode-2, and the colon-and-diacritics title
        /// resolves with the same rule.
        /// </summary>
        [Theory]
        [InlineData("99 Amuletos, 99 Desilusões", "99-amuletos-99-desilusões")]
        [InlineData("Empatia e Egoísmo: A Mulher Que Condenou a Própria Filha", "empatia-e-egoísmo-a-mulher-que-condenou-a-própria-filha")]
        [InlineData("Disparo do Destino", "disparo-do-destino")]
        [InlineData("Meu Filho é Meu Pai!", "meu-filho-é-meu-pai")]
        [InlineData("  Espaços   Sobrando  ", "espaços-sobrando")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void BuildSlug_MatchesTheLiveUrlShape(string? title, string expected)
        {
            Assert.Equal(expected, DramaFindsTitleMatcher.BuildSlug(title));
        }

        [Fact]
        public void BuildEpisodeUrl_MatchesTheLiveCanonicalUrl()
        {
            var url = DramaFindsTitleMatcher.BuildEpisodeUrl("47011", "99 Amuletos, 99 Desilusões", 2);

            Assert.Equal("https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilus%C3%B5es/episode-2", url);
        }

        [Fact]
        public void BuildEpisodeUrl_OmitsTheSlugSegmentWhenThereIsNoTitle()
        {
            var url = DramaFindsTitleMatcher.BuildEpisodeUrl("47011", null, 1);

            Assert.Equal("https://dramafinds.com/pt/video-play/47011/episode-1", url);
        }

        /// <summary>
        /// Covers are served signed and the signature expires within hours, so a stored URL that
        /// keeps it would stop resolving. The same object is served unsigned, which is why the
        /// query is dropped instead of the cover being rejected.
        /// </summary>
        [Fact]
        public void StripAuthKey_RemovesTheExpiringQuery()
        {
            var result = DramaFindsTitleMatcher.StripAuthKey(
                "https://static.flareflow.tv/images/cover/2026/08/19/257a729035714b5ea33e4c6c963f6ba8.jpg?auth_key=1790122205-0-0-3baa2feceb8ceb65e263121cb57003a4");

            Assert.Equal(
                "https://static.flareflow.tv/images/cover/2026/08/19/257a729035714b5ea33e4c6c963f6ba8.jpg",
                result);
        }

        [Theory]
        [InlineData("https://example.invalid/cover.jpg", "https://example.invalid/cover.jpg")]
        [InlineData("https://example.invalid/cover.jpg#frag", "https://example.invalid/cover.jpg")]
        [InlineData("  https://example.invalid/cover.jpg  ", "https://example.invalid/cover.jpg")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void StripAuthKey_LeavesAnUnsignedUrlAlone(string? input, string expected)
        {
            Assert.Equal(expected, DramaFindsTitleMatcher.StripAuthKey(input));
        }

        [Theory]
        [InlineData("https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilusões/episode-2", "47011")]
        [InlineData("https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilusões", "47011")]
        [InlineData("https://dramafinds.com/pt/drama/47011", "47011")]
        [InlineData("47011", "47011")]
        [InlineData("{\"dramaId\":\"47011\",\"indexE\":0}", "47011")]
        [InlineData("https://api.dramafinds.com/api/v1/short-dramas/episodes?dramaId=47011", "47011")]
        public void ExtractDramaId_ResolvesEveryAcceptedInputShape(string input, string expected)
        {
            Assert.Equal(expected, DramaFindsTitleMatcher.ExtractDramaId(input));
        }

        [Theory]
        [InlineData("99 Amuletos, 99 Desilusões")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("https://dramafinds.com/pt/video-play/abc/def/episode-2")]
        public void ExtractDramaId_ReturnsNullWhenInputCarriesNoId(string? input)
        {
            Assert.Null(DramaFindsTitleMatcher.ExtractDramaId(input));
        }
    }
}
