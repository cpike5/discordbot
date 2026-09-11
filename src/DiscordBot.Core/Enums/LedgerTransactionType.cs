namespace DiscordBot.Core.Enums;

/// <summary>
/// What a ledger row represents. The type decides which balance rules apply.
/// </summary>
public enum LedgerTransactionType
{
    /// <summary>New units created by a mint authority. The only source of currency.</summary>
    Mint = 0,

    /// <summary>The credit half of a user-to-user transfer.</summary>
    TransferIn = 1,

    /// <summary>The debit half of a user-to-user transfer.</summary>
    TransferOut = 2,

    /// <summary>Payment for a priced feature.</summary>
    Spend = 3,

    /// <summary>Reversal of a spend.</summary>
    Refund = 4,

    /// <summary>A moderator-initiated debit. The only row that may cross zero.</summary>
    Fine = 5,

    /// <summary>A correction referencing the row it fixes.</summary>
    Adjustment = 6
}
