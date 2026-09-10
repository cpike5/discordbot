namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Range limits shared by <c>LlmUsageController</c> (API validation) and <c>LlmUsageModel</c>
/// (the server-rendered page) so both enforce the same widest allowed date span.
/// </summary>
public static class LlmUsageRangeLimits
{
    /// <summary>Widest range a single query may span.</summary>
    public const int MaxRangeDays = 366;
}

/// <summary>
/// Envelope for <c>GET api/admin/llm-usage/summary</c>: totals plus every breakdown the usage
/// dashboard renders, all over the same <see cref="LlmUsageQuery"/> range/filters.
/// </summary>
public sealed record LlmUsageSummaryResponseDto
{
    public required LlmUsageTotalsDto Totals { get; init; }

    public required IReadOnlyList<LlmUsageByUserDto> ByUser { get; init; }

    public required IReadOnlyList<LlmUsageByModelDto> ByModel { get; init; }

    public required IReadOnlyList<LlmUsageByModeDto> ByMode { get; init; }

    public required IReadOnlyList<LlmUsageByDayDto> ByDay { get; init; }

    /// <summary>The (possibly defaulted/clamped) range this summary was computed over, ISO-8601.</summary>
    public required DateTime From { get; init; }

    public required DateTime To { get; init; }
}

/// <summary>Aggregate totals, shaped for JSON (decimal cost, no repository-internal types).</summary>
public sealed record LlmUsageTotalsDto
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

/// <summary>Per-user breakdown row with the Discord ID emitted as a string (CLAUDE.md snowflake gotcha) and a resolved display name.</summary>
public sealed record LlmUsageByUserDto
{
    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CachedTokens { get; init; }
    public decimal CostUsd { get; init; }

    /// <summary>Fraction (0-1) of the query's total cost this user accounts for.</summary>
    public double CostShare { get; init; }
}

/// <summary>Per-model breakdown row.</summary>
public sealed record LlmUsageByModelDto
{
    public required string Model { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}

/// <summary>Per-mode breakdown row (mode as its string name, e.g. "GuildAssistant").</summary>
public sealed record LlmUsageByModeDto
{
    public required string Mode { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}

/// <summary>Per-day breakdown row (UTC calendar day).</summary>
public sealed record LlmUsageByDayDto
{
    public required DateTime Day { get; init; }
    public int MessageCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}

/// <summary>Envelope for <c>GET api/admin/llm-usage/records</c>: one page of raw ledger rows.</summary>
public sealed record LlmUsagePagedRecordsResponseDto
{
    public required IReadOnlyList<LlmUsageRecordRowDto> Records { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

/// <summary>One raw ledger row, shaped for JSON - IDs as strings, mode/cost-source as their names.</summary>
public sealed record LlmUsageRecordRowDto
{
    public required long Id { get; init; }

    public required DateTime Timestamp { get; init; }

    public required string Mode { get; init; }

    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public string? GuildId { get; init; }

    public required string Model { get; init; }

    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int CachedTokens { get; init; }
    public int CacheWriteTokens { get; init; }
    public int LlmCalls { get; init; }
    public int ToolCalls { get; init; }
    public decimal CostUsd { get; init; }

    public required string CostSource { get; init; }

    public int LatencyMs { get; init; }

    public bool Success { get; init; }

    public long? InteractionLogId { get; init; }
}
