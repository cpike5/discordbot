using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for caching FFmpeg-processed PCM audio files.
/// Reduces playback latency by storing transcoded audio for frequently-played sounds.
/// </summary>
public interface ISoundCacheService
{
    /// <summary>
    /// Attempts to get a cached audio stream for the specified sound and filter combination.
    /// </summary>
    /// <param name="soundId">The unique identifier of the sound.</param>
    /// <param name="filter">The audio filter applied to the sound.</param>
    /// <param name="sourceFileModifiedUtc">The last modified time of the source file (for invalidation).</param>
    /// <returns>
    /// A readable stream of the cached PCM audio if found and valid, or null if not cached.
    /// The caller is responsible for disposing the stream.
    /// </returns>
    Task<Stream?> TryGetAsync(Guid soundId, AudioFilter filter, DateTime sourceFileModifiedUtc);

    /// <summary>
    /// Stores processed PCM audio in the cache.
    /// </summary>
    /// <param name="soundId">The unique identifier of the sound.</param>
    /// <param name="filter">The audio filter applied to the sound.</param>
    /// <param name="pcmData">The PCM audio data to cache.</param>
    /// <param name="sourceFileModifiedUtc">The last modified time of the source file (for invalidation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the data was cached successfully, false if caching was skipped (e.g., too large).</returns>
    Task<bool> StoreAsync(Guid soundId, AudioFilter filter, byte[] pcmData, DateTime sourceFileModifiedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached entries for a specific sound (e.g., when the source file is updated).
    /// </summary>
    /// <param name="soundId">The unique identifier of the sound to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAsync(Guid soundId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes expired entries and enforces size limits on the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries removed during cleanup.</returns>
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current cache statistics.
    /// </summary>
    /// <returns>Cache statistics including hit rate, size, and entry count.</returns>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Statistics about the audio cache.
/// </summary>
public record CacheStatistics
{
    /// <summary>
    /// Total number of cache hits since service start.
    /// </summary>
    public long HitCount { get; init; }

    /// <summary>
    /// Total number of cache misses since service start.
    /// </summary>
    public long MissCount { get; init; }

    /// <summary>
    /// Cache hit rate as a percentage (0-100).
    /// </summary>
    public double HitRate => HitCount + MissCount > 0
        ? (double)HitCount / (HitCount + MissCount) * 100
        : 0;

    /// <summary>
    /// Current number of entries in the cache.
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Current total size of cached files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }
}
