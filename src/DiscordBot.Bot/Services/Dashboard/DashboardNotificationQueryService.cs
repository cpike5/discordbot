using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Dashboard;

/// <summary>
/// Default implementation of <see cref="IDashboardNotificationQueryService"/>.
/// Resolves <see cref="INotificationService"/> from a fresh scope per call since it is scoped
/// and the dashboard hub itself is not.
/// </summary>
public class DashboardNotificationQueryService : IDashboardNotificationQueryService
{
    private const string SignalRConnectionIdAttribute = "signalr.connection.id";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DashboardNotificationQueryService> _logger;

    public DashboardNotificationQueryService(
        IServiceProvider serviceProvider,
        ILogger<DashboardNotificationQueryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationSummaryDto> GetNotificationSummaryAsync(string? userId, string? connectionId)
    {
        return await ServiceActivityHelper.ExecuteAsync<NotificationSummaryDto>(
            "dashboard_hub", "get_notification_summary",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userId);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

                _logger.LogDebug(
                    "Notification summary requested by client: ConnectionId={ConnectionId}, UserId={UserId}",
                    connectionId,
                    userId);

                await using var scope = _serviceProvider.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var summary = await notificationService.GetSummaryAsync(userId!);

                _logger.LogTrace(
                    "Notification summary retrieved: UserId={UserId}, TotalUnread={TotalUnread}",
                    userId,
                    summary.TotalUnread);

                return summary;
            });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserNotificationDto>> GetNotificationsAsync(string? userId, string? connectionId, int limit)
    {
        return await ServiceActivityHelper.ExecuteAsync<IEnumerable<UserNotificationDto>>(
            "dashboard_hub", "get_notifications",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userId);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);
                activity?.SetTag("limit", limit);

                _logger.LogDebug(
                    "Notifications requested by client: ConnectionId={ConnectionId}, UserId={UserId}, Limit={Limit}",
                    connectionId,
                    userId,
                    limit);

                await using var scope = _serviceProvider.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var notifications = await notificationService.GetUserNotificationsAsync(userId!, limit);

                _logger.LogTrace(
                    "Notifications retrieved: UserId={UserId}, Count={Count}",
                    userId,
                    notifications.Count());

                return notifications;
            });
    }

    /// <inheritdoc />
    public async Task MarkNotificationReadAsync(string? userId, string? connectionId, Guid notificationId)
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "mark_notification_read",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userId);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);
                activity?.SetTag("notification_id", notificationId.ToString());

                _logger.LogDebug(
                    "Mark notification read requested: ConnectionId={ConnectionId}, UserId={UserId}, NotificationId={NotificationId}",
                    connectionId,
                    userId,
                    notificationId);

                await using var scope = _serviceProvider.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await notificationService.MarkAsReadAsync(userId!, notificationId);

                _logger.LogTrace(
                    "Notification marked as read: UserId={UserId}, NotificationId={NotificationId}",
                    userId,
                    notificationId);
            });
    }

    /// <inheritdoc />
    public async Task MarkAllNotificationsReadAsync(string? userId, string? connectionId)
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "mark_all_notifications_read",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userId);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

                _logger.LogDebug(
                    "Mark all notifications read requested: ConnectionId={ConnectionId}, UserId={UserId}",
                    connectionId,
                    userId);

                await using var scope = _serviceProvider.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await notificationService.MarkAllAsReadAsync(userId!);

                _logger.LogTrace("All notifications marked as read: UserId={UserId}", userId);
            });
    }

    /// <inheritdoc />
    public async Task DismissNotificationAsync(string? userId, string? connectionId, Guid notificationId)
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "dismiss_notification",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userId);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);
                activity?.SetTag("notification_id", notificationId.ToString());

                _logger.LogDebug(
                    "Dismiss notification requested: ConnectionId={ConnectionId}, UserId={UserId}, NotificationId={NotificationId}",
                    connectionId,
                    userId,
                    notificationId);

                await using var scope = _serviceProvider.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await notificationService.DismissAsync(userId!, notificationId);

                _logger.LogTrace(
                    "Notification dismissed: UserId={UserId}, NotificationId={NotificationId}",
                    userId,
                    notificationId);
            });
    }
}
