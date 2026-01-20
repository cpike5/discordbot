# Navigation Component Unification - Technical Specification

**Epic:** #1248 - Unified Navigation Component System
**Version:** 1.0
**Status:** Draft
**Author:** System Architect
**Date:** 2026-01-19

---

## Executive Summary

This specification unifies five different tab/navigation implementations into a single, extensible component system by **extending the existing `TabPanelViewModel`** rather than creating new ViewModels. This approach prevents fragmentation, maintains backward compatibility, and provides a clear migration path.

### Scope

**In Scope:**
- Extend `TabPanelViewModel` with style variants and AJAX navigation
- Enhance `_TabPanel.cshtml` with three visual styles (Underline, Pills, Portal)
- Add AJAX navigation mode to `tab-panel.js`
- Migrate existing implementations: `_GuildNavBar`, `_PerformanceTabs`, `_AudioTabs`
- Update accessibility features for all variants
- Create comprehensive documentation and migration guide

**Out of Scope:**
- Mobile dropdown pattern (remains guild-specific)
- Portal authentication/authorization changes
- Performance dashboard real-time updates (separate from tab system)
- Command Pages tab integration (separate epic)

---

## 1. Current State Analysis

### Existing Components to Consolidate

| Component | File | Usage | Style | Issues |
|-----------|------|-------|-------|--------|
| **GuildNavBar** | `_GuildNavBar.cshtml` | Guild pages | Underline + mobile dropdown | Guild-specific ViewModel, duplicated HTML |
| **PerformanceTabs** | `_PerformanceTabs.cshtml` | Performance dashboard | Underline | AJAX mode flag, hardcoded tabs |
| **AudioTabs** | `_AudioTabs.cshtml` | Audio section | Pills | Guild-specific, hardcoded tabs |
| **TabPanel** | `_TabPanel.cshtml` | Commands pages | Underline | Missing Pills/Portal styles, no AJAX |
| **Portal Header** | `_PortalHeader.cshtml` | Portal pages | Portal | Embedded in header, not reusable |

### Existing Foundation

`TabPanelViewModel` (169 lines) already provides:
- ✅ `TabNavigationMode` (InPage, PageNavigation)
- ✅ `TabPersistenceMode` (None, UrlHash, LocalStorage)
- ✅ Icon support (outline/solid swapping)
- ✅ Badge support with variants
- ✅ Disabled tabs
- ✅ Short labels for mobile
- ✅ ARIA attributes

`tab-panel.js` (estimated 400+ lines) provides:
- ✅ Keyboard navigation (arrow keys, Home, End, wrap-around)
- ✅ ARIA live region announcements
- ✅ URL hash persistence
- ✅ localStorage persistence
- ✅ Scroll indicators
- ✅ Focus management

### Gaps to Address

**Missing Features:**
1. ❌ AJAX navigation mode (PerformanceTabs has this)
2. ❌ Style variant system (Underline, Pills, Portal)
3. ❌ Guild context helper (for URL building)
4. ❌ Long/short label responsive switching
5. ❌ Portal-specific styling

---

## 2. Proposed Architecture

### 2.1 ViewModel Extensions

**Extend existing `TabPanelViewModel` in `ViewModels/Components/TabPanelViewModel.cs`:**

```csharp
// ADD to existing TabPanelViewModel record
public record TabPanelViewModel
{
    // ... existing properties ...

    /// <summary>
    /// Visual style variant for the tab panel.
    /// </summary>
    public TabStyleVariant StyleVariant { get; init; } = TabStyleVariant.Underline;

    /// <summary>
    /// Optional AJAX URL for loading tab content dynamically.
    /// Only used when NavigationMode is Ajax.
    /// URL can use {tabId} placeholder, e.g., "/Admin/Performance/Partial/{tabId}"
    /// </summary>
    public string? AjaxUrlPattern { get; init; }

    /// <summary>
    /// Container element selector where AJAX content will be loaded.
    /// Defaults to "#[Id]-content" if not specified.
    /// </summary>
    public string? AjaxContentTarget { get; init; }

    /// <summary>
    /// When true, shows long/short label based on viewport width.
    /// Uses `data-label-long` and `data-label-short` attributes.
    /// </summary>
    public bool UseResponsiveLabels { get; init; }
}

// EXTEND TabNavigationMode enum
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
    PageNavigation,

    /// <summary>
    /// Tabs fetch content dynamically via AJAX without page reload.
    /// Uses AjaxUrlPattern to construct request URLs.
    /// </summary>
    Ajax
}

// ADD new enum
/// <summary>
/// Visual style variants for tab panel component.
/// </summary>
public enum TabStyleVariant
{
    /// <summary>
    /// Underline style with bottom border (default).
    /// Used in: GuildNavBar, PerformanceTabs, Commands pages.
    /// </summary>
    Underline,

    /// <summary>
    /// Pills/segmented control style with rounded background.
    /// Used in: AudioTabs.
    /// </summary>
    Pills,

    /// <summary>
    /// Portal-specific styling with Discord branding.
    /// Used in: Portal pages.
    /// </summary>
    Portal
}

// EXTEND TabItemViewModel record
public record TabItemViewModel
{
    // ... existing properties ...

    /// <summary>
    /// Optional longer label for desktop viewports.
    /// Used when UseResponsiveLabels is true.
    /// Falls back to Label if not provided.
    /// </summary>
    public string? LongLabel { get; init; }

    /// <summary>
    /// Optional data payload for AJAX requests.
    /// Serialized to JSON and included in POST body.
    /// </summary>
    public object? AjaxData { get; init; }
}
```

