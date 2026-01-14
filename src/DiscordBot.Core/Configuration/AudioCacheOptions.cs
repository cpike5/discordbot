namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the audio cache that stores FFmpeg-processed PCM audio.
/// Reduces playback latency by caching transcoded audio for frequently-played sounds.
/// </summary>
public class AudioCacheOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "AudioCache";

    /// <summary>
    /// Gets or sets whether audio caching is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the base path for cached audio files.
    /// Default is "./cache/audio". Use forward slashes for cross-platform compatibility;
    /// paths are normalized at runtime using Path.Combine.
    /// </summary>
    public string CachePath { get; set; } = "./cache/audio";

    /// <summary>
    /// Gets or sets the maximum total size of the cache in bytes.
    /// When exceeded, least recently used entries are evicted.
    /// Default is 500MB (524,288,000 bytes).
    /// </summary>
    public long MaxCacheSizeBytes { get; set; } = 524_288_000;

    /// <summary>
    /// Gets or sets the maximum number of cached entries.
    /// When exceeded, least recently used entries are evicted.
    /// Default is 1000 entries.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the time-to-live for cache entries in hours.
    /// Entries older than this are eligible for eviction even if below size limits.
    /// Default is 168 hours (7 days).
    /// </summary>
    public int EntryTtlHours { get; set; } = 168;

    /// <summary>
    /// Gets or sets the maximum duration of sounds to cache in seconds.
    /// Sounds longer than this duration will not be cached to avoid excessive disk usage.
    /// Default is 60 seconds.
    /// </summary>
    public int MaxCacheDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the interval in minutes between cache cleanup runs.
    /// Cleanup removes expired entries and enforces size limits.
    /// Default is 60 minutes.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}
