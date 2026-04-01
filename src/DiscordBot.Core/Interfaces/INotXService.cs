using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for orchestrating not-X tweet preview operations.
/// </summary>
public interface INotXService
{
    /// <summary>
    /// Processes a tweet URL, fetching its content and posting a Discord embed to the appropriate channel.
    /// </summary>
    /// <param name="guildId">Discord guild ID where the tweet link was posted.</param>
    /// <param name="channelId">Discord channel ID where the tweet link was posted.</param>
    /// <param name="sourceMessageId">Discord message ID of the original message containing the tweet link.</param>
    /// <param name="tweetUrl">The full X/Twitter URL of the tweet.</param>
    /// <param name="ignoreSettingsGate">
    /// When true, bypasses the IsEnabled and SensitiveOnly checks.
    /// Used for manual context menu invocations. Output channel routing is still respected.
    /// </param>
    /// <returns>True if an embed was successfully posted; false otherwise.</returns>
    Task<bool> ProcessTweetAsync(
        ulong guildId,
        ulong channelId,
        ulong sourceMessageId,
        string tweetUrl,
        bool ignoreSettingsGate = false);

    /// <summary>
    /// Gets the not-X settings for a guild, creating default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <returns>The guild's not-X settings.</returns>
    Task<NotXGuildSettings> GetOrCreateSettingsAsync(ulong guildId);

    /// <summary>
    /// Persists updated not-X settings for a guild.
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    Task UpdateSettingsAsync(NotXGuildSettings settings);
}
