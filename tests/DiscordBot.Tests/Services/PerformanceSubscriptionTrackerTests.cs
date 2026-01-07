using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PerformanceSubscriptionTracker"/>.
/// Tests subscription tracking for SignalR groups.
/// </summary>
public class PerformanceSubscriptionTrackerTests
{
    private readonly Mock<ILogger<PerformanceSubscriptionTracker>> _mockLogger;
    private readonly PerformanceSubscriptionTracker _tracker;

    public PerformanceSubscriptionTrackerTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceSubscriptionTracker>>();
        _tracker = new PerformanceSubscriptionTracker(_mockLogger.Object);
    }

    #region Performance Group Tests

    [Fact]
    public void OnJoinPerformanceGroup_ShouldIncrementCount()
    {
        // Arrange
        _tracker.PerformanceGroupClientCount.Should().Be(0);

        // Act
        _tracker.OnJoinPerformanceGroup();

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(1);
    }

    [Fact]
    public void OnJoinPerformanceGroup_MultipleCalls_ShouldTrackAllClients()
    {
        // Act
        _tracker.OnJoinPerformanceGroup();
        _tracker.OnJoinPerformanceGroup();
        _tracker.OnJoinPerformanceGroup();

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(3);
    }

    [Fact]
    public void OnLeavePerformanceGroup_ShouldDecrementCount()
    {
        // Arrange
        _tracker.OnJoinPerformanceGroup();
        _tracker.OnJoinPerformanceGroup();
        _tracker.PerformanceGroupClientCount.Should().Be(2);

        // Act
        _tracker.OnLeavePerformanceGroup();

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(1);
    }

    [Fact]
    public void OnLeavePerformanceGroup_WithZeroCount_ShouldNotGoBelowZero()
    {
        // Arrange
        _tracker.PerformanceGroupClientCount.Should().Be(0);

        // Act
        _tracker.OnLeavePerformanceGroup();

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(0);
    }

    #endregion

    #region System Health Group Tests

    [Fact]
    public void OnJoinSystemHealthGroup_ShouldIncrementCount()
    {
        // Arrange
        _tracker.SystemHealthGroupClientCount.Should().Be(0);

        // Act
        _tracker.OnJoinSystemHealthGroup();

        // Assert
        _tracker.SystemHealthGroupClientCount.Should().Be(1);
    }

    [Fact]
    public void OnJoinSystemHealthGroup_MultipleCalls_ShouldTrackAllClients()
    {
        // Act
        _tracker.OnJoinSystemHealthGroup();
        _tracker.OnJoinSystemHealthGroup();
        _tracker.OnJoinSystemHealthGroup();

        // Assert
        _tracker.SystemHealthGroupClientCount.Should().Be(3);
    }

    [Fact]
    public void OnLeaveSystemHealthGroup_ShouldDecrementCount()
    {
        // Arrange
        _tracker.OnJoinSystemHealthGroup();
        _tracker.OnJoinSystemHealthGroup();

        // Act
        _tracker.OnLeaveSystemHealthGroup();

        // Assert
        _tracker.SystemHealthGroupClientCount.Should().Be(1);
    }

    [Fact]
    public void OnLeaveSystemHealthGroup_WithZeroCount_ShouldNotGoBelowZero()
    {
        // Arrange
        _tracker.SystemHealthGroupClientCount.Should().Be(0);

        // Act
        _tracker.OnLeaveSystemHealthGroup();

        // Assert
        _tracker.SystemHealthGroupClientCount.Should().Be(0);
    }

    #endregion

    #region Subscription Tracking Tests

    [Fact]
    public void TrackSubscription_ShouldStoreSubscription()
    {
        // Arrange
        const string connectionId = "conn-1";
        const string groupName = "performance";

        // Act
        _tracker.TrackSubscription(connectionId, groupName);

        // Assert - No exception should be thrown
        // Verify by attempting to disconnect and checking the behavior
        _tracker.OnJoinPerformanceGroup();
        _tracker.TrackSubscription(connectionId, DashboardHub.PerformanceGroupName);

        // Should decrement when disconnected
        _tracker.OnClientDisconnected(connectionId);
        _tracker.PerformanceGroupClientCount.Should().Be(0);
    }

    [Fact]
    public void UntrackSubscription_ShouldRemoveSubscription()
    {
        // Arrange
        const string connectionId = "conn-1";
        _tracker.OnJoinPerformanceGroup();
        _tracker.TrackSubscription(connectionId, DashboardHub.PerformanceGroupName);

        // Act
        _tracker.UntrackSubscription(connectionId, DashboardHub.PerformanceGroupName);

        // Assert - Disconnect should not decrement (subscription was removed)
        _tracker.OnClientDisconnected(connectionId);
        _tracker.PerformanceGroupClientCount.Should().Be(1); // Still 1 because we didn't leave
    }

    [Fact]
    public void UntrackSubscription_WithNonExistentConnection_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        _tracker.UntrackSubscription("non-existent", DashboardHub.PerformanceGroupName);
    }

    #endregion

    #region OnClientDisconnected Tests

    [Fact]
    public void OnClientDisconnected_WithPerformanceSubscription_ShouldDecrementCount()
    {
        // Arrange
        const string connectionId = "conn-1";
        _tracker.OnJoinPerformanceGroup();
        _tracker.TrackSubscription(connectionId, DashboardHub.PerformanceGroupName);

        // Act
        _tracker.OnClientDisconnected(connectionId);

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(0);
    }

    [Fact]
    public void OnClientDisconnected_WithSystemHealthSubscription_ShouldDecrementCount()
    {
        // Arrange
        const string connectionId = "conn-1";
        _tracker.OnJoinSystemHealthGroup();
        _tracker.TrackSubscription(connectionId, DashboardHub.SystemHealthGroupName);

        // Act
        _tracker.OnClientDisconnected(connectionId);

        // Assert
        _tracker.SystemHealthGroupClientCount.Should().Be(0);
    }

    [Fact]
    public void OnClientDisconnected_WithMultipleSubscriptions_ShouldDecrementAllCounts()
    {
        // Arrange
        const string connectionId = "conn-1";
        _tracker.OnJoinPerformanceGroup();
        _tracker.OnJoinSystemHealthGroup();
        _tracker.TrackSubscription(connectionId, DashboardHub.PerformanceGroupName);
        _tracker.TrackSubscription(connectionId, DashboardHub.SystemHealthGroupName);

        // Act
        _tracker.OnClientDisconnected(connectionId);

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(0);
        _tracker.SystemHealthGroupClientCount.Should().Be(0);
    }

    [Fact]
    public void OnClientDisconnected_WithNoSubscriptions_ShouldDoNothing()
    {
        // Arrange
        _tracker.OnJoinPerformanceGroup();

        // Act
        _tracker.OnClientDisconnected("non-existent");

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(1);
    }

    [Fact]
    public void OnClientDisconnected_CalledTwice_ShouldOnlyDecrementOnce()
    {
        // Arrange
        const string connectionId = "conn-1";
        _tracker.OnJoinPerformanceGroup();
        _tracker.OnJoinPerformanceGroup(); // Two clients
        _tracker.TrackSubscription(connectionId, DashboardHub.PerformanceGroupName);

        // Act
        _tracker.OnClientDisconnected(connectionId);
        _tracker.OnClientDisconnected(connectionId); // Second call should do nothing

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(1);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentJoinsAndLeaves_ShouldMaintainAccurateCount()
    {
        // Arrange
        const int operationsPerThread = 100;
        var tasks = new List<Task>();

        // Act - Multiple threads joining and leaving
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    _tracker.OnJoinPerformanceGroup();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should have exactly 1000 clients
        _tracker.PerformanceGroupClientCount.Should().Be(1000);

        // Now leave
        tasks.Clear();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    _tracker.OnLeavePerformanceGroup();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        _tracker.PerformanceGroupClientCount.Should().Be(0);
    }

    #endregion
}
