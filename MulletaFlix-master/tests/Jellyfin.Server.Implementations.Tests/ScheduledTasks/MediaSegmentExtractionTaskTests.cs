using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class MediaSegmentExtractionTaskTests
{
    [Fact]
    public async Task ExecuteAsync_ReportsEmptyLibraryAndReleasesActiveGauge()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == MediaSegmentExtractionMetrics.MeterName)
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
        library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(0);
        var task = new MediaSegmentExtractionTask(
            library.Object,
            Mock.Of<ILocalizationManager>(),
            Mock.Of<IMediaSegmentManager>());

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.segment_extraction.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.segment_extraction.duration"));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.segment_extraction.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("path", System.StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("title", System.StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", System.StringComparison.OrdinalIgnoreCase)));
        library.Verify(manager => manager.GetCount(It.IsAny<InternalItemsQuery>()), Times.Once);
    }
}
