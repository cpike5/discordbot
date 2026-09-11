using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// The rules a caller supplies when creating a currency.
/// </summary>
public record CurrencyCreateDto
{
    /// <summary>Whether the new currency belongs to one guild or to every guild.</summary>
    public CurrencyScope Scope { get; init; }

    /// <summary>Owning guild. Required for a guild currency, must be null for a global one.</summary>
    public ulong? GuildId { get; init; }

    /// <summary>Display name, unique within the scope.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Emoji or short text shown next to amounts.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Whether users may send this currency to each other. Defaults to true for a guild currency
    /// and false for a global one when left null.
    /// </summary>
    public bool? IsTransferable { get; init; }

    /// <summary>Whether a fine may push a balance below zero.</summary>
    public bool AllowNegative { get; init; }

    /// <summary>Most negative balance a fine may reach. Required when debt is allowed.</summary>
    public long? DebtFloor { get; init; }

    /// <summary>Automatic income per interval. Groundwork only.</summary>
    public long? IncomeAmount { get; init; }

    /// <summary>How often income pays out. Groundwork only.</summary>
    public IncomeInterval? IncomeInterval { get; init; }
}

/// <summary>
/// The rules a caller may change on an existing currency. Scope, guild, and creator are fixed.
/// </summary>
public record CurrencyUpdateDto
{
    /// <summary>New display name. Null leaves it unchanged.</summary>
    public string? Name { get; init; }

    /// <summary>New symbol. Null leaves it unchanged.</summary>
    public string? Symbol { get; init; }

    /// <summary>New transfer flag. Null leaves it unchanged.</summary>
    public bool? IsTransferable { get; init; }

    /// <summary>New debt flag. Null leaves it unchanged.</summary>
    public bool? AllowNegative { get; init; }

    /// <summary>New debt floor. Null leaves it unchanged.</summary>
    public long? DebtFloor { get; init; }

    /// <summary>New income amount. Groundwork only. Null leaves it unchanged.</summary>
    public long? IncomeAmount { get; init; }

    /// <summary>New income interval. Groundwork only. Null leaves it unchanged.</summary>
    public IncomeInterval? IncomeInterval { get; init; }
}
