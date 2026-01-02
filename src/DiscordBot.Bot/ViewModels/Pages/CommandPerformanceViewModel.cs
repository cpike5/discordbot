using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Command Performance Analytics page.
/// Displays command response times, throughput, error tracking, and timeout analysis.
/// </summary>
public record CommandPerformanceViewModel
{
    /// <summary>
    /// Gets the total number of commands executed in the selected time period.
    /// </summary>
    public int TotalCommands { get; init; }

    /// <summary>
    /// Gets the average response time across all commands in milliseconds.
    /// </summary>
    public double AvgResponseTimeMs { get; init; }

    /// <summary>
    /// Gets the overall error rate as a percentage (0-100).
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Gets the 99th percentile response time in milliseconds.
    /// </summary>
    public double P99ResponseTimeMs { get; init; }

    /// <summary>
    /// Gets the 50th percentile (median) response time in milliseconds.
    /// </summary>
    public double P50Ms { get; init; }

    /// <summary>
    /// Gets the 95th percentile response time in milliseconds.
    /// </summary>
    public double P95Ms { get; init; }

    /// <summary>
    /// Gets the list of slowest command executions for server-side rendering.
    /// </summary>
    public IReadOnlyList<SlowestCommandDto> SlowestCommands { get; init; } = Array.Empty<SlowestCommandDto>();

    /// <summary>
    /// Gets the total number of command timeouts (commands > 3000ms).
    /// </summary>
    public int TimeoutCount { get; init; }

    /// <summary>
    /// Gets the list of commands with timeout issues.
    /// </summary>
    public IReadOnlyList<CommandTimeoutDto> RecentTimeouts { get; init; } = Array.Empty<CommandTimeoutDto>();

    /// <summary>
    /// Gets the trend for average response time compared to previous period (negative = improvement).
    /// </summary>
    public double AvgResponseTimeTrend { get; init; }

    /// <summary>
    /// Gets the trend for error rate compared to previous period (negative = improvement).
    /// </summary>
    public double ErrorRateTrend { get; init; }

    /// <summary>
    /// Gets the trend for P99 latency compared to previous period (negative = improvement).
    /// </summary>
    public double P99Trend { get; init; }

    /// <summary>
    /// Gets the CSS class for response time trend styling.
    /// </summary>
    public static string GetTrendClass(double trend) => trend switch
    {
        < 0 => "metric-trend-up",    // improvement (lower is better for latency)
        > 0 => "metric-trend-down",  // degradation
        _ => "metric-trend-neutral"
    };

    /// <summary>
    /// Gets the CSS class for error rate trend styling.
    /// </summary>
    public static string GetErrorRateTrendClass(double trend) => trend switch
    {
        < 0 => "metric-trend-up",    // improvement (lower is better)
        > 0 => "metric-trend-down",  // degradation (more errors)
        _ => "metric-trend-neutral"
    };

    /// <summary>
    /// Formats a trend value for display with appropriate sign and unit.
    /// </summary>
    public static string FormatTrend(double trend, string unit = "ms")
    {
        if (Math.Abs(trend) < 0.1) return "No change";
        var sign = trend < 0 ? "" : "+";
        return $"{sign}{trend:F0}{unit} vs yesterday";
    }

    /// <summary>
    /// Gets the CSS class for latency value based on thresholds.
    /// </summary>
    public static string GetLatencyClass(double ms) => ms switch
    {
        < 100 => "text-success",
        < 500 => "text-warning",
        _ => "text-error"
    };

    /// <summary>
    /// Gets the CSS class for error rate value based on thresholds.
    /// </summary>
    public static string GetErrorRateClass(double rate) => rate switch
    {
        < 1.0 => "text-success",
        < 5.0 => "text-warning",
        _ => "text-error"
    };
}

/// <summary>
/// Details about a command that has exceeded the Discord interaction timeout limit.
/// </summary>
public record CommandTimeoutDto
{
    /// <summary>
    /// Gets the command name that timed out.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of times this command has timed out.
    /// </summary>
    public int TimeoutCount { get; init; }

    /// <summary>
    /// Gets when the most recent timeout occurred (UTC).
    /// </summary>
    public DateTime LastTimeout { get; init; }

    /// <summary>
    /// Gets the average response time before timeout in milliseconds.
    /// </summary>
    public double AvgResponseBeforeTimeout { get; init; }

    /// <summary>
    /// Gets the investigation status (Investigating if recent, Resolved otherwise).
    /// </summary>
    public string Status { get; init; } = "Investigating";
}
