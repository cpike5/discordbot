namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// View model for the Commands tab, wrapping CommandPerformanceViewModel.
/// Provides a consistent interface for partial view rendering with time range filtering.
/// </summary>
public record CommandsTabViewModel : IPerformanceTabViewModel
{
    /// <inheritdoc />
    public string TabId => "commands";

    /// <inheritdoc />
    public int Hours { get; init; } = 24;

    /// <summary>
    /// Gets the underlying command performance data.
    /// </summary>
    public required CommandPerformanceViewModel Data { get; init; }
}
