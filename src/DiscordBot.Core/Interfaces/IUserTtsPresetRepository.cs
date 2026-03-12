using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserTtsPreset entities with preset-specific operations.
/// </summary>
public interface IUserTtsPresetRepository : IRepository<UserTtsPreset>
{
    /// <summary>
    /// Gets all presets for a specific user, ordered by creation date.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of the user's presets.</returns>
    Task<IReadOnlyList<UserTtsPreset>> GetByUserIdAsync(
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific preset by its ID.
    /// </summary>
    /// <param name="id">Preset unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preset if found, null otherwise.</returns>
    Task<UserTtsPreset?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of presets for a specific user.
    /// Used to enforce the maximum preset limit.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of presets the user has.</returns>
    Task<int> GetCountByUserIdAsync(
        ulong userId,
        CancellationToken cancellationToken = default);
}
