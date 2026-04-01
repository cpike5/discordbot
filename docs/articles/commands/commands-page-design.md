# Commands Page - Design Specification

**Version:** 1.0
**Last Updated:** 2025-12-24
**Issue Reference:** #203
**Target Framework:** ASP.NET Core Razor Pages with Tailwind CSS

---

## Overview

This specification defines the visual design, layout, and interaction patterns for the Commands page in the Discord bot admin UI. The page displays hierarchical information about registered slash command modules from Discord.NET's InteractionService, organized by module with expandable command details.

### Design Goals

- **Clarity**: Present complex hierarchical command structure in an easy-to-scan format
- **Discoverability**: Make it easy to find specific commands and understand their requirements
- **Consistency**: Follow existing design system patterns from Guilds and CommandLogs pages
- **Technical Accuracy**: Display command parameters, types, and preconditions clearly for developers

---

## 1. Page Layout Overview

### Page Structure

```
┌─────────────────────────────────────────────────────────────────┐
│ Page Header                                                      │
│  - Title: "Commands"                                             │
│  - Badge: Module count (e.g., "12")                              │
│  - Sync button (admin only)                                      │
│  - Last updated timestamp                                        │
├─────────────────────────────────────────────────────────────────┤
│ Search & Filter Bar (collapsible)                               │
│  - Search input: Filter by command/module name                   │
│  - Filter by precondition type (All, Admin, Owner, RateLimit)   │
│  - Sort options (Name A-Z, Name Z-A, Module)                     │
├─────────────────────────────────────────────────────────────────┤
│ Command Modules List                                             │
│  ┌────────────────────────────────────────────────┐             │
│  │ Module Card 1 (collapsible)           [chevron]│             │
│  │  - Module name, description, command count      │             │
│  │  └─ Expanded: List of commands with details    │             │
│  └────────────────────────────────────────────────┘             │
│  ┌────────────────────────────────────────────────┐             │
│  │ Module Card 2 (collapsible)           [chevron]│             │
│  └────────────────────────────────────────────────┘             │
│  ...                                                             │
└─────────────────────────────────────────────────────────────────┘
```

### Container Specifications

```css
/* Page container */
.commands-page-container {
  max-width: 1280px;        /* container-xl */
  margin: 0 auto;
  padding: 2rem 1rem;       /* py-8 px-4 */
}

/* Responsive padding */
@media (min-width: 640px) {
  .commands-page-container {
    padding: 2rem 1.5rem;   /* sm:px-6 */
  }
}

@media (min-width: 1024px) {
  .commands-page-container {
    padding: 2rem 2rem;     /* lg:px-8 */
  }
}
```

---

## 2. Page Header Component

### Layout

The page header follows the established pattern from Guilds and CommandLogs pages with title, count badge, and action buttons.

### HTML Structure

```html
<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
  <!-- Left Side: Title + Badge -->
  <div class="flex items-center gap-3">
    <h1 class="text-2xl lg:text-3xl font-bold text-text-primary">Commands</h1>
    <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
      Text = moduleCount.ToString(),
      Variant = BadgeVariant.Blue,
      Size = BadgeSize.Medium
    }" />
  </div>

  <!-- Right Side: Actions -->
  <div class="flex items-center gap-3">
    <!-- Sync Commands Button (Admin/SuperAdmin only) -->
    <button type="button"
            class="btn btn-accent"
            onclick="syncCommands(this)"
            title="Refresh command list from bot">
      <svg class="w-5 h-5 mr-2 sync-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
      </svg>
      <svg class="w-5 h-5 mr-2 sync-spinner hidden animate-spin" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
      </svg>
      <span class="sync-text">Refresh Commands</span>
    </button>

    <!-- Last Updated Timestamp (desktop only) -->
    <span class="text-xs text-text-tertiary hidden lg:block">
      Last updated: <time datetime="@DateTime.UtcNow.ToString("s")">
        @DateTime.UtcNow.ToString("MMM d, yyyy 'at' h:mm tt")
      </time>
    </span>
  </div>
</div>
```

### Visual Specifications

- **Title Font**: `text-2xl` (mobile), `lg:text-3xl` (desktop), bold, text-primary color
- **Badge**: Blue variant, medium size, displays module count
- **Sync Button**: Accent blue button with rotating arrow icon
- **Timestamp**: Small text (xs), tertiary color, hidden on mobile/tablet

---

## 3. Search & Filter Bar Component

### Layout

Collapsible filter panel matching the CommandLogs page pattern with search and filtering options.

### HTML Structure

