using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using MediaBrowser.Model.Querying;

namespace MulletaFlix.Server.Implementations.Activity;

/// <summary>
/// Interface for managing playback reports.
/// </summary>
public interface IPlaybackReportManager
{
    /// <summary>
    /// Create a new playback report entry.
    /// </summary>
    Task CreateAsync(PlaybackReport entry);

    /// <summary>
    /// Update an existing playback report entry (e.g. when playback stops).
    /// </summary>
    Task UpdateAsync(PlaybackReport entry);

    /// <summary>
    /// Get a paged list of playback report entries.
    /// </summary>
    Task<QueryResult<PlaybackReportDto>> GetPagedResultAsync(PlaybackReportQuery query);

    /// <summary>
    /// Get aggregated statistics for playback reports.
    /// </summary>
    Task<PlaybackReportStats> GetStatsAsync(PlaybackReportQuery query);

    /// <summary>
    /// Remove all playback reports before the specified date.
    /// </summary>
    Task CleanAsync(DateTime startDate);
}