namespace DiscordBot.Core.Entities;

/// <summary>
/// Aggregated daily usage metrics for the DM assistant feature.
/// User-scoped (not guild-scoped) since DMs are direct.
/// </summary>
public class DmAssistantUsageMetrics
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public DateTime Date { get; set; }
    public int TotalMessages { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalCachedTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public int FailedRequests { get; set; }
    public int AverageLatencyMs { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
