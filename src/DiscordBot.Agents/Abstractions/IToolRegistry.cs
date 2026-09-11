using System.Text.Json;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents.Abstractions;

/// <summary>
/// Holds the registered tool providers and routes a tool call to the one that owns the tool.
/// </summary>
/// <remarks>
/// Scoping - which of the registered tools a particular run may see and call - is not this
/// interface's job. It is applied by wrapping a registry in <c>FilteredToolRegistry</c>, so the
/// policy lives with whoever decides it and a registry stays a plain lookup.
/// </remarks>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool provider.
    /// </summary>
    /// <param name="provider">The tool provider to register.</param>
    void RegisterProvider(IToolProvider provider);

    /// <summary>
    /// Gets the tool definitions of every registered provider.
    /// </summary>
    /// <returns>Enumerable of tool definitions, ordered by name.</returns>
    IEnumerable<LlmToolDefinition> GetEnabledTools();

    /// <summary>
    /// The name of the provider that owns <paramref name="toolName"/>, or null when no registered
    /// provider does (or this scope is not allowed the tool).
    /// </summary>
    /// <remarks>
    /// Exists so a caller can attribute a tool to its provider without executing it - the loop tags
    /// its per-tool span with it. Resolution is the same first-match walk
    /// <see cref="ExecuteToolAsync"/> does, so the two never disagree.
    /// </remarks>
    /// <param name="toolName">Name of the tool to attribute.</param>
    string? FindProviderName(string toolName);

    /// <summary>
    /// Executes a tool through the provider that owns it.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute.</param>
    /// <param name="input">Tool input as a JSON element.</param>
    /// <param name="context">Tool execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of tool execution.</returns>
    /// <exception cref="NotSupportedException">Thrown if no registered provider owns the tool.</exception>
    Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
