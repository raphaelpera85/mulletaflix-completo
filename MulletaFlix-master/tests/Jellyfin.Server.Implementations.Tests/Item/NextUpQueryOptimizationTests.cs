using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public sealed class NextUpQueryOptimizationTests : IDisposable
{
    private readonly MulletaFlixDbContext _context;
    private readonly Mock<IDbContextFactory<MulletaFlixDbContext>> _dbProviderMock;
    private readonly Mock<IItemTypeLookup> _itemTypeLookupMock;
    private readonly Mock<IItemQueryHelpers> _queryHelpersMock;
    private readonly NextUpService _service;

    public NextUpQueryOptimizationTests()
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

        _dbProviderMock = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        _dbProviderMock.Setup(f => f.CreateDbContext()).Returns(_context);

        _itemTypeLookupMock = new Mock<IItemTypeLookup>();
        _itemTypeLookupMock.Setup(l => l.BaseItemKindNames)
            .Returns(new Dictionary<BaseItemKind, string> { { BaseItemKind.Episode, "Episode" } });

        _queryHelpersMock = new Mock<IItemQueryHelpers>();

        _service = new NextUpService(
            _dbProviderMock.Object,
            _itemTypeLookupMock.Object,
            _queryHelpersMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void KeepPreferredCandidate_RetainsOnlyEarliestPositionPerSeries()
    {
        var first = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 1, 1);
        var laterSeason = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 2, 1);
        var earlierEpisode = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 1, 2);
        var candidates = new Dictionary<string, NextUpService.EpisodeCandidate>();

        NextUpService.KeepPreferredCandidate(candidates, laterSeason, descending: false);
        NextUpService.KeepPreferredCandidate(candidates, earlierEpisode, descending: false);
        NextUpService.KeepPreferredCandidate(candidates, first, descending: false);

        Assert.Single(candidates);
        Assert.Equal(first.Id, candidates["SeriesA"].Id);
    }

    [Fact]
    public void KeepPreferredCandidate_RetainsOnlyLatestPositionPerSeries()
    {
        var earlier = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 1, 12);
        var laterSeason = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 2, 1);
        var laterEpisode = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 2, 2);
        var anotherSeries = new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesB", 1, 1);
        var candidates = new Dictionary<string, NextUpService.EpisodeCandidate>();

        NextUpService.KeepPreferredCandidate(candidates, earlier, descending: true);
        NextUpService.KeepPreferredCandidate(candidates, laterSeason, descending: true);
        NextUpService.KeepPreferredCandidate(candidates, laterEpisode, descending: true);
        NextUpService.KeepPreferredCandidate(candidates, anotherSeries, descending: true);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(laterEpisode.Id, candidates["SeriesA"].Id);
        Assert.Equal(anotherSeries.Id, candidates["SeriesB"].Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepPreferredCandidate_MatchesStableOrderingForNullablePositions(bool descending)
    {
        var episodes = new[]
        {
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", null, 2),
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 1, null),
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 1, 2),
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 1, 2),
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesA", 2, null),
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesB", null, null),
            new NextUpService.EpisodeCandidate(Guid.NewGuid(), "SeriesB", 1, 1)
        };
        var selected = new Dictionary<string, NextUpService.EpisodeCandidate>();

        foreach (var episode in episodes)
        {
            NextUpService.KeepPreferredCandidate(selected, episode, descending);
        }

        foreach (var group in episodes.GroupBy(episode => episode.SeriesKey))
        {
            var ordered = descending
                ? group.OrderByDescending(episode => episode.Season).ThenByDescending(episode => episode.Episode)
                : group.OrderBy(episode => episode.Season).ThenBy(episode => episode.Episode);
            Assert.Equal(ordered.First().Id, selected[group.Key].Id);
        }
    }

    [Fact]
    public void GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeWithoutBufferingAllCandidates()
    {
        var userId = Guid.NewGuid();
        var seriesKey = "SeriesA";
        var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth")
        {
            Id = userId
        };
        var watched = CreateEpisode(seriesKey, 1, 1);
        var expectedNext = CreateEpisode(seriesKey, 1, 2);
        var laterEpisode = CreateEpisode(seriesKey, 1, 3);
        _context.Users.Add(userEntity);
        _context.BaseItems.AddRange(watched, expectedNext, laterEpisode);
        _context.UserData.Add(new UserData
        {
            UserId = userId,
            ItemId = watched.Id,
            Item = watched,
            User = userEntity,
            CustomDataKey = string.Empty,
            LastPlayedDate = DateTime.UtcNow,
            Played = true
        });
        _context.SaveChanges();

        _queryHelpersMock
            .Setup(h => h.ApplyAccessFiltering(_context, It.IsAny<IQueryable<BaseItemEntity>>(), It.IsAny<InternalItemsQuery>()))
            .Returns<MulletaFlixDbContext, IQueryable<BaseItemEntity>, InternalItemsQuery>((_, query, _) => query);
        _queryHelpersMock
            .Setup(h => h.ApplyNavigations(It.IsAny<IQueryable<BaseItemEntity>>(), It.IsAny<InternalItemsQuery>()))
            .Returns<IQueryable<BaseItemEntity>, InternalItemsQuery>((query, _) => query);
        var deserializedIds = new List<Guid>();
        _queryHelpersMock
            .Setup(h => h.DeserializeBaseItem(It.IsAny<BaseItemEntity>(), It.IsAny<bool>()))
            .Callback<BaseItemEntity, bool>((entity, _) => deserializedIds.Add(entity.Id))
            .Returns((BaseItem?)null);

        var result = _service.GetNextUpEpisodesBatch(
            new InternalItemsQuery { User = new User("testuser", "auth", "auth") { Id = userId } },
            [seriesKey],
            includeSpecials: false,
            includeWatchedForRewatching: false);

        Assert.Contains(expectedNext.Id, deserializedIds);
        Assert.DoesNotContain(laterEpisode.Id, deserializedIds);
        Assert.Contains(seriesKey, result.Keys);
    }

    [Fact]
    public void GetNextUpEpisodesBatch_SelectsNextPlayedEpisodeAfterMostRecentlyPlayedPosition()
    {
        var userId = Guid.NewGuid();
        var seriesKey = "SeriesRewatch";
        var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth")
        {
            Id = userId
        };
        var watchedMostRecently = CreateEpisode(seriesKey, 1, 1);
        var watchedLater = CreateEpisode(seriesKey, 1, 3);
        _context.Users.Add(userEntity);
        _context.BaseItems.AddRange(watchedMostRecently, CreateEpisode(seriesKey, 1, 2), watchedLater);
        var now = DateTime.UtcNow;
        _context.UserData.AddRange(
            new UserData
            {
                UserId = userId,
                ItemId = watchedMostRecently.Id,
                Item = watchedMostRecently,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = now,
                Played = true
            },
            new UserData
            {
                UserId = userId,
                ItemId = watchedLater.Id,
                Item = watchedLater,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = now.AddMinutes(-1),
                Played = true
            },
            new UserData
            {
                UserId = userId,
                ItemId = watchedMostRecently.Id,
                Item = watchedMostRecently,
                User = userEntity,
                CustomDataKey = "secondary",
                LastPlayedDate = now.AddMinutes(-5),
                Played = true
            });
        _context.SaveChanges();

        _queryHelpersMock
            .Setup(h => h.ApplyAccessFiltering(_context, It.IsAny<IQueryable<BaseItemEntity>>(), It.IsAny<InternalItemsQuery>()))
            .Returns<MulletaFlixDbContext, IQueryable<BaseItemEntity>, InternalItemsQuery>((_, query, _) => query);
        _queryHelpersMock
            .Setup(h => h.ApplyNavigations(It.IsAny<IQueryable<BaseItemEntity>>(), It.IsAny<InternalItemsQuery>()))
            .Returns<IQueryable<BaseItemEntity>, InternalItemsQuery>((query, _) => query);
        var deserializedIds = new List<Guid>();
        _queryHelpersMock
            .Setup(h => h.DeserializeBaseItem(It.IsAny<BaseItemEntity>(), It.IsAny<bool>()))
            .Callback<BaseItemEntity, bool>((entity, _) => deserializedIds.Add(entity.Id))
            .Returns((BaseItem?)null);

        var result = _service.GetNextUpEpisodesBatch(
            new InternalItemsQuery { User = new User("testuser", "auth", "auth") { Id = userId } },
            [seriesKey],
            includeSpecials: false,
            includeWatchedForRewatching: true);

        Assert.Contains(watchedLater.Id, deserializedIds);
        Assert.Contains(watchedMostRecently.Id, deserializedIds);
        Assert.Equal(2, deserializedIds.Count(id => id == watchedLater.Id));
        Assert.Null(result[seriesKey].NextUp);
    }

    private static BaseItemEntity CreateEpisode(string seriesKey, int season, int episode)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = seriesKey,
            ParentIndexNumber = season,
            IndexNumber = episode
        };

    [Fact]
    public void GetNextUpSeriesKeys_ReturnsProjectedKeys()
    {
        var userId = Guid.NewGuid();
        var seriesAKey = "SeriesA";
        var seriesBKey = "SeriesB";
        var topParentId = Guid.NewGuid();

        var episodeA = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = seriesAKey,
            TopParentId = topParentId,
            ParentIndexNumber = 1,
            IndexNumber = 1
        };
        var episodeB = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = seriesBKey,
            TopParentId = topParentId,
            ParentIndexNumber = 1,
            IndexNumber = 1
        };

        var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth")
        {
            Id = userId
        };
        _context.Users.Add(userEntity);

        _context.BaseItems.AddRange(episodeA, episodeB);
        _context.UserData.AddRange(
            new UserData
            {
                UserId = userId,
                ItemId = episodeA.Id,
                Item = episodeA,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = DateTime.UtcNow.AddDays(-1),
                Played = true
            },
            new UserData
            {
                UserId = userId,
                ItemId = episodeB.Id,
                Item = episodeB,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = DateTime.UtcNow.AddDays(-2),
                Played = true
            });

        _context.SaveChanges();

        var query = new InternalItemsQuery
        {
            User = new User("testuser", "auth", "auth") { Id = userId },
            TopParentIds = [topParentId]
        };

        var result = _service.GetNextUpSeriesKeys(query, DateTime.UtcNow.AddDays(-3));

        Assert.Equal(2, result.Count);
        Assert.Equal(seriesAKey, result[0]);
        Assert.Equal(seriesBKey, result[1]);
    }

    [Fact]
    public void GetNextUpSeriesKeys_AppliesLimit()
    {
        var userId = Guid.NewGuid();
        var seriesAKey = "SeriesA";
        var seriesBKey = "SeriesB";
        var topParentId = Guid.NewGuid();
        var lastPlayedDate = DateTime.UtcNow.AddDays(-1);

        var episodeA = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = seriesAKey,
            TopParentId = topParentId,
            ParentIndexNumber = 1,
            IndexNumber = 1
        };
        var episodeB = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = seriesBKey,
            TopParentId = topParentId,
            ParentIndexNumber = 1,
            IndexNumber = 1
        };

        var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth")
        {
            Id = userId
        };
        _context.Users.Add(userEntity);

        _context.BaseItems.AddRange(episodeA, episodeB);
        _context.UserData.AddRange(
            new UserData
            {
                UserId = userId,
                ItemId = episodeA.Id,
                Item = episodeA,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = lastPlayedDate,
                Played = true
            },
            new UserData
            {
                UserId = userId,
                ItemId = episodeB.Id,
                Item = episodeB,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = lastPlayedDate,
                Played = true
            });

        _context.SaveChanges();

        var query = new InternalItemsQuery
        {
            User = new User("testuser", "auth", "auth") { Id = userId },
            TopParentIds = [topParentId],
            Limit = 1
        };

        var result = _service.GetNextUpSeriesKeys(query, DateTime.UtcNow.AddDays(-3));

        Assert.Single(result);
        Assert.Equal(seriesAKey, result[0]);
    }
}

