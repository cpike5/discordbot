using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for reading alert configuration.
/// Includes current metric value for display purposes.
/// </summary>
public record AlertConfigDto
{
    /// <summary>
    /// Unique identifier for this alert configuration.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Internal metric name identifier.
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name for this metric.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Description of what this metric measures and when alerts are raised.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Threshold value that triggers a warning-level alert.
    /// Null if no warning threshold is configured.
    /// </summary>
    public double? WarningThreshold { get; init; }

    /// <summary>
    /// Threshold value that triggers a critical-level alert.
    /// Null if no critical threshold is configured.
    /// </summary>
    public double? CriticalThreshold { get; init; }

    /// <summary>
    /// Unit of measurement for the threshold values.
    /// Examples: "ms", "%", "MB", "count".
    /// </summary>
    public string ThresholdUnit { get; init; } = string.Empty;

    /// <summary>
    /// Whether this alert is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Current value of the metric for display purposes.
    /// Null if metric value is not available.
    /// </summary>
    public double? CurrentValue { get; init; }
}

/// <summary>
/// Data transfer object for updating alert configuration.
/// Only includes fields that can be modified.
/// </summary>
public record AlertConfigUpdateDto
{
    /// <summary>
    /// New warning threshold value.
    /// Null to keep existing value or clear threshold.
    /// </summary>
    public double? WarningThreshold { get; init; }

    /// <summary>
    /// New critical threshold value.
    /// Null to keep existing value or clear threshold.
    /// </summary>
    public double? CriticalThreshold { get; init; }

    /// <summary>
    /// Whether this alert should be enabled.
    /// Null to keep existing value.
    /// </summary>
    public bool? IsEnabled { get; init; }
}

/// <summary>
/// Data transfer object for reading performance incident details.
/// </summary>
public record PerformanceIncidentDto
{
    /// <summary>
    /// Unique identifier for this incident.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Metric name that triggered this incident.
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Severity level of this incident.
    /// </summary>
    public AlertSeverity Severity { get; init; }

    /// <summary>
    /// Current status of this incident.
    /// </summary>
    public IncidentStatus Status { get; init; }

    /// <summary>
    /// Timestamp when this incident was triggered (UTC).
    /// </summary>
    public DateTime TriggeredAt { get; init; }

    /// <summary>
    /// Timestamp when this incident was resolved (UTC).
    /// Null if still active or acknowledged.
    /// </summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>
    /// Threshold value that was configured when the incident was triggered.
    /// </summary>
    public double ThresholdValue { get; init; }

    /// <summary>
    /// Actual metric value that triggered the incident.
    /// </summary>
    public double ActualValue { get; init; }

    /// <summary>
    /// Descriptive message about the incident.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Whether this incident has been acknowledged by an administrator.
    /// </summary>
    public bool IsAcknowledged { get; init; }

    /// <summary>
    /// Identifier of the user who acknowledged this incident.
    /// Null if not acknowledged.
    /// </summary>
    public string? AcknowledgedBy { get; init; }

    /// <summary>
    /// Timestamp when this incident was acknowledged (UTC).
    /// Null if not acknowledged.
    /// </summary>
    public DateTime? AcknowledgedAt { get; init; }

    /// <summary>
    /// Optional notes from the administrator who acknowledged this incident.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Duration of the incident in seconds.
    /// Null if still active. Calculated from TriggeredAt to ResolvedAt.
    /// </summary>
    public double? DurationSeconds { get; init; }
}

/// <summary>
/// Data transfer object for querying incidents with pagination and filtering.
/// </summary>
public record IncidentQueryDto
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Filter by severity level.
    /// Null to include all severities.
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Filter by incident status.
    /// Null to include all statuses.
    /// </summary>
    public IncidentStatus? Status { get; init; }

    /// <summary>
    /// Filter incidents triggered on or after this date (UTC).
    /// Null to not filter by start date.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Filter incidents triggered before this date (UTC).
    /// Null to not filter by end date.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Filter by metric name.
    /// Null to include all metrics.
    /// </summary>
    public string? MetricName { get; init; }
}

/// <summary>
/// Data transfer object for paginated incident results.
/// </summary>
public record IncidentPagedResultDto
{
    /// <summary>
    /// Collection of incidents for the current page.
    /// </summary>
    public IReadOnlyList<PerformanceIncidentDto> Items { get; init; } = Array.Empty<PerformanceIncidentDto>();

    /// <summary>
    /// Total number of incidents matching the query (across all pages).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of pages available.
    /// </summary>
    public int TotalPages { get; init; }
}

/// <summary>
/// Data transfer object for acknowledging an incident.
/// </summary>
public record AcknowledgeIncidentDto
{
    /// <summary>
    /// Optional notes about the acknowledgment.
    /// Can include investigation findings, mitigation actions, or other context.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Data transfer object for active alert summary statistics.
/// Used for dashboard display.
/// </summary>
public record ActiveAlertSummaryDto
{
    /// <summary>
    /// Total number of active incidents.
    /// </summary>
    public int ActiveCount { get; init; }

    /// <summary>
    /// Number of active critical incidents.
    /// </summary>
    public int CriticalCount { get; init; }

    /// <summary>
    /// Number of active warning incidents.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Number of active info incidents.
    /// </summary>
    public int InfoCount { get; init; }
}

/// <summary>
/// Data transfer object for alert frequency chart data.
/// Represents daily incident counts by severity.
/// </summary>
public record AlertFrequencyDataDto
{
    /// <summary>
    /// Date for this data point (date only, no time component).
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Number of critical incidents on this date.
    /// </summary>
    public int CriticalCount { get; init; }

    /// <summary>
    /// Number of warning incidents on this date.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Number of info incidents on this date.
    /// </summary>
    public int InfoCount { get; init; }
}

/// <summary>
/// Data transfer object for auto-recovery event information.
/// Used to display recent automatic incident resolutions.
/// </summary>
public record AutoRecoveryEventDto
{
    /// <summary>
    /// Timestamp when the auto-recovery occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Metric name that auto-recovered.
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Description of the issue that was auto-recovered.
    /// Example: "Command response time exceeded 1000ms".
    /// </summary>
    public string Issue { get; init; } = string.Empty;

    /// <summary>
    /// Action taken for auto-recovery.
    /// Example: "Metric returned to normal range".
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Result of the auto-recovery.
    /// Example: "Incident auto-resolved after 3 consecutive normal readings".
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// Duration of the incident in seconds before auto-recovery.
    /// </summary>
    public double DurationSeconds { get; init; }
}
