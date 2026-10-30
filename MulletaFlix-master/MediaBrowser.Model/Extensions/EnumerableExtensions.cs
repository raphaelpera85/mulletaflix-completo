using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Model.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Orders <see cref="RemoteImageInfo"/> by requested language in descending order, then "en", then no language, over other non-matches.
        /// </summary>
        /// <param name="remoteImageInfos">The remote image infos.</param>
        /// <param name="requestedLanguage">The requested language for the images.</param>
        /// <returns>The ordered remote image infos.</returns>
        public static IEnumerable<RemoteImageInfo> OrderByLanguageDescending(this IEnumerable<RemoteImageInfo> remoteImageInfos, string requestedLanguage)
        {
            if (string.IsNullOrWhiteSpace(requestedLanguage))
            {
                // Default to English if no requested language is specified.
                requestedLanguage = "en";
            }

            return remoteImageInfos.OrderByDescending(i =>
                {
                    // Image priority ordering:
                    //  - Images that match the requested locale
                    //  - Images that match the requested base language
                    //  - Images in English
                    //  - Images with no language
                    //  - Images that don't match the requested language

                    if (IsLanguageMatch(i.Language, requestedLanguage))
                    {
                        return string.Equals(NormalizeLanguageTag(i.Language), NormalizeLanguageTag(requestedLanguage), StringComparison.OrdinalIgnoreCase) ? 5 : 4;
                    }

                    if (IsLanguageMatch(i.Language, "en"))
                    {
                        return 3;
                    }

                    if (string.IsNullOrEmpty(i.Language))
                    {
                        return 2;
                    }

                    return 0;
                })
                .ThenByDescending(i => Math.Round(i.CommunityRating ?? 0, 1))
                .ThenByDescending(i => i.VoteCount ?? 0);
        }

        /// <summary>
        /// Checks whether an image language is the requested language, including regional variants.
        /// </summary>
        /// <param name="imageLanguage">The image language.</param>
        /// <param name="requestedLanguage">The requested language.</param>
        /// <returns><c>true</c> when both tags use the same language subtag.</returns>
        public static bool IsLanguageMatch(string? imageLanguage, string? requestedLanguage)
        {
            if (string.IsNullOrWhiteSpace(imageLanguage) || string.IsNullOrWhiteSpace(requestedLanguage))
            {
                return false;
            }

            var normalizedImageLanguage = NormalizeLanguageTag(imageLanguage);
            var normalizedRequestedLanguage = NormalizeLanguageTag(requestedLanguage);

            return string.Equals(normalizedImageLanguage, normalizedRequestedLanguage, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    normalizedImageLanguage.Split('-')[0],
                    normalizedRequestedLanguage.Split('-')[0],
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeLanguageTag(string language)
        {
            return language.Replace('_', '-').Trim();
        }
    }
}
