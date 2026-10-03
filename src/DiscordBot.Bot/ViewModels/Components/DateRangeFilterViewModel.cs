namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_DateRangeFilter</c>: preset buttons plus a start and end date, for a GET
/// filter form. The presets are filled in by <c>shared/filter-panel.js</c> from
/// <c>DateRangeFilter.presetRange</c> (the viewer's local calendar), so the server never works out
/// "today" and the highlighted preset is the one the inputs match in the browser.
/// </summary>
public record DateRangeFilterViewModel
{
    /// <summary>Current start date, as bound from the query string.</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>Current end date, as bound from the query string.</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>Form field name of the start input (also its id).</summary>
    public string StartName { get; init; } = "StartDate";

    /// <summary>Form field name of the end input (also its id).</summary>
    public string EndName { get; init; } = "EndDate";

    /// <summary>Label above the preset buttons; also the group's accessible name.</summary>
    public string PresetLabel { get; init; } = "Quick date range";

    public string StartLabel { get; init; } = "Start date";

    public string EndLabel { get; init; } = "End date";

    /// <summary>Preset buttons, in order. Keys are names <c>DateRangeFilter.presetRange</c> understands.</summary>
    public IReadOnlyList<DateRangePreset> Presets { get; init; } = DateRangePreset.Standard;

    /// <summary>
    /// When true the partial also draws the Apply and Clear buttons. Pages whose form has more
    /// fields after the dates set this false and draw their own action row.
    /// </summary>
    public bool ShowActions { get; init; } = true;

    /// <summary>Where "Clear filters" goes; shown only when <see cref="HasActiveFilters"/>.</summary>
    public string? ClearUrl { get; init; }

    public bool HasActiveFilters { get; init; }
}

/// <summary>One preset button: a <c>DateRangeFilter.presetRange</c> key and its label.</summary>
public record DateRangePreset(string Key, string Label)
{
    public static readonly DateRangePreset Today = new("today", "Today");
    public static readonly DateRangePreset Last7Days = new("7days", "Last 7 days");
    public static readonly DateRangePreset Last30Days = new("30days", "Last 30 days");
    public static readonly DateRangePreset Last90Days = new("90days", "Last 90 days");

    /// <summary>Today, 7 days, 30 days: the guild analytics pages.</summary>
    public static IReadOnlyList<DateRangePreset> Standard { get; } = new[] { Today, Last7Days, Last30Days };

    /// <summary>7, 30 and 90 days: the cross-guild admin page.</summary>
    public static IReadOnlyList<DateRangePreset> Long { get; } = new[] { Last7Days, Last30Days, Last90Days };
}