**No new ViewModels needed.** This extends the existing, proven foundation.

### 2.2 Guild Context Helper

Create `ViewModels/Components/GuildNavBarHelper.cs` for backward compatibility:

```csharp
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Helper methods for creating guild-specific navigation using TabPanelViewModel.
/// Provides backward compatibility with GuildNavBarViewModel usage patterns.
/// </summary>
public static class GuildNavBarHelper
{
    /// <summary>
    /// Create a TabPanelViewModel configured for guild navigation.
    /// </summary>
    public static TabPanelViewModel CreateGuildNavBar(
        ulong guildId,
        string activeTab,
        IEnumerable<GuildNavItem> navItems)
    {
        return new TabPanelViewModel
        {
            Id = "guildNav",
            Tabs = navItems
                .OrderBy(t => t.Order)
                .Select(item => new TabItemViewModel
                {
                    Id = item.Id,
                    Label = item.Label,
                    Href = item.GetUrl(guildId),
                    IconPathOutline = item.IconOutline,
                    IconPathSolid = item.IconSolid
                })
                .ToList(),
            ActiveTabId = activeTab,
            StyleVariant = TabStyleVariant.Underline,
            NavigationMode = TabNavigationMode.PageNavigation,
            AriaLabel = "Guild sections",
            PersistenceMode = TabPersistenceMode.None
        };
    }
}

// KEEP GuildNavItem for reuse
public record GuildNavItem
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string PageName { get; init; } = string.Empty;
    public string UrlPattern { get; init; } = string.Empty;
    public string? IconOutline { get; init; }
    public string? IconSolid { get; init; }
    public int Order { get; init; }

    public string GetUrl(ulong guildId) => UrlPattern.Replace("{guildId}", guildId.ToString());
}
```

### 2.3 CSS Architecture

**Extend `wwwroot/css/tab-panel.css` with variant modifiers:**

```css
/* ================================================
   BASE STYLES (unchanged)
   ================================================ */
.tab-panel-container { /* existing */ }
.tab-panel-tabs { /* existing base */ }
.tab-panel-tab { /* existing base */ }

/* ================================================
   VARIANT: Underline (Default)
   ================================================ */
.tab-panel-tabs.variant-underline {
    /* Existing default styles - no changes needed */
    border-bottom: 2px solid var(--color-border-primary);
}

.tab-panel-tabs.variant-underline .tab-panel-tab {
    border-bottom: 2px solid transparent;
    margin-bottom: -2px;
}

.tab-panel-tabs.variant-underline .tab-panel-tab.active {
    color: var(--color-accent-blue);
    border-bottom-color: var(--color-accent-blue);
}

/* ================================================
   VARIANT: Pills (AudioTabs style)
   ================================================ */
.tab-panel-tabs.variant-pills {
    background: var(--color-bg-secondary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.75rem;
    padding: 0.25rem;
    gap: 0.25rem;
    border-bottom: none; /* Remove underline border */
}

.tab-panel-tabs.variant-pills .tab-panel-tab {
    border-radius: 0.625rem;
    padding: 0.625rem 1rem;
    border-bottom: none;
    margin-bottom: 0;
    transition: all 0.2s ease;
}

.tab-panel-tabs.variant-pills .tab-panel-tab:hover:not(.active):not(.disabled) {
    background: var(--color-bg-hover);
    border-bottom-color: transparent;
}

.tab-panel-tabs.variant-pills .tab-panel-tab.active {
    background: var(--color-accent-blue);
    color: #ffffff;
    border-bottom-color: transparent;
}

.tab-panel-tabs.variant-pills .tab-panel-tab.active .tab-icon {
    color: #ffffff;
}

/* Pills badge styling */
.tab-panel-tabs.variant-pills .tab-badge {
    background: rgba(255, 255, 255, 0.2);
    color: #ffffff;
    padding: 0.125rem 0.5rem;
    border-radius: 0.5rem;
    font-size: 0.75rem;
    font-weight: 600;
    margin-left: 0.5rem;
}

/* ================================================
   VARIANT: Portal (Portal header style)
   ================================================ */
.tab-panel-tabs.variant-portal {
    background: linear-gradient(135deg, var(--color-accent-blue) 0%, var(--color-accent-orange) 100%);
    border: none;
    border-radius: 0.5rem;
    padding: 0.5rem;
    gap: 0.5rem;
}

.tab-panel-tabs.variant-portal .tab-panel-tab {
    color: rgba(255, 255, 255, 0.85);
    border: none;
    border-radius: 0.375rem;
    padding: 0.75rem 1.25rem;
    font-weight: 600;
    transition: all 0.2s ease;
}

.tab-panel-tabs.variant-portal .tab-panel-tab:hover:not(.active):not(.disabled) {
    background: rgba(255, 255, 255, 0.15);
    color: #ffffff;
    border-bottom-color: transparent;
}

.tab-panel-tabs.variant-portal .tab-panel-tab.active {
    background: #ffffff;
    color: var(--color-accent-blue);
    border-bottom-color: transparent;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.tab-panel-tabs.variant-portal .tab-icon {
    color: currentColor;
}

/* ================================================
   RESPONSIVE LABELS
   ================================================ */
/* Hide long labels on mobile by default */
@media (max-width: 640px) {
    .tab-panel-tab[data-label-long] .tab-label-long {
        display: none;
    }
    .tab-panel-tab[data-label-short] .tab-label-short {
        display: inline;
    }
}

/* Show long labels on desktop */
@media (min-width: 641px) {
    .tab-panel-tab[data-label-long] .tab-label-long {
        display: inline;
    }
    .tab-panel-tab[data-label-short] .tab-label-short {
        display: none;
    }
}
```

