using System.Text.Json.Serialization;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// The body of a mint request from the portal. The currency comes from the route and the actor
/// from the signed-in user, so only the recipient, the amount and the reason are posted.
/// </summary>
public record MintRequestDto
{
    /// <summary>Discord user snowflake ID receiving the units, as a string so JavaScript keeps every digit.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong UserId { get; init; }

    /// <summary>Units to create. Must be greater than zero.</summary>
    public long Amount { get; init; }

    /// <summary>Why the units were created. Required.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// The body of a fine issued from the portal.
/// </summary>
public record FineRequestDto
{
    /// <summary>Discord user snowflake ID being fined, as a string so JavaScript keeps every digit.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong UserId { get; init; }

    /// <summary>Units to take. Clamped at zero, or at the currency's debt floor.</summary>
    public long Amount { get; init; }

    /// <summary>Why they were fined. Required, and it is what the mod case carries.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Whether to open a <c>Note</c> moderation case alongside the fine.</summary>
    public bool OpenCase { get; init; }
}

/// <summary>
/// The body of an adjustment, the only way to correct a ledger mistake.
/// </summary>
public record AdjustRequestDto
{
    /// <summary>Signed amount. May move either direction, but not zero.</summary>
    public long Amount { get; init; }

    /// <summary>Why the correction was made. Required.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// The body of a mint authority grant.
/// </summary>
public record MintAuthorityGrantRequestDto
{
    /// <summary>The kind of principal being granted.</summary>
    public MintPrincipalType PrincipalType { get; init; }

    /// <summary>
    /// Discord user or role snowflake ID, as a string so JavaScript keeps every digit. Null for
    /// the system principal.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong? PrincipalId { get; init; }
}

/// <summary>
/// The body of a price save. The feature key and the guild come from the route.
/// </summary>
public record PriceSaveRequestDto
{
    /// <summary>The currency to charge.</summary>
    public Guid CurrencyId { get; init; }

    /// <summary>Price in whole units. Must be greater than zero.</summary>
    public long Amount { get; init; }

    /// <summary>
    /// Discord role snowflake IDs whose holders pay nothing, as strings so JavaScript keeps every
    /// digit.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public IReadOnlyList<ulong> ExemptRoleIds { get; init; } = Array.Empty<ulong>();

    /// <summary>Whether the price is charged. False leaves the feature free without losing the entry.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// One wallet in a currency's holder list, with the display name the portal renders. The ledger
/// stores snowflakes; only this view needs a name against them.
/// </summary>
public record WalletHolderDto
{
    /// <summary>Wallet id.</summary>
    public Guid WalletId { get; init; }

    /// <summary>Discord user snowflake ID of the holder, as a string so JavaScript keeps every digit.</summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong UserId { get; init; }

    /// <summary>Discord username, or a fallback when the user cannot be resolved.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Discord avatar URL, when there is one.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Current balance.</summary>
    public long Balance { get; init; }

    /// <summary>Whether the holder is in debt and therefore locked out of priced features.</summary>
    public bool IsInDebt => Balance < 0;
}
