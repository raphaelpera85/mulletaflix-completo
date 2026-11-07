using MulletaFlix.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MulletaFlix.Database.Implementations.ModelConfiguration;

/// <summary>
/// FluentAPI configuration for the BaseItemImageInfo entity.
/// </summary>
public class BaseItemImageInfoConfiguration : IEntityTypeConfiguration<BaseItemImageInfo>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BaseItemImageInfo> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.Item).WithMany(e => e.Images).HasForeignKey(e => e.ItemId);
        builder.Property(e => e.ProviderName).HasMaxLength(255);
        builder.Property(e => e.Language).HasMaxLength(32);

        // Composite index for filtering by item and image type (also covers ItemId-only lookups)
        builder.HasIndex(e => new { e.ItemId, e.ImageType });
    }
}

