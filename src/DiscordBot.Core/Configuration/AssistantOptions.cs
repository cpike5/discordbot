using DiscordBot.Core.Configuration.Assistant;

namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the AI assistant feature.
/// Controls Claude API integration, rate limiting, documentation tools, and response behavior.
/// </summary>
/// <remarks>
/// Settings are grouped into cohesive nested option classes (<see cref="Sampling"/>,
/// <see cref="RateLimits"/>, <see cref="Messages"/>, <see cref="Tools"/>, <see cref="Cost"/>,
/// <see cref="Privacy"/>) that bind under their own configuration sub-sections, e.g.
/// "Assistant:Sampling:MaxTokens". The historical flat keys (e.g. "Assistant:MaxTokens")
/// remain fully supported via the <c>[Obsolete]</c> forwarding properties below, which
/// read/write the same nested objects. New code should use the nested properties directly.
///
/// <para>
/// <b>Binding precedence when both a flat legacy key and its nested equivalent are present:
/// the flat legacy key wins.</b> This is NOT left to accident of property declaration order —
/// plain <c>ConfigurationBinder</c>/<c>services.Configure&lt;AssistantOptions&gt;</c> binding order
/// is an implementation detail, not a contract. Instead, precedence is enforced explicitly: the DI
/// registration in <c>AssistantServiceExtensions.AddAssistant</c> follows the normal
/// <c>Configure&lt;AssistantOptions&gt;</c> call with a <c>PostConfigure&lt;AssistantOptions&gt;</c> step
/// that re-applies every flat legacy key actually present in the "Assistant" configuration section,
/// overwriting whatever its nested equivalent bound to. This keeps existing deployments that only set
/// flat keys unaffected, while still allowing operators to opt into the nested keys by removing the flat
/// ones. See <c>AssistantServiceExtensions.ApplyFlatLegacyKeyPrecedence</c> for the implementation and
/// AssistantOptionsBindingTests for the precedence contract under test (including through the real DI
/// registration, not just raw <c>ConfigurationBinder.Bind</c>).
/// </para>
/// </remarks>
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

    /// <summary>
    /// Gets or sets the base URL for generating links in responses.
    /// Uses ApplicationOptions.BaseUrl by default if not set.
    /// Falls back to <see cref="ApplicationOptions.BaseUrl"/> (default: "https://localhost:5001").
    /// </summary>
    /// <remarks>
    /// Base URL is passed to tools (not embedded in cached system prompt).
    /// Tools generate guild-specific URLs like:
    /// - {BASE_URL}/Portal/Soundboard/{GUILD_ID}
    /// - {BASE_URL}/Portal/TTS/{GUILD_ID}
    /// Guild ID is provided via tool context, not in cached prompt (to maintain cache sharing).
    /// </remarks>
    public string? BaseUrl { get; set; }

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

    #region Nested Option Groups

    /// <summary>
    /// Claude API model and sampling configuration. Binds under "Assistant:Sampling".
    /// </summary>
    public AssistantSamplingOptions Sampling { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration. Binds under "Assistant:RateLimits".
    /// </summary>
    public AssistantRateLimitOptions RateLimits { get; set; } = new();

    /// <summary>
    /// Message length constraints and error/retry behavior. Binds under "Assistant:Messages".
    /// </summary>
    public AssistantMessageOptions Messages { get; set; } = new();

    /// <summary>
    /// Tool execution and prompt/documentation path configuration. Binds under "Assistant:Tools".
    /// </summary>
    public AssistantToolOptions Tools { get; set; } = new();

    /// <summary>
    /// Cost tracking and prompt-caching configuration. Binds under "Assistant:Cost".
    /// </summary>
    public AssistantCostOptions Cost { get; set; } = new();

    /// <summary>
    /// Consent and audit logging configuration. Binds under "Assistant:Privacy".
    /// </summary>
    public AssistantPrivacyOptions Privacy { get; set; } = new();

    #endregion

    #region Obsolete Flat Forwarding Properties (backward-compatible binding)

    /// <summary>
    /// Gets or sets the default maximum number of questions a user can ask within the rate limit window.
    /// </summary>
    [Obsolete("Use RateLimits.DefaultRateLimit instead.")]
    public int DefaultRateLimit
    {
        get => RateLimits.DefaultRateLimit;
        set => RateLimits.DefaultRateLimit = value;
    }

    /// <summary>
    /// Gets or sets the time window for rate limiting in minutes.
    /// </summary>
    [Obsolete("Use RateLimits.RateLimitWindowMinutes instead.")]
    public int RateLimitWindowMinutes
    {
        get => RateLimits.RateLimitWindowMinutes;
        set => RateLimits.RateLimitWindowMinutes = value;
    }

    /// <summary>
    /// Gets or sets the minimum role required to bypass rate limits.
    /// </summary>
    [Obsolete("Use RateLimits.RateLimitBypassRole instead.")]
    public string? RateLimitBypassRole
    {
        get => RateLimits.RateLimitBypassRole;
        set => RateLimits.RateLimitBypassRole = value;
    }

    /// <summary>
    /// Gets or sets the maximum length of a user's question in characters.
    /// </summary>
    [Obsolete("Use Messages.MaxQuestionLength instead.")]
    public int MaxQuestionLength
    {
        get => Messages.MaxQuestionLength;
        set => Messages.MaxQuestionLength = value;
    }

    /// <summary>
    /// Gets or sets the maximum length of Claude's response in characters.
    /// </summary>
    [Obsolete("Use Messages.MaxResponseLength instead.")]
    public int MaxResponseLength
    {
        get => Messages.MaxResponseLength;
        set => Messages.MaxResponseLength = value;
    }

    /// <summary>
    /// Gets or sets the suffix appended when responses are truncated.
    /// </summary>
    [Obsolete("Use Messages.TruncationSuffix instead.")]
    public string TruncationSuffix
    {
        get => Messages.TruncationSuffix;
        set => Messages.TruncationSuffix = value;
    }

    /// <summary>
    /// Gets or sets the friendly error message shown to users when the API fails.
    /// </summary>
    [Obsolete("Use Messages.ErrorMessage instead.")]
    public string ErrorMessage
    {
        get => Messages.ErrorMessage;
        set => Messages.ErrorMessage = value;
    }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed API calls.
    /// </summary>
    [Obsolete("Use Messages.MaxRetryAttempts instead.")]
    public int MaxRetryAttempts
    {
        get => Messages.MaxRetryAttempts;
        set => Messages.MaxRetryAttempts = value;
    }

    /// <summary>
    /// Gets or sets the delay between retry attempts in milliseconds.
    /// </summary>
    [Obsolete("Use Messages.RetryDelayMs instead.")]
    public int RetryDelayMs
    {
        get => Messages.RetryDelayMs;
        set => Messages.RetryDelayMs = value;
    }

    /// <summary>
    /// Gets or sets the Claude model identifier to use.
    /// </summary>
    [Obsolete("Use Sampling.Model instead.")]
    public string Model
    {
        get => Sampling.Model;
        set => Sampling.Model = value;
    }

    /// <summary>
    /// Gets or sets the timeout for Claude API calls in milliseconds.
    /// </summary>
    [Obsolete("Use Sampling.ApiTimeoutMs instead.")]
    public int ApiTimeoutMs
    {
        get => Sampling.ApiTimeoutMs;
        set => Sampling.ApiTimeoutMs = value;
    }

    /// <summary>
    /// Gets or sets the maximum number of tokens for Claude's response.
    /// </summary>
    [Obsolete("Use Sampling.MaxTokens instead.")]
    public int MaxTokens
    {
        get => Sampling.MaxTokens;
        set => Sampling.MaxTokens = value;
    }

    /// <summary>
    /// Gets or sets the temperature for Claude's responses (0.0 to 1.0).
    /// </summary>
    [Obsolete("Use Sampling.Temperature instead.")]
    public double Temperature
    {
        get => Sampling.Temperature;
        set => Sampling.Temperature = value;
    }

    /// <summary>
    /// Gets or sets the path to the agent behavior/security prompt file.
    /// </summary>
    [Obsolete("Use Tools.AgentPromptPath instead.")]
    public string AgentPromptPath
    {
        get => Tools.AgentPromptPath;
        set => Tools.AgentPromptPath = value;
    }

    /// <summary>
    /// Gets or sets the base directory for documentation files.
    /// </summary>
    [Obsolete("Use Tools.DocumentationBasePath instead.")]
    public string DocumentationBasePath
    {
        get => Tools.DocumentationBasePath;
        set => Tools.DocumentationBasePath = value;
    }

    /// <summary>
    /// Gets or sets the path to the README file for command lists.
    /// </summary>
    [Obsolete("Use Tools.ReadmePath instead.")]
    public string ReadmePath
    {
        get => Tools.ReadmePath;
        set => Tools.ReadmePath = value;
    }

    /// <summary>
    /// Gets or sets whether documentation tools are enabled.
    /// </summary>
    [Obsolete("Use Tools.EnableDocumentationTools instead.")]
    public bool EnableDocumentationTools
    {
        get => Tools.EnableDocumentationTools;
        set => Tools.EnableDocumentationTools = value;
    }

    /// <summary>
    /// Gets or sets the maximum number of tool calls Claude can make per question.
    /// </summary>
    [Obsolete("Use Tools.MaxToolCallsPerQuestion instead.")]
    public int MaxToolCallsPerQuestion
    {
        get => Tools.MaxToolCallsPerQuestion;
        set => Tools.MaxToolCallsPerQuestion = value;
    }

    /// <summary>
    /// Gets or sets the timeout for individual tool executions in milliseconds.
    /// </summary>
    [Obsolete("Use Tools.ToolExecutionTimeoutMs instead.")]
    public int ToolExecutionTimeoutMs
    {
        get => Tools.ToolExecutionTimeoutMs;
        set => Tools.ToolExecutionTimeoutMs = value;
    }

    /// <summary>
    /// Gets or sets whether to track and log token usage for cost monitoring.
    /// </summary>
    [Obsolete("Use Cost.EnableCostTracking instead.")]
    public bool EnableCostTracking
    {
        get => Cost.EnableCostTracking;
        set => Cost.EnableCostTracking = value;
    }

    /// <summary>
    /// Gets or sets the daily cost threshold in USD for performance alerts.
    /// </summary>
    [Obsolete("Use Cost.DailyCostThresholdUsd instead.")]
    public decimal? DailyCostThresholdUsd
    {
        get => Cost.DailyCostThresholdUsd;
        set => Cost.DailyCostThresholdUsd = value;
    }

    /// <summary>
    /// Gets or sets the cost per million input tokens in USD for cost estimation.
    /// </summary>
    [Obsolete("Use Cost.CostPerMillionInputTokens instead.")]
    public decimal CostPerMillionInputTokens
    {
        get => Cost.CostPerMillionInputTokens;
        set => Cost.CostPerMillionInputTokens = value;
    }

    /// <summary>
    /// Gets or sets the cost per million output tokens in USD for cost estimation.
    /// </summary>
    [Obsolete("Use Cost.CostPerMillionOutputTokens instead.")]
    public decimal CostPerMillionOutputTokens
    {
        get => Cost.CostPerMillionOutputTokens;
        set => Cost.CostPerMillionOutputTokens = value;
    }

    /// <summary>
    /// Gets or sets whether users must explicitly opt-in via /consent before using the assistant.
    /// </summary>
    [Obsolete("Use Privacy.RequireExplicitConsent instead.")]
    public bool RequireExplicitConsent
    {
        get => Privacy.RequireExplicitConsent;
        set => Privacy.RequireExplicitConsent = value;
    }

    /// <summary>
    /// Gets or sets whether to log user questions and Claude responses for audit/debugging.
    /// </summary>
    [Obsolete("Use Privacy.LogInteractions instead.")]
    public bool LogInteractions
    {
        get => Privacy.LogInteractions;
        set => Privacy.LogInteractions = value;
    }

    /// <summary>
    /// Gets or sets the number of days to retain assistant interaction logs.
    /// </summary>
    [Obsolete("Use Privacy.InteractionLogRetentionDays instead.")]
    public int InteractionLogRetentionDays
    {
        get => Privacy.InteractionLogRetentionDays;
        set => Privacy.InteractionLogRetentionDays = value;
    }

    /// <summary>
    /// Gets or sets whether to use Claude's Prompt Caching for agent prompt and common docs.
    /// </summary>
    [Obsolete("Use Cost.EnablePromptCaching instead.")]
    public bool EnablePromptCaching
    {
        get => Cost.EnablePromptCaching;
        set => Cost.EnablePromptCaching = value;
    }

    /// <summary>
    /// Gets or sets whether to pre-cache common documentation in the system message.
    /// </summary>
    [Obsolete("Use Cost.CacheCommonDocumentation instead.")]
    public bool CacheCommonDocumentation
    {
        get => Cost.CacheCommonDocumentation;
        set => Cost.CacheCommonDocumentation = value;
    }

    /// <summary>
    /// Gets or sets the list of documentation files to include in cached prompt.
    /// </summary>
    [Obsolete("Use Cost.CachedDocumentationFiles instead.")]
    public string[] CachedDocumentationFiles
    {
        get => Cost.CachedDocumentationFiles;
        set => Cost.CachedDocumentationFiles = value;
    }

    /// <summary>
    /// Gets or sets the cost per million cached input tokens in USD for cost estimation.
    /// </summary>
    [Obsolete("Use Cost.CostPerMillionCachedTokens instead.")]
    public decimal CostPerMillionCachedTokens
    {
        get => Cost.CostPerMillionCachedTokens;
        set => Cost.CostPerMillionCachedTokens = value;
    }

    /// <summary>
    /// Gets or sets the cost per million cache write tokens in USD for cost estimation.
    /// </summary>
    [Obsolete("Use Cost.CostPerMillionCacheWriteTokens instead.")]
    public decimal CostPerMillionCacheWriteTokens
    {
        get => Cost.CostPerMillionCacheWriteTokens;
        set => Cost.CostPerMillionCacheWriteTokens = value;
    }

    #endregion
}
