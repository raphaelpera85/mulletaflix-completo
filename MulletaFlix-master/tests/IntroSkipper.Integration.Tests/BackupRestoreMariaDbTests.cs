// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.IO.Compression;
using System.Text.Json.Nodes;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.System;
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
using MulletaFlix.Server.Implementations.Billing;
using MulletaFlix.Server.Implementations.Extensions;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Server.Implementations.StorageHelpers;
using MySqlConnector;
using Xunit;
using MulletaFlix.Testing;

namespace IntroSkipper.Integration.Tests;

/// <summary>Exercises full backup restore against an explicitly configured disposable MariaDB.</summary>
[Collection(ServerPersistenceCollection.Name)]
public sealed class BackupRestoreMariaDbTests
{
    private const string ConnectionStringVariable = MariaDbTestEnvironment.ConnectionStringVariable;

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
        var previousDatabaseName = Environment.GetEnvironmentVariable("MulletaFlix_DATABASE_NAME");
        Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", schema);
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-restore-mariadb-" + Guid.NewGuid().ToString("N"));
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
        try
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
                        new CustomDatabaseOption { Key = "server", Value = adminConnection.Server },
                        new CustomDatabaseOption { Key = "port", Value = adminConnection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                        new CustomDatabaseOption { Key = "user", Value = adminConnection.UserID },
                        new CustomDatabaseOption { Key = "password", Value = adminConnection.Password },
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
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.MigrateAsync(cancellationToken);
                context.ActivityLogs.Add(new ActivityLog("restore-fixture", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(cancellationToken);
            }

            await EnsureDomainTablesAsync<MoviesDbContext>(services, cancellationToken);
            await EnsureDomainTablesAsync<SeriesDbContext>(services, cancellationToken);
            await EnsureDomainTablesAsync<ChannelsDbContext>(services, cancellationToken);
            await EnsureDomainTablesAsync<BooksDbContext>(services, cancellationToken);
            await using (var context = await services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Movies.Add(new Movie { BaseItemId = Guid.NewGuid(), Name = "snapshot-movie" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Series.Add(new Series { BaseItemId = Guid.NewGuid(), Name = "snapshot-series" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Channels.Add(new Channel { BaseItemId = Guid.NewGuid(), Name = "snapshot-channel" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Books.Add(new Book { BaseItemId = Guid.NewGuid(), Name = "snapshot-book" });
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
            var storageSpaceProvider = new Mock<IStorageSpaceProvider>();
            storageSpaceProvider.Setup(provider => provider.GetFreeSpaceOf(backupPath)).Returns(new FolderStorageInfo
            {
                Path = backupPath,
                ResolvedPath = backupPath,
                FreeSpace = 10_737_418_240,
                UsedSpace = 1_073_741_824,
                StorageType = "Fixed",
                DeviceId = "integration-test"
            });
            var concurrentMutationCount = 0;
            var backupLogger = new CallbackLogger<BackupService>(message =>
            {
                if (!message.Contains("Backup of entity HistoryRow with", StringComparison.Ordinal))
                {
                    return;
                }

                using (var context = services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContext())
                {
                    concurrentMutationCount += context.Movies.ExecuteUpdate(setters => setters.SetProperty(movie => movie.Name, "post-backup-movie"));
                }

                using (var context = services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContext())
                {
                    concurrentMutationCount += context.Series.ExecuteUpdate(setters => setters.SetProperty(series => series.Name, "post-backup-series"));
                }

                using (var context = services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContext())
                {
                    concurrentMutationCount += context.Channels.ExecuteUpdate(setters => setters.SetProperty(channel => channel.Name, "post-backup-channel"));
                }

                using (var context = services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContext())
                {
                    concurrentMutationCount += context.Books.ExecuteUpdate(setters => setters.SetProperty(book => book.Name, "post-backup-book"));
                }
            });
            var backupService = ActivatorUtilities.CreateInstance<BackupService>(
                services,
                appHost.Object,
                paths,
                Mock.Of<IHostApplicationLifetime>(),
                storageSpaceProvider.Object,
                backupLogger);

            var createdBackup = await backupService.CreateBackupAsync(new BackupOptionsDto
            {
                Database = true,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });
            Assert.Equal(4, concurrentMutationCount);

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.ActivityLogs.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.ActivityLogs.ToListAsync(cancellationToken));
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                await context.Movies.ExecuteDeleteAsync(cancellationToken);
                context.Movies.Add(new Movie { BaseItemId = Guid.NewGuid(), Name = "post-backup-movie" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                await context.Series.ExecuteDeleteAsync(cancellationToken);
                context.Series.Add(new Series { BaseItemId = Guid.NewGuid(), Name = "post-backup-series" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                await context.Channels.ExecuteDeleteAsync(cancellationToken);
                context.Channels.Add(new Channel { BaseItemId = Guid.NewGuid(), Name = "post-backup-channel" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                await context.Books.ExecuteDeleteAsync(cancellationToken);
                context.Books.Add(new Book { BaseItemId = Guid.NewGuid(), Name = "post-backup-book" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await backupService.RestoreBackupAsync(createdBackup.Path);

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var restored = await context.ActivityLogs.SingleAsync(entry => entry.Name == "restore-fixture", cancellationToken);
                Assert.Equal("integration-test", restored.Type);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("snapshot-movie", (await context.Movies.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("snapshot-series", (await context.Series.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("snapshot-channel", (await context.Channels.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("snapshot-book", (await context.Books.SingleAsync(cancellationToken)).Name);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", previousDatabaseName);
            if (services is not null)
            {
                await services.DisposeAsync();
            }

            await using (var connection = new MySqlConnection(adminConnection.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{schema}`;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

        }
    }

    [Theory]
    [InlineData("ActivityLogs")]
    [InlineData("Series")]
    [InlineData("Books")]
    public async Task RestoreBackupAsync_RollsBackAllRelationalContextsAndPreservesFilesWhenDatabaseWriteFails(string failureTable)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuredConnection = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            Assert.Skip($"Set {ConnectionStringVariable} to an isolated, disposable MariaDB instance to run this integration test.");
        }

        var adminConnection = CreateDisposableConnection(configuredConnection);
        var schema = "mfx_restore_rb_" + Guid.NewGuid().ToString("N");
        var previousDatabaseName = Environment.GetEnvironmentVariable("MulletaFlix_DATABASE_NAME");
        Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", schema);
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-restore-rollback-test-" + Guid.NewGuid().ToString("N"));
        var configurationPath = Path.Combine(root, "config");
        var dataPath = Path.Combine(root, "data");
        var cachePath = Path.Combine(root, "cache");
        var programDataPath = Path.Combine(root, "program-data");
        var appRootPath = Path.Combine(root, "root");
        var metadataPath = Path.Combine(root, "metadata");
        var backupPath = Path.Combine(root, "backups");
        foreach (var path in new[] { configurationPath, dataPath, cachePath, programDataPath, appRootPath, metadataPath })
        {
            Directory.CreateDirectory(path);
        }

        ServiceProvider? services = null;
        try
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
                        new CustomDatabaseOption { Key = "server", Value = adminConnection.Server },
                        new CustomDatabaseOption { Key = "port", Value = adminConnection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                        new CustomDatabaseOption { Key = "user", Value = adminConnection.UserID },
                        new CustomDatabaseOption { Key = "password", Value = adminConnection.Password },
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
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.MigrateAsync(cancellationToken);
                context.ActivityLogs.Add(new ActivityLog("preserve-existing", "integration-test", Guid.NewGuid()));
                await context.SaveChangesAsync(cancellationToken);
            }

            await EnsureDomainTablesAsync<MoviesDbContext>(services, cancellationToken);
            await EnsureDomainTablesAsync<SeriesDbContext>(services, cancellationToken);
            await EnsureDomainTablesAsync<ChannelsDbContext>(services, cancellationToken);
            await EnsureDomainTablesAsync<BooksDbContext>(services, cancellationToken);

            await using (var context = await services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Movies.Add(new Movie { BaseItemId = Guid.NewGuid(), Name = "preserve-movie" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Series.Add(new Series { BaseItemId = Guid.NewGuid(), Name = "preserve-series" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Channels.Add(new Channel { BaseItemId = Guid.NewGuid(), Name = "preserve-channel" });
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                context.Books.Add(new Book { BaseItemId = Guid.NewGuid(), Name = "preserve-book" });
                await context.SaveChangesAsync(cancellationToken);
            }

            var paths = CreatePaths(configurationPath, dataPath, cachePath, programDataPath, appRootPath, metadataPath, backupPath);
            var appHost = new Mock<IServerApplicationHost>();
            appHost.SetupGet(host => host.ApplicationVersion).Returns(new Version(12, 1, 0));
            var storageSpaceProvider = new Mock<IStorageSpaceProvider>();
            storageSpaceProvider.Setup(provider => provider.GetFreeSpaceOf(backupPath)).Returns(new FolderStorageInfo
            {
                Path = backupPath,
                ResolvedPath = backupPath,
                FreeSpace = 10_737_418_240,
                UsedSpace = 1_073_741_824,
                StorageType = "Fixed",
                DeviceId = "integration-test"
            });
            var backupService = ActivatorUtilities.CreateInstance<BackupService>(
                services,
                appHost.Object,
                paths,
                Mock.Of<IHostApplicationLifetime>(),
                storageSpaceProvider.Object);

            var existingConfig = Path.Combine(configurationPath, "server.json");
            await File.WriteAllTextAsync(existingConfig, "before-backup", cancellationToken);
            var backup = await backupService.CreateBackupAsync(new BackupOptionsDto
            {
                Database = true,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });
            await RewriteBackupWithDatabaseFailureAsync(backup.Path, failureTable, cancellationToken);
            await File.WriteAllTextAsync(existingConfig, "must-remain-after-failed-restore", cancellationToken);

            var blockedConfigPath = Path.Combine(configurationPath, "blocked.txt");
            Directory.CreateDirectory(blockedConfigPath);
            await Assert.ThrowsAnyAsync<IOException>(() => backupService.RestoreBackupAsync(backup.Path));
            Assert.Equal("must-remain-after-failed-restore", await File.ReadAllTextAsync(existingConfig, cancellationToken));
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("preserve-existing", (await context.ActivityLogs.SingleAsync(cancellationToken)).Name);
            }

            Directory.Delete(blockedConfigPath);

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var triggerTable = failureTable switch
                {
                    "ActivityLogs" => "`ActivityLogs`",
                    "Series" => "`Series`",
                    "Books" => "`Books`",
                    _ => throw new ArgumentOutOfRangeException(nameof(failureTable))
                };
                await context.Database.ExecuteSqlRawAsync(
                    $"CREATE TRIGGER `fail_restore_fixture` BEFORE INSERT ON {triggerTable} FOR EACH ROW BEGIN IF NEW.`Name` = 'force-restore-error' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced restore regression'; END IF; END;",
                    cancellationToken);
            }

            await Assert.ThrowsAsync<DbUpdateException>(() => backupService.RestoreBackupAsync(backup.Path));

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var preserved = await context.ActivityLogs.SingleAsync(cancellationToken);
                Assert.Equal("preserve-existing", preserved.Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<MoviesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("preserve-movie", (await context.Movies.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<SeriesDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("preserve-series", (await context.Series.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<ChannelsDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("preserve-channel", (await context.Channels.SingleAsync(cancellationToken)).Name);
            }

            await using (var context = await services.GetRequiredService<IDbContextFactory<BooksDbContext>>().CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal("preserve-book", (await context.Books.SingleAsync(cancellationToken)).Name);
            }

            Assert.Equal("must-remain-after-failed-restore", await File.ReadAllTextAsync(existingConfig, cancellationToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", previousDatabaseName);
            if (services is not null)
            {
                await services.DisposeAsync();
            }

            await using (var connection = new MySqlConnection(adminConnection.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{schema}`;";
                await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static async Task EnsureDomainTablesAsync<TContext>(ServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using var context = await services.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContextAsync(cancellationToken);
        await DomainSchemaInitializer.EnsureDomainTablesAsync(context, cancellationToken);
    }

    private static MySqlConnectionStringBuilder CreateDisposableConnection(string connectionString)
        => MariaDbTestEnvironment.Validate(connectionString);

    private static async Task RewriteBackupWithDatabaseFailureAsync(string archivePath, string tableName, CancellationToken cancellationToken)
    {
        var temporaryPath = archivePath + ".duplicate.tmp";
        using (var sourceArchive = ZipFile.OpenRead(archivePath))
        using (var destinationArchive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
        {
            foreach (var sourceEntry in sourceArchive.Entries)
            {
                var destinationEntry = destinationArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                if (string.Equals(sourceEntry.FullName, $"Database/{tableName}.json", StringComparison.Ordinal))
                {
                    await using var sourceStream = sourceEntry.Open();
                    using var reader = new StreamReader(sourceStream);
                    var json = await reader.ReadToEndAsync(cancellationToken);
                    var rows = JsonNode.Parse(json)?.AsArray() ?? throw new InvalidDataException("Activity log fixture is empty.");
                    if (rows.Count == 0)
                    {
                        throw new InvalidDataException("Activity log fixture is empty.");
                    }

                    var failingRow = rows[0]!.DeepClone().AsObject();
                    failingRow.Remove("Id");
                    failingRow["Name"] = "force-restore-error";
                    rows.Add(failingRow);
                    await using var destinationStream = destinationEntry.Open();
                    await using var writer = new StreamWriter(destinationStream);
                    await writer.WriteAsync(rows.ToJsonString().AsMemory(), cancellationToken);
                }
                else
                {
                    await using var sourceStream = sourceEntry.Open();
                    await using var destinationStream = destinationEntry.Open();
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                }
            }

            var blockedEntry = destinationArchive.CreateEntry("Config/blocked.txt", CompressionLevel.Optimal);
            await using (var blockedStream = blockedEntry.Open())
            await using (var writer = new StreamWriter(blockedStream))
            {
                await writer.WriteAsync("forces-file-restore-failure".AsMemory(), cancellationToken);
            }
        }

        File.Move(temporaryPath, archivePath, overwrite: true);
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

    private sealed class CallbackLogger<T>(Action<string> onInformation) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                onInformation(formatter(state, exception));
            }
        }
    }
}
