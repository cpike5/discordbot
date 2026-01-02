using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing performance alerts and incidents.
/// Provides business logic and orchestration for the performance monitoring system.
/// </summary>
public class PerformanceAlertService : IPerformanceAlertService
{
    private readonly IPerformanceAlertRepository _repository;
    private readonly IMetricsProvider _metricsProvider;
    private readonly ILogger<PerformanceAlertService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceAlertService"/> class.
    /// </summary>
    /// <param name="repository">The performance alert repository.</param>
    /// <param name="metricsProvider">The metrics provider for getting current metric values.</param>
    /// <param name="logger">The logger.</param>
    public PerformanceAlertService(
        IPerformanceAlertRepository repository,
        IMetricsProvider metricsProvider,
        ILogger<PerformanceAlertService> logger)
    {
        _repository = repository;
        _metricsProvider = metricsProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlertConfigDto>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all alert configurations with current metric values");

        var configs = await _repository.GetAllConfigsAsync(cancellationToken);

        // Get current metric values for enrichment
        var currentValues = await _metricsProvider.GetAllCurrentValuesAsync();

        var dtos = configs.Select(config =>
        {
            var dto = MapToDto(config);
            // Enrich with current value if available
            if (currentValues.TryGetValue(config.MetricName, out var currentValue))
            {
                dto = dto with { CurrentValue = currentValue };
            }
            return dto;
        }).ToList();

        _logger.LogInformation("Retrieved {Count} alert configurations with current values", dtos.Count);

        return dtos;
    }

    /// <inheritdoc/>
    public async Task<AlertConfigDto?> GetConfigByMetricNameAsync(string metricName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving alert configuration for metric {MetricName}", metricName);

        var config = await _repository.GetConfigByMetricNameAsync(metricName, cancellationToken);

        if (config == null)
        {
            _logger.LogWarning("Alert configuration not found for metric {MetricName}", metricName);
            return null;
        }

        var dto = MapToDto(config);

        // Enrich with current value
        var currentValue = await _metricsProvider.GetCurrentValueAsync(metricName);
        dto = dto with { CurrentValue = currentValue };

        _logger.LogInformation("Retrieved alert configuration for metric {MetricName}", metricName);

        return dto;
    }

    /// <inheritdoc/>
    public async Task<AlertConfigDto> UpdateConfigAsync(
        string metricName,
        AlertConfigUpdateDto update,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating alert configuration for metric {MetricName} by user {UserId}", metricName, userId);

        var config = await _repository.GetConfigByMetricNameAsync(metricName, cancellationToken);

        if (config == null)
        {
            _logger.LogError("Alert configuration not found for metric {MetricName}", metricName);
            throw new InvalidOperationException($"Alert configuration not found for metric: {metricName}");
        }

        // Apply updates
        if (update.WarningThreshold.HasValue)
        {
            config.WarningThreshold = update.WarningThreshold.Value;
        }

        if (update.CriticalThreshold.HasValue)
        {
            config.CriticalThreshold = update.CriticalThreshold.Value;
        }

        if (update.IsEnabled.HasValue)
        {
            config.IsEnabled = update.IsEnabled.Value;
        }

        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedBy = userId;

        var updatedConfig = await _repository.UpdateConfigAsync(config, cancellationToken);

        _logger.LogInformation(
            "Updated alert configuration for metric {MetricName}: Warning={Warning}, Critical={Critical}, Enabled={Enabled}",
            metricName,
            updatedConfig.WarningThreshold,
            updatedConfig.CriticalThreshold,
            updatedConfig.IsEnabled);

        return MapToDto(updatedConfig);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PerformanceIncidentDto>> GetActiveIncidentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving active incidents");

        var incidents = await _repository.GetActiveIncidentsAsync(cancellationToken);

        var dtos = incidents.Select(MapToIncidentDto).ToList();

        _logger.LogInformation("Retrieved {Count} active incidents", dtos.Count);

        return dtos;
    }

    /// <inheritdoc/>
    public async Task<IncidentPagedResultDto> GetIncidentHistoryAsync(
        IncidentQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving incident history: Page={PageNumber}, PageSize={PageSize}, Severity={Severity}, Status={Status}",
            query.PageNumber,
            query.PageSize,
            query.Severity,
            query.Status);

        var (items, totalCount) = await _repository.GetIncidentsAsync(query, cancellationToken);

