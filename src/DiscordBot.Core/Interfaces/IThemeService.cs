using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing themes with user preference hierarchy.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets all active themes available for selection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active theme DTOs.</returns>
    Task<IReadOnlyList<ThemeDto>> GetActiveThemesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a theme by its unique key.
    /// </summary>
    /// <param name="themeKey">The theme key (e.g., "discord-dark").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The theme DTO if found, otherwise null.</returns>
    Task<ThemeDto?> GetThemeByKeyAsync(string themeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective theme for a user following the hierarchy:
    /// user preference > admin default > system default.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current theme with its source.</returns>
    Task<CurrentThemeDto> GetUserThemeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the system default theme.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default theme DTO.</returns>
    Task<ThemeDto> GetDefaultThemeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a user's theme preference.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="themeId">The theme ID to set, or null to clear preference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false if theme not found.</returns>
    Task<bool> SetUserThemeAsync(string userId, int? themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the system default theme.
    /// </summary>
    /// <param name="themeId">The theme ID to set as default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false if theme not found.</returns>
    Task<bool> SetDefaultThemeAsync(int themeId, CancellationToken cancellationToken = default);
}
