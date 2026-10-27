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
                    //  - Images that match the requested language, including its ISO-639 base code
                    //  - TODO: Images that match the original language
                    //  - Images in English
                    //  - Images with no language
                    //  - Images that don't match the requested language

                    if (string.Equals(requestedLanguage, i.Language, StringComparison.OrdinalIgnoreCase))
                    {
                        return 5;
                    }

                    if (MatchesLanguage(requestedLanguage, i.Language))
                    {
                        return 4;
                    }

                    if (string.Equals(i.Language, "en", StringComparison.OrdinalIgnoreCase))
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
        /// Checks whether an image language matches a requested language, accepting regional variants.
        /// </summary>
        /// <param name="requestedLanguage">The requested language, optionally including a region.</param>
        /// <param name="imageLanguage">The image language.</param>
        /// <returns>True when both languages have the same ISO-639 language code.</returns>
        public static bool MatchesLanguage(string requestedLanguage, string? imageLanguage)
        {
            if (string.IsNullOrWhiteSpace(requestedLanguage) || string.IsNullOrWhiteSpace(imageLanguage))
            {
                return false;
            }

            if (string.Equals(requestedLanguage, imageLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var requestedBaseLanguage = requestedLanguage.Split('-', 2)[0];
            var imageBaseLanguage = imageLanguage.Split('-', 2)[0];
            return string.Equals(requestedBaseLanguage, imageBaseLanguage, StringComparison.OrdinalIgnoreCase);
        }
    }
}
