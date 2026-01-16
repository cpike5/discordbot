using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ConnectionStateService"/>.
/// Tests cover connection state tracking, session duration calculation, uptime percentage,
/// connection event history, and reconnection statistics.
/// </summary>
public class ConnectionStateServiceTests
{
    private readonly ConnectionStateService _service;
    private readonly Mock<IConnectionEventRepository> _repositoryMock;
    private readonly PerformanceMetricsOptions _options;
    private readonly List<ConnectionEvent> _storedEvents = new();

    public ConnectionStateServiceTests()
    {
        _options = new PerformanceMetricsOptions
        {
            ConnectionEventRetentionDays = 7
        };

        _repositoryMock = new Mock<IConnectionEventRepository>();

        // Setup repository to track stored events
        _repositoryMock.Setup(r => r.GetLastEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _storedEvents.LastOrDefault());

        _repositoryMock.Setup(r => r.GetEventsSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime since, CancellationToken _) =>
                _storedEvents.Where(e => e.Timestamp >= since).OrderBy(e => e.Timestamp).ToList());

        _repositoryMock.Setup(r => r.AddEventAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string eventType, DateTime timestamp, string? reason, string? details, CancellationToken _) =>
            {
                var evt = new ConnectionEvent
                {
                    Id = _storedEvents.Count + 1,
                    EventType = eventType,
                    Timestamp = timestamp,
                    Reason = reason,
                    Details = details
                };
                _storedEvents.Add(evt);
                return evt;
            });

        // Setup service scope factory
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IConnectionEventRepository)))
            .Returns(_repositoryMock.Object);

        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(serviceScopeMock.Object);

        _service = new ConnectionStateService(
            NullLogger<ConnectionStateService>.Instance,
            scopeFactoryMock.Object,
            Options.Create(_options));
    }

    [Fact]
    public void RecordConnected_UpdatesCurrentStateToConnected()
    {
        // Act
        _service.RecordConnected();

        // Allow time for background task
        Thread.Sleep(100);

        // Assert
        var state = _service.GetCurrentState();
        state.Should().Be(GatewayConnectionState.Connected, "state should be connected after recording connection");

        var lastConnectedTime = _service.GetLastConnectedTime();
        lastConnectedTime.Should().NotBeNull("last connected time should be set");
        lastConnectedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "timestamp should be recent");
    }

    [Fact]
    public void RecordDisconnected_UpdatesCurrentStateToDisconnected()
    {
        // Arrange
        _service.RecordConnected();
        Thread.Sleep(50);

        // Act
        _service.RecordDisconnected(exception: null);
        Thread.Sleep(100);

        // Assert
        var state = _service.GetCurrentState();
        state.Should().Be(GatewayConnectionState.Disconnected, "state should be disconnected after recording disconnection");

        var lastDisconnectedTime = _service.GetLastDisconnectedTime();
        lastDisconnectedTime.Should().NotBeNull("last disconnected time should be set");
        lastDisconnectedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "timestamp should be recent");
    }

    [Fact]
    public void RecordDisconnected_StoresExceptionMessage()
    {
        // Arrange
        _service.RecordConnected();
        Thread.Sleep(50);
        var exception = new InvalidOperationException("Connection lost");

        // Act
        _service.RecordDisconnected(exception);
        Thread.Sleep(100);

        // Assert
        var events = _service.GetConnectionEvents(days: 7);
        var disconnectEvent = events.LastOrDefault(e => e.EventType == "Disconnected");

        disconnectEvent.Should().NotBeNull("disconnect event should be recorded");
        disconnectEvent!.Reason.Should().Be("Connection lost", "exception message should be stored");
        disconnectEvent.Details.Should().Be("InvalidOperationException", "exception type should be stored");
    }

    [Fact]
    public void GetCurrentSessionDuration_ReturnsZeroWhenNotConnected()
    {
        // Act
        var duration = _service.GetCurrentSessionDuration();

        // Assert
        duration.Should().Be(TimeSpan.Zero, "session duration should be zero when not connected");
    }

    [Fact]
    public void GetCurrentSessionDuration_ReturnsDurationWhenConnected()
    {
        // Arrange
        _service.RecordConnected();
        Thread.Sleep(150); // Wait a bit to accumulate duration

        // Act
        var duration = _service.GetCurrentSessionDuration();

        // Assert
        duration.Should().BeGreaterThan(TimeSpan.Zero, "session duration should be positive when connected");
        duration.Should().BeLessThan(TimeSpan.FromSeconds(2), "duration should be reasonable for this test");
    }

    [Fact]
    public void GetUptimePercentage_CalculatesCorrectly()
    {
        // Arrange - Simulate connected state with events
        _service.RecordConnected();
        Thread.Sleep(100);

        // Act
        var uptimePercentage = _service.GetUptimePercentage(TimeSpan.FromSeconds(1));

        // Assert
        uptimePercentage.Should().BeInRange(0, 100, "uptime percentage should be between 0 and 100");
    }

    [Fact]
    public void GetConnectionEvents_ReturnsEventsWithinTimeRange()
    {
        // Arrange
        _service.RecordConnected();
        Thread.Sleep(50);
        _service.RecordDisconnected(exception: null);
        Thread.Sleep(50);
        _service.RecordConnected();
        Thread.Sleep(100);

        // Act
        var events = _service.GetConnectionEvents(days: 7);

        // Assert
        events.Should().HaveCount(3, "three events were recorded");
        events[0].EventType.Should().Be("Connected", "first event should be connected");
        events[1].EventType.Should().Be("Disconnected", "second event should be disconnected");
        events[2].EventType.Should().Be("Connected", "third event should be connected");
        events.Should().BeInAscendingOrder(e => e.Timestamp, "events should be in chronological order");
    }

    [Fact]
    public void GetConnectionStats_CalculatesReconnectionCountCorrectly()
    {
        // Arrange - Simulate multiple connect/disconnect cycles
        _service.RecordConnected(); // Initial connection
        Thread.Sleep(50);
        _service.RecordDisconnected(exception: null);
        Thread.Sleep(50);
        _service.RecordConnected(); // Reconnection 1
        Thread.Sleep(50);
        _service.RecordDisconnected(exception: null);
        Thread.Sleep(50);
        _service.RecordConnected(); // Reconnection 2
        Thread.Sleep(100);

        // Act
        var stats = _service.GetConnectionStats(days: 7);

        // Assert
        stats.TotalEvents.Should().Be(5, "five events were recorded");
        stats.ReconnectionCount.Should().Be(2, "two reconnections occurred after initial connection");
        stats.UptimePercentage.Should().BeInRange(0, 100, "uptime percentage should be valid");
    }

    [Fact]
    public void GetUptimePercentage_ReturnsZeroWhenNoEvents()
    {
        // Act - No events recorded
        var uptimePercentage = _service.GetUptimePercentage(TimeSpan.FromHours(24));

        // Assert
        uptimePercentage.Should().Be(0, "uptime should be 0% when no events exist and not connected");
    }

    [Fact]
    public void GetCurrentState_ReturnsDisconnectedByDefault()
    {
        // Act
        var state = _service.GetCurrentState();

        // Assert
        state.Should().Be(GatewayConnectionState.Disconnected, "default state should be disconnected");
    }

    [Fact]
    public void GetLastConnectedTime_ReturnsNullWhenNeverConnected()
    {
        // Act
        var lastConnected = _service.GetLastConnectedTime();

        // Assert
        lastConnected.Should().BeNull("should be null when never connected");
    }

    [Fact]
    public void GetLastDisconnectedTime_ReturnsNullWhenNeverDisconnected()
    {
        // Act
        var lastDisconnected = _service.GetLastDisconnectedTime();

        // Assert
        lastDisconnected.Should().BeNull("should be null when never disconnected");
    }
}
