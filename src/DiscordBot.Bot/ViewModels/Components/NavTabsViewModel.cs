namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the unified navigation tabs component.
///
/// Supports horizontal tab-style navigation with multiple modes (in-page, page navigation, AJAX)
/// and style variants (underline, pills, bordered). This component consolidates the functionality
/// of legacy tab implementations (PerformanceTabs, AudioTabs, GuildNavBar, TabPanel) into a single,
/// consistent, and maintainable solution.
///
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new NavTabsViewModel
/// {
///     Tabs = new List&lt;NavTabItem&gt;
///     {
///         new NavTabItem { Id = "overview", Label = "Overview" },
///         new NavTabItem { Id = "settings", Label = "Settings" }
///     },
///     ActiveTabId = "overview",
///     NavigationMode = NavMode.InPage,
///     StyleVariant = NavTabStyle.Underline
/// };
/// </code>
/// </para>
///
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _NavTabs partial with required CSS/JS:
/// <code>
/// @section Styles {
///     &lt;link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" /&gt;
/// }
/// @section Scripts {
///     &lt;script src="~/js/nav-tabs.js" asp-append-version="true"&gt;&lt;/script&gt;
/// }
/// @await Html.PartialAsync("Components/_NavTabs", Model)
/// </code>
/// </para>
///
/// <para>
/// <strong>For In-Page Navigation Mode:</strong> Include content panels with matching ContainerId and tab IDs:
/// <code>
/// &lt;div data-nav-panel-for="settingsTabs" data-tab-id="overview"&gt;...&lt;/div&gt;
/// &lt;div data-nav-panel-for="settingsTabs" data-tab-id="settings" hidden&gt;...&lt;/div&gt;
/// </code>
/// </para>
///
/// <para>
/// See docs/articles/nav-tabs-component.md for comprehensive documentation.
/// </para>
/// </summary>
/// <remarks>
/// This is a record type for immutability and thread-safety. Use initializer syntax for creation:
/// <code>
/// var model = new NavTabsViewModel { Tabs = ..., ActiveTabId = ... };
/// </code>
/// </remarks>
public record NavTabsViewModel
{
    /// <summary>
    /// Gets the collection of navigation tabs to display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All tabs must have unique <see cref="NavTabItem.Id"/> values. IDs are used to:
    /// - Match with content panels (data-tab-id attribute)
    /// - Track the active tab state
    /// - Generate element IDs in HTML output
    /// - Persist state in URL hash or localStorage
    /// </para>
    /// <para>Minimum 1 tab is required for a meaningful component.</para>
    /// </remarks>
    public IReadOnlyList<NavTabItem> Tabs { get; init; } = [];

    /// <summary>
    /// Gets the ID of the currently active tab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must match the <see cref="NavTabItem.Id"/> of one of the tabs in <see cref="Tabs"/>.
    /// When rendered, the matching tab will have:
    /// - <c>aria-selected="true"</c>
    /// - CSS class "active"
    /// - <c>tabindex="0"</c>
    /// </para>
    /// <para>
    /// All other tabs will have:
    /// - <c>aria-selected="false"</c>
    /// - <c>tabindex="-1"</c>
    /// </para>
    /// <para>If empty or doesn't match any tab, the first tab becomes active by default.</para>
    /// </remarks>
    public string ActiveTabId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the visual style variant for the navigation tabs.
    /// </summary>
    /// <remarks>
    /// <para>Three variants are available:</para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="NavTabStyle.Underline"/></term>
    /// <description>
    /// Minimal style with underline indicator on active tab.
    /// Best for content pages and documentation. (Default)
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="NavTabStyle.Pills"/></term>
    /// <description>
    /// Rounded background on active tab. Best for modals and compact spaces.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="NavTabStyle.Bordered"/></term>
    /// <description>
    /// Full borders around tabs. Best for settings and clearly separated sections.
    /// </description>
    /// </item>
    /// </list>
    /// <para>The style is rendered as CSS class "nav-tabs-{variant}" on the container.</para>
    /// </remarks>
    public NavTabStyle StyleVariant { get; init; } = NavTabStyle.Underline;

