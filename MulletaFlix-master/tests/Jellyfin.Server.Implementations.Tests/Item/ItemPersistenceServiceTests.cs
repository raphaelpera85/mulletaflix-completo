using System;
using System.Linq;
using System.Net.Sockets;
using MediaBrowser.Model.Entities;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Item;

public class ItemPersistenceServiceTests
{
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
        Assert.True(ItemPersistenceService.IsTransientConnectionFailure(
            new InvalidOperationException("wrapped", new SocketException(10054))));
    }

    [Fact]
    public void IsTransientConnectionFailure_RejectsUnrelatedFailures()
    {
        Assert.False(ItemPersistenceService.IsTransientConnectionFailure(new InvalidOperationException("identity conflict")));
        Assert.False(ItemPersistenceService.IsTransientConnectionFailure(
            new DbUpdateException("duplicate", CreateMySqlException(1062))));
    }

    private static MySqlConnector.MySqlException CreateMySqlException(int errorCode)
    {
        var ctor = typeof(MySqlConnector.MySqlException).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(MySqlConnector.MySqlErrorCode), typeof(string) },
            null);

        return (MySqlConnector.MySqlException)ctor!.Invoke(new object[] { (MySqlConnector.MySqlErrorCode)errorCode, "Test error message" });
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
}
