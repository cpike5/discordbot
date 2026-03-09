using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserSoundFavorite entity.
/// </summary>
public class UserSoundFavoriteConfiguration : IEntityTypeConfiguration<UserSoundFavorite>
{
    public void Configure(EntityTypeBuilder<UserSoundFavorite> builder)
    {
        builder.ToTable("UserSoundFavorites");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        // Convert ulong to long for SQLite compatibility
        builder.Property(f => f.UserId)
            .HasConversion<long>()
            .IsRequired();

        // Convert ulong to long for SQLite compatibility
        builder.Property(f => f.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(f => f.SoundId)
            .IsRequired();

        builder.Property(f => f.FavoritedAt)
            .IsRequired();

        // Relationship with Sound entity
        builder.HasOne(f => f.Sound)
            .WithMany()
            .HasForeignKey(f => f.SoundId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique index to prevent duplicate favorites
        builder.HasIndex(f => new { f.UserId, f.SoundId, f.GuildId })
            .IsUnique()
            .HasDatabaseName("IX_UserSoundFavorites_UserId_SoundId_GuildId");

        // Index on (UserId, GuildId) for the common query pattern
        builder.HasIndex(f => new { f.UserId, f.GuildId })
            .HasDatabaseName("IX_UserSoundFavorites_UserId_GuildId");
    }
}
