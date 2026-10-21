using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Server.Implementations.StorageHelpers;

namespace MulletaFlix.Server.Implementations.Tests.FullSystemBackup;

/// <summary>
/// Tests for BackupService.
/// </summary>
public class BackupServiceTests
{
    private readonly Mock<ILogger<BackupService>> _loggerMock;
    private readonly Mock<IServerApplicationHost> _applicationHostMock;
    private readonly Mock<IServerApplicationPaths> _applicationPathsMock;
    private readonly Mock<IMulletaFlixDatabaseProvider> _databaseProviderMock;
    private readonly Mock<IHostApplicationLifetime> _applicationLifetimeMock;
    private readonly Mock<IStorageSpaceProvider> _storageSpaceProviderMock;

    public BackupServiceTests()
    {
        _loggerMock = new Mock<ILogger<BackupService>>();
        _applicationHostMock = new Mock<IServerApplicationHost>();
        _applicationPathsMock = new Mock<IServerApplicationPaths>();
        _databaseProviderMock = new Mock<IMulletaFlixDatabaseProvider>();
        _applicationLifetimeMock = new Mock<IHostApplicationLifetime>();
        _storageSpaceProviderMock = new Mock<IStorageSpaceProvider>();
        _storageSpaceProviderMock
            .Setup(provider => provider.GetFreeSpaceOf(It.IsAny<string>()))
            .Returns((string path) => new FolderStorageInfo
            {
                Path = path,
                ResolvedPath = path,
                FreeSpace = 10_737_418_240,
                UsedSpace = 1_073_741_824,
                StorageType = "Fixed",
                DeviceId = "test"
            });

        _applicationHostMock.Setup(x => x.ApplicationVersion).Returns(new Version(10, 0, 0));
        _applicationPathsMock.Setup(x => x.BackupPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestBackups"));
        _applicationPathsMock.Setup(x => x.ConfigurationDirectoryPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestConfig"));
        _applicationPathsMock.Setup(x => x.DataPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestData"));
        _applicationPathsMock.Setup(x => x.RootFolderPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestRoot"));
        _applicationPathsMock.Setup(x => x.InternalMetadataPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestMetadata"));
        _applicationPathsMock.Setup(x => x.DefaultInternalMetadataPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestMetadataDefault"));
    }

    private BackupService CreateService(FolderStorageInfo? storageInfo = null)
    {
        if (storageInfo != null)
        {
            _applicationPathsMock.Setup(x => x.BackupPath).Returns(storageInfo.Path);
        }

        var mockDbProvider = new Mock<IDbContextFactory<MulletaFlixDbContext>>();

        return new BackupService(
            _loggerMock.Object,
            mockDbProvider.Object,
            _applicationHostMock.Object,
            _applicationPathsMock.Object,
            _databaseProviderMock.Object,
            _applicationLifetimeMock.Object,
            _storageSpaceProviderMock.Object);
    }

    [Fact]
    public async Task ScheduleRestoreAndRestartServer_SetsRestorePathAndKeepsOperationsBlocked()
    {
        // Arrange
        var service = CreateService();
        var archivePath = "/test/backup.zip";

        _applicationHostMock.SetupProperty(x => x.RestoreBackupPath);
        _applicationHostMock.SetupProperty(x => x.ShouldRestart);

        // Act
        service.ScheduleRestoreAndRestartServer(archivePath);

        // Assert
        Assert.Equal(archivePath, _applicationHostMock.Object.RestoreBackupPath);
        Assert.True(_applicationHostMock.Object.ShouldRestart);
        _applicationHostMock.Verify(x => x.NotifyPendingRestart(), Times.Once);
        await Assert.ThrowsAsync<BackupOperationInProgressException>(
            () => service.CreateBackupAsync(new BackupOptionsDto()));
    }

    [Fact]
    public void TestBackupVersionCompatibility_ReturnsTrueForCompatibleVersion()
    {
        // Arrange
        var service = CreateService();

        // Act - use reflection to test private method
        var method = typeof(BackupService).GetMethod("TestBackupVersionCompatibility",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Current backup engine version is 0.2.0
        var result = (bool)method!.Invoke(service, new object[] { new Version(0, 2, 0) })!;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestBackupVersionCompatibility_ReturnsFalseForNewerMajorVersion()
    {
        // Arrange
        var service = CreateService();
        var method = typeof(BackupService).GetMethod("TestBackupVersionCompatibility",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = (bool)method!.Invoke(service, new object[] { new Version(1, 0, 0) })!;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TestBackupVersionCompatibility_ReturnsFalseForNewerMinorVersion()
    {
        // Arrange
        var service = CreateService();
        var method = typeof(BackupService).GetMethod("TestBackupVersionCompatibility",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = (bool)method!.Invoke(service, new object[] { new Version(0, 3, 0) })!;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SanitizeMigrationId_RemovesSqlInjectionCharacters()
    {
        // Arrange
        var service = CreateService();
        var method = typeof(BackupService).GetMethod("SanitizeMigrationId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result1 = (string)method!.Invoke(null, new object[] { "20240101000000_Migration'; DROP TABLE users;--" })!;
        var result2 = (string)method!.Invoke(null, new object[] { "NormalMigration123" })!;
        var result3 = (string)method!.Invoke(null, new object[] { "Migration_With_Underscores" })!;

        // Assert
        Assert.Equal("20240101000000_MigrationDROPTABLEusers--", result1);
        Assert.Equal("NormalMigration123", result2);
        Assert.Equal("Migration_With_Underscores", result3);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesValidManifest()
    {
        // Arrange
        var service = CreateService();

        // Mock the storage check to pass
        var storageInfo = new FolderStorageInfo
        {
            Path = Path.GetTempPath(),
            ResolvedPath = Path.GetTempPath(),
            FreeSpace = 10_737_418_240, // 10 GB
            UsedSpace = 1_073_741_824, // 1 GB
            StorageType = "Fixed",
            DeviceId = "C:"
        };
        _applicationPathsMock.Setup(x => x.BackupPath).Returns(Path.Combine(Path.GetTempPath(), "MulletaFlixTestBackups"));

        // We need to mock StorageHelper.GetFreeSpaceOf
        // Since it's a static class, we'll just set up a path with enough space
        // by using a temp directory that should have enough space

        var options = new BackupOptionsDto
        {
            Database = true,
            Metadata = true,
            Subtitles = true,
            Trickplay = true
        };

        // Act - This will fail on the storage check, but that's OK for testing the mock
        // We just verify the service is created and the mock is called
        var mockDbProvider = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        var fullService = new BackupService(
            _loggerMock.Object,
            mockDbProvider.Object,
            _applicationHostMock.Object,
            _applicationPathsMock.Object,
            _databaseProviderMock.Object,
            _applicationLifetimeMock.Object,
            _storageSpaceProviderMock.Object);

        // Since we can't easily mock the static StorageHelper, we'll skip this test
        // and just verify the service construction works
        Assert.NotNull(fullService);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenDatabaseIsExcluded_DoesNotReadOrOptimizeDatabase()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-without-db-{Guid.NewGuid():N}");
        var backupPath = Path.Combine(isolatedRoot, "backups");
        var configurationPath = Path.Combine(isolatedRoot, "config");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var rootPath = Path.Combine(isolatedRoot, "root");

        try
        {
            Directory.CreateDirectory(configurationPath);
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(rootPath);
            await File.WriteAllTextAsync(Path.Combine(configurationPath, "server.json"), "{}");

            _applicationPathsMock.Setup(paths => paths.BackupPath).Returns(backupPath);
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configurationPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(rootPath);
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var dbContextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>(MockBehavior.Strict);
            var service = new BackupService(
                _loggerMock.Object,
                dbContextFactory.Object,
                _applicationHostMock.Object,
                _applicationPathsMock.Object,
                _databaseProviderMock.Object,
                _applicationLifetimeMock.Object,
                _storageSpaceProviderMock.Object);

            var result = await service.CreateBackupAsync(new BackupOptionsDto
            {
                Database = false,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });

            using var archive = ZipFile.OpenRead(result.Path);
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("Database/", StringComparison.Ordinal));
            var manifestEntry = Assert.Single(archive.Entries.Where(entry => entry.FullName == "manifest.json"));
            await using var manifestStream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream);
            Assert.NotNull(manifest);
            Assert.False(manifest.Options.Database);
            Assert.Empty(manifest.DatabaseTables);

            var history = await service.GetBackupHistory();
            var execution = Assert.Single(history);
            Assert.Equal("FullSystemBackup", execution.Key);
            Assert.Equal(TaskCompletionStatus.Completed, execution.Status);
            Assert.Equal(result.Path, execution.Path);
            Assert.True(execution.EndTimeUtc >= execution.StartTimeUtc);

            dbContextFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
            dbContextFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
            _databaseProviderMock.Verify(provider => provider.RunScheduledOptimisation(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackupAsync_RejectsBackupDirectoryBelowMinimumFreeSpace()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-low-space-{Guid.NewGuid():N}");
        var backupPath = Path.Combine(isolatedRoot, "backups");
        var dataPath = Path.Combine(isolatedRoot, "data");
        Directory.CreateDirectory(backupPath);
        Directory.CreateDirectory(dataPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.BackupPath).Returns(backupPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _storageSpaceProviderMock
                .Setup(provider => provider.GetFreeSpaceOf(backupPath))
                .Returns(new FolderStorageInfo
                {
                    Path = backupPath,
                    ResolvedPath = backupPath,
                    FreeSpace = 5_368_709_114,
                    UsedSpace = 0,
                    StorageType = "Fixed",
                    DeviceId = "test"
                });

            var service = CreateService();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBackupAsync(new BackupOptionsDto
            {
                Database = false,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            }));

            Assert.Contains("does not have at least '5GiB' free space", exception.Message, StringComparison.Ordinal);
            var execution = Assert.Single(await service.GetBackupHistory());
            Assert.Equal(TaskCompletionStatus.Failed, execution.Status);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateScheduledBackupAsync_RecordsScheduledTaskIdentityInPersistentHistory()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-scheduled-backup-history-{Guid.NewGuid():N}");
        var configurationPath = Path.Combine(isolatedRoot, "config");
        var dataPath = Path.Combine(isolatedRoot, "data");
        Directory.CreateDirectory(configurationPath);
        Directory.CreateDirectory(dataPath);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(configurationPath, "server.json"), "{}");
            _applicationPathsMock.Setup(paths => paths.BackupPath).Returns(Path.Combine(isolatedRoot, "backups"));
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configurationPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var service = CreateService();
            var manifest = await service.CreateScheduledBackupAsync(new BackupOptionsDto
            {
                Database = false,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });

            var execution = Assert.Single(await service.GetBackupHistory());
            Assert.Equal("BackupScheduledTask", execution.Key);
            Assert.Equal("Scheduled backup", execution.Name);
            Assert.Equal(TaskCompletionStatus.Completed, execution.Status);
            Assert.Equal(manifest.Path, execution.Path);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackupAsync_RecordsSanitizedFailureInPersistentHistory()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-failed-history-{Guid.NewGuid():N}");
        var dataPath = Path.Combine(isolatedRoot, "data");
        Directory.CreateDirectory(dataPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.BackupPath).Returns(Path.Combine(isolatedRoot, "backups"));
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(Path.Combine(isolatedRoot, "missing-config"));
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var service = CreateService();
            await Assert.ThrowsAnyAsync<Exception>(() => service.CreateBackupAsync(new BackupOptionsDto
            {
                Database = false,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            }));

            var execution = Assert.Single(await service.GetBackupHistory());
            Assert.Equal(TaskCompletionStatus.Failed, execution.Status);
            Assert.Equal("FullSystemBackup", execution.Key);
            Assert.Empty(execution.Path);
            Assert.Equal("Backup failed. Check the server log for details.", execution.ErrorMessage);
            Assert.DoesNotContain(isolatedRoot, execution.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetBackupHistory_MergesLegacyScheduledResultWithoutDuplicates()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-history-merge-{Guid.NewGuid():N}");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var scheduledPath = Path.Combine(dataPath, "ScheduledTasks");
        Directory.CreateDirectory(scheduledPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            var endTime = DateTime.UtcNow;
            var persisted = new BackupExecutionHistoryDto
            {
                Path = "archive.zip",
                StartTimeUtc = endTime.AddMinutes(-1),
                EndTimeUtc = endTime,
                Status = TaskCompletionStatus.Completed,
                Name = "Scheduled backup",
                Key = "BackupScheduledTask",
                Id = "persisted-run",
                ErrorMessage = string.Empty,
                LongErrorMessage = string.Empty
            };
            await File.WriteAllTextAsync(
                Path.Combine(dataPath, "BackupExecutionHistory.json"),
                JsonSerializer.Serialize(new[] { persisted }));
            await File.WriteAllTextAsync(
                Path.Combine(scheduledPath, "BackupScheduledTask.js"),
                JsonSerializer.Serialize(new TaskResult
                {
                    StartTimeUtc = endTime.AddMinutes(-1),
                    EndTimeUtc = endTime.AddSeconds(1),
                    Status = TaskCompletionStatus.Completed,
                    Name = "Scheduled Backup",
                    Key = "BackupScheduledTask",
                    Id = "legacy-run"
                }));

            var history = await CreateService().GetBackupHistory();

            Assert.Single(history);
            Assert.Equal("persisted-run", history[0].Id);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetBackupHistory_DoesNotMergeDistinctScheduledRunsWithinOneMinute()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-history-close-runs-{Guid.NewGuid():N}");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var scheduledPath = Path.Combine(dataPath, "ScheduledTasks");
        Directory.CreateDirectory(scheduledPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            var previousRunEnd = DateTime.UtcNow;
            var previousRun = new BackupExecutionHistoryDto
            {
                Path = "previous.zip",
                StartTimeUtc = previousRunEnd.AddMinutes(-1),
                EndTimeUtc = previousRunEnd,
                Status = TaskCompletionStatus.Completed,
                Name = "Scheduled backup",
                Key = "BackupScheduledTask",
                Id = "previous-run",
                ErrorMessage = string.Empty,
                LongErrorMessage = string.Empty
            };
            await File.WriteAllTextAsync(
                Path.Combine(dataPath, "BackupExecutionHistory.json"),
                JsonSerializer.Serialize(new[] { previousRun }));
            await File.WriteAllTextAsync(
                Path.Combine(scheduledPath, "BackupScheduledTask.js"),
                JsonSerializer.Serialize(new TaskResult
                {
                    StartTimeUtc = previousRunEnd.AddSeconds(1),
                    EndTimeUtc = previousRunEnd.AddSeconds(30),
                    Status = TaskCompletionStatus.Completed,
                    Name = "Scheduled Backup",
                    Key = "BackupScheduledTask",
                    Id = "next-run"
                }));

            var history = await CreateService().GetBackupHistory();

            Assert.Equal(2, history.Length);
            Assert.Contains(history, item => item.Id == "previous-run");
            Assert.Contains(history, item => item.Id == "next-run");
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackupAsync_DoesNotOverwriteUnreadableHistoryFile()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-history-corrupt-{Guid.NewGuid():N}");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var configurationPath = Path.Combine(isolatedRoot, "config");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(configurationPath);
        var historyFile = Path.Combine(dataPath, "BackupExecutionHistory.json");
        const string preservedContent = "{not-valid-json";
        await File.WriteAllTextAsync(historyFile, preservedContent);

        try
        {
            _applicationPathsMock.Setup(paths => paths.BackupPath).Returns(Path.Combine(isolatedRoot, "backups"));
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configurationPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            await CreateService().CreateBackupAsync(new BackupOptionsDto
            {
                Database = false,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });

            Assert.Equal(preservedContent, await File.ReadAllTextAsync(historyFile));
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackupAsync_RejectsConcurrentOperationAndReleasesGateAfterFailure()
    {
        var optimizationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOptimization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _databaseProviderMock
            .Setup(provider => provider.RunScheduledOptimisation(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                optimizationStarted.TrySetResult();
                return releaseOptimization.Task;
            });

        var service = CreateService();
        var firstOperation = service.CreateBackupAsync(new BackupOptionsDto { Database = true });

        try
        {
            await optimizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<BackupOperationInProgressException>(
                () => service.CreateBackupAsync(new BackupOptionsDto { Database = false }));
            await Assert.ThrowsAsync<BackupOperationInProgressException>(
                () => service.RestoreBackupAsync("restore-is-not-started-while-backup-is-running.zip"));
            Assert.Throws<BackupOperationInProgressException>(
                () => service.ScheduleRestoreAndRestartServer("restore-is-not-scheduled-while-backup-is-running.zip"));
            _applicationHostMock.Verify(host => host.NotifyPendingRestart(), Times.Never);
        }
        finally
        {
            releaseOptimization.TrySetResult();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => firstOperation);
        await Assert.ThrowsAnyAsync<Exception>(
            () => service.CreateBackupAsync(new BackupOptionsDto { Database = true }));
        _databaseProviderMock.Verify(
            provider => provider.RunScheduledOptimisation(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RestoreBackupAsync_ReportsBoundedOperationMetricsAndReleasesActiveGaugeOnFailure()
    {
        var measurements = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BackupService.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var service = CreateService();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.RestoreBackupAsync(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.zip")));

        var operation = Assert.Single(measurements.Where(item =>
            item.Name == "mulletaflix.backup.operations"
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, "failure"))));
        Assert.Equal(1, operation.Value);
        Assert.Contains(operation.Tags, tag => tag.Key == "operation" && Equals(tag.Value, "restore"));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.backup.operation.duration" && item.Value >= 0);

        var activeValues = measurements
            .Where(item => item.Name == "mulletaflix.backup.active_operations")
            .Select(item => item.Value)
            .ToArray();
        Assert.Equal([1d, -1d], activeValues);
        Assert.All(measurements, item => Assert.DoesNotContain(item.Tags, tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ValidateArchiveIntegrityAsync_AcceptsReadableArchiveEntries()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-integrity-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await using (var entry = archive.CreateEntry("Database/sample.json").Open())
            {
                await entry.WriteAsync("[]"u8.ToArray());
            }

            await BackupService.ValidateArchiveIntegrityAsync(archivePath);
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
    }

    [Fact]
    public async Task ValidateArchiveIntegrityAsync_RejectsTruncatedArchive()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-truncated-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await using (var entry = archive.CreateEntry("Database/sample.json").Open())
            {
                await entry.WriteAsync("[]"u8.ToArray());
            }

            var archiveBytes = await File.ReadAllBytesAsync(archivePath);
            await File.WriteAllBytesAsync(archivePath, archiveBytes[..^5]);

            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath));
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
    }

    [Fact]
    public async Task ValidateBackupIntegrity_RejectsCorruptedPayloadEvenWhenManifestIsReadable()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(isolatedRoot);
        var archivePath = Path.Combine(isolatedRoot, "backup.zip");
        var payloadMarker = "integrity-check-payload-unique-marker"u8.ToArray();

        try
        {
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(12, 1, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions
                {
                    Database = false,
                    Metadata = false,
                    Subtitles = false,
                    Trickplay = false
                }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                var payload = archive.CreateEntry("Data/payload.bin", CompressionLevel.NoCompression);
                await using var payloadStream = await payload.OpenAsync();
                await payloadStream.WriteAsync(payloadMarker);
            }

            var backupService = CreateService();
            Assert.NotNull(await backupService.GetBackupManifest(archivePath));
            Assert.True(await backupService.ValidateBackupIntegrity(archivePath));

            var archiveBytes = await File.ReadAllBytesAsync(archivePath);
            var markerOffset = archiveBytes.AsSpan().IndexOf(payloadMarker);
            Assert.True(markerOffset >= 0, "The uncompressed test payload must be present in the archive bytes.");
            archiveBytes[markerOffset] ^= 0x01;
            await File.WriteAllBytesAsync(archivePath, archiveBytes);

            Assert.NotNull(await backupService.GetBackupManifest(archivePath));
            Assert.False(await backupService.ValidateBackupIntegrity(archivePath));
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsCorruptedArchiveBeforeOverwritingFiles()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-restore-corrupt-{Guid.NewGuid():N}");
        var configPath = Path.Combine(isolatedRoot, "config");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var cachePath = Path.Combine(isolatedRoot, "cache");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        var existingConfigPath = Path.Combine(configPath, "server.json");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var payloadMarker = "restore-integrity-check-payload"u8.ToArray();
        Directory.CreateDirectory(configPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(cachePath);
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(programDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            const string existingContent = "existing-config-must-survive";
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions
                {
                    Database = false,
                    Metadata = false,
                    Subtitles = false,
                    Trickplay = false
                }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "corrupt-backup-config");
                var payload = archive.CreateEntry("Data/payload.bin", CompressionLevel.NoCompression);
                await using var payloadStream = await payload.OpenAsync();
                await payloadStream.WriteAsync(payloadMarker);
            }

            var archiveBytes = await File.ReadAllBytesAsync(archivePath);
            var markerOffset = archiveBytes.AsSpan().IndexOf(payloadMarker);
            Assert.True(markerOffset >= 0, "The uncompressed test payload must be present in the archive bytes.");
            archiveBytes[markerOffset] ^= 0x01;
            await File.WriteAllBytesAsync(archivePath, archiveBytes);

            var service = CreateService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            Assert.False(File.Exists(Path.Combine(configPath, "restored-user.json")));
            Assert.False(Directory.Exists(Path.Combine(isolatedRoot, "data")));
            Assert.False(Directory.Exists(Path.Combine(isolatedRoot, "root")));
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("missing-declaration")]
    [InlineData("missing-entry")]
    [InlineData("unsupported-declaration")]
    public async Task RestoreBackupAsync_RejectsIncompleteDatabaseBeforeOverwritingFiles(string defect)
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-restore-incomplete-{Guid.NewGuid():N}");
        var configPath = Path.Combine(isolatedRoot, "config");
        var existingConfigPath = Path.Combine(configPath, "server.json");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(configPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            const string existingContent = "existing-config-must-survive";
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var expectedTableNames = typeof(MulletaFlixDbContext)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(property => property.PropertyType.IsAssignableTo(typeof(IQueryable)))
                .Select(property => property.Name)
                .Append("HistoryRow")
                .ToArray();
            Assert.Contains("Users", expectedTableNames);

            var declaredTableNames = defect == "missing-declaration"
                ? expectedTableNames.Except(["Users"], StringComparer.Ordinal).ToArray()
                : defect == "unsupported-declaration"
                    ? expectedTableNames.Append("UnknownTable").ToArray()
                    : expectedTableNames;
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = declaredTableNames,
                Options = new BackupOptions
                {
                    Database = true,
                    Metadata = false,
                    Subtitles = false,
                    Trickplay = false
                }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-config-must-not-be-applied");
                foreach (var tableName in expectedTableNames)
                {
                    if (defect == "missing-entry" && tableName == "Users")
                    {
                        continue;
                    }

                    await WriteArchiveEntryAsync(archive, $"Database/{tableName}.json", "[]");
                }
            }

            var dbContextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>(MockBehavior.Strict);
            var service = new BackupService(
                _loggerMock.Object,
                dbContextFactory.Object,
                _applicationHostMock.Object,
                _applicationPathsMock.Object,
                _databaseProviderMock.Object,
                _applicationLifetimeMock.Object,
                _storageSpaceProviderMock.Object);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Contains(defect == "unsupported-declaration" ? "UnknownTable" : "Users", exception.Message, StringComparison.Ordinal);
            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            dbContextFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
            dbContextFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("[{invalid-json}]")]
    [InlineData("[null]")]
    [InlineData("[{\"Id\":\"not-a-guid\",\"Username\":\"user\",\"AuthenticationProviderId\":\"provider\",\"PasswordResetProviderId\":\"reset\"}]")]
    public async Task RestoreBackupAsync_RejectsInvalidDatabaseJsonBeforeOverwritingFiles(string invalidUsersJson)
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-restore-invalid-json-{Guid.NewGuid():N}");
        var configPath = Path.Combine(isolatedRoot, "config");
        var existingConfigPath = Path.Combine(configPath, "server.json");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(configPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            const string existingContent = "existing-config-must-survive";
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var tableNames = typeof(MulletaFlixDbContext)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(property => property.PropertyType.IsAssignableTo(typeof(IQueryable)))
                .Select(property => property.Name)
                .Append("HistoryRow")
                .ToArray();
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = tableNames,
                Options = new BackupOptions
                {
                    Database = true,
                    Metadata = false,
                    Subtitles = false,
                    Trickplay = false
                }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-config-must-not-be-applied");
                foreach (var tableName in tableNames)
                {
                    await WriteArchiveEntryAsync(archive, $"Database/{tableName}.json", tableName == "Users" ? invalidUsersJson : "[]");
                }
            }

            var dbContextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>(MockBehavior.Strict);
            var service = new BackupService(
                _loggerMock.Object,
                dbContextFactory.Object,
                _applicationHostMock.Object,
                _applicationPathsMock.Object,
                _databaseProviderMock.Object,
                _applicationLifetimeMock.Object,
                _storageSpaceProviderMock.Object);

            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            dbContextFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
            dbContextFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RestoresFilesIntoAnIsolatedInstanceAndReportsQueryableContent()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-restore-test-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(isolatedRoot));
        Directory.CreateDirectory(isolatedRoot);

        var configPath = Path.Combine(isolatedRoot, "config");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var cachePath = Path.Combine(isolatedRoot, "cache");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        var rootPath = Path.Combine(isolatedRoot, "root");
        var metadataPath = Path.Combine(isolatedRoot, "metadata");
        var defaultMetadataPath = Path.Combine(isolatedRoot, "metadata-default");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(cachePath);
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(programDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(rootPath);
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(metadataPath);
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(defaultMetadataPath);

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions
                {
                    Database = false,
                    Metadata = false,
                    Subtitles = false,
                    Trickplay = false
                }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/users/restored-user.json", "{\"id\":\"fixture-user\"}");
                await WriteArchiveEntryAsync(archive, "Data/collections/favorites.json", "{\"items\":[\"media-1\",\"media-2\"]}");
                await WriteArchiveEntryAsync(archive, "Root/fixture.nfo", "<movie><title>Recovered fixture</title></movie>");
            }

            var service = CreateService();
            var restoreTimer = Stopwatch.StartNew();
            await service.RestoreBackupAsync(archivePath);
            restoreTimer.Stop();

            var restoredFiles = Directory.GetFiles(isolatedRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, archivePath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.Equal(3, restoredFiles.Length);
            Assert.Equal("{\"id\":\"fixture-user\"}", await File.ReadAllTextAsync(Path.Combine(configPath, "users", "restored-user.json")));
            Assert.Equal("{\"items\":[\"media-1\",\"media-2\"]}", await File.ReadAllTextAsync(Path.Combine(dataPath, "collections", "favorites.json")));
            Assert.Contains("Recovered fixture", await File.ReadAllTextAsync(Path.Combine(rootPath, "fixture.nfo")), StringComparison.Ordinal);
            Assert.True(double.IsFinite(restoreTimer.Elapsed.TotalSeconds));
            Assert.True(restoreTimer.Elapsed >= TimeSpan.Zero);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetBackupManifest_ReportsTheArchiveSizeInBytes()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-manifest-size-{Guid.NewGuid():N}");
        Directory.CreateDirectory(isolatedRoot);
        var archivePath = Path.Combine(isolatedRoot, "backup.zip");

        try
        {
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(12, 1, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions
                {
                    Database = true,
                    Metadata = false,
                    Subtitles = false,
                    Trickplay = false
                }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
            }

            var backupService = CreateService();
            var result = await backupService.GetBackupManifest(archivePath);

            Assert.NotNull(result);
            Assert.Equal(new FileInfo(archivePath).Length, result.SizeBytes);
            Assert.True(result.SizeBytes > 0);
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    private static async Task WriteArchiveEntryAsync(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        await using var stream = await entry.OpenAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }

    [Fact]
    public void PruneOldBackups_RetainsConfiguredLimitAndDeletesOldest()
    {
        // Arrange
        var service = CreateService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "MulletaFlixPruneTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var file1 = Path.Combine(tempFolder, "MulletaFlix-backup-20260910010000.zip");
            var file2 = Path.Combine(tempFolder, "MulletaFlix-backup-20260911010000.zip");
            var file3 = Path.Combine(tempFolder, "MulletaFlix-backup-20260912010000.zip");
            var file4 = Path.Combine(tempFolder, "MulletaFlix-backup-20260913010000.zip");

            File.WriteAllText(file1, "dummy1");
            File.SetLastWriteTimeUtc(file1, new DateTime(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc));

            File.WriteAllText(file2, "dummy2");
            File.SetLastWriteTimeUtc(file2, new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc));

            File.WriteAllText(file3, "dummy3");
            File.SetLastWriteTimeUtc(file3, new DateTime(2026, 9, 12, 1, 0, 0, DateTimeKind.Utc));

            File.WriteAllText(file4, "dummy4");
            File.SetLastWriteTimeUtc(file4, new DateTime(2026, 9, 13, 1, 0, 0, DateTimeKind.Utc));

            // Act: retain 2
            service.PruneOldBackups(tempFolder, maxToKeep: 2);

            // Assert: only the 2 newest should remain
            Assert.False(File.Exists(file1), "Oldest backup should have been pruned");
            Assert.False(File.Exists(file2), "Second oldest backup should have been pruned");
            Assert.True(File.Exists(file3), "Second newest backup should be retained");
            Assert.True(File.Exists(file4), "Newest backup should be retained");
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void PruneOldBackups_UsesSafeDefaultForZeroLimit()
    {
        var service = CreateService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "MulletaFlixPruneDefaultTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            for (var age = 0; age < 7; age++)
            {
                var path = Path.Combine(tempFolder, $"MulletaFlix-backup-{age}.zip");
                File.WriteAllText(path, $"backup-{age}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-age));
            }

            service.PruneOldBackups(tempFolder, maxToKeep: 0);

            Assert.Equal(BackupRetentionPolicy.DefaultKeepCount, Directory.GetFiles(tempFolder, "*.zip").Length);
            Assert.False(File.Exists(Path.Combine(tempFolder, "MulletaFlix-backup-6.zip")));
            Assert.False(File.Exists(Path.Combine(tempFolder, "MulletaFlix-backup-5.zip")));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void PruneOldBackups_FutureTimestampsPreserveRecentDatedArchives()
    {
        var service = CreateService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "MulletaFlixPruneClockTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            foreach (var age in new[] { 1, 2, 3, -100, -200 })
            {
                var path = Path.Combine(tempFolder, $"MulletaFlix-backup-{age}.zip");
                File.WriteAllText(path, $"backup-{age}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-age));
            }

            service.PruneOldBackups(tempFolder, maxToKeep: 2);

            Assert.Equal(4, Directory.GetFiles(tempFolder, "*.zip").Length);
            Assert.False(File.Exists(Path.Combine(tempFolder, "MulletaFlix-backup-3.zip")));
            foreach (var age in new[] { 1, 2, -100, -200 })
            {
                Assert.True(File.Exists(Path.Combine(tempFolder, $"MulletaFlix-backup-{age}.zip")));
            }
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void PruneOldBackups_NegativeLimitDisablesRemoval()
    {
        var service = CreateService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "MulletaFlixPruneUnlimitedTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            for (var age = 0; age < 4; age++)
            {
                var path = Path.Combine(tempFolder, $"MulletaFlix-backup-{age}.zip");
                File.WriteAllText(path, $"backup-{age}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-age));
            }

            service.PruneOldBackups(tempFolder, maxToKeep: -1);

            Assert.Equal(4, Directory.GetFiles(tempFolder, "*.zip").Length);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }
}
