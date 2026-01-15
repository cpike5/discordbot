using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Theme entity.
/// </summary>
public class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("Themes");

        // Primary key
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        builder.Property(t => t.ThemeKey)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        // ColorDefinition stores JSON - use max length for flexibility
        builder.Property(t => t.ColorDefinition)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint on ThemeKey
        builder.HasIndex(t => t.ThemeKey)
            .IsUnique();

        // Index for active themes (common query filter)
        builder.HasIndex(t => t.IsActive);

        // Configure relationship with ApplicationUser
        builder.HasMany(t => t.PreferringUsers)
            .WithOne(u => u.PreferredTheme)
            .HasForeignKey(u => u.PreferredThemeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
