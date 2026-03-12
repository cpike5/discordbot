using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserTtsPreset entity.
/// </summary>
public class UserTtsPresetConfiguration : IEntityTypeConfiguration<UserTtsPreset>
{
    public void Configure(EntityTypeBuilder<UserTtsPreset> builder)
    {
        builder.ToTable("UserTtsPresets");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        // Convert ulong to long for SQLite compatibility
        builder.Property(p => p.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.VoiceName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Style)
            .HasMaxLength(50);

        builder.Property(p => p.Speed)
            .IsRequired();

        builder.Property(p => p.Pitch)
            .IsRequired();

        builder.Property(p => p.Icon)
            .HasMaxLength(50);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Index on UserId for the common query pattern (get all presets for a user)
        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("IX_UserTtsPresets_UserId");
    }
}
