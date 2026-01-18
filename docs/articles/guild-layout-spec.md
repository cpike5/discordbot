# Guild Layout Specification

This document provides the design specification for the standardized guild page layout system, including the shared layout, header component, navigation bar, and breadcrumbs.

## Implementation Status

| Attribute | Value |
|-----------|-------|
| Status | Complete |
| Completed | January 2026 |
| Epic Issue | #1189 |
| Pages Migrated | 20+ |

All guild-related pages now use the standardized layout system including breadcrumbs, headers, and navigation tabs.

## Overview

All guild-related pages will use a consistent layout structure that includes:

1. **Standardized Breadcrumbs** - Consistent navigation path showing page hierarchy
2. **Guild Header** - Guild icon, page title, description, and action buttons
3. **Guild Navigation Bar** - Horizontal tab navigation for all guild sections
4. **Content Area** - Page-specific content

## Component Structure

### 1. Shared Guild Layout (`_GuildLayout.cshtml`)

The shared layout wraps all guild pages and provides the common structure.

**Location:** `Pages/Shared/_GuildLayout.cshtml`

**Structure:**
```razor
@{
    Layout = "_Layout.cshtml";
}

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Breadcrumb Navigation -->
    @await Html.PartialAsync("Shared/Components/_GuildBreadcrumb", Model.Breadcrumb)

    <!-- Guild Header -->
    @await Html.PartialAsync("Shared/Components/_GuildHeader", Model.Header)

    <!-- Guild Navigation Bar -->
    @await Html.PartialAsync("Shared/Components/_GuildNavBar", Model.Navigation)

    <!-- Page Content -->
    <div class="guild-page-content">
        @RenderBody()
    </div>
</div>
```

**Required ViewModel Properties:**
- `Breadcrumb` (GuildBreadcrumbViewModel)
- `Header` (GuildHeaderViewModel)
- `Navigation` (GuildNavBarViewModel)

---

### 2. Guild Breadcrumb Component (`_GuildBreadcrumb.cshtml`)

Displays consistent breadcrumb navigation across all guild pages.

**Location:** `Pages/Shared/Components/_GuildBreadcrumb.cshtml`

**ViewModel (`GuildBreadcrumbViewModel`):**
```csharp
public class GuildBreadcrumbViewModel
{
    public List<BreadcrumbItem> Items { get; set; } = new();
}

public class BreadcrumbItem
{
    public string Label { get; set; }
    public string? Url { get; set; }  // Null for current page
    public bool IsCurrent { get; set; }
}
```

**Markup Pattern:**
```html
<nav aria-label="Breadcrumb" class="mb-6">
    <ol class="flex items-center gap-2 text-sm">
        <li>
            <a href="/" class="text-text-secondary hover:text-accent-blue transition-colors">Home</a>
        </li>
        <li class="text-text-tertiary">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
            </svg>
        </li>
        <li>
            <a href="/Guilds" class="text-text-secondary hover:text-accent-blue transition-colors">Servers</a>
        </li>
        <!-- ... more items ... -->
        <li>
            <span class="text-text-primary font-medium">Current Page</span>
        </li>
    </ol>
</nav>
```

**Tailwind Classes:**
- Container: `mb-6`
- List: `flex items-center gap-2 text-sm`
- Link: `text-text-secondary hover:text-accent-blue transition-colors`
- Separator: `text-text-tertiary`
- Current page: `text-text-primary font-medium`
- Chevron icon: `w-4 h-4`

**Breadcrumb Patterns:**

| Page Type | Pattern |
|-----------|---------|
| Guild Overview | Home > Servers > [Guild Name] |
| Guild Settings | Home > Servers > [Guild Name] > [Section Name] |
| Sub-pages | Home > Servers > [Guild Name] > [Parent] > [Current Page] |

---

### 3. Guild Header Component (`_GuildHeader.cshtml`)

Displays guild icon, page title, description, and action buttons.

**Location:** `Pages/Shared/Components/_GuildHeader.cshtml`

**ViewModel (`GuildHeaderViewModel`):**
```csharp
public class GuildHeaderViewModel
{
    public ulong GuildId { get; set; }
    public string GuildName { get; set; }
    public string? GuildIconUrl { get; set; }
    public string PageTitle { get; set; }
    public string? PageDescription { get; set; }
    public List<HeaderAction>? Actions { get; set; }
}

public class HeaderAction
{
    public string Label { get; set; }
    public string Url { get; set; }
    public string? Icon { get; set; }  // SVG path or CSS class
    public HeaderActionStyle Style { get; set; } = HeaderActionStyle.Secondary;
    public bool OpenInNewTab { get; set; }
}

public enum HeaderActionStyle
{
    Primary,    // Orange button
    Secondary,  // Border button
    Link        // Text link
}
```

