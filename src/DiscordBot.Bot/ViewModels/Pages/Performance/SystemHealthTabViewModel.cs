namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// View model for the System Health tab, wrapping SystemHealthViewModel.
/// Provides a consistent interface for partial view rendering with time range filtering.
/// </summary>
public record SystemHealthTabViewModel : IPerformanceTabViewModel
{
    /// <inheritdoc />
    public string TabId => "system-health";

    /// <inheritdoc />
    public int Hours { get; init; } = 24;

    /// <summary>
    /// Gets the underlying system health data including database, cache, and service metrics.
    /// </summary>
    public required SystemHealthViewModel Data { get; init; }
}
