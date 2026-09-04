namespace DiscordBot.Core.DTOs;

/// <summary>
/// A connection state change event.
/// </summary>
public record ConnectionEventDto
{
    /// <summary>
    /// Gets or sets the type of connection event (Connected, Disconnected, Connecting).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the reason for the event (e.g., exception message on disconnect).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets additional details about the event.
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Aggregate statistics about connection events over a time period.
/// </summary>
public record ConnectionStatsDto
{
    /// <summary>
    /// Gets or sets the total number of connection events recorded.
    /// </summary>
    public int TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of reconnection events.
    /// </summary>
    public int ReconnectionCount { get; set; }

    /// <summary>
    /// Gets or sets the average session duration.
    /// </summary>
    public TimeSpan AverageSessionDuration { get; set; }

    /// <summary>
    /// Gets or sets the uptime percentage over the specified period.
    /// </summary>
    public double UptimePercentage { get; set; }
}

// ============================================================================
// Command Performance DTOs
// ============================================================================
