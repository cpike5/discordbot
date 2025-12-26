namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the connection status indicator component.
/// </summary>
/// <param name="State">The current connection state.</param>
/// <param name="CustomText">Optional custom text to display instead of the default state label.</param>
public record ConnectionStatusViewModel(
    ConnectionState State = ConnectionState.Disconnected,
    string? CustomText = null
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
