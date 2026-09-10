using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM;

/// <summary>
/// Background worker draining <see cref="LlmUsageRecorder"/>'s channel into
/// <see cref="ILlmUsageRepository"/> in batches, mirroring <c>AuditLogQueueProcessor</c>. Registered
/// ungated (alongside <see cref="LlmUsageRecorder"/>) — usage recording must work whenever any
/// assistant runs, and the processor is harmless idling with no API key configured.
/// </summary>
public class LlmUsageRecordProcessor : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LlmUsageRecorder _recorder;
    private readonly TimeProvider _timeProvider;

    private const int BatchSize = 20;
    private const int BatchTimeoutMilliseconds = 1000;

    /// <summary>Settable internally so tests can shrink the batch-collection window.</summary>
    internal TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(BatchTimeoutMilliseconds);

    /// <summary>Settable internally so tests can shrink the error-retry delay.</summary>
    internal TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public override string ServiceName => "LLM Usage Record Processor";

    public LlmUsageRecordProcessor(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        LlmUsageRecorder recorder,
        ILogger<LlmUsageRecordProcessor> logger,
        TimeProvider? timeProvider = null)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "LLM usage record processor starting. Batch size: {BatchSize}, Timeout: {TimeoutMs}ms",
            BatchSize, BatchTimeout.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            // One heartbeat per batch, not per record: ProcessBatchAsync blocks on the first dequeue
            // (no timeout) whenever the queue is empty, so with a quiet assistant this loop can sit
            // here for a long time between beats. That is expected - LastHeartbeat going stale means
            // "no usage to record", not "the processor is stuck" - a health check on this service
            // should key off Status/LastError, not solely off heartbeat age.
            UpdateHeartbeat();

            try
            {
                await ProcessBatchAsync(stoppingToken);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("LLM usage record processor shutting down, processing remaining items");
                await ProcessRemainingItemsAsync(CancellationToken.None);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LLM usage record batch");
                RecordError(ex);
                await Task.Delay(ErrorRetryDelay, _timeProvider, stoppingToken);
            }
        }

        _logger.LogInformation("LLM usage record processor stopped");
    }

    /// <summary>Collects up to <see cref="BatchSize"/> records (or waits up to <see cref="BatchTimeout"/>) and writes them.</summary>
    internal async Task<int> ProcessBatchAsync(CancellationToken stoppingToken)
    {
        var batch = new List<LlmUsageRecord>(BatchSize);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var firstItem = await _recorder.DequeueAsync(stoppingToken);
        batch.Add(firstItem);

        timeoutCts.CancelAfter(BatchTimeout);

        // True when collection stopped because stoppingToken itself fired (shutdown), not just
        // BatchTimeout elapsing normally - distinguishes the two below so a shutdown racing an
        // in-progress batch still flushes it instead of silently discarding already-dequeued
        // records (they are gone from the channel the moment DequeueAsync returns them).
        var stoppedForShutdown = false;

        while (batch.Count < BatchSize && !timeoutCts.Token.IsCancellationRequested)
        {
            try
            {
                var item = await _recorder.DequeueAsync(timeoutCts.Token);
                batch.Add(item);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                stoppedForShutdown = stoppingToken.IsCancellationRequested;
                break;
            }
        }

        if (batch.Count > 0)
        {
            // stoppingToken is already cancelled when stoppedForShutdown is true - writing with it
            // would fail this flush for no reason, so use a fresh token rather than losing records
            // that were already pulled off the channel to the very cancellation that ended collection.
            await WriteBatchAsync(batch, stoppedForShutdown ? CancellationToken.None : stoppingToken);
        }

        return batch.Count;
    }

    private async Task ProcessRemainingItemsAsync(CancellationToken cancellationToken)
    {
        var remaining = _recorder.Count;
        if (remaining == 0)
        {
            _logger.LogInformation("No remaining LLM usage records to process");
            return;
        }

        _logger.LogInformation("Processing {Count} remaining LLM usage records", remaining);

        var batch = new List<LlmUsageRecord>(BatchSize);
        while (_recorder.Count > 0)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                var item = await _recorder.DequeueAsync(cts.Token);
                batch.Add(item);

                if (batch.Count >= BatchSize)
                {
                    await TryWriteRemainingBatchAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (batch.Count > 0)
        {
            await TryWriteRemainingBatchAsync(batch, cancellationToken);
        }

        _logger.LogInformation("Finished processing remaining LLM usage records");
    }

    /// <summary>
    /// Shutdown-drain wrapper around <see cref="WriteBatchAsync"/>: it already logs and calls
    /// <see cref="RecordError(Exception)"/> before rethrowing, but there is no retry left during
    /// shutdown, so this catches that rethrow to let the drain keep pulling and attempting the
    /// remaining batches instead of aborting the whole drain on the first failed one.
    /// </summary>
    private async Task TryWriteRemainingBatchAsync(List<LlmUsageRecord> batch, CancellationToken cancellationToken)
    {
        try
        {
            await WriteBatchAsync(batch, cancellationToken);
        }
        catch (Exception)
        {
            // Already logged and recorded by WriteBatchAsync. Nothing left to retry during
            // shutdown - move on to whatever else is still queued.
        }
    }

    /// <summary>
    /// Writes one batch to the repository. Does NOT swallow a repository failure: it logs the lost
    /// count, calls the base class's <c>RecordError</c> so health reporting reflects it, and
    /// rethrows so the caller's own handling (the main loop's retry-delay catch, or the shutdown
    /// drain's per-batch catch) decides what happens next. The batch itself is not retried - its
    /// records are gone once this method throws.
    /// </summary>
    private async Task WriteBatchAsync(List<LlmUsageRecord> batch, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Writing batch of {Count} LLM usage records to repository", batch.Count);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILlmUsageRepository>();

            await repository.AddRangeAsync(batch, cancellationToken);

            _logger.LogInformation("Successfully wrote batch of {Count} LLM usage records to database", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch of {Count} LLM usage records to database. Batch is lost", batch.Count);
            RecordError(ex);
            throw;
        }
    }
}
