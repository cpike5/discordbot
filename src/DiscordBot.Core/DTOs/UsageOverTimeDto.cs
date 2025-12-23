namespace DiscordBot.Core.DTOs;

/// <summary>
/// Time series data point for command usage over time.
/// </summary>
public record UsageOverTimeDto
{
    public DateTime Date { get; init; }
    public int Count { get; init; }
}
