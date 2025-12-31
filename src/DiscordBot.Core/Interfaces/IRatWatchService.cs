using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing Rat Watch accountability trackers.
/// Handles watch creation, voting, execution, and statistics.
/// </summary>
public interface IRatWatchService
{
    /// <summary>
    /// Creates a new Rat Watch.
    /// </summary>
    /// <param name="dto">The watch creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created watch as a DTO.</returns>
    Task<RatWatchDto> CreateWatchAsync(RatWatchCreateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Gets a Rat Watch by ID.
    /// </summary>
    /// <param name="id">Unique identifier of the watch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The watch DTO, or null if not found.</returns>
    Task<RatWatchDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets Rat Watches for a specific guild with pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the paginated watches and the total count.</returns>
    Task<(IEnumerable<RatWatchDto> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels a pending Rat Watch.
    /// </summary>
    /// <param name="id">Unique identifier of the watch.</param>
    /// <param name="reason">Reason for cancellation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if cancelled successfully, false if not found or already completed.</returns>
    Task<bool> CancelWatchAsync(Guid id, string reason, CancellationToken ct = default);

    /// <summary>
    /// Clears a watch when the accused checks in early.
    /// Only allowed for the accused user.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="userId">Discord user ID of the user checking in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if cleared successfully, false if not found, not the accused, or already completed.</returns>
    Task<bool> ClearWatchAsync(Guid watchId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Casts or changes a vote on a Rat Watch.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="voterId">Discord user ID of the voter.</param>
    /// <param name="isGuilty">True for guilty vote, false for not guilty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if vote was cast successfully, false if watch not found or not in voting status.</returns>
    Task<bool> CastVoteAsync(Guid watchId, ulong voterId, bool isGuilty, CancellationToken ct = default);

    /// <summary>
    /// Gets the current vote tally for a Rat Watch.
    /// </summary>
    /// <param name="watchId">Unique identifier of the watch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing guilty vote count and not guilty vote count.</returns>
    Task<(int Guilty, int NotGuilty)> GetVoteTallyAsync(Guid watchId, CancellationToken ct = default);

    /// <summary>
    /// Gets statistics for a user in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User statistics including guilty count and recent records.</returns>
    Task<RatStatsDto> GetUserStatsAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the leaderboard of users with the most guilty verdicts in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of leaderboard entries.</returns>
    Task<IReadOnlyList<RatLeaderboardEntryDto>> GetLeaderboardAsync(
        ulong guildId,
        int limit = 10,
        CancellationToken ct = default);

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

    /// <summary>
    /// Parses a schedule time string and converts it to UTC.
    /// Supports relative formats (e.g., "10m", "2h", "1h30m") and absolute formats (e.g., "10pm", "22:00").
    /// </summary>
    /// <param name="input">The time string to parse.</param>
    /// <param name="timezone">IANA timezone identifier for absolute time parsing.</param>
    /// <returns>The parsed UTC DateTime, or null if parsing fails.</returns>
    DateTime? ParseScheduleTime(string input, string timezone);

    /// <summary>
    /// Checks if there are any active Rat Watches (Pending or Voting status) across all guilds.
    /// Used to determine whether the bot should show a "Rat Watch" status indicator.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if there are any active watches, false otherwise.</returns>
    Task<bool> HasActiveWatchesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets Rat Watches for a guild with advanced filtering and pagination.
    /// </summary>
    Task<(IEnumerable<RatWatchDto> Items, int TotalCount)> GetFilteredByGuildAsync(
        ulong guildId,
        RatWatchIncidentFilterDto filter,
        CancellationToken ct = default);
}
