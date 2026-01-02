namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the performance alerting system.
/// Controls alert behavior, thresholds, and retention policies.
/// </summary>
public class PerformanceAlertOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PerformanceAlerts";

    /// <summary>
    /// Gets or sets the interval in seconds between metric checks.
    /// The monitoring system will evaluate all enabled metrics at this frequency.
    /// Default is 30 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of consecutive threshold breaches required before raising an alert.
    /// This helps prevent alert noise from temporary spikes.
    /// Default is 2 consecutive breaches.
    /// </summary>
    public int ConsecutiveBreachesRequired { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of consecutive normal readings required before auto-resolving an incident.
    /// This ensures the metric has stabilized before marking the incident as resolved.
    /// Default is 3 consecutive normal readings.
    /// </summary>
    public int ConsecutiveNormalRequired { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of days to retain resolved incidents before cleanup.
    /// Incidents older than this will be deleted by the cleanup task.
    /// Default is 90 days.
    /// </summary>
    public int IncidentRetentionDays { get; set; } = 90;
}
