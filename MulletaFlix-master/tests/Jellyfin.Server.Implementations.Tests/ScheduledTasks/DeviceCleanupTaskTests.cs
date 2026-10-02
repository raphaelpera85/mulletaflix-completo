using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.ScheduledTasks;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public class DeviceCleanupTaskTests
{
    [Fact]
    public void ShouldDeleteDevice_RecentOfflineDevice_ReturnsFalse()
    {
        var lastActivity = DateTime.UtcNow.AddDays(-1);

        Assert.False(DeviceCleanupTask.ShouldDeleteDevice(lastActivity, false, DateTime.UtcNow.AddDays(-90)));
    }

    [Fact]
    public void ShouldDeleteDevice_ExpiredOfflineDevice_ReturnsTrue()
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);

        Assert.True(DeviceCleanupTask.ShouldDeleteDevice(cutoff.AddDays(-1), false, cutoff));
    }

    [Fact]
    public void ShouldDeleteDevice_ActiveExpiredDevice_ReturnsFalse()
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);

        Assert.False(DeviceCleanupTask.ShouldDeleteDevice(cutoff.AddDays(-1), true, cutoff));
    }

    [Fact]
    public async System.Threading.Tasks.Task ExecuteAsync_EmptyDeviceList_ReportsAggregatedMetricsAndReleasesGauge()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DeviceCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var deviceManager = new Mock<IDeviceManager>();
        deviceManager.Setup(manager => manager.GetDevicesForUser(null))
            .Returns(new QueryResult<DeviceInfoDto>(Array.Empty<DeviceInfoDto>()));
        var sessionManager = new Mock<ISessionManager>();
        sessionManager.SetupGet(manager => manager.Sessions).Returns(Array.Empty<MediaBrowser.Controller.Session.SessionInfo>());
        var task = new DeviceCleanupTask(
            deviceManager.Object,
            sessionManager.Object,
            NullLogger<DeviceCleanupTask>.Instance);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.device_cleanup.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.device_cleanup.duration"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.device_cleanup.devices.scanned" && Equals(item.Value, 0L)));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.device_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("device", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("session", StringComparison.OrdinalIgnoreCase)));
        deviceManager.Verify(manager => manager.GetDevicesForUser(null), Times.Once);
    }
}
