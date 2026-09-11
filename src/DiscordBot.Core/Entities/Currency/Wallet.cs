namespace DiscordBot.Core.Entities;

/// <summary>
/// One user's holding in one currency. <see cref="CachedBalance"/> is a cache of the wallet's
/// ledger rows and is written in the same transaction as each row, so it can always be rebuilt by
/// summing <see cref="LedgerTransaction.Amount"/>.
/// </summary>
public class Wallet
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The currency this wallet holds.</summary>
    public Guid CurrencyId { get; set; }

    /// <summary>Discord user snowflake ID of the holder.</summary>
    public ulong UserId { get; set; }

    /// <summary>Sum of the wallet's ledger rows.</summary>
    public long CachedBalance { get; set; }

    /// <summary>UTC creation timestamp. A wallet is created on first credit.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation to the owning currency.</summary>
    public Currency? Currency { get; set; }

    /// <summary>Rows written against this wallet.</summary>
    public ICollection<LedgerTransaction> Transactions { get; set; } = new List<LedgerTransaction>();
}
