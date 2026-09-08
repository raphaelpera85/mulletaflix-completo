using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Emby.Naming.Common;

namespace Emby.Naming.Video
{
    /// <summary>
    /// <see href="http://kodi.wiki/view/Advancedsettings.xml#video" />.
    /// </summary>
    public static class CleanDateTimeParser
    {
        /// <summary>
        /// Attempts to clean the name.
        /// </summary>
        /// <param name="name">Name of video.</param>
        /// <param name="cleanDateTimeRegexes">Optional list of regexes to clean the name.</param>
        /// <returns>Returns <see cref="CleanDateTimeResult"/> object.</returns>
        public static CleanDateTimeResult Clean(string name, IReadOnlyList<Regex> cleanDateTimeRegexes)
        {
            CleanDateTimeResult result = new CleanDateTimeResult(name);
            if (string.IsNullOrEmpty(name))
            {
                return result;
            }

            var len = cleanDateTimeRegexes.Count;

            // 1. Try with release tags cleaned (e.g. "Deadpool & Wolverine (LEG) (2024)", "[Multi-Subs] [2018]")
            var cleanedTagsName = TitleNormalization.CleanReleaseTags(name);
            if (!string.IsNullOrWhiteSpace(cleanedTagsName) && !string.Equals(cleanedTagsName, name, StringComparison.Ordinal))
            {
                for (int i = 0; i < len; i++)
                {
                    if (TryClean(cleanedTagsName, cleanDateTimeRegexes[i], ref result))
                    {
                        var cleaned = TitleNormalization.CleanReleaseTags(result.Name);
                        if (!string.IsNullOrWhiteSpace(cleaned))
                        {
                            result = new CleanDateTimeResult(cleaned, result.Year);
                        }

                        return result;
                    }
                }
            }

            // 2. Try with raw name
            for (int i = 0; i < len; i++)
            {
                if (TryClean(name, cleanDateTimeRegexes[i], ref result))
                {
                    var cleaned = TitleNormalization.CleanReleaseTags(result.Name);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        result = new CleanDateTimeResult(cleaned, result.Year);
                    }

                    return result;
                }
            }

            // 3. Fallback year extraction (e.g. bracketed years "[2018]", "(LEG)(2024)")
            var year = TitleNormalization.ExtractYear(name);
            if (year.HasValue)
            {
                var yearStr = year.Value.ToString(CultureInfo.InvariantCulture);
                var yearIdx = name.LastIndexOf(yearStr, StringComparison.Ordinal);
                if (yearIdx > 0)
                {
                    var prefix = name[..yearIdx].TrimEnd(' ', '.', '_', '-', '(', '[', '{');
                    var cleanedPrefix = TitleNormalization.CleanReleaseTags(prefix);
                    if (!string.IsNullOrWhiteSpace(cleanedPrefix))
                    {
                        return new CleanDateTimeResult(cleanedPrefix, year.Value);
                    }
                }
            }

            return result;
        }

        private static bool TryClean(string name, Regex expression, ref CleanDateTimeResult result)
        {
            var match = expression.Match(name);

            if (match.Success
                && match.Groups.Count == 5
                && match.Groups[1].Success
                && match.Groups[2].Success
                && int.TryParse(match.Groups[2].ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
            {
                result = new CleanDateTimeResult(match.Groups[1].Value.TrimEnd(), year);
                return true;
            }

            return false;
        }
    }
}

