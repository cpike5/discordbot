# Navigation Tabs Component Guide

**Version:** 1.0
**Last Updated:** 2026-01-26
**Related Issue:** [#1253](https://github.com/cpike5/discordbot/issues/1253)

---

## Overview

The Navigation Tabs component is a unified, flexible tab navigation system that replaces multiple legacy tab implementations across the Discord Bot Admin UI. It provides a consistent pattern for organizing content into logical sections with support for multiple navigation styles and interaction modes.

### Key Features

- **Multiple Style Variants**: Underline, Pills, and Bordered visual styles
- **Three Navigation Modes**: In-page, page navigation, and AJAX-based loading
- **Flexible Persistence**: State can be persisted via URL hash or localStorage
- **Icon Support**: Optional outline and solid SVG icons for visual richness
- **Mobile Responsive**: Automatic label abbreviation and horizontal scrolling
- **Full Accessibility**: ARIA roles, keyboard navigation, and screen reader support
- **Extensible Design**: Easy to customize and integrate with existing components

### Problem It Solves

Previously, the application used multiple ad-hoc tab implementations (`GuildNavBar`, `PerformanceTabs`, `AudioTabs`, `TabPanel`, etc.) with inconsistent APIs, styling, and behavior. This component provides a single, standardized solution that:

- Eliminates code duplication
- Ensures visual consistency
- Provides a predictable developer experience
- Simplifies maintenance and updates
- Reduces CSS and JavaScript overhead

---

## Basic Usage

### Minimal Example

```csharp
// In your page model or controller
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "tab1", Label = "First Tab" },
        new NavTabItem { Id = "tab2", Label = "Second Tab" }
    },
    ActiveTabId = "tab1"
};

return View(model);
```

```html
<!-- In your Razor Page -->
@model NavTabsViewModel

@section Styles {
    <link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" />
}

@section Scripts {
    <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
}

@await Html.PartialAsync("Components/_NavTabs", Model)
```

### ViewModel Setup

```csharp
// Create the ViewModel
var navTabs = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem
        {
            Id = "overview",
            Label = "Overview",
            ShortLabel = "Info"  // For mobile
        },
        new NavTabItem
        {
            Id = "settings",
            Label = "Settings"
        }
    },
    ActiveTabId = "overview",
    StyleVariant = NavTabStyle.Underline,
    NavigationMode = NavMode.InPage,
    PersistenceMode = NavPersistence.Hash,
    ContainerId = "settingsTabs",
    AriaLabel = "Settings sections"
};

return View(navTabs);
```

### Partial Inclusion

The `_NavTabs.cshtml` partial requires the CSS and JavaScript to be loaded:

```html
<!-- Required CSS -->
@section Styles {
    <link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" />
}

<!-- Required JavaScript -->
@section Scripts {
    <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
}

<!-- Include the partial -->
@await Html.PartialAsync("Components/_NavTabs", Model)
```

---

## Configuration Options

### NavTabsViewModel Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Tabs` | `IReadOnlyList<NavTabItem>` | `[]` | Collection of navigation tabs to display |
| `ActiveTabId` | `string` | `string.Empty` | ID of the currently active tab |
| `StyleVariant` | `NavTabStyle` | `Underline` | Visual style of the tabs (Underline, Pills, Bordered) |
| `NavigationMode` | `NavMode` | `InPage` | How tabs navigate (InPage, PageNavigation, Ajax) |
| `PersistenceMode` | `NavPersistence` | `Hash` | How active tab state is persisted (None, Hash, LocalStorage) |
| `AriaLabel` | `string` | `"Navigation"` | ARIA label for accessibility |
| `ContainerId` | `string` | `"navTabs"` | Unique identifier for this component instance |

### NavTabItem Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `string` | Yes | Unique identifier for the tab |
| `Label` | `string` | Yes | Display label for the tab |
| `ShortLabel` | `string?` | No | Abbreviated label for mobile devices |
| `Href` | `string?` | Conditional | URL for PageNavigation and AJAX modes |
| `IconPathOutline` | `string?` | No | SVG path data for outline icon |
| `IconPathSolid` | `string?` | No | SVG path data for solid (active) icon |
| `Disabled` | `bool` | No | Whether tab can be selected |
| `HasIcon` | `bool` (readonly) | N/A | Helper property indicating icon presence |

### Style Variants

**Underline** (`NavTabStyle.Underline`)
- Default variant
- Minimal visual weight
- Suitable for content pages
- Active indicator: Bottom underline

**Pills** (`NavTabStyle.Pills`)
- Rounded background on active tab
- Higher visual prominence
- Good for modals and cards
- Active indicator: Rounded background

**Bordered** (`NavTabStyle.Bordered`)
- Full borders around tabs
- Clear tab separation
- Good for discrete sections
- Active indicator: Highlight

### Navigation Modes

**In-Page** (`NavMode.InPage`)
- Content panels switch without page reload
- Content is rendered with the page
- JavaScript toggles visibility
- Best for: Related content on same page
- Requires: Content panels with `data-nav-panel-for` and `data-tab-id` attributes

**Page Navigation** (`NavMode.PageNavigation`)
- Each tab navigates to a different URL
- Full page load occurs
- URL changes in address bar
- Best for: Separate pages or sections
- Requires: `Href` property on each NavTabItem

**AJAX** (`NavMode.Ajax`)
- Content loads dynamically via AJAX
- No full page reload
- URL may update based on persistence mode
- Best for: Heavy content or API-backed data
- Requires: `Href` property (API endpoint) on each NavTabItem

### Persistence Modes

**None** (`NavPersistence.None`)
- No persistence
- Tab resets to default on page reload
- Default for page navigation mode

**Hash** (`NavPersistence.Hash`)
- Active tab stored in URL hash (e.g., `#overview`)
- Shareable URLs
- Survives page reload
- Default for in-page and AJAX modes

**LocalStorage** (`NavPersistence.LocalStorage`)
- Stored in browser localStorage
- Clean URLs (no hash)
- Persists across sessions
- User-specific per domain

---

## Navigation Modes

### In-Page Navigation

Content panels are rendered on the page and switched client-side.

```csharp
// Controller/Page Model
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "overview", Label = "Overview" },
        new NavTabItem { Id = "health", Label = "Health Metrics" },
        new NavTabItem { Id = "commands", Label = "Commands" }
    },
    ActiveTabId = "overview",
    NavigationMode = NavMode.InPage,
    StyleVariant = NavTabStyle.Underline,
    ContainerId = "performanceTabs"
};

return View(model);
```

```html
<!-- Razor Page -->
@model NavTabsViewModel

@await Html.PartialAsync("Components/_NavTabs", Model)

<!-- Content Panels - hidden until activated -->
<div data-nav-panel-for="performanceTabs" data-tab-id="overview">
    <div class="card">
        <h2>Overview</h2>
        <p>Performance overview content...</p>
    </div>
</div>

<div data-nav-panel-for="performanceTabs" data-tab-id="health" hidden>
    <div class="card">
        <h2>Health Metrics</h2>
        <p>Health metrics content...</p>
    </div>
</div>

<div data-nav-panel-for="performanceTabs" data-tab-id="commands" hidden>
    <div class="card">
        <h2>Commands</h2>
        <p>Commands content...</p>
    </div>
</div>
```

**JavaScript Behavior:**
- Clicking a tab finds the corresponding panel with matching `data-nav-panel-for` and `data-tab-id`
- Current panel's `hidden` attribute is removed
- Previous panel's `hidden` attribute is added
- URL hash updates if Hash persistence is enabled

### Page Navigation

Each tab is a link to a different page.

```csharp
// /Pages/Guilds/Audio/Index.cshtml.cs
public class AudioModel : PageModel
{
    public IActionResult OnGet(ulong guildId)
    {
        // Determine active tab based on current page
        var activeTab = "soundboard"; // or read from query parameter

        var model = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new NavTabItem
                {
                    Id = "soundboard",
                    Label = "Soundboard",
                    Href = $"/guilds/{guildId}/audio/soundboard"
                },
                new NavTabItem
                {
                    Id = "tts",
                    Label = "Text-to-Speech",
                    Href = $"/guilds/{guildId}/audio/tts"
                },
                new NavTabItem
                {
                    Id = "settings",
                    Label = "Settings",
                    Href = $"/guilds/{guildId}/audio/settings"
                }
            },
            ActiveTabId = activeTab,
            NavigationMode = NavMode.PageNavigation,
            StyleVariant = NavTabStyle.Pills
        };

        return Page();
    }
}
```

```html
<!-- Each page includes the nav tabs with different active tab -->
@model AudioModel

@await Html.PartialAsync("Components/_NavTabs", Model.NavTabs)

<!-- Page-specific content -->
<div class="content">
    <!-- Soundboard content here -->
</div>
```

**URL Pattern:**
```
/guilds/123456/audio/soundboard
/guilds/123456/audio/tts
/guilds/123456/audio/settings
```

### AJAX Navigation

Content is loaded dynamically from API endpoints.

```csharp
// Controller/Page Model
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem
        {
            Id = "overview",
            Label = "Overview",
            Href = "/api/performance/overview"
        },
        new NavTabItem
        {
            Id = "health",
            Label = "Health Metrics",
            Href = "/api/performance/health"
        },
        new NavTabItem
        {
            Id = "metrics",
            Label = "API Metrics",
            Href = "/api/performance/metrics"
        }
    },
    ActiveTabId = "overview",
    NavigationMode = NavMode.Ajax,
    PersistenceMode = NavPersistence.Hash,
    StyleVariant = NavTabStyle.Underline,
    ContainerId = "performanceMetrics"
};

return View(model);
```

```html
<!-- Razor Page -->
@model NavTabsViewModel

@section Styles {
    <link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" />
}

@section Scripts {
    <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
}

@await Html.PartialAsync("Components/_NavTabs", Model)

<!-- Container for AJAX content -->
<div id="performanceMetrics-content" class="mt-6">
    <!-- Content loaded via AJAX will be inserted here -->
</div>
```

**API Endpoint Response:**
```html
<!-- GET /api/performance/overview returns -->
<div class="card">
    <h2>Overview</h2>
    <div class="metric">
        <span class="label">Uptime</span>
        <span class="value">99.9%</span>
    </div>
    <!-- More content -->
</div>
```

**JavaScript Behavior:**
- Clicking a tab triggers an AJAX request to the `Href` endpoint
- Response HTML is inserted into a container (`{ContainerId}-content`)
- Loading spinner is shown during request
- URL hash updates if Hash persistence is enabled

---

## Style Variants

### Underline Variant

Simple underline indicator on the active tab.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "overview", Label = "Overview" },
        new NavTabItem { Id = "members", Label = "Members" }
    },
    StyleVariant = NavTabStyle.Underline
};
```

**Visual Characteristics:**
- Minimal background styling
- Bottom underline on active tab
- Subtle hover effect
- Good for document-style content

**Best Used For:**
- Content pages
- Documentation
- Multi-section forms
- Articles or guides

### Pills Variant

Rounded background on the active tab.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "soundboard", Label = "Soundboard" },
        new NavTabItem { Id = "tts", Label = "Text-to-Speech" }
    },
    StyleVariant = NavTabStyle.Pills
};
```

