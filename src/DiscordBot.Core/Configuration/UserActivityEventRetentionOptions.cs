namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for user activity event retention and cleanup.
/// </summary>
public class UserActivityEventRetentionOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "UserActivityEventRetention";

    /// <summary>
    /// Gets or sets the number of days to retain activity events before cleanup.
    /// Default is 90 days.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the maximum number of events to delete in a single cleanup operation.
    /// Used to prevent long-running transactions. Default is 1000.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the interval (in hours) between automatic cleanup operations.
    /// Default is 24 hours (daily cleanup).
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether activity event retention cleanup is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
