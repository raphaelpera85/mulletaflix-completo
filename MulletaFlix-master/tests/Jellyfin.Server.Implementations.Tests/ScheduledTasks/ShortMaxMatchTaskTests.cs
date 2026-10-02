using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Plugins.ShortMax;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class ShortMaxMatchTaskTests
{
    [Fact]
    public async Task ExecuteAsync_ReportsEmptyLibraryAndReleasesActiveGauge()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ShortMaxMatchMetrics.MeterName)
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
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([]);
        using var client = new ShortMaxClient(
            Mock.Of<IHttpClientFactory>(),
            NullLogger<ShortMaxClient>.Instance);
        var task = new ShortMaxMatchTask(
            client,
            library.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            NullLogger<ShortMaxMatchTask>.Instance);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.shortmax_match.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.shortmax_match.duration"));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.shortmax_match.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("title", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
        library.Verify(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()), Times.Once);
    }
}
