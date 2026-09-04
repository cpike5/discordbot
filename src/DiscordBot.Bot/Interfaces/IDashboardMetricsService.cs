using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Provides bot status, health, alert, and performance metrics for the dashboard hub.
/// Extracted from <see cref="DiscordBot.Bot.Hubs.DashboardHub"/> to keep the hub thin.
/// </summary>
public interface IDashboardMetricsService
{
    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    BotStatusDto GetCurrentStatus(string? connectionId, string? userName);

    /// <summary>
    /// Gets the current health metrics including connection state, uptime, and latency.
    /// </summary>
    PerformanceHealthDto GetHealthStatus(string? connectionId, string? userName);

    /// <summary>
    /// Gets the current active alert count for dashboard display.
    /// </summary>
    Task<ActiveAlertSummaryDto> GetActiveAlertCountAsync(string? connectionId, string? userName);

    /// <summary>
    /// Gets the current performance metrics including latency, memory, CPU, and connection state.
    /// </summary>
    HealthMetricsUpdateDto GetCurrentPerformanceMetrics(string? connectionId, string? userName);

    /// <summary>
    /// Gets the current system health including database, cache, and background service metrics.
    /// </summary>
    SystemMetricsUpdateDto GetCurrentSystemHealth(string? connectionId, string? userName);

    /// <summary>
    /// Gets the current command performance metrics over a specified number of hours.
    /// </summary>
    Task<CommandPerformanceUpdateDto> GetCurrentCommandPerformanceAsync(string? connectionId, string? userName, int hours);
}
