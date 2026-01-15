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
}
