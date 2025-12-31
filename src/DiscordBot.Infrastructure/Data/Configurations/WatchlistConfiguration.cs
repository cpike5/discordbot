using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Watchlist entity.
/// </summary>
public class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
{
    public void Configure(EntityTypeBuilder<Watchlist> builder)
    {
        builder.ToTable("Watchlists");

        // Primary key
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(w => w.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(w => w.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(w => w.AddedByUserId)
            .HasConversion<long>()
            .IsRequired();

        // String property
        builder.Property(w => w.Reason)
            .HasMaxLength(2000);

        // DateTime property
        builder.Property(w => w.AddedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(w => w.Guild)
            .WithMany()
            .HasForeignKey(w => w.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // Guild watchlist query - primary pattern
        builder.HasIndex(w => new { w.GuildId, w.AddedAt })
            .HasDatabaseName("IX_Watchlists_GuildId_AddedAt");

        // Prevent duplicate entries and fast user lookup
        builder.HasIndex(w => new { w.GuildId, w.UserId })
            .HasDatabaseName("IX_Watchlists_GuildId_UserId")
            .IsUnique();

        // Moderator tracking
        builder.HasIndex(w => new { w.GuildId, w.AddedByUserId })
            .HasDatabaseName("IX_Watchlists_GuildId_AddedByUserId");
    }
}
