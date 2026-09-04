using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Runs one assistant message exchange: builds the <see cref="AgentContext"/> from an
/// <see cref="IAssistantContext"/>, invokes the <see cref="IAgentRunner"/>, prices the
/// resulting usage, and truncates the response. Shared by the guild and DM assistant services
/// so the agentic-loop invocation and cost/truncation logic exists in exactly one place.
/// </summary>
/// <remarks>
/// The pipeline does not perform rate limiting, consent checks, or telemetry persistence —
/// those stay with each service/context since they touch scope-specific entities and public
/// contracts. See <see cref="IAssistantRateLimiter"/> for the shared rate-limiting piece.
/// </remarks>
public interface IAssistantMessagePipeline
{
    Task<AssistantPipelineResult> RunAsync(
        string userMessage,
        IAssistantContext context,
        CancellationToken cancellationToken = default);
}
