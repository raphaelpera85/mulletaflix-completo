using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using MulletaFlix.Server.Implementations.Tests.Logging;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Item;

public class ItemPersistenceServiceTests
{
    [Fact]
    public void GetDeleteItemPersistenceLockIndices_IncludesPlaceholderAndOrdersDistinctStripes()
    {
        var itemIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var expected = itemIds
            .Append(BaseItemRepository.PlaceholderId)
            .Select(id => id.GetHashCode() & 15)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(expected, ItemPersistenceService.GetDeleteItemPersistenceLockIndices(itemIds));
    }

    [Fact]
    public void GetItemPersistenceLockIndices_IncludesItemParentAndOwnerStripesInOrder()
    {
        var itemId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var items = new[]
        {
            new Movie { Id = itemId, ParentId = parentId, OwnerId = ownerId },
            new Movie { Id = parentId },
        };

        var result = ItemPersistenceService.GetItemPersistenceLockIndices(items);

        var expected = new[] { itemId, parentId, ownerId }
            .Select(id => id.GetHashCode() & 15)
            .Distinct()
            .Order()
            .ToArray();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetMissingItemPersistenceLockIndices_ReturnsOnlyNewStripesInOrder()
    {
        var heldItemId = Guid.NewGuid();
        var missingItemId = Guid.NewGuid();
        while (ItemPersistenceService.GetItemPersistenceLock(heldItemId)
            == ItemPersistenceService.GetItemPersistenceLock(missingItemId))
        {
            missingItemId = Guid.NewGuid();
        }

        var heldLockIndices = new HashSet<int> { heldItemId.GetHashCode() & 15 };
        var missingLockIndex = missingItemId.GetHashCode() & 15;

        var result = ItemPersistenceService.GetMissingItemPersistenceLockIndices(
            [missingItemId, heldItemId, missingItemId],
            heldLockIndices);

        Assert.Equal(new[] { missingLockIndex }, result);
    }

    [Fact]
    public async Task SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext()
    {
        var itemId = Guid.NewGuid();
        var itemLock = ItemPersistenceService.GetItemPersistenceLock(itemId);
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database context requested"));
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);
        var item = new Movie { Id = itemId };

        await itemLock.WaitAsync();
        try
        {
            var saveTask = service.SaveImagesAsync(item);
            Assert.False(saveTask.IsCompleted);
            dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);

            itemLock.Release();
            await Assert.ThrowsAsync<InvalidOperationException>(() => saveTask);
        }
        finally
        {
            if (itemLock.CurrentCount == 0)
            {
                itemLock.Release();
            }
        }

        dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void UpdatePeople_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext()
    {
        var itemId = Guid.NewGuid();
        var itemLock = ItemPersistenceService.GetItemPersistenceLock(itemId);
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContext())
            .Throws(new InvalidOperationException("Database context requested"));
        var repository = new PeopleRepository(dbFactory.Object, Mock.Of<IItemTypeLookup>());
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        Exception? updateException = null;
        var updateThread = new Thread(() =>
        {
            started.Set();
            try
            {
                repository.UpdatePeople(itemId, [new PersonInfo { Name = "Locked person" }]);
            }
            catch (Exception exception)
            {
                updateException = exception;
            }
            finally
            {
                finished.Set();
            }
        });

