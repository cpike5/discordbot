using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Default implementation of <see cref="IBackgroundTaskRunner"/>.
/// Wraps fire-and-forget work in a <see cref="Task.Run"/> with structured
/// logging so that failures are always observable.
/// </summary>
public sealed class BackgroundTaskRunner : IBackgroundTaskRunner
{
    private readonly ILogger<BackgroundTaskRunner> _logger;

    public BackgroundTaskRunner(ILogger<BackgroundTaskRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Run(Func<CancellationToken, Task> work, string operationName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogTrace("Background task '{OperationName}' starting", operationName);
                await work(CancellationToken.None);
                _logger.LogTrace("Background task '{OperationName}' completed", operationName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Background task '{OperationName}' was cancelled", operationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task '{OperationName}' failed", operationName);
            }
        });
    }

    /// <inheritdoc/>
    public void Run<TState>(Func<TState, CancellationToken, Task> work, TState state, string operationName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogTrace("Background task '{OperationName}' starting", operationName);
                await work(state, CancellationToken.None);
                _logger.LogTrace("Background task '{OperationName}' completed", operationName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Background task '{OperationName}' was cancelled", operationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task '{OperationName}' failed", operationName);
            }
        });
    }
}
