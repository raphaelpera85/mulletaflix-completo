using MediaBrowser.Providers.Plugins.GoodShort;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.GoodShort
{
    /// <summary>
    /// Covers the id and slug extraction and the title scoring, using the URL and title shapes the
    /// live catalogue actually serves.
    /// </summary>
    public class GoodShortTitleMatcherTests
    {
        [Theory]
        // The two series URLs reported by the user, one of which carries "dublado-" and one of
        // which carries a literal accented "á" inside the slug.
        [InlineData("https://www.goodshort.com/drama/abandonei-o-rei-dos-deuses-no-altar-31001543625", "31001543625")]
        [InlineData("https://www.goodshort.com/drama/dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548", "31001499548")]
        // The URLs the page itself links to.
        [InlineData("https://www.goodshort.com/episodes/abandonei-o-rei-dos-deuses-no-altar-31001543625", "31001543625")]
        // An episode URL must yield the series id, not the trailing episode id.
        [InlineData("https://www.goodshort.com/episode/abandonei-o-rei-dos-deuses-no-altar-31001543625/001-56639125", "31001543625")]
        [InlineData("/episode/dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548/024-53772607", "31001499548")]
        // A bare slug, which is what GoodShort calls the book resource URL.
        [InlineData("dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548", "31001499548")]
        // A bare id, which is what the Identify dialog gets when only the number is typed.
        [InlineData("31001543625", "31001543625")]
        // A trailing slash and a query string must not confuse the parse.
        [InlineData("https://www.goodshort.com/drama/abandonei-o-rei-dos-deuses-no-altar-31001543625/", "31001543625")]
        [InlineData("https://www.goodshort.com/search?keyword=abandonada", null)]
        // A slug whose hyphens look like separators but whose trailing number is the only id.
        [InlineData("the-alpha-s-rejected-mate-31000636867", "31000636867")]
        // The live catalogue contains a non-breaking hyphen inside a slug.
        [InlineData("sinking-with-my-step‑sister-31001747666", "31001747666")]
        public void ExtractSeriesId_ReadsEveryShapeAUserMayPaste(string input, string? expected)
        {
            Assert.Equal(expected, GoodShortTitleMatcher.ExtractSeriesId(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abandonei-o-rei-dos-deuses-no-altar")]
        [InlineData("31001")]
        [InlineData("https://www.goodshort.com/")]
        public void ExtractSeriesId_ReturnsNullWhenThereIsNoId(string input)
        {
            Assert.Null(GoodShortTitleMatcher.ExtractSeriesId(input));
        }

        [Fact]
        public void ExtractSeriesId_ReturnsNullForNull()
        {
            Assert.Null(GoodShortTitleMatcher.ExtractSeriesId(null));
        }

        [Theory]
        [InlineData(
            "https://www.goodshort.com/drama/dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548",
            "dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548")]
        [InlineData(
            "/episodes/abandonei-o-rei-dos-deuses-no-altar-31001543625",
            "abandonei-o-rei-dos-deuses-no-altar-31001543625")]
        [InlineData("abandonei-o-rei-dos-deuses-no-altar-31001543625", "abandonei-o-rei-dos-deuses-no-altar-31001543625")]
        public void ExtractSlug_ReadsThePageSegment(string input, string expected)
        {
            Assert.Equal(expected, GoodShortTitleMatcher.ExtractSlug(input));
        }

        [Theory]
        [InlineData("31001543625")]
        [InlineData("")]
        [InlineData("https://www.goodshort.com/")]
        public void ExtractSlug_ReturnsNullWhenNoSlugExists(string input)
        {
            Assert.Null(GoodShortTitleMatcher.ExtractSlug(input));
        }

        [Fact]
        public void Normalize_FoldsTheSeparatorsLibraryNamesRewrite()
        {
            Assert.Equal(
                GoodShortTitleMatcher.Normalize("3.2.1, Adeus e Ponto Final"),
                GoodShortTitleMatcher.Normalize("3.2 1, Adeus e Ponto Final"));
        }

        [Fact]
        public void Normalize_DropsTheDubbingMarkerOnBothSides()
        {
            // The platform brackets it and the library appends it, so the token must be noise in
            // both shapes or the two never compare equal.
            Assert.Equal(
                GoodShortTitleMatcher.Normalize("Abandonada pelo Don, Coroada pela Máfia"),
                GoodShortTitleMatcher.Normalize("[Dublado] Abandonada pelo Don, Coroada pela Máfia"));
            Assert.Equal(
                GoodShortTitleMatcher.Normalize("Abandonada pelo Don, Coroada pela Máfia"),
                GoodShortTitleMatcher.Normalize("Abandonada pelo Don, Coroada pela Máfia (Dublado)"));
        }

        [Fact]
        public void Normalize_DropsDiacriticsAndYears()
        {
            Assert.Equal(
                GoodShortTitleMatcher.Normalize("Abandonada pelo Don, Coroada pela Mafia"),
                GoodShortTitleMatcher.Normalize("Abandonada pelo Don, Coroada pela Máfia (2026)"));
        }

        [Fact]
        public void Similarity_ScoresAnExactPlatformTitleAgainstTheLibraryNameAsOne()
        {
            Assert.Equal(
                1.0,
                GoodShortTitleMatcher.Similarity(
                    "Abandonada pelo Don, Coroada pela Máfia (Dublado)",
                    "[Dublado] Abandonada pelo Don, Coroada pela Máfia"));
        }

        [Fact]
        public void Similarity_KeepsAFullContainmentMatchAboveTheAutoMatchBar()
        {
            // A library folder named after only part of the platform title must still clear a high
            // threshold, which is the job of the containment boost.
            var score = GoodShortTitleMatcher.Similarity(
                "Abandonei o Rei dos Deuses no Altar",
                "Abandonei o Rei dos Deuses no Altar - EP 1");

            Assert.True(score >= 0.92, $"Expected a confident score but got {score}.");
        }

        [Fact]
        public void Similarity_KeepsUnrelatedTitlesLow()
        {
            var score = GoodShortTitleMatcher.Similarity(
                "Abandonada pelo Don, Coroada pela Máfia",
                "One Coin Made Me King of the Apocalypse");

            Assert.True(score < 0.4, $"Expected a low score but got {score}.");
        }

        [Fact]
        public void Similarity_ReturnsZeroForUnusableInput()
        {
            Assert.Equal(0, GoodShortTitleMatcher.Similarity((string?)null, "Abandonada"));
            Assert.Equal(0, GoodShortTitleMatcher.Similarity("(2026)", "Abandonada"));
            Assert.Equal(0, GoodShortTitleMatcher.Similarity("Abandonada", string.Empty));
        }

        [Fact]
        public void CreateTokenSet_MatchesTheDirectScoringPath()
        {
            var tokens = GoodShortTitleMatcher.CreateTokenSet("[Dublado] Abandonada pelo Don, Coroada pela Máfia");

            Assert.Equal(
                GoodShortTitleMatcher.Similarity("Abandonada pelo Don, Coroada pela Máfia", "Abandonada pelo Don, Coroada pela Máfia"),
                GoodShortTitleMatcher.Similarity(tokens, "Abandonada pelo Don, Coroada pela Máfia"));
        }
    }
}
