# Navigation Tabs Migration Guide

**Version:** 1.0
**Last Updated:** 2026-01-26
**Related Issue:** [#1253](https://github.com/cpike5/discordbot/issues/1253)

---

## Overview

This guide documents how to migrate from legacy tab implementations to the unified `NavTabs` component. The new component consolidates functionality from multiple ad-hoc implementations into a single, consistent, and maintainable solution.

### Legacy Components to Migrate

| Component | Location | Purpose | Migration Priority |
|-----------|----------|---------|-------------------|
| `_GuildNavBar` | `Pages/Shared/Components/` | Guild-level navigation | High |
| `_PerformanceTabs` | `Pages/Shared/Components/` | Performance dashboard tabs | High |
| `_AudioTabs` | `Pages/Shared/Components/` | Audio feature tabs | High |
| `_TabPanel` | `Pages/Shared/Components/` | Generic tab panels | Medium |
| `PerformanceTabsViewModel` | `ViewModels/Components/` | Performance tabs model | High |
| `AudioTabsViewModel` | `ViewModels/Components/` | Audio tabs model | High |

### Benefits of Migration

- **Consistent Styling**: All tabs use the same design language
- **Unified API**: Single ViewModel for all tab scenarios
- **Easier Maintenance**: Fix bugs in one place, benefit everywhere
- **Better Performance**: Shared CSS and JavaScript reduces overhead
- **Improved Testing**: Easier to test with consistent patterns
- **Enhanced Accessibility**: All implementations follow WCAG 2.1 AA
- **Better DX**: Developers learn one API instead of multiple

---

## Quick Migration Steps

1. Create a `NavTabsViewModel` in your page model
2. Populate tabs list from your current tab data
3. Set the appropriate `NavigationMode` and `StyleVariant`
4. Replace the old partial include with `_NavTabs`
5. Update content panels to use new data attributes
6. Remove old CSS/JavaScript if not used elsewhere
7. Test all navigation modes
8. Deploy and monitor

---

## Migration: PerformanceTabs

### Before (Old Implementation)

```csharp
// PerformanceModel.cs
public class PerformanceModel : PageModel
{
    public PerformanceTabsViewModel Tabs { get; set; }

    public IActionResult OnGet(string activeTab = "overview")
    {
        Tabs = new PerformanceTabsViewModel
        {
            ActiveTab = activeTab,
            UseAjaxNavigation = false,
            ActiveAlertCount = 3
        };

        return Page();
    }
}
```

```html
<!-- Performance.cshtml -->
@model PerformanceModel

@section Styles {
    <link rel="stylesheet" href="~/css/performance-tabs.css" />
}

@section Scripts {
    <script src="~/js/performance-tabs.js"></script>
}

@await Html.PartialAsync("Components/_PerformanceTabs", Model.Tabs)

<!-- Manual content panels -->
<div id="overview-tab" class="tab-content">Overview content</div>
<div id="health-tab" class="tab-content" hidden>Health content</div>
<!-- More tabs... -->
```

### After (New NavTabs)

```csharp
// PerformanceModel.cs
public class PerformanceModel : PageModel
{
    public NavTabsViewModel Tabs { get; set; }

    public IActionResult OnGet(string activeTab = "overview")
    {
        var alertCount = 3; // Your alert logic

        Tabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new NavTabItem { Id = "overview", Label = "Overview" },
                new NavTabItem
                {
                    Id = "health",
                    Label = "Health Metrics",
                    ShortLabel = "Health"
                },
                new NavTabItem { Id = "commands", Label = "Commands" },
                new NavTabItem
                {
                    Id = "api",
                    Label = "API & Rate Limits",
                    ShortLabel = "API"
                },
                new NavTabItem { Id = "system", Label = "System" },
                new NavTabItem { Id = "alerts", Label = "Alerts" }
            },
            ActiveTabId = activeTab,
            NavigationMode = NavMode.InPage,  // Or AJAX if you prefer
            StyleVariant = NavTabStyle.Underline,
            PersistenceMode = NavPersistence.Hash,
            ContainerId = "performanceTabs",
            AriaLabel = "Performance metrics sections"
        };

        return Page();
    }
}
```

```html
<!-- Performance.cshtml -->
@model PerformanceModel

@section Styles {
    <link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" />
}

@section Scripts {
    <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
}

@await Html.PartialAsync("Components/_NavTabs", Model.Tabs)

<!-- New content panel structure -->
<div data-nav-panel-for="performanceTabs" data-tab-id="overview">
    <div class="card">
        <h2>Overview</h2>
        <!-- Overview content -->
    </div>
</div>

<div data-nav-panel-for="performanceTabs" data-tab-id="health" hidden>
    <div class="card">
        <h2>Health Metrics</h2>
        <!-- Health metrics content -->
    </div>
</div>

<!-- Rest of tabs... -->
```

### Key Changes

| Old | New | Notes |
|-----|-----|-------|
| `PerformanceTabsViewModel` | `NavTabsViewModel` | New unified model |
| `ActiveTab` property | `ActiveTabId` property | Renamed, same purpose |
| `UseAjaxNavigation` flag | `NavigationMode` enum | More flexible, includes PageNavigation |
| ID-based div selectors | `data-nav-panel-for` attributes | Consistent, clearer intent |
| Manual tab styling | Auto-styled via variant | Underline, Pills, or Bordered |
| `performance-tabs.css` | `nav-tabs.css` | Shared stylesheet |
| `performance-tabs.js` | `nav-tabs.js` | Shared JavaScript |

### ViewModel Mapping

```csharp
// Property mapping guide
PerformanceTabsViewModel          →  NavTabsViewModel
├── ActiveTab                     →  ActiveTabId
├── UseAjaxNavigation             →  NavigationMode (AJAX/InPage)
├── [hardcoded tabs]              →  Tabs (IReadOnlyList<NavTabItem>)
├── [no style variant]            →  StyleVariant (Pills recommended)
└── [no persistence]              →  PersistenceMode (Hash recommended)
```

### Content Panel Migration

```html
<!-- Old Pattern -->
<div id="performance-tab-overview" class="tab-content">
    <!-- Content -->
</div>

<!-- New Pattern -->
<div data-nav-panel-for="performanceTabs" data-tab-id="overview">
    <!-- Content -->
</div>

<!-- Attributes to use -->
data-nav-panel-for="performanceTabs"  <!-- Must match ContainerId -->
data-tab-id="overview"                <!-- Must match NavTabItem.Id -->
```

### Icon Integration (Optional)

If you want to add icons like the old implementation:

```csharp
var icons = new Dictionary<string, (string outline, string solid)>
{
    ["overview"] = (
        "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z",
        "M2 11a1 1 0 011-1h2a1 1 0 011 1v5a1 1 0 01-1 1H3a1 1 0 01-1-1v-5zm6-4a1 1 0 011-1h2a1 1 0 011 1v9a1 1 0 01-1 1H9a1 1 0 01-1-1V7zm6-3a1 1 0 011-1h2a1 1 0 011 1v12a1 1 0 01-1 1h-2a1 1 0 01-1-1V4z"
    ),
    // ... more icons
};

var tabs = new List<NavTabItem>
{
    new NavTabItem
    {
        Id = "overview",
        Label = "Overview",
        IconPathOutline = icons["overview"].outline,
        IconPathSolid = icons["overview"].solid
    },
    // ... more tabs
};
```

---

## Migration: AudioTabs

### Before (Old Implementation)

```csharp
// AudioModel.cs
public class AudioModel : PageModel
{
    public AudioTabsViewModel Tabs { get; set; }

    public IActionResult OnGet(ulong guildId, string activeTab = "soundboard")
    {
        Tabs = new AudioTabsViewModel
        {
            GuildId = guildId,
            ActiveTab = activeTab,
            SoundCount = 42
        };

        return Page();
    }
}
```

```html
<!-- Audio.cshtml -->
@model AudioModel

@await Html.PartialAsync("Components/_AudioTabs", Model.Tabs)
```

### After (New NavTabs with Page Navigation)

```csharp
// AudioModel.cs
public class AudioModel : PageModel
{
    public NavTabsViewModel Tabs { get; set; }

    public IActionResult OnGet(ulong guildId, string activeTab = "soundboard")
    {
        Tabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new NavTabItem
                {
                    Id = "soundboard",
                    Label = "Soundboard",
                    ShortLabel = "Sounds",
                    Href = $"/guilds/{guildId}/audio/soundboard",
                    IconPathOutline = "M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3",
                    IconPathSolid = "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"
                },
                new NavTabItem
                {
                    Id = "tts",
                    Label = "Text-to-Speech",
                    Href = $"/guilds/{guildId}/audio/tts",
                    IconPathOutline = "M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"
                },
                new NavTabItem
                {
                    Id = "settings",
                    Label = "Settings",
                    Href = $"/guilds/{guildId}/audio/settings",
                    IconPathOutline = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                }
            },
            ActiveTabId = activeTab,
            NavigationMode = NavMode.PageNavigation,
            StyleVariant = NavTabStyle.Pills,
            ContainerId = "audioFeatures",
            AriaLabel = "Audio feature tabs"
        };

        return Page();
    }
}
```

### Key Changes

| Old | New |
|-----|-----|
| `AudioTabsViewModel` | `NavTabsViewModel` |
| `GuildId` property | Not needed (use in Href) |
| `ActiveTab` | `ActiveTabId` |
| `SoundCount` | Not tracked (use badge if needed) |
| Manual icon setup in partial | Icons in NavTabItem |
| Hard-coded layout | Style variants (Pills used here) |

### If Using In-Page Mode Instead

If you prefer to keep content on one page:

```csharp
Tabs = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "soundboard", Label = "Soundboard", ShortLabel = "Sounds" },
        new NavTabItem { Id = "tts", Label = "Text-to-Speech" },
        new NavTabItem { Id = "settings", Label = "Settings" }
    },
    ActiveTabId = activeTab,
    NavigationMode = NavMode.InPage,  // Changed
    StyleVariant = NavTabStyle.Pills,
    ContainerId = "audioFeatures"
};
```

---

## Migration: GuildNavBar

### Before (Old Implementation)

```csharp
// GuildModel.cs
public IActionResult OnGet(ulong guildId, string activeNav = "overview")
{
    var guildNavItems = new List<(string id, string label, string href)>
    {
        ("overview", "Overview", $"/guilds/{guildId}/overview"),
        ("members", "Members", $"/guilds/{guildId}/members"),
        ("settings", "Settings", $"/guilds/{guildId}/settings")
    };

    ViewData["GuildNavItems"] = guildNavItems;
    ViewData["ActiveNav"] = activeNav;

    return Page();
}
```

### After (New NavTabs)

```csharp
// GuildModel.cs
public IActionResult OnGet(ulong guildId, string activeNav = "overview")
{
    var navTabs = new NavTabsViewModel
    {
        Tabs = new List<NavTabItem>
        {
            new NavTabItem
            {
                Id = "overview",
                Label = "Overview",
                Href = $"/guilds/{guildId}/overview"
            },
            new NavTabItem
            {
                Id = "members",
                Label = "Members",
                Href = $"/guilds/{guildId}/members"
            },
            new NavTabItem
            {
                Id = "settings",
                Label = "Settings",
                Href = $"/guilds/{guildId}/settings"
            }
        },
        ActiveTabId = activeNav,
        NavigationMode = NavMode.PageNavigation,
        StyleVariant = NavTabStyle.Underline,
        ContainerId = "guildNav",
        AriaLabel = "Guild navigation"
    };

    return Page();
}
```

### Simplified Model

With NavTabs, you no longer need to build and pass tuple lists. Just create the ViewModel directly.

---

## Migration: TabPanel

### Before (Old Implementation)

Generic tab panel component with manual tab management:

```html
@await Html.PartialAsync("Components/_TabPanel", new {
    TabId = "settings-panel",
    Tabs = new[] {
        new { Id = "general", Label = "General" },
        new { Id = "advanced", Label = "Advanced" }
    },
    ActiveTab = "general"
})
```

### After (New NavTabs)

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "general", Label = "General" },
        new NavTabItem { Id = "advanced", Label = "Advanced" }
    },
    ActiveTabId = "general",
    NavigationMode = NavMode.InPage,
    StyleVariant = NavTabStyle.Underline,
    ContainerId = "settingsPanel"
};
```

```html
@await Html.PartialAsync("Components/_NavTabs", Model)

