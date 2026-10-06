using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class StrmProbeScheduledTaskTests
{
    [Fact]
    public void RunScope_ReportsCountsAndBoundedPartialFailure()
    {
        var measurements = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == StrmProbeMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        using (var run = StrmProbeMetrics.BeginRun())
        {
            StrmProbeMetrics.RecordScannedItems(17);
            StrmProbeMetrics.RecordCandidateItems(2);
            StrmProbeMetrics.RecordFailedItem();
            run.Complete(StrmProbeMetrics.Result.PartialFailure);
        }

        Assert.Equal(17L, Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.items.scanned")).Value);
        Assert.Equal(2L, Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.items.candidates")).Value);
        Assert.Equal(1L, Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.items.failed")).Value);
        var runMeasurement = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.runs"));
        Assert.Contains(runMeasurement.Tags, tag => tag.Key == "result" && Equals(tag.Value, "partial_failure"));
        Assert.All(measurements, item => Assert.All(item.Tags, tag => Assert.Equal("result", tag.Key)));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsNoItemsAndReleasesActiveGauge()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == StrmProbeMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var repository = new Mock<IItemRepository>();
        repository.Setup(itemRepository => itemRepository.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<BaseItem>());
        var task = new StrmProbeScheduledTask(
            repository.Object,
            Mock.Of<IFileSystem>(),
            Mock.Of<MulletaFlix.Api.Jobs.IJobQueue>(),
            NullLogger<StrmProbeScheduledTask>.Instance);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.runs"));
        Assert.Equal(1L, run.Value);
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.duration"));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.strm_probe.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Name.Contains("items.scanned", StringComparison.Ordinal));
        Assert.DoesNotContain(measurements, item => item.Name.Contains("items.candidates", StringComparison.Ordinal));
        Assert.All(measurements, item => Assert.All(item.Tags, tag =>
        {
            Assert.False(tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
            Assert.False(tag.Key.Contains("title", StringComparison.OrdinalIgnoreCase));
            Assert.False(tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase));
        }));
        repository.Verify(itemRepository => itemRepository.GetItemList(It.Is<InternalItemsQuery>(query => query.Limit == 500)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsCancellationAndReleasesActiveGauge()
    {
        var measurements = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == StrmProbeMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var task = new StrmProbeScheduledTask(
            Mock.Of<IItemRepository>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<MulletaFlix.Api.Jobs.IJobQueue>(),
            NullLogger<StrmProbeScheduledTask>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.strm_probe.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "cancelled"));
        Assert.Equal(new long[] { 1, -1 }, measurements
            .Where(item => item.Name == "mulletaflix.strm_probe.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_WaitsForJobCompletionWithoutFixedIntervalPolling()
    {
        // S-11: the task used to poll GetJob(id) every 500 ms in a loop. It must now await a
        // single event-driven WaitForCompletionAsync call signalled via TaskCompletionSource, so
        // GetJob is never called at all and progress only arrives through the onProgress callback.
        BaseItem.MediaSourceManager ??= Mock.Of<MediaBrowser.Controller.Library.IMediaSourceManager>(
            manager => manager.GetMediaStreams(It.IsAny<MediaBrowser.Controller.Persistence.MediaStreamQuery>()) == Array.Empty<MediaBrowser.Model.Entities.MediaStream>());

        var item = new MediaBrowser.Controller.Entities.Movies.Movie
        {
            Id = Guid.NewGuid(),
            Name = "Strm item",
            Path = "strm/item.strm",
            IsShortcut = true
        };

        var repository = new Mock<IItemRepository>();
        repository.Setup(itemRepository => itemRepository.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns((InternalItemsQuery query) => query.StartIndex == 0 ? new BaseItem[] { item } : Array.Empty<BaseItem>());

        var reportedProgress = new List<int>();
        var jobQueue = new Mock<MulletaFlix.Api.Jobs.IJobQueue>(MockBehavior.Strict);
        jobQueue.Setup(queue => queue.CancelByCorrelationId(It.IsAny<string>())).Returns(false);

        var enqueuedJob = new MulletaFlix.Api.Jobs.JobQueueItemDto { Id = "job-1", Status = "Queued" };
        jobQueue.Setup(queue => queue.Enqueue(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, IProgress<MulletaFlix.Api.Jobs.JobQueueProgress>, Task>>(),
                It.IsAny<string>()))
            .Returns(enqueuedJob);

        jobQueue.Setup(queue => queue.WaitForCompletionAsync(
                "job-1",
                It.IsAny<Action<MulletaFlix.Api.Jobs.JobQueueItemDto>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Action<MulletaFlix.Api.Jobs.JobQueueItemDto>? onProgress, CancellationToken _) =>
            {
                onProgress?.Invoke(new MulletaFlix.Api.Jobs.JobQueueItemDto { Id = "job-1", Status = "Running", Progress = 42 });
                return Task.FromResult<MulletaFlix.Api.Jobs.JobQueueItemDto?>(
                    new MulletaFlix.Api.Jobs.JobQueueItemDto { Id = "job-1", Status = "Completed", Progress = 100 });
            });

        var task = new StrmProbeScheduledTask(
            repository.Object,
            Mock.Of<IFileSystem>(),
            jobQueue.Object,
            NullLogger<StrmProbeScheduledTask>.Instance);

        // A plain synchronous IProgress<double> instead of System.Progress<T>: the latter posts
        // reports through a captured SynchronizationContext/ThreadPool, which races with the
        // rest of this single-threaded test and makes ordering assertions flaky.
        var progress = new SynchronousProgress<double>(value => reportedProgress.Add((int)value));

        await task.ExecuteAsync(progress, CancellationToken.None);

        // GetJob was never set up on the strict mock, so any call to it (the old polling path)
        // would throw before this assertion is reached; reaching here already proves no polling.
        jobQueue.Verify(
            queue => queue.WaitForCompletionAsync("job-1", It.IsAny<Action<MulletaFlix.Api.Jobs.JobQueueItemDto>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains(42, reportedProgress);
        Assert.Contains(100, reportedProgress);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
    }
}
