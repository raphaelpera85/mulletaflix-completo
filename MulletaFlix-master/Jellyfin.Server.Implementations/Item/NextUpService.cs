#pragma warning disable RS0030 // Do not use banned APIs

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.EntityFrameworkCore;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using BaseItemDto = MediaBrowser.Controller.Entities.BaseItem;

namespace MulletaFlix.Server.Implementations.Item;

/// <summary>
/// Provides next-up episode query operations.
/// </summary>
public class NextUpService : INextUpService
{
    internal readonly record struct EpisodeCandidate(Guid Id, string SeriesKey, int? Season, int? Episode);

    private readonly IDbContextFactory<MulletaFlixDbContext> _dbProvider;
    private readonly IItemTypeLookup _itemTypeLookup;
    private readonly IItemQueryHelpers _queryHelpers;

    /// <summary>
    /// Initializes a new instance of the <see cref="NextUpService"/> class.
    /// </summary>
    /// <param name="dbProvider">The database context factory.</param>
    /// <param name="itemTypeLookup">The item type lookup.</param>
    /// <param name="queryHelpers">The shared query helpers.</param>
    public NextUpService(
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IItemTypeLookup itemTypeLookup,
        IItemQueryHelpers queryHelpers)
    {
        _dbProvider = dbProvider;
        _itemTypeLookup = itemTypeLookup;
        _queryHelpers = queryHelpers;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetNextUpSeriesKeys(InternalItemsQuery filter, DateTime dateCutoff)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(filter.User);

        using var context = _dbProvider.CreateDbContext();

        var query = context.BaseItems
            .AsNoTracking()
            .Where(i => i.TopParentId.HasValue && Enumerable.Contains(filter.TopParentIds, i.TopParentId.Value))
            .Where(i => i.Type == _itemTypeLookup.BaseItemKindNames[BaseItemKind.Episode])
            .Join(
                context.UserData.AsNoTracking().Where(e => e.ItemId != EF.Constant(BaseItemRepository.PlaceholderId)),
                i => new { UserId = filter.User.Id, ItemId = i.Id },
                u => new { u.UserId, u.ItemId },
                (entity, data) => new { Item = entity, UserData = data })
            .GroupBy(g => g.Item.SeriesPresentationUniqueKey)
            .Select(g => new { g.Key, LastPlayedDate = g.Max(u => u.UserData.LastPlayedDate) })
            .Where(g => g.Key != null && g.LastPlayedDate != null && g.LastPlayedDate >= dateCutoff)
            .OrderByDescending(g => g.LastPlayedDate)
            .ThenBy(g => g.Key)
            .Select(g => g.Key!);

        if (filter.Limit.HasValue)
        {
            query = query.Take(filter.Limit.Value);
        }

        return query.ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, NextUpEpisodeBatchResult> GetNextUpEpisodesBatch(
        InternalItemsQuery filter,
        IReadOnlyList<string> seriesKeys,
        bool includeSpecials,
        bool includeWatchedForRewatching)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(filter.User);

        if (seriesKeys.Count == 0)
        {
            return new Dictionary<string, NextUpEpisodeBatchResult>();
        }

        _queryHelpers.PrepareFilterQuery(filter);
        using var context = _dbProvider.CreateDbContext();

        var userId = filter.User.Id;
        var episodeTypeName = _itemTypeLookup.BaseItemKindNames[BaseItemKind.Episode];

        var lastWatchedBase = context.BaseItems
            .AsNoTracking()
            .Where(e => e.Type == episodeTypeName)
            .Where(e => e.SeriesPresentationUniqueKey != null && seriesKeys.Contains(e.SeriesPresentationUniqueKey))
            .Where(e => e.ParentIndexNumber != 0);
        lastWatchedBase = _queryHelpers.ApplyAccessFiltering(context, lastWatchedBase, filter);

        var playedWithDates = lastWatchedBase
            .Join(
                context.UserData
                    .AsNoTracking()
                    .Where(ud => ud.ItemId != EF.Constant(BaseItemRepository.PlaceholderId))
                    .Where(ud => ud.Played),
                e => new { UserId = userId, ItemId = e.Id },
                ud => new { ud.UserId, ud.ItemId },
                (e, ud) => new { Episode = e, ud.LastPlayedDate });

        var latestPlayedPositionRows = lastWatchedBase
            .Where(e => e.UserData!.Any(ud => ud.UserId == userId && ud.Played))
            .GroupBy(e => e.SeriesPresentationUniqueKey)
            .Select(group => group
                .OrderByDescending(e => e.ParentIndexNumber)
                .ThenByDescending(e => e.IndexNumber)
                .ThenByDescending(e => e.Id)
                .Select(e => new
                {
                    e.Id,
                    SeriesKey = e.SeriesPresentationUniqueKey,
                    Season = e.ParentIndexNumber,
                    Episode = e.IndexNumber
                })
                .First())
            .ToDictionary(row => row.SeriesKey!, row => new EpisodeCandidate(row.Id, row.SeriesKey!, row.Season, row.Episode));
        var lastWatchedInfo = latestPlayedPositionRows.ToDictionary(row => row.Key, row => row.Value.Id);

        IQueryable<BaseItemEntity>? latestPlayedByDateQuery = null;
        var lastWatchedByDateInfo = new Dictionary<string, Guid>();
        if (includeWatchedForRewatching)
        {
            var latestPlayedByDateIds = playedWithDates
                .GroupBy(row => row.Episode.SeriesPresentationUniqueKey)
                .Select(group => group
                    .OrderByDescending(row => row.LastPlayedDate)
                    .ThenByDescending(row => row.Episode.ParentIndexNumber)
                    .ThenByDescending(row => row.Episode.IndexNumber)
                    .ThenByDescending(row => row.Episode.Id)
                    .Select(row => row.Episode.Id)
                    .First());
            latestPlayedByDateQuery = context.BaseItems
                .AsNoTracking()
                .Where(e => latestPlayedByDateIds.Contains(e.Id));
            lastWatchedByDateInfo = latestPlayedByDateQuery
                .Select(e => new { e.SeriesPresentationUniqueKey, e.Id })
                .ToDictionary(row => row.SeriesPresentationUniqueKey!, row => row.Id);
        }

        var allLastWatchedIds = lastWatchedInfo.Values
            .Concat(lastWatchedByDateInfo.Values)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var lwQuery = context.BaseItems.AsNoTracking().Where(e => allLastWatchedIds.Contains(e.Id));
        lwQuery = _queryHelpers.ApplyNavigations(lwQuery, filter);
        var lastWatchedEpisodes = lwQuery.ToDictionary(e => e.Id);

        Dictionary<string, List<BaseItemEntity>> specialsBySeriesKey = new();
        if (includeSpecials)
        {
            var specialsQuery = context.BaseItems
                .AsNoTracking()
                .Where(e => e.Type == episodeTypeName)
                .Where(e => e.SeriesPresentationUniqueKey != null && seriesKeys.Contains(e.SeriesPresentationUniqueKey))
                .Where(e => e.ParentIndexNumber == 0)
                .Where(e => !e.IsVirtualItem);
            specialsQuery = _queryHelpers.ApplyAccessFiltering(context, specialsQuery, filter);
            specialsQuery = _queryHelpers.ApplyNavigations(specialsQuery, filter).AsSingleQuery();

            foreach (var special in specialsQuery)
            {
                var key = special.SeriesPresentationUniqueKey!;
                if (!specialsBySeriesKey.TryGetValue(key, out var list))
                {
                    list = new List<BaseItemEntity>();
                    specialsBySeriesKey[key] = list;
                }

                list.Add(special);
            }
        }

        var allUnplayedBase = context.BaseItems
            .AsNoTracking()
            .Where(e => e.Type == episodeTypeName)
            .Where(e => e.SeriesPresentationUniqueKey != null && seriesKeys.Contains(e.SeriesPresentationUniqueKey))
            .Where(e => e.ParentIndexNumber != 0)
            .Where(e => !e.IsVirtualItem)
            .Where(e => !e.UserData!.Any(ud => ud.UserId == userId && ud.Played));
        allUnplayedBase = _queryHelpers.ApplyAccessFiltering(context, allUnplayedBase, filter);
        var nextUnplayedBySeries = allUnplayedBase
            .Where(candidate => !lastWatchedBase.Any(watched =>
                watched.SeriesPresentationUniqueKey == candidate.SeriesPresentationUniqueKey
                && watched.UserData!.Any(ud => ud.UserId == userId && ud.Played)
                && watched.ParentIndexNumber != null
                && watched.IndexNumber != null
                && !lastWatchedBase.Any(later =>
                    later.SeriesPresentationUniqueKey == watched.SeriesPresentationUniqueKey
                    && later.UserData!.Any(ud => ud.UserId == userId && ud.Played)
                    && (later.ParentIndexNumber > watched.ParentIndexNumber
                        || (later.ParentIndexNumber == watched.ParentIndexNumber
                            && later.IndexNumber > watched.IndexNumber)))
                && ((candidate.ParentIndexNumber == null || candidate.IndexNumber == null)
                    || watched.ParentIndexNumber > candidate.ParentIndexNumber
                    || (watched.ParentIndexNumber == candidate.ParentIndexNumber
                        && watched.IndexNumber >= candidate.IndexNumber))))
            .GroupBy(candidate => candidate.SeriesPresentationUniqueKey)
            .Select(group => group
                .OrderBy(candidate => candidate.ParentIndexNumber)
                .ThenBy(candidate => candidate.IndexNumber)
                .Select(candidate => new EpisodeCandidate(
                    candidate.Id,
                    candidate.SeriesPresentationUniqueKey!,
                    candidate.ParentIndexNumber,
                    candidate.IndexNumber))
                .First())
            .ToDictionary(candidate => candidate.SeriesKey);

        var nextEpisodeIds = new HashSet<Guid>();
        var seriesNextIdMap = new Dictionary<string, Guid>();

        foreach (var seriesKey in seriesKeys)
        {
            if (nextUnplayedBySeries.TryGetValue(seriesKey, out var nextCandidate) && nextCandidate.Id != Guid.Empty)
            {
                nextEpisodeIds.Add(nextCandidate.Id);
                seriesNextIdMap[seriesKey] = nextCandidate.Id;
            }
        }

        var seriesNextPlayedIdMap = new Dictionary<string, Guid>();
        if (includeWatchedForRewatching)
        {
            var allPlayedBase = context.BaseItems
                .AsNoTracking()
                .Where(e => e.Type == episodeTypeName)
                .Where(e => e.SeriesPresentationUniqueKey != null && seriesKeys.Contains(e.SeriesPresentationUniqueKey))
                .Where(e => e.ParentIndexNumber != 0)
                .Where(e => !e.IsVirtualItem)
                .Where(e => e.UserData!.Any(ud => ud.UserId == userId && ud.Played));
            allPlayedBase = _queryHelpers.ApplyAccessFiltering(context, allPlayedBase, filter);
            var latestPlayedByDate = latestPlayedByDateQuery!;
            var nextPlayedBySeries = allPlayedBase
                .Join(
                    latestPlayedByDate,
                    candidate => candidate.SeriesPresentationUniqueKey,
                    latest => latest.SeriesPresentationUniqueKey,
                    (candidate, latest) => new { Candidate = candidate, Latest = latest })
                .Where(row => !row.Latest.ParentIndexNumber.HasValue
                    || !row.Latest.IndexNumber.HasValue
                    || (row.Candidate.ParentIndexNumber > row.Latest.ParentIndexNumber.Value
                        || (row.Candidate.ParentIndexNumber == row.Latest.ParentIndexNumber.Value
                            && row.Candidate.IndexNumber > row.Latest.IndexNumber.Value)))
                .GroupBy(row => row.Candidate.SeriesPresentationUniqueKey)
                .Select(group => group
                    .OrderBy(row => row.Candidate.ParentIndexNumber)
                    .ThenBy(row => row.Candidate.IndexNumber)
                    .ThenBy(row => row.Candidate.Id)
                    .Select(row => new
                    {
                        row.Candidate.Id,
                        SeriesKey = row.Candidate.SeriesPresentationUniqueKey,
                        Season = row.Candidate.ParentIndexNumber,
                        Episode = row.Candidate.IndexNumber
                    })
                    .First())
                .ToDictionary(row => row.SeriesKey!, row => new EpisodeCandidate(row.Id, row.SeriesKey!, row.Season, row.Episode));

            foreach (var seriesKey in seriesKeys)
            {
                if (nextPlayedBySeries.TryGetValue(seriesKey, out var nextPlayedCandidate)
                    && nextPlayedCandidate.Id != Guid.Empty)
                {
                    nextEpisodeIds.Add(nextPlayedCandidate.Id);
                    seriesNextPlayedIdMap[seriesKey] = nextPlayedCandidate.Id;
                }
            }
        }

        var nextEpisodes = new Dictionary<Guid, BaseItemEntity>();
        if (nextEpisodeIds.Count > 0)
        {
            var nextQuery = context.BaseItems.AsNoTracking().Where(e => nextEpisodeIds.Contains(e.Id));
            nextQuery = _queryHelpers.ApplyNavigations(nextQuery, filter).AsSingleQuery();
            nextEpisodes = nextQuery.ToDictionary(e => e.Id);
        }

        var result = new Dictionary<string, NextUpEpisodeBatchResult>();
        foreach (var seriesKey in seriesKeys)
        {
            var batchResult = new NextUpEpisodeBatchResult();

            if (lastWatchedInfo.TryGetValue(seriesKey, out var lwId) && lwId != Guid.Empty)
            {
                if (lastWatchedEpisodes.TryGetValue(lwId, out var entity))
                {
                    batchResult.LastWatched = _queryHelpers.DeserializeBaseItem(entity, filter.SkipDeserialization);
                }
            }

            if (seriesNextIdMap.TryGetValue(seriesKey, out var nextId) && nextEpisodes.TryGetValue(nextId, out var nextEntity))
            {
                batchResult.NextUp = _queryHelpers.DeserializeBaseItem(nextEntity, filter.SkipDeserialization);
            }

            if (includeSpecials && specialsBySeriesKey.TryGetValue(seriesKey, out var specials))
            {
                batchResult.Specials = specials.Select(s => _queryHelpers.DeserializeBaseItem(s, filter.SkipDeserialization)!).ToList();
            }
            else
            {
                batchResult.Specials = Array.Empty<BaseItemDto>();
            }

            if (includeWatchedForRewatching)
            {
                if (lastWatchedByDateInfo.TryGetValue(seriesKey, out var lastByDateId) &&
                    lastWatchedEpisodes.TryGetValue(lastByDateId, out var lastByDateEntity))
                {
                    batchResult.LastWatchedForRewatching = _queryHelpers.DeserializeBaseItem(lastByDateEntity, filter.SkipDeserialization);
                }

                if (seriesNextPlayedIdMap.TryGetValue(seriesKey, out var nextPlayedId) &&
                    nextEpisodes.TryGetValue(nextPlayedId, out var nextPlayedEntity))
                {
                    batchResult.NextPlayedForRewatching = _queryHelpers.DeserializeBaseItem(nextPlayedEntity, filter.SkipDeserialization);
                }
            }

            result[seriesKey] = batchResult;
        }

        return result;
    }

    internal static void KeepPreferredCandidate(
        Dictionary<string, EpisodeCandidate> candidatesBySeries,
        EpisodeCandidate candidate,
        bool descending)
    {
        if (!candidatesBySeries.TryGetValue(candidate.SeriesKey, out var current))
        {
            candidatesBySeries[candidate.SeriesKey] = candidate;
            return;
        }

        var comparison = Nullable.Compare(candidate.Season, current.Season);
        if (comparison == 0)
        {
            comparison = Nullable.Compare(candidate.Episode, current.Episode);
        }

        if ((descending && comparison > 0) || (!descending && comparison < 0))
        {
            candidatesBySeries[candidate.SeriesKey] = candidate;
        }
    }
}
