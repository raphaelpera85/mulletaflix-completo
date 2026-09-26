using System;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MulletaFlix.Database.Implementations.ModelConfiguration;

/// <summary>
/// Configuration for BaseItem.
/// </summary>
public class BaseItemConfiguration : IEntityTypeConfiguration<BaseItemEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BaseItemEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(100);
        builder.Property(e => e.SeriesPresentationUniqueKey).HasMaxLength(100);
        builder.Property(e => e.PresentationUniqueKey).HasMaxLength(100);
        builder.Property(e => e.SortName).HasMaxLength(255);
        builder.Property(e => e.CleanName).HasMaxLength(255);
        builder.Property(e => e.MediaType).HasMaxLength(100);
        builder.Property(e => e.Path).HasMaxLength(512);
        // TODO: See rant in entity file.
        // builder.HasOne(e => e.Parent).WithMany(e => e.DirectChildren).HasForeignKey(e => e.ParentId);
        // builder.HasOne(e => e.TopParent).WithMany(e => e.AllChildren).HasForeignKey(e => e.TopParentId);
        // builder.HasOne(e => e.Season).WithMany(e => e.SeasonEpisodes).HasForeignKey(e => e.SeasonId);
        // builder.HasOne(e => e.Series).WithMany(e => e.SeriesEpisodes).HasForeignKey(e => e.SeriesId);
        builder.HasMany(e => e.Peoples);
        builder.HasMany(e => e.UserData);
        builder.HasMany(e => e.ItemValues);
        builder.HasMany(e => e.MediaStreams);
        builder.HasMany(e => e.Chapters);
        builder.HasMany(e => e.Provider);
        builder.HasMany(e => e.Parents);
        builder.HasMany(e => e.Children);
        builder.HasMany(e => e.DirectChildren).WithOne(e => e.DirectParent).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Extras).WithOne(e => e.Owner).HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(e => e.LockedFields);
        builder.HasMany(e => e.TrailerTypes);
        builder.HasMany(e => e.Images);

        builder.HasIndex(e => e.Path);
        builder.HasIndex(e => e.ParentId);
        builder.HasIndex(e => e.OwnerId);
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => new { e.ExtraType, e.OwnerId });
        builder.HasIndex(e => e.PresentationUniqueKey);
        // covering index
        builder.HasIndex(e => new { e.TopParentId, e.Id });
        // series
        builder.HasIndex(e => new { e.Type, e.SeriesPresentationUniqueKey, e.PresentationUniqueKey, e.SortName });
        // series counts
        // seriesdateplayed sort order
        builder.HasIndex(e => new { e.Type, e.SeriesPresentationUniqueKey, e.IsFolder, e.IsVirtualItem });
        // live tv programs
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.StartDate });
        // covering index for getitemvalues
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.Id });
        // used by movie suggestions
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.PresentationUniqueKey });
        // latest items
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.IsVirtualItem, e.PresentationUniqueKey, e.DateCreated });
        builder.HasIndex(e => new { e.IsFolder, e.TopParentId, e.IsVirtualItem, e.PresentationUniqueKey, e.DateCreated });
        // latest items - optimized for sorting by DateCreated (no PresentationUniqueKey breaking the sort)
        builder.HasIndex(e => new { e.TopParentId, e.Type, e.IsVirtualItem, e.DateCreated });
        builder.HasIndex(e => new { e.TopParentId, e.IsFolder, e.IsVirtualItem, e.DateCreated });
        builder.HasIndex(e => new { e.TopParentId, e.MediaType, e.IsVirtualItem, e.DateCreated });
        // resume
        builder.HasIndex(e => new { e.MediaType, e.TopParentId, e.IsVirtualItem, e.PresentationUniqueKey });
        // sorted library queries (e.g., Series sorted by SortName)
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.SortName });
        // Default ordering. ApplyOrder falls back to ORDER BY SortName when no type/parent is
        // pinned, and IX_BaseItems_Type_TopParentId_SortName only serves the sort when BOTH of its
        // leading columns are equality-bound (verified with EXPLAIN: the pinned case is
        // "Using index", the default case degrades to "Using filesort" over all 78k rows). Without
        // an index whose leading column is SortName, every page of the default ordering sorts the
        // whole table, and OFFSET produces the identical plan, so page N costs the same as page 1.
        // Id second keeps the id-only projections (GetItemIdsList, QueryFiltersLegacy) covering.
        builder.HasIndex(e => new { e.SortName, e.Id });
        // NextUp: per-series episode ordering (index seek + range scan on season/episode)
        builder.HasIndex(e => new { e.Type, e.SeriesPresentationUniqueKey, e.ParentIndexNumber, e.IndexNumber });
        // ByName queries: WHERE Type = X AND CleanName IN (...)
        builder.HasIndex(e => new { e.Type, e.CleanName });
        // Latest TV: GROUP BY SeriesName
        builder.HasIndex(e => e.SeriesName);
        // Latest TV: episode count per season, season count per series
        builder.HasIndex(e => e.SeasonId);
        builder.HasIndex(e => e.SeriesId);

        // Items/Counts: SELECT Type, COUNT(*) GROUP BY Type filtered by TopParentId.
        // This used to declare a second index on (TopParentId, Type, IsVirtualItem) with a
        // HasFilter(...) partial-index predicate. Two reasons it was removed:
        //   1. MariaDB has no partial indexes, so the filter was silently ignored and the index was
        //      created full-sized anyway (verified against the live DDL: plan KEY, no WHERE clause).
        //   2. Its columns are an exact leftmost prefix of
        //      IX_BaseItems_TopParentId_Type_IsVirtualItem_DateCreated declared below, so the count
        //      query already gets an equal-or-better seek from that index.
        // Keeping it cost roughly 9 MB of index and one extra B-tree to maintain on every item write.

        // Full-text search index for CleanName and OriginalTitle
        // Note: MySQL FULLTEXT indexes do not support partial filters — filter removed intentionally.
        builder.HasIndex(e => new { e.CleanName, e.OriginalTitle })
            .HasDatabaseName("IX_BaseItems_FullTextSearch")
            .HasAnnotation("MySql:FullTextIndex", true);

        builder.HasData(new BaseItemEntity()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Type = "PLACEHOLDER",
            Name = "This is a placeholder item for UserData that has been detached from its original item",
        });
    }
}
