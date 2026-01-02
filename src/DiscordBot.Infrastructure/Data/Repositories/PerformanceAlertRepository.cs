using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for performance alert configurations and incidents.
/// Provides data access for the performance monitoring and alerting system.
/// </summary>
public class PerformanceAlertRepository : IPerformanceAlertRepository
{
    private readonly BotDbContext _context;
    private readonly ILogger<PerformanceAlertRepository> _logger;

    public PerformanceAlertRepository(
        BotDbContext context,
        ILogger<PerformanceAlertRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PerformanceAlertConfig>> GetAllConfigsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all performance alert configurations");

        var configs = await _context.PerformanceAlertConfigs
            .AsNoTracking()
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} performance alert configurations", configs.Count);

        return configs;
    }

    /// <inheritdoc/>
    public async Task<PerformanceAlertConfig?> GetConfigByMetricNameAsync(
        string metricName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving performance alert configuration for metric: {MetricName}", metricName);

        var config = await _context.PerformanceAlertConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.MetricName == metricName, cancellationToken);

        if (config != null)
        {
            _logger.LogDebug("Found configuration for metric: {MetricName}", metricName);
        }
        else
        {
            _logger.LogDebug("No configuration found for metric: {MetricName}", metricName);
        }

        return config;
    }

    /// <inheritdoc/>
    public async Task<PerformanceAlertConfig> UpdateConfigAsync(
        PerformanceAlertConfig config,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating performance alert configuration for metric: {MetricName}, IsEnabled: {IsEnabled}, Warning: {Warning}, Critical: {Critical}",
            config.MetricName, config.IsEnabled, config.WarningThreshold, config.CriticalThreshold);

        _context.PerformanceAlertConfigs.Update(config);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully updated configuration for metric: {MetricName}", config.MetricName);

        return config;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PerformanceIncident>> GetActiveIncidentsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all active performance incidents");

        var incidents = await _context.PerformanceIncidents
            .AsNoTracking()
            .Where(i => i.Status == IncidentStatus.Active || i.Status == IncidentStatus.Acknowledged)
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.TriggeredAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} active performance incidents", incidents.Count);

        return incidents;
    }

    /// <inheritdoc/>
    public async Task<PerformanceIncident?> GetIncidentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving performance incident by ID: {IncidentId}", id);

        var incident = await _context.PerformanceIncidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (incident != null)
        {
            _logger.LogDebug("Found incident: {IncidentId}, Metric: {MetricName}, Status: {Status}",
                id, incident.MetricName, incident.Status);
        }
        else
        {
            _logger.LogDebug("No incident found with ID: {IncidentId}", id);
        }

        return incident;
    }

    /// <inheritdoc/>
    public async Task<PerformanceIncident?> GetActiveIncidentByMetricAsync(
        string metricName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving active incident for metric: {MetricName}", metricName);

        var incident = await _context.PerformanceIncidents
            .AsNoTracking()
            .Where(i => i.MetricName == metricName &&
                       (i.Status == IncidentStatus.Active || i.Status == IncidentStatus.Acknowledged))
            .OrderByDescending(i => i.TriggeredAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (incident != null)
        {
            _logger.LogDebug("Found active incident for metric: {MetricName}, Severity: {Severity}",
                metricName, incident.Severity);
        }
        else
        {
            _logger.LogDebug("No active incident found for metric: {MetricName}", metricName);
        }

        return incident;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<PerformanceIncident> Items, int TotalCount)> GetIncidentsAsync(
        IncidentQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving incidents with filters - Severity: {Severity}, Status: {Status}, MetricName: {MetricName}, Page: {Page}, PageSize: {PageSize}",
            query.Severity, query.Status, query.MetricName, query.PageNumber, query.PageSize);

        var queryable = _context.PerformanceIncidents.AsNoTracking();

        // Apply filters
        if (query.Severity.HasValue)
        {
            queryable = queryable.Where(i => i.Severity == query.Severity.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(i => i.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.MetricName))
        {
            queryable = queryable.Where(i => i.MetricName == query.MetricName);
        }

        if (query.StartDate.HasValue)
        {
            queryable = queryable.Where(i => i.TriggeredAt >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            queryable = queryable.Where(i => i.TriggeredAt <= query.EndDate.Value);
        }

        // Get total count before pagination
        var totalCount = await queryable.CountAsync(cancellationToken);

        // Apply sorting and pagination
        var skip = (query.PageNumber - 1) * query.PageSize;
        var items = await queryable
            .OrderByDescending(i => i.TriggeredAt)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} incidents out of {TotalCount} total matching filters",
            items.Count, totalCount);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<PerformanceIncident> CreateIncidentAsync(
        PerformanceIncident incident,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Creating performance incident for metric: {MetricName}, Severity: {Severity}, ActualValue: {ActualValue}, ThresholdValue: {ThresholdValue}",
            incident.MetricName, incident.Severity, incident.ActualValue, incident.ThresholdValue);

        await _context.PerformanceIncidents.AddAsync(incident, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Performance incident created: {IncidentId}, Metric: {MetricName}, Severity: {Severity}",
            incident.Id, incident.MetricName, incident.Severity);

        return incident;
    }

    /// <inheritdoc/>
    public async Task<PerformanceIncident> UpdateIncidentAsync(
        PerformanceIncident incident,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating performance incident: {IncidentId}, Status: {Status}, IsAcknowledged: {IsAcknowledged}",
            incident.Id, incident.Status, incident.IsAcknowledged);

        _context.PerformanceIncidents.Update(incident);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Performance incident updated: {IncidentId}, Metric: {MetricName}, Status: {Status}",
            incident.Id, incident.MetricName, incident.Status);

        return incident;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlertFrequencyDataDto>> GetAlertFrequencyAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving alert frequency data for last {Days} days", days);

        var cutoffDate = DateTime.UtcNow.AddDays(-days).Date;

        var incidents = await _context.PerformanceIncidents
            .AsNoTracking()
            .Where(i => i.TriggeredAt >= cutoffDate)
            .ToListAsync(cancellationToken);

        // Group by day and severity
        var frequencyData = incidents
            .GroupBy(i => i.TriggeredAt.Date)
            .Select(g => new AlertFrequencyDataDto
            {
                Date = g.Key,
                CriticalCount = g.Count(i => i.Severity == AlertSeverity.Critical),
                WarningCount = g.Count(i => i.Severity == AlertSeverity.Warning),
                InfoCount = g.Count(i => i.Severity == AlertSeverity.Info)
            })
            .OrderByDescending(d => d.Date)
            .ToList();

        _logger.LogDebug("Retrieved alert frequency data for {Count} days", frequencyData.Count);

        return frequencyData;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PerformanceIncident>> GetAutoRecoveredIncidentsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving last {Limit} auto-recovered incidents", limit);

        var incidents = await _context.PerformanceIncidents
            .AsNoTracking()
            .Where(i => i.Status == IncidentStatus.Resolved &&
                       i.ResolvedAt != null &&
                       !i.IsAcknowledged)
            .OrderByDescending(i => i.ResolvedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} auto-recovered incidents", incidents.Count);

        return incidents;
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOldIncidentsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation(
            "Cleaning up resolved performance incidents older than {CutoffDate} (retention: {RetentionDays} days)",
            cutoffDate, retentionDays);

        var incidentsToDelete = await _context.PerformanceIncidents
            .Where(i => i.Status == IncidentStatus.Resolved &&
                       i.ResolvedAt != null &&
                       i.ResolvedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        var count = incidentsToDelete.Count;

        if (count > 0)
        {
            _context.PerformanceIncidents.RemoveRange(incidentsToDelete);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Deleted {Count} resolved performance incidents older than {CutoffDate}",
                count, cutoffDate);
        }
        else
        {
            _logger.LogDebug("No performance incidents found older than {CutoffDate}", cutoffDate);
        }

        return count;
    }
}
