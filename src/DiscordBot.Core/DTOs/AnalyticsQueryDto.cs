namespace DiscordBot.Core.DTOs;

/// <summary>
/// Common query parameters for analytics endpoints.
/// Used to filter and configure analytics data requests.
/// </summary>
public record AnalyticsQueryDto
{
    /// <summary>
    /// Gets the start date for the query (inclusive).
    /// If null, defaults to a service-specific value (e.g., 7 days ago).
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets the end date for the query (inclusive).
    /// If null, defaults to the current date/time.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets the granularity for time series data.
    /// Valid values: "hourly", "daily". If null, service determines default.
    /// </summary>
    public string? Granularity { get; init; }

    /// <summary>
    /// Gets the maximum number of results to return.
    /// Default is 100.
    /// </summary>
    public int Limit { get; init; } = 100;
}