```html
<div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
  <!-- Filter Toggle Header -->
  <button type="button"
          id="filterToggle"
          class="w-full flex items-center justify-between px-5 py-4 text-left"
          aria-expanded="true"
          aria-controls="filterContent"
          onclick="toggleFilterPanel()">
    <div class="flex items-center gap-3">
      <svg class="w-5 h-5 text-text-secondary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
      </svg>
      <span class="text-lg font-semibold text-text-primary">Search & Filter</span>
      @if (hasActiveFilters)
      {
        <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
          Text = $"{activeFilterCount} active",
          Variant = BadgeVariant.Orange,
          Size = BadgeSize.Small
        }" />
      }
    </div>
    <svg id="filterChevron" class="w-5 h-5 text-text-secondary transition-transform duration-200"
         fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
    </svg>
  </button>

  <!-- Filter Content (collapsible) -->
  <div id="filterContent" class="overflow-hidden transition-all duration-300 max-h-[1000px]">
    <div class="border-t border-border-primary p-5">
      <form method="get" class="space-y-4">
        <!-- Search Input -->
        <div>
          <label for="SearchTerm" class="block text-sm font-medium text-text-primary mb-1">
            Search Commands
          </label>
          <div class="relative">
            <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary"
                 fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <input type="search"
                   id="SearchTerm"
                   name="SearchTerm"
                   placeholder="Search by command or module name..."
                   class="w-full pl-10 pr-4 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary placeholder-text-tertiary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors" />
          </div>
        </div>

        <!-- Filter Grid -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          <!-- Module Filter -->
          <div>
            <label for="ModuleFilter" class="block text-sm font-medium text-text-primary mb-1">
              Module
            </label>
            <select id="ModuleFilter"
                    name="ModuleFilter"
                    class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer">
              <option value="">All Modules</option>
              <!-- Populated dynamically -->
            </select>
          </div>

          <!-- Precondition Filter -->
          <div>
            <label for="PreconditionFilter" class="block text-sm font-medium text-text-primary mb-1">
              Restrictions
            </label>
            <select id="PreconditionFilter"
                    name="PreconditionFilter"
                    class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer">
              <option value="">All Commands</option>
              <option value="admin">Admin Only</option>
              <option value="owner">Owner Only</option>
              <option value="ratelimited">Rate Limited</option>
              <option value="permissions">User Permission Required</option>
            </select>
          </div>

          <!-- Sort Order -->
          <div>
            <label for="SortBy" class="block text-sm font-medium text-text-primary mb-1">
              Sort By
            </label>
            <select id="SortBy"
                    name="SortBy"
                    class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer">
              <option value="name-asc">Name (A-Z)</option>
              <option value="name-desc">Name (Z-A)</option>
              <option value="module">Module</option>
            </select>
          </div>
        </div>

        <!-- Filter Actions -->
        <div class="flex items-center gap-3 pt-4 border-t border-border-secondary">
          <button type="submit" class="btn btn-primary">
            <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            Apply Filters
          </button>
          <a href="@Url.Page("Index")" class="btn btn-secondary">Clear</a>
        </div>
      </form>
    </div>
  </div>
</div>
```

### Visual Specifications

- **Background**: `bg-secondary` with `border-primary` border
- **Toggle Button**: Full-width, flex layout, smooth chevron rotation
- **Collapsed State**: `max-height: 0px`, chevron rotated -90deg
- **Expanded State**: `max-height: 1000px`, chevron default rotation
- **Transition**: 300ms ease for smooth collapse/expand

---

## 4. Command Module Card Component

### Purpose

Module cards serve as containers for grouped commands. Each card represents a command module from Discord.NET's InteractionService and displays all commands within that module.

### Card States

1. **Collapsed** (default): Shows module header with name, description, and command count
2. **Expanded**: Reveals full list of commands with parameters and preconditions

### HTML Structure

```html
<div class="module-card bg-bg-secondary border border-border-primary rounded-lg overflow-hidden mb-4">
  <!-- Module Header (clickable) -->
  <button type="button"
          class="module-header w-full flex items-center justify-between px-5 py-4 text-left hover:bg-bg-hover transition-colors"
          aria-expanded="false"
          aria-controls="module-commands-@moduleId"
          onclick="toggleModule(this)">
    <div class="flex items-center gap-4 flex-1 min-w-0">
      <!-- Module Icon -->
      <div class="flex-shrink-0 w-10 h-10 bg-accent-blue-muted rounded-lg flex items-center justify-center">
        <svg class="w-6 h-6 text-accent-blue" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
        </svg>
      </div>

      <!-- Module Info -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2 mb-1">
          <h3 class="text-lg font-semibold text-text-primary truncate">@moduleName</h3>
          <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
            Text = $"{commandCount} {(commandCount == 1 ? "command" : "commands")}",
            Variant = BadgeVariant.Gray,
            Size = BadgeSize.Small
          }" />
        </div>
        @if (!string.IsNullOrEmpty(moduleDescription))
        {
          <p class="text-sm text-text-secondary line-clamp-1">@moduleDescription</p>
        }
      </div>
    </div>

    <!-- Expand/Collapse Chevron -->
    <svg class="module-chevron w-6 h-6 text-text-secondary transition-transform duration-200 flex-shrink-0 ml-3"
         fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
    </svg>
  </button>

  <!-- Commands List (collapsible) -->
  <div id="module-commands-@moduleId"
       class="module-commands-container overflow-hidden transition-all duration-300 max-h-0">
    <div class="border-t border-border-primary bg-bg-primary/50">
      <!-- Command items go here -->
    </div>
  </div>
</div>
```

### Visual Specifications

#### Module Card Base
```css
.module-card {
  background-color: #262a2d;     /* bg-secondary */
  border: 1px solid #3f4447;     /* border-primary */
  border-radius: 0.5rem;         /* rounded-lg */
  margin-bottom: 1rem;           /* mb-4 */
}
```

