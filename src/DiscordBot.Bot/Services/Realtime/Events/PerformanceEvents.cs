using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services.Realtime.Events;

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceMetricsBroadcastService.BroadcastHealthMetricsAsync"/>
/// ("HealthMetricsUpdate", sent to the <c>performance</c> group).
/// </summary>
public sealed record HealthMetricsUpdatedEvent : IDashboardEvent
{
    public required HealthMetricsUpdateDto Metrics { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceMetricsBroadcastService.BroadcastCommandPerformanceAsync"/>
/// ("CommandPerformanceUpdate", sent to the <c>performance</c> group).
/// </summary>
public sealed record CommandPerformanceUpdatedEvent : IDashboardEvent
{
    public required CommandPerformanceUpdateDto Metrics { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceMetricsBroadcastService.BroadcastSystemMetricsAsync"/>
/// ("SystemMetricsUpdate", sent to the <c>system-health</c> group).
/// </summary>
public sealed record SystemMetricsUpdatedEvent : IDashboardEvent
{
    public required SystemMetricsUpdateDto Metrics { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceNotifier.BroadcastAlertTriggeredAsync"/>
/// ("OnAlertTriggered", sent to the <c>alerts</c> group).
/// </summary>
public sealed record AlertTriggeredEvent : IDashboardEvent
{
    public required PerformanceIncidentDto Incident { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceNotifier.BroadcastAlertResolvedAsync"/>
/// ("OnAlertResolved", sent to the <c>alerts</c> group).
/// </summary>
public sealed record AlertResolvedEvent : IDashboardEvent
{
    public required PerformanceIncidentDto Incident { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceNotifier.BroadcastAlertAcknowledgedAsync"/>
/// ("OnAlertAcknowledged", sent to the <c>alerts</c> group). The hub send carries an anonymous
/// object with these same three fields; there is no existing named DTO for it.
/// </summary>
public sealed record AlertAcknowledgedEvent : IDashboardEvent
{
    public required Guid IncidentId { get; init; }

    public required string AcknowledgedBy { get; init; }

    public required DateTime AcknowledgedAt { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.PerformanceNotifier.BroadcastActiveAlertCountAsync"/>
/// ("OnActiveAlertCountChanged", sent to the <c>alerts</c> group).
/// </summary>
public sealed record ActiveAlertCountChangedEvent : IDashboardEvent
{
    public required ActiveAlertSummaryDto Summary { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