    /// <summary>
    /// Gets the navigation mode for the tabs.
    /// </summary>
    /// <remarks>
    /// <para>Three modes are available:</para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="NavMode.InPage"/></term>
    /// <description>
    /// Tabs switch content in-page via JavaScript (show/hide panels).
    /// Content must be rendered on the page. (Default)
    /// Use when content is related and should remain on the same page.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="NavMode.PageNavigation"/></term>
    /// <description>
    /// Tabs link to separate pages (full page navigation).
    /// Each tab's <see cref="NavTabItem.Href"/> property must be set.
    /// Use when tabs represent independent sections.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="NavMode.Ajax"/></term>
    /// <description>
    /// Tabs trigger AJAX requests to load content dynamically.
    /// Content is fetched on demand from the URL in each tab's <see cref="NavTabItem.Href"/>.
    /// Use for heavy content or real-time data.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public NavMode NavigationMode { get; init; } = NavMode.InPage;

    /// <summary>
    /// Gets the persistence mode for the active tab state.
    /// </summary>
    /// <remarks>
    /// <para>Three persistence modes are available:</para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="NavPersistence.None"/></term>
    /// <description>
    /// No persistence - tab resets to default on page reload.
    /// Use when tab selection is session-specific.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="NavPersistence.Hash"/></term>
    /// <description>
    /// Persist active tab in URL hash (e.g., #overview).
    /// Best for shareable URLs. Allows back/forward button navigation. (Default)
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="NavPersistence.LocalStorage"/></term>
    /// <description>
    /// Persist active tab in localStorage.
    /// Best when URL should stay clean. Persists across sessions.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Note: <see cref="NavMode.PageNavigation"/> typically uses <see cref="NavPersistence.None"/>
    /// because the URL query parameter already indicates the active tab.
    /// </para>
    /// </remarks>
    public NavPersistence PersistenceMode { get; init; } = NavPersistence.Hash;

    /// <summary>
    /// Gets the ARIA label for the navigation element, used for accessibility.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This label is applied to the &lt;nav&gt; element with <c>role="tablist"</c>.
    /// It helps screen reader users understand the purpose of the tab navigation.
    /// </para>
    /// <para>Examples:</para>
    /// <list type="bullet">
    /// <item>"Settings sections"</item>
    /// <item>"Performance metrics"</item>
    /// <item>"Guild navigation"</item>
    /// <item>"Audio features"</item>
    /// </list>
    /// <para>Should be descriptive and concise. Avoid generic labels like "Navigation" when possible.</para>
    /// </remarks>
    public string AriaLabel { get; init; } = "Navigation";

    /// <summary>
    /// Gets the unique identifier for this navigation instance, used for generating element IDs
    /// and managing multiple navigation components on the same page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate unique IDs for each tab and panel: {ContainerId}-tab-{TabId}, {ContainerId}-panel-{TabId}
    /// - Match content panels via data-nav-panel-for attribute: &lt;div data-nav-panel-for="{ContainerId}"&gt;
    /// - Store state in localStorage with key: {ContainerId}-active
    /// - Generate the container div ID: {ContainerId}-container
    /// </para>
    /// <para>
    /// When using multiple tabs on the same page, ensure each has a unique ContainerId.
    /// </para>
    /// <para>Examples:</para>
    /// <list type="bullet">
    /// <item>"audioFeatures" for audio-related tabs</item>
    /// <item>"performanceMetrics" for performance dashboard tabs</item>
    /// <item>"guildSettings" for guild settings tabs</item>
    /// <item>"settingsTabs" for a generic settings page</item>
    /// </list>
    /// <para>
    /// Should be camelCase, alphanumeric only, and descriptive of the tab group's purpose.
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "navTabs";
}

