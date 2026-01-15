using System.Text.Json;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Definition of a tool that an LLM can call.
/// </summary>
public class LlmToolDefinition
{
    /// <summary>
    /// Unique identifier for this tool (e.g., "get_user_roles").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the tool does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON Schema defining the input parameters.
    /// </summary>
    public JsonElement InputSchema { get; set; }
}