**Markup Pattern:**
```html
<div class="mb-8">
    <div class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
        <!-- Left: Icon + Title + Description -->
        <div class="flex items-center gap-4">
            <!-- Guild Icon -->
            @if (!string.IsNullOrEmpty(Model.GuildIconUrl))
            {
                <img src="@Model.GuildIconUrl"
                     alt="@Model.GuildName"
                     class="w-12 h-12 rounded-full flex-shrink-0" />
            }
            else
            {
                <div class="w-12 h-12 rounded-full bg-gradient-to-br from-accent-blue to-accent-orange flex items-center justify-center text-white font-bold text-lg flex-shrink-0">
                    @(Model.GuildName[..2].ToUpper())
                </div>
            }

            <!-- Title + Description -->
            <div>
                <h1 class="text-2xl font-bold text-text-primary">@Model.PageTitle</h1>
                @if (!string.IsNullOrEmpty(Model.PageDescription))
                {
                    <p class="mt-1 text-sm text-text-secondary">@Model.PageDescription</p>
                }
            </div>
        </div>

        <!-- Right: Action Buttons -->
        @if (Model.Actions?.Any() == true)
        {
            <div class="flex items-center gap-2">
                @foreach (var action in Model.Actions)
                {
                    <!-- Render button/link based on action.Style -->
                }
            </div>
        }
    </div>
</div>
```

**Tailwind Classes:**

**Container:**
- Outer: `mb-8`
- Flex container: `flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4`

**Icon:**
- With image: `w-12 h-12 rounded-full flex-shrink-0`
- Without image (initials): `w-12 h-12 rounded-full bg-gradient-to-br from-accent-blue to-accent-orange flex items-center justify-center text-white font-bold text-lg flex-shrink-0`

**Title & Description:**
- Title: `text-2xl font-bold text-text-primary`
- Description: `mt-1 text-sm text-text-secondary`

**Action Buttons:**
- Primary: `px-4 py-2 bg-accent-orange text-white font-medium text-sm rounded-lg hover:bg-accent-orange-hover transition-colors inline-flex items-center gap-2`
- Secondary: `px-3 py-2 text-sm font-medium text-text-secondary hover:text-accent-blue bg-bg-secondary border border-border-primary rounded-lg hover:border-border-focus transition-colors inline-flex items-center gap-2`
- Link: `text-sm font-medium text-accent-blue hover:text-accent-blue-hover transition-colors inline-flex items-center gap-1`

**Responsive Behavior:**
- Mobile: Stack header vertically, full-width action buttons
- Tablet+: Horizontal layout with actions right-aligned

---

### 4. Guild Navigation Bar Component (`_GuildNavBar.cshtml`)

Horizontal tab navigation for all guild sections.

**Location:** `Pages/Shared/Components/_GuildNavBar.cshtml`

**ViewModel (`GuildNavBarViewModel`):**
```csharp
public class GuildNavBarViewModel
{
    public ulong GuildId { get; set; }
    public string ActiveTab { get; set; }
    public List<GuildNavItem> Tabs { get; set; } = new();
}

public class GuildNavItem
{
    public string Id { get; set; }           // Unique identifier (e.g., "overview")
    public string Label { get; set; }        // Display text (e.g., "Overview")
    public string PageName { get; set; }     // Razor page name
    public string? Area { get; set; }        // Optional area name
    public string? IconOutline { get; set; } // SVG path for outline icon
    public string? IconSolid { get; set; }   // SVG path for solid icon (active state)
    public int Order { get; set; }           // Display order
}
```

