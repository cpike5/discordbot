using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for performance alert configurations and incidents.
/// Handles data access for the performance monitoring and alerting system.
/// </summary>
public interface IPerformanceAlertRepository
{
    /// <summary>
    /// Retrieves all alert configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of all alert configurations.</returns>
    Task<IReadOnlyList<PerformanceAlertConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific alert configuration by metric name.
    /// </summary>
    /// <param name="metricName">The metric name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The alert configuration if found, otherwise null.</returns>
    Task<PerformanceAlertConfig?> GetConfigByMetricNameAsync(string metricName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing alert configuration.
    /// </summary>
    /// <param name="config">The alert configuration to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated alert configuration.</returns>
    Task<PerformanceAlertConfig> UpdateConfigAsync(PerformanceAlertConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active (unresolved) incidents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of active incidents.</returns>
    Task<IReadOnlyList<PerformanceIncident>> GetActiveIncidentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific incident by ID.
    /// </summary>
    /// <param name="id">The incident ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The incident if found, otherwise null.</returns>
    Task<PerformanceIncident?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the currently active incident for a specific metric.
    /// </summary>
    /// <param name="metricName">The metric name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active incident for the metric if found, otherwise null.</returns>
    Task<PerformanceIncident?> GetActiveIncidentByMetricAsync(string metricName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves incidents with pagination and filtering.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing the list of incidents and total count matching the query.</returns>
    Task<(IReadOnlyList<PerformanceIncident> Items, int TotalCount)> GetIncidentsAsync(IncidentQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new incident.
    /// </summary>
    /// <param name="incident">The incident to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created incident with generated ID.</returns>
    Task<PerformanceIncident> CreateIncidentAsync(PerformanceIncident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing incident.
    /// </summary>
    /// <param name="incident">The incident to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated incident.</returns>
    Task<PerformanceIncident> UpdateIncidentAsync(PerformanceIncident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves alert frequency data for the specified number of days.
    /// Returns daily counts of incidents grouped by severity.
    /// </summary>
    /// <param name="days">Number of days to include in the result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of alert frequency data ordered by date descending.</returns>
    Task<IReadOnlyList<AlertFrequencyDataDto>> GetAlertFrequencyAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent auto-recovered incidents.
    /// Auto-recovered incidents are those that resolved automatically without manual intervention.
    /// </summary>
    /// <param name="limit">Maximum number of incidents to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of auto-recovered incidents ordered by resolved time descending.</returns>
    Task<IReadOnlyList<PerformanceIncident>> GetAutoRecoveredIncidentsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes resolved incidents older than the specified retention period.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain resolved incidents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of incidents deleted.</returns>
    Task<int> CleanupOldIncidentsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
