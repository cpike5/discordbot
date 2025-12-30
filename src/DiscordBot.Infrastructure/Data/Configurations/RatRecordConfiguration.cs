using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the RatRecord entity.
/// </summary>
public class RatRecordConfiguration : IEntityTypeConfiguration<RatRecord>
{
    public void Configure(EntityTypeBuilder<RatRecord> builder)
    {
        builder.ToTable("RatRecords");

        // Primary key
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        // Foreign key to RatWatch
        builder.Property(r => r.RatWatchId)
            .IsRequired();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(r => r.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasConversion<long>()
            .IsRequired();

        // Vote counts
        builder.Property(r => r.GuiltyVotes)
            .IsRequired();

        builder.Property(r => r.NotGuiltyVotes)
            .IsRequired();

        // DateTime property - stored as UTC
        builder.Property(r => r.RecordedAt)
            .IsRequired();

        // String property
        builder.Property(r => r.OriginalMessageLink)
            .HasMaxLength(500);

        // Relationship with RatWatch (one-to-one)
        builder.HasOne(r => r.RatWatch)
            .WithOne(w => w.Record)
            .HasForeignKey<RatRecord>(r => r.RatWatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Guild
        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns
        // Leaderboard queries - count records by user in guild
        builder.HasIndex(r => new { r.GuildId, r.UserId })
            .HasDatabaseName("IX_RatRecords_GuildId_UserId");

        // Recent records query - ordered by time
        builder.HasIndex(r => r.RecordedAt)
            .HasDatabaseName("IX_RatRecords_RecordedAt");
    }
}
