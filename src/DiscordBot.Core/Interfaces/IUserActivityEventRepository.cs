using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserActivityEvent entities with analytics-specific operations.
/// </summary>
public interface IUserActivityEventRepository : IRepository<UserActivityEvent>
{
    /// <summary>
    /// Adds a batch of activity events efficiently.
    /// </summary>
    /// <param name="events">The events to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of events added.</returns>
    Task<int> AddBatchAsync(
        IEnumerable<UserActivityEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity events for a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Activity events in the specified range.</returns>
    Task<IEnumerable<UserActivityEvent>> GetGuildEventsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity events for a specific user in a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Activity events for the user.</returns>
    Task<IEnumerable<UserActivityEvent>> GetUserEventsAsync(
        ulong guildId,
        ulong userId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity events for a specific channel within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Activity events for the channel.</returns>
    Task<IEnumerable<UserActivityEvent>> GetChannelEventsAsync(
        ulong guildId,
        ulong channelId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of events by type for a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping event types to counts.</returns>
    Task<Dictionary<ActivityEventType, int>> GetEventCountsByTypeAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unique active user IDs for a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Set of unique user IDs.</returns>
    Task<HashSet<ulong>> GetActiveUserIdsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unique active channel IDs for a guild within a time range.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Set of unique channel IDs.</returns>
    Task<HashSet<ulong>> GetActiveChannelIdsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes events older than the specified cutoff date.
    /// Used for implementing data retention policies.
    /// </summary>
    /// <param name="cutoff">Cutoff date - events before this date will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of events deleted.</returns>
    Task<int> DeleteEventsOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a batch of events older than the specified cutoff date.
    /// Used to prevent long-running transactions during cleanup operations.
    /// </summary>
    /// <param name="cutoff">Cutoff date - events before this date will be deleted.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of events deleted in this batch.</returns>
    Task<int> DeleteBatchOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated activity counts per user for a guild within a time range.
    /// Used for member activity analytics.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of (UserId, MessageCount, ReactionCount, UniqueChannels) tuples.</returns>
    Task<IEnumerable<(ulong UserId, int MessageCount, int ReactionCount, int UniqueChannels)>> GetUserActivitySummaryAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated activity counts per channel for a guild within a time range.
    /// Used for channel activity analytics.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of (ChannelId, MessageCount, ReactionCount, UniqueUsers) tuples.</returns>
    Task<IEnumerable<(ulong ChannelId, int MessageCount, int ReactionCount, int UniqueUsers)>> GetChannelActivitySummaryAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the oldest event in the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Timestamp of the oldest event, or null if no events exist.</returns>
    Task<DateTime?> GetOldestEventDateAsync(CancellationToken cancellationToken = default);
}
