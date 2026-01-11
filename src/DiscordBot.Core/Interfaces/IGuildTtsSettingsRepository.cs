using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for GuildTtsSettings entities with settings-specific operations.
/// </summary>
public interface IGuildTtsSettingsRepository : IRepository<GuildTtsSettings>
{
    /// <summary>
    /// Gets the TTS settings for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings for the guild, or null if not found.</returns>
    Task<GuildTtsSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the TTS settings for a guild, creating default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings for the guild (existing or newly created with defaults).</returns>
    Task<GuildTtsSettings> GetOrCreateAsync(
        ulong guildId,
        CancellationToken ct = default);
}
