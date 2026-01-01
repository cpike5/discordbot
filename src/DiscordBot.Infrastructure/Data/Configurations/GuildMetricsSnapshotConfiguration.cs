using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GuildMetricsSnapshot entity.
/// </summary>
public class GuildMetricsSnapshotConfiguration : IEntityTypeConfiguration<GuildMetricsSnapshot>
{
    public void Configure(EntityTypeBuilder<GuildMetricsSnapshot> builder)
    {
        builder.ToTable("GuildMetricsSnapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        // ulong to long conversion for SQLite compatibility
        builder.Property(s => s.GuildId).HasConversion<long>().IsRequired();
        builder.Property(s => s.SnapshotDate).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        // Metrics columns
        builder.Property(s => s.TotalMembers).IsRequired();
        builder.Property(s => s.ActiveMembers).IsRequired();
        builder.Property(s => s.MembersJoined).IsRequired();
        builder.Property(s => s.MembersLeft).IsRequired();
        builder.Property(s => s.TotalMessages).IsRequired();
        builder.Property(s => s.CommandsExecuted).IsRequired();
        builder.Property(s => s.ModerationActions).IsRequired();
        builder.Property(s => s.ActiveChannels).IsRequired();
        builder.Property(s => s.TotalVoiceMinutes).IsRequired();

        // Indexes for common queries
        builder.HasIndex(s => new { s.GuildId, s.SnapshotDate })
            .HasDatabaseName("IX_GuildMetricsSnapshots_Guild_Date");

        // Unique constraint to prevent duplicate snapshots for the same day
        builder.HasIndex(s => new { s.GuildId, s.SnapshotDate })
            .IsUnique()
            .HasDatabaseName("IX_GuildMetricsSnapshots_Unique");

        // Navigation properties
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
