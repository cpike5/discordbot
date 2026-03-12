using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the VoxMessageHistory entity.
/// </summary>
public class VoxMessageHistoryConfiguration : IEntityTypeConfiguration<VoxMessageHistory>
{
    public void Configure(EntityTypeBuilder<VoxMessageHistory> builder)
    {
        builder.ToTable("VoxMessageHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedOnAdd();

        // Convert ulong to long for SQLite compatibility
        builder.Property(h => h.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Convert ulong to long for SQLite compatibility
        builder.Property(h => h.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(h => h.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(h => h.ClipGroup)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(h => h.WordGapMs)
            .IsRequired();

        builder.Property(h => h.IsFavorite)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(h => h.PlayedAt)
            .IsRequired();

        // Relationship with Guild entity
        builder.HasOne(h => h.Guild)
            .WithMany()
            .HasForeignKey(h => h.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for recent history queries (UserId, GuildId, PlayedAt)
        builder.HasIndex(h => new { h.UserId, h.GuildId, h.PlayedAt })
            .HasDatabaseName("IX_VoxMessageHistory_UserId_GuildId_PlayedAt");

        // Index for favorite queries (UserId, GuildId, IsFavorite)
        builder.HasIndex(h => new { h.UserId, h.GuildId, h.IsFavorite })
            .HasDatabaseName("IX_VoxMessageHistory_UserId_GuildId_IsFavorite");
    }
}
