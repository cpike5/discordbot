namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for updating welcome configuration settings.
/// All properties are nullable to support partial updates.
/// </summary>
public class WelcomeConfigurationUpdateDto
{
    /// <summary>
    /// Gets or sets whether welcome messages are enabled for this guild. Null means no change.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where welcome messages will be sent. Null means no change.
    /// </summary>
    public ulong? WelcomeChannelId { get; set; }

    /// <summary>
    /// Gets or sets the welcome message template to send to new members. Null means no change.
    /// Supports placeholders like {user}, {guild}, {memberCount}, etc.
    /// </summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets whether to include the user's avatar in the welcome message. Null means no change.
    /// </summary>
    public bool? IncludeAvatar { get; set; }

    /// <summary>
    /// Gets or sets whether to send the welcome message as an embed (rich message). Null means no change.
    /// If false, sends as a plain text message.
    /// </summary>
    public bool? UseEmbed { get; set; }

    /// <summary>
    /// Gets or sets the hex color code for the embed (e.g., "#5865F2"). Null means no change.
    /// Only used when UseEmbed is true. Null uses Discord's default embed color.
    /// </summary>
    public string? EmbedColor { get; set; }
}
