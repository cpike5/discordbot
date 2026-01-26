namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the unified navigation tabs component.
/// Supports horizontal tab-style navigation with multiple modes (in-page, page navigation, AJAX)
/// and style variants (underline, pills, bordered).
/// </summary>
public record NavTabsViewModel
{
    /// <summary>
    /// Collection of navigation tabs to display.
    /// </summary>
    public IReadOnlyList<NavTabItem> Tabs { get; init; } = [];

    /// <summary>
    /// The ID of the currently active tab.
    /// </summary>
    public string ActiveTabId { get; init; } = string.Empty;

    /// <summary>
    /// Visual style variant for the navigation tabs.
    /// </summary>
    public NavTabStyle StyleVariant { get; init; } = NavTabStyle.Underline;

    /// <summary>
    /// Navigation mode for the tabs.
    /// </summary>
    public NavMode NavigationMode { get; init; } = NavMode.InPage;

    /// <summary>
    /// Persistence mode for the active tab state.
    /// </summary>
    public NavPersistence PersistenceMode { get; init; } = NavPersistence.Hash;

    /// <summary>
    /// ARIA label for the navigation element for accessibility.
    /// </summary>
    public string AriaLabel { get; init; } = "Navigation";

    /// <summary>
    /// Unique identifier for this navigation instance. Used for generating
    /// element IDs and managing multiple navigation components on the same page.
    /// </summary>
    public string ContainerId { get; init; } = "navTabs";
}

/// <summary>
/// Represents a single navigation tab item.
/// </summary>
public record NavTabItem
{
    /// <summary>
    /// Unique identifier for this tab. Used for activation and state management.
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
    /// When true, this tab is disabled and cannot be selected.
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// Helper to check if this tab has an icon.
    /// </summary>
    public bool HasIcon => !string.IsNullOrEmpty(IconPathOutline);
}

/// <summary>
/// Visual style variant for navigation tabs.
/// </summary>
public enum NavTabStyle
{
    /// <summary>
    /// Tabs with an underline indicator on the active tab.
    /// </summary>
    Underline,

    /// <summary>
    /// Tabs styled as pills (rounded background on active tab).
    /// </summary>
    Pills,

    /// <summary>
    /// Tabs with visible borders around each tab.
    /// </summary>
    Bordered
}

/// <summary>
/// Navigation mode for the navigation tabs component.
/// </summary>
public enum NavMode
{
    /// <summary>
    /// Tabs switch content in-page via JavaScript (show/hide panels).
    /// Tab content should be rendered with the component.
    /// </summary>
    InPage,

    /// <summary>
    /// Tabs link to separate pages (full page navigation).
    /// Each tab's Href property must be set.
    /// </summary>
    PageNavigation,

    /// <summary>
    /// Tabs trigger AJAX requests to load content dynamically.
    /// Tab content is fetched on demand.
    /// </summary>
    Ajax
}

/// <summary>
/// Persistence mode for remembering the active tab.
/// </summary>
public enum NavPersistence
{
    /// <summary>
    /// No persistence - tab resets to default on page reload.
    /// </summary>
    None,

    /// <summary>
    /// Persist active tab in URL hash (e.g., #overview).
    /// Best for shareable URLs.
    /// </summary>
    Hash,

    /// <summary>
    /// Persist active tab in localStorage.
    /// Best when URL should stay clean.
    /// </summary>
    LocalStorage
}