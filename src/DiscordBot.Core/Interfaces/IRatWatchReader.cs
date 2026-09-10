using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Read/query side of Rat Watch: lookups, stats, leaderboards, and status checks.
/// </summary>
public interface IRatWatchReader
{
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

    /// <summary>
    /// Gets recent Rat Watch events across all guilds for activity feeds.
    /// Returns watches ordered by most recent status change timestamp.
    /// </summary>
    /// <param name="limit">Maximum number of watches to return. Defaults to 10.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of recent watches with guild names included.</returns>
    Task<IEnumerable<RatWatchDto>> GetRecentActivityAsync(int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// Checks if a watch has the specified status.
    /// More efficient than loading the full entity when only status verification is needed.
    /// Used by background services to verify watch status before processing.
    /// </summary>
    /// <param name="watchId">The watch ID to check.</param>
    /// <param name="expectedStatus">The expected status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the watch exists and has the expected status, false otherwise.</returns>
    Task<bool> HasStatusAsync(Guid watchId, Enums.RatWatchStatus expectedStatus, CancellationToken ct = default);
}
