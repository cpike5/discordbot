using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ScheduledMessage entity.
/// </summary>
public class ScheduledMessageConfiguration : IEntityTypeConfiguration<ScheduledMessage>
{
    public void Configure(EntityTypeBuilder<ScheduledMessage> builder)
    {
        builder.ToTable("ScheduledMessages");

        // Primary key
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(s => s.ChannelId)
            .HasConversion<long>()
            .IsRequired();

        // String properties
        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Content)
            .IsRequired()
            .HasMaxLength(2000); // Discord message limit

        builder.Property(s => s.CronExpression)
            .HasMaxLength(100);

        builder.Property(s => s.CreatedBy)
            .IsRequired()
            .HasMaxLength(450); // ASP.NET Identity user ID length

        // Enum stored as int
        builder.Property(s => s.Frequency)
            .HasConversion<int>()
            .IsRequired();

        // Boolean with default
        builder.Property(s => s.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        // DateTime properties
        builder.Property(s => s.LastExecutedAt);

        builder.Property(s => s.NextExecutionAt);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // Guild listing queries
        builder.HasIndex(s => new { s.GuildId, s.IsEnabled })
            .HasDatabaseName("IX_ScheduledMessages_GuildId_IsEnabled");

        // Execution scheduling queries - critical for background service
        builder.HasIndex(s => new { s.NextExecutionAt, s.IsEnabled })
            .HasDatabaseName("IX_ScheduledMessages_NextExecutionAt_IsEnabled");

        // Channel lookup
        builder.HasIndex(s => s.ChannelId)
            .HasDatabaseName("IX_ScheduledMessages_ChannelId");
    }
}
