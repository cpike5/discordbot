using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the AssistantUsageMetrics entity.
/// </summary>
public class AssistantUsageMetricsConfiguration : IEntityTypeConfiguration<AssistantUsageMetrics>
{
    public void Configure(EntityTypeBuilder<AssistantUsageMetrics> builder)
    {
        builder.ToTable("AssistantUsageMetrics");

        // Primary key
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        // ulong property converted to long for SQLite compatibility
        builder.Property(m => m.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // DateTime properties
        builder.Property(m => m.Date)
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .IsRequired();

        // Integer properties with defaults
        builder.Property(m => m.TotalQuestions)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalInputTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalOutputTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalCachedTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalCacheWriteTokens)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalCacheHits)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalCacheMisses)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.TotalToolCalls)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.FailedRequests)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.AverageLatencyMs)
            .IsRequired()
            .HasDefaultValue(0);

        // Decimal property for cost
        builder.Property(m => m.EstimatedCostUsd)
            .HasColumnType("decimal(18,8)")
            .IsRequired()
            .HasDefaultValue(0m);

        // Composite unique index to prevent duplicate metrics for same guild/date
        builder.HasIndex(m => new { m.GuildId, m.Date })
            .IsUnique()
            .HasDatabaseName("IX_AssistantUsageMetrics_GuildId_Date_Unique");

        // Index on Date for retention cleanup queries
        builder.HasIndex(m => m.Date)
            .HasDatabaseName("IX_AssistantUsageMetrics_Date");

        // Relationship with Guild
        builder.HasOne(m => m.Guild)
            .WithMany()
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
