using System;
using System.IO;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMediaRequestQueueStatusTests
{
    [Fact]
    public void Summarize_UsesTitleCategoryAndYearAndReportsNextEpisode()
    {
        var request = new NebulaMediaRequestQueueQueryDto
        {
            RequestId = 12,
            Title = "Átomic (2025)",
            MediaType = "Series",
            Year = 2025
        };
        var capturedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var snapshot = new NebulaMediaQueueSnapshot(true, true, capturedAt, new[]
        {
            new NebulaMediaQueueEntry("Atomic (2025)", "SERIE", 2025, 1, true, true),
            new NebulaMediaQueueEntry("Atomic (2025)", "SERIE", 2025, 3, false),
            new NebulaMediaQueueEntry("Atomic (2024)", "SERIE", 2024, 2, false),
            new NebulaMediaQueueEntry("Atomic (2025)", "ANIMACAO", 2025, 4, false)
        });

        var result = NebulaMediaQueueMatcher.Summarize(request, snapshot);

        Assert.True(result.SnapshotAvailable);
        Assert.Equal(capturedAt, result.SnapshotAtUtc);
        Assert.Equal(2, result.Position);
        Assert.Equal(3, result.QueueItemCount);
        Assert.Equal(2, result.MatchingItemCount);
        Assert.Equal(1, result.CurrentItemCount);
        Assert.True(result.IsPriority);
    }

    [Fact]
    public void Summarize_UsesPendingQueueRankAndKeepsTitleCountSeparate()
    {
        var request = new NebulaMediaRequestQueueQueryDto
        {
            RequestId = 13,
            Title = "Atomic",
            MediaType = "Series"
        };
        var snapshot = new NebulaMediaQueueSnapshot(true, true, DateTime.UtcNow, new[]
        {
            new NebulaMediaQueueEntry("Atomic", "SERIE", null, 1, true),
            new NebulaMediaQueueEntry("Other", "SERIE", null, 2, false),
            new NebulaMediaQueueEntry("Atomic", "SERIE", null, 3, false),
            new NebulaMediaQueueEntry("Other", "SERIE", null, 4, false),
            new NebulaMediaQueueEntry("Atomic", "SERIE", null, 5, false)
        });

        var result = NebulaMediaQueueMatcher.Summarize(request, snapshot);

        Assert.Equal(2, result.Position);
        Assert.Equal(4, result.QueueItemCount);
        Assert.Equal(3, result.MatchingItemCount);
        Assert.Equal(1, result.CurrentItemCount);
    }

    [Fact]
    public void Summarize_DoesNotInventPositionForUnavailableSnapshot()
    {
        var result = NebulaMediaQueueMatcher.Summarize(
            new NebulaMediaRequestQueueQueryDto { Title = "The Example", MediaType = "Movie" },
            NebulaMediaQueueSnapshot.Unavailable);

        Assert.False(result.SnapshotAvailable);
        Assert.Null(result.SnapshotAtUtc);
        Assert.Null(result.Position);
        Assert.Equal(0, result.QueueItemCount);
        Assert.Equal(0, result.MatchingItemCount);
    }

    [Fact]
    public void CreateEntry_UsesWorkIdentityWithoutExposingLocalPath()
    {
        var path = Path.Combine("D:", "series", "Atomic (2025)", "Season 01", "Atomic S01E01.strm");

        var entry = NebulaMediaQueueMatcher.CreateEntry(path, 2, false);

        Assert.Equal("Atomic (2025)", entry.Title);
        Assert.Equal("SERIE", entry.MediaType);
        Assert.Equal(2025, entry.Year);
        Assert.Equal(2, entry.Position);
        Assert.DoesNotContain("D:", entry.Title, StringComparison.Ordinal);
    }
}
