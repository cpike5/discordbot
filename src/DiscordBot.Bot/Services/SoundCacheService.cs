using System.Collections.Concurrent;
using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// File-based cache service for FFmpeg-processed PCM audio.
/// Stores transcoded audio to reduce playback latency for frequently-played sounds.
/// </summary>
public class SoundCacheService : ISoundCacheService, IDisposable
{
    private readonly ILogger<SoundCacheService> _logger;
    private readonly AudioCacheOptions _options;
    private readonly string _cachePath;
    private readonly string _metadataPath;

    private readonly ConcurrentDictionary<string, CacheEntryMetadata> _metadata = new();
    private readonly SemaphoreSlim _metadataLock = new(1, 1);

    private long _hitCount;
    private long _missCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundCacheService"/> class.
    /// </summary>
    public SoundCacheService(
        ILogger<SoundCacheService> logger,
        IOptions<AudioCacheOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _cachePath = Path.GetFullPath(_options.CachePath);
        _metadataPath = Path.Combine(_cachePath, "metadata.json");

        InitializeCache();
    }

    /// <inheritdoc/>
    public async Task<Stream?> TryGetAsync(Guid soundId, AudioFilter filter, DateTime sourceFileModifiedUtc)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var cacheKey = BuildCacheKey(soundId, filter);
        var filePath = GetCacheFilePath(cacheKey);

        // Check if entry exists in metadata
        if (!_metadata.TryGetValue(cacheKey, out var entry))
        {
            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Cache miss for {CacheKey}: no metadata entry", cacheKey);
            return null;
        }

        // Check if source file has been modified (invalidation)
        if (entry.SourceFileModifiedUtc < sourceFileModifiedUtc)
        {
            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Cache miss for {CacheKey}: source file modified ({CachedTime} < {SourceTime})",
                cacheKey, entry.SourceFileModifiedUtc, sourceFileModifiedUtc);