<!-- Content panels with consistent naming -->
<div data-nav-panel-for="settingsPanel" data-tab-id="general">
    <!-- General settings -->
</div>

<div data-nav-panel-for="settingsPanel" data-tab-id="advanced" hidden>
    <!-- Advanced settings -->
</div>
```

### Benefits

- **Unified naming**: All components use `data-nav-panel-for` and `data-tab-id`
- **Consistent styling**: No more custom panel CSS per implementation
- **Shared JavaScript**: One event handler for all panels
- **Easier testing**: Single component to test

---

## Testing Checklist

After migrating to NavTabs, verify:

### Functionality
- [ ] Clicking tabs switches to correct content
- [ ] Active tab is visually indicated
- [ ] Hash updates correctly (if using Hash persistence)
- [ ] Back/forward buttons work (if using Hash)
- [ ] Content doesn't show/hide unexpectedly

### Navigation Mode
- [ ] For PageNavigation: Each tab navigates to correct URL
- [ ] For InPage: Content switches without page reload
- [ ] For AJAX: Content loads and displays correctly

### Responsive
- [ ] Short labels display on mobile
- [ ] Full labels display on desktop
- [ ] Tabs scroll horizontally if needed
- [ ] Scroll indicators appear/disappear correctly

### Accessibility
- [ ] Tab to tabs navigates through them
- [ ] Arrow keys move between tabs
- [ ] Enter/Space activates tabs
- [ ] Screen reader announces tabs and active state
- [ ] ARIA attributes are correct
- [ ] Disabled tabs are skipped in keyboard navigation

### Styling
- [ ] Style variant displays correctly
- [ ] Active tab is clearly visible
- [ ] Hover states work
- [ ] Icons display if provided
- [ ] Colors match design system

### Performance
- [ ] No layout shift when switching tabs
- [ ] AJAX content loads quickly
- [ ] No memory leaks with repeated tab switches
- [ ] CSS/JavaScript load without blocking

---

## Common Pitfalls

### Pitfall 1: Using Old Partial Name

```csharp
// Wrong
@await Html.PartialAsync("Components/_PerformanceTabs", Model.OldTabs)

