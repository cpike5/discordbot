namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// Cost tracking and prompt-caching configuration for the guild AI assistant.
/// Binds under "Assistant:Cost" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantCostOptions
{
    /// <summary>
    /// Gets or sets whether to track and log token usage for cost monitoring.
    /// Default is true.
    /// </summary>
    public bool EnableCostTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets the daily cost threshold in USD for performance alerts.
    /// If daily costs exceed this, an alert incident will be created.
    /// Default is 5.00 (alerts if costs exceed $5/day).
    /// </summary>
    /// <remarks>
    /// Costs are estimated based on Claude API pricing:
    /// - Input tokens: ~$3 per million tokens
    /// - Output tokens: ~$15 per million tokens
    /// Set to a reasonable daily budget (e.g., 1.00 for $1/day).
    /// </remarks>
    public decimal? DailyCostThresholdUsd { get; set; } = 5.00m;

    /// <summary>
    /// Gets or sets the cost per million input tokens in USD for cost estimation.
    /// Default is 3.00 (Claude 3.5 Sonnet pricing).
    /// </summary>
    public decimal CostPerMillionInputTokens { get; set; } = 3.00m;

    /// <summary>
    /// Gets or sets the cost per million output tokens in USD for cost estimation.
    /// Default is 15.00 (Claude 3.5 Sonnet pricing).
    /// </summary>
    public decimal CostPerMillionOutputTokens { get; set; } = 15.00m;

    /// <summary>
    /// Gets or sets whether to use Claude's Prompt Caching for agent prompt and common docs.
    /// Reduces costs by ~50% and improves latency for cached content.
    /// Cache is valid for 5 minutes and shared across all requests.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Prompt caching pricing (Claude 3.5 Sonnet):
    /// - Regular input tokens: $3.00 per million
    /// - Cached input tokens: $0.30 per million (90% discount)
    /// - Cache write: $3.75 per million (only on cache miss)
    /// With typical usage, expect 50%+ cost reduction.
    /// </remarks>
    public bool EnablePromptCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to pre-cache common documentation in the system message.
    /// When true, frequently-accessed docs are included in cached prompt.
    /// When false, all docs are fetched via tools only (smaller prompts, more tool calls).
    /// Default is true.
    /// </summary>
    public bool CacheCommonDocumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of documentation files to include in cached prompt.
    /// Only used if CacheCommonDocumentation is true.
    /// Files are loaded from DocumentationBasePath.
    /// Default includes most frequently requested features.
    /// </summary>
    public string[] CachedDocumentationFiles { get; set; } =
    [
        "commands-page.md",    // Command overview and metadata
        "soundboard.md",       // Most requested feature
        "rat-watch.md",        // Second most requested
        "tts-support.md"       // TTS feature documentation
    ];

    /// <summary>
    /// Gets or sets the cost per million cached input tokens in USD for cost estimation.
    /// Cached tokens cost 90% less than regular input tokens.
    /// Default is 0.30 (Claude 3.5 Sonnet caching pricing).
    /// </summary>
    public decimal CostPerMillionCachedTokens { get; set; } = 0.30m;

    /// <summary>
    /// Gets or sets the cost per million cache write tokens in USD for cost estimation.
    /// Cache writes occur on cache miss (first request or after 5-min expiry).
    /// Default is 3.75 (Claude 3.5 Sonnet cache write pricing).
    /// </summary>
    public decimal CostPerMillionCacheWriteTokens { get; set; } = 3.75m;
}
