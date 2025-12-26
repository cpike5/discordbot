using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DashboardNotifier"/>.
/// Tests SignalR broadcasting and guild-specific notification capabilities.
/// </summary>
public class DashboardNotifierTests
{
    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly Mock<ILogger<DashboardNotifier>> _mockLogger;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockAllClientsProxy;
    private readonly Mock<IClientProxy> _mockGroupClientsProxy;
    private readonly DashboardNotifier _notifier;

    public DashboardNotifierTests()
    {
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _mockLogger = new Mock<ILogger<DashboardNotifier>>();
        _mockClients = new Mock<IHubClients>();
        _mockAllClientsProxy = new Mock<IClientProxy>();
        _mockGroupClientsProxy = new Mock<IClientProxy>();

        // Setup hub context with mocked clients
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.All).Returns(_mockAllClientsProxy.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockGroupClientsProxy.Object);

        _notifier = new DashboardNotifier(_mockHubContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_ShouldSendToAllClients()
    {
        // Arrange
        var status = new BotStatusDto
        {
            Uptime = TimeSpan.FromHours(3),
            GuildCount = 10,
            LatencyMs = 25,
            ConnectionState = "Connected",
            BotUsername = "TestBot",
            StartTime = DateTime.UtcNow.AddHours(-3)
        };

        // Act
        await _notifier.BroadcastBotStatusAsync(status);

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
    public async Task BroadcastBotStatusAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var status = new BotStatusDto
        {
            GuildCount = 5,
            ConnectionState = "Connected"
        };

        // Act
        await _notifier.BroadcastBotStatusAsync(status);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Broadcasting bot status update to all clients")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when broadcasting bot status");
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var status = new BotStatusDto { GuildCount = 3 };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _notifier.BroadcastBotStatusAsync(status, token);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                "BotStatusUpdated",
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token to SignalR send method");
    }

    [Fact]
    public async Task SendGuildUpdateAsync_ShouldSendToGuildGroup()
    {
        // Arrange
        const ulong guildId = 123456789;
        const string eventName = "GuildMemberJoined";
        var eventData = new { UserId = 987654321, Username = "NewUser" };
        var expectedGroupName = $"guild-{guildId}";

        // Act
        await _notifier.SendGuildUpdateAsync(guildId, eventName, eventData);

        // Assert
        _mockClients.Verify(
            c => c.Group(expectedGroupName),
            Times.Once,
            "Should target the correct guild group");

        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.Is<object[]>(args => args.Length == 1 && args[0] == eventData),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send event to guild group with correct event name and data");
    }

    [Fact]
    public async Task SendGuildUpdateAsync_ShouldLogDebugMessage()
    {
        // Arrange
        const ulong guildId = 555555555;
        const string eventName = "GuildSettingsChanged";
        var eventData = new { SettingName = "WelcomeChannel", NewValue = 123456 };

        // Act
        await _notifier.SendGuildUpdateAsync(guildId, eventName, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Sending guild update") &&
                    v.ToString()!.Contains(guildId.ToString()) &&
                    v.ToString()!.Contains(eventName)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message with guild ID and event name");
    }

    [Fact]
    public async Task SendGuildUpdateAsync_WithDifferentGuilds_ShouldTargetCorrectGroups()
    {
        // Arrange
        const ulong guildId1 = 111111111;
        const ulong guildId2 = 222222222;
        const string eventName = "Update";
        var data = new { Value = 123 };

        // Act
        await _notifier.SendGuildUpdateAsync(guildId1, eventName, data);
        await _notifier.SendGuildUpdateAsync(guildId2, eventName, data);

        // Assert
        _mockClients.Verify(
            c => c.Group($"guild-{guildId1}"),
            Times.Once,
            "Should target first guild group");

        _mockClients.Verify(
            c => c.Group($"guild-{guildId2}"),
            Times.Once,
            "Should target second guild group");

        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "Should send to both guild groups");
    }

    [Fact]
    public async Task SendGuildUpdateAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        const ulong guildId = 999999999;
        const string eventName = "TestEvent";
        var data = new { Test = true };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _notifier.SendGuildUpdateAsync(guildId, eventName, data, token);

        // Assert
        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token to SignalR send method");
    }

    [Fact]
    public async Task BroadcastToAllAsync_ShouldSendEventToAllClients()
    {
        // Arrange
        const string eventName = "ServerMaintenance";
        var eventData = new
        {
            Message = "Server will restart in 5 minutes",
            ScheduledTime = DateTime.UtcNow.AddMinutes(5)
        };

        // Act
        await _notifier.BroadcastToAllAsync(eventName, eventData);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.Is<object[]>(args => args.Length == 1 && args[0] == eventData),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send custom event to all clients");
    }

    [Fact]
    public async Task BroadcastToAllAsync_ShouldLogDebugMessage()
    {
        // Arrange
        const string eventName = "CustomNotification";
        var data = new { Type = "Info", Content = "Test" };

        // Act
        await _notifier.BroadcastToAllAsync(eventName, data);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting to all clients") &&
                    v.ToString()!.Contains(eventName)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message with event name");
    }

    [Fact]
    public async Task BroadcastToAllAsync_WithDifferentEvents_ShouldSendEachEvent()
    {
        // Arrange
        const string event1 = "Event1";
        const string event2 = "Event2";
        var data1 = new { Value = 1 };
        var data2 = new { Value = 2 };

        // Act
        await _notifier.BroadcastToAllAsync(event1, data1);
        await _notifier.BroadcastToAllAsync(event2, data2);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                event1,
                It.Is<object[]>(args => args[0] == data1),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send first event with correct data");

        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                event2,
                It.Is<object[]>(args => args[0] == data2),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send second event with correct data");
    }

    [Fact]
    public async Task BroadcastToAllAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        const string eventName = "TestEvent";
        var data = new { Test = true };
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _notifier.BroadcastToAllAsync(eventName, data, token);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.IsAny<object[]>(),
                token),
            Times.Once,
            "Should pass cancellation token to SignalR send method");
    }

    [Fact]
    public async Task BroadcastToAllAsync_WithComplexObject_ShouldSerializeCorrectly()
    {
        // Arrange
        const string eventName = "ComplexEvent";
        var complexData = new
        {
            Id = 12345,
            Name = "Test",
            Tags = new[] { "tag1", "tag2", "tag3" },
            Metadata = new { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            IsActive = true
        };

        // Act
        await _notifier.BroadcastToAllAsync(eventName, complexData);

        // Assert
        _mockAllClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.Is<object[]>(args =>
                    args.Length == 1 &&
                    args[0] == complexData),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send complex object without modification");
    }

    [Fact]
    public async Task SendGuildUpdateAsync_WithComplexEventData_ShouldSendCorrectly()
    {
        // Arrange
        const ulong guildId = 777777777;
        const string eventName = "ComplexGuildEvent";
        var complexData = new
        {
            GuildId = guildId,
            Members = new[] { 1UL, 2UL, 3UL },
            Settings = new { WelcomeEnabled = true, LogChannel = 123456UL },
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _notifier.SendGuildUpdateAsync(guildId, eventName, complexData);

        // Assert
        _mockGroupClientsProxy.Verify(
            c => c.SendCoreAsync(
                eventName,
                It.Is<object[]>(args =>
                    args.Length == 1 &&
                    args[0] == complexData),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should send complex guild event data without modification");
    }
}
