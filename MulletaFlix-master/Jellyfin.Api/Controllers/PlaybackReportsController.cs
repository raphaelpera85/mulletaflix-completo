using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations.Enums;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MulletaFlix.Server.Implementations.Activity;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Playback report controller.
/// </summary>
[Route("PlaybackReports")]
[Authorize(Policy = Policies.RequiresElevation)]
[Tags("PlaybackReports")]
public class PlaybackReportsController : BaseMulletaFlixApiController
{
    private readonly IPlaybackReportManager _playbackReportManager;
    private readonly ILogger<PlaybackReportsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackReportsController"/> class.
    /// </summary>
    /// <param name="playbackReportManager">Instance of <see cref="IPlaybackReportManager"/> interface.</param>
    /// <param name="logger">The logger.</param>
    public PlaybackReportsController(IPlaybackReportManager playbackReportManager, ILogger<PlaybackReportsController> logger)
    {
        _playbackReportManager = playbackReportManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets playback report entries.
    /// </summary>
    /// <param name="userId">Filter by user id.</param>
    /// <param name="itemId">Filter by item id.</param>
    /// <param name="deviceId">Filter by device id.</param>
    /// <param name="libraryId">Filter by library id.</param>
    /// <param name="minDate">The minimum date.</param>
    /// <param name="maxDate">The maximum date.</param>
    /// <param name="itemType">Filter by item type.</param>
    /// <param name="wasTranscoded">Filter by transcoded status.</param>
    /// <param name="playedToCompletion">Filter by completion status.</param>
    /// <param name="hasError">Filter by error status.</param>
    /// <param name="startIndex">The record index to start at.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <param name="sortBy">Specify one or more sort orders.</param>
    /// <param name="sortOrder">Sort order.</param>
    /// <response code="200">Playback reports returned.</response>
    /// <returns>A <see cref="QueryResult{PlaybackReportDto}"/> containing the playback reports.</returns>
    [HttpGet("Entries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<QueryResult<PlaybackReportDto>>> GetPlaybackReports(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? itemId,
        [FromQuery] string? deviceId,
        [FromQuery] string? libraryId,
        [FromQuery] DateTime? minDate,
        [FromQuery] DateTime? maxDate,
        [FromQuery] string? itemType,
        [FromQuery] bool? wasTranscoded,
        [FromQuery] bool? playedToCompletion,
        [FromQuery] bool? hasError,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] PlaybackReportSortBy[]? sortBy,
        [FromQuery] SortOrder[]? sortOrder)
    {
        var query = new PlaybackReportQuery
        {
            UserId = userId,
            ItemId = itemId,
            DeviceId = deviceId,
            LibraryId = libraryId,
            MinDate = minDate,
            MaxDate = maxDate,
            ItemType = itemType,
            WasTranscoded = wasTranscoded,
            PlayedToCompletion = playedToCompletion,
            HasError = hasError,
            Skip = startIndex,
            Limit = limit,
            OrderBy = GetOrderBy(sortBy ?? [], sortOrder ?? [])
        };

        return await _playbackReportManager.GetPagedResultAsync(query).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets aggregated playback report statistics.
    /// </summary>
    /// <param name="userId">Filter by user id.</param>
    /// <param name="itemId">Filter by item id.</param>
    /// <param name="deviceId">Filter by device id.</param>
    /// <param name="libraryId">Filter by library id.</param>
    /// <param name="minDate">The minimum date.</param>
    /// <param name="maxDate">The maximum date.</param>
    /// <param name="itemType">Filter by item type.</param>
    /// <param name="wasTranscoded">Filter by transcoded status.</param>
    /// <param name="playedToCompletion">Filter by completion status.</param>
    /// <param name="hasError">Filter by error status.</param>
    /// <response code="200">Playback report statistics returned.</response>
    /// <returns>A <see cref="PlaybackReportStats"/> containing the aggregated statistics.</returns>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlaybackReportStats>> GetPlaybackReportStats(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? itemId,
        [FromQuery] string? deviceId,
        [FromQuery] string? libraryId,
        [FromQuery] DateTime? minDate,
        [FromQuery] DateTime? maxDate,
        [FromQuery] string? itemType,
        [FromQuery] bool? wasTranscoded,
        [FromQuery] bool? playedToCompletion,
        [FromQuery] bool? hasError)
    {
        var query = new PlaybackReportQuery
        {
            UserId = userId,
            ItemId = itemId,
            DeviceId = deviceId,
            LibraryId = libraryId,
            MinDate = minDate,
            MaxDate = maxDate,
            ItemType = itemType,
            WasTranscoded = wasTranscoded,
            PlayedToCompletion = playedToCompletion,
            HasError = hasError
        };

        var stats = await _playbackReportManager.GetStatsAsync(query).ConfigureAwait(false);
        return stats;
    }

    private static (PlaybackReportSortBy SortBy, SortOrder SortOrder)[] GetOrderBy(
        IReadOnlyList<PlaybackReportSortBy> sortBy,
        IReadOnlyList<SortOrder> requestedSortOrder)
    {
        if (sortBy.Count == 0)
        {
            return [];
        }

        var result = new (PlaybackReportSortBy, SortOrder)[sortBy.Count];
        var i = 0;

        for (; i < requestedSortOrder.Count; i++)
        {
            result[i] = (sortBy[i], requestedSortOrder[i]);
        }

        var order = requestedSortOrder.Count > 0 ? requestedSortOrder[0] : SortOrder.Ascending;
        for (; i < sortBy.Count; i++)
        {
            result[i] = (sortBy[i], order);
        }

        return result;
    }

    /// <summary>
    /// Exports playback report entries as CSV.
    /// </summary>
    /// <param name="userId">Filter by user id.</param>
    /// <param name="itemId">Filter by item id.</param>
    /// <param name="deviceId">Filter by device id.</param>
    /// <param name="libraryId">Filter by library id.</param>
    /// <param name="minDate">The minimum date.</param>
    /// <param name="maxDate">The maximum date.</param>
    /// <param name="itemType">Filter by item type.</param>
    /// <param name="wasTranscoded">Filter by transcoded status.</param>
    /// <param name="playedToCompletion">Filter by completion status.</param>
    /// <param name="hasError">Filter by error status.</param>
    /// <param name="startIndex">The record index to start at.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <response code="200">CSV export returned.</response>
    /// <returns>A CSV file containing the playback reports.</returns>
    [HttpGet("Export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPlaybackReports(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? itemId,
        [FromQuery] string? deviceId,
        [FromQuery] string? libraryId,
        [FromQuery] DateTime? minDate,
        [FromQuery] DateTime? maxDate,
        [FromQuery] string? itemType,
        [FromQuery] bool? wasTranscoded,
        [FromQuery] bool? playedToCompletion,
        [FromQuery] bool? hasError,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit)
    {
        var query = new PlaybackReportQuery
        {
            UserId = userId,
            ItemId = itemId,
            DeviceId = deviceId,
            LibraryId = libraryId,
            MinDate = minDate,
            MaxDate = maxDate,
            ItemType = itemType,
            WasTranscoded = wasTranscoded,
            PlayedToCompletion = playedToCompletion,
            HasError = hasError,
            Skip = startIndex,
            Limit = limit ?? 10000
        };

        var result = await _playbackReportManager.GetPagedResultAsync(query).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Date,User,Item,Type,Series,Season,Episode,Device,Client,Method,DurationSeconds,CompletionPercentage,PlayedToCompletion,WasTranscoded,Library,Error");
        foreach (var entry in result.Items)
        {
            sb.AppendLine(string.Join(',',
                Csv(entry.DateCreated.ToString("o", CultureInfo.InvariantCulture)),
                Csv(entry.Username ?? entry.UserId.ToString()),
                Csv(entry.ItemName),
                Csv(entry.ItemType),
                Csv(entry.SeriesName),
                entry.SeasonNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                entry.EpisodeNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Csv(entry.DeviceName),
                Csv(entry.ClientName),
                Csv(entry.PlayMethod),
                entry.DurationSeconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                entry.CompletionPercentage?.ToString("F1", CultureInfo.InvariantCulture) ?? string.Empty,
                entry.PlayedToCompletion ? "true" : "false",
                entry.WasTranscoded ? "true" : "false",
                Csv(entry.LibraryName),
                Csv(entry.ErrorMessage)));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "playback-reports.csv");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}