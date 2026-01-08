namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a Discord user known to the bot.
/// </summary>
public class User
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Discord username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Discord discriminator (legacy, may be "0" for new usernames).
    /// </summary>
    public string Discriminator { get; set; } = "0";

    /// <summary>
    /// Timestamp when the user was first seen by the bot.
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// Timestamp of the user's most recent interaction.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Timestamp when the Discord account was created.
    /// Extracted from the Discord snowflake ID.
    /// </summary>
    public DateTime? AccountCreatedAt { get; set; }

    /// <summary>
    /// Discord avatar hash for constructing avatar URLs.
    /// Avatar URL pattern: https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png
    /// </summary>
    public string? AvatarHash { get; set; }

    /// <summary>
    /// Discord global display name (separate from per-guild nicknames).
    /// </summary>
    public string? GlobalDisplayName { get; set; }

    /// <summary>
    /// Navigation property for command logs by this user.
    /// </summary>
    public ICollection<CommandLog> CommandLogs { get; set; } = new List<CommandLog>();

    /// <summary>
    /// Navigation property for message logs by this user.
    /// </summary>
    public ICollection<MessageLog> MessageLogs { get; set; } = new List<MessageLog>();

    /// <summary>
    /// Navigation property for guild memberships.
    /// </summary>
    public ICollection<GuildMember> GuildMemberships { get; set; } = new List<GuildMember>();

    /// <summary>
    /// Navigation property for anonymous activity events by this user.
    /// </summary>
    public ICollection<UserActivityEvent> UserActivityEvents { get; set; } = new List<UserActivityEvent>();
}
