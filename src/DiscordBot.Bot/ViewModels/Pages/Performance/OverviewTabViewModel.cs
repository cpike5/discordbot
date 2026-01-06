namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// View model for the Overview tab, wrapping PerformanceOverviewViewModel.
/// Provides a consistent interface for partial view rendering with time range filtering.
/// </summary>
public record OverviewTabViewModel : IPerformanceTabViewModel
{
    /// <inheritdoc />
    public string TabId => "overview";

    /// <inheritdoc />
    public int Hours { get; init; } = 24;

    /// <summary>
    /// Gets the underlying performance overview data.
    /// </summary>
    public required PerformanceOverviewViewModel Data { get; init; }
}
