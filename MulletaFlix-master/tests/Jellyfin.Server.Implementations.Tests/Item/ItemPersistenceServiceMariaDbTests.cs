using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MulletaFlix.Testing;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public sealed class ItemPersistenceServiceMariaDbTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void DeleteItem_RemovesLargeOwnedTreeAndOwnerItemsInMariaDb()
    {
        using var database = new IsolatedMariaDbDatabase();
        var root = CreateItem("Folder", isFolder: true);
        var children = Enumerable.Range(0, 2005)
            .Select(_ => CreateItem("Movie", parentId: root.Id))
            .ToArray();
        var ownerItems = new[]
        {
            CreateItem("Movie", ownerId: children[0].Id),
            CreateItem("Movie", ownerId: children[1000].Id),
            CreateItem("Movie", ownerId: children[^1].Id)
        };
        var nestedOwnerItem = CreateItem("Movie", ownerId: ownerItems[0].Id);
        var preserved = CreateItem("Movie");
        var userId = Guid.NewGuid();
        var user = new User("delete-test", "test-auth", "test-reset") { Id = userId };
        var latestPlayedDate = new DateTime(DateTime.UtcNow.AddDays(-1).Ticks / 10 * 10, DateTimeKind.Utc);

        using (var context = database.CreateDbContext())
        {
            context.BaseItems.AddRange([root, .. children, .. ownerItems, nestedOwnerItem, preserved]);
            context.Users.Add(user);
            context.AncestorIds.AddRange(children.Select(child => new AncestorId
            {
                ParentItemId = root.Id,
                ItemId = child.Id,
                ParentItem = root,
                Item = child
            }));
            context.UserData.AddRange(
                new UserData
                {
                    ItemId = BaseItemRepository.PlaceholderId,
                    Item = null!,
                    UserId = userId,
                    User = user,
                    CustomDataKey = "playback",
                    LastPlayedDate = latestPlayedDate.AddDays(-20),
                    PlayCount = 20
                },
                new UserData
                {
                    ItemId = children[0].Id,
                    Item = children[0],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "playback",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 4
                },
                new UserData
                {
                    ItemId = children[1].Id,
                    Item = children[1],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "playback",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 4,
                    IsFavorite = true
                },
                new UserData
                {
                    ItemId = children[1000].Id,
                    Item = children[1000],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "playback",
                    LastPlayedDate = latestPlayedDate.AddDays(-10),
                    PlayCount = 99
                },
                new UserData
                {
                    ItemId = children[0].Id,
                    Item = children[0],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "resume",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 2,
                    PlaybackPositionTicks = 100,
                    Rating = 9,
                    AudioStreamIndex = 2
                },
                new UserData
                {
                    ItemId = children[1].Id,
                    Item = children[1],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "resume",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 2,
                    PlaybackPositionTicks = 200,
                    Rating = 8,
                    AudioStreamIndex = 1,
                    SubtitleStreamIndex = 1
                },
                new UserData
                {
                    ItemId = BaseItemRepository.PlaceholderId,
                    Item = null!,
                    UserId = userId,
                    User = user,
                    CustomDataKey = "favorite",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 10,
                    IsFavorite = true,
                    RetentionDate = latestPlayedDate.AddDays(-1)
                },
                new UserData
                {
                    ItemId = children[1000].Id,
                    Item = children[1000],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "favorite",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 10,
                    IsFavorite = false
                },
                new UserData
                {
                    ItemId = BaseItemRepository.PlaceholderId,
                    Item = null!,
                    UserId = userId,
                    User = user,
                    CustomDataKey = "candidate-favorite",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 10,
                    IsFavorite = false
                },
                new UserData
                {
                    ItemId = children[2].Id,
                    Item = children[2],
                    UserId = userId,
                    User = user,
                    CustomDataKey = "candidate-favorite",
                    LastPlayedDate = latestPlayedDate,
                    PlayCount = 10,
                    IsFavorite = true
                });
            context.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContext()).Returns(database.CreateDbContext);
        var service = new ItemPersistenceService(
            factory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);

        service.DeleteItem([root.Id, .. children.Select(child => child.Id)]);

        using var verificationContext = database.CreateDbContext();
        var remainingIds = verificationContext.BaseItems.Select(item => item.Id).ToArray();
        var retainedPlayback = verificationContext.UserData
            .Where(data => data.ItemId == BaseItemRepository.PlaceholderId
                && data.UserId == userId
                && data.CustomDataKey == "playback")
            .Single();
        var retainedFavorite = verificationContext.UserData
            .Where(data => data.ItemId == BaseItemRepository.PlaceholderId
                && data.UserId == userId
                && data.CustomDataKey == "favorite")
            .Single();
        var retainedResume = verificationContext.UserData
            .Where(data => data.ItemId == BaseItemRepository.PlaceholderId
                && data.UserId == userId
                && data.CustomDataKey == "resume")
            .Single();
        var retainedCandidateFavorite = verificationContext.UserData
            .Where(data => data.ItemId == BaseItemRepository.PlaceholderId
                && data.UserId == userId
                && data.CustomDataKey == "candidate-favorite")
            .Single();
        Assert.Equal(2, remainingIds.Length);
        Assert.Contains(BaseItemRepository.PlaceholderId, remainingIds);
        Assert.Contains(preserved.Id, remainingIds);
        Assert.Equal(latestPlayedDate, retainedPlayback.LastPlayedDate);
        Assert.Equal(4, retainedPlayback.PlayCount);
        Assert.True(retainedPlayback.IsFavorite);
        Assert.True(retainedFavorite.IsFavorite);
        Assert.True(retainedFavorite.RetentionDate > latestPlayedDate);
        Assert.Equal(200L, retainedResume.PlaybackPositionTicks);
        Assert.Equal(8d, retainedResume.Rating);
        Assert.Equal(1, retainedResume.AudioStreamIndex);
        Assert.Equal(1, retainedResume.SubtitleStreamIndex);
        Assert.True(retainedCandidateFavorite.IsFavorite);
    }

    private static BaseItemEntity CreateItem(string type, bool isFolder = false, Guid? ownerId = null, Guid? parentId = null)
    {
        return new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = type,
            IsFolder = isFolder,
            OwnerId = ownerId,
            ParentId = parentId
        };
    }

    private sealed class IsolatedMariaDbDatabase : IDisposable
    {
        private const string DatabasePrefix = "mfx_item_delete_test_";
        private readonly string _adminConnectionString;
        private readonly DbContextOptions<MulletaFlixDbContext> _options;
        private readonly IMulletaFlixDatabaseProvider _databaseProvider;
        private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior;

        public IsolatedMariaDbDatabase()
        {
            MariaDbTestEnvironment.SkipUnlessConfigured();
            var admin = MariaDbTestEnvironment.GetAdminConnection();
            _adminConnectionString = admin.ConnectionString;
            DatabaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
            var databaseConnection = new MySqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = DatabaseName
            };
            _options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseMySql(databaseConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;

            var provider = new Mock<IMulletaFlixDatabaseProvider>();
            provider.Setup(item => item.OnModelCreating(It.IsAny<ModelBuilder>()));
            _databaseProvider = provider.Object;
            var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
            locking.Setup(item => item.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>(static (_, save) => save());
            locking.Setup(item => item.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<System.Threading.Tasks.Task>>()))
                .Callback<MulletaFlixDbContext, Func<System.Threading.Tasks.Task>>(static (_, save) => save());
            _lockingBehavior = locking.Object;

            try
            {
                using var connection = new MySqlConnection(_adminConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                command.ExecuteNonQuery();
                using var context = CreateDbContext();
                context.Database.EnsureCreated();
            }
            catch
            {
                DropDatabase();
                throw;
            }
        }

        private string DatabaseName { get; }

        public MulletaFlixDbContext CreateDbContext()
            => new(_options, NullLogger<MulletaFlixDbContext>.Instance, _databaseProvider, _lockingBehavior);

        public void Dispose() => DropDatabase();

        private void DropDatabase()
        {
            if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to drop a database outside the isolated item-delete test prefix.");
            }

            using var connection = new MySqlConnection(_adminConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
            command.ExecuteNonQuery();
        }
    }
}
