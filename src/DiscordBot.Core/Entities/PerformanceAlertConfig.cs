namespace DiscordBot.Core.Entities;

/// <summary>
/// Configuration for a performance alert threshold.
/// Defines warning and critical thresholds for a specific performance metric.
/// </summary>
public class PerformanceAlertConfig
{
    /// <summary>
    /// Gets or sets the unique identifier for this alert configuration.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the internal metric name identifier.
    /// Must be unique. Examples: "CommandResponseTime", "DatabaseQueryTime", "CacheHitRate".
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name for this metric.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this metric measures and when alerts are raised.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the threshold value that triggers a warning-level alert.
    /// Null if no warning threshold is configured.
    /// </summary>
    public double? WarningThreshold { get; set; }

    /// <summary>
    /// Gets or sets the threshold value that triggers a critical-level alert.
    /// Null if no critical threshold is configured.
    /// </summary>
    public double? CriticalThreshold { get; set; }

    /// <summary>
    /// Gets or sets the unit of measurement for the threshold values.
    /// Examples: "ms" (milliseconds), "%" (percentage), "MB" (megabytes), "count".
    /// </summary>
    public string ThresholdUnit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this alert is currently enabled.
    /// Disabled alerts will not trigger incidents.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp when this configuration was created.
    /// Stored in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this configuration was last updated.
    /// Null if never updated. Stored in UTC.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last updated this configuration.
    /// Null if never updated or updated by system.
    /// </summary>
    public string? UpdatedBy { get; set; }
}
