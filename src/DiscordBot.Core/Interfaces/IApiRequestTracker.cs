using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for tracking Discord API requests and rate limiting events.
/// </summary>
public interface IApiRequestTracker
{
    /// <summary>
    /// Tracks a Discord API log event to extract API request information.
    /// </summary>
    /// <param name="source">The source of the log message (e.g., "Rest", "Gateway").</param>
    /// <param name="message">The log message text.</param>
    /// <param name="severity">The severity level of the log message.</param>
    void TrackLogEvent(string source, string message, int severity);

    /// <summary>
    /// Records a rate limit event from the Discord API.
    /// </summary>
    /// <param name="endpoint">The API endpoint that was rate limited.</param>
    /// <param name="retryAfterMs">The retry-after duration in milliseconds.</param>
    /// <param name="isGlobal">Whether this is a global rate limit.</param>
    void RecordRateLimitHit(string endpoint, int retryAfterMs, bool isGlobal);

    /// <summary>
    /// Gets API usage statistics grouped by category for a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of history to retrieve (default: 24).</param>
    /// <returns>A read-only list of API usage statistics by category.</returns>
    IReadOnlyList<ApiUsageDto> GetUsageStatistics(int hours = 24);

    /// <summary>
    /// Gets rate limit events that occurred within a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of history to retrieve (default: 24).</param>
    /// <returns>A read-only list of rate limit events.</returns>
    IReadOnlyList<RateLimitEventDto> GetRateLimitEvents(int hours = 24);

    /// <summary>
    /// Gets the total number of API requests made within a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours to count requests for (default: 24).</param>
    /// <returns>The total request count.</returns>
    long GetTotalRequests(int hours = 24);

    /// <summary>
    /// Records an API request with latency information.
    /// </summary>
    /// <param name="category">The API category (REST, Gateway, etc.).</param>
    /// <param name="latencyMs">The request latency in milliseconds.</param>
    void RecordRequest(string category, int latencyMs);

    /// <summary>
    /// Gets API latency statistics for a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of history to analyze (default: 24).</param>
    /// <returns>Aggregate latency statistics.</returns>
    ApiLatencyStatsDto GetLatencyStatistics(int hours = 24);

    /// <summary>
    /// Gets API latency samples for time series charting.
    /// </summary>
    /// <param name="hours">The number of hours of history to retrieve (default: 24).</param>
    /// <returns>A read-only list of latency samples.</returns>
    IReadOnlyList<ApiLatencySampleDto> GetLatencySamples(int hours = 24);

    /// <summary>
    /// Gets request volume over time for charting.
    /// </summary>
    /// <param name="hours">The number of hours of history to retrieve (default: 24).</param>
    /// <returns>A read-only list of request volume data points.</returns>
    IReadOnlyList<ApiRequestVolumeDto> GetRequestVolume(int hours = 24);
}
