using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the NotXGuildSettings entity.
/// </summary>
public class NotXGuildSettingsConfiguration : IEntityTypeConfiguration<NotXGuildSettings>
{
    public void Configure(EntityTypeBuilder<NotXGuildSettings> builder)
    {
        builder.ToTable("NotXGuildSettings");

        // Primary key on GuildId
        builder.HasKey(s => s.GuildId);

        // ulong property converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Nullable ulong OutputChannelId converted to long? for SQLite compatibility
        builder.Property(s => s.OutputChannelId)
            .HasConversion<long?>();

        // Boolean with default false (feature is disabled by default)
        builder.Property(s => s.IsEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // JSON-backed monitored channel IDs — nullable, no max length constraint
        builder.Property(s => s.MonitoredChannelIdsJson);

        // SensitiveOnly defaults to true (primary use-case: only post for sensitive tweets)
        builder.Property(s => s.SensitiveOnly)
            .IsRequired()
            .HasDefaultValue(true);

        // HideSensitiveLabel defaults to false (show the sensitive label by default)
        builder.Property(s => s.HideSensitiveLabel)
            .IsRequired()
            .HasDefaultValue(false);

        // DateTime properties - stored as UTC
        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Relationship with Guild — one-to-one, cascade delete
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
