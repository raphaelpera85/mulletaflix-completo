// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using IntroSkipper.Data;
using IntroSkipper.Db;
using IntroSkipper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Entities.Books;
using MulletaFlix.Database.Implementations.Entities.Channels;
using MulletaFlix.Database.Implementations.Entities.Movies;
using MulletaFlix.Database.Implementations.Entities.Series;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Server.Implementations.Billing;
using MulletaFlix.Server.Implementations.Extensions;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Testing;
using MySqlConnector;
using Xunit;

namespace IntroSkipper.Integration.Tests;

/// <summary>Exercises full backup restore against an explicitly configured disposable MariaDB.</summary>
[Collection(ServerPersistenceCollection.Name)]
public sealed class BackupRestoreMariaDbTests
{
    private const string ConnectionStringVariable = MariaDbTestEnvironment.ConnectionStringVariable;
    private const string CrashTestModeVariable = "MFX_RESTORE_MARIADB_CRASH_TEST_MODE";
    private const string CrashTestRootVariable = "MFX_RESTORE_MARIADB_CRASH_TEST_ROOT";
    private const string CrashTestSchemaVariable = "MFX_RESTORE_MARIADB_CRASH_TEST_SCHEMA";
    private const string CrashTestPluginSchemaVariable = "MFX_RESTORE_MARIADB_CRASH_TEST_PLUGIN_SCHEMA";
    private const string CrashTestIndependentSchemaVariable = "MFX_RESTORE_MARIADB_CRASH_TEST_INDEPENDENT_SCHEMA";
    private const string CrashTestSignalVariable = "MFX_RESTORE_MARIADB_CRASH_TEST_SIGNAL";

    [Fact]
    public async Task RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit()
    {
        var mode = Environment.GetEnvironmentVariable(CrashTestModeVariable);
        if (mode is not null)
        {
            await RunCrashTestChildAsync(mode);
            return;
        }

        var configuredConnection = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            Assert.Skip($"Set {ConnectionStringVariable} to an isolated, disposable MariaDB instance to run this integration test.");
        }

