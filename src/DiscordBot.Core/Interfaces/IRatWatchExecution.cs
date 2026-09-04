using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Background-service side of Rat Watch: due/expired scans, voting/finalization execution, and guild settings.
/// </summary>
public interface IRatWatchExecution
{
    /// <summary>
    /// Gets Rat Watches that are due for execution.
    /// Called by the background service to start voting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of watches due for execution.</returns>
    Task<IEnumerable<RatWatch>> GetDueWatchesAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts the voting phase for a Rat Watch.
    /// Called by the background service when scheduled time is reached.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="votingMessageId">Optional Discord message ID of the voting message to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if voting started successfully, false if watch not found or not pending.</returns>
    Task<bool> StartVotingAsync(Guid watchId, ulong? votingMessageId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets Rat Watches where voting has expired and needs finalization.
    /// Called by the background service to finalize votes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of watches with expired voting windows.</returns>
    Task<IEnumerable<RatWatch>> GetExpiredVotingAsync(CancellationToken ct = default);

    /// <summary>
    /// Finalizes voting on a Rat Watch and determines the verdict.
    /// Called by the background service when voting window expires.
    /// Creates a RatRecord if the verdict is guilty.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if finalized successfully, false if watch not found or not voting.</returns>
    Task<bool> FinalizeVotingAsync(Guid watchId, CancellationToken ct = default);

    /// <summary>
    /// Gets the Rat Watch settings for a guild.
    /// Creates default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The guild's Rat Watch settings.</returns>
    Task<GuildRatWatchSettings> GetGuildSettingsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Updates the Rat Watch settings for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="update">Action to update the settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated guild settings.</returns>
    Task<GuildRatWatchSettings> UpdateGuildSettingsAsync(
        ulong guildId,
        Action<GuildRatWatchSettings> update,
        CancellationToken ct = default);
}
