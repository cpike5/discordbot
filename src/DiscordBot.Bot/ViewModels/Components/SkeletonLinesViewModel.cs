namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_SkeletonLines</c>: placeholder text lines (the last one shorter).
/// </summary>
public record SkeletonLinesViewModel
{
    public int Lines { get; init; } = 3;
    public string? CssClass { get; init; }

    /// <summary>Screen reader text. Defaults to "Loading".</summary>
    public string Label { get; init; } = "Loading";
}
