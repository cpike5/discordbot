namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for Anthropic Claude API integration.
/// </summary>
public class AnthropicOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Gets or sets the Anthropic API key.
    /// This should be configured via user secrets, never in appsettings.json.
    /// </summary>
    /// <remarks>
    /// Required for Claude API access. If not configured, the Anthropic LLM client will be disabled.
    /// Set via user secrets: dotnet user-secrets set "Anthropic:ApiKey" "your-api-key-here"
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the default Claude model to use.
    /// Default is "claude-sonnet-4-20250514".
    /// </summary>
    /// <remarks>
    /// Available models: claude-opus-4-20250514, claude-sonnet-4-20250514, claude-haiku-4-20250514.
    /// Model names may change with new releases. Check Anthropic documentation for current model names.
    /// </remarks>
    public string DefaultModel { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// Default is 3.
    /// </summary>
    /// <remarks>
    /// Retries use exponential backoff. Set to 0 to disable retries.
    /// Only retries on transient errors (rate limits, timeouts, 5xx errors).
    /// </remarks>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    /// <remarks>
    /// Claude API calls can take time for large context windows or complex tool use.
    /// Adjust based on your use case and expected response times.
    /// </remarks>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the base delay in milliseconds for exponential backoff.
    /// Default is 1000ms (1 second).
    /// </summary>
    /// <remarks>
    /// Delay formula: baseDelay * (2 ^ retryAttempt).
    /// Example with base 1000ms: 1s, 2s, 4s for attempts 0, 1, 2.
    /// </remarks>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to enable automatic prompt caching by default.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Prompt caching can significantly reduce costs for repeated requests with similar prompts.
    /// Individual requests can override this setting via LlmRequest.EnablePromptCaching.
    /// </remarks>
    public bool EnablePromptCachingByDefault { get; set; } = true;
}
