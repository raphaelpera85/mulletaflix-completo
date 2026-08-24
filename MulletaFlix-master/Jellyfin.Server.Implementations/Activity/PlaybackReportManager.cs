using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MediaBrowser.Model.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Extensions;
using MulletaFlix.Server.Implementations.Activity;

namespace MulletaFlix.Server.Implementations.Activity;

/// <summary>
/// Manages the storage and retrieval of <see cref="PlaybackReport"/> instances.
/// </summary>
public class PlaybackReportManager : IPlaybackReportManager
{
    private readonly IDbContextFactory<UsersDbContext> _provider;
    private readonly ILogger<PlaybackReportManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackReportManager"/> class.
    /// </summary>
    /// <param name="provider">The Users database provider.</param>
    /// <param name="logger">The logger.</param>
    public PlaybackReportManager(IDbContextFactory<UsersDbContext> provider, ILogger<PlaybackReportManager> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(PlaybackReport entry)
    {
        var dbContext = await _provider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            dbContext.PlaybackReports.Add(entry);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(PlaybackReport entry)
    {
        var dbContext = await _provider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            dbContext.PlaybackReports.Update(entry);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<QueryResult<PlaybackReportDto>> GetPagedResultAsync(PlaybackReportQuery query)
    {
        var dbContext = await _provider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            var entries = from p in dbContext.PlaybackReports
                          join u in dbContext.Users on p.UserId equals u.Id into ugj
                          from u in ugj.DefaultIfEmpty()
                          select new ExpandedPlaybackReport { PlaybackReport = p, Username = u.Username };

            if (query.UserId is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.UserId == query.UserId.Value);
            }

            if (query.ItemId is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.ItemId == query.ItemId.Value);
            }

            if (!string.IsNullOrEmpty(query.DeviceId))
            {
                entries = entries.Where(e => e.PlaybackReport.DeviceId == query.DeviceId);
            }

            if (!string.IsNullOrEmpty(query.LibraryId))
            {
                var libId = Guid.Parse(query.LibraryId);
                entries = entries.Where(e => e.PlaybackReport.LibraryId == libId);
            }

            if (query.MinDate is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.DateCreated >= query.MinDate.Value);
            }

            if (query.MaxDate is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.DateCreated <= query.MaxDate.Value);
            }

            if (!string.IsNullOrEmpty(query.ItemType))
            {
                entries = entries.Where(e => e.PlaybackReport.ItemType == query.ItemType);
            }

