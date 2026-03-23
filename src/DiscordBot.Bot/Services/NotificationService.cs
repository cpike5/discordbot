using DiscordBot.Bot.Services.Notifications;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing user notifications.
/// Handles creation, retrieval, and lifecycle management of notifications.
/// Broadcasting is delegated to <see cref="INotificationBroadcaster"/>
/// and mapping to <see cref="NotificationMapper"/>.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BotDbContext _dbContext;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        UserManager<ApplicationUser> userManager,
        BotDbContext dbContext,
        INotificationBroadcaster broadcaster,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _userManager = userManager;
        _dbContext = dbContext;
        _broadcaster = broadcaster;
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

        await _broadcaster.BroadcastNotificationAsync(userId, notification, cancellationToken);
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

        // Broadcast notifications to each admin user (sequentially -- DbContext is not thread-safe)
        foreach (var n in notifications)
        {
            await _broadcaster.BroadcastNotificationAsync(n.UserId, n, cancellationToken);
        }

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

        // Broadcast notifications to each guild admin user (sequentially -- DbContext is not thread-safe)
        foreach (var n in notifications)
        {
            await _broadcaster.BroadcastNotificationAsync(n.UserId, n, cancellationToken);
        }

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

        return notifications.Select(NotificationMapper.ToDto);
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

        await _broadcaster.BroadcastNotificationMarkedReadAsync(userId, notificationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking all notifications as read for user {UserId}", userId);

        await _repository.MarkAllAsReadAsync(userId, cancellationToken);

        await _broadcaster.BroadcastAllReadAsync(userId, cancellationToken);
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

        await _broadcaster.BroadcastCountChangedAsync(userId, cancellationToken);
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
            Items = items.Select(NotificationMapper.ToDto).ToList(),
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
            await _broadcaster.BroadcastCountChangedAsync(userId, cancellationToken);
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
        await _broadcaster.BroadcastCountChangedAsync(userId, cancellationToken);
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
        await _broadcaster.BroadcastCountChangedAsync(userId, cancellationToken);
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

        var ownedIds = await _repository.GetOwnedNotificationIdsAsync(userId, idList, cancellationToken);

        if (ownedIds.Count == 0) return 0;

        if (ownedIds.Count != idList.Count)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete notifications they don't own. Requested: {Requested}, Owned: {Owned}",
                userId, idList.Count, ownedIds.Count);
        }

        var deleted = await _repository.DeleteMultipleAsync(ownedIds, cancellationToken);
        await _broadcaster.BroadcastCountChangedAsync(userId, cancellationToken);
        return deleted;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAllAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting all notifications for user {UserId}", userId);
        var deleted = await _repository.DeleteAllByUserAsync(userId, cancellationToken);
        await _broadcaster.BroadcastCountChangedAsync(userId, cancellationToken);
        return deleted;
    }
}
