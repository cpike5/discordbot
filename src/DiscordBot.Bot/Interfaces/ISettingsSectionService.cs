using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Handles loading and saving the settings/command-module sections of the Application
/// Settings page (General, Features, Advanced, Commands), including audit logging.
/// Extracted from <c>DiscordBot.Bot.Pages.Admin.SettingsModel</c> so the page model
/// stays a thin request/response shell.
/// </summary>
public interface ISettingsSectionService
{
    /// <summary>
    /// Loads the settings view model for the given active category, including
    /// settings for all categories and command modules grouped by category.
    /// </summary>
    Task<SettingsViewModel> LoadViewModelAsync(string activeCategory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the settings for one category (or all, when the update covers every key).
    /// </summary>
    Task<SettingsSectionResult> SaveCategoryAsync(string category, Dictionary<string, string> formSettings, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all settings across all categories.
    /// </summary>
    Task<SettingsSectionResult> SaveAllAsync(Dictionary<string, string> formSettings, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets one category to its default values.
    /// </summary>
    Task<SettingsSectionResult> ResetCategoryAsync(string category, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all settings to their default values.
    /// </summary>
    Task<SettingsSectionResult> ResetAllAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves command module enabled/disabled states.
    /// </summary>
    Task<SettingsSectionResult> SaveCommandModulesAsync(Dictionary<string, bool> commandModules, string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a settings save/reset operation, shaped for direct use in a page model's
/// <see cref="Microsoft.AspNetCore.Mvc.JsonResult"/> response.
/// </summary>
public sealed record SettingsSectionResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool RestartRequired { get; init; }

    /// <summary>Optional extra field name for a themed save (e.g. the new theme's display name).</summary>
    public string? ThemeName { get; init; }

    /// <summary>HTTP status code the page model should use for a failed response (200 on success).</summary>
    public int StatusCode { get; init; } = 200;
}
