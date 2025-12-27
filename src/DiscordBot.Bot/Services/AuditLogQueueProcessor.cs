using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that processes audit log entries from the queue.
/// Batches entries for efficient bulk insertion into the database.
/// </summary>
public class AuditLogQueueProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditLogQueue _queue;
    private readonly ILogger<AuditLogQueueProcessor> _logger;

    private const int BatchSize = 10;
    private const int BatchTimeoutMilliseconds = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogQueueProcessor"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped dependencies.</param>
    /// <param name="queue">The audit log queue.</param>
    /// <param name="logger">The logger.</param>
    public AuditLogQueueProcessor(
        IServiceScopeFactory scopeFactory,
        IAuditLogQueue queue,
        ILogger<AuditLogQueueProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Audit log queue processor starting. Batch size: {BatchSize}, Timeout: {TimeoutMs}ms",
            BatchSize, BatchTimeoutMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown - process remaining items
                _logger.LogInformation("Audit log queue processor shutting down, processing remaining items");
                await ProcessRemainingItemsAsync(CancellationToken.None);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audit log batch");
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Audit log queue processor stopped");
    }

    /// <summary>
    /// Processes a single batch of audit log entries.
    /// Collects up to BatchSize entries or waits up to BatchTimeoutMilliseconds.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditLogCreateDto>(BatchSize);
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        try
        {
            // Wait for first item (blocking)
            var firstItem = await _queue.DequeueAsync(stoppingToken);
            batch.Add(firstItem);

            // Collect additional items up to batch size with timeout
            timeoutCts.CancelAfter(BatchTimeoutMilliseconds);

            while (batch.Count < BatchSize && !timeoutCts.Token.IsCancellationRequested)
            {
                try
                {
                    var item = await _queue.DequeueAsync(timeoutCts.Token);
                    batch.Add(item);
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    // Timeout reached, process what we have
                    break;
                }
            }

            // Process the batch
            if (batch.Count > 0)
            {
                await WriteBatchToRepositoryAsync(batch, stoppingToken);
            }
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }

    /// <summary>
    /// Processes all remaining items in the queue during shutdown.
    /// Called when the service is stopping to ensure no audit logs are lost.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (usually none during shutdown).</param>
    private async Task ProcessRemainingItemsAsync(CancellationToken cancellationToken)
    {
        var remainingCount = _queue.Count;

        if (remainingCount == 0)
        {
            _logger.LogInformation("No remaining audit log entries to process");
            return;
        }

        _logger.LogInformation("Processing {Count} remaining audit log entries", remainingCount);

        var batch = new List<AuditLogCreateDto>(BatchSize);

        while (_queue.Count > 0)
        {
            try
            {
                // Use a short timeout to avoid blocking indefinitely
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                var item = await _queue.DequeueAsync(cts.Token);
                batch.Add(item);

                // Process in batches
                if (batch.Count >= BatchSize)
                {
                    await WriteBatchToRepositoryAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // No more items available
                break;
            }
        }

        // Process any remaining items in the final batch
        if (batch.Count > 0)
        {
            await WriteBatchToRepositoryAsync(batch, cancellationToken);
        }

        _logger.LogInformation("Finished processing remaining audit log entries");
    }

    /// <summary>
    /// Writes a batch of audit log entries to the repository.
    /// Creates a scoped service to access the repository.
    /// </summary>
    /// <param name="batch">The batch of audit log DTOs to write.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private async Task WriteBatchToRepositoryAsync(
        List<AuditLogCreateDto> batch,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Writing batch of {Count} audit log entries to repository", batch.Count);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

            // Convert DTOs to entities
            var entities = batch.Select(dto => new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Category = dto.Category,
                Action = dto.Action,
                ActorId = dto.ActorId,
                ActorType = dto.ActorType,
                TargetType = dto.TargetType,
                TargetId = dto.TargetId,
                GuildId = dto.GuildId,
                Details = dto.Details,
                IpAddress = dto.IpAddress,
                CorrelationId = dto.CorrelationId
            }).ToList();

            // Bulk insert
            await repository.BulkInsertAsync(entities, cancellationToken);

            _logger.LogInformation(
                "Successfully wrote batch of {Count} audit log entries to database",
                batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write batch of {Count} audit log entries to database. Entries will be lost: {Entries}",
                batch.Count,
                string.Join(", ", batch.Select(b => $"{b.Category}.{b.Action}")));

            // Note: In a production system, you might want to implement a dead-letter queue
            // or retry logic here to prevent losing audit logs during database failures.
        }
    }
}
