using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MulletaFlix.Testing;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public sealed class NextUpServiceMariaDbTests
{
    [Fact]
    public void GetNextUpSeriesKeys_AppliesStableOrderingAndPagingOnMariaDb()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        using var database = new IsolatedMariaDbDatabase();
        var userId = Guid.NewGuid();
        var topParentId = Guid.NewGuid();
        var lastPlayedDate = DateTime.UtcNow.AddDays(-1);

        using (var context = database.CreateDbContext())
        {
            context.Database.EnsureCreated();
            var userEntity = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth")
            {
                Id = userId
            };
            context.Users.Add(userEntity);

            var episodes = new[] { "SeriesC", "SeriesA", "SeriesD", "SeriesB" }
                .Select(key => new BaseItemEntity
                {
                    Id = Guid.NewGuid(),
                    Type = "Episode",
                    SeriesPresentationUniqueKey = key,
                    TopParentId = topParentId
                })
                .ToArray();
            context.BaseItems.AddRange(episodes);
            context.UserData.AddRange(episodes.Select(episode => new UserData
            {
                UserId = userId,
                ItemId = episode.Id,
                Item = episode,
                User = userEntity,
                CustomDataKey = string.Empty,
                LastPlayedDate = lastPlayedDate,
                Played = true
            }));
            context.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(contextFactory => contextFactory.CreateDbContext())
            .Returns(() => database.CreateDbContext());
        var itemTypeLookup = new Mock<IItemTypeLookup>();
        itemTypeLookup.Setup(lookup => lookup.BaseItemKindNames)
            .Returns(new Dictionary<BaseItemKind, string> { [BaseItemKind.Episode] = "Episode" });
        var service = new NextUpService(factory.Object, itemTypeLookup.Object, new Mock<IItemQueryHelpers>().Object);
        var query = new InternalItemsQuery(new User("testuser", "auth", "auth") { Id = userId })
        {
            TopParentIds = [topParentId],
            StartIndex = 1,
            Limit = 2
        };

        var result = service.GetNextUpSeriesKeys(query, lastPlayedDate.AddDays(-1));

        Assert.Equal(["SeriesB", "SeriesC"], result.Select(item => item.SeriesKey));
        var storedDate = new DateTime(lastPlayedDate.Ticks - (lastPlayedDate.Ticks % 10), DateTimeKind.Unspecified);
        Assert.All(result, item => Assert.Equal(storedDate, item.LastPlayedDate));

        var nextPage = service.GetNextUpSeriesKeys(
            new InternalItemsQuery(new User("testuser", "auth", "auth") { Id = userId })
            {
                TopParentIds = [topParentId],
                NextUpLastPlayedDateAfter = storedDate,
                NextUpSeriesKeyAfter = "SeriesB",
                Limit = 2
            },
            lastPlayedDate.AddDays(-1));

        Assert.Equal(["SeriesC", "SeriesD"], nextPage.Select(item => item.SeriesKey));
    }

    private sealed class IsolatedMariaDbDatabase : IDisposable
    {
        private const string DatabasePrefix = "mfx_nextup_test_";
        private readonly string _adminConnectionString;
        private readonly DbContextOptions<MulletaFlixDbContext> _options;

        public IsolatedMariaDbDatabase()
        {
            var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
            _adminConnectionString = adminConnection.ConnectionString;
            DatabaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
            var databaseConnection = new MySqlConnector.MySqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = DatabaseName
            };
            _options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseMySql(databaseConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 4)))
                .Options;

            using var connection = new MySqlConnector.MySqlConnection(_adminConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
            command.ExecuteNonQuery();
        }

        public string DatabaseName { get; }

        public MulletaFlixDbContext CreateDbContext()
        {
            var provider = new Mock<IMulletaFlixDatabaseProvider>();
            provider.Setup(instance => instance.OnModelCreating(It.IsAny<ModelBuilder>()));
            var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
            lockingBehavior.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((_, save) => save());
            lockingBehavior.Setup(locking => locking.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<System.Threading.Tasks.Task>>()))
                .Callback<MulletaFlixDbContext, Func<System.Threading.Tasks.Task>>((_, save) => save());
            return new MulletaFlixDbContext(_options, NullLogger<MulletaFlixDbContext>.Instance, provider.Object, lockingBehavior.Object);
        }

        public void Dispose()
        {
            if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to drop a database outside the isolated Next Up test prefix.");
            }

            using var connection = new MySqlConnector.MySqlConnection(_adminConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
            command.ExecuteNonQuery();
        }
    }
}
