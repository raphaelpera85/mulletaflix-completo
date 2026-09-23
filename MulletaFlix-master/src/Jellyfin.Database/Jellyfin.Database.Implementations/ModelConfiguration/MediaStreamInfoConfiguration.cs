using MulletaFlix.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MulletaFlix.Database.Implementations.ModelConfiguration;

/// <summary>
/// People configuration.
/// </summary>
public class MediaStreamInfoConfiguration : IEntityTypeConfiguration<MediaStreamInfo>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MediaStreamInfo> builder)
    {
        builder.HasKey(e => new { e.ItemId, e.StreamIndex });

        // The primary key already covers lookups by ItemId (and ItemId + StreamIndex). Language
        // discovery instead filters on StreamType alone (MediaStreamRepository.GetMediaStreamLanguages),
        // which otherwise degrades into a full scan of the whole MediaStreamInfos table. Only the
        // filter column is indexed: adding Language here would force it from longtext to varchar(255).
        builder.HasIndex(e => e.StreamType);
    }
}

