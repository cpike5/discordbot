using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Sound entity.
/// </summary>
public class SoundConfiguration : IEntityTypeConfiguration<Sound>
{
    public void Configure(EntityTypeBuilder<Sound> builder)
    {
        builder.ToTable("Sounds");

        // Primary key on Id
        builder.HasKey(s => s.Id);

        // ulong property converted to long for SQLite compatibility
        builder.Property(s => s.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // Index on GuildId for foreign key lookups
        builder.HasIndex(s => s.GuildId);

        // String properties
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.FileName)
            .IsRequired()
            .HasMaxLength(255);

        // Numeric properties
        builder.Property(s => s.FileSizeBytes)
            .IsRequired();

        builder.Property(s => s.DurationSeconds)
            .IsRequired();

        builder.Property(s => s.PlayCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Nullable ulong property converted to long for SQLite compatibility
        builder.Property(s => s.UploadedById)
            .HasConversion<long?>();

        // DateTime property - stored as UTC
        builder.Property(s => s.UploadedAt)
            .IsRequired();

        // Unique index on (GuildId, Name) to prevent duplicate sound names per guild
        builder.HasIndex(s => new { s.GuildId, s.Name })
            .IsUnique();

        // Relationship with Guild
        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
