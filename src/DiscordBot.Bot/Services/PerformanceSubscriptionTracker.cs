using System.Collections.Concurrent;
using DiscordBot.Bot.Hubs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Tracks SignalR client subscriptions to performance metric groups.
/// Thread-safe implementation using interlocked operations and concurrent collections.
/// </summary>
public class PerformanceSubscriptionTracker : IPerformanceSubscriptionTracker
{
    private readonly ILogger<PerformanceSubscriptionTracker> _logger;

    // Atomic counters for group membership
    private int _performanceGroupClientCount;
    private int _systemHealthGroupClientCount;

    // Track which groups each connection is subscribed to (for cleanup on disconnect)
    private readonly ConcurrentDictionary<string, HashSet<string>> _connectionSubscriptions = new();
    private readonly object _subscriptionLock = new();

    /// <inheritdoc />
    public int PerformanceGroupClientCount => _performanceGroupClientCount;

    /// <inheritdoc />
    public int SystemHealthGroupClientCount => _systemHealthGroupClientCount;

    public PerformanceSubscriptionTracker(ILogger<PerformanceSubscriptionTracker> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnJoinPerformanceGroup()
    {
        var newCount = Interlocked.Increment(ref _performanceGroupClientCount);
        _logger.LogDebug(
            "Client joined performance group. Active clients: {Count}",
            newCount);
    }

    /// <inheritdoc />
    public void OnLeavePerformanceGroup()
    {
        var newCount = Interlocked.Decrement(ref _performanceGroupClientCount);
        if (newCount < 0)
        {
            // Correct underflow (shouldn't happen but be safe)
            Interlocked.CompareExchange(ref _performanceGroupClientCount, 0, newCount);
            _logger.LogWarning("Performance group client count underflowed, corrected to 0");
        }
        else
        {
            _logger.LogDebug(
                "Client left performance group. Active clients: {Count}",
                newCount);
        }
    }

    /// <inheritdoc />
    public void OnJoinSystemHealthGroup()
    {
        var newCount = Interlocked.Increment(ref _systemHealthGroupClientCount);
        _logger.LogDebug(
            "Client joined system health group. Active clients: {Count}",
            newCount);
    }

    /// <inheritdoc />
    public void OnLeaveSystemHealthGroup()
    {
        var newCount = Interlocked.Decrement(ref _systemHealthGroupClientCount);
        if (newCount < 0)
        {
            // Correct underflow
            Interlocked.CompareExchange(ref _systemHealthGroupClientCount, 0, newCount);
            _logger.LogWarning("System health group client count underflowed, corrected to 0");
        }
        else
        {
            _logger.LogDebug(
                "Client left system health group. Active clients: {Count}",
                newCount);
        }
    }

    /// <inheritdoc />
    public void TrackSubscription(string connectionId, string groupName)
    {
        lock (_subscriptionLock)
        {
            var subscriptions = _connectionSubscriptions.GetOrAdd(connectionId, _ => new HashSet<string>());
            subscriptions.Add(groupName);
        }

        _logger.LogTrace(
            "Tracked subscription: ConnectionId={ConnectionId}, Group={GroupName}",
            connectionId,
            groupName);
    }

    /// <inheritdoc />
    public void UntrackSubscription(string connectionId, string groupName)
    {
        lock (_subscriptionLock)
        {
            if (_connectionSubscriptions.TryGetValue(connectionId, out var subscriptions))
            {
                subscriptions.Remove(groupName);

                // Clean up empty entries
                if (subscriptions.Count == 0)
                {
                    _connectionSubscriptions.TryRemove(connectionId, out _);
                }
            }
        }

        _logger.LogTrace(
            "Untracked subscription: ConnectionId={ConnectionId}, Group={GroupName}",
            connectionId,
            groupName);
    }

    /// <inheritdoc />
    public void OnClientDisconnected(string connectionId)
    {
        HashSet<string>? subscriptions;

        lock (_subscriptionLock)
        {
            if (!_connectionSubscriptions.TryRemove(connectionId, out subscriptions))
            {
                return; // Client had no tracked subscriptions
            }
        }

        // Decrement counts for each group the client was subscribed to
        foreach (var groupName in subscriptions)
        {
            if (groupName == DashboardHub.PerformanceGroupName)
            {
                OnLeavePerformanceGroup();
            }
            else if (groupName == DashboardHub.SystemHealthGroupName)
            {
                OnLeaveSystemHealthGroup();
            }
        }

        _logger.LogDebug(
            "Client disconnected, cleaned up {Count} subscriptions: ConnectionId={ConnectionId}",
            subscriptions.Count,
            connectionId);
    }
}
