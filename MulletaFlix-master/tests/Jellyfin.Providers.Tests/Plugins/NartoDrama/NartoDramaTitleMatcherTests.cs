using System.Linq;
using MediaBrowser.Providers.Plugins.NartoDrama;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NartoDrama
{
    public class NartoDramaTitleMatcherTests
    {
        /// <summary>
        /// Verified live against the same series: the site appends a localized marketing marker to
        /// the title it publishes, and the value differs per language.
        /// </summary>
        [Theory]
        [InlineData("Abandonei o Rei dos Deuses no Altar - Streaming grátis", "Abandonei o Rei dos Deuses no Altar")]
        [InlineData("Coração à Conquista - Streaming gratis", "Coração à Conquista")]
        [InlineData("Coração à Conquista - Streaming Gratis", "Coração à Conquista")]
        [InlineData("Coração à Conquista - Free Streaming", "Coração à Conquista")]
        public void StripStreamingSuffix_RemovesTheLocalizedMarker(string raw, string expected)
        {
            Assert.Equal(expected, NartoDramaTitleMatcher.StripStreamingSuffix(raw));
        }

        [Theory]
        [InlineData("Abandonei o Rei dos Deuses no Altar")]
        [InlineData("Amor - A História Continua")]
        [InlineData("(Dublado) Não Mexa com a Coitadinha")]
        public void StripStreamingSuffix_LeavesATitleWithoutTheMarkerAlone(string raw)
        {
            Assert.Equal(raw, NartoDramaTitleMatcher.StripStreamingSuffix(raw));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void StripStreamingSuffix_EmptyInputStaysEmpty(string? raw)
        {
            Assert.Equal(string.Empty, NartoDramaTitleMatcher.StripStreamingSuffix(raw));
        }

        /// <summary>
        /// Every shape a user may paste. The percent encoded form is accepted because the site itself
        /// answers 200 for an accented slug, which was verified live.
        /// </summary>
        [Theory]
        [InlineData("https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT", "abandonei-o-rei-dos-deuses-no-altar")]
        [InlineData("https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar", "abandonei-o-rei-dos-deuses-no-altar")]
        [InlineData("https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/37?lang=pt-PT", "abandonei-o-rei-dos-deuses-no-altar")]
        [InlineData("https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT&amp;from=home", "abandonei-o-rei-dos-deuses-no-altar")]
        [InlineData("https://narto-drama.com/detail/watch/cora%C3%A7%C3%A3o-a-conquista?lang=pt-PT", "coração-a-conquista")]
        [InlineData("abandonei-o-rei-dos-deuses-no-altar", "abandonei-o-rei-dos-deuses-no-altar")]
        public void ExtractSlug_ResolvesEveryAcceptedInputShape(string input, string expected)
        {
            Assert.Equal(expected, NartoDramaTitleMatcher.ExtractSlug(input));
        }

        /// <summary>
        /// A bare single word is a search term, not a slug: treating it as one would cost a wasted
        /// request per library item on every refresh, and an episode number is not a slug either.
        /// </summary>
        [Theory]
        [InlineData("Coração à Conquista")]
        [InlineData("Vingança")]
        [InlineData("abandonei")]
        [InlineData("91437")]
        [InlineData("https://narto-drama.com/search?q=abandonei")]
        [InlineData("https://narto-drama.com/detail/watch/")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ExtractSlug_ReturnsNullWhenInputCarriesNoSlug(string? input)
        {
            Assert.Null(NartoDramaTitleMatcher.ExtractSlug(input));
        }

        /// <summary>
        /// The dubbing marker is structural on NartoDrama: the catalogue really contains both
        /// "Abandonei o Rei dos Deuses no Altar" and "[Dublado] Abandonei o Rei dos Deuses no Altar",
        /// and a library folder for the dubbed version is usually the bare title plus "(Dublado)".
        /// </summary>
        [Theory]
        [InlineData("[Dublado] Abandonei o Rei dos Deuses no Altar", "Abandonei o Rei dos Deuses no Altar")]
        [InlineData("Abandonei o Rei dos Deuses no Altar (Dublado)", "Abandonei o Rei dos Deuses no Altar")]
        [InlineData("Abandonei o Rei dos Deuses no Altar (2024)", "Abandonei o Rei dos Deuses no Altar")]
        public void Normalize_IgnoresDubbingMarkersAndYears(string libraryName, string platformName)
        {
            Assert.Equal(
                NartoDramaTitleMatcher.Normalize(libraryName),
                NartoDramaTitleMatcher.Normalize(platformName));
        }

        [Fact]
        public void Similarity_IdenticalAfterNormalization_IsOne()
        {
            Assert.Equal(
                1.0,
                NartoDramaTitleMatcher.Similarity("Coração à Conquista", "Coracao a Conquista"));
        }

        /// <summary>
        /// A library folder named after only part of the platform title must still clear the
        /// automatic match threshold used by the provider.
        /// </summary>
        [Fact]
        public void Similarity_SubtitleContainedInFullTitle_ClearsTheAutomaticThreshold()
        {
            var score = NartoDramaTitleMatcher.Similarity(
                "Dunia Game Ini",
                "(Sulih suara) Dunia Game Ini, Aku yang Berkuasa");

            Assert.True(score >= 0.9, $"Expected at least 0.9 but got {score}.");
        }

        [Fact]
        public void Similarity_UnrelatedTitles_ScoresZero()
        {
            Assert.Equal(
                0.0,
                NartoDramaTitleMatcher.Similarity("Chuva Negra", "Charli XCX: Alone Together (2022)"));
        }

        [Fact]
        public void Similarity_SharedLeadingWordOnly_StaysBelowTheAutomaticThreshold()
        {
            // Both titles start with "Abandonei", and a one-token overlap must never be enough to
            // auto-identify a series.
            var score = NartoDramaTitleMatcher.Similarity(
                "Abandonei o Rei dos Deuses no Altar",
                "Abandonei Minha Família e Virei Milionária");

            Assert.True(score < 0.9, $"Expected below 0.9 but got {score}.");
        }

        [Fact]
        public void Similarity_PreTokenized_CarriesNoDiacriticsAndDropsTheNoiseTokens()
        {
            var tokens = NartoDramaTitleMatcher.CreateTokenSet("[Dublado] Coração à Conquista (2026)");

            Assert.DoesNotContain("dublado", tokens);
            Assert.DoesNotContain("2026", tokens);
            Assert.Contains("coracao", tokens);
            Assert.Contains("a", tokens);

            Assert.Equal(
                NartoDramaTitleMatcher.Similarity("Coração à Conquista", "Coração à Conquista"),
                NartoDramaTitleMatcher.Similarity(tokens, "Coração à Conquista"));
        }

        [Theory]
        [InlineData(null, "Abandonei")]
        [InlineData("", "Abandonei")]
        [InlineData("Abandonei", null)]
        [InlineData("(2026)", "Abandonei")]
        public void Similarity_UnusableInputScoresZero(string? left, string? right)
        {
            Assert.Equal(0.0, NartoDramaTitleMatcher.Similarity(left, right));
        }

        [Fact]
        public void Normalize_EmptyInputStaysEmpty()
        {
            Assert.Equal(string.Empty, NartoDramaTitleMatcher.Normalize(null));
            Assert.Equal(string.Empty, NartoDramaTitleMatcher.Normalize("   "));
            Assert.Empty(NartoDramaTitleMatcher.Tokenize(null));
        }

        [Fact]
        public void Tokenize_TreatsEverySeparatorAsASpace()
        {
            Assert.Equal(
                NartoDramaTitleMatcher.Normalize("3.2.1, Adeus e Ponto Final"),
                NartoDramaTitleMatcher.Normalize("3.2 1, Adeus e Ponto Final"));
        }

        [Fact]
        public void Tokenize_KeepsTheTokensOfAMultiWordTitleInOrder()
        {
            Assert.Equal(
                new[] { "abandonei", "o", "rei", "dos", "deuses", "no", "altar" },
                NartoDramaTitleMatcher.Tokenize("Abandonei o Rei dos Deuses no Altar").ToArray());
        }
    }
}
