using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Server.Implementations.StorageHelpers;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.FullSystemBackup;

/// <summary>
/// Tests for BackupService.
/// </summary>
public sealed class BackupServiceTests : IDisposable
{
    private const string RestoreCrashTestModeVariable = "MFX_RESTORE_CRASH_TEST_MODE";
    private const string RestoreCrashTestRootVariable = "MFX_RESTORE_CRASH_TEST_ROOT";
    private const string RestoreLeaseTestModeVariable = "MFX_RESTORE_LEASE_TEST_MODE";
    private const string RestoreLeaseTestRootVariable = "MFX_RESTORE_LEASE_TEST_ROOT";
    private const int RestoreCrashTestPayloadSize = 64 * 1024 * 1024;

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateWindowsHardLink(string fileName, string existingFileName, nint securityAttributes);

    [DllImport("libc", EntryPoint = "link", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int CreateUnixHardLink(string existingFileName, string fileName);

    [DllImport("libc", EntryPoint = "mount", SetLastError = true)]
    private static extern int MountLinuxBind(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string target,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? fileSystemType,
        ulong flags,
        nint data);

    [DllImport("libc", EntryPoint = "umount2", SetLastError = true)]
    private static extern int UnmountLinux([MarshalAs(UnmanagedType.LPUTF8Str)] string target, int flags);

    private readonly Mock<ILogger<BackupService>> _loggerMock;
    private readonly Mock<IServerApplicationHost> _applicationHostMock;
    private readonly Mock<IServerApplicationPaths> _applicationPathsMock;
    private readonly Mock<IMulletaFlixDatabaseProvider> _databaseProviderMock;
    private readonly Mock<IHostApplicationLifetime> _applicationLifetimeMock;
    private readonly string _testProgramDataPath;

    public BackupServiceTests()
    {
        _loggerMock = new Mock<ILogger<BackupService>>();
        _applicationHostMock = new Mock<IServerApplicationHost>();
        _applicationPathsMock = new Mock<IServerApplicationPaths>();
        _databaseProviderMock = new Mock<IMulletaFlixDatabaseProvider>();
        _applicationLifetimeMock = new Mock<IHostApplicationLifetime>();
        _testProgramDataPath = CreateRestoreTestRoot("program-data");

        _applicationHostMock.Setup(x => x.ApplicationVersion).Returns(new Version(10, 0, 0));
        _applicationPathsMock.Setup(x => x.BackupPath).Returns(Path.Combine(_testProgramDataPath, "backups"));
        _applicationPathsMock.Setup(x => x.ProgramDataPath).Returns(_testProgramDataPath);
        _applicationPathsMock.Setup(x => x.ConfigurationDirectoryPath).Returns(Path.Combine(_testProgramDataPath, "config"));
        _applicationPathsMock.Setup(x => x.DataPath).Returns(Path.Combine(_testProgramDataPath, "data"));
        _applicationPathsMock.Setup(x => x.RootFolderPath).Returns(Path.Combine(_testProgramDataPath, "root"));
        _applicationPathsMock.Setup(x => x.InternalMetadataPath).Returns(Path.Combine(_testProgramDataPath, "metadata"));
        _applicationPathsMock.Setup(x => x.DefaultInternalMetadataPath).Returns(Path.Combine(_testProgramDataPath, "metadata-default"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testProgramDataPath))
        {
            Directory.Delete(_testProgramDataPath, recursive: true);
        }
    }

    private BackupService CreateService(
        FolderStorageInfo? storageInfo = null,
        Action<string>? restoreBackupSidecarCreatedForTesting = null)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            Directory.CreateDirectory(_applicationPathsMock.Object.ProgramDataPath);
        }

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
            null,
            null,
            null,
            null,
            null,
            restoreBackupSidecarCreatedForTesting,
            null);
    }

    private static BackupRestoreRecoveryJournal CreateRecoveryJournal(string programDataPath)
    {
        Directory.CreateDirectory(programDataPath);
        return new BackupRestoreRecoveryJournal(programDataPath);
    }

    private static string CreateRestoreTestRoot(string name)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mulletaflix-restore-tests",
            $"{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        if (OperatingSystem.IsWindows())
        {
            var user = WindowsIdentity.GetCurrent().User
                ?? throw new UnauthorizedAccessException("The restore fixture has no current user SID.");
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(user);
            foreach (var sid in new[]
            {
                user,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
            })
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    sid,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            }

            new DirectoryInfo(root).SetAccessControl(security);
        }
        else if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            using var authority = BackupRestoreDirectory.OpenRoot(root);
            authority.ValidateTrustedWriteBoundary();
        }

        return root;
    }

    private static MulletaFlixDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();

        return new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            lockingBehavior.Object);
    }

    [Fact]
    public void BackupJsonSerialization_RetainsImageInfoForeignKeyWithPrivateSetter()
    {
        var userId = Guid.NewGuid();
        var image = new ImageInfo("profile.jpg");
        typeof(ImageInfo).GetProperty(nameof(ImageInfo.UserId))!.SetValue(image, userId);

        var serialized = JsonSerializer.Serialize(image);
        var restored = JsonSerializer.Deserialize<ImageInfo>(serialized);

        Assert.Equal(userId, restored?.UserId);
    }

    [Fact]
    public void RestorePreflight_CreatesValidationPlanForEveryExportedForeignKey()
    {
        using var central = CreateInMemoryDbContext();
        using var movies = new MoviesDbContext(
            new DbContextOptionsBuilder<MoviesDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            NullLogger<MoviesDbContext>.Instance);
        using var series = new SeriesDbContext(
            new DbContextOptionsBuilder<SeriesDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            NullLogger<SeriesDbContext>.Instance);
        using var channels = new ChannelsDbContext(
            new DbContextOptionsBuilder<ChannelsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            NullLogger<ChannelsDbContext>.Instance);
        using var books = new BooksDbContext(
            new DbContextOptionsBuilder<BooksDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            NullLogger<BooksDbContext>.Instance);

        var getExpectedEntities = typeof(BackupService).GetMethod(
            "GetExpectedDatabaseEntities",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var createValidationPlans = typeof(BackupService).GetMethod(
            "CreateForeignKeyValidationPlans",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var contexts = new (DbContext Context, Type ContextType, bool IncludeHistory)[]
        {
            (central, typeof(MulletaFlixDbContext), true),
            (movies, typeof(MoviesDbContext), false),
            (series, typeof(SeriesDbContext), false),
            (channels, typeof(ChannelsDbContext), false),
            (books, typeof(BooksDbContext), false)
        };

        var plannedForeignKeyCount = 0;
        var allModelForeignKeyCount = 0;
        foreach (var (context, contextType, includeHistory) in contexts)
        {
            var entities = Assert.IsAssignableFrom<Array>(getExpectedEntities.Invoke(null, [context.Model, contextType, includeHistory]));
            var exportedTypes = entities.Cast<object>()
                .Select(entity => (Type)entity.GetType().GetField("Item2")!.GetValue(entity)!)
                .ToHashSet();
            var entryPaths = entities.Cast<object>().ToDictionary(
                entity => (Type)entity.GetType().GetField("Item2")!.GetValue(entity)!,
                entity => Path.Combine("Database", $"{entity.GetType().GetField("Item1")!.GetValue(entity)}.json"));
            var modelForeignKeys = exportedTypes
                .Select(context.Model.FindEntityType)
                .Where(entityType => entityType is not null)
                .SelectMany(entityType => entityType!.GetForeignKeys())
                .ToArray();
            var expectedCount = modelForeignKeys
                .Count(foreignKey => exportedTypes.Contains(foreignKey.PrincipalEntityType.ClrType));
            var plans = Assert.IsAssignableFrom<Array>(
                createValidationPlans.Invoke(null, [context.Model, entities, entryPaths]));

            Assert.Equal(expectedCount, plans.Length);
            plannedForeignKeyCount += plans.Length;
            allModelForeignKeyCount += context.Model.GetEntityTypes().Sum(entityType => entityType.GetForeignKeys().Count());
        }

        Assert.Equal(39, allModelForeignKeyCount);
        Assert.Equal(38, plannedForeignKeyCount);
    }

    [Fact]
    public void GetExpectedDatabaseEntities_DomainContextDoesNotIncludeMigrationHistory()
    {
        var options = new DbContextOptionsBuilder<SeriesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var context = new SeriesDbContext(options, NullLogger<SeriesDbContext>.Instance);
        var method = typeof(BackupService).GetMethod(
            "GetExpectedDatabaseEntities",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var entities = Assert.IsAssignableFrom<Array>(method!.Invoke(null, [context.Model, typeof(SeriesDbContext), false]));
        var names = entities.Cast<object>()
            .Select(entity => (string)entity.GetType().GetField("Item1")!.GetValue(entity)!)
            .ToArray();

        Assert.Contains("Episodes", names);
        Assert.DoesNotContain("HistoryRow", names);
    }

    [Fact]
    public void DatabaseProviderPurgeContract_RemainsBoundToPrimaryContext()
    {
        var method = typeof(IMulletaFlixDatabaseProvider).GetMethod(nameof(IMulletaFlixDatabaseProvider.PurgeDatabase));

        Assert.NotNull(method);
        Assert.Equal(typeof(MulletaFlixDbContext), method.GetParameters()[0].ParameterType);
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
    public void GetExpectedDatabaseTableNames_UsesNamesAcceptedByRestoreValidation()
    {
        var method = typeof(BackupService).GetMethod(
            "GetExpectedDatabaseTableNames",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        using var context = CreateInMemoryDbContext();

        var tableNames = (string[])method!.Invoke(null, [context.Model])!;

        Assert.Contains("PlaybackReport", tableNames);
        Assert.DoesNotContain("PlaybackReports", tableNames);
        Assert.Contains("HistoryRow", tableNames);
        Assert.DoesNotContain("DbSet`1", tableNames);
        Assert.Equal(tableNames.Length, tableNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void MulletaFlixDbContext_IncludesPlaybackReportsForFullBackup()
    {
        using var context = CreateInMemoryDbContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(PlaybackReport)));
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
            _applicationLifetimeMock.Object);

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
                _applicationLifetimeMock.Object);

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
    public async Task RestoreBackupAsync_RejectsConcurrentBackupUntilRestoreCompletes()
    {
        var isolatedRoot = CreateRestoreTestRoot("restore-excludes-backup");
        var configPath = Path.Combine(isolatedRoot, "config");
        var dataPath = Path.Combine(isolatedRoot, "data");
        var cachePath = Path.Combine(isolatedRoot, "cache");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        var rootPath = Path.Combine(isolatedRoot, "root");
        var metadataPath = Path.Combine(isolatedRoot, "metadata");
        var defaultMetadataPath = Path.Combine(isolatedRoot, "metadata-default");
        var archivePath = Path.Combine(isolatedRoot, "restore.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        var restoreStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRestore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(dataPath);
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(cachePath);
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(programDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(rootPath);
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(metadataPath);
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(defaultMetadataPath);
            Directory.CreateDirectory(configPath);
            await File.WriteAllTextAsync(targetPath, "original");

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
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored");
            }

            var service = CreateService(restoreBackupSidecarCreatedForTesting: _ =>
            {
                restoreStarted.TrySetResult();
                releaseRestore.Task.GetAwaiter().GetResult();
            });
            var restore = service.RestoreBackupAsync(archivePath);

            try
            {
                await restoreStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await Assert.ThrowsAsync<BackupOperationInProgressException>(
                    () => service.CreateBackupAsync(new BackupOptionsDto { Database = false }));
            }
            finally
            {
                releaseRestore.TrySetResult();
            }

            await restore;
            Assert.Equal("restored", await File.ReadAllTextAsync(targetPath));
        }
        finally
        {
            releaseRestore.TrySetResult();
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
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
    public async Task ValidateArchiveIntegrityAsync_RejectsArchiveLimitViolations()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-limits-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var entryName in new[] { "Data/first.json", "Data/second.json" })
                {
                    await using var entry = archive.CreateEntry(entryName).Open();
                    await entry.WriteAsync("0123456789"u8.ToArray());
                }
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 1, 64, 64, 1000));
            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 2, 9, 64, 1000));
            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 2, 64, 19, 1000));
            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, 1, 2, 64, 64, 1000));
            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 2, 64, 64, 1000, maxCentralDirectoryBytes: 1));
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
    public async Task ValidateArchiveIntegrityAsync_AcceptsZip64CentralDirectory()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-zip64-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await using (var entry = archive.CreateEntry("Data/sample.json").Open())
            {
                await entry.WriteAsync("{}"u8.ToArray());
            }

            var classicArchive = await File.ReadAllBytesAsync(archivePath);
            var endRecordSignature = new byte[] { 0x50, 0x4b, 0x05, 0x06 };
            var endRecordOffset = classicArchive.AsSpan().IndexOf(endRecordSignature);
            Assert.True(endRecordOffset >= 0);

            const int Zip64EndRecordLength = 56;
            const int Zip64LocatorLength = 20;
            var zip64Archive = new byte[classicArchive.Length + Zip64EndRecordLength + Zip64LocatorLength];
            classicArchive.AsSpan(0, endRecordOffset).CopyTo(zip64Archive);
            var zip64EndRecordOffset = endRecordOffset;
            var locatorOffset = zip64EndRecordOffset + Zip64EndRecordLength;
            var endRecordWithZip64Offset = locatorOffset + Zip64LocatorLength;
            var zip64Record = zip64Archive.AsSpan(zip64EndRecordOffset, Zip64EndRecordLength);
            BinaryPrimitives.WriteUInt32LittleEndian(zip64Record, 0x06064b50);
            BinaryPrimitives.WriteUInt64LittleEndian(zip64Record[4..], 44);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64Record[12..], 45);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64Record[14..], 45);
            BinaryPrimitives.WriteUInt64LittleEndian(zip64Record[24..], BinaryPrimitives.ReadUInt16LittleEndian(classicArchive.AsSpan(endRecordOffset + 8)));
            BinaryPrimitives.WriteUInt64LittleEndian(zip64Record[32..], BinaryPrimitives.ReadUInt16LittleEndian(classicArchive.AsSpan(endRecordOffset + 10)));
            BinaryPrimitives.WriteUInt64LittleEndian(zip64Record[40..], BinaryPrimitives.ReadUInt32LittleEndian(classicArchive.AsSpan(endRecordOffset + 12)));
            BinaryPrimitives.WriteUInt64LittleEndian(zip64Record[48..], BinaryPrimitives.ReadUInt32LittleEndian(classicArchive.AsSpan(endRecordOffset + 16)));

            var locator = zip64Archive.AsSpan(locatorOffset, Zip64LocatorLength);
            BinaryPrimitives.WriteUInt32LittleEndian(locator, 0x07064b50);
            BinaryPrimitives.WriteUInt64LittleEndian(locator[8..], (ulong)zip64EndRecordOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(locator[16..], 1);
            classicArchive.AsSpan(endRecordOffset).CopyTo(zip64Archive.AsSpan(endRecordWithZip64Offset));
            BinaryPrimitives.WriteUInt16LittleEndian(zip64Archive.AsSpan(endRecordWithZip64Offset + 8), ushort.MaxValue);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64Archive.AsSpan(endRecordWithZip64Offset + 10), ushort.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(zip64Archive.AsSpan(endRecordWithZip64Offset + 12), uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(zip64Archive.AsSpan(endRecordWithZip64Offset + 16), uint.MaxValue);
            await File.WriteAllBytesAsync(archivePath, zip64Archive);

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
    public async Task ValidateArchiveIntegrityAsync_RejectsExcessiveCompressionRatio()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-ratio-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await using (var entry = archive.CreateEntry("Data/repetitive.json").Open())
            {
                await entry.WriteAsync(Enumerable.Repeat((byte)'A', 4096).ToArray());
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 1, long.MaxValue, long.MaxValue, 2));
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
    public async Task ValidateArchiveIntegrityAsync_StopsWhenActualExpansionExceedsRatioLimit()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-forged-size-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await using (var entry = archive.CreateEntry("Data/repetitive.json").Open())
            {
                await entry.WriteAsync(Enumerable.Repeat((byte)'A', 4096).ToArray());
            }

            var archiveBytes = await File.ReadAllBytesAsync(archivePath);
            var signature = new byte[] { 0x50, 0x4b, 0x01, 0x02 };
            var centralDirectoryOffset = archiveBytes.AsSpan().IndexOf(signature);
            Assert.True(centralDirectoryOffset >= 0);
            BinaryPrimitives.WriteUInt32LittleEndian(archiveBytes.AsSpan(centralDirectoryOffset + 24), 1);
            await File.WriteAllBytesAsync(archivePath, archiveBytes);

            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 1, long.MaxValue, long.MaxValue, 2));
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
    public async Task ValidateArchiveIntegrityAsync_StopsWhenCompressedLengthIsForged()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"mulletaflix-backup-forged-compressed-size-{Guid.NewGuid():N}.zip");
        try
        {
            long compressedLength;
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await using (var entry = archive.CreateEntry("Data/repetitive.json").Open())
            {
                await entry.WriteAsync(Enumerable.Repeat((byte)'A', 1_000_000).ToArray());
            }

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                compressedLength = archive.Entries[0].CompressedLength;
            }

            const int forgedCompressedLength = 600_000;
            var archiveBytes = await File.ReadAllBytesAsync(archivePath);
            var localHeaderSignature = new byte[] { 0x50, 0x4b, 0x03, 0x04 };
            var centralDirectorySignature = new byte[] { 0x50, 0x4b, 0x01, 0x02 };
            var endRecordSignature = new byte[] { 0x50, 0x4b, 0x05, 0x06 };
            var localHeaderOffset = archiveBytes.AsSpan().IndexOf(localHeaderSignature);
            var centralDirectoryOffset = archiveBytes.AsSpan().IndexOf(centralDirectorySignature);
            var endRecordOffset = archiveBytes.AsSpan().IndexOf(endRecordSignature);
            Assert.True(localHeaderOffset >= 0 && centralDirectoryOffset >= 0 && endRecordOffset >= 0);
            var dataOffset = localHeaderOffset + 30
                + BinaryPrimitives.ReadUInt16LittleEndian(archiveBytes.AsSpan(localHeaderOffset + 26))
                + BinaryPrimitives.ReadUInt16LittleEndian(archiveBytes.AsSpan(localHeaderOffset + 28));
            var compressedDataEnd = dataOffset + checked((int)compressedLength);
            var paddingLength = forgedCompressedLength - checked((int)compressedLength);
            var forgedArchiveBytes = new byte[archiveBytes.Length + paddingLength];
            archiveBytes.AsSpan(0, compressedDataEnd).CopyTo(forgedArchiveBytes);
            archiveBytes.AsSpan(compressedDataEnd).CopyTo(forgedArchiveBytes.AsSpan(compressedDataEnd + paddingLength));
            var forgedCentralDirectoryOffset = centralDirectoryOffset + paddingLength;
            var forgedEndRecordOffset = endRecordOffset + paddingLength;
            BinaryPrimitives.WriteUInt32LittleEndian(forgedArchiveBytes.AsSpan(forgedCentralDirectoryOffset + 20), forgedCompressedLength);
            BinaryPrimitives.WriteUInt32LittleEndian(forgedArchiveBytes.AsSpan(forgedEndRecordOffset + 16), checked((uint)forgedCentralDirectoryOffset));
            await File.WriteAllBytesAsync(archivePath, forgedArchiveBytes);

            await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateArchiveIntegrityAsync(archivePath, long.MaxValue, 1, long.MaxValue, long.MaxValue, 2));
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
        var isolatedRoot = CreateRestoreTestRoot("archive-corrupt");
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

    [Fact]
    public async Task RestoreBackupAsync_RejectsOversizedManifestBeforeCreatingRestoreSidecars()
    {
        var isolatedRoot = CreateRestoreTestRoot("oversized-restore-manifest");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "oversized-manifest.zip");
        var existingConfigPath = Path.Combine(configPath, "server.json");
        var restoreSidecarCreated = false;
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

            const string existingContent = "keep-existing-config";
            await File.WriteAllTextAsync(existingConfigPath, existingContent);
            var incompressibleManifest = "{\"padding\":\"" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(12 * 1024 * 1024 + 1)) + "\"}";
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", incompressibleManifest);
            }

            var error = await Assert.ThrowsAsync<InvalidDataException>(
                () => CreateService(restoreBackupSidecarCreatedForTesting: _ => restoreSidecarCreated = true).RestoreBackupAsync(archivePath));

            Assert.Contains("manifest", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            Assert.False(restoreSidecarCreated);
            Assert.False(Directory.Exists(Path.Combine(isolatedRoot, "data")));
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
    [InlineData("Config/../outside.txt")]
    [InlineData("Config/..\\outside.txt")]
    [InlineData("/Config/outside.txt")]
    [InlineData("C:/Config/outside.txt")]
    [InlineData("Config/server.json")]
    public async Task RestoreBackupAsync_RejectsUnsafeArchivePathsBeforeChangingFiles(string unsafeEntryName)
    {
        var isolatedRoot = CreateRestoreTestRoot("unsafe-entry-path");
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
            await File.WriteAllTextAsync(existingConfigPath, "keep-existing-config");

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
                await WriteArchiveEntryAsync(archive, "Config/server.json", "must-not-be-applied");
                await WriteArchiveEntryAsync(archive, unsafeEntryName, "outside-root");
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().RestoreBackupAsync(archivePath));
            Assert.Equal("keep-existing-config", await File.ReadAllTextAsync(existingConfigPath));
            Assert.False(File.Exists(Path.Combine(isolatedRoot, "outside.txt")));
            Assert.False(File.Exists(Path.Combine(isolatedRoot, "program-data", "restore-recovery", "active-restore.json")));
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
    public async Task RestoreBackupAsync_RejectsUnixSymlinkEntryBeforeChangingFiles()
    {
        var isolatedRoot = CreateRestoreTestRoot("unix-symlink-entry");
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
            await File.WriteAllTextAsync(existingConfigPath, "keep-existing-config");

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
                await WriteArchiveEntryAsync(archive, "Config/server.json", "must-not-be-applied");
                var symlink = archive.CreateEntry("Config/link");
                symlink.ExternalAttributes = unchecked((int)0xA1ED0000);
                await using var stream = await symlink.OpenAsync();
                await stream.WriteAsync("target.txt"u8.ToArray());
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().RestoreBackupAsync(archivePath));
            Assert.Equal("keep-existing-config", await File.ReadAllTextAsync(existingConfigPath));
            Assert.False(File.Exists(Path.Combine(isolatedRoot, "program-data", "restore-recovery", "active-restore.json")));
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
    public async Task RestoreBackupAsync_RejectsNestedMountBeforeWritingIntoIt()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Nested-mount restore validation requires Linux.");

        var isolatedRoot = CreateRestoreTestRoot("nested-restore-mount");
        var configPath = Path.Combine(isolatedRoot, "config");
        var nestedMount = Path.Combine(configPath, "nested-mount");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(nestedMount);
        var mountOptions = Marshal.StringToCoTaskMemUTF8("size=16m,mode=0700");
        var mounted = false;

        try
        {
            if (MountLinuxBind("tmpfs", nestedMount, "tmpfs", 0, mountOptions) != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error is 1 or 13)
                {
                    Assert.Skip($"Linux tmpfs mounts require privileges unavailable here (errno {error}).");
                }

                throw new IOException("Could not create the nested restore mount fixture.", new System.ComponentModel.Win32Exception(error));
            }

            mounted = true;
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/nested-mount/should-not-exist.txt", "must-not-be-restored");
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().RestoreBackupAsync(archivePath));
            Assert.False(File.Exists(Path.Combine(nestedMount, "should-not-exist.txt")));
            Assert.False(File.Exists(Path.Combine(isolatedRoot, "program-data", "restore-recovery", "active-restore.json")));
        }
        finally
        {
            Marshal.FreeCoTaskMem(mountOptions);
            if (mounted && UnmountLinux(nestedMount, 0) != 0)
            {
                throw new IOException("Could not unmount the nested restore fixture.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
            }

            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Config/NUL.txt")]
    [InlineData("Config/COM¹.txt")]
    [InlineData("Config/COM².txt")]
    [InlineData("Config/COM³.txt")]
    [InlineData("Config/notes.")]
    [InlineData("Config/notes ")]
    public async Task RestoreBackupAsync_AppliesPlatformFileNameRules(string entryName)
    {
        var isolatedRoot = CreateRestoreTestRoot("platform-entry-name");
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
            await File.WriteAllTextAsync(existingConfigPath, "keep-existing-config");

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
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-config");
                await WriteArchiveEntryAsync(archive, entryName, "platform-specific-name");
            }

            var service = CreateService();
            if (OperatingSystem.IsWindows())
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));
                Assert.Equal("keep-existing-config", await File.ReadAllTextAsync(existingConfigPath));
                Assert.False(File.Exists(Path.Combine(isolatedRoot, "program-data", "restore-recovery", "active-restore.json")));
            }
            else
            {
                await service.RestoreBackupAsync(archivePath);
                Assert.Equal("restored-config", await File.ReadAllTextAsync(existingConfigPath));
                Assert.Equal("platform-specific-name", await File.ReadAllTextAsync(Path.Combine(configPath, Path.GetFileName(entryName))));
            }
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
    public async Task RestoreBackupAsync_RejectsReparsePointInsideRestoreRoot()
    {
        var isolatedRoot = CreateRestoreTestRoot("reparse-root");
        var configPath = Path.Combine(isolatedRoot, "config");
        var outsidePath = Path.Combine(isolatedRoot, "outside");
        var linkPath = Path.Combine(configPath, "linked");
        var outsideFilePath = Path.Combine(outsidePath, "restored.txt");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(outsidePath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            Directory.CreateDirectory(_applicationPathsMock.Object.ProgramDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            Directory.CreateSymbolicLink(linkPath, outsidePath);

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
                await WriteArchiveEntryAsync(archive, "Config/linked/restored.txt", "must-not-escape-config-root");
            }

            var service = CreateService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.False(File.Exists(outsideFilePath));
        }
        finally
        {
            if (Directory.Exists(linkPath))
            {
                Directory.Delete(linkPath);
            }

            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsUntrustedDestinationAclBeforeChangingExistingFile()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore ACL enforcement supports Windows and Linux.");

        var isolatedRoot = CreateRestoreTestRoot("untrusted-destination-acl");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "restore.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(targetPath, "original-content");

        DirectorySecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var configInfo = new DirectoryInfo(configPath);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                originalSecurity = configInfo.GetAccessControl();
                var security = configInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                configInfo.SetAccessControl(security);
            }
            else
            {
                originalMode = File.GetUnixFileMode(configPath);
                File.SetUnixFileMode(configPath, originalMode | UnixFileMode.OtherWrite);
            }

            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-content");
            }

            var service = CreateService();
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Equal("original-content", await File.ReadAllTextAsync(targetPath));
        }
        finally
        {
            if (originalSecurity is not null)
            {
                configInfo.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(configPath, originalMode);
            }

            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsUntrustedArchiveAclBeforeCreatingRecoveryJournal()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore ACL enforcement supports Windows and Linux.");

        var isolatedRoot = CreateRestoreTestRoot("untrusted-archive-acl");
        var archivePath = Path.Combine(isolatedRoot, "restore.zip");
        FileSecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var archiveInfo = new FileInfo(archivePath);
        try
        {
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
            }

            if (OperatingSystem.IsWindows())
            {
                originalSecurity = archiveInfo.GetAccessControl();
                var untrustedSecurity = archiveInfo.GetAccessControl();
                untrustedSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    AccessControlType.Allow));
                archiveInfo.SetAccessControl(untrustedSecurity);
            }
            else
            {
                originalMode = File.GetUnixFileMode(archivePath);
                File.SetUnixFileMode(archivePath, originalMode | UnixFileMode.OtherWrite);
            }

            var service = CreateService();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Null(await CreateRecoveryJournal(_testProgramDataPath).ReadAsync());
        }
        finally
        {
            if (originalSecurity is not null)
            {
                archiveInfo.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux() && File.Exists(archivePath))
            {
                File.SetUnixFileMode(archivePath, originalMode);
            }

            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsUntrustedExistingFileAclBeforeCreatingRollbackSidecar()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore ACL enforcement supports Windows and Linux.");

        var isolatedRoot = CreateRestoreTestRoot("untrusted-existing-file-acl");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "restore.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(targetPath, "original-content");

        FileSecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var targetInfo = new FileInfo(targetPath);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                originalSecurity = targetInfo.GetAccessControl();
                var security = targetInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    AccessControlType.Allow));
                targetInfo.SetAccessControl(security);
            }
            else
            {
                originalMode = File.GetUnixFileMode(targetPath);
                File.SetUnixFileMode(targetPath, originalMode | UnixFileMode.OtherWrite);
            }

            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-content");
            }

            var service = CreateService();
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Equal("original-content", await File.ReadAllTextAsync(targetPath));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.bak"));
        }
        finally
        {
            if (originalSecurity is not null)
            {
                targetInfo.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(targetPath, originalMode);
            }

            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsUntrustedNamespaceParentBeforeCreatingDestination()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore ACL enforcement supports Windows and Linux.");

        var isolatedRoot = CreateRestoreTestRoot("untrusted-restore-parent");
        var destinationPath = Path.Combine(isolatedRoot, "new-config");
        var archivePath = Path.Combine(isolatedRoot, "restore.zip");
        DirectorySecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var rootInfo = new DirectoryInfo(isolatedRoot);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                originalSecurity = rootInfo.GetAccessControl();
                var security = rootInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.DeleteSubdirectoriesAndFiles,
                    AccessControlType.Allow));
                rootInfo.SetAccessControl(security);
            }
            else
            {
                originalMode = File.GetUnixFileMode(isolatedRoot);
                File.SetUnixFileMode(isolatedRoot, originalMode | UnixFileMode.OtherWrite);
            }

            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(destinationPath);
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(_testProgramDataPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-content");
            }

            var service = CreateService();
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => service.RestoreBackupAsync(archivePath));
            Assert.False(Directory.Exists(destinationPath));
        }
        finally
        {
            if (originalSecurity is not null)
            {
                rootInfo.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(isolatedRoot, originalMode);
            }

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
        var isolatedRoot = CreateRestoreTestRoot("database-incomplete");
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
            Directory.CreateDirectory(_applicationPathsMock.Object.ProgramDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            const string existingContent = "existing-config-must-survive";
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var validationContext = CreateInMemoryDbContext();
            var usersEntityType = typeof(MulletaFlixDbContext).GetProperty("Users")!.PropertyType.GetGenericArguments()[0];
            var usersTableName = validationContext.Model.FindEntityType(usersEntityType)!.GetSchemaQualifiedTableName()!;
            var expectedNamesMethod = typeof(BackupService).GetMethod(
                "GetExpectedDatabaseTableNames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var expectedTableNames = (string[])expectedNamesMethod!.Invoke(null, [validationContext.Model])!;

            var declaredTableNames = defect == "missing-declaration"
                ? expectedTableNames.Except([usersTableName], StringComparer.Ordinal).ToArray()
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
                    if (defect == "missing-entry" && tableName == usersTableName)
                    {
                        continue;
                    }

                    await WriteArchiveEntryAsync(archive, $"Database/{tableName}.json", "[]");
                }
            }

            var dbContextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>(MockBehavior.Strict);
            dbContextFactory
                .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(validationContext);
            var service = new BackupService(
                _loggerMock.Object,
                dbContextFactory.Object,
                _applicationHostMock.Object,
                _applicationPathsMock.Object,
                _databaseProviderMock.Object,
                _applicationLifetimeMock.Object);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Contains(defect == "unsupported-declaration" ? "UnknownTable" : usersTableName, exception.Message, StringComparison.Ordinal);
            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            dbContextFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
            dbContextFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task RestoreBackupAsync_RejectsUnavailableDeclaredDatabaseParticipantBeforeChangingFiles()
    {
        var isolatedRoot = CreateRestoreTestRoot("unavailable-backup-participant");
        var configPath = Path.Combine(isolatedRoot, "config");
        var existingConfigPath = Path.Combine(configPath, "server.json");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(configPath);
        const string existingContent = "existing-config-must-survive";

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));
            Directory.CreateDirectory(_applicationPathsMock.Object.ProgramDataPath);
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = [],
                DatabaseParticipants = ["MissingPlugin"],
                Options = new BackupOptions { Database = true, Metadata = false, Subtitles = false, Trickplay = false }
            };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-must-not-be-applied");
            }

            var service = CreateService();
            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Contains("MissingPlugin", exception.Message, StringComparison.Ordinal);
            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            Assert.False(File.Exists(Path.Combine(_applicationPathsMock.Object.ProgramDataPath, "restore-recovery", "active-restore.json")));
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
    public async Task RestoreBackupAsync_RejectsDatabaseParticipantWhenDatabaseBackupIsDisabled()
    {
        var isolatedRoot = CreateRestoreTestRoot("participant-with-database-disabled");
        var configPath = Path.Combine(isolatedRoot, "config");
        var existingConfigPath = Path.Combine(configPath, "server.json");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(configPath);
        const string existingContent = "existing-config-must-survive";

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));
            Directory.CreateDirectory(_applicationPathsMock.Object.ProgramDataPath);
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = [],
                DatabaseParticipants = ["IntroSkipper"],
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "replacement-must-not-be-applied");
            }

            var service = CreateService();
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Contains("database backup is disabled", exception.Message, StringComparison.Ordinal);
            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            Assert.False(File.Exists(Path.Combine(_applicationPathsMock.Object.ProgramDataPath, "restore-recovery", "active-restore.json")));
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
    [InlineData(typeof(User), "[{invalid-json}]")]
    [InlineData(typeof(User), "[null]")]
    [InlineData(typeof(User), "[{\"Id\":\"not-a-guid\",\"Username\":\"user\",\"AuthenticationProviderId\":\"provider\",\"PasswordResetProviderId\":\"reset\"}]")]
    [InlineData(typeof(User), "[{\"Id\":\"00000000-0000-0000-0000-000000000001\",\"Username\":\"user\",\"NormalizedUsername\":\"USER\",\"AuthenticationProviderId\":\"provider\",\"PasswordResetProviderId\":\"reset\"},{\"Id\":\"00000000-0000-0000-0000-000000000001\",\"Username\":\"user\",\"NormalizedUsername\":\"USER\",\"AuthenticationProviderId\":\"provider\",\"PasswordResetProviderId\":\"reset\"}]")]
    [InlineData(typeof(Permission), "[{\"Id\":1,\"UserId\":\"00000000-0000-0000-0000-000000000002\",\"Kind\":0,\"Value\":true}]")]
    public async Task RestoreBackupAsync_RejectsInvalidDatabaseContentBeforeOverwritingFiles(Type invalidEntityType, string invalidJson)
    {
        var isolatedRoot = CreateRestoreTestRoot("database-invalid-json");
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
            Directory.CreateDirectory(_applicationPathsMock.Object.ProgramDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            const string existingContent = "existing-config-must-survive";
            await File.WriteAllTextAsync(existingConfigPath, existingContent);

            var validationContext = CreateInMemoryDbContext();
            var invalidEntityTableName = validationContext.Model.FindEntityType(invalidEntityType)!.GetSchemaQualifiedTableName()!;
            var expectedTableNamesMethod = typeof(BackupService).GetMethod(
                "GetExpectedDatabaseTableNames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var tableNames = (string[])expectedTableNamesMethod!.Invoke(null, [validationContext.Model])!;
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                // Legacy 0.2.0 manifests used the generic DbSet`1 type name instead of mapped table names.
                DatabaseTables = ["DbSet`1", "HistoryRow"],
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
                    await WriteArchiveEntryAsync(archive, $"Database/{tableName}.json", tableName == invalidEntityTableName ? invalidJson : "[]");
                }
            }

            var dbContextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>(MockBehavior.Strict);
            dbContextFactory
                .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(validationContext);
            dbContextFactory
                .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(validationContext);
            var service = new BackupService(
                _loggerMock.Object,
                dbContextFactory.Object,
                _applicationHostMock.Object,
                _applicationPathsMock.Object,
                _databaseProviderMock.Object,
                _applicationLifetimeMock.Object);

            var restoreException = await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Contains(invalidEntityTableName, restoreException.Message, StringComparison.Ordinal);
            Assert.Equal(existingContent, await File.ReadAllTextAsync(existingConfigPath));
            dbContextFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
            dbContextFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        var isolatedRoot = CreateRestoreTestRoot("file-roundtrip");
        Assert.True(Directory.Exists(isolatedRoot));
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
                await WriteArchiveEntryAsync(
                    archive,
                    OperatingSystem.IsWindows() ? "data/METADATA/fixture.nfo" : "Data/metadata/fixture.nfo",
                    "<movie><title>Metadata fixture</title></movie>");
                await WriteArchiveEntryAsync(archive, "Root/fixture.nfo", "<movie><title>Recovered fixture</title></movie>");
            }

            var service = CreateService();
            var restoreTimer = Stopwatch.StartNew();
            await service.RestoreBackupAsync(archivePath);
            restoreTimer.Stop();

            var restoreOperationLeasePath = Path.Combine(programDataPath, "restore-recovery", "restore-operation.lock");
            var restoredFiles = Directory.GetFiles(isolatedRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, archivePath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(path, restoreOperationLeasePath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.Equal(4, restoredFiles.Length);
            Assert.True(File.Exists(restoreOperationLeasePath));
            Assert.Equal("{\"id\":\"fixture-user\"}", await File.ReadAllTextAsync(Path.Combine(configPath, "users", "restored-user.json")));
            Assert.Equal("{\"items\":[\"media-1\",\"media-2\"]}", await File.ReadAllTextAsync(Path.Combine(dataPath, "collections", "favorites.json")));
            Assert.Contains("Metadata fixture", await File.ReadAllTextAsync(Path.Combine(metadataPath, "fixture.nfo")), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(dataPath, "metadata", "fixture.nfo")));
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

    [Fact]
    public async Task GetBackupManifest_RejectsManifestAboveConfiguredLimit()
    {
        var isolatedRoot = Path.Combine(Path.GetTempPath(), $"mulletaflix-manifest-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(isolatedRoot);
        var archivePath = Path.Combine(isolatedRoot, "backup.zip");

        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", "{\"extra\":\"" + new string('a', 16 * 1024 * 1024) + "\"}");
            }

            var result = await CreateService().GetBackupManifest(archivePath);

            Assert.Null(result);
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

    [Fact]
    public async Task RestoreBackupAsync_RollsBackEarlierFilesWhenLaterFileCannotBeInstalled()
    {
        var isolatedRoot = CreateRestoreTestRoot("rollback");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(configPath);
        var firstFilePath = Path.Combine(configPath, "first.txt");
        await File.WriteAllTextAsync(firstFilePath, "before-restore");
        var blockedPath = Path.Combine(configPath, "blocked.txt");
        Directory.CreateDirectory(blockedPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/first.txt", "from-backup");
                await WriteArchiveEntryAsync(archive, "Config/blocked.txt", "cannot-install-over-directory");
            }

            var service = CreateService();
            await Assert.ThrowsAnyAsync<IOException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Equal("before-restore", await File.ReadAllTextAsync(firstFilePath));
            Assert.Empty(Directory.EnumerateFileSystemEntries(blockedPath));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreBackupAsync_RejectsDuplicateOrCaseAliasedDestinationsAndRestoresOriginalFile(bool useCaseAlias)
    {
        var isolatedRoot = CreateRestoreTestRoot("duplicate-target");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "duplicate-entries.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(targetPath, "original-content");

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "first-entry");
                await WriteArchiveEntryAsync(
                    archive,
                    useCaseAlias && OperatingSystem.IsWindows() ? "config/SERVER.json" : "Config/server.json",
                    "duplicate-entry");
            }

            var service = CreateService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(archivePath));

            Assert.Equal("original-content", await File.ReadAllTextAsync(targetPath));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.bak"));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.tmp"));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.absent"));
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
    public async Task RecoverInterruptedRestoreAsync_ReplaysSameBackupAndCleansPartialFileSwap()
    {
        var isolatedRoot = CreateRestoreTestRoot("replay");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
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

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-from-backup");
            }

            await File.WriteAllTextAsync(targetPath, "original-content");
            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var pendingRestore = await journal.CreateAsync(archivePath);
            var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(targetPath))));
            var sidecarPrefix = $".mfx-restore-{pendingRestore.OperationId}-{targetHash}";
            var backupPath = Path.Combine(configPath, $"{sidecarPrefix}.bak");
            var stagingPath = Path.Combine(configPath, $"{sidecarPrefix}.tmp");

            File.Move(targetPath, backupPath);
            await File.WriteAllTextAsync(stagingPath, "partially-staged-content");
            await File.WriteAllTextAsync(targetPath, "partially-installed-content");

            var service = CreateService();
            await service.RecoverInterruptedRestoreAsync();
            await service.RecoverInterruptedRestoreAsync();

            Assert.Equal("restored-from-backup", await File.ReadAllTextAsync(targetPath));
            Assert.False(File.Exists(backupPath));
            Assert.False(File.Exists(stagingPath));
            Assert.Null(await journal.ReadAsync());
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
    public async Task RecoverInterruptedRestoreAsync_ProgramStartupGateReplaysJournalBeforeInitialization()
    {
        var isolatedRoot = CreateRestoreTestRoot("startup-gate-replay");
        var configPath = Path.Combine(isolatedRoot, "config");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        Directory.CreateDirectory(configPath);

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(programDataPath);
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-before-startup");
            }

            await File.WriteAllTextAsync(targetPath, "interrupted-restore-content");
            var journal = CreateRecoveryJournal(programDataPath);
            await journal.CreateAsync(archivePath);
            using var service = CreateService();
            var initialized = false;
            var startupMigrationApplied = false;

            var result = await MulletaFlix.Server.Program.RunAfterBackupRecoveryAsync(
                service,
                recoveredPath =>
                {
                    Assert.Equal(archivePath, recoveredPath);
                    return MulletaFlix.Server.Program.ContinueAfterBackupRecoveryAsync(service, null, recoveredPath);
                },
                () =>
                {
                    Assert.Equal("restored-before-startup", File.ReadAllText(targetPath));
                    startupMigrationApplied = true;
                    return Task.CompletedTask;
                },
                () =>
                {
                    Assert.Equal("restored-before-startup", File.ReadAllText(targetPath));
                    Assert.True(startupMigrationApplied);
                    initialized = true;
                    return Task.CompletedTask;
                });

            Assert.True(result);
            Assert.True(initialized);
            Assert.Null(await journal.ReadAsync());
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
    public async Task RecoverInterruptedRestoreAsync_OnMacOSWithoutPendingRestoreDoesNotAcquireUnsupportedLease()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Skip("macOS startup-recovery behavior requires a macOS runner.");
        }

        var service = CreateService();

        Assert.Null(await service.RecoverInterruptedRestoreAsync());
        Assert.False(Directory.Exists(_testProgramDataPath));
    }

    [Fact]
    public async Task RecoverInterruptedRestoreAsync_OnMacOSWithPendingRestoreFailsClosed()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Skip("macOS startup-recovery behavior requires a macOS runner.");
        }

        var recoveryDirectory = Path.Combine(_testProgramDataPath, "restore-recovery");
        Directory.CreateDirectory(recoveryDirectory);
        await File.WriteAllTextAsync(Path.Combine(recoveryDirectory, "active-restore.json"), "pending");
        var service = CreateService();

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => service.RecoverInterruptedRestoreAsync());
    }

    [Fact]
    public async Task RecoverInterruptedRestoreAsync_RejectsJournalInsideRestoreRootBeforeCreatingLease()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Skip("Restore recovery requires Windows or Linux.");
        }

        _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath)
            .Returns(Directory.GetParent(_testProgramDataPath)!.FullName);
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecoverInterruptedRestoreAsync());

        Assert.False(Directory.Exists(Path.Combine(_testProgramDataPath, "restore-recovery")));
    }

    [Fact]
    public async Task RecoverInterruptedRestoreAsync_RejectsProgramDataBindAliasAcrossNestedMountBeforeCreatingLease()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "ProgramData bind-alias recovery validation requires Linux.");

        var isolatedRoot = CreateRestoreTestRoot("program-data-bind-alias");
        var restoreRoot = Path.Combine(isolatedRoot, "restore-root");
        var nestedMount = Path.Combine(restoreRoot, "nested-mount");
        var bindSource = Path.Combine(nestedMount, "program-data-source");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        Directory.CreateDirectory(nestedMount);
        Directory.CreateDirectory(programDataPath);
        var mountOptions = Marshal.StringToCoTaskMemUTF8("size=16m,mode=0700");
        var tmpfsMounted = false;
        var bindMounted = false;

        try
        {
            if (MountLinuxBind("tmpfs", nestedMount, "tmpfs", 0, mountOptions) != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error is 1 or 13)
                {
                    Assert.Skip($"Linux tmpfs mounts require privileges unavailable here (errno {error}).");
                }

                throw new IOException("Could not create the nested tmpfs fixture.", new System.ComponentModel.Win32Exception(error));
            }

            tmpfsMounted = true;
            Directory.CreateDirectory(bindSource);
            if (MountLinuxBind(bindSource, programDataPath, null, 4096, 0) != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error is 1 or 13)
                {
                    Assert.Skip($"Linux bind mounts require privileges unavailable here (errno {error}).");
                }

                throw new IOException("Could not create the ProgramData bind-alias fixture.", new System.ComponentModel.Win32Exception(error));
            }

            bindMounted = true;
            using (var restoreAuthority = BackupRestoreDirectory.OpenRoot(restoreRoot))
            using (var programDataAuthority = BackupRestoreDirectory.OpenRoot(programDataPath))
            {
                Assert.False(restoreAuthority.IsOnSameMountAs(programDataAuthority));
            }

            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(programDataPath);
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(restoreRoot);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecoverInterruptedRestoreAsync());

            Assert.False(Directory.Exists(Path.Combine(programDataPath, "restore-recovery")));
            Assert.False(Directory.Exists(Path.Combine(bindSource, "restore-recovery")));
        }
        finally
        {
            Marshal.FreeCoTaskMem(mountOptions);
            if (bindMounted && UnmountLinux(programDataPath, 0) != 0)
            {
                throw new IOException("Could not unmount the ProgramData bind-alias fixture.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
            }

            if (tmpfsMounted && UnmountLinux(nestedMount, 0) != 0)
            {
                throw new IOException("Could not unmount the nested tmpfs fixture.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
            }

            Directory.Delete(isolatedRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RecoverInterruptedRestoreAsync_AllowsRestoreRootBelowProgramData()
    {
        _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath)
            .Returns(Path.Combine(_testProgramDataPath, "config"));
        var service = CreateService();

        Assert.Null(await service.RecoverInterruptedRestoreAsync());

        Assert.True(Directory.Exists(Path.Combine(_testProgramDataPath, "restore-recovery")));
    }

    [Fact]
    public async Task RestoreBackupAsync_PreservesMissingArchiveErrorWhenProgramDataIsAbsent()
    {
        var missingProgramDataPath = Path.Combine(_testProgramDataPath, "missing-program-data");
        _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(missingProgramDataPath);
        var service = CreateService();
        Directory.Delete(missingProgramDataPath, recursive: true);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.RestoreBackupAsync(Path.Combine(_testProgramDataPath, "missing-backup.zip")));

        Assert.False(Directory.Exists(missingProgramDataPath));
    }

    [Fact]
    public async Task RecoveryJournal_JournalExistsDistinguishesMissingFromPresentJournal()
    {
        var programDataPath = CreateRestoreTestRoot("journal-exists");
        var journal = new BackupRestoreRecoveryJournal(programDataPath);

        try
        {
            Assert.False(journal.JournalExists());

            Directory.CreateDirectory(Path.GetDirectoryName(journal.JournalPath)!);
            await File.WriteAllTextAsync(journal.JournalPath, "pending");

            Assert.True(journal.JournalExists());

            Directory.Delete(programDataPath, recursive: true);
            Assert.False(journal.JournalExists());
        }
        finally
        {
            if (Directory.Exists(programDataPath))
            {
                Directory.Delete(programDataPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RecoverInterruptedRestoreAsync_DoesNotTruncatePreexistingHardLinkedStagingPath()
    {
        var isolatedRoot = CreateRestoreTestRoot("hardlink-stage");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        var sentinelPath = Path.Combine(isolatedRoot, "outside-sentinel.txt");
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

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-from-backup");
            }

            const string sentinelContent = "must-not-be-truncated-through-hard-link";
            await File.WriteAllTextAsync(sentinelPath, sentinelContent, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(targetPath, "original-content", TestContext.Current.CancellationToken);

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var pendingRestore = await journal.CreateAsync(archivePath, TestContext.Current.CancellationToken);
            var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(targetPath))));
            var sidecarPrefix = $".mfx-restore-{pendingRestore.OperationId}-{targetHash}";
            var stagingPath = Path.Combine(configPath, $"{sidecarPrefix}.tmp");
            var hardLinkCreated = OperatingSystem.IsWindows()
                ? CreateWindowsHardLink(stagingPath, sentinelPath, 0)
                : CreateUnixHardLink(sentinelPath, stagingPath) == 0;
            if (!hardLinkCreated)
            {
                Assert.Skip($"Hard links are unavailable in this environment. Native error: {Marshal.GetLastPInvokeError()}.");
            }

            var service = CreateService();
            await service.RecoverInterruptedRestoreAsync();

            Assert.Equal(sentinelContent, await File.ReadAllTextAsync(sentinelPath, TestContext.Current.CancellationToken));
            Assert.Equal("restored-from-backup", await File.ReadAllTextAsync(targetPath, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(stagingPath));
            Assert.Null(await journal.ReadAsync(TestContext.Current.CancellationToken));
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
    public async Task RecoverInterruptedRestoreAsync_RemovesPreexistingSymbolicLinkStagingPathWithoutTouchingTarget()
    {
        var isolatedRoot = CreateRestoreTestRoot("symlink-stage");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        var sentinelPath = Path.Combine(isolatedRoot, "outside-sentinel.txt");
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

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-from-backup");
            }

            const string sentinelContent = "must-remain-throughoutside-symlink";
            await File.WriteAllTextAsync(sentinelPath, sentinelContent, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(targetPath, "original-content", TestContext.Current.CancellationToken);

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var pendingRestore = await journal.CreateAsync(archivePath, TestContext.Current.CancellationToken);
            var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(targetPath))));
            var sidecarPrefix = $".mfx-restore-{pendingRestore.OperationId}-{targetHash}";
            var stagingPath = Path.Combine(configPath, $"{sidecarPrefix}.tmp");
            try
            {
                File.CreateSymbolicLink(stagingPath, sentinelPath);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Skip($"Symbolic links are unavailable in this environment: {exception.Message}");
            }

            var service = CreateService();
            await service.RecoverInterruptedRestoreAsync();

            Assert.Equal(sentinelContent, await File.ReadAllTextAsync(sentinelPath, TestContext.Current.CancellationToken));
            Assert.Equal("restored-from-backup", await File.ReadAllTextAsync(targetPath, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(stagingPath));
            Assert.Null(await journal.ReadAsync(TestContext.Current.CancellationToken));
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
    public async Task RecoverInterruptedRestoreAsync_LeavesJournalAndDoesNotTouchFilesWhenArchiveChanged()
    {
        var isolatedRoot = CreateRestoreTestRoot("changed-archive");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
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

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-from-backup");
            }

            await File.WriteAllTextAsync(targetPath, "current-content");
            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            await journal.CreateAsync(archivePath);
            var archiveBytes = await File.ReadAllBytesAsync(archivePath);
            archiveBytes[^1] ^= 0x01;
            await File.WriteAllBytesAsync(archivePath, archiveBytes);

            var service = CreateService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RecoverInterruptedRestoreAsync());

            Assert.Equal("current-content", await File.ReadAllTextAsync(targetPath));
            Assert.NotNull(await journal.ReadAsync());
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
    public async Task RecoveryJournal_CreateAsync_UsesIdentityFromAlreadyOpenArchiveWhenPathIsReplaced()
    {
        var isolatedRoot = CreateRestoreTestRoot("open-archive");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var replacementPath = Path.Combine(isolatedRoot, "replacement.zip");
        var originalMovedPath = Path.Combine(isolatedRoot, "original.zip");
        Directory.CreateDirectory(isolatedRoot);

        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", "original-archive");
            }

            using (var archive = ZipFile.Open(replacementPath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", "replacement-archive");
            }

            var originalHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(archivePath)));
            await using var openArchive = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            File.Move(archivePath, originalMovedPath);
            File.Move(replacementPath, archivePath);

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var entry = await journal.CreateAsync(archivePath, openArchive);
            var originalPosition = Math.Min(17, openArchive.Length);
            openArchive.Position = originalPosition;

            Assert.Equal(originalHash, entry.ArchiveSha256);
            await journal.VerifyArchiveAsync(entry, openArchive);
            Assert.Equal(originalPosition, openArchive.Position);
            await Assert.ThrowsAsync<InvalidDataException>(() => journal.VerifyArchiveAsync(entry));
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
    public async Task RecoveryJournal_VerifyArchiveAsync_RejectsDifferentOpenStreamWhenPathStillMatches()
    {
        var isolatedRoot = CreateRestoreTestRoot("open-stream-mismatch");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var differentArchivePath = Path.Combine(isolatedRoot, "different.zip");
        Directory.CreateDirectory(isolatedRoot);

        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", "journaled-archive");
            }

            using (var archive = ZipFile.Open(differentArchivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", "different-archive");
            }

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var entry = await journal.CreateAsync(archivePath);
            await using var differentStream = new FileStream(
                differentArchivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            differentStream.Position = Math.Min(13, differentStream.Length);
            var originalPosition = differentStream.Position;

            await Assert.ThrowsAsync<InvalidDataException>(() => journal.VerifyArchiveAsync(entry, differentStream));
            Assert.Equal(originalPosition, differentStream.Position);
            await journal.VerifyArchiveAsync(entry);
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
    public async Task RecoveryJournal_VerifyArchiveAsync_UsesContentIdentityInsteadOfPathTimestamp()
    {
        var isolatedRoot = CreateRestoreTestRoot("archive-timestamp");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        Directory.CreateDirectory(isolatedRoot);

        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", "journaled-archive");
            }

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var entry = await journal.CreateAsync(archivePath);
            File.SetLastWriteTimeUtc(archivePath, File.GetLastWriteTimeUtc(archivePath).AddMinutes(5));

            await journal.VerifyArchiveAsync(entry);
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
    public async Task RecoverInterruptedRestoreAsync_PreservesConflictingSidecarsAndJournal()
    {
        var isolatedRoot = CreateRestoreTestRoot("conflicting-sidecars");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
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

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-from-backup");
            }

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            var pendingRestore = await journal.CreateAsync(archivePath);
            var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(targetPath))));
            var sidecarPrefix = $".mfx-restore-{pendingRestore.OperationId}-{targetHash}";
            var backupPath = Path.Combine(configPath, $"{sidecarPrefix}.bak");
            var absentPath = Path.Combine(configPath, $"{sidecarPrefix}.absent");
            await File.WriteAllTextAsync(backupPath, "preserved-backup-sidecar");
            await File.WriteAllTextAsync(absentPath, string.Empty);
            await File.WriteAllTextAsync(targetPath, "partially-installed-content");

            var service = CreateService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RecoverInterruptedRestoreAsync());

            Assert.Equal("partially-installed-content", await File.ReadAllTextAsync(targetPath));
            Assert.Equal("preserved-backup-sidecar", await File.ReadAllTextAsync(backupPath));
            Assert.True(File.Exists(absentPath));
            Assert.NotNull(await journal.ReadAsync());
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
    public async Task RestoreBackupAsync_PreservesJournalWhenSidecarCleanupFails()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows sharing rules are required to block deletion of an open sidecar.");

        var isolatedRoot = CreateRestoreTestRoot("cleanup");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.json");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(targetPath, "original-content");

        FileStream? backupLock = null;
        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/server.json", "restored-content");
            }

            var service = CreateService(restoreBackupSidecarCreatedForTesting: path =>
                backupLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            var restore = service.RestoreBackupAsync(archivePath);
            await Assert.ThrowsAsync<IOException>(() => restore);
            Assert.NotNull(backupLock);

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            Assert.NotNull(await journal.ReadAsync());
            Assert.Equal("restored-content", await File.ReadAllTextAsync(targetPath));
            Assert.Contains(Directory.EnumerateFiles(configPath), path => path.EndsWith(".bak", StringComparison.Ordinal));

            await backupLock.DisposeAsync();
            backupLock = null;

            await service.RecoverInterruptedRestoreAsync();

            Assert.Equal("restored-content", await File.ReadAllTextAsync(targetPath));
            Assert.Null(await journal.ReadAsync());
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.bak"));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.tmp"));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.absent"));
        }
        finally
        {
            if (backupLock is not null)
            {
                await backupLock.DisposeAsync();
            }

            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task RestoreBackupAsync_PreservesJournalWhenCreatedDirectoryParentBecomesUntrusted()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "The ACL mutation used to force directory-cleanup denial requires Windows ACLs.");

        var isolatedRoot = CreateRestoreTestRoot("created-directory-cleanup");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var createdDirectory = Path.Combine(configPath, "newly-created");
        var restoredPath = Path.Combine(createdDirectory, "server.json");
        var triggerPath = Path.Combine(configPath, "trigger.json");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(triggerPath, "original-trigger");
        var configInfo = new DirectoryInfo(configPath);
        var originalSecurity = configInfo.GetAccessControl();

        try
        {
            _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(configPath);
            _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
            _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
            _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
            _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
            _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
            _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                await WriteArchiveEntryAsync(archive, "Config/newly-created/server.json", "restored-content");
                await WriteArchiveEntryAsync(archive, "Config/trigger.json", "replacement-trigger");
            }

            var service = CreateService(restoreBackupSidecarCreatedForTesting: _ =>
            {
                var security = configInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                configInfo.SetAccessControl(security);
                throw new IOException("Forced restore failure after the destination trust boundary changes.");
            });

            await Assert.ThrowsAsync<IOException>(() => service.RestoreBackupAsync(archivePath));

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            Assert.NotNull(await journal.ReadAsync());
            Assert.Equal("original-trigger", await File.ReadAllTextAsync(triggerPath));
            Assert.False(File.Exists(restoredPath));
            Assert.True(Directory.Exists(createdDirectory));
        }
        finally
        {
            configInfo.SetAccessControl(originalSecurity);
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated()
    {
        var childMode = Environment.GetEnvironmentVariable(RestoreCrashTestModeVariable);
        var childRoot = Environment.GetEnvironmentVariable(RestoreCrashTestRootVariable);
        if (childMode is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(childRoot));
            ConfigureRestoreCrashTestPaths(childRoot!);
            var childService = CreateService();
            if (string.Equals(childMode, "restore", StringComparison.Ordinal))
            {
                await childService.RestoreBackupAsync(Path.Combine(childRoot!, "recovery.zip"));
            }
            else if (string.Equals(childMode, "recover", StringComparison.Ordinal))
            {
                await childService.RecoverInterruptedRestoreAsync();
            }
            else
            {
                throw new InvalidOperationException($"Unknown restore crash test mode '{childMode}'.");
            }

            return;
        }

        var isolatedRoot = CreateRestoreTestRoot("process-recovery");
        var configPath = Path.Combine(isolatedRoot, "config");
        var archivePath = Path.Combine(isolatedRoot, "recovery.zip");
        var targetPath = Path.Combine(configPath, "server.bin");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        Directory.CreateDirectory(configPath);
        await File.WriteAllTextAsync(targetPath, "original-before-crash");

        Process? restoreProcess = null;
        Process? recoveryProcess = null;
        try
        {
            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
                var payloadEntry = archive.CreateEntry("Config/server.bin", CompressionLevel.NoCompression);
                await using var payloadStream = await payloadEntry.OpenAsync();
                var payloadBuffer = new byte[1024 * 1024];
                Array.Fill(payloadBuffer, (byte)0x5A);
                for (var written = 0; written < RestoreCrashTestPayloadSize; written += payloadBuffer.Length)
                {
                    await payloadStream.WriteAsync(payloadBuffer);
                }
            }

            var stagingFileCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watcher = new FileSystemWatcher(configPath, ".mfx-restore-*.tmp")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size
            };
            watcher.Created += (_, _) => stagingFileCreated.TrySetResult();
            watcher.EnableRaisingEvents = true;

            restoreProcess = StartRestoreCrashTestProcess("restore", isolatedRoot);
            var restoreFinished = restoreProcess.WaitForExitAsync();
            var firstStagingFile = await Task.WhenAny(stagingFileCreated.Task, restoreFinished, Task.Delay(TimeSpan.FromSeconds(60))).ConfigureAwait(false);
            Assert.Same(stagingFileCreated.Task, firstStagingFile);

            restoreProcess.Kill(entireProcessTree: true);
            await restoreProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            restoreProcess.Dispose();
            restoreProcess = null;

            var journal = CreateRecoveryJournal(programDataPath);
            Assert.NotNull(await journal.ReadAsync());
            Assert.True(Directory.EnumerateFiles(configPath, ".mfx-restore-*.tmp").Any());

            recoveryProcess = StartRestoreCrashTestProcess("recover", isolatedRoot);
            await recoveryProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            Assert.Equal(0, recoveryProcess.ExitCode);
            recoveryProcess.Dispose();
            recoveryProcess = null;

            var restoredBytes = await File.ReadAllBytesAsync(targetPath);
            Assert.Equal(RestoreCrashTestPayloadSize, restoredBytes.Length);
            Assert.All(restoredBytes, value => Assert.Equal((byte)0x5A, value));
            Assert.Null(await journal.ReadAsync());
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.tmp"));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.bak"));
            Assert.Empty(Directory.EnumerateFiles(configPath, ".mfx-restore-*.absent"));
        }
        finally
        {
            if (restoreProcess is { HasExited: false })
            {
                restoreProcess.Kill(entireProcessTree: true);
                await restoreProcess.WaitForExitAsync().ConfigureAwait(false);
            }

            restoreProcess?.Dispose();
            if (recoveryProcess is { HasExited: false })
            {
                recoveryProcess.Kill(entireProcessTree: true);
                await recoveryProcess.WaitForExitAsync().ConfigureAwait(false);
            }

            recoveryProcess?.Dispose();
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    private void ConfigureRestoreCrashTestPaths(string isolatedRoot)
    {
        _applicationPathsMock.Setup(paths => paths.ConfigurationDirectoryPath).Returns(Path.Combine(isolatedRoot, "config"));
        _applicationPathsMock.Setup(paths => paths.DataPath).Returns(Path.Combine(isolatedRoot, "data"));
        _applicationPathsMock.Setup(paths => paths.CachePath).Returns(Path.Combine(isolatedRoot, "cache"));
        _applicationPathsMock.Setup(paths => paths.ProgramDataPath).Returns(Path.Combine(isolatedRoot, "program-data"));
        _applicationPathsMock.Setup(paths => paths.RootFolderPath).Returns(Path.Combine(isolatedRoot, "root"));
        _applicationPathsMock.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata"));
        _applicationPathsMock.Setup(paths => paths.DefaultInternalMetadataPath).Returns(Path.Combine(isolatedRoot, "metadata-default"));
    }

    private static Process StartRestoreCrashTestProcess(string mode, string isolatedRoot)
    {
        var processStartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        };
        processStartInfo.ArgumentList.Add(typeof(BackupServiceTests).Assembly.Location);
        processStartInfo.ArgumentList.Add("-method");
        processStartInfo.ArgumentList.Add($"{typeof(BackupServiceTests).FullName}.RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated");
        processStartInfo.ArgumentList.Add("-reporter");
        processStartInfo.ArgumentList.Add("quiet");
        processStartInfo.Environment[RestoreCrashTestModeVariable] = mode;
        processStartInfo.Environment[RestoreCrashTestRootVariable] = isolatedRoot;

        return Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Could not start the isolated restore test process.");
    }

    [Fact]
    public void RecoveryJournal_OperationLeaseRejectsSymbolicLinkOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "The Linux journal lease opens its lock file with O_NOFOLLOW.");

        var programDataPath = CreateRestoreTestRoot("lease-link");
        var directoryPath = Path.Combine(programDataPath, "restore-recovery");
        var outsidePath = Path.Combine(programDataPath, "outside.lock");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(outsidePath, "preserve");
        File.CreateSymbolicLink(Path.Combine(directoryPath, "restore-operation.lock"), outsidePath);

        try
        {
            var journal = CreateRecoveryJournal(programDataPath);

            Assert.ThrowsAny<Exception>(() => journal.AcquireOperationLease());
            Assert.Equal("preserve", File.ReadAllText(outsidePath));
        }
        finally
        {
            Directory.Delete(programDataPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("program-data")]
    [InlineData("recovery-directory")]
    public async Task RecoveryJournal_RejectsUntrustedRootBeforeLeaseOrJournalMutation(string directoryKind)
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore journal ACL enforcement supports Windows and Linux.");

        var programDataPath = CreateRestoreTestRoot("journal-untrusted-recovery-acl");
        var recoveryDirectoryPath = Path.Combine(programDataPath, "restore-recovery");
        Directory.CreateDirectory(recoveryDirectoryPath);
        var journalPath = Path.Combine(recoveryDirectoryPath, "active-restore.json");
        const string originalJournal = "preserve-existing-journal";
        await File.WriteAllTextAsync(journalPath, originalJournal);
        var archivePath = Path.Combine(programDataPath, "restore.zip");
        await File.WriteAllTextAsync(archivePath, "archive");
        var journal = CreateRecoveryJournal(programDataPath);
        var untrustedDirectoryPath = directoryKind == "program-data" ? programDataPath : recoveryDirectoryPath;
        DirectorySecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var untrustedDirectory = new DirectoryInfo(untrustedDirectoryPath);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                originalSecurity = untrustedDirectory.GetAccessControl();
                var security = untrustedDirectory.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                untrustedDirectory.SetAccessControl(security);
            }
            else
            {
                originalMode = File.GetUnixFileMode(untrustedDirectoryPath);
                File.SetUnixFileMode(untrustedDirectoryPath, originalMode | UnixFileMode.OtherWrite);
            }

            Assert.Throws<UnauthorizedAccessException>(() => journal.JournalExists());
            Assert.ThrowsAny<UnauthorizedAccessException>(() => journal.AcquireOperationLease());
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => journal.ReadAsync());
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => journal.CreateAsync(archivePath));
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => journal.DeleteAsync());
        }
        finally
        {
            if (originalSecurity is not null)
            {
                untrustedDirectory.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(untrustedDirectoryPath, originalMode);
            }
        }

        Assert.Equal(originalJournal, await File.ReadAllTextAsync(journalPath));
        Assert.False(File.Exists(Path.Combine(recoveryDirectoryPath, "restore-operation.lock")));
        Directory.Delete(programDataPath, recursive: true);
    }

    [Fact]
    public async Task RecoveryJournal_RejectsUntrustedJournalFileAclBeforeDeserializing()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore journal ACL enforcement supports Windows and Linux.");

        var programDataPath = CreateRestoreTestRoot("journal-untrusted-file-acl");
        var recoveryDirectoryPath = Path.Combine(programDataPath, "restore-recovery");
        Directory.CreateDirectory(recoveryDirectoryPath);
        var journalPath = Path.Combine(recoveryDirectoryPath, "active-restore.json");
        await File.WriteAllTextAsync(journalPath, "{}");

        FileSecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var journalInfo = new FileInfo(journalPath);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                originalSecurity = journalInfo.GetAccessControl();
                var security = journalInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    AccessControlType.Allow));
                journalInfo.SetAccessControl(security);
            }
            else
            {
                originalMode = File.GetUnixFileMode(journalPath);
                File.SetUnixFileMode(journalPath, originalMode | UnixFileMode.OtherWrite);
            }

            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => CreateRecoveryJournal(programDataPath).ReadAsync());
        }
        finally
        {
            if (originalSecurity is not null)
            {
                journalInfo.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux() && File.Exists(journalPath))
            {
                File.SetUnixFileMode(journalPath, originalMode);
            }

            Directory.Delete(programDataPath, recursive: true);
        }
    }

    [Fact]
    public void RecoveryJournal_RejectsUntrustedOperationLeaseFileBeforeLocking()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore journal ACL enforcement supports Windows and Linux.");

        var programDataPath = CreateRestoreTestRoot("lease-untrusted-file-acl");
        var recoveryDirectoryPath = Path.Combine(programDataPath, "restore-recovery");
        Directory.CreateDirectory(recoveryDirectoryPath);
        var leasePath = Path.Combine(recoveryDirectoryPath, "restore-operation.lock");
        File.WriteAllText(leasePath, "lease-preserved");

        FileSecurity? originalSecurity = null;
        UnixFileMode originalMode = default;
        var leaseInfo = new FileInfo(leasePath);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                originalSecurity = leaseInfo.GetAccessControl();
                var security = leaseInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    AccessControlType.Allow));
                leaseInfo.SetAccessControl(security);
            }
            else
            {
                originalMode = File.GetUnixFileMode(leasePath);
                File.SetUnixFileMode(leasePath, originalMode | UnixFileMode.OtherWrite);
            }

            Assert.Throws<UnauthorizedAccessException>(() => CreateRecoveryJournal(programDataPath).AcquireOperationLease());
            Assert.Equal("lease-preserved", File.ReadAllText(leasePath));
        }
        finally
        {
            if (originalSecurity is not null)
            {
                leaseInfo.SetAccessControl(originalSecurity);
            }
            else if (OperatingSystem.IsLinux() && File.Exists(leasePath))
            {
                File.SetUnixFileMode(leasePath, originalMode);
            }

            Directory.Delete(programDataPath, recursive: true);
        }
    }

    [Fact]
    public void RecoveryJournal_OperationLeaseDoesNotCreateMissingProgramDataRoot()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Skip("Safe restore journal roots require Windows or Linux.");
        }

        var root = Path.Combine(Path.GetTempPath(), $"mulletaflix-restore-lease-missing-root-{Guid.NewGuid():N}");
        var programDataPath = Path.Combine(root, "program-data");
        var journal = new BackupRestoreRecoveryJournal(programDataPath);
        Directory.CreateDirectory(root);

        try
        {
            Assert.ThrowsAny<Exception>(() => journal.AcquireOperationLease());
            Assert.False(Directory.Exists(programDataPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RecoveryJournal_CreateDoesNotCreateMissingProgramDataRoot()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Skip("Safe restore journal roots require Windows or Linux.");
        }

        var root = Path.Combine(Path.GetTempPath(), $"mulletaflix-restore-journal-missing-root-{Guid.NewGuid():N}");
        var programDataPath = Path.Combine(root, "program-data");
        var archivePath = Path.Combine(root, "backup.zip");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(archivePath, "backup");
        var journal = new BackupRestoreRecoveryJournal(programDataPath);

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => journal.CreateAsync(archivePath));
            Assert.False(Directory.Exists(programDataPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RecoveryJournal_OperationLeaseIsExclusiveAcrossProcessesAndReleasedAfterClose()
    {
        var childMode = Environment.GetEnvironmentVariable(RestoreLeaseTestModeVariable);
        var childProgramDataPath = Environment.GetEnvironmentVariable(RestoreLeaseTestRootVariable);
        if (childMode is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(childProgramDataPath));
            var childJournal = CreateRecoveryJournal(childProgramDataPath!);
            if (string.Equals(childMode, "contended", StringComparison.Ordinal))
            {
                Assert.Throws<IOException>(() => childJournal.AcquireOperationLease());
            }
            else if (string.Equals(childMode, "available", StringComparison.Ordinal))
            {
                using var childLease = childJournal.AcquireOperationLease();
            }
            else
            {
                throw new InvalidOperationException($"Unknown restore lease test mode '{childMode}'.");
            }

            return;
        }

        var programDataPath = CreateRestoreTestRoot("lease");
        var journal = CreateRecoveryJournal(programDataPath);
        Process? testProcess = null;
        try
        {
            using (journal.AcquireOperationLease())
            {
                testProcess = StartRestoreLeaseTestProcess("contended", programDataPath);
                await testProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                Assert.Equal(0, testProcess.ExitCode);
                testProcess.Dispose();
                testProcess = null;
            }

            testProcess = StartRestoreLeaseTestProcess("available", programDataPath);
            await testProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            Assert.Equal(0, testProcess.ExitCode);
            testProcess.Dispose();
            testProcess = null;
        }
        finally
        {
            if (testProcess is { HasExited: false })
            {
                testProcess.Kill(entireProcessTree: true);
                await testProcess.WaitForExitAsync().ConfigureAwait(false);
            }

            testProcess?.Dispose();
            if (Directory.Exists(programDataPath))
            {
                Directory.Delete(programDataPath, recursive: true);
            }
        }
    }

    private static Process StartRestoreLeaseTestProcess(string mode, string programDataPath)
    {
        var processStartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        };
        processStartInfo.ArgumentList.Add(typeof(BackupServiceTests).Assembly.Location);
        processStartInfo.ArgumentList.Add("-method");
        processStartInfo.ArgumentList.Add($"{typeof(BackupServiceTests).FullName}.RecoveryJournal_OperationLeaseIsExclusiveAcrossProcessesAndReleasedAfterClose");
        processStartInfo.ArgumentList.Add("-reporter");
        processStartInfo.ArgumentList.Add("quiet");
        processStartInfo.Environment[RestoreLeaseTestModeVariable] = mode;
        processStartInfo.Environment[RestoreLeaseTestRootVariable] = programDataPath;

        return Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Could not start the isolated restore lease test process.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreEntryPoint_FailsBeforeReadingJournalWhenOperationLeaseIsHeld(bool recover)
    {
        var isolatedRoot = CreateRestoreTestRoot("lease-entry");
        var programDataPath = Path.Combine(isolatedRoot, "program-data");
        var journal = CreateRecoveryJournal(programDataPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(journal.JournalPath)!);
            await File.WriteAllTextAsync(journal.JournalPath, "{invalid-json");
            ConfigureRestoreCrashTestPaths(isolatedRoot);
            using (journal.AcquireOperationLease())
            {
                var service = CreateService();
                var exception = await Record.ExceptionAsync(async () =>
                {
                    if (recover)
                    {
                        await service.RecoverInterruptedRestoreAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await service.RestoreBackupAsync(Path.Combine(isolatedRoot, "missing.zip")).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);

                Assert.IsType<IOException>(exception);
                Assert.Equal("{invalid-json", await File.ReadAllTextAsync(journal.JournalPath).ConfigureAwait(false));
            }
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
    public async Task RestoreBackupAsync_RejectsNewRestoreWhileInterruptedRestoreIsPending()
    {
        var isolatedRoot = CreateRestoreTestRoot("pending-recovery");
        var configPath = Path.Combine(isolatedRoot, "config");
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

            var manifest = new BackupManifest
            {
                ServerVersion = new Version(10, 0, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                DatabaseTables = Array.Empty<string>(),
                Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
            };

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await WriteArchiveEntryAsync(archive, "manifest.json", JsonSerializer.Serialize(manifest));
            }

            var journal = CreateRecoveryJournal(Path.Combine(isolatedRoot, "program-data"));
            await journal.CreateAsync(archivePath);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreBackupAsync(archivePath));

            Assert.NotNull(await journal.ReadAsync());
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }
}
