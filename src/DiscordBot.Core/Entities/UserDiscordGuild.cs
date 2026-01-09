namespace DiscordBot.Core.Entities;

/// <summary>
/// Stores Discord guild memberships captured during OAuth authentication.
/// This provides a local record of which guilds a user belongs to, enabling
/// guild-based access control without requiring real-time Discord API calls.
/// </summary>
public class UserDiscordGuild
{
    /// <summary>
    /// Primary key for the user-guild membership record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to ApplicationUser. The user who has membership in the guild.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the ApplicationUser.
    /// </summary>
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// Discord guild ID (snowflake).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Guild name at the time membership was recorded.
    /// Stored for display purposes; may become stale if guild is renamed.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Guild icon hash at the time membership was recorded.
    /// Used to construct icon URL; may become stale if icon changes.
    /// </summary>
    public string? GuildIconHash { get; set; }

    /// <summary>
    /// Whether the user is the owner of this guild.
    /// </summary>
    public bool IsOwner { get; set; }

    /// <summary>
    /// Bitwise permission flags for the user in this guild.
    /// </summary>
    public long Permissions { get; set; }

    /// <summary>
    /// When this membership record was first captured (during OAuth).
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this membership record was last updated (on subsequent OAuth logins).
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Full URL to the guild's icon image.
    /// Null if the guild has no custom icon.
    /// </summary>
    public string? GuildIconUrl => string.IsNullOrEmpty(GuildIconHash)
        ? null
        : $"https://cdn.discordapp.com/icons/{GuildId}/{GuildIconHash}.png";
}