/// <summary>
/// Represents a single navigation tab item in the tabs collection.
/// </summary>
/// <remarks>
/// <para>
/// This record defines the properties for a single tab that will be rendered in the NavTabs component.
/// Each tab must have a unique <see cref="Id"/> within its parent <see cref="NavTabsViewModel"/>.
/// </para>
/// <para>
/// <strong>Required Fields:</strong>
/// <list type="bullet">
/// <item><see cref="Id"/> - Unique identifier for the tab</item>
/// <item><see cref="Label"/> - Display text shown to users</item>
/// </list>
/// </para>
/// <para>
/// <strong>Conditional Fields:</strong>
/// <list type="table">
/// <item>
/// <term><see cref="Href"/></term>
/// <description>Required when using <see cref="NavMode.PageNavigation"/> or <see cref="NavMode.Ajax"/> navigation modes</description>
/// </item>
/// </list>
/// </para>
/// </remarks>
public record NavTabItem
{
    /// <summary>
    /// Gets the unique identifier for this tab. Used for activation and state management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ID must be:
    /// - Unique within the <see cref="NavTabsViewModel.Tabs"/> collection
    /// - Alphanumeric and kebab-case (e.g., "overview", "api-metrics", "advanced-settings")
    /// - Used to match with content panels via data-tab-id attribute
    /// - Used in URL hash (e.g., #overview)
    /// - Used in localStorage key (e.g., "myTabs-active": "overview")
    /// </para>
    /// <para>Examples of good IDs:</para>
    /// <list type="bullet">
    /// <item>"overview" - Simple, single-word section</item>
    /// <item>"health-metrics" - Multi-word with dash separators</item>
    /// <item>"api-rate-limits" - Descriptive, matches label</item>
    /// <item>"advanced-settings" - Clear intent</item>
    /// </list>
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display label for the tab, shown to users.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The label is the text visible in the tab navigation bar. Recommendations:
    /// - Keep labels short (1-2 words when possible)
    /// - Be clear and descriptive
    /// - Match common terminology in your domain
    /// - Use Title Case for consistency
    /// </para>
    /// <para>Examples of good labels:</para>
    /// <list type="bullet">
    /// <item>"Overview" - Simple and clear</item>
    /// <item>"Health Metrics" - Descriptive</item>
    /// <item>"Settings" - Common term</item>
    /// <item>"API & Rate Limits" - If abbreviation needed, provide ShortLabel</item>
    /// </list>
    /// <para>
    /// If the label is long and you want to save space on mobile devices,
    /// provide <see cref="ShortLabel"/> with an abbreviated version.
    /// </para>
    /// </remarks>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the optional shorter label to display on mobile devices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When provided, this shorter label will be displayed on narrow screens (mobile devices)
    /// while the full <see cref="Label"/> is shown on desktop.
    /// </para>
    /// <para>
    /// If not provided, the full Label is used on all screen sizes.
    /// </para>
    /// <para>Examples of short labels:</para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="Label"/></term>
    /// <term><see cref="ShortLabel"/></term>
    /// <description>Use Case</description>
    /// </item>
    /// <item>
    /// <term>Health Metrics</term>
    /// <term>Health</term>
    /// <description>Removes secondary word</description>
    /// </item>
    /// <item>
    /// <term>API & Rate Limits</term>
    /// <term>API</term>
    /// <description>Abbreviates long compound label</description>
    /// </item>
    /// <item>
    /// <term>Text-to-Speech</term>
    /// <term>TTS</term>
    /// <description>Uses standard acronym</description>
    /// </item>
    /// </list>
    /// <para>
    /// Short labels are rendered with CSS classes .tab-label-long (full label) and .tab-label-short (abbreviated).
    /// Only one is visible at a time based on viewport width.
    /// </para>
    /// </remarks>
    public string? ShortLabel { get; init; }

    /// <summary>
    /// Gets the URL to navigate to, used in page navigation and AJAX modes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The usage of this property depends on <see cref="NavTabsViewModel.NavigationMode"/>:
    /// </para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="NavMode.InPage"/></term>
    /// <description>Not used (optional)</description>
    /// </item>
    /// <item>
    /// <term><see cref="NavMode.PageNavigation"/></term>
    /// <description>Required - full URL to navigate to when tab is clicked (e.g., "/guilds/123/settings")</description>
    /// </item>
    /// <item>
    /// <term><see cref="NavMode.Ajax"/></term>
    /// <description>Required - API endpoint URL to fetch content from (e.g., "/api/performance/overview")</description>
    /// </item>
    /// </list>
    /// <para>
    /// For PageNavigation mode, use relative URLs (starting with /) or absolute URLs.
    /// The tab will render as an &lt;a&gt; element with this href.
    /// </para>
    /// <para>
    /// For AJAX mode, the endpoint should return HTML fragment that will be inserted
    /// into the content container (typically {ContainerId}-content).
    /// </para>
    /// </remarks>
    public string? Href { get; init; }

