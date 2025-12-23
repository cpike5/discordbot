using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Settings page.
/// Organizes settings by category with state for restart requirements and messages.
/// </summary>
public class SettingsViewModel
{
    /// <summary>
    /// Gets or sets the currently active category tab.
    /// </summary>
    public string ActiveCategory { get; set; } = "General";

    /// <summary>
    /// Gets or sets the settings for the General category.
    /// </summary>
    public IReadOnlyList<SettingDto> GeneralSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets the settings for the Logging category.
    /// </summary>
    public IReadOnlyList<SettingDto> LoggingSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets the settings for the Features category.
    /// </summary>
    public IReadOnlyList<SettingDto> FeaturesSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets the settings for the Advanced category.
    /// </summary>
    public IReadOnlyList<SettingDto> AdvancedSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets whether a restart is pending due to settings changes.
    /// </summary>
    public bool IsRestartPending { get; set; }

    /// <summary>
    /// Gets or sets a success message to display to the user.
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets an error message to display to the user.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
