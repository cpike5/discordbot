using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing application settings persistence.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Gets a setting by its key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The setting if found, otherwise null.</returns>
    Task<ApplicationSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all settings for a specific category.
    /// </summary>
    /// <param name="category">The setting category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of settings in the category.</returns>
    Task<IReadOnlyList<ApplicationSetting>> GetByCategoryAsync(
        SettingCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all settings across all categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all settings.</returns>
    Task<IReadOnlyList<ApplicationSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a setting.
    /// </summary>
    /// <param name="setting">The setting to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(ApplicationSetting setting, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a setting by its key.
    /// </summary>
    /// <param name="key">The setting key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all settings in a specific category.
    /// </summary>
    /// <param name="category">The category to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteByCategoryAsync(SettingCategory category, CancellationToken cancellationToken = default);
}
