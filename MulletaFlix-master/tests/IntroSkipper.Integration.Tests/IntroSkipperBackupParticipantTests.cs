using System.IO.Compression;
using System.Reflection;
using IntroSkipper.Data;
using IntroSkipper.Db;
using IntroSkipper.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class IntroSkipperBackupParticipantTests
{
    [Fact]
    public void PluginServiceRegistrator_InjectsBackupParticipantIntoBackupService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseConfiguration = new DatabaseConfigurationOptions
        {
            DatabaseType = "MulletaFlix-MySQL",
            CustomProviderOptions = new CustomDatabaseOptions
            {
                PluginName = string.Empty,
                PluginAssembly = string.Empty,
                ConnectionString = string.Empty
            }
        };
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("database"))
            .Returns(databaseConfiguration);
        services.AddSingleton(configurationManager.Object);
        services.AddSingleton(Mock.Of<IMulletaFlixDatabaseProvider>());
        new PluginServiceRegistrator().RegisterServices(services, Mock.Of<IServerApplicationHost>());
        services.AddSingleton(Mock.Of<IDbContextFactory<MulletaFlixDbContext>>());
        services.AddSingleton(Mock.Of<IDbContextFactory<MoviesDbContext>>());
        services.AddSingleton(Mock.Of<IDbContextFactory<SeriesDbContext>>());
        services.AddSingleton(Mock.Of<IDbContextFactory<ChannelsDbContext>>());
        services.AddSingleton(Mock.Of<IDbContextFactory<BooksDbContext>>());
        services.AddSingleton(Mock.Of<IServerApplicationPaths>());
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        services.AddSingleton<IBackupService>(serviceProvider => new BackupService(
            serviceProvider.GetRequiredService<ILogger<BackupService>>(),
            serviceProvider.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>(),
            Mock.Of<IServerApplicationHost>(),
            serviceProvider.GetRequiredService<IServerApplicationPaths>(),
            serviceProvider.GetRequiredService<IMulletaFlixDatabaseProvider>(),
            serviceProvider.GetRequiredService<IHostApplicationLifetime>(),
            null,
            null,
            null,
            null,
            serviceProvider.GetServices<IBackupDatabaseParticipant>()));

        using var provider = services.BuildServiceProvider();
        var backupService = Assert.IsType<BackupService>(provider.GetRequiredService<IBackupService>());
        var participants = typeof(BackupService)
            .GetField("_backupDatabaseParticipants", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(backupService);

        Assert.Collection(
            Assert.IsAssignableFrom<IEnumerable<IBackupDatabaseParticipant>>(participants),
            participant => Assert.IsType<IntroSkipperBackupParticipant>(participant));
    }

    [Fact]
    public async Task RestoreBackupAsync_ReplacesDurableRowsAndClearsCache()
    {
        await using var connection = OpenConnection();
        var (participant, contexts, cacheContexts) = CreateParticipant(connection);
        var backup = new MemoryStream();
        await using (var archive = new ZipArchive(backup, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var db = await contexts.CreateDbContextAsync();
            await IntroSkipperSchema.EnsureAsync(db, CancellationToken.None);
            db.Segments.Add(CreateSegment(Guid.NewGuid(), 0));
            db.SeasonStates.Add(new DbSeasonState(Guid.NewGuid(), AnalysisMode.Credits, AnalyzerAction.None, [Guid.NewGuid()]));
            db.SeasonAnalysisOverrides.Add(new DbSeasonAnalysisOverride { SeasonId = Guid.NewGuid(), AnalysisPercent = 75 });
            db.AnalyzedItems.Add(new DbAnalyzedItem(Guid.NewGuid(), AnalysisMode.Introduction, "config"));
            db.ImportHistory.Add(new DbImportRecord { Id = 41, ImportedAt = DateTime.UtcNow, SourceFileFound = true, Notes = "backup" });
            db.DisabledItems.Add(new DbDisabledItem(Guid.NewGuid()));
            db.ProjectionQueue.Add(new DbProjectionQueueItem { ItemId = Guid.NewGuid(), Version = 2, AttemptCount = 1 });
            db.ProjectionExternalOperations.Add(new DbProjectionExternalOperation
            {
                Id = 42,
                ItemId = Guid.NewGuid(),
                ExternalSegmentId = Guid.NewGuid(),
                ExpectedType = MediaSegmentType.Intro,
                StartTicks = 1,
                EndTicks = 2
            });
            await db.SaveChangesAsync();
            await participant.WriteBackupAsync(archive, "Database/Participants/IntroSkipper", CancellationToken.None);
        }

        backup.Position = 0;
        await using (var db = await contexts.CreateDbContextAsync())
        await using (var cache = await cacheContexts.CreateDbContextAsync())
        {
            await IntroSkipperSchema.EnsureAsync(cache, CancellationToken.None);
            db.Segments.Add(CreateSegment(Guid.NewGuid(), 10));
            cache.DetectionCache.Add(new DbDetectionCache(Guid.NewGuid(), AnalysisMode.Introduction, CacheEntryType.Silence, [1, 2, 3]));
            await db.SaveChangesAsync();
            await cache.SaveChangesAsync();
        }

        await using (var archive = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true))
        {
            await participant.ValidateBackupAsync(archive, "Database/Participants/IntroSkipper", CancellationToken.None);
            await participant.RestoreBackupAsync(archive, "Database/Participants/IntroSkipper", CancellationToken.None);
            await participant.RestoreBackupAsync(archive, "Database/Participants/IntroSkipper", CancellationToken.None);
        }

        await using var restored = await contexts.CreateDbContextAsync();
        await using var restoredCache = await cacheContexts.CreateDbContextAsync();
        var segments = await restored.Segments.AsNoTracking().ToListAsync();
        Assert.Single(segments);
        Assert.Equal(0, segments[0].StartTicks);
        Assert.Single(await restored.SeasonStates.ToListAsync());
        Assert.Single(await restored.SeasonAnalysisOverrides.ToListAsync());
        Assert.Single(await restored.AnalyzedItems.ToListAsync());
        Assert.Equal(41, Assert.Single(await restored.ImportHistory.ToListAsync()).Id);
        Assert.Single(await restored.DisabledItems.ToListAsync());
        Assert.Single(await restored.ProjectionQueue.ToListAsync());
        Assert.Equal(42, Assert.Single(await restored.ProjectionExternalOperations.ToListAsync()).Id);
        Assert.Empty(await restoredCache.DetectionCache.ToListAsync());
    }

    [Fact]
    public async Task RestoreBackupAsync_RollsBackEarlierTableWritesWhenLaterTableInsertFails()
    {
        await using var connection = OpenConnection();
        var (participant, contexts, _) = CreateParticipant(connection);
        var backupSegment = CreateSegment(Guid.NewGuid(), 0);
        var backupSeasonId = Guid.NewGuid();
        using var backup = new MemoryStream();

        await using (var db = await contexts.CreateDbContextAsync())
        {
            await IntroSkipperSchema.EnsureAsync(db, CancellationToken.None);
            db.Segments.Add(backupSegment);
            db.SeasonStates.Add(new DbSeasonState(backupSeasonId, AnalysisMode.Credits, AnalyzerAction.None, []));
            await db.SaveChangesAsync();
            await using var archive = new ZipArchive(backup, ZipArchiveMode.Create, leaveOpen: true);
            await participant.WriteBackupAsync(archive, "Database/Participants/IntroSkipper", CancellationToken.None);
        }

        var currentSegment = CreateSegment(Guid.NewGuid(), 500);
        var currentSeasonId = Guid.NewGuid();
        await using (var db = await contexts.CreateDbContextAsync())
        {
            await db.Segments.ExecuteDeleteAsync();
            await db.SeasonStates.ExecuteDeleteAsync();
            db.Segments.Add(currentSegment);
            db.SeasonStates.Add(new DbSeasonState(currentSeasonId, AnalysisMode.Credits, AnalyzerAction.None, []));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_restore_season_state BEFORE INSERT ON SeasonStates BEGIN SELECT RAISE(ABORT, 'Injected restore failure'); END;");
        }

        backup.Position = 0;
        await using (var archive = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => participant.RestoreBackupAsync(
                archive,
                "Database/Participants/IntroSkipper",
                CancellationToken.None));
        }

        await using var restored = await contexts.CreateDbContextAsync();
        Assert.Equal(currentSegment.Id, Assert.Single(await restored.Segments.AsNoTracking().ToListAsync()).Id);
        Assert.Equal(currentSeasonId, Assert.Single(await restored.SeasonStates.AsNoTracking().ToListAsync()).SeasonId);
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsInvalidSnapshotBeforeDeletingCurrentRows()
    {
        await using var connection = OpenConnection();
        var (participant, contexts, _) = CreateParticipant(connection);
        var existing = CreateSegment(Guid.NewGuid(), 10);
        await using (var db = await contexts.CreateDbContextAsync())
        {
            await IntroSkipperSchema.EnsureAsync(db, CancellationToken.None);
            db.Segments.Add(existing);
            await db.SaveChangesAsync();
        }

        using var backup = new MemoryStream();
        await using (var archive = new ZipArchive(backup, ZipArchiveMode.Create, leaveOpen: true))
        await using (var writer = new StreamWriter(archive.CreateEntry("Database/Participants/IntroSkipper/data.json").Open()))
        {
            await writer.WriteAsync("""{"Segments":[{"Id":"11111111-1111-1111-1111-111111111111","ItemId":"22222222-2222-2222-2222-222222222222","StartTicks":10,"EndTicks":5,"ConfigHash":""}]}""");
        }

        backup.Position = 0;
        await using (var archive = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true))
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => participant.RestoreBackupAsync(archive, "Database/Participants/IntroSkipper", CancellationToken.None));
        }

        await using var verify = await contexts.CreateDbContextAsync();
        Assert.Equal(existing.Id, Assert.Single(await verify.Segments.Select(row => row.Id).ToListAsync()));
    }

    [Fact]
    public async Task EnsureAsync_CreatesMissingTablesInPartiallyCreatedSchemaWithoutDroppingExistingTable()
    {
        await using var connection = OpenConnection();
        var options = new DbContextOptionsBuilder<IntroSkipperDbContext>().UseSqlite(connection).Options;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE Segments (Id TEXT NOT NULL PRIMARY KEY); INSERT INTO Segments (Id) VALUES ('preserve-me');";
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = new IntroSkipperDbContext(options))
        {
            await IntroSkipperSchema.EnsureAsync(db, CancellationToken.None);
            var tables = db.Model.GetRelationalModel().Tables.Select(table => table.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Equal(8, tables.Count);
            foreach (var table in tables)
            {
                await using var check = connection.CreateCommand();
                check.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
                Assert.Equal(1L, await check.ExecuteScalarAsync());
            }
        }

        await using var preserved = connection.CreateCommand();
        preserved.CommandText = "SELECT Id FROM Segments";
        Assert.Equal("preserve-me", await preserved.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ValidateAsync_RejectsExistingTableWithMissingMappedColumns()
    {
        await using var connection = OpenConnection();
        var options = new DbContextOptionsBuilder<IntroSkipperDbContext>().UseSqlite(connection).Options;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE Segments (Id TEXT NOT NULL PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        await using var db = new IntroSkipperDbContext(options);
        await IntroSkipperSchema.EnsureAsync(db, CancellationToken.None);

        await Assert.ThrowsAsync<SqliteException>(() => IntroSkipperSchema.ValidateAsync(db, CancellationToken.None));
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static (IntroSkipperBackupParticipant Participant, IDbContextFactory<IntroSkipperDbContext> Contexts, IDbContextFactory<DetectionCacheDbContext> CacheContexts)
        CreateParticipant(SqliteConnection connection)
    {
        var dbOptions = new DbContextOptionsBuilder<IntroSkipperDbContext>().UseSqlite(connection).Options;
        var cacheOptions = new DbContextOptionsBuilder<DetectionCacheDbContext>().UseSqlite(connection).Options;
        var contexts = new TestContextFactory<IntroSkipperDbContext>(() => new IntroSkipperDbContext(dbOptions));
        var cacheContexts = new TestContextFactory<DetectionCacheDbContext>(() => new DetectionCacheDbContext(cacheOptions));
        return (new IntroSkipperBackupParticipant(contexts, cacheContexts), contexts, cacheContexts);
    }

    private static DbSegment CreateSegment(Guid itemId, long startTicks)
        => new(itemId, AnalysisMode.Introduction, startTicks, startTicks + 100, SegmentSource.User);

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
