using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for AssistantGuildSettings entities.
/// Provides data access operations for assistant configuration per guild.
/// </summary>
public interface IAssistantGuildSettingsRepository : IRepository<AssistantGuildSettings>
{
    /// <summary>
    /// Gets settings for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID to retrieve settings for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild's assistant settings, or null if not found.</returns>
    Task<AssistantGuildSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all guilds with assistant enabled.
    /// Useful for monitoring and administrative operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of settings for all guilds where IsEnabled is true.</returns>
    Task<IEnumerable<AssistantGuildSettings>> GetEnabledGuildsAsync(
        CancellationToken cancellationToken = default);
}
