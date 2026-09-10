namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Per-million-token USD cost rates used to price an LLM usage report.
/// Shared value object so the guild and DM assistants compute cost identically.
/// </summary>
public readonly record struct AssistantCostRates(
    decimal InputPerMillion,
    decimal OutputPerMillion,
    decimal CachedPerMillion,
    decimal CacheWritePerMillion);
