using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Normalizes and compares DramaFinds titles against library folder names, and derives the
    /// public URLs the platform does not return in its API payloads.
    /// </summary>
    /// <remarks>
    /// This is deliberately free of I/O so it can be unit tested directly.
    /// The normalization mirrors the DramaBox matcher on purpose: both platforms feed off the same
    /// library folders, so a title that normalizes cleanly for one must normalize identically for
    /// the other. Matching is not optional here: the search endpoint is fuzzy by substring, so
    /// searching "A Agência" also returns "Traído e Caçado pela Própria Agência" and
    /// "Cazado por mi propia agencia". Neither is the same title, and taking the first API row
    /// would identify the wrong series. Both measured 0.25 and 0.2857, so the candidate threshold
    /// below drops them, while the genuine partial-title case ("Adeus e Ponto Final" against
    /// "3.2.1, Adeus e Ponto Final") still measures 0.9357.
    /// The URL builders live here for the same reason they live in the DramaBox matcher: the API
    /// hands back a cover URL signed with an expiring auth_key and no episode URLs at all, so both
    /// have to be derived from the payload.
    /// </remarks>
    public static class DramaFindsTitleMatcher
    {
        private const string SiteRoot = "https://dramafinds.com";

        /// <summary>
        /// The locale segment every public DramaFinds URL carries.
        /// </summary>
        private const string Locale = "pt";

        /// <summary>
        /// The minimum score a candidate needs to be offered as a result, whether it came from the
        /// fuzzy search endpoint or from a library title. Chosen from measured behaviour: the
        /// unrelated substring hits score below 0.29, while a real subtitle match scores above 0.93.
        /// </summary>
        public const double MinimumCandidateScore = 0.34;

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
        /// against a whole result page tokenizes each candidate exactly once.
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
        /// Extracts a DramaFinds drama id from any of the shapes a user may paste: a public episode
        /// URL, a public drama URL, a bare numeric id, or an API body that carries "dramaId".
        /// </summary>
        /// <param name="value">The raw input.</param>
        /// <returns>The drama id, or null when the input does not carry one.</returns>
        public static string? ExtractDramaId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();

            // A bare id, which is what the Identify dialog gets when only the number is typed.
            // DramaFinds ids are five digits today; the upper bound keeps a pasted timestamp or
            // hash from being mistaken for one.
            if (trimmed.Length >= 5 && trimmed.Length <= 12 && trimmed.All(char.IsDigit))
            {
                return trimmed;
            }

            // A pasted API body, query string or link that carries the parameter, in either the JSON
            // shape ({"dramaId":"47011","indexE":0}) or the query shape (?dramaId=47011).
            var marker = trimmed.IndexOf("dramaId", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
            {
                var start = marker + "dramaId".Length;
                while (start < trimmed.Length && !char.IsDigit(trimmed[start]))
                {
                    start++;
                }

                var end = start;
                while (end < trimmed.Length && char.IsDigit(trimmed[end]))
                {
                    end++;
                }

                if (end > start)
                {
                    var parameter = trimmed.Substring(start, end - start);
                    if (parameter.Length >= 5 && parameter.Length <= 12)
                    {
                        return parameter;
                    }
                }
            }

            // https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilusões/episode-2
            var segments = trimmed.Split(new[] { '/', '?', '#' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if ((segments[i].Equals("video-play", StringComparison.OrdinalIgnoreCase)
                        || segments[i].Equals("drama", StringComparison.OrdinalIgnoreCase))
                    && i + 1 < segments.Length)
                {
                    var candidate = segments[i + 1];
                    if (candidate.Length >= 5 && candidate.All(char.IsDigit))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Removes the expiring signature from a cover URL.
        /// </summary>
        /// <param name="coverUrl">The cover URL as the API returned it.</param>
        /// <returns>The URL without its query string, or an empty string.</returns>
        public static string StripAuthKey(string? coverUrl)
        {
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                return string.Empty;
            }

            var trimmed = coverUrl.Trim();

            // Covers are served as "....jpg?auth_key=<expires within hours>". The same object is
            // served unsigned (verified: HTTP 200, image/jpeg), so dropping the query is the only
            // way to persist a cover URL that will still resolve tomorrow.
            var query = trimmed.IndexOf('?', StringComparison.Ordinal);
            if (query >= 0)
            {
                trimmed = trimmed.Substring(0, query);
            }

            var fragment = trimmed.IndexOf('#', StringComparison.Ordinal);
            if (fragment >= 0)
            {
                trimmed = trimmed.Substring(0, fragment);
            }

            return trimmed;
        }

        /// <summary>
        /// Builds the slug the platform puts in a public URL from a drama title.
        /// </summary>
        /// <param name="title">The drama title.</param>
        /// <returns>The slug, possibly empty.</returns>
        public static string BuildSlug(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(title.Length);
            var pendingSeparator = false;

            foreach (var character in title.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && builder.Length > 0)
                    {
                        builder.Append('-');
                    }

                    pendingSeparator = false;
                    builder.Append(character);
                }
                else
                {
                    // Diacritics are kept and every punctuation run collapses to a single hyphen,
                    // which is what the platform itself publishes: the canonical URL of drama 47011
                    // is /47011/99-amuletos-99-desilus%C3%B5es/episode-2, and the colon title
                    // "Empatia e Egoísmo: A Mulher..." becomes empatia-e-egoísmo-a-mulher....
                    pendingSeparator = true;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Builds the public URL of one episode. The platform exposes no episode API, but its own
        /// episode pages confirm the shape and the numbering.
        /// </summary>
        /// <param name="dramaId">The numeric drama id.</param>
        /// <param name="title">The drama title, used for the slug segment.</param>
        /// <param name="number">The one based episode number.</param>
        /// <returns>The absolute URL.</returns>
        public static string BuildEpisodeUrl(string? dramaId, string? title, int number)
        {
            var slug = BuildSlug(title);

            // Without a title there is no slug segment to build. The platform still serves the
            // shorter form, so the URL stays valid instead of carrying an empty path segment.
            var path = string.IsNullOrEmpty(slug)
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "/{0}/video-play/{1}/episode-{2}",
                    Locale,
                    dramaId,
                    number)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "/{0}/video-play/{1}/{2}/episode-{3}",
                    Locale,
                    dramaId,
                    Uri.EscapeDataString(slug),
                    number);

            return SiteRoot + path;
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
