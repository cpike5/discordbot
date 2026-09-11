namespace DiscordBot.Agents.Contracts;

/// <summary>
/// Request sent to an LLM provider for completion.
/// </summary>
public class LlmRequest
{
    /// <summary>
    /// The system prompt that establishes the agent's behavior and constraints.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// The conversation history including user messages and assistant responses.
    /// </summary>
    public List<LlmMessage> Messages { get; set; } = new();

    /// <summary>
    /// Available tools the LLM can call (null if tools not supported).
    /// </summary>
    public List<LlmToolDefinition>? Tools { get; set; }

    /// <summary>
    /// The model to use for this request (provider-specific).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Maximum tokens to generate in the response.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Sampling temperature (0-1). Lower = more deterministic, higher = more creative.
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Whether to enable prompt caching if the provider supports it.
    /// </summary>
    public bool EnablePromptCaching { get; set; } = true;

    /// <summary>
    /// How the provider should treat <see cref="Tools"/> on this call. Null leaves the decision to
    /// the model; <c>"none"</c> forbids tool calls, which is how the loop asks for a text-only
    /// answer without dropping the tool schemas from the request.
    /// </summary>
    /// <remarks>
    /// A string rather than an enum because OpenRouter also accepts an object form naming a
    /// specific tool, which nothing here needs yet.
    /// </remarks>
    public string? ToolChoice { get; set; }
}
