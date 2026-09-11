using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Enums;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Interfaces.LLM;

namespace DiscordBot.Infrastructure.Abstractions.LLM;

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

    /// <summary>Which <see cref="LlmMode"/> this context belongs to, for the usage ledger.</summary>
    LlmMode Mode { get; }

    int MaxTokens { get; }
    double Temperature { get; }
    int MaxToolCallIterations { get; }

    /// <summary>
    /// Ceiling in characters on one tool result entering conversation history; 0 disables the cap.
    /// </summary>
    int MaxToolResultChars { get; }

    /// <summary>Deadline for a single tool execution in milliseconds; 0 disables it.</summary>
    int ToolExecutionTimeoutMs { get; }

    /// <summary>
    /// How many identical calls to one tool a run allows before refusing further ones; 0 disables
    /// the guard.
    /// </summary>
    int DuplicateToolCallLimit { get; }

    IToolRegistry? ToolRegistry { get; }
    ToolContext ExecutionContext { get; }

    /// <summary>
    /// The skills this exchange may load, or null when the scope has none.
    /// </summary>
    /// <remarks>
    /// Defaulted rather than required, because a context that knows nothing about skills is a
    /// perfectly good context and there are several of them in the tests. A scope that wants skills
    /// builds a <c>SkillSession</c> in its factory, puts it here <em>and</em> on
    /// <c>ExecutionContext</c> (where the loader tool reads it), and appends
    /// <c>SkillRoster</c>'s block to its system prompt.
    /// </remarks>
    ISkillActivationState? Skills => null;

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
