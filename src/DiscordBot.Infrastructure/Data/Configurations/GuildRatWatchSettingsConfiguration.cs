using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GuildRatWatchSettings entity.
/// </summary>
public class GuildRatWatchSettingsConfiguration : IEntityTypeConfiguration<GuildRatWatchSettings>
{
    public void Configure(EntityTypeBuilder<GuildRatWatchSettings> builder)
    {
        builder.ToTable("GuildRatWatchSettings");

        // Primary key on GuildId
        builder.HasKey(s => s.GuildId);

        // ulong property converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Boolean with default
        builder.Property(s => s.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        // String property
        builder.Property(s => s.Timezone)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("UTC");

        // Integer properties with defaults
        builder.Property(s => s.MaxAdvanceHours)
            .IsRequired()
            .HasDefaultValue(24);

        builder.Property(s => s.VotingDurationMinutes)
            .IsRequired()
            .HasDefaultValue(5);

        // Public leaderboard setting with default false
        builder.Property(s => s.PublicLeaderboardEnabled)
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
    }
}