// Correct
@await Html.PartialAsync("Components/_NavTabs", Model.NewTabs)
```

### Pitfall 2: Missing ContainerId Consistency

```csharp
// Wrong - Partial vs HTML attribute mismatch
NavTabsViewModel: ContainerId = "performanceTabs"
HTML: data-nav-panel-for="perfTabs"  // Doesn't match!

// Correct
NavTabsViewModel: ContainerId = "performanceTabs"
HTML: data-nav-panel-for="performanceTabs"  // Matches!
```

### Pitfall 3: Forgetting CSS/JS Includes

```csharp
// Wrong - No styles or scripts
@await Html.PartialAsync("Components/_NavTabs", Model)

// Correct
@section Styles {
    <link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" />
}

@section Scripts {
    <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
}

@await Html.PartialAsync("Components/_NavTabs", Model)
```

### Pitfall 4: Incorrect Data Attribute Names

```html
<!-- Wrong - Old naming pattern -->
<div id="panel-overview" class="tab-content">

<!-- Correct - New naming pattern -->
<div data-nav-panel-for="performanceTabs" data-tab-id="overview">
```

### Pitfall 5: Not Setting NavigationMode

```csharp
// Wrong - Defaults to InPage if content is client-side, confusing
var model = new NavTabsViewModel { /* no NavigationMode set */ };

