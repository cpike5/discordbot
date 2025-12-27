using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the AuditLog entity.
/// Defines table structure, indexes, and conversions for optimal query performance.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        // Primary key - using long for high-volume scenarios
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        // Timestamp - required, indexed for range queries
        builder.Property(a => a.Timestamp)
            .IsRequired();

        // Enums stored as int
        builder.Property(a => a.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.Action)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.ActorType)
            .HasConversion<int>()
            .IsRequired();

        // String properties
        builder.Property(a => a.ActorId)
            .HasMaxLength(450); // ASP.NET Identity user ID length

        builder.Property(a => a.TargetType)
            .HasMaxLength(200);

        builder.Property(a => a.TargetId)
            .HasMaxLength(450);

        // ulong property converted to long for SQLite compatibility
        builder.Property(a => a.GuildId)
            .HasConversion<long>();

        // Details stored as JSON string, no max length for flexibility
        builder.Property(a => a.Details);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(100); // GUID or correlation identifier

        // Indexes for optimal query performance
        // Timestamp index - critical for time-range queries and cleanup
        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_AuditLogs_Timestamp");

        // Category index - for filtering by category
        builder.HasIndex(a => a.Category)
            .HasDatabaseName("IX_AuditLogs_Category");

        // Actor index - for user activity queries
        builder.HasIndex(a => new { a.ActorId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_ActorId_Timestamp");

        // Guild index - for guild-specific audit log queries
        builder.HasIndex(a => new { a.GuildId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_GuildId_Timestamp");

        // Correlation index - for tracing related events
        builder.HasIndex(a => a.CorrelationId)
            .HasDatabaseName("IX_AuditLogs_CorrelationId");

        // Composite index for common filtering patterns
        builder.HasIndex(a => new { a.Category, a.Action, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Category_Action_Timestamp");

        // Target lookup index - for entity-specific audit trails
        builder.HasIndex(a => new { a.TargetType, a.TargetId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_TargetType_TargetId_Timestamp");
    }
}
