// src/DiscordBot.Bot/ViewModels/Components/FilterPanelViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for the reusable filter panel component.
/// Supports collapsible filter sections with active filter count badges.
/// </summary>
public record FilterPanelViewModel
{
    /// <summary>
    /// Title displayed in the filter panel header (default: "Filters").
    /// </summary>
    public string Title { get; init; } = "Filters";

    /// <summary>
    /// Whether the panel can be collapsed/expanded (default: true).
    /// </summary>
    public bool IsCollapsible { get; init; } = true;

    /// <summary>
    /// Number of active filters to display in the badge.
    /// If null or 0, no badge is shown.
    /// </summary>
    public int? ActiveFilterCount { get; init; }

    /// <summary>
    /// Whether the panel should be expanded by default (default: false).
    /// When true, panel starts expanded; when false, panel starts collapsed.
    /// </summary>
    public bool DefaultExpanded { get; init; } = false;

    /// <summary>
    /// Badge text to display when filters are active (default: "Active" when ActiveFilterCount is 1, "{count} active" otherwise).
    /// </summary>
    public string? ActiveFilterBadgeText { get; init; }

    /// <summary>
    /// CSS class to apply to the container div (optional).
    /// </summary>
    public string? ContainerClass { get; init; }

    /// <summary>
    /// ID for the filter toggle button (default: "filterToggle").
    /// </summary>
    public string ToggleId { get; init; } = "filterToggle";

    /// <summary>
    /// ID for the filter content area (default: "filterContent").
    /// </summary>
    public string ContentId { get; init; } = "filterContent";

    /// <summary>
    /// ID for the chevron icon (default: "filterChevron").
    /// </summary>
    public string ChevronId { get; init; } = "filterChevron";

    /// <summary>
    /// Use hidden class toggle instead of max-height animation (default: false).
    /// When true, uses hidden class and rotate-180 on chevron.
    /// When false, uses max-height animation and -rotate-90 on chevron.
    /// </summary>
    public bool UseHiddenToggle { get; init; } = false;

    /// <summary>
    /// Gets whether to show the active filter badge.
    /// </summary>
    public bool ShowBadge => ActiveFilterCount.HasValue && ActiveFilterCount.Value > 0;

    /// <summary>
    /// Gets the computed badge text.
    /// </summary>
    public string ComputedBadgeText
    {
        get
        {
            if (!string.IsNullOrEmpty(ActiveFilterBadgeText))
            {
                return ActiveFilterBadgeText;
            }

            if (ActiveFilterCount.HasValue && ActiveFilterCount.Value > 0)
            {
                return ActiveFilterCount.Value == 1 ? "Active" : $"{ActiveFilterCount.Value} active";
            }

            return "Active";
        }
    }
}
