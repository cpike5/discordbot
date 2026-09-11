namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Scoped (one per circuit) <see cref="IToastService"/>. Thread-safe: an auto-dismiss timer fires
/// on a thread-pool thread, and <see cref="Show"/>/<see cref="Dismiss"/> may also be called from
/// component event handlers running on the circuit's own thread.
/// </summary>
public sealed class ToastService : IToastService, IDisposable
{
    private const int MaxToasts = 5;

    private readonly Lock _lock = new();
    private readonly List<Entry> _entries = [];
    private bool _disposed;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public IReadOnlyList<ToastMessage> Toasts
    {
        get
        {
            lock (_lock)
            {
                return _entries.ConvertAll(e => e.Message);
            }
        }
    }

    /// <inheritdoc/>
    public void Show(ToastLevel level, string message, string? title = null, TimeSpan? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var effectiveDuration = duration ?? DefaultDuration(level);
        var toastMessage = new ToastMessage(Guid.NewGuid(), level, message, title, DateTimeOffset.UtcNow, effectiveDuration);

        Timer? timer = null;
        if (effectiveDuration is { } d && d > TimeSpan.Zero)
        {
            // Captures toastMessage.Id, not `this` beyond the delegate scope, so the timer alone
            // can't keep the service alive past Dispose.
            timer = new Timer(_ => Dismiss(toastMessage.Id), state: null, dueTime: d, period: Timeout.InfiniteTimeSpan);
        }

        List<Entry>? evicted = null;
        lock (_lock)
        {
            if (_disposed)
            {
                timer?.Dispose();
                return;
            }

            while (_entries.Count >= MaxToasts)
            {
                evicted ??= [];
                evicted.Add(_entries[0]);
                _entries.RemoveAt(0);
            }

            _entries.Add(new Entry(toastMessage, timer));
        }

        if (evicted is not null)
        {
            foreach (var entry in evicted)
            {
                entry.Timer?.Dispose();
            }
        }

        Changed?.Invoke();
    }

    /// <inheritdoc/>
    public void Success(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Success, message, title, duration);

    /// <inheritdoc/>
    public void Info(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Info, message, title, duration);

    /// <inheritdoc/>
    public void Warning(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Warning, message, title, duration);

    /// <inheritdoc/>
    public void Error(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Error, message, title, duration);

    /// <inheritdoc/>
    public void Dismiss(Guid id)
    {
        Entry? removed = null;
        lock (_lock)
        {
            var index = _entries.FindIndex(e => e.Message.Id == id);
            if (index >= 0)
            {
                removed = _entries[index];
                _entries.RemoveAt(index);
            }
        }

        if (removed is null)
        {
            return;
        }

        removed.Timer?.Dispose();
        Changed?.Invoke();
    }

    /// <summary>Disposes every pending auto-dismiss timer. Does not raise <see cref="Changed"/>.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _entries)
            {
                entry.Timer?.Dispose();
            }

            _entries.Clear();
        }
    }

    private static TimeSpan? DefaultDuration(ToastLevel level) => level switch
    {
        ToastLevel.Success => TimeSpan.FromSeconds(3),
        ToastLevel.Error => null,
        _ => TimeSpan.FromSeconds(5)
    };

    private sealed record Entry(ToastMessage Message, Timer? Timer);
}
