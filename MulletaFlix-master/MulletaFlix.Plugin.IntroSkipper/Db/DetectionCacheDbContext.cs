// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-FileCopyrightText: 2026 AbandonedCart
// SPDX-FileCopyrightText: 2026 Kilian von Pflugk
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;

namespace IntroSkipper.Db;

/// <summary>
/// EF Core database context for the FFmpeg detection cache.
/// Stored in MariaDB alongside the segment and season tables.
/// </summary>
public class DetectionCacheDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DetectionCacheDbContext"/> class.
    /// </summary>
    /// <param name="options">The context options configured by the server MariaDB provider.</param>
    public DetectionCacheDbContext(DbContextOptions<DetectionCacheDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets the <see cref="DbSet{TEntity}"/> containing the detection cache entries.
    /// </summary>
    public DbSet<DbDetectionCache> DetectionCache => Set<DbDetectionCache>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbDetectionCache>(entity =>
        {
            entity.ToTable("DetectionCache");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            // Composite unique index: one cache entry per (ItemId, Mode, Type, Start, End).
            entity.HasIndex(e => new { e.ItemId, e.Mode, e.Type, e.Start, e.End })
                  .HasDatabaseName("IX_DetectionCache_Unique")
                  .IsUnique();

            entity.HasIndex(e => e.Mode);

            entity.Property(e => e.Start)
                  .HasDefaultValue(0.0)
                  .IsRequired();

            entity.Property(e => e.End)
                  .HasDefaultValue(0.0)
                  .IsRequired();

            entity.Property(e => e.Data)
                  .IsRequired();

            entity.Property(e => e.ConfigHash)
                  .HasDefaultValue(string.Empty)
                  .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Ensures the detection-cache table exists inside the shared plugin schema. Uses
    /// <see cref="IntroSkipperSchema"/> rather than <c>EnsureCreated</c> alone because the
    /// segment context owns the other tables of the same schema: the segment tables
    /// already being present must not stop the cache table from being created, and vice
    /// versa. Nothing here may drop the schema — it holds the segment data.
    /// </summary>
    public void EnsureSchema()
    {
        IntroSkipperSchema.EnsureAsync(this, CancellationToken.None).GetAwaiter().GetResult();
        DetectionCache.AsNoTracking().Take(1).Load();
    }
}
