using System;

namespace MediaBrowser.Model.Nebula;

/// <summary>Identifies one caller-owned media request for a path-free queue lookup.</summary>
public sealed class NebulaMediaRequestQueueQueryDto
{
    public long RequestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public int? Year { get; set; }
}

/// <summary>Queue status for one caller-owned request.</summary>
public sealed class NebulaMediaRequestQueueStatusDto
{
    public long RequestId { get; set; }

    public NebulaMediaQueuePositionDto Download { get; set; } = new();

    public NebulaMediaQueuePositionDto Upload { get; set; } = new();
}

/// <summary>Path-free position summary from one queue snapshot.</summary>
public sealed class NebulaMediaQueuePositionDto
{
    public bool SnapshotAvailable { get; set; }

    public bool IsRunning { get; set; }

    public DateTime? SnapshotAtUtc { get; set; }

    /// <summary>One-based rank of the next matching item among pending media, or null when none is queued.</summary>
    public int? Position { get; set; }

    /// <summary>Number of pending media items in this queue snapshot, excluding active uploads and sidecars.</summary>
    public int QueueItemCount { get; set; }

    /// <summary>Number of matching media files in the snapshot, including active items and excluding sidecars.</summary>
    public int MatchingItemCount { get; set; }

    /// <summary>Number of matching items currently being processed.</summary>
    public int CurrentItemCount { get; set; }

    /// <summary>Whether a matching item is marked as priority in the captured queue.</summary>
    public bool IsPriority { get; set; }
}
