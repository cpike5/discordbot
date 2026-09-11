namespace DiscordBot.Core.DTOs;

/// <summary>
/// One user's balance in one currency, with enough currency detail to render an amount.
/// </summary>
public record WalletDto
{
    /// <summary>Wallet id.</summary>
    public Guid Id { get; init; }

    /// <summary>The currency held.</summary>
    public Guid CurrencyId { get; init; }

    /// <summary>Currency display name.</summary>
    public string CurrencyName { get; init; } = string.Empty;

    /// <summary>Currency symbol.</summary>
    public string CurrencySymbol { get; init; } = string.Empty;

    /// <summary>Owning guild of the currency, or null when it is global.</summary>
    public ulong? GuildId { get; init; }

    /// <summary>Discord user snowflake ID of the holder.</summary>
    public ulong UserId { get; init; }

    /// <summary>Current balance.</summary>
    public long Balance { get; init; }

    /// <summary>Whether the holder is in debt and therefore locked out of priced features.</summary>
    public bool IsInDebt => Balance < 0;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// One wallet's cached balance next to the sum of its ledger rows, for the reconcile check.
/// </summary>
public record WalletReconciliationDto
{
    /// <summary>Wallet id.</summary>
    public Guid WalletId { get; init; }

    /// <summary>Discord user snowflake ID of the holder.</summary>
    public ulong UserId { get; init; }

    /// <summary>The balance stored on the wallet row.</summary>
    public long CachedBalance { get; init; }

    /// <summary>The balance implied by summing the wallet's ledger rows.</summary>
    public long LedgerSum { get; init; }

    /// <summary>Difference between the two. Zero means the wallet reconciles.</summary>
    public long Difference => CachedBalance - LedgerSum;
}
