using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing performance alerts and incidents.
/// Provides business logic and orchestration for the performance monitoring system.
/// </summary>
public interface IPerformanceAlertService
{
    /// <summary>
    /// Retrieves all alert configurations with their current metric values.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of all alert configurations.</returns>
    Task<IReadOnlyList<AlertConfigDto>> GetAllConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific alert configuration by metric name.
    /// </summary>
    /// <param name="metricName">The metric name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The alert configuration if found, otherwise null.</returns>
    Task<AlertConfigDto?> GetConfigByMetricNameAsync(string metricName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an alert configuration with new threshold values.
    /// </summary>
    /// <param name="metricName">The metric name to update.</param>
    /// <param name="update">The update data containing new threshold values.</param>
    /// <param name="userId">The identifier of the user performing the update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated alert configuration.</returns>
    Task<AlertConfigDto> UpdateConfigAsync(string metricName, AlertConfigUpdateDto update, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all currently active (unresolved) incidents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of active incidents.</returns>
    Task<IReadOnlyList<PerformanceIncidentDto>> GetActiveIncidentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves incident history with pagination and filtering.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated result containing incidents and metadata.</returns>
    Task<IncidentPagedResultDto> GetIncidentHistoryAsync(IncidentQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific incident by ID.
    /// </summary>
    /// <param name="id">The incident ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The incident if found, otherwise null.</returns>
    Task<PerformanceIncidentDto?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges an incident, marking it as reviewed by an administrator.
    /// </summary>
    /// <param name="id">The incident ID to acknowledge.</param>
    /// <param name="userId">The identifier of the user acknowledging the incident.</param>
    /// <param name="notes">Optional notes about the acknowledgment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated incident.</returns>
    Task<PerformanceIncidentDto> AcknowledgeIncidentAsync(Guid id, string userId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges all currently active incidents.
    /// </summary>
    /// <param name="userId">The identifier of the user acknowledging the incidents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of incidents acknowledged.</returns>
    Task<int> AcknowledgeAllActiveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves summary statistics for active alerts.
    /// Used for dashboard display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of active alerts by severity.</returns>
    Task<ActiveAlertSummaryDto> GetActiveAlertSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves alert frequency data for charting.
    /// Returns daily incident counts by severity for the specified number of days.
    /// </summary>
    /// <param name="days">Number of days to include in the result. Default is 30.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of alert frequency data points.</returns>
    Task<IReadOnlyList<AlertFrequencyDataDto>> GetAlertFrequencyDataAsync(int days = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent auto-recovery events for display.
    /// Shows incidents that were automatically resolved without manual intervention.
    /// </summary>
    /// <param name="limit">Maximum number of events to return. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of auto-recovery events.</returns>
    Task<IReadOnlyList<AutoRecoveryEventDto>> GetAutoRecoveryEventsAsync(int limit = 10, CancellationToken cancellationToken = default);
}
