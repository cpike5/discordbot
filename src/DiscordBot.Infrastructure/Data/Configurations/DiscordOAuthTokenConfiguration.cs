using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the DiscordOAuthToken entity.
/// </summary>
public class DiscordOAuthTokenConfiguration : IEntityTypeConfiguration<DiscordOAuthToken>
{
    public void Configure(EntityTypeBuilder<DiscordOAuthToken> builder)
    {
        builder.ToTable("DiscordOAuthTokens");

        // Primary key
        builder.HasKey(t => t.Id);

        // One-to-one relationship with ApplicationUser
        builder.HasOne(t => t.ApplicationUser)
            .WithOne(u => u.DiscordOAuthToken)
            .HasForeignKey<DiscordOAuthToken>(t => t.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure string properties with max lengths
        builder.Property(t => t.ApplicationUserId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity key length

        builder.Property(t => t.EncryptedAccessToken)
            .IsRequired()
            .HasMaxLength(2000); // Encrypted tokens are larger than plain text

        builder.Property(t => t.EncryptedRefreshToken)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(t => t.Scopes)
            .IsRequired()
            .HasMaxLength(500);

        // Configure ulong to long conversion for SQLite compatibility
        builder.Property(t => t.DiscordUserId)
            .HasConversion(
                v => (long)v,
                v => (ulong)v)
            .IsRequired();

        // Configure required DateTime properties
        builder.Property(t => t.AccessTokenExpiresAt)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.LastRefreshedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique index on ApplicationUserId (one-to-one relationship)
        builder.HasIndex(t => t.ApplicationUserId)
            .IsUnique();

        // Index on DiscordUserId for lookups
        builder.HasIndex(t => t.DiscordUserId);

        // Index on AccessTokenExpiresAt for finding expiring tokens
        builder.HasIndex(t => t.AccessTokenExpiresAt);
    }
}