**Visual Characteristics:**
- Rounded corners on tabs
- Background fill on active tab
- Higher visual prominence
- Good for compact spaces

**Best Used For:**
- Modal dialogs
- Card components
- Sidebar sections
- Compact layouts

### Bordered Variant

Full borders around each tab.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "settings", Label = "General" },
        new NavTabItem { Id = "advanced", Label = "Advanced" }
    },
    StyleVariant = NavTabStyle.Bordered
};
```

**Visual Characteristics:**
- Visible borders on all tabs
- Clear separation between tabs
- Background change on active tab
- Higher visual hierarchy

**Best Used For:**
- Settings pages
- Configuration interfaces
- Clearly separated sections
- Technical content

---

## Advanced Features

### Icons

Add SVG icons to tabs for visual communication.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem
        {
            Id = "overview",
            Label = "Overview",
            // Outline version (inactive state)
            IconPathOutline = "M3 12a9 9 0 110-18 9 9 0 010 18z",
            // Solid version (active state)
            IconPathSolid = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z"
        },
        new NavTabItem
        {
            Id = "settings",
            Label = "Settings",
            IconPathOutline = "M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z",
            IconPathSolid = "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.62l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.48.1.62l2.03 1.58c-.05.3-.07.62-.07.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.62l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.48-.12-.62l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z"
        }
    },
    ActiveTabId = "overview"
};
```

