namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Provides a unified way to run fire-and-forget background tasks with
/// structured logging and error handling. Replaces scattered
/// <c>_ = Task.Run(...)</c> and <c>_ = SomeTaskAsync(...)</c> patterns.
/// </summary>
public interface IBackgroundTaskRunner
{
    /// <summary>
    /// Queues a fire-and-forget background task. Exceptions are logged
    /// against <paramref name="operationName"/> and never propagated.
    /// </summary>
    /// <param name="work">The async work to execute.</param>
    /// <param name="operationName">
    /// A human-readable label used in log messages and traces
    /// (e.g. <c>"BroadcastBotStatus"</c>).
    /// </param>
    void Run(Func<CancellationToken, Task> work, string operationName);

    /// <summary>
    /// Queues a fire-and-forget background task that receives caller-provided state,
    /// avoiding closure allocations for hot paths.
    /// </summary>
    /// <typeparam name="TState">The type of state passed to the work delegate.</typeparam>
    /// <param name="work">The async work to execute.</param>
    /// <param name="state">State to pass into the work delegate.</param>
    /// <param name="operationName">
    /// A human-readable label used in log messages and traces.
    /// </param>
    void Run<TState>(Func<TState, CancellationToken, Task> work, TState state, string operationName);
}
