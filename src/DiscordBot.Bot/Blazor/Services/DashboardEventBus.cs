using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Default singleton implementation of <see cref="IDashboardEventBus"/>.
/// </summary>
/// <remarks>
/// Publishers are notifier services whose broadcasts must never be impacted by a
/// faulty subscriber, so each handler is invoked individually and any exception it
/// throws is caught and logged rather than propagated (or short-circuiting the rest
/// of the invocation list). A common cause is a handler that touches a circuit which
/// has just been disposed; islands also unsubscribe in <c>Dispose</c> to minimise this.
/// </remarks>
public sealed class DashboardEventBus : IDashboardEventBus
{
    private readonly ILogger<DashboardEventBus> _logger;

    public DashboardEventBus(ILogger<DashboardEventBus> logger) => _logger = logger;

    /// <inheritdoc/>
    public event Action<string, UserNotificationDto>? NotificationReceived;

    /// <inheritdoc/>
    public event Action<string, NotificationSummaryDto>? NotificationCountChanged;

    /// <inheritdoc/>
    public event Action<string, Guid>? NotificationMarkedRead;

    /// <inheritdoc/>
    public event Action<string>? AllNotificationsRead;

    /// <inheritdoc/>
    public event Action<BotStatusUpdateDto>? BotStatusChanged;

    /// <inheritdoc/>
    public void PublishNotificationReceived(string userId, UserNotificationDto notification)
        => Raise(NotificationReceived, h => h(userId, notification), nameof(NotificationReceived));

    /// <inheritdoc/>
    public void PublishNotificationCountChanged(string userId, NotificationSummaryDto summary)
        => Raise(NotificationCountChanged, h => h(userId, summary), nameof(NotificationCountChanged));

    /// <inheritdoc/>
    public void PublishNotificationMarkedRead(string userId, Guid notificationId)
        => Raise(NotificationMarkedRead, h => h(userId, notificationId), nameof(NotificationMarkedRead));

    /// <inheritdoc/>
    public void PublishAllNotificationsRead(string userId)
        => Raise(AllNotificationsRead, h => h(userId), nameof(AllNotificationsRead));

    /// <inheritdoc/>
    public void PublishBotStatusChanged(BotStatusUpdateDto status)
        => Raise(BotStatusChanged, h => h(status), nameof(BotStatusChanged));

    /// <summary>
    /// Invokes each subscriber of a multicast delegate in isolation so one throwing
    /// (or disposed-circuit) handler cannot break the publisher or the other subscribers.
    /// </summary>
    private void Raise<TDelegate>(TDelegate? handlers, Action<TDelegate> invoke, string eventName)
        where TDelegate : Delegate
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                invoke((TDelegate)handler);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard event bus subscriber threw while handling {Event}", eventName);
            }
        }
    }
}
