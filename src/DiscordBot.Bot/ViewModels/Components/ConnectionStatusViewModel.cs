namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the connection status indicator component.
/// </summary>
/// <param name="State">The current connection state.</param>
/// <param name="CustomText">Optional custom text to display instead of the default state label.</param>
/// <param name="Id">
/// Element id. The default, <c>connection-status</c>, is the one <c>dashboard-realtime.js</c> looks
/// up; the global banner passes its own so the two never share an id.
/// </param>
/// <param name="Live">
/// Whether the indicator is itself a polite live region (the default). The banner turns this off
/// and announces through one dedicated region instead, so a change is not read out twice.
/// </param>
public record ConnectionStatusViewModel(
    ConnectionState State = ConnectionState.Disconnected,
    string? CustomText = null,
    string Id = "connection-status",
    bool Live = true
);

/// <summary>
/// Represents the possible connection states for the SignalR connection.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Successfully connected to the SignalR hub.
    /// </summary>
    Connected,

    /// <summary>
    /// Initial connection attempt in progress.
    /// </summary>
    Connecting,

    /// <summary>
    /// Attempting to reconnect after a disconnection.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Not connected to the SignalR hub.
    /// </summary>
    Disconnected
}
