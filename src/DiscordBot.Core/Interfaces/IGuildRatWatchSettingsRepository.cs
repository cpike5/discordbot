using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for GuildRatWatchSettings entities with settings-specific operations.
/// </summary>
public interface IGuildRatWatchSettingsRepository : IRepository<GuildRatWatchSettings>
{
    /// <summary>
    /// Gets the Rat Watch settings for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The settings for the guild, or null if not found.</returns>
    Task<GuildRatWatchSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Rat Watch settings for a guild, creating default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The settings for the guild (existing or newly created with defaults).</returns>
    Task<GuildRatWatchSettings> GetOrCreateAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the voting duration for specific guilds.
    /// Returns a dictionary mapping guild ID to voting duration in minutes.
    /// Only includes guilds that have custom settings configured.
    /// </summary>
    /// <param name="guildIds">The guild IDs to get voting durations for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping guild ID to voting duration in minutes.</returns>
    Task<Dictionary<ulong, int>> GetVotingDurationsForGuildsAsync(
        IEnumerable<ulong> guildIds,
        CancellationToken cancellationToken = default);
}