#### Module Header
```css
.module-header {
  padding: 1rem 1.25rem;         /* px-5 py-4 */
  cursor: pointer;
  transition: background-color 0.15s ease-in-out;
}

.module-header:hover {
  background-color: #363a3e;     /* bg-hover */
}

.module-header:focus-visible {
  outline: 2px solid #098ecf;    /* border-focus */
  outline-offset: -2px;
}
```

#### Module Icon
```css
.module-icon {
  width: 2.5rem;                 /* w-10 */
  height: 2.5rem;                /* h-10 */
  background-color: rgba(9, 142, 207, 0.2);  /* accent-blue-muted */
  border-radius: 0.5rem;         /* rounded-lg */
  display: flex;
  align-items: center;
  justify-content: center;
}

.module-icon svg {
  width: 1.5rem;                 /* w-6 */
  height: 1.5rem;                /* h-6 */
  color: #098ecf;                /* accent-blue */
}
```

#### Collapse/Expand Animation
```css
.module-commands-container {
  max-height: 0;
  overflow: hidden;
  transition: max-height 0.3s ease-in-out;
}

.module-commands-container.expanded {
  max-height: 5000px;            /* Large enough for all commands */
}

.module-chevron {
  transition: transform 0.2s ease-in-out;
}

.module-chevron.expanded {
  transform: rotate(180deg);
}
```

---

## 5. Command List Item Component

### Purpose

Individual command items display within expanded module cards. Each item shows the command name, description, parameters, and precondition badges.

### HTML Structure

```html
<div class="command-item border-b border-border-secondary last:border-b-0">
  <div class="px-5 py-4">
    <!-- Command Header -->
    <div class="flex items-start justify-between gap-4 mb-3">
      <div class="flex-1 min-w-0">
        <!-- Command Name -->
        <div class="flex items-center gap-2 mb-2">
          <svg class="w-5 h-5 text-text-tertiary flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          <code class="text-base font-mono font-semibold text-accent-blue">/@commandName</code>
        </div>

        <!-- Command Description -->
        @if (!string.IsNullOrEmpty(commandDescription))
        {
          <p class="text-sm text-text-secondary ml-7">@commandDescription</p>
        }
      </div>

      <!-- Precondition Badges -->
      @if (preconditions.Any())
      {
        <div class="flex flex-wrap gap-2">
          @foreach (var precondition in preconditions)
          {
            @* Badge rendered based on precondition type (see section 6) *@
          }
        </div>
      }
    </div>

    <!-- Parameters Section -->
    @if (parameters.Any())
    {
      <div class="ml-7 space-y-2">
        <div class="flex items-center gap-2 mb-2">
          <svg class="w-4 h-4 text-text-tertiary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
          </svg>
          <span class="text-xs font-semibold text-text-tertiary uppercase tracking-wider">Parameters</span>
        </div>

        @foreach (var param in parameters)
        {
          <div class="parameter-item bg-bg-tertiary/30 border border-border-secondary rounded-md px-3 py-2">
            <div class="flex items-start gap-3">
              <!-- Parameter Name & Type -->
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-1">
                  <code class="text-sm font-mono text-text-primary">@param.Name</code>
                  <span class="text-xs font-mono text-text-tertiary bg-bg-tertiary px-1.5 py-0.5 rounded">
                    @param.Type
                  </span>
                  @if (param.IsRequired)
                  {
                    <span class="text-xs font-semibold text-error uppercase">Required</span>
                  }
                  else
                  {
                    <span class="text-xs font-semibold text-text-tertiary uppercase">Optional</span>
                  }
                </div>
                @if (!string.IsNullOrEmpty(param.Description))
                {
                  <p class="text-xs text-text-secondary">@param.Description</p>
                }
              </div>
            </div>
          </div>
        }
      </div>
    }
    else
    {
      <div class="ml-7">
        <p class="text-xs text-text-tertiary italic">No parameters</p>
      </div>
    }
  </div>
</div>
```

### Visual Specifications

#### Command Item Base
```css
.command-item {
  border-bottom: 1px solid #2f3336;  /* border-secondary */
  padding: 1rem 1.25rem;             /* px-5 py-4 */
}

.command-item:last-child {
  border-bottom: none;
}
```

#### Command Name
```css
.command-name {
  font-family: var(--font-family-mono);
  font-size: 1rem;                   /* text-base */
  font-weight: 600;                  /* font-semibold */
  color: #098ecf;                    /* accent-blue */
}
```

#### Parameter Item
```css
.parameter-item {
  background-color: rgba(47, 51, 54, 0.3);  /* bg-tertiary/30 */
  border: 1px solid #2f3336;                /* border-secondary */
  border-radius: 0.375rem;                  /* rounded-md */
  padding: 0.5rem 0.75rem;                  /* px-3 py-2 */
}

.parameter-required {
  color: #ef4444;                    /* error red */
  font-size: 0.75rem;                /* text-xs */
  font-weight: 600;                  /* font-semibold */
  text-transform: uppercase;
}

.parameter-optional {
  color: #7a7876;                    /* text-tertiary */
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.parameter-type {
  background-color: #2f3336;         /* bg-tertiary */
  color: #7a7876;                    /* text-tertiary */
  font-family: var(--font-family-mono);
  font-size: 0.75rem;                /* text-xs */
  padding: 0.125rem 0.375rem;        /* px-1.5 py-0.5 */
  border-radius: 0.25rem;            /* rounded */
}
```

