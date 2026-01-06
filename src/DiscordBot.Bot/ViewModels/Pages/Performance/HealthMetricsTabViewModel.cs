namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// View model for the Health Metrics tab, wrapping HealthMetricsViewModel.
/// Provides a consistent interface for partial view rendering with time range filtering.
/// </summary>
public record HealthMetricsTabViewModel : IPerformanceTabViewModel
{
    /// <inheritdoc />
    public string TabId => "health-metrics";

    /// <inheritdoc />
    public int Hours { get; init; } = 24;

    /// <summary>
    /// Gets the underlying health metrics data.
    /// </summary>
    public required HealthMetricsViewModel Data { get; init; }
}
