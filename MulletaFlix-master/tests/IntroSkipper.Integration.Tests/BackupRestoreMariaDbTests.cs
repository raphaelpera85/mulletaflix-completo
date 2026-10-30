// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using IntroSkipper.Data;
using IntroSkipper.Db;
using IntroSkipper.Services;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Entities.Books;
using MulletaFlix.Database.Implementations.Entities.Channels;
using MulletaFlix.Database.Implementations.Entities.Movies;
using MulletaFlix.Database.Implementations.Entities.Security;
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
        var signalPath = Path.Combine(root, "database-commit-boundary.signal");
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
            foreach (var crashMode in new[] { "restore-before-commit", "restore" })
            {
                await using (var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
                {
                    await context.ActivityLogs.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                    context.ActivityLogs.Add(new ActivityLog("live-state", "integration-test", Guid.NewGuid()));
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                await File.WriteAllTextAsync(configFilePath, "live-state", TestContext.Current.CancellationToken);
                if (File.Exists(signalPath))
                {
                    File.Delete(signalPath);
                }

                restoreChild = StartMariaDbCrashTestProcess(crashMode, root, schema, signalPath, applicationConnection.ConnectionString);
                var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
                while (!File.Exists(signalPath) && !restoreChild.HasExited && DateTimeOffset.UtcNow < deadline)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
                }

                var crashPoint = crashMode == "restore-before-commit" ? "before" : "after";
                Assert.True(File.Exists(signalPath), $"Restore child did not reach the {crashPoint}-commit crash point.");
                Assert.False(restoreChild.HasExited, "Restore child exited before the test could terminate it.");
                await using (var context = await contextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
                {
                    var rows = await context.ActivityLogs.ToListAsync(TestContext.Current.CancellationToken);
                    if (crashMode == "restore-before-commit")
                    {
                        Assert.Contains(rows, entry => entry.Name == "live-state");
                        Assert.DoesNotContain(rows, entry => entry.Name == "backup-state");
                    }
                    else
                    {
                        Assert.Contains(rows, entry => entry.Name == "backup-state");
                    }
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
                    var rows = await context.ActivityLogs.ToListAsync(TestContext.Current.CancellationToken);
                    Assert.Contains(rows, entry => entry.Name == "backup-state");
                    Assert.DoesNotContain(rows, entry => entry.Name == "live-state");
                }

                Assert.Null(await new BackupRestoreRecoveryJournal(programDataPath).ReadAsync(TestContext.Current.CancellationToken));
                Assert.Empty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.tmp"));
                Assert.Empty(Directory.EnumerateFiles(configurationPath, ".mfx-restore-*.bak"));
            }
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
        if (string.Equals(mode, "restore", StringComparison.Ordinal)
            || string.Equals(mode, "restore-before-commit", StringComparison.Ordinal))
        {
            var signalPath = Environment.GetEnvironmentVariable(CrashTestSignalVariable)
                ?? throw new InvalidOperationException("Crash test signal path was not provided.");
            afterCommit = async commitRelationalTransaction =>
            {
                if (!string.Equals(mode, "restore-before-commit", StringComparison.Ordinal))
                {
                    await commitRelationalTransaction().ConfigureAwait(false);
                }

                await File.WriteAllTextAsync(signalPath, mode).ConfigureAwait(false);
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
            || string.Equals(mode, "restore-before-commit", StringComparison.Ordinal)
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
    public Task RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb()
        => RunDatabaseAndFileRestoreAsync(includeMongoRestore: false);

    [Fact]
    [Trait("Category", "RequiresMongo")]
    public Task RecoveryExercise_RestoresMariaDbMongoAndFilesFromIsolatedSources()
        => RunDatabaseAndFileRestoreAsync(includeMongoRestore: true);

    private async Task RunDatabaseAndFileRestoreAsync(bool includeMongoRestore)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mongoConnection = includeMongoRestore ? GetIsolatedMongoConnection() : null;
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
            var backupUserId = Guid.NewGuid();
            var backupBaseItemId = Guid.NewGuid();
            var backupChildItemId = Guid.NewGuid();
            var backupBaseItemImageId = Guid.NewGuid();
            var backupPersonId = Guid.NewGuid();
            var backupItemValueId = Guid.NewGuid();
            var paymentTransactionWithCouponId = Guid.NewGuid();
            var paymentTransactionWithoutCouponId = Guid.NewGuid();
            const string backupApiToken = "backup-system-api-token";
            const string backupDeviceId = "backup-system-device";
            const int backupPricingPlanId = 9101;
            const int backupDiscountCouponId = 9102;
            var displayPreferencesItemId = Guid.NewGuid();
            var itemDisplayPreferencesItemId = Guid.NewGuid();
            var customItemDisplayPreferencesItemId = Guid.NewGuid();
            var trickplayItemId = Guid.NewGuid();
            var mediaSegmentId = Guid.NewGuid();
            const string preferenceValue = "backup-blocked-tags";
            var licenseStartDate = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var licenseExpirationDate = new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var mediaRecognitionDate = new DateTime(2026, 2, 20, 8, 30, 0, DateTimeKind.Utc);
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
                var backupUser = new User("backup-user", "integration-auth-provider", "integration-reset-provider")
                {
                    Id = backupUserId
                };
                var backupBaseItem = new BaseItemEntity
                {
                    Id = backupBaseItemId,
                    Type = "Movie",
                    Name = "backup-central-item-fixture",
                    Path = "backup-fixture/movie.strm",
                    ProductionYear = 2026,
                    IsMovie = true,
                    DateCreated = licenseStartDate,
                    DateModified = licenseStartDate.AddDays(1)
                };
                var backupChildItem = new BaseItemEntity
                {
                    Id = backupChildItemId,
                    Type = "Episode",
                    Name = "backup-central-child-fixture",
                    Path = "backup-fixture/child.strm",
                    ProductionYear = 2026
                };
                backupUser.AccessSchedules.Add(new AccessSchedule(DynamicDayOfWeek.Weekday, 8, 18, backupUserId));
                context.Users.Add(backupUser);
                context.BaseItems.AddRange(backupBaseItem, backupChildItem);
                context.UserData.Add(new UserData
                {
                    CustomDataKey = "backup-user-data-fixture",
                    Rating = 8.5,
                    PlaybackPositionTicks = 123456789,
                    PlayCount = 3,
                    IsFavorite = true,
                    LastPlayedDate = mediaRecognitionDate,
                    Played = true,
                    AudioStreamIndex = 1,
                    SubtitleStreamIndex = 2,
                    Likes = true,
                    RetentionDate = licenseExpirationDate,
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    UserId = backupUserId,
                    User = backupUser
                });
                context.AttachmentStreamInfos.Add(new AttachmentStreamInfo
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    Index = 2,
                    Codec = "ttf",
                    CodecTag = "backup-codec-tag",
                    Comment = "backup-attachment-fixture",
                    Filename = "fixture-font.ttf",
                    MimeType = "application/x-truetype-font"
                });
                context.BaseItemProviders.Add(new BaseItemProvider
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    ProviderId = "backup-provider-fixture",
                    ProviderValue = "backup-provider-value"
                });
                context.BaseItemImageInfos.Add(new BaseItemImageInfo
                {
                    Id = backupBaseItemImageId,
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    Path = "images/backup-base-item-primary.jpg",
                    DateModified = licenseStartDate,
                    ImageType = ImageInfoImageType.Primary,
                    Width = 1200,
                    Height = 1800,
                    Blurhash = [1, 2, 3, 4],
                    ProviderName = "backup-image-provider",
                    Language = "pt-br"
                });
                context.BaseItemMetadataFields.Add(new BaseItemMetadataField
                {
                    Id = 5,
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem
                });
                context.BaseItemTrailerTypes.Add(new BaseItemTrailerType
                {
                    Id = 2,
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem
                });
                context.Chapters.Add(new Chapter
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    ChapterIndex = 1,
                    StartPositionTicks = 90000000,
                    Name = "backup-chapter-fixture",
                    ImagePath = "chapters/backup-chapter.jpg",
                    ImageDateModified = licenseStartDate
                });
                context.MediaStreamInfos.Add(new MediaStreamInfo
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    StreamIndex = 0,
                    StreamType = MediaStreamTypeEntity.Video,
                    Codec = "h264",
                    Language = "pt-br",
                    ChannelLayout = "stereo",
                    Profile = "high",
                    Path = "backup-fixture/movie.strm",
                    IsInterlaced = false,
                    BitRate = 8000000,
                    Channels = 2,
                    SampleRate = 48000,
                    IsDefault = true,
                    IsForced = false,
                    IsExternal = false,
                    IsOriginal = true,
                    Height = 1080,
                    Width = 1920,
                    AverageFrameRate = 23.976f,
                    RealFrameRate = 24f,
                    PixelFormat = "yuv420p",
                    BitDepth = 8,
                    CodecTag = "avc1",
                    Title = "backup-video-stream-fixture",
                    ColorPrimaries = "bt709",
                    ColorSpace = "bt709",
                    ColorTransfer = "bt709"
                });
                context.KeyframeData.Add(new KeyframeData
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    TotalDuration = 1800000000,
                    KeyframeTicks = [0, 90000000, 180000000]
                });
                context.AncestorIds.Add(new AncestorId
                {
                    ParentItemId = backupBaseItemId,
                    ItemId = backupChildItemId,
                    ParentItem = backupBaseItem,
                    Item = backupChildItem
                });
                context.LinkedChildren.Add(new LinkedChildEntity
                {
                    ParentId = backupBaseItemId,
                    ChildId = backupChildItemId,
                    ChildType = LinkedChildType.Manual,
                    SortOrder = 3,
                    Parent = backupBaseItem,
                    Child = backupChildItem
                });
                var backupPerson = new People
                {
                    Id = backupPersonId,
                    Name = "Backup Person Fixture",
                    PersonType = "Actor"
                };
                context.Peoples.Add(backupPerson);
                context.PeopleBaseItemMap.Add(new PeopleBaseItemMap
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    PeopleId = backupPersonId,
                    People = backupPerson,
                    Role = "backup-role-fixture",
                    SortOrder = 2,
                    ListOrder = 1
                });
                var backupItemValue = new ItemValue
                {
                    ItemValueId = backupItemValueId,
                    Type = ItemValueType.Genre,
                    Value = "Backup Genre Fixture",
                    CleanValue = "backup genre fixture"
                };
                context.ItemValues.Add(backupItemValue);
                context.ItemValuesMap.Add(new ItemValueMap
                {
                    ItemId = backupBaseItemId,
                    Item = backupBaseItem,
                    ItemValueId = backupItemValueId,
                    ItemValue = backupItemValue
                });
                context.ActionLogs.Add(ActionLog.Create(
                    "restore-fixture",
                    "Backup",
                    "backup-state",
                    backupUserId,
                    "backup-user",
                    details: "backup-action-log"));
                context.ApiKeys.Add(new ApiKey("backup-api-key")
                {
                    AccessToken = backupApiToken
                });
                context.Preferences.Add(new Preference(PreferenceKind.BlockedTags, preferenceValue)
                {
                    UserId = backupUserId
                });
                context.DisplayPreferences.Add(new DisplayPreferences(backupUserId, displayPreferencesItemId, "backup-client")
                {
                    ShowSidebar = true,
                    ShowBackdrop = false,
                    SkipForwardLength = 45000,
                    SkipBackwardLength = 15000
                });
                context.ItemDisplayPreferences.Add(new ItemDisplayPreferences(backupUserId, itemDisplayPreferencesItemId, "backup-client")
                {
                    ViewType = ViewType.Movies,
                    RememberSorting = true,
                    RememberIndexing = true,
                    SortBy = "DateCreated",
                    SortOrder = SortOrder.Descending
                });
                context.CustomItemDisplayPreferences.Add(new CustomItemDisplayPreferences(
                    backupUserId,
                    customItemDisplayPreferencesItemId,
                    "backup-client",
                    "preferred-audio-language",
                    "pt-BR"));
                context.Permissions.Add(new Permission(PermissionKind.EnableRemoteAccess, true)
                {
                    UserId = backupUserId
                });
                context.Devices.Add(new Device(backupUserId, "backup-app", "1.2.3", "backup-device", "backup-device-id")
                {
                    AccessToken = "backup-device-access-token",
                    IsActive = true,
                    DateCreated = licenseStartDate,
                    DateModified = licenseStartDate.AddDays(1),
                    DateLastActivity = licenseStartDate.AddDays(2)
                });
                context.UserLicenses.Add(new UserLicense
                {
                    UserId = backupUserId,
                    User = backupUser,
                    StartDate = licenseStartDate,
                    DurationHours = 8760,
                    ExpirationDate = licenseExpirationDate,
                    IsUnlimited = false,
                    AdminNotes = "backup-license-fixture",
                    GrantedByUserId = backupUserId,
                    CreatedAt = licenseStartDate,
                    UpdatedAt = licenseStartDate
                });
                context.TrickplayInfos.Add(new TrickplayInfo
                {
                    ItemId = trickplayItemId,
                    Width = 320,
                    Height = 180,
                    TileWidth = 10,
                    TileHeight = 10,
                    ThumbnailCount = 100,
                    Interval = 10000,
                    Bandwidth = 256000
                });
                context.MediaSegments.Add(new MediaSegment
                {
                    Id = mediaSegmentId,
                    ItemId = trickplayItemId,
                    Type = MediaSegmentType.Intro,
                    StartTicks = 120000000,
                    EndTicks = 300000000,
                    SegmentProviderId = "backup-segment-provider"
                });
                context.ImageInfos.Add(new ImageInfo("images/backup-profile.png")
                {
                    LastModified = new DateTime(2026, 2, 18, 9, 45, 0, DateTimeKind.Utc)
                });
                context.MidiaStorageOnlineMediaMetadata.Add(new MidiaStorageOnlineMediaMetadata
                {
                    RelativePath = "Filmes/backup-movie.strm",
                    ContentType = "movie",
                    Title = "backup-recognized-movie",
                    Year = 2026,
                    Mode = "strm",
                    SourceUrl = "https://media.example.test/backup-movie",
                    SourceId = "backup-source-id",
                    OriginalFileName = "backup-movie.strm",
                    RecognizedAtUtc = mediaRecognitionDate
                });
                context.PricingPlans.Add(new PricingPlan
                {
                    Id = backupPricingPlanId,
                    Name = "backup-plan-fixture",
                    DurationMonths = 17,
                    PricePerMonth = 12.50m,
                    TotalPrice = 212.50m,
                    IsActive = true,
                    IsHighlighted = true,
                    SortOrder = 4,
                    CreatedAt = licenseStartDate,
                    UpdatedAt = licenseStartDate.AddDays(1)
                });
                context.DiscountCoupons.Add(new DiscountCoupon
                {
                    Id = backupDiscountCouponId,
                    Code = "BACKUP-COUPON-FIXTURE",
                    DiscountPercent = 12.5m,
                    DiscountFixed = 3.25m,
                    MaxUses = 40,
                    CurrentUses = 7,
                    ValidFrom = licenseStartDate,
                    ValidUntil = licenseExpirationDate,
                    IsActive = true,
                    FirstPurchaseOnly = true,
                    MinPlanMonths = 6,
                    MinOrderAmount = 20m,
                    CreatedAt = licenseStartDate,
                    UpdatedAt = licenseStartDate.AddDays(1)
                });
                context.PaymentGatewayConfigs.Add(new PaymentGatewayConfig
                {
                    GatewayName = "backup-gateway-fixture",
                    DisplayName = "Backup Gateway Fixture",
                    IsEnabled = true,
                    IsPrimary = false,
                    AccessToken = "synthetic-access-token",
                    PublicKey = "synthetic-public-key",
                    WebhookSecret = "synthetic-webhook-secret",
                    SandboxMode = true,
                    EnablePix = true,
                    EnableCredit = true,
                    EnableDebit = false,
                    ExtraConfig = "{\"fixture\":true}",
                    CreatedAt = licenseStartDate,
                    UpdatedAt = licenseStartDate.AddDays(1)
                });
                context.PaymentTransactions.AddRange(
                    new PaymentTransaction
                    {
                        Id = paymentTransactionWithCouponId,
                        UserId = backupUserId,
                        PricingPlanId = backupPricingPlanId,
                        GatewayName = "backup-gateway-fixture",
                        GatewayTransactionId = "backup-transaction-with-coupon",
                        PaymentMethod = PaymentMethod.Pix,
                        Amount = 209.25m,
                        DiscountAmount = 3.25m,
                        CouponId = backupDiscountCouponId,
                        Status = PaymentStatus.Approved,
                        IsRecurring = true,
                        RecurringSubscriptionId = "backup-recurring-fixture",
                        CustomerEmail = "backup-customer@example.test",
                        CustomerName = "Backup Customer Fixture",
                        CustomerPhone = "+5500000000000",
                        IpAddress = "192.0.2.10",
                        UserAgent = "BackupRestoreMariaDbTests",
                        CreatedAt = licenseStartDate,
                        PaidAt = licenseStartDate.AddHours(1),
                        ExpiresAt = licenseExpirationDate,
                        GatewayResponse = "{\"fixture\":\"approved\"}"
                    },
                    new PaymentTransaction
                    {
                        Id = paymentTransactionWithoutCouponId,
                        UserId = backupUserId,
                        PricingPlanId = backupPricingPlanId,
                        GatewayName = "backup-gateway-fixture",
                        GatewayTransactionId = "backup-transaction-without-coupon",
                        PaymentMethod = PaymentMethod.Credit,
                        Amount = 212.50m,
                        DiscountAmount = 0m,
                        CouponId = null,
                        Status = PaymentStatus.Pending,
                        IsRecurring = false,
                        CustomerEmail = "backup-customer@example.test",
                        CustomerName = "Backup Customer Fixture",
                        IpAddress = "192.0.2.11",
                        CreatedAt = licenseStartDate.AddMinutes(1)
                    });
                context.DeviceOptions.Add(new DeviceOptions(backupDeviceId)
                {
                    CustomName = "backup-device-name"
                });
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
                var movie = new Movie { BaseItemId = Guid.NewGuid(), Name = "backup-domain-movie" };
                movie.Metadata.Add(new MovieMetadata
                {
                    Title = "backup-movie-title",
                    Language = "pt-BR",
                    IsDefault = true
                });
                movie.UserData.Add(new MovieUserData
                {
                    UserId = Guid.NewGuid(),
                    Played = true,
                    PlayCount = 3,
                    IsFavorite = true,
                    LastPlayedDate = DateTime.UtcNow
                });
                context.Movies.Add(movie);
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await seriesFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                const int seriesId = 71;
                const int domainSeasonId = 72;
                const int episodeId = 73;
                var series = new Series { Id = seriesId, BaseItemId = Guid.NewGuid(), Name = "backup-domain-series" };
                series.Seasons.Add(new Season
                {
                    Id = domainSeasonId,
                    SeriesId = seriesId,
                    BaseItemId = Guid.NewGuid(),
                    Name = "backup-domain-season",
                    IndexNumber = 1
                });
                series.Seasons.Single().Episodes.Add(new Episode
                {
                    Id = episodeId,
                    SeasonId = domainSeasonId,
                    BaseItemId = Guid.NewGuid(),
                    Name = "backup-domain-episode",
                    IndexNumber = 2,
                    ParentIndexNumber = 1,
                    RunTimeTicks = TimeSpan.FromMinutes(42).Ticks
                });
                series.UserData.Add(new SeriesUserData
                {
                    UserId = Guid.NewGuid(),
                    Played = true,
                    IsFavorite = true,
                    LastPlayedDate = DateTime.UtcNow
                });
                context.Series.Add(series);
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await channelsFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                context.Channels.Add(new Channel
                {
                    Id = 81,
                    BaseItemId = Guid.NewGuid(),
                    Name = "backup-domain-channel",
                    Programs =
                    {
                        new Program
                        {
                            Id = 82,
                            ChannelId = 81,
                            BaseItemId = Guid.NewGuid(),
                            Name = "backup-domain-program",
                            StartDate = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
                            EndDate = new DateTime(2026, 10, 29, 13, 0, 0, DateTimeKind.Utc)
                        }
                    }
                });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await booksFactory.CreateDbContextAsync(cancellationToken))
            {
                await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
                var book = new Book { BaseItemId = Guid.NewGuid(), Name = "backup-domain-book" };
                book.UserData.Add(new BookUserData
                {
                    UserId = Guid.NewGuid(),
                    Played = true,
                    IsFavorite = true
                });
                context.Books.Add(book);
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
                var restoredMovie = await context.Movies
                    .Include(movie => movie.Metadata)
                    .Include(movie => movie.UserData)
                    .SingleAsync(cancellationToken);
                Assert.Equal("backup-domain-movie", restoredMovie.Name);
                var restoredMovieMetadata = Assert.Single(restoredMovie.Metadata);
                Assert.Equal("backup-movie-title", restoredMovieMetadata.Title);
                Assert.Equal("pt-BR", restoredMovieMetadata.Language);
                Assert.True(restoredMovieMetadata.IsDefault);
                var restoredMovieUserData = Assert.Single(restoredMovie.UserData);
                Assert.True(restoredMovieUserData.Played);
                Assert.Equal(3, restoredMovieUserData.PlayCount);
                Assert.True(restoredMovieUserData.IsFavorite);
                Assert.NotNull(restoredMovieUserData.LastPlayedDate);
            }

            await using (var context = await seriesFactory.CreateDbContextAsync(cancellationToken))
            {
                var restoredSeries = await context.Series
                    .Include(series => series.UserData)
                    .Include(series => series.Seasons)
                        .ThenInclude(season => season.Episodes)
                    .SingleAsync(cancellationToken);
                Assert.Equal("backup-domain-series", restoredSeries.Name);
                var restoredSeason = Assert.Single(restoredSeries.Seasons);
                Assert.Equal("backup-domain-season", restoredSeason.Name);
                Assert.Equal(1, restoredSeason.IndexNumber);
                var restoredEpisode = Assert.Single(restoredSeason.Episodes);
                Assert.Equal("backup-domain-episode", restoredEpisode.Name);
                Assert.Equal(2, restoredEpisode.IndexNumber);
                Assert.Equal(1, restoredEpisode.ParentIndexNumber);
                Assert.Equal(TimeSpan.FromMinutes(42).Ticks, restoredEpisode.RunTimeTicks);
                var restoredSeriesUserData = Assert.Single(restoredSeries.UserData);
                Assert.True(restoredSeriesUserData.Played);
                Assert.True(restoredSeriesUserData.IsFavorite);
                Assert.NotNull(restoredSeriesUserData.LastPlayedDate);
            }

            await using (var context = await channelsFactory.CreateDbContextAsync(cancellationToken))
            {
                var restoredChannel = await context.Channels
                    .Include(channel => channel.Programs)
                    .SingleAsync(cancellationToken);
                Assert.Equal("backup-domain-channel", restoredChannel.Name);
                var restoredProgram = Assert.Single(restoredChannel.Programs);
                Assert.Equal("backup-domain-program", restoredProgram.Name);
                Assert.Equal(new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc), restoredProgram.StartDate);
                Assert.Equal(new DateTime(2026, 10, 29, 13, 0, 0, DateTimeKind.Utc), restoredProgram.EndDate);
            }

            await using (var context = await booksFactory.CreateDbContextAsync(cancellationToken))
            {
                var restoredBook = await context.Books
                    .Include(book => book.UserData)
                    .SingleAsync(cancellationToken);
                Assert.Equal("backup-domain-book", restoredBook.Name);
                var restoredBookUserData = Assert.Single(restoredBook.UserData);
                Assert.True(restoredBookUserData.Played);
                Assert.True(restoredBookUserData.IsFavorite);
            }

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.ActivityLogs.ExecuteDeleteAsync(cancellationToken);
                await context.PlaybackReports.ExecuteDeleteAsync(cancellationToken);
                var user = await context.Users.SingleAsync(value => value.Id == backupUserId, cancellationToken);
                user.Username = "live-user";
                user.NormalizedUsername = "LIVE-USER";
                var apiKey = await context.ApiKeys.SingleAsync(value => value.AccessToken == backupApiToken, cancellationToken);
                apiKey.Name = "live-api-key";
                var deviceOptions = await context.DeviceOptions.SingleAsync(value => value.DeviceId == backupDeviceId, cancellationToken);
                deviceOptions.CustomName = "live-device-name";
                await context.SaveChangesAsync(cancellationToken);
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

            var restoreHistory = (await backupService.GetBackupHistory())
                .Single(operation => operation.Key == "restore"
                    && operation.Path == createdBackup.Path
                    && operation.Status == TaskCompletionStatus.Completed);
            Assert.True(restoreHistory.EndTimeUtc > restoreHistory.StartTimeUtc);

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
                var restoredUser = await context.Users
                    .Include(user => user.AccessSchedules)
                    .SingleAsync(value => value.Id == backupUserId, cancellationToken);
                Assert.Equal("backup-user", restoredUser.Username);
                var restoredBaseItem = await context.BaseItems.SingleAsync(value => value.Id == backupBaseItemId, cancellationToken);
                Assert.Equal("Movie", restoredBaseItem.Type);
                Assert.Equal("backup-central-item-fixture", restoredBaseItem.Name);
                Assert.Equal("backup-fixture/movie.strm", restoredBaseItem.Path);
                Assert.Equal(2026, restoredBaseItem.ProductionYear);
                Assert.True(restoredBaseItem.IsMovie);
                Assert.Equal(licenseStartDate, restoredBaseItem.DateCreated);
                Assert.Equal(licenseStartDate.AddDays(1), restoredBaseItem.DateModified);
                var restoredAttachment = await context.AttachmentStreamInfos.SingleAsync(
                    value => value.ItemId == backupBaseItemId && value.Index == 2,
                    cancellationToken);
                Assert.Equal("ttf", restoredAttachment.Codec);
                Assert.Equal("backup-codec-tag", restoredAttachment.CodecTag);
                Assert.Equal("backup-attachment-fixture", restoredAttachment.Comment);
                Assert.Equal("fixture-font.ttf", restoredAttachment.Filename);
                Assert.Equal("application/x-truetype-font", restoredAttachment.MimeType);
                var restoredProvider = await context.BaseItemProviders.SingleAsync(
                    value => value.ItemId == backupBaseItemId,
                    cancellationToken);
                Assert.Equal("backup-provider-fixture", restoredProvider.ProviderId);
                Assert.Equal("backup-provider-value", restoredProvider.ProviderValue);
                var restoredBaseItemImage = await context.BaseItemImageInfos.SingleAsync(
                    value => value.Id == backupBaseItemImageId,
                    cancellationToken);
                Assert.Equal(backupBaseItemId, restoredBaseItemImage.ItemId);
                Assert.Equal("images/backup-base-item-primary.jpg", restoredBaseItemImage.Path);
                Assert.Equal(licenseStartDate, restoredBaseItemImage.DateModified);
                Assert.Equal(ImageInfoImageType.Primary, restoredBaseItemImage.ImageType);
                Assert.Equal(1200, restoredBaseItemImage.Width);
                Assert.Equal(1800, restoredBaseItemImage.Height);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, restoredBaseItemImage.Blurhash);
                Assert.Equal("backup-image-provider", restoredBaseItemImage.ProviderName);
                Assert.Equal("pt-br", restoredBaseItemImage.Language);
                var restoredMetadataField = await context.BaseItemMetadataFields.SingleAsync(
                    value => value.ItemId == backupBaseItemId,
                    cancellationToken);
                Assert.Equal(5, restoredMetadataField.Id);
                var restoredTrailerType = await context.BaseItemTrailerTypes.SingleAsync(
                    value => value.ItemId == backupBaseItemId,
                    cancellationToken);
                Assert.Equal(2, restoredTrailerType.Id);
                var restoredChapter = await context.Chapters.SingleAsync(
                    value => value.ItemId == backupBaseItemId && value.ChapterIndex == 1,
                    cancellationToken);
                Assert.Equal(90000000, restoredChapter.StartPositionTicks);
                Assert.Equal("backup-chapter-fixture", restoredChapter.Name);
                Assert.Equal("chapters/backup-chapter.jpg", restoredChapter.ImagePath);
                Assert.Equal(licenseStartDate, restoredChapter.ImageDateModified);
                var restoredMediaStream = await context.MediaStreamInfos.SingleAsync(
                    value => value.ItemId == backupBaseItemId && value.StreamIndex == 0,
                    cancellationToken);
                Assert.Equal(MediaStreamTypeEntity.Video, restoredMediaStream.StreamType);
                Assert.Equal("h264", restoredMediaStream.Codec);
                Assert.Equal("pt-br", restoredMediaStream.Language);
                Assert.Equal("stereo", restoredMediaStream.ChannelLayout);
                Assert.Equal("high", restoredMediaStream.Profile);
                Assert.Equal("backup-fixture/movie.strm", restoredMediaStream.Path);
                Assert.Equal(false, restoredMediaStream.IsInterlaced);
                Assert.Equal(8000000, restoredMediaStream.BitRate);
                Assert.Equal(2, restoredMediaStream.Channels);
                Assert.Equal(48000, restoredMediaStream.SampleRate);
                Assert.True(restoredMediaStream.IsDefault);
                Assert.False(restoredMediaStream.IsForced);
                Assert.False(restoredMediaStream.IsExternal);
                Assert.True(restoredMediaStream.IsOriginal);
                Assert.Equal(1080, restoredMediaStream.Height);
                Assert.Equal(1920, restoredMediaStream.Width);
                Assert.Equal(23.976f, restoredMediaStream.AverageFrameRate);
                Assert.Equal(24f, restoredMediaStream.RealFrameRate);
                Assert.Equal("yuv420p", restoredMediaStream.PixelFormat);
                Assert.Equal(8, restoredMediaStream.BitDepth);
                Assert.Equal("avc1", restoredMediaStream.CodecTag);
                Assert.Equal("backup-video-stream-fixture", restoredMediaStream.Title);
                Assert.Equal("bt709", restoredMediaStream.ColorPrimaries);
                Assert.Equal("bt709", restoredMediaStream.ColorSpace);
                Assert.Equal("bt709", restoredMediaStream.ColorTransfer);
                var restoredKeyframeData = await context.KeyframeData.SingleAsync(
                    value => value.ItemId == backupBaseItemId,
                    cancellationToken);
                Assert.Equal(1800000000, restoredKeyframeData.TotalDuration);
                Assert.Equal(new long[] { 0, 90000000, 180000000 }, restoredKeyframeData.KeyframeTicks);
                var restoredChildItem = await context.BaseItems.SingleAsync(value => value.Id == backupChildItemId, cancellationToken);
                Assert.Equal("Episode", restoredChildItem.Type);
                Assert.Equal("backup-central-child-fixture", restoredChildItem.Name);
                var restoredAncestor = await context.AncestorIds.SingleAsync(
                    value => value.ItemId == backupChildItemId && value.ParentItemId == backupBaseItemId,
                    cancellationToken);
                Assert.Equal(backupBaseItemId, restoredAncestor.ParentItemId);
                var restoredLinkedChild = await context.LinkedChildren.SingleAsync(
                    value => value.ParentId == backupBaseItemId && value.ChildId == backupChildItemId,
                    cancellationToken);
                Assert.Equal(LinkedChildType.Manual, restoredLinkedChild.ChildType);
                Assert.Equal(3, restoredLinkedChild.SortOrder);
                var restoredPerson = await context.Peoples.SingleAsync(value => value.Id == backupPersonId, cancellationToken);
                Assert.Equal("Backup Person Fixture", restoredPerson.Name);
                Assert.Equal("Actor", restoredPerson.PersonType);
                var restoredPeopleMap = await context.PeopleBaseItemMap.SingleAsync(
                    value => value.ItemId == backupBaseItemId && value.PeopleId == backupPersonId,
                    cancellationToken);
                Assert.Equal("backup-role-fixture", restoredPeopleMap.Role);
                Assert.Equal(2, restoredPeopleMap.SortOrder);
                Assert.Equal(1, restoredPeopleMap.ListOrder);
                var restoredItemValue = await context.ItemValues.SingleAsync(value => value.ItemValueId == backupItemValueId, cancellationToken);
                Assert.Equal(ItemValueType.Genre, restoredItemValue.Type);
                Assert.Equal("Backup Genre Fixture", restoredItemValue.Value);
                Assert.Equal("backup genre fixture", restoredItemValue.CleanValue);
                var restoredItemValueMap = await context.ItemValuesMap.SingleAsync(
                    value => value.ItemId == backupBaseItemId && value.ItemValueId == backupItemValueId,
                    cancellationToken);
                Assert.Equal(backupBaseItemId, restoredItemValueMap.ItemId);
                var restoredUserData = await context.UserData.SingleAsync(
                    value => value.ItemId == backupBaseItemId && value.UserId == backupUserId,
                    cancellationToken);
                Assert.Equal("backup-user-data-fixture", restoredUserData.CustomDataKey);
                Assert.Equal(8.5, restoredUserData.Rating);
                Assert.Equal(123456789, restoredUserData.PlaybackPositionTicks);
                Assert.Equal(3, restoredUserData.PlayCount);
                Assert.True(restoredUserData.IsFavorite);
                Assert.Equal(mediaRecognitionDate, restoredUserData.LastPlayedDate);
                Assert.True(restoredUserData.Played);
                Assert.Equal(1, restoredUserData.AudioStreamIndex);
                Assert.Equal(2, restoredUserData.SubtitleStreamIndex);
                Assert.True(restoredUserData.Likes);
                Assert.Equal(licenseExpirationDate, restoredUserData.RetentionDate);
                Assert.Equal(backupBaseItemId, restoredUserData.ItemId);
                Assert.Equal(backupUserId, restoredUserData.UserId);
                var restoredSchedule = Assert.Single(restoredUser.AccessSchedules);
                Assert.Equal(DynamicDayOfWeek.Weekday, restoredSchedule.DayOfWeek);
                Assert.Equal(8, restoredSchedule.StartHour);
                Assert.Equal(18, restoredSchedule.EndHour);
                var restoredActionLog = await context.ActionLogs.SingleAsync(cancellationToken);
                Assert.Equal("restore-fixture", restoredActionLog.ActionType);
                Assert.Equal("Backup", restoredActionLog.EntityType);
                Assert.Equal("backup-state", restoredActionLog.EntityId);
                Assert.Equal(backupUserId, restoredActionLog.UserId);
                Assert.Equal("backup-action-log", restoredActionLog.Details);
                Assert.Equal("backup-api-key", (await context.ApiKeys.SingleAsync(value => value.AccessToken == backupApiToken, cancellationToken)).Name);
                var restoredPreference = await context.Preferences.SingleAsync(value => value.UserId == backupUserId, cancellationToken);
                Assert.Equal(PreferenceKind.BlockedTags, restoredPreference.Kind);
                Assert.Equal(preferenceValue, restoredPreference.Value);
                var restoredDisplayPreferences = await context.DisplayPreferences.SingleAsync(value => value.UserId == backupUserId, cancellationToken);
                Assert.Equal(displayPreferencesItemId, restoredDisplayPreferences.ItemId);
                Assert.True(restoredDisplayPreferences.ShowSidebar);
                Assert.False(restoredDisplayPreferences.ShowBackdrop);
                Assert.Equal(45000, restoredDisplayPreferences.SkipForwardLength);
                Assert.Equal(15000, restoredDisplayPreferences.SkipBackwardLength);
                var restoredItemDisplayPreferences = await context.ItemDisplayPreferences.SingleAsync(value => value.UserId == backupUserId, cancellationToken);
                Assert.Equal(itemDisplayPreferencesItemId, restoredItemDisplayPreferences.ItemId);
                Assert.Equal(ViewType.Movies, restoredItemDisplayPreferences.ViewType);
                Assert.True(restoredItemDisplayPreferences.RememberSorting);
                Assert.True(restoredItemDisplayPreferences.RememberIndexing);
                Assert.Equal("DateCreated", restoredItemDisplayPreferences.SortBy);
                Assert.Equal(SortOrder.Descending, restoredItemDisplayPreferences.SortOrder);
                var restoredCustomItemDisplayPreferences = await context.CustomItemDisplayPreferences.SingleAsync(value => value.UserId == backupUserId, cancellationToken);
                Assert.Equal(customItemDisplayPreferencesItemId, restoredCustomItemDisplayPreferences.ItemId);
                Assert.Equal("backup-client", restoredCustomItemDisplayPreferences.Client);
                Assert.Equal("preferred-audio-language", restoredCustomItemDisplayPreferences.Key);
                Assert.Equal("pt-BR", restoredCustomItemDisplayPreferences.Value);
                var restoredPermission = await context.Permissions.SingleAsync(value => value.UserId == backupUserId, cancellationToken);
                Assert.Equal(PermissionKind.EnableRemoteAccess, restoredPermission.Kind);
                Assert.True(restoredPermission.Value);
                var restoredDevice = await context.Devices.SingleAsync(value => value.DeviceId == "backup-device-id", cancellationToken);
                Assert.Equal(backupUserId, restoredDevice.UserId);
                Assert.Equal("backup-device-access-token", restoredDevice.AccessToken);
                Assert.Equal("backup-app", restoredDevice.AppName);
                Assert.Equal("1.2.3", restoredDevice.AppVersion);
                Assert.Equal("backup-device", restoredDevice.DeviceName);
                Assert.True(restoredDevice.IsActive);
                Assert.Equal(licenseStartDate, restoredDevice.DateCreated);
                Assert.Equal(licenseStartDate.AddDays(1), restoredDevice.DateModified);
                Assert.Equal(licenseStartDate.AddDays(2), restoredDevice.DateLastActivity);
                var restoredLicense = await context.UserLicenses.SingleAsync(value => value.UserId == backupUserId, cancellationToken);
                Assert.Equal(licenseStartDate, restoredLicense.StartDate);
                Assert.Equal(8760, restoredLicense.DurationHours);
                Assert.Equal(licenseExpirationDate, restoredLicense.ExpirationDate);
                Assert.False(restoredLicense.IsUnlimited);
                Assert.Equal("backup-license-fixture", restoredLicense.AdminNotes);
                Assert.Equal(backupUserId, restoredLicense.GrantedByUserId);
                var restoredTrickplay = await context.TrickplayInfos.SingleAsync(value => value.ItemId == trickplayItemId, cancellationToken);
                Assert.Equal(320, restoredTrickplay.Width);
                Assert.Equal(180, restoredTrickplay.Height);
                Assert.Equal(10, restoredTrickplay.TileWidth);
                Assert.Equal(10, restoredTrickplay.TileHeight);
                Assert.Equal(100, restoredTrickplay.ThumbnailCount);
                Assert.Equal(10000, restoredTrickplay.Interval);
                Assert.Equal(256000, restoredTrickplay.Bandwidth);
                var restoredMediaSegment = await context.MediaSegments.SingleAsync(value => value.Id == mediaSegmentId, cancellationToken);
                Assert.Equal(trickplayItemId, restoredMediaSegment.ItemId);
                Assert.Equal(MediaSegmentType.Intro, restoredMediaSegment.Type);
                Assert.Equal(120000000, restoredMediaSegment.StartTicks);
                Assert.Equal(300000000, restoredMediaSegment.EndTicks);
                Assert.Equal("backup-segment-provider", restoredMediaSegment.SegmentProviderId);
                var restoredImageInfo = await context.ImageInfos.SingleAsync(value => value.Path == "images/backup-profile.png", cancellationToken);
                Assert.Equal(new DateTime(2026, 2, 18, 9, 45, 0, DateTimeKind.Utc), restoredImageInfo.LastModified);
                var restoredRecognition = await context.MidiaStorageOnlineMediaMetadata.SingleAsync(cancellationToken);
                Assert.Equal("Filmes/backup-movie.strm", restoredRecognition.RelativePath);
                Assert.Equal("movie", restoredRecognition.ContentType);
                Assert.Equal("backup-recognized-movie", restoredRecognition.Title);
                Assert.Equal(2026, restoredRecognition.Year);
                Assert.Equal("https://media.example.test/backup-movie", restoredRecognition.SourceUrl);
                Assert.Equal("backup-source-id", restoredRecognition.SourceId);
                Assert.Equal("backup-movie.strm", restoredRecognition.OriginalFileName);
                Assert.Equal(mediaRecognitionDate, restoredRecognition.RecognizedAtUtc);
                var restoredPlan = await context.PricingPlans.SingleAsync(value => value.Id == backupPricingPlanId, cancellationToken);
                Assert.Equal("backup-plan-fixture", restoredPlan.Name);
                Assert.Equal(17, restoredPlan.DurationMonths);
                Assert.Equal(12.50m, restoredPlan.PricePerMonth);
                Assert.Equal(212.50m, restoredPlan.TotalPrice);
                Assert.True(restoredPlan.IsActive);
                Assert.True(restoredPlan.IsHighlighted);
                Assert.Equal(4, restoredPlan.SortOrder);
                Assert.Equal(licenseStartDate, restoredPlan.CreatedAt);
                Assert.Equal(licenseStartDate.AddDays(1), restoredPlan.UpdatedAt);
                var restoredCoupon = await context.DiscountCoupons.SingleAsync(value => value.Id == backupDiscountCouponId, cancellationToken);
                Assert.Equal("BACKUP-COUPON-FIXTURE", restoredCoupon.Code);
                Assert.Equal(12.5m, restoredCoupon.DiscountPercent);
                Assert.Equal(3.25m, restoredCoupon.DiscountFixed);
                Assert.Equal(40, restoredCoupon.MaxUses);
                Assert.Equal(7, restoredCoupon.CurrentUses);
                Assert.Equal(licenseStartDate, restoredCoupon.ValidFrom);
                Assert.Equal(licenseExpirationDate, restoredCoupon.ValidUntil);
                Assert.True(restoredCoupon.IsActive);
                Assert.True(restoredCoupon.FirstPurchaseOnly);
                Assert.Equal(6, restoredCoupon.MinPlanMonths);
                Assert.Equal(20m, restoredCoupon.MinOrderAmount);
                var restoredGateway = await context.PaymentGatewayConfigs.SingleAsync(value => value.GatewayName == "backup-gateway-fixture", cancellationToken);
                Assert.Equal("Backup Gateway Fixture", restoredGateway.DisplayName);
                Assert.True(restoredGateway.IsEnabled);
                Assert.False(restoredGateway.IsPrimary);
                Assert.Equal("synthetic-access-token", restoredGateway.AccessToken);
                Assert.Equal("synthetic-public-key", restoredGateway.PublicKey);
                Assert.Equal("synthetic-webhook-secret", restoredGateway.WebhookSecret);
                Assert.True(restoredGateway.SandboxMode);
                Assert.True(restoredGateway.EnablePix);
                Assert.True(restoredGateway.EnableCredit);
                Assert.False(restoredGateway.EnableDebit);
                Assert.Equal("{\"fixture\":true}", restoredGateway.ExtraConfig);
                var restoredTransactionWithCoupon = await context.PaymentTransactions
                    .SingleAsync(value => value.Id == paymentTransactionWithCouponId, cancellationToken);
                Assert.Equal(backupUserId, restoredTransactionWithCoupon.UserId);
                Assert.Equal(backupPricingPlanId, restoredTransactionWithCoupon.PricingPlanId);
                Assert.Equal("backup-gateway-fixture", restoredTransactionWithCoupon.GatewayName);
                Assert.Equal("backup-transaction-with-coupon", restoredTransactionWithCoupon.GatewayTransactionId);
                Assert.Equal(PaymentMethod.Pix, restoredTransactionWithCoupon.PaymentMethod);
                Assert.Equal(209.25m, restoredTransactionWithCoupon.Amount);
                Assert.Equal(3.25m, restoredTransactionWithCoupon.DiscountAmount);
                Assert.Equal(backupDiscountCouponId, restoredTransactionWithCoupon.CouponId);
                Assert.Equal(PaymentStatus.Approved, restoredTransactionWithCoupon.Status);
                Assert.True(restoredTransactionWithCoupon.IsRecurring);
                Assert.Equal("backup-recurring-fixture", restoredTransactionWithCoupon.RecurringSubscriptionId);
                Assert.Equal("backup-customer@example.test", restoredTransactionWithCoupon.CustomerEmail);
                Assert.Equal("Backup Customer Fixture", restoredTransactionWithCoupon.CustomerName);
                Assert.Equal("+5500000000000", restoredTransactionWithCoupon.CustomerPhone);
                Assert.Equal("192.0.2.10", restoredTransactionWithCoupon.IpAddress);
                Assert.Equal("BackupRestoreMariaDbTests", restoredTransactionWithCoupon.UserAgent);
                Assert.Equal(licenseStartDate, restoredTransactionWithCoupon.CreatedAt);
                Assert.Equal(licenseStartDate.AddHours(1), restoredTransactionWithCoupon.PaidAt);
                Assert.Equal(licenseExpirationDate, restoredTransactionWithCoupon.ExpiresAt);
                Assert.Equal("{\"fixture\":\"approved\"}", restoredTransactionWithCoupon.GatewayResponse);
                var restoredTransactionWithoutCoupon = await context.PaymentTransactions
                    .SingleAsync(value => value.Id == paymentTransactionWithoutCouponId, cancellationToken);
                Assert.Equal(backupUserId, restoredTransactionWithoutCoupon.UserId);
                Assert.Equal(backupPricingPlanId, restoredTransactionWithoutCoupon.PricingPlanId);
                Assert.Null(restoredTransactionWithoutCoupon.CouponId);
                Assert.Equal(PaymentMethod.Credit, restoredTransactionWithoutCoupon.PaymentMethod);
                Assert.Equal(PaymentStatus.Pending, restoredTransactionWithoutCoupon.Status);
                Assert.Equal("backup-device-name", (await context.DeviceOptions.SingleAsync(value => value.DeviceId == backupDeviceId, cancellationToken)).CustomName);
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

            if (includeMongoRestore)
            {
                Assert.Equal("before-backup", await File.ReadAllTextAsync(configFile, cancellationToken));
                await RestoreNebulaCatalogFromIsolatedSupabaseAsync(mongoConnection!, cancellationToken);
                return;
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

    private static string GetIsolatedMongoConnection()
    {
        const string variableName = "MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING";
        var candidate = Environment.GetEnvironmentVariable(variableName);
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "mongodb", StringComparison.Ordinal)
            || !string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
            || uri.Port != 27099
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Authority.Contains(',', StringComparison.Ordinal))
        {
            Assert.Skip($"Set {variableName} to the isolated MongoDB test instance at 127.0.0.1:27099.");
        }

        return "mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=1500&directConnection=true";
    }

    private static async Task RestoreNebulaCatalogFromIsolatedSupabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        var databaseName = "mfx_full_recovery_" + Guid.NewGuid().ToString("N");
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        var client = new MongoClient(settings);
        try
        {
            using var mongoContext = new NebulaMongoContext(connectionString, databaseName, NullLogger<NebulaMongoContext>.Instance);
            var usersOptions = new DbContextOptionsBuilder<UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var usersFactory = new Mock<IDbContextFactory<UsersDbContext>>();
            usersFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new UsersDbContext(usersOptions, NullLogger<UsersDbContext>.Instance));
            using var syncService = new NebulaSupabaseSyncService(
                mongoContext,
                NullLogger<NebulaSupabaseSyncService>.Instance,
                usersFactory.Object,
                new FullRecoverySupabaseHandler());

            var restore = await syncService.PerformRestoreAsync(
                "https://supabase.invalid",
                "sb_secret_test",
                progressAction: null,
                forceFullRestore: true,
                cancellationToken);

            Assert.True(restore.Success, restore.Message);
            Assert.Equal(1, restore.FilesRestored);
            var files = await mongoContext.GetAllFilesForSyncAsync(cancellationToken);
            var restored = Assert.Single(files);
            Assert.Equal("full-recovery-fixture", restored["_id"].AsString);
            Assert.Equal("Recovery fixture.mkv", restored["name"].AsString);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
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

    private sealed class FullRecoverySupabaseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var table = request.RequestUri!.AbsolutePath.Split('/').Last();
            var body = string.Equals(table, "nebula_files", StringComparison.Ordinal)
                && !request.RequestUri.Query.Contains("id=gt.", StringComparison.Ordinal)
                    ? "[{\"id\":\"full-recovery-fixture\",\"name\":\"Recovery fixture.mkv\",\"parent\":null,\"size\":12345,\"status\":\"completed\",\"parts\":[],\"uploaded_at\":1700000000,\"doc_data\":{\"_id\":\"full-recovery-fixture\",\"name\":\"Recovery fixture.mkv\",\"status\":\"completed\"}}]"
                    : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
