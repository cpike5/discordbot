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
}
