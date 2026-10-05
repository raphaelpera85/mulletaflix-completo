using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Providers.MediaInfo;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.MediaInfo;

[Collection("LibraryManagerTests")]
public sealed class SubtitleScheduledTaskMetricsTests : IDisposable
{
    private const string MeterName = "MulletaFlix.Providers.SubtitlesScheduledTask";
    private readonly IRecordingsManager? _previousRecordingsManager = MediaBrowser.Controller.Entities.Video.RecordingsManager;

    public SubtitleScheduledTaskMetricsTests()
    {
        MediaBrowser.Controller.Entities.Video.RecordingsManager = Mock.Of<IRecordingsManager>();
    }

    public void Dispose()
    {
        MediaBrowser.Controller.Entities.Video.RecordingsManager = _previousRecordingsManager;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutItems_RecordsNoItemsAndReleasesActiveGauge()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        var rootFolder = new Mock<AggregateFolder>();
        rootFolder.SetupGet(folder => folder.Children).Returns(Array.Empty<BaseItem>());
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.SetupGet(manager => manager.RootFolder).Returns(rootFolder.Object);
        var task = CreateTask(libraryManager.Object);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertRun(measurements, "no_items");
        AssertCounter(measurements, "mulletaflix.subtitles.items.scanned", 0L);
        AssertCounter(measurements, "mulletaflix.subtitles.failures", 0L);
        AssertActiveGaugeBalanced(measurements);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLibraryQueryFails_RecordsFailureAndReleasesActiveGauge()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.SetupGet(manager => manager.RootFolder).Throws(new InvalidOperationException("private library error"));
        var task = CreateTask(libraryManager.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None));

        AssertRun(measurements, "failure");
        AssertCounter(measurements, "mulletaflix.subtitles.failures", 1L);
        AssertActiveGaugeBalanced(measurements);
        Assert.All(measurements, measurement => Assert.DoesNotContain(measurement.Tags, tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("title", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_RecordsCancellationAndReleasesActiveGauge()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        using var cancellation = new CancellationTokenSource();
        var library = new Folder();
        var rootFolder = new Mock<AggregateFolder>();
        rootFolder.SetupGet(folder => folder.Children).Returns([library]);
        var video = new Mock<MediaBrowser.Controller.Entities.Video>();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.SetupGet(manager => manager.RootFolder).Returns(rootFolder.Object);
        libraryManager.Setup(manager => manager.GetLibraryOptions(library))
            .Returns(new LibraryOptions { SubtitleDownloadLanguages = ["pt-BR"] });
        libraryManager.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Callback(() => cancellation.Cancel())
            .Returns([video.Object]);
        var provider = new Mock<ISubtitleProvider>();
        provider.SetupGet(item => item.Name).Returns("test-provider");
        var task = CreateTask(libraryManager.Object, subtitleProviders: [provider.Object]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));

        AssertRun(measurements, "cancelled");
        AssertCounter(measurements, "mulletaflix.subtitles.items.scanned", 0L);
        AssertCounter(measurements, "mulletaflix.subtitles.failures", 0L);
        AssertActiveGaugeBalanced(measurements);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderSearchFails_RecordsPartialFailure()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        var library = new Folder();
        var rootFolder = new Mock<AggregateFolder>();
        rootFolder.SetupGet(folder => folder.Children).Returns([library]);
        var video = new Mock<Movie>();
        video.Setup(item => item.GetMediaStreams()).Returns(Array.Empty<MediaBrowser.Model.Entities.MediaStream>());
        var options = new LibraryOptions { SubtitleDownloadLanguages = ["pt-BR"] };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.SetupGet(manager => manager.RootFolder).Returns(rootFolder.Object);
        libraryManager.Setup(manager => manager.GetLibraryOptions(It.IsAny<BaseItem>())).Returns(options);
        libraryManager.SetupSequence(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns([video.Object])
            .Returns([]);
        var subtitleManager = new Mock<ISubtitleManager>();
        subtitleManager.Setup(manager => manager.SearchSubtitles(It.IsAny<SubtitleSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("private provider error"));
        var provider = new Mock<ISubtitleProvider>();
        provider.SetupGet(item => item.Name).Returns("test-provider");
        var task = CreateTask(libraryManager.Object, subtitleManager.Object, [provider.Object]);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertRun(measurements, "partial_failure");
        AssertCounter(measurements, "mulletaflix.subtitles.items.scanned", 1L);
        AssertCounter(measurements, "mulletaflix.subtitles.failures", 1L);
        AssertActiveGaugeBalanced(measurements);
    }

    private static SubtitleScheduledTask CreateTask(
        ILibraryManager libraryManager,
        ISubtitleManager? subtitleManager = null,
        IEnumerable<ISubtitleProvider>? subtitleProviders = null)
        => new(
            libraryManager,
            Mock.Of<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(),
            subtitleManager ?? Mock.Of<ISubtitleManager>(),
            NullLogger<SubtitleScheduledTask>.Instance,
            Mock.Of<ILocalizationManager>(),
            subtitleProviders ?? []);

    private static MeterListener Listen(List<Measurement> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, CopyTags(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, CopyTags(tags))));
        listener.Start();
        return listener;
    }

    private static KeyValuePair<string, object?>[] CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new KeyValuePair<string, object?>[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            copy[i] = tags[i];
        }

        return copy;
    }

    private static void AssertRun(List<Measurement> measurements, string result)
    {
        Assert.Contains(measurements, item => item.Name == "mulletaflix.subtitles.runs"
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, result)));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.subtitles.duration");
    }

    private static void AssertCounter(List<Measurement> measurements, string name, long expected)
        => Assert.Contains(measurements, item => item.Name == name && Equals(item.Value, expected));

    private static void AssertActiveGaugeBalanced(List<Measurement> measurements)
        => Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.subtitles.active_runs")
            .Select(item => item.Value)
            .ToArray());

    private sealed record Measurement(string Name, object Value, KeyValuePair<string, object?>[] Tags);
}
