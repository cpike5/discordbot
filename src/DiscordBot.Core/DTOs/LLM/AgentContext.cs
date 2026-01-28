using DiscordBot.Core.Interfaces.LLM;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Context for running an agent, including tools and configuration.
/// </summary>
public class AgentContext
{
    /// <summary>
    /// The system prompt defining agent behavior.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Tool registry providing access to enabled tools.
    /// </summary>
    public IToolRegistry? ToolRegistry { get; set; }

    /// <summary>
    /// Execution context with user/guild/channel information.
    /// </summary>
    public ToolContext ExecutionContext { get; set; } = new();

    /// <summary>
    /// The model identifier to use for this agent run.
    /// If null, falls back to provider default.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for generation.
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of tool call iterations allowed.
    /// </summary>
    public int MaxToolCallIterations { get; set; } = 10;
}
