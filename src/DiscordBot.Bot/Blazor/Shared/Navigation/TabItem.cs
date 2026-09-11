using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// One tab in a <see cref="TabGroup"/>. Union of <c>NavTabItem</c> (<c>_NavTabs.cshtml</c>) and
/// <c>TabItemViewModel</c> (<c>_TabPanel.cshtml</c>) - see
/// <c>docs/articles/blazor-components.md</c> "Consolidations". Reuses
/// <see cref="TabBadgeVariant"/> from the legacy view models rather than introducing a duplicate
/// enum.
/// </summary>
public sealed record TabItem
{
    /// <summary>Unique id within the parent <see cref="TabGroup"/>. Matches with content panels
    /// (a child <see cref="TabPanel"/>'s <c>Id</c>) and drives generated element ids.</summary>
    public required string Id { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>Shorter label shown on narrow screens (<c>tab-label-short</c>) instead of
    /// <see cref="Label"/> (<c>tab-label-long</c>). Both render when set; CSS picks one.</summary>
    public string? ShortLabel { get; init; }

    /// <summary>Target URL. Required in <see cref="TabGroupMode.Navigation"/>; unused in
    /// <see cref="TabGroupMode.InPage"/>.</summary>
    public string? Href { get; init; }

    /// <summary>Heroicons-outline (24x24) <c>d</c> path, e.g. an <see cref="IconPaths"/>
    /// constant.</summary>
    public string? IconPath { get; init; }

    /// <summary>Optional badge count. A badge only renders when this is set and positive.</summary>
    public int? BadgeCount { get; init; }

    /// <summary>Badge color, when <see cref="BadgeCount"/> is set.</summary>
    public TabBadgeVariant BadgeVariant { get; init; } = TabBadgeVariant.Default;

    /// <summary>Optional secondary line under the label.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Disabled tabs render with reduced opacity, are skipped by arrow-key roving, and
    /// never activate.</summary>
    public bool Disabled { get; init; }

    public bool HasIcon => !string.IsNullOrEmpty(IconPath);
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
    public bool HasBadge => BadgeCount is > 0;
}

/// <summary>
/// How a <see cref="TabGroup"/> switches content. The AJAX mode both legacy partials supported
/// is dropped - see <c>docs/articles/blazor-components.md</c> "Consolidations".
/// </summary>
public enum TabGroupMode
{
    /// <summary>Tabs are buttons that switch which child <see cref="TabPanel"/> is rendered - no
    /// navigation, no JS.</summary>
    InPage,

    /// <summary>Tabs are <see cref="Microsoft.AspNetCore.Components.Routing.NavLink"/>s that
    /// navigate to <see cref="TabItem.Href"/>.</summary>
    Navigation
}
