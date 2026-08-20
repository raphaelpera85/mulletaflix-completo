using System;
using Emby.Server.Implementations.ScheduledTasks;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public class ActivityLogCleanupTaskTests
{
    [Fact]
    public void ShouldDeleteActivityLog_RecentEntry_ReturnsFalse()
    {
        var cutoff = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(ActivityLogCleanupTask.ShouldDeleteActivityLog(cutoff.AddDays(1), cutoff));
    }

    [Fact]
    public void ShouldDeleteActivityLog_ExpiredEntry_ReturnsTrue()
    {
        var cutoff = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(ActivityLogCleanupTask.ShouldDeleteActivityLog(cutoff.AddDays(-91), cutoff));
    }

    [Fact]
    public void ShouldDeleteActivityLog_CutoffBoundary_ReturnsFalse()
    {
        var cutoff = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(ActivityLogCleanupTask.ShouldDeleteActivityLog(cutoff, cutoff));
    }
}
