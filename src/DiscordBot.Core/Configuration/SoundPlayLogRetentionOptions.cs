namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for sound play log data retention.
/// </summary>
public class SoundPlayLogRetentionOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "SoundPlayLogRetention";

    /// <summary>
    /// Gets or sets the number of days to retain sound play logs before cleanup.
    /// Default is 90 days.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets whether sound play log cleanup is enabled.
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
}
