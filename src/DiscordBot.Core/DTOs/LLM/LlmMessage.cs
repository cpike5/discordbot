using DiscordBot.Core.DTOs.LLM.Enums;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Represents a single message in an LLM conversation.
/// </summary>
public class LlmMessage
{
    /// <summary>
    /// The role of who sent this message.
    /// </summary>
    public LlmRole Role { get; set; }

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Tool calls made by the assistant in this message (if any).
    /// </summary>
    public List<LlmToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Results from tool executions (in response to assistant's tool calls).
    /// </summary>
    public List<LlmToolResult>? ToolResults { get; set; }
}
