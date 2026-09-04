namespace DiscordBot.Core.DTOs;

/// <summary>
/// Overall performance health status with uptime and latency information.
/// </summary>
public record PerformanceHealthDto
{
    /// <summary>
    /// Gets or sets the overall health status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bot uptime duration.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the current gateway latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the current CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets the current connection state.
    /// </summary>
    public string ConnectionState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this health snapshot was taken (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }
}
