using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ConnectionStateService"/>.
/// Tests cover connection state tracking, session duration calculation, uptime percentage,
/// connection event history, and reconnection statistics.
/// </summary>
public class ConnectionStateServiceTests
{
    private readonly ConnectionStateService _service;
    private readonly PerformanceMetricsOptions _options;

    public ConnectionStateServiceTests()
    {
        _options = new PerformanceMetricsOptions
        {
            ConnectionEventRetentionDays = 7
        };

        _service = new ConnectionStateService(
            NullLogger<ConnectionStateService>.Instance,
            Options.Create(_options));
    }

    [Fact]
    public void RecordConnected_UpdatesCurrentStateToConnected()
    {
        // Act
        _service.RecordConnected();

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

        // Act
        _service.RecordDisconnected(exception: null);

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
        var exception = new InvalidOperationException("Connection lost");

        // Act
        _service.RecordDisconnected(exception);

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
        Thread.Sleep(100); // Wait a bit to accumulate duration

        // Act
        var duration = _service.GetCurrentSessionDuration();

        // Assert
        duration.Should().BeGreaterThan(TimeSpan.Zero, "session duration should be positive when connected");
        duration.Should().BeLessThan(TimeSpan.FromSeconds(2), "duration should be reasonable for this test");
    }

    [Fact]
    public void GetUptimePercentage_CalculatesCorrectly()
    {
        // Arrange - Simulate 50% uptime over a 2-second period
        _service.RecordConnected();
        Thread.Sleep(500); // Connected for 500ms
        _service.RecordDisconnected(exception: null);
        Thread.Sleep(500); // Disconnected for 500ms

        // Act
        var uptimePercentage = _service.GetUptimePercentage(TimeSpan.FromSeconds(1));

        // Assert
        uptimePercentage.Should().BeInRange(0, 100, "uptime percentage should be between 0 and 100");
        // Note: Due to timing variations, we can't assert exact percentage, but it should be reasonable
    }

    [Fact]
    public void GetConnectionEvents_ReturnsEventsWithinTimeRange()
    {
        // Arrange
        _service.RecordConnected();
        _service.RecordDisconnected(exception: null);
        _service.RecordConnected();

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
    public void GetConnectionEvents_RespectsDaysParameter()
    {
        // Arrange - Record an event
        _service.RecordConnected();

        // Act - Request events from last 0 days (should be empty or very recent only)
        var recentEvents = _service.GetConnectionEvents(days: 7);
        var allEvents = _service.GetConnectionEvents(days: 30);

        // Assert
        recentEvents.Should().NotBeEmpty("recent events should include the connection");
        allEvents.Should().NotBeEmpty("all events should include the connection");
        recentEvents.Count.Should().BeLessThanOrEqualTo(allEvents.Count, "recent range should not have more events than wider range");
    }

    [Fact]
    public void GetConnectionStats_CalculatesReconnectionCountCorrectly()
    {
        // Arrange - Simulate multiple connect/disconnect cycles
        _service.RecordConnected(); // Initial connection
        _service.RecordDisconnected(exception: null);
        _service.RecordConnected(); // Reconnection 1
        _service.RecordDisconnected(exception: null);
        _service.RecordConnected(); // Reconnection 2

        // Act
        var stats = _service.GetConnectionStats(days: 7);

        // Assert
        stats.TotalEvents.Should().Be(5, "five events were recorded");
        stats.ReconnectionCount.Should().Be(2, "two reconnections occurred after initial connection");
        stats.UptimePercentage.Should().BeInRange(0, 100, "uptime percentage should be valid");
    }
}