        var adminConnection = CreateDisposableConnection(configuredConnection);
        var schema = "mulletaflix_restore_crash_test_" + Guid.NewGuid().ToString("N");
        var applicationConnection = CreateApplicationConnection(adminConnection, [schema], out var applicationUser);
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mulletaflix-restore-crash-mariadb-" + Guid.NewGuid().ToString("N"));
        var configurationPath = Path.Combine(root, "config");
        var dataPath = Path.Combine(root, "data");
        var cachePath = Path.Combine(root, "cache");
        var programDataPath = Path.Combine(root, "program-data");
        var appRootPath = Path.Combine(root, "root");
        var metadataPath = Path.Combine(root, "metadata");
        var backupPath = Path.Combine(root, "backups");
        var archivePath = Path.Combine(root, "restore.zip");
        var configFilePath = Path.Combine(configurationPath, "server.json");
        var signalPath = Path.Combine(root, "after-database-commit.signal");
        Process? restoreChild = null;
        Process? recoveryChild = null;
        try
        {
            Assert.False(Directory.Exists(root));
            Directory.CreateDirectory(configurationPath);
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(cachePath);
            Directory.CreateDirectory(programDataPath);
            Directory.CreateDirectory(appRootPath);
            Directory.CreateDirectory(metadataPath);

            AssertApplicationCannotCreateSchema(applicationConnection);
            await using var services = CreateMariaDbServices(applicationConnection, schema);
            var contextFactory = services.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>();
            await using (var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
                context.ActivityLogs.Add(new ActivityLog("backup-state", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, TestContext.Current.CancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, TestContext.Current.CancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, TestContext.Current.CancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, TestContext.Current.CancellationToken);
            }

            var paths = CreatePaths(configurationPath, dataPath, cachePath, programDataPath, appRootPath, metadataPath, backupPath);
            using var backupService = CreateMariaDbBackupService(services, paths);
            await File.WriteAllTextAsync(configFilePath, "backup-state", TestContext.Current.CancellationToken);
            var createdBackup = await backupService.CreateBackupAsync(new BackupOptionsDto
            {
                Database = true,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });
            File.Copy(createdBackup.Path, archivePath);

            await using (var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await context.ActivityLogs.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                context.ActivityLogs.Add(new ActivityLog("live-state", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await File.WriteAllTextAsync(configFilePath, "live-state", TestContext.Current.CancellationToken);
            restoreChild = StartMariaDbCrashTestProcess("restore", root, schema, signalPath, applicationConnection.ConnectionString);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            while (!File.Exists(signalPath) && !restoreChild.HasExited && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(signalPath), "Restore child did not reach the post-database-commit crash point.");
            Assert.True(restoreChild.HasExited is false, "Restore child exited before the test could terminate it.");
            await using (var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                Assert.Contains(await context.ActivityLogs.ToListAsync(TestContext.Current.CancellationToken), entry => entry.Name == "backup-state");
            }

            restoreChild.Kill(entireProcessTree: true);
            await restoreChild.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            restoreChild.Dispose();
            restoreChild = null;

            recoveryChild = StartMariaDbCrashTestProcess("recover", root, schema, signalPath, applicationConnection.ConnectionString);
            var recoveryOutputTask = recoveryChild.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var recoveryErrorTask = recoveryChild.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await recoveryChild.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
            var recoveryOutput = await recoveryOutputTask;
            var recoveryError = await recoveryErrorTask;
            Assert.True(
                recoveryChild.ExitCode == 0,
                $"Recovery child exited with code {recoveryChild.ExitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{recoveryOutput}{Environment.NewLine}stderr:{Environment.NewLine}{recoveryError}");
            recoveryChild.Dispose();
            recoveryChild = null;

            Assert.Equal("backup-state", await File.ReadAllTextAsync(configFilePath, TestContext.Current.CancellationToken));
            await using (var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                Assert.Contains(await context.ActivityLogs.ToListAsync(TestContext.Current.CancellationToken), entry => entry.Name == "backup-state");
                Assert.DoesNotContain(await context.ActivityLogs.ToListAsync(TestContext.Current.CancellationToken), entry => entry.Name == "live-state");
            }

            Assert.Null(await new BackupRestoreRecoveryJournal(programDataPath).ReadAsync(TestContext.Current.CancellationToken));
            Assert.Empty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.tmp"));
            Assert.Empty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.bak"));
        }
        finally
        {
            foreach (var child in new[] { restoreChild, recoveryChild })
            {
                if (child is { HasExited: false })
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync(CancellationToken.None);
                }

                child?.Dispose();
            }

            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            finally
            {
                await using var connection = new MySqlConnection(adminConnection.ConnectionString);
                await connection.OpenAsync(CancellationToken.None);
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{schema}`;";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
                command.CommandText = $"DROP USER IF EXISTS '{applicationUser}'@'127.0.0.1';";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
    }

    private static async Task RunCrashTestChildAsync(string mode)
    {
        var root = Environment.GetEnvironmentVariable(CrashTestRootVariable)
            ?? throw new InvalidOperationException("Crash test root was not provided.");
        var schema = Environment.GetEnvironmentVariable(CrashTestSchemaVariable)
            ?? throw new InvalidOperationException("Crash test schema was not provided.");
        var configuredConnection = Environment.GetEnvironmentVariable(ConnectionStringVariable)
            ?? throw new InvalidOperationException("Disposable MariaDB connection was not provided.");
        var adminConnection = CreateDisposableConnection(configuredConnection);
        await using var services = CreateMariaDbServices(adminConnection, schema);
        var paths = CreatePaths(
            Path.Combine(root, "config"),
            Path.Combine(root, "data"),
            Path.Combine(root, "cache"),
            Path.Combine(root, "program-data"),
            Path.Combine(root, "root"),
            Path.Combine(root, "metadata"),
            Path.Combine(root, "backups"));
        Func<Func<Task>, Task>? afterCommit = null;
        IBackupDatabaseParticipant[]? backupDatabaseParticipants = null;
        if (string.Equals(mode, "restore", StringComparison.Ordinal))
        {
            var signalPath = Environment.GetEnvironmentVariable(CrashTestSignalVariable)
                ?? throw new InvalidOperationException("Crash test signal path was not provided.");
            afterCommit = async commitRelationalTransaction =>
            {
                await commitRelationalTransaction().ConfigureAwait(false);
                await File.WriteAllTextAsync(signalPath, "committed").ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            };
        }
        else if (string.Equals(mode, "restore-after-participant", StringComparison.Ordinal)
            || (string.Equals(mode, "recover", StringComparison.Ordinal)
                && Environment.GetEnvironmentVariable(CrashTestPluginSchemaVariable) is not null))
        {
            var pluginSchema = Environment.GetEnvironmentVariable(CrashTestPluginSchemaVariable)
                ?? throw new InvalidOperationException("IntroSkipper test schema was not provided.");
            var pluginConnection = new MySqlConnectionStringBuilder(adminConnection.ConnectionString)
            {
                Database = pluginSchema,
            };
            var pluginOptions = new DbContextOptionsBuilder<IntroSkipperDbContext>()
                .UseMySql(pluginConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;
            var cacheOptions = new DbContextOptionsBuilder<DetectionCacheDbContext>()
                .UseMySql(pluginConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;
            var participant = new IntroSkipperBackupParticipant(
                new TestContextFactory<IntroSkipperDbContext>(() => new IntroSkipperDbContext(pluginOptions)),
                new TestContextFactory<DetectionCacheDbContext>(() => new DetectionCacheDbContext(cacheOptions)));
            var independentSchema = Environment.GetEnvironmentVariable(CrashTestIndependentSchemaVariable)
                ?? throw new InvalidOperationException("Independent participant test schema was not provided.");
            var independentParticipant = new IndependentMariaDbBackupParticipant(adminConnection, independentSchema);
            backupDatabaseParticipants = [participant, independentParticipant];
            if (string.Equals(mode, "restore-after-participant", StringComparison.Ordinal))
            {
                var signalPath = Environment.GetEnvironmentVariable(CrashTestSignalVariable)
                    ?? throw new InvalidOperationException("Participant commit signal path was not provided.");
                backupDatabaseParticipants = [new SignalAfterRestoreParticipant(participant, signalPath), independentParticipant];
            }
        }

        using var backupService = CreateMariaDbBackupService(services, paths, afterCommit, backupDatabaseParticipants);
        if (string.Equals(mode, "restore", StringComparison.Ordinal)
            || string.Equals(mode, "restore-after-participant", StringComparison.Ordinal))
        {
            await backupService.RestoreBackupAsync(Path.Combine(root, "restore.zip")).ConfigureAwait(false);
        }
        else if (string.Equals(mode, "recover", StringComparison.Ordinal))
        {
            await backupService.RecoverInterruptedRestoreAsync().ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Unknown MariaDB restore crash test mode '{mode}'.");
        }
    }

    private static Process StartMariaDbCrashTestProcess(
        string mode,
        string root,
        string schema,
        string signalPath,
        string? applicationConnectionString = null,
        string? pluginSchema = null,
        string? independentSchema = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(typeof(BackupRestoreMariaDbTests).Assembly.Location);
        startInfo.ArgumentList.Add("-method");
        startInfo.ArgumentList.Add($"{typeof(BackupRestoreMariaDbTests).FullName}.RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit");
        startInfo.ArgumentList.Add("-reporter");
        startInfo.ArgumentList.Add("quiet");
        startInfo.Environment[CrashTestModeVariable] = mode;
        startInfo.Environment[CrashTestRootVariable] = root;
        startInfo.Environment[CrashTestSchemaVariable] = schema;
        startInfo.Environment[CrashTestSignalVariable] = signalPath;
        if (applicationConnectionString is not null)
        {
            startInfo.Environment[ConnectionStringVariable] = applicationConnectionString;
        }
        if (pluginSchema is not null)
        {
            startInfo.Environment[CrashTestPluginSchemaVariable] = pluginSchema;
        }
        else
        {
            startInfo.Environment.Remove(CrashTestPluginSchemaVariable);
        }
        if (independentSchema is not null)
        {
            startInfo.Environment[CrashTestIndependentSchemaVariable] = independentSchema;
        }
        else
        {
            startInfo.Environment.Remove(CrashTestIndependentSchemaVariable);
        }
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the isolated MariaDB restore crash-test process.");
    }

    private static ServiceProvider CreateMariaDbServices(MySqlConnectionStringBuilder applicationConnection, string schema)
    {
        var databaseConfiguration = new DatabaseConfigurationOptions
        {
            DatabaseType = "MulletaFlix-MySQL",
            LockingBehavior = DatabaseLockingBehaviorTypes.NoLock,
            CustomProviderOptions = new CustomDatabaseOptions
            {
                PluginName = string.Empty,
                PluginAssembly = string.Empty,
                ConnectionString = string.Empty,
                Options =
                [
                    new CustomDatabaseOption { Key = "server", Value = applicationConnection.Server },
                    new CustomDatabaseOption { Key = "port", Value = applicationConnection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new CustomDatabaseOption { Key = "user", Value = applicationConnection.UserID },
                    new CustomDatabaseOption { Key = "password", Value = applicationConnection.Password },
                    new CustomDatabaseOption { Key = "database", Value = schema },
                ]
            }
        };
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("database")).Returns(databaseConfiguration);
        configurationManager.Setup(manager => manager.Configuration).Returns(new ServerConfiguration());
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        serviceCollection.AddSingleton(configurationManager.Object);
        serviceCollection.AddMulletaFlixDbContext(configurationManager.Object, new ConfigurationBuilder().Build());
        return serviceCollection.BuildServiceProvider();
    }

    private static BackupService CreateMariaDbBackupService(
        ServiceProvider services,
        IServerApplicationPaths paths,
        Func<Func<Task>, Task>? relationalCommitForTesting = null,
        IEnumerable<IBackupDatabaseParticipant>? backupDatabaseParticipants = null)
    {
        var appHost = new Mock<IServerApplicationHost>();
        appHost.SetupGet(host => host.ApplicationVersion).Returns(new Version(12, 1, 0));
        return new BackupService(
            services.GetRequiredService<ILogger<BackupService>>(),
            services.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>(),
            appHost.Object,
            paths,
            services.GetRequiredService<IMulletaFlixDatabaseProvider>(),
            Mock.Of<IHostApplicationLifetime>(),
            services.GetRequiredService<IDbContextFactory<MoviesDbContext>>(),
            services.GetRequiredService<IDbContextFactory<SeriesDbContext>>(),
            services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>(),
            services.GetRequiredService<IDbContextFactory<BooksDbContext>>(),
            relationalCommitForTesting,
            null,
            backupDatabaseParticipants);
    }

    [Fact]
    public async Task RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuredConnection = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            Assert.Skip($"Set {ConnectionStringVariable} to an isolated, disposable MariaDB instance to run this integration test.");
        }

        var adminConnection = CreateDisposableConnection(configuredConnection);
        var schema = "mulletaflix_restore_test_" + Guid.NewGuid().ToString("N");
        var introSkipperSchema = "mulletaflix_restore_plugin_test_" + Guid.NewGuid().ToString("N");
        var independentSchema = "mfx_restore_independent_" + Guid.NewGuid().ToString("N");
        MySqlConnectionStringBuilder? applicationConnection = null;
        string? applicationUser = null;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mulletaflix-restore-mariadb-" + Guid.NewGuid().ToString("N"));
        var configurationPath = Path.Combine(root, "config");
        var dataPath = Path.Combine(root, "data");
        var cachePath = Path.Combine(root, "cache");
        var programDataPath = Path.Combine(root, "program-data");
        var appRootPath = Path.Combine(root, "root");
        var metadataPath = Path.Combine(root, "metadata");
        var backupPath = Path.Combine(root, "backups");
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(configurationPath);
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(cachePath);
        Directory.CreateDirectory(programDataPath);
        Directory.CreateDirectory(appRootPath);
        Directory.CreateDirectory(metadataPath);

        ServiceProvider? services = null;
        Process? restoreChild = null;
        Process? recoveryChild = null;
        try
        {
            applicationConnection = CreateApplicationConnection(
                adminConnection,
                [schema, introSkipperSchema, independentSchema],
                out applicationUser);
            AssertApplicationCannotCreateSchema(applicationConnection);
            var databaseConfiguration = new DatabaseConfigurationOptions
            {
                DatabaseType = "MulletaFlix-MySQL",
                LockingBehavior = DatabaseLockingBehaviorTypes.NoLock,
                CustomProviderOptions = new CustomDatabaseOptions
                {
                    PluginName = string.Empty,
                    PluginAssembly = string.Empty,
                    ConnectionString = string.Empty,
                    Options =
                    [
                        new CustomDatabaseOption { Key = "server", Value = applicationConnection.Server },
                        new CustomDatabaseOption { Key = "port", Value = applicationConnection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                        new CustomDatabaseOption { Key = "user", Value = applicationConnection.UserID },
                        new CustomDatabaseOption { Key = "password", Value = applicationConnection.Password },
                        new CustomDatabaseOption { Key = "database", Value = schema },
                    ]
                }
            };
            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager.Setup(manager => manager.GetConfiguration("database")).Returns(databaseConfiguration);
            configurationManager.Setup(manager => manager.Configuration).Returns(new ServerConfiguration());

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            serviceCollection.AddSingleton(configurationManager.Object);
            serviceCollection.AddMulletaFlixDbContext(configurationManager.Object, new ConfigurationBuilder().Build());
            services = serviceCollection.BuildServiceProvider();

            var contextFactory = services.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>();
            var moviesFactory = services.GetRequiredService<IDbContextFactory<MoviesDbContext>>();
            var seriesFactory = services.GetRequiredService<IDbContextFactory<SeriesDbContext>>();
            var channelsFactory = services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>();
            var booksFactory = services.GetRequiredService<IDbContextFactory<BooksDbContext>>();
            var introSkipperConnection = new MySqlConnectionStringBuilder(applicationConnection.ConnectionString)
            {
                Database = introSkipperSchema,
            };
            var introSkipperOptions = new DbContextOptionsBuilder<IntroSkipperDbContext>()
                .UseMySql(introSkipperConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;
            var detectionCacheOptions = new DbContextOptionsBuilder<DetectionCacheDbContext>()
                .UseMySql(introSkipperConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;
            var introSkipperFactory = new TestContextFactory<IntroSkipperDbContext>(() => new IntroSkipperDbContext(introSkipperOptions));
            var detectionCacheFactory = new TestContextFactory<DetectionCacheDbContext>(() => new DetectionCacheDbContext(detectionCacheOptions));
            var introSkipperParticipant = new IntroSkipperBackupParticipant(introSkipperFactory, detectionCacheFactory);
            var independentParticipant = new IndependentMariaDbBackupParticipant(applicationConnection, independentSchema);
            await independentParticipant.EnsureFixtureAsync("backup-state", cancellationToken);
            var introSkipperItemId = Guid.NewGuid();
            var seasonId = Guid.NewGuid();
            var analyzedItemId = Guid.NewGuid();
            var disabledItemId = Guid.NewGuid();
            var projectionQueueItemId = Guid.NewGuid();
            var externalOperationItemId = Guid.NewGuid();
            var externalSegmentId = Guid.NewGuid();
            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                await IntroSkipperSchema.EnsureAsync(context, cancellationToken);
                context.Segments.Add(new DbSegment(introSkipperItemId, AnalysisMode.Introduction, 0, 100, SegmentSource.User));
                context.SeasonStates.Add(new DbSeasonState(seasonId, AnalysisMode.Credits, AnalyzerAction.None, [Guid.NewGuid()]));
                context.SeasonAnalysisOverrides.Add(new DbSeasonAnalysisOverride { SeasonId = seasonId, AnalysisPercent = 75 });
                context.AnalyzedItems.Add(new DbAnalyzedItem(analyzedItemId, AnalysisMode.Introduction, "backup-config"));
                context.ImportHistory.Add(new DbImportRecord { Id = 41, ImportedAt = DateTime.UtcNow, SourceFileFound = true, Notes = "backup-import" });
                context.DisabledItems.Add(new DbDisabledItem(disabledItemId));
                context.ProjectionQueue.Add(new DbProjectionQueueItem { ItemId = projectionQueueItemId, Version = 2, AttemptCount = 1 });
                context.ProjectionExternalOperations.Add(new DbProjectionExternalOperation
                {
                    Id = 42,
                    ItemId = externalOperationItemId,
                    ExternalSegmentId = externalSegmentId,
                    ExpectedType = MediaSegmentType.Intro,
                    StartTicks = 10,
                    EndTicks = 20
                });
                await context.SaveChangesAsync(cancellationToken);
            }
            await using (var context = await detectionCacheFactory.CreateDbContextAsync(cancellationToken))
            {
                await IntroSkipperSchema.EnsureAsync(context, cancellationToken);
                context.DetectionCache.Add(new DbDetectionCache(Guid.NewGuid(), AnalysisMode.Introduction, CacheEntryType.Silence, [1, 2, 3]));
                await context.SaveChangesAsync(cancellationToken);
            }
            var playbackUserId = Guid.NewGuid();
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.MigrateAsync(cancellationToken);
                context.ActivityLogs.Add(new ActivityLog("restore-fixture", "integration-test", Guid.NewGuid()));
                context.PlaybackReports.Add(new PlaybackReport(
                    playbackUserId,
                    Guid.NewGuid(),
                    "restore-fixture",
                    "Movie",
                    "integration-device",
                    "Integration Device",
                    "integration-test",
                    "restore-play-session",
                    "restore-session"));
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await moviesFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                context.Movies.Add(new Movie { BaseItemId = Guid.NewGuid(), Name = "backup-domain-movie" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await seriesFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                context.Series.Add(new Series { BaseItemId = Guid.NewGuid(), Name = "backup-domain-series" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await channelsFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                context.Channels.Add(new Channel { BaseItemId = Guid.NewGuid(), Name = "backup-domain-channel" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await booksFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                context.Books.Add(new Book { BaseItemId = Guid.NewGuid(), Name = "backup-domain-book" });
                await context.SaveChangesAsync(cancellationToken);
            }

            var paths = CreatePaths(
                configurationPath,
                dataPath,
                cachePath,
                programDataPath,
                appRootPath,
                metadataPath,
                backupPath);
            var appHost = new Mock<IServerApplicationHost>();
            appHost.SetupGet(host => host.ApplicationVersion).Returns(new Version(12, 1, 0));
            using var backupService = CreateMariaDbBackupService(services, paths, backupDatabaseParticipants: [introSkipperParticipant, independentParticipant]);

            var configFile = Path.Combine(configurationPath, "server.json");
            await File.WriteAllTextAsync(configFile, "before-backup", cancellationToken);

            var createdBackup = await backupService.CreateBackupAsync(new BackupOptionsDto
            {
                Database = true,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });

            using (var archive = ZipFile.OpenRead(createdBackup.Path))
            {
                using var manifestStream = archive.GetEntry("manifest.json")!.Open();
                using var manifest = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
                Assert.Contains("IntroSkipper", manifest.RootElement.GetProperty("DatabaseParticipants").EnumerateArray().Select(value => value.GetString()));
                Assert.Contains("TestIndependentDb", manifest.RootElement.GetProperty("DatabaseParticipants").EnumerateArray().Select(value => value.GetString()));
                Assert.NotNull(archive.GetEntry("Database/Participants/IntroSkipper/data.json"));
                Assert.NotNull(archive.GetEntry("Database/Participants/TestIndependentDb/data.json"));
                await independentParticipant.ValidateBackupAsync(archive, "Database/Participants/TestIndependentDb", cancellationToken);
            }

            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Segments.ExecuteDeleteAsync(cancellationToken);
                context.Segments.Add(new DbSegment(introSkipperItemId, AnalysisMode.Introduction, 500, 600, SegmentSource.User));
                await context.SeasonStates.ExecuteDeleteAsync(cancellationToken);
                await context.SeasonAnalysisOverrides.ExecuteDeleteAsync(cancellationToken);
                await context.AnalyzedItems.ExecuteDeleteAsync(cancellationToken);
                await context.ImportHistory.ExecuteDeleteAsync(cancellationToken);
                await context.DisabledItems.ExecuteDeleteAsync(cancellationToken);
                await context.ProjectionQueue.ExecuteDeleteAsync(cancellationToken);
                await context.ProjectionExternalOperations.ExecuteDeleteAsync(cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            const string preservedAfterInsertFailure = "preserve-after-insert-failure";
            var failingInsertTrigger = "fail_restore_activity_insert_" + Guid.NewGuid().ToString("N");
            await File.WriteAllTextAsync(configFile, "after-backup", cancellationToken);
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                context.ActivityLogs.Add(new ActivityLog(preservedAfterInsertFailure, "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(cancellationToken);

                var tableName = context.Model.FindEntityType(typeof(ActivityLog))!.GetTableName()!;
                var quotedTableName = tableName.Replace("`", "``", StringComparison.Ordinal);
                await context.Database.ExecuteSqlRawAsync(
                    $"CREATE TRIGGER `{failingInsertTrigger}` BEFORE INSERT ON `{quotedTableName}` FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Injected restore failure';",
                    cancellationToken);
            }

            await Assert.ThrowsAsync<DbUpdateException>(() => backupService.RestoreBackupAsync(createdBackup.Path));
            Assert.Equal("after-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal(500, (await context.Segments.SingleAsync(cancellationToken)).StartTicks);
            }
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var activityLogs = await context.ActivityLogs.ToListAsync(cancellationToken);
                Assert.Contains(activityLogs, entry => entry.Name == "restore-fixture");
                Assert.Contains(activityLogs, entry => entry.Name == preservedAfterInsertFailure);
                await context.Database.ExecuteSqlRawAsync($"DROP TRIGGER `{failingInsertTrigger}`;", cancellationToken);
            }

            var duplicateBackupPath = Path.Combine(root, "duplicate-record.zip");
            File.Copy(createdBackup.Path, duplicateBackupPath);
            await DuplicateFirstDatabaseRecordAsync(duplicateBackupPath, "ActivityLogs", cancellationToken);
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                context.ActivityLogs.Add(new ActivityLog("preserve-after-failed-restore", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(cancellationToken);
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => backupService.RestoreBackupAsync(duplicateBackupPath));

            Assert.Equal("after-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Contains(await context.ActivityLogs.ToListAsync(cancellationToken), entry => entry.Name == "preserve-after-failed-restore");
            }

            var invalidForeignKeyBackupPath = Path.Combine(root, "invalid-domain-foreign-key.zip");
            File.Copy(createdBackup.Path, invalidForeignKeyBackupPath);
            await AppendInvalidEpisodeRecordAsync(invalidForeignKeyBackupPath, cancellationToken);
            var foreignKeyException = await Assert.ThrowsAsync<InvalidDataException>(() => backupService.RestoreBackupAsync(invalidForeignKeyBackupPath));
            Assert.Contains("invalid reference", foreignKeyException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("after-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Contains(await context.ActivityLogs.ToListAsync(cancellationToken), entry => entry.Name == "preserve-after-failed-restore");
            }

            await using (var context = await moviesFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-movie", (await context.Movies.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await seriesFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-series", (await context.Series.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await channelsFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-channel", (await context.Channels.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await booksFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-book", (await context.Books.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.ActivityLogs.ExecuteDeleteAsync(cancellationToken);
                await context.PlaybackReports.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.ActivityLogs.ToListAsync(cancellationToken));
                Assert.Empty(await context.PlaybackReports.ToListAsync(cancellationToken));
            }

            await using (var context = await moviesFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Movies.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.Movies.ToListAsync(cancellationToken));
            }

            await using (var context = await seriesFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Series.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.Series.ToListAsync(cancellationToken));
            }

            await using (var context = await channelsFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Channels.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.Channels.ToListAsync(cancellationToken));
            }

            await using (var context = await booksFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Books.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.Books.ToListAsync(cancellationToken));
            }

            await backupService.RestoreBackupAsync(createdBackup.Path);

            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                var restoredSegment = await context.Segments.SingleAsync(cancellationToken);
                Assert.Equal(introSkipperItemId, restoredSegment.ItemId);
                Assert.Equal(0, restoredSegment.StartTicks);
                Assert.Equal(seasonId, (await context.SeasonStates.SingleAsync(cancellationToken)).SeasonId);
                Assert.Equal(75, (await context.SeasonAnalysisOverrides.SingleAsync(cancellationToken)).AnalysisPercent);
                Assert.Equal("backup-config", (await context.AnalyzedItems.SingleAsync(cancellationToken)).ConfigHash);
                Assert.Equal("backup-import", (await context.ImportHistory.SingleAsync(cancellationToken)).Notes);
                Assert.Equal(disabledItemId, (await context.DisabledItems.SingleAsync(cancellationToken)).ItemId);
                var restoredQueueItem = await context.ProjectionQueue.SingleAsync(cancellationToken);
                Assert.Equal(projectionQueueItemId, restoredQueueItem.ItemId);
                Assert.Equal(2, restoredQueueItem.Version);
                var restoredExternalOperation = await context.ProjectionExternalOperations.SingleAsync(cancellationToken);
                Assert.Equal(42, restoredExternalOperation.Id);
                Assert.Equal(externalOperationItemId, restoredExternalOperation.ItemId);
                Assert.Equal(externalSegmentId, restoredExternalOperation.ExternalSegmentId);
            }

            await using (var context = await detectionCacheFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Empty(await context.DetectionCache.ToListAsync(cancellationToken));
            }

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var restored = await context.ActivityLogs.SingleAsync(entry => entry.Name == "restore-fixture", cancellationToken);
                Assert.Equal("integration-test", restored.Type);
                var restoredPlayback = await context.PlaybackReports.SingleAsync(
                    report => report.PlaySessionId == "restore-play-session",
                    cancellationToken);
                Assert.Equal(playbackUserId, restoredPlayback.UserId);
            }

