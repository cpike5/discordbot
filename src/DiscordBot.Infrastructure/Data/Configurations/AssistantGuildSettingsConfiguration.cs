using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the AssistantGuildSettings entity.
/// </summary>
public class AssistantGuildSettingsConfiguration : IEntityTypeConfiguration<AssistantGuildSettings>
{
    public void Configure(EntityTypeBuilder<AssistantGuildSettings> builder)
    {
        builder.ToTable("AssistantGuildSettings");

        // Primary key on GuildId
        builder.HasKey(s => s.GuildId);

        // ulong property converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Boolean with default
        builder.Property(s => s.IsEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // String property for JSON array
        builder.Property(s => s.AllowedChannelIds)
            .IsRequired()
            .HasDefaultValue("[]");

        // Nullable integer for rate limit override
        builder.Property(s => s.RateLimitOverride);

        // DateTime properties - stored as UTC
        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
