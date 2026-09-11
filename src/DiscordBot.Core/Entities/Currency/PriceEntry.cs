namespace DiscordBot.Core.Entities;

/// <summary>
/// The price of one feature in one guild. Features with no active entry are free, so nothing
/// changes for a guild until an admin sets a price.
/// </summary>
public class PriceEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The priceable action, shaped <c>{area}:{identifier}</c>, e.g. <c>soundboard:{soundId}</c>.
    /// </summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>
    /// The guild this price applies in. Null means it applies everywhere the feature is used.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>The currency charged. May be a guild currency or a global one.</summary>
    public Guid CurrencyId { get; set; }

    /// <summary>Price in whole units. Always greater than zero.</summary>
    public long Amount { get; set; }

    /// <summary>
    /// Discord role snowflake IDs whose holders pay nothing. Stored as a JSON array.
    /// </summary>
    public List<ulong> ExemptRoleIds { get; set; } = new();

    /// <summary>Whether this price is charged. Inactive entries leave the feature free.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Discord user snowflake ID of whoever last saved this entry.</summary>
    public ulong UpdatedById { get; set; }

    /// <summary>UTC timestamp of the last save.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Navigation to the currency charged.</summary>
    public Currency? Currency { get; set; }
}
