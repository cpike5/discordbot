namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_SkeletonTable</c>: placeholder rows for a table or list that loads after the page.
/// </summary>
public record SkeletonTableViewModel
{
    public int Rows { get; init; } = 5;
    public int Columns { get; init; } = 4;
    public string? CssClass { get; init; }

    /// <summary>Screen reader text, for example "Loading users". Defaults to "Loading".</summary>
    public string Label { get; init; } = "Loading";
}
