namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// View model for the API Metrics tab, wrapping ApiRateLimitsViewModel.
/// Provides a consistent interface for partial view rendering with time range filtering.
/// </summary>
public record ApiMetricsTabViewModel : IPerformanceTabViewModel
{
    /// <inheritdoc />
    public string TabId => "api-metrics";

    /// <inheritdoc />
    public int Hours { get; init; } = 24;

    /// <summary>
    /// Gets the underlying API rate limits and metrics data.
    /// </summary>
    public required ApiRateLimitsViewModel Data { get; init; }
}
