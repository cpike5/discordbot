using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ModNote entity.
/// </summary>
public class ModNoteConfiguration : IEntityTypeConfiguration<ModNote>
{
    public void Configure(EntityTypeBuilder<ModNote> builder)
    {
        builder.ToTable("ModNotes");

        // Primary key
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedOnAdd();

        // ulong properties converted to long for SQLite compatibility
        builder.Property(n => n.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(n => n.TargetUserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(n => n.AuthorUserId)
            .HasConversion<long>()
            .IsRequired();

        // String property
        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(2000);

        // DateTime property
        builder.Property(n => n.CreatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(n => n.Guild)
            .WithMany()
            .HasForeignKey(n => n.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns and performance optimization
        // User note listing - primary query pattern
        builder.HasIndex(n => new { n.GuildId, n.TargetUserId, n.CreatedAt })
            .HasDatabaseName("IX_ModNotes_GuildId_TargetUserId_CreatedAt");

        // Author tracking
        builder.HasIndex(n => new { n.GuildId, n.AuthorUserId, n.CreatedAt })
            .HasDatabaseName("IX_ModNotes_GuildId_AuthorUserId_CreatedAt");
    }
}
