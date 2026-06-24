namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// Coalesces rapid calls (e.g. per-keystroke filter input) into a single
/// trailing-edge invocation after a quiet period. Each new call cancels the
/// previous pending action, so only the final one within the window runs.
/// </summary>
/// <remarks>
/// This is the server-side equivalent of the <c>AbortController</c> +
/// <c>setTimeout</c> debounce pattern used throughout the existing JS modules
/// (e.g. <c>command-filters.js</c> at 300ms). Hold one instance per debounced
/// input and dispose it with the component.
/// </remarks>
public sealed class Debouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public Debouncer(TimeSpan delay) => _delay = delay;

    public Debouncer(int milliseconds) : this(TimeSpan.FromMilliseconds(milliseconds)) { }

    /// <summary>
    /// Schedules <paramref name="action"/> to run after the debounce delay,
    /// cancelling any previously scheduled (still-pending) action. The provided
    /// <see cref="CancellationToken"/> is cancelled if a newer call supersedes
    /// this one, so long-running work can bail out early.
    /// </summary>
    public async Task DebounceAsync(Func<CancellationToken, Task> action)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Delay(_delay, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                await action(token).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            // Superseded by a newer call — expected, ignore.
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