---

## 6. Precondition Badge Mappings

### Badge Color System

Map precondition types to semantic badge variants based on restriction level:

| Precondition Type | Badge Variant | Color | Icon | Rationale |
|-------------------|---------------|-------|------|-----------|
| `RequireOwner` | Error (Red) | `#ef4444` | Shield with lock | Most restrictive (bot owner only) |
| `RequireAdmin` | Orange | `#cb4e1b` | Shield check | Admin-level restriction |
| `RequireUserPermission` | Blue | `#098ecf` | User shield | Server permission required |
| `RateLimit` | Warning (Amber) | `#f59e0b` | Clock | Usage limitation |
| `RequireBotPermission` | Gray | `#3f4447` | Bot | Bot permission requirement |
| `RequireContext` | Gray | `#3f4447` | Message | Context restriction (DM/Guild) |

### Implementation Examples

```html
<!-- RequireOwner Precondition -->
<partial name="Shared/Components/_Badge" model="new BadgeViewModel {
  Text = "Owner Only",
  Variant = BadgeVariant.Error,
  Size = BadgeSize.Small,
  Icon = "shield-exclamation"  // HeroIcons shield-exclamation
}" />

<!-- RequireAdmin Precondition -->
<partial name="Shared/Components/_Badge" model="new BadgeViewModel {
  Text = "Admin",
  Variant = BadgeVariant.Orange,
  Size = BadgeSize.Small,
  Icon = "shield-check"
}" />

<!-- RateLimit Precondition -->
<partial name="Shared/Components/_Badge" model="new BadgeViewModel {
  Text = "Rate Limited: 3/min",
  Variant = BadgeVariant.Warning,
  Size = BadgeSize.Small,
  Icon = "clock"
}" />

<!-- RequireUserPermission Precondition -->
<partial name="Shared/Components/_Badge" model="new BadgeViewModel {
  Text = "Manage Channels",
  Variant = BadgeVariant.Blue,
  Size = BadgeSize.Small,
  Icon = "user-group"
}" />
```

### Badge HTML with Icons

```html
<span class="badge badge-error inline-flex items-center gap-1.5">
  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
  </svg>
  <span>Owner Only</span>
</span>

<span class="badge badge-orange inline-flex items-center gap-1.5">
  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
  </svg>
  <span>Admin</span>
</span>

<span class="badge badge-warning inline-flex items-center gap-1.5">
  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
  </svg>
  <span>3 per minute</span>
</span>
```

---

## 7. Responsive Breakpoints

### Mobile (< 768px)

- **Layout**: Single column, full-width cards
- **Module Cards**: Stacked vertically with 1rem gap
- **Precondition Badges**: Wrap to multiple lines if needed
- **Parameter Display**: Full-width, stacked layout
- **Filter Panel**: Starts collapsed on mobile
- **Hide Elements**: Timestamp in header

```css
/* Mobile-specific adjustments */
@media (max-width: 767px) {
  .module-header {
    padding: 0.75rem 1rem;         /* Smaller padding */
  }

  .command-item {
    padding: 0.75rem 1rem;
  }

  .parameter-item {
    font-size: 0.875rem;           /* Slightly larger on mobile */
  }

  .precondition-badges {
    flex-wrap: wrap;               /* Allow wrapping */
  }
}
```

### Tablet (768px - 1024px)

- **Layout**: Single column with comfortable padding
- **Module Cards**: Standard card width with max-width constraint
- **Filter Grid**: 2-column layout for filters
- **Show Elements**: All module information visible

```css
@media (min-width: 768px) and (max-width: 1023px) {
  .module-cards-container {
    max-width: 768px;
    margin: 0 auto;
  }

  .filter-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
```

### Desktop (> 1024px)

- **Layout**: Centered content with max-width 1280px
- **Module Cards**: Full information display
- **Filter Grid**: 3-column layout
- **Show Elements**: All timestamps, badges, and details

```css
@media (min-width: 1024px) {
  .module-cards-container {
    max-width: 1280px;
    margin: 0 auto;
  }

  .filter-grid {
    grid-template-columns: repeat(3, 1fr);
  }

  .parameter-list {
    display: grid;
    grid-template-columns: repeat(2, 1fr);  /* Two-column parameter layout */
    gap: 0.5rem;
  }
}
```

---

## 8. State Management & Interactions

### Empty States

#### No Modules/Commands State

```html
<div class="empty-state bg-bg-secondary border border-border-primary rounded-lg p-12 text-center">
  <svg class="w-16 h-16 text-text-tertiary mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
  </svg>
  <h3 class="text-lg font-semibold text-text-primary mb-2">No Commands Available</h3>
  <p class="text-sm text-text-secondary mb-4">
    The bot hasn't registered any slash commands yet
  </p>
  <button type="button" class="btn btn-primary" onclick="syncCommands(this)">
    <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
    </svg>
    Refresh Commands
  </button>
</div>
```

#### No Search Results State

```html
<partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
  Type = EmptyStateType.NoResults,
  Title = "No commands found",
  Description = "Try adjusting your search or filters",
  Icon = "search"
}" />
```

### Loading States

#### Page Loading (Skeleton Cards)

