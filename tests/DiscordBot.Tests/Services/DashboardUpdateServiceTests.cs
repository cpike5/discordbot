using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DashboardUpdateService"/>.
/// Tests SignalR broadcasting for all update types with error handling.
/// </summary>
public class DashboardUpdateServiceTests
{
    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly Mock<ILogger<DashboardUpdateService>> _mockLogger;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockAllClientsProxy;
    private readonly Mock<IClientProxy> _mockGroupClientsProxy;
    private readonly DashboardUpdateService _service;

    public DashboardUpdateServiceTests()
    {
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _mockLogger = new Mock<ILogger<DashboardUpdateService>>();
        _mockClients = new Mock<IHubClients>();
        _mockAllClientsProxy = new Mock<IClientProxy>();
        _mockGroupClientsProxy = new Mock<IClientProxy>();

        // Setup hub context with mocked clients
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.All).Returns(_mockAllClientsProxy.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockGroupClientsProxy.Object);

        _service = new DashboardUpdateService(_mockHubContext.Object, _mockLogger.Object);
    }

    #region BroadcastBotStatusAsync Tests

    [Fact]
    public async Task BroadcastBotStatusAsync_ShouldSendToAllClients()
    {
        // Arrange
        var status = new BotStatusUpdateDto
        {
            ConnectionState = "Connected",
            Latency = 25,
            GuildCount = 10,
            Uptime = TimeSpan.FromHours(5),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _service.BroadcastBotStatusAsync(status);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "BotStatusUpdated",
                It.Is<object[]>(args => args.Length == 1 && args[0] == status),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send bot status update to all clients");
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var status = new BotStatusUpdateDto { ConnectionState = "Connected" };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _service.BroadcastBotStatusAsync(status, token);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "BotStatusUpdated",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token");
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_WhenExceptionThrown_ShouldNotRethrow()
    {
        // Arrange
        var status = new BotStatusUpdateDto { ConnectionState = "Connected" };
        _mockAllClientsProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR error"));

        // Act
        var act = async () => await _service.BroadcastBotStatusAsync(status);

        // Assert
        await act.Should().NotThrowAsync("Error handling should swallow exceptions");
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_WhenExceptionThrown_ShouldLogError()
    {
        // Arrange
        var status = new BotStatusUpdateDto { ConnectionState = "Disconnected" };
        var exception = new InvalidOperationException("SignalR error");
        _mockAllClientsProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        await _service.BroadcastBotStatusAsync(status);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to broadcast bot status update")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log error when exception occurs");
    }

    #endregion

    #region BroadcastCommandExecutedAsync Tests

    [Fact]
    public async Task BroadcastCommandExecutedAsync_ShouldSendToAllClients()
    {
        // Arrange
        var update = new CommandExecutedUpdateDto
        {
            CommandName = "ping",
            GuildId = 123456789,
            GuildName = "Test Guild",
            UserId = 987654321,
            Username = "TestUser",
            Success = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _service.BroadcastCommandExecutedAsync(update);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "CommandExecuted",
                It.Is<object[]>(args => args.Length == 1 && args[0] == update),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send command executed update to all clients");
    }

    [Fact]
    public async Task BroadcastCommandExecutedAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var update = new CommandExecutedUpdateDto { CommandName = "help" };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _service.BroadcastCommandExecutedAsync(update, token);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "CommandExecuted",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token");
    }

    [Fact]
    public async Task BroadcastCommandExecutedAsync_WhenExceptionThrown_ShouldNotRethrow()
    {
        // Arrange
        var update = new CommandExecutedUpdateDto { CommandName = "test" };
        _mockAllClientsProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR error"));

        // Act
        var act = async () => await _service.BroadcastCommandExecutedAsync(update);

        // Assert
        await act.Should().NotThrowAsync("Error handling should swallow exceptions");
    }

    #endregion

    #region BroadcastGuildActivityAsync Tests

    [Fact]
    public async Task BroadcastGuildActivityAsync_ShouldSendToAllClientsAndGuildGroup()
    {
        // Arrange
        var update = new GuildActivityUpdateDto
        {
            GuildId = 123456789,
            GuildName = "Test Guild",
            EventType = "MemberJoined",
            Timestamp = DateTime.UtcNow
        };
        var expectedGroupName = $"guild-{update.GuildId}";

        // Act
        await _service.BroadcastGuildActivityAsync(update);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "GuildActivity",
                It.Is<object[]>(args => args.Length == 1 && args[0] == update),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send to all clients");

        _mockClients.Verify(
            c => c.Group(expectedGroupName),
            Times.Once,
            "Should target correct guild group");

        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                "GuildActivity",
                It.Is<object[]>(args => args.Length == 1 && args[0] == update),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should also send to guild group");
    }

    [Fact]
    public async Task BroadcastGuildActivityAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var update = new GuildActivityUpdateDto { GuildId = 111111111, EventType = "MemberLeft" };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _service.BroadcastGuildActivityAsync(update, token);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "GuildActivity",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token to all clients broadcast");

        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                "GuildActivity",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token to guild group broadcast");
    }

    [Fact]
    public async Task BroadcastGuildActivityAsync_WhenExceptionThrown_ShouldNotRethrow()
    {
        // Arrange
        var update = new GuildActivityUpdateDto { GuildId = 123, EventType = "Test" };
        _mockAllClientsProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR error"));

        // Act
        var act = async () => await _service.BroadcastGuildActivityAsync(update);

        // Assert
        await act.Should().NotThrowAsync("Error handling should swallow exceptions");
    }

    #endregion

    #region BroadcastStatsUpdateAsync Tests

    [Fact]
    public async Task BroadcastStatsUpdateAsync_ShouldSendToAllClients()
    {
        // Arrange
        var stats = new DashboardStatsDto
        {
            CommandsToday = 150,
            TotalMembers = 5000,
            ActiveUsersLastHour = 42,
            MessagesToday = 12000,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _service.BroadcastStatsUpdateAsync(stats);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "StatsUpdated",
                It.Is<object[]>(args => args.Length == 1 && args[0] == stats),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send stats update to all clients");
    }

    [Fact]
    public async Task BroadcastStatsUpdateAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var stats = new DashboardStatsDto { CommandsToday = 100 };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _service.BroadcastStatsUpdateAsync(stats, token);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "StatsUpdated",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token");
    }

    [Fact]
    public async Task BroadcastStatsUpdateAsync_WhenExceptionThrown_ShouldNotRethrow()
    {
        // Arrange
        var stats = new DashboardStatsDto { CommandsToday = 50 };
        _mockAllClientsProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR error"));

        // Act
        var act = async () => await _service.BroadcastStatsUpdateAsync(stats);

        // Assert
        await act.Should().NotThrowAsync("Error handling should swallow exceptions");
    }

    #endregion

    #region BroadcastGuildActivityToGuildAsync Tests

    [Fact]
    public async Task BroadcastGuildActivityToGuildAsync_ShouldSendToGuildGroupOnly()
    {
        // Arrange
        const ulong guildId = 555555555;
        var update = new GuildActivityUpdateDto
        {
            GuildId = guildId,
            GuildName = "Target Guild",
            EventType = "SettingsChanged",
            Timestamp = DateTime.UtcNow
        };
        var expectedGroupName = $"guild-{guildId}";

        // Act
        await _service.BroadcastGuildActivityToGuildAsync(guildId, update);

        // Assert
        _mockClients.Verify(
            c => c.Group(expectedGroupName),
            Times.Once,
            "Should target correct guild group");

        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                "GuildActivity",
                It.Is<object[]>(args => args.Length == 1 && args[0] == update),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send to guild group only");

        // Verify all clients were NOT called
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not broadcast to all clients");
    }

    [Fact]
    public async Task BroadcastGuildActivityToGuildAsync_WithDifferentGuildIds_ShouldTargetCorrectGroups()
    {
        // Arrange
        const ulong guildId1 = 111111111;
        const ulong guildId2 = 222222222;
        var update = new GuildActivityUpdateDto { EventType = "Test" };

        // Act
        await _service.BroadcastGuildActivityToGuildAsync(guildId1, update);
        await _service.BroadcastGuildActivityToGuildAsync(guildId2, update);

        // Assert
        _mockClients.Verify(
            c => c.Group($"guild-{guildId1}"),
            Times.Once,
            "Should target first guild group");

        _mockClients.Verify(
            c => c.Group($"guild-{guildId2}"),
            Times.Once,
            "Should target second guild group");
    }

    [Fact]
    public async Task BroadcastGuildActivityToGuildAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        const ulong guildId = 999999999;
        var update = new GuildActivityUpdateDto { EventType = "Test" };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _service.BroadcastGuildActivityToGuildAsync(guildId, update, token);

        // Assert
        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                "GuildActivity",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token");
    }

    [Fact]
    public async Task BroadcastGuildActivityToGuildAsync_WhenExceptionThrown_ShouldNotRethrow()
    {
        // Arrange
        const ulong guildId = 123;
        var update = new GuildActivityUpdateDto { EventType = "Test" };
        _mockGroupClientsProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR error"));

        // Act
        var act = async () => await _service.BroadcastGuildActivityToGuildAsync(guildId, update);

        // Assert
        await act.Should().NotThrowAsync("Error handling should swallow exceptions");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task BroadcastBotStatusAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var status = new BotStatusUpdateDto
        {
            ConnectionState = "Connected",
            GuildCount = 5
        };

        // Act
        await _service.BroadcastBotStatusAsync(status);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting bot status update") &&
                    v.ToString()!.Contains(status.ConnectionState) &&
                    v.ToString()!.Contains(status.GuildCount.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message with connection state and guild count");
    }

    [Fact]
    public async Task BroadcastCommandExecutedAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var update = new CommandExecutedUpdateDto
        {
            CommandName = "info",
            GuildId = 123456789,
            Success = true
        };

        // Act
        await _service.BroadcastCommandExecutedAsync(update);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting command executed") &&
                    v.ToString()!.Contains(update.CommandName)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message with command name");
    }

    [Fact]
    public async Task BroadcastGuildActivityAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var update = new GuildActivityUpdateDto
        {
            GuildId = 777777777,
            EventType = "MemberJoined"
        };

        // Act
        await _service.BroadcastGuildActivityAsync(update);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting guild activity") &&
                    v.ToString()!.Contains(update.GuildId.ToString()) &&
                    v.ToString()!.Contains(update.EventType)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message with guild ID and event type");
    }

    [Fact]
    public async Task BroadcastStatsUpdateAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var stats = new DashboardStatsDto
        {
            CommandsToday = 200,
            TotalMembers = 10000
        };

        // Act
        await _service.BroadcastStatsUpdateAsync(stats);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting stats update") &&
                    v.ToString()!.Contains(stats.CommandsToday.ToString()) &&
                    v.ToString()!.Contains(stats.TotalMembers.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message with commands and members count");
    }

    #endregion
}
