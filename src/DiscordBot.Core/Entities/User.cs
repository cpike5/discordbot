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
    /// Navigation property for command logs by this user.
    /// </summary>
    public ICollection<CommandLog> CommandLogs { get; set; } = new List<CommandLog>();
}
