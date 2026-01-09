namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the reusable TabPanel component.
/// Supports both in-page tab switching (JS shows/hides content) and
/// page navigation mode (tabs link to separate pages).
/// </summary>
public record TabPanelViewModel
{
    /// <summary>
    /// Unique identifier for this tab panel instance. Used for generating
    /// element IDs and managing multiple tab panels on the same page.
    /// </summary>
    public string Id { get; init; } = "tabPanel";

    /// <summary>
    /// Collection of tabs to display in the tab panel.
    /// </summary>
    public IReadOnlyList<TabItemViewModel> Tabs { get; init; } = [];

    /// <summary>
    /// The ID of the currently active tab.
    /// </summary>
    public string ActiveTabId { get; init; } = string.Empty;

    /// <summary>
    /// Navigation mode for the tab panel.
    /// </summary>
    public TabNavigationMode NavigationMode { get; init; } = TabNavigationMode.InPage;

    /// <summary>
    /// Persistence mode for the active tab state.
    /// </summary>
    public TabPersistenceMode PersistenceMode { get; init; } = TabPersistenceMode.UrlHash;

    /// <summary>
    /// ARIA label for the tablist element for accessibility.
    /// </summary>
    public string AriaLabel { get; init; } = "Tab navigation";

    /// <summary>
    /// Optional CSS class to apply to the container.
    /// </summary>
    public string? ContainerClass { get; init; }

    /// <summary>
    /// When true, uses compact styling suitable for smaller containers.
    /// </summary>
    public bool Compact { get; init; }
}

/// <summary>
/// Represents a single tab in the tab panel.
/// </summary>
public record TabItemViewModel
{
    /// <summary>
    /// Unique identifier for this tab. Used for activation and panel association.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display label for the tab.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Optional shorter label to display on mobile devices.
    /// If not provided, the full Label is used.
    /// </summary>
    public string? ShortLabel { get; init; }

    /// <summary>
    /// URL to navigate to when in page navigation mode.
    /// Required when NavigationMode is PageNavigation.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// Optional SVG icon path (outline version) to display before the label.
    /// Use Heroicons-style 24x24 paths.
    /// </summary>
    public string? IconPathOutline { get; init; }

    /// <summary>
    /// Optional SVG icon path (solid version) to display when tab is active.
    /// If not provided, IconPathOutline is used for both states.
    /// </summary>
    public string? IconPathSolid { get; init; }

    /// <summary>
    /// Optional badge count to display on the tab (e.g., notification count).
    /// </summary>
    public int? BadgeCount { get; init; }

    /// <summary>
    /// Badge variant when BadgeCount is set.
    /// </summary>
    public TabBadgeVariant BadgeVariant { get; init; } = TabBadgeVariant.Default;

    /// <summary>
    /// When true, this tab is disabled and cannot be selected.
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// Helper to check if this tab has an icon.
    /// </summary>
    public bool HasIcon => !string.IsNullOrEmpty(IconPathOutline);

    /// <summary>
    /// Helper to check if this tab has a badge.
    /// </summary>
    public bool HasBadge => BadgeCount.HasValue && BadgeCount.Value > 0;
}

/// <summary>
/// Navigation mode for the tab panel component.
/// </summary>
public enum TabNavigationMode
{
    /// <summary>
    /// Tabs switch content in-page via JavaScript (show/hide panels).
    /// Tab panels should be rendered with the component.
    /// </summary>
    InPage,

    /// <summary>
    /// Tabs link to separate pages (full page navigation).
    /// Each tab's Href property must be set.
    /// </summary>
    PageNavigation
}

/// <summary>
/// Persistence mode for remembering the active tab.
/// </summary>
public enum TabPersistenceMode
{
    /// <summary>
    /// No persistence - tab resets to default on page reload.
    /// </summary>
    None,

    /// <summary>
    /// Persist active tab in URL hash (e.g., #tab-overview).
    /// Best for shareable URLs.
    /// </summary>
    UrlHash,

    /// <summary>
    /// Persist active tab in localStorage.
    /// Best when URL should stay clean.
    /// </summary>
    LocalStorage
}

/// <summary>
/// Badge variants for tab badges.
/// </summary>
public enum TabBadgeVariant
{
    Default,
    Success,
    Warning,
    Error,
    Info
}
