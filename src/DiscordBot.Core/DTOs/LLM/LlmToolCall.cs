using System.Text.Json;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Represents a tool call request from an LLM.
/// </summary>
public class LlmToolCall
{
    /// <summary>
    /// Unique ID for this tool call (assigned by the LLM provider).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the tool being called.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The input parameters as JSON.
    /// </summary>
    public JsonElement Input { get; set; }
}
