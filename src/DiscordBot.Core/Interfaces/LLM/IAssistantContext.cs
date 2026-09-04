using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Scope-specific bits of one assistant message exchange, supplied to the
/// <see cref="IAssistantMessagePipeline"/> so it can run the shared agentic flow
/// (build agent context, invoke the agent runner, price usage, truncate the response)
/// identically for the guild assistant and the DM assistant.
/// </summary>
/// <remarks>
/// Implementations: <c>GuildAssistantContext</c> (guild-scoped, rate limited, no conversation
/// history) and <c>DmAssistantContext</c> (owner-scoped, unlimited, sliding-window history).
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

    int RateLimitWindowMinutes { get; }

    string? Model { get; }
    int MaxTokens { get; }
    double Temperature { get; }
    int MaxToolCallIterations { get; }

    IToolRegistry? ToolRegistry { get; }
    ToolContext ExecutionContext { get; }

    /// <summary>Prior turns to seed the agent with, or an empty list when the scope has no history.</summary>
    List<LlmMessage> ConversationHistory { get; }

    AssistantCostRates CostRates { get; }
    int MaxResponseLength { get; }
    string TruncationSuffix { get; }

    Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Formats the raw user message for the agent (e.g. the guild context prepends guild id/name;
    /// the DM context returns the message unchanged).
    /// </summary>
    Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken);

    /// <summary>
    /// Persists usage metrics and the interaction log entry for this exchange, and performs
    /// any scope-specific bookkeeping (e.g. saving DM conversation turns). Implementations are
    /// expected to catch and log their own errors so a telemetry failure never fails the user-facing request.
    /// </summary>
    Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken);
}