**Navigation Configuration (Static Class):**
```csharp
public static class GuildNavigationConfig
{
    public static IReadOnlyList<GuildNavItem> GetTabs()
    {
        return new List<GuildNavItem>
        {
            new() { Id = "overview", Label = "Overview", PageName = "Details", Area = "Guilds", Order = 1,
                    IconOutline = "M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6",
                    IconSolid = "M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L4 10.414V17a1 1 0 001 1h2a1 1 0 001-1v-2a1 1 0 011-1h2a1 1 0 011 1v2a1 1 0 001 1h2a1 1 0 001-1v-6.586l.293.293a1 1 0 001.414-1.414l-7-7z" },
            new() { Id = "members", Label = "Members", PageName = "Index", Area = "Members", Order = 2,
                    IconOutline = "M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z",
                    IconSolid = "M9 6a3 3 0 11-6 0 3 3 0 016 0zM17 6a3 3 0 11-6 0 3 3 0 016 0zM12.93 17c.046-.327.07-.66.07-1a6.97 6.97 0 00-1.5-4.33A5 5 0 0119 16v1h-6.07zM6 11a5 5 0 015 5v1H1v-1a5 5 0 015-5z" },
            new() { Id = "moderation", Label = "Moderation", PageName = "ModerationSettings", Area = "Guilds", Order = 3,
                    IconOutline = "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z",
                    IconSolid = "M9 6a3 3 0 11-6 0 3 3 0 016 0zM17 6a3 3 0 11-6 0 3 3 0 016 0zM12.93 17c.046-.327.07-.66.07-1a6.97 6.97 0 00-1.5-4.33A5 5 0 0119 16v1h-6.07zM6 11a5 5 0 015 5v1H1v-1a5 5 0 015-5z" },
            new() { Id = "messages", Label = "Messages", PageName = "ScheduledMessages", Area = "Guilds", Order = 4,
                    IconOutline = "M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z",
                    IconSolid = "M2 5a2 2 0 012-2h7a2 2 0 012 2v4a2 2 0 01-2 2H9l-3 3v-3H4a2 2 0 01-2-2V5zm15 0a2 2 0 012 2v4a2 2 0 01-2 2h-1v3l-3-3h-2a2 2 0 01-2-2V7a2 2 0 012-2h6z" },
            new() { Id = "audio", Label = "Audio", PageName = "Soundboard", Area = "Guilds", Order = 5,
                    IconOutline = "M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z",
                    IconSolid = "M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" },
            new() { Id = "ratwatch", Label = "Rat Watch", PageName = "RatWatch", Area = "Guilds", Order = 6,
                    IconOutline = "M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z",
                    IconSolid = "M10 12a2 2 0 100-4 2 2 0 000 4z M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10z" },
            new() { Id = "reminders", Label = "Reminders", PageName = "Reminders", Area = "Guilds", Order = 7,
                    IconOutline = "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9",
                    IconSolid = "M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6zM10 18a3 3 0 01-3-3h6a3 3 0 01-3 3z" },
            new() { Id = "welcome", Label = "Welcome", PageName = "Welcome", Area = "Guilds", Order = 8,
                    IconOutline = "M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z",
                    IconSolid = "M8 9a3 3 0 100-6 3 3 0 000 6zM8 11a6 6 0 016 6H2a6 6 0 016-6zM16 7a1 1 0 10-2 0v1h-1a1 1 0 100 2h1v1a1 1 0 102 0v-1h1a1 1 0 100-2h-1V7z" },
            new() { Id = "assistant", Label = "Assistant", PageName = "AssistantSettings", Area = "Guilds", Order = 9,
                    IconOutline = "M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z",
                    IconSolid = "M11 3a1 1 0 10-2 0v1a1 1 0 102 0V3zM15.657 5.757a1 1 0 00-1.414-1.414l-.707.707a1 1 0 001.414 1.414l.707-.707zM18 10a1 1 0 01-1 1h-1a1 1 0 110-2h1a1 1 0 011 1zM5.05 6.464A1 1 0 106.464 5.05l-.707-.707a1 1 0 00-1.414 1.414l.707.707zM5 10a1 1 0 01-1 1H3a1 1 0 110-2h1a1 1 0 011 1zM8 16v-1h4v1a2 2 0 11-4 0zM12 14c.015-.34.208-.646.477-.859a4 4 0 10-4.954 0c.27.213.462.519.476.859h4.002z" }
        };
    }
}
```

**Markup Pattern:**
```html
<div class="guild-nav-container mb-6" id="guildNavContainer">
    <!-- Desktop: Horizontal tabs -->
    <nav class="guild-nav-tabs hidden sm:flex" id="guildNavTabs" role="tablist" aria-label="Guild sections">
        @foreach (var tab in Model.Tabs.OrderBy(t => t.Order))
        {
            var isActive = Model.ActiveTab == tab.Id;
            <a asp-page="/Guilds/@tab.PageName"
               asp-route-guildId="@Model.GuildId"
               class="guild-nav-tab @(isActive ? "active" : "")"
               role="tab"
               aria-selected="@(isActive ? "true" : "false")">
                @if (isActive && !string.IsNullOrEmpty(tab.IconSolid))
                {
                    <svg class="tab-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="@tab.IconSolid" />
                    </svg>
                }
                else if (!string.IsNullOrEmpty(tab.IconOutline))
                {
                    <svg class="tab-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="@tab.IconOutline" />
                    </svg>
                }
                <span>@tab.Label</span>
            </a>
        }
    </nav>

    <!-- Mobile: Dropdown -->
    <div class="guild-nav-dropdown sm:hidden">
        <button type="button"
                id="guildNavDropdownToggle"
                class="guild-nav-dropdown-button"
                aria-haspopup="listbox"
                aria-expanded="false">
            <span id="guildNavSelectedText">@Model.Tabs.First(t => t.Id == Model.ActiveTab).Label</span>
            <svg class="w-4 h-4 text-text-tertiary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
            </svg>
        </button>
        <div id="guildNavDropdownMenu"
             class="hidden guild-nav-dropdown-menu"
             role="listbox">
            @foreach (var tab in Model.Tabs.OrderBy(t => t.Order))
            {
                var isActive = Model.ActiveTab == tab.Id;
                <a asp-page="/Guilds/@tab.PageName"
                   asp-route-guildId="@Model.GuildId"
                   class="guild-nav-dropdown-item @(isActive ? "active" : "")"
                   role="option">
                    @tab.Label
                </a>
            }
        </div>
    </div>
</div>
```