    /// <summary>
    /// Gets the optional SVG icon path (outline version) to display before the label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides the SVG path data for the icon.
    /// When this property is set, <see cref="HasIcon"/> returns true and an SVG icon is rendered.
    /// </para>
    /// <para>
    /// Icon Guidelines:
    /// - Use Heroicons-style 24x24 outline SVG paths
    /// - Must be a valid SVG path data string (the "d" attribute of a &lt;path&gt; element)
    /// - Icons are rendered with <c>aria-hidden="true"</c> for accessibility
    /// - The outline style is used for both active and inactive states for consistent visual weight
    /// </para>
    /// <para>
    /// Example outline icon for "Overview" (from Heroicons):
    /// <c>"M3 12a9 9 0 110-18 9 9 0 010 18z"</c>
    /// </para>
    /// <para>
    /// To add icons:
    /// 1. Find the icon in https://heroicons.com
    /// 2. Copy the outline path data
    /// 3. Set <see cref="IconPathOutline"/>
    /// </para>
    /// <para>
    /// Icons are positioned before the label and styled with CSS class "nav-tabs-icon".
    /// </para>
    /// </remarks>
    public string? IconPathOutline { get; init; }

    /// <summary>
    /// Gets a value indicating whether this tab is disabled and cannot be selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true:
    /// - The tab renders with disabled styling (reduced opacity)
    /// - The tab cannot be clicked or focused via keyboard
    /// - The tab is skipped in keyboard tab order (tabindex="-1")
    /// - Assistive technologies mark it as <c>aria-disabled="true"</c>
    /// - The tab may be visually grayed out or otherwise indicated as unavailable
    /// </para>
    /// <para>
    /// Use disabled tabs when:
    /// - A feature is not available to the current user
    /// - A feature requires conditions that aren't met (e.g., setup not complete)
    /// - Certain permissions aren't granted
    /// - Content is loading or initializing
    /// </para>
    /// <para>
    /// Default value: false (tabs are enabled by default).
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// new NavTabItem
    /// {
    ///     Id = "advanced",
    ///     Label = "Advanced Settings",
    ///     Disabled = !user.IsAdmin  // Only enable for admins
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public bool Disabled { get; init; }

    /// <summary>
    /// Gets a helper value indicating whether this tab has an icon defined.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a computed property that returns true if <see cref="IconPathOutline"/> is set to a non-empty value.
    /// It's used in the Razor partial to decide whether to render an SVG icon element.
    /// </para>
    /// <para>
    /// Example usage in partial:
    /// <code>
    /// @if (tab.HasIcon) {
    ///     &lt;svg class="nav-tabs-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor"&gt;
    ///         &lt;path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="@tab.IconPathOutline" /&gt;
    ///     &lt;/svg&gt;
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public bool HasIcon => !string.IsNullOrEmpty(IconPathOutline);
}

/// <summary>
/// Visual style variant for navigation tabs.
/// </summary>
/// <remarks>
/// <para>
/// Controls the visual appearance of the tabs in the navigation bar. Different variants suit different contexts:
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
/// Full borders around all tabs with clear delineation. Suitable for settings pages,
/// configuration interfaces, and clearly separated sections.
/// </description>
/// </item>
/// </list>
/// <para>
/// The variant is rendered as a CSS class on the container: "nav-tabs-{variant}" (e.g., "nav-tabs-pills").
/// Styling is defined in nav-tabs.css.
/// </para>
/// </remarks>
public enum NavTabStyle
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

