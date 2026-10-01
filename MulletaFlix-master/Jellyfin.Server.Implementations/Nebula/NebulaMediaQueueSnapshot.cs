using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Model.Nebula;

namespace Jellyfin.Server.Implementations.Nebula;

internal sealed record NebulaMediaQueueEntry(string Title, string MediaType, int? Year, int Position, bool IsCurrent, bool IsPriority = false);

internal sealed record NebulaMediaQueueSnapshot(bool Available, bool IsRunning, DateTime? CapturedAtUtc, IReadOnlyList<NebulaMediaQueueEntry> Items)
{
    public static NebulaMediaQueueSnapshot Unavailable { get; } = new(false, false, null, Array.Empty<NebulaMediaQueueEntry>());
}

internal static class NebulaMediaQueueMatcher
{
    private static readonly Regex TrailingYear = new(@"\s*(?:[\(\[]\s*(?<year>(?:19|20)\d{2})\s*[\)\]]|[-–]\s*(?<plainYear>(?:19|20)\d{2}))\s*$", RegexOptions.Compiled);

    private sealed record QueueOrder(int PendingItemCount, IReadOnlyDictionary<int, int> RankByPosition);

    public static NebulaMediaQueueEntry CreateEntry(string path, int position, bool isCurrent, bool isPriority = false)
    {
        var title = NebulaDownloaderEngine.GetMediaSortTitle(path);
        var parent = Path.GetDirectoryName(path) ?? string.Empty;
        var filename = Path.GetFileName(path);
        var mediaType = NebulaUploadEngine.ClassifyMediaType(parent, filename);
        var year = GetYear(title) ?? GetYear(Path.GetFileNameWithoutExtension(path)) ?? GetYear(Path.GetFileName(parent));
        return new NebulaMediaQueueEntry(title, mediaType, year, position, isCurrent, isPriority);
    }

    public static NebulaMediaQueuePositionDto Summarize(NebulaMediaRequestQueueQueryDto request, NebulaMediaQueueSnapshot snapshot)
    {
        var index = snapshot.Items
            .GroupBy(item => NormalizeTitle(item.Title), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<NebulaMediaQueueEntry>)group.ToArray(), StringComparer.Ordinal);
        return Summarize(request, snapshot, index, CreateQueueOrder(snapshot));
    }

    public static IReadOnlyDictionary<long, NebulaMediaQueuePositionDto> SummarizeMany(
        IReadOnlyList<NebulaMediaRequestQueueQueryDto> requests,
        NebulaMediaQueueSnapshot snapshot)
    {
        var index = snapshot.Items
            .GroupBy(item => NormalizeTitle(item.Title), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<NebulaMediaQueueEntry>)group.ToArray(), StringComparer.Ordinal);
        var queueOrder = CreateQueueOrder(snapshot);
        return requests.ToDictionary(request => request.RequestId, request => Summarize(request, snapshot, index, queueOrder));
    }

    private static NebulaMediaQueuePositionDto Summarize(
        NebulaMediaRequestQueueQueryDto request,
        NebulaMediaQueueSnapshot snapshot,
        IReadOnlyDictionary<string, IReadOnlyList<NebulaMediaQueueEntry>> index,
        QueueOrder queueOrder)
    {
        var result = new NebulaMediaQueuePositionDto
        {
            SnapshotAvailable = snapshot.Available,
            IsRunning = snapshot.IsRunning,
            SnapshotAtUtc = snapshot.CapturedAtUtc,
            QueueItemCount = queueOrder.PendingItemCount
        };
        if (!snapshot.Available || string.IsNullOrWhiteSpace(request.Title))
        {
            return result;
        }

        var titleKey = NormalizeTitle(request.Title);
        var typeKey = NormalizeMediaType(request.MediaType);
        if (!index.TryGetValue(titleKey, out var titleMatches))
        {
            return result;
        }

        var matched = titleMatches.Where(item =>
            (typeKey.Length == 0 || NormalizeMediaType(item.MediaType) == typeKey)
            && (!request.Year.HasValue || item.Year == request.Year.Value)).ToArray();

        result.MatchingItemCount = matched.Length;
        result.CurrentItemCount = matched.Count(item => item.IsCurrent);
        result.IsPriority = matched.Any(item => item.IsPriority);
        var pendingPositions = matched
            .Where(item => !item.IsCurrent && item.Position > 0)
            .Select(item => queueOrder.RankByPosition[item.Position])
            .ToArray();
        result.Position = pendingPositions.Length == 0 ? null : pendingPositions.Min();
        return result;
    }

    private static QueueOrder CreateQueueOrder(NebulaMediaQueueSnapshot snapshot)
    {
        var pendingItems = snapshot.Items
            .Where(item => !item.IsCurrent && item.Position > 0)
            .OrderBy(item => item.Position)
            .ToArray();
        var rankByPosition = pendingItems
            .Select((item, index) => (item.Position, Rank: index + 1))
            .ToDictionary(item => item.Position, item => item.Rank);
        return new QueueOrder(pendingItems.Length, rankByPosition);
    }

    private static int? GetYear(string value)
    {
        var match = TrailingYear.Match(value.Trim());
        if (!match.Success)
        {
            return null;
        }

        var year = match.Groups["year"].Success ? match.Groups["year"].Value : match.Groups["plainYear"].Value;
        return int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string NormalizeTitle(string value)
    {
        var withoutYear = TrailingYear.Replace(value.Trim(), string.Empty);
        var decomposed = withoutYear.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && normalized.Length > 0)
                {
                    normalized.Append(' ');
                }

                normalized.Append(char.ToLowerInvariant(character));
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return normalized.ToString();
    }

    private static string NormalizeMediaType(string value)
    {
        var normalized = NormalizeTitle(value).Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalized switch
        {
            "movie" or "movies" or "filme" or "filmes" => "FILME",
            "series" or "serie" or "séries" or "seriados" => "SERIE",
            "animation" or "animations" or "animacao" or "animacoes" or "anime" => "ANIMACAO",
            "novel" or "novels" or "novela" or "novelas" => "NOVELA",
            "dorama" or "doramas" or "kdrama" or "koreandrama" => "DORAMA",
            "porno" or "adult" or "adulto" => "PORNO",
            _ => string.Empty
        };
    }
}
