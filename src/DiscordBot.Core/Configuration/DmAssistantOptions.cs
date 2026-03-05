namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the DM-based AI assistant feature.
/// Independent from guild assistant — supports different model settings and conversation behavior.
/// </summary>
public class DmAssistantOptions
{
    public const string SectionName = "DmAssistant";

    /// <summary>
    /// Gets or sets whether the DM assistant feature is enabled.
    /// Default is false (disabled until explicitly enabled).
    /// </summary>
    public bool Enabled { get; set; } = false;

    #region Prompt Paths

    /// <summary>
    /// Gets or sets the path to the owner system prompt file.
    /// Default is "docs/agents/dm-owner-agent.md".
    /// </summary>
    public string OwnerSystemPromptPath { get; set; } = "docs/agents/dm-owner-agent.md";

    /// <summary>
    /// Gets or sets the placeholder message shown to non-owner users.
    /// </summary>
    public string PlaceholderMessage { get; set; } = "DM assistant support is coming soon! Stay tuned.";

    #endregion

    #region Conversation History

    /// <summary>
    /// Gets or sets the maximum number of conversation messages to retain per user (sliding window).
    /// Default is 20 messages.
    /// </summary>
    public int MaxConversationMessages { get; set; } = 20;

    #endregion

    #region Claude API Configuration

    /// <summary>
    /// Gets or sets the Claude model identifier to use.
    /// Default is "claude-sonnet-4-20250514".
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Gets or sets the maximum number of tokens for Claude's response.
    /// Higher than guild assistant (512) — general-purpose conversation needs longer responses.
    /// Default is 4096 tokens.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the temperature for Claude's responses (0.0 to 1.0).
    /// Default is 0.7 (balanced).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    #endregion

    #region Message Constraints

    /// <summary>
    /// Gets or sets the maximum length of Claude's response in characters.
    /// Default is 1800 (leaves buffer for Discord's 2000 char limit).
    /// </summary>
    public int MaxResponseLength { get; set; } = 1800;

    /// <summary>
    /// Gets or sets the suffix appended when responses are truncated.
    /// </summary>
    public string TruncationSuffix { get; set; } = "\n\n... *(response truncated)*";

    #endregion

    #region Error Handling

    /// <summary>
    /// Gets or sets the friendly error message shown when the API fails.
    /// </summary>
    public string ErrorMessage { get; set; } = "Oops, I'm having trouble thinking right now. Please try again in a moment.";

    #endregion

    #region Cost Tracking

    /// <summary>
    /// Gets or sets whether to track and log token usage for cost monitoring.
    /// Default is true.
    /// </summary>
    public bool EnableCostTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets the cost per million input tokens in USD.
    /// </summary>
    public decimal CostPerMillionInputTokens { get; set; } = 3.00m;

    /// <summary>
    /// Gets or sets the cost per million output tokens in USD.
    /// </summary>
    public decimal CostPerMillionOutputTokens { get; set; } = 15.00m;

    /// <summary>
    /// Gets or sets the cost per million cached input tokens in USD.
    /// </summary>
    public decimal CostPerMillionCachedTokens { get; set; } = 0.30m;

    #endregion

    #region Privacy and Audit

    /// <summary>
    /// Gets or sets whether to log interactions for audit/debugging.
    /// Default is true.
    /// </summary>
    public bool LogInteractions { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain interaction logs.
    /// Default is 90 days.
    /// </summary>
    public int InteractionLogRetentionDays { get; set; } = 90;

    #endregion

    #region Feature Flags

    /// <summary>
    /// Gets or sets whether to show typing indicator while waiting for Claude's response.
    /// Default is true.
    /// </summary>
    public bool ShowTypingIndicator { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use Claude's Prompt Caching for the system prompt.
    /// Default is true.
    /// </summary>
    public bool EnablePromptCaching { get; set; } = true;

    #endregion
}
