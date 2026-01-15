using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Groups related tools for the agent.
/// Implements the Provider pattern for tool organization and execution.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Unique name for this provider (e.g., "Documentation", "UserGuildInfo").
    /// Used for enable/disable management via IToolRegistry.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this provider does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets all tools provided by this provider.
    /// </summary>
    /// <returns>Enumerable of tool definitions for use in LLM requests.</returns>
    IEnumerable<LlmToolDefinition> GetTools();

    /// <summary>
    /// Executes a tool by name with the given input.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute.</param>
    /// <param name="input">Tool input as a JSON element.</param>
    /// <param name="context">Tool execution context (user ID, guild ID, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of tool execution (success/error with data).</returns>
    /// <exception cref="NotSupportedException">Thrown if tool is not supported by this provider.</exception>
    Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
