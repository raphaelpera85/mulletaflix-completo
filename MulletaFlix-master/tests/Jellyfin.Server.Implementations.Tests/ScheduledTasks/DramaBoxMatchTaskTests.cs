using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Plugins.DramaBox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class DramaBoxMatchTaskTests
{
    [Fact]
    public async Task ExecuteAsync_ReportsOfflineEmptyCatalogAndLibraryWithoutSensitiveTags()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DramaBoxMatchMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var cachePath = Path.Combine(Path.GetTempPath(), "mulletaflix-dramabox-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string page = "<script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"pages\":1,\"bookList\":[]}}}</script>";
            using var httpClient = new HttpClient(new FixedContentHandler(page));
            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(httpClient);
            using var client = new DramaBoxClient(
                httpClientFactory.Object,
                Mock.Of<IServerApplicationPaths>(paths => paths.CachePath == cachePath),
                NullLogger<DramaBoxClient>.Instance);
            var library = new Mock<ILibraryManager>();
            library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(0);
            library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([]);
            var task = new DramaBoxMatchTask(
                client,
                library.Object,
                Mock.Of<IProviderManager>(),
                Mock.Of<IFileSystem>(),
                NullLogger<DramaBoxMatchTask>.Instance);

            await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

            var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.dramabox_match.runs"));
            Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
            Assert.Single(measurements.Where(item => item.Name == "mulletaflix.dramabox_match.duration"));
            Assert.Single(measurements.Where(item => item.Name == "mulletaflix.dramabox_match.index.duration"));
            Assert.Single(measurements.Where(item => item.Name == "mulletaflix.dramabox_match.library_match.duration"));
            Assert.Single(measurements.Where(item => item.Name == "mulletaflix.dramabox_match.catalog.books" && Equals(item.Value, 0L)));
            Assert.Equal(new object[] { 1L, -1L }, measurements
                .Where(item => item.Name == "mulletaflix.dramabox_match.active_runs")
                .Select(item => item.Value)
                .ToArray());
            Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
                tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
                || tag.Key.Contains("title", StringComparison.OrdinalIgnoreCase)
                || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
            library.Verify(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, recursive: true);
            }
        }
    }

    private sealed class FixedContentHandler : HttpMessageHandler
    {
        private readonly string _content;

        public FixedContentHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content)
            });
        }
    }
}
