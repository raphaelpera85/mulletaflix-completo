#pragma warning disable RS0030 // Do not use banned APIs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Playlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Extensions;
using MySqlConnector;
using BaseItemDto = MediaBrowser.Controller.Entities.BaseItem;
using DbLinkedChildType = MulletaFlix.Database.Implementations.Entities.LinkedChildType;
using LinkedChildType = MediaBrowser.Controller.Entities.LinkedChildType;

namespace MulletaFlix.Server.Implementations.Item;

/// <summary>
/// Handles item persistence operations (save, delete, update).
/// </summary>
public class ItemPersistenceService : IItemPersistenceService
{
    internal static readonly IEqualityComparer<(ItemValueType MagicNumber, string Value)> ItemValueKeyComparer = new ItemValueKeyEqualityComparer();
    private static readonly SemaphoreSlim[] _updateOrInsertLocks = Enumerable.Range(0, 16).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    // Path is retained only for migrating legacy links that predate ItemId.
    private static string? GetLegacyLinkedChildPath(LinkedChild linkedChild)
    {
#pragma warning disable CS0618
        return linkedChild.Path;
#pragma warning restore CS0618
    }

    private readonly IDbContextFactory<MulletaFlixDbContext> _dbProvider;
    private readonly IServerApplicationHost _appHost;
    private readonly ILogger<ItemPersistenceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemPersistenceService"/> class.
    /// </summary>
    /// <param name="dbProvider">The database context factory.</param>
    /// <param name="appHost">The application host.</param>
    /// <param name="logger">The logger.</param>
    public ItemPersistenceService(
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IServerApplicationHost appHost,
        ILogger<ItemPersistenceService> logger)
    {
        _dbProvider = dbProvider;
        _appHost = appHost;
        _logger = logger;
    }

    /// <inheritdoc />
    public void DeleteItem(params IReadOnlyList<Guid> ids)
    {
        if (ids is null || ids.Count == 0 || ids.Any(f => f.Equals(BaseItemRepository.PlaceholderId)))
        {
            throw new ArgumentException("Guid can't be empty or the placeholder id.", nameof(ids));
        }

        using var context = _dbProvider.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();

        var date = (DateTime?)DateTime.UtcNow;

        var descendantIds = DescendantQueryHelper.GetOwnedDescendantIdsBatch(context, ids);
        foreach (var id in ids)
        {
            descendantIds.Add(id);
        }

        var extraIds = context.BaseItems
            .Where(e => e.OwnerId.HasValue)
            .WhereOneOrMany(descendantIds.ToList(), e => e.OwnerId!.Value)
            .Select(e => e.Id)
            .ToArray();

        foreach (var extraId in extraIds)
        {
            descendantIds.Add(extraId);
        }

        var relatedItems = descendantIds.ToArray();

        // When batch-deleting, multiple items may have UserData for the same (UserId, CustomDataKey).
        // Moving all of them to PlaceholderId would violate the UNIQUE constraint.
        // Deduplicate by loading keys client-side, keeping the best row per group.
        var batchUserData = context.UserData.WhereOneOrMany(relatedItems, e => e.ItemId);

        var allRows = batchUserData
            .Select(ud => new { ud.ItemId, ud.UserId, ud.CustomDataKey, ud.LastPlayedDate, ud.PlayCount })
            .ToList();

        var duplicateRows = allRows
            .GroupBy(ud => new { ud.UserId, ud.CustomDataKey })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g
                .OrderByDescending(ud => ud.LastPlayedDate)
                .ThenByDescending(ud => ud.PlayCount)
                .Skip(1))
            .ToList();

        if (duplicateRows.Count > 0)
        {
            var dupItemIds = duplicateRows.Select(d => d.ItemId).Distinct().ToList();
            var candidates = context.UserData
                .WhereOneOrMany(dupItemIds, ud => ud.ItemId)
                .ToList();
            var duplicateKeys = duplicateRows
                .Select(d => (d.ItemId, d.UserId, d.CustomDataKey))
                .ToHashSet();
            var toDelete = candidates
                .Where(ud => duplicateKeys.Contains((ud.ItemId, ud.UserId, ud.CustomDataKey)))
                .ToList();
            if (toDelete.Count > 0)
            {
                context.UserData.RemoveRange(toDelete);
            }
        }

        // Delete existing placeholder rows that would conflict with the incoming ones
        context.UserData
            .Join(
                batchUserData,
                placeholder => new { placeholder.UserId, placeholder.CustomDataKey },
                userData => new { userData.UserId, userData.CustomDataKey },
                (placeholder, userData) => placeholder)
            .Where(e => e.ItemId == BaseItemRepository.PlaceholderId)
            .ExecuteDelete();

        batchUserData
            .ExecuteUpdate(e => e
                .SetProperty(f => f.RetentionDate, date)
                .SetProperty(f => f.ItemId, BaseItemRepository.PlaceholderId));

