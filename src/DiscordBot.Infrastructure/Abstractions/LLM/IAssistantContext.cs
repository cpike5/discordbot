using cpike.Agent;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Services.LLM.Tooling;

namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// Scope-specific bits of one assistant message exchange, supplied to the
/// <see cref="IAssistantMessagePipeline"/> so it can run the shared agentic flow (seed the run's
/// scope, run the cpike.Agent loop, price usage, truncate the response) identically for the guild
/// assistant, the DM assistant and the feature-request agent.
/// </summary>
/// <remarks>
/// Implementations: <c>GuildAssistantContext</c> (guild-scoped, rate limited, no conversation
/// history), <c>DmAssistantContext</c> (owner-scoped, unlimited, sliding-window history and sticky
/// skills) and <c>FeatureRequestAssistantContext</c> (one requirements-gathering conversation).
/// The prompt and tool set come from the mode named by <see cref="ModeKey"/>; see
/// <c>CatalogPromptStore</c>.
/// </remarks>
public interface IAssistantContext
{
    /// <summary>
    /// Prefix used to namespace this context's rate-limit cache entries
    /// (e.g. "assistant_ratelimit:" vs "dm_assistant_ratelimit:") so guild and DM
    /// rate limiting can never collide even if scope keys happened to match.
    /// </summary>
    string RateLimitCacheKeyPrefix { get; }

    /// <summary>
    /// The scope key rate limiting is tracked against (e.g. "{guildId}:{userId}" or "{userId}").
    /// </summary>
    string RateLimitScopeKey { get; }

    /// <summary>
    /// Maximum requests per <see cref="RateLimitWindowMinutes"/>, or null if this context is not rate limited.
    /// </summary>
    int? RateLimit { get; }

    /// <summary>The rate-limit window in minutes.</summary>
    int RateLimitWindowMinutes { get; }

    /// <summary>The OpenRouter model slug to run on, or null for the framework's default.</summary>
    string? Model { get; }

    /// <summary>Which <see cref="LlmMode"/> this context belongs to, for the usage ledger.</summary>
    LlmMode Mode { get; }

    /// <summary>
    /// The framework mode the run uses: the prompt file under <c>docs/agents/</c> and the
    /// <c>ToolCatalog</c> surface that sets its tools (<c>guild</c>, <c>dm</c>, <c>feature-requests</c>).
    /// </summary>
    string ModeKey { get; }

    /// <summary>The caller and where they are, seeded into the run's scope for the tools.</summary>
    DiscordRunIdentity Identity { get; }

    /// <summary>The per-call output ceiling in tokens.</summary>
    int MaxTokens { get; }

    /// <summary>The most model calls the run may make.</summary>
    int MaxToolCallIterations { get; }

    /// <summary>Ceiling in characters on one tool result entering conversation history; 0 disables the cap.</summary>
    int MaxToolResultChars { get; }

    /// <summary>
    /// Tools the mode offers that this exchange may not use, such as a guild's disabled tools. A
    /// disabled tool is neither advertised nor executable.
    /// </summary>
    IReadOnlyCollection<string> DisabledTools { get; }

    /// <summary>Skills loaded on an earlier turn, offered already loaded on this one.</summary>
    IReadOnlyCollection<string> PreloadedSkills { get; }

    /// <summary>Prior turns to seed the agent with, oldest first, or empty when the scope has no history.</summary>
    IReadOnlyList<ConversationTurn> History { get; }

    /// <summary>Rates used to estimate cost when the provider reports none.</summary>
    AssistantCostRates CostRates { get; }

    /// <summary>The longest reply sent back; longer replies are cut and get <see cref="TruncationSuffix"/>.</summary>
    int MaxResponseLength { get; }

    /// <summary>Appended to a reply cut at <see cref="MaxResponseLength"/>.</summary>
    string TruncationSuffix { get; }

    /// <summary>
    /// Per-turn prompt content: facts that change from turn to turn (the guild a message came from)
    /// go in <see cref="PromptPlacement.TurnContext"/> so they never break the cached system prompt.
    /// </summary>
    Task<IReadOnlyList<PromptPart>> BuildPartsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Formats the raw user message for the agent. Every current scope returns it unchanged; the
    /// guild's id and name now travel as a turn-context part.
    /// </summary>
    Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken);

    /// <summary>
    /// Persists usage metrics and the interaction log entry for this exchange, and performs
    /// any scope-specific bookkeeping (e.g. saving DM conversation turns). Implementations are
    /// expected to catch and log their own errors so a telemetry failure never fails the user-facing request.
    /// </summary>
    Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken);
}
