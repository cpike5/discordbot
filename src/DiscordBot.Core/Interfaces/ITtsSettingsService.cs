using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing guild TTS settings.
/// Handles configuration retrieval and updates for TTS features.
/// </summary>
public interface ITtsSettingsService
{
    /// <summary>
    /// Gets the TTS settings for a guild, creating defaults if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The guild's TTS settings entity.</returns>
    Task<GuildTtsSettings> GetOrCreateSettingsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Updates the TTS settings for a guild.
    /// </summary>
    /// <param name="settings">The settings entity with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSettingsAsync(GuildTtsSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Checks if TTS is enabled for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if TTS is enabled, false otherwise.</returns>
    Task<bool> IsTtsEnabledAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a user has exceeded the rate limit for TTS messages.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user is rate limited, false if they can send more messages.</returns>
    Task<bool> IsUserRateLimitedAsync(ulong guildId, ulong userId, CancellationToken ct = default);
}
