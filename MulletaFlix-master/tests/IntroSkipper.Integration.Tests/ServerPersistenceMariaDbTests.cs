// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using Emby.Server.Implementations.Library;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
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
/// The schema is a dedicated throwaway (<see cref="TestDatabase"/>), never the production one, and it is
/// dropped before the run so a stale schema cannot make a broken path look healthy.
/// </para>
/// </remarks>
[Collection(ServerPersistenceCollection.Name)]
public sealed class ServerPersistenceMariaDbTests : IAsyncLifetime
{
    /// <summary>Throwaway schema, never the server's own database.</summary>
    private const string TestDatabase = "mulletaflix_integration_persistence";

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

        DropTestDatabase();
        _services = BuildServices(MariaDbTestEnvironment.GetAdminConnection());

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
        return ValueTask.CompletedTask;
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
    public async Task SaveItems_UpdatingProvidersAcrossDeleteBatchBoundary_RewritesEveryProvider()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var itemIds = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToArray();
        var initialItems = itemIds
            .Select((id, index) =>
            {
                var item = CreateSeries(id, $"Batch provider {index}", 2026, $"N:\\Series\\Batch provider {index}");
                item.SetProviderId("BatchAudit", $"old-{index}");
                return item;
            })
            .ToArray();

        persistence.SaveItems(initialItems, CancellationToken.None);

        var updatedItems = initialItems
            .Select((item, index) =>
            {
                var updated = CreateSeries(item.Id, item.Name, item.ProductionYear!.Value, item.Path!);
                updated.SetProviderId("BatchAudit", $"provider-{index}");
                return updated;
            })
            .ToArray();
        persistence.SaveItems(updatedItems, CancellationToken.None);

        await using var context = await Contexts.CreateDbContextAsync();
        var itemIdList = itemIds.ToList();
        var providers = await context.BaseItemProviders
            .Where(provider => itemIdList.Contains(provider.ItemId))
            .ToArrayAsync();

        Assert.Equal(itemIds.Length, providers.Length);
        Assert.Equal(itemIds.Order(), providers.Select(provider => provider.ItemId).Order());
        Assert.Equal(
            Enumerable.Range(0, itemIds.Length).Select(index => $"provider-{index}").Order(StringComparer.Ordinal),
            providers.Select(provider => provider.ProviderValue).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task PeopleValidationTask_QueriesDuplicateGroupsOnceAcrossBatchBoundary()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();

        var prefix = $"PeopleValidationTask-{Guid.NewGuid():N}-";
        var people = Enumerable.Range(0, 101)
            .SelectMany(index =>
            {
                var name = prefix + index.ToString(CultureInfo.InvariantCulture);
                return new[]
                {
                    new People { Id = Guid.NewGuid(), Name = name, PersonType = "Actor" },
                    new People { Id = Guid.NewGuid(), Name = name, PersonType = "Actor" }
                };
            })
            .ToArray();

        await using (var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await context.Peoples.AddRangeAsync(people);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.ValidatePeopleAsync(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        long catalogReadCommands = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == Jellyfin.Server.Implementations.Database.DatabaseTelemetryMetrics.MeterName
                    && instrument.Name == "mulletaflix.database.commands")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            var operation = string.Empty;
            var context = string.Empty;
            foreach (var tag in tags)
            {
                if (tag.Key == "db.operation")
                {
                    operation = tag.Value as string ?? string.Empty;
                }
                else if (tag.Key == "db.context")
                {
                    context = tag.Value as string ?? string.Empty;
                }
            }

            if (operation == "read" && context == "catalog")
            {
                Interlocked.Add(ref catalogReadCommands, measurement);
            }
        });

        try
        {
            var task = new PeopleValidationTask(libraryManager.Object, Mock.Of<ILocalizationManager>(), Contexts);
            listener.Start();
            await task.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            listener.Dispose();

            await using var context = await Contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);
            var remaining = await context.Peoples
                .Where(person => person.Name.StartsWith(prefix))
                .GroupBy(person => new { person.Name, person.PersonType })
                .Select(group => group.Count())
                .ToArrayAsync();

