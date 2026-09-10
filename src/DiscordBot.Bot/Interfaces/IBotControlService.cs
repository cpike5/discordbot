using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Handles the Bot Control tab of the Application Settings page: status/configuration
/// loading plus restart and shutdown operations, including audit logging. Extracted
/// from <c>DiscordBot.Bot.Pages.Admin.SettingsModel</c>.
/// </summary>
public interface IBotControlService
{
    /// <summary>Builds the Bot Control view model from current bot status/configuration.</summary>
    BotControlViewModel LoadViewModel();

    /// <summary>Restarts the bot and audit-logs the action.</summary>
    Task<SettingsSectionResult> RestartAsync(string userId);

    /// <summary>Shuts the bot down and audit-logs the action.</summary>
    Task<SettingsSectionResult> ShutdownAsync(string userId);
}
