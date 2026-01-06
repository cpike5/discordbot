using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the MessageLog entity.
/// </summary>
public class MessageLogConfiguration : IEntityTypeConfiguration<MessageLog>
{
    public void Configure(EntityTypeBuilder<MessageLog> builder)
    {
        builder.ToTable("MessageLogs");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        // ulong properties require explicit lambda-based value converters to prevent ID corruption.
        // Using unchecked to handle potential overflow for very large Discord snowflake IDs.
        builder.Property(m => m.DiscordMessageId)
            .HasConversion(
                v => unchecked((long)v),
                v => unchecked((ulong)v))
            .IsRequired();

        builder.Property(m => m.AuthorId)
            .HasConversion(
                v => unchecked((long)v),
                v => unchecked((ulong)v))
            .IsRequired();

        builder.Property(m => m.ChannelId)
            .HasConversion(
                v => unchecked((long)v),
                v => unchecked((ulong)v))
            .IsRequired();

        builder.Property(m => m.GuildId)
            .HasConversion(
                v => v.HasValue ? unchecked((long)v.Value) : (long?)null,
                v => v.HasValue ? unchecked((ulong)v.Value) : (ulong?)null);

        builder.Property(m => m.ReplyToMessageId)
            .HasConversion(
                v => v.HasValue ? unchecked((long)v.Value) : (long?)null,
                v => v.HasValue ? unchecked((ulong)v.Value) : (ulong?)null);

        // MessageSource enum stored as int
        builder.Property(m => m.Source)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.Property(m => m.Timestamp)
            .IsRequired();

        builder.Property(m => m.LoggedAt)
            .IsRequired();

        builder.Property(m => m.HasAttachments)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.HasEmbeds)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(m => m.Guild)
            .WithMany(g => g.MessageLogs)
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.User)
            .WithMany(u => u.MessageLogs)
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // User history queries
        builder.HasIndex(m => new { m.AuthorId, m.Timestamp })
            .HasDatabaseName("IX_MessageLogs_AuthorId_Timestamp");

        // Channel history queries
        builder.HasIndex(m => new { m.ChannelId, m.Timestamp })
            .HasDatabaseName("IX_MessageLogs_ChannelId_Timestamp");

        // Guild analytics queries
        builder.HasIndex(m => new { m.GuildId, m.Timestamp })
            .HasDatabaseName("IX_MessageLogs_GuildId_Timestamp");

        // Retention cleanup queries
        builder.HasIndex(m => m.LoggedAt)
            .HasDatabaseName("IX_MessageLogs_LoggedAt");

        // Unique constraint to prevent duplicate message logging
        builder.HasIndex(m => m.DiscordMessageId)
            .IsUnique()
            .HasDatabaseName("IX_MessageLogs_DiscordMessageId_Unique");
    }
}
