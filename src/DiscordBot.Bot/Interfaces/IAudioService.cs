using Discord.Audio;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Service interface for voice channel connection management and audio playback.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Joins a voice channel in the specified guild.
    /// If already connected to a different channel in the same guild, disconnects first.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="voiceChannelId">The Discord voice channel snowflake ID to join.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audio client for the connection, or null if the guild or channel was not found.</returns>
    Task<IAudioClient?> JoinChannelAsync(ulong guildId, ulong voiceChannelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves the voice channel the bot is currently connected to in the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the bot was connected and successfully disconnected, false if not connected.</returns>
    Task<bool> LeaveChannelAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the audio client for the specified guild if connected.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <returns>The audio client if connected, null otherwise.</returns>
    IAudioClient? GetAudioClient(ulong guildId);

    /// <summary>
    /// Checks if the bot is currently connected to a voice channel in the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <returns>True if connected to a voice channel, false otherwise.</returns>
    bool IsConnected(ulong guildId);

    /// <summary>
    /// Gets the voice channel ID the bot is currently connected to in the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <returns>The voice channel ID if connected, null otherwise.</returns>
    ulong? GetConnectedChannelId(ulong guildId);

    /// <summary>
    /// Disconnects from all voice channels across all guilds.
    /// Called during bot shutdown to clean up connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last activity timestamp for a guild's voice connection.
    /// Called when audio is played to reset the auto-leave timer.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    void UpdateLastActivity(ulong guildId);
}
