using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ModerationCase entity.
/// </summary>
public class ModerationCaseConfiguration : IEntityTypeConfiguration<ModerationCase>
{
    public void Configure(EntityTypeBuilder<ModerationCase> builder)
    {
        builder.ToTable("ModerationCases");

        // Primary key
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // Case number auto-increment
        builder.Property(c => c.CaseNumber)
            .IsRequired();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(c => c.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(c => c.TargetUserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(c => c.ModeratorUserId)
            .HasConversion<long>()
            .IsRequired();

        // Enum stored as int
        builder.Property(c => c.Type)
            .HasConversion<int>()
            .IsRequired();

        // String property
        builder.Property(c => c.Reason)
            .HasMaxLength(2000);

        // TimeSpan stored as ticks
        builder.Property(c => c.Duration);

        // DateTime properties - all stored as UTC
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.ExpiresAt);

        // Foreign key to FlaggedEvent
        builder.Property(c => c.RelatedFlaggedEventId);

        // Relationship with Guild
        builder.HasOne(c => c.Guild)
            .WithMany()
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with FlaggedEvent
        builder.HasOne(c => c.RelatedFlaggedEvent)
            .WithMany()
            .HasForeignKey(c => c.RelatedFlaggedEventId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for common query patterns and performance optimization
        // Guild case listing and filtering
        builder.HasIndex(c => new { c.GuildId, c.CaseNumber })
            .HasDatabaseName("IX_ModerationCases_GuildId_CaseNumber")
            .IsUnique();

        // User history query - find all cases for a specific user
        builder.HasIndex(c => new { c.GuildId, c.TargetUserId, c.CreatedAt })
            .HasDatabaseName("IX_ModerationCases_GuildId_TargetUserId_CreatedAt");

        // Moderator activity tracking
        builder.HasIndex(c => new { c.GuildId, c.ModeratorUserId, c.CreatedAt })
            .HasDatabaseName("IX_ModerationCases_GuildId_ModeratorUserId_CreatedAt");

        // Type-based filtering
        builder.HasIndex(c => new { c.GuildId, c.Type, c.CreatedAt })
            .HasDatabaseName("IX_ModerationCases_GuildId_Type_CreatedAt");

        // Expiration tracking for temporary bans/mutes
        builder.HasIndex(c => new { c.ExpiresAt, c.Type })
            .HasDatabaseName("IX_ModerationCases_ExpiresAt_Type");
    }
}
