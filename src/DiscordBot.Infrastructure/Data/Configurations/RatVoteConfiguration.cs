using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the RatVote entity.
/// </summary>
public class RatVoteConfiguration : IEntityTypeConfiguration<RatVote>
{
    public void Configure(EntityTypeBuilder<RatVote> builder)
    {
        builder.ToTable("RatVotes");

        // Primary key
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .ValueGeneratedOnAdd();

        // Foreign key to RatWatch
        builder.Property(v => v.RatWatchId)
            .IsRequired();

        // ulong property converted to long for SQLite compatibility
        builder.Property(v => v.VoterUserId)
            .HasConversion<long>()
            .IsRequired();

        // Boolean for vote type
        builder.Property(v => v.IsGuiltyVote)
            .IsRequired();

        // DateTime property - stored as UTC
        builder.Property(v => v.VotedAt)
            .IsRequired();

        // Relationship with RatWatch
        builder.HasOne(v => v.RatWatch)
            .WithMany(r => r.Votes)
            .HasForeignKey(v => v.RatWatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint - one vote per user per watch
        builder.HasIndex(v => new { v.RatWatchId, v.VoterUserId })
            .IsUnique()
            .HasDatabaseName("IX_RatVotes_RatWatchId_VoterUserId_Unique");

        // Index for efficient watch lookup
        builder.HasIndex(v => v.RatWatchId)
            .HasDatabaseName("IX_RatVotes_RatWatchId");
    }
}