**Icon Guidelines:**
- Use Heroicons-style 24x24 SVG paths
- Provide both outline and solid versions
- Icons are automatically swapped on active state
- Icons are marked with `aria-hidden="true"` for accessibility

### Short Labels for Mobile

Provide abbreviated labels that display on narrow screens.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem
        {
            Id = "health-metrics",
            Label = "Health Metrics",
            ShortLabel = "Health"
        },
        new NavTabItem
        {
            Id = "api-limits",
            Label = "API & Rate Limits",
            ShortLabel = "API"
        }
    }
};
```

**Behavior:**
- Full label shown on desktop
- Short label shown on mobile/narrow screens
- Controlled via CSS classes: `.tab-label-long` and `.tab-label-short`
- Useful for readability on small screens

### Disabled Tabs

Prevent selection of certain tabs.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "overview", Label = "Overview" },
        new NavTabItem
        {
            Id = "advanced",
            Label = "Advanced Settings",
            Disabled = true
        }
    }
};
```

**Behavior:**
- Disabled tabs appear visually dimmed
- Cannot be clicked or selected
- Have `aria-disabled="true"` for accessibility
- Useful for permission-based features or conditional content

### URL Hash Persistence

Maintain tab state in URL for shareable links.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        new NavTabItem { Id = "overview", Label = "Overview" },
        new NavTabItem { Id = "settings", Label = "Settings" }
    },
    ActiveTabId = "overview",
    PersistenceMode = NavPersistence.Hash
};
```

**Behavior:**
- URL becomes: `https://example.com/page#overview`
- Clicking a tab updates the hash
- Page reload preserves selected tab
- Users can share URLs with specific tab selected
- Component detects hash on page load and activates tab

