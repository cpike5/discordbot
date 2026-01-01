namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the reminder system.
/// </summary>
public class ReminderOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Reminder";

    /// <summary>
    /// Gets or sets the interval (in seconds) between checking for due reminders.
    /// Default is 30 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of reminders to process concurrently.
    /// Default is 5.
    /// </summary>
    public int MaxConcurrentDeliveries { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before marking as failed.
    /// Default is 3.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay (in minutes) before retrying a failed delivery.
    /// Default is 5 minutes.
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of pending reminders allowed per user.
    /// Default is 25.
    /// </summary>
    public int MaxRemindersPerUser { get; set; } = 25;

    /// <summary>
    /// Gets or sets the maximum number of days in advance a reminder can be scheduled.
    /// Default is 365 days.
    /// </summary>
    public int MaxAdvanceDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets the minimum number of minutes in the future a reminder can be scheduled.
    /// Default is 1 minute.
    /// </summary>
    public int MinAdvanceMinutes { get; set; } = 1;
}
