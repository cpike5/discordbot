using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the welcome configuration page.
/// </summary>
public class WelcomeConfigurationViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name (display only).
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (display only).
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets whether welcome messages are enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where welcome messages will be sent.
    /// </summary>
    public ulong? WelcomeChannelId { get; set; }

    /// <summary>
    /// Gets or sets the welcome message template to send to new members.
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to include the user's avatar in the welcome message.
    /// </summary>
    public bool IncludeAvatar { get; set; }

    /// <summary>
    /// Gets or sets whether to send the welcome message as an embed (rich message).
    /// </summary>
    public bool UseEmbed { get; set; }

    /// <summary>
    /// Gets or sets the hex color code for the embed (e.g., "#5865F2").
    /// </summary>
    public string? EmbedColor { get; set; }

    /// <summary>
    /// Gets or sets the list of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// Creates a WelcomeConfigurationViewModel from a WelcomeConfigurationDto.
    /// </summary>
    /// <param name="dto">The welcome configuration DTO.</param>
    /// <param name="guildName">The guild name.</param>
    /// <param name="guildIconUrl">The guild icon URL.</param>
    /// <param name="availableChannels">The list of available text channels.</param>
    /// <returns>A new WelcomeConfigurationViewModel instance.</returns>
    public static WelcomeConfigurationViewModel FromDto(
        WelcomeConfigurationDto dto,
        string guildName,
        string? guildIconUrl,
        List<ChannelSelectItem> availableChannels)
    {
        return new WelcomeConfigurationViewModel
        {
            GuildId = dto.GuildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            IsEnabled = dto.IsEnabled,
            WelcomeChannelId = dto.WelcomeChannelId,
            WelcomeMessage = dto.WelcomeMessage,
            IncludeAvatar = dto.IncludeAvatar,
            UseEmbed = dto.UseEmbed,
            EmbedColor = dto.EmbedColor,
            AvailableChannels = availableChannels
        };
    }
}

/// <summary>
/// Represents a selectable channel item for dropdown lists.
/// </summary>
public class ChannelSelectItem
{
    /// <summary>
    /// Gets or sets the channel's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel position (for sorting).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the channel type for display purposes.
    /// </summary>
    public ChannelDisplayType Type { get; set; } = ChannelDisplayType.Text;
}

/// <summary>
/// Channel types for display purposes in the admin UI.
/// </summary>
public enum ChannelDisplayType
{
    /// <summary>Regular text channel.</summary>
    Text,

    /// <summary>Voice channel (with text chat capability).</summary>
    Voice,

    /// <summary>Announcement/news channel.</summary>
    Announcement,

    /// <summary>Stage channel.</summary>
    Stage,

    /// <summary>Forum channel.</summary>
    Forum
}
