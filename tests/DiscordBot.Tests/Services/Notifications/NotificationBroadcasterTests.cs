using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services.Notifications;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Notifications;

/// <summary>
/// Unit tests for <see cref="NotificationBroadcaster"/>'s dual-publish to
/// <see cref="IDashboardEventBus"/> alongside its existing per-user SignalR sends.
/// </summary>
public class NotificationBroadcasterTests
{
    private const string UserId = "user-123";

    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly Mock<INotificationRepository> _mockRepository;
    private readonly IDashboardEventBus _eventBus;
    private readonly Mock<ILogger<NotificationBroadcaster>> _mockLogger;
    private readonly NotificationBroadcaster _broadcaster;

    public NotificationBroadcasterTests()
    {
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _mockRepository = new Mock<INotificationRepository>();
        _eventBus = new DashboardEventBus(new Mock<ILogger<DashboardEventBus>>().Object);
        _mockLogger = new Mock<ILogger<NotificationBroadcaster>>();

        var mockClients = new Mock<IHubClients>();
        var mockUserProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockUserProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        _mockRepository
            .Setup(r => r.GetUserNotificationSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSummaryDto { TotalUnread = 3 });

        _broadcaster = new NotificationBroadcaster(_mockHubContext.Object, _mockRepository.Object, _eventBus, _mockLogger.Object);
    }

    private static UserNotification CreateNotification() => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        Type = NotificationType.BotStatus,
        Title = "Test",
        Message = "Test message",
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task BroadcastNotificationAsync_ShouldDualPublishToEventBus()
    {
        // Arrange
        var notification = CreateNotification();
        NotificationReceivedEvent? received = null;
        NotificationCountChangedEvent? countChanged = null;
        using var s1 = _eventBus.Subscribe<NotificationReceivedEvent>((evt, _) => { received = evt; return Task.CompletedTask; });
        using var s2 = _eventBus.Subscribe<NotificationCountChangedEvent>((evt, _) => { countChanged = evt; return Task.CompletedTask; });

        // Act
        await _broadcaster.BroadcastNotificationAsync(UserId, notification, CancellationToken.None);

        // Assert
        received.Should().NotBeNull();
        received!.UserId.Should().Be(UserId);
        received.Notification.Id.Should().Be(notification.Id);
        countChanged.Should().NotBeNull();
        countChanged!.UserId.Should().Be(UserId);
        countChanged.Summary.TotalUnread.Should().Be(3);
    }

    [Fact]
    public async Task BroadcastNotificationMarkedReadAsync_ShouldDualPublishToEventBus()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        NotificationMarkedReadEvent? received = null;
        using var subscription = _eventBus.Subscribe<NotificationMarkedReadEvent>((evt, _) =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        // Act
        await _broadcaster.BroadcastNotificationMarkedReadAsync(UserId, notificationId, CancellationToken.None);

        // Assert
        received.Should().NotBeNull();
        received!.UserId.Should().Be(UserId);
        received.NotificationId.Should().Be(notificationId);
    }

    [Fact]
    public async Task BroadcastCountChangedAsync_ShouldDualPublishToEventBus()
    {
        // Arrange
        NotificationCountChangedEvent? received = null;
        using var subscription = _eventBus.Subscribe<NotificationCountChangedEvent>((evt, _) =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        // Act
        await _broadcaster.BroadcastCountChangedAsync(UserId, CancellationToken.None);

        // Assert
        received.Should().NotBeNull();
        received!.UserId.Should().Be(UserId);
        received.Summary.TotalUnread.Should().Be(3);
    }

    [Fact]
    public async Task BroadcastAllReadAsync_ShouldDualPublishToEventBus()
    {
        // Arrange
        AllNotificationsReadEvent? received = null;
        using var subscription = _eventBus.Subscribe<AllNotificationsReadEvent>((evt, _) =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        // Act
        await _broadcaster.BroadcastAllReadAsync(UserId, CancellationToken.None);

        // Assert
        received.Should().NotBeNull();
        received!.UserId.Should().Be(UserId);
    }
}
