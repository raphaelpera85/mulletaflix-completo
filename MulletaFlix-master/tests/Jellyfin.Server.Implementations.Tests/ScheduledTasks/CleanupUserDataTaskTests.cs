using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Model.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class CleanupUserDataTaskTests
{
    [Fact]
    public void FilterExpiredDetachedUserData_UsesStrictRetentionCutoff()
    {
        var cutoff = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var expired = CreateUserData(cutoff.AddTicks(-1));
        var boundary = CreateUserData(cutoff);
        var recent = CreateUserData(cutoff.AddTicks(1));
        var missingRetentionDate = CreateUserData(null);

        var result = CleanupUserDataTask.FilterExpiredDetachedUserData(
            new[] { expired, boundary, recent, missingRetentionDate }.AsQueryable(),
            cutoff);

        Assert.Equal(new[] { expired.ItemId }, result.Select(entry => entry.ItemId));
    }

    [Fact]
    public async Task ExecuteAsync_DeletesOnlyExpiredEntriesAndReportsSuccessfulCounts()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), databaseRoot)
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        lockingBehavior.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, save) => save());
        lockingBehavior.Setup(locking => locking.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
            .Callback<MulletaFlixDbContext, Func<Task>>((_, save) => save());
        MulletaFlixDbContext CreateContext() => new(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            lockingBehavior.Object);

        var expired = CreateUserData(DateTime.UtcNow.AddDays(-91), BaseItemRepository.PlaceholderId);
        var retained = CreateUserData(DateTime.UtcNow.AddDays(-1), BaseItemRepository.PlaceholderId);
        await using (var seedContext = CreateContext())
        {
            seedContext.Database.EnsureCreated();
            seedContext.UserData.AddRange(expired, retained);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, await seedContext.UserData.CountAsync(TestContext.Current.CancellationToken));
        }

        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == UserDataCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateContext);
        async Task<int> DeleteFromInMemoryAsync(IQueryable<UserData> query, CancellationToken cancellationToken)
        {
            var candidates = await query.ToListAsync(cancellationToken);
            Assert.Equal(expired.CustomDataKey, Assert.Single(candidates).CustomDataKey);
            var candidateKeys = candidates.Select(candidate => candidate.CustomDataKey).ToArray();
            await using var deleteContext = CreateContext();
            var allRows = await deleteContext.UserData.ToListAsync(cancellationToken);
            var rowsToDelete = allRows.Where(entry => candidateKeys.Contains(entry.CustomDataKey)).ToArray();
            deleteContext.UserData.RemoveRange(rowsToDelete);
            await deleteContext.SaveChangesAsync(cancellationToken);
            return candidates.Count;
        }

        var task = new CleanupUserDataTask(
            Mock.Of<ILocalizationManager>(),
            factory.Object,
            NullLogger<CleanupUserDataTask>.Instance,
            DeleteFromInMemoryAsync);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);

        await using var verifyContext = CreateContext();
        Assert.Equal(1, await verifyContext.UserData.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(await verifyContext.UserData.AnyAsync(entry => entry.CustomDataKey == expired.CustomDataKey));
        Assert.True(await verifyContext.UserData.AnyAsync(entry => entry.CustomDataKey == retained.CustomDataKey));
        Assert.Contains(measurements.Single(item => item.Name == "mulletaflix.user_data_cleanup.runs").Tags,
            tag => tag.Key == "result" && Equals(tag.Value, "success"));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.user_data_cleanup.entries.detached_per_run" && Equals(item.Value, 2L));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.user_data_cleanup.entries.expired_candidates_per_run" && Equals(item.Value, 1L));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.user_data_cleanup.entries.deleted_per_run" && Equals(item.Value, 1L));
    }

    [Fact]
    public async System.Threading.Tasks.Task ExecuteAsync_RecordsAggregateMetricsForEmptyDatabaseAndFailure()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == UserDataCleanupMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot())
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        var context = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            Mock.Of<IEntityFrameworkCoreLockingBehavior>());
        context.Database.EnsureCreated();

        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        var task = new CleanupUserDataTask(
            Mock.Of<ILocalizationManager>(),
            factory.Object,
            NullLogger<CleanupUserDataTask>.Instance);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        factory.Setup(provider => provider.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("private database failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None));

        var runMeasurements = measurements.Where(item => item.Name == "mulletaflix.user_data_cleanup.runs").ToArray();
        Assert.Equal(2, runMeasurements.Length);
        Assert.Contains(runMeasurements[0].Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Contains(runMeasurements[1].Tags, tag => tag.Key == "result" && Equals(tag.Value, "failure"));
        Assert.Equal(2, measurements.Count(item => item.Name == "mulletaflix.user_data_cleanup.duration"));
        Assert.Equal(2, measurements.Count(item => item.Name == "mulletaflix.user_data_cleanup.entries.detached_per_run" && Equals(item.Value, 0L)));
        Assert.Equal(2, measurements.Count(item => item.Name == "mulletaflix.user_data_cleanup.entries.expired_candidates_per_run" && Equals(item.Value, 0L)));
        Assert.Equal(2, measurements.Count(item => item.Name == "mulletaflix.user_data_cleanup.entries.deleted_per_run" && Equals(item.Value, 0L)));
        Assert.Equal(new object[] { 1L, -1L, 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.user_data_cleanup.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("item", StringComparison.OrdinalIgnoreCase)));
    }

    private static UserData CreateUserData(DateTime? retentionDate, Guid? itemId = null)
    {
        return new UserData
        {
            CustomDataKey = Guid.NewGuid().ToString("N"),
            ItemId = itemId ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Item = null,
            User = null,
            RetentionDate = retentionDate
        };
    }
}
