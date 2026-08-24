using MulletaFlix.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MulletaFlix.Database.Implementations.ModelConfiguration;

/// <summary>
/// FluentAPI configuration for the PlaybackReport entity.
/// </summary>
public class PlaybackReportConfiguration : IEntityTypeConfiguration<PlaybackReport>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PlaybackReport> builder)
    {
        builder.HasIndex(entity => entity.DateCreated);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => entity.ItemId);
        builder.HasIndex(entity => entity.DeviceId);
        builder.HasIndex(entity => entity.PlaySessionId);
        builder.HasIndex(entity => new { entity.UserId, entity.DateCreated });
        builder.HasIndex(entity => new { entity.ItemId, entity.DateCreated });
        builder.HasIndex(entity => new { entity.DeviceId, entity.DateCreated });
    }
}