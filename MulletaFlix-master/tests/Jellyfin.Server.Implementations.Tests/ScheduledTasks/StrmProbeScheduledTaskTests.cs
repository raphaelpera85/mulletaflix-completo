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
}
