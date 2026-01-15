using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Manages tool providers with enable/disable capability.
/// Routes tool execution to the appropriate provider.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool provider.
    /// </summary>
    /// <param name="provider">The tool provider to register.</param>
    /// <param name="enabled">Whether the provider is initially enabled.</param>
    void RegisterProvider(IToolProvider provider, bool enabled = true);

    /// <summary>
    /// Enables a provider by name.
    /// </summary>
    /// <param name="providerName">Name of the provider to enable.</param>
    /// <exception cref="InvalidOperationException">Thrown if provider not found.</exception>
    void EnableProvider(string providerName);

    /// <summary>
    /// Disables a provider by name.
    /// </summary>
    /// <param name="providerName">Name of the provider to disable.</param>
    /// <exception cref="InvalidOperationException">Thrown if provider not found.</exception>
    void DisableProvider(string providerName);

    /// <summary>
    /// Gets all tool definitions from enabled providers.
    /// </summary>
    /// <returns>Enumerable of tool definitions from enabled providers only.</returns>
    IEnumerable<LlmToolDefinition> GetEnabledTools();

    /// <summary>
    /// Executes a tool through the appropriate enabled provider.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute.</param>
    /// <param name="input">Tool input as a JSON element.</param>
    /// <param name="context">Tool execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of tool execution.</returns>
    /// <exception cref="NotSupportedException">Thrown if tool not found in any enabled provider.</exception>
    Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
