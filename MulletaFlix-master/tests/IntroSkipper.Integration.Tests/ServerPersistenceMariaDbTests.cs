// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MediaBrowser.Providers.Manager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Extensions;
using MulletaFlix.Server.Implementations.Item;
using MulletaFlix.Server.Implementations.Users;
using MulletaFlix.Server.Migrations.Routines;
using MulletaFlix.Server.ServerSetupApp;
using MulletaFlix.Testing;
using Xunit;

namespace IntroSkipper.Integration.Tests;

/// <summary>
/// Serializes the server persistence tests: they own one throwaway schema.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ServerPersistenceCollection
{
    /// <summary>Collection name.</summary>
    public const string Name = "MariaDB server persistence";
}

/// <summary>
/// Proves the server's full item save path against a real MariaDB.
/// </summary>
/// <remarks>
/// This closes a gap that could not be closed by unit tests: <c>SaveBaseItemEntities</c> runs
/// <c>ExecuteDelete</c> for the child rows of an item being rewritten (providers, images, metadata
/// fields, trailer types), and the EF InMemory provider does not support <c>ExecuteDelete</c>, so the
/// unit suite can only reach the parts of the path that do not touch it. The production failure this
/// covers is
/// "The instance of entity type 'BaseItemEntity' cannot be tracked because another instance with the
/// same key value for {'Id'} is already being tracked", thrown from <c>AddRange</c> while re-saving an
/// item that already existed — which is what silently aborted metadata refreshes during a library scan.
/// <para>
/// The schema is uniquely named per test instance and dropped after the run, so a stale or
/// pre-existing schema cannot be deleted or make a broken path look healthy.
/// </para>
/// </remarks>
[Collection(ServerPersistenceCollection.Name)]
public sealed class ServerPersistenceMariaDbTests : IAsyncLifetime
{
    /// <summary>Unique throwaway schema, never an existing or server database.</summary>
    private readonly string _testDatabase = "mfx_it_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private readonly string _testUser = "mfx_it_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private readonly string _testPassword = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private ServiceProvider? _services;

    private IDbContextFactory<MulletaFlixDbContext> Contexts
        => _services!.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>();

    private IDbContextFactory<UsersDbContext> UserContexts
        => _services!.GetRequiredService<IDbContextFactory<UsersDbContext>>();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (!MariaDbTestEnvironment.IsConfigured)
        {
            return;
        }

        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        var applicationConnection = CreateApplicationConnection(adminConnection, _testDatabase, _testUser, _testPassword);
        _services = BuildServices(applicationConnection, _testDatabase);

