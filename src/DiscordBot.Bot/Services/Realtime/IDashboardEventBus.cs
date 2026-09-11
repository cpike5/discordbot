namespace DiscordBot.Bot.Services.Realtime;

/// <summary>
/// In-process publish/subscribe bus for dashboard real-time events. Every broadcaster that
/// pushes over <see cref="Microsoft.AspNetCore.SignalR.IHubContext{THub}"/> for
/// <see cref="Hubs.DashboardHub"/> also publishes the matching typed event here (dual-publish),
/// so a Blazor component can subscribe in-process instead of running a SignalR client.
/// See docs/articles/signalr-realtime.md, "In-process event bus".
/// </summary>
public interface IDashboardEventBus
{
    /// <summary>
    /// Subscribes to every event of type <typeparamref name="TEvent"/>. Dispose the returned
    /// handle to unsubscribe; a component should do this from <c>Dispose</c>.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <param name="handler">
    /// Invoked for each published event. A handler that throws is caught and logged; it never
    /// faults the publisher or other subscribers.
    /// </param>
    /// <returns>A disposable that removes the subscription.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDashboardEvent;

    /// <summary>
    /// Subscribes to events of type <typeparamref name="TEvent"/> for one guild only, mirroring
    /// the <c>guild-{guildId}</c> SignalR group filter. Equivalent to <see cref="Subscribe{TEvent}(Func{TEvent, CancellationToken, Task})"/>
    /// with a <c>evt.GuildId == guildId</c> check in the handler.
    /// </summary>
    /// <typeparam name="TEvent">The guild-scoped event type to subscribe to.</typeparam>
    /// <param name="guildId">The guild to filter to.</param>
    /// <param name="handler">Invoked only for events whose <see cref="GuildScopedEvent.GuildId"/> matches.</param>
    /// <returns>A disposable that removes the subscription.</returns>
    IDisposable Subscribe<TEvent>(ulong guildId, Func<TEvent, CancellationToken, Task> handler)
        where TEvent : GuildScopedEvent;

    /// <summary>
    /// Publishes an event to every current subscriber of its exact type. Subscribers are invoked
    /// concurrently; a throwing or disposed subscriber does not affect this call or any other
    /// subscriber.
    /// </summary>
    /// <typeparam name="TEvent">The event type being published.</typeparam>
    /// <param name="evt">The event instance.</param>
    /// <param name="ct">Cancellation token passed through to subscriber handlers.</param>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        where TEvent : IDashboardEvent;
}
