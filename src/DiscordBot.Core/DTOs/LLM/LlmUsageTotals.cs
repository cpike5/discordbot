using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>Aggregate totals over an <see cref="LlmUsageQuery"/> — the hero numbers for a usage dashboard.</summary>
public class LlmUsageTotals
{
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CachedTokens { get; init; }
    public long CacheWriteTokens { get; init; }
    public decimal CostUsd { get; init; }

    /// <summary>Fraction (0-1) of <see cref="CostUsd"/> that came from a billed row rather than an estimate. Null when there are no rows.</summary>
    public double? BilledCostShare { get; init; }

    public int FailedCount { get; init; }
    public double AverageLatencyMs { get; init; }
}

/// <summary>Per-user breakdown row, ordered by <see cref="CostUsd"/> descending by the repository.</summary>
public class LlmUsageByUser
{
    public required ulong UserId { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CachedTokens { get; init; }
    public decimal CostUsd { get; init; }

    /// <summary>Fraction (0-1) of the query's total cost this user accounts for.</summary>
    public double CostShare { get; init; }
}

/// <summary>Per-model breakdown row.</summary>
public class LlmUsageByModel
{
    public required string Model { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}

/// <summary>Per-<see cref="LlmMode"/> breakdown row.</summary>
public class LlmUsageByMode
{
    public required LlmMode Mode { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}

/// <summary>Per-day breakdown row (grouped by <c>Timestamp.Date</c>, UTC).</summary>
public class LlmUsageByDay
{
    public required DateTime Day { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}
