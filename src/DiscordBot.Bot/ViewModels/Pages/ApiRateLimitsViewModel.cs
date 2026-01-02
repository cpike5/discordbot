using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// ViewModel for the API Rate Limits dashboard page.
/// </summary>
public class ApiRateLimitsViewModel
{
    /// <summary>
    /// Gets or sets the total number of API requests.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of rate limit hits.
    /// </summary>
    public int RateLimitHits { get; set; }

    /// <summary>
    /// Gets or sets the average API latency in milliseconds.
    /// </summary>
    public double AvgLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile API latency in milliseconds.
    /// </summary>
    public double P95LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the API usage breakdown by category (REST, Gateway, etc.).
    /// </summary>
    public IReadOnlyList<ApiUsageDto> UsageByCategory { get; set; } = Array.Empty<ApiUsageDto>();

    /// <summary>
    /// Gets or sets the recent rate limit events.
    /// </summary>
    public IReadOnlyList<RateLimitEventDto> RecentRateLimitEvents { get; set; } = Array.Empty<RateLimitEventDto>();

    /// <summary>
    /// Gets or sets the API latency statistics for display.
    /// </summary>
    public ApiLatencyStatsDto? LatencyStats { get; set; }

    /// <summary>
    /// Gets or sets the time filter in hours.
    /// </summary>
    public int Hours { get; set; } = 24;

    /// <summary>
    /// Determines the health status based on rate limit hits and latency.
    /// </summary>
    /// <returns>Health status: "critical", "warning", or "healthy".</returns>
    public string GetHealthStatus()
    {
        if (RateLimitHits > 10 || AvgLatencyMs > 500)
            return "critical";
        if (RateLimitHits > 0 || AvgLatencyMs > 200)
            return "warning";
        return "healthy";
    }

    /// <summary>
    /// Gets the human-readable health status text.
    /// </summary>
    /// <returns>Status text for display.</returns>
    public string GetHealthStatusText()
    {
        return GetHealthStatus() switch
        {
            "critical" => "Critical",
            "warning" => "Rate Limit Warning",
            _ => "Healthy"
        };
    }

    /// <summary>
    /// Gets the CSS class for the health status badge.
    /// </summary>
    /// <returns>CSS class name.</returns>
    public string GetHealthStatusClass()
    {
        return GetHealthStatus() switch
        {
            "critical" => "health-status-critical",
            "warning" => "health-status-warning",
            _ => "health-status-healthy"
        };
    }

    /// <summary>
    /// Gets the CSS class for latency value coloring.
    /// </summary>
    /// <param name="latencyMs">The latency value in milliseconds.</param>
    /// <returns>CSS class for color coding.</returns>
    public static string GetLatencyClass(double latencyMs)
    {
        if (latencyMs > 500)
            return "text-error";
        if (latencyMs > 200)
            return "text-warning";
        return "text-success";
    }
}
