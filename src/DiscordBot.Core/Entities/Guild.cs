namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a Discord guild (server) registered with the bot.
/// </summary>
public class Guild
{
    /// <summary>
    /// Discord guild snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Display name of the guild.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the bot joined this guild.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Whether the bot is currently active in this guild.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional custom command prefix for text commands.
    /// Null uses the default prefix.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// JSON-serialized guild-specific settings.
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Navigation property for command logs in this guild.
    /// </summary>
    public ICollection<CommandLog> CommandLogs { get; set; } = new List<CommandLog>();

    /// <summary>
    /// Navigation property for message logs in this guild.
    /// </summary>
    public ICollection<MessageLog> MessageLogs { get; set; } = new List<MessageLog>();

    /// <summary>
    /// Navigation property for guild members.
    /// </summary>
    public ICollection<GuildMember> Members { get; set; } = new List<GuildMember>();
}
