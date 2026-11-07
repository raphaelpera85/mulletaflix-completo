using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Interfaces;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MySqlConnector;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class PeopleRepositoryRetryTests
{
    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public void UpdatePeople_RetriesWholeTransactionWithFreshContextAfterMariaDbLockFailure(int errorCode)
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
            seedContext.BaseItems.Add(new BaseItemEntity { Id = itemId, Type = "Series" });
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

        var repository = new PeopleRepository(contextFactory.Object, Mock.Of<IItemTypeLookup>());
        repository.UpdatePeople(itemId, [new PersonInfo { Name = "Pessoa do teste", Role = "Ator", Type = PersonKind.Actor }]);

        Assert.Equal(2, contextsCreated);
        Assert.Equal(3, attempts);

        using var verifyContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            seedLocking.Object);
        Assert.Single(verifyContext.Peoples);
        Assert.Equal("Ator", Assert.Single(verifyContext.PeopleBaseItemMap).Role);

        var nonTransientAttempts = 0;
        var nonTransientLocking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        nonTransientLocking.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, _) =>
            {
                Interlocked.Increment(ref nonTransientAttempts);
                throw new DbUpdateException("Simulated MariaDB duplicate key", CreateMariaDbException(1062));
            });
        var nonTransientContextsCreated = 0;
        var nonTransientFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        nonTransientFactory.Setup(factory => factory.CreateDbContext())
            .Returns(() =>
            {
                Interlocked.Increment(ref nonTransientContextsCreated);
                return new MulletaFlixDbContext(
                    options,
                    NullLogger<MulletaFlixDbContext>.Instance,
                    databaseProvider.Object,
                    nonTransientLocking.Object);
            });

        var nonTransientRepository = new PeopleRepository(nonTransientFactory.Object, Mock.Of<IItemTypeLookup>());
        Assert.Throws<DbUpdateException>(() => nonTransientRepository.UpdatePeople(
            itemId,
            [new PersonInfo { Name = "Pessoa do teste", Role = "Ator", Type = PersonKind.Actor }]));
        Assert.Equal(1, nonTransientContextsCreated);
        Assert.Equal(1, nonTransientAttempts);
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
