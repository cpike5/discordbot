namespace DiscordBot.Core.DTOs;

/// <summary>
/// One reservation against a wallet, held in memory until the feature that asked for it commits or
/// releases it. Nothing is written to the ledger while a hold is open.
/// </summary>
public record ChargeHold
{
    /// <summary>Hold id. The only handle a caller needs to commit or release.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The wallet the amount is reserved against.</summary>
    public Guid WalletId { get; init; }

    /// <summary>The currency being charged.</summary>
    public Guid CurrencyId { get; init; }

    /// <summary>Discord user snowflake ID paying.</summary>
    public ulong UserId { get; init; }

    /// <summary>Guild the priced feature was used in.</summary>
    public ulong GuildId { get; init; }

    /// <summary>The priced feature, shaped <c>{area}:{identifier}</c>.</summary>
    public string FeatureKey { get; init; } = string.Empty;

    /// <summary>Units reserved.</summary>
    public long Amount { get; init; }

    /// <summary>The key the <c>Spend</c> row will carry, so a retried commit writes once.</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the reservation stops counting against the wallet. An expired hold still commits: the
    /// expiry only frees the funds for concurrent holds, so a slow feature is not punished.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// The <c>Spend</c> row this hold became, once committed. A committed hold reserves nothing and
    /// lets a repeated commit return the same row instead of writing a second one.
    /// </summary>
    public long? CommittedTransactionId { get; init; }

    /// <summary>Whether the reservation has lapsed. Committing is still allowed.</summary>
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
}
