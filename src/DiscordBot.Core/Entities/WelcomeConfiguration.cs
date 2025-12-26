namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents per-guild welcome message configuration.
/// Controls how the bot greets new members when they join a Discord guild.
/// </summary>
public class WelcomeConfiguration
{
    /// <summary>
    /// Discord guild snowflake ID.
    /// This is the primary key.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Whether welcome messages are enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// ID of the channel where welcome messages will be sent.
    /// Null if no channel is configured.
    /// </summary>
    public ulong? WelcomeChannelId { get; set; }

    /// <summary>
    /// The welcome message template to send to new members.
    /// Supports placeholders like {user}, {guild}, {memberCount}, etc.
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include the user's avatar in the welcome message.
    /// </summary>
    public bool IncludeAvatar { get; set; } = true;

    /// <summary>
    /// Whether to send the welcome message as an embed (rich message).
    /// If false, sends as a plain text message.
    /// </summary>
    public bool UseEmbed { get; set; } = true;

    /// <summary>
    /// Hex color code for the embed (e.g., "#5865F2").
    /// Only used when UseEmbed is true. Null uses Discord's default embed color.
    /// </summary>
    public string? EmbedColor { get; set; }

    /// <summary>
    /// Timestamp when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this configuration belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
