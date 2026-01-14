using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserActivityEvent entities.
/// Provides query methods for consent-free analytics.
/// </summary>
public interface IUserActivityEventRepository : IRepository<UserActivityEvent>
{
    /// <summary>
    /// Gets activity events for a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID to query.</param>
    /// <param name="since">Start of time range (inclusive).</param>
    /// <param name="until">End of time range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of activity events.</returns>
    Task<IEnumerable<UserActivityEvent>> GetByGuildAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity events for a user within a time range.
    /// </summary>
    /// <param name="userId">The user ID to query.</param>
    /// <param name="since">Start of time range (inclusive).</param>
    /// <param name="until">End of time range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of activity events.</returns>
    Task<IEnumerable<UserActivityEvent>> GetByUserAsync(
        ulong userId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets event counts grouped by event type for a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID to query.</param>
    /// <param name="since">Start of time range (inclusive).</param>
    /// <param name="until">End of time range (inclusive).</param>
    /// <param name="eventType">Optional filter for specific event type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping event type to count.</returns>
    Task<Dictionary<ActivityEventType, long>> GetEventCountsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        ActivityEventType? eventType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes events older than the specified cutoff date in batches.
    /// Used for retention cleanup.
    /// </summary>
    /// <param name="cutoff">Events logged before this date will be deleted.</param>
    /// <param name="batchSize">Maximum number of events to delete in one operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of events deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all activity events for a specific user.
    /// Used for GDPR data purge requests.
    /// </summary>
    /// <param name="userId">The user ID whose events should be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of events deleted.</returns>
    Task<int> DeleteByUserIdAsync(
        ulong userId,
        CancellationToken cancellationToken = default);
}