            if (query.WasTranscoded is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.WasTranscoded == query.WasTranscoded.Value);
            }

            if (query.PlayedToCompletion is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.PlayedToCompletion == query.PlayedToCompletion.Value);
            }

            if (query.HasError is not null)
            {
                if (query.HasError.Value)
                {
                    entries = entries.Where(e => !string.IsNullOrEmpty(e.PlaybackReport.ErrorMessage));
                }
                else
                {
                    entries = entries.Where(e => string.IsNullOrEmpty(e.PlaybackReport.ErrorMessage));
                }
            }

            entries = entries.AsNoTracking();

            var totalCount = await entries.CountAsync().ConfigureAwait(false);

            var orderedEntries = ApplyOrdering(entries, query.OrderBy);

            var results = await orderedEntries
                .Skip(query.Skip ?? 0)
                .Take(query.Limit ?? 100)
                .Select(entity => new PlaybackReportDto
                {
                    Id = entity.PlaybackReport.Id,
                    UserId = entity.PlaybackReport.UserId,
                    Username = entity.Username,
                    ItemId = entity.PlaybackReport.ItemId,
                    ItemName = entity.PlaybackReport.ItemName,
                    ItemType = entity.PlaybackReport.ItemType,
                    SeriesName = entity.PlaybackReport.SeriesName,
                    SeasonNumber = entity.PlaybackReport.SeasonNumber,
                    EpisodeNumber = entity.PlaybackReport.EpisodeNumber,
                    Artist = entity.PlaybackReport.Artist,
                    Album = entity.PlaybackReport.Album,
                    DeviceId = entity.PlaybackReport.DeviceId,
                    DeviceName = entity.PlaybackReport.DeviceName,
                    ClientName = entity.PlaybackReport.ClientName,
                    PlaySessionId = entity.PlaybackReport.PlaySessionId,
                    SessionId = entity.PlaybackReport.SessionId,
                    StartTimeUtc = entity.PlaybackReport.StartTimeUtc,
                    EndTimeUtc = entity.PlaybackReport.EndTimeUtc,
                    DurationSeconds = entity.PlaybackReport.DurationSeconds,
                    StartPositionTicks = entity.PlaybackReport.StartPositionTicks,
                    EndPositionTicks = entity.PlaybackReport.EndPositionTicks,
                    ItemRuntimeTicks = entity.PlaybackReport.ItemRuntimeTicks,
                    CompletionPercentage = entity.PlaybackReport.CompletionPercentage,
                    PlayedToCompletion = entity.PlaybackReport.PlayedToCompletion,
                    WasTranscoded = entity.PlaybackReport.WasTranscoded,
                    VideoCodec = entity.PlaybackReport.VideoCodec,
                    AudioCodec = entity.PlaybackReport.AudioCodec,
                    Container = entity.PlaybackReport.Container,
                    Bitrate = entity.PlaybackReport.Bitrate,
                    Width = entity.PlaybackReport.Width,
                    Height = entity.PlaybackReport.Height,
                    Protocol = entity.PlaybackReport.Protocol,
                    PlayMethod = entity.PlaybackReport.PlayMethod,
                    RemoteEndPoint = entity.PlaybackReport.RemoteEndPoint,
                    IsLocal = entity.PlaybackReport.IsLocal,
                    LibraryId = entity.PlaybackReport.LibraryId,
                    LibraryName = entity.PlaybackReport.LibraryName,
                    ErrorMessage = entity.PlaybackReport.ErrorMessage,
                    DateCreated = entity.PlaybackReport.DateCreated,
                    LogSeverity = entity.PlaybackReport.LogSeverity
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new QueryResult<PlaybackReportDto>(query.Skip, totalCount, results);
        }
    }

    /// <inheritdoc/>
    public async Task<PlaybackReportStats> GetStatsAsync(PlaybackReportQuery query)
    {
        var dbContext = await _provider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            var entries = from p in dbContext.PlaybackReports
                          join u in dbContext.Users on p.UserId equals u.Id into ugj
                          from u in ugj.DefaultIfEmpty()
                          select new { PlaybackReport = p, Username = u.Username };

            if (query.UserId is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.UserId == query.UserId.Value);
            }

            if (query.ItemId is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.ItemId == query.ItemId.Value);
            }

            if (!string.IsNullOrEmpty(query.DeviceId))
            {
                entries = entries.Where(e => e.PlaybackReport.DeviceId == query.DeviceId);
            }

            if (!string.IsNullOrEmpty(query.LibraryId))
            {
                var libId = Guid.Parse(query.LibraryId);
                entries = entries.Where(e => e.PlaybackReport.LibraryId == libId);
            }

            if (query.MinDate is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.DateCreated >= query.MinDate.Value);
            }

            if (query.MaxDate is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.DateCreated <= query.MaxDate.Value);
            }

            if (!string.IsNullOrEmpty(query.ItemType))
            {
                entries = entries.Where(e => e.PlaybackReport.ItemType == query.ItemType);
            }

            if (query.WasTranscoded is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.WasTranscoded == query.WasTranscoded.Value);
            }

            if (query.PlayedToCompletion is not null)
            {
                entries = entries.Where(e => e.PlaybackReport.PlayedToCompletion == query.PlayedToCompletion.Value);
            }

            if (query.HasError is not null)
            {
                if (query.HasError.Value)
                {
                    entries = entries.Where(e => !string.IsNullOrEmpty(e.PlaybackReport.ErrorMessage));
                }
                else
                {
                    entries = entries.Where(e => string.IsNullOrEmpty(e.PlaybackReport.ErrorMessage));
                }
            }

            entries = entries.AsNoTracking();

            var allEntries = await entries.ToListAsync().ConfigureAwait(false);

            var hasDuration = allEntries.Where(e => e.PlaybackReport.DurationSeconds.HasValue).ToList();
            var hasCompletion = allEntries.Where(e => e.PlaybackReport.CompletionPercentage.HasValue).ToList();

            var stats = new PlaybackReportStats
            {
                TotalPlays = allEntries.Count,
                UniqueUsers = allEntries.Select(e => e.PlaybackReport.UserId).Distinct().Count(),
                UniqueItems = allEntries.Select(e => e.PlaybackReport.ItemId).Distinct().Count(),
                TotalDurationSeconds = hasDuration.Sum(e => e.PlaybackReport.DurationSeconds!.Value),
                AverageDurationSeconds = hasDuration.Count > 0 ? hasDuration.Average(e => e.PlaybackReport.DurationSeconds!.Value) : 0,
                AverageCompletionPercentage = hasCompletion.Count > 0 ? hasCompletion.Average(e => e.PlaybackReport.CompletionPercentage!.Value) : 0,
                TranscodedPlays = allEntries.Count(e => e.PlaybackReport.WasTranscoded),
                DirectPlayPlays = allEntries.Count(e => e.PlaybackReport.PlayMethod == "DirectPlay"),
                DirectStreamPlays = allEntries.Count(e => e.PlaybackReport.PlayMethod == "DirectStream"),
                ErrorPlays = allEntries.Count(e => !string.IsNullOrEmpty(e.PlaybackReport.ErrorMessage)),
                PlaysByItemType = allEntries
                    .GroupBy(e => e.PlaybackReport.ItemType)
                    .ToDictionary(g => g.Key, g => (long)g.Count()),
                PlaysByDevice = allEntries
                    .GroupBy(e => e.PlaybackReport.DeviceName)
                    .ToDictionary(g => g.Key, g => (long)g.Count()),
                PlaysByPlayMethod = allEntries
                    .GroupBy(e => e.PlaybackReport.PlayMethod ?? "Unknown")
                    .ToDictionary(g => g.Key, g => (long)g.Count()),
                PlaysByDate = allEntries
                    .GroupBy(e => e.PlaybackReport.DateCreated.Date)
                    .ToDictionary(g => g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g => (long)g.Count()),
                TopUsers = allEntries
                    .GroupBy(e => e.PlaybackReport.UserId)
                    .Select(g =>
                    {
                        var hasCompletion = g.Where(x => x.PlaybackReport.CompletionPercentage.HasValue).ToList();
                        return new UserPlaybackSummary
                        {
                            UserId = g.Key,
                            Username = g.First().Username ?? "Unknown",
                            PlayCount = g.Count(),
                            TotalDurationSeconds = g.Where(x => x.PlaybackReport.DurationSeconds.HasValue).Sum(x => x.PlaybackReport.DurationSeconds!.Value),
                            AverageCompletionPercentage = hasCompletion.Count > 0 ? hasCompletion.Average(x => x.PlaybackReport.CompletionPercentage!.Value) : 0
                        };
                    })
                    .OrderByDescending(x => x.PlayCount)
                    .Take(10)
                    .ToDictionary(x => x.UserId, x => x),
                TopItems = allEntries
                    .GroupBy(e => e.PlaybackReport.ItemId)
                    .Select(g =>
                    {
                        var hasCompletion = g.Where(x => x.PlaybackReport.CompletionPercentage.HasValue).ToList();
                        return new ItemPlaybackSummary
                        {
                            ItemId = g.Key,
                            ItemName = g.First().PlaybackReport.ItemName,
                            ItemType = g.First().PlaybackReport.ItemType,
                            PlayCount = g.Count(),
                            TotalDurationSeconds = g.Where(x => x.PlaybackReport.DurationSeconds.HasValue).Sum(x => x.PlaybackReport.DurationSeconds!.Value),
                            AverageCompletionPercentage = hasCompletion.Count > 0 ? hasCompletion.Average(x => x.PlaybackReport.CompletionPercentage!.Value) : 0,
                            UniqueUsers = g.Select(x => x.PlaybackReport.UserId).Distinct().Count()
                        };
                    })
                    .OrderByDescending(x => x.PlayCount)
                    .Take(10)
                    .ToDictionary(x => x.ItemId, x => x)
            };

            return stats;
        }
    }

    /// <inheritdoc/>
    public async Task CleanAsync(DateTime startDate)
    {
        var dbContext = await _provider.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            await dbContext.PlaybackReports
                .Where(entry => entry.DateCreated <= startDate)
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);
        }
    }

    private IOrderedQueryable<ExpandedPlaybackReport> ApplyOrdering(IQueryable<ExpandedPlaybackReport> query, IReadOnlyCollection<(PlaybackReportSortBy, MulletaFlix.Database.Implementations.Enums.SortOrder)>? sorting)
    {
        if (sorting is null || sorting.Count == 0)
        {
            return query.OrderByDescending(e => e.PlaybackReport.DateCreated);
        }

        IOrderedQueryable<ExpandedPlaybackReport> ordered = null!;

        foreach (var (sortBy, sortOrder) in sorting)
        {
            var orderBy = MapOrderBy(sortBy);

            if (ordered == null)
            {
                ordered = sortOrder == MulletaFlix.Database.Implementations.Enums.SortOrder.Ascending
                    ? query.OrderBy(orderBy)
                    : query.OrderByDescending(orderBy);
            }
            else
            {
                ordered = sortOrder == MulletaFlix.Database.Implementations.Enums.SortOrder.Ascending
                    ? ordered.ThenBy(orderBy)
                    : ordered.ThenByDescending(orderBy);
            }
        }

        return ordered ?? query.OrderByDescending(e => e.PlaybackReport.DateCreated);
    }

    private static Expression<Func<ExpandedPlaybackReport, object>> MapOrderBy(PlaybackReportSortBy sortBy)
    {
        return sortBy switch
        {
            PlaybackReportSortBy.DateCreated => e => e.PlaybackReport.DateCreated,
            PlaybackReportSortBy.UserId => e => e.PlaybackReport.UserId,
            PlaybackReportSortBy.ItemId => e => e.PlaybackReport.ItemId,
            PlaybackReportSortBy.DurationSeconds => e => e.PlaybackReport.DurationSeconds ?? 0,
            PlaybackReportSortBy.CompletionPercentage => e => e.PlaybackReport.CompletionPercentage ?? 0,
            PlaybackReportSortBy.Bitrate => e => e.PlaybackReport.Bitrate ?? 0,
            _ => e => e.PlaybackReport.DateCreated
        };
    }

    private class ExpandedPlaybackReport
    {
        public PlaybackReport PlaybackReport { get; set; } = null!;
        public string? Username { get; set; }
    }
}