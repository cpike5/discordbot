namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Tracks SignalR client subscriptions to performance metric groups.
/// Used to optimize broadcasting by only sending metrics when clients are subscribed.
/// </summary>
public interface IPerformanceSubscriptionTracker
{
    /// <summary>
    /// Gets the current number of clients subscribed to the performance metrics group.
    /// </summary>
    int PerformanceGroupClientCount { get; }

    /// <summary>
    /// Gets the current number of clients subscribed to the system health group.
    /// </summary>
    int SystemHealthGroupClientCount { get; }

    /// <summary>
    /// Records a client joining the performance metrics group.
    /// </summary>
    void OnJoinPerformanceGroup();

    /// <summary>
    /// Records a client leaving the performance metrics group.
    /// </summary>
    void OnLeavePerformanceGroup();

    /// <summary>
    /// Records a client joining the system health group.
    /// </summary>
    void OnJoinSystemHealthGroup();

    /// <summary>
    /// Records a client leaving the system health group.
    /// </summary>
    void OnLeaveSystemHealthGroup();

    /// <summary>
    /// Records a client disconnecting from the hub.
    /// Decrements counts for any groups the client was subscribed to.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID of the disconnecting client.</param>
    void OnClientDisconnected(string connectionId);

    /// <summary>
    /// Tracks a client's subscription to a group for automatic cleanup on disconnect.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="groupName">The group name the client joined.</param>
    void TrackSubscription(string connectionId, string groupName);

    /// <summary>
    /// Removes a client's subscription to a group.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="groupName">The group name the client left.</param>
    void UntrackSubscription(string connectionId, string groupName);
}
