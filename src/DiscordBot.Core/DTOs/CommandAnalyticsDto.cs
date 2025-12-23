namespace DiscordBot.Core.DTOs;

/// <summary>
/// Aggregate analytics data for command usage.
/// </summary>
public record CommandAnalyticsDto
{
    public int TotalCommands { get; init; }
    public decimal SuccessRate { get; init; }
    public double AvgResponseTimeMs { get; init; }
    public int UniqueCommands { get; init; }
    public IReadOnlyList<UsageOverTimeDto> UsageOverTime { get; init; } = Array.Empty<UsageOverTimeDto>();
    public IDictionary<string, int> TopCommands { get; init; } = new Dictionary<string, int>();
    public CommandSuccessRateDto SuccessRateData { get; init; } = new();
    public IReadOnlyList<CommandPerformanceDto> PerformanceData { get; init; } = Array.Empty<CommandPerformanceDto>();
}
