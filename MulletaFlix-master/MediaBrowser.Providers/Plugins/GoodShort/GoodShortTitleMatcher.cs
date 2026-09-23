using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Normalizes and compares GoodShort titles against library folder names, and extracts the
    /// external id from the shapes a user may paste.
    /// </summary>
    /// <remarks>
    /// Deliberately free of I/O so it can be unit tested directly. The normalization rules are the
    /// ones the DramaBox matcher already proved necessary on this library and they apply unchanged
    /// to GoodShort: library folder names carry "(Dublado)" and "(2024)" markers the platform title
    /// does not, and a separator character is frequently rewritten to a space by the naming
    /// pipeline. GoodShort makes the dubbing marker a structural part of the title rather than an
    /// afterthought — "[Dublado] Abandonada pelo Don, Coroada pela Máfia" — so the noise token list
    /// matters more here than it did for DramaBox.
    /// </remarks>
    public static class GoodShortTitleMatcher
    {
        private static readonly HashSet<string> NoiseTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "dublado",
            "dublada",
            "legendado",
            "legendada",
            "original"
        };

        /// <summary>
        /// Normalizes a title into a comparable form: lowercase, without diacritics, with every
        /// punctuation and separator character turned into a space, and without noise tokens.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The normalized title, possibly empty.</returns>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(' ', Tokenize(value));
        }

        /// <summary>
        /// Splits a title into normalized comparison tokens.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The tokens.</returns>
        public static IReadOnlyList<string> Tokenize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var lowered = value.ToLowerInvariant();
            var decomposed = lowered.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                // Every separator becomes a space, so "3.2.1" and "3 2 1" normalize identically.
                // "Sinking With My Step-sister" uses U+2011, a non-breaking hyphen, which is not
                // char.IsPunctuation for char.IsLetterOrDigit purposes and therefore already folds
                // to a space through this branch.
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }

            var tokens = builder.ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !NoiseTokens.Contains(token))
                .Where(token => !IsYear(token))
                .ToList();

            return tokens;
        }

        /// <summary>
        /// Scores how similar two titles are.
        /// </summary>
        /// <param name="left">The first title.</param>
        /// <param name="right">The second title.</param>
        /// <returns>A score from 0 (unrelated) to 1 (identical after normalization).</returns>
        public static double Similarity(string? left, string? right)
        {
            var leftTokens = Tokenize(left);
            var rightTokens = Tokenize(right);

            if (leftTokens.Count == 0 || rightTokens.Count == 0)
            {
                return 0;
            }

            return SimilarityCore(
                new HashSet<string>(leftTokens, StringComparer.Ordinal),
                new HashSet<string>(rightTokens, StringComparer.Ordinal));
        }

        /// <summary>
        /// Builds the token set used by the scoring overload, so a caller that scores one title
        /// against many candidates tokenizes each candidate exactly once.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The token set.</returns>
        public static HashSet<string> CreateTokenSet(string? value)
        {
            return new HashSet<string>(Tokenize(value), StringComparer.Ordinal);
        }

        /// <summary>
        /// Scores a pre-tokenized title against another title.
        /// </summary>
        /// <param name="leftTokens">The pre-tokenized first title.</param>
        /// <param name="right">The second title.</param>
        /// <returns>A score from 0 to 1.</returns>
        public static double Similarity(IReadOnlyCollection<string> leftTokens, string? right)
        {
            if (leftTokens.Count == 0)
            {
                return 0;
            }

            var rightTokens = CreateTokenSet(right);
            if (rightTokens.Count == 0)
            {
                return 0;
            }

            return SimilarityCore(
                leftTokens as HashSet<string> ?? new HashSet<string>(leftTokens, StringComparer.Ordinal),
                rightTokens);
        }

        /// <summary>
        /// Extracts a GoodShort series id from any of the shapes a user may paste: a drama URL, an
        /// episode URL, an episodes URL, a bare slug, or a bare numeric id.
        /// </summary>
        /// <param name="value">The raw input.</param>
        /// <returns>The series id, or null when the input does not carry one.</returns>
        public static string? ExtractSeriesId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();

            // A bare id, which is what the Identify dialog gets when only the number is typed.
            if (IsSeriesId(trimmed))
            {
                return trimmed;
            }

            foreach (var segment in SplitSegments(trimmed))
            {
                // /drama/<slug>-<id>, /episodes/<slug>-<id> and /episode/<slug>-<id>/<n>-<episodeId>
                // all end their series segment with the id, so the trailing number is authoritative.
                var slug = ExtractSlugFromSegment(segment);
                if (slug is not null && TryReadTrailingId(slug, out var id))
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the goodshort.com slug of a series from a pasted URL or slug.
        /// </summary>
        /// <param name="value">The raw input.</param>
        /// <returns>The slug, or null when the input carries none.</returns>
        /// <remarks>
        /// The HTML fallback addresses a series page as /drama/&lt;slug&gt;-&lt;id&gt;, so the slug
        /// is kept whenever the user supplied one. The JSON path does not need it.
        /// </remarks>
        public static string? ExtractSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            foreach (var segment in SplitSegments(trimmed))
            {
                var slug = ExtractSlugFromSegment(segment);
                if (slug is not null && TryReadTrailingId(slug, out _))
                {
                    return slug;
                }
            }

            return null;
        }

        private static double SimilarityCore(HashSet<string> leftSet, HashSet<string> rightSet)
        {
            if (leftSet.SetEquals(rightSet))
            {
                return 1;
            }

            var intersection = leftSet.Count(token => rightSet.Contains(token));

            // Dice coefficient over token sets, matching the DramaBox matcher. Chosen over Jaccard
            // because titles frequently differ by one subtitle clause, and Dice degrades more
            // gracefully there.
            var dice = (2.0 * intersection) / (leftSet.Count + rightSet.Count);

            // Full containment means one title is a strict subset of the other, which is a much
            // stronger signal than the raw overlap suggests.
            if (intersection == leftSet.Count || intersection == rightSet.Count)
            {
                var smaller = Math.Min(leftSet.Count, rightSet.Count);
                var larger = Math.Max(leftSet.Count, rightSet.Count);
                var coverage = (double)smaller / larger;
                dice = Math.Max(dice, 0.85 + (0.15 * coverage));
            }

            return Math.Round(dice, 4);
        }

        private static IEnumerable<string> SplitSegments(string value)
        {
            // Accented and non-breaking hyphens appear inside real slugs, so only the ASCII
            // separators are treated as path separators here.
            return value.Split(new[] { '/', '?', '#' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string? ExtractSlugFromSegment(string segment)
        {
            var candidate = segment.Trim();

            // A pasted value may still carry a scheme or host when the split above produced one piece.
            if (candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return candidate.Length > 0 ? candidate : null;
        }

        private static bool IsSeriesId(string value)
        {
            if (value.Length < 6 || !value.All(char.IsAsciiDigit))
            {
                return false;
            }

            return true;
        }

        private static bool TryReadTrailingId(string slug, out string id)
        {
            id = string.Empty;

            var dash = slug.LastIndexOf('-');
            if (dash < 0 || dash == slug.Length - 1)
            {
                return false;
            }

            var candidate = slug.Substring(dash + 1);
            if (!IsSeriesId(candidate))
            {
                return false;
            }

            id = candidate;
            return true;
        }

        private static bool IsYear(string token)
        {
            return token.Length == 4
                && token.All(char.IsAsciiDigit)
                && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                && year >= 1900
                && year <= 2099;
        }
    }
}
