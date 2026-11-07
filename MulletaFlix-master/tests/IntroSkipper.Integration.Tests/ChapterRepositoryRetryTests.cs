using System.Reflection;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Interfaces;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MySqlConnector;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class ChapterRepositoryRetryTests
{
    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public void SaveChapters_RetriesWholeTransactionWithFreshContextAfterMariaDbLockFailure(int errorCode)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseSqlite(connection)
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var seedLocking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        seedLocking.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, saveChanges) => saveChanges());

        var itemId = Guid.NewGuid();
        using (var seedContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            seedLocking.Object))
        {
            seedContext.Database.EnsureCreated();
            seedContext.BaseItems.Add(new BaseItemEntity { Id = itemId, Type = "Series", Path = "test" });
            seedContext.SaveChanges();
        }

        var attempts = 0;
        var retryLocking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        retryLocking.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((context, saveChanges) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    context.Database.CurrentTransaction!.Rollback();
                    throw new DbUpdateException("Simulated MariaDB lock failure", CreateMariaDbException(errorCode));
                }

                saveChanges();
            });

        var contextsCreated = 0;
        var contextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        contextFactory.Setup(factory => factory.CreateDbContext())
            .Returns(() =>
            {
                Interlocked.Increment(ref contextsCreated);
                return new MulletaFlixDbContext(
                    options,
                    NullLogger<MulletaFlixDbContext>.Instance,
                    databaseProvider.Object,
                    retryLocking.Object);
            });

        var repository = new ChapterRepository(contextFactory.Object, Mock.Of<IImageProcessor>());
        repository.SaveChapters(itemId, [new ChapterInfo { StartPositionTicks = 42, Name = "Capítulo" }]);

        Assert.Equal(2, contextsCreated);
        Assert.Equal(2, attempts);

        using var verifyContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            seedLocking.Object);
        var saved = Assert.Single(verifyContext.Chapters);
        Assert.Equal(42, saved.StartPositionTicks);
        Assert.Equal("Capítulo", saved.Name);
    }

    private static MySqlException CreateMariaDbException(int errorCode)
    {
        var constructor = typeof(MySqlException).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(MySqlErrorCode), typeof(string)],
            null);
        return (MySqlException)constructor!.Invoke([(MySqlErrorCode)errorCode, "Simulated MariaDB lock failure"]);
    }
}
