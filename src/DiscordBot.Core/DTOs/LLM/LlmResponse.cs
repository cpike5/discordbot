using DiscordBot.Core.DTOs.LLM.Enums;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Response from an LLM provider.
/// </summary>
public class LlmResponse
{
    /// <summary>
    /// Indicates whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The text content of the response (null if tool use or error).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Why the model stopped generating.
    /// </summary>
    public LlmStopReason StopReason { get; set; }

    /// <summary>
    /// Tool calls made by the model (if StopReason == ToolUse).
    /// </summary>
    public List<LlmToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Token usage metrics for monitoring and cost tracking.
    /// </summary>
    public LlmUsage Usage { get; set; } = new();

    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The model that actually served this response, from the wire response's <c>model</c> field
    /// (OpenRouter can route a request to a different concrete model than the one requested, e.g.
    /// for an alias slug). Null when the provider did not report one.
    /// </summary>
    public string? Model { get; set; }
}
