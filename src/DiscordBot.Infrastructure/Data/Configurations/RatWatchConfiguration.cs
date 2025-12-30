using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the RatWatch entity.
/// </summary>
public class RatWatchConfiguration : IEntityTypeConfiguration<RatWatch>
{
    public void Configure(EntityTypeBuilder<RatWatch> builder)
    {
        builder.ToTable("RatWatches");

        // Primary key
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(r => r.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.ChannelId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.AccusedUserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.InitiatorUserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.OriginalMessageId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(r => r.NotificationMessageId)
            .HasConversion<long?>();

        builder.Property(r => r.VotingMessageId)
            .HasConversion<long?>();

        // String properties
        builder.Property(r => r.CustomMessage)
            .HasMaxLength(200);

        // Enum stored as int
        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        // DateTime properties - all stored as UTC
        builder.Property(r => r.ScheduledAt)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.ClearedAt);

        builder.Property(r => r.VotingStartedAt);

        builder.Property(r => r.VotingEndedAt);

        // Relationship with Guild
        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with RatVotes (one-to-many)
        builder.HasMany(r => r.Votes)
            .WithOne(v => v.RatWatch)
            .HasForeignKey(v => v.RatWatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with RatRecord (one-to-one)
        builder.HasOne(r => r.Record)
            .WithOne(rec => rec.RatWatch)
            .HasForeignKey<RatRecord>(rec => rec.RatWatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // Critical for background service polling - finds pending watches due for execution
        builder.HasIndex(r => new { r.GuildId, r.ScheduledAt, r.Status })
            .HasDatabaseName("IX_RatWatches_GuildId_ScheduledAt_Status");

        // Stats queries - find all watches for a specific user
        builder.HasIndex(r => new { r.GuildId, r.AccusedUserId })
            .HasDatabaseName("IX_RatWatches_GuildId_AccusedUserId");

        // Channel lookup for message operations
        builder.HasIndex(r => r.ChannelId)
            .HasDatabaseName("IX_RatWatches_ChannelId");
    }
}
