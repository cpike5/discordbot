using System.Threading.Channels;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Interface for a thread-safe queue that stores guild member sync requests for background processing.
/// </summary>
public interface IMemberSyncQueue
{
    /// <summary>
    /// Enqueues a guild for member synchronization.
    /// This is a non-blocking operation.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="reason">The reason for the sync request.</param>
    void EnqueueGuild(ulong guildId, MemberSyncReason reason);

    /// <summary>
    /// Dequeues a guild member sync request for processing.
    /// This method will wait asynchronously until an item is available or the cancellation token is triggered.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing the guild ID and sync reason.</returns>
    ValueTask<(ulong GuildId, MemberSyncReason Reason)> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Reason for member sync request.
/// </summary>
public enum MemberSyncReason
{
    /// <summary>
    /// Initial sync on application startup.
    /// </summary>
    InitialSync,

    /// <summary>
    /// Daily reconciliation to catch missed events.
    /// </summary>
    DailyReconciliation,

    /// <summary>
    /// Manual sync requested via admin UI or command.
    /// </summary>
    ManualRequest,

    /// <summary>
    /// Bot joined a new guild and needs to sync members.
    /// </summary>
    BotJoinedGuild
}

/// <summary>
/// Thread-safe queue implementation for member sync requests using System.Threading.Channels.
/// Provides high-performance, bounded queue with backpressure handling.
/// </summary>
public class MemberSyncQueue : IMemberSyncQueue
{
    private readonly Channel<(ulong GuildId, MemberSyncReason Reason)> _channel;
    private readonly ILogger<MemberSyncQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberSyncQueue"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MemberSyncQueue(ILogger<MemberSyncQueue> logger)
    {
        _logger = logger;

        // Create a bounded channel with a capacity of 1000 entries
        // Using DropOldest strategy to prevent blocking under extreme load
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,  // Only one background processor reads
            SingleWriter = false  // Multiple threads can write
        };

        _channel = Channel.CreateBounded<(ulong GuildId, MemberSyncReason Reason)>(options);
    }

    /// <inheritdoc/>
    public void EnqueueGuild(ulong guildId, MemberSyncReason reason)
    {
        if (!_channel.Writer.TryWrite((guildId, reason)))
        {
            // This should rarely happen due to DropOldest strategy
            // Log a warning if we couldn't write (likely channel is closed)
            _logger.LogWarning(
                "Failed to enqueue member sync for guild {GuildId} with reason {Reason}. Queue may be closed.",
                guildId, reason);
        }
        else
        {
            _logger.LogDebug(
                "Enqueued member sync for guild {GuildId} with reason {Reason}. Queue count: {Count}",
                guildId, reason, Count);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(ulong GuildId, MemberSyncReason Reason)> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public int Count => _channel.Reader.Count;
}