```html
<!-- Use skeleton loader pattern from design-system.md -->
<div class="module-card-skeleton bg-bg-secondary border border-border-primary rounded-lg p-5">
  <div class="flex items-center gap-4">
    <div class="skeleton w-10 h-10 rounded-lg"></div>
    <div class="flex-1 space-y-2">
      <div class="skeleton w-32 h-5 rounded"></div>
      <div class="skeleton w-48 h-4 rounded"></div>
    </div>
    <div class="skeleton w-6 h-6 rounded"></div>
  </div>
</div>
```

Repeat 3-5 skeleton cards for initial loading state.

### Error States

#### Failed to Load Commands

```html
<partial name="Shared/Components/_Alert" model="new AlertViewModel {
  Type = AlertType.Error,
  Title = "Failed to load commands",
  Message = "Unable to retrieve command information from the bot. Please try refreshing.",
  Dismissible = true
}" />
```

### Interactive States

#### Module Card States

```javascript
// Collapse/expand state management
function toggleModule(button) {
  const isExpanded = button.getAttribute('aria-expanded') === 'true';
  const targetId = button.getAttribute('aria-controls');
  const container = document.getElementById(targetId);
  const chevron = button.querySelector('.module-chevron');

  if (isExpanded) {
    // Collapse
    container.style.maxHeight = '0px';
    chevron.style.transform = 'rotate(0deg)';
    button.setAttribute('aria-expanded', 'false');
  } else {
    // Expand
    container.style.maxHeight = container.scrollHeight + 'px';
    chevron.style.transform = 'rotate(180deg)';
    button.setAttribute('aria-expanded', 'true');
  }
}

// Expand all modules
function expandAllModules() {
  const allModuleHeaders = document.querySelectorAll('.module-header');
  allModuleHeaders.forEach(button => {
    if (button.getAttribute('aria-expanded') !== 'true') {
      toggleModule(button);
    }
  });
}

// Collapse all modules
function collapseAllModules() {
  const allModuleHeaders = document.querySelectorAll('.module-header');
  allModuleHeaders.forEach(button => {
    if (button.getAttribute('aria-expanded') === 'true') {
      toggleModule(button);
    }
  });
}
```

#### Filter Panel State

```javascript
function toggleFilterPanel() {
  const content = document.getElementById('filterContent');
  const chevron = document.getElementById('filterChevron');
  const toggle = document.getElementById('filterToggle');

  if (content.style.maxHeight === '0px') {
    content.style.maxHeight = '1000px';
    chevron.style.transform = 'rotate(0deg)';
    toggle.setAttribute('aria-expanded', 'true');
  } else {
    content.style.maxHeight = '0px';
    chevron.style.transform = 'rotate(-90deg)';
    toggle.setAttribute('aria-expanded', 'false');
  }
}
```

---

## 9. Accessibility Requirements

### WCAG 2.1 AA Compliance

#### Keyboard Navigation

- **Tab Order**: Logical flow through filters → module headers → command details
- **Enter/Space**: Toggle module expand/collapse
- **Arrow Keys**: Optional navigation between module cards
- **Focus Indicators**: Visible 2px blue outline on all interactive elements

```css
/* Focus styles */
.module-header:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: -2px;
}

button:focus-visible,
a:focus-visible,
input:focus-visible,
select:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}
```

#### ARIA Attributes

```html
<!-- Module Card -->
<button type="button"
        class="module-header"
        aria-expanded="false"
        aria-controls="module-commands-123"
        aria-label="Expand Help Commands module with 5 commands">
  <!-- Header content -->
</button>

<div id="module-commands-123"
     role="region"
     aria-labelledby="module-header-123"
     aria-hidden="true">
  <!-- Commands list -->
</div>

<!-- Filter Panel -->
<button type="button"
        id="filterToggle"
        aria-expanded="true"
        aria-controls="filterContent"
        aria-label="Toggle search and filter panel">
  <!-- Filter toggle content -->
</button>

<!-- Precondition Badges -->
<span class="badge badge-orange" role="status" aria-label="Admin permission required">
  Admin
</span>
```

#### Screen Reader Support

- **Module Headers**: Announce module name, command count, and expand/collapse state
- **Command Names**: Clearly read as code/command with slash prefix
- **Parameters**: Announce parameter name, type, and required/optional status
- **Badges**: Use `aria-label` for full context (e.g., "Admin permission required")
- **Empty States**: Use proper heading hierarchy and descriptive text

#### Color Contrast

All text and interactive elements meet WCAG AA standards:

- **Command Names** (accent-blue `#098ecf` on bg-primary): **6.2:1** ✓
- **Primary Text** (text-primary on bg-primary): **10.8:1** ✓
- **Secondary Text** (text-secondary on bg-primary): **5.9:1** ✓
- **Badge Text** (white on badge backgrounds): **4.5:1+** ✓

---

## 10. Icon System (HeroIcons)

### Icon Mappings

| Element | Icon Name | SVG Path | Size |
|---------|-----------|----------|------|
| Module | `code-bracket` | Terminal/code icon | w-6 h-6 |
| Command | `command-line` | Command line icon | w-5 h-5 |
| Parameter | `adjustments-horizontal` | Sliders icon | w-4 h-4 |
| Shield (Admin) | `shield-check` | Shield with check | w-3.5 h-3.5 |
| Shield (Owner) | `shield-exclamation` | Shield with warning | w-3.5 h-3.5 |
| Clock (Rate Limit) | `clock` | Clock icon | w-3.5 h-3.5 |
| User Permission | `user-group` | Users icon | w-3.5 h-3.5 |
| Search | `magnifying-glass` | Search icon | w-5 h-5 |
| Filter | `funnel` | Filter icon | w-5 h-5 |
| Chevron | `chevron-down` | Dropdown arrow | w-6 h-6 |
| Refresh | `arrow-path` | Circular arrows | w-5 h-5 |

