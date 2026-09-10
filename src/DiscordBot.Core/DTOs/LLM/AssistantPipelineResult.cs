using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Scope-neutral result of running a message through the <see cref="Interfaces.LLM.IAssistantMessagePipeline"/>.
/// The guild and DM assistant services each map this to their own public result DTO
/// (<c>AssistantResponseResult</c> / <c>DmAssistantResponse</c>).
/// </summary>
public class AssistantPipelineResult
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? ErrorMessage { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public int CacheCreationTokens { get; set; }
    public bool CacheHit { get; set; }

    public int ToolCalls { get; set; }
    public int LoopCount { get; set; }
    public List<string> ToolNames { get; set; } = new();
    public bool ConversationCleared { get; set; }

    public int LatencyMs { get; set; }
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>The model that answered — <c>AgentRunResult.Model</c> when reported, else the requested slug.</summary>
    public string? Model { get; set; }

    /// <summary>
    /// The ledger row built for this exchange, or null on an error path that never reached the
    /// agent runner. <c>InteractionLogId</c> is unset here — the caller (<c>GuildAssistantContext</c>/
    /// <c>DmAssistantContext</c>) fills it in after its own <c>AddAsync</c> and then hands the
    /// record to <see cref="Interfaces.LLM.ILlmUsageRecorder"/>.
    /// </summary>
    public LlmUsageRecord? UsageRecord { get; set; }

    public static AssistantPipelineResult FromError(string errorMessage, int latencyMs = 0)
    {
        return new AssistantPipelineResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            LatencyMs = latencyMs
        };
    }
}
