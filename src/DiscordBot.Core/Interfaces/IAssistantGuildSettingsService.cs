using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing assistant guild settings.
/// Handles configuration, enable/disable operations, and default settings creation.
/// </summary>
public interface IAssistantGuildSettingsService
{
    /// <summary>
    /// Gets guild settings, creating default settings if none exist.
    /// Ensures every guild has a settings entry before performing operations.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild's assistant settings.</returns>
    Task<AssistantGuildSettings> GetOrCreateSettingsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates guild settings.
    /// </summary>
    /// <param name="settings">The updated settings entity to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSettingsAsync(
        AssistantGuildSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables the assistant for a guild.
    /// Creates default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnableAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables the assistant for a guild.
    /// Settings are preserved for future re-enablement.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisableAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the assistant is enabled for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if enabled globally and for the guild.</returns>
    Task<bool> IsEnabledAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a channel is allowed for the assistant in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="channelId">Discord channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the channel is allowed (or no restrictions are set).</returns>
    Task<bool> IsChannelAllowedAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the rate limit for a guild (guild override or global default).
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rate limit value.</returns>
    Task<int> GetRateLimitAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);
}
