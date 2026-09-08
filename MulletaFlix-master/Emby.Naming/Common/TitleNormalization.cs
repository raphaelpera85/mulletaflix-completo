using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Emby.Naming.Common;

/// <summary>
/// Helpers for normalizing lookup titles derived from filenames and folders.
/// </summary>
public static partial class TitleNormalization
{
    private static readonly HashSet<string> ReleaseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "leg",
        "legendado",
        "legendada",
        "dublado",
        "dublada",
        "dub",
        "sub",
        "subbed",
        "subs",
        "multisubs",
        "multi-subs",
        "multi subs",
        "ptbr",
        "pt-br",
        "pt br",
        "br",
        "brasil",
        "latino",
        "audiolatino",
        "audio latino",
        "originalaudio",
        "audio original",
        "dual",
        "dualaudio",
        "dual audio",
        "nacional",
        "multi",
        "portugues",
        "portuguese"
    };

    [GeneratedRegex(@"^(?<cleaned>.+?)(?:[\s\._-]*[\(\[\{]\s*(?<tag>[^()\[\]{}]+?)\s*[\)\]\}]|[\s\._-]+(?<tag>[^()\[\]{}]+?))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingReleaseTagRegex();

    [GeneratedRegex(@"[\(\[\{]\s*(?:leg|legendado|legendada|dublado|dublada|dub|sub|subbed|subs|multi[- ]?subs?|pt[- ]?br|ptbr|br|brasil|latino|audio[- ]?latino|audio[- ]?original|original[- ]?audio|dual[- ]?audio|dual|nacional|multi|portugu[eê]s)\s*[\)\]\}]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DelimitedReleaseTagRegex();

    [GeneratedRegex(@"(?<=^|[\s\._\-\,\(\[\{])(?:leg|legendado|legendada|dublado|dublada|dub|sub|subbed|subs|multi[- ]?subs?|pt[- ]?br|ptbr|latino|audio[- ]?latino|audio[- ]?original|original[- ]?audio|dual[- ]?audio|dual|nacional)(?=[\s\._\-\,\)\]\}]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WordReleaseTagRegex();

    [GeneratedRegex(@"[\(\[\{]\s*(?<year>(?:18|19|20)\d{2})\s*[\)\]\}]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedYearRegex();

    [GeneratedRegex(@"(?<=^|[\s\._\-\(\[\{])(?<year>(?:18|19|20)\d{2})(?!\d|[\._-]\d{2}[\._-]\d{2})(?=[\s\._\-\)\]\}]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex BoundaryYearRegex();

    [GeneratedRegex(@"\(\s*\)|\[\s*\]|\{\s*\}", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyBracketsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleSpacesRegex();

    /// <summary>
    /// Removes release tags (such as (LEG), [LEG], DUB, etc.) anywhere in the title (leading, embedded, or trailing).
    /// </summary>
    /// <param name="name">Raw title or filename.</param>
    /// <returns>Cleaned title without release tags.</returns>
    public static string CleanReleaseTags(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var current = name;
        current = DelimitedReleaseTagRegex().Replace(current, " ");
        current = WordReleaseTagRegex().Replace(current, " ");
        current = EmptyBracketsRegex().Replace(current, " ");
        current = MultipleSpacesRegex().Replace(current, " ").Trim();
        current = current.Trim('-', '_', '.', ',');

        return current;
    }

    /// <summary>
    /// Extracts a release year (between 1888 and near future) from anywhere in the string.
    /// </summary>
    /// <param name="name">The name to parse.</param>
    /// <returns>Extracted year or null.</returns>
    public static int? ExtractYear(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var maxYear = DateTime.UtcNow.Year + 5;

        // 1. Prefer bracketed year: (2024), [2024], {2024}
        var bracketMatches = BracketedYearRegex().Matches(name);
        if (bracketMatches.Count > 0)
        {
            for (int i = bracketMatches.Count - 1; i >= 0; i--)
            {
                if (int.TryParse(bracketMatches[i].Groups["year"].ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    && y >= 1888 && y <= maxYear)
                {
                    return y;
                }
            }
        }

        // 2. Fallback to boundary year
        var boundaryMatches = BoundaryYearRegex().Matches(name);
        if (boundaryMatches.Count > 0)
        {
            for (int i = boundaryMatches.Count - 1; i >= 0; i--)
            {
                if (int.TryParse(boundaryMatches[i].Groups["year"].ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    && y >= 1888 && y <= maxYear)
                {
                    return y;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Removes common release tags from the title.
    /// </summary>
    /// <param name="name">Raw title.</param>
    /// <returns>Normalized title.</returns>
    public static string RemoveTrailingReleaseTags(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var current = CleanReleaseTags(name);

        while (TryRemoveTrailingReleaseTag(current, out var cleaned))
        {
            current = CleanReleaseTags(cleaned);
        }

        return current.Trim();
    }

    private static bool TryRemoveTrailingReleaseTag(string name, out string cleaned)
    {
        var match = TrailingReleaseTagRegex().Match(name);
        if (!match.Success)
        {
            cleaned = string.Empty;
            return false;
        }

        var tag = NormalizeToken(match.Groups["tag"].Value);
        if (!ReleaseTags.Contains(tag))
        {
            cleaned = string.Empty;
            return false;
        }

        cleaned = match.Groups["cleaned"].Value;
        return true;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"[^\p{L}\p{N}]+", string.Empty).ToLowerInvariant();
    }
}