**CSS Styles:**

```css
/* Guild Navigation Tabs (Desktop) */
.guild-nav-container {
    position: relative;
}

.guild-nav-tabs {
    display: flex;
    gap: 0.25rem;
    padding: 0.25rem;
    background: var(--color-bg-secondary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.75rem;
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
}

.guild-nav-tabs::-webkit-scrollbar {
    display: none;
}

.guild-nav-tab {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.625rem 1rem;
    font-size: 0.875rem;
    font-weight: 500;
    color: var(--color-text-secondary);
    background: transparent;
    border-radius: 0.5rem;
    text-decoration: none;
    white-space: nowrap;
    transition: all 0.15s ease-out;
}

.guild-nav-tab:hover {
    color: var(--color-text-primary);
    background: var(--color-bg-hover);
}

.guild-nav-tab.active {
    color: var(--color-text-primary);
    background: var(--color-bg-tertiary);
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.guild-nav-tab .tab-icon {
    width: 1.125rem;
    height: 1.125rem;
    flex-shrink: 0;
}

.guild-nav-tab.active .tab-icon {
    color: var(--color-accent-blue);
}

/* Guild Navigation Dropdown (Mobile) */
.guild-nav-dropdown {
    position: relative;
}

.guild-nav-dropdown-button {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    padding: 0.75rem 1rem;
    font-size: 0.875rem;
    font-weight: 500;
    color: var(--color-text-primary);
    background: var(--color-bg-secondary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.75rem;
    transition: all 0.15s ease-out;
}

.guild-nav-dropdown-button:hover {
    border-color: var(--color-border-focus);
}

.guild-nav-dropdown-menu {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    margin-top: 0.5rem;
    background: var(--color-bg-tertiary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.75rem;
    box-shadow: var(--shadow-lg);
    overflow: hidden;
    z-index: var(--z-dropdown);
}

.guild-nav-dropdown-item {
    display: block;
    padding: 0.75rem 1rem;
    font-size: 0.875rem;
    color: var(--color-text-primary);
    text-decoration: none;
    transition: background 0.15s ease-out;
}

.guild-nav-dropdown-item:hover {
    background: var(--color-bg-hover);
}

.guild-nav-dropdown-item.active {
    background: var(--color-bg-hover);
    font-weight: 600;
}

/* Mobile responsive */
@media (max-width: 640px) {
    .guild-nav-tab {
        padding: 0.5rem 0.875rem;
        font-size: 0.8125rem;
    }

    .guild-nav-tab .tab-icon {
        width: 1rem;
        height: 1rem;
    }
}
```

**JavaScript (Dropdown Toggle):**
```javascript
document.addEventListener('DOMContentLoaded', function() {
    const dropdownToggle = document.getElementById('guildNavDropdownToggle');
    const dropdownMenu = document.getElementById('guildNavDropdownMenu');

    if (dropdownToggle && dropdownMenu) {
        dropdownToggle.addEventListener('click', function(e) {
            e.stopPropagation();
            const isHidden = dropdownMenu.classList.toggle('hidden');
            dropdownToggle.setAttribute('aria-expanded', !isHidden);
        });

        // Close on click outside
        document.addEventListener('click', function(e) {
            if (!e.target.closest('.guild-nav-dropdown')) {
                dropdownMenu.classList.add('hidden');
                dropdownToggle.setAttribute('aria-expanded', 'false');
            }
        });
    }
});
```

---

## Usage Examples

### Example 1: Guild Overview Page