### 2.4 Partial View Updates

**Update `Pages/Shared/Components/_TabPanel.cshtml`:**

```cshtml
@model DiscordBot.Bot.ViewModels.Components.TabPanelViewModel
@using DiscordBot.Bot.ViewModels.Components
@{
    var containerId = $"{Model.Id}-container";
    var tablistId = $"{Model.Id}-tablist";
    var variantClass = Model.StyleVariant.ToString().ToLowerInvariant();
    var containerClass = "tab-panel-container";
    if (Model.Compact) { containerClass += " tab-panel-compact"; }
    if (!string.IsNullOrEmpty(Model.ContainerClass)) { containerClass += $" {Model.ContainerClass}"; }

    string GetTabClass(TabItemViewModel tab)
    {
        var classes = "tab-panel-tab";
        if (tab.Id == Model.ActiveTabId) { classes += " active"; }
        if (tab.Disabled) { classes += " disabled"; }
        return classes;
    }
}

<div class="@containerClass"
     id="@containerId"
     data-tab-panel
     data-navigation-mode="@Model.NavigationMode.ToString().ToLowerInvariant()"
     data-persistence-mode="@Model.PersistenceMode.ToString().ToLowerInvariant()"
     data-panel-id="@Model.Id"
     data-ajax-url-pattern="@Model.AjaxUrlPattern"
     data-ajax-content-target="@Model.AjaxContentTarget">
    <nav class="tab-panel-tabs variant-@variantClass"
         id="@tablistId"
         role="tablist"
         aria-label="@Model.AriaLabel">
        @foreach (var tab in Model.Tabs)
        {
            var isActive = tab.Id == Model.ActiveTabId;
            var tabElementId = $"{Model.Id}-tab-{tab.Id}";
            var panelElementId = $"{Model.Id}-panel-{tab.Id}";
            var useResponsiveLabels = Model.UseResponsiveLabels && !string.IsNullOrEmpty(tab.LongLabel) && !string.IsNullOrEmpty(tab.ShortLabel);

            @if (Model.NavigationMode == TabNavigationMode.PageNavigation)
            {
                <a href="@(tab.Href ?? "#")"
                   class="@GetTabClass(tab)"
                   id="@tabElementId"
                   role="tab"
                   aria-selected="@(isActive ? "true" : "false")"
                   aria-disabled="@(tab.Disabled ? "true" : null)"
                   tabindex="@(tab.Disabled ? "-1" : null)"
                   data-tab-id="@tab.Id"
                   data-label-long="@(useResponsiveLabels ? tab.LongLabel : null)"
                   data-label-short="@(useResponsiveLabels ? tab.ShortLabel : null)">
                    @{ RenderTabContent(tab, isActive, useResponsiveLabels); }
                </a>
            }
            else
            {
                <button type="button"
                        class="@GetTabClass(tab)"
                        id="@tabElementId"
                        role="tab"
                        aria-selected="@(isActive ? "true" : "false")"
                        aria-controls="@(Model.NavigationMode == TabNavigationMode.InPage ? panelElementId : null)"
                        tabindex="@(isActive ? "0" : "-1")"
                        disabled="@(tab.Disabled ? true : null)"
                        aria-disabled="@(tab.Disabled ? "true" : null)"
                        data-tab-id="@tab.Id"
                        data-ajax-url="@(Model.NavigationMode == TabNavigationMode.Ajax && !string.IsNullOrEmpty(tab.Href) ? tab.Href : null)"
                        data-ajax-data="@(tab.AjaxData != null ? System.Text.Json.JsonSerializer.Serialize(tab.AjaxData) : null)"
                        data-label-long="@(useResponsiveLabels ? tab.LongLabel : null)"
                        data-label-short="@(useResponsiveLabels ? tab.ShortLabel : null)">
                    @{ RenderTabContent(tab, isActive, useResponsiveLabels); }
                </button>
            }
        }
    </nav>
</div>

@* Loading indicator for AJAX mode *@
@if (Model.NavigationMode == TabNavigationMode.Ajax)
{
    <div id="@(Model.AjaxContentTarget ?? $"{Model.Id}-content")"
         class="tab-ajax-content"
         role="region"
         aria-live="polite">
        @* AJAX content loads here *@
    </div>
}

@functions {
    private void RenderTabContent(TabItemViewModel tab, bool isActive, bool useResponsiveLabels)
    {
        // Icon rendering
        if (tab.HasIcon)
        {
            var iconPath = isActive && !string.IsNullOrEmpty(tab.IconPathSolid) ? tab.IconPathSolid : tab.IconPathOutline;
            var isSolid = isActive && !string.IsNullOrEmpty(tab.IconPathSolid);
            <svg class="tab-icon" viewBox="0 0 24 24" fill="@(isSolid ? "currentColor" : "none")" stroke="@(isSolid ? "none" : "currentColor")" aria-hidden="true">
                @if (!isSolid)
                {
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="@iconPath" />
                }
                else
                {
                    <path d="@iconPath" />
                }
            </svg>
        }

        // Label rendering
        if (useResponsiveLabels)
        {
            <span class="tab-label-long">@tab.LongLabel</span>
            <span class="tab-label-short">@tab.ShortLabel</span>
        }
        else
        {
            <span>@tab.Label</span>
        }

        // Badge rendering
        if (tab.HasBadge)
        {
            <span class="tab-badge badge-@tab.BadgeVariant.ToString().ToLowerInvariant()">@tab.BadgeCount</span>
        }
    }
}
```