            await using (var context = await moviesFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-movie", (await context.Movies.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await seriesFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-series", (await context.Series.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await channelsFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-channel", (await context.Channels.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await booksFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("backup-domain-book", (await context.Books.SingleAsync(cancellationToken)).Name);
            }

            // Simulate a lost/failed acknowledgement after the database transaction committed.
            // The restore must retain its journal and file sidecars, then replay before serving.
            var interruptedHost = new Mock<IServerApplicationHost>();
            interruptedHost.SetupGet(host => host.ApplicationVersion).Returns(new Version(12, 1, 0));
            interruptedHost.SetupProperty(host => host.ShouldRestart);
            var interruptedLifetime = new Mock<IHostApplicationLifetime>();
            var shutdownRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            interruptedLifetime
                .Setup(lifetime => lifetime.StopApplication())
                .Callback(() => shutdownRequested.TrySetResult());
            using var interruptedRestoreService = new BackupService(
                services.GetRequiredService<ILogger<BackupService>>(),
                contextFactory,
                interruptedHost.Object,
                paths,
                services.GetRequiredService<IMulletaFlixDatabaseProvider>(),
                interruptedLifetime.Object,
                moviesFactory,
                seriesFactory,
                channelsFactory,
                booksFactory,
                async commitRelationalTransaction =>
                {
                    await commitRelationalTransaction().ConfigureAwait(false);
                    throw new IOException("Simulated lost commit acknowledgement.");
                },
                null,
                [introSkipperParticipant, independentParticipant]);

            await File.WriteAllTextAsync(configFile, "after-uncertain-commit", cancellationToken);
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                context.ActivityLogs.Add(new ActivityLog("after-uncertain-commit", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(cancellationToken);
            }

            await Assert.ThrowsAsync<IOException>(() => interruptedRestoreService.RestoreBackupAsync(createdBackup.Path));
            Assert.Equal("before-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            Assert.NotNull(await new BackupRestoreRecoveryJournal(programDataPath).ReadAsync(cancellationToken));
            Assert.NotEmpty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.bak"));
            Assert.True(interruptedHost.Object.ShouldRestart);
            interruptedHost.Verify(host => host.NotifyPendingRestart(), Times.Once);
            await shutdownRequested.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            interruptedLifetime.Verify(lifetime => lifetime.StopApplication(), Times.Once);

            // Recovery runs after restart; the injected acknowledgement failure must not run again.
            using var recoveryService = CreateMariaDbBackupService(services, paths, backupDatabaseParticipants: [introSkipperParticipant, independentParticipant]);
            await recoveryService.RecoverInterruptedRestoreAsync();
            Assert.Null(await new BackupRestoreRecoveryJournal(programDataPath).ReadAsync(cancellationToken));
            Assert.Empty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.bak"));
            Assert.Equal("before-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.DoesNotContain(await context.ActivityLogs.ToListAsync(cancellationToken), entry => entry.Name == "after-uncertain-commit");
            }
            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                var recoveredSegment = await context.Segments.SingleAsync(cancellationToken);
                Assert.Equal(introSkipperItemId, recoveredSegment.ItemId);
                Assert.Equal(0, recoveredSegment.StartTicks);
            }
            Assert.Equal("backup-state", await independentParticipant.ReadValueAsync(cancellationToken));

            // Exercise a real process termination after the plugin commits, before file commit.
            await File.WriteAllTextAsync(configFile, "after-participant-interruption", cancellationToken);
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                context.ActivityLogs.Add(new ActivityLog("after-participant-interruption", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(cancellationToken);
            }
            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Segments.ExecuteDeleteAsync(cancellationToken);
                context.Segments.Add(new DbSegment(introSkipperItemId, AnalysisMode.Introduction, 900, 1000, SegmentSource.User));
                await context.SaveChangesAsync(cancellationToken);
            }
            await independentParticipant.SetValueAsync("after-participant-interruption", cancellationToken);

            File.Copy(createdBackup.Path, Path.Combine(root, "restore.zip"));
            var participantSignalPath = Path.Combine(root, "after-participant-commit.signal");
            restoreChild = StartMariaDbCrashTestProcess("restore-after-participant", root, schema, participantSignalPath, applicationConnection.ConnectionString, introSkipperSchema, independentSchema);
            var participantDeadline = DateTimeOffset.UtcNow.AddSeconds(90);
            while (!File.Exists(participantSignalPath) && !restoreChild.HasExited && DateTimeOffset.UtcNow < participantDeadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            }

            var restoreFailure = restoreChild.HasExited
                ? $" Exit code {restoreChild.ExitCode}; stdout: {await restoreChild.StandardOutput.ReadToEndAsync(cancellationToken)}; stderr: {await restoreChild.StandardError.ReadToEndAsync(cancellationToken)}"
                : string.Empty;
            Assert.True(File.Exists(participantSignalPath), $"Restore child did not reach the post-participant-commit crash point.{restoreFailure}");
            Assert.False(restoreChild.HasExited, "Restore child exited before the test could terminate it after participant commit.");
            Assert.Equal("before-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            Assert.NotNull(await new BackupRestoreRecoveryJournal(programDataPath).ReadAsync(cancellationToken));
            Assert.NotEmpty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.bak"));
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.DoesNotContain(await context.ActivityLogs.ToListAsync(cancellationToken), entry => entry.Name == "after-participant-interruption");
            }
            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                var participantCommittedRows = await context.Segments.AsNoTracking().ToArrayAsync(cancellationToken);
                var participantCommitted = Assert.Single(participantCommittedRows);
                Assert.Equal(introSkipperItemId, participantCommitted.ItemId);
                Assert.Equal(0, participantCommitted.StartTicks);
            }
            Assert.Equal("after-participant-interruption", await independentParticipant.ReadValueAsync(cancellationToken));

            restoreChild.Kill(entireProcessTree: true);
            await restoreChild.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            restoreChild.Dispose();
            restoreChild = null;

            recoveryChild = StartMariaDbCrashTestProcess("recover", root, schema, participantSignalPath, applicationConnection.ConnectionString, introSkipperSchema, independentSchema);
            var participantRecoveryOutputTask = recoveryChild.StandardOutput.ReadToEndAsync(cancellationToken);
            var participantRecoveryErrorTask = recoveryChild.StandardError.ReadToEndAsync(cancellationToken);
            await recoveryChild.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);
            var participantRecoveryOutput = await participantRecoveryOutputTask;
            var participantRecoveryError = await participantRecoveryErrorTask;
            Assert.True(
                recoveryChild.ExitCode == 0,
                $"Participant recovery child exited with code {recoveryChild.ExitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{participantRecoveryOutput}{Environment.NewLine}stderr:{Environment.NewLine}{participantRecoveryError}");
            recoveryChild.Dispose();
            recoveryChild = null;

