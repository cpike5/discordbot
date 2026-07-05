using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// In-process publish/subscribe bus that lets Blazor Server islands receive the
/// same real-time events the existing notifier services already push over the
/// <c>DashboardHub</c> SignalR connection — without opening a second WebSocket back
/// to our own hub (see <c>docs/architecture/blazor-modernization-selective-plan.md</c> §3.2).
/// </summary>
/// <remarks>
/// Registered as a <b>singleton</b>. The existing notifier services dual-publish:
/// after broadcasting to <c>IHubContext&lt;DashboardHub&gt;</c> they also call the
/// matching <c>Publish*</c> method here (additive — the JS path is untouched).
/// Islands subscribe to the events in <c>OnAfterRenderAsync(firstRender)</c>, marshal
/// to the UI thread with <c>InvokeAsync(StateHasChanged)</c>, and unsubscribe in
/// <c>Dispose</c>. Notification events carry the target <c>userId</c> (the
/// <c>ApplicationUser</c> Id) so each circuit filters to its own user, mirroring the
/// per-user <c>Clients.User(userId)</c> targeting of <c>NotificationBroadcaster</c>.
/// </remarks>
public interface IDashboardEventBus
{
    /// <summary>Raised when a new notification is created for a user. Args: userId, notification.</summary>
    event Action<string, UserNotificationDto>? NotificationReceived;

    /// <summary>Raised when a user's unread summary changes. Args: userId, summary.</summary>
    event Action<string, NotificationSummaryDto>? NotificationCountChanged;

    /// <summary>Raised when a single notification is marked read. Args: userId, notificationId.</summary>
    event Action<string, Guid>? NotificationMarkedRead;

    /// <summary>Raised when all of a user's notifications are marked read. Args: userId.</summary>
    event Action<string>? AllNotificationsRead;

    /// <summary>Raised when the bot's connection status changes (broadcast to all).</summary>
    event Action<BotStatusUpdateDto>? BotStatusChanged;

    // ── Performance dashboard live-tile streams (Slice 7) ──────────────────
    // Dual-published by PerformanceMetricsBroadcastService / PerformanceNotifier
    // alongside their DashboardHub broadcasts, so the perf islands receive the same
    // real-time data over the in-process bus instead of a second WebSocket.

    /// <summary>Raised on each health-metrics tick (latency/memory/cpu/connection).</summary>
    event Action<HealthMetricsUpdateDto>? HealthMetricsUpdated;

    /// <summary>Raised on each command-performance tick (avg/p95/p99/totals/error rate).</summary>
    event Action<CommandPerformanceUpdateDto>? CommandPerformanceUpdated;

    /// <summary>Raised on each system-metrics tick (db/cache/background-service stats).</summary>
    event Action<SystemMetricsUpdateDto>? SystemMetricsUpdated;

    /// <summary>Raised when a performance alert is triggered.</summary>
    event Action<PerformanceIncidentDto>? AlertTriggered;

    /// <summary>Raised when a performance alert is resolved.</summary>
    event Action<PerformanceIncidentDto>? AlertResolved;

    /// <summary>Raised when an alert is acknowledged. Args: incidentId, acknowledgedBy.</summary>
    event Action<Guid, string>? AlertAcknowledged;

    /// <summary>Raised when the active-alert summary changes.</summary>
    event Action<ActiveAlertSummaryDto>? ActiveAlertCountChanged;

    /// <summary>True when at least one island is subscribed to <see cref="HealthMetricsUpdated"/>.</summary>
    bool HasHealthMetricsSubscribers { get; }

    /// <summary>True when at least one island is subscribed to <see cref="CommandPerformanceUpdated"/>.</summary>
    bool HasCommandPerformanceSubscribers { get; }

    /// <summary>True when at least one island is subscribed to <see cref="SystemMetricsUpdated"/>.</summary>
    bool HasSystemMetricsSubscribers { get; }

    /// <summary>Publishes a new-notification event to subscribed islands.</summary>
    void PublishNotificationReceived(string userId, UserNotificationDto notification);

    /// <summary>Publishes an unread-count change to subscribed islands.</summary>
    void PublishNotificationCountChanged(string userId, NotificationSummaryDto summary);

    /// <summary>Publishes a single mark-read event to subscribed islands.</summary>
    void PublishNotificationMarkedRead(string userId, Guid notificationId);

    /// <summary>Publishes an all-read event to subscribed islands.</summary>
    void PublishAllNotificationsRead(string userId);

    /// <summary>Publishes a bot-status change to subscribed islands.</summary>
    void PublishBotStatusChanged(BotStatusUpdateDto status);

    /// <summary>Publishes a health-metrics tick to subscribed islands.</summary>
    void PublishHealthMetricsUpdated(HealthMetricsUpdateDto metrics);

    /// <summary>Publishes a command-performance tick to subscribed islands.</summary>
    void PublishCommandPerformanceUpdated(CommandPerformanceUpdateDto metrics);

    /// <summary>Publishes a system-metrics tick to subscribed islands.</summary>
    void PublishSystemMetricsUpdated(SystemMetricsUpdateDto metrics);

    /// <summary>Publishes an alert-triggered event to subscribed islands.</summary>
    void PublishAlertTriggered(PerformanceIncidentDto incident);

    /// <summary>Publishes an alert-resolved event to subscribed islands.</summary>
    void PublishAlertResolved(PerformanceIncidentDto incident);

    /// <summary>Publishes an alert-acknowledged event to subscribed islands.</summary>
    void PublishAlertAcknowledged(Guid incidentId, string acknowledgedBy);

    /// <summary>Publishes an active-alert-summary change to subscribed islands.</summary>
    void PublishActiveAlertCountChanged(ActiveAlertSummaryDto summary);
}