### 2.5 JavaScript Extensions

**Extend `wwwroot/js/tab-panel.js` with AJAX support:**

```javascript
// Add after line ~115 in initPanel function
if (navigationMode === 'ajax') {
    this.bindAjaxHandlers(container, tabs, panelId, persistenceMode);
}

// Add new method to TabPanel object
/**
 * Bind AJAX click handlers for dynamic content loading.
 * @param {HTMLElement} container - The tab panel container
 * @param {NodeList} tabs - The tab elements
 * @param {string} panelId - The panel ID
 * @param {string} persistenceMode - Persistence mode
 */
bindAjaxHandlers: function(container, tabs, panelId, persistenceMode) {
    const self = this;
    const urlPattern = container.dataset.ajaxUrlPattern;
    const contentTarget = container.dataset.ajaxContentTarget || `#${panelId}-content`;
    const contentElement = document.querySelector(contentTarget);

    if (!contentElement) {
        console.error('TabPanel AJAX: Content target not found:', contentTarget);
        return;
    }

    tabs.forEach(tab => {
        tab.addEventListener('click', function(e) {
            e.preventDefault();
            if (this.disabled || this.getAttribute('aria-disabled') === 'true') return;

            const tabId = this.dataset.tabId;
            const ajaxUrl = this.dataset.ajaxUrl || (urlPattern ? urlPattern.replace('{tabId}', tabId) : null);

            if (!ajaxUrl) {
                console.error('TabPanel AJAX: No URL specified for tab', tabId);
                return;
            }

            // Update active tab state
            self.updateTabState(container, tabs, this);

            // Show loading state
            contentElement.innerHTML = '<div class="tab-ajax-loading" role="status" aria-live="polite"><span class="spinner"></span> Loading...</div>';
            self.announce(`Loading ${this.textContent.trim()}...`);

            // Fetch content
            const ajaxData = this.dataset.ajaxData;
            const fetchOptions = {
                method: ajaxData ? 'POST' : 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Content-Type': 'application/json'
                }
            };

            if (ajaxData) {
                fetchOptions.body = ajaxData;
            }

            fetch(ajaxUrl, fetchOptions)
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                    }
                    return response.text();
                })
                .then(html => {
                    contentElement.innerHTML = html;
                    self.announce(`Content loaded: ${this.textContent.trim()}`);

                    // Persist active tab
                    self.persistActiveTab(panelId, tabId, persistenceMode);

                    // Execute any scripts in the loaded content
                    const scripts = contentElement.querySelectorAll('script');
                    scripts.forEach(script => {
                        const newScript = document.createElement('script');
                        if (script.src) {
                            newScript.src = script.src;
                        } else {
                            newScript.textContent = script.textContent;
                        }
                        script.parentNode.replaceChild(newScript, script);
                    });
                })
                .catch(error => {
                    console.error('TabPanel AJAX error:', error);
                    contentElement.innerHTML = `
                        <div class="tab-ajax-error alert-error" role="alert">
                            <strong>Error loading content:</strong> ${error.message}
                            <button type="button" onclick="this.closest('.tab-panel-container').querySelector('[data-tab-id="${tabId}"]').click()">Retry</button>
                        </div>
                    `;
                    self.announce(`Error loading content: ${error.message}`);
                });
        });
    });
},

