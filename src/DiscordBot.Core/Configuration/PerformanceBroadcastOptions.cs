namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the performance metrics broadcast service.
/// Controls the intervals at which different metric types are broadcast via SignalR.
/// </summary>
public class PerformanceBroadcastOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "PerformanceBroadcast";

    /// <summary>
    /// Gets or sets the interval (in seconds) for health metrics broadcasts
    /// (latency, memory, CPU, connection state).
    /// Default: 5 seconds.
    /// </summary>
    public int HealthMetricsIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the interval (in seconds) for command performance broadcasts
    /// (response times, throughput, error rates).
    /// Default: 30 seconds.
    /// </summary>
    public int CommandMetricsIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the interval (in seconds) for system health broadcasts
    /// (database, cache, background services).
    /// Default: 10 seconds.
    /// </summary>
    public int SystemMetricsIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether performance broadcasting is enabled.
    /// When disabled, no metrics will be broadcast to SignalR clients.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
