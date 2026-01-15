using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Orchestrates the agentic loop (tool use cycles).
/// Manages conversation history and iterates until a final response is generated.
/// </summary>
public interface IAgentRunner
{
    /// <summary>
    /// Runs the agent with a user message, handling tool use cycles.
    /// </summary>
    /// <param name="userMessage">The user's input message.</param>
    /// <param name="context">Agent context (tools, system prompt, configuration).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final agent result with response text and token usage.</returns>
    Task<AgentRunResult> RunAsync(
        string userMessage,
        AgentContext context,
        CancellationToken cancellationToken = default);
}
