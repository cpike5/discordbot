namespace DiscordBot.Core.Enums;

/// <summary>
/// Where a <see cref="Entities.LlmUsageRecord"/>'s <c>CostUsd</c> came from.
/// </summary>
public enum LlmCostSource
{
    /// <summary>OpenRouter reported a real billed <c>usage.cost</c> for the run.</summary>
    Billed,

    /// <summary>No billed cost was reported; the fallback per-million rates were used instead.</summary>
    Estimated
}
