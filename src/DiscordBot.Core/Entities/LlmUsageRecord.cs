using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// One row per user message sent through any <see cref="LlmMode"/> (guild assistant, DM
/// assistant, feature requests) — tokens, cost, and which model answered. No message text is
/// stored here; the existing per-mode interaction logs keep that. Written by
/// <c>ILlmUsageRecorder</c> off a background queue so recording never adds latency to a reply.
/// </summary>
public class LlmUsageRecord
{
    public long Id { get; set; }

    /// <summary>UTC timestamp of the exchange.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Which of the three model-resolution modes this row belongs to.</summary>
    public LlmMode Mode { get; set; }

    /// <summary>Discord user ID who sent the message.</summary>
    public ulong UserId { get; set; }

    /// <summary>Discord guild ID, or null for DM assistant and DM-based feature requests.</summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// OpenRouter slug that actually answered — <c>LlmResponse.Model</c> from the wire response
    /// when present, else the slug that was requested.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public int CacheWriteTokens { get; set; }

    /// <summary>Number of LLM calls made across the agentic loop for this message (<c>AgentRunResult.LoopCount</c>).</summary>
    public int LlmCalls { get; set; }

    /// <summary>Number of tool calls executed across the agentic loop for this message.</summary>
    public int ToolCalls { get; set; }

    public decimal CostUsd { get; set; }

    /// <summary>Whether <see cref="CostUsd"/> is OpenRouter's billed cost or the fallback estimate.</summary>
    public LlmCostSource CostSource { get; set; }

    public int LatencyMs { get; set; }

    public bool Success { get; set; }

    /// <summary>
    /// Links to the row in the mode's own interaction log (e.g. <see cref="AssistantInteractionLog"/>,
    /// <see cref="DmAssistantInteractionLog"/>) when one was logged. Null when the mode's
    /// interaction logging is disabled, or the mode (feature requests) keeps no interaction log.
    /// </summary>
    public long? InteractionLogId { get; set; }
}
