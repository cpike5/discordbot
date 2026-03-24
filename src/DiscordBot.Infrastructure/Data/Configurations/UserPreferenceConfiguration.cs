using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserPreference entity.
/// </summary>
public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("UserPreferences");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        // Convert ulong to long for SQLite compatibility
        builder.Property(p => p.UserId)
            .HasConversion<long>()
            .IsRequired();

        // Convert ulong to long for SQLite compatibility
        builder.Property(p => p.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Value)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Relationship with Guild entity
        builder.HasOne(p => p.Guild)
            .WithMany()
            .HasForeignKey(p => p.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique index to prevent duplicate preferences per user/guild/key
        builder.HasIndex(p => new { p.UserId, p.GuildId, p.Key })
            .IsUnique()
            .HasDatabaseName("IX_UserPreferences_UserId_GuildId_Key");
    }
}
