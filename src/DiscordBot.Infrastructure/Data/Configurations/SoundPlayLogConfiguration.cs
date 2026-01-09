using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the SoundPlayLog entity.
/// Defines table structure, indexes, and conversions for optimal time-series query performance.
/// </summary>
public class SoundPlayLogConfiguration : IEntityTypeConfiguration<SoundPlayLog>
{
    public void Configure(EntityTypeBuilder<SoundPlayLog> builder)
    {
        builder.ToTable("SoundPlayLogs");

        // Primary key - using long for high-volume scenarios
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        // SoundId - required foreign key
        builder.Property(s => s.SoundId)
            .IsRequired();

        // GuildId - converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // UserId - converted to long for SQLite compatibility
        builder.Property(s => s.UserId)
            .HasConversion<long>()
            .IsRequired();

        // PlayedAt - required, indexed for time-series queries
        builder.Property(s => s.PlayedAt)
            .IsRequired();

        // Foreign key relationship to Sound with cascade delete
        builder.HasOne(s => s.Sound)
            .WithMany()
            .HasForeignKey(s => s.SoundId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for optimal query performance
        // Guild + time index - for guild-specific time-series queries (plays today, etc.)
        builder.HasIndex(s => new { s.GuildId, s.PlayedAt })
            .HasDatabaseName("IX_SoundPlayLogs_GuildId_PlayedAt");

        // Sound + time index - for per-sound analytics
        builder.HasIndex(s => new { s.SoundId, s.PlayedAt })
            .HasDatabaseName("IX_SoundPlayLogs_SoundId_PlayedAt");

        // PlayedAt index - for retention cleanup queries
        builder.HasIndex(s => s.PlayedAt)
            .HasDatabaseName("IX_SoundPlayLogs_PlayedAt");
    }
}
