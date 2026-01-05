using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserNotification entities with notification-specific operations.
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
    /// <remarks>
    /// Overrides base implementation to include User navigation property.
    /// </remarks>
    public override async Task<UserNotification?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for UserNotification: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == guidId, cancellationToken);

        _logger.LogDebug("Notification {Id} found: {Found}", id, result != null);
        return result;
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<UserNotification> Items, int TotalCount)> GetByUserAsync(
        string userId,
        int page,
        int pageSize,
        NotificationType? type = null,
        AlertSeverity? severity = null,
        bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving notifications for user {UserId}, page {Page}, pageSize {PageSize}, type {Type}, severity {Severity}, isRead {IsRead}",
            userId, page, pageSize, type, severity, isRead);

        var query = DbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        // Apply filters
        if (type.HasValue)
        {
            query = query.Where(n => n.Type == type.Value);
        }

        if (severity.HasValue)
        {
            query = query.Where(n => n.Severity == severity.Value);
        }

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} notifications for user {UserId} out of {TotalCount} total",
            items.Count, userId, totalCount);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting unread notifications for user {UserId}", userId);

        var count = await DbSet
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        _logger.LogDebug("User {UserId} has {Count} unread notifications", userId, count);
        return count;
    }

    /// <inheritdoc/>
    public async Task<(int UnreadCount, int CriticalCount, int WarningCount, int InfoCount)> GetSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification summary for user {UserId}", userId);

        var unreadQuery = DbSet.AsNoTracking().Where(n => n.UserId == userId && !n.IsRead);

        var unreadCount = await unreadQuery.CountAsync(cancellationToken);
        var criticalCount = await unreadQuery.CountAsync(n => n.Severity == AlertSeverity.Critical, cancellationToken);
        var warningCount = await unreadQuery.CountAsync(n => n.Severity == AlertSeverity.Warning, cancellationToken);
        var infoCount = await unreadQuery.CountAsync(n => n.Severity == AlertSeverity.Info, cancellationToken);

        _logger.LogDebug(
            "User {UserId} notification summary: Unread={Unread}, Critical={Critical}, Warning={Warning}, Info={Info}",
            userId, unreadCount, criticalCount, warningCount, infoCount);

        return (unreadCount, criticalCount, warningCount, infoCount);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserNotification>> GetRecentUnreadAsync(
        string userId,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} recent unread notifications for user {UserId}", limit, userId);

        var items = await DbSet
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} recent unread notifications for user {UserId}", items.Count, userId);
        return items;
    }

    /// <inheritdoc/>
    public async Task<UserNotification?> GetByIdForUserAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification {Id} for user {UserId}", id, userId);

        var result = await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        _logger.LogDebug("Notification {Id} for user {UserId} found: {Found}", id, userId, result != null);
        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> MarkAsReadAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking notification {Id} as read for user {UserId}", id, userId);

        var notification = await DbSet
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (notification == null)
        {
            _logger.LogDebug("Notification {Id} not found for user {UserId}", id, userId);
            return false;
        }

        if (notification.IsRead)
        {
            _logger.LogDebug("Notification {Id} already marked as read", id);
            return true;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification {Id} marked as read for user {UserId}", id, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking all notifications as read for user {UserId}", userId);

        var now = DateTime.UtcNow;
        var count = await DbSet
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now),
                cancellationToken);

        _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", count, userId);
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOldNotificationsAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting notifications older than {OlderThan}", olderThan);

        var count = await DbSet
            .Where(n => n.CreatedAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} notifications older than {OlderThan}", count, olderThan);
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> CreateBatchAsync(
        IEnumerable<UserNotification> notifications,
        CancellationToken cancellationToken = default)
    {
        var notificationList = notifications.ToList();
        _logger.LogDebug("Creating batch of {Count} notifications", notificationList.Count);

        if (notificationList.Count == 0)
        {
            return 0;
        }

        await DbSet.AddRangeAsync(notificationList, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created batch of {Count} notifications", notificationList.Count);
        return notificationList.Count;
    }
}
