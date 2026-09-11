namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Scoped (one per circuit) <see cref="ILoadingState"/>. Thread-safe: scopes can legitimately be
/// opened and closed from different async continuations on the same circuit.
/// </summary>
public sealed class LoadingState : ILoadingState
{
    private readonly Lock _lock = new();
    private readonly List<(Guid Id, string? Message)> _active = [];

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public bool IsLoading
    {
        get
        {
            lock (_lock)
            {
                return _active.Count > 0;
            }
        }
    }

    /// <inheritdoc/>
    public string? Message
    {
        get
        {
            lock (_lock)
            {
                return _active.Count > 0 ? _active[^1].Message : null;
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable Begin(string? message = null)
    {
        var id = Guid.NewGuid();
        lock (_lock)
        {
            _active.Add((id, message));
        }

        Changed?.Invoke();
        return new Scope(this, id);
    }

    private void End(Guid id)
    {
        bool removed;
        lock (_lock)
        {
            var index = _active.FindIndex(e => e.Id == id);
            removed = index >= 0;
            if (removed)
            {
                _active.RemoveAt(index);
            }
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    private sealed class Scope(LoadingState owner, Guid id) : IDisposable
    {
        private LoadingState? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.End(id);
    }
}
