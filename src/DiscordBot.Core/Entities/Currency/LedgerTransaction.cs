using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// One immutable balance change on one wallet. Rows are never updated or deleted; they are the
/// record. Written only through <c>ILedgerRepository.AppendAsync</c>.
/// </summary>
public class LedgerTransaction
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>The wallet this row moves.</summary>
    public Guid WalletId { get; set; }

    /// <summary>What this row represents.</summary>
    public LedgerTransactionType Type { get; set; }

    /// <summary>
    /// Where the row came from. Meaningful for <see cref="LedgerTransactionType.Mint"/>;
    /// <see cref="LedgerSource.Manual"/> otherwise.
    /// </summary>
    public LedgerSource Source { get; set; }

    /// <summary>Signed amount. Positive for credits, negative for debits.</summary>
    public long Amount { get; set; }

    /// <summary>
    /// Wallet balance after this row. Set by the ledger repository so history renders without
    /// summing.
    /// </summary>
    public long BalanceAfter { get; set; }

    /// <summary>
    /// Free text. Required for <see cref="LedgerTransactionType.Mint"/>,
    /// <see cref="LedgerTransactionType.Fine"/>, and
    /// <see cref="LedgerTransactionType.Adjustment"/>.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// The priced feature, e.g. <c>soundboard:{soundId}</c>. Set on
    /// <see cref="LedgerTransactionType.Spend"/> and <see cref="LedgerTransactionType.Refund"/>.
    /// </summary>
    public string? FeatureKey { get; set; }

    /// <summary>
    /// Caller-supplied key. Unique across the table: a duplicate returns the existing row and
    /// writes nothing, which is what stops retries from double-charging.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// The row this one relates to: the other side of a transfer, the spend a refund reverses, or
    /// the row an adjustment corrects.
    /// </summary>
    public long? ReferenceTransactionId { get; set; }

    /// <summary>The moderation case a fine was opened alongside, when there is one.</summary>
    public Guid? ModerationCaseId { get; set; }

    /// <summary>Discord user who caused the row. Null for the system principal.</summary>
    public ulong? ActorId { get; set; }

    /// <summary>Matches the audit log correlation id when one exists.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>UTC timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation to the wallet.</summary>
    public Wallet? Wallet { get; set; }
}
