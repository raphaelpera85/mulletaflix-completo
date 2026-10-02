using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class DeleteTranscodeFileTaskTests
{
    [Fact]
    public async System.Threading.Tasks.Task ExecuteAsync_ReportsAggregatedFileMetricsWithoutPathTags()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TranscodeCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        const string transcodePath = "test-cache/transcodes";
        var expiredFile = new FileSystemMetadata { FullName = "private/old-transcode.ts", LastWriteTimeUtc = DateTime.UtcNow.AddDays(-2) };
        var recentFile = new FileSystemMetadata { FullName = "private/current-transcode.ts", LastWriteTimeUtc = DateTime.UtcNow };
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(system => system.GetFiles(transcodePath, true)).Returns([expiredFile, recentFile]);
        fileSystem.Setup(system => system.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns((FileSystemMetadata file) => file.LastWriteTimeUtc);
        fileSystem.Setup(system => system.GetDirectoryPaths(transcodePath)).Returns(Array.Empty<string>());

        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(paths => paths.CachePath).Returns("test-cache");
        applicationPaths.Setup(paths => paths.CreateAndCheckMarker(transcodePath, "transcode", true));
        var configurationManager = new Mock<IConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("encoding"))
            .Returns(new EncodingOptions { TranscodingTempPath = transcodePath });
        configurationManager.SetupGet(manager => manager.CommonApplicationPaths).Returns(applicationPaths.Object);
        var task = new DeleteTranscodeFileTask(
            NullLogger<DeleteTranscodeFileTask>.Instance,
            fileSystem.Object,
            configurationManager.Object,
            Mock.Of<ILocalizationManager>());

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        var run = Assert.Single(measurements, item => item.Name == "mulletaflix.transcode_cleanup.runs");
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "success"));
        Assert.Single(measurements, item => item.Name == "mulletaflix.transcode_cleanup.duration");
        Assert.Single(measurements, item => item.Name == "mulletaflix.transcode_cleanup.files.scanned" && Equals(item.Value, 2L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.transcode_cleanup.files.expired" && Equals(item.Value, 1L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.transcode_cleanup.files.delete_attempts" && Equals(item.Value, 1L));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.transcode_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
        fileSystem.Verify(system => system.DeleteFile(expiredFile.FullName), Times.Once);
        fileSystem.Verify(system => system.DeleteFile(recentFile.FullName), Times.Never);
    }
}
