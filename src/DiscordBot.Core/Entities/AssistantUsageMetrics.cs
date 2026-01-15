namespace DiscordBot.Core.Entities;

/// <summary>
/// Aggregated daily usage metrics for the AI assistant feature.
/// Tracks token usage, costs, and question counts per guild.
/// Used for cost monitoring, usage analytics, and budget management.
/// </summary>
public class AssistantUsageMetrics
{
    /// <summary>
    /// Unique identifier for this metrics record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Date for which metrics are aggregated (UTC date only, no time component).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Total number of questions asked on this date.
    /// </summary>
    public int TotalQuestions { get; set; } = 0;

    /// <summary>
    /// Total input tokens consumed (non-cached).
    /// </summary>
    public int TotalInputTokens { get; set; } = 0;

    /// <summary>
    /// Total output tokens consumed.
    /// </summary>
    public int TotalOutputTokens { get; set; } = 0;

    /// <summary>
    /// Total cached tokens served from prompt cache.
    /// </summary>
    public int TotalCachedTokens { get; set; } = 0;

    /// <summary>
    /// Total tokens written to cache (on cache miss).
    /// </summary>
    public int TotalCacheWriteTokens { get; set; } = 0;

    /// <summary>
    /// Total number of cache hits.
    /// </summary>
    public int TotalCacheHits { get; set; } = 0;

    /// <summary>
    /// Total number of cache misses.
    /// </summary>
    public int TotalCacheMisses { get; set; } = 0;

    /// <summary>
    /// Total number of tool calls executed.
    /// </summary>
    public int TotalToolCalls { get; set; } = 0;

    /// <summary>
    /// Estimated total cost in USD for this date.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; } = 0m;

    /// <summary>
    /// Number of failed requests (API errors, timeouts).
    /// </summary>
    public int FailedRequests { get; set; } = 0;

    /// <summary>
    /// Average response latency in milliseconds.
    /// </summary>
    public int AverageLatencyMs { get; set; } = 0;

    /// <summary>
    /// Timestamp when this record was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }
}
