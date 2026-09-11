using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// The answer to "may this user use this priced feature, and at what cost". A
/// <see cref="ChargeHoldStatus.Free"/> or refused result carries no hold.
/// </summary>
public record ChargeHoldResult
{
    /// <summary>What the charge seam decided.</summary>
    public ChargeHoldStatus Status { get; init; }

    /// <summary>The hold to commit or release, set only when <see cref="Status"/> is Held.</summary>
    public Guid? HoldId { get; init; }

    /// <summary>The price of the feature. Zero when it is free here.</summary>
    public long Price { get; init; }

    /// <summary>The user's balance in the priced currency when the decision was made.</summary>
    public long Balance { get; init; }

    /// <summary>Symbol of the priced currency, for rendering the refusal.</summary>
    public string? CurrencySymbol { get; init; }

    /// <summary>Whether the caller may go ahead: nothing to pay, or the price is reserved.</summary>
    public bool IsAllowed => Status is ChargeHoldStatus.Free or ChargeHoldStatus.Held;

    /// <summary>The result for a feature that costs nothing here.</summary>
    public static ChargeHoldResult Free() => new() { Status = ChargeHoldStatus.Free };

    /// <summary>Builds a refusal.</summary>
    public static ChargeHoldResult Refused(ChargeHoldStatus status, long price, long balance, string? symbol) =>
        new() { Status = status, Price = price, Balance = balance, CurrencySymbol = symbol };
}

/// <summary>
/// Outcome of committing a hold into a <c>Spend</c> row.
/// </summary>
public record ChargeCommitResult
{
    /// <summary>Whether the spend was written, or already existed.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The <c>Spend</c> row.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>Balance after the spend.</summary>
    public long Balance { get; init; }

    /// <summary>True when the hold had already been committed, so nothing new was written.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static ChargeCommitResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Outcome of refunding a committed spend.
/// </summary>
public record RefundResult
{
    /// <summary>Whether the refund was written.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The <c>Refund</c> row, which references the spend it reverses.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static RefundResult Failed(string error) => new() { Success = false, Error = error };
}
