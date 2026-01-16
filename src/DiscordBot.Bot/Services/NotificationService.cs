using DiscordBot.Bot.Hubs;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing user notifications.
/// Handles creation, retrieval, and lifecycle management of notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BotDbContext _dbContext;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        UserManager<ApplicationUser> userManager,
        BotDbContext dbContext,
        IHubContext<DashboardHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _userManager = userManager;
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CreateForUserAsync(
        string userId,
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        AlertSeverity? severity = null,
        ulong? guildId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating notification for user {UserId}: Type={Type}, Title={Title}",
            userId, type, title);

        var notification = new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            GuildId = guildId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        };

        await _repository.AddAsync(notification, cancellationToken);

        _logger.LogInformation(
            "Created notification {NotificationId} for user {UserId}: {Title}",
            notification.Id, userId, title);

        // Broadcast notification to the user via SignalR
        await BroadcastNotificationToUserAsync(userId, notification, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CreateForAllAdminsAsync(
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        AlertSeverity? severity = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        TimeSpan? deduplicationWindow = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating notification for all admins: Type={Type}, Title={Title}",
            type, title);

        // Check for duplicate if deduplication window is specified
        if (deduplicationWindow.HasValue &&
            !string.IsNullOrEmpty(relatedEntityType) &&
            !string.IsNullOrEmpty(relatedEntityId))
        {
            var hasDuplicate = await _repository.HasRecentNotificationAsync(
                type, relatedEntityType, relatedEntityId, deduplicationWindow.Value, cancellationToken);

            if (hasDuplicate)
            {
                _logger.LogDebug(
                    "Suppressing duplicate notification: Type={Type}, EntityType={EntityType}, EntityId={EntityId}",
                    type, relatedEntityType, relatedEntityId);
                return false;
            }
        }

        // Get all users in SuperAdmin or Admin roles
        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);

        var adminUserIds = superAdmins
            .Concat(admins)
            .Select(u => u.Id)
            .Distinct()
            .ToList();

        _logger.LogDebug("Found {Count} admin users to notify", adminUserIds.Count);

        var now = DateTime.UtcNow;
        var notifications = adminUserIds.Select(userId => new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            IsRead = false,
            CreatedAt = now,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        }).ToList();

        await _repository.AddRangeAsync(notifications, cancellationToken);

        _logger.LogInformation(
            "Created {Count} notifications for admins: {Title}",
            notifications.Count, title);

        // Broadcast notifications to each admin user via SignalR (in parallel)
        var broadcastTasks = notifications.Select(n =>
            BroadcastNotificationToUserAsync(n.UserId, n, cancellationToken));
        await Task.WhenAll(broadcastTasks);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CreateForGuildAdminsAsync(
        ulong guildId,
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        TimeSpan? deduplicationWindow = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating notification for guild {GuildId} admins: Type={Type}, Title={Title}",
            guildId, type, title);

        // Check for duplicate if deduplication window is specified
        if (deduplicationWindow.HasValue &&
            !string.IsNullOrEmpty(relatedEntityType) &&
            !string.IsNullOrEmpty(relatedEntityId))
        {
            var hasDuplicate = await _repository.HasRecentNotificationAsync(
                type, relatedEntityType, relatedEntityId, deduplicationWindow.Value, cancellationToken);

            if (hasDuplicate)
            {
                _logger.LogDebug(
                    "Suppressing duplicate notification for guild {GuildId}: Type={Type}, EntityType={EntityType}, EntityId={EntityId}",
                    guildId, type, relatedEntityType, relatedEntityId);
                return false;
            }
        }

        // Get users with admin-level access to this guild
        var guildAdminUserIds = await _dbContext.UserGuildAccess
            .AsNoTracking()
            .Where(uga => uga.GuildId == guildId &&
                          (uga.AccessLevel == GuildAccessLevel.Admin || uga.AccessLevel == GuildAccessLevel.Owner))
            .Select(uga => uga.ApplicationUserId)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} guild admin users to notify for guild {GuildId}",
            guildAdminUserIds.Count, guildId);

        var now = DateTime.UtcNow;
        var notifications = guildAdminUserIds.Select(userId => new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            GuildId = guildId,
            IsRead = false,
            CreatedAt = now,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        }).ToList();

        await _repository.AddRangeAsync(notifications, cancellationToken);

        _logger.LogInformation(
            "Created {Count} notifications for guild {GuildId} admins: {Title}",
            notifications.Count, guildId, title);

        // Broadcast notifications to each guild admin user via SignalR (in parallel)
        var broadcastTasks = notifications.Select(n =>
            BroadcastNotificationToUserAsync(n.UserId, n, cancellationToken));
        await Task.WhenAll(broadcastTasks);

        return true;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserNotificationDto>> GetUserNotificationsAsync(
        string userId,
        int limit = 15,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting notifications for user {UserId}, limit: {Limit}", userId, limit);

        var notifications = await _repository.GetUserNotificationsAsync(
            userId, limit, includeRead: true, cancellationToken);

        return notifications.Select(MapToDto);
    }

    /// <inheritdoc/>
    public async Task<NotificationSummaryDto> GetSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting notification summary for user {UserId}", userId);

        return await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Marking notification {NotificationId} as read for user {UserId}",
            notificationId, userId);

        // Validate ownership
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning("Notification {NotificationId} not found", notificationId);
            return;
        }

        if (notification.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to mark notification {NotificationId} owned by {OwnerId}",
                userId, notificationId, notification.UserId);
            return;
        }

        await _repository.MarkAsReadAsync(notificationId, cancellationToken);

        // Broadcast the individual read event and updated count to the user
        await BroadcastNotificationMarkedReadAsync(userId, notificationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking all notifications as read for user {UserId}", userId);

        await _repository.MarkAllAsReadAsync(userId, cancellationToken);

        // Broadcast updated count and all-read event to the user
        await BroadcastAllNotificationsReadAsync(userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DismissAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Dismissing notification {NotificationId} for user {UserId}",
            notificationId, userId);

        // Validate ownership
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning("Notification {NotificationId} not found", notificationId);
            return;
        }

        if (notification.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to dismiss notification {NotificationId} owned by {OwnerId}",
                userId, notificationId, notification.UserId);
            return;
        }

        await _repository.DismissAsync(notificationId, cancellationToken);

        // Broadcast updated count to the user
        await BroadcastNotificationCountChangedAsync(userId, cancellationToken);
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
            TypeDisplay = GetTypeDisplayName(notification.Type),
            Severity = notification.Severity,
            Title = notification.Title,
            Message = notification.Message,
            LinkUrl = notification.LinkUrl,
            GuildId = notification.GuildId,
            GuildName = notification.Guild?.Name,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            TimeAgo = GetTimeAgo(notification.CreatedAt)
        };
    }

    /// <summary>
    /// Gets the display name for a notification type.
    /// </summary>
    private static string GetTypeDisplayName(NotificationType type)
    {
        return type switch
        {
            NotificationType.PerformanceAlert => "Performance Alert",
            NotificationType.BotStatus => "Bot Status",
            NotificationType.GuildEvent => "Guild Event",
            NotificationType.CommandError => "Command Error",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Gets a human-readable relative time string.
    /// </summary>
    private static string GetTimeAgo(DateTime createdAt)
    {
        var diff = DateTime.UtcNow - createdAt;

        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes == 1 ? "" : "s")} ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hour{((int)diff.TotalHours == 1 ? "" : "s")} ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} day{((int)diff.TotalDays == 1 ? "" : "s")} ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} week{((int)(diff.TotalDays / 7) == 1 ? "" : "s")} ago";

        return createdAt.ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Broadcasts a notification and updated count to a specific user via SignalR.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="notification">The notification entity to broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task BroadcastNotificationToUserAsync(
        string userId,
        UserNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Map the entity to DTO for transmission
            var notificationDto = MapToDto(notification);

            // Get updated summary for the user
            var summary = await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);

            // Broadcast both the new notification and the updated count
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
            // Log but don't fail the notification creation if broadcast fails
            _logger.LogWarning(
                ex,
                "Failed to broadcast notification {NotificationId} to user {UserId}",
                notification.Id,
                userId);
        }
    }

    /// <summary>
    /// Broadcasts the notification-marked-read event and updated count to a specific user via SignalR.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="notificationId">The notification ID that was marked as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task BroadcastNotificationMarkedReadAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _repository.GetUserNotificationSummaryAsync(userId, cancellationToken);

            // Broadcast the individual notification marked as read
            await _hubContext.Clients
                .User(userId)
                .SendAsync(DashboardHub.OnNotificationMarkedRead, new { notificationId }, cancellationToken);

            // Also broadcast updated count
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

    /// <summary>
    /// Broadcasts an updated notification count to a specific user via SignalR.
    /// Used when a notification is dismissed.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task BroadcastNotificationCountChangedAsync(
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

    /// <summary>
    /// Broadcasts the all-notifications-read event and updated count to a specific user via SignalR.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task BroadcastAllNotificationsReadAsync(
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

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<UserNotificationDto>> GetUserNotificationsPagedAsync(
        string userId,
        NotificationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting paged notifications for user {UserId}", userId);

        var (items, totalCount) = await _repository.GetUserNotificationsPagedAsync(
            userId, query, cancellationToken);

        return new PaginatedResponseDto<UserNotificationDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc/>
    public async Task MarkMultipleAsReadAsync(
        string userId,
        IEnumerable<Guid> notificationIds,
        CancellationToken cancellationToken = default)
    {
        var idList = notificationIds.ToList();
        _logger.LogDebug("Marking {Count} notifications as read for user {UserId}", idList.Count, userId);

        if (idList.Count == 0) return;

        // Validate ownership of all notifications via repository
        var ownedIds = await _repository.GetOwnedNotificationIdsAsync(userId, idList, cancellationToken);

        if (ownedIds.Count != idList.Count)
        {
            _logger.LogWarning(
                "User {UserId} attempted to mark notifications they don't own. Requested: {Requested}, Owned: {Owned}",
                userId, idList.Count, ownedIds.Count);
        }

        if (ownedIds.Count > 0)
        {
            await _repository.MarkMultipleAsReadAsync(ownedIds, cancellationToken);
            await BroadcastNotificationCountChangedAsync(userId, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> MarkAsUnreadAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking notification {NotificationId} as unread for user {UserId}", notificationId, userId);

        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning("Notification {NotificationId} not found", notificationId);
            return false;
        }

        if (notification.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to mark notification {NotificationId} owned by {OwnerId} as unread",
                userId, notificationId, notification.UserId);
            return false;
        }

        await _repository.MarkAsUnreadAsync(notificationId, cancellationToken);
        await BroadcastNotificationCountChangedAsync(userId, cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting notification {NotificationId} for user {UserId}", notificationId, userId);

        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning("Notification {NotificationId} not found", notificationId);
            return false;
        }

        if (notification.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete notification {NotificationId} owned by {OwnerId}",
                userId, notificationId, notification.UserId);
            return false;
        }

        await _repository.DeleteAsync(notificationId, cancellationToken);
        await BroadcastNotificationCountChangedAsync(userId, cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteMultipleAsync(
        string userId,
        IEnumerable<Guid> notificationIds,
        CancellationToken cancellationToken = default)
    {
        var idList = notificationIds.ToList();
        _logger.LogDebug("Deleting {Count} notifications for user {UserId}", idList.Count, userId);

        if (idList.Count == 0) return 0;

        // Validate ownership via repository
        var ownedIds = await _repository.GetOwnedNotificationIdsAsync(userId, idList, cancellationToken);

        if (ownedIds.Count == 0) return 0;

        if (ownedIds.Count != idList.Count)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete notifications they don't own. Requested: {Requested}, Owned: {Owned}",
                userId, idList.Count, ownedIds.Count);
        }

        var deleted = await _repository.DeleteMultipleAsync(ownedIds, cancellationToken);
        await BroadcastNotificationCountChangedAsync(userId, cancellationToken);
        return deleted;
    }
}
