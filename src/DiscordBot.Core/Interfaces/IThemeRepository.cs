using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing theme persistence.
/// </summary>
public interface IThemeRepository
{
    /// <summary>
    /// Gets a theme by its unique key.
    /// </summary>
    /// <param name="themeKey">The theme key (e.g., "discord-dark").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The theme if found, otherwise null.</returns>
    Task<Theme?> GetByKeyAsync(string themeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a theme by its ID.
    /// </summary>
    /// <param name="id">The theme ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The theme if found, otherwise null.</returns>
    Task<Theme?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active themes available for selection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active themes.</returns>
    Task<IReadOnlyList<Theme>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing theme.
    /// </summary>
    /// <param name="theme">The theme to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Theme theme, CancellationToken cancellationToken = default);
}
