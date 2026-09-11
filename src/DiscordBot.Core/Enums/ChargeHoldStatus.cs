namespace DiscordBot.Core.Enums;

/// <summary>
/// The outcome of asking the charge seam to reserve the price of a feature.
/// </summary>
public enum ChargeHoldStatus
{
    /// <summary>No active price applies, or the user holds an exempt role. Nothing was reserved.</summary>
    Free = 0,

    /// <summary>The price was reserved. The caller must commit or release the hold.</summary>
    Held = 1,

    /// <summary>The wallet cannot cover the price once open holds are counted.</summary>
    InsufficientFunds = 2,

    /// <summary>The wallet is below zero, so priced features are locked until it is back up.</summary>
    InDebt = 3,

    /// <summary>The priced currency is deactivated, so nothing may be charged in it.</summary>
    CurrencyInactive = 4,

    /// <summary>The priced currency no longer exists.</summary>
    NoWallet = 5
}
