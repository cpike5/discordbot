using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Bot Control Panel page.
/// </summary>
public class BotControlViewModel
{
    /// <summary>
    /// Gets or sets the current bot status.
    /// </summary>
    public BotStatusViewModel Status { get; set; } = null!;

    /// <summary>
    /// Gets or sets the bot configuration (with masked sensitive values).
    /// </summary>
    public BotConfigurationDto Configuration { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether restart is available.
    /// </summary>
    public bool CanRestart { get; set; } = true;

    /// <summary>
    /// Gets or sets whether shutdown is available.
    /// </summary>
    public bool CanShutdown { get; set; } = true;
}