/// <summary>
/// Navigation mode for the navigation tabs component, determining how tabs function.
/// </summary>
/// <remarks>
/// <para>
/// The navigation mode determines how content is organized and accessed when a tab is clicked.
/// Choose based on your content structure and user experience goals:
/// </para>
/// <list type="table">
/// <item>
/// <term><see cref="InPage"/></term>
/// <description>
/// Tabs switch content in-page via JavaScript. Use when content is related and should
/// remain on the same page. Content is rendered server-side with the page.
/// </description>
/// </item>
/// <item>
/// <term><see cref="PageNavigation"/></term>
/// <description>
/// Tabs link to separate pages (full page navigation). Use when tabs represent independent
/// sections or pages. Each click causes a full page load.
/// </description>
/// </item>
/// <item>
/// <term><see cref="Ajax"/></term>
/// <description>
/// Tabs trigger AJAX requests to load content dynamically. Use for heavy content or
/// real-time data. No full page reload occurs.
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum NavMode
{
    /// <summary>
    /// Tabs switch content in-page via JavaScript (show/hide panels). (Default)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Content panels are rendered on the page and JavaScript toggles their visibility.
    /// When a tab is clicked, the previous panel gets a "hidden" attribute and the new
    /// panel's "hidden" attribute is removed.
    /// </para>
    /// <para>
    /// Advantages:
    /// <list type="bullet">
    /// <item>No page reload needed - fast and responsive</item>
    /// <item>Smooth transitions can be added via CSS</item>
    /// <item>Content is pre-loaded, good for smaller content</item>
    /// <item>Simpler implementation for related content</item>
    /// </list>
    /// </para>
    /// <para>
    /// Disadvantages:
    /// <list type="bullet">
    /// <item>All content must be rendered initially (increases page size)</item>
    /// <item>Not suitable for heavy/large content</item>
    /// <item>SEO may be affected as not all content is in separate URLs</item>
    /// </list>
    /// </para>
    /// <para>
    /// Required content panel structure:
    /// <code>
    /// &lt;div data-nav-panel-for="{ContainerId}" data-tab-id="{TabId}"&gt;...&lt;/div&gt;
    /// &lt;div data-nav-panel-for="{ContainerId}" data-tab-id="{TabId2}" hidden&gt;...&lt;/div&gt;
    /// </code>
    /// </para>
    /// </remarks>
    InPage = 0,

    /// <summary>
    /// Tabs link to separate pages (full page navigation).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each tab is rendered as an &lt;a&gt; element with an href. Clicking a tab
    /// navigates to the specified URL with a full page load.
    /// </para>
    /// <para>
    /// Advantages:
    /// <list type="bullet">
    /// <item>Each tab can have unique URL - better SEO</item>
    /// <item>Smaller initial page load (only active content)</item>
    /// <item>Separate page refresh for each tab</item>
    /// <item>Clear visual indication of different sections</item>
    /// <item>Browser history/back button works naturally</item>
    /// </list>
    /// </para>
    /// <para>
    /// Disadvantages:
    /// <list type="bullet">
    /// <item>Full page reload on each tab click</item>
    /// <item>Slower perceived performance</item>
    /// <item>No smooth animations between tabs</item>
    /// </list>
    /// </para>
    /// <para>
    /// Required for each tab: Set <see cref="NavTabItem.Href"/> property.
    /// Example: <c>Href = "/guilds/123/settings/general"</c>
    /// </para>
    /// </remarks>
    PageNavigation = 1,

    /// <summary>
    /// Tabs trigger AJAX requests to load content dynamically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a tab is clicked, JavaScript makes an AJAX request to the URL specified in Href.
    /// The response is expected to be HTML content that is inserted into the page.
    /// </para>
    /// <para>
    /// Advantages:
    /// <list type="bullet">
    /// <item>No full page reload - fast and responsive</item>
    /// <item>Only active content is loaded (smaller payloads)</item>
    /// <item>Can load content on-demand (lazy loading)</item>
    /// <item>URL can reflect current tab state (with Hash persistence)</item>
    /// <item>Good for real-time data</item>
    /// </list>
    /// </para>
    /// <para>
    /// Disadvantages:
    /// <list type="bullet">
    /// <item>Requires backend AJAX endpoints</item>
    /// <item>Content must be fetched over network</item>
    /// <item>Slightly more complex implementation</item>
    /// <item>Must handle loading/error states</item>
    /// </list>
    /// </para>
    /// <para>
    /// Required for each tab: Set <see cref="NavTabItem.Href"/> to API endpoint.
    /// Example: <c>Href = "/api/performance/overview"</c>
    /// </para>
    /// <para>
    /// The endpoint should return HTML fragment (not full page):
    /// <code>
    /// GET /api/performance/overview
    /// Response:
    /// &lt;div class="metric"&gt;
    ///     &lt;span class="label"&gt;Uptime&lt;/span&gt;
    ///     &lt;span class="value"&gt;99.9%&lt;/span&gt;
    /// &lt;/div&gt;
    /// </code>
    /// </para>
    /// </remarks>
    Ajax = 2
}

