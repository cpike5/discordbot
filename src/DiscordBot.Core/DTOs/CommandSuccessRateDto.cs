namespace DiscordBot.Core.DTOs;

/// <summary>
/// Success/failure aggregation for commands.
/// </summary>
public record CommandSuccessRateDto
{
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int TotalCount => SuccessCount + FailureCount;
    public decimal SuccessRate => TotalCount > 0 ? (decimal)SuccessCount / TotalCount * 100 : 0;
}
