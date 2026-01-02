using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the PerformanceAlertConfig entity.
/// Defines table structure, indexes, and constraints for performance alert configurations.
/// </summary>
public class PerformanceAlertConfigConfiguration : IEntityTypeConfiguration<PerformanceAlertConfig>
{
    public void Configure(EntityTypeBuilder<PerformanceAlertConfig> builder)
    {
        builder.ToTable("PerformanceAlertConfigs");

        // Primary key
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // MetricName - unique, required
        builder.Property(c => c.MetricName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(c => c.MetricName)
            .IsUnique()
            .HasDatabaseName("IX_PerformanceAlertConfigs_MetricName");

        // DisplayName - required
        builder.Property(c => c.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        // Description - optional
        builder.Property(c => c.Description)
            .HasMaxLength(500);

        // Threshold values - nullable doubles
        builder.Property(c => c.WarningThreshold);
        builder.Property(c => c.CriticalThreshold);

        // ThresholdUnit - required
        builder.Property(c => c.ThresholdUnit)
            .IsRequired()
            .HasMaxLength(20);

        // IsEnabled - required, indexed for filtering
        builder.Property(c => c.IsEnabled)
            .IsRequired();

        builder.HasIndex(c => c.IsEnabled)
            .HasDatabaseName("IX_PerformanceAlertConfigs_IsEnabled");

        // Timestamps - required
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt);

        // UpdatedBy - optional
        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(450); // ASP.NET Identity user ID length
    }
}
