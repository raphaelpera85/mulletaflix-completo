using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Normalizes and compares NartoDrama titles against library folder names, and extracts the
    /// series slug from the shapes a user may paste.
    /// </summary>
    /// <remarks>
    /// Deliberately free of I/O so it can be unit tested directly. The normalization rules are the
    /// ones the DramaBox and GoodShort matchers already proved necessary on this same library, and
    /// they carry over unchanged: a library folder name carries "(Dublado)" / "(2024)" markers the
    /// platform title does not, and the naming pipeline rewrites separator characters. NartoDrama
    /// makes the dubbing marker structural rather than decorative — the catalogue really contains
    /// both "Abandonei o Rei dos Deuses no Altar" and
    /// "[Dublado] Abandonei o Rei dos Deuses no Altar", and both are distinct series with distinct
    /// slugs — so the marker is dropped for comparison and the two are separated by their scores
    /// rather than by an exact match.
    /// </remarks>
    public static class NartoDramaTitleMatcher
    {
        /// <summary>
        /// The marker segment the site appends to the title it publishes, for example "- Streaming grátis".
        /// </summary>
        private const string StreamingMarker = "streaming";

        /// <summary>
        /// The characters that end a path segment in the shapes a user may paste.
        /// </summary>
        private static readonly char[] QuerySeparators = { '?', '#' };

        private static readonly HashSet<string> NoiseTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "dublado",
            "dublada",
            "legendado",
            "legendada",
            "original"
        };

        /// <summary>
        /// Matches a bare slug: lowercase alphanumeric words joined by hyphens, with at least one
        /// hyphen. Every slug in the live catalogue has this shape.
        /// </summary>
        private static readonly Regex SlugRegex = new Regex(
            "^[a-z0-9]+(?:-[a-z0-9]+)+$",
            RegexOptions.CultureInvariant);

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

                // Every separator becomes a space, so "3.2.1" and "3 2 1" normalize identically, and
                // the "[Dublado]" / "(Dublado)" wrapper NartoDrama uses for its dubbed catalogue
                // simply becomes another token the noise list can drop.
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
        /// Removes the marketing suffix the site appends to the title it publishes.
        /// </summary>
        /// <param name="value">The raw title, for example "Abandonei o Rei dos Deuses no Altar - Streaming grátis".</param>
        /// <returns>The clean title, or the input unchanged when it carries no such suffix.</returns>
        /// <remarks>
        /// The suffix is localized, so it is matched by shape instead of by literal text. Measured
        /// on the live site against the same series: "- Streaming grátis" (pt-PT), "- Streaming
        /// gratis" (es-ES), "- Streaming Gratis" (pt-BR) and "- Free Streaming" (en-US). Each of
        /// them normalizes to a marker whose first token is "streaming", or to "free streaming".
        /// </remarks>
        public static string StripStreamingSuffix(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var separator = trimmed.LastIndexOf(" - ", StringComparison.Ordinal);
            if (separator <= 0)
            {
                return trimmed;
            }

            var marker = trimmed.Substring(separator + 3);
            return IsStreamingMarker(marker) ? trimmed.Substring(0, separator).Trim() : trimmed;
        }

        /// <summary>
        /// Extracts a NartoDrama slug from any of the shapes a user may paste: a detail URL, an
        /// episode URL, a bare slug, or the value of a <c>data-watch-url</c> attribute.
        /// </summary>
        /// <param name="value">The raw input.</param>
        /// <returns>The slug, or null when the input does not carry one.</returns>
        public static string? ExtractSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();

            // https://narto-drama.com/detail/watch/<slug> and https://narto-drama.com/detail/watch/<slug>/<episode>
            if (trimmed.Contains('/', StringComparison.Ordinal))
            {
                var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    if (!segments[i].Equals("watch", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var candidate = Uri.UnescapeDataString(StripQuery(segments[i + 1]));
                    if (IsSlugCandidate(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            // A bare slug. The shape check is deliberately strict: a single word library title such
            // as "Vingança" is far more likely to be a search term than a slug, and treating it as a
            // slug would cost one wasted request per library item on every refresh.
            var bare = Uri.UnescapeDataString(StripQuery(trimmed));
            return SlugRegex.IsMatch(bare) ? bare : null;
        }

        private static double SimilarityCore(HashSet<string> leftSet, HashSet<string> rightSet)
        {
            if (leftSet.SetEquals(rightSet))
            {
                return 1;
            }

            var intersection = leftSet.Count(token => rightSet.Contains(token));

            // Dice coefficient over token sets, matching the DramaBox and GoodShort matchers.
            // Chosen over Jaccard because titles frequently differ by one subtitle clause, and Dice
            // degrades more gracefully there.
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

        private static bool IsStreamingMarker(string marker)
        {
            var tokens = Tokenize(marker);
            if (tokens.Count == 0)
            {
                return false;
            }

            if (string.Equals(tokens[0], StreamingMarker, StringComparison.Ordinal))
            {
                return true;
            }

            // The English variant puts the words the other way round: "Free Streaming".
            return tokens.Count == 2
                && string.Equals(tokens[0], "free", StringComparison.Ordinal)
                && string.Equals(tokens[1], StreamingMarker, StringComparison.Ordinal);
        }

        private static string StripQuery(string segment)
        {
            var cut = segment.IndexOfAny(QuerySeparators);
            return cut >= 0 ? segment.Substring(0, cut) : segment;
        }

        private static bool IsSlugCandidate(string value)
        {
            if (value.Length < 3)
            {
                return false;
            }

            // The episode number that follows the slug in an episode URL is never a slug.
            return !value.All(char.IsAsciiDigit);
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
