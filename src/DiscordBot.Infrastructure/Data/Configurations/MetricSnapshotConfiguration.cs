using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the MetricSnapshot entity.
/// </summary>
public class MetricSnapshotConfiguration : IEntityTypeConfiguration<MetricSnapshot>
{
    public void Configure(EntityTypeBuilder<MetricSnapshot> builder)
    {
        builder.ToTable("MetricSnapshots");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        // Timestamp with UTC handling
        builder.Property(m => m.Timestamp)
            .IsRequired()
            .HasConversion(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // Database metrics
        builder.Property(m => m.DatabaseAvgQueryTimeMs)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.DatabaseTotalQueries)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.DatabaseSlowQueryCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Memory metrics
        builder.Property(m => m.WorkingSetMB)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.PrivateMemoryMB)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.HeapSizeMB)
            .IsRequired()
            .HasDefaultValue(0);

        // GC metrics
        builder.Property(m => m.Gen0Collections)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.Gen1Collections)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.Gen2Collections)
            .IsRequired()
            .HasDefaultValue(0);

        // Cache metrics
        builder.Property(m => m.CacheHitRatePercent)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.CacheTotalEntries)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.CacheTotalHits)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.CacheTotalMisses)
            .IsRequired()
            .HasDefaultValue(0);

        // Service health metrics
        builder.Property(m => m.ServicesRunningCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.ServicesErrorCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.ServicesTotalCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Index for efficient time-range queries (descending for latest-first queries)
        builder.HasIndex(m => m.Timestamp)
            .IsDescending();
    }
}
