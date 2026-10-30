using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MySqlConnector;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MulletaFlix.Testing;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public sealed class BaseItemRepositoryLatestTvShowTests : IDisposable
{
    private readonly MulletaFlixDbContext _context;
    private readonly BaseItemRepository _repository;

    public BaseItemRepositoryLatestTvShowTests()
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var dbProvider = new Mock<IMulletaFlixDatabaseProvider>();
        dbProvider.Setup(p => p.OnModelCreating(It.IsAny<ModelBuilder>()));

        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        lockingBehavior.Setup(l => l.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>(static (_, action) => action());
        lockingBehavior.Setup(l => l.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
            .Callback<MulletaFlixDbContext, Func<Task>>(static (_, func) => func());

        _context = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            dbProvider.Object,
            lockingBehavior.Object);

        _context.Database.EnsureCreated();

        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory.Setup(f => f.CreateDbContext()).Returns(_context);
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_context);

        _repository = CreateRepository(dbFactory.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void GetLatestItemList_TvShows_ReturnsSeriesContainersInLatestOrder()
    {
        var now = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);

        var seriesA = CreateSeries("Series A");
        var seasonA1 = CreateSeason(seriesA, "Series A - Season 1");
        var seasonA2 = CreateSeason(seriesA, "Series A - Season 2");
        var seriesAEpisodes = new[]
        {
            CreateEpisode("Series A", seriesA, seasonA1, now.AddHours(-1)),
            CreateEpisode("Series A", seriesA, seasonA1, now.AddHours(-2)),
            CreateEpisode("Series A", seriesA, seasonA1, now.AddDays(-3))
        };

        var seriesB = CreateSeries("Series B");
        var seasonB1 = CreateSeason(seriesB, "Series B - Season 1");
        var seriesBEpisodes = new[]
        {
            CreateEpisode("Series B", seriesB, seasonB1, now.AddHours(-3)),
            CreateEpisode("Series B", seriesB, seasonB1, now.AddHours(-4)),
            CreateEpisode("Series B", seriesB, seasonB1, now.AddDays(-3))
        };

        var seriesC = CreateSeries("Series C");
        var seasonC1 = CreateSeason(seriesC, "Series C - Season 1");
        var seriesCEpisodes = new[]
        {
            CreateEpisode("Series C", seriesC, seasonC1, now.AddHours(-5)),
            CreateEpisode("Series C", seriesC, seasonC1, now.AddDays(-3)),
            CreateEpisode("Series C", seriesC, seasonC1, now.AddDays(-4))
        };

        _context.BaseItems.AddRange(
            seriesA,
            seasonA1,
            seasonA2,
            seriesAEpisodes[0],
            seriesAEpisodes[1],
            seriesAEpisodes[2],
            seriesB,
            seasonB1,
            seriesBEpisodes[0],
            seriesBEpisodes[1],
            seriesBEpisodes[2],
            seriesC,
            seasonC1,
            seriesCEpisodes[0],
            seriesCEpisodes[1],
            seriesCEpisodes[2]);
        _context.SaveChanges();

        var result = _repository.GetLatestItemList(
            new InternalItemsQuery
            {
                Limit = 3
            },
            CollectionType.tvshows);

        Assert.Equal(3, result.Count);
        Assert.IsType<Series>(result[1]);
        Assert.IsType<Series>(result[0]);
        Assert.IsType<Series>(result[2]);
        Assert.Equal(seriesA.Id, result[0].Id);
        Assert.Equal(seriesB.Id, result[1].Id);
        Assert.Equal(seriesC.Id, result[2].Id);
    }

    [Fact]
    public void GetLatestItemList_TvShows_DoesNotMergeDifferentSeriesWithTheSameName()
    {
        var now = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);

        var sharedSeriesName = "Shared Series";
        var seriesA = CreateSeries(sharedSeriesName);
        var seasonA = CreateSeason(seriesA, $"{sharedSeriesName} - Season 1");
        var episodeA = CreateEpisode(sharedSeriesName, seriesA, seasonA, now.AddHours(-1));

        var seriesB = CreateSeries(sharedSeriesName);
        var seasonB = CreateSeason(seriesB, $"{sharedSeriesName} - Season 1");
        var episodeB = CreateEpisode(sharedSeriesName, seriesB, seasonB, now.AddHours(-2));

        _context.BaseItems.AddRange(
            seriesA,
            seasonA,
            episodeA,
            seriesB,
            seasonB,
            episodeB);
        _context.SaveChanges();

        var result = _repository.GetLatestItemList(
            new InternalItemsQuery
            {
                Limit = 2
            },
            CollectionType.tvshows);

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.IsType<Series>(item));
        Assert.Contains(result, item => item.Id == seriesA.Id);
        Assert.Contains(result, item => item.Id == seriesB.Id);
    }

    [Fact]
    public void GetLatestItemList_Movies_FillsLimitAfterSkippingDuplicatePresentationKeys()
    {
        var now = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);
        var candidates = new List<BaseItemEntity>();

        // A library can contain many cuts/qualities of the same movie. Ensure a run of
        // duplicates ahead of the limit does not make the latest shelf return too few cards.
        for (var i = 0; i < 12; i++)
        {
            candidates.Add(CreateMovie("Movie A", "movie-a", now.AddMinutes(-i)));
        }

        candidates.Add(CreateMovie("Movie B", "movie-b", now.AddHours(-1)));
        candidates.Add(CreateMovie("Movie C", "movie-c", now.AddHours(-2)));

        _context.BaseItems.AddRange(candidates);
        _context.SaveChanges();

        var result = _repository.GetLatestItemList(
            new InternalItemsQuery
            {
                Limit = 3
            },
            CollectionType.movies);

        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.IsType<Movie>(item));
        Assert.Equal(
            new[] { "movie-a", "movie-b", "movie-c" },
            result.Select(item => item.PresentationUniqueKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetLatestItemList_Movies_ReturnsNoItemsForNonPositiveLimit(int limit)
    {
        _context.BaseItems.Add(CreateMovie("Movie A", "movie-a", DateTime.UtcNow));
        _context.SaveChanges();

        var result = _repository.GetLatestItemList(
            new InternalItemsQuery
            {
                Limit = limit
            },
            CollectionType.movies);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLatestItemList_Music_ReturnsAlbumsWithMatchingTracksOnly()
    {
        var matchingAlbum = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(MusicAlbum).ToString(),
            Name = "Matching Album",
            DateCreated = DateTime.UtcNow,
            IsFolder = true
        };
        var unrelatedAlbum = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(MusicAlbum).ToString(),
            Name = "Unrelated Album",
            DateCreated = DateTime.UtcNow.AddMinutes(-1),
            IsFolder = true
        };
        var matchingTrack = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Audio).ToString(),
            Name = "Matching Track",
            IsFolder = false
        };
        var unrelatedTrack = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Audio).ToString(),
            Name = "Unrelated Track",
            IsFolder = false
        };

        _context.BaseItems.AddRange(matchingAlbum, unrelatedAlbum, matchingTrack, unrelatedTrack);
        _context.AncestorIds.Add(new AncestorId
        {
            ItemId = matchingTrack.Id,
            ParentItemId = matchingAlbum.Id,
            Item = matchingTrack,
            ParentItem = matchingAlbum
        });
        _context.AncestorIds.Add(new AncestorId
        {
            ItemId = unrelatedTrack.Id,
            ParentItemId = unrelatedAlbum.Id,
            Item = unrelatedTrack,
            ParentItem = unrelatedAlbum
        });
        _context.SaveChanges();

        var result = _repository.GetLatestItemList(
            new InternalItemsQuery
            {
                Limit = 1,
                ItemIds = [matchingTrack.Id]
            },
            CollectionType.music);

        var album = Assert.Single(result);
        Assert.Equal(matchingAlbum.Id, album.Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GetLatestItemList_Music_UsesMariaDbSubqueryForMatchingAlbums()
    {
        using var database = new IsolatedMariaDbDatabase();
        using (var context = database.CreateDbContext())
        {
            context.Database.EnsureCreated();
            AddMatchingMusicData(context, database.MatchingAlbumId, database.MatchingTrackId);
        }

        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(database.CreateDbContext);
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(database.CreateDbContext);

        var result = CreateRepository(factory.Object).GetLatestItemList(
            new InternalItemsQuery
            {
                Limit = 1,
                ItemIds = [database.MatchingTrackId]
            },
            CollectionType.music);

        var album = Assert.Single(result);
        Assert.Equal(database.MatchingAlbumId, album.Id);
    }

    private static BaseItemRepository CreateRepository(IDbContextFactory<MulletaFlixDbContext> dbFactory)
    {
        var itemTypeLookup = new Mock<IItemTypeLookup>();
        itemTypeLookup.Setup(l => l.BaseItemKindNames)
            .Returns(new Dictionary<BaseItemKind, string>
            {
                { BaseItemKind.Episode, typeof(Episode).ToString() },
                { BaseItemKind.Season, typeof(Season).ToString() },
                { BaseItemKind.Series, typeof(Series).ToString() },
                { BaseItemKind.MusicAlbum, typeof(MusicAlbum).ToString() }
            });

        return new BaseItemRepository(
            dbFactory,
            Mock.Of<IServerApplicationHost>(),
            itemTypeLookup.Object,
            Mock.Of<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(manager =>
                manager.Configuration == new ServerConfiguration()),
            NullLogger<BaseItemRepository>.Instance);
    }

    private static void AddMatchingMusicData(MulletaFlixDbContext context, Guid matchingAlbumId, Guid matchingTrackId)
    {
        var matchingAlbum = new BaseItemEntity
        {
            Id = matchingAlbumId,
            Type = typeof(MusicAlbum).ToString(),
            Name = "Matching Album",
            DateCreated = DateTime.UtcNow,
            IsFolder = true
        };
        var unrelatedAlbum = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(MusicAlbum).ToString(),
            Name = "Unrelated Album",
            DateCreated = DateTime.UtcNow.AddMinutes(-1),
            IsFolder = true
        };
        var matchingTrack = new BaseItemEntity
        {
            Id = matchingTrackId,
            Type = typeof(Audio).ToString(),
            Name = "Matching Track",
            IsFolder = false
        };
        var unrelatedTrack = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Audio).ToString(),
            Name = "Unrelated Track",
            IsFolder = false
        };

        context.BaseItems.AddRange(matchingAlbum, unrelatedAlbum, matchingTrack, unrelatedTrack);
        context.AncestorIds.Add(new AncestorId
        {
            ItemId = matchingTrack.Id,
            ParentItemId = matchingAlbum.Id,
            Item = matchingTrack,
            ParentItem = matchingAlbum
        });
        context.AncestorIds.Add(new AncestorId
        {
            ItemId = unrelatedTrack.Id,
            ParentItemId = unrelatedAlbum.Id,
            Item = unrelatedTrack,
            ParentItem = unrelatedAlbum
        });
        context.SaveChanges();
    }

    private sealed class IsolatedMariaDbDatabase : IDisposable
    {
        private const string DatabasePrefix = "mfx_latest_music_test_";
        private readonly string _adminConnectionString;
        private readonly DbContextOptions<MulletaFlixDbContext> _options;
        private readonly IMulletaFlixDatabaseProvider _databaseProvider;
        private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior;

        public IsolatedMariaDbDatabase()
        {
            MariaDbTestEnvironment.SkipUnlessConfigured();
            var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
            _adminConnectionString = adminConnection.ConnectionString;
            DatabaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
            var databaseConnection = new MySqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = DatabaseName
            };
            _options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseMySql(databaseConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;

            var provider = new Mock<IMulletaFlixDatabaseProvider>();
            provider.Setup(p => p.OnModelCreating(It.IsAny<ModelBuilder>()));
            _databaseProvider = provider.Object;
            var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
            locking.Setup(l => l.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>(static (_, action) => action());
            locking.Setup(l => l.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
                .Callback<MulletaFlixDbContext, Func<Task>>(static (_, action) => action());
            _lockingBehavior = locking.Object;

            using var connection = new MySqlConnection(_adminConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
            command.ExecuteNonQuery();
        }

        public string DatabaseName { get; }

        public Guid MatchingAlbumId { get; } = Guid.NewGuid();

        public Guid MatchingTrackId { get; } = Guid.NewGuid();

        public MulletaFlixDbContext CreateDbContext()
            => new(_options, NullLogger<MulletaFlixDbContext>.Instance, _databaseProvider, _lockingBehavior);

        public void Dispose()
        {
            if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to drop a database outside the isolated test schema prefix.");
            }

            using var connection = new MySqlConnection(_adminConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
            command.ExecuteNonQuery();
        }
    }

    private static BaseItemEntity CreateSeries(string name)
    {
        return new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Series).ToString(),
            Name = name,
            IsFolder = true,
            IsSeries = true
        };
    }

    private static BaseItemEntity CreateMovie(string name, string presentationUniqueKey, DateTime dateCreated)
    {
        return new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Movie).ToString(),
            Name = name,
            PresentationUniqueKey = presentationUniqueKey,
            DateCreated = dateCreated,
            IsFolder = false
        };
    }

    private static BaseItemEntity CreateSeason(BaseItemEntity series, string name)
    {
        return new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Season).ToString(),
            Name = name,
            SeriesId = series.Id,
            ParentId = series.Id,
            IsFolder = true,
            DateCreated = series.DateCreated
        };
    }

    private static BaseItemEntity CreateEpisode(string seriesName, BaseItemEntity series, BaseItemEntity season, DateTime dateCreated)
    {
        return new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = typeof(Episode).ToString(),
            Name = $"{seriesName} Episode",
            SeriesName = seriesName,
            SeriesId = series.Id,
            SeasonId = season.Id,
            ParentId = season.Id,
            DateCreated = dateCreated,
            IsFolder = false
        };
    }
}
