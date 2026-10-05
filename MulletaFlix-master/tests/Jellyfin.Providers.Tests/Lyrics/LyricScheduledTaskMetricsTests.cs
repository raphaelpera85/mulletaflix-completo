using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Lyrics;
using MediaBrowser.Providers.Lyric;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Lyrics;

public sealed class LyricScheduledTaskMetricsTests
{
    private const string MeterName = "MulletaFlix.Providers.LyricsScheduledTask";

    [Fact]
    public async Task ExecuteAsync_WithoutItems_RecordsNoItemsAndReleasesActiveGauge()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        var rootFolder = new Mock<AggregateFolder>();
        rootFolder.SetupGet(folder => folder.Children).Returns(Array.Empty<BaseItem>());
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.SetupGet(manager => manager.RootFolder).Returns(rootFolder.Object);
        libraryManager.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(0);
        var task = CreateTask(libraryManager.Object);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertRun(measurements, "no_items");
        AssertCounter(measurements, "mulletaflix.lyrics.items.scanned", 0L);
        AssertCounter(measurements, "mulletaflix.lyrics.items.missing", 0L);
        AssertCounter(measurements, "mulletaflix.lyrics.searches", 0L);
        AssertCounter(measurements, "mulletaflix.lyrics.downloads", 0L);
        AssertCounter(measurements, "mulletaflix.lyrics.failures", 0L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.lyrics.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLibraryQueryFails_RecordsFailureAndReleasesActiveGauge()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>()))
            .Throws(new InvalidOperationException("private library error"));
        var task = CreateTask(libraryManager.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None));

        AssertRun(measurements, "failure");
        AssertCounter(measurements, "mulletaflix.lyrics.failures", 1L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.lyrics.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.All(measurements, measurement => Assert.DoesNotContain(measurement.Tags, tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("title", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_RecordsCancellationAndRethrows()
    {
        var measurements = new List<Measurement>();
        using var listener = Listen(measurements);
        using var cancellation = new CancellationTokenSource();
        var pendingSearch = new TaskCompletionSource<IReadOnlyList<RemoteLyricInfoDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rootFolder = new Mock<AggregateFolder>();
        rootFolder.SetupGet(folder => folder.Children).Returns([new Folder()]);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.SetupGet(manager => manager.RootFolder).Returns(rootFolder.Object);
        libraryManager.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
        libraryManager.Setup(manager => manager.GetLibraryOptions(It.IsAny<BaseItem>())).Returns(new LibraryOptions());
        var audio = new Mock<MediaBrowser.Controller.Entities.Audio.Audio>();
        audio.Setup(item => item.GetMediaStreams()).Returns(Array.Empty<MediaBrowser.Model.Entities.MediaStream>());
        libraryManager.SetupSequence(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns([audio.Object])
            .Returns([]);
        var lyricManager = new Mock<MediaBrowser.Controller.Lyrics.ILyricManager>();
        lyricManager.Setup(manager => manager.SearchLyricsAsync(It.IsAny<LyricSearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LyricSearchRequest _, CancellationToken token) =>
            {
                cancellation.Cancel();
                pendingSearch.TrySetCanceled(token);
                return pendingSearch.Task;
            });
        var task = CreateTask(libraryManager.Object, lyricManager.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));

        AssertRun(measurements, "cancelled");
        AssertCounter(measurements, "mulletaflix.lyrics.searches", 1L);
        AssertCounter(measurements, "mulletaflix.lyrics.failures", 0L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.lyrics.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    private static LyricScheduledTask CreateTask(ILibraryManager libraryManager)
        => CreateTask(libraryManager, Mock.Of<MediaBrowser.Controller.Lyrics.ILyricManager>());

    private static LyricScheduledTask CreateTask(
        ILibraryManager libraryManager,
        MediaBrowser.Controller.Lyrics.ILyricManager lyricManager)
        => new(
            libraryManager,
            lyricManager,
            NullLogger<LyricScheduledTask>.Instance,
            Mock.Of<ILocalizationManager>());

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
        Assert.Contains(measurements, item => item.Name == "mulletaflix.lyrics.runs"
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, result)));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.lyrics.duration");
    }

    private static void AssertCounter(List<Measurement> measurements, string name, long expected)
        => Assert.Contains(measurements, item => item.Name == name && Equals(item.Value, expected));

    private sealed record Measurement(string Name, object Value, KeyValuePair<string, object?>[] Tags);
}
