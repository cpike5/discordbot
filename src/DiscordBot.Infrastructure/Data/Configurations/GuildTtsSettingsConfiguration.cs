using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GuildTtsSettings entity.
/// </summary>
public class GuildTtsSettingsConfiguration : IEntityTypeConfiguration<GuildTtsSettings>
{
    public void Configure(EntityTypeBuilder<GuildTtsSettings> builder)
    {
        builder.ToTable("GuildTtsSettings");

        // Primary key on GuildId
        builder.HasKey(s => s.GuildId);

        // ulong property converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Boolean properties with defaults
        builder.Property(s => s.TtsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.AutoPlayOnSend)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.AnnounceJoinsLeaves)
            .IsRequired()
            .HasDefaultValue(false);

        // String properties
        builder.Property(s => s.DefaultVoice)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);

        // Double properties with defaults
        builder.Property(s => s.DefaultSpeed)
            .IsRequired()
            .HasDefaultValue(1.0);

        builder.Property(s => s.DefaultPitch)
            .IsRequired()
            .HasDefaultValue(1.0);

        builder.Property(s => s.DefaultVolume)
            .IsRequired()
            .HasDefaultValue(0.8);

        // Integer properties with defaults
        builder.Property(s => s.MaxMessageLength)
            .IsRequired()
            .HasDefaultValue(500);

        builder.Property(s => s.RateLimitPerMinute)
            .IsRequired()
            .HasDefaultValue(5);

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
    }
}
