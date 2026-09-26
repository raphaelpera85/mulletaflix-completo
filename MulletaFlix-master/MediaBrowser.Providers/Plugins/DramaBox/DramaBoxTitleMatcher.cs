using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Normalizes and compares DramaBox titles against library folder names.
    /// </summary>
    /// <remarks>
    /// This is deliberately free of I/O so it can be unit tested directly. It exists because
    /// DramaBox exposes no search endpoint: the only way to resolve a library folder to a book id
    /// is to compare its name against a locally crawled index, and the two sides never match
    /// byte for byte. Two real cases drove the normalization:
    /// the library item for the folder "3.2.1, Adeus e Ponto Final" is named
    /// "3.2 1, Adeus e Ponto Final" (the separator dot became a space), and library names carry
    /// "(Dublado)" / "(2024)" markers that DramaBox titles do not.
    /// </remarks>
    public static class DramaBoxTitleMatcher
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
        /// Builds the token set used by the scoring overload, so a caller that compares one title
        /// against a whole catalogue can tokenize each catalogue entry exactly once.
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

        private static double SimilarityCore(HashSet<string> leftSet, HashSet<string> rightSet)
        {
            if (leftSet.SetEquals(rightSet))
            {
                return 1;
            }

            var intersection = leftSet.Count(token => rightSet.Contains(token));

            // Dice coefficient over token sets. Chosen over Jaccard because titles frequently
            // differ by one subtitle clause, and Dice degrades more gracefully there.
            var dice = (2.0 * intersection) / (leftSet.Count + rightSet.Count);

            // Full containment means one title is a strict subset of the other, which is a much
            // stronger signal than the raw overlap suggests ("Adeus e Ponto Final" vs
            // "3.2.1, Adeus e Ponto Final").
            if (intersection == leftSet.Count || intersection == rightSet.Count)
            {
                var smaller = Math.Min(leftSet.Count, rightSet.Count);
                var larger = Math.Max(leftSet.Count, rightSet.Count);
                var coverage = (double)smaller / larger;
                dice = Math.Max(dice, 0.85 + (0.15 * coverage));
            }

            return Math.Round(dice, 4);
        }

        /// <summary>
        /// Extracts a DramaBox book id from any of the shapes a user may paste:
        /// a full drama URL, a video URL, or a bare numeric id.
        /// </summary>
        /// <param name="value">The raw input.</param>
        /// <returns>The book id, or null when the input does not carry one.</returns>
        public static string? ExtractBookId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();

            // A bare id, which is what the Identify dialog gets when only the number is typed.
            if (trimmed.All(char.IsDigit) && trimmed.Length >= 6)
            {
                return trimmed;
            }

            // https://www.dramabox.com/pt/drama/42000002641/321-Adeus-e-Ponto-Final
            var segments = trimmed.Split(new[] { '/', '?', '#' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("drama", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                {
                    var candidate = segments[i + 1];

                    // Video URLs look like /video/<bookId>_<slug>/<chapterId>_Episode-1.
                    var underscore = candidate.IndexOf('_', StringComparison.Ordinal);
                    if (underscore > 0)
                    {
                        candidate = candidate.Substring(0, underscore);
                    }

                    if (candidate.Length >= 6 && candidate.All(char.IsDigit))
                    {
                        return candidate;
                    }
                }

                if (segments[i].Equals("video", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                {
                    var candidate = segments[i + 1];
                    var underscore = candidate.IndexOf('_', StringComparison.Ordinal);
                    if (underscore > 0)
                    {
                        candidate = candidate.Substring(0, underscore);
                    }

                    if (candidate.Length >= 6 && candidate.All(char.IsDigit))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a higher resolution cover URL by rewriting the CDN transform suffix.
        /// DramaBox serves covers as "....jpg@w=360&amp;h=640".
        /// </summary>
        /// <param name="coverUrl">The cover URL as returned by DramaBox.</param>
        /// <param name="width">The requested width.</param>
        /// <param name="height">The requested height.</param>
        /// <returns>The rewritten URL, or the original when it has no transform suffix.</returns>
        public static string BuildCoverUrl(string? coverUrl, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                return string.Empty;
            }

            var at = coverUrl.IndexOf('@', StringComparison.Ordinal);
            var baseUrl = at >= 0 ? coverUrl.Substring(0, at) : coverUrl;

            return string.Format(CultureInfo.InvariantCulture, "{0}@w={1}&h={2}", baseUrl, width, height);
        }

        private static bool IsYear(string token)
        {
            return token.Length == 4
                && token.All(char.IsDigit)
                && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                && year >= 1900
                && year <= 2099;
        }
    }
}
