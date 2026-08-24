using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Implementations.Activity;

/// <summary>
/// Query parameters for playback reports.
/// </summary>
public class PlaybackReportQuery : MulletaFlix.Data.Queries.PaginatedQuery
{
    public Guid? UserId { get; set; }
    public Guid? ItemId { get; set; }
    public string? DeviceId { get; set; }
    public string? LibraryId { get; set; }
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public string? ItemType { get; set; }
    public bool? WasTranscoded { get; set; }
    public bool? PlayedToCompletion { get; set; }
    public bool? HasError { get; set; }
    public IReadOnlyCollection<(PlaybackReportSortBy, MulletaFlix.Database.Implementations.Enums.SortOrder)>? OrderBy { get; set; }
}

/// <summary>
/// Sort options for playback reports.
/// </summary>
public enum PlaybackReportSortBy
{
    DateCreated,
    UserId,
    ItemId,
    DurationSeconds,
    CompletionPercentage,
    Bitrate
}

/// <summary>
/// Playback report DTO for API responses.
/// </summary>
public class PlaybackReportDto
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "";
    public string? SeriesName { get; set; }
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string PlaySessionId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public long? DurationSeconds { get; set; }
    public long? StartPositionTicks { get; set; }
    public long? EndPositionTicks { get; set; }
    public long? ItemRuntimeTicks { get; set; }
    public double? CompletionPercentage { get; set; }
    public bool PlayedToCompletion { get; set; }
    public bool WasTranscoded { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public string? Container { get; set; }
    public long? Bitrate { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Protocol { get; set; }
    public string? PlayMethod { get; set; }
    public string? RemoteEndPoint { get; set; }
    public bool IsLocal { get; set; }
    public Guid? LibraryId { get; set; }
    public string? LibraryName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DateCreated { get; set; }
    public LogLevel LogSeverity { get; set; }
}

/// <summary>
/// Aggregated playback report statistics.
/// </summary>
public class PlaybackReportStats
{
    public long TotalPlays { get; set; }
    public long UniqueUsers { get; set; }
    public long UniqueItems { get; set; }
    public long TotalDurationSeconds { get; set; }
    public double AverageDurationSeconds { get; set; }
    public double AverageCompletionPercentage { get; set; }
    public long TranscodedPlays { get; set; }
    public long DirectPlayPlays { get; set; }
    public long DirectStreamPlays { get; set; }
    public long ErrorPlays { get; set; }
    public Dictionary<string, long> PlaysByItemType { get; set; } = new();
    public Dictionary<string, long> PlaysByDevice { get; set; } = new();
    public Dictionary<string, long> PlaysByPlayMethod { get; set; } = new();
    public Dictionary<string, long> PlaysByDate { get; set; } = new();
    public Dictionary<Guid, UserPlaybackSummary> TopUsers { get; set; } = new();
    public Dictionary<Guid, ItemPlaybackSummary> TopItems { get; set; } = new();
}

/// <summary>
/// User playback summary for stats.
/// </summary>
public class UserPlaybackSummary
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public long PlayCount { get; set; }
    public long TotalDurationSeconds { get; set; }
    public double AverageCompletionPercentage { get; set; }
}

/// <summary>
/// Item playback summary for stats.
/// </summary>
public class ItemPlaybackSummary
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "";
    public long PlayCount { get; set; }
    public long TotalDurationSeconds { get; set; }
    public double AverageCompletionPercentage { get; set; }
    public long UniqueUsers { get; set; }
}