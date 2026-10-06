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

    [Fact]
    public async Task ExecuteAsync_ThrottlesRefreshBurstAndReusesDirectoryService()
    {
        // S-6: a run that finds many unidentified items must not dump every QueueRefresh call
        // into the provider manager in one uninterrupted burst. The throttle below is set to 2
        // in-flight slots so the assertion can observe the burst being paced without needing
        // tens of thousands of items or any real wall-clock waiting.
        var items = Enumerable.Range(0, 5)
            .Select(i => new Movie { Id = System.Guid.NewGuid(), Name = $"Item {i}", Path = $"path/{i}.mkv" })
            .ToArray();

        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns((InternalItemsQuery query) => query.StartIndex == 0 ? items : Array.Empty<BaseItem>());

        var directoryServiceInstances = new System.Collections.Generic.HashSet<object>();
        var queuedIds = new System.Collections.Generic.List<System.Guid>();
        var provider = new Mock<IProviderManager>();
        provider.Setup(manager => manager.QueueRefresh(
                It.IsAny<System.Guid>(),
                It.IsAny<MetadataRefreshOptions>(),
                It.IsAny<RefreshPriority>()))
            .Callback<System.Guid, MetadataRefreshOptions, RefreshPriority>((id, options, _) =>
            {
                lock (queuedIds)
                {
                    queuedIds.Add(id);
                    directoryServiceInstances.Add(options.DirectoryService);
                }
            });

        var task = new UnidentifiedMediaCleanupTask(
            library.Object,
            provider.Object,
            Mock.Of<ILocalizationManager>(),
            NullLogger<UnidentifiedMediaCleanupTask>.Instance,
            Mock.Of<IFileSystem>())
        {
            MaxConcurrentQueuedRefreshes = 2
        };

        // Gate every batch-window delay behind a TaskCompletionSource the test controls, instead
        // of waiting on the real RefreshBatchWindow: deterministic and instant either way.
        var releaseGate = new TaskCompletionSource();
        task.DelayAsync = (_, _) => releaseGate.Task;

        var executeTask = task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        // The throttle only allows 2 in-flight refreshes until the gate is released, so the
        // other 3 items must still be waiting on the semaphore at this point.
        var deadline = System.DateTime.UtcNow.AddSeconds(5);
        while (System.DateTime.UtcNow < deadline)
        {
            lock (queuedIds)
            {
                if (queuedIds.Count == 2)
                {
                    break;
                }
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        lock (queuedIds)
        {
            Assert.Equal(2, queuedIds.Count);
        }

        Assert.False(executeTask.IsCompleted);

        releaseGate.SetResult();
        await executeTask.ConfigureAwait(false);

        lock (queuedIds)
        {
            Assert.Equal(5, queuedIds.Count);
            Assert.Equal(items.Select(item => item.Id).OrderBy(id => id), queuedIds.OrderBy(id => id));
        }

        // Exactly one DirectoryService instance must have backed every QueueRefresh call: the
        // task must not allocate a new one per item.
        Assert.Single(directoryServiceInstances);
    }
}
