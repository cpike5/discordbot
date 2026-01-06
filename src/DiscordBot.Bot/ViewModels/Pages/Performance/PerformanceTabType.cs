namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// Identifies the type of Performance Dashboard tab for routing and display.
/// </summary>
public enum PerformanceTabType
{
    /// <summary>
    /// Overview tab showing aggregated performance metrics.
    /// </summary>
    Overview,

    /// <summary>
    /// Health Metrics tab showing bot health, uptime, and connection status.
    /// </summary>
    HealthMetrics,

    /// <summary>
    /// Commands tab showing command response times, throughput, and errors.
    /// </summary>
    Commands,

    /// <summary>
    /// API Metrics tab showing API usage, rate limits, and latency.
    /// </summary>
    ApiMetrics,

    /// <summary>
    /// System Health tab showing database, cache, and background services.
    /// </summary>
    SystemHealth,

    /// <summary>
    /// Alerts tab showing active alerts, incident history, and configuration.
    /// </summary>
    Alerts
}
