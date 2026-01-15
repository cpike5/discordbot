using System.Text.Json;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Result of a tool execution to be sent back to the LLM.
/// </summary>
public class LlmToolResult
{
    /// <summary>
    /// The ID of the tool call this result responds to.
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// The result of executing the tool (success data or error).
    /// </summary>
    public JsonElement Content { get; set; }

    /// <summary>
    /// Whether the tool execution resulted in an error.
    /// </summary>
    public bool IsError { get; set; }
}
