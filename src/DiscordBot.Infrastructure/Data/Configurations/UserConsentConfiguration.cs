using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the UserConsent entity.
/// </summary>
public class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> builder)
    {
        builder.ToTable("UserConsents");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // Convert ulong to long for SQLite compatibility
        builder.Property(c => c.DiscordUserId)
            .HasConversion<long>()
            .IsRequired();

        // Store ConsentType as int
        builder.Property(c => c.ConsentType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.GrantedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.RevokedAt)
            .IsRequired(false);

        builder.Property(c => c.GrantedVia)
            .HasMaxLength(50);

        builder.Property(c => c.RevokedVia)
            .HasMaxLength(50);

        // Relationship with User entity
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.DiscordUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for efficient lookups (DiscordUserId, ConsentType)
        // This supports the common query pattern: find active consent by user and type
        builder.HasIndex(c => new { c.DiscordUserId, c.ConsentType })
            .HasDatabaseName("IX_UserConsents_DiscordUserId_ConsentType");

        // Index on RevokedAt for filtering active consents
        builder.HasIndex(c => c.RevokedAt)
            .HasDatabaseName("IX_UserConsents_RevokedAt");

        // Index on GrantedAt for temporal queries
        builder.HasIndex(c => c.GrantedAt)
            .HasDatabaseName("IX_UserConsents_GrantedAt");
    }
}
