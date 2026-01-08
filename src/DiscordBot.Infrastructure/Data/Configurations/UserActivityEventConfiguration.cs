using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserActivityEvent entity.
/// </summary>
public class UserActivityEventConfiguration : IEntityTypeConfiguration<UserActivityEvent>
{
    public void Configure(EntityTypeBuilder<UserActivityEvent> builder)
    {
        builder.ToTable("UserActivityEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // ulong properties require explicit lambda-based value converters to prevent ID corruption.
        // Using unchecked to handle potential overflow for very large Discord snowflake IDs.
        builder.Property(e => e.UserId)
            .HasConversion(
                v => unchecked((long)v),
                v => unchecked((ulong)v))
            .IsRequired();

        builder.Property(e => e.GuildId)
            .HasConversion(
                v => unchecked((long)v),
                v => unchecked((ulong)v))
            .IsRequired();

        builder.Property(e => e.ChannelId)
            .HasConversion(
                v => unchecked((long)v),
                v => unchecked((ulong)v))
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .IsRequired();

        // ActivityEventType enum stored as int
        builder.Property(e => e.EventType)
            .HasConversion<int>()
            .IsRequired();

        // Relationships
        builder.HasOne(e => e.Guild)
            .WithMany(g => g.UserActivityEvents)
            .HasForeignKey(e => e.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserActivityEvents)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and analytics aggregation
        // Guild + time range queries (most common for analytics)
        builder.HasIndex(e => new { e.GuildId, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_GuildId_Timestamp");

        // User activity queries within a guild
        builder.HasIndex(e => new { e.GuildId, e.UserId, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_GuildId_UserId_Timestamp");

        // Channel activity queries within a guild
        builder.HasIndex(e => new { e.GuildId, e.ChannelId, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_GuildId_ChannelId_Timestamp");

        // Event type filtering
        builder.HasIndex(e => new { e.GuildId, e.EventType, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_GuildId_EventType_Timestamp");

        // Retention cleanup queries
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_UserActivityEvents_Timestamp");
    }
}
