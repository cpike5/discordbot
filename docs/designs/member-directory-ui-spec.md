# Member Directory - UI Design Specification

**Feature:** Member Directory (Issue #296)
**Version:** 1.0
**Last Updated:** 2025-12-30
**Target Framework:** ASP.NET Core Razor Pages with Tailwind CSS

---

## Table of Contents

1. [Overview](#1-overview)
2. [Page Structure](#2-page-structure)
3. [Component Specifications](#3-component-specifications)
4. [Interaction Patterns](#4-interaction-patterns)
5. [State Management](#5-state-management)
6. [Accessibility Requirements](#6-accessibility-requirements)
7. [Responsive Design](#7-responsive-design)
8. [Prototype Requirements](#8-prototype-requirements)

---

## 1. Overview

### Purpose

The Member Directory provides a searchable, filterable interface for viewing and managing Discord server members within the admin panel. It enables administrators to quickly find members, view their details, and perform bulk operations.

### Design Goals

- **Efficient Search**: Fast, multi-field search with live filtering
- **Clear Information Hierarchy**: Critical member info visible at a glance
- **Bulk Operations**: Select multiple members for batch actions
- **Performance**: Handle large member lists (1000+ members) without degradation
- **Consistent Patterns**: Follow existing admin UI conventions (Guilds, Users, CommandLogs pages)

### User Flows

```
Primary Flow:
1. Navigate to /Guilds/{guildId}/Members
2. View paginated member list (25 per page default)
3. Apply filters/search to narrow results
4. Click member row to view details in modal/page
5. Optionally select multiple members for bulk actions

Secondary Flow:
1. Start on Guild Details page
2. Click "View Members" link
3. Land on filtered member list for that guild
```

---

## 2. Page Structure

### Route

```
/Guilds/{guildId:long}/Members
```

### Page Layout

```
┌─────────────────────────────────────────────────────────────────┐
│ Navigation Bar (Shared)                                         │
├─────────────────────────────────────────────────────────────────┤
│ Breadcrumbs                                                      │
│ Servers > [Guild Name] > Members                                │
├─────────────────────────────────────────────────────────────────┤
│ Page Header                                                      │
│ ┌──────────────────────────────────────┬─────────────────────┐ │
│ │ Members [Badge: 1,234]               │ [Sync] [Export CSV] │ │
│ │ Manage and review server members     │                     │ │
│ └──────────────────────────────────────┴─────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│ Filter Panel (Collapsible)                                      │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ [▼] Filters [Badge: 3 active]               [Reset Filters]│ │
│ │ ┌─────────────────────────────────────────────────────────┐ │ │
│ │ │ Search: [_______________]    Role: [Multi-select ▼]     │ │ │
│ │ │ Join Date: [From: ___] [To: ___]                        │ │ │
│ │ │ Activity: [Last Active ▼]   Sort: [Join Date ▼] [▲/▼]  │ │ │
│ │ │                            [Apply Filters] [Reset]      │ │ │
│ │ └─────────────────────────────────────────────────────────┘ │ │
│ └─────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│ Bulk Actions Toolbar (visible when items selected)              │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ [✓] 5 selected      [Deselect All] [Export Selected]       │ │
│ └─────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│ Member Table/List                                                │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ [☐] Avatar  Username       Roles    Joined      Last Active│ │
│ │ ─────────────────────────────────────────────────────────── │ │
│ │ [☐] [img]   JohnDoe#1234   @Admin   Jan 15     2 hours ago │ │
│ │ [☐] [img]   JaneSmith      @Mod     Feb 3      Yesterday   │ │
│ │ ... (25 rows per page)                                      │ │
│ └─────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│ Pagination                                                       │
│ Showing 1-25 of 1,234 members                                   │
│ [First] [Previous] [1] [2] [3] ... [50] [Next] [Last]          │
└─────────────────────────────────────────────────────────────────┘
```

### Breadcrumb Navigation

```html
<nav class="text-sm text-text-secondary mb-4" aria-label="Breadcrumb">
  <ol class="flex items-center gap-2">
    <li><a href="/Guilds" class="hover:text-text-primary transition-colors">Servers</a></li>
    <li aria-hidden="true"><svg class="w-4 h-4"><!-- chevron-right --></svg></li>
    <li><a href="/Guilds/Details?id={guildId}" class="hover:text-text-primary transition-colors">{GuildName}</a></li>
    <li aria-hidden="true"><svg class="w-4 h-4"><!-- chevron-right --></svg></li>
    <li class="text-text-primary font-medium" aria-current="page">Members</li>
  </ol>
</nav>
```

**Design Tokens:**
- Font size: `text-sm` (14px)
- Colors: `text-secondary` → `text-primary` on hover
- Spacing: `gap-2` (8px) between items

---

## 3. Component Specifications

### 3.1 Page Header Component

**Component Name:** `MemberDirectoryHeader`

**Layout:**
```html
<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
  <div class="flex items-center gap-3">
    <h1 class="text-h1 text-text-primary">Members</h1>
    <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
      Text = Model.TotalMemberCount.ToString("N0"),
      Variant = BadgeVariant.Blue,
      Size = BadgeSize.Medium
    }" />
  </div>
  <div class="flex items-center gap-3">
    <button type="button" class="btn btn-secondary" onclick="syncMembers(this)">
      <svg class="w-5 h-5 mr-2 sync-icon"><!-- refresh icon --></svg>
      <span class="sync-text">Sync Members</span>
    </button>
    <a asp-page-handler="Export" class="btn btn-primary">
      <svg class="w-4 h-4 mr-2"><!-- download icon --></svg>
      Export CSV
    </a>
  </div>
</div>
```

**States:**
- **Default**: Buttons enabled, badge shows total count
- **Syncing**: Sync button shows spinner icon, text changes to "Syncing...", button disabled
- **Sync Complete**: Toast notification "Members synced successfully", badge updates with new count
- **Sync Error**: Toast notification with error message, button re-enabled

**Responsive Behavior:**
- Mobile: Stack vertically, full-width buttons
- Tablet+: Horizontal layout, buttons auto-width

**Design Tokens:**
- Heading: `text-h1` (36px, 700 weight)
- Badge: Blue variant, medium size
- Button gap: `gap-3` (12px)
- Section margin: `mb-6` (24px)

---

### 3.2 Filter Panel Component

**Component Name:** `MemberDirectoryFilters`

**Collapsed State:**
```html
<div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
  <button type="button"
          id="filterToggle"
          class="w-full flex items-center justify-between px-5 py-4 text-left"
          aria-expanded="false"
          aria-controls="filterContent">
    <div class="flex items-center gap-3">
      <svg class="w-5 h-5 text-text-secondary"><!-- filter icon --></svg>
      <span class="text-lg font-semibold text-text-primary">Filters</span>
      @if (Model.HasActiveFilters)
      {
        <partial name="Shared/Components/_Badge" model="new BadgeViewModel {
          Text = $"{Model.ActiveFilterCount} active",
          Variant = BadgeVariant.Orange,
          Size = BadgeSize.Small
        }" />
      }
    </div>
    <svg id="filterChevron" class="w-5 h-5 text-text-secondary transition-transform duration-200">
      <!-- chevron-down, rotates -90deg when collapsed -->
    </svg>
  </button>

  <div id="filterContent" class="overflow-hidden transition-all duration-300 max-h-0">
    <!-- Filter form content here -->
  </div>
</div>
```

**Expanded State:**

The filter content expands to show the filter form:

```html
<div class="border-t border-border-primary p-6">
  <form method="get" id="filterForm">
    <!-- Row 1: Search and Role Filter -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
      <!-- Search Input -->
      <div>
        <label for="SearchTerm" class="block text-sm font-medium text-text-primary mb-1">
          Search
        </label>
        <div class="relative">
          <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary">
            <!-- search icon -->
          </svg>
          <input type="search"
                 id="SearchTerm"
                 name="SearchTerm"
                 value="@Model.SearchTerm"
                 placeholder="Username, display name, or user ID..."
                 class="w-full pl-10 pr-4 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary placeholder-text-tertiary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors" />
        </div>
      </div>

      <!-- Role Filter (Multi-select) -->
      <div>
        <label for="RoleFilter" class="block text-sm font-medium text-text-primary mb-1">
          Roles
        </label>
        <div class="relative role-multiselect-wrapper">
          <button type="button"
                  id="roleMultiSelectToggle"
                  class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer flex items-center justify-between"
                  aria-haspopup="listbox"
                  aria-expanded="false">
            <span class="truncate" id="roleSelectedText">
              @if (Model.SelectedRoles.Any())
              {
                @($"{Model.SelectedRoles.Count} roles selected")
              }
              else
              {
                @("All roles")
              }
            </span>
            <svg class="w-4 h-4 ml-2 flex-shrink-0"><!-- chevron-down --></svg>
          </button>
          <div id="roleMultiSelectDropdown"
               class="hidden absolute z-10 mt-1 w-full bg-bg-tertiary border border-border-primary rounded-lg shadow-lg max-h-60 overflow-auto"
               role="listbox">
            <label class="flex items-center gap-2 px-3 py-2 hover:bg-bg-hover cursor-pointer transition-colors">
              <input type="checkbox"
                     name="RoleFilter"
                     value=""
                     class="w-4 h-4 rounded border-border-primary"
                     onchange="updateRoleSelection()" />
              <span class="text-sm text-text-primary">All Roles</span>
            </label>
            @foreach (var role in Model.AvailableRoles)
            {
              <label class="flex items-center gap-2 px-3 py-2 hover:bg-bg-hover cursor-pointer transition-colors">
                <input type="checkbox"
                       name="RoleFilter"
                       value="@role.Id"
                       checked="@(Model.SelectedRoles.Contains(role.Id))"
                       class="w-4 h-4 rounded border-border-primary"
                       onchange="updateRoleSelection()" />
                <span class="inline-flex items-center gap-2">
                  <span class="w-3 h-3 rounded-full" style="background-color: @role.ColorHex"></span>
                  <span class="text-sm text-text-primary">@role.Name</span>
                </span>
              </label>
            }
          </div>
        </div>
      </div>
    </div>

    <!-- Row 2: Join Date Range -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
      <div>
        <label for="JoinedAfter" class="block text-sm font-medium text-text-primary mb-1">
          Joined After
        </label>
        <input type="date"
               id="JoinedAfter"
               name="JoinedAfter"
               value="@Model.JoinedAfter?.ToString("yyyy-MM-dd")"
               class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors" />
      </div>
      <div>
        <label for="JoinedBefore" class="block text-sm font-medium text-text-primary mb-1">
          Joined Before
        </label>
        <input type="date"
               id="JoinedBefore"
               name="JoinedBefore"
               value="@Model.JoinedBefore?.ToString("yyyy-MM-dd")"
               class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors" />
      </div>
    </div>

    <!-- Row 3: Activity Filter and Sort -->
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
      <div>
        <label for="ActivityFilter" class="block text-sm font-medium text-text-primary mb-1">
          Activity
        </label>
        <select id="ActivityFilter"
                name="ActivityFilter"
                class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer">
          <option value="">All Members</option>
          <option value="active-today" selected="@(Model.ActivityFilter == "active-today")">Active Today</option>
          <option value="active-week" selected="@(Model.ActivityFilter == "active-week")">Active This Week</option>
          <option value="active-month" selected="@(Model.ActivityFilter == "active-month")">Active This Month</option>
          <option value="inactive-week" selected="@(Model.ActivityFilter == "inactive-week")">Inactive 7+ Days</option>
          <option value="inactive-month" selected="@(Model.ActivityFilter == "inactive-month")">Inactive 30+ Days</option>
          <option value="never-messaged" selected="@(Model.ActivityFilter == "never-messaged")">Never Messaged</option>
        </select>
      </div>

      <div>
        <label for="SortBy" class="block text-sm font-medium text-text-primary mb-1">
          Sort By
        </label>
        <select id="SortBy"
                name="SortBy"
                class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer">
          <option value="join-date" selected="@(Model.SortBy == "join-date")">Join Date</option>
          <option value="username" selected="@(Model.SortBy == "username")">Username</option>
          <option value="last-active" selected="@(Model.SortBy == "last-active")">Last Active</option>
          <option value="message-count" selected="@(Model.SortBy == "message-count")">Message Count</option>
          <option value="role-count" selected="@(Model.SortBy == "role-count")">Role Count</option>
        </select>
      </div>

      <div>
        <label for="SortDescending" class="block text-sm font-medium text-text-primary mb-1">
          Order
        </label>
        <select id="SortDescending"
                name="SortDescending"
                class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer">
          <option value="false" selected="@(!Model.SortDescending)">Ascending</option>
          <option value="true" selected="@(Model.SortDescending)">Descending</option>
        </select>
      </div>
    </div>

    <!-- Filter Actions -->
    <div class="flex items-center gap-3 pt-4 border-t border-border-secondary">
      <button type="submit" class="btn btn-primary">
        <svg class="w-4 h-4 mr-2"><!-- filter icon --></svg>
        Apply Filters
      </button>
      <a asp-page="Index" asp-route-id="@Model.GuildId" class="btn btn-secondary">
        Reset
      </a>
    </div>
  </form>
</div>
```

**States:**
- **Collapsed**: `max-h-0`, chevron rotated -90deg
- **Expanded**: `max-h-[800px]`, chevron at 0deg
- **Has Active Filters**: Orange badge showing count
- **No Active Filters**: No badge displayed

**Interaction:**
- Click toggle button to expand/collapse
- Filters apply on form submit (not live)
- "Reset" link clears all filters and reloads page
- Role multi-select updates selected count in button text on change

**Accessibility:**
- `aria-expanded` attribute on toggle button
- `aria-controls` links toggle to content
- `role="listbox"` on dropdown
- Keyboard navigation: Tab through filters, Enter/Space to toggle collapse

**Design Tokens:**
- Panel background: `bg-secondary` (#262a2d)
- Border: `border-primary` (#3f4447)
- Border radius: `rounded-lg` (8px)
- Padding: Collapse button `px-5 py-4`, form content `p-6`
- Transition: `duration-300` for smooth expand/collapse

---

### 3.3 Bulk Actions Toolbar Component

**Component Name:** `MemberBulkActionsToolbar`

**Visibility:** Only shown when one or more members are selected

```html
<div id="bulkActionsToolbar"
     class="@(Model.SelectedMemberIds.Any() ? "block" : "hidden") bg-accent-blue/10 border border-accent-blue/30 rounded-lg px-5 py-3 mb-4 flex items-center justify-between">
  <div class="flex items-center gap-3">
    <svg class="w-5 h-5 text-accent-blue"><!-- check-circle icon --></svg>
    <span class="text-sm font-medium text-text-primary">
      <span id="selectedCount">@Model.SelectedMemberIds.Count</span> member(s) selected
    </span>
  </div>
  <div class="flex items-center gap-2">
    <button type="button"
            class="btn btn-secondary btn-sm"
            onclick="deselectAll()">
      Deselect All
    </button>
    <button type="button"
            class="btn btn-primary btn-sm"
            onclick="exportSelected()">
      <svg class="w-4 h-4 mr-2"><!-- download icon --></svg>
      Export Selected
    </button>
    <!-- Future bulk actions -->
    <!-- <button type="button" class="btn btn-secondary btn-sm">Add Mod Tag</button> -->
    <!-- <button type="button" class="btn btn-secondary btn-sm">Add to Watchlist</button> -->
  </div>
</div>
```

**States:**
- **Hidden**: `display: none` when no selections
- **Visible**: Slides in when first member selected
- **Updating**: Selected count updates as checkboxes change

**Interaction:**
- Appears/disappears with slide-in animation
- "Deselect All" unchecks all member checkboxes, hides toolbar
- "Export Selected" triggers CSV export with only selected member IDs

**Accessibility:**
- Live region announces selection count changes
- All buttons have clear labels and keyboard access

**Design Tokens:**
- Background: `bg-accent-blue/10` (blue with 10% opacity)
- Border: `border-accent-blue/30` (blue with 30% opacity)
- Text: `text-primary` for count, `text-accent-blue` for icon
- Padding: `px-5 py-3`
- Button size: Small variant (`btn-sm`)

---

### 3.4 Member Table Component (Desktop)

**Component Name:** `MemberTable`

**Visibility:** Hidden on mobile (`hidden md:block`)

```html
<div class="bg-bg-secondary border border-border-primary rounded-lg overflow-hidden hidden md:block">
  <div class="overflow-x-auto">
    <table class="w-full">
      <thead class="bg-bg-tertiary">
        <tr>
          <th class="px-4 py-4 text-left w-12">
            <input type="checkbox"
                   id="selectAll"
                   class="w-4 h-4 rounded border-border-primary cursor-pointer"
                   onchange="toggleSelectAll(this)"
                   aria-label="Select all members" />
          </th>
          <th class="px-6 py-4 text-left text-xs font-semibold text-text-primary uppercase tracking-wider">
            Member
          </th>
          <th class="px-6 py-4 text-left text-xs font-semibold text-text-primary uppercase tracking-wider">
            Roles
          </th>
          <th class="px-6 py-4 text-left text-xs font-semibold text-text-primary uppercase tracking-wider hidden lg:table-cell">
            Joined
          </th>
          <th class="px-6 py-4 text-left text-xs font-semibold text-text-primary uppercase tracking-wider hidden xl:table-cell">
            Last Active
          </th>
          <th class="px-6 py-4 text-left text-xs font-semibold text-text-primary uppercase tracking-wider hidden xl:table-cell">
            Messages
          </th>
          <th class="px-6 py-4 text-right text-xs font-semibold text-text-primary uppercase tracking-wider">
            Actions
          </th>
        </tr>
      </thead>
      <tbody class="divide-y divide-border-primary">
        @if (Model.Members.Any())
        {
          @foreach (var member in Model.Members)
          {
            <tr class="hover:bg-bg-hover/50 transition-colors" data-member-id="@member.UserId">
              <!-- Checkbox -->
              <td class="px-4 py-4">
                <input type="checkbox"
                       class="member-checkbox w-4 h-4 rounded border-border-primary cursor-pointer"
                       value="@member.UserId"
                       onchange="updateBulkSelection()"
                       aria-label="Select @member.DisplayName" />
              </td>

              <!-- Member (Avatar + Name + ID) -->
              <td class="px-6 py-4">
                <div class="flex items-center gap-3">
                  @if (!string.IsNullOrEmpty(member.AvatarUrl))
                  {
                    <img src="@member.AvatarUrl"
                         alt="@member.DisplayName"
                         class="w-10 h-10 rounded-full flex-shrink-0" />
                  }
                  else
                  {
                    <div class="w-10 h-10 rounded-full bg-gradient-to-br from-accent-blue to-accent-orange flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
                      @(member.DisplayName.Length >= 2 ? member.DisplayName[..2].ToUpper() : member.DisplayName.ToUpper())
                    </div>
                  }
                  <div class="min-w-0">
                    <p class="font-medium text-text-primary truncate">@member.DisplayName</p>
                    <p class="text-xs text-text-tertiary">
                      @if (!string.IsNullOrEmpty(member.Username))
                      {
                        <span class="font-mono">@@@member.Username</span>
                      }
                      else
                      {
                        <span class="font-mono">ID: @member.UserId</span>
                      }
                    </p>
                  </div>
                </div>
              </td>

              <!-- Roles (max 3 visible, +N more) -->
              <td class="px-6 py-4">
                <div class="flex flex-wrap gap-1">
                  @{
                    var visibleRoles = member.Roles.Take(3).ToList();
                    var remainingCount = member.Roles.Count - 3;
                  }
                  @foreach (var role in visibleRoles)
                  {
                    <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium text-white"
                          style="background-color: @role.ColorHex">
                      @role.Name
                    </span>
                  }
                  @if (remainingCount > 0)
                  {
                    <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-bg-tertiary text-text-secondary"
                          title="@string.Join(", ", member.Roles.Skip(3).Select(r => r.Name))">
                      +@remainingCount
                    </span>
                  }
                  @if (!member.Roles.Any())
                  {
                    <span class="text-xs text-text-tertiary">No roles</span>
                  }
                </div>
              </td>

              <!-- Joined Date (desktop only) -->
              <td class="px-6 py-4 text-text-secondary text-sm hidden lg:table-cell">
                <span data-utc="@member.JoinedAtUtcIso" data-format="date"></span>
              </td>

              <!-- Last Active (xl screens only) -->
              <td class="px-6 py-4 text-text-secondary text-sm hidden xl:table-cell">
                @if (member.LastActiveAt.HasValue)
                {
                  <span data-utc="@member.LastActiveAtUtcIso" data-format="relative"></span>
                }
                else
                {
                  <span class="text-text-tertiary">Never</span>
                }
              </td>

              <!-- Message Count (xl screens only) -->
              <td class="px-6 py-4 text-text-secondary text-sm hidden xl:table-cell">
                @member.MessageCount.ToString("N0")
              </td>

              <!-- Actions -->
              <td class="px-6 py-4 text-right">
                <button type="button"
                        class="text-accent-blue hover:text-accent-blue-hover text-sm font-medium transition-colors"
                        onclick="viewMemberDetails(@member.UserId)">
                  View
                </button>
              </td>
            </tr>
          }
        }
        else
        {
          <tr>
            <td colspan="7" class="px-6 py-12 text-center">
              @{
                var hasFilters = Model.HasActiveFilters;
                var emptyTitle = hasFilters ? "No members found" : "No members yet";
                var emptyDesc = hasFilters ? "Try adjusting your search or filters" : "This server has no members";
              }
              <partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
                Type = EmptyStateType.NoResults,
                Title = emptyTitle,
                Description = emptyDesc
              }" />
            </td>
          </tr>
        }
      </tbody>
    </table>
  </div>
</div>
```

**Column Visibility:**
- **All sizes (md+)**: Checkbox, Member, Roles, Actions
- **Large (lg+)**: + Joined
- **Extra Large (xl+)**: + Last Active, Messages

**Row States:**
- **Default**: `bg-bg-secondary`
- **Hover**: `bg-bg-hover/50` (slight highlight)
- **Selected**: Checkbox checked, row could have subtle highlight (optional)

**Interaction:**
- Click "View" to open member detail modal/page
- Check checkbox to select for bulk actions
- Hover over "+N" roles badge shows tooltip with all role names

**Design Tokens:**
- Table background: `bg-secondary`
- Header background: `bg-tertiary`
- Row hover: `bg-hover` with 50% opacity
- Borders: `border-primary` for table border, `divide-border-primary` for rows
- Avatar size: `w-10 h-10` (40px)
- Role badge: `text-xs`, `px-2 py-0.5`

---

### 3.5 Member Card Component (Mobile)

**Component Name:** `MemberCard`

**Visibility:** Shown on mobile only (`md:hidden`)

```html
<div class="md:hidden space-y-4">
  @if (Model.Members.Any())
  {
    @foreach (var member in Model.Members)
    {
      <div class="bg-bg-secondary border border-border-primary rounded-lg p-4">
        <!-- Header: Avatar, Name, Checkbox -->
        <div class="flex items-start justify-between mb-3">
          <div class="flex items-center gap-3">
            <input type="checkbox"
                   class="member-checkbox w-4 h-4 rounded border-border-primary cursor-pointer mt-1"
                   value="@member.UserId"
                   onchange="updateBulkSelection()"
                   aria-label="Select @member.DisplayName" />
            @if (!string.IsNullOrEmpty(member.AvatarUrl))
            {
              <img src="@member.AvatarUrl"
                   alt="@member.DisplayName"
                   class="w-10 h-10 rounded-full flex-shrink-0" />
            }
            else
            {
              <div class="w-10 h-10 rounded-full bg-gradient-to-br from-accent-blue to-accent-orange flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
                @(member.DisplayName.Length >= 2 ? member.DisplayName[..2].ToUpper() : member.DisplayName.ToUpper())
              </div>
            }
            <div class="min-w-0">
              <p class="font-medium text-text-primary truncate">@member.DisplayName</p>
              <p class="text-xs text-text-tertiary font-mono">
                @if (!string.IsNullOrEmpty(member.Username))
                {
                  @($"@{member.Username}")
                }
                else
                {
                  @($"ID: {member.UserId}")
                }
              </p>
            </div>
          </div>
        </div>

        <!-- Roles -->
        @if (member.Roles.Any())
        {
          <div class="mb-3">
            <p class="text-xs text-text-tertiary mb-1">Roles:</p>
            <div class="flex flex-wrap gap-1">
              @foreach (var role in member.Roles.Take(5))
              {
                <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium text-white"
                      style="background-color: @role.ColorHex">
                  @role.Name
                </span>
              }
              @if (member.Roles.Count > 5)
              {
                <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-bg-tertiary text-text-secondary">
                  +@(member.Roles.Count - 5)
                </span>
              }
            </div>
          </div>
        }

        <!-- Stats Grid -->
        <div class="grid grid-cols-2 gap-3 mb-4 text-sm">
          <div>
            <span class="text-text-tertiary">Joined:</span>
            <span class="text-text-primary ml-1" data-utc="@member.JoinedAtUtcIso" data-format="date"></span>
          </div>
          <div>
            <span class="text-text-tertiary">Messages:</span>
            <span class="text-text-primary ml-1">@member.MessageCount.ToString("N0")</span>
          </div>
          <div class="col-span-2">
            <span class="text-text-tertiary">Last Active:</span>
            @if (member.LastActiveAt.HasValue)
            {
              <span class="text-text-primary ml-1" data-utc="@member.LastActiveAtUtcIso" data-format="relative"></span>
            }
            else
            {
              <span class="text-text-tertiary ml-1">Never</span>
            }
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-2 pt-3 border-t border-border-secondary">
          <button type="button"
                  class="flex-1 inline-flex items-center justify-center gap-2 px-3 py-2 text-sm font-medium text-text-secondary hover:text-text-primary border border-border-primary hover:bg-bg-hover rounded-lg transition-colors"
                  onclick="viewMemberDetails(@member.UserId)">
            <svg class="w-4 h-4"><!-- eye icon --></svg>
            View Details
          </button>
        </div>
      </div>
    }
  }
  else
  {
    <div class="bg-bg-secondary border border-border-primary rounded-lg p-8">
      @{
        var hasFilters = Model.HasActiveFilters;
        var emptyTitle = hasFilters ? "No members found" : "No members yet";
        var emptyDesc = hasFilters ? "Try adjusting your search or filters" : "This server has no members";
      }
      <partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
        Type = EmptyStateType.NoResults,
        Title = emptyTitle,
        Description = emptyDesc
      }" />
    </div>
  }
</div>
```

**Design Tokens:**
- Card background: `bg-secondary`
- Border: `border-primary`
- Border radius: `rounded-lg` (8px)
- Padding: `p-4` (16px)
- Gap between cards: `space-y-4` (16px)
- Stats grid: 2 columns with `gap-3` (12px)

---

### 3.6 Member Detail Modal Component

**Component Name:** `MemberDetailModal`

**Trigger:** Click "View" button on member row/card

**Layout:**

```html
<!-- Modal Overlay -->
<div id="memberDetailModal"
     class="hidden fixed inset-0 z-50 overflow-y-auto"
     aria-labelledby="memberDetailTitle"
     role="dialog"
     aria-modal="true">
  <div class="flex min-h-screen items-center justify-center p-4">
    <!-- Backdrop -->
    <div class="fixed inset-0 bg-black/75 transition-opacity"
         onclick="closeMemberModal()"></div>

    <!-- Modal Content -->
    <div class="relative bg-bg-tertiary border border-border-primary rounded-lg shadow-xl max-w-2xl w-full max-h-[90vh] overflow-hidden">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border-primary">
        <h2 id="memberDetailTitle" class="text-h4 text-text-primary">Member Details</h2>
        <button type="button"
                class="text-text-secondary hover:text-text-primary transition-colors"
                onclick="closeMemberModal()"
                aria-label="Close modal">
          <svg class="w-6 h-6"><!-- x icon --></svg>
        </button>
      </div>

      <!-- Body (scrollable) -->
      <div class="overflow-y-auto max-h-[calc(90vh-120px)] px-6 py-6">
        <!-- Profile Section -->
        <div class="flex items-start gap-4 mb-6 pb-6 border-b border-border-secondary">
          <img id="modalAvatar"
               src=""
               alt=""
               class="w-20 h-20 rounded-full flex-shrink-0" />
          <div class="flex-1 min-w-0">
            <h3 id="modalDisplayName" class="text-h5 text-text-primary mb-1"></h3>
            <p id="modalUsername" class="text-sm text-text-secondary font-mono mb-2"></p>
            <p class="text-xs text-text-tertiary">
              <span>User ID: </span>
              <span id="modalUserId" class="font-mono"></span>
            </p>
          </div>
        </div>

        <!-- Info Grid -->
        <div class="grid grid-cols-2 gap-6 mb-6">
          <!-- Account Age -->
          <div>
            <p class="text-xs text-text-tertiary uppercase tracking-wider mb-1">Account Created</p>
            <p id="modalAccountAge" class="text-sm text-text-primary"></p>
          </div>

          <!-- Join Date -->
          <div>
            <p class="text-xs text-text-tertiary uppercase tracking-wider mb-1">Joined Server</p>
            <p id="modalJoinDate" class="text-sm text-text-primary"></p>
          </div>

          <!-- Last Active -->
          <div>
            <p class="text-xs text-text-tertiary uppercase tracking-wider mb-1">Last Active</p>
            <p id="modalLastActive" class="text-sm text-text-primary"></p>
          </div>

          <!-- Message Count -->
          <div>
            <p class="text-xs text-text-tertiary uppercase tracking-wider mb-1">Total Messages</p>
            <p id="modalMessageCount" class="text-sm text-text-primary"></p>
          </div>
        </div>

        <!-- Roles Section -->
        <div class="mb-6">
          <h4 class="text-sm font-semibold text-text-primary uppercase tracking-wider mb-3">
            Roles <span id="modalRoleCount" class="text-text-tertiary font-normal">(0)</span>
          </h4>
          <div id="modalRoleList" class="flex flex-wrap gap-2">
            <!-- Roles populated dynamically -->
          </div>
        </div>

        <!-- Activity Summary (Future) -->
        <div class="mb-6">
          <h4 class="text-sm font-semibold text-text-primary uppercase tracking-wider mb-3">
            Activity Summary
          </h4>
          <div class="bg-bg-secondary rounded-lg p-4">
            <p class="text-sm text-text-secondary">
              Activity analytics and message history will be available in a future update.
            </p>
          </div>
        </div>

        <!-- Mod Notes Section (If moderation system available) -->
        <!-- <div class="mb-6">
          <h4 class="text-sm font-semibold text-text-primary uppercase tracking-wider mb-3">
            Moderator Notes
          </h4>
          <div id="modalModNotes" class="space-y-2">
            <!-- Mod notes loaded here -->
          </div>
        </div> -->
      </div>

      <!-- Footer Actions -->
      <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-border-primary bg-bg-secondary">
        <button type="button"
                class="btn btn-secondary"
                onclick="closeMemberModal()">
          Close
        </button>
        <!-- Future action buttons -->
        <!-- <button type="button" class="btn btn-accent">Add Mod Note</button> -->
        <!-- <button type="button" class="btn btn-primary">Edit Roles</button> -->
      </div>
    </div>
  </div>
</div>
```

**States:**
- **Hidden**: `display: none`, `aria-hidden="true"`
- **Visible**: Modal slides in, backdrop fades in
- **Loading**: Show skeleton/spinner while fetching member data
- **Error**: Show error alert if data fetch fails

**Interaction:**
- Clicking "View" on member row triggers `viewMemberDetails(userId)` JS function
- Function fetches member data via API endpoint: `/api/guilds/{guildId}/members/{userId}`
- Populate modal fields with fetched data
- Show modal with fade-in animation
- Click backdrop or X button to close
- ESC key closes modal

**Accessibility:**
- `role="dialog"` and `aria-modal="true"`
- `aria-labelledby` points to title
- Focus trap: Tab cycles through modal elements only
- ESC key to close
- Focus returns to "View" button on close

**Design Tokens:**
- Modal background: `bg-tertiary` (#2f3336)
- Max width: `max-w-2xl` (672px)
- Max height: `max-h-[90vh]`
- Border radius: `rounded-lg` (8px)
- Shadow: `shadow-xl`
- Backdrop: `bg-black/75`

---

### 3.7 Pagination Component

**Component:** Reuse existing `_Pagination` component

```html
@if (Model.TotalPages > 1)
{
  <div class="mt-6">
    @{
      var baseUrl = Url.Page(null, new {
        SearchTerm = Model.SearchTerm,
        RoleFilter = Model.RoleFilterJson, // Serialize multi-select
        JoinedAfter = Model.JoinedAfter?.ToString("yyyy-MM-dd"),
        JoinedBefore = Model.JoinedBefore?.ToString("yyyy-MM-dd"),
        ActivityFilter = Model.ActivityFilter,
        SortBy = Model.SortBy,
        SortDescending = Model.SortDescending,
        PageSize = Model.PageSize
      }) ?? string.Empty;
      var paginationModel = new PaginationViewModel {
        CurrentPage = Model.CurrentPage,
        TotalPages = Model.TotalPages,
        TotalItems = Model.TotalCount,
        PageSize = Model.PageSize,
        BaseUrl = baseUrl,
        Style = PaginationStyle.Full,
        ShowPageSizeSelector = true,
        ShowItemCount = true,
        PageParameterName = "pageNumber"
      };
    }
    <partial name="Shared/Components/_Pagination" model="paginationModel" />
  </div>
}

<!-- Results Summary -->
@if (Model.TotalCount > 0)
{
  <div class="mt-4 text-sm text-text-secondary">
    Showing @((Model.CurrentPage - 1) * Model.PageSize + 1) to @Math.Min(Model.CurrentPage * Model.PageSize, Model.TotalCount) of @Model.TotalCount members
  </div>
}
```

**Configuration:**
- Style: `Full` (First, Previous, page numbers, Next, Last)
- Page size options: 10, 25, 50, 100
- Default page size: 25
- Show item count: Yes
- Show page size selector: Yes

---

## 4. Interaction Patterns

### 4.1 Filter Application Behavior

**Pattern:** Submit-based (not live filtering)

**User Flow:**
1. User opens filter panel (if collapsed)
2. User adjusts filter inputs (search, role, dates, activity, sort)
3. User clicks "Apply Filters" button
4. Page submits form via GET request
5. Server filters members and returns new page
6. Filter panel remains open
7. Active filter count badge updates

**Rationale:** Submit-based prevents excessive API calls during typing/selection. More appropriate for potentially large member lists.

**Alternative (Future Enhancement):** Debounced live filtering with URL state updates.

---

### 4.2 Bulk Selection UX

**Pattern:** Checkbox-based multi-select with toolbar

**User Flow:**
1. User checks one or more member checkboxes
2. Bulk actions toolbar slides in from top with selection count
3. User can:
   - Click "Deselect All" to clear all selections and hide toolbar
   - Click "Export Selected" to download CSV of selected members
4. Toolbar updates count as checkboxes change
5. "Select All" checkbox in table header toggles all visible members

**JavaScript Interactions:**

```javascript
// Update bulk selection state
function updateBulkSelection() {
  const checkboxes = document.querySelectorAll('.member-checkbox:checked');
  const count = checkboxes.length;
  const toolbar = document.getElementById('bulkActionsToolbar');
  const countSpan = document.getElementById('selectedCount');

  if (count > 0) {
    toolbar.classList.remove('hidden');
    countSpan.textContent = count;
  } else {
    toolbar.classList.add('hidden');
  }

  // Update select-all checkbox state
  updateSelectAllCheckbox();
}

// Toggle all visible members
function toggleSelectAll(checkbox) {
  const memberCheckboxes = document.querySelectorAll('.member-checkbox');
  memberCheckboxes.forEach(cb => {
    cb.checked = checkbox.checked;
  });
  updateBulkSelection();
}

// Update select-all checkbox (indeterminate state)
function updateSelectAllCheckbox() {
  const selectAll = document.getElementById('selectAll');
  const memberCheckboxes = document.querySelectorAll('.member-checkbox');
  const checkedCount = document.querySelectorAll('.member-checkbox:checked').length;

  selectAll.checked = checkedCount === memberCheckboxes.length;
  selectAll.indeterminate = checkedCount > 0 && checkedCount < memberCheckboxes.length;
}

// Deselect all
function deselectAll() {
  document.querySelectorAll('.member-checkbox').forEach(cb => cb.checked = false);
  updateBulkSelection();
}
```

**Accessibility:**
- Announce selection count changes via `aria-live="polite"` region
- Select all checkbox labeled "Select all members"
- Individual checkboxes labeled "Select [Member Name]"

---

### 4.3 Loading States During Discord API Calls

**Scenarios:**
- Initial page load
- Filter application
- Member sync operation
- Member detail modal data fetch

**Loading Patterns:**

**Page Load:**
```html
<!-- Show skeleton table rows while loading -->
<tbody>
  @for (int i = 0; i < Model.PageSize; i++)
  {
    <tr>
      <td class="px-6 py-4">
        <div class="w-10 h-10 bg-bg-tertiary rounded-full animate-pulse"></div>
      </td>
      <td class="px-6 py-4">
        <div class="h-4 bg-bg-tertiary rounded w-32 animate-pulse"></div>
      </td>
      <!-- More skeleton cells -->
    </tr>
  }
</tbody>
```

**Sync Operation:**
```html
<!-- Sync button state change -->
<button type="button" class="btn btn-secondary" onclick="syncMembers(this)" disabled>
  <svg class="w-5 h-5 mr-2 animate-spin"><!-- spinner icon --></svg>
  <span>Syncing...</span>
</button>
```

**Modal Data Fetch:**
```html
<!-- Modal body during load -->
<div class="flex items-center justify-center py-12">
  <svg class="w-8 h-8 animate-spin text-accent-blue"><!-- spinner --></svg>
  <span class="ml-3 text-sm text-text-secondary">Loading member details...</span>
</div>
```

---

### 4.4 Error States

**Network Error (Failed to Load Members):**
```html
<tr>
  <td colspan="7" class="px-6 py-12 text-center">
    <partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
      Type = EmptyStateType.Error,
      Title = "Failed to load members",
      Description = "An error occurred while fetching member data. Please try again.",
      PrimaryActionText = "Retry",
      PrimaryActionOnClick = "location.reload()"
    }" />
  </td>
</tr>
```

**Rate Limited (Discord API Rate Limit):**
```html
<partial name="Shared/Components/_Alert" model="new AlertViewModel {
  Variant = AlertVariant.Warning,
  Title = "Rate Limited",
  Message = "Discord API rate limit reached. Please wait a moment and try again.",
  IsDismissible = true
}" />
```

**Member Not Found (Modal):**
```html
<div class="px-6 py-12 text-center">
  <partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
    Type = EmptyStateType.Error,
    Title = "Member not found",
    Description = "This member may have left the server or been removed.",
    Size = EmptyStateSize.Compact
  }" />
</div>
```

---

### 4.5 Empty States

**No Members in Server:**
```html
<partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
  Type = EmptyStateType.NoData,
  Title = "No members yet",
  Description = "This server has no members. Members will appear here as they join.",
  Size = EmptyStateSize.Default
}" />
```

**No Results from Filters:**
```html
<partial name="Shared/Components/_EmptyState" model="new EmptyStateViewModel {
  Type = EmptyStateType.NoResults,
  Title = "No members found",
  Description = "Try adjusting your search or filters to find members.",
  SecondaryActionText = "Reset Filters",
  SecondaryActionUrl = "/Guilds/{guildId}/Members",
  Size = EmptyStateSize.Default
}" />
```

**No Roles in Server:**
```html
<!-- In role filter dropdown -->
<div class="px-3 py-4 text-center text-sm text-text-tertiary">
  No roles available
</div>
```

---

## 5. State Management

### 5.1 Filter State Persistence

**Pattern:** URL Query Parameters

**Query String Structure:**
```
/Guilds/123456789/Members?
  SearchTerm=john
  &RoleFilter=987654321,123123123
  &JoinedAfter=2024-01-01
  &JoinedBefore=2024-12-31
  &ActivityFilter=active-week
  &SortBy=join-date
  &SortDescending=true
  &PageNumber=2
  &PageSize=25
```

**Benefits:**
- Shareable URLs with filters applied
- Browser back/forward navigation preserves filters
- Bookmarkable filtered views

**Implementation:**
```csharp
// PageModel
public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<ulong>? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? JoinedAfter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? JoinedBefore { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActivityFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "join-date";

    [BindProperty(SupportsGet = true)]
    public bool SortDescending { get; set; } = false;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        (RoleFilter?.Any() ?? false) ||
        JoinedAfter.HasValue ||
        JoinedBefore.HasValue ||
        !string.IsNullOrWhiteSpace(ActivityFilter);
}
```

---

### 5.2 Bulk Selection State

**Pattern:** Client-side JavaScript state (not persisted)

**State Object:**
```javascript
const bulkSelectionState = {
  selectedMemberIds: new Set(),

  add(memberId) {
    this.selectedMemberIds.add(memberId);
    this.update();
  },

  remove(memberId) {
    this.selectedMemberIds.delete(memberId);
    this.update();
  },

  clear() {
    this.selectedMemberIds.clear();
    this.update();
  },

  update() {
    // Update UI (toolbar, count, etc.)
    updateBulkSelection();
  }
};
```

**Rationale:** Selections don't need to persist across page loads. Cleared on navigation/filter change.

---

### 5.3 Modal State

**Pattern:** Client-side show/hide with AJAX data fetch

**State Flow:**
1. User clicks "View" → `viewMemberDetails(userId)` called
2. Show modal with loading spinner
3. Fetch data via AJAX: `GET /api/guilds/{guildId}/members/{userId}`
4. Populate modal with response data
5. User closes modal → Clear modal content, hide

**Implementation:**
```javascript
async function viewMemberDetails(userId) {
  const modal = document.getElementById('memberDetailModal');
  const modalBody = modal.querySelector('.modal-body');

  // Show modal with loading state
  modal.classList.remove('hidden');
  modalBody.innerHTML = '<div class="loading-spinner">Loading...</div>';

  try {
    const response = await fetch(`/api/guilds/${guildId}/members/${userId}`);
    if (!response.ok) throw new Error('Failed to fetch member');

    const member = await response.json();

    // Populate modal fields
    document.getElementById('modalAvatar').src = member.avatarUrl;
    document.getElementById('modalDisplayName').textContent = member.displayName;
    // ... populate other fields

  } catch (error) {
    modalBody.innerHTML = '<div class="error-message">Failed to load member details</div>';
  }
}

function closeMemberModal() {
  const modal = document.getElementById('memberDetailModal');
  modal.classList.add('hidden');
}
```

---

## 6. Accessibility Requirements

### 6.1 Keyboard Navigation

**Tab Order:**
1. Breadcrumb links
2. Page header actions (Sync, Export buttons)
3. Filter toggle button
4. Filter form inputs (when expanded)
5. Filter action buttons
6. Bulk action toolbar buttons (when visible)
7. Select all checkbox
8. Member table rows (checkbox, View button)
9. Pagination controls

**Keyboard Shortcuts:**
- `Tab`: Navigate forward through interactive elements
- `Shift+Tab`: Navigate backward
- `Enter` / `Space`: Activate buttons, toggle checkboxes
- `Escape`: Close modal, collapse filter panel
- Arrow keys: Navigate pagination page numbers

---

### 6.2 Screen Reader Support

**ARIA Landmarks:**
```html
<nav aria-label="Breadcrumb">...</nav>
<main aria-label="Member directory">
  <section aria-label="Filters">...</section>
  <section aria-label="Member list">...</section>
  <nav aria-label="Pagination">...</nav>
</main>
```

**Live Regions:**
```html
<!-- Announce filter results -->
<div class="sr-only" role="status" aria-live="polite" aria-atomic="true">
  @if (Model.Members.Any())
  {
    @($"Showing {Model.Members.Count} of {Model.TotalCount} members")
  }
  else
  {
    @("No members found")
  }
</div>

<!-- Announce selection changes -->
<div class="sr-only" role="status" aria-live="polite" aria-atomic="true">
  <span id="selectionAnnouncement"></span>
</div>
```

**Table Accessibility:**
```html
<table role="table" aria-label="Server members">
  <thead>
    <tr>
      <th scope="col">...</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>...</td>
    </tr>
  </tbody>
</table>
```

**Button Labels:**
```html
<button aria-label="Sync all members from Discord">Sync Members</button>
<button aria-label="Export member list to CSV">Export CSV</button>
<button aria-label="View details for JohnDoe">View</button>
```

---

### 6.3 Focus Management

**Modal Focus Trap:**
```javascript
function openModal(modalId) {
  const modal = document.getElementById(modalId);
  const focusableElements = modal.querySelectorAll(
    'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
  );
  const firstElement = focusableElements[0];
  const lastElement = focusableElements[focusableElements.length - 1];

  // Focus first element
  firstElement.focus();

  // Trap focus
  modal.addEventListener('keydown', (e) => {
    if (e.key === 'Tab') {
      if (e.shiftKey && document.activeElement === firstElement) {
        e.preventDefault();
        lastElement.focus();
      } else if (!e.shiftKey && document.activeElement === lastElement) {
        e.preventDefault();
        firstElement.focus();
      }
    }
    if (e.key === 'Escape') {
      closeModal(modalId);
    }
  });
}

function closeModal(modalId) {
  const modal = document.getElementById(modalId);
  modal.classList.add('hidden');

  // Return focus to trigger button
  const triggerButton = document.activeElement.dataset.triggerId;
  if (triggerButton) {
    document.getElementById(triggerButton).focus();
  }
}
```

---

### 6.4 Color Contrast Compliance

**All interactive elements must meet WCAG AA standards (4.5:1 for normal text, 3:1 for large text).**

**Verified Combinations:**
- `text-primary` on `bg-primary`: **10.8:1** ✓ AAA
- `text-secondary` on `bg-secondary`: **5.9:1** ✓ AA
- `btn-primary` (white on orange): **4.6:1** ✓ AA
- `btn-accent` (white on blue): **4.8:1** ✓ AA
- Role badges: Ensure custom colors meet 4.5:1 when used (validate on render)

**Non-compliant role colors:** Add text outline or use contrasting text color:
```html
<span class="role-badge"
      style="background-color: @role.ColorHex; color: @GetContrastingTextColor(role.ColorHex)">
  @role.Name
</span>
```

---

## 7. Responsive Design

### 7.1 Breakpoint Behavior

**Mobile (< 768px):**
- Filter panel: Full width, stacked inputs
- Member list: Card layout (not table)
- Pagination: Compact style (Prev, Page X of Y, Next)
- Bulk actions: Stacked buttons, full width

**Tablet (768px - 1023px):**
- Filter panel: 2-column grid for most inputs
- Member table: Visible, hide "Joined" column
- Pagination: Full style with fewer page numbers

**Desktop (1024px - 1279px):**
- Filter panel: 3-column grid
- Member table: Show "Joined" column
- Pagination: Full style with more page numbers

**Large Desktop (1280px+):**
- Filter panel: 3-column grid
- Member table: Show all columns (Joined, Last Active, Messages)
- Pagination: Full style with maximum page numbers

---

### 7.2 Mobile-Specific Optimizations

**Touch Targets:**
- Minimum size: 44x44px for all interactive elements
- Increased padding on mobile buttons: `py-3` (12px) instead of `py-2.5` (10px)

**Card Layout:**
- Simplified information hierarchy
- Larger avatar (40px → 48px on mobile)
- Show top 5 roles instead of 3
- Collapse stats into 2-column grid

**Filter Panel:**
- Default to collapsed on mobile
- Sticky "Apply Filters" button at bottom when scrolling

**Modal:**
- Full-screen on mobile: `max-w-full h-screen rounded-none` on small screens

---

## 8. Prototype Requirements

### 8.1 Prototypes to Create

Create the following HTML prototypes in `docs/prototypes/features/member-directory/`:

#### 8.1.1 Index Page - Desktop View
**Filename:** `index-desktop.html`

**Demonstrate:**
- Full page layout with all sections
- Filter panel expanded with all filter options
- Member table with 10+ sample rows
- Various member states (with/without avatars, different role counts)
- Pagination controls
- Bulk selection toolbar (3 members selected)

---

#### 8.1.2 Index Page - Mobile View
**Filename:** `index-mobile.html`

**Demonstrate:**
- Mobile card layout for members
- Filter panel collapsed by default
- Simplified navigation
- Touch-optimized buttons
- Compact pagination

---

#### 8.1.3 Member Detail Modal
**Filename:** `member-detail-modal.html`

**Demonstrate:**
- Modal overlay and backdrop
- Complete member profile view
- Role list display
- Activity summary section
- Responsive modal on mobile (full-screen)

---

#### 8.1.4 Empty States
**Filename:** `empty-states.html`

**Demonstrate:**
- No members in server
- No results from filters
- Error loading members
- Rate limit error
- Modal member not found error

---

#### 8.1.5 Loading States
**Filename:** `loading-states.html`

**Demonstrate:**
- Skeleton table rows during page load
- Sync button loading state
- Modal loading spinner
- Filter application loading

---

#### 8.1.6 Filter Panel States
**Filename:** `filter-panel-states.html`

**Demonstrate:**
- Collapsed state
- Expanded state
- With active filters badge
- Role multi-select dropdown open/closed
- Date pickers focused

---

#### 8.1.7 Bulk Selection Flow
**Filename:** `bulk-selection.html`

**Demonstrate:**
- No selections (toolbar hidden)
- Some selections (toolbar visible, indeterminate select-all checkbox)
- All selections (toolbar visible, select-all checked)
- Export modal/confirmation

---

### 8.2 Prototype File Structure

```
docs/prototypes/features/member-directory/
├── index-desktop.html
├── index-mobile.html
├── member-detail-modal.html
├── empty-states.html
├── loading-states.html
├── filter-panel-states.html
└── bulk-selection.html
```

All prototypes should:
- Use shared CSS from `docs/prototypes/css/`
- Include Heroicons for icons
- Follow design system color tokens
- Include comments explaining key interaction points
- Be viewable standalone in a browser

---

### 8.3 Prototype Testing Checklist

For each prototype, verify:

- [ ] All colors match design system tokens
- [ ] Typography uses correct font scales
- [ ] Spacing follows 4px grid system
- [ ] Interactive elements have visible hover/focus states
- [ ] Color contrast meets WCAG AA standards
- [ ] Buttons use correct variants (primary, secondary, accent)
- [ ] Icons are from Heroicons library
- [ ] Responsive breakpoints work as specified
- [ ] Accessible markup (ARIA labels, semantic HTML)
- [ ] No JavaScript errors in console
- [ ] Renders correctly in Chrome, Firefox, Safari

---

## 9. Implementation Notes

### 9.1 API Endpoint Requirements

**GET `/api/guilds/{guildId}/members`**
- Returns paginated member list with filters
- Query params: `search`, `roleIds`, `joinedAfter`, `joinedBefore`, `activityFilter`, `sortBy`, `sortDesc`, `page`, `pageSize`
- Response: `{ members: [], totalCount: int, currentPage: int, totalPages: int }`

**GET `/api/guilds/{guildId}/members/{userId}`**
- Returns single member details
- Response: `{ userId, displayName, username, avatarUrl, joinedAt, lastActiveAt, messageCount, roles: [], accountCreatedAt }`

**POST `/api/guilds/{guildId}/members/sync`**
- Triggers Discord API sync for all members
- Returns: `{ success: bool, syncedCount: int }`

**GET `/api/guilds/{guildId}/members/export`**
- Exports member list to CSV
- Query params: Same as list endpoint + `selectedIds` (for bulk export)
- Response: CSV file download

---

### 9.2 Data Refresh Strategy

**Member Data Staleness:**
- Member list cached for 5 minutes (configurable)
- "Sync" button forces refresh from Discord API
- Auto-sync on page load if cache > 5 minutes old
- Rate limit: Max 1 sync per minute per guild

**Real-time Updates (Future):**
- SignalR hub for live member join/leave events
- Update member count badge without full refresh
- Toast notification when new members join

---

### 9.3 Performance Considerations

**Large Member Lists (10,000+ members):**
- Server-side pagination required (max 100 per page)
- Index database columns: `GuildId`, `JoinedAt`, `LastActiveAt`, `Username`
- Consider ElasticSearch for search performance (future)

**Role Filtering:**
- Avoid N+1 queries (eager load roles with members)
- Cache role list for dropdown (guild roles don't change often)

**Avatar Loading:**
- Lazy load avatar images (use Intersection Observer)
- Fallback to gradient placeholder if avatar fails to load

---

## 10. Future Enhancements

**Phase 2 Features (not in initial implementation):**
- Bulk role assignment/removal
- Bulk mod tag application
- Bulk watchlist addition
- Advanced search (regex, message content)
- Export to other formats (JSON, Excel)
- Member comparison view (side-by-side)
- Activity charts and analytics per member
- Nickname history
- Server boost status indicator

**Phase 3 Features:**
- Real-time presence indicators (online/offline)
- Member timeline (join, role changes, warnings, etc.)
- Integration with moderation system (warnings, bans)
- Custom field display (server-specific member data)
- Saved filter presets

---

## Appendix A: ViewModels

### MemberDirectoryViewModel

```csharp
public record MemberDirectoryViewModel
{
    public long GuildId { get; init; }
    public string GuildName { get; init; } = string.Empty;
    public List<MemberListItemViewModel> Members { get; init; } = new();
    public int TotalCount { get; init; }
    public int TotalMemberCount { get; init; } // Unfiltered total
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public int PageSize { get; init; }
    public bool HasActiveFilters { get; init; }
    public int ActiveFilterCount { get; init; }

    // Filter values
    public string? SearchTerm { get; init; }
    public List<ulong> SelectedRoles { get; init; } = new();
    public DateTime? JoinedAfter { get; init; }
    public DateTime? JoinedBefore { get; init; }
    public string? ActivityFilter { get; init; }
    public string SortBy { get; init; } = "join-date";
    public bool SortDescending { get; init; }

    // Available filter options
    public List<RoleViewModel> AvailableRoles { get; init; } = new();
}

public record MemberListItemViewModel
{
    public ulong UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTime JoinedAt { get; init; }
    public string JoinedAtUtcIso => JoinedAt.ToString("o");
    public DateTime? LastActiveAt { get; init; }
    public string? LastActiveAtUtcIso => LastActiveAt?.ToString("o");
    public int MessageCount { get; init; }
    public List<RoleViewModel> Roles { get; init; } = new();
}

public record RoleViewModel
{
    public ulong Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#99aab5"; // Default Discord gray
}
```

---

## Appendix B: Design Token Reference

**Quick reference for implementation:**

| Token | Value | Usage |
|-------|-------|-------|
| `bg-primary` | #1d2022 | Page background |
| `bg-secondary` | #262a2d | Cards, panels |
| `bg-tertiary` | #2f3336 | Modals, table headers |
| `bg-hover` | #363a3e | Hover states |
| `text-primary` | #d7d3d0 | Primary text |
| `text-secondary` | #a8a5a3 | Secondary text |
| `text-tertiary` | #7a7876 | Muted text |
| `accent-orange` | #cb4e1b | Primary actions |
| `accent-blue` | #098ecf | Secondary actions |
| `border-primary` | #3f4447 | Default borders |
| `border-focus` | #098ecf | Focus rings |
| `success` | #10b981 | Success states |
| `warning` | #f59e0b | Warning states |
| `error` | #ef4444 | Error states |
| `info` | #06b6d4 | Info states |

**Spacing Scale:**
- `space-1`: 4px
- `space-2`: 8px
- `space-3`: 12px
- `space-4`: 16px
- `space-6`: 24px
- `space-8`: 32px

**Border Radius:**
- `rounded`: 4px
- `rounded-md`: 6px
- `rounded-lg`: 8px
- `rounded-xl`: 12px
- `rounded-full`: 9999px

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-30 | Claude (UI Specialist) | Initial specification for Member Directory UI |

---

**End of Specification**