### Auto-Scroll to Active Tab

On load and on activation, the component automatically scrolls tabs into view.

```csharp
var model = new NavTabsViewModel
{
    Tabs = new List<NavTabItem>
    {
        // ... many tabs ...
        new NavTabItem { Id = "tab-50", Label = "Tab 50" }
    },
    ActiveTabId = "tab-50"
};
```

**Behavior:**
- When tab list overflows horizontally, active tab scrolls into view
- Smooth scroll animation
- Prevents active tab from being hidden off-screen
- Works on both page load and tab activation

---

## Accessibility

### ARIA Attributes

The component implements full WAI-ARIA tab pattern:

```html
<!-- Tablist container -->
<nav role="tablist" aria-label="Navigation">
    <!-- Individual tabs -->
    <button
        role="tab"
        aria-selected="true"
        aria-controls="panel-overview"
        tabindex="0"
    >Overview</button>

    <button
        role="tab"
        aria-selected="false"
        aria-controls="panel-settings"
        tabindex="-1"
        disabled
        aria-disabled="true"
    >Settings</button>
</nav>

<!-- Panel containers -->
<div role="tabpanel" id="panel-overview" aria-labelledby="tab-overview">
    <!-- Content -->
</div>

<div role="tabpanel" id="panel-settings" aria-labelledby="tab-settings" hidden>
    <!-- Content -->
</div>
```

### Keyboard Navigation

- **Tab/Shift+Tab**: Navigate to the tab list and between tabs
- **Left/Right Arrows**: Move focus between tabs (with wrapping)
- **Enter/Space**: Activate the focused tab
- **Home**: Jump to first tab
- **End**: Jump to last tab

### Screen Reader Support

- Tab list is announced with `role="tablist"` and `aria-label`
- Individual tabs announced with `role="tab"` and `aria-selected`
- Active state is conveyed through `aria-selected="true"`
- Disabled tabs have `aria-disabled="true"`
- Icons are hidden from screen readers with `aria-hidden="true"`
- Content panels are linked to tabs via `aria-controls`

### Testing Accessibility

```csharp
// Page model test
[Fact]
public void NavTabs_ShouldHaveProperAriaAttributes()
{
    var model = new NavTabsViewModel
    {
        Tabs = new List<NavTabItem>
        {
            new NavTabItem { Id = "tab1", Label = "Tab 1" }
        },
        ActiveTabId = "tab1",
        AriaLabel = "Test Navigation"
    };

    // Verify aria-label is set
    Assert.Equal("Test Navigation", model.AriaLabel);
}
```

