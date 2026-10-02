using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class UnidentifiedMediaCleanupTaskTests
{
    [Fact]
    public async Task ExecuteAsync_ReportsEmptyScanAndReleasesActiveGauge()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == UnidentifiedMediaCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<BaseItem>());
        var task = new UnidentifiedMediaCleanupTask(
            library.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<ILocalizationManager>(),
            NullLogger<UnidentifiedMediaCleanupTask>.Instance,
            Mock.Of<IFileSystem>());

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.unidentified_cleanup.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.unidentified_cleanup.duration"));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.unidentified_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("path", System.StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("title", System.StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", System.StringComparison.OrdinalIgnoreCase)));
        library.Verify(manager => manager.GetItemList(It.Is<InternalItemsQuery>(query => query.Limit == 500)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsPartialFailureWithoutMediaDetailsInMetricTags()
    {
        var measurements = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == UnidentifiedMediaCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var item = new Movie { Id = System.Guid.NewGuid(), Name = "Private title", Path = "private/path.mkv" };
        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns((InternalItemsQuery query) => query.StartIndex == 0 ? [item] : Array.Empty<BaseItem>());
        var provider = new Mock<IProviderManager>();
        provider.Setup(manager => manager.QueueRefresh(
                It.IsAny<System.Guid>(),
                It.IsAny<MetadataRefreshOptions>(),
                It.IsAny<RefreshPriority>()))
            .Throws(new System.InvalidOperationException("private/path.mkv"));
        var task = new UnidentifiedMediaCleanupTask(
            library.Object,
            provider.Object,
            Mock.Of<ILocalizationManager>(),
            NullLogger<UnidentifiedMediaCleanupTask>.Instance,
            Mock.Of<IFileSystem>());

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements.Where(measurement => measurement.Name == "mulletaflix.unidentified_cleanup.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "partial_failure"));
        Assert.Equal(1L, Assert.Single(measurements.Where(measurement => measurement.Name == "mulletaflix.unidentified_cleanup.items.scanned")).Value);
        Assert.Equal(1L, Assert.Single(measurements.Where(measurement => measurement.Name == "mulletaflix.unidentified_cleanup.refreshes.failed")).Value);
        Assert.All(measurements, measurement => Assert.All(measurement.Tags, tag =>
        {
            Assert.False(tag.Key.Contains("path", System.StringComparison.OrdinalIgnoreCase));
            Assert.False(tag.Key.Contains("title", System.StringComparison.OrdinalIgnoreCase));
            Assert.False(tag.Key.Contains("user", System.StringComparison.OrdinalIgnoreCase));
        }));
    }
}
