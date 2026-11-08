using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using MediaBrowser.Model.Configuration;
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
        [InlineData("en", "en-US", "en-US")]
        [InlineData("fr-CA", "fr-BE", "fr-CA")]
        [InlineData("fr-CA", "fr", "fr-CA")]
        [InlineData("de", "en-US", "de")]
        [InlineData("", "en-US", "")]
        public static void AdjustImageLanguage_Valid_Success(string imageLanguage, string requestLanguage, string? expected)
        {
            Assert.Equal(expected, TmdbUtils.AdjustImageLanguage(imageLanguage, requestLanguage));
        }

        [Theory]
        [InlineData("pt-BR", "BR", "pt,null,en")]
        [InlineData("en", "US", "en,null")]
        [InlineData("en-US", "US", "en,null")]
        [InlineData("", "BR", "null,en")]
        public static void GetImageLanguagesParam_PrefersRequestedLanguageAndRetainsFallbacks(string language, string countryCode, string expected)
        {
            Assert.Equal(expected, TmdbUtils.GetImageLanguagesParam(language, countryCode));
        }

        [Fact]
        public static void GetImageLanguagesParam_UsesPortugueseThenNullAndEnglishFallback()
        {
            var result = TmdbUtils.GetImageLanguagesParam("pt-BR", "BR");

            Assert.Equal("pt,null,en", result);
        }

        [Fact]
        public static void ServerConfiguration_DefaultsToBrazilianPortugueseWithEnglishImageFallback()
        {
            var configuration = new ServerConfiguration();

            Assert.Equal("pt-BR", configuration.PreferredMetadataLanguage);
            Assert.Equal("BR", configuration.MetadataCountryCode);
            Assert.Equal("pt,null,en", TmdbUtils.GetImageLanguagesParam(
                configuration.PreferredMetadataLanguage,
                configuration.MetadataCountryCode));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public static void ServerConfiguration_EmptyPreferredMetadataLanguageFallsBackToBrazilianPortuguese(string language)
        {
            var configuration = new ServerConfiguration
            {
                PreferredMetadataLanguage = language
            };

            Assert.Equal("pt-BR", configuration.PreferredMetadataLanguage);
            Assert.Equal("pt,null,en", TmdbUtils.GetImageLanguagesParam(
                configuration.PreferredMetadataLanguage,
                configuration.MetadataCountryCode));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public static void ServerConfiguration_DeserializingEmptyPreferredMetadataLanguageUsesBrazilianPortuguese(string language)
        {
            var serializer = new XmlSerializer(typeof(ServerConfiguration));
            using var reader = new StringReader($"<ServerConfiguration><PreferredMetadataLanguage>{language}</PreferredMetadataLanguage></ServerConfiguration>");
            var configuration = (ServerConfiguration)serializer.Deserialize(reader)!;

            Assert.Equal("pt-BR", configuration.PreferredMetadataLanguage);
            Assert.Equal("pt,null,en", TmdbUtils.GetImageLanguagesParam(
                configuration.PreferredMetadataLanguage,
                configuration.MetadataCountryCode));
        }

        [Fact]
        public static void ServerConfiguration_PreservesExplicitPreferredMetadataLanguage()
        {
            var configuration = new ServerConfiguration
            {
                PreferredMetadataLanguage = "en"
            };

            Assert.Equal("en", configuration.PreferredMetadataLanguage);
            Assert.Equal("en,null", TmdbUtils.GetImageLanguagesParam(
                configuration.PreferredMetadataLanguage,
                configuration.MetadataCountryCode));
        }

        [Fact]
        public static void GetImageLanguagesParam_ExcludesUnrequestedLanguages()
        {
            var result = TmdbUtils.GetImageLanguagesParam("pt-BR", "BR");

            Assert.DoesNotContain("ko", result, StringComparison.Ordinal);
            Assert.DoesNotContain("fr", result, StringComparison.Ordinal);
        }

        [Fact]
        public static void BuildCacheKey_DistinguishesRequestedImageLanguagesAndCountry()
        {
            var portuguese = TmdbClientManager.BuildCacheKey("movie", "123", "pt-BR", "pt,null,en", "BR");

            Assert.NotEqual(portuguese, TmdbClientManager.BuildCacheKey("movie", "123", "en", "pt,null,en", "BR"));
            Assert.NotEqual(portuguese, TmdbClientManager.BuildCacheKey("movie", "123", "pt-BR", "en,null", "BR"));
            Assert.NotEqual(portuguese, TmdbClientManager.BuildCacheKey("movie", "123", "pt-BR", "pt,null,en", "US"));
            Assert.Equal(portuguese, TmdbClientManager.BuildCacheKey("movie", "123", "pt-BR", "pt,null,en", "BR"));
        }

        [Theory]
        [InlineData("series-images", 1, "42", "pt,null,en", "en,null")]
        [InlineData("season-images", 2, "42", "1", "pt,null,en", "en,null")]
        [InlineData("episode", 5, "42", "1", "2", "dvd", "pt-BR", "pt,null,en", "BR", "en,null")]
        public static void BuildCacheKey_DistinguishesTvArtworkLanguageLists(string entity, int imageLanguagesIndex, string id, params string[] dimensions)
        {
            var preferredLanguageDimensions = new[] { id }.Concat(dimensions.Take(dimensions.Length - 1)).ToArray();
            var fallbackLanguageDimensions = (string[])preferredLanguageDimensions.Clone();
            fallbackLanguageDimensions[imageLanguagesIndex] = dimensions[^1];

            Assert.NotEqual(
                TmdbClientManager.BuildCacheKey(entity, preferredLanguageDimensions),
                TmdbClientManager.BuildCacheKey(entity, fallbackLanguageDimensions));
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
