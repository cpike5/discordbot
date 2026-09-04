namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// Claude API model and sampling configuration for the guild AI assistant.
/// Binds under "Assistant:Sampling" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantSamplingOptions
{
    /// <summary>
    /// Gets or sets the Claude model identifier to use.
    /// Default is "claude-sonnet-4-20250514".
    /// </summary>
    /// <remarks>
    /// Available models:
    /// - claude-sonnet-4-20250514 (recommended for balance of speed/quality)
    /// - claude-opus-4-20250514 (highest quality, slower, more expensive)
    /// - claude-haiku-4-20250514 (fastest, cheapest, lower quality)
    /// If null or empty, falls back to Anthropic:DefaultModel.
    /// </remarks>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

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
}
