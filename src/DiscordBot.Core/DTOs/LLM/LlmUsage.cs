namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Token usage metrics from an LLM response.
/// </summary>
public class LlmUsage
{
    /// <summary>
    /// Number of input tokens consumed (non-cached).
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Number of output tokens generated.
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Number of tokens served from cache.
    /// </summary>
    public int CachedTokens { get; set; }

    /// <summary>
    /// Number of tokens written to cache (on cache miss).
    /// </summary>
    public int CacheWriteTokens { get; set; }

    /// <summary>
    /// Total tokens for billing/monitoring purposes.
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Estimated cost in USD if provider supports billing calculation.
    /// </summary>
    public decimal? EstimatedCost { get; set; }
}
