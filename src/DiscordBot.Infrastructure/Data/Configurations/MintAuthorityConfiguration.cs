using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="MintAuthority"/> entity.
/// </summary>
public class MintAuthorityConfiguration : IEntityTypeConfiguration<MintAuthority>
{
    public void Configure(EntityTypeBuilder<MintAuthority> builder)
    {
        builder.ToTable("MintAuthorities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PrincipalType)
            .IsRequired()
            .HasConversion<int>();

        // Null for the System principal, which is how a background job mints without a fake user.
        builder.Property(a => a.PrincipalId)
            .HasConversion<long?>();

        builder.Property(a => a.GrantedById)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(a => a.GrantedAt)
            .IsRequired();

        builder.HasIndex(a => new { a.CurrencyId, a.PrincipalType, a.PrincipalId })
            .HasDatabaseName("IX_MintAuthorities_CurrencyId_PrincipalType_PrincipalId");

        builder.HasOne(a => a.Currency)
            .WithMany(c => c.MintAuthorities)
            .HasForeignKey(a => a.CurrencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
