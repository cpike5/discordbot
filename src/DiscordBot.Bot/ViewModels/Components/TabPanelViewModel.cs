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

    /// <summary>
    /// Gets the visual style variant for the tab panel tabs.
    /// </summary>
    /// <remarks>
    /// <para>Three variants are available:</para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="TabStyleVariant.Underline"/></term>
    /// <description>
    /// Minimal style with underline indicator on active tab.
    /// Best for content pages and documentation. (Default)
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="TabStyleVariant.Pills"/></term>
    /// <description>
    /// Rounded background on active tab. Best for modals and compact spaces.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="TabStyleVariant.Bordered"/></term>
    /// <description>
    /// Full borders around tabs. Best for settings and clearly separated sections.
    /// </description>
    /// </item>
    /// </list>
    /// <para>The style is rendered as CSS class "tab-panel-{variant}" on the container.</para>
    /// </remarks>
    public TabStyleVariant StyleVariant { get; init; } = TabStyleVariant.Underline;
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

/// <summary>
/// Visual style variant for tab panel tabs.
/// </summary>
/// <remarks>
/// <para>
/// Controls the visual appearance of the tabs in the tab panel. Different variants suit different contexts:
/// </para>
/// <list type="table">
/// <item>
/// <term><see cref="Underline"/></term>
/// <description>
/// Minimal style with underline indicator on active tab. Suitable for content pages,
/// documentation, and article-style layouts. Default variant.
/// </description>
/// </item>
/// <item>
/// <term><see cref="Pills"/></term>
/// <description>
/// Rounded background on active tab. Provides higher visual prominence. Suitable for
/// modals, cards, sidebars, and compact layouts.
/// </description>
/// </item>
/// <item>
/// <term><see cref="Bordered"/></term>
/// <description>
/// Full borders around tabs with clear delineation. Suitable for settings pages,
/// configuration interfaces, and clearly separated sections.
/// </description>
/// </item>
/// </list>
/// <para>
/// The variant is rendered as a CSS class on the container: "tab-panel-{variant}" (e.g., "tab-panel-pills").
/// Styling is defined in tab-panel.css.
/// </para>
/// </remarks>
public enum TabStyleVariant
{
    /// <summary>
    /// Tabs with an underline indicator on the active tab. (Default)
    /// </summary>
    /// <remarks>
    /// Minimal visual weight, suitable for most content areas. The active tab is indicated
    /// by a bottom border/underline using the primary accent color. Provides a clean,
    /// modern appearance without overwhelming the content.
    /// </remarks>
    Underline = 0,

    /// <summary>
    /// Tabs styled as pills with rounded background on the active tab.
    /// </summary>
    /// <remarks>
    /// The active tab has a rounded rectangular background fill. Provides higher visual
    /// prominence while remaining elegant. Good for modals, cards, and compact spaces where
    /// clear visual hierarchy is important.
    /// </remarks>
    Pills = 1,

    /// <summary>
    /// Tabs with visible borders around each tab.
    /// </summary>
    /// <remarks>
    /// All tabs have visible borders creating clear separation. The active tab has a filled
    /// background or distinct highlight. Provides the strongest visual separation between
    /// tabs. Suitable for settings and configuration interfaces.
    /// </remarks>
    Bordered = 2
}
