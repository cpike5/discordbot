using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserNotification entities.
/// Provides methods for retrieving, managing, and cleaning up user notifications.
/// </summary>
public class NotificationRepository : Repository<UserNotification>, INotificationRepository
{
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(
        BotDbContext context,
        ILogger<NotificationRepository> logger,
        ILogger<Repository<UserNotification>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserNotification>> GetUserNotificationsAsync(
        string userId,
        int limit = 15,
        bool includeRead = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving notifications for user {UserId}, limit: {Limit}, includeRead: {IncludeRead}",
            userId, limit, includeRead);

        var query = DbSet
            .AsNoTracking()
            .Include(n => n.Guild)
            .Where(n => n.UserId == userId && n.DismissedAt == null);

        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} notifications for user {UserId}",
            notifications.Count, userId);

        return notifications;
    }

    /// <inheritdoc/>
    public async Task<NotificationSummaryDto> GetUserNotificationSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification summary for user {UserId}", userId);

        var unreadQuery = DbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead && n.DismissedAt == null);

        var totalUnread = await unreadQuery.CountAsync(cancellationToken);

        // Get counts by type
        var byType = await unreadQuery
            .GroupBy(n => n.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Get counts by severity (for notifications that have severity)
        var bySeverity = await unreadQuery
            .Where(n => n.Severity != null)
            .GroupBy(n => n.Severity!.Value)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var summary = new NotificationSummaryDto
        {
            TotalUnread = totalUnread,
            PerformanceAlertCount = byType.FirstOrDefault(t => t.Type == NotificationType.PerformanceAlert)?.Count ?? 0,
            BotStatusCount = byType.FirstOrDefault(t => t.Type == NotificationType.BotStatus)?.Count ?? 0,
            GuildEventCount = byType.FirstOrDefault(t => t.Type == NotificationType.GuildEvent)?.Count ?? 0,
            CommandErrorCount = byType.FirstOrDefault(t => t.Type == NotificationType.CommandError)?.Count ?? 0,
            CriticalCount = bySeverity.FirstOrDefault(s => s.Severity == AlertSeverity.Critical)?.Count ?? 0,
            WarningCount = bySeverity.FirstOrDefault(s => s.Severity == AlertSeverity.Warning)?.Count ?? 0
        };

        _logger.LogDebug(
            "Notification summary for user {UserId}: TotalUnread={TotalUnread}, Critical={Critical}, Warning={Warning}",
            userId, summary.TotalUnread, summary.CriticalCount, summary.WarningCount);

        return summary;
    }

    /// <inheritdoc/>
    public async Task<UserNotification?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification by ID: {NotificationId}", id);

        var notification = await DbSet
            .Include(n => n.Guild)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification == null)
        {
            _logger.LogDebug("Notification not found: {NotificationId}", id);
        }

        return notification;
    }

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking notification as read: {NotificationId}", id);

        var notification = await DbSet.FindAsync(new object[] { id }, cancellationToken);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await Context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Notification marked as read: {NotificationId}", id);
        }
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking all notifications as read for user: {UserId}", userId);

        var now = DateTime.UtcNow;
        var unreadNotifications = await DbSet
            .Where(n => n.UserId == userId && !n.IsRead && n.DismissedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked {Count} notifications as read for user {UserId}",
            unreadNotifications.Count, userId);
    }

    /// <inheritdoc/>
    public async Task DismissAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Dismissing notification: {NotificationId}", id);

        var notification = await DbSet.FindAsync(new object[] { id }, cancellationToken);
        if (notification != null && notification.DismissedAt == null)
        {
            notification.DismissedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Notification dismissed: {NotificationId}", id);
        }
    }

    /// <inheritdoc/>
    public async Task<int> DeleteDismissedOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting dismissed notifications older than {Cutoff} (batch size: {BatchSize})",
            cutoff, batchSize);

        // Get the IDs of dismissed records to delete
        var idsToDelete = await DbSet
            .AsNoTracking()
            .Where(n => n.DismissedAt != null && n.DismissedAt < cutoff)
            .OrderBy(n => n.DismissedAt)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        if (idsToDelete.Count == 0)
        {
            _logger.LogDebug("No dismissed notifications found older than {Cutoff}", cutoff);
            return 0;
        }

        // Delete the batch
        var deleted = await DbSet
            .Where(n => idsToDelete.Contains(n.Id))
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} dismissed notifications", deleted);
        return deleted;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteReadOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting read notifications older than {Cutoff} (batch size: {BatchSize})",
            cutoff, batchSize);

        // Get the IDs of read (but not dismissed) records to delete
        var idsToDelete = await DbSet
            .AsNoTracking()
            .Where(n => n.IsRead && n.DismissedAt == null && n.CreatedAt < cutoff)
            .OrderBy(n => n.CreatedAt)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        if (idsToDelete.Count == 0)
        {
            _logger.LogDebug("No read notifications found older than {Cutoff}", cutoff);
            return 0;
        }

        // Delete the batch
        var deleted = await DbSet
            .Where(n => idsToDelete.Contains(n.Id))
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} read notifications", deleted);
        return deleted;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteUnreadOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting unread notifications older than {Cutoff} (batch size: {BatchSize})",
            cutoff, batchSize);

        // Get the IDs of unread (and not dismissed) records to delete
        var idsToDelete = await DbSet
            .AsNoTracking()
            .Where(n => !n.IsRead && n.DismissedAt == null && n.CreatedAt < cutoff)
            .OrderBy(n => n.CreatedAt)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        if (idsToDelete.Count == 0)
        {
            _logger.LogDebug("No unread notifications found older than {Cutoff}", cutoff);
            return 0;
        }

        // Delete the batch
        var deleted = await DbSet
            .Where(n => idsToDelete.Contains(n.Id))
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} unread notifications", deleted);
        return deleted;
    }

    /// <inheritdoc/>
    public async Task AddRangeAsync(
        IEnumerable<UserNotification> notifications,
        CancellationToken cancellationToken = default)
    {
        var notificationList = notifications.ToList();
        if (notificationList.Count == 0)
        {
            _logger.LogDebug("AddRangeAsync called with empty collection, skipping");
            return;
        }

        _logger.LogDebug("Adding {Count} notifications in bulk", notificationList.Count);

        await DbSet.AddRangeAsync(notificationList, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added {Count} notifications in bulk", notificationList.Count);
    }

    /// <inheritdoc/>
    public async Task<bool> HasRecentNotificationAsync(
        NotificationType type,
        string? relatedEntityType,
        string? relatedEntityId,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - window;

        _logger.LogDebug(
            "Checking for recent notification: Type={Type}, EntityType={EntityType}, EntityId={EntityId}, Window={Window}",
            type, relatedEntityType, relatedEntityId, window);

        var exists = await DbSet
            .AsNoTracking()
            .AnyAsync(n =>
                n.Type == type &&
                n.RelatedEntityType == relatedEntityType &&
                n.RelatedEntityId == relatedEntityId &&
                n.CreatedAt >= cutoff &&
                n.DismissedAt == null,
                cancellationToken);

        _logger.LogDebug(
            "Recent notification check result: {Exists} for Type={Type}, EntityType={EntityType}, EntityId={EntityId}",
            exists, type, relatedEntityType, relatedEntityId);

        return exists;
    }
}
