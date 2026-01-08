namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for analytics data retention and aggregation.
/// </summary>
public class AnalyticsRetentionOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "AnalyticsRetention";

    /// <summary>
    /// Gets or sets the number of days to retain hourly snapshots before cleanup.
    /// Default is 14 days.
    /// </summary>
    public int HourlyRetentionDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the number of days to retain daily snapshots before cleanup.
    /// Default is 365 days.
    /// </summary>
    public int DailyRetentionDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets whether analytics aggregation is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

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
    /// Gets or sets the number of days to retain raw UserActivityEvent records before cleanup.
    /// Once aggregated into hourly snapshots, raw events can be deleted.
    /// Default is 3 days to ensure events are aggregated before deletion.
    /// </summary>
    public int ActivityEventRetentionDays { get; set; } = 3;
}
