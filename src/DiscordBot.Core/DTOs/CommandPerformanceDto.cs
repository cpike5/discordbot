namespace DiscordBot.Core.DTOs;

/// <summary>
/// Response time metrics for a command.
/// </summary>
public record CommandPerformanceDto
{
    public string CommandName { get; init; } = string.Empty;
    public double AvgResponseTimeMs { get; init; }
    public int MinResponseTimeMs { get; init; }
    public int MaxResponseTimeMs { get; init; }
    public int ExecutionCount { get; init; }
}
