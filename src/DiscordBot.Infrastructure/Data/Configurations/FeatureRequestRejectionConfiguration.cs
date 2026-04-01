using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the FeatureRequestRejection entity.
/// Standalone abuse-log table with no foreign keys to support independent retention.
/// </summary>
public class FeatureRequestRejectionConfiguration : IEntityTypeConfiguration<FeatureRequestRejection>
{
    public void Configure(EntityTypeBuilder<FeatureRequestRejection> builder)
    {
        builder.ToTable("FeatureRequestRejections");

        // Primary key
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(r => r.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.RejectionReason)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // Index for abuse-pattern queries per guild/user
        builder.HasIndex(r => new { r.GuildId, r.UserId, r.CreatedAt })
            .HasDatabaseName("IX_FeatureRequestRejections_GuildId_UserId_CreatedAt");
    }
}
