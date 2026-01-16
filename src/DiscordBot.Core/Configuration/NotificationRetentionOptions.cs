namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for notification data retention and cleanup policies.
/// </summary>
public class NotificationRetentionOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "NotificationRetention";

    /// <summary>
    /// Gets or sets the number of days to retain dismissed notifications before cleanup.
    /// Default is 7 days.
    /// </summary>
    public int DismissedRetentionDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the number of days to retain read notifications before cleanup.
    /// Default is 30 days.
    /// </summary>
    public int ReadRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of days to retain unread notifications before cleanup.
    /// Set to 0 to never delete unread notifications.
    /// Default is 90 days.
    /// </summary>
    public int UnreadRetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the maximum number of records to delete in a single cleanup operation.
    /// Used to prevent long-running transactions. Default is 1000.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the interval (in hours) between automatic cleanup operations.
    /// Default is 24 hours (daily cleanup).
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether automatic cleanup is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
