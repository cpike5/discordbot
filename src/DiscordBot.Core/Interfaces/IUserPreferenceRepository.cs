using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserPreference entities with preference-specific operations.
/// </summary>
public interface IUserPreferenceRepository : IRepository<UserPreference>
{
    /// <summary>
    /// Gets a single preference by user, guild, and key.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="key">Preference key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preference if found, null otherwise.</returns>
    Task<UserPreference?> GetAsync(ulong userId, ulong guildId, string key, CancellationToken ct = default);

    /// <summary>
    /// Gets all preferences for a user within a guild.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of user preferences for the guild.</returns>
    Task<IReadOnlyList<UserPreference>> GetAllAsync(ulong userId, ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Sets (creates or updates) a preference for a user within a guild.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="key">Preference key.</param>
    /// <param name="value">Preference value.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync(ulong userId, ulong guildId, string key, string value, CancellationToken ct = default);

    /// <summary>
    /// Deletes a preference for a user within a guild.
    /// No-op if the preference does not exist.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="key">Preference key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(ulong userId, ulong guildId, string key, CancellationToken ct = default);
}
