namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>One applied filter, shown as a removable chip.</summary>
/// <param name="Label">What the chip says, e.g. "Role: Moderator".</param>
/// <param name="RemoveUrl">The page URL without this filter (and back on page 1).</param>
public sealed record ActiveFilterChip(string Label, string RemoveUrl);

/// <summary>
/// View model for <c>_ActiveFilterChips</c>: the filters applied to a list, between the filter
/// panel and the list. Renders nothing when there are no chips.
/// </summary>
public record ActiveFilterChipsViewModel
{
    /// <summary>The applied filters, in the order they should read.</summary>
    public IReadOnlyList<ActiveFilterChip> Chips { get; init; } = Array.Empty<ActiveFilterChip>();

    /// <summary>Where "Clear all" goes: the page with no filters.</summary>
    public string? ClearAllUrl { get; init; }

    /// <summary>Names the group for screen readers (default "Active filters").</summary>
    public string Label { get; init; } = "Active filters";
}
