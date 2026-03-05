using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

public class DmAssistantUsageMetricsConfiguration : IEntityTypeConfiguration<DmAssistantUsageMetrics>
{
    public void Configure(EntityTypeBuilder<DmAssistantUsageMetrics> builder)
    {
        builder.ToTable("DmAssistantUsageMetrics");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        builder.Property(m => m.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(m => m.Date).IsRequired();

        builder.Property(m => m.TotalMessages).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.TotalInputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.TotalOutputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.TotalCachedTokens).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.FailedRequests).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.AverageLatencyMs).IsRequired().HasDefaultValue(0);

        builder.Property(m => m.EstimatedCostUsd)
            .HasColumnType("decimal(18,8)")
            .IsRequired()
            .HasDefaultValue(0m);

        builder.Property(m => m.UpdatedAt).IsRequired();

        // Unique index for upsert pattern (one record per user per day)
        builder.HasIndex(m => new { m.UserId, m.Date })
            .IsUnique()
            .HasDatabaseName("IX_DmAssistantUsageMetrics_UserId_Date");

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