// Correct - Explicit about intended behavior
var model = new NavTabsViewModel
{
    NavigationMode = NavMode.PageNavigation,
    // ... other properties
};
```

### Pitfall 6: Mixing Old and New Components

```csharp
// Wrong - Can't mix components on same page
@await Html.PartialAsync("Components/_PerformanceTabs", oldModel)
@await Html.PartialAsync("Components/_NavTabs", newModel)

// Correct - Migrate entire page at once
@await Html.PartialAsync("Components/_NavTabs", newModel)
```

---

## Rollback Plan

If issues arise during migration:

1. Keep old components in a feature branch
2. Use `git revert` to restore old implementation
3. File issues in GitHub
4. Work with team to resolve problems
5. Migrate again once fixed

```bash
# If needed to rollback
git revert <commit-hash>

# Then deploy old version
```

---

## Migration Order

Recommended order for component migration:

1. **Phase 1 (High Priority)**: Start with pages with fewer interactions
   - Single-instance pages (e.g., settings overview)
   - Pages with simple in-page navigation

2. **Phase 2 (Medium)**: Multi-instance pages
   - Pages with multiple tab groups
   - Complex permission-based content

3. **Phase 3 (Low Priority)**: Dynamic/AJAX content
   - Performance dashboard
   - Real-time data pages

### Example Schedule

```
Week 1: Migrate AudioTabs → NavTabs
Week 2: Migrate PerformanceTabs → NavTabs
Week 3: Migrate GuildNavBar → NavTabs
Week 4: Clean up old components, test thoroughly
```

---

## Cleanup After Migration

Once all pages are migrated, remove legacy components:

1. **Deleted partial files:**
   - ~~`_PerformanceTabs.cshtml`~~ ✅ Removed
   - ~~`_AudioTabs.cshtml`~~ ✅ Removed
   - ~~`_GuildNavBar.cshtml`~~ ✅ Removed (desktop navigation now uses `_TabPanel`, mobile uses inline dropdown)

2. **Deleted ViewModels:**
   - ~~`PerformanceTabsViewModel.cs`~~ ✅ Removed
   - ~~`AudioTabsViewModel.cs`~~ (if it existed)

3. **Kept ViewModels:**
   - `GuildNavBarViewModel.cs` - ✅ **Still in use** for mobile dropdown in `_GuildLayout.cshtml`

4. Delete old CSS (if not shared):
   - `performance-tabs.css`
   - `audio-tabs.css`

5. Delete old JavaScript (if not shared):
   - `performance-tabs.js`
   - `audio-tabs.js`

6. Update documentation:
   - Update component index
   - Deprecate old guides
   - Link to NavTabs guide

---

## Need Help?

If you encounter issues during migration:

1. Check [Navigation Tabs Component Guide](nav-tabs-component.md)
2. Review the [troubleshooting section](nav-tabs-component.md#troubleshooting)
3. Look at existing migrated examples in the codebase
4. Create an issue with details

---

## Related Documentation

- [Navigation Tabs Component Guide](nav-tabs-component.md)
- [Component API - NavTabs](component-api.md#navtabs-component)
- [Design System - Navigation Tabs](design-system.md#navigation-tabs)
- [Interactive Components](interactive-components.md)

## Files

- **ViewModel:** `src/DiscordBot.Bot/ViewModels/Components/NavTabsViewModel.cs`
- **Partial:** `src/DiscordBot.Bot/Pages/Shared/Components/_NavTabs.cshtml`
- **Styles:** `src/DiscordBot.Bot/wwwroot/css/nav-tabs.css`
- **Scripts:** `src/DiscordBot.Bot/wwwroot/js/nav-tabs.js`

---

## Changelog

### Version 1.0 (2026-01-26)
- Initial migration guide
- Documented migration from PerformanceTabs
- Documented migration from AudioTabs
- Documented migration from GuildNavBar
- Documented migration from TabPanel
- Provided testing checklist
- Documented common pitfalls and solutions
- Created rollback plan
- Related to Issue #1253: Feature: Navigation Component Documentation and Usage Guide