---

## Examples

### Guild Settings Tabs

```csharp
// GuildSettingsModel.cs
public class GuildSettingsModel : PageModel
{
    public NavTabsViewModel NavigationTabs { get; set; }

    public IActionResult OnGet(ulong guildId, string tab = "general")
    {
        NavigationTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new NavTabItem
                {
                    Id = "general",
                    Label = "General Settings",
                    Href = $"/guilds/{guildId}/settings?tab=general"
                },
                new NavTabItem
                {
                    Id = "roles",
                    Label = "Roles & Permissions",
                    Href = $"/guilds/{guildId}/settings?tab=roles"
                },
                new NavTabItem
                {
                    Id = "moderation",
                    Label = "Moderation",
                    Href = $"/guilds/{guildId}/settings?tab=moderation"
                },
                new NavTabItem
                {
                    Id = "logging",
                    Label = "Logging",
                    Href = $"/guilds/{guildId}/settings?tab=logging"
                }
            },
            ActiveTabId = tab,
            NavigationMode = NavMode.PageNavigation,
            StyleVariant = NavTabStyle.Bordered,
            ContainerId = "guildSettings"
        };

        return Page();
    }
}
```

### Performance Dashboard (AJAX)

```csharp
// PerformanceModel.cs
public class PerformanceModel : PageModel
{
    public NavTabsViewModel MetricsTabs { get; set; }

    public IActionResult OnGet()
    {
        MetricsTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new NavTabItem
                {
                    Id = "overview",
                    Label = "Overview",
                    Href = "/api/performance/overview",
                    IconPathOutline = "M3 12a9 9 0 110-18 9 9 0 010 18z",
                    IconPathSolid = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z"
                },
                new NavTabItem
                {
                    Id = "health",
                    Label = "Health Metrics",
                    Href = "/api/performance/health",
                    ShortLabel = "Health",
                    IconPathOutline = "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z",
                    IconPathSolid = "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                },
                new NavTabItem
                {
                    Id = "commands",
                    Label = "Commands",
                    Href = "/api/performance/commands",
                    IconPathOutline = "M13 10V3L4 14h7v7l9-11h-7z"
                }
            },
            ActiveTabId = "overview",
            NavigationMode = NavMode.Ajax,
            PersistenceMode = NavPersistence.Hash,
            StyleVariant = NavTabStyle.Pills,
            ContainerId = "performanceMetrics",
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

<div class="container max-w-7xl">
    <h1>Performance Dashboard</h1>

    @await Html.PartialAsync("Components/_NavTabs", Model.MetricsTabs)

    <div id="performanceMetrics-content" class="mt-6 min-h-96">
        <!-- AJAX content loaded here -->
    </div>
</div>
```

### Audio Features (In-Page with Icons)

```csharp
// AudioModel.cs
public class AudioModel : PageModel
{
    public NavTabsViewModel AudioTabs { get; set; }

    public IActionResult OnGet(ulong guildId)
    {
        AudioTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new NavTabItem
                {
                    Id = "soundboard",
                    Label = "Soundboard",
                    ShortLabel = "Sounds",
                    IconPathOutline = "M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3",
                    IconPathSolid = "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"
                },
                new NavTabItem
                {
                    Id = "tts",
                    Label = "Text-to-Speech",
                    IconPathOutline = "M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"
                },
                new NavTabItem
                {
                    Id = "settings",
                    Label = "Settings",
                    IconPathOutline = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                }
            },
            ActiveTabId = "soundboard",
            NavigationMode = NavMode.InPage,
            PersistenceMode = NavPersistence.Hash,
            StyleVariant = NavTabStyle.Pills,
            ContainerId = "audioFeatures",
            AriaLabel = "Audio feature sections"
        };

        return Page();
    }
}
```

