using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.ScheduledTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations.Contexts;
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

    [Fact]
    public async System.Threading.Tasks.Task ExecuteAsync_ReportsAggregateMetricsWithoutPersonalData()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ActivityLogCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance);
        var dbContextFactory = new Mock<IDbContextFactory<UsersDbContext>>();
        dbContextFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbContext);
        var task = new ActivityLogCleanupTask(dbContextFactory.Object, NullLogger<ActivityLogCleanupTask>.Instance);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements, item => item.Name == "mulletaflix.activity_log_cleanup.runs");
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Single(measurements, item => item.Name == "mulletaflix.activity_log_cleanup.duration");
        Assert.Single(measurements, item => item.Name == "mulletaflix.activity_log_cleanup.entries.expired_candidates" && Equals(item.Value, 0L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.activity_log_cleanup.entries.deleted" && Equals(item.Value, 0L));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.activity_log_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("entry", StringComparison.OrdinalIgnoreCase)));
    }
}
