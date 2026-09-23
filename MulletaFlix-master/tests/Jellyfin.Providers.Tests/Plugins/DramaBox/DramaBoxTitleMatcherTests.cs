using MediaBrowser.Providers.Plugins.DramaBox;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaBox
{
    public class DramaBoxTitleMatcherTests
    {
        /// <summary>
        /// The regression this matcher was written for: the folder is "3.2.1, Adeus e Ponto Final"
        /// but the library item MulletaFlix created is named "3.2 1, Adeus e Ponto Final", because
        /// the naming pipeline turned the separator dot into a space. If separators are merely
        /// dropped instead of replaced by a space, these two never match.
        /// </summary>
        [Fact]
        public void Normalize_TreatsDotAndSpaceSeparatorsIdentically()
        {
            Assert.Equal(
                DramaBoxTitleMatcher.Normalize("3.2.1, Adeus e Ponto Final"),
                DramaBoxTitleMatcher.Normalize("3.2 1, Adeus e Ponto Final"));
        }

        [Fact]
        public void Similarity_IdenticalAfterNormalization_IsOne()
        {
            Assert.Equal(1.0, DramaBoxTitleMatcher.Similarity("3.2.1, Adeus e Ponto Final", "3.2 1, Adeus e Ponto Final"));
        }

        [Fact]
        public void Normalize_RemovesDiacritics()
        {
            Assert.Equal(
                DramaBoxTitleMatcher.Normalize("Cicatrizes da Traição"),
                DramaBoxTitleMatcher.Normalize("Cicatrizes da Traicao"));
        }

        [Theory]
        [InlineData("Reencontro à Esquina da Nova Vida (Dublado)", "Reencontro à Esquina da Nova Vida")]
        [InlineData("A Rainha Não Ama, Só Conquista (2024)", "A Rainha Não Ama, Só Conquista")]
        [InlineData("Amor na Primeira Neve (Legendado)", "Amor na Primeira Neve")]
        public void Normalize_IgnoresDubbingMarkersAndYears(string libraryName, string dramaBoxName)
        {
            Assert.Equal(
                DramaBoxTitleMatcher.Normalize(libraryName),
                DramaBoxTitleMatcher.Normalize(dramaBoxName));
        }

        /// <summary>
        /// A library folder named after only part of the DramaBox title must still clear the
        /// automatic match threshold used by the scheduled task.
        /// </summary>
        [Fact]
        public void Similarity_SubtitleContainedInFullTitle_ClearsAutomaticThreshold()
        {
            var score = DramaBoxTitleMatcher.Similarity("Adeus e Ponto Final", "3.2.1, Adeus e Ponto Final");

            Assert.True(score >= 0.92, $"Expected at least 0.92 but got {score}.");
        }

        [Fact]
        public void Similarity_UnrelatedTitles_ScoresLow()
        {
            var score = DramaBoxTitleMatcher.Similarity(
                "Chuva Negra",
                "Renascida das Cinzas: A Vingança contra Meus Irmãos");

            Assert.True(score < 0.5, $"Expected below 0.5 but got {score}.");
        }

        [Fact]
        public void Similarity_SharedLeadingWordOnly_ScoresBelowAutomaticThreshold()
        {
            // Both titles contain "Adeus", and a partial overlap must never be enough to
            // auto-identify a series.
            var score = DramaBoxTitleMatcher.Similarity("Adeus ao Casamento que Não Era Meu", "Adeus, Terra");

            Assert.True(score < 0.92, $"Expected below 0.92 but got {score}.");
        }

        [Theory]
        [InlineData("https://www.dramabox.com/pt/drama/42000002641/321-Adeus-e-Ponto-Final", "42000002641")]
        [InlineData("https://www.dramabox.com/pt/drama/42000002641", "42000002641")]
        [InlineData("https://www.dramabox.com/pt/video/42000002641_3-2-1-Farewell-Forever/700157514_Episode-1", "42000002641")]
        [InlineData("42000002641", "42000002641")]
        public void ExtractBookId_ResolvesEveryAcceptedInputShape(string input, string expected)
        {
            Assert.Equal(expected, DramaBoxTitleMatcher.ExtractBookId(input));
        }

        [Theory]
        [InlineData("3.2.1, Adeus e Ponto Final")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ExtractBookId_ReturnsNullWhenInputCarriesNoId(string? input)
        {
            Assert.Null(DramaBoxTitleMatcher.ExtractBookId(input));
        }

        [Fact]
        public void BuildCoverUrl_ReplacesTheCdnTransformSuffix()
        {
            var result = DramaBoxTitleMatcher.BuildCoverUrl(
                "https://thwztchapter.dramaboxdb.com/data/cppartner/4x2/42x0/420x0/42000004501/42000004501.jpg@w=360&h=640",
                720,
                1280);

            Assert.Equal(
                "https://thwztchapter.dramaboxdb.com/data/cppartner/4x2/42x0/420x0/42000004501/42000004501.jpg@w=720&h=1280",
                result);
        }

        [Fact]
        public void BuildCoverUrl_AddsATransformWhenTheUrlHasNone()
        {
            var result = DramaBoxTitleMatcher.BuildCoverUrl("https://example.invalid/cover.jpg", 480, 640);

            Assert.Equal("https://example.invalid/cover.jpg@w=480&h=640", result);
        }

        [Fact]
        public void BuildCoverUrl_EmptyInputStaysEmpty()
        {
            Assert.Equal(string.Empty, DramaBoxTitleMatcher.BuildCoverUrl(null, 720, 1280));
        }
    }
}
