using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Reminder entity.
/// </summary>
public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");

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

        builder.Property(r => r.UserId)
            .HasConversion<long>()
            .IsRequired();

        // String properties
        builder.Property(r => r.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.LastError)
            .HasMaxLength(500);

        // Enum stored as int
        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        // Integer with default
        builder.Property(r => r.DeliveryAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        // DateTime properties
        builder.Property(r => r.TriggerAt)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.DeliveredAt);

        // Relationship with Guild
        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // Background service queries - finding due reminders
        builder.HasIndex(r => new { r.Status, r.TriggerAt })
            .HasDatabaseName("IX_Reminders_Status_TriggerAt");

        // User queries - list user's reminders
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_Reminders_UserId");

        // Admin/guild queries
        builder.HasIndex(r => r.GuildId)
            .HasDatabaseName("IX_Reminders_GuildId");
    }
}
