using System.Collections.Concurrent;

namespace DiscordBot.Bot.Services.Realtime;

/// <summary>
/// Thread-safe, singleton <see cref="IDashboardEventBus"/>. Subscriber lists are per event
/// <see cref="Type"/>; publishing snapshots the current subscribers and awaits them
/// concurrently, each wrapped in its own try/catch so one faulted or disposed subscriber can
/// never affect the publisher or any other subscriber.
/// </summary>
public sealed class DashboardEventBus : IDashboardEventBus
{
    private readonly ILogger<DashboardEventBus> _logger;
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Delegate>> _subscribers = new();

    public DashboardEventBus(ILogger<DashboardEventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDashboardEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var bucket = _subscribers.GetOrAdd(typeof(TEvent), static _ => new ConcurrentDictionary<Guid, Delegate>());
        var id = Guid.NewGuid();
        bucket[id] = handler;

        return new Subscription(() => bucket.TryRemove(id, out _));
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(ulong guildId, Func<TEvent, CancellationToken, Task> handler)
        where TEvent : GuildScopedEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe<TEvent>((evt, ct) => evt.GuildId == guildId ? handler(evt, ct) : Task.CompletedTask);
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        where TEvent : IDashboardEvent
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (!_subscribers.TryGetValue(typeof(TEvent), out var bucket) || bucket.IsEmpty)
        {
            return;
        }

        // Snapshot so a subscriber that unsubscribes mid-publish (from inside its own handler,
        // or concurrently on another thread) can never mutate the collection we are iterating.
        var handlers = bucket.Values.Cast<Func<TEvent, CancellationToken, Task>>().ToArray();

        var tasks = new Task[handlers.Length];
        for (var i = 0; i < handlers.Length; i++)
        {
            tasks[i] = InvokeGuardedAsync(handlers[i], evt, ct);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task InvokeGuardedAsync<TEvent>(Func<TEvent, CancellationToken, Task> handler, TEvent evt, CancellationToken ct)
        where TEvent : IDashboardEvent
    {
        try
        {
            await handler(evt, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard event subscriber threw while handling {EventType}", typeof(TEvent).Name);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;

        public Subscription(Action dispose) => _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
