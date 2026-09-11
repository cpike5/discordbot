using System.Threading;
using System.Threading.Channels;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.LLM;

/// <summary>
/// <see cref="ILlmUsageRecorder"/> over a bounded <see cref="Channel{T}"/>, matching
/// <c>AuditLogQueue</c>'s posture: <see cref="Record"/> is a non-blocking <c>TryWrite</c>, and a
/// full queue drops the oldest entry rather than applying backpressure to the caller. Drained by
/// <see cref="LlmUsageRecordProcessor"/>. Capacity is <c>Llm:UsageQueueCapacity</c> (default
/// 10,000).
/// </summary>
public class LlmUsageRecorder : ILlmUsageRecorder
{
    /// <summary>Log the first drop immediately, then only every Nth after that, so a sustained
    /// overload doesn't spam the log at one line per dropped record.</summary>
    private const int LogEveryNDrops = 100;

    private readonly Channel<LlmUsageRecord> _channel;
    private readonly ILogger<LlmUsageRecorder> _logger;
    private readonly int _capacity;
    private long _totalDropped;

    public LlmUsageRecorder(IOptions<LlmOptions> options, ILogger<LlmUsageRecorder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _capacity = options?.Value.UsageQueueCapacity ?? 10000;

        _channel = Channel.CreateBounded<LlmUsageRecord>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public void Record(LlmUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        // With FullMode.DropOldest, TryWrite only ever returns false once the channel's writer has
        // been completed (never happens in normal operation) - a full queue instead silently
        // discards the oldest entry to make room, and still returns true. Detect that case by
        // comparing depth immediately before and after: a write into an already-full queue leaves
        // Reader.Count unchanged (one out, one in) instead of growing by one. This is a best-effort
        // signal under concurrent writers, not an exact count, which is fine for a rate-limited log.
        var countBefore = _channel.Reader.Count;
        var written = _channel.Writer.TryWrite(record);

        if (!written)
        {
            _logger.LogWarning(
                "Failed to enqueue LLM usage record for user {UserId}, mode {Mode}. Queue may be closed.",
                record.UserId, record.Mode);
            return;
        }

        if (countBefore >= _capacity)
        {
            var dropped = Interlocked.Increment(ref _totalDropped);
            if (dropped == 1 || dropped % LogEveryNDrops == 0)
            {
                _logger.LogWarning(
                    "LLM usage record queue is full (capacity {Capacity}); dropped the oldest entry " +
                    "to enqueue this one (user {UserId}, mode {Mode}). Total dropped so far: {TotalDropped}",
                    _capacity, record.UserId, record.Mode, dropped);
            }
        }
    }

    /// <summary>Dequeues one record for the background processor. Waits until one is available or cancellation fires.</summary>
    internal ValueTask<LlmUsageRecord> DequeueAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken);

    /// <summary>Current queue depth, for the processor's shutdown drain.</summary>
    internal int Count => _channel.Reader.Count;

    /// <summary>Total records dropped for capacity since this recorder was created. Exposed for tests.</summary>
    internal long TotalDropped => Interlocked.Read(ref _totalDropped);
}