```csharp
public class DetailsModel : PageModel
{
    public GuildBreadcrumbViewModel Breadcrumb { get; set; }
    public GuildHeaderViewModel Header { get; set; }
    public GuildNavBarViewModel Navigation { get; set; }

    public async Task<IActionResult> OnGetAsync(ulong id)
    {
        var guild = await _guildService.GetGuildAsync(id);

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = guild.Name,
            PageDescription = "Overview and settings for this server",
            Actions = new List<HeaderAction>
            {
                new() { Label = "Edit Settings", Url = $"/Guilds/Edit/{guild.Id}", Style = HeaderActionStyle.Primary },
                new() { Label = "Sync Guild", Url = $"/Guilds/Sync/{guild.Id}", Style = HeaderActionStyle.Secondary }
            }
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "overview",
            Tabs = GuildNavigationConfig.GetTabs()
        };

        return Page();
    }
}
```

### Example 2: Member Directory Page

```csharp
public class IndexModel : PageModel
{
    public GuildBreadcrumbViewModel Breadcrumb { get; set; }
    public GuildHeaderViewModel Header { get; set; }
    public GuildNavBarViewModel Navigation { get; set; }

    public async Task<IActionResult> OnGetAsync(ulong guildId)
    {
        var guild = await _guildService.GetGuildAsync(guildId);

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details?id={guild.Id}" },
                new() { Label = "Members", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = "Members",
            PageDescription = $"Browse and manage members for {guild.Name}",
            Actions = new List<HeaderAction>
            {
                new() { Label = "Export CSV", Url = $"/api/guilds/{guild.Id}/members/export", Style = HeaderActionStyle.Secondary }
            }
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "members",
            Tabs = GuildNavigationConfig.GetTabs()
        };

        return Page();
    }
}
```

---

## Migration Strategy

### Phase 1: Create Infrastructure
1. Create ViewModels in `ViewModels/Components/`
2. Create partial views in `Pages/Shared/Components/`
3. Create `GuildNavigationConfig` static class
4. Create shared layout `_GuildLayout.cshtml`

### Phase 2: Migrate Pages (Order by Priority)
1. Guild Overview (Details)
2. Welcome Settings
3. Audio/Soundboard
4. Member Directory
5. Moderation Settings
6. Scheduled Messages
7. Rat Watch
8. Reminders
9. Assistant Settings
10. Sub-pages (Analytics, Incidents, etc.)

### Phase 3: Testing & Refinement
1. Test responsive behavior on mobile/tablet
2. Verify accessibility (keyboard navigation, ARIA labels)
3. Check navigation consistency across all pages
4. Ensure action buttons work correctly

---

## Accessibility Requirements

### ARIA Labels
- Breadcrumb: `aria-label="Breadcrumb"`
- Navigation tabs: `role="tablist"`, `aria-label="Guild sections"`
- Individual tabs: `role="tab"`, `aria-selected="true|false"`
- Dropdown: `aria-haspopup="listbox"`, `aria-expanded="true|false"`

### Keyboard Navigation
- Tab through breadcrumb links
- Tab through navigation items
- Enter/Space to activate tabs
- Escape to close dropdown on mobile

### Focus States
- All interactive elements must have visible focus states
- Use `focus:outline-none focus:ring-2 focus:ring-border-focus`

---

## Design Tokens Reference

### Colors
- **Background:** `bg-bg-primary`, `bg-bg-secondary`, `bg-bg-tertiary`, `bg-bg-hover`
- **Text:** `text-text-primary`, `text-text-secondary`, `text-text-tertiary`
- **Accent:** `text-accent-blue`, `text-accent-orange`, `bg-accent-orange`
- **Border:** `border-border-primary`, `border-border-secondary`, `border-border-focus`

### Spacing
- **Gap:** `gap-2` (8px), `gap-4` (16px)
- **Padding:** `p-4` (16px), `px-6 py-4` (24px/16px)
- **Margin:** `mb-6` (24px), `mb-8` (32px)

### Typography
- **Page title:** `text-2xl font-bold text-text-primary`
- **Description:** `text-sm text-text-secondary`
- **Nav tabs:** `text-sm font-medium`

### Border Radius
- **Large containers:** `rounded-lg` (8px), `rounded-xl` (12px)
- **Small elements:** `rounded-md` (6px)
- **Circular:** `rounded-full`

---

## Notes

- **Discord Snowflake IDs:** Always pass `GuildId` as string in JavaScript to avoid precision loss
- **Icon Library:** Use Hero Icons (https://heroicons.com) for consistency
- **Responsive Breakpoints:** `sm` (640px), `md` (768px), `lg` (1024px)
- **Tab Order:** Configurable in `GuildNavigationConfig` - easy to reorder without touching markup
- **No Count Badges:** Keep navigation clean (except Audio tabs which already have them)
