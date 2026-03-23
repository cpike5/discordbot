using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the SoundCategory entity.
/// </summary>
public class SoundCategoryConfiguration : IEntityTypeConfiguration<SoundCategory>
{
    public void Configure(EntityTypeBuilder<SoundCategory> builder)
    {
        builder.ToTable("SoundCategories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // Convert ulong to long for SQLite compatibility
        builder.Property(c => c.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Unique index on (GuildId, Name) to prevent duplicate category names per guild
        builder.HasIndex(c => new { c.GuildId, c.Name })
            .IsUnique()
            .HasDatabaseName("IX_SoundCategories_GuildId_Name");

        // Index on GuildId for foreign key lookups and GetByGuildAsync queries
        builder.HasIndex(c => c.GuildId)
            .HasDatabaseName("IX_SoundCategories_GuildId");

        // Relationship with Guild entity
        builder.HasOne(c => c.Guild)
            .WithMany()
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
