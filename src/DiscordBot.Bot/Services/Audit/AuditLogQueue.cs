using DiscordBot.Core.DTOs;
using System.Threading.Channels;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Interface for a thread-safe queue that stores audit log entries for background processing.
/// </summary>
public interface IAuditLogQueue
{
    /// <summary>
    /// Enqueues an audit log entry for background processing.
    /// This is a non-blocking operation.
    /// </summary>
    /// <param name="dto">The audit log data to enqueue.</param>
    void Enqueue(AuditLogCreateDto dto);

    /// <summary>
    /// Dequeues an audit log entry for processing.
    /// This method will wait asynchronously until an item is available or the cancellation token is triggered.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The dequeued audit log entry.</returns>
    ValueTask<AuditLogCreateDto> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Thread-safe queue implementation for audit log entries using System.Threading.Channels.
/// Provides high-performance, bounded queue with backpressure handling.
/// </summary>
public class AuditLogQueue : IAuditLogQueue
{
    private readonly Channel<AuditLogCreateDto> _channel;
    private readonly ILogger<AuditLogQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogQueue"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AuditLogQueue(ILogger<AuditLogQueue> logger)
    {
        _logger = logger;

        // Create a bounded channel with a capacity of 10,000 entries
        // Using DropOldest strategy to prevent blocking under extreme load
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,  // Only one background processor reads
            SingleWriter = false  // Multiple threads can write
        };

        _channel = Channel.CreateBounded<AuditLogCreateDto>(options);
    }

    /// <inheritdoc/>
    public void Enqueue(AuditLogCreateDto dto)
    {
        if (!_channel.Writer.TryWrite(dto))
        {
            // This should rarely happen due to DropOldest strategy
            // Log a warning if we couldn't write (likely channel is closed)
            _logger.LogWarning(
                "Failed to enqueue audit log entry for {Category}.{Action}. Queue may be closed.",
                dto.Category, dto.Action);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AuditLogCreateDto> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public int Count => _channel.Reader.Count;
}
