namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the OpenRouter LLM integration.
/// </summary>
/// <remarks>
/// OpenRouter exposes an OpenAI-compatible chat-completions API in front of many model providers,
/// so model names here are OpenRouter slugs (e.g. "anthropic/claude-sonnet-4", "openai/gpt-4o")
/// rather than a single vendor's model IDs.
/// </remarks>
public class OpenRouterOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "OpenRouter";

    /// <summary>
    /// Gets or sets the OpenRouter API key.
    /// This should be configured via user secrets, never in appsettings.json.
    /// </summary>
    /// <remarks>
    /// Required for LLM access. If not configured, the assistant features are not registered.
    /// Set via user secrets: dotnet user-secrets set "OpenRouter:ApiKey" "your-api-key-here",
    /// or via the OpenRouter__ApiKey environment variable.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the OpenRouter API base URL.
    /// Default is "https://openrouter.ai/api/v1/".
    /// </summary>
    /// <remarks>
    /// The trailing slash matters: request paths are relative to this address.
    /// </remarks>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";

    /// <summary>
    /// Gets or sets the default model slug to use when a request does not name one.
    /// Default is "anthropic/claude-sonnet-4".
    /// </summary>
    /// <remarks>
    /// Any slug from https://openrouter.ai/models works. Prompt caching is only honoured for
    /// Claude-family slugs; other models simply report zero cached tokens.
    /// </remarks>
    public string DefaultModel { get; set; } = "anthropic/claude-sonnet-4";

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// Default is 3.
    /// </summary>
    /// <remarks>
    /// Retries use exponential backoff. Set to 0 to disable retries.
    /// Only retries on transient failures (HTTP 408/429/5xx, timeouts, network errors).
    /// </remarks>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    /// <remarks>
    /// LLM calls can take time for large context windows or complex tool use.
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
    /// Adds a cache breakpoint to the system prompt. OpenRouter passes this through to
    /// Claude-family models and ignores it elsewhere, so leaving it on is safe for any slug.
    /// Individual requests can override this via LlmRequest.EnablePromptCaching.
    /// </remarks>
    public bool EnablePromptCachingByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets the site URL reported to OpenRouter via the HTTP-Referer header.
    /// Optional; used for attribution on openrouter.ai rankings.
    /// </summary>
    public string? AppUrl { get; set; }

    /// <summary>
    /// Gets or sets the application name reported to OpenRouter via the X-Title header.
    /// Default is "DiscordBot".
    /// </summary>
    public string? AppTitle { get; set; } = "DiscordBot";
}
