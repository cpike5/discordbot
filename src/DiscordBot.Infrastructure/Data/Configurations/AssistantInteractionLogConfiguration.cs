using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the AssistantInteractionLog entity.
/// </summary>
public class AssistantInteractionLogConfiguration : IEntityTypeConfiguration<AssistantInteractionLog>
{
    public void Configure(EntityTypeBuilder<AssistantInteractionLog> builder)
    {
        builder.ToTable("AssistantInteractionLogs");

        // Primary key
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(l => l.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(l => l.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(l => l.ChannelId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(l => l.MessageId)
            .HasConversion<long>()
            .IsRequired();

        // DateTime property
        builder.Property(l => l.Timestamp)
            .IsRequired();

        // String properties with max lengths
        builder.Property(l => l.Question)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(l => l.Response)
            .HasMaxLength(2000);

        builder.Property(l => l.ErrorMessage)
            .HasMaxLength(1000);

        // Integer properties with defaults
        builder.Property(l => l.InputTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.OutputTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.CachedTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.CacheCreationTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.ToolCalls)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.LatencyMs)
            .IsRequired()
            .HasDefaultValue(0);

        // Boolean properties with defaults
        builder.Property(l => l.CacheHit)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(l => l.Success)
            .IsRequired()
            .HasDefaultValue(true);

        // Decimal property for cost
        builder.Property(l => l.EstimatedCostUsd)
            .HasColumnType("decimal(18,8)")
            .IsRequired()
            .HasDefaultValue(0m);

        // Indexes for common query patterns
        // Guild-specific queries with timestamp for filtering/sorting
        builder.HasIndex(l => new { l.GuildId, l.Timestamp })
            .HasDatabaseName("IX_AssistantInteractionLogs_GuildId_Timestamp");

        // User-specific queries with timestamp
        builder.HasIndex(l => new { l.UserId, l.Timestamp })
            .HasDatabaseName("IX_AssistantInteractionLogs_UserId_Timestamp");

        // Timestamp index for retention cleanup queries
        builder.HasIndex(l => l.Timestamp)
            .HasDatabaseName("IX_AssistantInteractionLogs_Timestamp");

        // Relationships with SetNull on delete for both User and Guild
        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Guild)
            .WithMany()
            .HasForeignKey(l => l.GuildId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
