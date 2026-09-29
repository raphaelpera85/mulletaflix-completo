// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Net;
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
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Extensions;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MySqlConnector;
using Xunit;

namespace IntroSkipper.Integration.Tests;

/// <summary>Exercises full backup restore against an explicitly configured disposable MariaDB.</summary>
[Collection(ServerPersistenceCollection.Name)]
public sealed class BackupRestoreMariaDbTests
{
    private const string ConnectionStringVariable = "MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING";

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
            var backupService = new BackupService(
                services.GetRequiredService<ILogger<BackupService>>(),
                contextFactory,
                appHost.Object,
                paths,
                services.GetRequiredService<IMulletaFlixDatabaseProvider>(),
                Mock.Of<IHostApplicationLifetime>());

            var createdBackup = await backupService.CreateBackupAsync(new BackupOptionsDto
            {
                Database = true,
                Metadata = false,
                Subtitles = false,
                Trickplay = false
            });

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.ActivityLogs.ExecuteDeleteAsync(cancellationToken);
                Assert.Empty(await context.ActivityLogs.ToListAsync(cancellationToken));
            }

            await backupService.RestoreBackupAsync(createdBackup.Path);

            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var restored = await context.ActivityLogs.SingleAsync(entry => entry.Name == "restore-fixture", cancellationToken);
                Assert.Equal("integration-test", restored.Type);
            }
        }
        finally
        {
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

    private static MySqlConnectionStringBuilder CreateDisposableConnection(string connectionString)
    {
        var adminConnection = new MySqlConnectionStringBuilder(connectionString)
        {
            Database = string.Empty,
            ConnectionTimeout = 5
        };
        var isLoopback = string.Equals(adminConnection.Server, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(adminConnection.Server, out var address) && IPAddress.IsLoopback(address));
        if (!isLoopback || adminConnection.Port == 3306)
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} must target a loopback MariaDB on a non-default port; refusing database creation on '{adminConnection.Server}:{adminConnection.Port}'.");
        }

        return adminConnection;
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
}
