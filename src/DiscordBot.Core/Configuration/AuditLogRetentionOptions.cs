namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for audit log data retention and cleanup policies.
/// </summary>
public class AuditLogRetentionOptions
{
    /// <summary>
    /// Gets or sets the number of days to retain audit logs before cleanup.
    /// Default is 90 days.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

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
