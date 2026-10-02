using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class ChapterImagesTaskTests
{
    [Fact]
    public async Task ExecuteAsync_RefreshesVideos_RecordsAggregateMetricsWithoutSensitiveTags()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var video = CreateVideo("Private title", "private/path.mkv");
        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([video]);
        var chapterManager = new Mock<IChapterManager>();
        chapterManager.Setup(manager => manager.GetChapters(video.Id)).Returns(Array.Empty<ChapterInfo>());
        chapterManager.Setup(manager => manager.RefreshChapterImages(
                video,
                It.IsAny<IDirectoryService>(),
                It.IsAny<IReadOnlyList<ChapterInfo>>(),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var task = CreateTask(library.Object, chapterManager.Object, Path.GetTempPath());

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertMetric(measurements, "mulletaflix.chapter_images.runs", "success");
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.total_per_run", 1L);
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.scanned", 1L);
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.processed", 1L);
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.failed", 0L);
        Assert.Single(measurements, item => item.Name == "mulletaflix.chapter_images.duration");
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.chapter_images.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.All(measurements, measurement => Assert.All(measurement.Tags, tag =>
        {
            if (tag.Value is string tagValue)
            {
                Assert.DoesNotContain("Private title", tagValue, StringComparison.Ordinal);
                Assert.DoesNotContain("private/path.mkv", tagValue, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("id", tag.Key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("path", tag.Key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("title", tag.Key, StringComparison.OrdinalIgnoreCase);
        }));
        chapterManager.Verify(manager => manager.RefreshChapterImages(
                video,
                It.IsAny<IDirectoryService>(),
                It.IsAny<IReadOnlyList<ChapterInfo>>(),
                true,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyLibrary_RecordsNoItems()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(0);
        var task = CreateTask(library.Object, Mock.Of<IChapterManager>(), Path.GetTempPath());

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertMetric(measurements, "mulletaflix.chapter_images.runs", "no_items");
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.total_per_run", 0L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.chapter_images.active_runs")
            .Select(item => item.Value)
            .ToArray());
        library.Verify(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RefreshReturnsFalse_RecordsPartialFailure()
    {
        var cachePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-chapter-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cachePath);
        try
        {
            var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
            using var listener = Listen(measurements);
            var video = CreateVideo("Private title", "private/path.mkv");
            var library = new Mock<ILibraryManager>();
            library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
            library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([video]);
            var chapterManager = new Mock<IChapterManager>();
            chapterManager.Setup(manager => manager.GetChapters(video.Id)).Returns(Array.Empty<ChapterInfo>());
            chapterManager.Setup(manager => manager.RefreshChapterImages(
                    video,
                    It.IsAny<IDirectoryService>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    true,
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var task = CreateTask(library.Object, chapterManager.Object, cachePath);

            await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

            AssertMetric(measurements, "mulletaflix.chapter_images.runs", "partial_failure");
            AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.failed", 1L);
            Assert.True(File.Exists(Path.Combine(cachePath, "chapter-failures.txt")));
        }
        finally
        {
            Directory.Delete(cachePath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_RecordsFailureAndRethrows()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var video = CreateVideo("Private title", "private/path.mkv");
        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([video]);
        var chapterManager = new Mock<IChapterManager>();
        chapterManager.Setup(manager => manager.GetChapters(video.Id)).Returns(Array.Empty<ChapterInfo>());
        chapterManager.Setup(manager => manager.RefreshChapterImages(
                video,
                It.IsAny<IDirectoryService>(),
                It.IsAny<IReadOnlyList<ChapterInfo>>(),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("private/path.mkv"));
        var task = CreateTask(library.Object, chapterManager.Object, Path.GetTempPath());

        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None));

        AssertMetric(measurements, "mulletaflix.chapter_images.runs", "failure");
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.failed", 1L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.chapter_images.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringRefresh_RecordsCancellationWithoutFailedVideo()
    {
        using var cancellation = new CancellationTokenSource();
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var video = CreateVideo("Private title", "private/path.mkv");
        var library = new Mock<ILibraryManager>();
        library.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
        library.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([video]);
        var chapterManager = new Mock<IChapterManager>();
        chapterManager.Setup(manager => manager.GetChapters(video.Id)).Returns(Array.Empty<ChapterInfo>());
        chapterManager.Setup(manager => manager.RefreshChapterImages(
                video,
                It.IsAny<IDirectoryService>(),
                It.IsAny<IReadOnlyList<ChapterInfo>>(),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .Returns((Video _, IDirectoryService _, IReadOnlyList<ChapterInfo> _, bool _, bool _, CancellationToken _) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<bool>(cancellation.Token);
            });
        var task = CreateTask(library.Object, chapterManager.Object, Path.GetTempPath());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));

        AssertMetric(measurements, "mulletaflix.chapter_images.runs", "cancelled");
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.failed", 0L);
        AssertMetricValue(measurements, "mulletaflix.chapter_images.videos.processed", 0L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.chapter_images.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    private static ChapterImagesTask CreateTask(ILibraryManager libraryManager, IChapterManager chapterManager, string cachePath)
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(applicationPaths => applicationPaths.CachePath).Returns(cachePath);
        return new ChapterImagesTask(
            NullLogger<ChapterImagesTask>.Instance,
            libraryManager,
            paths.Object,
            chapterManager,
            Mock.Of<IFileSystem>(),
            Mock.Of<ILocalizationManager>());
    }

    private static Video CreateVideo(string name, string path)
        => new() { Id = Guid.NewGuid(), Name = name, Path = path, DateModified = DateTime.UtcNow };

    private static void AssertMetric(
        IReadOnlyCollection<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements,
        string name,
        string expectedResult)
    {
        var run = Assert.Single(measurements, item => item.Name == name);
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, expectedResult));
    }

    private static void AssertMetricValue(
        IReadOnlyCollection<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements,
        string name,
        long expectedValue)
        => Assert.Equal(expectedValue, Assert.Single(measurements, item => item.Name == name).Value);

    private static MeterListener Listen(List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ChapterImagesMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();
        return listener;
    }
}