            // Remove stale entry
            await RemoveEntryAsync(cacheKey);
            return null;
        }

        // Check if file exists on disk
        if (!File.Exists(filePath))
        {
            Interlocked.Increment(ref _missCount);
            _logger.LogWarning("Cache miss for {CacheKey}: metadata exists but file missing", cacheKey);

            // Remove orphaned metadata
            _metadata.TryRemove(cacheKey, out _);
            return null;
        }

        // Update last access time
        entry.LastAccessedUtc = DateTime.UtcNow;

        Interlocked.Increment(ref _hitCount);
        _logger.LogDebug("Cache hit for {CacheKey} ({SizeBytes} bytes)", cacheKey, entry.SizeBytes);

        try
        {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open cached file for {CacheKey}", cacheKey);
            Interlocked.Decrement(ref _hitCount);
            Interlocked.Increment(ref _missCount);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StoreAsync(Guid soundId, AudioFilter filter, byte[] pcmData, DateTime sourceFileModifiedUtc, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        // Check if data is too large (would exceed per-sound limit based on duration)
        // PCM format: 48kHz * 2 channels * 2 bytes = 192,000 bytes per second
        const int bytesPerSecond = 192_000;
        var maxBytes = _options.MaxCacheDurationSeconds * bytesPerSecond;
        if (pcmData.Length > maxBytes)
        {
            _logger.LogDebug("Skipping cache for sound {SoundId}: data too large ({DataSize} > {MaxSize} bytes)",
                soundId, pcmData.Length, maxBytes);
            return false;
        }

        var cacheKey = BuildCacheKey(soundId, filter);
        var filePath = GetCacheFilePath(cacheKey);

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write PCM data to file
            await File.WriteAllBytesAsync(filePath, pcmData, cancellationToken);

            // Update metadata
            var entry = new CacheEntryMetadata
            {
                CacheKey = cacheKey,
                SoundId = soundId,
                Filter = filter,
                SizeBytes = pcmData.Length,
                CreatedUtc = DateTime.UtcNow,
                LastAccessedUtc = DateTime.UtcNow,
                SourceFileModifiedUtc = sourceFileModifiedUtc
            };

            _metadata[cacheKey] = entry;

            _logger.LogDebug("Cached audio for {CacheKey} ({SizeBytes} bytes)", cacheKey, pcmData.Length);

            // Check if cleanup is needed
            await EnforceLimitsAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache audio for {CacheKey}", cacheKey);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(Guid soundId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _metadata.Keys
            .Where(k => k.StartsWith($"{soundId}_"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            await RemoveEntryAsync(key);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogInformation("Invalidated {Count} cache entries for sound {SoundId}", keysToRemove.Count, soundId);
            await SaveMetadataAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var removedCount = 0;
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromHours(_options.EntryTtlHours);

        // Remove expired entries
        var expiredKeys = _metadata
            .Where(kvp => now - kvp.Value.LastAccessedUtc > ttl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await RemoveEntryAsync(key);
            removedCount++;
        }

        // Enforce size limits
        removedCount += await EnforceLimitsAsync(cancellationToken);

        if (removedCount > 0)
        {
            await SaveMetadataAsync(cancellationToken);
            _logger.LogInformation("Cache cleanup removed {Count} entries", removedCount);
        }

        return removedCount;
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        var totalSize = _metadata.Values.Sum(e => e.SizeBytes);

        return new CacheStatistics
        {
            HitCount = Interlocked.Read(ref _hitCount),
            MissCount = Interlocked.Read(ref _missCount),
            EntryCount = _metadata.Count,
            TotalSizeBytes = totalSize
        };
    }

    /// <summary>
    /// Builds a cache key from sound ID and filter.
    /// </summary>
    private static string BuildCacheKey(Guid soundId, AudioFilter filter)
    {
        return $"{soundId}_{filter}";
    }

    /// <summary>
    /// Gets the file path for a cache entry.
    /// </summary>
    private string GetCacheFilePath(string cacheKey)
    {
        return Path.Combine(_cachePath, $"{cacheKey}.pcm");
    }

    /// <summary>
    /// Initializes the cache directory and loads existing metadata.
    /// </summary>
    private void InitializeCache()
    {
        try
        {
            // Ensure cache directory exists
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
                _logger.LogInformation("Created audio cache directory: {CachePath}", _cachePath);
            }

            // Load existing metadata
            if (File.Exists(_metadataPath))
            {
                var json = File.ReadAllText(_metadataPath);
                var entries = JsonSerializer.Deserialize<List<CacheEntryMetadata>>(json);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        // Verify file still exists
                        var filePath = GetCacheFilePath(entry.CacheKey);
                        if (File.Exists(filePath))
                        {
                            _metadata[entry.CacheKey] = entry;
                        }
                    }

                    _logger.LogInformation("Loaded {Count} audio cache entries from metadata", _metadata.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize audio cache");
        }
    }

    /// <summary>
    /// Removes a cache entry (file and metadata).
    /// </summary>
    private async Task RemoveEntryAsync(string cacheKey)
    {
        if (_metadata.TryRemove(cacheKey, out _))
        {
            var filePath = GetCacheFilePath(cacheKey);
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cache file for {CacheKey}", cacheKey);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Enforces cache size and entry count limits using LRU eviction.
    /// </summary>
    private async Task<int> EnforceLimitsAsync(CancellationToken cancellationToken)
    {
        var removedCount = 0;

        // Get entries sorted by last access time (LRU)
        var sortedEntries = _metadata.Values
            .OrderBy(e => e.LastAccessedUtc)
            .ToList();

        var currentSize = sortedEntries.Sum(e => e.SizeBytes);
        var currentCount = sortedEntries.Count;

        // Remove entries until within limits
        foreach (var entry in sortedEntries)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var overSize = currentSize > _options.MaxCacheSizeBytes;
            var overCount = currentCount > _options.MaxEntries;

            if (!overSize && !overCount) break;

            await RemoveEntryAsync(entry.CacheKey);
            currentSize -= entry.SizeBytes;
            currentCount--;
            removedCount++;

            _logger.LogDebug("Evicted cache entry {CacheKey} (LRU)", entry.CacheKey);
        }

        return removedCount;
    }

    /// <summary>
    /// Persists metadata to disk.
    /// </summary>
    private async Task SaveMetadataAsync(CancellationToken cancellationToken)
    {
        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            var entries = _metadata.Values.ToList();
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_metadataPath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audio cache metadata");
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Save metadata on shutdown
        try
        {
            SaveMetadataAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audio cache metadata on shutdown");
        }

        _metadataLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Metadata for a cached audio entry.
    /// </summary>
    private class CacheEntryMetadata
    {
        public string CacheKey { get; set; } = string.Empty;
        public Guid SoundId { get; set; }
        public AudioFilter Filter { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastAccessedUtc { get; set; }
        public DateTime SourceFileModifiedUtc { get; set; }
    }
}
