using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Application user entity extending ASP.NET Core Identity.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Discord user ID (snowflake). Nullable for users who haven't linked Discord.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// Discord username (e.g., "username#1234" or new format "username").
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// Discord avatar URL for displaying user profile picture.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>
    /// Display name shown in the admin UI. Defaults to Discord username or email.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Indicates whether the user account is active. Inactive users cannot log in.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time when the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time of the user's last login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
