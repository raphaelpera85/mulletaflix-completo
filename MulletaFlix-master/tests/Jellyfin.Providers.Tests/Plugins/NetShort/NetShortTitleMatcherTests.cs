using System;
using MediaBrowser.Providers.Plugins.NetShort;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NetShort
{
    /// <summary>
    /// Checks the NetShort id extraction and title scoring.
    /// </summary>
    /// <remarks>
    /// Every URL below is one the live site serves. The episode and full-episodes routes carry the
    /// series id as the trailing number of the last segment, and the drama route is shared with
    /// category pages, so the id has to be read positionally rather than by route.
    /// </remarks>
    public class NetShortTitleMatcherTests
    {
        [Theory]
        [InlineData("2082282581191360513", "2082282581191360513")]
        [InlineData(" 2082282581191360513 ", "2082282581191360513")]
        [InlineData("https://netshort.com/pt/hotseries/além-do-99-perdões-2082282581191360513", "2082282581191360513")]
        [InlineData("https://netshort.com/pt/hotseries/al%C3%A9m-do-99-perd%C3%B5es-2082282581191360513", "2082282581191360513")]
        [InlineData("https://netshort.com/pt/episode/além-do-99-perdões-2082282581191360513", "2082282581191360513")]
        [InlineData("https://netshort.com/pt/episode/além-do-99-perdões-2082282581191360513-ep-2", "2082282581191360513")]
        [InlineData("https://netshort.com/pt/full-episodes/além-do-99-perdões-2082282581191360513", "2082282581191360513")]
        [InlineData("https://netshort.com/pt/drama/Deus%20da%20Guerra-1983832036122378248", "1983832036122378248")]
        [InlineData("https://netshort.com/pt/episode/perdão-até-quando-1927680500222066690-ep-12", "1927680500222066690")]
        [InlineData("https://netshort.com/pt/full-episodes/2082282581191360513?from=share", "2082282581191360513")]
        public void ExtractSeriesId_ReadsEveryShapeTheSiteServes(string input, string expected)
        {
            Assert.Equal(expected, NetShortTitleMatcher.ExtractSeriesId(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Além do 99 Perdões")]
        [InlineData("https://netshort.com/pt/hotseries")]
        [InlineData("12345")]
        public void ExtractSeriesId_ReturnsNullWhenThereIsNoId(string? input)
        {
            Assert.Null(NetShortTitleMatcher.ExtractSeriesId(input));
        }

        [Fact]
        public void Normalize_FoldsDiacriticsAndPunctuation()
        {
            // The two real spellings MulletaFlix ends up storing for the same title.
            Assert.Equal("alem do 99 perdoes", NetShortTitleMatcher.Normalize("Além do 99 Perdões"));
            Assert.Equal(
                NetShortTitleMatcher.Normalize("3.2.1, Adeus e Ponto Final"),
                NetShortTitleMatcher.Normalize("3 2 1 Adeus e Ponto Final"));
        }

        [Fact]
        public void Normalize_DropsTheMarkersTheNamingPipelineAdds()
        {
            Assert.Equal(
                NetShortTitleMatcher.Normalize("Além do 99 Perdões"),
                NetShortTitleMatcher.Normalize("Além do 99 Perdões (Dublado) (2026)"));
        }

        [Fact]
        public void Similarity_ScoresAnExactTitleHighest()
        {
            Assert.Equal(1.0, NetShortTitleMatcher.Similarity("Além do 99 Perdões", "Além do 99 Perdões"));
            Assert.Equal(1.0, NetShortTitleMatcher.Similarity("Além do 99 Perdões", "alem do 99 perdoes"));
        }

        [Fact]
        public void Similarity_KeepsUnrelatedTitlesApart()
        {
            var score = NetShortTitleMatcher.Similarity("Além do 99 Perdões", "Sem Perdão no Apocalipse Zumbi");

            Assert.True(score < 0.5, $"Unrelated titles scored {score}.");
        }

        [Fact]
        public void Similarity_TreatsAContainedTitleAsAStrongMatch()
        {
            // "Perdão, Até Quando?" against a library name carrying an extra clause.
            var score = NetShortTitleMatcher.Similarity(
                "Perdão, Até Quando? A Vingança",
                "Perdão, Até Quando?");

            Assert.True(score >= 0.85, $"A contained title only scored {score}.");
        }

        [Fact]
        public void Similarity_IsZeroWhenEitherSideIsEmpty()
        {
            Assert.Equal(0, NetShortTitleMatcher.Similarity(null, "Além do 99 Perdões"));
            Assert.Equal(0, NetShortTitleMatcher.Similarity("Além do 99 Perdões", "   "));
        }

        [Fact]
        public void CreateTokenSet_IsWhatTheScoringOverloadExpects()
        {
            var tokens = NetShortTitleMatcher.CreateTokenSet("Além do 99 Perdões (Dublado)");

            Assert.Contains("perdoes", tokens);
            Assert.DoesNotContain("dublado", tokens);
            Assert.Equal(4, tokens.Count);
        }

        [Fact]
        public void ResolveSeriesId_IsTheClientEntryPoint()
        {
            Assert.Equal(
                "2082282581191360513",
                NetShortClient.ResolveSeriesId("https://netshort.com/pt/full-episodes/além-do-99-perdões-2082282581191360513"));
        }

        [Fact]
        public void Normalize_KeepsTheFourDigitYearGuardFromLosingRealTitles()
        {
            // A year-shaped token is dropped, but only when it really is a plausible year.
            Assert.Equal("duna", NetShortTitleMatcher.Normalize("Duna 2021"));
            Assert.Equal("agente 007", NetShortTitleMatcher.Normalize("Agente 007"));
        }

        /// <summary>
        /// Guards against a percent-encoded dash sequence being decoded after the id was read.
        /// </summary>
        [Fact]
        public void ExtractSeriesId_DecodesBeforeReadingTheTail()
        {
            // Encoding every dash proves the decode happens first: reading the tail of the raw
            // segment would return "%2D2082282581191360513".
            Assert.Equal(
                "2082282581191360513",
                NetShortTitleMatcher.ExtractSeriesId("https://netshort.com/pt/hotseries/al%C3%A9m%2Ddo%2D99%2Dperd%C3%B5es%2D2082282581191360513"));
        }

        /// <summary>
        /// The extractor must ignore a query string that carries another number.
        /// </summary>
        [Fact]
        public void ExtractSeriesId_ScansSegmentsRatherThanTheWholeInput()
        {
            Assert.Equal(
                "2082282581191360513",
                NetShortTitleMatcher.ExtractSeriesId("https://netshort.com/pt/full-episodes/além-do-99-perdões-2082282581191360513?ref=9999999999"));
        }

        [Fact]
        public void ExtractSeriesId_IgnoresANonNumericTailLikeTheSiteProduces()
        {
            // /pt/hotseries/page/2 has a numeric tail that is a page number, not a snowflake id.
            Assert.Null(NetShortTitleMatcher.ExtractSeriesId("https://netshort.com/pt/hotseries/page/2"));
        }

        [Fact]
        public void Similarity_IsSymmetric()
        {
            const string Left = "Além do Perdão";
            const string Right = "Além do 99 Perdões";

            Assert.Equal(
                NetShortTitleMatcher.Similarity(Left, Right),
                NetShortTitleMatcher.Similarity(Right, Left));
        }
    }
}
