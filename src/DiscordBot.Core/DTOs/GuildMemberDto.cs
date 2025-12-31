namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object representing a guild member with user information and roles.
/// </summary>
public class GuildMemberDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord discriminator (legacy, may be "0" for new usernames).
    /// </summary>
    public string Discriminator { get; set; } = "0";

    /// <summary>
    /// Gets or sets the Discord global display name (separate from per-guild nicknames).
    /// </summary>
    public string? GlobalDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the guild-specific nickname for this member, if set.
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// Gets or sets the Discord avatar hash for constructing avatar URLs.
    /// Avatar URL pattern: https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png
    /// </summary>
    public string? AvatarHash { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the member's most recent activity in the guild.
    /// </summary>
    public DateTime? LastActiveAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the Discord account was created.
    /// </summary>
    public DateTime? AccountCreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs assigned to this member.
    /// </summary>
    public List<ulong> RoleIds { get; set; } = new List<ulong>();

    /// <summary>
    /// Gets or sets the roles assigned to this member with full role information.
    /// </summary>
    public List<GuildRoleDto> Roles { get; set; } = new List<GuildRoleDto>();

    /// <summary>
    /// Gets or sets whether the member is currently active in the guild.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the member data was last synchronized.
    /// </summary>
    public DateTime LastCachedAt { get; set; }

    /// <summary>
    /// Gets the effective display name (nickname if set, otherwise global display name or username).
    /// </summary>
    public string DisplayName => Nickname ?? GlobalDisplayName ?? Username;
}
