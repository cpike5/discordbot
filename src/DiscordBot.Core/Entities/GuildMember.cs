namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents the many-to-many relationship between users and guilds with additional metadata.
/// </summary>
public class GuildMember
{
    /// <summary>
    /// Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Timestamp when the user joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Guild-specific nickname for this member, if set.
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// JSON array of role IDs assigned to this member.
    /// Cached for quick access without additional API calls.
    /// </summary>
    public string? CachedRolesJson { get; set; }

    /// <summary>
    /// Timestamp of the member's most recent activity in the guild.
    /// </summary>
    public DateTime? LastActiveAt { get; set; }

    /// <summary>
    /// Timestamp when the Discord data was last synchronized.
    /// </summary>
    public DateTime LastCachedAt { get; set; }

    /// <summary>
    /// Whether the member is currently active in the guild.
    /// False when the member has left (soft delete).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property to the guild.
    /// </summary>
    public Guild Guild { get; set; } = null!;

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public User User { get; set; } = null!;
}
