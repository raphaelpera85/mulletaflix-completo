using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Normalizes NetShort titles and extracts a series id from anything a user may paste.
    /// </summary>
    /// <remarks>
    /// This is deliberately free of I/O so it can be unit tested directly. The normalization rules
    /// are the ones already validated against the real MulletaFlix library by
    /// <c>MediaBrowser.Providers.Plugins.DramaBox.DramaBoxTitleMatcher</c>: lowercase, diacritics
    /// folded, every punctuation character turned into a space, and the "(Dublado)" / "(2024)"
    /// markers the naming pipeline adds stripped out. They are repeated here rather than shared,
    /// because the NetShort provider owns its own folder and must not reach into the sibling's.
    /// </remarks>
    public static class NetShortTitleMatcher
    {
        /// <summary>
        /// NetShort appends the episode number as "-ep-N" to an episode URL, which has to go before
        /// the series id can be read.
        /// </summary>
        private const string EpisodeMarker = "-ep-";

        private static readonly HashSet<string> NoiseTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "dublado",
            "dublada",
            "legendado",
            "legendada",
            "original"
        };

        /// <summary>
        /// Extracts a NetShort series id from any of the shapes a user may paste: a series, episode,
        /// hotseries or full-episodes URL, or a bare numeric id.
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

            var segments = trimmed.Split(new[] { '/', '?', '#' }, StringSplitOptions.RemoveEmptyEntries);

            // The id is always the last number of a path segment: the trailing "-<digits>" of a
            // slug, or a segment that is nothing but the id. NetShort routes cover /drama/,
            // /episode/, /full-episodes/ and /hotseries/ and every one of them ends in the same
            // number. The id-only shape is what the rendered-page fallback and the JSON-LD @id use.
            for (var i = segments.Length - 1; i >= 0; i--)
            {
                var candidate = Decode(segments[i]);
                if (IsSeriesId(candidate))
                {
                    return candidate;
                }

                var markerIndex = candidate.IndexOf(EpisodeMarker, StringComparison.Ordinal);
                if (markerIndex > 0)
                {
                    candidate = candidate.Substring(0, markerIndex);
                }

                var dash = candidate.LastIndexOf('-');
                if (dash <= 0)
                {
                    continue;
                }

                var tail = candidate.Substring(dash + 1);
                if (IsSeriesId(tail))
                {
                    return tail;
                }
            }

            return null;
        }

        /// <summary>
        /// Normalizes a title into a comparable form: lowercase, without diacritics, with every
        /// punctuation and separator character turned into a space, and without noise tokens.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The normalized title, possibly empty.</returns>
        public static string Normalize(string? value)
        {
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

            var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                // Every separator becomes a space, so "3.2.1" and "3 2 1" normalize identically.
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }

            return builder.ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !NoiseTokens.Contains(token))
                .Where(token => !IsYear(token))
                .ToList();
        }

        /// <summary>
        /// Scores how similar two titles are.
        /// </summary>
        /// <param name="left">The first title.</param>
        /// <param name="right">The second title.</param>
        /// <returns>A score from 0 (unrelated) to 1 (identical after normalization).</returns>
        public static double Similarity(string? left, string? right)
        {
            var leftTokens = CreateTokenSet(left);
            var rightTokens = CreateTokenSet(right);

            if (leftTokens.Count == 0 || rightTokens.Count == 0)
            {
                return 0;
            }

            return SimilarityCore(leftTokens, rightTokens);
        }

        /// <summary>
        /// Builds the token set used by the scoring overload.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The token set.</returns>
        public static HashSet<string> CreateTokenSet(string? value)
        {
            return new HashSet<string>(Tokenize(value), StringComparer.Ordinal);
        }

        private static double SimilarityCore(HashSet<string> leftSet, HashSet<string> rightSet)
        {
            if (leftSet.SetEquals(rightSet))
            {
                return 1;
            }

            var intersection = leftSet.Count(token => rightSet.Contains(token));

            // Dice coefficient over token sets, the same metric the DramaBox matcher uses: titles
            // frequently differ by one subtitle clause and Dice degrades more gracefully there than
            // Jaccard.
            var dice = (2.0 * intersection) / (leftSet.Count + rightSet.Count);

            // Full containment means one title is a strict subset of the other, which is a much
            // stronger signal than the raw overlap suggests.
            if (intersection == leftSet.Count || intersection == rightSet.Count)
            {
                var coverage = (double)Math.Min(leftSet.Count, rightSet.Count) / Math.Max(leftSet.Count, rightSet.Count);
                dice = Math.Max(dice, 0.85 + (0.15 * coverage));
            }

            return Math.Round(dice, 4);
        }

        private static bool IsSeriesId(string value)
        {
            // NetShort ids are snowflake numbers; the shortest observed is 19 digits, but older
            // catalogue entries are shorter and a threshold of 7 keeps the check meaningful.
            return value.Length is >= 7 and <= 20 && value.All(char.IsDigit);
        }

        private static string Decode(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                return value;
            }
        }

        private static bool IsYear(string token)
        {
            return token.Length == 4
                && token.All(char.IsDigit)
                && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                && year is >= 1900 and <= 2099;
        }
    }
}
