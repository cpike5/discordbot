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
