using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for NotificationRepository.
/// Tests cover CRUD operations, pagination, filtering, and notification-specific operations.
/// </summary>
public class NotificationRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly NotificationRepository _repository;
    private readonly Mock<ILogger<NotificationRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<UserNotification>>> _mockBaseLogger;

    public NotificationRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<NotificationRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<UserNotification>>>();
        _repository = new NotificationRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test notification with sensible defaults.
    /// </summary>
    private static UserNotification CreateTestNotification(
        string userId = "user123",
        NotificationType type = NotificationType.PerformanceAlert,
        string title = "Test Notification",
        string message = "Test message",
        AlertSeverity severity = AlertSeverity.Info,
        bool isRead = false,
        DateTime? createdAt = null,
        string? actionUrl = null,
        string? relatedEntityId = null)
    {
        return new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Severity = severity,
            IsRead = isRead,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ReadAt = isRead ? DateTime.UtcNow : null,
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId
        };
    }

    /// <summary>
    /// Creates a test ApplicationUser record to satisfy foreign key constraints.
    /// </summary>
    private async Task EnsureUserExistsAsync(string userId)
    {
        var existingUser = await _context.Set<ApplicationUser>().FindAsync(userId);
        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"testuser{userId}@example.com",
                Email = $"testuser{userId}@example.com",
                EmailConfirmed = true
            };
            _context.Set<ApplicationUser>().Add(user);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Helper method to add a notification and ensure its user exists.
    /// </summary>
    private async Task AddNotificationAsync(UserNotification notification)
    {
        await EnsureUserExistsAsync(notification.UserId);
        await _context.UserNotifications.AddAsync(notification);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Helper method to add multiple notifications and ensure their users exist.
    /// </summary>
    private async Task AddNotificationsAsync(IEnumerable<UserNotification> notifications)
    {
        var notificationList = notifications.ToList();
        var uniqueUserIds = notificationList.Select(n => n.UserId).Distinct();

        foreach (var userId in uniqueUserIds)
        {
            await EnsureUserExistsAsync(userId);
        }

        await _context.UserNotifications.AddRangeAsync(notificationList);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region CRUD Operations Tests

    [Fact]
    public async Task AddAsync_CreatesNotification_WhenValid()
    {
        // Arrange
        await EnsureUserExistsAsync("user123");
        var notification = CreateTestNotification(
            title: "Performance Alert",
            message: "CPU usage exceeded 90%",
            severity: AlertSeverity.Warning,
            actionUrl: "/Admin/Performance");

        // Act
        var result = await _repository.AddAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.UserId.Should().Be("user123");
        result.Title.Should().Be("Performance Alert");
        result.Message.Should().Be("CPU usage exceeded 90%");
        result.Severity.Should().Be(AlertSeverity.Warning);
        result.IsRead.Should().BeFalse();
        result.ActionUrl.Should().Be("/Admin/Performance");

        // Verify it was saved to the database
        var saved = await _context.UserNotifications.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Performance Alert");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotification_WhenExists()
    {
        // Arrange
        var notification = CreateTestNotification(title: "Test Alert");
        await AddNotificationAsync(notification);

        // Act
        var result = await _repository.GetByIdAsync(notification.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(notification.Id);
        result.Title.Should().Be("Test Alert");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesNotification()
    {
        // Arrange
        var notification = CreateTestNotification();
        await AddNotificationAsync(notification);

        // Act
        await _repository.DeleteAsync(notification);

        // Assert
        var deleted = await _context.UserNotifications.FindAsync(notification.Id);
        deleted.Should().BeNull();
    }

    #endregion

    #region GetByUserAsync Tests

    [Fact]
    public async Task GetByUserAsync_ReturnsCorrectNotifications_ForUser()
    {
        // Arrange
        const string userId = "user123";
        const string otherUserId = "user456";

        var userNotifications = new[]
        {
            CreateTestNotification(userId: userId, title: "Notification 1"),
            CreateTestNotification(userId: userId, title: "Notification 2"),
            CreateTestNotification(userId: userId, title: "Notification 3")
        };

        var otherUserNotification = CreateTestNotification(userId: otherUserId, title: "Other User");

        await AddNotificationsAsync(userNotifications);
        await AddNotificationAsync(otherUserNotification);

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(userId, page: 1, pageSize: 10);

        // Assert
        items.Should().HaveCount(3);
        totalCount.Should().Be(3);
        items.Should().AllSatisfy(n => n.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetByUserAsync_RespectsPagination()
    {
        // Arrange
        const string userId = "user123";
        var notifications = Enumerable.Range(1, 25)
            .Select(i => CreateTestNotification(userId: userId, title: $"Notification {i}"))
            .ToList();

        await AddNotificationsAsync(notifications);

        // Act - Get page 1
        var (page1Items, page1Total) = await _repository.GetByUserAsync(userId, page: 1, pageSize: 10);

        // Act - Get page 2
        var (page2Items, page2Total) = await _repository.GetByUserAsync(userId, page: 2, pageSize: 10);

        // Assert
        page1Items.Should().HaveCount(10);
        page1Total.Should().Be(25);

        page2Items.Should().HaveCount(10);
        page2Total.Should().Be(25);

        // Verify pages don't overlap
        var page1Ids = page1Items.Select(n => n.Id).ToHashSet();
        var page2Ids = page2Items.Select(n => n.Id).ToHashSet();
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    [Fact]
    public async Task GetByUserAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        const string userId = "user123";
        var now = DateTime.UtcNow;

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, title: "Oldest", createdAt: now.AddHours(-3)),
            CreateTestNotification(userId: userId, title: "Middle", createdAt: now.AddHours(-2)),
            CreateTestNotification(userId: userId, title: "Newest", createdAt: now.AddHours(-1))
        };

        await AddNotificationsAsync(notifications);

        // Act
        var (items, _) = await _repository.GetByUserAsync(userId, page: 1, pageSize: 10);

        // Assert
        items.Should().HaveCount(3);
        items.First().Title.Should().Be("Newest");
        items.Last().Title.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetByUserAsync_FiltersBy_Type()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, type: NotificationType.PerformanceAlert, title: "Alert 1"),
            CreateTestNotification(userId: userId, type: NotificationType.BotStatus, title: "Status 1"),
            CreateTestNotification(userId: userId, type: NotificationType.PerformanceAlert, title: "Alert 2")
        };

        await AddNotificationsAsync(notifications);

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(
            userId, page: 1, pageSize: 10, type: NotificationType.PerformanceAlert);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(n => n.Type.Should().Be(NotificationType.PerformanceAlert));
    }

    [Fact]
    public async Task GetByUserAsync_FiltersBy_Severity()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, title: "Critical 1"),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Warning, title: "Warning 1"),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, title: "Critical 2")
        };

        await AddNotificationsAsync(notifications);

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(
            userId, page: 1, pageSize: 10, severity: AlertSeverity.Critical);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(n => n.Severity.Should().Be(AlertSeverity.Critical));
    }

    [Fact]
    public async Task GetByUserAsync_FiltersBy_IsRead()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: true, title: "Read 1"),
            CreateTestNotification(userId: userId, isRead: false, title: "Unread 1"),
            CreateTestNotification(userId: userId, isRead: false, title: "Unread 2")
        };

        await AddNotificationsAsync(notifications);

        // Act - Get unread only
        var (items, totalCount) = await _repository.GetByUserAsync(
            userId, page: 1, pageSize: 10, isRead: false);

        // Assert
        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(n => n.IsRead.Should().BeFalse());
    }

    [Fact]
    public async Task GetByUserAsync_AppliesMultipleFilters()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, type: NotificationType.PerformanceAlert,
                severity: AlertSeverity.Critical, isRead: false, title: "Match"),
            CreateTestNotification(userId: userId, type: NotificationType.PerformanceAlert,
                severity: AlertSeverity.Warning, isRead: false, title: "Wrong Severity"),
            CreateTestNotification(userId: userId, type: NotificationType.BotStatus,
                severity: AlertSeverity.Critical, isRead: false, title: "Wrong Type"),
            CreateTestNotification(userId: userId, type: NotificationType.PerformanceAlert,
                severity: AlertSeverity.Critical, isRead: true, title: "Already Read")
        };

        await AddNotificationsAsync(notifications);

        // Act
        var (items, totalCount) = await _repository.GetByUserAsync(
            userId,
            page: 1,
            pageSize: 10,
            type: NotificationType.PerformanceAlert,
            severity: AlertSeverity.Critical,
            isRead: false);

        // Assert
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items.First().Title.Should().Be("Match");
    }

    #endregion

    #region GetUnreadCountAsync Tests

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: userId, isRead: true)
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsZero_WhenNoUnread()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: true),
            CreateTestNotification(userId: userId, isRead: true)
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCountAsync_OnlyCountsCurrentUser()
    {
        // Arrange
        const string userId = "user123";
        const string otherUserId = "user456";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: otherUserId, isRead: false)
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Warning, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Info, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Info, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Info, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, isRead: true) // Read, should not count
        };

        await AddNotificationsAsync(notifications);

        // Act
        var (unreadCount, criticalCount, warningCount, infoCount) = await _repository.GetSummaryAsync(userId);

        // Assert
        unreadCount.Should().Be(6);
        criticalCount.Should().Be(2);
        warningCount.Should().Be(1);
        infoCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsZeros_WhenNoNotifications()
    {
        // Arrange
        const string userId = "user123";

        // Act
        var (unreadCount, criticalCount, warningCount, infoCount) = await _repository.GetSummaryAsync(userId);

        // Assert
        unreadCount.Should().Be(0);
        criticalCount.Should().Be(0);
        warningCount.Should().Be(0);
        infoCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_OnlyCountsUnread()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, isRead: false),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Critical, isRead: true),
            CreateTestNotification(userId: userId, severity: AlertSeverity.Warning, isRead: true)
        };

        await AddNotificationsAsync(notifications);

        // Act
        var (unreadCount, criticalCount, warningCount, infoCount) = await _repository.GetSummaryAsync(userId);

        // Assert
        unreadCount.Should().Be(1);
        criticalCount.Should().Be(1);
        warningCount.Should().Be(0);
    }

    #endregion

    #region GetRecentUnreadAsync Tests

    [Fact]
    public async Task GetRecentUnreadAsync_ReturnsRecentUnread()
    {
        // Arrange
        const string userId = "user123";
        var now = DateTime.UtcNow;

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: false, createdAt: now.AddMinutes(-5), title: "Recent 1"),
            CreateTestNotification(userId: userId, isRead: false, createdAt: now.AddMinutes(-10), title: "Recent 2"),
            CreateTestNotification(userId: userId, isRead: false, createdAt: now.AddMinutes(-15), title: "Recent 3"),
            CreateTestNotification(userId: userId, isRead: true, createdAt: now.AddMinutes(-2), title: "Read") // Should not appear
        };

        await AddNotificationsAsync(notifications);

        // Act
        var items = await _repository.GetRecentUnreadAsync(userId, limit: 5);

        // Assert
        items.Should().HaveCount(3);
        items.Should().AllSatisfy(n => n.IsRead.Should().BeFalse());
        items.First().Title.Should().Be("Recent 1"); // Most recent first
    }

    [Fact]
    public async Task GetRecentUnreadAsync_RespectsLimit()
    {
        // Arrange
        const string userId = "user123";
        var notifications = Enumerable.Range(1, 10)
            .Select(i => CreateTestNotification(userId: userId, isRead: false, title: $"Notification {i}"))
            .ToList();

        await AddNotificationsAsync(notifications);

        // Act
        var items = await _repository.GetRecentUnreadAsync(userId, limit: 5);

        // Assert
        items.Should().HaveCount(5);
    }

    #endregion

    #region GetByIdForUserAsync Tests

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsNotification_WhenOwnedByUser()
    {
        // Arrange
        const string userId = "user123";
        var notification = CreateTestNotification(userId: userId, title: "My Notification");
        await AddNotificationAsync(notification);

        // Act
        var result = await _repository.GetByIdForUserAsync(notification.Id, userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(notification.Id);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsNull_WhenOwnedByDifferentUser()
    {
        // Arrange
        const string userId = "user123";
        const string otherUserId = "user456";
        var notification = CreateTestNotification(userId: otherUserId, title: "Other User's Notification");
        await AddNotificationAsync(notification);

        // Act
        var result = await _repository.GetByIdForUserAsync(notification.Id, userId);

        // Assert
        result.Should().BeNull("notification belongs to different user");
    }

    #endregion

    #region MarkAsReadAsync Tests

    [Fact]
    public async Task MarkAsReadAsync_MarksNotificationAsRead()
    {
        // Arrange
        const string userId = "user123";
        var notification = CreateTestNotification(userId: userId, isRead: false);
        await AddNotificationAsync(notification);

        var beforeMark = DateTime.UtcNow;

        // Act
        var result = await _repository.MarkAsReadAsync(notification.Id, userId);

        // Assert
        result.Should().BeTrue();

        var updated = await _context.UserNotifications.FindAsync(notification.Id);
        updated.Should().NotBeNull();
        updated!.IsRead.Should().BeTrue();
        updated.ReadAt.Should().NotBeNull();
        updated.ReadAt.Should().BeOnOrAfter(beforeMark);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        const string userId = "user123";
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.MarkAsReadAsync(nonExistentId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsFalse_WhenOwnedByDifferentUser()
    {
        // Arrange
        const string userId = "user123";
        const string otherUserId = "user456";
        var notification = CreateTestNotification(userId: otherUserId, isRead: false);
        await AddNotificationAsync(notification);

        // Act
        var result = await _repository.MarkAsReadAsync(notification.Id, userId);

        // Assert
        result.Should().BeFalse("notification belongs to different user");

        var unchanged = await _context.UserNotifications.FindAsync(notification.Id);
        unchanged!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsTrue_WhenAlreadyRead()
    {
        // Arrange
        const string userId = "user123";
        var notification = CreateTestNotification(userId: userId, isRead: true);
        await AddNotificationAsync(notification);

        // Act
        var result = await _repository.MarkAsReadAsync(notification.Id, userId);

        // Assert
        result.Should().BeTrue("already marked as read should still succeed");
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnreadAsRead()
    {
        // Arrange
        const string userId = "user123";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: userId, isRead: true) // Already read
        };

        await AddNotificationsAsync(notifications);

        var beforeMark = DateTime.UtcNow;

        // Act
        var count = await _repository.MarkAllAsReadAsync(userId);

        // Assert
        count.Should().Be(2, "only unread notifications should be marked");

        // Reload from database to get updated state
        _context.ChangeTracker.Clear();
        var allNotifications = _context.UserNotifications.Where(n => n.UserId == userId).ToList();
        allNotifications.Should().AllSatisfy(n => n.IsRead.Should().BeTrue());
        allNotifications.Where(n => n.ReadAt >= beforeMark).Should().HaveCount(2);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_OnlyAffectsCurrentUser()
    {
        // Arrange
        const string userId = "user123";
        const string otherUserId = "user456";

        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: false),
            CreateTestNotification(userId: otherUserId, isRead: false)
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.MarkAllAsReadAsync(userId);

        // Assert
        count.Should().Be(1);

        var otherUserNotification = _context.UserNotifications.First(n => n.UserId == otherUserId);
        otherUserNotification.IsRead.Should().BeFalse("other user's notifications should not be affected");
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ReturnsZero_WhenAllAlreadyRead()
    {
        // Arrange
        const string userId = "user123";
        var notifications = new[]
        {
            CreateTestNotification(userId: userId, isRead: true),
            CreateTestNotification(userId: userId, isRead: true)
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.MarkAllAsReadAsync(userId);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region DeleteOldNotificationsAsync Tests

    [Fact]
    public async Task DeleteOldNotificationsAsync_DeletesOldNotifications()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var cutoffDate = now.AddDays(-30);

        var notifications = new[]
        {
            CreateTestNotification(createdAt: now.AddDays(-31), title: "Old 1"), // Should be deleted
            CreateTestNotification(createdAt: now.AddDays(-45), title: "Old 2"), // Should be deleted
            CreateTestNotification(createdAt: now.AddDays(-29), title: "Recent 1"), // Should remain
            CreateTestNotification(createdAt: now.AddDays(-1), title: "Recent 2") // Should remain
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.DeleteOldNotificationsAsync(cutoffDate);

        // Assert
        count.Should().Be(2);

        var remaining = _context.UserNotifications.ToList();
        remaining.Should().HaveCount(2);
        remaining.Should().AllSatisfy(n => n.CreatedAt.Should().BeOnOrAfter(cutoffDate));
    }

    [Fact]
    public async Task DeleteOldNotificationsAsync_ReturnsZero_WhenNoOldNotifications()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var cutoffDate = now.AddDays(-30);

        var notifications = new[]
        {
            CreateTestNotification(createdAt: now.AddDays(-1)),
            CreateTestNotification(createdAt: now.AddDays(-5))
        };

        await AddNotificationsAsync(notifications);

        // Act
        var count = await _repository.DeleteOldNotificationsAsync(cutoffDate);

        // Assert
        count.Should().Be(0);
        _context.UserNotifications.Should().HaveCount(2);
    }

    #endregion

    #region CreateBatchAsync Tests

    [Fact]
    public async Task CreateBatchAsync_CreatesMultipleNotifications()
    {
        // Arrange
        await EnsureUserExistsAsync("user1");
        await EnsureUserExistsAsync("user2");
        await EnsureUserExistsAsync("user3");

        var notifications = new[]
        {
            CreateTestNotification(userId: "user1", title: "Notification 1"),
            CreateTestNotification(userId: "user2", title: "Notification 2"),
            CreateTestNotification(userId: "user3", title: "Notification 3")
        };

        // Act
        var count = await _repository.CreateBatchAsync(notifications);

        // Assert
        count.Should().Be(3);

        var saved = _context.UserNotifications.ToList();
        saved.Should().HaveCount(3);
        saved.Select(n => n.Title).Should().Contain(new[] { "Notification 1", "Notification 2", "Notification 3" });
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsZero_WhenEmptyCollection()
    {
        // Arrange
        var notifications = Array.Empty<UserNotification>();

        // Act
        var count = await _repository.CreateBatchAsync(notifications);

        // Assert
        count.Should().Be(0);
        _context.UserNotifications.Should().BeEmpty();
    }

    #endregion
}
