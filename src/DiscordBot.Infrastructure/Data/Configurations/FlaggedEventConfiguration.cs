using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the FlaggedEvent entity.
/// </summary>
public class FlaggedEventConfiguration : IEntityTypeConfiguration<FlaggedEvent>
{
    public void Configure(EntityTypeBuilder<FlaggedEvent> builder)
    {
        builder.ToTable("FlaggedEvents");

        // Primary key
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(f => f.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(f => f.ChannelId)
            .HasConversion<long?>();

        builder.Property(f => f.ReviewedByUserId)
            .HasConversion<long?>();

        // Enum properties stored as int
        builder.Property(f => f.RuleType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(f => f.Severity)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(f => f.Status)
            .HasConversion<int>()
            .IsRequired();

        // String properties
        builder.Property(f => f.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(f => f.Evidence)
            .IsRequired(); // JSON, no length limit

        builder.Property(f => f.ActionTaken)
            .HasMaxLength(2000);

        // DateTime properties - all stored as UTC
        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.ReviewedAt);

        // Relationship with Guild
        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // Pending events query - critical for moderator review workflow
        builder.HasIndex(f => new { f.GuildId, f.Status, f.CreatedAt })
            .HasDatabaseName("IX_FlaggedEvents_GuildId_Status_CreatedAt");

        // User history query - find all flags for a specific user
        builder.HasIndex(f => new { f.GuildId, f.UserId, f.CreatedAt })
            .HasDatabaseName("IX_FlaggedEvents_GuildId_UserId_CreatedAt");

        // Severity filtering and alerting
        builder.HasIndex(f => new { f.GuildId, f.Severity, f.Status })
            .HasDatabaseName("IX_FlaggedEvents_GuildId_Severity_Status");

        // Rule type analytics
        builder.HasIndex(f => new { f.GuildId, f.RuleType, f.CreatedAt })
            .HasDatabaseName("IX_FlaggedEvents_GuildId_RuleType_CreatedAt");
    }
}
