using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class DeleteCacheFileTaskTests
{
    [Fact]
    public void ExecuteAsync_ReportsAggregateMetricsAndPreservesPerDirectoryRetention()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CacheCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        const string cachePath = "private/cache";
        const string tempPath = "private/temp";
        var oldCacheFile = new FileSystemMetadata { FullName = "private/cache/old.bin", LastWriteTimeUtc = DateTime.UtcNow.AddDays(-31) };
        var oldTempFile = new FileSystemMetadata { FullName = "private/temp/old.bin", LastWriteTimeUtc = DateTime.UtcNow.AddDays(-2) };
        var recentCacheFile = new FileSystemMetadata { FullName = "private/cache/recent.bin", LastWriteTimeUtc = DateTime.UtcNow.AddDays(-2) };
        var recentTempFile = new FileSystemMetadata { FullName = "private/temp/recent.bin", LastWriteTimeUtc = DateTime.UtcNow };
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(system => system.GetFiles(cachePath, true)).Returns([oldCacheFile, recentCacheFile]);
        fileSystem.Setup(system => system.GetFiles(tempPath, true)).Returns([oldTempFile, recentTempFile]);
        fileSystem.Setup(system => system.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns((FileSystemMetadata file) => file.LastWriteTimeUtc);
        fileSystem.Setup(system => system.GetDirectoryPaths(It.IsAny<string>())).Returns(Array.Empty<string>());

        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(paths => paths.CachePath).Returns(cachePath);
        applicationPaths.SetupGet(paths => paths.TempDirectory).Returns(tempPath);
        var task = new DeleteCacheFileTask(
            applicationPaths.Object,
            NullLogger<DeleteCacheFileTask>.Instance,
            fileSystem.Object,
            Mock.Of<ILocalizationManager>());

        task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None).GetAwaiter().GetResult();

        var run = Assert.Single(measurements, item => item.Name == "mulletaflix.cache_cleanup.runs");
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "success"));
        Assert.Single(measurements, item => item.Name == "mulletaflix.cache_cleanup.files.scanned" && Equals(item.Value, 4L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.cache_cleanup.files.expired" && Equals(item.Value, 2L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.cache_cleanup.files.delete_attempts" && Equals(item.Value, 2L));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.cache_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
        fileSystem.Verify(system => system.DeleteFile(oldCacheFile.FullName), Times.Once);
        fileSystem.Verify(system => system.DeleteFile(oldTempFile.FullName), Times.Once);
        fileSystem.Verify(system => system.DeleteFile(recentCacheFile.FullName), Times.Never);
        fileSystem.Verify(system => system.DeleteFile(recentTempFile.FullName), Times.Never);
    }
}
