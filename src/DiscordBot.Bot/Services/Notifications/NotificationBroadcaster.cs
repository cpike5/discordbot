using DiscordBot.Bot.Hubs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Notifications;

/// <summary>
/// Broadcasts notification events to connected clients via SignalR.
/// Handles all real-time push concerns, keeping them separate from notification CRUD logic.
/// </summary>
public class NotificationBroadcaster : INotificationBroadcaster
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationBroadcaster> _logger;

    public NotificationBroadcaster(
        IHubContext<DashboardHub> hubContext,
        INotificationRepository repository,
        ILogger<NotificationBroadcaster> logger)
    {
        _hubContext = hubContext;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastNotificationAsync(
        string userId,
        UserNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var notificationDto = NotificationMapper.ToDto(notification);
            var summary = await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationReceived, notificationDto, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationCountChanged, summary, cancellationToken);

            _logger.LogDebug(
                "Broadcast notification {NotificationId} to user {UserId}",
                notification.Id,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast notification {NotificationId} to user {UserId}",
                notification.Id,
                userId);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastNotificationMarkedReadAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationMarkedRead, new { notificationId }, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationCountChanged, summary, cancellationToken);

            _logger.LogDebug(
                "Broadcast notification {NotificationId} marked as read to user {UserId}",
                notificationId,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast notification marked as read to user {UserId}",
                userId);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastCountChangedAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationCountChanged, summary, cancellationToken);

            _logger.LogDebug(
                "Broadcast notification count change to user {UserId}: TotalUnread={TotalUnread}",
                userId,
                summary.TotalUnread);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast notification count change to user {UserId}",
                userId);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastAllReadAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnAllNotificationsRead, cancellationToken);

            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationCountChanged, summary, cancellationToken);

            _logger.LogDebug(
                "Broadcast all notifications read to user {UserId}",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast all notifications read to user {UserId}",
                userId);
        }
    }
}
