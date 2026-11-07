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
        Assert.Equal(seriesAKey, result[0].SeriesKey);
        Assert.Equal(seriesBKey, result[1].SeriesKey);
    }

    [Fact]
    public void GetNextUpSeriesKeys_AppliesLimit()
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
            TopParentIds = [topParentId],
            Limit = 1
        };

        var result = _service.GetNextUpSeriesKeys(query, DateTime.UtcNow.AddDays(-3));

        Assert.Single(result);
        Assert.Equal(seriesAKey, result[0].SeriesKey);
    }

    [Fact]
    public void GetNextUpSeriesKeys_BreaksLastPlayedDateTiesBySeriesKeyBeforeLimit()
    {
        var userId = Guid.NewGuid();
        var topParentId = Guid.NewGuid();
        var lastPlayedDate = DateTime.UtcNow.AddDays(-1);
        var seriesKeys = new[] { "SeriesC", "SeriesA", "SeriesB" };
        var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth") { Id = userId };
        _context.Users.Add(userEntity);

        var episodes = seriesKeys.Select(key => new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = key,
            TopParentId = topParentId
        }).ToArray();
        _context.BaseItems.AddRange(episodes);
        _context.UserData.AddRange(episodes.Select(episode => new UserData
        {
            UserId = userId,
            ItemId = episode.Id,
            Item = episode,
            User = userEntity,
            CustomDataKey = string.Empty,
            LastPlayedDate = lastPlayedDate,
            Played = true
        }));
        _context.SaveChanges();

        var query = new InternalItemsQuery
        {
            User = new User("testuser", "auth", "auth") { Id = userId },
            TopParentIds = [topParentId],
            Limit = 2
        };

        var result = _service.GetNextUpSeriesKeys(query, lastPlayedDate.AddDays(-1));

        Assert.Equal(["SeriesA", "SeriesB"], result.Select(item => item.SeriesKey));
    }

    [Fact]
    public void GetNextUpSeriesKeys_AppliesStartIndexAfterStableOrdering()
    {
        var userId = Guid.NewGuid();
        var topParentId = Guid.NewGuid();
        var lastPlayedDate = DateTime.UtcNow.AddDays(-1);
        var seriesKeys = new[] { "SeriesC", "SeriesA", "SeriesB" };
        var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth") { Id = userId };
        _context.Users.Add(userEntity);

        var episodes = seriesKeys.Select(key => new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = key,
            TopParentId = topParentId
        }).ToArray();
        _context.BaseItems.AddRange(episodes);
        _context.UserData.AddRange(episodes.Select(episode => new UserData
        {
            UserId = userId,
            ItemId = episode.Id,
            Item = episode,
            User = userEntity,
            CustomDataKey = string.Empty,
            LastPlayedDate = lastPlayedDate,
            Played = true
        }));
        _context.SaveChanges();

        var query = new InternalItemsQuery
        {
            User = new User("testuser", "auth", "auth") { Id = userId },
            TopParentIds = [topParentId],
            StartIndex = 1,
            Limit = 1
        };

        var result = _service.GetNextUpSeriesKeys(query, lastPlayedDate.AddDays(-1));

        var item = Assert.Single(result);
        Assert.Equal("SeriesB", item.SeriesKey);
        Assert.Equal(lastPlayedDate, item.LastPlayedDate);
    }
}

