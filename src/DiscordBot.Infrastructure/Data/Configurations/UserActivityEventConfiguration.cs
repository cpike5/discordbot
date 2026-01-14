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

        // ActivityEventType enum stored as int
        builder.Property(e => e.EventType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .IsRequired();

        builder.Property(e => e.LoggedAt)
            .IsRequired();

        // Indexes for common query patterns and performance optimization
        // Guild analytics queries (most common - filter by guild and time range)
        builder.HasIndex(e => new { e.GuildId, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_GuildId_Timestamp");

        // User activity history queries
        builder.HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_UserId_Timestamp");

        // Retention cleanup queries
        builder.HasIndex(e => e.LoggedAt)
            .HasDatabaseName("IX_UserActivityEvents_LoggedAt");

        // Event type filtering with guild and time range
        builder.HasIndex(e => new { e.EventType, e.GuildId, e.Timestamp })
            .HasDatabaseName("IX_UserActivityEvents_EventType_GuildId_Timestamp");
    }
}
