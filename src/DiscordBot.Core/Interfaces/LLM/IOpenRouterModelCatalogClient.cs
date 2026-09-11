using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Fetches and parses OpenRouter's model catalog (<c>GET /models</c>). Implemented in
/// Infrastructure as a second typed <see cref="System.Net.Http.HttpClient"/>, separate from
/// the agent engine's <c>ILlmClient</c>, carrying the same auth and attribution headers.
/// </summary>
public interface IOpenRouterModelCatalogClient
{
    /// <summary>
    /// Fetches the full catalog, skipping alias entries (slugs starting with "~"). Throws
    /// <c>OpenRouterException</c> for a non-success HTTP status. No retry loop - a refresh is
    /// user-initiated or scheduled and can simply fail with a message.
    /// </summary>
    Task<IReadOnlyList<LlmCatalogModel>> GetModelsAsync(CancellationToken cancellationToken = default);
}
