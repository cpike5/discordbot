using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing personal reminders.
/// </summary>
public interface IReminderService
{
    /// <summary>
    /// Creates a new reminder.
    /// </summary>
    /// <param name="guildId">The Discord guild ID where the reminder was created.</param>
    /// <param name="channelId">The Discord channel ID where the command was invoked.</param>
    /// <param name="userId">The Discord user ID who is setting the reminder.</param>
    /// <param name="message">The reminder message content.</param>
    /// <param name="triggerAt">The UTC time when the reminder should be triggered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created reminder.</returns>
    Task<Reminder> CreateReminderAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        string message,
        DateTime triggerAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reminders for a specific user with pagination.
    /// </summary>
    /// <param name="userId">The Discord user ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated reminders and total count.</returns>
    Task<(IEnumerable<Reminder> Items, int TotalCount)> GetUserRemindersAsync(
        ulong userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a reminder.
    /// </summary>
    /// <param name="id">The reminder ID.</param>
    /// <param name="userId">The Discord user ID (for ownership verification).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled reminder if found and owned by the user, otherwise null.</returns>
    Task<Reminder?> CancelReminderAsync(
        Guid id,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending reminders for a user.
    /// </summary>
    /// <param name="userId">The Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of pending reminders.</returns>
    Task<int> GetPendingCountAsync(
        ulong userId,
        CancellationToken cancellationToken = default);
}
