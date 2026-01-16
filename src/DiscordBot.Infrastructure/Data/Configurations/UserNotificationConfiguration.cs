using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserNotification entity.
/// Defines table structure, indexes, and conversions for optimal notification query performance.
/// </summary>
public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");

        // Primary key - using Guid for distributed scenarios
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedOnAdd();

        // UserId - required, indexed for user-specific queries
        builder.Property(n => n.UserId)
            .IsRequired()
            .HasMaxLength(450); // ASP.NET Identity user ID length

        // Composite index for efficient unread notification queries
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .HasDatabaseName("IX_UserNotifications_UserId_IsRead_CreatedAt");

        // Index for cleanup queries on dismissed notifications
        builder.HasIndex(n => new { n.UserId, n.DismissedAt })
            .HasDatabaseName("IX_UserNotifications_UserId_DismissedAt");

        // Enum stored as int
        builder.Property(n => n.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(n => n.Severity)
            .HasConversion<int>();

        // Title - required
        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Message - required
        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        // LinkUrl - optional
        builder.Property(n => n.LinkUrl)
            .HasMaxLength(500);

        // GuildId - optional, with SQLite compatibility conversion
        builder.Property(n => n.GuildId)
            .HasConversion(
                v => v.HasValue ? (long)v.Value : (long?)null,
                v => v.HasValue ? (ulong)v.Value : (ulong?)null);

        // Index on GuildId for guild-specific notification queries
        builder.HasIndex(n => n.GuildId)
            .HasDatabaseName("IX_UserNotifications_GuildId");

        // IsRead - required with default
        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        // Timestamps
        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.ReadAt);

        builder.Property(n => n.DismissedAt);

        // Related entity fields - optional
        builder.Property(n => n.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(n => n.RelatedEntityId)
            .HasMaxLength(100);

        // Foreign key to ApplicationUser
        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional foreign key to Guild
        builder.HasOne(n => n.Guild)
            .WithMany()
            .HasForeignKey(n => n.GuildId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
