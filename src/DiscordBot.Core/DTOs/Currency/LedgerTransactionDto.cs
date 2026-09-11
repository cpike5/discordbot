using DiscordBot.Core.Enums;
using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// One ledger row as rendered in history views.
/// </summary>
public record LedgerTransactionDto
{
    /// <summary>Row id.</summary>
    public long Id { get; init; }

    /// <summary>The wallet moved.</summary>
    public Guid WalletId { get; init; }

    /// <summary>What this row represents.</summary>
    public LedgerTransactionType Type { get; init; }

    /// <summary>Where the row came from.</summary>
    public LedgerSource Source { get; init; }

    /// <summary>Signed amount.</summary>
    public long Amount { get; init; }

    /// <summary>Wallet balance after this row.</summary>
    public long BalanceAfter { get; init; }

    /// <summary>Free text reason.</summary>
    public string? Reason { get; init; }

    /// <summary>The priced feature, when this row paid for one.</summary>
    public string? FeatureKey { get; init; }

    /// <summary>Caller-supplied idempotency key.</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>The related row, when there is one.</summary>
    public long? ReferenceTransactionId { get; init; }

    /// <summary>The moderation case a fine was opened alongside.</summary>
    public Guid? ModerationCaseId { get; init; }

    /// <summary>Discord user who caused the row. Null for the system principal.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong? ActorId { get; init; }

    /// <summary>Audit log correlation id, when one exists.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>UTC timestamp.</summary>
    public DateTime CreatedAt { get; init; }
}
