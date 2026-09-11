namespace DiscordBot.Core.DTOs;

/// <summary>
/// Reason codes returned in the <c>Error</c> of a currency result record. They are codes, not
/// messages: the Discord and portal layers turn them into text. Names match the
/// <c>ChargeHoldStatus</c> members where the two overlap, so a refusal reads the same whichever
/// seam produced it.
/// </summary>
public static class CurrencyErrors
{
    /// <summary>The currency id does not exist.</summary>
    public const string CurrencyNotFound = "CurrencyNotFound";

    /// <summary>The currency is deactivated, so every operation except reading is refused.</summary>
    public const string CurrencyInactive = "CurrencyInactive";

    /// <summary>The wallet holds less than the amount asked for.</summary>
    public const string InsufficientFunds = "InsufficientFunds";

    /// <summary>The wallet is below zero, so priced actions are locked until it is back up.</summary>
    public const string InDebt = "InDebt";

    /// <summary>The amount is zero or negative where a positive amount is required.</summary>
    public const string InvalidAmount = "InvalidAmount";

    /// <summary>The currency has transfers turned off.</summary>
    public const string TransfersDisabled = "TransfersDisabled";

    /// <summary>A user cannot send currency to themselves.</summary>
    public const string SelfTransfer = "SelfTransfer";

    /// <summary>Fines only apply to guild currencies.</summary>
    public const string FineRequiresGuildCurrency = "FineRequiresGuildCurrency";

    /// <summary>An adjustment must name an existing ledger row.</summary>
    public const string ReferenceNotFound = "ReferenceNotFound";

    /// <summary>A reason is required for this operation.</summary>
    public const string ReasonRequired = "ReasonRequired";

    /// <summary>The caller supplied no idempotency key.</summary>
    public const string IdempotencyKeyRequired = "IdempotencyKeyRequired";

    /// <summary>A currency with this name already exists in the same scope.</summary>
    public const string DuplicateName = "DuplicateName";

    /// <summary>A required field was blank or a field combination is not allowed.</summary>
    public const string InvalidCurrencyRules = "InvalidCurrencyRules";

    /// <summary>The feature key is blank or too long.</summary>
    public const string InvalidFeatureKey = "InvalidFeatureKey";

    /// <summary>A guild currency may only price features in its own guild.</summary>
    public const string PriceScopeMismatch = "PriceScopeMismatch";

    /// <summary>The wallet id does not exist.</summary>
    public const string WalletNotFound = "WalletNotFound";

    /// <summary>The hold has expired out of the store, or never existed.</summary>
    public const string HoldNotFound = "HoldNotFound";

    /// <summary>The caller is not on the currency's mint authority list.</summary>
    public const string NotAuthorizedToMint = "NotAuthorizedToMint";

    /// <summary>The transaction being refunded is not a spend.</summary>
    public const string NotRefundable = "NotRefundable";

    /// <summary>The spend has already been refunded.</summary>
    public const string AlreadyRefunded = "AlreadyRefunded";
}
