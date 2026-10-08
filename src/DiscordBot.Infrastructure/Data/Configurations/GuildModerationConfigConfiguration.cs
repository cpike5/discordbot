using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GuildModerationConfig entity.
/// </summary>
public class GuildModerationConfigConfiguration : IEntityTypeConfiguration<GuildModerationConfig>
{
    public void Configure(EntityTypeBuilder<GuildModerationConfig> builder)
    {
        builder.ToTable("GuildModerationConfigs");

        // Primary key on GuildId
        builder.HasKey(c => c.GuildId);

        // ulong property converted to long for SQLite compatibility
        builder.Property(c => c.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Enum stored as int
        builder.Property(c => c.Mode)
            .HasConversion<int>()
            .IsRequired();

        // String properties
        builder.Property(c => c.SimplePreset)
            .HasMaxLength(50);

        builder.Property(c => c.SpamConfig)
            .IsRequired(); // JSON, no length limit

        builder.Property(c => c.ContentFilterConfig)
            .IsRequired(); // JSON, no length limit

        builder.Property(c => c.RaidProtectionConfig)
            .IsRequired(); // JSON, no length limit

        // Mod-log feed: nullable channel (ulong as long, like GuildId) and a flags enum as int
        builder.Property(c => c.ModLogChannelId)
            .HasConversion<long?>();

        // No database default: "None" (every kind off) is a value an admin can choose, and a
        // database default would silently replace it on insert. The entity's initializer gives a
        // new row All.
        builder.Property(c => c.ModLogEvents)
            .HasConversion<int>()
            .IsRequired();

        // DateTime property
        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(c => c.Guild)
            .WithMany()
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
