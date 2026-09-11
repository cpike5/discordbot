using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services.Realtime.Events;

/// <summary>
/// Dual-publish of <see cref="Services.Notifications.NotificationBroadcaster.BroadcastNotificationAsync"/>
/// ("OnNotificationReceived", delivered via <c>Clients.User(userId)</c>).
/// </summary>
public sealed record NotificationReceivedEvent : UserScopedEvent
{
    public required UserNotificationDto Notification { get; init; }
}

/// <summary>
/// Dual-publish of the "OnNotificationCountChanged" send, which follows every notification
/// mutation (received, marked read, dismissed, all-read) in
/// <see cref="Services.Notifications.NotificationBroadcaster"/>.
/// </summary>
public sealed record NotificationCountChangedEvent : UserScopedEvent
{
    public required NotificationSummaryDto Summary { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.Notifications.NotificationBroadcaster.BroadcastNotificationMarkedReadAsync"/>
/// ("OnNotificationMarkedRead", delivered via <c>Clients.User(userId)</c>).
/// </summary>
public sealed record NotificationMarkedReadEvent : UserScopedEvent
{
    public required Guid NotificationId { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.Notifications.NotificationBroadcaster.BroadcastAllReadAsync"/>
/// ("OnAllNotificationsRead", delivered via <c>Clients.User(userId)</c>).
/// </summary>
public sealed record AllNotificationsReadEvent : UserScopedEvent;
