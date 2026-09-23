// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Extensions;
using MulletaFlix.Server.Implementations.Item;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

    private static readonly string ConnectionString =
        "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharSet=utf8mb4;Connection Timeout=5;";

    private ServiceProvider? _services;

    private IDbContextFactory<MulletaFlixDbContext> Contexts
        => _services!.GetRequiredService<IDbContextFactory<MulletaFlixDbContext>>();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        DropTestDatabase();
        _services = BuildServices();

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
        using var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{TestDatabase}`;";
        command.ExecuteNonQuery();
    }

    private static ServiceProvider BuildServices()
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
                    new CustomDatabaseOption { Key = "server", Value = "127.0.0.1" },
                    new CustomDatabaseOption { Key = "port", Value = "3306" },
                    new CustomDatabaseOption { Key = "user", Value = "root" },
                    new CustomDatabaseOption { Key = "password", Value = string.Empty },

                    // Keeps the tests off the server's own schema. Without this the provider defaults
                    // to DatabaseNames.Main, which is the live database.
                    new CustomDatabaseOption { Key = "database", Value = TestDatabase },
                ],
            },
        };

        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("database")).Returns(configuration);
        configurationManager.Setup(manager => manager.Configuration).Returns(new ServerConfiguration());
        services.AddSingleton(configurationManager.Object);

        services.AddMulletaFlixDbContext(
            configurationManager.Object,
            new ConfigurationBuilder().Build());

        return services.BuildServiceProvider();
    }
}