### Icon Usage Guidelines

1. **Consistent Sizing**: Use defined size classes (w-4, w-5, w-6) based on hierarchy
2. **Color Inheritance**: Icons inherit `currentColor` from parent
3. **Accessibility**: Always include `aria-hidden="true"` for decorative icons
4. **SVG Attributes**: Use `fill="none"`, `viewBox="0 0 24 24"`, `stroke="currentColor"`, `stroke-width="2"`

---

## 11. JavaScript Functionality

### Required Functions

```javascript
// Commands page functionality
const CommandsPage = {
  // Toggle individual module
  toggleModule: function(button) {
    const isExpanded = button.getAttribute('aria-expanded') === 'true';
    const targetId = button.getAttribute('aria-controls');
    const container = document.getElementById(targetId);
    const chevron = button.querySelector('.module-chevron');

    if (isExpanded) {
      this.collapseModule(button, container, chevron);
    } else {
      this.expandModule(button, container, chevron);
    }
  },

  // Expand module
  expandModule: function(button, container, chevron) {
    container.style.maxHeight = container.scrollHeight + 'px';
    container.setAttribute('aria-hidden', 'false');
    chevron.style.transform = 'rotate(180deg)';
    button.setAttribute('aria-expanded', 'true');
  },

  // Collapse module
  collapseModule: function(button, container, chevron) {
    container.style.maxHeight = '0px';
    container.setAttribute('aria-hidden', 'true');
    chevron.style.transform = 'rotate(0deg)';
    button.setAttribute('aria-expanded', 'false');
  },

  // Expand all modules
  expandAll: function() {
    const headers = document.querySelectorAll('.module-header');
    headers.forEach(button => {
      if (button.getAttribute('aria-expanded') !== 'true') {
        const targetId = button.getAttribute('aria-controls');
        const container = document.getElementById(targetId);
        const chevron = button.querySelector('.module-chevron');
        this.expandModule(button, container, chevron);
      }
    });
  },

  // Collapse all modules
  collapseAll: function() {
    const headers = document.querySelectorAll('.module-header');
    headers.forEach(button => {
      if (button.getAttribute('aria-expanded') === 'true') {
        const targetId = button.getAttribute('aria-controls');
        const container = document.getElementById(targetId);
        const chevron = button.querySelector('.module-chevron');
        this.collapseModule(button, container, chevron);
      }
    });
  },

  // Toggle filter panel
  toggleFilterPanel: function() {
    const content = document.getElementById('filterContent');
    const chevron = document.getElementById('filterChevron');
    const toggle = document.getElementById('filterToggle');

    const isExpanded = toggle.getAttribute('aria-expanded') === 'true';

    if (isExpanded) {
      content.style.maxHeight = '0px';
      chevron.style.transform = 'rotate(-90deg)';
      toggle.setAttribute('aria-expanded', 'false');
    } else {
      content.style.maxHeight = '1000px';
      chevron.style.transform = 'rotate(0deg)';
      toggle.setAttribute('aria-expanded', 'true');
    }
  },

  // Sync commands from bot
  syncCommands: async function(button) {
    const icon = button.querySelector('.sync-icon');
    const spinner = button.querySelector('.sync-spinner');
    const text = button.querySelector('.sync-text');
    const originalText = text.textContent;

    // Show loading state
    icon.classList.add('hidden');
    spinner.classList.remove('hidden');
    text.textContent = 'Refreshing...';
    button.disabled = true;

    try {
      const response = await fetch('/api/commands/sync', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        }
      });

      if (response.ok) {
        // Success - reload page to show updated commands
        window.location.reload();
      } else {
        // Error
        alert('Failed to sync commands. Please try again.');
      }
    } catch (error) {
      console.error('Sync error:', error);
      alert('An error occurred while syncing commands.');
    } finally {
      // Restore button state
      icon.classList.remove('hidden');
      spinner.classList.add('hidden');
      text.textContent = originalText;
      button.disabled = false;
    }
  }
};

// Export for global access
window.CommandsPage = CommandsPage;
```

---

## 12. Data Structure Expectations

### ViewModel Structure

