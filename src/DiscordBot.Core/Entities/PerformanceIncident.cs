using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a performance incident that was triggered when a metric exceeded its threshold.
/// Tracks the lifecycle of the incident from trigger to resolution or acknowledgment.
/// </summary>
public class PerformanceIncident
{
    /// <summary>
    /// Gets or sets the unique identifier for this incident.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the metric name that triggered this incident.
    /// References PerformanceAlertConfig.MetricName.
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity level of this incident.
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the current status of this incident.
    /// </summary>
    public IncidentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this incident was triggered.
    /// Stored in UTC.
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this incident was resolved.
    /// Null if still active or acknowledged. Stored in UTC.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Gets or sets the threshold value that was configured when the incident was triggered.
    /// </summary>
    public double ThresholdValue { get; set; }

    /// <summary>
    /// Gets or sets the actual metric value that triggered the incident.
    /// </summary>
    public double ActualValue { get; set; }

    /// <summary>
    /// Gets or sets a descriptive message about the incident.
    /// Example: "Command response time exceeded critical threshold: 1500ms > 1000ms".
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this incident has been acknowledged by an administrator.
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who acknowledged this incident.
    /// Null if not acknowledged.
    /// </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this incident was acknowledged.
    /// Null if not acknowledged. Stored in UTC.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Gets or sets optional notes from the administrator who acknowledged this incident.
    /// Can include investigation findings, mitigation actions taken, or other context.
    /// </summary>
    public string? Notes { get; set; }
}
