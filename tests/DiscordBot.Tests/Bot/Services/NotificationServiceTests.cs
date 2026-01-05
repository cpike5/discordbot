using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for NotificationService.
/// Tests cover notification creation, retrieval, management, and DTO mapping.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _mockRepository;
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _mockRepository = new Mock<INotificationRepository>();
        _mockLogger = new Mock<ILogger<NotificationService>>();
        _service = new NotificationService(_mockRepository.Object, _mockLogger.Object);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test notification entity with sensible defaults.
    /// </summary>
    private static UserNotification CreateTestNotification(
        Guid? id = null,
        string userId = "user123",
        NotificationType type = NotificationType.PerformanceAlert,
        string title = "Test Notification",
        string message = "Test message",
        AlertSeverity severity = AlertSeverity.Info,
        bool isRead = false,
        DateTime? createdAt = null,
        DateTime? readAt = null,
        string? actionUrl = null,
        string? relatedEntityId = null)
    {
        return new UserNotification
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Severity = severity,
            IsRead = isRead,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ReadAt = readAt,
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId
        };
    }

    #endregion

    #region CreateNotificationAsync Tests

    [Fact]
    public async Task CreateNotificationAsync_CreatesNotification_WithCorrectProperties()
    {
        // Arrange
        var dto = new CreateNotificationDto
        {
            UserId = "user123",
            Type = NotificationType.PerformanceAlert,
            Title = "High CPU Usage",
            Message = "CPU usage exceeded 90% threshold",
            Severity = AlertSeverity.Warning,
            ActionUrl = "/Admin/Performance",
            RelatedEntityId = "incident-456"
        };

        UserNotification? capturedNotification = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Callback<UserNotification, CancellationToken>((n, ct) => capturedNotification = n)
            .ReturnsAsync((UserNotification n, CancellationToken ct) => n);

        // Act
        var result = await _service.CreateNotificationAsync(dto);

        // Assert
        result.Should().NotBeNull();
        capturedNotification.Should().NotBeNull();
        capturedNotification!.UserId.Should().Be("user123");
        capturedNotification.Type.Should().Be(NotificationType.PerformanceAlert);
        capturedNotification.Title.Should().Be("High CPU Usage");
        capturedNotification.Message.Should().Be("CPU usage exceeded 90% threshold");
        capturedNotification.Severity.Should().Be(AlertSeverity.Warning);
        capturedNotification.ActionUrl.Should().Be("/Admin/Performance");
        capturedNotification.RelatedEntityId.Should().Be("incident-456");
        capturedNotification.IsRead.Should().BeFalse();
        capturedNotification.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateNotificationAsync_SetsDefaultsCorrectly()
    {
        // Arrange
        var dto = new CreateNotificationDto
        {
            UserId = "user123",
            Type = NotificationType.BotStatus,
            Title = "Bot Restarted",
            Message = "Bot has been restarted",
            Severity = AlertSeverity.Info
            // ActionUrl and RelatedEntityId are null
        };

        UserNotification? capturedNotification = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Callback<UserNotification, CancellationToken>((n, ct) => capturedNotification = n)
            .ReturnsAsync((UserNotification n, CancellationToken ct) => n);

        // Act
        await _service.CreateNotificationAsync(dto);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Id.Should().NotBeEmpty();
        capturedNotification.IsRead.Should().BeFalse();
        capturedNotification.ReadAt.Should().BeNull();
        capturedNotification.ActionUrl.Should().BeNull();
        capturedNotification.RelatedEntityId.Should().BeNull();
    }

    #endregion

    #region CreateBroadcastNotificationAsync Tests

    [Fact]
    public async Task CreateBroadcastNotificationAsync_CreatesMultipleNotifications()
    {
        // Arrange
        var userIds = new[] { "user1", "user2", "user3" };

        IEnumerable<UserNotification>? capturedNotifications = null;
        _mockRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<UserNotification>, CancellationToken>((n, ct) => capturedNotifications = n.ToList())
            .ReturnsAsync((IEnumerable<UserNotification> n, CancellationToken ct) => n.Count());

        // Act
        var count = await _service.CreateBroadcastNotificationAsync(
            userIds,
            NotificationType.BotStatus,
            "Maintenance Notice",
            "System will be down for maintenance",
            AlertSeverity.Warning,
            actionUrl: "/Admin/Maintenance");

        // Assert
        count.Should().Be(3);
        capturedNotifications.Should().NotBeNull();
        capturedNotifications.Should().HaveCount(3);

        var notificationsList = capturedNotifications!.ToList();
        notificationsList.Should().AllSatisfy(n =>
        {
            n.Type.Should().Be(NotificationType.BotStatus);
            n.Title.Should().Be("Maintenance Notice");
            n.Message.Should().Be("System will be down for maintenance");
            n.Severity.Should().Be(AlertSeverity.Warning);
            n.ActionUrl.Should().Be("/Admin/Maintenance");
            n.IsRead.Should().BeFalse();
        });

        notificationsList.Select(n => n.UserId).Should().BeEquivalentTo(userIds);
    }

    [Fact]
    public async Task CreateBroadcastNotificationAsync_ReturnsZero_WhenNoUsers()
    {
        // Arrange
        var emptyUserIds = Array.Empty<string>();

        // Act
        var count = await _service.CreateBroadcastNotificationAsync(
            emptyUserIds,
            NotificationType.BotStatus,
            "Test",
            "Test message",
            AlertSeverity.Info);

        // Assert
        count.Should().Be(0);

        _mockRepository.Verify(
            r => r.CreateBatchAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "CreateBatchAsync should not be called when no users");
    }

    [Fact]
    public async Task CreateBroadcastNotificationAsync_UsesSharedTimestamp()
    {
        // Arrange
        var userIds = new[] { "user1", "user2" };

        IEnumerable<UserNotification>? capturedNotifications = null;
        _mockRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<UserNotification>, CancellationToken>((n, ct) => capturedNotifications = n.ToList())
            .ReturnsAsync((IEnumerable<UserNotification> n, CancellationToken ct) => n.Count());

        // Act
        await _service.CreateBroadcastNotificationAsync(
            userIds,
            NotificationType.GuildEvent,
            "Event",
            "Guild event occurred",
            AlertSeverity.Info);

        // Assert
        var notificationsList = capturedNotifications!.ToList();
        var timestamps = notificationsList.Select(n => n.CreatedAt).Distinct().ToList();
        timestamps.Should().HaveCount(1, "all notifications should share the same timestamp");
    }

    #endregion

    #region GetNotificationsAsync Tests

    [Fact]
    public async Task GetNotificationsAsync_ReturnsMappedDtos()
    {
        // Arrange
        const string userId = "user123";
        var query = new NotificationQueryDto { PageNumber = 1, PageSize = 20 };

        var now = DateTime.UtcNow;
        var notifications = new[]
        {
            CreateTestNotification(
                userId: userId,
                title: "Alert 1",
                createdAt: now.AddMinutes(-5),
                isRead: false),
            CreateTestNotification(
                userId: userId,
                title: "Alert 2",
                createdAt: now.AddMinutes(-10),
                isRead: true,
                readAt: now.AddMinutes(-8))
        };

        _mockRepository
            .Setup(r => r.GetByUserAsync(
                userId,
                query.PageNumber,
                query.PageSize,
                query.Type,
                query.Severity,
                query.IsRead,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((notifications, 2));

        // Act
        var result = await _service.GetNotificationsAsync(userId, query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalPages.Should().Be(1);

        var firstDto = result.Items.First();
        firstDto.Title.Should().Be("Alert 1");
        firstDto.IsRead.Should().BeFalse();
        firstDto.TimeAgo.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetNotificationsAsync_CalculatesTotalPagesCorrectly()
    {
        // Arrange
        const string userId = "user123";
        var query = new NotificationQueryDto { PageNumber = 1, PageSize = 10 };

        _mockRepository
            .Setup(r => r.GetByUserAsync(
                userId,
                query.PageNumber,
                query.PageSize,
                query.Type,
                query.Severity,
                query.IsRead,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<UserNotification>(), 25)); // 25 total items

        // Act
        var result = await _service.GetNotificationsAsync(userId, query);

        // Assert
        result.TotalPages.Should().Be(3, "25 items / 10 per page = 3 pages");
    }

    [Fact]
    public async Task GetNotificationsAsync_PassesFiltersToRepository()
    {
        // Arrange
        const string userId = "user123";
        var query = new NotificationQueryDto
        {
            PageNumber = 2,
            PageSize = 15,
            Type = NotificationType.PerformanceAlert,
            Severity = AlertSeverity.Critical,
            IsRead = false
        };

        _mockRepository
            .Setup(r => r.GetByUserAsync(
                userId,
                2,
                15,
                NotificationType.PerformanceAlert,
                AlertSeverity.Critical,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<UserNotification>(), 0));

        // Act
        await _service.GetNotificationsAsync(userId, query);

        // Assert
        _mockRepository.Verify(
            r => r.GetByUserAsync(
                userId,
                2,
                15,
                NotificationType.PerformanceAlert,
                AlertSeverity.Critical,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetNotificationAsync Tests

    [Fact]
    public async Task GetNotificationAsync_ReturnsMappedDto_WhenFound()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            title: "Test Alert");

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(notificationId);
        result.Title.Should().Be("Test Alert");
        result.TimeAgo.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetNotificationAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotification?)null);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectSummary()
    {
        // Arrange
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.GetSummaryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10, 2, 5, 3)); // unread, critical, warning, info

        var recentNotifications = new[]
        {
            CreateTestNotification(userId: userId, title: "Recent 1"),
            CreateTestNotification(userId: userId, title: "Recent 2")
        };

        _mockRepository
            .Setup(r => r.GetRecentUnreadAsync(userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentNotifications);

        // Act
        var result = await _service.GetSummaryAsync(userId, recentLimit: 5);

        // Assert
        result.Should().NotBeNull();
        result.UnreadCount.Should().Be(10);
        result.CriticalCount.Should().Be(2);
        result.WarningCount.Should().Be(5);
        result.InfoCount.Should().Be(3);
        result.RecentNotifications.Should().HaveCount(2);
        result.RecentNotifications.First().Title.Should().Be("Recent 1");
    }

    [Fact]
    public async Task GetSummaryAsync_PassesLimitToRepository()
    {
        // Arrange
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.GetSummaryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, 0, 0));

        _mockRepository
            .Setup(r => r.GetRecentUnreadAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserNotification>());

        // Act
        await _service.GetSummaryAsync(userId, recentLimit: 10);

        // Assert
        _mockRepository.Verify(
            r => r.GetRecentUnreadAsync(userId, 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetUnreadCountAsync Tests

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        // Arrange
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        // Act
        var count = await _service.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(7);

        _mockRepository.Verify(
            r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region MarkAsReadAsync Tests

    [Fact]
    public async Task MarkAsReadAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.MarkAsReadAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.MarkAsReadAsync(notificationId, userId);

        // Assert
        result.Should().BeTrue();

        _mockRepository.Verify(
            r => r.MarkAsReadAsync(notificationId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.MarkAsReadAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.MarkAsReadAsync(notificationId, userId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Fact]
    public async Task MarkAllAsReadAsync_ReturnsCount()
    {
        // Arrange
        const string userId = "user123";

        _mockRepository
            .Setup(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var count = await _service.MarkAllAsReadAsync(userId);

        // Assert
        count.Should().Be(5);

        _mockRepository.Verify(
            r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region DeleteNotificationAsync Tests

    [Fact]
    public async Task DeleteNotificationAsync_ReturnsTrue_WhenFound()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(id: notificationId, userId: userId);

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        _mockRepository
            .Setup(r => r.DeleteAsync(notification, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteNotificationAsync(notificationId, userId);

        // Assert
        result.Should().BeTrue();

        _mockRepository.Verify(
            r => r.DeleteAsync(notification, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteNotificationAsync_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotification?)null);

        // Act
        var result = await _service.DeleteNotificationAsync(notificationId, userId);

        // Assert
        result.Should().BeFalse();

        _mockRepository.Verify(
            r => r.DeleteAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "DeleteAsync should not be called when notification not found");
    }

    #endregion

    #region CleanupOldNotificationsAsync Tests

    [Fact]
    public async Task CleanupOldNotificationsAsync_CallsRepositoryWithCorrectDate()
    {
        // Arrange
        const int retentionDays = 90;
        var expectedCutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _mockRepository
            .Setup(r => r.DeleteOldNotificationsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        // Act
        var count = await _service.CleanupOldNotificationsAsync(retentionDays);

        // Assert
        count.Should().Be(15);

        _mockRepository.Verify(
            r => r.DeleteOldNotificationsAsync(
                It.Is<DateTime>(d => Math.Abs((d - expectedCutoffDate).TotalSeconds) < 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldNotificationsAsync_ReturnsDeletedCount()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.DeleteOldNotificationsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var count = await _service.CleanupOldNotificationsAsync(30);

        // Assert
        count.Should().Be(42);
    }

    #endregion

    #region TimeAgo Formatting Tests

    [Fact]
    public async Task GetNotificationAsync_FormatsTimeAgo_JustNow()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            createdAt: DateTime.UtcNow.AddSeconds(-30));

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result!.TimeAgo.Should().Be("just now");
    }

    [Fact]
    public async Task GetNotificationAsync_FormatsTimeAgo_Minutes()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            createdAt: DateTime.UtcNow.AddMinutes(-5));

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result!.TimeAgo.Should().Be("5 minutes ago");
    }

    [Fact]
    public async Task GetNotificationAsync_FormatsTimeAgo_Hours()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            createdAt: DateTime.UtcNow.AddHours(-3));

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result!.TimeAgo.Should().Be("3 hours ago");
    }

    [Fact]
    public async Task GetNotificationAsync_FormatsTimeAgo_Days()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            createdAt: DateTime.UtcNow.AddDays(-2));

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result!.TimeAgo.Should().Be("2 days ago");
    }

    [Fact]
    public async Task GetNotificationAsync_FormatsTimeAgo_Weeks()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            createdAt: DateTime.UtcNow.AddDays(-14));

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result!.TimeAgo.Should().Be("2 weeks ago");
    }

    [Fact]
    public async Task GetNotificationAsync_FormatsTimeAgo_Months()
    {
        // Arrange
        const string userId = "user123";
        var notificationId = Guid.NewGuid();
        var notification = CreateTestNotification(
            id: notificationId,
            userId: userId,
            createdAt: DateTime.UtcNow.AddDays(-60));

        _mockRepository
            .Setup(r => r.GetByIdForUserAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.GetNotificationAsync(notificationId, userId);

        // Assert
        result!.TimeAgo.Should().Be("2 months ago");
    }

    #endregion
}
