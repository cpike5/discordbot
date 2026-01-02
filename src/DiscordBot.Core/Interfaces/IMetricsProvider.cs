namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for providing current metric values.
/// Used by the alert configuration page to display current values alongside thresholds.
/// </summary>
public interface IMetricsProvider
{
    /// <summary>
    /// Gets the current value for a specific metric.
    /// </summary>
    /// <param name="metricName">The internal metric name (e.g., "gateway_latency", "error_rate").</param>
    /// <returns>The current metric value, or null if not available.</returns>
    Task<double?> GetCurrentValueAsync(string metricName);

    /// <summary>
    /// Gets all current metric values as a dictionary.
    /// </summary>
    /// <returns>Dictionary mapping metric names to their current values. Missing metrics will have null values.</returns>
    Task<IReadOnlyDictionary<string, double?>> GetAllCurrentValuesAsync();
}
