using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs.Llm.Reporting;

/// <summary>One page of raw <see cref="LlmUsageRecord"/> rows, newest first.</summary>
public class LlmUsagePagedRecords
{
    public required IReadOnlyList<LlmUsageRecord> Records { get; init; }

    /// <summary>Total rows matching the query, ignoring paging — for computing page count.</summary>
    public int TotalCount { get; init; }
}
