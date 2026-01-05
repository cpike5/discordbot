using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing user notifications in the admin UI.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    public NotificationService(
        INotificationRepository notificationRepository,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserNotification> CreateNotificationAsync(
        CreateNotificationDto dto,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "create",
            userId: null,
            entityId: dto.UserId);

        try
        {
            var notification = new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = dto.UserId,
                Type = dto.Type,
                Title = dto.Title,
                Message = dto.Message,
                Severity = dto.Severity,
                ActionUrl = dto.ActionUrl,
                RelatedEntityId = dto.RelatedEntityId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _notificationRepository.AddAsync(notification, cancellationToken);

            _logger.LogInformation(
                "Notification created: ID {NotificationId} for user {UserId}, type {Type}, severity {Severity}",
                notification.Id,
                dto.UserId,
                dto.Type,
                dto.Severity);

            BotActivitySource.SetSuccess(activity);
            return notification;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CreateBroadcastNotificationAsync(
        IEnumerable<string> userIds,
        NotificationType type,
        string title,
        string message,
        AlertSeverity severity,
        string? actionUrl = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "broadcast");

        try
        {
            var userIdList = userIds.ToList();
            if (userIdList.Count == 0)
            {
                _logger.LogDebug("No users to broadcast notification to");
                BotActivitySource.SetSuccess(activity);
                return 0;
            }

            var now = DateTime.UtcNow;
            var notifications = userIdList.Select(userId => new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Severity = severity,
                ActionUrl = actionUrl,
                RelatedEntityId = relatedEntityId,
                CreatedAt = now,
                IsRead = false
            }).ToList();

            var count = await _notificationRepository.CreateBatchAsync(notifications, cancellationToken);

            _logger.LogInformation(
                "Broadcast notification created: {Count} notifications for type {Type}, severity {Severity}",
                count,
                type,
                severity);

            BotActivitySource.SetRecordsReturned(activity, count);
            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<NotificationPagedResultDto> GetNotificationsAsync(
        string userId,
        NotificationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "get_list",
            entityId: userId);

        try
        {
            var (items, totalCount) = await _notificationRepository.GetByUserAsync(
                userId,
                query.PageNumber,
                query.PageSize,
                query.Type,
                query.Severity,
                query.IsRead,
                cancellationToken);

            var itemList = items.ToList();
            var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

            var result = new NotificationPagedResultDto
            {
                Items = itemList.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalPages = totalPages
            };

            BotActivitySource.SetRecordsReturned(activity, itemList.Count);
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<UserNotificationDto?> GetNotificationAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "get_by_id",
            entityId: id.ToString());

        try
        {
            var notification = await _notificationRepository.GetByIdForUserAsync(id, userId, cancellationToken);

            if (notification == null)
            {
                _logger.LogDebug(
                    "Notification {NotificationId} not found or not owned by user {UserId}",
                    id,
                    userId);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            BotActivitySource.SetSuccess(activity);
            return MapToDto(notification);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<NotificationSummaryDto> GetSummaryAsync(
        string userId,
        int recentLimit = 5,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "get_summary",
            entityId: userId);

        try
        {
            var (unreadCount, criticalCount, warningCount, infoCount) =
                await _notificationRepository.GetSummaryAsync(userId, cancellationToken);

            var recentNotifications = await _notificationRepository.GetRecentUnreadAsync(
                userId, recentLimit, cancellationToken);

            var result = new NotificationSummaryDto
            {
                UnreadCount = unreadCount,
                CriticalCount = criticalCount,
                WarningCount = warningCount,
                InfoCount = infoCount,
                RecentNotifications = recentNotifications.Select(MapToDto).ToList()
            };

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "get_unread_count",
            entityId: userId);

        try
        {
            var count = await _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);

            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "mark_read",
            entityId: id.ToString());

        try
        {
            var result = await _notificationRepository.MarkAsReadAsync(id, userId, cancellationToken);

            if (result)
            {
                _logger.LogInformation(
                    "Notification {NotificationId} marked as read by user {UserId}",
                    id,
                    userId);
            }

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "mark_all_read",
            entityId: userId);

        try
        {
            var count = await _notificationRepository.MarkAllAsReadAsync(userId, cancellationToken);

            _logger.LogInformation(
                "Marked {Count} notifications as read for user {UserId}",
                count,
                userId);

            BotActivitySource.SetRecordsProcessed(activity, count);
            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteNotificationAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "delete",
            entityId: id.ToString());

        try
        {
            var notification = await _notificationRepository.GetByIdForUserAsync(id, userId, cancellationToken);

            if (notification == null)
            {
                _logger.LogDebug(
                    "Delete notification failed: {NotificationId} not found or not owned by user {UserId}",
                    id,
                    userId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            await _notificationRepository.DeleteAsync(notification, cancellationToken);

            _logger.LogInformation(
                "Notification {NotificationId} deleted by user {UserId}",
                id,
                userId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldNotificationsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "notification",
            "cleanup");

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var count = await _notificationRepository.DeleteOldNotificationsAsync(cutoffDate, cancellationToken);

            _logger.LogInformation(
                "Cleaned up {Count} notifications older than {RetentionDays} days",
                count,
                retentionDays);

            BotActivitySource.SetRecordsDeleted(activity, count);
            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Maps a UserNotification entity to a UserNotificationDto.
    /// </summary>
    private static UserNotificationDto MapToDto(UserNotification notification)
    {
        return new UserNotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Severity = notification.Severity,
            ActionUrl = notification.ActionUrl,
            RelatedEntityId = notification.RelatedEntityId,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            IsRead = notification.IsRead,
            TimeAgo = GetTimeAgo(notification.CreatedAt)
        };
    }

    /// <summary>
    /// Calculates a human-readable relative time string.
    /// </summary>
    private static string GetTimeAgo(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;

        if (elapsed.TotalSeconds < 60)
            return "just now";

        if (elapsed.TotalMinutes < 60)
        {
            var minutes = (int)elapsed.TotalMinutes;
            return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
        }

        if (elapsed.TotalHours < 24)
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (elapsed.TotalDays < 7)
        {
            var days = (int)elapsed.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        if (elapsed.TotalDays < 30)
        {
            var weeks = (int)(elapsed.TotalDays / 7);
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }

        var months = (int)(elapsed.TotalDays / 30);
        return months == 1 ? "1 month ago" : $"{months} months ago";
    }
}