            Assert.Null(await new BackupRestoreRecoveryJournal(programDataPath).ReadAsync(cancellationToken));
            Assert.Empty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.bak"));
            Assert.Equal("before-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.DoesNotContain(await context.ActivityLogs.ToListAsync(cancellationToken), entry => entry.Name == "after-participant-interruption");
            }
            await using (var context = await introSkipperFactory.CreateDbContextAsync(cancellationToken))
            {
                var replayedRows = await context.Segments.AsNoTracking().ToArrayAsync(cancellationToken);
                var replayed = Assert.Single(replayedRows);
                Assert.Equal(introSkipperItemId, replayed.ItemId);
                Assert.Equal(0, replayed.StartTicks);
            }
            Assert.Equal("backup-state", await independentParticipant.ReadValueAsync(cancellationToken));
        }
        finally
        {
            foreach (var child in new[] { restoreChild, recoveryChild })
            {
                if (child is { HasExited: false })
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync(CancellationToken.None);
                }

                child?.Dispose();
            }

            if (services is not null)
            {
                await services.DisposeAsync();
            }

            await using (var connection = new MySqlConnection(adminConnection.ConnectionString))
            {
                await connection.OpenAsync(CancellationToken.None);
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{introSkipperSchema}`;";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
                command.CommandText = $"DROP DATABASE IF EXISTS `{independentSchema}`;";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
                command.CommandText = $"DROP DATABASE IF EXISTS `{schema}`;";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
                if (applicationUser is not null)
                {
                    command.CommandText = $"DROP USER IF EXISTS '{applicationUser}'@'127.0.0.1';";
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Server=db.example.test;Port=13306;User ID=root;Password=;")]
    [InlineData("Server=127.0.0.1;Port=3306;User ID=root;Password=;")]
    public void DisposableConnection_RejectsRemoteOrDefaultPort(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => CreateDisposableConnection(connectionString));
    }

    private static MySqlConnectionStringBuilder CreateDisposableConnection(string connectionString)
        => MariaDbTestEnvironment.Validate(connectionString);

    private static MySqlConnectionStringBuilder CreateApplicationConnection(
        MySqlConnectionStringBuilder adminConnection,
        IReadOnlyList<string> databaseNames,
        out string userName)
    {
        userName = "mfx_restore_" + Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N");
        var createdDatabases = new List<string>();
        var createdUser = false;
        using var connection = new MySqlConnection(adminConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        try
        {
            foreach (var databaseName in databaseNames)
            {
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                command.ExecuteNonQuery();
                createdDatabases.Add(databaseName);
            }

            command.CommandText = $"CREATE USER '{userName}'@'127.0.0.1' IDENTIFIED BY '{password}';";
            command.ExecuteNonQuery();
            createdUser = true;
            foreach (var databaseName in databaseNames)
            {
                command.CommandText = $"GRANT ALL PRIVILEGES ON `{databaseName}`.* TO '{userName}'@'127.0.0.1';";
                command.ExecuteNonQuery();
            }
        }
        catch
        {
            if (createdUser)
            {
                command.CommandText = $"DROP USER '{userName}'@'127.0.0.1';";
                command.ExecuteNonQuery();
            }

            foreach (var databaseName in createdDatabases)
            {
                command.CommandText = $"DROP DATABASE `{databaseName}`;";
                command.ExecuteNonQuery();
            }

            throw;
        }

        return new MySqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Database = databaseNames[0],
            UserID = userName,
            Password = password,
        };
    }

    private static void AssertApplicationCannotCreateSchema(MySqlConnectionStringBuilder applicationConnection)
    {
        using var connection = new MySqlConnection(applicationConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `mfx_forbidden_{Guid.NewGuid():N}`;";
        Assert.Throws<MySqlException>(() => command.ExecuteNonQuery());
    }

    private static async Task DuplicateFirstDatabaseRecordAsync(string archivePath, string tableName, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        var entryPath = $"Database/{tableName}.json";
        var entry = archive.GetEntry(entryPath) ?? throw new InvalidDataException($"Missing test archive entry '{entryPath}'.");
        string[] records;
        await using (var entryStream = entry.Open())
        using (var document = await JsonDocument.ParseAsync(entryStream, cancellationToken: cancellationToken))
        {
            records = document.RootElement.EnumerateArray().Select(item => item.GetRawText()).ToArray();
        }

        Assert.NotEmpty(records);
        entry.Delete();
        var duplicateEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        await using var duplicateStream = duplicateEntry.Open();
        await using var writer = new StreamWriter(duplicateStream);
        await writer.WriteAsync($"[{string.Join(',', records.Append(records[0]))}]").ConfigureAwait(false);
    }

    private static async Task AppendInvalidEpisodeRecordAsync(string archivePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        const string entryPath = "Database/Domain/Series/Episodes.json";
        var entry = archive.GetEntry(entryPath) ?? throw new InvalidDataException($"Missing test archive entry '{entryPath}'.");
        string[] records;
        await using (var entryStream = entry.Open())
        using (var document = await JsonDocument.ParseAsync(entryStream, cancellationToken: cancellationToken))
        {
            records = document.RootElement.EnumerateArray().Select(item => item.GetRawText()).ToArray();
        }

        entry.Delete();
        var invalidEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        await using var invalidStream = invalidEntry.Open();
        await using var writer = new StreamWriter(invalidStream);
        var invalidEpisode = JsonSerializer.Serialize(new MulletaFlix.Database.Implementations.Entities.Series.Episode
        {
            Id = int.MaxValue,
            SeasonId = int.MaxValue,
            BaseItemId = Guid.NewGuid(),
            Name = "invalid-foreign-key-fixture",
        });
        await writer.WriteAsync($"[{string.Join(',', records.Append(invalidEpisode))}]").ConfigureAwait(false);
    }

    private static IServerApplicationPaths CreatePaths(
        string configurationPath,
        string dataPath,
        string cachePath,
        string programDataPath,
        string appRootPath,
        string metadataPath,
        string backupPath)
    {
        var paths = new Mock<IServerApplicationPaths>();
        paths.SetupGet(value => value.RootFolderPath).Returns(appRootPath);
        paths.SetupGet(value => value.ConfigurationDirectoryPath).Returns(configurationPath);
        paths.SetupGet(value => value.DataPath).Returns(dataPath);
        paths.SetupGet(value => value.CachePath).Returns(cachePath);
        paths.SetupGet(value => value.ProgramDataPath).Returns(programDataPath);
        paths.SetupGet(value => value.BackupPath).Returns(backupPath);
        paths.SetupGet(value => value.InternalMetadataPath).Returns(metadataPath);
        paths.SetupGet(value => value.DefaultInternalMetadataPath).Returns(metadataPath);
        return paths.Object;
    }

    private sealed class SignalAfterRestoreParticipant(IBackupDatabaseParticipant inner, string signalPath) : IBackupDatabaseParticipant
    {
        public string Name => inner.Name;

        public Task WriteBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
            => inner.WriteBackupAsync(archive, entryPrefix, cancellationToken);

        public Task ValidateBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
            => inner.ValidateBackupAsync(archive, entryPrefix, cancellationToken);

        public async Task RestoreBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
        {
            await inner.RestoreBackupAsync(archive, entryPrefix, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(signalPath, "participant-committed", CancellationToken.None).ConfigureAwait(false);
            await Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private sealed class IndependentMariaDbBackupParticipant(MySqlConnectionStringBuilder applicationConnection, string schema) : IBackupDatabaseParticipant
    {
        private const string TableName = "RestoreParticipantState";

        public string Name => "TestIndependentDb";

        public async Task EnsureFixtureAsync(string value, CancellationToken cancellationToken)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE TABLE `{TableName}` (Id TINYINT NOT NULL PRIMARY KEY, StateValue VARCHAR(128) NOT NULL);";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = $"INSERT INTO `{TableName}` (Id, StateValue) VALUES (1, @value);";
            command.Parameters.AddWithValue("@value", value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task WriteBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
        {
            var value = await ReadValueAsync(cancellationToken);
            var entry = archive.CreateEntry($"{entryPrefix}/data.json", CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await JsonSerializer.SerializeAsync(stream, new IndependentParticipantSnapshot(value), cancellationToken: cancellationToken);
        }

        public async Task ValidateBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
            => _ = await ReadSnapshotAsync(archive, entryPrefix, cancellationToken);

        public async Task RestoreBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
        {
            var snapshot = await ReadSnapshotAsync(archive, entryPrefix, cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = $"DELETE FROM `{TableName}` WHERE Id = 1;";
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = $"INSERT INTO `{TableName}` (Id, StateValue) VALUES (1, @value);";
                insert.Parameters.AddWithValue("@value", snapshot.StateValue);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<string> ReadValueAsync(CancellationToken cancellationToken)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT StateValue FROM `{TableName}` WHERE Id = 1;";
            return (string?)await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidDataException("Independent participant fixture row is missing.");
        }

        public async Task SetValueAsync(string value, CancellationToken cancellationToken)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"UPDATE `{TableName}` SET StateValue = @value WHERE Id = 1;";
            command.Parameters.AddWithValue("@value", value);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
        }

        private MySqlConnection CreateConnection()
            => new(new MySqlConnectionStringBuilder(applicationConnection.ConnectionString) { Database = schema }.ConnectionString);

        private static async Task<IndependentParticipantSnapshot> ReadSnapshotAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
        {
            var entry = archive.GetEntry($"{entryPrefix}/data.json")
                ?? throw new InvalidDataException("Independent participant archive payload is missing.");
            await using var stream = entry.Open();
            var snapshot = await JsonSerializer.DeserializeAsync<IndependentParticipantSnapshot>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Independent participant archive payload is invalid.");
            if (string.IsNullOrWhiteSpace(snapshot.StateValue) || snapshot.StateValue.Length > 128)
            {
                throw new InvalidDataException("Independent participant archive payload contains an invalid state value.");
            }

            return snapshot;
        }

        private sealed record IndependentParticipantSnapshot(string StateValue);
    }

    private sealed class TestContextFactory<TContext>(Func<TContext> create) : IDbContextFactory<TContext>
        where TContext : DbContext
    {
        public TContext CreateDbContext() => create();

        public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(create());
        }
    }
}
