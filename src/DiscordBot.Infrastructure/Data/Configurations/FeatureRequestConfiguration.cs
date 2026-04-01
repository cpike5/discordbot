using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the FeatureRequest entity.
/// </summary>
public class FeatureRequestConfiguration : IEntityTypeConfiguration<FeatureRequest>
{
    public void Configure(EntityTypeBuilder<FeatureRequest> builder)
    {
        builder.ToTable("FeatureRequests");

        // Primary key
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(f => f.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(f => f.SubmittedByUserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(f => f.ReviewedByUserId)
            .HasConversion<long?>();

        // Enum stored as int
        builder.Property(f => f.Status)
            .HasConversion<int>()
            .IsRequired();

        // String properties with sensible max lengths
        builder.Property(f => f.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.GatheredRequirements)
            .HasMaxLength(4000);

        builder.Property(f => f.ConsolidatedSummary)
            .HasMaxLength(4000);

        builder.Property(f => f.ReviewNotes)
            .HasMaxLength(1000);

        builder.Property(f => f.DocBranchName)
            .HasMaxLength(500);

        builder.Property(f => f.DocPath)
            .HasMaxLength(500);

        builder.Property(f => f.DocGenError)
            .HasMaxLength(4000);

        // DateTime properties — all stored as UTC
        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for the primary query pattern (list by guild + optional status filter)
        builder.HasIndex(f => new { f.GuildId, f.Status, f.CreatedAt })
            .HasDatabaseName("IX_FeatureRequests_GuildId_Status_CreatedAt");
    }
}
