using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MulletaFlix.Database.Implementations.Entities;

namespace MulletaFlix.Database.Implementations.ModelConfiguration;

public class ActionLogConfiguration : IEntityTypeConfiguration<ActionLog>
{
    public void Configure(EntityTypeBuilder<ActionLog> builder)
    {
        builder.ToTable("ActionLogs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ActionType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.EntityId)
            .HasMaxLength(128);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.DateCreated)
            .IsRequired();

        builder.Property(e => e.Details)
            .HasMaxLength(2048);

        builder.Property(e => e.OldValues)
            .HasMaxLength(512);

        builder.Property(e => e.NewValues)
            .HasMaxLength(512);

        builder.Property(e => e.IpAddress)
            .HasMaxLength(64);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(512);

        builder.Property(e => e.IsSuccess)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(1024);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.EntityType);
        builder.HasIndex(e => e.ActionType);
        builder.HasIndex(e => e.DateCreated);
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}