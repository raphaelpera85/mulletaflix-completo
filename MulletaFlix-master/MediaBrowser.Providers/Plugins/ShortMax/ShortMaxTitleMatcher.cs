using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public static class ShortMaxTitleMatcher
{
    private static readonly Regex IdRegex = new Regex(@"-(?<id>\d{4,})(?:-\d+)?(?:[/?#]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? ExtractSeriesId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = Uri.UnescapeDataString(input.Trim());
        var match = IdRegex.Match(value);
        return match.Success ? match.Groups["id"].Value : (long.TryParse(value, out _) ? value : null);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = CleanName(value).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", " ").Trim();
    }

    public static double Similarity(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1;
        }

        var distance = 0;
        var previous = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            previous = current;
            distance = current[b.Length];
        }

        return 1d - (double)distance / Math.Max(a.Length, b.Length);
    }

    public static string CleanName(string value)
    {
        return Regex.Replace(value.Replace("[Dublado]", string.Empty, StringComparison.OrdinalIgnoreCase), @"\s+", " ").Trim();
    }
}