        itemLock.Wait();
        try
        {
            updateThread.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)), "UpdatePeople worker did not start.");
            Assert.True(
                SpinWait.SpinUntil(
                    () => !updateThread.IsAlive || (updateThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(5)),
                "UpdatePeople worker did not reach the persistence-lock wait.");
            Assert.True(updateThread.IsAlive, "UpdatePeople must wait for the item's persistence stripe.");
            Assert.False(finished.IsSet);
            dbFactory.Verify(factory => factory.CreateDbContext(), Times.Never);

            itemLock.Release();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(5)), "UpdatePeople worker did not finish after lock release.");
        }
        finally
        {
            if (itemLock.CurrentCount == 0)
            {
                itemLock.Release();
            }
        }

        Assert.IsType<InvalidOperationException>(updateException);
        dbFactory.Verify(factory => factory.CreateDbContext(), Times.Once);
    }

    [Fact]
    public void UpdateInheritedValues_WaitsForAnyItemPersistenceLockBeforeOpeningDatabaseContext()
    {
        var itemId = Guid.NewGuid();
        var itemLock = ItemPersistenceService.GetItemPersistenceLock(itemId);
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContext())
            .Throws(new InvalidOperationException("Database context requested"));
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        Exception? updateException = null;
        var updateThread = new Thread(() =>
        {
            started.Set();
            try
            {
                service.UpdateInheritedValues();
            }
            catch (Exception exception)
            {
                updateException = exception;
            }
            finally
            {
                finished.Set();
            }
        });

        itemLock.Wait();
        try
        {
            updateThread.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)), "UpdateInheritedValues worker did not start.");
            Assert.True(
                SpinWait.SpinUntil(
                    () => !updateThread.IsAlive || (updateThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(5)),
                "UpdateInheritedValues worker did not reach the persistence-lock wait.");
            Assert.True(updateThread.IsAlive, "UpdateInheritedValues must wait for every item persistence stripe.");
            Assert.False(finished.IsSet);
            dbFactory.Verify(factory => factory.CreateDbContext(), Times.Never);

            itemLock.Release();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(5)), "UpdateInheritedValues worker did not finish after lock release.");
        }
        finally
        {
            if (itemLock.CurrentCount == 0)
            {
                itemLock.Release();
            }
        }

        Assert.IsType<InvalidOperationException>(updateException);
        dbFactory.Verify(factory => factory.CreateDbContext(), Times.Once);
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public async Task SaveImagesAsync_RetriesTransientLockFailuresThreeTimesThenPropagates(int errorCode)
    {
        var expectedException = new InvalidOperationException(
            "Transient database failure.",
            CreateMySqlException(errorCode));
        var logger = new CapturingLogger<ItemPersistenceService>();
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var itemId = Guid.NewGuid();
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            logger);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveImagesAsync(new Movie { Id = itemId }));

        Assert.Same(expectedException, actualException);
        dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Contains(logger.Messages, message =>
            message.Contains($"process {Environment.ProcessId}", StringComparison.Ordinal)
            && message.Contains(
                $"assembly version {typeof(ItemPersistenceService).Assembly.GetName().Version?.ToString(3)}",
                StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message =>
            message.Contains("exhausted 3 attempts", StringComparison.Ordinal)
            && message.Contains(itemId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveImagesAsync_RetriesTransientConnectionFailuresTenTimesThenPropagates()
    {
        var expectedException = new InvalidOperationException(
            "Transient database connection failure.",
            CreateMySqlException(1042));
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveImagesAsync(new Movie { Id = Guid.NewGuid() }));

        Assert.Same(expectedException, actualException);
        dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task SaveImagesAsync_DoesNotRetryPermanentDatabaseFailure()
    {
        var expectedException = new InvalidOperationException("Permanent database failure.");
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveImagesAsync(new Movie { Id = Guid.NewGuid() }));

        Assert.Same(expectedException, actualException);
        dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteExpiredPlaceholderUserData_WaitsForPlaceholderLockBeforeOpeningDatabaseContext()
    {
        var placeholderLock = ItemPersistenceService.GetItemPersistenceLock(BaseItemRepository.PlaceholderId);
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database context requested"));
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);

        await placeholderLock.WaitAsync();
        try
        {
            var cleanupTask = service.DeleteExpiredPlaceholderUserDataAsync(DateTime.UtcNow, CancellationToken.None);
            Assert.False(cleanupTask.IsCompleted);
            dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);

            placeholderLock.Release();
            await Assert.ThrowsAsync<InvalidOperationException>(() => cleanupTask);
        }
        finally
        {
            if (placeholderLock.CurrentCount == 0)
            {
                placeholderLock.Release();
            }
        }

        dbFactory.Verify(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveItems_WaitsForParentOrOwnerPersistenceLockBeforeEvaluatingMetadata(bool useParentId)
    {
        var itemId = Guid.NewGuid();
        var relatedId = Guid.NewGuid();
        while ((itemId.GetHashCode() & 15) == (relatedId.GetHashCode() & 15))
        {
            relatedId = Guid.NewGuid();
        }

        var itemLock = ItemPersistenceService.GetItemPersistenceLock(relatedId);
        var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        dbFactory
            .Setup(factory => factory.CreateDbContext())
            .Throws(new InvalidOperationException("Database context requested"));
        var service = new ItemPersistenceService(
            dbFactory.Object,
            Mock.Of<IServerApplicationHost>(),
            NullLogger<ItemPersistenceService>.Instance);
        var item = new PersistenceLockTestMovie
        {
            Id = itemId,
            ParentId = useParentId ? relatedId : Guid.Empty,
            OwnerId = useParentId ? Guid.Empty : relatedId
        };
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        Exception? saveException = null;
        var saveThread = new Thread(() =>
        {
            started.Set();
            try
            {
                service.SaveItems([item], CancellationToken.None);
            }
            catch (Exception ex)
            {
                saveException = ex;
            }
            finally
            {
                finished.Set();
            }
        });

        itemLock.Wait();
        try
        {
            saveThread.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)), "SaveItems worker did not start.");
            Assert.True(
                SpinWait.SpinUntil(
                    () => !saveThread.IsAlive || (saveThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(5)),
                "SaveItems worker did not reach the lock wait.");
            Assert.True(saveThread.IsAlive, "SaveItems should still be blocked on the related item lock.");
            Assert.False(finished.IsSet);
            dbFactory.Verify(factory => factory.CreateDbContext(), Times.Never);

            itemLock.Release();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(5)), "SaveItems worker did not finish after lock release.");
            // The fixture lacks BaseItem.LibraryManager, so the worker stops before opening the database.
            Assert.IsType<NullReferenceException>(saveException);
            dbFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
        }
        finally
        {
            if (itemLock.CurrentCount == 0)
            {
                itemLock.Release();
            }

            if (saveThread.IsAlive)
            {
                saveThread.Join(TimeSpan.FromSeconds(5));
            }
        }

        dbFactory.Verify(factory => factory.CreateDbContext(), Times.Never);
    }

    [Fact]
    public void ItemValueKeyComparer_TreatsValuesAsCaseInsensitiveWithinSameType()
    {
        var values = new[]
        {
            (ItemValueType.Genre, "Magic"),
            (ItemValueType.Genre, "magic"),
            (ItemValueType.Studios, "Magic")
        };

        var distinctValues = values.Distinct(ItemPersistenceService.ItemValueKeyComparer).ToArray();

        Assert.Equal(2, distinctValues.Length);
    }

    [Fact]
    public void CreateItemValueLookup_UsesCleanValueForKeys()
    {
        var lookup = ItemPersistenceService.CreateItemValueLookup(
            new[]
            {
                new ItemValue
                {
                    ItemValueId = Guid.NewGuid(),
                    Type = ItemValueType.Studios,
                    Value = "Pathé",
                    CleanValue = "pathe"
                }
            });

        Assert.True(lookup.ContainsKey((ItemValueType.Studios, "Pathe")));
    }

    [Fact]
    public void HasProviderValuesChanged_UsesProviderIdentityAndValue()
    {
        var existing = new[]
        {
            new BaseItemProvider { ItemId = Guid.NewGuid(), Item = null!, ProviderId = "tmdb", ProviderValue = "42" },
            new BaseItemProvider { ItemId = Guid.NewGuid(), Item = null!, ProviderId = "imdb", ProviderValue = "tt123" }
        };
        var reordered = new[] { existing[1], existing[0] };
        var changed = new[]
        {
            new BaseItemProvider { ItemId = Guid.NewGuid(), Item = null!, ProviderId = "tmdb", ProviderValue = "42" },
            new BaseItemProvider { ItemId = Guid.NewGuid(), Item = null!, ProviderId = "imdb", ProviderValue = "tt999" }
        };

        Assert.False(ItemPersistenceService.HasProviderValuesChanged(reordered, existing));
        Assert.True(ItemPersistenceService.HasProviderValuesChanged(changed, existing));
        Assert.True(ItemPersistenceService.HasProviderValuesChanged(changed[..1], existing));
    }

    [Fact]
    public void HasLockedMetadataFieldsChanged_UsesFieldIdsAndCardinality()
    {
        var existing = new[]
        {
            new BaseItemMetadataField { Id = 1, ItemId = Guid.NewGuid(), Item = null! },
            new BaseItemMetadataField { Id = 2, ItemId = Guid.NewGuid(), Item = null! }
        };
        var reordered = new[] { existing[1], existing[0] };
        var changed = new[]
        {
            new BaseItemMetadataField { Id = 1, ItemId = Guid.NewGuid(), Item = null! },
            new BaseItemMetadataField { Id = 3, ItemId = Guid.NewGuid(), Item = null! }
        };

        Assert.False(ItemPersistenceService.HasLockedMetadataFieldsChanged(reordered, existing));
        Assert.True(ItemPersistenceService.HasLockedMetadataFieldsChanged(changed, existing));
        Assert.True(ItemPersistenceService.HasLockedMetadataFieldsChanged(changed[..1], existing));
    }

    [Fact]
    public void ClearTrackedNavigationProperties_RemovesTrackedCollectionsBeforeAttach()
    {
        var entity = new BaseItemEntity
        {
            Id = Guid.NewGuid(),
            Type = "Movie"
        };

        entity.Provider = new[]
        {
            new BaseItemProvider
            {
                ItemId = entity.Id,
                Item = entity,
                ProviderId = "tmdb",
                ProviderValue = "1234"
            }
        };

        entity.LockedFields = new[]
        {
            new BaseItemMetadataField
            {
                Id = 1,
                ItemId = entity.Id,
                Item = entity
            }
        };

        entity.Images = new[]
        {
            new BaseItemImageInfo
            {
                Id = Guid.NewGuid(),
                Path = "poster.jpg",
                ImageType = ImageInfoImageType.Primary,
                Width = 100,
                Height = 200,
                ItemId = entity.Id,
                Item = entity
            }
        };

        entity.TrailerTypes = new[]
        {
            new BaseItemTrailerType
            {
                Id = 1,
                ItemId = entity.Id,
                Item = entity
            }
        };

        ItemPersistenceService.ClearTrackedNavigationProperties(entity);

        Assert.Equal("Movie", entity.Type);
        Assert.Null(entity.Provider);
        Assert.Null(entity.LockedFields);
        Assert.Null(entity.Images);
        Assert.Null(entity.TrailerTypes);
    }

    [Theory]
    [InlineData(1062, true)]
    [InlineData(1452, true)]
    [InlineData(1213, true)]
    [InlineData(1205, true)]
    [InlineData(1048, false)]
    public void IsTransientMetadataConflict_RecognizesTransientConstraintErrors(int errorCode, bool expected)
    {
        var ctor = typeof(MySqlConnector.MySqlException).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(MySqlConnector.MySqlErrorCode), typeof(string) },
            null);
        var mySqlException = (MySqlConnector.MySqlException)ctor!.Invoke(new object[] { (MySqlConnector.MySqlErrorCode)errorCode, "Test error message" });
        var ex = new Microsoft.EntityFrameworkCore.DbUpdateException("Test db update exception", mySqlException);

        var result = ItemPersistenceService.IsTransientMetadataConflict(ex);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Reproduces the production failure: re-inserted child rows are mapped by
    /// <c>BaseItemMapper.Map</c> with <c>Item</c> pointing at the freshly mapped
    /// <see cref="BaseItemEntity"/>, so handing them to <c>AddRange</c> makes the EF graph attacher
    /// try to track a second instance whose Id is already tracked.
    /// </summary>
    [Fact]
    public void MappedProviderRows_CannotBeAddedWhileItemWithSameIdIsTracked()
    {
        var itemId = Guid.NewGuid();
        var providers = CreateMappedProviders(itemId);

        using var context = CreateContext();
        TrackExistingItem(context, itemId);

        var conflict = Assert.Throws<InvalidOperationException>(() => context.BaseItemProviders.AddRange(providers));

        Assert.Contains("already being tracked", conflict.Message, StringComparison.Ordinal);
        Assert.Contains("BaseItemEntity", conflict.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The fix: once the back-reference is replaced by the explicit foreign key, the same rows are
    /// accepted alongside the tracked instance and keep pointing at the right parent.
    /// </summary>
    [Fact]
    public void DetachItemBackReferences_MakesMappedRowsSafeToAddWhileItemIsTracked()
    {
        var itemId = Guid.NewGuid();
        var providers = CreateMappedProviders(itemId);

        Assert.Equal(Guid.Empty, providers[0].ItemId);

        ItemPersistenceService.DetachItemBackReferences(itemId, providers: providers);

        Assert.Equal(itemId, providers[0].ItemId);
        Assert.Null(providers[0].Item);

        using var context = CreateContext();
        TrackExistingItem(context, itemId);

        context.BaseItemProviders.AddRange(providers);

        var entry = context.Entry(providers[0]);
        Assert.Equal(EntityState.Added, entry.State);
        Assert.Equal(itemId, entry.Property(nameof(BaseItemProvider.ItemId)).CurrentValue);
    }

    [Fact]
    public void DetachItemBackReferences_ClearsEveryChildBackReferenceAndKeepsForeignKey()
    {
        var itemId = Guid.NewGuid();
        var entity = new BaseItemEntity { Id = itemId, Type = "Series" };

        var providers = new[]
        {
            new BaseItemProvider { Item = entity, ProviderId = "Tmdb", ProviderValue = "1" }
        };

        var lockedFields = new[]
        {
            new BaseItemMetadataField { Id = 1, ItemId = itemId, Item = entity }
        };

        var images = new[]
        {
            new BaseItemImageInfo
            {
                Id = Guid.NewGuid(),
                Path = "poster.jpg",
                ImageType = ImageInfoImageType.Primary,
                Width = 100,
                Height = 200,
                ItemId = itemId,
                Item = entity
            }
        };

        var trailerTypes = new[]
        {
            new BaseItemTrailerType { Id = 1, ItemId = Guid.Empty, Item = entity }
        };

        ItemPersistenceService.DetachItemBackReferences(
            itemId,
            providers: providers,
            lockedFields: lockedFields,
            images: images,
            trailerTypes: trailerTypes);

        Assert.Null(providers[0].Item);
        Assert.Null(lockedFields[0].Item);
        Assert.Null(images[0].Item);
        Assert.Null(trailerTypes[0].Item);

        Assert.Equal(itemId, providers[0].ItemId);
        Assert.Equal(itemId, lockedFields[0].ItemId);
        Assert.Equal(itemId, images[0].ItemId);
        Assert.Equal(itemId, trailerTypes[0].ItemId);
    }

    [Fact]
    public void DetachItemBackReferences_LeavesParentCollectionsUntouched()
    {
        var itemId = Guid.NewGuid();
        var entity = new BaseItemEntity { Id = itemId, Type = "Series" };
        var providers = new[]
        {
            new BaseItemProvider { Item = entity, ProviderId = "Tmdb", ProviderValue = "1" }
        };

        ItemPersistenceService.DetachItemBackReferences(itemId, providers: providers);

        Assert.Equal("Series", entity.Type);
        Assert.Equal(itemId, entity.Id);
    }

    [Theory]
    [InlineData(1040, true)]  // Too many connections
    [InlineData(1042, true)]  // Unable to connect to host (connect timeout, pool exhausted)
    [InlineData(1043, true)]  // Bad handshake
    [InlineData(2006, true)]  // Server has gone away
    [InlineData(2013, true)]  // Lost connection during query
    [InlineData(1213, true)]  // Deadlock: the driver marks deadlocks transient too, and a deadlock is worth replaying. Arriving as a DbUpdateException it is handled by IsTransientMetadataConflict first, so this branch is the backstop.
    [InlineData(1062, false)] // Duplicate entry: a statement failure, handled by IsTransientMetadataConflict
    [InlineData(1064, false)] // Syntax error: retrying cannot help
    public void IsTransientConnectionFailure_RecognizesMariaDbConnectionErrors(int errorCode, bool expected)
    {
        // Reproduces the production shape: EF wraps the driver exception.
        var ex = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            CreateMySqlException(errorCode));

        Assert.Equal(expected, ItemPersistenceService.IsTransientConnectionFailure(ex));
    }

    [Fact]
    public void IsTransientConnectionFailure_RecognizesBareDriverException()
    {
        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(CreateMySqlException(1042)));
    }

    [Fact]
    public void IsTransientConnectionFailure_RecognizesSocketAndTimeoutFailures()
    {
        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(new SocketException(10054)));
        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(new TimeoutException()));
        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(new IOException("database stream read failed")));
        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(
            new InvalidOperationException("wrapped", new SocketException(10054))));
    }

    [Fact]
    public void IsTransientConnectionFailure_RecognizesMySqlConnectTimeoutMessage()
    {
        var exception = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            CreateMySqlException(0, "Connect Timeout expired."));

        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(exception));
    }

    [Fact]
    public void IsTransientConnectionFailure_RejectsUnrelatedFailures()
    {
        Assert.False(ItemPersistenceService.IsTransientConnectionFailure(new InvalidOperationException("identity conflict")));
        Assert.False(ItemPersistenceService.IsTransientConnectionFailure(
            new DbUpdateException("duplicate", CreateMySqlException(1062))));
    }

    private static MySqlConnector.MySqlException CreateMySqlException(int errorCode, string message = "Test error message")
    {
        var ctor = typeof(MySqlConnector.MySqlException).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(MySqlConnector.MySqlErrorCode), typeof(string) },
            null);

        return (MySqlConnector.MySqlException)ctor!.Invoke(new object[] { (MySqlConnector.MySqlErrorCode)errorCode, message });
    }

    private static BaseItemProvider[] CreateMappedProviders(Guid itemId)
    {
        // Mirrors BaseItemMapper.Map: the row is wired through the navigation only, with no
        // explicit ItemId, because EF used to derive the key from the tracked parent.
        return new[]
        {
            new BaseItemProvider
            {
                Item = new BaseItemEntity { Id = itemId, Type = "Series" },
                ProviderId = "Tmdb",
                ProviderValue = "1234"
            }
        };
    }

    private static void TrackExistingItem(MulletaFlixDbContext context, Guid itemId)
    {
        context.BaseItems.Add(new BaseItemEntity { Id = itemId, Type = "Series" });
        context.SaveChanges();
    }

    private static MulletaFlixDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var dbProvider = new Mock<IMulletaFlixDatabaseProvider>();
        dbProvider.Setup(p => p.OnModelCreating(It.IsAny<ModelBuilder>()));

        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        lockingBehavior.Setup(l => l.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>(static (_, action) => action());
        lockingBehavior.Setup(l => l.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<System.Threading.Tasks.Task>>()))
            .Callback<MulletaFlixDbContext, Func<System.Threading.Tasks.Task>>(static (_, func) => func());

        return new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            dbProvider.Object,
            lockingBehavior.Object);
    }

    private sealed class PersistenceLockTestMovie : Movie
    {
        public override bool SupportsAncestors => false;

        public override bool IsTopParent => true;
    }
}
