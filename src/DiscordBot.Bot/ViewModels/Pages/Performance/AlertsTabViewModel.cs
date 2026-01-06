namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// View model for the Alerts tab, wrapping AlertsPageViewModel.
/// Provides a consistent interface for partial view rendering with time range filtering.
/// </summary>
public record AlertsTabViewModel : IPerformanceTabViewModel
{
    /// <inheritdoc />
    public string TabId => "alerts";

    /// <inheritdoc />
    public int Hours { get; init; } = 24;

    /// <summary>
    /// Gets the underlying alerts page data including active incidents and alert configurations.
    /// </summary>
    public required AlertsPageViewModel Data { get; init; }
}
