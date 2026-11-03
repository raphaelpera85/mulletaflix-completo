using System.Reflection;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
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

public sealed class MediaStreamRepositoryRetryTests
{
    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public void SaveMediaStreams_RetriesWholeTransactionWithFreshContextAfterMariaDbLockFailure(int errorCode)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseSqlite(connection)
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var normalLocking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        normalLocking.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, saveChanges) => saveChanges());

        var itemId = Guid.NewGuid();
        using (var seedContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            normalLocking.Object))
        {
            seedContext.Database.EnsureCreated();
            seedContext.BaseItems.Add(new BaseItemEntity { Id = itemId, Type = "Series", Path = "test" });
            seedContext.SaveChanges();
        }

        var contextsCreated = 0;
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

        var repository = new MediaStreamRepository(
            contextFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            Mock.Of<ILocalizationManager>());
        repository.SaveMediaStreams(
            itemId,
            [new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" }],
            CancellationToken.None);

        Assert.Equal(2, contextsCreated);
        Assert.Equal(2, attempts);

        using var verifyContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            normalLocking.Object);
        var saved = Assert.Single(verifyContext.MediaStreamInfos);
        Assert.Equal(0, saved.StreamIndex);
        Assert.Equal("h264", saved.Codec);
    }

    [Fact]
    public void SaveMediaStreams_DoesNotRetryPermanentFailureAndPreservesPreviousRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseSqlite(connection)
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var normalLocking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        normalLocking.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, saveChanges) => saveChanges());

        var itemId = Guid.NewGuid();
        using (var seedContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            normalLocking.Object))
        {
            seedContext.Database.EnsureCreated();
            seedContext.BaseItems.Add(new BaseItemEntity { Id = itemId, Type = "Series", Path = "test" });
            seedContext.SaveChanges();
        }

        var seedFactory = CreateFactory(options, databaseProvider.Object, normalLocking.Object);
        var repository = new MediaStreamRepository(seedFactory, Mock.Of<IServerApplicationHost>(), Mock.Of<ILocalizationManager>());
        repository.SaveMediaStreams(
            itemId,
            [new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" }],
            CancellationToken.None);

        var attempts = 0;
        var permanentFailure = new Mock<IEntityFrameworkCoreLockingBehavior>();
        permanentFailure.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new DbUpdateException("Simulated permanent database failure", CreateMariaDbException(1062));
            });
        repository = new MediaStreamRepository(
            CreateFactory(options, databaseProvider.Object, permanentFailure.Object),
            Mock.Of<IServerApplicationHost>(),
            Mock.Of<ILocalizationManager>());

        Assert.Throws<DbUpdateException>(() => repository.SaveMediaStreams(
            itemId,
            [new MediaStream { Index = 1, Type = MediaStreamType.Video, Codec = "hevc" }],
            CancellationToken.None));
        Assert.Equal(1, attempts);

        using var verifyContext = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            normalLocking.Object);
        var previous = Assert.Single(verifyContext.MediaStreamInfos);
        Assert.Equal(0, previous.StreamIndex);
        Assert.Equal("h264", previous.Codec);
    }

    private static IDbContextFactory<MulletaFlixDbContext> CreateFactory(
        DbContextOptions<MulletaFlixDbContext> options,
        IMulletaFlixDatabaseProvider databaseProvider,
        IEntityFrameworkCoreLockingBehavior lockingBehavior)
    {
        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContext())
            .Returns(() => new MulletaFlixDbContext(
                options,
                NullLogger<MulletaFlixDbContext>.Instance,
                databaseProvider,
                lockingBehavior));
        return factory.Object;
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
