using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for Reminder entities with reminder-specific operations.
/// </summary>
public interface IReminderRepository : IRepository<Reminder>
{
    /// <summary>
    /// Gets pending reminders that are due for delivery.
    /// Returns reminders where Status is Pending and TriggerAt is less than or equal to the current UTC time.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of reminders that need to be delivered.</returns>
    Task<IEnumerable<Reminder>> GetDueRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reminders for a specific user with pagination.
    /// </summary>
    /// <param name="userId">Discord user ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="pendingOnly">If true, only returns pending reminders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated reminders and the total count.</returns>
    Task<(IEnumerable<Reminder> Items, int TotalCount)> GetByUserAsync(
        ulong userId,
        int page,
        int pageSize,
        bool pendingOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending reminders for a specific user.
    /// Used for enforcing per-user reminder limits.
    /// </summary>
    /// <param name="userId">Discord user ID to count for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of pending reminders for the user.</returns>
    Task<int> GetPendingCountByUserAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reminders for a specific guild with pagination and optional status filtering.
    /// Used for admin views.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated reminders and the total count.</returns>
    Task<(IEnumerable<Reminder> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        ReminderStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a reminder by ID for a specific user.
    /// Used for cancel operations to ensure users can only cancel their own reminders.
    /// </summary>
    /// <param name="id">Reminder ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reminder if found and owned by the user, otherwise null.</returns>
    Task<Reminder?> GetByIdForUserAsync(Guid id, ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reminder statistics for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID to get stats for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing total, pending, delivered today, and failed counts.</returns>
    Task<(int TotalCount, int PendingCount, int DeliveredTodayCount, int FailedCount)> GetGuildStatsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);
}
