using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// The price of one feature in one guild.
/// </summary>
public record PriceEntryDto
{
    /// <summary>Price entry id.</summary>
    public Guid Id { get; init; }

    /// <summary>The priced feature, shaped <c>{area}:{identifier}</c>.</summary>
    public string FeatureKey { get; init; } = string.Empty;

    /// <summary>The guild this price applies in, or null for everywhere.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong? GuildId { get; init; }

    /// <summary>The currency charged.</summary>
    public Guid CurrencyId { get; init; }

    /// <summary>Currency display name.</summary>
    public string CurrencyName { get; init; } = string.Empty;

    /// <summary>Currency symbol.</summary>
    public string CurrencySymbol { get; init; } = string.Empty;

    /// <summary>Price in whole units.</summary>
    public long Amount { get; init; }

    /// <summary>Discord role snowflake IDs whose holders pay nothing.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public IReadOnlyList<ulong> ExemptRoleIds { get; init; } = Array.Empty<ulong>();

    /// <summary>Whether this price is charged.</summary>
    public bool IsActive { get; init; }

    /// <summary>Discord user snowflake ID of whoever last saved this entry.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong UpdatedById { get; init; }

    /// <summary>UTC timestamp of the last save.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// The fields a caller supplies when setting a price.
/// </summary>
public record PriceEntrySaveDto
{
    /// <summary>The priced feature, shaped <c>{area}:{identifier}</c>.</summary>
    public string FeatureKey { get; init; } = string.Empty;

    /// <summary>The guild this price applies in, or null for everywhere.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong? GuildId { get; init; }

    /// <summary>The currency to charge.</summary>
    public Guid CurrencyId { get; init; }

    /// <summary>Price in whole units. Must be greater than zero.</summary>
    public long Amount { get; init; }

    /// <summary>Discord role snowflake IDs whose holders pay nothing.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public IReadOnlyList<ulong> ExemptRoleIds { get; init; } = Array.Empty<ulong>();

    /// <summary>Whether the price is charged.</summary>
    public bool IsActive { get; init; } = true;
}