/**
 * Update tab state (active/inactive) for all tabs.
 * @param {HTMLElement} container - The tab panel container
 * @param {NodeList} tabs - All tab elements
 * @param {HTMLElement} activeTab - The newly active tab
 */
updateTabState: function(container, tabs, activeTab) {
    tabs.forEach(tab => {
        const isActive = tab === activeTab;
        tab.classList.toggle(this.config.activeClass, isActive);
        tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
        tab.setAttribute('tabindex', isActive ? '0' : '-1');
    });
}
```

---

## 3. Migration Strategy

### Phase 1: Extend Foundation (Feature #1249)

**Tasks:**
1. Add new properties to `TabPanelViewModel` (StyleVariant, AjaxUrlPattern, etc.)
2. Add new enums (TabStyleVariant, update TabNavigationMode)
3. Update `TabItemViewModel` with LongLabel and AjaxData
4. Create `GuildNavBarHelper.cs`
5. No visual changes yet - all changes are additive and backward compatible

**Acceptance Criteria:**
- Existing `_TabPanel` usages continue working without changes
- New properties have sensible defaults
- Unit tests pass

### Phase 2: CSS Variants (Feature #1250)

**Tasks:**
1. Extend `tab-panel.css` with variant-underline, variant-pills, variant-portal
2. Update `_TabPanel.cshtml` to apply variant class
3. Create visual regression tests comparing new Pills/Portal variants to existing implementations

**Acceptance Criteria:**
- Pills variant matches `_AudioTabs` pixel-perfect
- Portal variant matches Portal Header styling
- Underline variant unchanged (no regressions)
- All three variants tested on mobile/tablet/desktop

### Phase 3: JavaScript AJAX Mode (Feature #1251)

**Tasks:**
1. Add `bindAjaxHandlers` method to `tab-panel.js`
2. Add loading state HTML/CSS
3. Add error handling UI
4. Test with PerformanceTabs AJAX navigation

**Acceptance Criteria:**
- AJAX content loads without page refresh
- Loading spinner shown during fetch
- Errors displayed with retry button
- URL hash updates on tab change
- Screen reader announcements work

### Phase 4: Accessibility Enhancements (Feature #1252)

**Tasks:**
1. Audit all three variants for ARIA compliance
2. Test keyboard navigation with NVDA/JAWS
3. Verify focus indicators on all variants
4. Test with axe DevTools

**Acceptance Criteria:**
- All variants pass WCAG 2.1 AA
- Keyboard navigation works consistently across variants
- Screen reader announces tab changes
- Focus indicators visible and meet contrast requirements

### Phase 5: Migration & Documentation (Feature #1253)

**Tasks:**
1. Migrate `_GuildNavBar` to use `GuildNavBarHelper`
2. Migrate `_PerformanceTabs` to use TabPanel with AJAX mode
3. Migrate `_AudioTabs` to use TabPanel with Pills variant
4. Update `design-system.md` with tab component section
5. Create `nav-tabs-component.md` usage guide
6. Create `nav-tabs-migration.md` with before/after examples
7. Mark old ViewModels as `[Obsolete]`

**Acceptance Criteria:**
- All guild pages use new TabPanel component
- Performance dashboard uses AJAX mode
- Audio pages use Pills variant
- Zero visual regressions
- Documentation complete and accurate

### Deprecation Timeline

| Date | Action |
|------|--------|
| **v0.13.0** | Add new properties, mark old ViewModels as `[Obsolete("Use TabPanelViewModel with GuildNavBarHelper")]` |
| **v0.14.0** | Migrate all internal usages, update documentation |
| **v0.15.0** | Remove old ViewModels and partials (`_GuildNavBar`, `_PerformanceTabs`, `_AudioTabs`) |

---

## 4. Implementation Details

### 4.1 Example: Migrating GuildNavBar

**Before (GuildNavBarViewModel):**

```cshtml
@model GuildNavBarViewModel

<div class="guild-nav-container">
    <nav class="guild-nav-tabs" role="tablist">
        @foreach (var tab in Model.Tabs.OrderBy(t => t.Order))
        {
            var isActive = Model.ActiveTab == tab.Id;
            <a href="@tab.GetUrl(Model.GuildId)"
               class="guild-nav-tab @(isActive ? "active" : "")"
               role="tab"
               aria-selected="@(isActive ? "true" : "false")">
                @* Icon and label rendering *@
                <span>@tab.Label</span>
            </a>
        }
    </nav>
