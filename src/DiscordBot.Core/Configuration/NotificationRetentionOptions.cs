namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for notification data retention and cleanup policies.
/// </summary>
public class NotificationRetentionOptions
{
    /// <summary>
    /// Gets or sets the number of days to retain dismissed notifications before cleanup.
    /// Default is 30 days.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

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