        var dtos = items.Select(MapToIncidentDto).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var result = new IncidentPagedResultDto
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalPages = totalPages
        };

        _logger.LogInformation(
            "Retrieved {Count} incidents (page {PageNumber} of {TotalPages}, total {TotalCount})",
            dtos.Count,
            query.PageNumber,
            totalPages,
            totalCount);

        return result;
    }

    /// <inheritdoc/>
    public async Task<PerformanceIncidentDto?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving incident {IncidentId}", id);

        var incident = await _repository.GetIncidentByIdAsync(id, cancellationToken);

        if (incident == null)
        {
            _logger.LogWarning("Incident {IncidentId} not found", id);
            return null;
        }

        var dto = MapToIncidentDto(incident);

        _logger.LogInformation("Retrieved incident {IncidentId}", id);

        return dto;
    }

    /// <inheritdoc/>
    public async Task<PerformanceIncidentDto> AcknowledgeIncidentAsync(
        Guid id,
        string userId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Acknowledging incident {IncidentId} by user {UserId}", id, userId);

        var incident = await _repository.GetIncidentByIdAsync(id, cancellationToken);

        if (incident == null)
        {
            _logger.LogError("Incident {IncidentId} not found for acknowledgment", id);
            throw new InvalidOperationException($"Incident not found: {id}");
        }

        incident.IsAcknowledged = true;
        incident.AcknowledgedBy = userId;
        incident.AcknowledgedAt = DateTime.UtcNow;
        incident.Notes = notes;

        // If still active, update status to acknowledged
        if (incident.Status == IncidentStatus.Active)
        {
            incident.Status = IncidentStatus.Acknowledged;
        }

        var updatedIncident = await _repository.UpdateIncidentAsync(incident, cancellationToken);

        _logger.LogInformation(
            "Acknowledged incident {IncidentId} for metric {MetricName} by user {UserId}",
            id,
            incident.MetricName,
            userId);

        return MapToIncidentDto(updatedIncident);
    }

    /// <inheritdoc/>
    public async Task<int> AcknowledgeAllActiveAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Acknowledging all active incidents by user {UserId}", userId);

        var activeIncidents = await _repository.GetActiveIncidentsAsync(cancellationToken);
        var count = 0;

        foreach (var incident in activeIncidents)
        {
            incident.IsAcknowledged = true;
            incident.AcknowledgedBy = userId;
            incident.AcknowledgedAt = DateTime.UtcNow;
            incident.Status = IncidentStatus.Acknowledged;

            await _repository.UpdateIncidentAsync(incident, cancellationToken);
            count++;
        }

        _logger.LogInformation("Acknowledged {Count} active incidents by user {UserId}", count, userId);

        return count;
    }

    /// <inheritdoc/>
    public async Task<ActiveAlertSummaryDto> GetActiveAlertSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving active alert summary");

        var activeIncidents = await _repository.GetActiveIncidentsAsync(cancellationToken);

        var summary = new ActiveAlertSummaryDto
        {
            ActiveCount = activeIncidents.Count,
            CriticalCount = activeIncidents.Count(i => i.Severity == AlertSeverity.Critical),
            WarningCount = activeIncidents.Count(i => i.Severity == AlertSeverity.Warning),
            InfoCount = activeIncidents.Count(i => i.Severity == AlertSeverity.Info)
        };

        _logger.LogInformation(
            "Active alert summary: Total={Total}, Critical={Critical}, Warning={Warning}, Info={Info}",
            summary.ActiveCount,
            summary.CriticalCount,
            summary.WarningCount,
            summary.InfoCount);

        return summary;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlertFrequencyDataDto>> GetAlertFrequencyDataAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving alert frequency data for {Days} days", days);

        var data = await _repository.GetAlertFrequencyAsync(days, cancellationToken);

        _logger.LogInformation("Retrieved alert frequency data: {Count} data points for {Days} days", data.Count, days);

        return data;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AutoRecoveryEventDto>> GetAutoRecoveryEventsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} auto-recovery events", limit);

        var incidents = await _repository.GetAutoRecoveredIncidentsAsync(limit, cancellationToken);

        var events = incidents.Select(i => new AutoRecoveryEventDto
        {
            Timestamp = i.ResolvedAt ?? i.TriggeredAt,
            MetricName = i.MetricName,
            Issue = i.Message,
            Action = "Metric returned to normal range",
            Result = "Incident auto-resolved",
            DurationSeconds = i.ResolvedAt.HasValue
                ? (i.ResolvedAt.Value - i.TriggeredAt).TotalSeconds
                : 0
        }).ToList();

        _logger.LogInformation("Retrieved {Count} auto-recovery events", events.Count);

        return events;
    }

    /// <summary>
    /// Maps a PerformanceAlertConfig entity to an AlertConfigDto.
    /// CurrentValue is initially null but will be enriched by the calling methods.
    /// </summary>
    /// <param name="config">The entity to map.</param>
    /// <returns>The DTO representation with CurrentValue as null.</returns>
    private static AlertConfigDto MapToDto(PerformanceAlertConfig config)
    {
        return new AlertConfigDto
        {
            Id = config.Id,
            MetricName = config.MetricName,
            DisplayName = config.DisplayName,
            Description = config.Description,
            WarningThreshold = config.WarningThreshold,
            CriticalThreshold = config.CriticalThreshold,
            ThresholdUnit = config.ThresholdUnit,
            IsEnabled = config.IsEnabled,
            CurrentValue = null // Enriched by calling methods via IMetricsProvider
        };
    }

    /// <summary>
    /// Maps a PerformanceIncident entity to a PerformanceIncidentDto.
    /// Calculates duration if the incident is resolved.
    /// </summary>
    /// <param name="incident">The entity to map.</param>
    /// <returns>The DTO representation.</returns>
    private static PerformanceIncidentDto MapToIncidentDto(PerformanceIncident incident)
    {
        double? durationSeconds = null;

        if (incident.ResolvedAt.HasValue)
        {
            durationSeconds = (incident.ResolvedAt.Value - incident.TriggeredAt).TotalSeconds;
        }

        return new PerformanceIncidentDto
        {
            Id = incident.Id,
            MetricName = incident.MetricName,
            Severity = incident.Severity,
            Status = incident.Status,
            TriggeredAt = incident.TriggeredAt,
            ResolvedAt = incident.ResolvedAt,
            ThresholdValue = incident.ThresholdValue,
            ActualValue = incident.ActualValue,
            Message = incident.Message,
            IsAcknowledged = incident.IsAcknowledged,
            AcknowledgedBy = incident.AcknowledgedBy,
            AcknowledgedAt = incident.AcknowledgedAt,
            Notes = incident.Notes,
            DurationSeconds = durationSeconds
        };
    }
}