</div>
```

**After (TabPanelViewModel with GuildNavBarHelper):**

```csharp
// In Page Model OnGet method
var navItems = new List<GuildNavItem>
{
    new() { Id = "overview", Label = "Overview", UrlPattern = "/Guilds/Details/{guildId}", IconOutline = "...", IconSolid = "...", Order = 1 },
    new() { Id = "members", Label = "Members", UrlPattern = "/Guilds/{guildId}/Members", IconOutline = "...", IconSolid = "...", Order = 2 },
    // ... more items
};

GuildNavViewModel = GuildNavBarHelper.CreateGuildNavBar(guildId, "overview", navItems);
```

```cshtml
@* In Page *@
@await Html.PartialAsync("Components/_TabPanel", Model.GuildNavViewModel)
```

**Benefits:**
- ✅ Consistent keyboard navigation across all pages
- ✅ Automatic accessibility features
- ✅ URL hash persistence if needed
- ✅ Future-proof for new variants

### 4.2 Example: Migrating PerformanceTabs to AJAX

**Before (PerformanceTabsViewModel):**

```cshtml
@model PerformanceTabsViewModel

<div class="performance-tabs-container">
    <nav class="performance-tabs" role="tablist">
        <a @Html.Raw(GetTabAttributes("overview", "/Admin/Performance"))
           class="@GetTabClass("overview")"
           role="tab">
            @* Hardcoded icon and label *@
            <span>Overview</span>
        </a>
        @* ... more tabs *@
    </nav>
</div>
```

**After (TabPanelViewModel with AJAX):**

```csharp
// In Page Model
PerformanceTabsViewModel = new TabPanelViewModel
{
    Id = "performanceTabs",
    StyleVariant = TabStyleVariant.Underline,
    NavigationMode = TabNavigationMode.Ajax,
    AjaxUrlPattern = "/Admin/Performance/Partial/{tabId}",
    AjaxContentTarget = "#performanceContent",
    Tabs = new List<TabItemViewModel>
    {
        new() { Id = "overview", Label = "Overview", IconPathOutline = "...", IconPathSolid = "..." },
        new() { Id = "health", LongLabel = "Health Metrics", ShortLabel = "Health", IconPathOutline = "...", IconPathSolid = "..." },
        new() { Id = "commands", Label = "Commands", IconPathOutline = "...", IconPathSolid = "..." },
        new() { Id = "api", LongLabel = "API & Rate Limits", ShortLabel = "API", IconPathOutline = "...", IconPathSolid = "..." },
        new() { Id = "system", LongLabel = "System Health", ShortLabel = "System", IconPathOutline = "...", IconPathSolid = "..." },
        new() { Id = "alerts", Label = "Alerts", IconPathOutline = "...", IconPathSolid = "...", BadgeCount = Model.ActiveAlertCount, BadgeVariant = TabBadgeVariant.Warning }
    },
    ActiveTabId = "overview",
    AriaLabel = "Performance sections",
    UseResponsiveLabels = true
};
```

```cshtml
@* In Page *@
@await Html.PartialAsync("Components/_TabPanel", Model.PerformanceTabsViewModel)
<div id="performanceContent" class="mt-6">
    @* Initial content or default message *@
</div>
```

**Benefits:**
- ✅ No JavaScript duplication - uses shared tab-panel.js
- ✅ Consistent error handling
- ✅ Loading states built-in
- ✅ ARIA live region announcements

### 4.3 Example: Migrating AudioTabs to Pills

**Before (AudioTabsViewModel):**

```cshtml
@model AudioTabsViewModel

<div class="audio-tabs-container">
    <nav class="audio-tabs" role="tablist">
        <a asp-page="/Guilds/Soundboard/Index" asp-route-guildId="@Model.GuildId"
           class="@GetTabClass("soundboard")"
           role="tab">
            @* Icon rendering *@
            <span>Soundboard</span>
            @if (Model.SoundCount.HasValue && Model.SoundCount.Value > 0)
            {
                <span class="audio-tab-badge">@Model.SoundCount</span>
            }
        </a>
        @* ... more tabs *@
    </nav>
</div>

<style>
    .audio-tabs {
        background: var(--color-bg-secondary);
        border: 1px solid var(--color-border-primary);
        border-radius: 0.75rem;
        padding: 0.25rem;
        gap: 0.25rem;
    }
    @* ... more styles *@
