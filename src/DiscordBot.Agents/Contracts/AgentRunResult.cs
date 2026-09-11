namespace DiscordBot.Agents.Contracts;

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
    /// Number of agentic loop iterations executed (tool use cycles), plus the run's text-only
    /// follow-up call when it made one (the budget wrap-up, or blank-final-text recovery). Also
    /// the number of LLM calls made during this run — one completion call per iteration — which is
    /// what the usage ledger records.
    /// </summary>
    public int LoopCount { get; set; }

    /// <summary>
    /// The model that answered, taken from the last <see cref="LlmResponse.Model"/> that was not
    /// null across the loop's iterations. Null when no response reported one (e.g. every call
    /// failed before reaching the provider, or the provider never reports it).
    /// </summary>
    public string? Model { get; set; }

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

    /// <summary>
    /// Names of tools that were invoked during this run, in order of execution.
    /// </summary>
    public List<string> ToolNames { get; set; } = new();

    /// <summary>
    /// Whether the run used up its tool-round budget instead of the model ending its own turn.
    /// The reply is then the wrap-up answer (or, if that call failed too, no reply at all), so
    /// callers can record it distinctly from a clean run.
    /// </summary>
    public bool StoppedOnMaxIterations { get; set; }

    /// <summary>
    /// Whether the clear_conversation tool was invoked during this run.
    /// When true, callers should not save the exchange to conversation history.
    /// </summary>
    public bool ConversationCleared { get; set; }
}
