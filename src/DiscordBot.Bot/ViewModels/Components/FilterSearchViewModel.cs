namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_FilterSearch</c>: the search box that sits above a list, outside the
/// collapsible filter panel, and filters as you type (<c>data-filter-live</c>).
/// </summary>
public record FilterSearchViewModel
{
    /// <summary>Input id (default "SearchTerm").</summary>
    public string Id { get; init; } = "SearchTerm";

    /// <summary>Query-string name the page binds (default "SearchTerm").</summary>
    public string Name { get; init; } = "SearchTerm";

    /// <summary>Current value.</summary>
    public string? Value { get; init; }

    /// <summary>Accessible name; the box shows no visible label.</summary>
    public string Label { get; init; } = "Search";

    public string? Placeholder { get; init; }

    /// <summary>
    /// The other filters currently applied, written as hidden inputs so pressing Enter without
    /// scripts keeps them. Build with <c>QueryStringHelper.Pairs</c>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Hidden { get; init; } = Array.Empty<KeyValuePair<string, string>>();
}