/// <summary>
/// Persistence mode for remembering the active tab state across page reloads.
/// </summary>
/// <remarks>
/// <para>
/// Controls how the active tab selection is preserved:
/// </para>
/// <list type="table">
/// <item>
/// <term><see cref="None"/></term>
/// <description>
/// No persistence. Tab resets to default on page reload. Useful when tab selection
/// is session-specific or temporary.
/// </description>
/// </item>
/// <item>
/// <term><see cref="Hash"/></term>
/// <description>
/// Persist in URL hash (e.g., #overview). Best for shareable URLs and back/forward
/// button support. (Default)
/// </description>
/// </item>
/// <item>
/// <term><see cref="LocalStorage"/></term>
/// <description>
/// Persist in browser localStorage. Best when clean URLs are important. Persists
/// across sessions and browser restarts.
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum NavPersistence
{
    /// <summary>
    /// No persistence - tab resets to default on page reload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The active tab state is not saved. When the page is reloaded or revisited,
    /// the first tab (or tab specified in ActiveTabId) becomes active again.
    /// </para>
    /// <para>
    /// Use when:
    /// <list type="bullet">
    /// <item>Tab selection is temporary or session-specific</item>
    /// <item>You don't want to clutter the URL with hash</item>
    /// <item>Using <see cref="NavMode.PageNavigation"/> (URL already indicates active tab)</item>
    /// <item>Users shouldn't expect their tab choice to persist</item>
    /// </list>
    /// </para>
    /// </remarks>
    None = 0,

    /// <summary>
    /// Persist active tab in URL hash (e.g., #overview). (Default)
    /// </summary>
    /// <remarks>
    /// <para>
    /// The active tab ID is stored in the URL hash fragment. When someone shares the URL
    /// or uses back/forward buttons, the correct tab is displayed.
    /// </para>
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item>Before: https://example.com/settings</item>
    /// <item>After: https://example.com/settings#advanced</item>
    /// </list>
    /// </para>
    /// <para>
    /// Advantages:
    /// <list type="bullet">
    /// <item>URL is shareable - others see the same tab</item>
    /// <item>Browser back/forward buttons work correctly</item>
    /// <item>Visible in browser history</item>
    /// <item>Supports browser bookmarks with specific tab</item>
    /// <item>Works across page reloads</item>
    /// </list>
    /// </para>
    /// <para>
    /// Disadvantages:
    /// <list type="bullet">
    /// <item>Changes the URL appearance (adds #)</item>
    /// <item>May conflict with page anchors</item>
    /// </list>
    /// </para>
    /// <para>
    /// Implementation: JavaScript watches the hash and updates the active tab on load
    /// and when hash changes.
    /// </para>
    /// </remarks>
    Hash = 1,

    /// <summary>
    /// Persist active tab in localStorage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The active tab ID is stored in the browser's localStorage. The URL remains clean
    /// (no hash), but the tab choice persists across sessions and browser restarts
    /// (per domain/origin).
    /// </para>
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item>URL: https://example.com/settings (no hash)</item>
    /// <item>localStorage key: "settingsTabs-active": "advanced"</item>
    /// </list>
    /// </para>
    /// <para>
    /// Advantages:
    /// <list type="bullet">
    /// <item>Clean URLs (no hash fragment)</item>
    /// <item>Persists across sessions</item>
    /// <item>No browser history impact</item>
    /// <item>User preference is remembered</item>
    /// <item>Works well for user preferences</item>
    /// </list>
    /// </para>
    /// <para>
    /// Disadvantages:
    /// <list type="bullet">
    /// <item>URL is not shareable - others won't see the same tab</item>
    /// <item>Browser back/forward doesn't restore tab state</item>
    /// <item>Different per domain/user agent</item>
    /// <item>Cleared if user clears browser data</item>
    /// </list>
    /// </para>
    /// <para>
    /// Best used for:
    /// <list type="bullet">
    /// <item>User preferences that should persist</item>
    /// <item>Settings pages where clean URLs are important</item>
    /// <item>Tabs that don't need to be shareable</item>
    /// </list>
    /// </para>
    /// </remarks>
    LocalStorage = 2
}