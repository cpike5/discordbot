using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Command Analytics Dashboard page.
/// </summary>
public record CommandAnalyticsViewModel
{
    /// <summary>
    /// Total number of commands executed in the time period.
    /// </summary>
    public int TotalCommands { get; init; }

    /// <summary>
    /// Overall success rate as a percentage (0-100).
    /// </summary>
    public decimal SuccessRate { get; init; }

    /// <summary>
    /// Average response time across all commands in milliseconds.
    /// </summary>
    public double AvgResponseTimeMs { get; init; }

    /// <summary>
    /// Number of unique commands executed.
    /// </summary>
    public int UniqueCommands { get; init; }

    /// <summary>
    /// Usage data aggregated by day for the line chart.
    /// </summary>
    public IReadOnlyList<UsageOverTimeDto> UsageOverTime { get; init; } = Array.Empty<UsageOverTimeDto>();

    /// <summary>
    /// Top commands by usage count for the bar chart.
    /// </summary>
    public IDictionary<string, int> TopCommands { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Success/failure counts for the donut chart.
    /// </summary>
    public CommandSuccessRateDto SuccessRateData { get; init; } = new();

    /// <summary>
    /// Performance metrics by command for the response time chart.
    /// </summary>
    public IReadOnlyList<CommandPerformanceDto> PerformanceData { get; init; } = Array.Empty<CommandPerformanceDto>();

    /// <summary>
    /// Selected start date for filtering.
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// Selected end date for filtering.
    /// </summary>
    public DateTime EndDate { get; init; }

    /// <summary>
    /// Selected guild ID for filtering (null = all guilds).
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Available guilds for the filter dropdown.
    /// </summary>
    public IReadOnlyList<GuildSelectOption> AvailableGuilds { get; init; } = Array.Empty<GuildSelectOption>();
}

/// <summary>
/// Simple option for guild dropdown.
/// </summary>
public record GuildSelectOption(ulong Id, string Name);
