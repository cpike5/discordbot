using DiscordBot.Core.Enums;
using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// A currency and its rules, as shown in commands and the portal.
/// </summary>
public record CurrencyDto
{
    /// <summary>Currency id.</summary>
    public Guid Id { get; init; }

    /// <summary>Whether this currency belongs to one guild or to every guild.</summary>
    public CurrencyScope Scope { get; init; }

    /// <summary>Owning guild, or null for a global currency.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong? GuildId { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Emoji or short text shown next to amounts.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Whether users may send this currency to each other.</summary>
    public bool IsTransferable { get; init; }

    /// <summary>Whether a fine may push a balance below zero.</summary>
    public bool AllowNegative { get; init; }

    /// <summary>Most negative balance a fine may reach, stored negative.</summary>
    public long? DebtFloor { get; init; }

    /// <summary>Automatic income per interval. Groundwork only.</summary>
    public long? IncomeAmount { get; init; }

    /// <summary>How often income pays out. Groundwork only.</summary>
    public IncomeInterval? IncomeInterval { get; init; }

    /// <summary>Whether the currency accepts new operations.</summary>
    public bool IsActive { get; init; }

    /// <summary>Discord user snowflake ID of the creator.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong CreatedById { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }
}