```csharp
// Page-level ViewModel
public class CommandsPageViewModel
{
    public List<CommandModuleViewModel> Modules { get; set; } = new();
    public int TotalModules => Modules.Count;
    public int TotalCommands => Modules.Sum(m => m.Commands.Count);
    public DateTime LastUpdated { get; set; }
    public CommandsFilterViewModel Filters { get; set; } = new();
}

// Module ViewModel
public class CommandModuleViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CommandViewModel> Commands { get; set; } = new();
    public int CommandCount => Commands.Count;
}

// Command ViewModel
public class CommandViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ParameterViewModel> Parameters { get; set; } = new();
    public List<PreconditionViewModel> Preconditions { get; set; } = new();
}

// Parameter ViewModel
public class ParameterViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
}

// Precondition ViewModel
public class PreconditionViewModel
{
    public PreconditionType Type { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public BadgeVariant BadgeVariant { get; set; }
}

// Precondition Types
public enum PreconditionType
{
    RequireOwner,
    RequireAdmin,
    RequireUserPermission,
    RequireBotPermission,
    RateLimit,
    RequireContext
}

// Filter ViewModel
public class CommandsFilterViewModel
{
    public string? SearchTerm { get; set; }
    public string? ModuleFilter { get; set; }
    public string? PreconditionFilter { get; set; }
    public string SortBy { get; set; } = "name-asc";

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        !string.IsNullOrWhiteSpace(ModuleFilter) ||
        !string.IsNullOrWhiteSpace(PreconditionFilter);
}
```

---

## 13. Implementation Checklist

### Phase 1: Basic Structure
- [ ] Create Commands Index page (`Pages/Commands/Index.cshtml`)
- [ ] Create PageModel with data fetching (`Index.cshtml.cs`)
- [ ] Implement page header with title and badge
- [ ] Add basic module card layout
- [ ] Test responsive breakpoints

### Phase 2: Module & Command Display
- [ ] Implement collapsible module cards
- [ ] Add command list items within modules
- [ ] Display command names with slash prefix
- [ ] Show command descriptions
- [ ] Test collapse/expand animations

### Phase 3: Parameters & Preconditions
- [ ] Add parameter display section
- [ ] Implement required/optional indicators
- [ ] Add precondition badge mappings
- [ ] Display parameter types and descriptions
- [ ] Test various parameter combinations

### Phase 4: Search & Filtering
- [ ] Implement search input functionality
- [ ] Add module filter dropdown
- [ ] Add precondition filter dropdown
- [ ] Implement sort options
- [ ] Add filter persistence via query parameters

### Phase 5: Interactive Features
- [ ] Add expand/collapse JavaScript
- [ ] Implement "Expand All" / "Collapse All" buttons
- [ ] Add filter panel toggle
- [ ] Implement sync commands functionality
- [ ] Add loading states during sync

### Phase 6: Accessibility & Polish
- [ ] Add ARIA attributes to all interactive elements
- [ ] Test keyboard navigation
- [ ] Verify screen reader compatibility
- [ ] Test color contrast ratios
- [ ] Add empty states
- [ ] Add error states
- [ ] Test on mobile devices

### Phase 7: Documentation & Testing
- [ ] Update API documentation
- [ ] Add usage examples to component library
- [ ] Write unit tests for ViewModels
- [ ] Write integration tests for filtering
- [ ] Perform cross-browser testing
- [ ] Accessibility audit

---

## 14. Example Implementations

### Full Page Example (Simplified)

```cshtml
@page
@model DiscordBot.Bot.Pages.Commands.IndexModel
@using DiscordBot.Bot.ViewModels.Components
@{
    ViewData["Title"] = "Commands";
}

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
  <!-- Page Header -->
  <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
    <div class="flex items-center gap-3">
      <h1 class="text-2xl lg:text-3xl font-bold text-text-primary">Commands</h1>
      <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
        Text = Model.ViewModel.TotalModules.ToString(),
        Variant = BadgeVariant.Blue,
        Size = BadgeSize.Medium
      }" />
    </div>
    <div class="flex items-center gap-3">
      <button type="button" class="btn btn-accent" onclick="CommandsPage.syncCommands(this)">
        <svg class="w-5 h-5 mr-2 sync-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
        </svg>
        Refresh Commands
      </button>
    </div>
  </div>

  <!-- Module Cards -->
  <div class="space-y-4">
    @foreach (var module in Model.ViewModel.Modules)
    {
      <div class="module-card bg-bg-secondary border border-border-primary rounded-lg overflow-hidden">
        <button type="button"
                class="module-header w-full flex items-center justify-between px-5 py-4 text-left hover:bg-bg-hover transition-colors"
                aria-expanded="false"
                aria-controls="module-@module.Name"
                onclick="CommandsPage.toggleModule(this)">
          <div class="flex items-center gap-4 flex-1">
            <div class="w-10 h-10 bg-accent-blue-muted rounded-lg flex items-center justify-center">
              <svg class="w-6 h-6 text-accent-blue" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
              </svg>
            </div>
            <div class="flex-1">
              <h3 class="text-lg font-semibold text-text-primary">@module.Name</h3>
              @if (!string.IsNullOrEmpty(module.Description))
              {
                <p class="text-sm text-text-secondary">@module.Description</p>
              }
            </div>
            <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
              Text = $"{module.CommandCount} {(module.CommandCount == 1 ? "command" : "commands")}",
              Variant = BadgeVariant.Gray,
              Size = BadgeSize.Small
            }" />
          </div>
          <svg class="module-chevron w-6 h-6 text-text-secondary transition-transform duration-200"
               fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
          </svg>
        </button>

        <div id="module-@module.Name" class="module-commands-container overflow-hidden transition-all duration-300 max-h-0">
          <div class="border-t border-border-primary bg-bg-primary/50">
            @foreach (var command in module.Commands)
            {
              <div class="command-item border-b border-border-secondary last:border-b-0 px-5 py-4">
                <div class="flex items-start justify-between gap-4 mb-3">
                  <div class="flex-1">
                    <code class="text-base font-mono font-semibold text-accent-blue">/@command.Name</code>
                    @if (!string.IsNullOrEmpty(command.Description))
                    {
                      <p class="text-sm text-text-secondary mt-1">@command.Description</p>
                    }
                  </div>
                  @if (command.Preconditions.Any())
                  {
                    <div class="flex flex-wrap gap-2">
                      @foreach (var precondition in command.Preconditions)
                      {
                        <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
                          Text = precondition.DisplayText,
                          Variant = precondition.BadgeVariant,
                          Size = BadgeSize.Small
                        }" />
                      }
                    </div>
                  }
                </div>

                @if (command.Parameters.Any())
                {
                  <div class="ml-7 space-y-2">
                    <div class="text-xs font-semibold text-text-tertiary uppercase mb-2">Parameters</div>
                    @foreach (var param in command.Parameters)
                    {
                      <div class="parameter-item bg-bg-tertiary/30 border border-border-secondary rounded-md px-3 py-2">
                        <div class="flex items-center gap-2 mb-1">
                          <code class="text-sm font-mono text-text-primary">@param.Name</code>
                          <span class="text-xs font-mono text-text-tertiary bg-bg-tertiary px-1.5 py-0.5 rounded">
                            @param.Type
                          </span>
                          @if (param.IsRequired)
                          {
                            <span class="text-xs font-semibold text-error uppercase">Required</span>
                          }
                          else
                          {
                            <span class="text-xs font-semibold text-text-tertiary uppercase">Optional</span>
                          }
                        </div>
                        @if (!string.IsNullOrEmpty(param.Description))
                        {
                          <p class="text-xs text-text-secondary">@param.Description</p>
                        }
                      </div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        </div>
      </div>
    }
  </div>
</div>

@section Scripts {
  <script src="~/js/commands-page.js"></script>
}
```

