using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Real-time health metrics broadcast via SignalR.
/// Provides current system health snapshot for dashboard updates.
/// </summary>
public record HealthMetricsUpdateDto
{
    /// <summary>
    /// Current gateway latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; init; }

    /// <summary>
    /// Working set memory in MB.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Private memory in MB.
    /// </summary>
    public long PrivateMemoryMB { get; init; }

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Current thread count.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Number of generation 2 garbage collections.
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    /// Current Discord connection state (Connected, Disconnected, Connecting).
    /// </summary>
    public string ConnectionState { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when this metric was captured (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Real-time command performance metrics broadcast via SignalR.
/// Provides aggregate command execution statistics for dashboard updates.
/// </summary>
public record CommandPerformanceUpdateDto
{
    /// <summary>
    /// Total number of commands executed in the last 24 hours.
    /// </summary>
    public int TotalCommands24h { get; init; }

    /// <summary>
    /// Average command response time in milliseconds.
    /// </summary>
    public double AvgResponseTimeMs { get; init; }

    /// <summary>
    /// 95th percentile command response time in milliseconds.
    /// </summary>
    public double P95ResponseTimeMs { get; init; }

    /// <summary>
    /// 99th percentile command response time in milliseconds.
    /// </summary>
    public double P99ResponseTimeMs { get; init; }

    /// <summary>
    /// Command error rate as a percentage (0-100).
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Number of commands executed in the last hour.
    /// </summary>
    public int CommandsLastHour { get; init; }

    /// <summary>
    /// Timestamp when this metric was captured (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Cache hit/miss statistics for SignalR broadcast.
/// Provides simplified cache statistics for real-time monitoring.
/// </summary>
public record CacheStatsDto
{
    /// <summary>
    /// Cache key prefix identifier.
    /// </summary>
    public string KeyPrefix { get; init; } = string.Empty;

    /// <summary>
    /// Number of cache hits.
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Number of cache misses.
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Cache hit rate as a percentage (0-100).
    /// </summary>
    public double HitRate { get; init; }

    /// <summary>
    /// Approximate number of items in the cache for this prefix.
    /// </summary>
    public int Size { get; init; }
}

/// <summary>
/// Background service health status for SignalR broadcast.
/// Provides simplified service status for real-time monitoring.
/// </summary>
public record BackgroundServiceStatusDto
{
    /// <summary>
    /// Service name.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Service status (Running, Stopped, Error).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Last heartbeat timestamp (UTC).
    /// Null if service has not yet sent a heartbeat.
    /// </summary>
    public DateTime? LastHeartbeat { get; init; }

    /// <summary>
    /// Last error message from this service.
    /// Null if no errors have occurred.
    /// </summary>
    public string? LastError { get; init; }
}

/// <summary>
/// Real-time system metrics broadcast via SignalR.
/// Provides database, cache, and background service statistics for dashboard updates.
/// </summary>
public record SystemMetricsUpdateDto
{
    /// <summary>
    /// Average query execution time in milliseconds.
    /// </summary>
    public double AvgQueryTimeMs { get; init; }

    /// <summary>
    /// Total number of database queries executed.
    /// </summary>
    public int TotalQueries { get; init; }

    /// <summary>
    /// Database queries per second.
    /// </summary>
    public double QueriesPerSecond { get; init; }

    /// <summary>
    /// Number of slow queries (exceeding threshold).
    /// </summary>
    public int SlowQueryCount { get; init; }

    /// <summary>
    /// Cache statistics breakdown by key prefix.
    /// </summary>
    public Dictionary<string, CacheStatsDto> CacheStats { get; init; } = new();

    /// <summary>
    /// Collection of background service health statuses.
    /// </summary>
    public List<BackgroundServiceStatusDto> BackgroundServices { get; init; } = new();

    /// <summary>
    /// Timestamp when this metric was captured (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Real-time alert notification broadcast via SignalR.
/// Notifies clients of active performance alerts and incidents.
/// </summary>
public record AlertNotificationDto
{
    /// <summary>
    /// Unique identifier for the performance incident.
    /// </summary>
    public long IncidentId { get; init; }

    /// <summary>
    /// Internal metric name identifier that triggered this alert.
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the alert.
    /// Example: "Command response time exceeded critical threshold (1500ms)".
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Severity level of this alert.
    /// </summary>
    public AlertSeverity Severity { get; init; }

    /// <summary>
    /// Threshold value that was configured when the alert was triggered.
    /// </summary>
    public double ThresholdValue { get; init; }

    /// <summary>
    /// Actual metric value that triggered the alert.
    /// </summary>
    public double ActualValue { get; init; }

    /// <summary>
    /// Timestamp when the alert was triggered (UTC).
    /// </summary>
    public DateTime TriggeredAt { get; init; }

    /// <summary>
    /// Timestamp when the alert was resolved (UTC).
    /// Null if alert is still active.
    /// </summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>
    /// Whether this alert has been acknowledged by an administrator.
    /// </summary>
    public bool IsAcknowledged { get; init; }

    /// <summary>
    /// User identifier of the administrator who acknowledged this alert.
    /// Null if not yet acknowledged.
    /// </summary>
    public string? AcknowledgedBy { get; init; }
}
