using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Provider-agnostic interface for LLM completion calls.
/// Abstracts differences between Anthropic, OpenAI, and other providers.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a completion request to the LLM provider.
    /// </summary>
    /// <param name="request">The completion request with messages, tools, and configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response with content, stop reason, and token usage.</returns>
    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provider name (e.g., "Anthropic", "OpenAI", "Local").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this provider supports tool use (function calling).
    /// </summary>
    bool SupportsToolUse { get; }

    /// <summary>
    /// Whether this provider supports prompt caching.
    /// </summary>
    bool SupportsPromptCaching { get; }
}