            Assert.Equal(101, remaining.Length);
            Assert.All(remaining, count => Assert.Equal(1, count));
            Assert.Equal(1, Interlocked.Read(ref catalogReadCommands));
        }
        finally
        {
#pragma warning disable xUnit1051 // Cleanup must remain available after test cancellation.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var context = await Contexts.CreateDbContextAsync(cleanup.Token);
            await context.Peoples
                .Where(person => person.Name.StartsWith(prefix))
                .ExecuteDeleteAsync(cleanup.Token);
#pragma warning restore xUnit1051
        }

        libraryManager.Verify(
            manager => manager.ValidatePeopleAsync(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()),
            Times.Once);
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
    public void UpdatePeople_RoleCasingChange_UpdatesExistingMapping()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        persistence.SaveItems([CreateSeries(itemId, "Elenco com papel atualizado", 2026, "N:\\Series\\Elenco com papel atualizado")], CancellationToken.None);

        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Pessoa com papel", Role = "Diretor", Type = PersonKind.Actor }]);
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Pessoa com papel", Role = "diretor", Type = PersonKind.Actor }]);

        using var context = Contexts.CreateDbContext();
        var mapping = Assert.Single(context.PeopleBaseItemMap.Where(e => e.ItemId == itemId));
        Assert.Equal("diretor", mapping.Role);
    }

    [Fact]
    public async Task UpdatePeople_RollsBackRoleReplacementWhenInsertFails()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        persistence.SaveItems([CreateSeries(itemId, "Rollback de elenco", 2026, "N:\\Series\\Rollback de elenco")], CancellationToken.None);

        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Pessoa com papel", Role = "Diretor", Type = PersonKind.Actor }]);

        await using (var context = await Contexts.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "CREATE TRIGGER `TestPeopleMapReject` BEFORE INSERT ON `PeopleBaseItemMap` "
                + "FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced integration-test failure';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            Assert.Throws<DbUpdateException>(() => repository.UpdatePeople(
                itemId,
                [new PersonInfo { Name = "Pessoa com papel", Role = "diretor", Type = PersonKind.Actor }]));
        }
        finally
        {
            await using var context = await Contexts.CreateDbContextAsync(cancellationToken);
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TestPeopleMapReject;", cancellationToken);
        }

        using var verify = Contexts.CreateDbContext();
        var mapping = Assert.Single(verify.PeopleBaseItemMap.Where(e => e.ItemId == itemId));
        Assert.Equal("Diretor", mapping.Role);
    }

    [Fact]
    public async Task UpdatePeople_ConcurrentUpdatesForSameItem_DoNotInsertDuplicateMappings()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        persistence.SaveItems([CreateSeries(itemId, "Concorrência de elenco", 2026, "N:\\Series\\Concorrência de elenco")], CancellationToken.None);
        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        var seedPerson = new PersonInfo { Name = "Pessoa Concorrente", Type = PersonKind.Actor };
        repository.UpdatePeople(itemId, [seedPerson]);
        var writes = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => repository.UpdatePeople(itemId, [new PersonInfo { Name = seedPerson.Name, Type = seedPerson.Type }])));

        await Task.WhenAll(writes);

        using var context = Contexts.CreateDbContext();
        var person = context.Peoples.Single(e => e.Name == "Pessoa Concorrente");
        var maps = context.PeopleBaseItemMap.Where(e => e.ItemId == itemId && e.PeopleId == person.Id).ToArray();
        Assert.Single(maps);
    }

    [Fact]
    public async Task UpdatePeople_RetriesWholeTransactionAfterMariaDbLockWaitTimeout()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        persistence.SaveItems([CreateSeries(itemId, "Retry de elenco", 2026, "N:\\Series\\Retry de elenco")], CancellationToken.None);

        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Pessoa com lock", Role = "Diretor", Type = PersonKind.Actor }]);

        var retryLogger = new PeopleRetryLogger();
        repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>(), retryLogger);
        var previousTimeout = await SetMariaDbLockWaitTimeoutAsync(1, cancellationToken);
        try
        {
            var lockContext = await Contexts.CreateDbContextAsync(cancellationToken);
            await using (lockContext.ConfigureAwait(false))
            {
                await lockContext.Database.OpenConnectionAsync(cancellationToken);
                await using var lockTransaction = await lockContext.Database.BeginTransactionAsync(cancellationToken);
                _ = await lockContext.PeopleBaseItemMap
                    .FromSqlInterpolated($"SELECT * FROM `PeopleBaseItemMap` WHERE `ItemId` = {itemId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

                var saveTask = Task.Run(
                    () => repository.UpdatePeople(itemId, [new PersonInfo { Name = "Pessoa com lock", Role = "Produtor", Type = PersonKind.Actor }]),
                    cancellationToken);
                Exception? retrySignalError = null;
                try
                {
                    await retryLogger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (Exception exception)
                {
                    retrySignalError = exception;
                }
                finally
                {
                    await lockTransaction.RollbackAsync(CancellationToken.None);
                }

                if (retrySignalError is not null)
                {
                    try
                    {
                        await saveTask;
                    }
                    catch
                    {
                        // The retry-signal assertion below is the primary failure for this path.
                    }

                    Assert.Fail($"UpdatePeople did not enter the whole-operation retry path: {retrySignalError.Message}");
                }

                await saveTask;
                Assert.Equal(1, retryLogger.RetryCount);
            }
        }
        finally
        {
            _ = await SetMariaDbLockWaitTimeoutAsync(previousTimeout, CancellationToken.None);
        }

        using var context = Contexts.CreateDbContext();
        var mapping = Assert.Single(context.PeopleBaseItemMap.Where(e => e.ItemId == itemId));
        Assert.Equal("Produtor", mapping.Role);
    }

    [Fact]
    public async Task UpdatePeople_ConcurrentUpdatesForDifferentItems_ReuseTheSamePersonRow()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        persistence.SaveItems(
        [
            CreateSeries(itemIds[0], "Elenco compartilhado A", 2026, "N:\\Series\\Elenco compartilhado A"),
            CreateSeries(itemIds[1], "Elenco compartilhado B", 2026, "N:\\Series\\Elenco compartilhado B")
        ],
        CancellationToken.None);

        var repository = new PeopleRepository(Contexts, Mock.Of<IItemTypeLookup>());
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = Enumerable.Range(0, 16)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;
                repository.UpdatePeople(itemIds[index % itemIds.Length], [new PersonInfo { Name = "Pessoa compartilhada", Type = PersonKind.Actor }]);
            }));

        start.SetResult();
        await Task.WhenAll(writes);

        using var context = Contexts.CreateDbContext();
        var person = Assert.Single(context.Peoples.Where(e => e.Name == "Pessoa compartilhada" && e.PersonType == PersonKind.Actor.ToString()));
        foreach (var itemId in itemIds)
        {
            var mapping = Assert.Single(context.PeopleBaseItemMap.Where(e => e.ItemId == itemId));
            Assert.Equal(person.Id, mapping.PeopleId);
        }
    }

    [Fact]
    public async Task SaveItems_RescanningUnchangedImages_PreservesRowsAndOnlyReplacesChangedMetadata()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        const string path = "N:\\Movies\\Example (2024)";
        var initial = CreateSeries(itemId, "Example", 2024, path);
        initial.AddImage(CreateImage("poster.jpg", "pt-br"));
        persistence.SaveItems([initial], CancellationToken.None);

        Guid originalImageId;
        await using (var context = await Contexts.CreateDbContextAsync())
        {
            originalImageId = (await context.BaseItemImageInfos.SingleAsync(e => e.ItemId == itemId)).Id;
        }

        var unchangedRescan = CreateSeries(itemId, "Example", 2024, path);
        unchangedRescan.AddImage(CreateImage("poster.jpg", "pt-br"));
        persistence.SaveItems([unchangedRescan], CancellationToken.None);

        await using (var context = await Contexts.CreateDbContextAsync())
        {
            var unchangedImage = await context.BaseItemImageInfos.SingleAsync(e => e.ItemId == itemId);
            Assert.Equal(originalImageId, unchangedImage.Id);
            Assert.Equal("pt-br", unchangedImage.Language);
        }

        var changedRescan = CreateSeries(itemId, "Example", 2024, path);
        changedRescan.AddImage(CreateImage("poster.jpg", "en"));
        persistence.SaveItems([changedRescan], CancellationToken.None);

        await using (var context = await Contexts.CreateDbContextAsync())
        {
            var changedImage = await context.BaseItemImageInfos.SingleAsync(e => e.ItemId == itemId);
            Assert.NotEqual(originalImageId, changedImage.Id);
            Assert.Equal("en", changedImage.Language);
        }
    }

    [Fact]
    public async Task SaveImagesAsync_PersistsLocalizedImagesAndIsIdempotent()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        const string path = "N:\\Movies\\Localized image persistence (2026)";
        persistence.SaveItems([CreateSeries(itemId, "Localized image persistence", 2026, path)], cancellationToken);

        var portugueseItem = CreateSeries(itemId, "Localized image persistence", 2026, path);
        portugueseItem.AddImage(CreateImage("poster.jpg", "pt-br"));
        await persistence.SaveImagesAsync(portugueseItem, cancellationToken);

        Guid firstImageId;
        await using (var context = await Contexts.CreateDbContextAsync())
        {
            var storedImage = await context.BaseItemImageInfos.SingleAsync(image => image.ItemId == itemId);
            firstImageId = storedImage.Id;
            Assert.Equal("pt-br", storedImage.Language);
            Assert.Equal("TheMovieDb", storedImage.ProviderName);
        }

        var unchangedItem = CreateSeries(itemId, "Localized image persistence", 2026, path);
        unchangedItem.AddImage(CreateImage("poster.jpg", "pt-br"));
        await persistence.SaveImagesAsync(unchangedItem, cancellationToken);

        await using (var context = await Contexts.CreateDbContextAsync())
        {
            var unchangedImage = await context.BaseItemImageInfos.SingleAsync(image => image.ItemId == itemId);
            Assert.Equal(firstImageId, unchangedImage.Id);
        }

        var updatedItem = CreateSeries(itemId, "Localized image persistence", 2026, path);
        updatedItem.AddImage(CreateImage("poster.jpg", "en"));
        await persistence.SaveImagesAsync(updatedItem, cancellationToken);

        await using (var context = await Contexts.CreateDbContextAsync())
        {
            var updatedImage = await context.BaseItemImageInfos.SingleAsync(image => image.ItemId == itemId);
            Assert.NotEqual(firstImageId, updatedImage.Id);
            Assert.Equal("en", updatedImage.Language);
        }
    }

    [Fact]
    public async Task SaveImagesAsync_RollsBackDeletedImagesWhenInsertFails()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        var initialItem = CreateSeries(itemId, "Rollback de imagens", 2026, "N:\\Movies\\Rollback de imagens");
        initialItem.AddImage(CreateImage("old/poster.jpg", "pt-br"));
        persistence.SaveItems([initialItem], cancellationToken);

        Guid originalImageId;
        await using (var context = await Contexts.CreateDbContextAsync(cancellationToken))
        {
            originalImageId = await context.BaseItemImageInfos
                .Where(image => image.ItemId == itemId)
                .Select(image => image.Id)
                .SingleAsync(cancellationToken);

            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "CREATE TRIGGER `TestImageReject` BEFORE INSERT ON `BaseItemImageInfos` "
                + "FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced integration-test failure';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            var replacement = CreateSeries(itemId, "Rollback de imagens", 2026, "N:\\Movies\\Rollback de imagens");
            replacement.AddImage(CreateImage("new/poster.jpg", "en"));
            await Assert.ThrowsAsync<DbUpdateException>(() => persistence.SaveImagesAsync(replacement, cancellationToken));
        }
        finally
        {
            await using var context = await Contexts.CreateDbContextAsync(cancellationToken);
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TestImageReject;", cancellationToken);
        }

        await using var verify = await Contexts.CreateDbContextAsync(cancellationToken);
        var image = await verify.BaseItemImageInfos.SingleAsync(entry => entry.ItemId == itemId, cancellationToken);
        Assert.Equal(originalImageId, image.Id);
        Assert.Equal("old/poster.jpg", image.Path);
        Assert.Equal("pt-br", image.Language);
    }

    [Fact]
    public async Task SaveImagesAsync_RetriesWholeTransactionAfterMariaDbLockWaitTimeout()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        const string path = "N:\\Movies\\Image lock retry (2026)";
        var initialItem = CreateSeries(itemId, "Image lock retry", 2026, path);
        initialItem.AddImage(CreateImage("old/poster.jpg", "pt-br"));
        new ItemPersistenceService(Contexts, CreateAppHost(), NullLogger<ItemPersistenceService>.Instance)
            .SaveItems([initialItem], cancellationToken);

        var previousTimeout = await SetMariaDbLockWaitTimeoutAsync(1, cancellationToken);
        try
        {
            var lockContext = await Contexts.CreateDbContextAsync(cancellationToken);
            await using (lockContext.ConfigureAwait(false))
            {
                await lockContext.Database.OpenConnectionAsync(cancellationToken);
                await using var lockTransaction = await lockContext.Database
                    .BeginTransactionAsync(cancellationToken);
                _ = await lockContext.BaseItemImageInfos
                    .FromSqlInterpolated($"SELECT * FROM `BaseItemImageInfos` WHERE `ItemId` = {itemId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

                var retryLogger = new ImageRetryLogger();
                var persistence = new ItemPersistenceService(Contexts, CreateAppHost(), retryLogger);
                var replacement = CreateSeries(itemId, "Image lock retry", 2026, path);
                replacement.AddImage(CreateImage("new/poster.jpg", "pt-br"));
                var saveTask = persistence.SaveImagesAsync(replacement, cancellationToken);

                Exception? retrySignalError = null;
                try
                {
                    await retryLogger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken)
                        ;
                }
                catch (Exception exception)
                {
                    retrySignalError = exception;
                }
                finally
                {
                    await lockTransaction.RollbackAsync(CancellationToken.None);
                }

                if (retrySignalError is not null)
                {
                    try
                    {
                        await saveTask;
                    }
                    catch
                    {
                        // The retry-signal assertion below is the primary failure for this path.
                    }

                    Assert.Fail($"SaveImagesAsync did not enter the whole-operation retry path: {retrySignalError.Message}");
                }

                await saveTask;
                Assert.Equal(1, retryLogger.RetryCount);
            }

            await using var verify = await Contexts.CreateDbContextAsync(cancellationToken);
            var image = await verify.BaseItemImageInfos.SingleAsync(entry => entry.ItemId == itemId, cancellationToken);
            Assert.Equal("new/poster.jpg", image.Path);
            Assert.Equal("pt-br", image.Language);
        }
        finally
        {
            _ = await SetMariaDbLockWaitTimeoutAsync(previousTimeout, CancellationToken.None);
        }
    }

    [Fact]
    public async Task SaveUserDataAsync_RollsBackUpdatedKeysWhenInsertFails()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string providerKey = "tvdb-userdata-rollback";
        var item = CreateSeries(Guid.NewGuid(), "Rollback de progresso", 2026, "N:\\Series\\Rollback de progresso");
        item.SetProviderId("Tvdb", providerKey);
        item.SetProviderId("Imdb", "imdb-userdata-rollback");
        new ItemPersistenceService(Contexts, CreateAppHost(), NullLogger<ItemPersistenceService>.Instance)
            .SaveItems([item], cancellationToken);

        var userId = Guid.NewGuid();
        var username = "userdata-rollback-" + userId.ToString("N", CultureInfo.InvariantCulture);
        await using (var seedUsers = await UserContexts.CreateDbContextAsync(cancellationToken))
        {
            var seededUser = new MulletaFlix.Database.Implementations.Entities.User(username, "test-authentication", "test-password-reset");
            seededUser.Id = userId;
            seedUsers.Users.Add(seededUser);
            await seedUsers.SaveChangesAsync(cancellationToken);
        }

        MulletaFlix.Database.Implementations.Entities.User user;
        await using (var users = await UserContexts.CreateDbContextAsync(cancellationToken))
        {
            user = await users.Users.SingleAsync(entry => entry.Id == userId, cancellationToken);
        }

        await using (var context = await Contexts.CreateDbContextAsync(cancellationToken))
        {
            context.UserData.Add(new UserData
            {
                ItemId = item.Id,
                UserId = user.Id,
                CustomDataKey = providerKey,
                PlayCount = 2,
                Played = false,
                IsFavorite = false,
                Item = null,
                User = null
            });
            context.UserData.Add(new UserData
            {
                ItemId = item.Id,
                UserId = user.Id,
                CustomDataKey = "imdb-userdata-rollback",
                PlayCount = 2,
                Played = false,
                IsFavorite = false,
                Item = null,
                User = null
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        var userDataManager = new UserDataManager(
            _services!.GetRequiredService<IServerConfigurationManager>(),
            Contexts);
        var savedEventCount = 0;
        userDataManager.UserDataSaved += (_, _) => savedEventCount++;

        var request = userDataManager.GetUserDataBatch([item], user)[item.Id];
        request.Key = providerKey;
        request.PlayCount = 9;
        request.Played = true;
        request.IsFavorite = true;
        await AssertMariaDbInsertFailureAsync(
            "UserData",
            "TestUserDataReject",
            () => userDataManager.SaveUserDataAsync(
                user,
                item,
                request,
                UserDataSaveReason.PlaybackProgress,
                cancellationToken),
            cancellationToken);

        Assert.Equal(providerKey, request.Key);

        await using var verify = await Contexts.CreateDbContextAsync(cancellationToken);
        var savedRows = await verify.UserData
            .Where(entry => entry.ItemId == item.Id && entry.UserId == user.Id)
            .ToListAsync(cancellationToken);

        Assert.Equal(2, savedRows.Count);
        Assert.All(savedRows, savedRow =>
        {
            Assert.Contains(savedRow.CustomDataKey, new[] { providerKey, "imdb-userdata-rollback" });
            Assert.Equal(2, savedRow.PlayCount);
            Assert.False(savedRow.Played);
            Assert.False(savedRow.IsFavorite);
        });
        Assert.Equal(0, savedEventCount);
    }

    [Fact]
    public async Task SaveImagesAsync_ConcurrentWritesForSameItem_DoNotMixImageSets()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        const string itemPath = "N:\\Movies\\Concurrent image persistence (2026)";
        persistence.SaveItems([CreateSeries(itemId, "Concurrent image persistence", 2026, itemPath)], cancellationToken);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = Enumerable.Range(0, 20).Select(async index =>
        {
            var variant = index % 2 == 0 ? "variant-a" : "variant-b";
            var item = CreateSeries(itemId, "Concurrent image persistence", 2026, itemPath);
            item.AddImage(CreateImage($"{variant}/poster.jpg", variant));
            item.AddImage(CreateImage($"{variant}/backdrop.jpg", variant));
            await gate.Task;
            await persistence.SaveImagesAsync(item, cancellationToken);
        });

        gate.SetResult();
        await Task.WhenAll(writes);

        await using var context = await Contexts.CreateDbContextAsync(cancellationToken);
        var storedImages = await context.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .ToArrayAsync(cancellationToken);

        Assert.Equal(2, storedImages.Length);
        Assert.Single(storedImages.Select(image => image.Language).Distinct());
        Assert.Single(storedImages.Select(image => image.Path.Split('/')[0]).Distinct());
    }

    [Fact]
    public async Task SaveImagesAsync_UsesLatestImagesAfterWaitingForConcurrentSaveItems()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var initialPersistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        const string itemPath = "N:\\Movies\\Concurrent image refresh (2026)";
        var initialItem = CreateSeries(itemId, "Concurrent image refresh", 2026, itemPath);
        initialItem.AddImage(CreateImage("save-items/poster.jpg", "save-items"));
        initialItem.AddImage(CreateImage("save-items/backdrop.jpg", "save-items"));
        initialPersistence.SaveItems([initialItem], cancellationToken);

        var blockingFactory = new BlockingDbContextFactory(Contexts);
        var persistence = new ItemPersistenceService(
            blockingFactory,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        var saveItems = CreateSeries(itemId, "Concurrent image refresh", 2026, itemPath);
        saveItems.AddImage(CreateImage("save-items/poster.jpg", "save-items"));
        saveItems.AddImage(CreateImage("save-items/backdrop.jpg", "save-items"));
        var saveImages = CreateSeries(itemId, "Concurrent image refresh", 2026, itemPath);
        saveImages.AddImage(CreateImage("save-images/poster.jpg", "save-images"));
        saveImages.AddImage(CreateImage("save-images/backdrop.jpg", "save-images"));

        var saveItemsTask = Task.Run(() => persistence.SaveItems([saveItems], cancellationToken), cancellationToken);
        Task? saveImagesTask = null;
        var saveImagesReachedFactoryBeforeRelease = false;
        try
        {
            await blockingFactory.SynchronousCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            saveImagesTask = persistence.SaveImagesAsync(saveImages, cancellationToken);
            saveImages.AddImage(CreateImage("save-images/latest-after-wait.jpg", "save-images"));
            saveImagesReachedFactoryBeforeRelease = blockingFactory.AsynchronousCreateEntered.Task.IsCompleted;

            blockingFactory.ReleaseSynchronousCreate();
            await saveItemsTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            blockingFactory.ReleaseAsynchronousCreate();
            await saveImagesTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        finally
        {
            blockingFactory.ReleaseSynchronousCreate();
            blockingFactory.ReleaseAsynchronousCreate();
            await saveItemsTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (saveImagesTask is not null)
            {
                await saveImagesTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        Assert.False(
            saveImagesReachedFactoryBeforeRelease,
            "SaveImagesAsync must wait for SaveItems to release the same item lock before opening a database context.");

        await using var context = await Contexts.CreateDbContextAsync(cancellationToken);
        var storedImages = await context.BaseItemImageInfos
            .Where(image => image.ItemId == itemId)
            .ToArrayAsync(cancellationToken);

        var storedSet = storedImages
            .Select(image => (image.Path, image.Language))
            .OrderBy(image => image.Path)
            .ToArray();
        var expectedSet = new[]
        {
            ("save-images/backdrop.jpg", "save-images"),
            ("save-images/latest-after-wait.jpg", "save-images"),
            ("save-images/poster.jpg", "save-images")
        };
        Assert.Equal(expectedSet, storedSet);
    }

    [Fact]
    public async Task SaveItems_RescanningTrailerWithSameTypes_PreservesTheStoredTypeSet()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);

        var itemId = Guid.NewGuid();
        const string path = "N:\\Movies\\Example Trailer (2024).mkv";
        var initial = CreateTrailer(itemId, path, TrailerType.LocalTrailer, TrailerType.Archive);
        persistence.SaveItems([initial], CancellationToken.None);

        await using (var context = await Contexts.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE TestTrailerTypeWriteAudit (EventType VARCHAR(6) NOT NULL) ENGINE=InnoDB;");
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER TestTrailerTypeInsertAudit AFTER INSERT ON BaseItemTrailerTypes "
                + "FOR EACH ROW INSERT INTO TestTrailerTypeWriteAudit (EventType) VALUES ('INSERT');");
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER TestTrailerTypeDeleteAudit AFTER DELETE ON BaseItemTrailerTypes "
                + "FOR EACH ROW INSERT INTO TestTrailerTypeWriteAudit (EventType) VALUES ('DELETE');");
        }

        try
        {
            var reorderedRescan = CreateTrailer(itemId, path, TrailerType.Archive, TrailerType.LocalTrailer);
            persistence.SaveItems([reorderedRescan], CancellationToken.None);

            await using (var context = await Contexts.CreateDbContextAsync())
            {
                Assert.Equal(0, await CountTrailerTypeAuditWritesAsync(context));
                var storedTypeIds = await context.BaseItemTrailerTypes
                    .Where(e => e.ItemId == itemId)
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToArrayAsync();

                Assert.Equal(
                    new[] { (int)TrailerType.Archive, (int)TrailerType.LocalTrailer }.OrderBy(id => id),
                    storedTypeIds);
            }

            var changedRescan = CreateTrailer(itemId, path, TrailerType.Archive, TrailerType.ComingSoonToDvd);
            persistence.SaveItems([changedRescan], CancellationToken.None);

            await using (var context = await Contexts.CreateDbContextAsync())
            {
                Assert.True(await CountTrailerTypeAuditWritesAsync(context) > 0);
                var storedTypeIds = await context.BaseItemTrailerTypes
                    .Where(e => e.ItemId == itemId)
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToArrayAsync();

                Assert.Equal(
                    new[] { (int)TrailerType.Archive, (int)TrailerType.ComingSoonToDvd }.OrderBy(id => id),
                    storedTypeIds);
            }
        }
        finally
        {
            await using var context = await Contexts.CreateDbContextAsync();
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TestTrailerTypeInsertAudit;");
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TestTrailerTypeDeleteAudit;");
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS TestTrailerTypeWriteAudit;");
        }
    }

    [Fact]
    public async Task SaveMediaAttachments_ReplacesExistingSetAndEmptyInputClearsIt()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Anexos de teste", 2026, "N:\\Series\\Anexos de teste")], CancellationToken.None);

        var repository = new MediaAttachmentRepository(Contexts);
        repository.SaveMediaAttachments(
            itemId,
            [new MediaAttachment { Index = 1, FileName = "old.bin", MimeType = "application/old" }],
            CancellationToken.None);

        await using (var context = await Contexts.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER TestMediaAttachmentReject BEFORE INSERT ON AttachmentStreamInfos "
                + "FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced integration-test failure';",
                cancellationToken);
        }

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveMediaAttachmentsAsync(
                itemId,
                [new MediaAttachment { Index = 2, FileName = "force-failure.bin", MimeType = "application/octet-stream" }],
                cancellationToken));
        }
        finally
        {
            using var context = Contexts.CreateDbContext();
            context.Database.ExecuteSqlRaw("DROP TRIGGER IF EXISTS TestMediaAttachmentReject;");
        }

        var afterFailedReplace = repository.GetMediaAttachments(new MediaAttachmentQuery { ItemId = itemId });
        Assert.Collection(
            afterFailedReplace,
            attachment =>
            {
                Assert.Equal(1, attachment.Index);
                Assert.Equal("old.bin", attachment.FileName);
            });

        await repository.SaveMediaAttachmentsAsync(
            itemId,
            [
                new MediaAttachment { Index = 2, FileName = "cover.bin", MimeType = "application/cover" },
                new MediaAttachment { Index = 3, FileName = "font.ttf", MimeType = "font/ttf" }
            ],
            cancellationToken);

        var saved = repository.GetMediaAttachments(new MediaAttachmentQuery { ItemId = itemId })
            .OrderBy(attachment => attachment.Index)
            .Select(attachment => (attachment.Index, attachment.FileName, attachment.MimeType));
        Assert.Equal(
            new[] { (2, "cover.bin", "application/cover"), (3, "font.ttf", "font/ttf") },
            saved);

        repository.SaveMediaAttachments(itemId, [], CancellationToken.None);
        Assert.Empty(repository.GetMediaAttachments(new MediaAttachmentQuery { ItemId = itemId }));
    }

    [Fact]
    public async Task SaveMediaStreamsAsync_RollsBackFailedReplacement()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Streams de teste", 2026, "N:\\Series\\Streams de teste")], CancellationToken.None);

        var retryLogger = new MediaStreamRetryLogger();
        var repository = new MediaStreamRepository(Contexts, CreateAppHost(), Mock.Of<ILocalizationManager>(), retryLogger);
        await repository.SaveMediaStreamsAsync(
            itemId,
            [new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" }],
            cancellationToken);

        await AssertMariaDbInsertFailureAsync(
            "MediaStreamInfos",
            "TestMediaStreamReject",
            () => repository.SaveMediaStreamsAsync(
                itemId,
                [new MediaStream { Index = 1, Type = MediaStreamType.Video, Codec = "hevc" }],
                cancellationToken),
            cancellationToken);
        Assert.Equal(0, retryLogger.RetryCount);

        var afterFailedReplace = repository.GetMediaStreams(new MediaStreamQuery { ItemId = itemId });
        var previous = Assert.Single(afterFailedReplace);
        Assert.Equal(0, previous.Index);
        Assert.Equal("h264", previous.Codec);

        await repository.SaveMediaStreamsAsync(
            itemId,
            [new MediaStream { Index = 1, Type = MediaStreamType.Video, Codec = "hevc" }],
            cancellationToken);
        var saved = Assert.Single(repository.GetMediaStreams(new MediaStreamQuery { ItemId = itemId }));
        Assert.Equal(1, saved.Index);
        Assert.Equal("hevc", saved.Codec);
    }

    [Fact]
    public async Task SaveMediaStreamsAsync_RetriesWholeTransactionAfterMariaDbLockWaitTimeout()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Retry de streams", 2026, "N:\\Series\\Retry de streams")], CancellationToken.None);

        var repository = new MediaStreamRepository(
            Contexts,
            CreateAppHost(),
            Mock.Of<ILocalizationManager>(),
            NullLogger<MediaStreamRepository>.Instance);
        await repository.SaveMediaStreamsAsync(
            itemId,
            [new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" }],
            cancellationToken);

        var retryLogger = new MediaStreamRetryLogger();
        repository = new MediaStreamRepository(Contexts, CreateAppHost(), Mock.Of<ILocalizationManager>(), retryLogger);
        var previousTimeout = await SetMariaDbLockWaitTimeoutAsync(1, cancellationToken);
        try
        {
            var lockContext = await Contexts.CreateDbContextAsync(cancellationToken);
            await using (lockContext.ConfigureAwait(false))
            {
                await lockContext.Database.OpenConnectionAsync(cancellationToken);
                await using var lockTransaction = await lockContext.Database
                    .BeginTransactionAsync(cancellationToken);
                _ = await lockContext.MediaStreamInfos
                    .FromSqlInterpolated($"SELECT * FROM `MediaStreamInfos` WHERE `ItemId` = {itemId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

                var saveTask = repository.SaveMediaStreamsAsync(
                    itemId,
                    [new MediaStream { Index = 1, Type = MediaStreamType.Video, Codec = "hevc" }],
                    cancellationToken);
                Exception? retrySignalError = null;
                try
                {
                    await retryLogger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (Exception exception)
                {
                    retrySignalError = exception;
                }
                finally
                {
                    await lockTransaction.RollbackAsync(CancellationToken.None);
                }

                if (retrySignalError is not null)
                {
                    try
                    {
                        await saveTask;
                    }
                    catch
                    {
                        // The retry-signal assertion below is the primary failure for this path.
                    }

                    Assert.Fail($"SaveMediaStreamsAsync did not enter the whole-operation retry path: {retrySignalError.Message}");
                }

                await saveTask;
                Assert.Equal(1, retryLogger.RetryCount);
            }
        }
        finally
        {
            _ = await SetMariaDbLockWaitTimeoutAsync(previousTimeout, CancellationToken.None);
        }

        var saved = Assert.Single(repository.GetMediaStreams(new MediaStreamQuery { ItemId = itemId }));
        Assert.Equal(1, saved.Index);
        Assert.Equal("hevc", saved.Codec);
    }

    [Fact]
    public async Task SaveChaptersAsync_RollsBackFailedReplacement()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Capítulos de teste", 2026, "N:\\Series\\Capítulos de teste")], CancellationToken.None);

        var repository = new ChapterRepository(Contexts, Mock.Of<IImageProcessor>());
        await repository.SaveChaptersAsync(
            itemId,
            [new ChapterInfo { StartPositionTicks = 10, Name = "Capítulo antigo" }],
            cancellationToken);

        await AssertMariaDbInsertFailureAsync(
            "Chapters",
            "TestChapterReject",
            () => repository.SaveChaptersAsync(
                itemId,
                [new ChapterInfo { StartPositionTicks = 20, Name = "Capítulo novo" }],
                cancellationToken),
            cancellationToken);

        var previous = Assert.Single(repository.GetChapters(itemId));
        Assert.Equal(10, previous.StartPositionTicks);
        Assert.Equal("Capítulo antigo", previous.Name);

        await repository.SaveChaptersAsync(
            itemId,
            [new ChapterInfo { StartPositionTicks = 20, Name = "Capítulo novo" }],
            cancellationToken);
        var saved = Assert.Single(repository.GetChapters(itemId));
        Assert.Equal(20, saved.StartPositionTicks);
        Assert.Equal("Capítulo novo", saved.Name);
    }

    [Fact]
    public async Task SaveChaptersAsync_RetriesWholeTransactionAfterMariaDbLockWaitTimeout()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Retry de capítulos", 2026, "N:\\Series\\Retry de capítulos")], CancellationToken.None);

        var repository = new ChapterRepository(Contexts, Mock.Of<IImageProcessor>());
        await repository.SaveChaptersAsync(
            itemId,
            [new ChapterInfo { StartPositionTicks = 10, Name = "Capítulo antigo" }],
            cancellationToken);

        var retryLogger = new ChapterRetryLogger();
        repository = new ChapterRepository(Contexts, Mock.Of<IImageProcessor>(), retryLogger);
        var previousTimeout = await SetMariaDbLockWaitTimeoutAsync(1, cancellationToken);
        try
        {
            var lockContext = await Contexts.CreateDbContextAsync(cancellationToken);
            await using (lockContext.ConfigureAwait(false))
            {
                await lockContext.Database.OpenConnectionAsync(cancellationToken);
                await using var lockTransaction = await lockContext.Database.BeginTransactionAsync(cancellationToken);
                _ = await lockContext.Chapters
                    .FromSqlInterpolated($"SELECT * FROM `Chapters` WHERE `ItemId` = {itemId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

                var saveTask = repository.SaveChaptersAsync(
                    itemId,
                    [new ChapterInfo { StartPositionTicks = 20, Name = "Capítulo novo" }],
                    cancellationToken);
                Exception? retrySignalError = null;
                try
                {
                    await retryLogger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (Exception exception)
                {
                    retrySignalError = exception;
                }
                finally
                {
                    await lockTransaction.RollbackAsync(CancellationToken.None);
                }

                if (retrySignalError is not null)
                {
                    try
                    {
                        await saveTask;
                    }
                    catch
                    {
                        // The retry-signal assertion below is the primary failure for this path.
                    }

                    Assert.Fail($"SaveChaptersAsync did not enter the whole-operation retry path: {retrySignalError.Message}");
                }

                await saveTask;
                Assert.Equal(1, retryLogger.RetryCount);
            }
        }
        finally
        {
            _ = await SetMariaDbLockWaitTimeoutAsync(previousTimeout, CancellationToken.None);
        }

        var saved = Assert.Single(repository.GetChapters(itemId));
        Assert.Equal(20, saved.StartPositionTicks);
        Assert.Equal("Capítulo novo", saved.Name);
    }

    [Fact]
    public async Task SaveChapters_RetriesWholeTransactionAfterMariaDbLockWaitTimeout()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Retry síncrono de capítulos", 2026, "N:\\Series\\Retry síncrono de capítulos")], CancellationToken.None);

        var repository = new ChapterRepository(Contexts, Mock.Of<IImageProcessor>());
        repository.SaveChapters(itemId, [new ChapterInfo { StartPositionTicks = 10, Name = "Capítulo antigo" }]);

        var retryLogger = new ChapterRetryLogger();
        repository = new ChapterRepository(Contexts, Mock.Of<IImageProcessor>(), retryLogger);
        var previousTimeout = await SetMariaDbLockWaitTimeoutAsync(1, cancellationToken);
        try
        {
            var lockContext = await Contexts.CreateDbContextAsync(cancellationToken);
            await using (lockContext.ConfigureAwait(false))
            {
                await lockContext.Database.OpenConnectionAsync(cancellationToken);
                await using var lockTransaction = await lockContext.Database.BeginTransactionAsync(cancellationToken);
                _ = await lockContext.Chapters
                    .FromSqlInterpolated($"SELECT * FROM `Chapters` WHERE `ItemId` = {itemId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

                var saveTask = Task.Run(
                    () => repository.SaveChapters(itemId, [new ChapterInfo { StartPositionTicks = 20, Name = "Capítulo novo" }]),
                    cancellationToken);
                Exception? retrySignalError = null;
                try
                {
                    await retryLogger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (Exception exception)
                {
                    retrySignalError = exception;
                }
                finally
                {
                    await lockTransaction.RollbackAsync(CancellationToken.None);
                }

                if (retrySignalError is not null)
                {
                    try
                    {
                        await saveTask;
                    }
                    catch
                    {
                        // The retry-signal assertion below is the primary failure for this path.
                    }

                    Assert.Fail($"SaveChapters did not enter the whole-operation retry path: {retrySignalError.Message}");
                }

                await saveTask;
                Assert.Equal(1, retryLogger.RetryCount);
            }
        }
        finally
        {
            _ = await SetMariaDbLockWaitTimeoutAsync(previousTimeout, CancellationToken.None);
        }

        var saved = Assert.Single(repository.GetChapters(itemId));
        Assert.Equal(20, saved.StartPositionTicks);
        Assert.Equal("Capítulo novo", saved.Name);
    }

    [Fact]
    public async Task SaveKeyframeDataAsync_RetriesWholeTransactionAfterMariaDbLockWaitTimeout()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Retry de keyframes", 2026, "N:\\Series\\Retry de keyframes")], CancellationToken.None);

        var repository = new KeyframeRepository(Contexts);
        await repository.SaveKeyframeDataAsync(
            itemId,
            new MulletaFlix.MediaEncoding.Keyframes.KeyframeData(100, [10, 20]),
            cancellationToken);

        var retryLogger = new KeyframeRetryLogger();
        repository = new KeyframeRepository(Contexts, retryLogger);
        var previousTimeout = await SetMariaDbLockWaitTimeoutAsync(1, cancellationToken);
        try
        {
            var lockContext = await Contexts.CreateDbContextAsync(cancellationToken);
            await using (lockContext.ConfigureAwait(false))
            {
                await lockContext.Database.OpenConnectionAsync(cancellationToken);
                await using var lockTransaction = await lockContext.Database.BeginTransactionAsync(cancellationToken);
                _ = await lockContext.KeyframeData
                    .FromSqlInterpolated($"SELECT * FROM `KeyframeData` WHERE `ItemId` = {itemId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

                var saveTask = repository.SaveKeyframeDataAsync(
                    itemId,
                    new MulletaFlix.MediaEncoding.Keyframes.KeyframeData(200, [30, 40]),
                    cancellationToken);
                Exception? retrySignalError = null;
                try
                {
                    await retryLogger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (Exception exception)
                {
                    retrySignalError = exception;
                }
                finally
                {
                    await lockTransaction.RollbackAsync(CancellationToken.None);
                }

                if (retrySignalError is not null)
                {
                    try
                    {
                        await saveTask;
                    }
                    catch
                    {
                        // The retry-signal assertion below is the primary failure for this path.
                    }

                    Assert.Fail($"SaveKeyframeDataAsync did not enter the whole-operation retry path: {retrySignalError.Message}");
                }

                await saveTask;
                Assert.Equal(1, retryLogger.RetryCount);
            }
        }
        finally
        {
            _ = await SetMariaDbLockWaitTimeoutAsync(previousTimeout, CancellationToken.None);
        }

        var saved = Assert.Single(repository.GetKeyframeData(itemId));
        Assert.Equal(200, saved.TotalDuration);
        Assert.Equal(new long[] { 30, 40 }, saved.KeyframeTicks);
    }

    [Fact]
    public async Task SaveKeyframeDataAsync_RollsBackFailedReplacement()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemId = Guid.NewGuid();
        var persistence = new ItemPersistenceService(
            Contexts,
            CreateAppHost(),
            NullLogger<ItemPersistenceService>.Instance);
        persistence.SaveItems([CreateSeries(itemId, "Keyframes de teste", 2026, "N:\\Series\\Keyframes de teste")], CancellationToken.None);

        var repository = new KeyframeRepository(Contexts);
        await repository.SaveKeyframeDataAsync(
            itemId,
            new MulletaFlix.MediaEncoding.Keyframes.KeyframeData(100, [10, 20]),
            cancellationToken);

        await AssertMariaDbInsertFailureAsync(
            "KeyframeData",
            "TestKeyframeReject",
            () => repository.SaveKeyframeDataAsync(
                itemId,
                new MulletaFlix.MediaEncoding.Keyframes.KeyframeData(200, [30, 40]),
                cancellationToken),
            cancellationToken);

        var previous = Assert.Single(repository.GetKeyframeData(itemId));
        Assert.Equal(100, previous.TotalDuration);
        Assert.Equal(new long[] { 10, 20 }, previous.KeyframeTicks);

        await repository.SaveKeyframeDataAsync(
            itemId,
            new MulletaFlix.MediaEncoding.Keyframes.KeyframeData(200, [30, 40]),
            cancellationToken);
        var saved = Assert.Single(repository.GetKeyframeData(itemId));
        Assert.Equal(200, saved.TotalDuration);
        Assert.Equal(new long[] { 30, 40 }, saved.KeyframeTicks);
    }

    private async Task AssertMariaDbInsertFailureAsync(
        string tableName,
        string triggerName,
        Func<Task> saveAction,
        CancellationToken cancellationToken)
    {
        await using (var context = await Contexts.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"CREATE TRIGGER `{triggerName}` BEFORE INSERT ON `{tableName}` "
                + "FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced integration-test failure';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(saveAction);
        }
        finally
        {
            using var context = Contexts.CreateDbContext();
            context.Database.OpenConnection();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"DROP TRIGGER IF EXISTS `{triggerName}`;";
            command.ExecuteNonQuery();
        }
    }

    private static async Task<int> CountTrailerTypeAuditWritesAsync(MulletaFlixDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTrailerTypeWriteAudit;";
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static Trailer CreateTrailer(Guid id, string path, params TrailerType[] trailerTypes)
    {
        return new Trailer
        {
            Id = id,
            Name = "Example Trailer (2024)",
            Path = path,
            DateCreated = DateTime.UtcNow,
            TrailerTypes = trailerTypes
        };
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

    private static ItemImageInfo CreateImage(string path, string language)
    {
        return new ItemImageInfo
        {
            Path = path,
            Type = ImageType.Primary,
            DateModified = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ProviderName = "TheMovieDb",
            Language = language
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

    private static void DropTestDatabase()
    {
        using var connection = new MySqlConnector.MySqlConnection(MariaDbTestEnvironment.GetAdminConnection().ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{TestDatabase}`;";
        command.ExecuteNonQuery();
    }

    private static ServiceProvider BuildServices(MySqlConnector.MySqlConnectionStringBuilder adminConnection)
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
                    new CustomDatabaseOption { Key = "server", Value = adminConnection.Server },
                    new CustomDatabaseOption { Key = "port", Value = adminConnection.Port.ToString(CultureInfo.InvariantCulture) },
                    new CustomDatabaseOption { Key = "user", Value = adminConnection.UserID },
                    new CustomDatabaseOption { Key = "password", Value = adminConnection.Password },

                    // Keeps the tests off the server's own schema. Without this the provider defaults
                    // to DatabaseNames.Main, which is the live database.
                    new CustomDatabaseOption { Key = "database", Value = TestDatabase },
                ],
            },
        };

        var configurationManager = new Mock<IServerConfigurationManager>();
        var applicationPaths = new Mock<IServerApplicationPaths>();
        applicationPaths.Setup(paths => paths.ProgramDataPath).Returns(Path.GetTempPath());
        configurationManager.Setup(manager => manager.ApplicationPaths).Returns(applicationPaths.Object);
        configurationManager.Setup(manager => manager.GetConfiguration("database")).Returns(configuration);
        configurationManager.Setup(manager => manager.Configuration).Returns(new ServerConfiguration());
        services.AddSingleton(configurationManager.Object);

        services.AddMulletaFlixDbContext(
            configurationManager.Object,
            new ConfigurationBuilder().Build());

        return services.BuildServiceProvider();
    }

    private async Task<int> SetMariaDbLockWaitTimeoutAsync(int seconds, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(seconds, 1);
        await using var context = await Contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The isolated MariaDB test connection is not configured.");
        await using var connection = new MySqlConnector.MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var getTimeout = new MySqlConnector.MySqlCommand(
            "SELECT @@GLOBAL.innodb_lock_wait_timeout;",
            connection);
        var previous = Convert.ToInt32(
            await getTimeout.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);

        await using var setTimeout = new MySqlConnector.MySqlCommand(
            "SET GLOBAL innodb_lock_wait_timeout = @seconds;",
            connection);
        setTimeout.Parameters.AddWithValue("@seconds", seconds);
        await setTimeout.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await connection.CloseAsync().ConfigureAwait(false);

        // New physical sessions inherit the updated server value. Clear only this isolated test
        // connection pool, never unrelated MariaDB pools in the test runner.
        MySqlConnector.MySqlConnection.ClearPool(connection);
        return previous;
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

    private sealed class ImageRetryLogger : ILogger<ItemPersistenceService>
    {
        private readonly TaskCompletionSource _retryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _retryCount;

        public Task RetryObserved => _retryObserved.Task;

        public int RetryCount => Volatile.Read(ref _retryCount);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning
                && formatter(state, exception).Contains("Transient database failure while saving images", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _retryCount);
                _retryObserved.TrySetResult();
            }
        }
    }

    private sealed class MediaStreamRetryLogger : ILogger<MediaStreamRepository>
    {
        private readonly TaskCompletionSource _retryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _retryCount;

        public Task RetryObserved => _retryObserved.Task;

        public int RetryCount => Volatile.Read(ref _retryCount);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning
                && formatter(state, exception).Contains("Transient MariaDB lock failure while saving media streams", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _retryCount);
                _retryObserved.TrySetResult();
            }
        }
    }

    private sealed class ChapterRetryLogger : ILogger<ChapterRepository>
    {
        private readonly TaskCompletionSource _retryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _retryCount;

        public Task RetryObserved => _retryObserved.Task;

        public int RetryCount => Volatile.Read(ref _retryCount);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning
                && formatter(state, exception).Contains("Transient MariaDB lock failure while saving chapters", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _retryCount);
                _retryObserved.TrySetResult();
            }
        }
    }

    private sealed class KeyframeRetryLogger : ILogger<KeyframeRepository>
    {
        private readonly TaskCompletionSource _retryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _retryCount;

        public Task RetryObserved => _retryObserved.Task;

        public int RetryCount => Volatile.Read(ref _retryCount);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning
                && formatter(state, exception).Contains("Transient MariaDB lock failure while saving keyframe data", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _retryCount);
                _retryObserved.TrySetResult();
            }
        }
    }

    private sealed class PeopleRetryLogger : ILogger<PeopleRepository>
    {
        private readonly TaskCompletionSource _retryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _retryCount;

        public Task RetryObserved => _retryObserved.Task;

        public int RetryCount => Volatile.Read(ref _retryCount);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning
                && formatter(state, exception).Contains("Transient MariaDB lock failure while saving people", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _retryCount);
                _retryObserved.TrySetResult();
            }
        }
    }

    private sealed class BlockingDbContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        private readonly IDbContextFactory<MulletaFlixDbContext> _inner;
        private readonly TaskCompletionSource _allowSynchronousCreate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allowAsynchronousCreate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDbContextFactory(IDbContextFactory<MulletaFlixDbContext> inner)
        {
            _inner = inner;
        }

        public TaskCompletionSource SynchronousCreateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AsynchronousCreateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MulletaFlixDbContext CreateDbContext()
        {
            SynchronousCreateEntered.TrySetResult();
            if (!_allowSynchronousCreate.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("The test did not release the SaveItems context factory.");
            }

            return _inner.CreateDbContext();
        }

        public async Task<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            AsynchronousCreateEntered.TrySetResult();
            await _allowAsynchronousCreate.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return await _inner.CreateDbContextAsync(cancellationToken);
        }

        public void ReleaseSynchronousCreate() => _allowSynchronousCreate.TrySetResult();

        public void ReleaseAsynchronousCreate() => _allowAsynchronousCreate.TrySetResult();
    }
}
