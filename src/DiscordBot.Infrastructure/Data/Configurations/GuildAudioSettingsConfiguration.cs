using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GuildAudioSettings entity.
/// </summary>
public class GuildAudioSettingsConfiguration : IEntityTypeConfiguration<GuildAudioSettings>
{
    public void Configure(EntityTypeBuilder<GuildAudioSettings> builder)
    {
        builder.ToTable("GuildAudioSettings");

        // Primary key on GuildId
        builder.HasKey(s => s.GuildId);

        // ulong property converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Boolean properties with defaults
        builder.Property(s => s.AudioEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.QueueEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        // Integer properties with defaults
        builder.Property(s => s.AutoLeaveTimeoutMinutes)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(s => s.MaxDurationSeconds)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(s => s.MaxSoundsPerGuild)
            .IsRequired()
            .HasDefaultValue(50);

        // Long properties with defaults
        builder.Property(s => s.MaxFileSizeBytes)
            .IsRequired()
            .HasDefaultValue(5_242_880);

        builder.Property(s => s.MaxStorageBytes)
            .IsRequired()
            .HasDefaultValue(104_857_600);

        builder.Property(s => s.EnableMemberPortal)
            .IsRequired()
            .HasDefaultValue(false);

        // DateTime properties - stored as UTC
        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with CommandRoleRestrictions
        builder.HasMany(s => s.CommandRoleRestrictions)
            .WithOne(r => r.GuildAudioSettings)
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
