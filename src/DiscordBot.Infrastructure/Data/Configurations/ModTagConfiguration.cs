using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ModTag entity.
/// </summary>
public class ModTagConfiguration : IEntityTypeConfiguration<ModTag>
{
    public void Configure(EntityTypeBuilder<ModTag> builder)
    {
        builder.ToTable("ModTags");

        // Primary key
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        // ulong property converted to long for SQLite compatibility
        builder.Property(t => t.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // String properties
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Color)
            .IsRequired()
            .HasMaxLength(7); // Hex color code (#RRGGBB)

        builder.Property(t => t.Description)
            .HasMaxLength(200);

        // Enum stored as int
        builder.Property(t => t.Category)
            .HasConversion<int>()
            .IsRequired();

        // Boolean
        builder.Property(t => t.IsFromTemplate)
            .IsRequired();

        // DateTime property
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(t => t.Guild)
            .WithMany()
            .HasForeignKey(t => t.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with UserModTag (one-to-many)
        builder.HasMany(t => t.UserTags)
            .WithOne(ut => ut.Tag)
            .HasForeignKey(ut => ut.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // Guild tag listing
        builder.HasIndex(t => new { t.GuildId, t.Name })
            .HasDatabaseName("IX_ModTags_GuildId_Name")
            .IsUnique();

        // Category filtering
        builder.HasIndex(t => new { t.GuildId, t.Category })
            .HasDatabaseName("IX_ModTags_GuildId_Category");
    }
}
