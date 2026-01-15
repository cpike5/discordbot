namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Result of running an agent, including the final response and metrics.
/// </summary>
public class AgentRunResult
{
    /// <summary>
    /// Whether the agent run succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The final response text from the agent.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Number of agentic loop iterations executed (tool use cycles).
    /// </summary>
    public int LoopCount { get; set; }

    /// <summary>
    /// Number of tool calls executed across all iterations.
    /// </summary>
    public int TotalToolCalls { get; set; }

    /// <summary>
    /// Aggregate token usage across all LLM calls in this run.
    /// </summary>
    public LlmUsage TotalUsage { get; set; } = new();

    /// <summary>
    /// Error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
