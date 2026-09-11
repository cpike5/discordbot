namespace DiscordBot.Core.Enums;

/// <summary>
/// Where a ledger row came from. Meaningful for <see cref="LedgerTransactionType.Mint"/>;
/// <see cref="Manual"/> for every other type.
/// </summary>
public enum LedgerSource
{
    /// <summary>A person triggered it.</summary>
    Manual = 0,

    /// <summary>A recurring income job minted it. Groundwork only in this project.</summary>
    Income = 1,

    /// <summary>A moderator or admin awarded it.</summary>
    Award = 2,

    /// <summary>The bot itself, with no human actor.</summary>
    System = 3
}
