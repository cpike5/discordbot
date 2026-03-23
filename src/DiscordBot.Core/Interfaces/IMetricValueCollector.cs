namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Collects current values for performance metrics.
/// Provides a unified interface for reading live metric values from various
/// infrastructure services (latency, CPU, API rate limits, etc.).
/// </summary>
public interface IMetricValueCollector
{
    /// <summary>
    /// Gets the current value for a specific metric by name.
    /// </summary>
    /// <param name="metricName">The internal metric name (e.g., "gateway_latency", "error_rate").</param>
    /// <returns>The current metric value, or null if the metric is not available.</returns>
    Task<double?> GetCurrentMetricValueAsync(string metricName);

    /// <summary>
    /// Gets all known metric values as a dictionary.
    /// </summary>
    /// <returns>Dictionary mapping metric names to their current values. Unavailable metrics have null values.</returns>
    Task<IReadOnlyDictionary<string, double?>> GetAllMetricValuesAsync();
}
