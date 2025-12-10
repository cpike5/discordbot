namespace DiscordBot.Core.Entities;

/// <summary>
/// Stores encrypted Discord OAuth tokens for an ApplicationUser.
/// Tokens are used for Discord API access (guild membership verification, etc.)
/// </summary>
public class DiscordOAuthToken
{
    /// <summary>
    /// Primary key for the OAuth token record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to ApplicationUser. One-to-one relationship.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the ApplicationUser who owns this token.
    /// </summary>
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// Encrypted OAuth access token. Never stored in plain text.
    /// </summary>
    public string EncryptedAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted OAuth refresh token. Never stored in plain text.
    /// </summary>
    public string EncryptedRefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the access token expires. Used to trigger refresh before expiration.
    /// </summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// Space-separated OAuth scopes granted (e.g., "identify email guilds").
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// When the token was first created/stored.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the token was last refreshed. Updated on each refresh operation.
    /// </summary>
    public DateTime LastRefreshedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Discord user ID (snowflake) associated with this token.
    /// Used for validation and Discord API calls.
    /// </summary>
    public ulong DiscordUserId { get; set; }
}
