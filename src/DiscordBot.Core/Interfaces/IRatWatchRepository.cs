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
}
