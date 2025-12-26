using DiscordBot.Bot.Hubs;
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
/// Tests SignalR hub operations including group management, status retrieval, and connection lifecycle.
/// </summary>
public class DashboardHubTests
{
    private readonly Mock<IBotService> _mockBotService;
    private readonly Mock<ILogger<DashboardHub>> _mockLogger;
    private readonly Mock<IGroupManager> _mockGroupManager;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly DashboardHub _hub;

    public DashboardHubTests()
    {
        _mockBotService = new Mock<IBotService>();
        _mockLogger = new Mock<ILogger<DashboardHub>>();
        _mockGroupManager = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _hub = new DashboardHub(_mockBotService.Object, _mockLogger.Object);

        // Setup hub context with mocked group manager
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id-123");
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "TestAuth")));

        _hub.Context = _mockContext.Object;
        _hub.Groups = _mockGroupManager.Object;
    }

    [Fact]
    public async Task JoinGuildGroup_ShouldAddToGroup()
    {
        // Arrange
        const ulong guildId = 123456789;
        var expectedGroupName = $"guild-{guildId}";

        // Act
        await _hub.JoinGuildGroup(guildId);

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
        const ulong guildId = 987654321;

        // Act
        await _hub.JoinGuildGroup(guildId);

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
        const ulong guildId1 = 111111111;
        const ulong guildId2 = 222222222;

        // Act
        await _hub.JoinGuildGroup(guildId1);
        await _hub.JoinGuildGroup(guildId2);

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                $"guild-{guildId1}",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add to first guild group");

        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id-123",
                $"guild-{guildId2}",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should add to second guild group");
    }

    [Fact]
    public async Task LeaveGuildGroup_ShouldRemoveFromGroup()
    {
        // Arrange
        const ulong guildId = 123456789;
        var expectedGroupName = $"guild-{guildId}";

        // Act
        await _hub.LeaveGuildGroup(guildId);

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
        const ulong guildId = 987654321;

        // Act
        await _hub.LeaveGuildGroup(guildId);

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
    public void GetCurrentStatus_ShouldReturnBotStatus()
    {
        // Arrange
        var expectedStatus = new BotStatusDto
        {
            Uptime = TimeSpan.FromHours(2),
            GuildCount = 5,
            LatencyMs = 42,
            ConnectionState = "Connected",
            BotUsername = "TestBot",
            StartTime = DateTime.UtcNow.AddHours(-2)
        };

        _mockBotService
            .Setup(b => b.GetStatus())
            .Returns(expectedStatus);

        // Act
        var result = _hub.GetCurrentStatus();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedStatus, "Should return the exact status from bot service");

        _mockBotService.Verify(
            b => b.GetStatus(),
            Times.Once,
            "Should call bot service to get status");
    }

    [Fact]
    public void GetCurrentStatus_ShouldLogDebugMessage()
    {
        // Arrange
        var status = new BotStatusDto
        {
            GuildCount = 3,
            ConnectionState = "Connected"
        };

        _mockBotService.Setup(b => b.GetStatus()).Returns(status);

        // Act
        _hub.GetCurrentStatus();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Status requested by client")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when status is requested");
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
}
