using System.Linq;
using MediaBrowser.Providers.Plugins.Tmdb;
using Xunit;

namespace MulletaFlix.Providers.Tests.Tmdb
{
    public static class TmdbUtilsTests
    {
        [Theory]
        [InlineData("de", "de")]
        [InlineData("En", "En")]
        [InlineData("de-de", "de-DE")]
        [InlineData("en-US", "en-US")]
        [InlineData("de-CH", "de")]
        public static void NormalizeLanguage_Valid_Success(string input, string expected)
        {
            Assert.Equal(expected, TmdbUtils.NormalizeLanguage(input));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        public static void NormalizeLanguage_Invalid_Equal(string? input, string? expected)
        {
            Assert.Equal(expected, TmdbUtils.NormalizeLanguage(input!));
        }

        [Theory]
        // A regional request is only answered with a region when the image contradicts it.
        [InlineData("pt", "BR", "pt-BR", "pt-BR")]
        [InlineData("pt", "PT", "pt-BR", "pt-PT")]
        [InlineData("en", "GB", "en-US", "en-GB")]
        [InlineData("en", "US", "en-US", "en-US")]
        [InlineData("en", null, "en-US", "en-US")]
        // TMDb files nearly every image under a region, which must not stop a plain request matching.
        [InlineData("en", "US", "en", "en")]
        [InlineData("pt", "PT", "pt", "pt")]
        // An image in another language keeps its bare code, so the English fallback still resolves.
        [InlineData("en", "US", "pt-BR", "en")]
        [InlineData("de", "DE", "en-US", "de")]
        // TMDb returns xx rather than an empty string for an image with no text.
        [InlineData("xx", "US", "en-US", "")]
        [InlineData("", "US", "en-US", "")]
        [InlineData(null, null, "en-US", "")]
        public static void GetImageLanguage_Valid_Success(string? imageLanguage, string? imageRegion, string? requestLanguage, string expected)
        {
            Assert.Equal(expected, TmdbUtils.GetImageLanguage(imageLanguage, imageRegion, requestLanguage));
        }

        [Theory]
        [InlineData("pt-BR", "BR", "pt-BR,pt,null,en")]
        [InlineData("fr-CA", "CA", "fr-CA,fr,null,en")]
        [InlineData("ja-JP", "JP", "ja-JP,ja,null,en")]
        [InlineData("zh-Hant", "TW", "zh-HANT,zh,null,en")]
        [InlineData("es-419", "MX", "es-MX,es,null,en")]
        [InlineData("ar", "SA", "ar,null,en")]
        [InlineData("en-US", "US", "en-US,en,null")]
        [InlineData("", "", "null,en")]
        public static void GetImageLanguagesParam_IncludesRegionalAndBaseLanguageWithFallbacks(string language, string country, string expected)
        {
            Assert.Equal(expected, TmdbUtils.GetImageLanguagesParam(language, country));
        }

        [Fact]
        public static void GetImageLanguagesParam_IncludesAllLanguagesOnlyWhenRequested()
        {
            Assert.Equal(string.Empty, TmdbUtils.GetImageLanguagesParam("pt-BR", "BR", includeAllLanguages: true));
            Assert.Equal("pt-BR,pt,null,en", TmdbUtils.GetImageLanguagesParam("pt-BR", "BR", includeAllLanguages: false));
        }

        [Theory]
        [InlineData("pt-BR")]
        [InlineData("en-US")]
        [InlineData("ja-JP")]
        public static void GetImageQueryLanguage_OmitsLanguageFilterOnlyWhenAllLanguagesRequested(string language)
        {
            Assert.Equal(string.Empty, TmdbUtils.GetImageQueryLanguage(language, includeAllLanguages: true));
            Assert.Equal(language, TmdbUtils.GetImageQueryLanguage(language, includeAllLanguages: false));
        }

        [Fact]
        public static void TmdbImageCacheKey_DoesNotReuseResultsAcrossImageLanguagesOrCountries()
        {
            var portugueseBrazil = TmdbClientManager.CreateImageAwareCacheKey("movie", 42, "pt", "pt-BR,null,en", "BR");
            var portuguesePortugal = TmdbClientManager.CreateImageAwareCacheKey("movie", 42, "pt", "pt-PT,null,en", "PT");
            var english = TmdbClientManager.CreateImageAwareCacheKey("movie", 42, "en", "en,null", "US");

            Assert.NotEqual(portugueseBrazil, portuguesePortugal);
            Assert.NotEqual(portugueseBrazil, english);
        }

        [Fact]
        public static void BuildSearchNameVariants_StripsLeadingArticles_AndDiacritics()
        {
            var variants = TmdbUtils.BuildSearchNameVariants("A Fada dos Dentes").ToArray();

            Assert.Equal(new[] { "A Fada dos Dentes", "Fada dos Dentes" }, variants);
        }

        [Fact]
        public static void BuildSearchNameVariants_IncludesDiacriticsFreeVariant()
        {
            var variants = TmdbUtils.BuildSearchNameVariants("Árvore").ToArray();

            Assert.Equal(new[] { "Árvore", "Arvore" }, variants);
        }

        [Fact]
        public static void BuildSearchNameVariants_ExpandsFolderSafeEToAmpersand()
        {
            var variants = TmdbUtils.BuildSearchNameVariants("Bonnie e Clyde").ToArray();

            Assert.Contains("Bonnie e Clyde", variants);
            Assert.Contains("Bonnie & Clyde", variants);
        }

        [Fact]
        public static void BuildSearchNameVariants_ExpandsAmpersandToFolderSafeE()
        {
            var variants = TmdbUtils.BuildSearchNameVariants("Bonnie & Clyde").ToArray();

            Assert.Contains("Bonnie & Clyde", variants);
            Assert.Contains("Bonnie e Clyde", variants);
        }
    }
}
