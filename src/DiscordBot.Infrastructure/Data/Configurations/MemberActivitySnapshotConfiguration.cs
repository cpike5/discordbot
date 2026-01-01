using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the MemberActivitySnapshot entity.
/// </summary>
public class MemberActivitySnapshotConfiguration : IEntityTypeConfiguration<MemberActivitySnapshot>
{
    public void Configure(EntityTypeBuilder<MemberActivitySnapshot> builder)
    {
        builder.ToTable("MemberActivitySnapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        // ulong to long conversion for SQLite compatibility
        builder.Property(s => s.GuildId).HasConversion<long>().IsRequired();
        builder.Property(s => s.UserId).HasConversion<long>().IsRequired();

        builder.Property(s => s.PeriodStart).IsRequired();
        builder.Property(s => s.Granularity).HasConversion<int>().IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        // Metrics columns
        builder.Property(s => s.MessageCount).IsRequired();
        builder.Property(s => s.ReactionCount).IsRequired();
        builder.Property(s => s.VoiceMinutes).IsRequired();
        builder.Property(s => s.UniqueChannelsActive).IsRequired();

        // Indexes for common queries
        builder.HasIndex(s => new { s.GuildId, s.PeriodStart, s.Granularity })
            .HasDatabaseName("IX_MemberActivitySnapshots_Guild_Period_Granularity");

        builder.HasIndex(s => new { s.GuildId, s.UserId, s.PeriodStart })
            .HasDatabaseName("IX_MemberActivitySnapshots_Guild_User_Period");

        // Unique constraint to prevent duplicate snapshots
        builder.HasIndex(s => new { s.GuildId, s.UserId, s.PeriodStart, s.Granularity })
            .IsUnique()
            .HasDatabaseName("IX_MemberActivitySnapshots_Unique");

        // Navigation properties
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