        // BaseItem.LibraryManager is a static, and the persistence path dereferences it through
        // GetAncestorIds() -> LibraryManager.GetCollectionFolders(this). Items built by hand in a test
        // process do not have it, which is what made the first version of this test throw a
        // NullReferenceException before touching the database at all.
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetCollectionFolders(It.IsAny<BaseItem>()))
            .Returns([]);
        BaseItem.LibraryManager = libraryManager.Object;

        // ConfigurationManager is the second static the save path reaches, through
        // BaseItemMapper.Map -> BaseItem.SortName -> CreateSortName(), which reads
        // ServerConfiguration.SortRemoveWords. With it null the mapper threw inside the save, so the
        // test never got as far as the database.
        BaseItem.ConfigurationManager = _services.GetRequiredService<IServerConfigurationManager>();

        // Third static on the same path, and only reached by episodes: UpdateOrInsertItemsCore calls
        // GetUserDataKeys(), and Episode overrides it through Video, whose SourceType consults
        // IsActiveRecording() -> RecordingsManager.GetActiveRecordingInfo(Path). A mock that answers
        // "no active recording" is the truthful state for a library item in a test process.
        Video.RecordingsManager = Mock.Of<IRecordingsManager>();

        // The real migrations create the schema objects, exactly as server startup does.
        await using var context = await Contexts.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        // User tables are bootstrapped by UserManager rather than the main EF migrations.
        // Exercise that production initialization path before profile-image persistence tests.
        InitializeUserSchema();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _services?.Dispose();
        _services = null;
        if (MariaDbTestEnvironment.IsConfigured)
        {
            DropTestDatabase(_testDatabase, _testUser);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public void ApplicationAccount_CannotCreateAnotherSchema()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var applicationConnection = new MySqlConnector.MySqlConnectionStringBuilder(
            MariaDbTestEnvironment.GetAdminConnection().ConnectionString)
        {
            Database = _testDatabase,
            UserID = _testUser,
            Password = _testPassword,
        };

        using var connection = new MySqlConnector.MySqlConnection(applicationConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `mfx_forbidden_{Guid.NewGuid():N}`;";
        Assert.Throws<MySqlConnector.MySqlException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public async Task SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = "N:\\Series\\A Agência (2020)";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "A Agência", 2020, path)], CancellationToken.None);

        var initialItem = CreateSeries(itemId, "A Agência", 2020, path);
        initialItem.AddImage(new ItemImageInfo { Path = path + "\\poster-initial.jpg", Type = ImageType.Primary, DateModified = DateTime.UtcNow });
        initialItem.AddImage(new ItemImageInfo { Path = path + "\\backdrop-initial.jpg", Type = ImageType.Backdrop, DateModified = DateTime.UtcNow });
        await persistence.SaveImagesAsync(initialItem, TestContext.Current.CancellationToken);

        const int RefreshCount = 32;
        using var ready = new CountdownEvent(RefreshCount);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedImageSets = Enumerable.Range(0, RefreshCount)
            .Select(index => new HashSet<(ImageInfoImageType ImageType, string Path)>
            {
                (ImageInfoImageType.Primary, path + $"\\poster-{index}.jpg"),
                (ImageInfoImageType.Backdrop, path + $"\\backdrop-{index}.jpg"),
            })
            .ToArray();
        var refreshes = expectedImageSets.Select((_, index) => Task.Run(async () =>
        {
            ready.Signal();
            await started.Task;
            var item = CreateSeries(itemId, "A Agência", 2020, path);
            foreach (var image in expectedImageSets[index])
            {
                item.AddImage(new ItemImageInfo
                {
                    Path = image.Path,
                    Type = (ImageType)image.ImageType,
                    DateModified = DateTime.UtcNow,
                });
            }

            await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);
        })).ToArray();

        var allRefreshesReachedBarrier = ready.Wait(TimeSpan.FromSeconds(10));
        started.SetResult();
        await Task.WhenAll(refreshes).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(allRefreshesReachedBarrier, "All image refreshes must reach the concurrency barrier.");

        await using var context = await Contexts.CreateDbContextAsync();
        var savedImages = await context.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => new { image.ImageType, image.Path })
            .ToArrayAsync();
        var actual = savedImages.Select(image => (image.ImageType, image.Path)).ToHashSet();
        Assert.Equal(2, savedImages.Length);
        Assert.Contains(expectedImageSets, expected => expected.SetEquals(actual));
    }

    [Fact]
    public async Task SaveImagesAsync_UsesCompositeItemImageIndexForSelectiveLookup()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Image index plan {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var item = CreateSeries(itemId, "Image index plan", 2026, path);
        persistence.SaveItems([item], CancellationToken.None);
        item.AddImage(new ItemImageInfo { Path = path + "\\poster.jpg", Type = ImageType.Primary });
        await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);

        const int UnrelatedImageCount = 2048;
        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0", TestContext.Current.CancellationToken);
            context.BaseItemImageInfos.AddRange(Enumerable.Range(0, UnrelatedImageCount).Select(_ => new BaseItemImageInfo
            {
                Id = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                Item = null!,
                Path = path + "\\unrelated.jpg",
                ImageType = ImageInfoImageType.Primary,
                Width = 1,
                Height = 1,
            }));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1", TestContext.Current.CancellationToken);
        }

        await using (var analyze = context.Database.GetDbConnection().CreateCommand())
        {
            analyze.CommandText = "ANALYZE TABLE `BaseItemImageInfos`";
            await analyze.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var explain = context.Database.GetDbConnection().CreateCommand();
        explain.CommandText = "EXPLAIN SELECT `Id` FROM `BaseItemImageInfos` WHERE `ItemId` = @itemId";
        var parameter = explain.CreateParameter();
        parameter.ParameterName = "@itemId";
        parameter.Value = itemId;
        explain.Parameters.Add(parameter);
        await using var reader = await explain.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        var keyOrdinal = reader.GetOrdinal("key");
        var rowsOrdinal = reader.GetOrdinal("rows");
        Assert.Equal("IX_BaseItemImageInfos_ItemId_ImageType", reader.GetString(keyOrdinal));
        Assert.InRange(long.Parse(reader.GetString(rowsOrdinal), CultureInfo.InvariantCulture), 0, 4);
    }

    [Fact]
    public async Task SaveImagesAsync_RemainsCorrectWhenCompositeImageIndexIsMissing()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Image index missing {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var item = CreateSeries(itemId, "Image index missing", 2026, path);
        persistence.SaveItems([item], CancellationToken.None);
        item.AddImage(new ItemImageInfo { Path = path + "\\poster.jpg", Type = ImageType.Primary });
        await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);

        const int UnrelatedImageCount = 2048;
        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0", TestContext.Current.CancellationToken);
            context.BaseItemImageInfos.AddRange(Enumerable.Range(0, UnrelatedImageCount).Select(_ => new BaseItemImageInfo
            {
                Id = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                Item = null!,
                Path = path + "\\unrelated.jpg",
                ImageType = ImageInfoImageType.Primary,
                Width = 1,
                Height = 1,
            }));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1", TestContext.Current.CancellationToken);
        }

        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `BaseItemImageInfos` DROP FOREIGN KEY `FK_BaseItemImageInfos_BaseItems_ItemId`",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `BaseItemImageInfos` DROP INDEX `IX_BaseItemImageInfos_ItemId_ImageType`",
            TestContext.Current.CancellationToken);

        var tableRowCount = await context.BaseItemImageInfos.CountAsync(TestContext.Current.CancellationToken);
        Assert.True(tableRowCount >= UnrelatedImageCount);

        await using (var analyze = context.Database.GetDbConnection().CreateCommand())
        {
            analyze.CommandText = "ANALYZE TABLE `BaseItemImageInfos`";
            await analyze.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (var explain = context.Database.GetDbConnection().CreateCommand())
        {
            explain.CommandText = "EXPLAIN SELECT `Id` FROM `BaseItemImageInfos` WHERE `ItemId` = @itemId";
            var parameter = explain.CreateParameter();
            parameter.ParameterName = "@itemId";
            parameter.Value = itemId;
            explain.Parameters.Add(parameter);
            await using var reader = await explain.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal("ALL", reader.GetString(reader.GetOrdinal("type")));
            Assert.True(reader.IsDBNull(reader.GetOrdinal("key")));
            Assert.True(long.Parse(reader.GetString(reader.GetOrdinal("rows")), CultureInfo.InvariantCulture) > 0);
        }

        item.AddImage(new ItemImageInfo { Path = path + "\\backdrop.jpg", Type = ImageType.Backdrop });
        await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);

        var savedImageCount = await context.BaseItemImageInfos
            .CountAsync(image => image.ItemId == itemId, TestContext.Current.CancellationToken);
        Assert.Equal(2, savedImageCount);
    }

    [Fact]
    public async Task LatestImageProvenanceMigration_PreservesExistingImageRows()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        const string targetMigration = "20261022212236_AddRemoteImageProvenance";
        var itemId = Guid.NewGuid();
        var path = $"N:\\Movies\\Migration preservation {itemId:N}";
        var imagePath = path + "\\poster.jpg";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Migration preservation", 2026, path)], CancellationToken.None);

        var item = CreateSeries(itemId, "Migration preservation", 2026, path);
        item.AddImage(new ItemImageInfo
        {
            Path = imagePath,
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
        });
        await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);

        await using (var migrationContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            var migrations = migrationContext.Database.GetMigrations().ToArray();
            Assert.Equal(targetMigration, migrations[^1]);
            await migrationContext.Database.MigrateAsync(migrations[^2], TestContext.Current.CancellationToken);
            await migrationContext.Database.MigrateAsync(targetMigration, TestContext.Current.CancellationToken);
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedImages = await verifyContext.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => new { image.Path, image.ProviderName, image.Language })
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var savedImage = Assert.Single(savedImages);
        Assert.Equal(imagePath, savedImage.Path);
        Assert.Null(savedImage.ProviderName);
        Assert.Null(savedImage.Language);
    }

    [Fact]
    public async Task SaveImagesAsync_RecoversWhenMariaDbConnectionPoolIsExhausted()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Pool recovery {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Pool recovery", 2026, path)], CancellationToken.None);

        string connectionString;
        await using (var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            connectionString = context.Database.GetConnectionString()!;
        }

        var connectionOptions = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);
        Assert.True(connectionOptions.Pooling);
        Assert.True(connectionOptions.MaximumPoolSize > 1);
        var poolSize = checked((int)connectionOptions.MaximumPoolSize);
        var blockers = Enumerable.Range(0, poolSize)
            .Select(_ => new MySqlConnector.MySqlConnection(connectionString))
            .ToArray();
        var item = CreateSeries(itemId, "Pool recovery", 2026, path);
        item.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
        });
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? saveTask = null;

        try
        {
            foreach (var blocker in blockers)
            {
                await blocker.OpenAsync(TestContext.Current.CancellationToken);
            }

            saveTask = Task.Run(async () =>
            {
                saveStarted.SetResult();
                await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);
            });
            await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            Assert.False(saveTask.IsCompleted, "Image persistence must wait while every connection in its pool is checked out.");

            await blockers[0].CloseAsync();
            await saveTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            foreach (var blocker in blockers)
            {
                await blocker.DisposeAsync();
            }

            if (saveTask is not null)
            {
                await saveTask;
            }
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedImages = await verifyContext.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => image.Path)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([path + "\\poster.jpg"], savedImages);
    }

    [Fact]
    public async Task SaveImagesAsync_RetriesAfterMariaDbConnectionIsKilledDuringDelete()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Connection recovery {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var initialItem = CreateSeries(itemId, "Connection recovery", 2026, path);
        initialItem.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster-old.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow.AddMinutes(-1),
        });
        persistence.SaveItems([initialItem], CancellationToken.None);
        await persistence.SaveImagesAsync(initialItem, TestContext.Current.CancellationToken);

        var triggerName = "mfx_it_img_retry_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        adminConnection.Database = _testDatabase;
        await using var admin = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createTrigger = admin.CreateCommand())
        {
            createTrigger.CommandText = $"CREATE TRIGGER `{triggerName}` BEFORE DELETE ON `BaseItemImageInfos` FOR EACH ROW DO SLEEP(10)";
            await createTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var updatedItem = CreateSeries(itemId, "Connection recovery", 2026, path);
        updatedItem.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster-new.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
        });
        var saveTask = persistence.SaveImagesAsync(updatedItem, TestContext.Current.CancellationToken);
        long? connectionId = null;

        try
        {
            for (var attempt = 0; attempt < 100 && connectionId is null; attempt++)
            {
                await using var findCommand = admin.CreateCommand();
                findCommand.CommandText = "SELECT ID FROM information_schema.PROCESSLIST WHERE DB = @database AND ID <> CONNECTION_ID() AND STATE = 'User sleep' LIMIT 1";
                findCommand.Parameters.AddWithValue("@database", _testDatabase);
                var result = await findCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken);
                if (result is not null and not DBNull)
                {
                    connectionId = Convert.ToInt64(result, CultureInfo.InvariantCulture);
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            }

            Assert.True(connectionId.HasValue, "The image replacement DELETE must reach the test trigger before the connection is interrupted.");
            await using (var killCommand = admin.CreateCommand())
            {
                killCommand.CommandText = $"KILL CONNECTION {connectionId.Value.ToString(CultureInfo.InvariantCulture)}";
                await killCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using (var dropTrigger = admin.CreateCommand())
            {
                dropTrigger.CommandText = $"DROP TRIGGER IF EXISTS `{triggerName}`";
                await dropTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await saveTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            await using (var dropTrigger = admin.CreateCommand())
            {
                dropTrigger.CommandText = $"DROP TRIGGER IF EXISTS `{triggerName}`";
                await dropTrigger.ExecuteNonQueryAsync(CancellationToken.None);
            }

            await saveTask;
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedImages = await verifyContext.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => image.Path)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([path + "\\poster-new.jpg"], savedImages);
    }

    [Fact]
    public async Task SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var restartReadyMarker = Environment.GetEnvironmentVariable("MULLETAFLIX_TEST_MARIADB_RESTART_READY_MARKER");
        var restartMarker = Environment.GetEnvironmentVariable("MULLETAFLIX_TEST_MARIADB_RESTART_MARKER");
        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(restartReadyMarker) && !string.IsNullOrWhiteSpace(restartMarker),
            "Run this case through the isolated MariaDB restart CI job.");

        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Process recovery {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var initialItem = CreateSeries(itemId, "Process recovery", 2026, path);
        initialItem.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster-old.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow.AddMinutes(-1),
        });
        persistence.SaveItems([initialItem], CancellationToken.None);
        await persistence.SaveImagesAsync(initialItem, TestContext.Current.CancellationToken);

        var triggerName = "mfx_it_img_restart_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        adminConnection.Database = _testDatabase;
        await using (var admin = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString))
        {
            await admin.OpenAsync(TestContext.Current.CancellationToken);
            await using var createTrigger = admin.CreateCommand();
            createTrigger.CommandText = $"CREATE TRIGGER `{triggerName}` BEFORE DELETE ON `BaseItemImageInfos` FOR EACH ROW DO SLEEP(10)";
            await createTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var updatedItem = CreateSeries(itemId, "Process recovery", 2026, path);
        updatedItem.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster-new.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
        });
        var saveTask = persistence.SaveImagesAsync(updatedItem, TestContext.Current.CancellationToken);

        try
        {
            var restartRequested = false;
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await using var admin = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
                await admin.OpenAsync(TestContext.Current.CancellationToken);
                await using var findCommand = admin.CreateCommand();
                findCommand.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND ID <> CONNECTION_ID() AND STATE = 'User sleep'";
                findCommand.Parameters.AddWithValue("@database", _testDatabase);
                restartRequested = Convert.ToInt32(
                    await findCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (restartRequested)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            }

            Assert.True(restartRequested, "The image replacement DELETE must reach the test trigger before the CI job restarts its MariaDB container.");
            File.WriteAllText(restartReadyMarker!, "SaveImagesAsync is blocked inside the database trigger.");
            await saveTask.WaitAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
            Assert.True(File.Exists(restartMarker), "The isolated CI job must confirm it restarted MariaDB while image persistence was active.");
        }
        finally
        {
            await using (var cleanup = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString))
            {
                await cleanup.OpenAsync(CancellationToken.None);
                await using var dropTrigger = cleanup.CreateCommand();
                dropTrigger.CommandText = $"DROP TRIGGER IF EXISTS `{triggerName}`";
                await dropTrigger.ExecuteNonQueryAsync(CancellationToken.None);
            }

            await saveTask;
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedImages = await verifyContext.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => image.Path)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([path + "\\poster-new.jpg"], savedImages);
    }

    [Fact]
    public async Task SaveItemsAndSaveImagesAsync_ConcurrentWritesForSameItem_PersistMetadataAndCompleteImageSet()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Concurrent image metadata {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Concurrent image metadata", 2026, path)], CancellationToken.None);

        var imageInfos = new[]
        {
            new ItemImageInfo { Path = path + "\\poster.jpg", Type = ImageType.Primary, ProviderName = "TheMovieDb", Language = "pt-BR" },
            new ItemImageInfo { Path = path + "\\backdrop.jpg", Type = ImageType.Backdrop, ProviderName = "Fanart", Language = "pt-BR" },
        };
        var metadataRefresh = CreateSeries(itemId, "Concurrent image metadata (updated)", 2026, path);
        var imageRefresh = CreateSeries(itemId, "Concurrent image metadata", 2026, path);
        foreach (var image in imageInfos)
        {
            metadataRefresh.AddImage(image);
            imageRefresh.AddImage(new ItemImageInfo
            {
                Path = image.Path,
                Type = image.Type,
                ProviderName = image.ProviderName,
                Language = image.Language,
            });
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadataTask = Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            persistence.SaveItems([metadataRefresh], TestContext.Current.CancellationToken);
        });
        var imagesTask = Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            await persistence.SaveImagesAsync(imageRefresh, TestContext.Current.CancellationToken);
        });
        start.SetResult();

        await Task.WhenAll(metadataTask, imagesTask)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedItem = await context.BaseItems.SingleAsync(item => item.Id == itemId, TestContext.Current.CancellationToken);
        var savedImages = await context.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => new { image.ImageType, image.Path, image.ProviderName, image.Language })
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Concurrent image metadata (updated)", savedItem.Name);
        Assert.Equal(2, savedImages.Length);
        Assert.Equal(
            imageInfos.Select(image => (image.Type, image.Path, image.ProviderName, image.Language)).ToHashSet(),
            savedImages.Select(image => ((ImageType)image.ImageType, image.Path, image.ProviderName, image.Language)).ToHashSet());
    }

    [Fact]
    public async Task SaveItems_ConcurrentWithDeleteItemForSameItem_CompletesWithoutOrphanedImages()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = $"N:\\Series\\Concurrent delete and image metadata {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Concurrent delete and image metadata", 2026, path)], CancellationToken.None);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saves = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            var item = CreateSeries(itemId, $"Concurrent metadata {index}", 2026, path);
            item.AddImage(new ItemImageInfo
            {
                Path = path + "\\poster.jpg",
                Type = ImageType.Primary,
                ProviderName = "TheMovieDb",
                Language = "pt-BR",
            });
            persistence.SaveItems([item], TestContext.Current.CancellationToken);
        }));
        var deletes = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            persistence.DeleteItem([itemId]);
        }));
        start.SetResult();

        await Task.WhenAll(saves.Concat(deletes))
            .WaitAsync(TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken);

        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedItem = await context.BaseItems.SingleOrDefaultAsync(item => item.Id == itemId, TestContext.Current.CancellationToken);
        var savedImages = await context.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .Select(image => new { image.ImageType, image.Path, image.ProviderName, image.Language })
            .ToArrayAsync(TestContext.Current.CancellationToken);

        if (savedItem is null)
        {
            Assert.Empty(savedImages);
        }
        else
        {
            Assert.StartsWith("Concurrent metadata ", savedItem.Name, StringComparison.Ordinal);
            var image = Assert.Single(savedImages);
            Assert.Equal(ImageType.Primary, (ImageType)image.ImageType);
            Assert.Equal(path + "\\poster.jpg", image.Path);
            Assert.Equal("TheMovieDb", image.ProviderName);
            Assert.Equal("pt-BR", image.Language);
        }
    }

    [Fact]
    public async Task SaveItemsBatchAndSaveImagesAsync_ConcurrentItemsAcrossStripes_CompletesWithoutDeadlock()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        const int itemCount = 24;
        var items = Enumerable.Range(0, itemCount)
            .Select(index =>
            {
                var id = Guid.NewGuid();
                var path = $"N:\\Series\\Concurrent batch image {id:N}";
                return CreateSeries(id, $"Concurrent batch image {index}", 2026, path);
            })
            .ToArray();
        Assert.True(items.Select(item => item.Id.GetHashCode() & 15).Distinct().Count() > 1);

        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems(items, CancellationToken.None);

        var metadataUpdates = new List<BaseItem>();
        var imageRefreshes = new List<BaseItem>();
        foreach (var (item, index) in items.Select((item, index) => (item, index)))
        {
            var imagePath = item.Path + "\\poster.jpg";
            var metadata = CreateSeries(item.Id, $"Concurrent batch image {index} updated", 2026, item.Path);
            metadata.AddImage(new ItemImageInfo
            {
                Path = imagePath,
                Type = ImageType.Primary,
                ProviderName = "TheMovieDb",
                Language = "pt-BR",
            });
            metadataUpdates.Add(metadata);

            var imageRefresh = CreateSeries(item.Id, $"Concurrent batch image {index}", 2026, item.Path);
            imageRefresh.AddImage(new ItemImageInfo
            {
                Path = imagePath,
                Type = ImageType.Primary,
                ProviderName = "TheMovieDb",
                Language = "pt-BR",
            });
            imageRefreshes.Add(imageRefresh);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadataTask = Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            persistence.SaveItems(metadataUpdates, TestContext.Current.CancellationToken);
        });
        var imageTasks = imageRefreshes.Select(item => Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);
        }));
        start.SetResult();

        await Task.WhenAll(imageTasks.Append(metadataTask))
            .WaitAsync(TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken);

        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedItems = await context.BaseItems
            .Where(saved => items.Select(item => item.Id).Contains(saved.Id))
            .Select(saved => saved.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var savedImages = await context.BaseItemImageInfos
            .Where(image => items.Select(item => item.Id).Contains(image.ItemId))
            .Select(image => new { image.ItemId, image.ProviderName, image.Language })
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(itemCount, savedItems.Count(name => name.EndsWith(" updated", StringComparison.Ordinal)));
        Assert.Equal(itemCount, savedImages.Length);
        Assert.All(savedImages, image =>
        {
            Assert.Equal("TheMovieDb", image.ProviderName);
            Assert.Equal("pt-BR", image.Language);
        });
        Assert.Equal(itemCount, savedImages.Select(image => image.ItemId).Distinct().Count());
    }

    [Fact]
    public async Task SaveImagesAsync_PersistsProviderAndLanguageChanges()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = "N:\\Movies\\Image metadata refresh (2026).mkv";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Image metadata refresh", 2026, path)], CancellationToken.None);

        var firstRefresh = CreateSeries(itemId, "Image metadata refresh", 2026, path);
        firstRefresh.AddImage(new ItemImageInfo
        {
            Path = path + ".jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
            ProviderName = "TheMovieDb",
            Language = "en",
        });
        await persistence.SaveImagesAsync(firstRefresh, TestContext.Current.CancellationToken);

        var correctedRefresh = CreateSeries(itemId, "Image metadata refresh", 2026, path);
        correctedRefresh.AddImage(new ItemImageInfo
        {
            Path = path + ".jpg",
            Type = ImageType.Primary,
            DateModified = firstRefresh.ImageInfos[0].DateModified,
            ProviderName = "Fanart",
            Language = "pt-BR",
        });
        await persistence.SaveImagesAsync(correctedRefresh, TestContext.Current.CancellationToken);

        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedImage = await context.BaseItemImageInfos.SingleAsync(image => image.ItemId == itemId, TestContext.Current.CancellationToken);
        Assert.Equal("Fanart", savedImage.ProviderName);
        Assert.Equal("pt-BR", savedImage.Language);
    }

    [Fact]
    public async Task SaveImagesAsync_ReplacesImageRowsAndPreservesSharedPaths()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), $"mfx-image-cleanup-{Guid.NewGuid():N}");
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems(
        [
            CreateSeries(firstId, "First", 2020, path + "-first"),
            CreateSeries(secondId, "Second", 2021, path + "-second"),
        ],
        CancellationToken.None);

        var first = CreateSeries(firstId, "First", 2020, path + "-first");
        first.AddImage(new ItemImageInfo { Path = path + "-old.jpg", Type = ImageType.Primary });
        first.AddImage(new ItemImageInfo { Path = path + "-shared.jpg", Type = ImageType.Backdrop });
        await persistence.SaveImagesAsync(first, TestContext.Current.CancellationToken);

        var second = CreateSeries(secondId, "Second", 2021, path + "-second");
        second.AddImage(new ItemImageInfo { Path = path + "-shared.jpg", Type = ImageType.Primary });
        await persistence.SaveImagesAsync(second, TestContext.Current.CancellationToken);

        var replacement = CreateSeries(firstId, "First", 2020, path + "-first");
        replacement.AddImage(new ItemImageInfo { Path = path + "-new.jpg", Type = ImageType.Primary });
        replacement.AddImage(new ItemImageInfo { Path = path + "-shared.jpg", Type = ImageType.Backdrop });
        await persistence.SaveImagesAsync(replacement, TestContext.Current.CancellationToken);

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var firstImagePaths = await verifyContext.BaseItemImageInfos
            .Where(image => image.ItemId == firstId)
            .Select(image => image.Path)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var sharedImageOwners = await verifyContext.BaseItemImageInfos
            .Where(image => image.Path == path + "-shared.jpg")
            .Select(image => image.ItemId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains(path + "-new.jpg", firstImagePaths);
        Assert.Contains(firstId, sharedImageOwners);
        Assert.Contains(secondId, sharedImageOwners);
    }

    [Fact]
    public async Task RefreshImages_PersistsSelectedPortugueseCandidateToMariaDb()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        const string path = "N:\\Movies\\Localized poster (2026).mkv";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Localized poster", 2026, path)], CancellationToken.None);

        var item = CreateSeries(itemId, "Localized poster", 2026, path);
        item.PreferredMetadataLanguage = "pt-BR";
        var previousMediaSourceManager = BaseItem.MediaSourceManager;
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(files => files.GetFileInfo(It.IsAny<string>()))
            .Returns((string imagePath) =>
            {
                var imageFile = new FileInfo(imagePath);
                return new FileSystemMetadata
                {
                    Exists = imageFile.Exists,
                    FullName = imagePath,
                    Name = imageFile.Name,
                    Extension = imageFile.Extension,
                    Length = imageFile.Exists ? imageFile.Length : 0,
                };
            });
        fileSystem
            .Setup(files => files.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns((FileSystemMetadata image) => File.GetLastWriteTimeUtc(image.FullName));
        var previousFileSystem = BaseItem.FileSystem;
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetLibraryOptions(It.IsAny<BaseItem>()))
            .Returns(new LibraryOptions { SaveLocalMetadata = false });
        var previousLibraryManager = BaseItem.LibraryManager;
        var imageSaver = new ImageSaver(
            _services!.GetRequiredService<IServerConfigurationManager>(),
            Mock.Of<ILibraryMonitor>(),
            fileSystem.Object,
            NullLogger<ImageSaver>.Instance);
        var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
        remoteProvider.Setup(provider => provider.Name).Returns("PortugueseProvider");
        remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
        remoteProvider.Setup(provider => provider.GetImageResponse("localized-poster", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/poster.jpg"),
                Content = new StringContent("image content", System.Text.Encoding.UTF8, MediaTypeNames.Image.Jpeg)
            });

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RemoteImageInfo
            {
                ProviderName = "PortugueseProvider",
                Language = "pt",
                Type = ImageType.Primary,
                Url = "localized-poster"
            }]);
        providerManager.Setup(manager => manager.SaveImage(
                item,
                It.IsAny<Stream>(),
                MediaTypeNames.Image.Jpeg,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Returns((BaseItem imageItem, Stream source, string mimeType, ImageType imageType, int? imageIndex, CancellationToken cancellationToken) =>
                imageSaver.SaveImage(imageItem, source, mimeType, imageType, imageIndex, cancellationToken));

        BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
        BaseItem.FileSystem = fileSystem.Object;
        BaseItem.LibraryManager = libraryManager.Object;
        try
        {
            var refreshResult = await new ItemImageProvider(
                    NullLogger<ItemImageProvider>.Instance,
                    providerManager.Object,
                    fileSystem.Object)
                .RefreshImages(
                    item,
                    new LibraryOptions { PreferredMetadataLanguage = "pt-BR" },
                    [remoteProvider.Object],
                    new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                    TestContext.Current.CancellationToken);

            Assert.True(refreshResult.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            var refreshedImage = item.GetImageInfo(ImageType.Primary, 0)!;
            Assert.Equal("pt", refreshedImage.Language);
            Assert.Equal("PortugueseProvider", refreshedImage.ProviderName);
            Assert.True(File.Exists(refreshedImage.Path));
            Assert.Equal(
                "image content",
                await File.ReadAllTextAsync(refreshedImage.Path, TestContext.Current.CancellationToken));

            await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);

            await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
            var savedImage = await context.BaseItemImageInfos.SingleAsync(
                image => image.ItemId == itemId,
                TestContext.Current.CancellationToken);
            Assert.Equal("PortugueseProvider", savedImage.ProviderName);
            Assert.Equal("pt", savedImage.Language);
            Assert.Equal(refreshedImage.Path, savedImage.Path);
            Assert.True(File.Exists(savedImage.Path));
        }
        finally
        {
            BaseItem.MediaSourceManager = previousMediaSourceManager;
            BaseItem.FileSystem = previousFileSystem;
            BaseItem.LibraryManager = previousLibraryManager;
            var disposableRoot = Path.Combine(Path.GetTempPath(), _testDatabase);
            if (Directory.Exists(disposableRoot))
            {
                Directory.Delete(disposableRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveItems_WaitsForOrphanedLocalAlternateVersionDatabaseLock()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var primaryId = Guid.NewGuid();
        var alternateId = Guid.NewGuid();
        while ((primaryId.GetHashCode() & 15) == (alternateId.GetHashCode() & 15))
        {
            alternateId = Guid.NewGuid();
        }

        var primaryPath = "N:\\Movies\\Lock fixture primary.mkv";
        var alternatePath = "N:\\Movies\\Lock fixture alternate.mkv";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var primary = new Video
        {
            Id = primaryId,
            Name = "Lock fixture primary",
            Path = primaryPath,
            DateCreated = DateTime.UtcNow,
        };
        var alternate = new Video
        {
            Id = alternateId,
            OwnerId = primaryId,
            Name = "Lock fixture alternate",
            Path = alternatePath,
            DateCreated = DateTime.UtcNow
        };

        persistence.SaveItems([primary, alternate], CancellationToken.None);
        var linkedPrimary = new Video
        {
            Id = primaryId,
            Name = primary.Name,
            Path = primaryPath,
            DateCreated = primary.DateCreated,
            LocalAlternateVersions = [alternatePath]
        };
        persistence.SaveItems([linkedPrimary], CancellationToken.None);

        var alternateStripe = alternateId.GetHashCode() & 15;
        await using var blockerContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var monitorContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var alternateLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            blockerContext.Database,
            [alternateStripe],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);
        var saveTask = Task.Run(() => persistence.SaveItems(
            [new Video
            {
                Id = primaryId,
                Name = primary.Name,
                Path = primaryPath,
                DateCreated = primary.DateCreated,
                LocalAlternateVersions = []
            }],
            CancellationToken.None));
        await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var waitingForLock = false;
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var databaseParameter = command.CreateParameter();
                databaseParameter.ParameterName = "@database";
                databaseParameter.Value = monitorContext.Database.GetDbConnection().Database;
                command.Parameters.Add(databaseParameter);
                waitingForLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForLock, "Removing the orphaned alternate version must wait on its MariaDB advisory lock.");
            Assert.False(saveTask.IsCompleted, "Removing the alternate version must not complete while another database session holds its stripe.");
        }
        finally
        {
            await alternateLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await saveTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.False(await context.BaseItems.AnyAsync(item => item.Id == alternateId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ItemPersistenceAdvisoryLock_BlocksAnotherDatabaseSession()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await using var firstContext = await Contexts.CreateDbContextAsync();
        await using var secondContext = await Contexts.CreateDbContextAsync();
        await using var monitorContext = await Contexts.CreateDbContextAsync();
        var firstLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            firstContext.Database,
            [0],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);
        await secondContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var connectionIdCommand = secondContext.Database.GetDbConnection().CreateCommand();
        connectionIdCommand.CommandText = "SELECT CONNECTION_ID()";
        var secondConnectionId = Convert.ToInt64(
            await connectionIdCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            CultureInfo.InvariantCulture);

        try
        {
            var secondLockTask = ItemPersistenceDatabaseLock.AcquireAsync(
                secondContext.Database,
                [0],
                NullLogger.Instance,
                TestContext.Current.CancellationToken);
            await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            var waitingForLock = false;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID = @connectionId AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@connectionId";
                parameter.Value = secondConnectionId;
                command.Parameters.Add(parameter);
                waitingForLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForLock, "The second database session did not enter MariaDB's advisory-lock wait state.");

            await firstLock.DisposeAsync();
            await using var secondLock = await secondLockTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            await firstLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await secondContext.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task DeleteExpiredPlaceholderUserDataAsync_DeletesOnlyExpiredDetachedRows()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cutoff = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var realItemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(realItemId, "Dados retidos", 2020, "N:\\Series\\Dados retidos (2020)")], CancellationToken.None);

        var userId = Guid.NewGuid();
        await using (var userContext = await UserContexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            userContext.Users.Add(new User(
                "retention-" + userId.ToString("N", CultureInfo.InvariantCulture),
                "test-authentication",
                "test-password-reset")
            {
                Id = userId,
            });
            await userContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var expiredDetached = CreateUserData(BaseItemRepository.PlaceholderId, userId, DateTime.UnixEpoch);
        var cutoffDetached = CreateUserData(BaseItemRepository.PlaceholderId, userId, cutoff);
        var expiredAttached = CreateUserData(realItemId, userId, DateTime.UnixEpoch);
        await using (var seedContext = await Contexts.CreateDbContextAsync())
        {
            seedContext.UserData.AddRange(expiredDetached, cutoffDetached, expiredAttached);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var deleted = await persistence.DeleteExpiredPlaceholderUserDataAsync(cutoff, TestContext.Current.CancellationToken);

        await using var verifyContext = await Contexts.CreateDbContextAsync();
        Assert.Equal(1, deleted);
        Assert.False(await verifyContext.UserData.AnyAsync(data => data.CustomDataKey == expiredDetached.CustomDataKey));
        Assert.True(await verifyContext.UserData.AnyAsync(data => data.CustomDataKey == cutoffDetached.CustomDataKey));
        Assert.True(await verifyContext.UserData.AnyAsync(data => data.CustomDataKey == expiredAttached.CustomDataKey));
    }

    [Fact]
    public async Task SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = "N:\\Series\\Advisory lock de imagens (2026)";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Advisory lock de imagens", 2026, path)], CancellationToken.None);
        var item = CreateSeries(itemId, "Advisory lock de imagens", 2026, path);
        item.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
        });

        var lockIndex = itemId.GetHashCode() & 15;
        await using var blockerContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var monitorContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var databaseLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            blockerContext.Database,
            [lockIndex],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);
        var saveTask = persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);
        await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var waitingForLock = false;
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var databaseParameter = command.CreateParameter();
                databaseParameter.ParameterName = "@database";
                databaseParameter.Value = monitorContext.Database.GetDbConnection().Database;
                command.Parameters.Add(databaseParameter);
                waitingForLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForLock, "SaveImagesAsync must acquire its MariaDB advisory lock before changing image rows.");
            Assert.False(saveTask.IsCompleted, "SaveImagesAsync must not complete while another database session holds the item's stripe.");
        }
        finally
        {
            await databaseLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await saveTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var savedImages = await verifyContext.BaseItemImageInfos.Where(image => image.ItemId == itemId).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Single(savedImages);
        Assert.Equal(path + "\\poster.jpg", savedImages[0].Path);
    }

    [Fact]
    public async Task UpdateInheritedValues_WaitsForDatabaseAdvisoryLockHeldByAnotherSession()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        await using var blockerContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var monitorContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var databaseLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            blockerContext.Database,
            [15],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        var updateTask = Task.Run(persistence.UpdateInheritedValues, TestContext.Current.CancellationToken);
        await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var waitingForLock = false;
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var databaseParameter = command.CreateParameter();
                databaseParameter.ParameterName = "@database";
                databaseParameter.Value = monitorContext.Database.GetDbConnection().Database;
                command.Parameters.Add(databaseParameter);
                waitingForLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForLock, "UpdateInheritedValues must acquire all MariaDB advisory locks before deleting inherited-tag rows.");
            Assert.False(updateTask.IsCompleted, "UpdateInheritedValues must wait while another database session holds any item stripe.");
        }
        finally
        {
            await databaseLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await updateTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task FixDatesImageMigration_WaitsForAllImageAdvisoryLocks()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await using var blockerContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var monitorContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var migrationContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var databaseLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            blockerContext.Database,
            [15],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        var routine = new FixDates(
            NullLogger<FixDates>.Instance,
            new StartupLogger<FixDates>(NullLogger<FixDates>.Instance),
            Contexts);
        var migrationTask = routine.FixBaseItemImageInfos(
            migrationContext,
            Stopwatch.StartNew(),
            TestContext.Current.CancellationToken);
        await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        try
        {
            var waitingForLock = false;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var databaseParameter = command.CreateParameter();
                databaseParameter.ParameterName = "@database";
                databaseParameter.Value = monitorContext.Database.GetDbConnection().Database;
                command.Parameters.Add(databaseParameter);
                waitingForLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForLock, "FixDates must acquire the shared image advisory locks before updating image rows.");
            Assert.False(migrationTask.IsCompleted, "The migration must wait while another database session holds an image stripe.");
        }
        finally
        {
            await databaseLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await migrationTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = "N:\\Series\\Advisory lock de exclusão (2026)";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Advisory lock de exclusão", 2026, path)], CancellationToken.None);

        var lockIndex = itemId.GetHashCode() & 15;
        await using var blockerContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var monitorContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var databaseLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            blockerContext.Database,
            [lockIndex],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);
        var deleteTask = Task.Run(() => persistence.DeleteItem(new[] { itemId }));
        await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var waitingForLock = false;
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var databaseParameter = command.CreateParameter();
                databaseParameter.ParameterName = "@database";
                databaseParameter.Value = monitorContext.Database.GetDbConnection().Database;
                command.Parameters.Add(databaseParameter);
                waitingForLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForLock, "DeleteItem must acquire its MariaDB advisory lock before changing item rows.");
            Assert.False(deleteTask.IsCompleted, "DeleteItem must not complete while another database session holds the item's stripe.");
        }
        finally
        {
            await databaseLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await deleteTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.False(await verifyContext.BaseItems.AnyAsync(item => item.Id == itemId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.BaseItemImageInfos.AnyAsync(image => image.ItemId == itemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteItem_ConcurrentWithBlockedImageRefresh_CompletesWithoutDeadlock()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var path = "N:\\Series\\A Agência (2020)";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "A Agência", 2020, path)], CancellationToken.None);
        var imageRefresh = CreateSeries(itemId, "A Agência", 2020, path);
        imageRefresh.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow,
        });

        var lockIndex = itemId.GetHashCode() & 15;
        await using var blockerContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var monitorContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var databaseLock = await ItemPersistenceDatabaseLock.AcquireAsync(
            blockerContext.Database,
            [lockIndex],
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        var refreshTask = persistence.SaveImagesAsync(imageRefresh, TestContext.Current.CancellationToken);
        await monitorContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        Task? deleteTask = null;
        var waitingForRefreshLock = false;
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var command = monitorContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND STATE = 'User lock' AND INFO LIKE '%GET_LOCK%'";
                var databaseParameter = command.CreateParameter();
                databaseParameter.ParameterName = "@database";
                databaseParameter.Value = monitorContext.Database.GetDbConnection().Database;
                command.Parameters.Add(databaseParameter);
                waitingForRefreshLock = Convert.ToInt32(
                    await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (waitingForRefreshLock)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(waitingForRefreshLock, "SaveImagesAsync must be blocked on its advisory lock before DeleteItem starts.");
            Assert.False(refreshTask.IsCompleted, "The image refresh must remain blocked until the competing operation is queued.");

            var deleteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            deleteTask = Task.Run(() =>
            {
                deleteStarted.SetResult();
                persistence.DeleteItem(new[] { itemId });
            });
            await deleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Task.Delay(50, TestContext.Current.CancellationToken);
            Assert.Equal(TaskStatus.Running, deleteTask.Status);
            Assert.False(deleteTask.IsCompleted, "DeleteItem must remain blocked while the image refresh owns the local item stripe.");

            await databaseLock.DisposeAsync();
            await Task.WhenAll(refreshTask, deleteTask)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            await databaseLock.DisposeAsync();
            await monitorContext.Database.CloseConnectionAsync();
            await refreshTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            if (deleteTask is not null)
            {
                await deleteTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            }
        }

        await using var context = await Contexts.CreateDbContextAsync();
        Assert.False(await context.BaseItems.AnyAsync(item => item.Id == itemId));
        Assert.False(await context.BaseItemImageInfos.AnyAsync(image => image.ItemId == itemId));
    }

    [Fact]
    public async Task SaveItems_ReSavingAnExistingItem_RewritesItWithoutTrackingConflict()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        var path = "N:\\Series\\A Agência (2020)";

        persistence.SaveItems([CreateSeries(itemId, "A Agência", 2020, path)], CancellationToken.None);

        // The second save of the same id, with changed metadata and a new provider id, is the shape
        // that failed in production: the item already exists, so the save takes the rewrite branch that
        // deletes and re-inserts the child rows.
        var updated = CreateSeries(itemId, "A Agência (renomeada)", 2020, path);
        updated.SetProviderId("DramaBox", "42000002641");

        persistence.SaveItems([updated], CancellationToken.None);

        await using var context = await Contexts.CreateDbContextAsync();
        var row = await context.BaseItems.SingleAsync(e => e.Id == itemId);

        Assert.Equal("A Agência (renomeada)", row.Name);
    }

    [Fact]
    public async Task SaveItems_SavingTwoDistinctItems_KeepsBothRows()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        // Two series that share a name but not a year: the pair the identification fix is about. They
        // must remain two rows.
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        persistence.SaveItems(
            [
                CreateSeries(firstId, "A Agência", 2020, "N:\\Series\\A Agência (2020)"),
                CreateSeries(secondId, "A Agência", 2024, "N:\\Series\\A Agência (2024)")
            ],
            CancellationToken.None);

        await using var context = await Contexts.CreateDbContextAsync();
        var rows = await context.BaseItems
            .Where(e => e.Id == firstId || e.Id == secondId)
            .ToArrayAsync();
        var years = rows.Select(e => e.ProductionYear).OrderBy(e => e).ToArray();

        Assert.True(
            rows.Length == 2,
            $"rows={rows.Length}; years=[{string.Join(",", years.Select(e => e?.ToString(CultureInfo.InvariantCulture) ?? "null"))}]");

        // Identity is per row, and the year is what keeps the two "A Agência" apart. Merging them was
        // the reported bug.
        Assert.Equal(new int?[] { 2020, 2024 }, years);
        Assert.Equal(2, rows.Select(e => e.Path).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task UserProfileImage_CanBeDeletedAndRecreatedWithGeneratedIdentity()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();

        var userId = Guid.NewGuid();
        int originalImageId;
        var username = "avatar-rollback-" + userId.ToString("N", CultureInfo.InvariantCulture);

        await using (var context = await UserContexts.CreateDbContextAsync())
        {
            var user = new User(username, "test-authentication", "test-password-reset")
            {
                ProfileImage = new ImageInfo("avatars/original.png"),
            };
            user.Id = userId;
            context.Users.Add(user);
            await context.SaveChangesAsync();
            originalImageId = user.ProfileImage!.Id;
        }

        await using (var context = await UserContexts.CreateDbContextAsync())
        {
            var user = await context.Users.Include(entity => entity.ProfileImage)
                .SingleAsync(entity => entity.Id == userId);
            Assert.NotNull(user.ProfileImage);

            context.Remove(user.ProfileImage);
            await context.SaveChangesAsync();
            user.ProfileImage = null;
            await context.SaveChangesAsync();
        }

        await using (var context = await UserContexts.CreateDbContextAsync())
        {
            var user = await context.Users.Include(entity => entity.ProfileImage)
                .SingleAsync(entity => entity.Id == userId);
            Assert.Null(user.ProfileImage);

            user.ProfileImage = new ImageInfo("avatars/restored.png");
            await context.SaveChangesAsync();
        }

        await using (var context = await UserContexts.CreateDbContextAsync())
        {
            var restoredUser = await context.Users.Include(entity => entity.ProfileImage)
                .SingleAsync(entity => entity.Id == userId);

            Assert.NotNull(restoredUser.ProfileImage);
            Assert.NotEqual(originalImageId, restoredUser.ProfileImage.Id);
            Assert.True(restoredUser.ProfileImage.Id > 0);
            Assert.Equal("avatars/restored.png", restoredUser.ProfileImage.Path);
            Assert.Equal(userId, restoredUser.ProfileImage.UserId);
            Assert.False(await context.Set<ImageInfo>().AnyAsync(image => image.Id == originalImageId));
        }
    }

    /// <summary>
    /// Reproduces the shape that was failing in production while the library refreshed a series.
    /// </summary>
    /// <returns>A task.</returns>
    /// <remarks>
    /// The live stack (server 12.0.69, 2,355 occurrences in 72 minutes, ~75 per minute) was:
    /// <c>BaseItem.UpdateToRepositoryAsync</c> → <c>LibraryManager.UpdateItemsAsync</c> →
    /// <c>SeriesMetadataService.RefreshMetadata</c> → <c>Folder.RefreshChildMetadata</c> →
    /// <c>RefreshAllMetadataForContainer</c> → <c>SaveBaseItemEntities</c> →
    /// <c>IdentityMap.ThrowIdentityConflict</c>. The re-save of an <em>existing child</em> is the
    /// interesting part: a child carries all four kinds of row that the mapper wires back to the
    /// <c>BaseItemEntity</c> instance it just built (providers, images, locked fields, trailer types),
    /// and the row that throws is the one whose navigation still points at the fresh instance while a
    /// different instance with the same key is already tracked.
    /// <para>
    /// Keeping the assertions on the child rows — not just "it did not throw" — is deliberate: the
    /// failure mode was a save that aborted, and a test that only checks for the absence of an
    /// exception would still pass if the rows silently stopped being written.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task SaveItems_RefreshingAnExistingEpisodeUnderASavedSeries_RewritesTheChildRows()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var seriesId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var path = "N:\\Series\\A Agência (2020)";

        persistence.SaveItems([CreateSeries(seriesId, "A Agência", 2020, path)], CancellationToken.None);
        persistence.SaveItems([CreateSeason(seasonId, seriesId, 1)], CancellationToken.None);

        persistence.SaveItems(
            [CreateEpisode(episodeId, seriesId, seasonId, "Piloto")],
            CancellationToken.None);

        // The refresh: the same episode comes back with changed metadata and all of its child rows.
        var refreshed = CreateEpisode(episodeId, seriesId, seasonId, "Piloto (renomeado)");
        refreshed.SetProviderId("DramaBox", "42000002641");
        refreshed.LockedFields = [MetadataField.Name];
        refreshed.AddImage(new ItemImageInfo
        {
            Path = path + "\\poster.jpg",
            Type = ImageType.Primary,
            DateModified = DateTime.UtcNow
        });

        persistence.SaveItems([refreshed], CancellationToken.None);

        await using var context = await Contexts.CreateDbContextAsync();
        var row = await context.BaseItems.SingleAsync(e => e.Id == episodeId);
        var providers = await context.BaseItemProviders.Where(e => e.ItemId == episodeId).ToArrayAsync();
        var images = await context.BaseItemImageInfos.Where(e => e.ItemId == episodeId).ToArrayAsync();
        var locked = await context.BaseItemMetadataFields.Where(e => e.ItemId == episodeId).ToArrayAsync();

        Assert.Equal("Piloto (renomeado)", row.Name);
        Assert.Equal(seriesId, row.SeriesId);
        Assert.Equal("DramaBox", Assert.Single(providers).ProviderId);
        Assert.Equal(ImageInfoImageType.Primary, Assert.Single(images).ImageType);
        Assert.Equal(MetadataField.Name, (MetadataField)Assert.Single(locked).Id);
    }

    [Fact]
    public async Task GetLatestItemList_Music_UsesMariaDbSubqueryAndDeduplicatesAlbums()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var olderAlbum = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(MusicAlbum).FullName!,
            Name = "Older album",
            DateCreated = now.AddDays(-1),
            IsFolder = true
        };
        var newerAlbum = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(MusicAlbum).FullName!,
            Name = "Newer album",
            DateCreated = now,
            IsFolder = true
        };
        var matchingTracks = new[]
        {
            CreateAudioEntity("Track one", now.AddHours(-1)),
            CreateAudioEntity("Track two", now.AddHours(-2)),
            CreateAudioEntity("Track three", now.AddDays(-2))
        };

        await using (var context = await Contexts.CreateDbContextAsync())
        {
            context.BaseItems.AddRange([olderAlbum, newerAlbum, .. matchingTracks]);
            context.AncestorIds.AddRange(
                new AncestorId { ParentItemId = newerAlbum.Id, ItemId = matchingTracks[0].Id, ParentItem = newerAlbum, Item = matchingTracks[0] },
                new AncestorId { ParentItemId = newerAlbum.Id, ItemId = matchingTracks[1].Id, ParentItem = newerAlbum, Item = matchingTracks[1] },
                new AncestorId { ParentItemId = olderAlbum.Id, ItemId = matchingTracks[2].Id, ParentItem = olderAlbum, Item = matchingTracks[2] });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var itemTypeLookup = new Mock<IItemTypeLookup>();
        itemTypeLookup.Setup(lookup => lookup.BaseItemKindNames)
            .Returns(new Dictionary<BaseItemKind, string>
            {
                { BaseItemKind.Audio, typeof(Audio).FullName! },
                { BaseItemKind.MusicAlbum, typeof(MusicAlbum).FullName! }
            });
        var repository = new BaseItemRepository(
            Contexts,
            Mock.Of<IServerApplicationHost>(),
            itemTypeLookup.Object,
            Mock.Of<IServerConfigurationManager>(manager => manager.Configuration == new ServerConfiguration()),
            NullLogger<BaseItemRepository>.Instance);

        var result = repository.GetLatestItemList(
            new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Audio],
                Limit = 2
            },
            CollectionType.music);

        Assert.Equal([newerAlbum.Id, olderAlbum.Id], result.Select(item => item.Id));
    }

    private static BaseItemEntity CreateAudioEntity(string name, DateTime dateCreated)
    {
        return new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Audio).FullName!,
            Name = name,
            DateCreated = dateCreated,
            IsFolder = false
        };
    }

    /// <summary>
    /// Reproduces the people update that broke in production after the E-4 optimisation.
    /// </summary>
    /// <remarks>
    /// The optimisation moved the <c>Peoples</c> lookup to the database by filtering on the indexed
    /// <c>Name</c> column, but wrote the call as <c>candidateNames.Contains(e.Name)</c>. The compiler then
    /// binds to <c>MemoryExtensions.Contains(ReadOnlySpan&lt;string&gt;, string)</c> instead of
    /// <c>Enumerable.Contains</c>, and EF cannot turn that into a query parameter: every call threw
    /// "GenericArguments[1], 'System.ReadOnlySpan`1[System.String]' ... violates the constraint of type
    /// parameter 'TRet'" while evaluating the parameter expression, which surfaced as 1,389 failed
    /// library operations in the hour and a half after the server was restarted. This test calls the real
    /// repository against real MariaDB, so the span overload would fail it again.
    /// </remarks>
    [Fact]
    public void UpdatePeople_WithAPersonThatAlreadyExists_DoesNotFailWhileEvaluatingTheQuery()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        persistence.SaveItems([CreateSeries(itemId, "A Agência", 2020, "N:\\Series\\A Agência (2020)")], CancellationToken.None);

        // The concrete ItemTypeLookup lives in Emby.Server.Implementations, which this test project does
        // not reference; UpdatePeople does not consult it, so a mock is enough to construct the repository.
        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        var people = new List<PersonInfo> { new PersonInfo { Name = "Ana Moreira", Type = PersonKind.Actor } };

        // First call inserts the person; the second one has to find it through the database filter, which
        // is the branch that used to throw.
        repository.UpdatePeople(itemId, people);
        repository.UpdatePeople(itemId, people);

        using var context = Contexts.CreateDbContext();
        var person = context.Peoples.Single(e => e.Name == "Ana Moreira");
        var maps = context.PeopleBaseItemMap.Where(e => e.ItemId == itemId && e.PeopleId == person.Id).ToArray();

        // The second call must reuse the row instead of inserting a duplicate.
        Assert.Single(maps);
    }

    [Fact]
    public void DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "People orphan cleanup", 2026, "N:\\Series\\People orphan cleanup (2026)")], CancellationToken.None);

        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Orphan cleanup person", Type = PersonKind.Actor }]);
        Guid personId;
        using (var context = Contexts.CreateDbContext())
        {
            personId = context.Peoples.Single(person => person.Name == "Orphan cleanup person").Id;
        }

        persistence.DeleteItem(itemId);

        using var verifyContext = Contexts.CreateDbContext();
        Assert.False(verifyContext.Peoples.Any(person => person.Id == personId));
        Assert.False(verifyContext.PeopleBaseItemMap.Any(map => map.PeopleId == personId));
    }

    [Fact]
    public void GetNextUpSeriesKeys_WithEqualLastPlayedDates_AppliesStableTieBreakBeforeLimit()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var userId = Guid.NewGuid();
        var topParentId = Guid.NewGuid();
        var lastPlayedDate = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var firstEpisode = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = "SeriesA",
            TopParentId = topParentId
        };
        var secondEpisode = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = "SeriesB",
            TopParentId = topParentId
        };

        using (var context = Contexts.CreateDbContext())
        {
            context.BaseItems.AddRange(firstEpisode, secondEpisode);
            context.UserData.AddRange(
                new UserData
                {
                    UserId = userId,
                    ItemId = firstEpisode.Id,
                    Item = firstEpisode,
                    User = null!,
                    CustomDataKey = string.Empty,
                    LastPlayedDate = lastPlayedDate,
                    Played = true
                },
                new UserData
                {
                    UserId = userId,
                    ItemId = secondEpisode.Id,
                    Item = secondEpisode,
                    User = null!,
                    CustomDataKey = string.Empty,
                    LastPlayedDate = lastPlayedDate,
                    Played = true
                });
            context.SaveChanges();
        }

        var itemTypeLookup = new Mock<IItemTypeLookup>();
        itemTypeLookup.SetupGet(lookup => lookup.BaseItemKindNames)
            .Returns(new Dictionary<BaseItemKind, string> { [BaseItemKind.Episode] = "Episode" });
        var service = new NextUpService(Contexts, itemTypeLookup.Object, Mock.Of<IItemQueryHelpers>());
        var filter = new InternalItemsQuery
        {
            User = new User("testuser", "auth", "auth") { Id = userId },
            TopParentIds = [topParentId],
            Limit = 1
        };

        var result = service.GetNextUpSeriesKeys(filter, lastPlayedDate.AddDays(-1));

        Assert.Equal(["SeriesA"], result);
    }

    [Fact]
    public async Task DeleteItem_ConcurrentWithUpdatePeople_DoesNotLeaveOrphanPeople()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var itemId = Guid.NewGuid();
        var itemPath = $"N:\\Series\\People race {itemId:N}";
        var personName = $"People race {itemId:N}";
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var item = CreateSeries(itemId, "People race", 2026, itemPath);
        item.AddImage(new ItemImageInfo { Path = itemPath + "\\poster.jpg", Type = ImageType.Primary });
        persistence.SaveItems([item], CancellationToken.None);
        await persistence.SaveImagesAsync(item, TestContext.Current.CancellationToken);

        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Existing race person", Type = PersonKind.Actor }]);

        var triggerName = "mfx_it_people_race_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        adminConnection.Database = _testDatabase;
        await using var admin = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createTrigger = admin.CreateCommand())
        {
            createTrigger.CommandText = $"CREATE TRIGGER `{triggerName}` BEFORE DELETE ON `BaseItemImageInfos` FOR EACH ROW DO SLEEP(5)";
            await createTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var deleteTask = Task.Run(() => persistence.DeleteItem(new[] { itemId }));
        Task? updateTask = null;
        try
        {
            var reachedImageDelete = false;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var monitor = admin.CreateCommand();
                monitor.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB = @database AND ID <> CONNECTION_ID() AND STATE = 'User sleep'";
                monitor.Parameters.AddWithValue("@database", _testDatabase);
                reachedImageDelete = Convert.ToInt32(
                    await monitor.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                    CultureInfo.InvariantCulture) > 0;
                if (reachedImageDelete)
                {
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(reachedImageDelete, "DeleteItem must reach the image-delete barrier after collecting PeopleIds.");

            var updateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            updateTask = Task.Run(() =>
            {
                updateStarted.SetResult();
                repository.UpdatePeople(itemId, [new PersonInfo { Name = personName, Type = PersonKind.Actor }]);
            });
            await updateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(updateTask.IsCompleted, "UpdatePeople must wait for DeleteItem after the delete captured its PeopleIds.");
        }
        finally
        {
            await using var dropTrigger = admin.CreateCommand();
            dropTrigger.CommandText = $"DROP TRIGGER IF EXISTS `{triggerName}`";
            await dropTrigger.ExecuteNonQueryAsync(CancellationToken.None);
            await deleteTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            if (updateTask is not null)
            {
                await updateTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            }
        }

        await using var verifyContext = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.False(await verifyContext.BaseItems.AnyAsync(entry => entry.Id == itemId, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.Peoples.AnyAsync(entry => entry.Name == personName, TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.PeopleBaseItemMap.AnyAsync(entry => entry.ItemId == itemId, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Builds a season under a series.
    /// </summary>
    /// <param name="id">Season id.</param>
    /// <param name="seriesId">Parent series id.</param>
    /// <param name="indexNumber">Season number.</param>
    /// <returns>The season.</returns>
    private static Season CreateSeason(Guid id, Guid seriesId, int indexNumber)
    {
        return new Season
        {
            Id = id,
            Name = "Temporada " + indexNumber.ToString(CultureInfo.InvariantCulture),
            SeriesId = seriesId,
            ParentId = seriesId,
            IndexNumber = indexNumber,
            DateCreated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Builds an episode under a season.
    /// </summary>
    /// <param name="id">Episode id.</param>
    /// <param name="seriesId">Series id.</param>
    /// <param name="seasonId">Season id.</param>
    /// <param name="name">Episode name.</param>
    /// <returns>The episode.</returns>
    private static Episode CreateEpisode(Guid id, Guid seriesId, Guid seasonId, string name)
    {
        return new Episode
        {
            Id = id,
            Name = name,
            SeriesId = seriesId,
            SeasonId = seasonId,
            ParentId = seasonId,
            IndexNumber = 1,
            ParentIndexNumber = 1,
            Path = "N:\\Series\\A Agência (2020)\\Season 1\\S01E01.mkv",
            DateCreated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Builds the entity the persistence service actually accepts.
    /// </summary>
    /// <param name="id">Item id.</param>
    /// <param name="name">Item name.</param>
    /// <param name="year">Production year.</param>
    /// <param name="path">Item path.</param>
    /// <returns>The item.</returns>
    /// <remarks>
    /// Note the trap this test had to work around: <c>SaveItems</c> reads as
    /// <c>IReadOnlyList&lt;BaseItemDto&gt;</c> in the implementation, but the file aliases that name to
    /// <see cref="MediaBrowser.Controller.Entities.BaseItem"/> (<c>using BaseItemDto =
    /// MediaBrowser.Controller.Entities.BaseItem;</c>), so the parameter is an entity and not
    /// <c>MediaBrowser.Model.Dto.BaseItemDto</c>. Passing the model DTO does not compile.
    /// </remarks>
    private static Series CreateSeries(Guid id, string name, int year, string path)
    {
        return new Series
        {
            Id = id,
            Name = name,
            ProductionYear = year,
            Path = path,
            DateCreated = DateTime.UtcNow
        };
    }

    private static UserData CreateUserData(Guid itemId, Guid userId, DateTime? retentionDate)
    {
        return new UserData
        {
            CustomDataKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            ItemId = itemId,
            UserId = userId,
            Item = null,
            User = null,
            RetentionDate = retentionDate
        };
    }

    /// <summary>
    /// Builds the host the mapper needs to keep paths intact.
    /// </summary>
    /// <returns>The application host.</returns>
    /// <remarks>
    /// <c>BaseItemMapper.Map</c> stores <c>appHost.ReverseVirtualPath(dto.Path)</c>. A bare
    /// <c>Mock.Of&lt;IServerApplicationHost&gt;()</c> answers that with null, which nulls the Path of every
    /// saved row: the first version of the second test then saw one distinct path across two rows and
    /// looked like a merge bug in the persistence path, when the rows were in fact correct.
    /// </remarks>
    private static IServerApplicationHost CreateAppHost()
    {
        var appHost = new Mock<IServerApplicationHost>();
        appHost.Setup(host => host.ReverseVirtualPath(It.IsAny<string>())).Returns((string path) => path);
        return appHost.Object;
    }

    private static MySqlConnector.MySqlConnectionStringBuilder CreateApplicationConnection(
        MySqlConnector.MySqlConnectionStringBuilder adminConnection,
        string databaseName,
        string userName,
        string password)
    {
        try
        {
            using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE `{databaseName}`;";
            command.ExecuteNonQuery();
            command.CommandText = $"CREATE USER '{userName}'@'127.0.0.1' IDENTIFIED BY '{password}';";
            command.ExecuteNonQuery();
            command.CommandText = $"GRANT ALL PRIVILEGES ON `{databaseName}`.* TO '{userName}'@'127.0.0.1';";
            command.ExecuteNonQuery();
        }
        catch
        {
            DropTestDatabase(databaseName, userName);
            throw;
        }

        return new MySqlConnector.MySqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Database = databaseName,
            UserID = userName,
            Password = password,
        };
    }

    private static void DropTestDatabase(string databaseName, string userName)
    {
        using var connection = new MySqlConnector.MySqlConnection(MariaDbTestEnvironment.GetAdminConnection().ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{databaseName}`;";
        command.ExecuteNonQuery();
        command.CommandText = $"DROP USER IF EXISTS '{userName}'@'127.0.0.1';";
        command.ExecuteNonQuery();
    }

    private static ServiceProvider BuildServices(MySqlConnector.MySqlConnectionStringBuilder applicationConnection, string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        var configuration = new DatabaseConfigurationOptions
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
                    new CustomDatabaseOption { Key = "port", Value = applicationConnection.Port.ToString(CultureInfo.InvariantCulture) },
                    new CustomDatabaseOption { Key = "user", Value = applicationConnection.UserID },
                    new CustomDatabaseOption { Key = "password", Value = applicationConnection.Password },

                    // Keeps the tests off the server's own schema. Without this the provider defaults
                    // to DatabaseNames.Main, which is the live database.
                    new CustomDatabaseOption { Key = "database", Value = databaseName },
                ],
            },
        };

        var configurationManager = new Mock<IServerConfigurationManager>();
        var applicationPaths = new Mock<IServerApplicationPaths>();
        applicationPaths.Setup(paths => paths.ProgramDataPath).Returns(Path.GetTempPath());
        applicationPaths.Setup(paths => paths.InternalMetadataPath).Returns(Path.Combine(Path.GetTempPath(), databaseName, "metadata"));
        configurationManager.Setup(manager => manager.ApplicationPaths).Returns(applicationPaths.Object);
        configurationManager.Setup(manager => manager.GetConfiguration("database")).Returns(configuration);
        configurationManager.Setup(manager => manager.Configuration).Returns(new ServerConfiguration());
        services.AddSingleton(configurationManager.Object);

        services.AddMulletaFlixDbContext(
            configurationManager.Object,
            new ConfigurationBuilder().Build());

        return services.BuildServiceProvider();
    }

    private void InitializeUserSchema()
    {
        var configurationManager = _services!.GetRequiredService<IServerConfigurationManager>();
        var appHost = new Mock<IApplicationHost>();
        var passwordResetProvider = new DefaultPasswordResetProvider(configurationManager, appHost.Object);
        var authenticationProvider = new DefaultAuthenticationProvider(
            NullLogger<DefaultAuthenticationProvider>.Instance,
            Mock.Of<ICryptoProvider>());

        using var userManager = new UserManager(
            UserContexts,
            Mock.Of<IEventManager>(),
            Mock.Of<INetworkManager>(),
            appHost.Object,
            Mock.Of<IImageProcessor>(),
            NullLogger<UserManager>.Instance,
            configurationManager,
            [passwordResetProvider],
            [authenticationProvider, new InvalidAuthProvider()]);

        _ = userManager.GetFirstUser();
    }
}
