using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for MessageLog entities with message-specific operations.
/// </summary>
public interface IMessageLogRepository : IRepository<MessageLog>
{
    /// <summary>
    /// Gets messages sent by a specific user within an optional date range.
    /// </summary>
    /// <param name="authorId">Discord user ID of the message author.</param>
    /// <param name="since">Optional start date filter.</param>
    /// <param name="until">Optional end date filter.</param>
    /// <param name="limit">Maximum number of messages to return (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of message logs for the specified user.</returns>
    Task<IEnumerable<MessageLog>> GetUserMessagesAsync(
        ulong authorId,
        DateTime? since = null,
        DateTime? until = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages from a specific channel within an optional date range.
    /// </summary>
    /// <param name="channelId">Discord channel ID.</param>
    /// <param name="since">Optional start date filter.</param>
    /// <param name="limit">Maximum number of messages to return (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of message logs from the specified channel.</returns>
    Task<IEnumerable<MessageLog>> GetChannelMessagesAsync(
        ulong channelId,
        DateTime? since = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages from a specific guild within an optional date range.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="since">Optional start date filter.</param>
    /// <param name="limit">Maximum number of messages to return (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of message logs from the specified guild.</returns>
    Task<IEnumerable<MessageLog>> GetGuildMessagesAsync(
        ulong guildId,
        DateTime? since = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes message logs older than the specified cutoff date.
    /// Used for implementing data retention policies.
    /// </summary>
    /// <param name="cutoff">Cutoff date - messages logged before this date will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages deleted.</returns>
    Task<int> DeleteMessagesOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of messages matching optional filters.
    /// </summary>
    /// <param name="authorId">Optional filter by author ID.</param>
    /// <param name="guildId">Optional filter by guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of messages matching the filters.</returns>
    Task<long> GetMessageCountAsync(
        ulong? authorId = null,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated message logs with filtering based on query parameters.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the filtered message logs and the total count of matching records.</returns>
    Task<(IEnumerable<MessageLog> Items, int TotalCount)> GetPaginatedAsync(
        MessageLogQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all message logs for a specific user.
    /// Used for GDPR compliance and user data deletion requests.
    /// </summary>
    /// <param name="userId">The Discord user ID whose messages should be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of message logs deleted.</returns>
    Task<int> DeleteByUserIdAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets basic statistics about message logs including total counts and unique authors.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter statistics. If null, returns global statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing (TotalMessages, DmCount, ServerCount, UniqueAuthors).</returns>
    Task<(long Total, long DmCount, long ServerCount, long UniqueAuthors)> GetBasicStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets message count breakdown by day for trend analysis.
    /// </summary>
    /// <param name="days">Number of days to include (default 7).</param>
    /// <param name="guildId">Optional guild ID to filter results. If null, returns global data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of (Date, Count) tuples ordered by date.</returns>
    Task<IEnumerable<(DateOnly Date, long Count)>> GetMessagesByDayAsync(
        int days = 7,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a batch of message logs older than the specified cutoff date.
    /// Used to prevent long-running transactions during cleanup operations.
    /// </summary>
    /// <param name="cutoff">Cutoff date - messages logged before this date will be deleted.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages deleted in this batch.</returns>
    Task<int> DeleteBatchOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the oldest message in the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Timestamp of the oldest message, or null if no messages exist.</returns>
    Task<DateTime?> GetOldestMessageDateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the newest message in the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Timestamp of the newest message, or null if no messages exist.</returns>
    Task<DateTime?> GetNewestMessageDateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for message authors by username for autocomplete functionality.
    /// </summary>
    /// <param name="searchTerm">The search term to match against usernames (case-insensitive).</param>
    /// <param name="guildId">Optional guild ID to filter results to specific guild.</param>
    /// <param name="limit">Maximum number of results to return (default 25).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of distinct user ID and username pairs matching the search term.</returns>
    Task<IEnumerable<(ulong UserId, string Username)>> SearchAuthorsAsync(
        string searchTerm,
        ulong? guildId = null,
        int limit = 25,
        CancellationToken cancellationToken = default);
}
