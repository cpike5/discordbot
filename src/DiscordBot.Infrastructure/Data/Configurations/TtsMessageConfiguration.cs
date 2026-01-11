using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the TtsMessage entity.
/// Defines table structure, indexes, and conversions for optimal query performance.
/// </summary>
public class TtsMessageConfiguration : IEntityTypeConfiguration<TtsMessage>
{
    public void Configure(EntityTypeBuilder<TtsMessage> builder)
    {
        builder.ToTable("TtsMessages");

        // Primary key
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // GuildId - converted to long for SQLite compatibility
        builder.Property(m => m.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // UserId - converted to long for SQLite compatibility
        builder.Property(m => m.UserId)
            .HasConversion<long>()
            .IsRequired();

        // Username - stored for display purposes
        builder.Property(m => m.Username)
            .IsRequired()
            .HasMaxLength(100);

        // Message - the TTS text content
        builder.Property(m => m.Message)
            .IsRequired()
            .HasMaxLength(1000);

        // Voice - the voice identifier used
        builder.Property(m => m.Voice)
            .IsRequired()
            .HasMaxLength(100);

        // DurationSeconds - playback duration
        builder.Property(m => m.DurationSeconds)
            .IsRequired();

        // CreatedAt - timestamp
        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Foreign key relationship to Guild
        builder.HasOne(m => m.Guild)
            .WithMany()
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for optimal query performance
        // Guild + time index - for guild-specific time-series queries
        builder.HasIndex(m => new { m.GuildId, m.CreatedAt })
            .HasDatabaseName("IX_TtsMessages_GuildId_CreatedAt");

        // Guild + user + time index - for per-user rate limiting
        builder.HasIndex(m => new { m.GuildId, m.UserId, m.CreatedAt })
            .HasDatabaseName("IX_TtsMessages_GuildId_UserId_CreatedAt");

        // CreatedAt index - for retention cleanup queries
        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("IX_TtsMessages_CreatedAt");
    }
}
