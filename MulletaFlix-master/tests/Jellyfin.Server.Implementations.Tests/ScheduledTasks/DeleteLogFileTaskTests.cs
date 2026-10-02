using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public class DeleteLogFileTaskTests
{
    [Fact]
    public void ExecuteAsync_DeletesExpiredSerilogAndLegacyLogsButKeepsRecentLogs()
    {
        var expiredSerilogLog = new FileSystemMetadata { FullName = "logs/log_2026-09-01_001.log", Name = "log_2026-09-01_001.log" };
        var expiredLegacyLog = new FileSystemMetadata { FullName = "logs/legacy.log", Name = "legacy.log" };
        var recentSerilogLog = new FileSystemMetadata { FullName = "logs/log_2026-09-28.log", Name = "log_2026-09-28.log" };
        var files = new[] { expiredSerilogLog, expiredLegacyLog, recentSerilogLog };
        var modifiedTimes = new Dictionary<string, DateTime>
        {
            [expiredSerilogLog.FullName] = DateTime.UtcNow.AddDays(-8),
            [expiredLegacyLog.FullName] = DateTime.UtcNow.AddDays(-8),
            [recentSerilogLog.FullName] = DateTime.UtcNow.AddDays(-1)
        };

        var config = new Mock<IConfigurationManager>();
        config.SetupGet(x => x.CommonConfiguration).Returns(new BaseApplicationConfiguration { LogFileRetentionDays = 7 });
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.LogDirectoryPath).Returns("logs");
        config.SetupGet(x => x.CommonApplicationPaths).Returns(applicationPaths.Object);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.GetFiles("logs", true)).Returns(files);
        fileSystem.Setup(x => x.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns<FileSystemMetadata>(file => modifiedTimes[file.FullName]);

        var task = new DeleteLogFileTask(config.Object, fileSystem.Object, Mock.Of<ILocalizationManager>());
        task.ExecuteAsync(new Progress<double>(_ => { }), CancellationToken.None);

        fileSystem.Verify(x => x.DeleteFile(expiredSerilogLog.FullName), Times.Once);
        fileSystem.Verify(x => x.DeleteFile(expiredLegacyLog.FullName), Times.Once);
        fileSystem.Verify(x => x.DeleteFile(recentSerilogLog.FullName), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExecuteAsync_NonPositiveRetentionDoesNotDeleteEveryLogIncludingTheActiveOne(int retentionDays)
    {
        // `LogFileRetentionDays` é um int sem validação. Com 0 ou negativo o
        // corte cai em "agora" (ou no futuro) e TODO log passa a ser elegível,
        // inclusive o arquivo que está sendo escrito neste instante — apagando
        // justamente o rastro de diagnóstico que a tarefa deveria preservar.
        var todayLog = new FileSystemMetadata { FullName = "logs/log_2026-09-29.log", Name = "log_2026-09-29.log" };
        var oldLog = new FileSystemMetadata { FullName = "logs/log_2026-09-01.log", Name = "log_2026-09-01.log" };
        var files = new[] { todayLog, oldLog };
        var modifiedTimes = new Dictionary<string, DateTime>
        {
            [todayLog.FullName] = DateTime.UtcNow.AddMinutes(-1),
            [oldLog.FullName] = DateTime.UtcNow.AddDays(-30)
        };

        var fileSystem = CreateFileSystem(files, modifiedTimes);
        var task = new DeleteLogFileTask(
            CreateConfiguration(retentionDays).Object,
            fileSystem.Object,
            Mock.Of<ILocalizationManager>());

        task.ExecuteAsync(new Progress<double>(_ => { }), CancellationToken.None);

        // O log recém-escrito precisa sobreviver a uma retenção inválida.
        fileSystem.Verify(x => x.DeleteFile(todayLog.FullName), Times.Never);
    }

    [Fact]
    public void ExecuteAsync_ContinuesCleanupWhenOneFileIsLocked()
    {
        // Em Windows o log ativo fica bloqueado pelo sink. Sem tratamento, a
        // primeira IOException aborta a tarefa e os arquivos seguintes — de
        // fato vencidos — nunca são removidos, fazendo a retenção parar de
        // funcionar silenciosamente.
        var lockedLog = new FileSystemMetadata { FullName = "logs/locked.log", Name = "locked.log" };
        var deletableLog = new FileSystemMetadata { FullName = "logs/deletable.log", Name = "deletable.log" };
        var files = new[] { lockedLog, deletableLog };
        var modifiedTimes = new Dictionary<string, DateTime>
        {
            [lockedLog.FullName] = DateTime.UtcNow.AddDays(-30),
            [deletableLog.FullName] = DateTime.UtcNow.AddDays(-30)
        };

        var fileSystem = CreateFileSystem(files, modifiedTimes);
        fileSystem.Setup(x => x.DeleteFile(lockedLog.FullName))
            .Throws(new IOException("The process cannot access the file because it is being used by another process."));

        var task = new DeleteLogFileTask(
            CreateConfiguration(7).Object,
            fileSystem.Object,
            Mock.Of<ILocalizationManager>());

        task.ExecuteAsync(new Progress<double>(_ => { }), CancellationToken.None);

        fileSystem.Verify(x => x.DeleteFile(deletableLog.FullName), Times.Once);
    }

    [Fact]
    public void ExecuteAsync_HonorsLongCustomRetentionWindow()
    {
        var withinWindow = new FileSystemMetadata { FullName = "logs/within.log", Name = "within.log" };
        var beyondWindow = new FileSystemMetadata { FullName = "logs/beyond.log", Name = "beyond.log" };
        var files = new[] { withinWindow, beyondWindow };
        var modifiedTimes = new Dictionary<string, DateTime>
        {
            [withinWindow.FullName] = DateTime.UtcNow.AddDays(-200),
            [beyondWindow.FullName] = DateTime.UtcNow.AddDays(-400)
        };

        var fileSystem = CreateFileSystem(files, modifiedTimes);
        var task = new DeleteLogFileTask(
            CreateConfiguration(365).Object,
            fileSystem.Object,
            Mock.Of<ILocalizationManager>());

        task.ExecuteAsync(new Progress<double>(_ => { }), CancellationToken.None);

        fileSystem.Verify(x => x.DeleteFile(withinWindow.FullName), Times.Never);
        fileSystem.Verify(x => x.DeleteFile(beyondWindow.FullName), Times.Once);
    }

    [Fact]
    public void ExecuteAsync_ReportsAggregateMetricsAndContinuesAfterLockedFile()
    {
        var lockedLog = new FileSystemMetadata { FullName = "private/logs/locked.log", Name = "locked.log" };
        var recentLog = new FileSystemMetadata { FullName = "private/logs/recent.log", Name = "recent.log" };
        var files = new[] { lockedLog, recentLog };
        var modifiedTimes = new Dictionary<string, DateTime>
        {
            [lockedLog.FullName] = DateTime.UtcNow.AddDays(-30),
            [recentLog.FullName] = DateTime.UtcNow
        };
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LogCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var fileSystem = CreateFileSystem(files, modifiedTimes);
        fileSystem.Setup(system => system.DeleteFile(lockedLog.FullName))
            .Throws(new IOException("private path must not be exported"));
        var task = new DeleteLogFileTask(
            CreateConfiguration(7).Object,
            fileSystem.Object,
            Mock.Of<ILocalizationManager>());

        task.ExecuteAsync(new Progress<double>(_ => { }), CancellationToken.None);

        var run = Assert.Single(measurements, item => item.Name == "mulletaflix.log_cleanup.runs");
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "partial_failure"));
        Assert.Single(measurements, item => item.Name == "mulletaflix.log_cleanup.duration");
        Assert.Single(measurements, item => item.Name == "mulletaflix.log_cleanup.files.scanned" && Equals(item.Value, 2L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.log_cleanup.files.expired" && Equals(item.Value, 1L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.log_cleanup.files.delete_attempts" && Equals(item.Value, 1L));
        Assert.Single(measurements, item => item.Name == "mulletaflix.log_cleanup.files.delete_failures" && Equals(item.Value, 1L));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.log_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
        fileSystem.Verify(system => system.DeleteFile(recentLog.FullName), Times.Never);
    }

    private static Mock<IConfigurationManager> CreateConfiguration(int retentionDays)
    {
        var config = new Mock<IConfigurationManager>();
        config.SetupGet(x => x.CommonConfiguration)
            .Returns(new BaseApplicationConfiguration { LogFileRetentionDays = retentionDays });
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.LogDirectoryPath).Returns("logs");
        config.SetupGet(x => x.CommonApplicationPaths).Returns(applicationPaths.Object);
        return config;
    }

    private static Mock<IFileSystem> CreateFileSystem(
        FileSystemMetadata[] files,
        Dictionary<string, DateTime> modifiedTimes)
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.GetFiles("logs", true)).Returns(files);
        fileSystem.Setup(x => x.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns<FileSystemMetadata>(file => modifiedTimes[file.FullName]);
        return fileSystem;
    }
}
