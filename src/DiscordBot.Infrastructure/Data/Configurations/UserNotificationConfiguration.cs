using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserNotification entity.
/// </summary>
public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");

        // Primary key
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedOnAdd();

        // Required string properties
        builder.Property(n => n.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        // Optional string properties
        builder.Property(n => n.ActionUrl)
            .HasMaxLength(500);

        builder.Property(n => n.RelatedEntityId)
            .HasMaxLength(100);

        // Enum stored as int
        builder.Property(n => n.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(n => n.Severity)
            .HasConversion<int>()
            .IsRequired();

        // Boolean with default
        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        // DateTime properties
        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.ReadAt);

        // Relationship with ApplicationUser
        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization

        // Primary index for user queries - finding user's notifications
        // Composite index for efficient filtering by user + read status
        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_UserNotifications_UserId_IsRead");

        // Index for sorting by creation date (most common sort)
        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
            .HasDatabaseName("IX_UserNotifications_UserId_CreatedAt");

        // Index for cleanup queries - deleting old notifications
        builder.HasIndex(n => n.CreatedAt)
            .HasDatabaseName("IX_UserNotifications_CreatedAt");

        // Index for filtering by type (useful for notification center filtering)
        builder.HasIndex(n => new { n.UserId, n.Type })
            .HasDatabaseName("IX_UserNotifications_UserId_Type");

        // Index for filtering by severity (useful for showing critical alerts first)
        builder.HasIndex(n => new { n.UserId, n.Severity, n.IsRead })
            .HasDatabaseName("IX_UserNotifications_UserId_Severity_IsRead");
    }
}
