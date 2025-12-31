using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserModTag entity.
/// </summary>
public class UserModTagConfiguration : IEntityTypeConfiguration<UserModTag>
{
    public void Configure(EntityTypeBuilder<UserModTag> builder)
    {
        builder.ToTable("UserModTags");

        // Primary key
        builder.HasKey(ut => ut.Id);

        builder.Property(ut => ut.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(ut => ut.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(ut => ut.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(ut => ut.AppliedByUserId)
            .HasConversion<long>()
            .IsRequired();

        // Foreign key to ModTag
        builder.Property(ut => ut.TagId)
            .IsRequired();

        // DateTime property
        builder.Property(ut => ut.AppliedAt)
            .IsRequired();

        // Relationship with ModTag
        builder.HasOne(ut => ut.Tag)
            .WithMany(t => t.UserTags)
            .HasForeignKey(ut => ut.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // User tag listing - primary query pattern
        builder.HasIndex(ut => new { ut.GuildId, ut.UserId })
            .HasDatabaseName("IX_UserModTags_GuildId_UserId");

        // Prevent duplicate tag assignments
        builder.HasIndex(ut => new { ut.GuildId, ut.UserId, ut.TagId })
            .HasDatabaseName("IX_UserModTags_GuildId_UserId_TagId")
            .IsUnique();

        // Tag usage tracking
        builder.HasIndex(ut => new { ut.TagId, ut.AppliedAt })
            .HasDatabaseName("IX_UserModTags_TagId_AppliedAt");
    }
}