        context.AncestorIds.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.AncestorIds.WhereOneOrMany(relatedItems, e => e.ParentItemId).ExecuteDelete();
        context.AttachmentStreamInfos.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.BaseItemImageInfos.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.BaseItemMetadataFields.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.BaseItemProviders.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.BaseItemTrailerTypes.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.Chapters.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.CustomItemDisplayPreferences.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.ItemDisplayPreferences.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.ItemValues.Where(e => e.BaseItemsMap!.Count == 0).ExecuteDelete();
        context.ItemValuesMap.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.LinkedChildren.WhereOneOrMany(relatedItems, e => e.ParentId).ExecuteDelete();
        context.LinkedChildren.WhereOneOrMany(relatedItems, e => e.ChildId).ExecuteDelete();
        context.BaseItems.WhereOneOrMany(relatedItems, e => e.Id).ExecuteDelete();
        context.KeyframeData.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.MediaSegments.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.MediaStreamInfos.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        var query = context.PeopleBaseItemMap.WhereOneOrMany(relatedItems, e => e.ItemId).Select(f => f.PeopleId).Distinct().ToArray();
        context.PeopleBaseItemMap.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.Peoples.WhereOneOrMany(query, e => e.Id).Where(e => e.BaseItems!.Count == 0).ExecuteDelete();
        context.TrickplayInfos.WhereOneOrMany(relatedItems, e => e.ItemId).ExecuteDelete();
        context.SaveChanges();
        transaction.Commit();
    }

    /// <inheritdoc />
    public void UpdateInheritedValues()
    {
        using var context = _dbProvider.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();

        context.ItemValuesMap.Where(e => e.ItemValue.Type == ItemValueType.InheritedTags).ExecuteDelete();
        context.SaveChanges();

        transaction.Commit();
    }

    /// <inheritdoc />
    public void SaveItems(IReadOnlyList<BaseItemDto> items, CancellationToken cancellationToken)
    {
        UpdateOrInsertItems(items, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveImagesAsync(BaseItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var images = item.ImageInfos.Select(e => BaseItemMapper.MapImageToEntity(item.Id, e)).ToArray();

        var context = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            if (!await context.BaseItems
                .AnyAsync(bi => bi.Id == item.Id, cancellationToken)
                .ConfigureAwait(false))
            {
                _logger.LogWarning("Unable to save ImageInfo for non existing BaseItem");
                return;
            }

            var existingImages = await context.BaseItemImageInfos
                .Where(e => e.ItemId == item.Id)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasChanges = existingImages.Length != images.Length;
            if (!hasChanges)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    var newImg = images[i];
                    var oldImg = existingImages.FirstOrDefault(e => e.ImageType == newImg.ImageType && e.Path == newImg.Path);
                    if (oldImg == null
                        || oldImg.Width != newImg.Width
                        || oldImg.Height != newImg.Height
                        || oldImg.DateModified != newImg.DateModified
                        || !NullableSequenceEqual(oldImg.Blurhash, newImg.Blurhash))
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }

            if (!hasChanges)
            {
                return;
            }

            await context.BaseItemImageInfos
                .Where(e => e.ItemId == item.Id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await context.BaseItemImageInfos
                .AddRangeAsync(images, cancellationToken)
                .ConfigureAwait(false);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ReattachUserDataAsync(BaseItemDto item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (dbContext.ConfigureAwait(false))
        {
            var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var userKeys = item.GetUserDataKeys().ToArray();
                var retentionDate = (DateTime?)null;

                await dbContext.UserData
                    .Where(e => e.ItemId == BaseItemRepository.PlaceholderId)
                    .Where(e => Enumerable.Contains(userKeys, e.CustomDataKey))
                    .ExecuteUpdateAsync(
                        e => e
                            .SetProperty(f => f.ItemId, item.Id)
                            .SetProperty(f => f.RetentionDate, retentionDate),
                        cancellationToken).ConfigureAwait(false);

                item.UserData = await dbContext.UserData
                    .AsNoTracking()
                    .Where(e => e.ItemId == item.Id)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void UpdateOrInsertItems(IReadOnlyList<BaseItemDto> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        cancellationToken.ThrowIfCancellationRequested();

        // Every item in the batch can hash to a different lock stripe. Picking the stripe from
        // just items[0] let two batches that share no items but disagree only on their *first*
        // element run UpdateOrInsertItemsCore concurrently while both touch an item that hashes
        // to a stripe neither of them locked, risking a lost update. Instead, collect every
        // distinct stripe touched by the batch and acquire them all, always in ascending index
        // order, so two batches with overlapping stripe sets can never deadlock on each other.
        var lockIndices = items.Count > 0
            ? items.Select(e => e.Id.GetHashCode() & 15).Distinct().Order().ToArray()
            : [0];

        // This whole path is intentionally synchronous (called from library scans and scheduled
        // tasks). Use the blocking Wait instead of the async-over-sync WaitAsync().GetResult()
        // pattern to avoid spinning the async state machine and holding a thread longer than needed.
        var acquiredCount = 0;
        try
        {
            for (; acquiredCount < lockIndices.Length; acquiredCount++)
            {
                _updateOrInsertLocks[lockIndices[acquiredCount]].Wait(cancellationToken);
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    UpdateOrInsertItemsCore(items);
                    return;
                }
                catch (DbUpdateException ex) when (attempt < 3 && IsTransientMetadataConflict(ex))
                {
                    _logger.LogWarning(ex, "Transient metadata conflict detected on attempt {Attempt} while saving items. Retrying...", attempt);

                    // Short bounded backoff for a genuinely rare path (deadlock / lock-wait timeout).
                    // This is not on the request hot path and is reached only on transient DB conflicts.
                    Thread.Sleep(attempt * 75);
                }
                catch (Exception ex) when (attempt < 3 && IsTransientConnectionFailure(ex))
                {
                    // The MariaDB connection itself failed (pool exhausted, connect timeout, socket
                    // reset during the TLS handshake). The statement never ran, so replaying it is
                    // safe, and without this the whole metadata save for the item was lost and the
                    // library scan reported "Error while performing a library operation".
                    _logger.LogWarning(ex, "Transient MariaDB connection failure on attempt {Attempt} while saving items. Retrying...", attempt);

                    // Longer than the metadata-conflict backoff: a saturated pool needs a moment to
                    // hand a connection back.
                    Thread.Sleep(attempt * 150);
                }
            }
        }
        finally
        {
            for (var i = acquiredCount - 1; i >= 0; i--)
            {
                _updateOrInsertLocks[lockIndices[i]].Release();
            }
        }
    }

    private void UpdateOrInsertItemsCore(IReadOnlyList<BaseItemDto> items)
    {
        var tuples = new List<(BaseItemDto Item, List<Guid>? AncestorIds, BaseItemDto TopParent, IEnumerable<string> UserDataKey, List<string> InheritedTags)>();
        foreach (var item in items.GroupBy(e => e.Id).Select(e => e.Last()).Where(e => e.Id != BaseItemRepository.PlaceholderId))
        {
            var ancestorIds = item.SupportsAncestors ?
                item.GetAncestorIds().Distinct().ToList() :
                null;

            var topParent = item.GetTopParent();

            var userdataKey = item.GetUserDataKeys();
            var inheritedTags = item.GetInheritedTags();

            tuples.Add((item, ancestorIds, topParent, userdataKey, inheritedTags));
        }

        using var context = _dbProvider.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();

        var ids = tuples.Select(f => f.Item.Id).ToArray();

        // Fetch the *tracked* entities rather than just their ids. Change detection needs the
        // original values, and it is what lets an unchanged rescan skip the UPDATE entirely
        // instead of rewriting every column (and therefore every one of the 26 secondary indexes).
        var existingItems = context.BaseItems
            .Where(e => Enumerable.Contains(ids, e.Id))
            .ToDictionary(e => e.Id);

        // 1. Save Base Item Entities
        SaveBaseItemEntities(context, tuples, existingItems);

        // 2. Save Item Values Maps
        SaveItemValues(context, tuples, ids);

        // 3. Save Ancestor IDs
        SaveAncestorIds(context, tuples);

        context.SaveChanges();

        // 4. Save Linked Children (for Folders and Videos)
        var folderIds = tuples
            .Where(t => t.Item is Folder)
            .Select(t => t.Item.Id)
            .ToList();

        var videoIds = tuples
            .Where(t => t.Item is Video)
            .Select(t => t.Item.Id)
            .ToList();

        if (folderIds.Count > 0 || videoIds.Count > 0)
        {
            SaveLinkedChildren(context, tuples, folderIds, videoIds);
        }

        context.SaveChanges();
        transaction.Commit();
    }

    internal static bool IsTransientMetadataConflict(DbUpdateException exception)
    {
        if (exception.InnerException is MySqlException mySqlException)
        {
            // 1062 = Duplicate entry (IX_ItemValues_Type_Value)
            // 1452 = Cannot add or update a child row: a foreign key constraint fails
            // 1213 = Deadlock found when trying to get lock
            // 1205 = Lock wait timeout exceeded
            return mySqlException.Number is 1062 or 1452 or 1213 or 1205;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a save failed because the MariaDB connection did, rather than because the
    /// statement was rejected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These failures never reach <see cref="DbUpdateException"/>: the driver throws before the
    /// statement is sent. Observed shapes are an <see cref="InvalidOperationException"/> from EF
    /// ("An exception has been raised that is likely due to a transient failure") wrapping a
    /// <see cref="MySqlException"/>, and a bare <see cref="MySqlException"/> ("Connect Timeout
    /// expired. All pooled connections are in use." / "Couldn't connect to server"). None of them
    /// were retried before, so a single blip discarded the item's whole metadata save.
    /// </para>
    /// <para>
    /// <see cref="MySqlException"/> is the exception type of the MySqlConnector driver, which is
    /// what talks to MariaDB; the transient flag and the server error codes it carries are the
    /// MariaDB ones.
    /// </para>
    /// </remarks>
    /// <param name="exception">The exception thrown by the save.</param>
    /// <returns><see langword="true"/> when the operation is worth replaying.</returns>
    internal static bool IsTransientConnectionFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mySqlException)
            {
                // 1040 = Too many connections, 1042 = Unable to connect to host,
                // 1043 = Bad handshake, 2002/2003 = can't connect, 2006 = server gone away,
                // 2013 = lost connection during query.
                return mySqlException.IsTransient
                    || mySqlException.Number is 1040 or 1042 or 1043 or 2002 or 2003 or 2006 or 2013;
            }

            if (current is SocketException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static List<(ItemValueType MagicNumber, string Value)> GetItemValuesToSave(BaseItemDto item, List<string> inheritedTags)
    {
        var list = new List<(ItemValueType, string)>();

        if (item is IHasArtist hasArtist)
        {
            list.AddRange(hasArtist.Artists.Select(i => ((ItemValueType)0, i)));
        }

        if (item is IHasAlbumArtist hasAlbumArtist)
        {
            list.AddRange(hasAlbumArtist.AlbumArtists.Select(i => (ItemValueType.AlbumArtist, i)));
        }

        list.AddRange(item.Genres.Select(i => (ItemValueType.Genre, i)));
        list.AddRange(item.Studios.Select(i => (ItemValueType.Studios, i)));
        list.AddRange(item.Tags.Select(i => (ItemValueType.Tags, i)));

        list.AddRange(inheritedTags.Select(i => (ItemValueType.InheritedTags, i)));

        list.RemoveAll(i => string.IsNullOrWhiteSpace(i.Item2));

        return list;
    }

    internal static Dictionary<(ItemValueType MagicNumber, string Value), ItemValue> CreateItemValueLookup(IEnumerable<ItemValue> itemValues)
    {
        var lookup = new Dictionary<(ItemValueType MagicNumber, string Value), ItemValue>(ItemValueKeyComparer);

        foreach (var itemValue in itemValues)
        {
            lookup[NormalizeItemValueKey(itemValue.Type, itemValue.CleanValue)] = itemValue;
        }

        return lookup;
    }

    private static (ItemValueType MagicNumber, string Value) NormalizeItemValueKey(ItemValueType magicNumber, string value)
    {
        return (magicNumber, value.GetCleanValue());
    }

    internal static Dictionary<Guid, Dictionary<Guid, ItemValueMap>> CreateItemValueMapLookup(IEnumerable<ItemValueMap> mappedValues)
    {
        var lookup = new Dictionary<Guid, Dictionary<Guid, ItemValueMap>>();

        foreach (var mappedValue in mappedValues)
        {
            if (!lookup.TryGetValue(mappedValue.ItemId, out var itemLookup))
            {
                itemLookup = new Dictionary<Guid, ItemValueMap>();
                lookup[mappedValue.ItemId] = itemLookup;
            }

            itemLookup[mappedValue.ItemValueId] = mappedValue;
        }

        return lookup;
    }

    internal static Dictionary<Guid, Dictionary<Guid, AncestorId>> CreateAncestorLookup(IEnumerable<AncestorId> ancestorIds)
    {
        var lookup = new Dictionary<Guid, Dictionary<Guid, AncestorId>>();

        foreach (var ancestorId in ancestorIds)
        {
            if (!lookup.TryGetValue(ancestorId.ItemId, out var itemLookup))
            {
                itemLookup = new Dictionary<Guid, AncestorId>();
                lookup[ancestorId.ItemId] = itemLookup;
            }

            itemLookup[ancestorId.ParentItemId] = ancestorId;
        }

        return lookup;
    }

    internal static Dictionary<Guid, LinkedChildEntity> CreateLinkedChildLookup(IEnumerable<LinkedChildEntity> linkedChildren)
    {
        var lookup = new Dictionary<Guid, LinkedChildEntity>();

        foreach (var linkedChild in linkedChildren)
        {
            if (!lookup.ContainsKey(linkedChild.ChildId))
            {
                lookup[linkedChild.ChildId] = linkedChild;
            }
        }

        return lookup;
    }

    private void SaveBaseItemEntities(
        MulletaFlixDbContext context,
        List<(BaseItemDto Item, List<Guid>? AncestorIds, BaseItemDto TopParent, IEnumerable<string> UserDataKey, List<string> InheritedTags)> tuples,
        Dictionary<Guid, BaseItemEntity> existingItems)
    {
        var existingIdsList = existingItems.Keys.ToList();
        var existingProviders = existingIdsList.Count > 0
            ? context.BaseItemProviders
                .AsNoTracking()
                .WhereOneOrMany(existingIdsList, e => e.ItemId)
                .ToList()
                .GroupBy(e => e.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<Guid, List<BaseItemProvider>>();

        var existingMetadataFields = existingIdsList.Count > 0
            ? context.BaseItemMetadataFields
                .AsNoTracking()
                .WhereOneOrMany(existingIdsList, e => e.ItemId)
                .ToList()
                .GroupBy(e => e.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<Guid, List<BaseItemMetadataField>>();

        foreach (var item in tuples)
        {
            var entity = BaseItemMapper.Map(item.Item, _appHost);
            entity.TopParentId = item.TopParent?.Id;

            if (!existingItems.TryGetValue(entity.Id, out var current))
            {
                context.BaseItems.Add(entity);
            }
            else
            {
                var currentProviders = entity.Provider?.ToArray() ?? [];
                var currentLockedFields = entity.LockedFields?.ToArray();
                var currentImages = entity.Images?.ToArray();
                var currentTrailerTypes = entity.TrailerTypes?.ToArray();

                ClearTrackedNavigationProperties(entity);

                // The rows below are about to be re-inserted through AddRange, which runs them
                // through the EF graph attacher. Every one of them was mapped with "Item" pointing
                // at the fresh instance created for this scan, so the attacher would follow that
                // navigation, try to track a second BaseItemEntity with the Id that is already
                // tracked (the row fetched into existingItems) and abort the whole save with
                // IdentityMap.ThrowIdentityConflict. Replacing the navigation with the explicit
                // foreign key keeps the relationship and removes the graph edge.
                DetachItemBackReferences(
                    entity.Id,
                    providers: currentProviders,
                    lockedFields: currentLockedFields,
                    images: currentImages,
                    trailerTypes: currentTrailerTypes);

                // Check if Providers changed
                var oldProviders = existingProviders.GetValueOrDefault(entity.Id) ?? new List<BaseItemProvider>();
                bool providersChanged = currentProviders.Length != oldProviders.Count ||
                    currentProviders.Any(cp => !oldProviders.Any(op => op.ProviderId == cp.ProviderId && op.ProviderValue == cp.ProviderValue));

                if (providersChanged)
                {
                    context.BaseItemProviders.Where(e => e.ItemId == entity.Id).ExecuteDelete();
                    if (currentProviders.Length > 0)
                    {
                        context.BaseItemProviders.AddRange(currentProviders);
                    }
                }

                // Check if Images changed (only touch if entity.Images is explicitly defined/not null)
                if (currentImages is not null)
                {
                    context.BaseItemImageInfos.Where(e => e.ItemId == entity.Id).ExecuteDelete();
                    if (currentImages.Length > 0)
                    {
                        context.BaseItemImageInfos.AddRange(currentImages);
                    }
                }

                // Check if LockedFields changed
                var oldLockedFields = existingMetadataFields.GetValueOrDefault(entity.Id) ?? new List<BaseItemMetadataField>();
                bool lockedFieldsChanged = currentLockedFields is not null &&
                    (currentLockedFields.Length != oldLockedFields.Count ||
                    currentLockedFields.Any(cf => !oldLockedFields.Any(of => of.Id == cf.Id)));

                if (lockedFieldsChanged)
                {
                    context.BaseItemMetadataFields.Where(e => e.ItemId == entity.Id).ExecuteDelete();
                    if (currentLockedFields.Length > 0)
                    {
                        context.BaseItemMetadataFields.AddRange(currentLockedFields);
                    }
                }

                if (currentTrailerTypes is not null)
                {
                    context.BaseItemTrailerTypes.Where(e => e.ItemId == entity.Id).ExecuteDelete();
                    if (currentTrailerTypes.Length > 0)
                    {
                        context.BaseItemTrailerTypes.AddRange(currentTrailerTypes);
                    }
                }

                // Copy the mapped values onto the already-tracked instance. EF compares them with the
                // originals and marks only the columns that actually differ, so an unchanged rescan
                // produces either a narrow UPDATE or no UPDATE at all. The previous
                // Attach(entity).State = Modified marked all ~70 properties dirty, which forced a
                // full-column UPDATE — and therefore a write to all 26 secondary indexes — for every
                // item on every scan. That write amplification is what grew baseitems to 264 MB of
                // index against 55 MB of data.
                context.Entry(current).CurrentValues.SetValues(entity);
            }
        }
    }

    internal static void ClearTrackedNavigationProperties(BaseItemEntity entity)
    {
        entity.Provider = null;
        entity.LockedFields = null;
        entity.Images = null;
        entity.TrailerTypes = null;
    }

    /// <summary>
    /// Replaces the parent navigation of child rows that are about to be re-inserted with the
    /// explicit foreign key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BaseItemMapper.Map(BaseItemDto, IServerApplicationHost?)"/> wires every child row
    /// back to the <see cref="BaseItemEntity"/> instance it just created — for providers it does not
    /// even set <c>ItemId</c>, because EF used to derive the key from the navigation. That is fine
    /// while the parent is new and untracked, but when the parent already exists the row is added
    /// next to the instance fetched into <c>existingItems</c>, and the graph attacher then reports
    /// "The instance of entity type 'BaseItemEntity' cannot be tracked because another instance with
    /// the same key value for {'Id'} is already being tracked" — losing the entire metadata save.
    /// </para>
    /// <para>
    /// Clearing the navigation and writing the key explicitly removes the graph edge while keeping
    /// the relationship intact. It is the same shape <see cref="BaseItemMapper.MapImageToEntity"/>
    /// already uses for images.
    /// </para>
    /// </remarks>
    /// <param name="itemId">Id of the parent item the rows belong to.</param>
    /// <param name="providers">Provider rows to detach.</param>
    /// <param name="lockedFields">Locked metadata field rows to detach.</param>
    /// <param name="images">Image rows to detach.</param>
    /// <param name="trailerTypes">Trailer type rows to detach.</param>
    internal static void DetachItemBackReferences(
        Guid itemId,
        BaseItemProvider[]? providers = null,
        BaseItemMetadataField[]? lockedFields = null,
        BaseItemImageInfo[]? images = null,
        BaseItemTrailerType[]? trailerTypes = null)
    {
        if (providers is not null)
        {
            foreach (var provider in providers)
            {
                provider.ItemId = itemId;
                provider.Item = null!;
            }
        }

        if (lockedFields is not null)
        {
            foreach (var lockedField in lockedFields)
            {
                lockedField.ItemId = itemId;
                lockedField.Item = null!;
            }
        }

        if (images is not null)
        {
            foreach (var image in images)
            {
                image.ItemId = itemId;
                image.Item = null!;
            }
        }

        if (trailerTypes is not null)
        {
            foreach (var trailerType in trailerTypes)
            {
                trailerType.ItemId = itemId;
                trailerType.Item = null!;
            }
        }
    }

    private void SaveItemValues(
        MulletaFlixDbContext context,
        List<(BaseItemDto Item, List<Guid>? AncestorIds, BaseItemDto TopParent, IEnumerable<string> UserDataKey, List<string> InheritedTags)> tuples,
        Guid[] ids)
    {
        var itemValueMaps = tuples
            .Select(e => (e.Item, Values: GetItemValuesToSave(e.Item, e.InheritedTags)))
            .ToArray();
        var allListedItemValues = itemValueMaps
            .SelectMany(f => f.Values)
            .Distinct(ItemValueKeyComparer)
            .ToArray();

        var types = allListedItemValues.Select(e => e.MagicNumber).Distinct().ToArray();
        var cleanValues = allListedItemValues.Select(e => e.Value.GetCleanValue()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var allListedItemValuesSet = allListedItemValues.ToHashSet(ItemValueKeyComparer);

        InsertItemValuesIgnoreDuplicates(context, allListedItemValues);

        var existingValues = context.ItemValues
            .AsNoTracking()
            .Where(e => Enumerable.Contains(types, e.Type) && Enumerable.Contains(cleanValues, e.CleanValue))
            .AsEnumerable()
            .Where(e => allListedItemValuesSet.Contains((e.Type, e.CleanValue)))
            .DistinctBy(e => (e.Type, e.CleanValue), ItemValueKeyComparer)
            .ToArray();

        var itemValuesStore = existingValues;
        var itemValuesStoreLookup = CreateItemValueLookup(itemValuesStore);
        var valueMap = itemValueMaps
            .Select(f => (f.Item, Values: f.Values
                .Select(e => itemValuesStoreLookup.TryGetValue(NormalizeItemValueKey(e.MagicNumber, e.Value), out var val) ? val : null)
                .Where(e => e is not null)
                .Select(e => e!)
                .DistinctBy(e => e.ItemValueId)
                .ToArray()))
            .ToArray();

        var mappedValues = context.ItemValuesMap.Where(e => Enumerable.Contains(ids, e.ItemId)).ToList();
        var mappedValuesByItemId = CreateItemValueMapLookup(mappedValues);

        foreach (var item in valueMap)
        {
            var itemMappedValues = mappedValuesByItemId.GetValueOrDefault(item.Item.Id);
            foreach (var itemValue in item.Values)
            {
                if (itemMappedValues is null || !itemMappedValues.Remove(itemValue.ItemValueId, out _))
                {
                    context.ItemValuesMap.Add(new ItemValueMap()
                    {
                        Item = null!,
                        ItemId = item.Item.Id,
                        ItemValue = null!,
                        ItemValueId = itemValue.ItemValueId
                    });
                }
            }

            if (itemMappedValues is not null && itemMappedValues.Count > 0)
            {
                context.ItemValuesMap.RemoveRange(itemMappedValues.Values);
            }
        }
    }

    private static void InsertItemValuesIgnoreDuplicates(
        MulletaFlixDbContext context,
        IReadOnlyList<(ItemValueType MagicNumber, string Value)> allListedItemValues)
    {
        if (allListedItemValues.Count == 0)
        {
            return;
        }

        // Insert in a deterministic order. INSERT IGNORE that hits a duplicate key takes a shared
        // lock on the conflicting row in the unique (Type, Value) index, so two concurrent
        // transactions inserting the same value set in different orders acquire those locks in
        // opposite directions and deadlock — which is what the retry helper in MulletaFlixDbContext
        // (error 1213/1205) has been absorbing ~14 times a day. Sorting by the same key in every
        // transaction removes the circular wait without changing which rows are written.
        var orderedValues = allListedItemValues
            .OrderBy(v => v.MagicNumber)
            .ThenBy(v => v.Value.GetCleanValue(), StringComparer.Ordinal)
            .ToArray();

        var commandText = new StringBuilder("INSERT IGNORE INTO `ItemValues` (`ItemValueId`, `Type`, `Value`, `CleanValue`) VALUES ");
        var parameters = new List<object>(orderedValues.Length * 4);

        for (var index = 0; index < orderedValues.Length; index++)
        {
            if (index > 0)
            {
                commandText.Append(", ");
            }

            var value = orderedValues[index];
            commandText.Append("(@p").Append(index).Append("_id, @p").Append(index).Append("_type, @p").Append(index).Append("_value, @p").Append(index).Append("_cleanValue)");
            parameters.Add(new MySqlParameter($"@p{index}_id", MySqlDbType.VarChar) { Value = Guid.NewGuid().ToString() });
            parameters.Add(new MySqlParameter($"@p{index}_type", MySqlDbType.Int32) { Value = (int)value.MagicNumber });
            parameters.Add(new MySqlParameter($"@p{index}_value", MySqlDbType.VarChar) { Value = value.Value });
            parameters.Add(new MySqlParameter($"@p{index}_cleanValue", MySqlDbType.VarChar) { Value = value.Value.GetCleanValue() });
        }

        context.Database.ExecuteSqlRaw(commandText.ToString(), parameters.ToArray());
    }

    private void SaveAncestorIds(
        MulletaFlixDbContext context,
        List<(BaseItemDto Item, List<Guid>? AncestorIds, BaseItemDto TopParent, IEnumerable<string> UserDataKey, List<string> InheritedTags)> tuples)
    {
        var itemsWithAncestors = tuples
            .Where(t => t.Item.SupportsAncestors && t.AncestorIds != null)
            .Select(t => t.Item.Id)
            .ToList();

        var allExistingAncestorIds = itemsWithAncestors.Count > 0
            ? context.AncestorIds
                .Where(e => itemsWithAncestors.Contains(e.ItemId))
                .ToList()
            : [];
        var existingAncestorIdsByItemId = CreateAncestorLookup(allExistingAncestorIds);

        var allRequestedAncestorIds = tuples
            .Where(t => t.Item.SupportsAncestors && t.AncestorIds != null)
            .SelectMany(t => t.AncestorIds!)
            .Distinct()
            .ToList();

        var validAncestorIdsSet = allRequestedAncestorIds.Count > 0
            ? context.BaseItems
                .Where(e => allRequestedAncestorIds.Contains(e.Id))
                .Select(f => f.Id)
                .ToHashSet()
            : new HashSet<Guid>();

        foreach (var item in tuples)
        {
            if (item.Item.SupportsAncestors && item.AncestorIds != null)
            {
                var validAncestorIds = item.AncestorIds.Where(id => validAncestorIdsSet.Contains(id)).ToArray();
                var existingAncestorIds = existingAncestorIdsByItemId.GetValueOrDefault(item.Item.Id);
                foreach (var ancestorId in validAncestorIds)
                {
                    if (existingAncestorIds is null || !existingAncestorIds.Remove(ancestorId, out _))
                    {
                        context.AncestorIds.Add(new AncestorId()
                        {
                            ParentItemId = ancestorId,
                            ItemId = item.Item.Id,
                            Item = null!,
                            ParentItem = null!
                        });
                    }
                }

                if (existingAncestorIds is not null && existingAncestorIds.Count > 0)
                {
                    context.AncestorIds.RemoveRange(existingAncestorIds.Values);
                }
            }
        }
    }

    private void SaveLinkedChildren(
        MulletaFlixDbContext context,
        List<(BaseItemDto Item, List<Guid>? AncestorIds, BaseItemDto TopParent, IEnumerable<string> UserDataKey, List<string> InheritedTags)> tuples,
        List<Guid> folderIds,
        List<Guid> videoIds)
    {
        var allParentIds = folderIds.Concat(videoIds).Distinct().ToList();
        var allLinkedChildren = allParentIds.Count > 0
            ? context.LinkedChildren
                .Where(e => allParentIds.Contains(e.ParentId))
                .ToList()
            : new List<LinkedChildEntity>();

        var allLinkedChildrenByParent = allLinkedChildren
            .GroupBy(e => e.ParentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 1. Batch path resolution
        var allFolderPathsToResolve = tuples
            .Where(t => t.Item is Folder)
            .SelectMany(t => ((Folder)t.Item).LinkedChildren)
            .Where(lc => (!lc.ItemId.HasValue || lc.ItemId.Value.IsEmpty()) && !string.IsNullOrEmpty(GetLegacyLinkedChildPath(lc)))
            .Select(GetLegacyLinkedChildPath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct();

        var allVideoPathsToResolve = tuples
            .Where(t => t.Item is Video)
            .SelectMany(t => ((Video)t.Item).LocalAlternateVersions)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct();

        var allPathsToResolve = allFolderPathsToResolve
            .Concat(allVideoPathsToResolve)
            .Distinct()
            .ToList();

        var pathToIdMap = allPathsToResolve.Count > 0
            ? context.BaseItems
                .Where(e => e.Path != null && allPathsToResolve.Contains(e.Path))
                .Select(e => new { e.Path, e.Id })
                .GroupBy(e => e.Path!)
                .ToDictionary(g => g.Key, g => g.First().Id)
            : new Dictionary<string, Guid>();

        // 2. Collect all child IDs to check
        var allChildIdsToCheck = new HashSet<Guid>();
        foreach (var item in tuples)
        {
            if (item.Item is Folder folder)
            {
                foreach (var linkedChild in folder.LinkedChildren)
                {
                    var childId = linkedChild.ItemId;
                    var legacyPath = GetLegacyLinkedChildPath(linkedChild);
                    if ((!childId.HasValue || childId.Value.IsEmpty()) && !string.IsNullOrEmpty(legacyPath))
                    {
                        if (pathToIdMap.TryGetValue(legacyPath, out var resolvedId))
                        {
                            childId = resolvedId;
                        }
                    }
                    if (childId.HasValue && !childId.Value.IsEmpty())
                    {
                        allChildIdsToCheck.Add(childId.Value);
                    }
                }
            }
            else if (item.Item is Video video)
            {
                foreach (var path in video.LocalAlternateVersions)
                {
                    if (!string.IsNullOrEmpty(path) && pathToIdMap.TryGetValue(path, out var childId))
                    {
                        allChildIdsToCheck.Add(childId);
                    }
                }
                foreach (var linkedChild in video.LinkedAlternateVersions)
                {
                    if (linkedChild.ItemId.HasValue && !linkedChild.ItemId.Value.IsEmpty())
                    {
                        allChildIdsToCheck.Add(linkedChild.ItemId.Value);
                    }
                }
            }
        }

        // 3. Batch query existence of all child IDs
        var allExistingChildIds = allChildIdsToCheck.Count > 0
            ? context.BaseItems
                .WhereOneOrMany(allChildIdsToCheck.ToList(), e => e.Id)
                .Select(e => e.Id)
                .ToHashSet()
            : new HashSet<Guid>();

        // 4. Batch query all potential orphaned local version items
        var allOrphanedLocalVersionIds = new HashSet<Guid>();
        foreach (var item in tuples)
        {
            if (item.Item is Video video)
            {
                var existingLinkedChildren = (allLinkedChildrenByParent.GetValueOrDefault(video.Id) ?? new List<LinkedChildEntity>())
                    .Where(e => (int)e.ChildType == 2 || (int)e.ChildType == 3)
                    .ToList();

                var newLinkedChildren = new List<Guid>();
                if (video.LocalAlternateVersions.Length > 0)
                {
                    foreach (var path in video.LocalAlternateVersions)
                    {
                        if (!string.IsNullOrEmpty(path) && pathToIdMap.TryGetValue(path, out var childId))
                        {
                            newLinkedChildren.Add(childId);
                        }
                    }
                }
                if (video.LinkedAlternateVersions.Length > 0)
                {
                    foreach (var linkedChild in video.LinkedAlternateVersions)
                    {
                        if (linkedChild.ItemId.HasValue && !linkedChild.ItemId.Value.IsEmpty())
                        {
                            newLinkedChildren.Add(linkedChild.ItemId.Value);
                        }
                    }
                }

                var newLinkedChildrenSet = newLinkedChildren.ToHashSet();
                foreach (var existingLink in existingLinkedChildren)
                {
                    if (!newLinkedChildrenSet.Contains(existingLink.ChildId))
                    {
                        if (existingLink.ChildType == DbLinkedChildType.LocalAlternateVersion)
                        {
                            allOrphanedLocalVersionIds.Add(existingLink.ChildId);
                        }
                    }
                }
            }
        }

        var orphanedItems = allOrphanedLocalVersionIds.Count > 0
            ? context.BaseItems
                .WhereOneOrMany(allOrphanedLocalVersionIds.ToList(), e => e.Id)
                .Where(e => e.OwnerId.HasValue)
                .ToDictionary(e => (e.Id, e.OwnerId!.Value), e => e)
            : new Dictionary<(Guid Id, Guid OwnerId), BaseItemEntity>();

        // 5. Process Folder Linked Children
        foreach (var item in tuples)
        {
            if (item.Item is Folder folder)
            {
                var existingLinkedChildren = allLinkedChildrenByParent.GetValueOrDefault(item.Item.Id)?.ToList() ?? new List<LinkedChildEntity>();
                var existingLinkedChildrenByChildId = CreateLinkedChildLookup(existingLinkedChildren);
                var matchedLinkedChildren = new HashSet<LinkedChildEntity>();
                if (folder.LinkedChildren.Length > 0)
                {
                    var resolvedChildren = new List<(LinkedChild Child, Guid ChildId)>();
                    foreach (var linkedChild in folder.LinkedChildren)
                    {
                        var childItemId = linkedChild.ItemId;
                        if (!childItemId.HasValue || childItemId.Value.IsEmpty())
                        {
                            var legacyPath = GetLegacyLinkedChildPath(linkedChild);
                            if (!string.IsNullOrEmpty(legacyPath) && pathToIdMap.TryGetValue(legacyPath, out var resolvedId))
                            {
                                childItemId = resolvedId;
                            }
                        }

                        if (childItemId.HasValue && !childItemId.Value.IsEmpty())
                        {
                            resolvedChildren.Add((linkedChild, childItemId.Value));
                        }
                    }

                    resolvedChildren = resolvedChildren
                        .GroupBy(c => c.ChildId)
                        .Select(g => g.Last())
                        .ToList();

                    var isPlaylist = folder is Playlist;
                    var sortOrder = 0;
                    foreach (var (linkedChild, childId) in resolvedChildren)
                    {
                        if (!allExistingChildIds.Contains(childId))
                        {
                            _logger.LogWarning(
                                "Skipping LinkedChild for parent {ParentName} ({ParentId}): child item {ChildId} does not exist in database",
                                item.Item.Name,
                                item.Item.Id,
                                childId);
                            continue;
                        }

                        if (!existingLinkedChildrenByChildId.Remove(childId, out var existingLink))
                        {
                            context.LinkedChildren.Add(new LinkedChildEntity()
                            {
                                ParentId = item.Item.Id,
                                ChildId = childId,
                                ChildType = (DbLinkedChildType)linkedChild.Type,
                                SortOrder = isPlaylist ? sortOrder : null
                            });
                        }
                        else
                        {
                            existingLink.SortOrder = isPlaylist ? sortOrder : null;
                            existingLink.ChildType = (DbLinkedChildType)linkedChild.Type;
                            matchedLinkedChildren.Add(existingLink);
                        }

                        sortOrder++;
                    }
                }

                if (existingLinkedChildren.Count > 0)
                {
                    var linkedChildrenToRemove = existingLinkedChildren
                        .Where(e => !matchedLinkedChildren.Contains(e))
                        .ToList();

                    if (linkedChildrenToRemove.Count > 0)
                    {
                        context.LinkedChildren.RemoveRange(linkedChildrenToRemove);
                    }
                }
            }

            // 6. Process Video Linked Children
            if (item.Item is Video video)
            {
                var existingLinkedChildren = (allLinkedChildrenByParent.GetValueOrDefault(video.Id) ?? new List<LinkedChildEntity>())
                    .Where(e => (int)e.ChildType == 2 || (int)e.ChildType == 3)
                    .ToList();
                var existingLinkedChildrenByChildId = CreateLinkedChildLookup(existingLinkedChildren);
                var matchedLinkedChildren = new HashSet<LinkedChildEntity>();

                var newLinkedChildren = new List<(Guid ChildId, LinkedChildType Type)>();

                if (video.LocalAlternateVersions.Length > 0)
                {
                    foreach (var path in video.LocalAlternateVersions)
                    {
                        if (!string.IsNullOrEmpty(path) && pathToIdMap.TryGetValue(path, out var childId))
                        {
                            newLinkedChildren.Add((childId, LinkedChildType.LocalAlternateVersion));
                        }
                    }
                }

                if (video.LinkedAlternateVersions.Length > 0)
                {
                    foreach (var linkedChild in video.LinkedAlternateVersions)
                    {
                        if (linkedChild.ItemId.HasValue && !linkedChild.ItemId.Value.IsEmpty())
                        {
                            newLinkedChildren.Add((linkedChild.ItemId.Value, LinkedChildType.LinkedAlternateVersion));
                        }
                    }
                }

                newLinkedChildren = newLinkedChildren
                    .GroupBy(c => c.ChildId)
                    .Select(g => g.Last())
                    .ToList();

                int sortOrder = 0;
                foreach (var (childId, childType) in newLinkedChildren)
                {
                    if (!allExistingChildIds.Contains(childId))
                    {
                        _logger.LogWarning(
                            "Skipping alternate version for video {VideoName} ({VideoId}): child item {ChildId} does not exist in database",
                            video.Name,
                            video.Id,
                            childId);
                        continue;
                    }

                    if (!existingLinkedChildrenByChildId.Remove(childId, out var existingLink))
                    {
                        context.LinkedChildren.Add(new LinkedChildEntity
                        {
                            ParentId = video.Id,
                            ChildId = childId,
                            ChildType = (DbLinkedChildType)childType,
                            SortOrder = sortOrder
                        });
                    }
                    else
                    {
                        existingLink.ChildType = (DbLinkedChildType)childType;
                        existingLink.SortOrder = sortOrder;
                        matchedLinkedChildren.Add(existingLink);
                    }

                    sortOrder++;
                }

                if (existingLinkedChildren.Count > 0)
                {
                    var remainingLinkedChildren = existingLinkedChildren
                        .Where(e => !matchedLinkedChildren.Contains(e))
                        .ToList();

                    if (remainingLinkedChildren.Count > 0)
                    {
                        context.LinkedChildren.RemoveRange(remainingLinkedChildren);
                    }

                    var orphanedItemsToRemove = new List<BaseItemEntity>();
                    foreach (var remaining in remainingLinkedChildren)
                    {
                        if (remaining.ChildType == DbLinkedChildType.LocalAlternateVersion &&
                            orphanedItems.TryGetValue((remaining.ChildId, video.Id), out var orphanedItem))
                        {
                            orphanedItemsToRemove.Add(orphanedItem);
                        }
                    }

                    if (orphanedItemsToRemove.Count > 0)
                    {
                        _logger.LogInformation(
                            "Deleting {Count} orphaned LocalAlternateVersion items for video {VideoName} ({VideoId})",
                            orphanedItemsToRemove.Count,
                            video.Name,
                            video.Id);
                        context.BaseItems.RemoveRange(orphanedItemsToRemove);
                    }
                }
            }
        }
    }

    private static bool NullableSequenceEqual(byte[]? a, byte[]? b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.SequenceEqual(b);
    }

    private sealed class ItemValueKeyEqualityComparer : IEqualityComparer<(ItemValueType MagicNumber, string Value)>
    {
        public bool Equals((ItemValueType MagicNumber, string Value) x, (ItemValueType MagicNumber, string Value) y)
        {
            return x.MagicNumber == y.MagicNumber
                && string.Equals(NormalizeValue(x.Value), NormalizeValue(y.Value), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((ItemValueType MagicNumber, string Value) obj)
        {
            return HashCode.Combine(obj.MagicNumber, StringComparer.OrdinalIgnoreCase.GetHashCode(NormalizeValue(obj.Value)));
        }

        private static string NormalizeValue(string value)
        {
            return value.GetCleanValue();
        }
    }
}
