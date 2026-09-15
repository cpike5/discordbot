namespace DiscordBot.Bot.Services.Realtime;

/// <summary>
/// Marker interface for every event published on <see cref="IDashboardEventBus"/>.
/// Mirrors one of the SignalR push events <see cref="Hubs.DashboardHub"/> and its
/// broadcasters already send, so a Blazor component can subscribe to the same data
/// in-process instead of going through a SignalR client connection.
/// </summary>
public interface IDashboardEvent
{
    /// <summary>
    /// Gets the UTC instant the event was published.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
