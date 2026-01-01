using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ChannelActivitySnapshot entity.
/// </summary>
public class ChannelActivitySnapshotConfiguration : IEntityTypeConfiguration<ChannelActivitySnapshot>
{
    public void Configure(EntityTypeBuilder<ChannelActivitySnapshot> builder)
    {
        builder.ToTable("ChannelActivitySnapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        // ulong to long conversion for SQLite compatibility
        builder.Property(s => s.GuildId).HasConversion<long>().IsRequired();
        builder.Property(s => s.ChannelId).HasConversion<long>().IsRequired();
        builder.Property(s => s.ChannelName).HasMaxLength(100).IsRequired();

        builder.Property(s => s.PeriodStart).IsRequired();
        builder.Property(s => s.Granularity).HasConversion<int>().IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        // Metrics columns
        builder.Property(s => s.MessageCount).IsRequired();
        builder.Property(s => s.UniqueUsers).IsRequired();
        builder.Property(s => s.PeakHour);
        builder.Property(s => s.PeakHourMessageCount);
        builder.Property(s => s.AverageMessageLength).IsRequired();

        // Indexes for common queries
        builder.HasIndex(s => new { s.GuildId, s.PeriodStart, s.Granularity })
            .HasDatabaseName("IX_ChannelActivitySnapshots_Guild_Period_Granularity");

        builder.HasIndex(s => new { s.GuildId, s.ChannelId, s.PeriodStart })
            .HasDatabaseName("IX_ChannelActivitySnapshots_Guild_Channel_Period");

        // Unique constraint to prevent duplicate snapshots
        builder.HasIndex(s => new { s.GuildId, s.ChannelId, s.PeriodStart, s.Granularity })
            .IsUnique()
            .HasDatabaseName("IX_ChannelActivitySnapshots_Unique");

        // Navigation properties
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
