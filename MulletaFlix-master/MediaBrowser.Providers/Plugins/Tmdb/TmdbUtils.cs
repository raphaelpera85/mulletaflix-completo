using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions;
using MediaBrowser.Model.Entities;
using TMDbLib.Objects.General;

namespace MediaBrowser.Providers.Plugins.Tmdb
{
    /// <summary>
    /// Utilities for the TMDb provider.
    /// </summary>
    public static partial class TmdbUtils
    {
        private static readonly Regex StandaloneConjunctionRegex = new(
            @"(?<!\p{L})e(?!\p{L})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// URL of the TMDb instance to use.
        /// </summary>
        public const string BaseTmdbUrl = "https://www.themoviedb.org/";

        /// <summary>
        /// Name of the provider.
        /// </summary>
        public const string ProviderName = "TheMovieDb";

        /// <summary>
        /// API key to use when performing an API call.
        /// </summary>
        public const string ApiKey = "4219e299c89411838049ab0dab19ebd5";

        /// <summary>
        /// The crew types to keep.
        /// </summary>
        public static readonly string[] WantedCrewTypes =
        {
            PersonType.Director,
            PersonType.Writer,
            PersonType.Producer
        };

        /// <summary>
        /// The crew kinds to keep.
        /// </summary>
        public static readonly PersonKind[] WantedCrewKinds =
        {
            PersonKind.Director,
            PersonKind.Writer,
            PersonKind.Producer
        };

        [GeneratedRegex(@"[\W_-[Â·]]+")]
        private static partial Regex NonWordRegex();

        /// <summary>
        /// Cleans the name according to TMDb requirements.
        /// </summary>
        /// <param name="name">The name of the entity.</param>
        /// <returns>The cleaned name.</returns>
        public static string CleanName(string name)
        {
            // TMDb expects a space separated list of words make sure that is the case
            return NonWordRegex().Replace(name, " ");
        }

        /// <summary>
        /// Builds ordered title variants for TMDb search requests.
        /// </summary>
        /// <param name="name">The source title.</param>
        /// <returns>The ordered search variants.</returns>
        public static IEnumerable<string> BuildSearchNameVariants(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var variants = new[]
            {
                name,
                CleanName(name),
                CleanName(name).RemoveDiacritics()
            };

            foreach (var variant in variants)
            {
                foreach (var normalizedVariant in ExpandSearchVariant(variant))
                {
                    if (!string.IsNullOrWhiteSpace(normalizedVariant) && seen.Add(normalizedVariant))
                    {
                        yield return normalizedVariant;
                    }
                }
            }
        }

        private static IEnumerable<string> ExpandSearchVariant(string name)
        {
            var normalized = Regex.Replace(name, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            yield return normalized;

            // Pastas de mídia costumam substituir '&' por 'e'. Envie também a
            // grafia original para que o TMDb encontre títulos como "Velozes e
            // Furiosos" / "Fast & Furious", sem alterar o nome salvo da pasta.
            var ampersandVariant = StandaloneConjunctionRegex.Replace(normalized, "&");
            ampersandVariant = Regex.Replace(ampersandVariant, @"\s*&\s*", " & ", RegexOptions.CultureInvariant).Trim();
            if (!string.Equals(ampersandVariant, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return ampersandVariant;
            }

            var conjunctionVariant = Regex.Replace(normalized, @"\s*&\s*", " e ", RegexOptions.CultureInvariant).Trim();
            if (!string.Equals(conjunctionVariant, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return conjunctionVariant;
            }

            var withoutLeadingArticle = RemoveLeadingPortugueseArticle(normalized);
            if (!string.Equals(withoutLeadingArticle, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return withoutLeadingArticle;
            }
        }

        private static string RemoveLeadingPortugueseArticle(string name)
        {
            var firstSpace = name.IndexOf(' ', StringComparison.Ordinal);
            if (firstSpace <= 0 || firstSpace == name.Length - 1)
            {
                return name;
            }

            var firstWord = name[..firstSpace];
            if (!string.Equals(firstWord, "a", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(firstWord, "o", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(firstWord, "as", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(firstWord, "os", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            return name[(firstSpace + 1)..];
        }

        /// <summary>
        /// Maps the TMDb provided roles for crew members to MulletaFlix roles.
        /// </summary>
        /// <param name="crew">Crew member to map against the MulletaFlix person types.</param>
        /// <returns>The MulletaFlix person type.</returns>
        public static PersonKind MapCrewToPersonType(Crew crew)
        {
            if (string.Equals(crew.Department, "directing", StringComparison.OrdinalIgnoreCase)
                && string.Equals(crew.Job, "director", StringComparison.OrdinalIgnoreCase))
            {
                return PersonKind.Director;
            }

            if (string.Equals(crew.Department, "production", StringComparison.OrdinalIgnoreCase)
                && string.Equals(crew.Job, "producer", StringComparison.OrdinalIgnoreCase))
            {
                return PersonKind.Producer;
            }

            if (string.Equals(crew.Department, "writing", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(crew.Job, "writer", StringComparison.OrdinalIgnoreCase) || string.Equals(crew.Job, "screenplay", StringComparison.OrdinalIgnoreCase)))
            {
                return PersonKind.Writer;
            }

            return PersonKind.Unknown;
        }

        /// <summary>
        /// Determines whether a video is a trailer.
        /// </summary>
        /// <param name="video">The TMDb video.</param>
        /// <returns>A boolean indicating whether the video is a trailer.</returns>
        public static bool IsTrailerType(Video video)
        {
            return string.Equals(video.Site, "youtube", StringComparison.OrdinalIgnoreCase)
                   && (string.Equals(video.Type, "trailer", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(video.Type, "teaser", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Normalizes a language string for use with TMDb's include image language parameter.
        /// </summary>
        /// <param name="preferredLanguage">The preferred language as either a 2 letter code with or without country code.</param>
        /// <param name="countryCode">The country code, ISO 3166-1.</param>
        /// <returns>The comma separated language string.</returns>
        public static string GetImageLanguagesParam(string preferredLanguage, string? countryCode = null)
        {
            var languages = new List<string>();

            if (!string.IsNullOrEmpty(preferredLanguage))
            {
                preferredLanguage = NormalizeLanguage(preferredLanguage, countryCode);

                languages.Add(preferredLanguage);

                // TMDb image records use ISO-639-1 language codes; regional variants
                // such as pt-BR must also request the base language (pt).
                var baseLanguage = preferredLanguage.Split('-')[0];
                if (!string.Equals(preferredLanguage, baseLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    languages.Add(baseLanguage);
                }
            }

            languages.Add("null");

            // Always add English as fallback language
            if (!string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase))
            {
                languages.Add("en");
            }

            return string.Join(',', languages.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the TMDb image language parameter, or an empty string to omit it.
        /// </summary>
        /// <param name="preferredLanguage">The preferred metadata language.</param>
        /// <param name="countryCode">The preferred metadata country code.</param>
        /// <param name="includeAllLanguages">Whether to include images in every language.</param>
        /// <returns>The image language parameter, or an empty string when all languages are requested.</returns>
        public static string GetImageLanguagesParam(string preferredLanguage, string? countryCode, bool includeAllLanguages)
            => includeAllLanguages ? string.Empty : GetImageLanguagesParam(preferredLanguage, countryCode);

        /// <summary>
        /// Gets the TMDb query language, or an empty string to avoid filtering images by language.
        /// </summary>
        /// <param name="preferredLanguage">The preferred metadata language.</param>
        /// <param name="includeAllLanguages">Whether images in every language should be returned.</param>
        /// <returns>The preferred metadata language, or an empty string when all languages are requested.</returns>
        public static string GetImageQueryLanguage(string preferredLanguage, bool includeAllLanguages)
            => includeAllLanguages ? string.Empty : preferredLanguage;

        /// <summary>
        /// Normalizes a language string for use with TMDb's language parameter.
        /// </summary>
        /// <param name="language">The language code.</param>
        /// <param name="countryCode">The country code.</param>
        /// <returns>The normalized language code.</returns>
        [return: NotNullIfNotNull(nameof(language))]
        public static string? NormalizeLanguage(string? language, string? countryCode = null)
        {
            if (string.IsNullOrEmpty(language))
            {
                return language;
            }

            // Handle es-419 (Latin American Spanish) by converting to regional variant
            if (string.Equals(language, "es-419", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(countryCode))
            {
                language = string.Equals(countryCode, "AR", StringComparison.OrdinalIgnoreCase)
                    ? "es-AR"
                    : "es-MX";
            }

            // TMDb requires this to be uppercase
            // Everything after the hyphen must be written in uppercase due to a way TMDb wrote their API.
            // See here: https://www.themoviedb.org/talk/5119221d760ee36c642af4ad?page=3#56e372a0c3a3685a9e0019ab
            var parts = language.Split('-');

            if (parts.Length == 2)
            {
                // TMDb doesn't support Switzerland (de-CH, it-CH or fr-CH) so use the language (de, it or fr) without country code
                if (string.Equals(parts[1], "CH", StringComparison.OrdinalIgnoreCase))
                {
                    return parts[0];
                }

                language = parts[0] + "-" + parts[1].ToUpperInvariant();
            }

            return language;
        }

        /// <summary>
        /// Determines the language code to report for an image.
        /// </summary>
        /// <remarks>
        /// TMDb keeps an image's region in iso_3166_1, which is what separates a pt-PT poster from a pt-BR
        /// one. The region is added to the returned code only when it differs from the requested one, as in
        /// a pt-PT image for a pt-BR request. TMDb files nearly every image under a region, so adding it
        /// unconditionally would return en-US for an image that has to keep matching a plain "en" request.
        /// </remarks>
        /// <param name="imageLanguage">The image's ISO 639-1 language code.</param>
        /// <param name="imageRegion">The image's ISO 3166-1 country code.</param>
        /// <param name="requestLanguage">The requested language code.</param>
        /// <returns>The language code.</returns>
        public static string GetImageLanguage(string? imageLanguage, string? imageRegion, string? requestLanguage)
        {
            // TMDb now returns xx for no language instead of an empty string.
            if (string.IsNullOrEmpty(imageLanguage) || string.Equals(imageLanguage, "xx", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(requestLanguage))
            {
                return imageLanguage;
            }

            var requestParts = requestLanguage.Split('-');

            if (requestParts.Length != 2 || !string.Equals(requestParts[0], imageLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return imageLanguage;
            }

            return string.IsNullOrEmpty(imageRegion) || string.Equals(imageRegion, requestParts[1], StringComparison.OrdinalIgnoreCase)
                ? requestLanguage
                : imageLanguage + "-" + imageRegion.ToUpperInvariant();
        }

        /// <summary>
        /// Combines the metadata country code and the parental rating from the API into the value we store in our database.
        /// </summary>
        /// <param name="countryCode">The ISO 3166-1 country code of the rating country.</param>
        /// <param name="ratingValue">The rating value returned by the TMDb API.</param>
        /// <returns>The combined parental rating of country code+rating value.</returns>
        public static string BuildParentalRating(string countryCode, string ratingValue)
        {
            // Exclude US because we store US values as TV-14 without the country code.
            var ratingPrefix = string.Equals(countryCode, "US", StringComparison.OrdinalIgnoreCase) ? string.Empty : countryCode + "-";
            var newRating = ratingPrefix + ratingValue;

            return newRating.Replace("DE-", "FSK-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
