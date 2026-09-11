namespace DiscordBot.Core.DTOs;

/// <summary>
/// Outcome of a mint. <see cref="Transaction"/> carries the row that was written, or the row a
/// duplicate idempotency key matched.
/// </summary>
public record MintResult
{
    /// <summary>Whether the mint was applied.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The ledger row written.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>True when the idempotency key already existed and nothing new was written.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static MintResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Outcome of a spend against a wallet.
/// </summary>
public record SpendResult
{
    /// <summary>Whether the spend was applied.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The ledger row written.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>Balance the wallet held when the spend was evaluated.</summary>
    public long Balance { get; init; }

    /// <summary>True when the idempotency key already existed and nothing new was written.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static SpendResult Failed(string error, long balance = 0) =>
        new() { Success = false, Error = error, Balance = balance };
}

/// <summary>
/// Outcome of a user-to-user transfer. Both halves are written in one transaction and reference
/// each other.
/// </summary>
public record TransferResult
{
    /// <summary>Whether the transfer was applied.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The sender's <c>TransferOut</c> row.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>The recipient's <c>TransferIn</c> row.</summary>
    public LedgerTransactionDto? CounterpartTransaction { get; init; }

    /// <summary>Sender balance when the transfer was evaluated.</summary>
    public long SenderBalance { get; init; }

    /// <summary>True when the idempotency key already existed and nothing new was written.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static TransferResult Failed(string error, long senderBalance = 0) =>
        new() { Success = false, Error = error, SenderBalance = senderBalance };
}

/// <summary>
/// Outcome of a fine. A fine that clamps still writes one row, for the clamped amount, and says so.
/// </summary>
public record FineResult
{
    /// <summary>Whether the fine was applied.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The ledger row written.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>
    /// The amount actually taken, when it differs from the amount asked for. Null when the full
    /// fine applied.
    /// </summary>
    public long? ClampedAmount { get; init; }

    /// <summary>True when the fine was reduced to stop at zero or at the currency's debt floor.</summary>
    public bool WasClamped => ClampedAmount.HasValue;

    /// <summary>True when the idempotency key already existed and nothing new was written.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static FineResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Outcome of an adjustment. Adjustments are the only way to correct a mistake and always
/// reference the row they fix.
/// </summary>
public record AdjustmentResult
{
    /// <summary>Whether the adjustment was applied.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The ledger row written.</summary>
    public LedgerTransactionDto? Transaction { get; init; }

    /// <summary>True when the idempotency key already existed and nothing new was written.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static AdjustmentResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Outcome of creating or editing a currency.
/// </summary>
public record CurrencyResult
{
    /// <summary>Whether the change was saved.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The saved currency.</summary>
    public CurrencyDto? Currency { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static CurrencyResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Outcome of granting a mint authority.
/// </summary>
public record MintAuthorityResult
{
    /// <summary>Whether the grant was saved.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The saved grant.</summary>
    public MintAuthorityDto? Authority { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static MintAuthorityResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Outcome of setting a price.
/// </summary>
public record PriceEntryResult
{
    /// <summary>Whether the price was saved.</summary>
    public bool Success { get; init; }

    /// <summary>Reason code from <see cref="CurrencyErrors"/> when it was not.</summary>
    public string? Error { get; init; }

    /// <summary>The saved price.</summary>
    public PriceEntryDto? Price { get; init; }

    /// <summary>Builds a failed result.</summary>
    public static PriceEntryResult Failed(string error) => new() { Success = false, Error = error };
}
