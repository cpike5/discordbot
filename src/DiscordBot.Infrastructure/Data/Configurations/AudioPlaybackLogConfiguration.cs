using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the AudioPlaybackLog entity.
/// Defines table structure, indexes, and conversions for optimal query performance.
/// </summary>
public class AudioPlaybackLogConfiguration : IEntityTypeConfiguration<AudioPlaybackLog>
{
    public void Configure(EntityTypeBuilder<AudioPlaybackLog> builder)
    {
        builder.ToTable("AudioPlaybackLogs");

        // Primary key
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        // GuildId - converted to long for SQLite compatibility
        builder.Property(a => a.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // UserId - converted to long for SQLite compatibility
        builder.Property(a => a.UserId)
            .HasConversion<long>()
            .IsRequired();

        // FeatureType - stored as int
        builder.Property(a => a.FeatureType)
            .HasConversion<int>()
            .IsRequired();

        // ContentName - max 200 characters
        builder.Property(a => a.ContentName)
            .IsRequired()
            .HasMaxLength(200);

        // ChannelId - nullable, converted to long for SQLite compatibility
        builder.Property(a => a.ChannelId)
            .HasConversion<long?>();

        // PlayedAt - required, indexed for time-series queries
        builder.Property(a => a.PlayedAt)
            .IsRequired();

        // Navigation property to Guild
        builder.HasOne(a => a.Guild)
            .WithMany()
            .HasForeignKey(a => a.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for optimal query performance
        // Guild + time index - for guild-specific time-series queries (most recent first)
        builder.HasIndex(a => new { a.GuildId, a.PlayedAt })
            .HasDatabaseName("IX_AudioPlaybackLogs_GuildId_PlayedAt")
            .IsDescending(false, true);

        // Guild + user + time index - for per-user filtering within a guild
        builder.HasIndex(a => new { a.GuildId, a.UserId, a.PlayedAt })
            .HasDatabaseName("IX_AudioPlaybackLogs_GuildId_UserId_PlayedAt")
            .IsDescending(false, false, true);
    }
}
