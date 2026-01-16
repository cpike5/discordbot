namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the AI assistant feature.
/// Controls Claude API integration, rate limiting, documentation tools, and response behavior.
/// </summary>
public class AssistantOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Assistant";

    /// <summary>
    /// Gets or sets whether the assistant feature is enabled globally.
    /// Individual guilds can still disable it even if globally enabled.
    /// Default is false (disabled until explicitly enabled).
    /// </summary>
    public bool GloballyEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether new guilds have the assistant enabled by default.
    /// Default is false (guilds must opt-in).
    /// </summary>
    public bool EnabledByDefaultForNewGuilds { get; set; } = false;

    #region Rate Limiting

    /// <summary>
    /// Gets or sets the default maximum number of questions a user can ask within the rate limit window.
    /// Guilds can override this value.
    /// Default is 5 questions per 5 minutes.
    /// </summary>
    public int DefaultRateLimit { get; set; } = 5;

    /// <summary>
    /// Gets or sets the time window for rate limiting in minutes.
    /// Default is 5 minutes.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum role required to bypass rate limits.
    /// Roles at or above this level are not rate limited.
    /// Default is "Admin" (Admin and SuperAdmin bypass).
    /// </summary>
    /// <remarks>
    /// Valid values: "SuperAdmin", "Admin", "Moderator", "Viewer", or null (no bypass).
    /// </remarks>
    public string? RateLimitBypassRole { get; set; } = "Admin";

    #endregion

    #region Message Constraints

    /// <summary>
    /// Gets or sets the maximum length of a user's question in characters.
    /// Questions longer than this will be rejected.
    /// Default is 500 characters.
    /// </summary>
    public int MaxQuestionLength { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum length of Claude's response in characters.
    /// Responses exceeding this will be truncated with a suffix.
    /// Default is 1800 (leaves buffer for Discord's 2000 char limit).
    /// </summary>
    public int MaxResponseLength { get; set; } = 1800;

    /// <summary>
    /// Gets or sets the suffix appended when responses are truncated.
    /// Default is "\n\n... *(response truncated)*".
    /// </summary>
    public string TruncationSuffix { get; set; } = "\n\n... *(response truncated)*";

    #endregion

    #region Claude API Configuration

    /// <summary>
    /// Gets or sets the Claude model identifier to use.
    /// Default is "claude-3-5-sonnet-20241022".
    /// </summary>
    /// <remarks>
    /// Available models:
    /// - claude-3-5-sonnet-20241022 (recommended for balance of speed/quality)
    /// - claude-3-opus-20240229 (highest quality, slower, more expensive)
    /// - claude-3-haiku-20240307 (fastest, cheapest, lower quality)
    /// </remarks>
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";

    /// <summary>
    /// Gets or sets the timeout for Claude API calls in milliseconds.
    /// Default is 30000 (30 seconds).
    /// </summary>
    public int ApiTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the maximum number of tokens for Claude's response.
    /// Controls response length and API costs.
    /// Default is 512 tokens (~375 words) to encourage concise responses.
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    /// Gets or sets the temperature for Claude's responses (0.0 to 1.0).
    /// Lower values are more focused and deterministic, higher values are more creative.
    /// Default is 0.7 (balanced).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    #endregion

    #region Prompt and Documentation Paths

    /// <summary>
    /// Gets or sets the path to the agent behavior/security prompt file.
    /// Supports placeholders: {GUILD_ID}, {BASE_URL}
    /// Default is "docs/agents/assistant-agent.md".
    /// </summary>
    public string AgentPromptPath { get; set; } = "docs/agents/assistant-agent.md";

    /// <summary>
    /// Gets or sets the base directory for documentation files.
    /// Used by documentation tools to locate feature docs.
    /// Default is "docs/articles".
    /// </summary>
    public string DocumentationBasePath { get; set; } = "docs/articles";

    /// <summary>
    /// Gets or sets the path to the README file for command lists.
    /// Default is "README.md".
    /// </summary>
    public string ReadmePath { get; set; } = "README.md";

    #endregion

    #region Tool Configuration

    /// <summary>
    /// Gets or sets whether documentation tools are enabled.
    /// If false, Claude will only use the agent prompt without tool access.
    /// Default is true.
    /// </summary>
    public bool EnableDocumentationTools { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of tool calls Claude can make per question.
    /// Prevents infinite loops and controls API costs.
    /// Default is 5.
    /// </summary>
    public int MaxToolCallsPerQuestion { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout for individual tool executions in milliseconds.
    /// Default is 5000 (5 seconds).
    /// </summary>
    public int ToolExecutionTimeoutMs { get; set; } = 5000;

    #endregion

    #region Error Handling and Retry

    /// <summary>
    /// Gets or sets the friendly error message shown to users when the API fails.
    /// Default is "Oops, I'm having trouble thinking right now. Please try again in a moment."
    /// </summary>
    public string ErrorMessage { get; set; } = "Oops, I'm having trouble thinking right now. Please try again in a moment.";

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed API calls.
    /// Default is 2 (1 initial attempt + 2 retries = 3 total attempts).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay between retry attempts in milliseconds.
    /// Default is 1000 (1 second).
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    #endregion

    #region Cost Monitoring and Alerts

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

    #endregion

    #region Privacy and Audit

    /// <summary>
    /// Gets or sets whether users must explicitly opt-in via /consent before using the assistant.
    /// Default is true (explicit consent required).
    /// </summary>
    /// <remarks>
    /// When true, users must run /consent and enable "assistant_usage" before asking questions.
    /// When false, mentioning the bot implies consent (simpler UX, but less privacy control).
    /// </remarks>
    public bool RequireExplicitConsent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log user questions and Claude responses for audit/debugging.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When enabled, questions and responses are logged to the existing audit log system.
    /// Users are informed via /privacy command that questions are processed by Claude API.
    /// </remarks>
    public bool LogInteractions { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain assistant interaction logs.
    /// Aligns with message log retention policy.
    /// Default is 90 days.
    /// </summary>
    public int InteractionLogRetentionDays { get; set; } = 90;

    #endregion

    #region Prompt Caching

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

    #endregion

    #region URL Generation

    /// <summary>
    /// Gets or sets the base URL for generating links in responses.
    /// Uses ApplicationOptions.BaseUrl by default if not set.
    /// Example: "https://discordbot.cpike.ca"
    /// </summary>
    /// <remarks>
    /// Base URL is passed to tools (not embedded in cached system prompt).
    /// Tools generate guild-specific URLs like:
    /// - {BASE_URL}/Portal/Soundboard/{GUILD_ID}
    /// - {BASE_URL}/Portal/TTS/{GUILD_ID}
    /// Guild ID is provided via tool context, not in cached prompt (to maintain cache sharing).
    /// </remarks>
    public string? BaseUrl { get; set; }

    #endregion

    #region Feature Flags

    /// <summary>
    /// Gets or sets whether to show typing indicator while waiting for Claude's response.
    /// Default is true.
    /// </summary>
    public bool ShowTypingIndicator { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include guild-specific context in the agent prompt.
    /// If true, {GUILD_ID} placeholder is replaced with actual guild ID.
    /// Default is true (needed for URL generation).
    /// </summary>
    /// <remarks>
    /// User ID is never passed to Claude for privacy reasons.
    /// Only guild ID is included to enable guild-specific URL generation.
    /// </remarks>
    public bool IncludeGuildContext { get; set; } = true;

    #endregion
}
