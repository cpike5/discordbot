namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object containing full welcome configuration information for a guild.
/// </summary>
public class WelcomeConfigurationDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets whether welcome messages are enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where welcome messages will be sent.
    /// Null if no channel is configured.
    /// </summary>
    public ulong? WelcomeChannelId { get; set; }

    /// <summary>
    /// Gets or sets the welcome message template to send to new members.
    /// Supports placeholders like {user}, {guild}, {memberCount}, etc.
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to include the user's avatar in the welcome message.
    /// </summary>
    public bool IncludeAvatar { get; set; }

    /// <summary>
    /// Gets or sets whether to send the welcome message as an embed (rich message).
    /// If false, sends as a plain text message.
    /// </summary>
    public bool UseEmbed { get; set; }

    /// <summary>
    /// Gets or sets the hex color code for the embed (e.g., "#5865F2").
    /// Only used when UseEmbed is true. Null uses Discord's default embed color.
    /// </summary>
    public string? EmbedColor { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
