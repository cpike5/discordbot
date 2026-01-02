using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the PerformanceIncident entity.
/// Defines table structure, indexes, and conversions for optimal incident query performance.
/// </summary>
public class PerformanceIncidentConfiguration : IEntityTypeConfiguration<PerformanceIncident>
{
    public void Configure(EntityTypeBuilder<PerformanceIncident> builder)
    {
        builder.ToTable("PerformanceIncidents");

        // Primary key - using Guid for distributed scenarios
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd();

        // MetricName - required, indexed for metric-specific queries
        builder.Property(i => i.MetricName)
            .IsRequired()
            .HasMaxLength(100);

        // Composite index on MetricName and TriggeredAt for metric history queries
        builder.HasIndex(i => new { i.MetricName, i.TriggeredAt })
            .HasDatabaseName("IX_PerformanceIncidents_MetricName_TriggeredAt");

        // Enums stored as int
        builder.Property(i => i.Severity)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        // Status index - for filtering active/resolved incidents
        builder.HasIndex(i => i.Status)
            .HasDatabaseName("IX_PerformanceIncidents_Status");

        // Timestamp - required, indexed for time-range queries
        builder.Property(i => i.TriggeredAt)
            .IsRequired();

        builder.HasIndex(i => i.TriggeredAt)
            .HasDatabaseName("IX_PerformanceIncidents_TriggeredAt");

        builder.Property(i => i.ResolvedAt);

        // Threshold and actual values
        builder.Property(i => i.ThresholdValue)
            .IsRequired();

        builder.Property(i => i.ActualValue)
            .IsRequired();

        // Message - required
        builder.Property(i => i.Message)
            .IsRequired()
            .HasMaxLength(500);

        // Acknowledgment fields
        builder.Property(i => i.IsAcknowledged)
            .IsRequired();

        builder.Property(i => i.AcknowledgedBy)
            .HasMaxLength(450); // ASP.NET Identity user ID length

        builder.Property(i => i.AcknowledgedAt);

        // Notes - optional
        builder.Property(i => i.Notes)
            .HasMaxLength(1000);

        // Composite index on Severity and Status for dashboard queries
        builder.HasIndex(i => new { i.Severity, i.Status })
            .HasDatabaseName("IX_PerformanceIncidents_Severity_Status");
    }
}
