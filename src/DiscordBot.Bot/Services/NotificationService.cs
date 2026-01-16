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
    public async Task CreateForAllAdminsAsync(
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        AlertSeverity? severity = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating notification for all admins: Type={Type}, Title={Title}",
            type, title);

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

        // Broadcast notifications to each admin user via SignalR
        foreach (var notification in notifications)
        {
            await BroadcastNotificationToUserAsync(notification.UserId, notification, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task CreateForGuildAdminsAsync(
        ulong guildId,
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating notification for guild {GuildId} admins: Type={Type}, Title={Title}",
            guildId, type, title);

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

        // Broadcast notifications to each guild admin user via SignalR
        foreach (var notification in notifications)
        {
            await BroadcastNotificationToUserAsync(notification.UserId, notification, cancellationToken);
        }
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
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking all notifications as read for user {UserId}", userId);

        await _repository.MarkAllAsReadAsync(userId, cancellationToken);
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
}
