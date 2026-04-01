namespace DiscordBot.Infrastructure.Services.FeatureRequests;

/// <summary>
/// Thread-safe queue that delivers feature request IDs to the background doc generation processor.
/// </summary>
public interface IFeatureRequestDocGenQueue
{
    /// <summary>
    /// Enqueues a feature request ID for doc generation. Non-blocking.
    /// </summary>
    void Enqueue(Guid featureRequestId);

    /// <summary>
    /// Dequeues a feature request ID, waiting asynchronously until one is available
    /// or the cancellation token is triggered.
    /// </summary>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current number of items waiting in the queue.
    /// </summary>
    int Count { get; }
}
