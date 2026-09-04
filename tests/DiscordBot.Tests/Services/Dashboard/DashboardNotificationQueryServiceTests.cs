using DiscordBot.Bot.Services.Dashboard;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Dashboard;

/// <summary>
/// Unit tests for <see cref="DashboardNotificationQueryService"/>.
/// Moved out of DashboardHubTests when DashboardHub was split into per-feature services.
/// The authenticated-user short-circuit itself stayed in DashboardHubTests since that check
/// still lives in the hub.
/// </summary>
public class DashboardNotificationQueryServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<DashboardNotificationQueryService>> _mockLogger;
    private readonly DashboardNotificationQueryService _service;

    private const string ConnectionId = "test-connection-id-123";
    private const string UserId = "test-user-id-123";

    public DashboardNotificationQueryServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<DashboardNotificationQueryService>>();

        // Setup service provider to return notification service via scope
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(mockServiceScopeFactory.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(INotificationService))).Returns(_mockNotificationService.Object);

        _service = new DashboardNotificationQueryService(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetNotificationSummary_ShouldReturnSummary()
    {
        // Arrange
        var expectedSummary = new NotificationSummaryDto
        {
            TotalUnread = 5,
            PerformanceAlertCount = 2,
            BotStatusCount = 1,
            GuildEventCount = 1,
            CommandErrorCount = 1,
            CriticalCount = 1,
            WarningCount = 2
        };

        _mockNotificationService
            .Setup(s => s.GetSummaryAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _service.GetNotificationSummaryAsync(UserId, ConnectionId);

        // Assert
        result.Should().NotBeNull();
        result.TotalUnread.Should().Be(5);
        result.PerformanceAlertCount.Should().Be(2);
        result.CriticalCount.Should().Be(1);

        _mockNotificationService.Verify(
            s => s.GetSummaryAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should call notification service with user ID");
    }

    [Fact]
    public async Task GetNotifications_ShouldReturnNotifications()
    {
        // Arrange
        var expectedNotifications = new List<UserNotificationDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Test Alert 1", Message = "Test message 1" },
            new() { Id = Guid.NewGuid(), Title = "Test Alert 2", Message = "Test message 2" }
        };

        _mockNotificationService
            .Setup(s => s.GetUserNotificationsAsync(UserId, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedNotifications);

        // Act
        var result = await _service.GetNotificationsAsync(UserId, ConnectionId, 15);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        _mockNotificationService.Verify(
            s => s.GetUserNotificationsAsync(UserId, 15, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should call notification service with default limit");
    }

    [Fact]
    public async Task GetNotifications_WithCustomLimit_ShouldPassLimit()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.GetUserNotificationsAsync(UserId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotificationDto>());

        // Act
        await _service.GetNotificationsAsync(UserId, ConnectionId, 10);

        // Assert
        _mockNotificationService.Verify(
            s => s.GetUserNotificationsAsync(UserId, 10, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should pass custom limit to notification service");
    }

    [Fact]
    public async Task MarkNotificationRead_ShouldCallService()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        // Act
        await _service.MarkNotificationReadAsync(UserId, ConnectionId, notificationId);

        // Assert
        _mockNotificationService.Verify(
            s => s.MarkAsReadAsync(UserId, notificationId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should call notification service to mark notification as read");
    }

    [Fact]
    public async Task MarkAllNotificationsRead_ShouldCallService()
    {
        // Act
        await _service.MarkAllNotificationsReadAsync(UserId, ConnectionId);

        // Assert
        _mockNotificationService.Verify(
            s => s.MarkAllAsReadAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should call notification service to mark all notifications as read");
    }

    [Fact]
    public async Task DismissNotification_ShouldCallService()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        // Act
        await _service.DismissNotificationAsync(UserId, ConnectionId, notificationId);

        // Assert
        _mockNotificationService.Verify(
            s => s.DismissAsync(UserId, notificationId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should call notification service to dismiss notification");
    }

    [Fact]
    public async Task GetNotificationSummary_ShouldLogDebugMessage()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.GetSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSummaryDto());

        // Act
        await _service.GetNotificationSummaryAsync(UserId, ConnectionId);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Notification summary requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when notification summary is requested");
    }
}
