using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing application settings with validation and configuration merging.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets all settings for a specific category with metadata.
    /// Merges database values with defaults from appsettings.json.
    /// </summary>
    /// <param name="category">The setting category to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of settings in the category.</returns>
    Task<IReadOnlyList<SettingDto>> GetSettingsByCategoryAsync(
        SettingCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all settings across all categories with metadata.
    /// Merges database values with defaults from appsettings.json.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all settings.</returns>
    Task<IReadOnlyList<SettingDto>> GetAllSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single setting value with type conversion.
    /// Returns database value if exists, otherwise falls back to appsettings default.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The setting value, or default if not found.</returns>
    Task<T?> GetSettingValueAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple settings with validation.
    /// Sets the restart pending flag if any RequiresRestart setting is changed.
    /// </summary>
    /// <param name="updates">The settings to update.</param>
    /// <param name="userId">The ID of the user making the changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success, errors, and restart requirement.</returns>
    Task<SettingsUpdateResultDto> UpdateSettingsAsync(
        SettingsUpdateDto updates,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all settings in a category to their default values by deleting database overrides.
    /// </summary>
    /// <param name="category">The category to reset.</param>
    /// <param name="userId">The ID of the user performing the reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success and restart requirement.</returns>
    Task<SettingsUpdateResultDto> ResetCategoryAsync(
        SettingCategory category,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all settings to their default values by deleting all database overrides.
    /// </summary>
    /// <param name="userId">The ID of the user performing the reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success and restart requirement.</returns>
    Task<SettingsUpdateResultDto> ResetAllAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a restart is pending due to setting changes.
    /// This flag is maintained in memory and cleared when the bot restarts.
    /// </summary>
    bool IsRestartPending { get; }

    /// <summary>
    /// Clears the restart pending flag.
    /// Should be called by the bot hosted service after successful restart.
    /// </summary>
    void ClearRestartPending();

    /// <summary>
    /// Event that is raised when settings are updated.
    /// Subscribers can use this to react to setting changes in real-time.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
}

/// <summary>
/// Event arguments for settings changed events.
/// </summary>
public class SettingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// The keys that were updated.
    /// </summary>
    public IReadOnlyList<string> UpdatedKeys { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The user who made the changes.
    /// </summary>
    public string UserId { get; init; } = string.Empty;
}
