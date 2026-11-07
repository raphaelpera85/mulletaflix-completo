using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Database.Implementations.Interfaces;
using MySqlConnector;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class JellyfinDbContextTransactionRetryTests
{
    [Fact]
    public void OptimisticLockBehavior_OnSaveChanges_PropagatesPermanentFailure()
    {
        var behavior = new OptimisticLockBehavior(NullLogger<OptimisticLockBehavior>.Instance);
        var expected = new InvalidOperationException("permanent database failure");

        var actual = Assert.Throws<InvalidOperationException>(() => behavior.OnSaveChanges(null!, () => throw expected));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task OptimisticLockBehavior_OnSaveChangesAsync_PropagatesPermanentFailure()
    {
        var behavior = new OptimisticLockBehavior(NullLogger<OptimisticLockBehavior>.Instance);
        var expected = new InvalidOperationException("permanent database failure");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.OnSaveChangesAsync(null!, () => Task.FromException(expected)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void OptimisticLockBehavior_OnSaveChanges_RetriesTransientLockFailure()
    {
        var behavior = new OptimisticLockBehavior(NullLogger<OptimisticLockBehavior>.Instance);
        var attempts = 0;

        behavior.OnSaveChanges(null!, () =>
        {
            if (++attempts == 1)
            {
                throw new InvalidOperationException("database is locked");
            }
        });

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task OptimisticLockBehavior_OnSaveChangesAsync_RetriesTransientLockFailure()
    {
        var behavior = new OptimisticLockBehavior(NullLogger<OptimisticLockBehavior>.Instance);
        var attempts = 0;

        await behavior.OnSaveChangesAsync(null!, () =>
        {
            if (++attempts == 1)
            {
                return Task.FromException(new InvalidOperationException("database is locked"));
            }

            return Task.CompletedTask;
        });

        Assert.Equal(2, attempts);
    }

    [Fact]
    public void OptimisticLockBehavior_OnSaveChanges_PropagatesAfterRetryLimit()
    {
        var behavior = new OptimisticLockBehavior(NullLogger<OptimisticLockBehavior>.Instance);
        var expected = new InvalidOperationException("database is locked");
        var attempts = 0;

        var actual = Assert.Throws<InvalidOperationException>(() => behavior.OnSaveChanges(null!, () =>
        {
            attempts++;
            throw expected;
        }));

        Assert.Same(expected, actual);
        Assert.Equal(16, attempts);
    }

    [Fact]
    public async Task OptimisticLockBehavior_OnSaveChangesAsync_PropagatesAfterRetryLimit()
    {
        var behavior = new OptimisticLockBehavior(NullLogger<OptimisticLockBehavior>.Instance);
        var expected = new InvalidOperationException("database is locked");
        var attempts = 0;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.OnSaveChangesAsync(null!, () =>
            {
                attempts++;
                return Task.FromException(expected);
            }));

        Assert.Same(expected, actual);
        Assert.Equal(16, attempts);
    }

    [Fact]
    public async Task SaveChangesAsync_DoesNotRetryDeadlockInsideExplicitTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var (context, locking) = CreateContext(connection);
        await using (context)
        {
            var attempts = 0;
            locking.Setup(l => l.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
                .Returns((MulletaFlixDbContext database, Func<Task> saveChanges) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        database.Database.CurrentTransaction!.Rollback();
                        return Task.FromException(CreateDeadlockException());
                    }

                    return saveChanges();
                });

            await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

            Assert.Equal(1213, ((MySqlException)exception.InnerException!).Number);
            Assert.Equal(1, attempts);
        }
    }

    [Fact]
    public void SaveChanges_DoesNotRetryDeadlockInsideExplicitTransaction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var (context, locking) = CreateContext(connection);
        using (context)
        {
            var attempts = 0;
            locking.Setup(l => l.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((database, saveChanges) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        database.Database.CurrentTransaction!.Rollback();
                        throw CreateDeadlockException();
                    }

                    saveChanges();
                });

            using var transaction = context.Database.BeginTransaction();
            var exception = Assert.Throws<DbUpdateException>(() => context.SaveChanges());

            Assert.Equal(1213, ((MySqlException)exception.InnerException!).Number);
            Assert.Equal(1, attempts);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_RetriesDeadlockWithoutExplicitTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var (context, locking) = CreateContext(connection);
        await using (context)
        {
            var attempts = 0;
            locking.Setup(l => l.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
                .Returns((MulletaFlixDbContext _, Func<Task> saveChanges) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        return Task.FromException(CreateDeadlockException());
                    }

                    return saveChanges();
                });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, attempts);
        }
    }

    [Fact]
    public void SaveChanges_RetriesDeadlockWithoutExplicitTransaction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var (context, locking) = CreateContext(connection);
        using (context)
        {
            var attempts = 0;
            locking.Setup(l => l.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((_, saveChanges) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw CreateDeadlockException();
                    }

                    saveChanges();
                });

            context.SaveChanges();

            Assert.Equal(2, attempts);
        }
    }

    private static (MulletaFlixDbContext Context, Mock<IEntityFrameworkCoreLockingBehavior> Locking) CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseSqlite(connection)
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        var context = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            lockingBehavior.Object);

        return (context, lockingBehavior);
    }

    private static DbUpdateException CreateDeadlockException()
    {
        var constructor = typeof(MySqlException).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(MySqlErrorCode), typeof(string)],
            null);
        var deadlock = (MySqlException)constructor!.Invoke([(MySqlErrorCode)1213, "Deadlock found when trying to get lock"]);
        return new DbUpdateException("Simulated MariaDB transaction deadlock", deadlock);
    }
}
