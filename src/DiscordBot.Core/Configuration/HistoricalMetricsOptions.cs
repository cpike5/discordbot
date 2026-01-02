namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for historical metrics collection.
/// </summary>
public class HistoricalMetricsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "HistoricalMetrics";

    /// <summary>
    /// Interval between metric samples in seconds.
    /// Default: 60 seconds.
    /// </summary>
    public int SampleIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Number of days to retain historical snapshots.
    /// Default: 30 days.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether to enable historical metrics collection.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hours between cleanup runs.
    /// Default: 6 hours.
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 6;

    /// <summary>
    /// Initial delay in seconds before starting collection loop.
    /// Allows other services to initialize first.
    /// Default: 10 seconds.
    /// </summary>
    public double InitialDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Delay in seconds before retrying after an error.
    /// Default: 30 seconds.
    /// </summary>
    public double ErrorRetryDelaySeconds { get; set; } = 30;
}