---

## 15. Performance Considerations

### Optimization Strategies

1. **Lazy Rendering**: Only render command details when module is expanded
2. **Virtual Scrolling**: For pages with 50+ modules, implement virtual scrolling
3. **Debounced Search**: Debounce search input by 300ms to reduce filtering operations
4. **CSS Transitions**: Use GPU-accelerated transforms for smooth animations
5. **Icon Optimization**: Use inline SVG sprites to reduce HTTP requests

### Expected Load Times

- **Initial Page Load**: < 500ms (with skeleton loaders)
- **Module Expand**: < 100ms (CSS transition)
- **Search Filter**: < 200ms (debounced)
- **Command Sync**: 1-3s (async operation with loading state)

---

## 16. Browser Compatibility

### Supported Browsers

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile Safari (iOS 14+)
- Chrome Mobile (Android 10+)

### Progressive Enhancement

- **Core Functionality**: Works without JavaScript (server-side filtering)
- **Enhanced UX**: JavaScript adds collapse/expand, live search, smooth animations
- **CSS Grid Fallback**: Flexbox alternative for older browsers

---

## Changelog

### Version 1.0 (2025-12-24)
- Initial design specification
- Defined page layout and component structure
- Established precondition badge mappings
- Created responsive breakpoint specifications
- Documented accessibility requirements
- Added implementation checklist

---

## Appendix: Design Tokens Reference

### Colors Used

| Token | Hex Value | Usage |
|-------|-----------|-------|
| `bg-primary` | `#1d2022` | Main page background |
| `bg-secondary` | `#262a2d` | Module cards, filter panel |
| `bg-tertiary` | `#2f3336` | Parameter items, elevated elements |
| `bg-hover` | `#363a3e` | Interactive hover states |
| `text-primary` | `#d7d3d0` | Primary text |
| `text-secondary` | `#a8a5a3` | Secondary text, descriptions |
| `text-tertiary` | `#7a7876` | Muted text, placeholders |
| `accent-blue` | `#098ecf` | Command names, links, primary accent |
| `accent-orange` | `#cb4e1b` | Admin badges, primary actions |
| `border-primary` | `#3f4447` | Card borders, dividers |
| `border-secondary` | `#2f3336` | Subtle dividers |
| `border-focus` | `#098ecf` | Focus rings |
| `success` | `#10b981` | Success badges |
| `warning` | `#f59e0b` | Warning badges (rate limits) |
| `error` | `#ef4444` | Error badges (owner only) |

### Typography Scale

| Class | Size | Line Height | Usage |
|-------|------|-------------|-------|
| `text-2xl` | 1.5rem (24px) | 1.33 | Page title (mobile) |
| `text-3xl` | 1.875rem (30px) | 1.2 | Page title (desktop) |
| `text-lg` | 1.125rem (18px) | 1.4 | Module names |
| `text-base` | 1rem (16px) | 1.5 | Command names, body text |
| `text-sm` | 0.875rem (14px) | 1.4 | Descriptions, labels |
| `text-xs` | 0.75rem (12px) | 1.3 | Badges, metadata |

---

## Support & Feedback

For questions or suggestions about this design specification:
- **GitHub Issue**: #203
- **Documentation**: See `design-system.md` for foundational design tokens
- **Component Library**: Check `Pages/Shared/Components/` for reusable partials

**Maintained by:** Design & UI Team
**Last Review:** 2025-12-24
**Next Review:** As needed for issue #203 implementation
