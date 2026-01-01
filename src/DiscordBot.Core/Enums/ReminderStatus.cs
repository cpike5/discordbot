namespace DiscordBot.Core.Enums;

/// <summary>
/// Status states for a reminder.
/// </summary>
public enum ReminderStatus
{
    /// <summary>
    /// Reminder is waiting for the scheduled trigger time.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Reminder was successfully delivered to the user.
    /// </summary>
    Delivered = 1,

    /// <summary>
    /// Reminder delivery failed after maximum retry attempts.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Reminder was cancelled by the user or admin.
    /// </summary>
    Cancelled = 3
}
