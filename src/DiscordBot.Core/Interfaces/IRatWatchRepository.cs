using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for RatWatch entities with rat-watch-specific operations.
/// </summary>
public interface IRatWatchRepository : IRepository<RatWatch>
{
    /// <summary>
    /// Gets pending Rat Watches that are due for execution.
    /// Returns watches where Status is Pending and ScheduledAt is before the specified time.
    /// </summary>
    /// <param name="beforeTime">UTC time to compare against ScheduledAt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pending watches due for execution.</returns>
    Task<IEnumerable<RatWatch>> GetPendingWatchesAsync(DateTime beforeTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active voting watches where the voting window has expired.
    /// Returns watches where Status is Voting and voting window has ended.
    /// </summary>
    /// <param name="votingEndBefore">UTC time representing when voting should end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of voting watches that need finalization.</returns>
    Task<IEnumerable<RatWatch>> GetActiveVotingAsync(DateTime votingEndBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a Rat Watch by ID with all votes included.
    /// </summary>
    /// <param name="id">Unique identifier of the watch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The watch with votes collection, or null if not found.</returns>
    Task<RatWatch?> GetByIdWithVotesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Rat Watches for a specific guild with pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated watches and the total count.</returns>
    Task<(IEnumerable<RatWatch> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a duplicate watch for the same user and scheduled time.
    /// Used to prevent duplicate watches from being created.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="accusedUserId">Discord user ID of the accused.</param>
    /// <param name="scheduledAt">Scheduled time to check for duplicates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The duplicate watch if found, or null.</returns>
    Task<RatWatch?> FindDuplicateAsync(
        ulong guildId,
        ulong accusedUserId,
        DateTime scheduledAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active watches (Pending or Voting status) for a specific user in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of active watches for the user.</returns>
    Task<int> GetActiveWatchCountForUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there are any active Rat Watches (Pending or Voting status) across all guilds.
    /// Used to determine bot status indicator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if there are any active watches, false otherwise.</returns>
    Task<bool> HasActiveWatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets analytics summary statistics for Rat Watch dashboard.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter by. Null for all guilds.</param>
    /// <param name="startDate">Optional start date filter. Null for no start date limit.</param>
    /// <param name="endDate">Optional end date filter. Null for no end date limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analytics summary with counts, rates, and averages.</returns>
    Task<RatWatchAnalyticsSummaryDto> GetAnalyticsSummaryAsync(
        ulong? guildId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time series data for trend charts.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter by. Null for all guilds.</param>
    /// <param name="startDate">Start date for the time series.</param>
    /// <param name="endDate">End date for the time series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of time series data points ordered by date.</returns>
    Task<IEnumerable<RatWatchTimeSeriesDto>> GetTimeSeriesAsync(
        ulong? guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity heatmap data (day of week + hour).
    /// </summary>
    /// <param name="guildId">Guild ID to filter by.</param>
    /// <param name="startDate">Start date for the heatmap data.</param>
    /// <param name="endDate">End date for the heatmap data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of heatmap data points with counts per day/hour cell.</returns>
    Task<IEnumerable<ActivityHeatmapDto>> GetActivityHeatmapAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Rat Watches for a guild with advanced filtering and pagination.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="filter">The filter parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of filtered items and total count.</returns>
    Task<(IEnumerable<RatWatch> Items, int TotalCount)> GetFilteredByGuildAsync(
        ulong guildId,
        RatWatchIncidentFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent Rat Watch events across all guilds ordered by most recent activity timestamp.
    /// The activity timestamp is determined by status: CreatedAt for Pending, VotingStartedAt for Voting,
    /// VotingEndedAt for Guilty/NotGuilty, ClearedAt for ClearedEarly.
    /// </summary>
    /// <param name="limit">Maximum number of watches to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of recent watches with Guild navigation property included.</returns>
    Task<IEnumerable<RatWatch>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
