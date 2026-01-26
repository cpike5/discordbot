using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.ViewModels.Portal;

/// <summary>
/// View model for the shared portal header component.
/// </summary>
public class PortalHeaderViewModel
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the bot is online.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets or sets the active tab ("soundboard" or "tts").
    /// </summary>
    public string ActiveTab { get; set; } = "soundboard";

    /// <summary>
    /// Gets or sets the TabPanel view model for navigation tabs.
    /// </summary>
    public TabPanelViewModel? TabsViewModel { get; set; }
}