</style>
```

**After (TabPanelViewModel with Pills variant):**

```csharp
// In Page Model
AudioTabsViewModel = new TabPanelViewModel
{
    Id = "audioTabs",
    StyleVariant = TabStyleVariant.Pills,
    NavigationMode = TabNavigationMode.PageNavigation,
    Tabs = new List<TabItemViewModel>
    {
        new() { Id = "soundboard", Label = "Soundboard", Href = $"/Guilds/Soundboard/{guildId}", IconPathOutline = "...", IconPathSolid = "...", BadgeCount = soundCount },
        new() { Id = "tts", Label = "Text-to-Speech", Href = $"/Guilds/TextToSpeech/{guildId}", IconPathOutline = "...", IconPathSolid = "..." },
        new() { Id = "settings", Label = "Settings", Href = $"/Guilds/AudioSettings/{guildId}", IconPathOutline = "...", IconPathSolid = "..." }
    },
    ActiveTabId = "soundboard",
    AriaLabel = "Audio sections"
};
```

```cshtml
@* In Page *@
@await Html.PartialAsync("Components/_TabPanel", Model.AudioTabsViewModel)
```

**Benefits:**
- ✅ No inline `<style>` needed - uses shared CSS
- ✅ Consistent with other components
- ✅ Automatic keyboard navigation
- ✅ Badge styling handled by TabPanel

---

## 5. Testing Requirements

### 5.1 Visual Regression Tests

**Tool:** Percy or manual screenshot comparison

**Test Cases:**
- [ ] Underline variant matches existing GuildNavBar
- [ ] Pills variant matches existing AudioTabs
- [ ] Portal variant matches existing Portal Header
- [ ] Active state correct for all variants
- [ ] Hover states correct for all variants
- [ ] Focus indicators visible and styled correctly
- [ ] Badges render correctly in Pills and Underline variants
- [ ] Icons swap outline/solid correctly when active
- [ ] Mobile responsive behavior (scroll indicators, short labels)

### 5.2 Accessibility Tests

**Tools:** axe DevTools, NVDA, JAWS, VoiceOver

**Test Cases:**
- [ ] ARIA attributes correct (role, aria-selected, aria-controls, aria-labelledby)
- [ ] Keyboard navigation works (Arrow keys, Home, End, Tab, Enter, Space)
- [ ] Focus indicators meet WCAG AA contrast (4.5:1)
- [ ] Screen reader announces tab changes
- [ ] ARIA live region announces AJAX loading/errors
- [ ] Disabled tabs not focusable
- [ ] Tab order logical

### 5.3 Functional Tests

**Test Cases:**
- [ ] In-page mode shows/hides correct panels
- [ ] Page navigation mode navigates to correct URLs
- [ ] AJAX mode fetches content without page reload
- [ ] URL hash persistence works (forward/back buttons)
- [ ] localStorage persistence works (page reload)
- [ ] Scroll indicators appear/disappear correctly
- [ ] Long/short labels switch at correct breakpoint
- [ ] AJAX loading spinner shown during fetch
- [ ] AJAX error UI shown on failure
- [ ] AJAX retry button works

### 5.4 Performance Tests

**Test Cases:**
- [ ] CSS bundle size increase < 5KB
- [ ] JavaScript bundle size increase < 10KB
- [ ] AJAX requests don't block UI
- [ ] Scroll performance smooth (60fps)
- [ ] No layout shift when switching variants

---

## 6. Documentation Requirements

### 6.1 Design System Update

Update `docs/articles/design-system.md`:

**Add section:**

```markdown
## Navigation Component (Tabs)

### Overview

The unified tab navigation component (`_TabPanel`) provides accessible, keyboard-navigable tabs with three visual styles and multiple navigation modes.

### When to Use

- Switching between related content on the same page (In-page mode)
- Navigating between related pages in a section (Page navigation mode)
- Loading content dynamically without page refresh (AJAX mode)

### When Not to Use

- For primary site navigation (use main nav)
- For dropdown menus (use dropdown component)
- For fewer than 2 tabs (use different UI pattern)

### Style Variants

#### Underline (Default)
- Used for: Guild pages, Performance dashboard, Commands pages
- Visual: Horizontal tabs with bottom border on active tab
- Best for: 3-8 tabs with icons and labels

#### Pills
- Used for: Audio section, settings groups
- Visual: Rounded container with filled background on active tab
- Best for: 2-5 tabs, compact spacing

#### Portal
- Used for: Portal pages (public-facing)
- Visual: Discord-branded gradient background
- Best for: Portal-specific pages requiring distinct branding

### Code Example

