using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the VerificationCode entity.
/// </summary>
public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.ToTable("VerificationCodes");

        // Primary key
        builder.HasKey(v => v.Id);

        // Foreign key to ApplicationUser
        builder.HasOne(v => v.ApplicationUser)
            .WithMany()
            .HasForeignKey(v => v.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // String property configurations
        builder.Property(v => v.ApplicationUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(v => v.Code)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(v => v.IpAddress)
            .HasMaxLength(45);

        // Configure ulong to long conversion for SQLite compatibility
        builder.Property(v => v.DiscordUserId)
            .HasConversion(
                v => v.HasValue ? (long)v.Value : (long?)null,
                v => v.HasValue ? (ulong)v.Value : (ulong?)null);

        // Configure enum as int
        builder.Property(v => v.Status)
            .HasConversion<int>()
            .IsRequired();

        // Configure DateTime properties
        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.ExpiresAt)
            .IsRequired();

        // Indexes for efficient queries
        builder.HasIndex(v => v.Code);
        builder.HasIndex(v => v.ApplicationUserId);
        builder.HasIndex(v => v.DiscordUserId);
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.ExpiresAt);

        // Composite index for cleanup queries
        builder.HasIndex(v => new { v.Status, v.ExpiresAt });
    }
}
