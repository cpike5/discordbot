using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for ScheduledMessage entities with scheduled-message-specific operations.
/// </summary>
public interface IScheduledMessageRepository : IRepository<ScheduledMessage>
{
    /// <summary>
    /// Gets scheduled messages for a specific guild with pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated scheduled messages and the total count.</returns>
    Task<(IEnumerable<ScheduledMessage> Items, int TotalCount)> GetByGuildIdAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled messages that are due for execution.
    /// Returns messages where NextExecutionAt is less than or equal to the current UTC time and IsEnabled is true.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of scheduled messages that need to be executed.</returns>
    Task<IEnumerable<ScheduledMessage>> GetDueMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a scheduled message by ID with the guild navigation property included.
    /// </summary>
    /// <param name="id">Unique identifier of the scheduled message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scheduled message with guild navigation property, or null if not found.</returns>
    Task<ScheduledMessage?> GetByIdWithGuildAsync(Guid id, CancellationToken cancellationToken = default);
}
