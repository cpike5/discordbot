namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an ApplicationUser's access to a specific guild.
/// This entity enables guild-specific authorization, allowing users to manage only guilds they have been granted access to.
/// </summary>
public class UserGuildAccess
{
    /// <summary>
    /// The ApplicationUser ID (ASP.NET Core Identity user).
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the ApplicationUser.
    /// </summary>
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// The Guild ID (Discord snowflake).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Navigation property to the Guild.
    /// </summary>
    public Guild Guild { get; set; } = null!;

    /// <summary>
    /// The user's access level for this guild.
    /// </summary>
    public GuildAccessLevel AccessLevel { get; set; } = GuildAccessLevel.Viewer;

    /// <summary>
    /// When the access was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who granted the access (nullable for system-granted access).
    /// </summary>
    public string? GrantedByUserId { get; set; }
}

/// <summary>
/// Access levels for guild-specific permissions.
/// Higher values indicate higher permission levels.
/// </summary>
public enum GuildAccessLevel
{
    /// <summary>
    /// Read-only access to guild data (dashboards, logs).
    /// </summary>
    Viewer = 0,

    /// <summary>
    /// Can edit guild settings but cannot delete guilds or manage other users.
    /// </summary>
    Moderator = 1,

    /// <summary>
    /// Full administrative access to the guild (CRUD operations, bot control).
    /// </summary>
    Admin = 2,

    /// <summary>
    /// Guild owner with all permissions, typically the Discord server owner.
    /// </summary>
    Owner = 3
}
