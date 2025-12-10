namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for Discord user information retrieved from the Discord API.
/// </summary>
public class DiscordUserInfoDto
{
    /// <summary>
    /// Discord user ID (snowflake).
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Discord username (without discriminator in new username system).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Discord global display name (if set by the user).
    /// </summary>
    public string? GlobalName { get; set; }

    /// <summary>
    /// Avatar hash from Discord. Used to construct avatar URL.
    /// </summary>
    public string? AvatarHash { get; set; }

    /// <summary>
    /// User's email address (requires 'email' scope).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Whether the email address is verified (requires 'email' scope).
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// Full URL to the user's avatar image.
    /// Returns default Discord avatar if no custom avatar is set.
    /// </summary>
    public string AvatarUrl => string.IsNullOrEmpty(AvatarHash)
        ? $"https://cdn.discordapp.com/embed/avatars/{Id % 5}.png"
        : $"https://cdn.discordapp.com/avatars/{Id}/{AvatarHash}.png";
}
