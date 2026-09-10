using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DiscordBot.Tests.Hubs;

/// <summary>
/// Unit tests for <see cref="DashboardHub"/>.
/// Tests SignalR hub connection/group lifecycle and the authenticated-user short-circuit for
/// notification methods. Feature-specific behavior (metrics, audio status, notification content)
/// lives in the per-service tests for <see cref="IDashboardMetricsService"/>,
/// <see cref="IDashboardAudioStatusService"/>, and <see cref="IDashboardNotificationQueryService"/>.
/// </summary>
public class DashboardHubTests
{
    private readonly Mock<IDashboardMetricsService> _mockMetricsService;
    private readonly Mock<IDashboardAudioStatusService> _mockAudioStatusService;
    private readonly Mock<IDashboardNotificationQueryService> _mockNotificationQueryService;
    private readonly Mock<IPerformanceSubscriptionTracker> _mockSubscriptionTracker;
    private readonly Mock<ILogger<DashboardHub>> _mockLogger;
    private readonly Mock<IGroupManager> _mockGroupManager;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly DashboardHub _hub;

    public DashboardHubTests()
    {
        _mockMetricsService = new Mock<IDashboardMetricsService>();
        _mockAudioStatusService = new Mock<IDashboardAudioStatusService>();
        _mockNotificationQueryService = new Mock<IDashboardNotificationQueryService>();
        _mockSubscriptionTracker = new Mock<IPerformanceSubscriptionTracker>();
        _mockLogger = new Mock<ILogger<DashboardHub>>();
        _mockGroupManager = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _hub = new DashboardHub(
            _mockMetricsService.Object,
            _mockAudioStatusService.Object,
            _mockNotificationQueryService.Object,
            _mockSubscriptionTracker.Object,
            _mockLogger.Object);

        // Setup hub context with mocked group manager
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id-123");
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.NameIdentifier, "test-user-id-123")
        }, "TestAuth")));

        _hub.Context = _mockContext.Object;
        _hub.Groups = _mockGroupManager.Object;
    }

    [Fact]
    public async Task JoinGuildGroup_ShouldAddToGroup()
    {
        // Arrange
        const string guildIdString = "123456789";
        var expectedGroupName = $"guild-{guildIdString}";

        // Act
        await _hub.JoinGuildGroup(guildIdString);

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                expectedGroupName,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add connection to guild-specific group");
    }

    [Fact]
    public async Task JoinGuildGroup_ShouldLogDebugMessage()
    {
        // Arrange
        const string guildIdString = "987654321";

        // Act
        await _hub.JoinGuildGroup(guildIdString);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client joined guild group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when client joins guild group");
    }

    [Fact]
    public async Task JoinGuildGroup_WithMultipleGuilds_ShouldAddToEachGroup()
    {
        // Arrange
        const string guildIdString1 = "111111111";
        const string guildIdString2 = "222222222";

        // Act
        await _hub.JoinGuildGroup(guildIdString1);
        await _hub.JoinGuildGroup(guildIdString2);

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                $"guild-{guildIdString1}",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add to first guild group");

        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                $"guild-{guildIdString2}",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add to second guild group");
    }

    [Fact]
    public async Task LeaveGuildGroup_ShouldRemoveFromGroup()
    {
        // Arrange
        const string guildIdString = "123456789";
        var expectedGroupName = $"guild-{guildIdString}";

        // Act
        await _hub.LeaveGuildGroup(guildIdString);

        // Assert
        _mockGroupManager.Verify(
            g => g.RemoveFromGroupAsync(
                "test-connection-id-123",
                expectedGroupName,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should remove connection from guild-specific group");
    }

    [Fact]
    public async Task LeaveGuildGroup_ShouldLogDebugMessage()
    {
        // Arrange
        const string guildIdString = "987654321";

        // Act
        await _hub.LeaveGuildGroup(guildIdString);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client left guild group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when client leaves guild group");
    }

    [Fact]
    public async Task OnConnectedAsync_ShouldLogConnection()
    {
        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Dashboard client connected") &&
                    v.ToString()!.Contains("test-connection-id-123") &&
                    v.ToString()!.Contains("testuser")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log information when client connects");
    }

    [Fact]
    public async Task OnConnectedAsync_WithAnonymousUser_ShouldLogUnknownUser()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unknown")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log 'unknown' for anonymous users");
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutException_ShouldLogInformation()
    {
        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Dashboard client disconnected") &&
                    v.ToString()!.Contains("test-connection-id-123") &&
                    v.ToString()!.Contains("testuser")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log information when client disconnects normally");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Should not log warning when disconnection is clean");
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_ShouldLogWarning()
    {
        // Arrange
        var exception = new Exception("Connection lost");

        // Act
        await _hub.OnDisconnectedAsync(exception);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Dashboard client disconnected with error") &&
                    v.ToString()!.Contains("test-connection-id-123") &&
                    v.ToString()!.Contains("testuser")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log warning with exception when client disconnects abnormally");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Dashboard client disconnected") && !v.ToString()!.Contains("with error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Should not log normal disconnection when exception occurred");
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithAnonymousUser_ShouldLogUnknownUser()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unknown")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log 'unknown' for anonymous users on disconnect");
    }

    // ============================================================================
    // Performance Group Management Tests
    // ============================================================================

    [Fact]
    public async Task JoinPerformanceGroup_ShouldAddToPerformanceGroup()
    {
        // Arrange
        const string expectedGroupName = "performance";

        // Act
        await _hub.JoinPerformanceGroup();

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                expectedGroupName,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add connection to performance group");
    }

    [Fact]
    public async Task JoinPerformanceGroup_ShouldLogDebugMessage()
    {
        // Act
        await _hub.JoinPerformanceGroup();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client joined performance group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when client joins performance group");
    }

    [Fact]
    public async Task LeavePerformanceGroup_ShouldRemoveFromPerformanceGroup()
    {
        // Arrange
        const string expectedGroupName = "performance";

        // Act
        await _hub.LeavePerformanceGroup();

        // Assert
        _mockGroupManager.Verify(
            g => g.RemoveFromGroupAsync(
                "test-connection-id-123",
                expectedGroupName,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should remove connection from performance group");
    }

    [Fact]
    public async Task LeavePerformanceGroup_ShouldLogDebugMessage()
    {
        // Act
        await _hub.LeavePerformanceGroup();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client left performance group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when client leaves performance group");
    }

    [Fact]
    public async Task JoinSystemHealthGroup_ShouldAddToSystemHealthGroup()
    {
        // Arrange
        const string expectedGroupName = "system-health";

        // Act
        await _hub.JoinSystemHealthGroup();

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                expectedGroupName,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add connection to system health group");
    }

    [Fact]
    public async Task JoinSystemHealthGroup_ShouldLogDebugMessage()
    {
        // Act
        await _hub.JoinSystemHealthGroup();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client joined system health group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when client joins system health group");
    }

    [Fact]
    public async Task LeaveSystemHealthGroup_ShouldRemoveFromSystemHealthGroup()
    {
        // Arrange
        const string expectedGroupName = "system-health";

        // Act
        await _hub.LeaveSystemHealthGroup();

        // Assert
        _mockGroupManager.Verify(
            g => g.RemoveFromGroupAsync(
                "test-connection-id-123",
                expectedGroupName,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should remove connection from system health group");
    }

    [Fact]
    public async Task LeaveSystemHealthGroup_ShouldLogDebugMessage()
    {
        // Act
        await _hub.LeaveSystemHealthGroup();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client left system health group")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when client leaves system health group");
    }

    // ============================================================================
    // Notification Methods Tests - authenticated-user short-circuit
    // ============================================================================

    [Fact]
    public async Task GetNotificationSummary_WithNoAuthenticatedUser_ShouldReturnEmptySummary()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;

        // Act
        var result = await _hub.GetNotificationSummary();

        // Assert
        result.Should().NotBeNull();
        result.TotalUnread.Should().Be(0);

        _mockNotificationQueryService.Verify(
            s => s.GetNotificationSummaryAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should not call notification service for unauthenticated user");
    }

    [Fact]
    public async Task GetNotifications_WithNoAuthenticatedUser_ShouldReturnEmpty()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;

        // Act
        var result = await _hub.GetNotifications();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockNotificationQueryService.Verify(
            s => s.GetNotificationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never,
            "Should not call notification service for unauthenticated user");
    }

    [Fact]
    public async Task MarkNotificationRead_WithNoAuthenticatedUser_ShouldNotCallService()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;
        var notificationId = Guid.NewGuid();

        // Act
        await _hub.MarkNotificationRead(notificationId);

        // Assert
        _mockNotificationQueryService.Verify(
            s => s.MarkNotificationReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()),
            Times.Never,
            "Should not call notification service for unauthenticated user");
    }

    [Fact]
    public async Task MarkAllNotificationsRead_WithNoAuthenticatedUser_ShouldNotCallService()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;

        // Act
        await _hub.MarkAllNotificationsRead();

        // Assert
        _mockNotificationQueryService.Verify(
            s => s.MarkAllNotificationsReadAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Should not call notification service for unauthenticated user");
    }

    [Fact]
    public async Task DismissNotification_WithNoAuthenticatedUser_ShouldNotCallService()
    {
        // Arrange
        var anonymousContext = new Mock<HubCallerContext>();
        anonymousContext.Setup(c => c.ConnectionId).Returns("anonymous-connection");
        anonymousContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // Not authenticated

        _hub.Context = anonymousContext.Object;
        var notificationId = Guid.NewGuid();

        // Act
        await _hub.DismissNotification(notificationId);

        // Assert
        _mockNotificationQueryService.Verify(
            s => s.DismissNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()),
            Times.Never,
            "Should not call notification service for unauthenticated user");
    }
}