```html
<!-- Audio.cshtml -->
@model AudioModel

@await Html.PartialAsync("Components/_NavTabs", Model.AudioTabs)

<!-- Soundboard Tab Content -->
<div data-nav-panel-for="audioFeatures" data-tab-id="soundboard">
    <div class="card">
        <h2>Soundboard</h2>
        <!-- Soundboard content with sounds list -->
    </div>
</div>

<!-- TTS Tab Content -->
<div data-nav-panel-for="audioFeatures" data-tab-id="tts" hidden>
    <div class="card">
        <h2>Text-to-Speech</h2>
        <!-- TTS configuration -->
    </div>
</div>

<!-- Settings Tab Content -->
<div data-nav-panel-for="audioFeatures" data-tab-id="settings" hidden>
    <div class="card">
        <h2>Audio Settings</h2>
        <!-- Audio settings form -->
    </div>
</div>
```

---

## Best Practices

1. **Choose the Right Mode**
   - In-Page: For tightly related content that changes frequently
   - Page Navigation: For independent sections with separate concerns
   - AJAX: For heavy content or when you want URL cleanliness with persistence

2. **Keep Tab Labels Short**
   - Aim for 1-2 words
   - Provide short labels for mobile: "API & Rate Limits" → "API"
   - Avoid abbreviations unless universally understood

3. **Icon Consistency**
   - Use Heroicons or similar consistent icon set
   - Always provide both outline and solid versions
   - Icons should reinforce the label, not replace it

4. **Navigation Clarity**
   - Disable tabs when their content is unavailable
   - Don't hide important features behind disabled tabs
   - Provide clear indication of why a tab is disabled

5. **Performance**
   - For AJAX mode, cache responses when appropriate
   - Lazy-load heavy content on-demand
   - Show loading indicators during AJAX requests

6. **Accessibility**
   - Always set a meaningful `AriaLabel`
   - Test with keyboard navigation
   - Verify with screen readers
   - Ensure sufficient color contrast

7. **Mobile Considerations**
   - Always provide short labels for small screens
   - Test horizontal scrolling on actual devices
   - Ensure tap targets are at least 44x44 pixels
   - Consider hamburger menu alternative for many tabs

---

## Troubleshooting

### Tabs Not Responding to Clicks

**Problem:** Tabs appear but don't switch content
**Solution:** Ensure CSS and JavaScript files are loaded:
```html
@section Styles {
    <link rel="stylesheet" href="~/css/nav-tabs.css" asp-append-version="true" />
}

@section Scripts {
    <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
}
```

### Content Panels Not Hiding/Showing

**Problem:** All panels remain visible or first panel doesn't show
**Solution:** Check panel attributes match component ID:
```html
<!-- Must match ContainerId -->
<div data-nav-panel-for="audioFeatures" data-tab-id="soundboard">
    <!-- Content -->
</div>
```

### AJAX Content Not Loading

**Problem:** AJAX requests fail or content doesn't appear
**Solution:** Verify API endpoint returns valid HTML and check browser console for errors

### Hash Not Updating

**Problem:** URL hash doesn't change when tabs are clicked
**Solution:** Ensure `PersistenceMode` is set to `Hash`:
```csharp
PersistenceMode = NavPersistence.Hash
```

---

## Related Documentation

- [Design System - Navigation Tabs](design-system.md#navigation-tabs)
- [Navigation Tabs Migration Guide](nav-tabs-migration.md)
- [Navigation Tabs JavaScript API](nav-tabs-api.md)
- [Interactive Components](interactive-components.md)
- [Form Implementation Standards](form-implementation-standards.md)

## Files

- **ViewModel:** `src/DiscordBot.Bot/ViewModels/Components/NavTabsViewModel.cs`
- **Partial:** `src/DiscordBot.Bot/Pages/Shared/Components/_NavTabs.cshtml`
- **Styles:** `src/DiscordBot.Bot/wwwroot/css/nav-tabs.css`
- **Scripts:** `src/DiscordBot.Bot/wwwroot/js/nav-tabs.js`

---

## Changelog

### Version 1.0 (2026-01-26)
- Initial release of Navigation Tabs Component Guide
- Documented all style variants (Underline, Pills, Bordered)
- Documented all navigation modes (In-Page, Page Navigation, AJAX)
- Documented persistence modes (None, Hash, LocalStorage)
- Provided comprehensive usage examples
- Documented accessibility features and keyboard navigation
- Created troubleshooting guide
- Related to Issue #1253: Feature: Navigation Component Documentation and Usage Guide

