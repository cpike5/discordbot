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
}
