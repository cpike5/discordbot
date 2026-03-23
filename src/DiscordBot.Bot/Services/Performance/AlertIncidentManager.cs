using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Performance;

/// <summary>
/// Manages the lifecycle of performance alert incidents.
/// Creates new incidents when thresholds are breached, auto-resolves them when metrics
/// return to normal, broadcasts changes via SignalR, and sends admin notifications.
/// </summary>
public class AlertIncidentManager : IAlertIncidentManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPerformanceNotifier _performanceNotifier;
    private readonly ILogger<AlertIncidentManager> _logger;
    private readonly NotificationOptions _notificationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertIncidentManager"/> class.
    /// </summary>
    public AlertIncidentManager(
        IServiceProvider serviceProvider,
        IPerformanceNotifier performanceNotifier,
        ILogger<AlertIncidentManager> logger,
        IOptions<NotificationOptions> notificationOptions)
    {
        _serviceProvider = serviceProvider;
        _performanceNotifier = performanceNotifier;
        _logger = logger;
        _notificationOptions = notificationOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleBreachAsync(
        PerformanceAlertConfig config,
        double currentValue,
        AlertSeverity severity,
        double threshold,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken)
    {
        var existingIncident = await repository.GetActiveIncidentByMetricAsync(config.MetricName, cancellationToken);

        if (existingIncident != null)
        {
            _logger.LogTrace(
                "Active incident already exists for {MetricName}, not creating duplicate",
                config.MetricName);
            return false;
        }

        // Create new incident
        var incident = new PerformanceIncident
        {
            Id = Guid.NewGuid(),
            MetricName = config.MetricName,
            Severity = severity,
            Status = IncidentStatus.Active,
            TriggeredAt = DateTime.UtcNow,
            ThresholdValue = threshold,
            ActualValue = currentValue,
            Message = $"{config.DisplayName} exceeded {severity.ToString().ToLower()} threshold: {currentValue:F2}{config.ThresholdUnit} >= {threshold:F2}{config.ThresholdUnit}"
        };

        var createdIncident = await repository.CreateIncidentAsync(incident, cancellationToken);

        _logger.LogWarning(
            "Alert triggered for {MetricName}: {Message}",
            config.MetricName,
            incident.Message);

        // Broadcast via SignalR using the notifier
        var dto = MapToIncidentDto(createdIncident);
        await _performanceNotifier.BroadcastAlertTriggeredAsync(dto, cancellationToken);

        // Create admin notification (fire-and-forget)
        _ = CreateAlertNotificationAsync(createdIncident, isResolved: false, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleResolutionAsync(
        PerformanceAlertConfig config,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken)
    {
        var existingIncident = await repository.GetActiveIncidentByMetricAsync(config.MetricName, cancellationToken);

        if (existingIncident == null)
        {
            return false;
        }

        existingIncident.Status = IncidentStatus.Resolved;
        existingIncident.ResolvedAt = DateTime.UtcNow;

        var resolvedIncident = await repository.UpdateIncidentAsync(existingIncident, cancellationToken);

        _logger.LogInformation(
            "Auto-resolved incident for {MetricName}",
            config.MetricName);

        // Broadcast via SignalR using the notifier
        var dto = MapToIncidentDto(resolvedIncident);
        await _performanceNotifier.BroadcastAlertResolvedAsync(dto, cancellationToken);

        // Create admin notification for resolution (fire-and-forget)
        _ = CreateAlertNotificationAsync(resolvedIncident, isResolved: true, cancellationToken);

        return true;
    }

    /// <summary>
    /// Maps a PerformanceIncident entity to a PerformanceIncidentDto.
    /// </summary>
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

    /// <summary>
    /// Creates an admin notification for a performance alert incident.
    /// Uses fire-and-forget pattern with error handling to avoid blocking the main flow.
    /// </summary>
    private async Task CreateAlertNotificationAsync(
        PerformanceIncident incident,
        bool isResolved,
        CancellationToken cancellationToken)
    {
        if (!_notificationOptions.EnablePerformanceAlerts)
        {
            _logger.LogDebug("Performance alert notifications are disabled, skipping notification for {MetricName}", incident.MetricName);
            return;
        }

        // Skip Info severity alerts for notifications
        if (!isResolved && incident.Severity == AlertSeverity.Info)
        {
            _logger.LogDebug("Skipping notification for Info severity alert: {MetricName}", incident.MetricName);
            return;
        }

        // For resolved notifications, only notify if it was Critical severity
        if (isResolved && incident.Severity != AlertSeverity.Critical)
        {
            _logger.LogDebug("Skipping resolution notification for non-Critical severity alert: {MetricName}", incident.MetricName);
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var title = isResolved
                ? $"{incident.MetricName} Resolved"
                : $"{incident.MetricName} Alert";

            var deduplicationWindow = TimeSpan.FromMinutes(_notificationOptions.DuplicateSuppressionMinutes);

            await notificationService.CreateForAllAdminsAsync(
                NotificationType.PerformanceAlert,
                title,
                incident.Message,
                linkUrl: "/Admin/Performance/Alerts",
                severity: incident.Severity,
                relatedEntityType: "PerformanceIncident",
                relatedEntityId: incident.Id.ToString(),
                deduplicationWindow: deduplicationWindow,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Created {Status} notification for performance incident {IncidentId} ({MetricName})",
                isResolved ? "resolution" : "alert",
                incident.Id,
                incident.MetricName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create notification for performance incident {IncidentId}",
                incident.Id);
        }
    }
}
