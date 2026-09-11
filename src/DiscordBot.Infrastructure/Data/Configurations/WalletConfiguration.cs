using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Wallet"/> entity.
/// </summary>
public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(w => w.CachedBalance)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // One wallet per user per currency. This index is also what makes the
        // IWalletRepository.GetOrCreateAsync race safe.
        builder.HasIndex(w => new { w.CurrencyId, w.UserId })
            .IsUnique()
            .HasDatabaseName("IX_Wallets_CurrencyId_UserId");

        // Debtor lists and leaderboards sort on balance within a currency.
        builder.HasIndex(w => new { w.CurrencyId, w.CachedBalance })
            .HasDatabaseName("IX_Wallets_CurrencyId_CachedBalance");

        builder.HasIndex(w => w.UserId)
            .HasDatabaseName("IX_Wallets_UserId");

        // Restrict: currencies are deactivated, never deleted, and a wallet outliving its
        // currency would orphan history.
        builder.HasOne(w => w.Currency)
            .WithMany(c => c.Wallets)
            .HasForeignKey(w => w.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
