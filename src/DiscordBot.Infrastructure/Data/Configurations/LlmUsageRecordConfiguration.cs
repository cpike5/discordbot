using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="LlmUsageRecord"/> ledger.
/// </summary>
public class LlmUsageRecordConfiguration : IEntityTypeConfiguration<LlmUsageRecord>
{
    public void Configure(EntityTypeBuilder<LlmUsageRecord> builder)
    {
        builder.ToTable("LlmUsageRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        builder.Property(r => r.Timestamp)
            .IsRequired();

        builder.Property(r => r.Mode)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.UserId)
            .HasConversion<long>()
            .IsRequired();

        // Nullable - DM assistant and DM-based feature request conversations have no guild.
        builder.Property(r => r.GuildId)
            .HasConversion<long?>();

        builder.Property(r => r.Model)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.InputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.OutputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.CachedTokens).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.CacheWriteTokens).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.LlmCalls).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.ToolCalls).IsRequired().HasDefaultValue(0);

        builder.Property(r => r.CostUsd)
            .HasColumnType("decimal(18,8)")
            .IsRequired()
            .HasDefaultValue(0m);

        builder.Property(r => r.CostSource)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.LatencyMs).IsRequired().HasDefaultValue(0);

        builder.Property(r => r.Success)
            .IsRequired()
            .HasDefaultValue(true);

        // No FK: this is a usage ledger keyed on Discord snowflakes, not a relational join target -
        // deleting a User/Guild row must never cascade-delete cost history, and a DM user or a
        // guild that only ever appears in feature-request conversations may have no Users/Guilds
        // row at all.
        builder.HasIndex(r => new { r.UserId, r.Timestamp })
            .HasDatabaseName("IX_LlmUsageRecords_UserId_Timestamp");

        builder.HasIndex(r => new { r.GuildId, r.Timestamp })
            .HasDatabaseName("IX_LlmUsageRecords_GuildId_Timestamp");

        builder.HasIndex(r => new { r.Mode, r.Timestamp })
            .HasDatabaseName("IX_LlmUsageRecords_Mode_Timestamp");

        builder.HasIndex(r => new { r.Model, r.Timestamp })
            .HasDatabaseName("IX_LlmUsageRecords_Model_Timestamp");
    }
}