[Include example from migration guide]
```

### 6.2 Component Usage Guide

Create `docs/articles/nav-tabs-component.md`:

**Sections:**
1. Overview
2. Basic Usage (minimal example)
3. Configuration Options (all properties explained)
4. Navigation Modes (InPage, PageNavigation, AJAX)
5. Style Variants (with screenshots)
6. Advanced Features (badges, icons, responsive labels)
7. Accessibility Features
8. JavaScript API (for custom integrations)
9. Troubleshooting

### 6.3 Migration Guide

Create `docs/articles/nav-tabs-migration.md`:

**Sections:**
1. Migration Overview
2. Breaking Changes (none expected)
3. Deprecation Timeline
4. Migrating from GuildNavBar (step-by-step)
5. Migrating from PerformanceTabs (step-by-step)
6. Migrating from AudioTabs (step-by-step)
7. Migrating Portal Header tabs (step-by-step)
8. Testing Checklist
9. Rollback Plan

---

## 7. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CSS conflicts between variants | Medium | Medium | Use BEM naming, variant-specific selectors, test thoroughly |
| Breaking existing TabPanel users | Low | High | Make all new properties optional with defaults, extensive testing |
| AJAX mode CSRF issues | Medium | High | Include anti-forgery tokens in fetch requests |
| Performance regression | Low | Medium | Monitor bundle size, test on slow devices |
| Accessibility regressions | Low | High | Audit with axe DevTools, test with screen readers |
| Mobile dropdown pattern lost | Medium | Medium | Document mobile dropdown as separate guild-specific component |
| Migration effort underestimated | Medium | Medium | Create migration script/tool to automate some conversions |

---

## 8. Success Criteria

**Quantitative:**
- [ ] CSS bundle size increase < 5KB
- [ ] JavaScript bundle size increase < 10KB
- [ ] Zero breaking changes to existing TabPanel usages
- [ ] 100% WCAG 2.1 AA compliance
- [ ] 5 components consolidated into 1

**Qualitative:**
- [ ] All migrated pages visually identical to originals
- [ ] Developers prefer new component over creating custom tabs
- [ ] Reduced code duplication (DRY principle achieved)
- [ ] Clear documentation with migration path
- [ ] Positive feedback from accessibility audit

---

## 9. Timeline Estimate

| Phase | Features | Effort | Dependencies |
|-------|----------|--------|--------------|
| **Phase 1** | Core extensions | 3-5 hours | None |
| **Phase 2** | CSS variants | 4-6 hours | Phase 1 |
| **Phase 3** | AJAX mode | 6-8 hours | Phase 1 |
| **Phase 4** | Accessibility audit | 4-6 hours | Phases 2, 3 |
| **Phase 5** | Migration & docs | 8-12 hours | All phases |
| **Total** | All features | **25-37 hours** | - |

**Parallel Work Opportunities:**
- Phase 2 (CSS) and Phase 3 (JS) can be developed in parallel after Phase 1
- Documentation can start during Phase 4

---

## 10. Appendix: Design Decisions

### Why Extend Instead of Replace?

**Decision:** Extend `TabPanelViewModel` rather than create `NavTabsViewModel`.

**Rationale:**
- `TabPanelViewModel` already has 90% of needed functionality
- Creating a new ViewModel would fragment the ecosystem
- Existing users of `_TabPanel` would have no upgrade path
- Maintaining two similar but different systems increases complexity
- Design system should have ONE tab component, not two

### Why Not Make Mobile Dropdown Part of Core?

**Decision:** Keep mobile dropdown as guild-specific wrapper.

**Rationale:**
- Mobile dropdown is only used in GuildNavBar (1 of 5 implementations)
- Dropdown adds significant complexity (state management, animations)
- Other implementations work fine with horizontal scroll on mobile
- Guild-specific wrapper can compose TabPanel + dropdown if needed

### Why Three Style Variants Instead of CSS Classes?

**Decision:** Enum-based variants instead of arbitrary CSS class names.

**Rationale:**
- Type safety - compiler enforces valid variants
- Self-documenting - clear list of supported styles
- Prevents typos and invalid combinations
- Easier to deprecate/rename variants in future

### Why AJAX Mode Instead of Custom JavaScript?

**Decision:** Built-in AJAX mode instead of expecting pages to implement their own.

**Rationale:**
- Consistency - all AJAX tabs work the same way
- Accessibility - loading states and announcements handled automatically
- Error handling - unified error UI and retry logic
- Reduces boilerplate - no need to write fetch logic per page

---

## 11. Questions & Answers

**Q: Can I still use the old GuildNavBarViewModel?**
A: Yes, for now. It will be marked `[Obsolete]` in v0.13.0 and removed in v0.15.0. Use `GuildNavBarHelper.CreateGuildNavBar()` instead.

**Q: What if I need a custom variant not in the three provided?**
A: Use `ContainerClass` to add custom styling, or propose a new variant for inclusion.

**Q: How do I handle guild-specific permissions in tabs?**
A: Filter the `Tabs` list in your Page Model before passing to TabPanel. Set `Disabled = true` for tabs the user can't access.

**Q: Can I mix navigation modes (e.g., some tabs InPage, some PageNavigation)?**
A: No, all tabs in one TabPanel must use the same navigation mode. Create separate TabPanel instances if needed.

**Q: What about vertical tabs?**
A: Not currently supported. Horizontal-only for consistency. If needed, file a feature request with use case.

**Q: How do I add animations to tab switching?**
A: For InPage mode, add CSS transitions to your tab panels. For AJAX mode, add transitions to the content container.

---

**End of Specification**

**Next Steps:**
1. Review this spec with feature implementers (#1249-#1253)
2. Create implementation tasks from Phase 1
3. Set up visual regression testing infrastructure
4. Begin Phase 1 development
