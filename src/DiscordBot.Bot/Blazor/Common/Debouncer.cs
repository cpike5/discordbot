namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// Trailing-edge debounce for a Blazor component: coalesces rapid calls (a search box keystroke,
/// a burst of real-time events from <see cref="Services.Realtime.IDashboardEventBus"/>) into one
/// action after <paramref name="delay"/> of silence, cancelling any in-flight wait or action first.
/// Not thread-safe against calls from more than one Blazor synchronization context concurrently;
/// a component's own event handlers are already serialized, so this is not a concern in practice.
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly Lock _lock = new();
    private CancellationTokenSource? _pending;
    private bool _disposed;

    /// <summary>
    /// Schedules <paramref name="action"/> to run after <paramref name="delay"/>, cancelling any
    /// call to <see cref="Debounce"/> still waiting or running from a previous call. A no-op once
    /// disposed.
    /// </summary>
    /// <param name="delay">How long to wait for silence before running <paramref name="action"/>.</param>
    /// <param name="action">
    /// The action to run. Receives a token that is cancelled if a later <see cref="Debounce"/>
    /// call supersedes it, or the debouncer is disposed, while the action is still running.
    /// </param>
    public void Debounce(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationTokenSource cts;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            CancelSafely(_pending);
            cts = new CancellationTokenSource();
            _pending = cts;
        }

        _ = RunAsync(delay, action, cts);
    }

    private static async Task RunAsync(TimeSpan delay, Func<CancellationToken, Task> action, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            await action(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later Debounce call, or the debouncer was disposed. Expected.
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <summary>Cancels any pending wait or in-flight action. Further <see cref="Debounce"/> calls are no-ops.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelSafely(_pending);
            _pending = null;
        }
    }

    /// <summary>
    /// Cancels <paramref name="cts"/>, tolerating a concurrent <see cref="RunAsync"/> that has
    /// already disposed it after its delay/action finished (that race is expected: this class
    /// deliberately does not dispose the previous <see cref="CancellationTokenSource"/> itself,
    /// leaving that to <see cref="RunAsync"/>'s own <c>finally</c>).
    /// </summary>
    private static void CancelSafely(CancellationTokenSource? cts)
    {
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The in-flight RunAsync finished and disposed its own CTS between our null-check
            // and Cancel(); nothing left to cancel.
        }
    }
}
