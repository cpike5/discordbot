using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="LedgerTransaction"/> append-only ledger.
/// </summary>
public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        builder.Property(t => t.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Source)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Amount)
            .IsRequired();

        builder.Property(t => t.BalanceAfter)
            .IsRequired();

        builder.Property(t => t.Reason)
            .HasMaxLength(512);

        builder.Property(t => t.FeatureKey)
            .HasMaxLength(128);

        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(t => t.ActorId)
            .HasConversion<long?>();

        builder.Property(t => t.CorrelationId)
            .HasMaxLength(100);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // The invariant the whole system rests on: one row per idempotency key, so a retried
        // spend, mint, or refund can never write twice.
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_LedgerTransactions_IdempotencyKey");

        builder.HasIndex(t => new { t.WalletId, t.CreatedAt })
            .HasDatabaseName("IX_LedgerTransactions_WalletId_CreatedAt");

        // No FK on ReferenceTransactionId on purpose: the two halves of a transfer point at each
        // other, which a non-deferrable FK cannot express on either provider.
        builder.HasIndex(t => t.ReferenceTransactionId)
            .HasDatabaseName("IX_LedgerTransactions_ReferenceTransactionId");

        // Restrict: rows are the record and outlive everything above them.
        builder.HasOne(t => t.Wallet)
            .WithMany(w => w.Transactions)
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
