using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.FeatureRequests;

/// <summary>
/// Thread-safe bounded channel implementation for the doc generation queue.
/// Capacity is 500 items; oldest items are dropped under extreme load.
/// </summary>
public class FeatureRequestDocGenQueue : IFeatureRequestDocGenQueue
{
    private readonly Channel<Guid> _channel;
    private readonly ILogger<FeatureRequestDocGenQueue> _logger;

    public FeatureRequestDocGenQueue(ILogger<FeatureRequestDocGenQueue> logger)
    {
        _logger = logger;

        var options = new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<Guid>(options);
    }

    /// <inheritdoc/>
    public void Enqueue(Guid featureRequestId)
    {
        if (!_channel.Writer.TryWrite(featureRequestId))
        {
            _logger.LogWarning(
                "Failed to enqueue feature request {FeatureRequestId} for doc generation. Queue may be closed.",
                featureRequestId);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public int Count => _channel.Reader.Count;
}
