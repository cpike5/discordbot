namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// LLM model and sampling configuration for the guild AI assistant.
/// Binds under "Assistant:Sampling" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantSamplingOptions
{
    /// <summary>
    /// Gets or sets the OpenRouter model slug to use.
    /// Default is "openrouter/auto", which lets OpenRouter pick a model per request.
    /// </summary>
    /// <remarks>
    /// Any slug from https://openrouter.ai/models works. Pin a specific slug (for example
    /// "anthropic/claude-sonnet-4.5") for predictable behaviour and prompt caching, which only
    /// Claude-family models honour. If null or empty, falls back to OpenRouter:DefaultModel.
    /// </remarks>
    public string Model { get; set; } = "openrouter/auto";

    /// <summary>
    /// Gets or sets the timeout for LLM API calls in milliseconds.
    /// Default is 30000 (30 seconds).
    /// </summary>
    public int ApiTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the maximum number of tokens for the model's response.
    /// Controls response length and API costs.
    /// Default is 512 tokens (~375 words) to encourage concise responses.
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    /// Gets or sets the temperature for the model's responses (0.0 to 1.0).
    /// Lower values are more focused and deterministic, higher values are more creative.
    /// Default is 0.3: the guild assistant answers factual questions about commands and
    /// features, where consistent syntax matters more than variety.
    /// </summary>
    public double Temperature { get; set; } = 0.3;
}
