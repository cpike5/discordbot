# Audit Logs Table Improvements - Design Specification

**Version:** 1.0
**Created:** 2025-12-29
**Status:** Draft
**Target:** `/Admin/AuditLogs` page

---

## Executive Summary

This specification addresses critical usability issues identified in the Audit Logs page UI critique. The improvements focus on eliminating horizontal scrolling, improving actor display with meaningful usernames instead of GUIDs, and implementing a responsive mobile card layout to match the pattern established in the CommandLogs page.

## Issues Addressed

### Critical (P0)
1. **Horizontal scrolling** - Table requires horizontal scrolling even on 1280px desktop viewports due to oversized column padding (`px-6`) and inefficient column widths
2. **Actor GUIDs** - Actor column shows raw GUIDs like `12208c2b-db81-4683-9095-ee82d82b5a9e` instead of usernames
3. **Missing mobile view** - No responsive card layout for small screens (unlike CommandLogs page)

### Major (P1)
4. **"Unknown" actors** - Some entries show "Unknown" which provides insufficient audit context
5. **Inconsistent column ordering** - Current order doesn't follow natural narrative flow

### Minor
6. **Redundant Details column** - Details already shown in expandable row, wastes 320px of horizontal space
7. **Small touch targets** - Chevron expand button is 24x24px (should be 44x44px minimum)
8. **Inconsistent null display** - Guild column shows dash "-" for null values

---

## 1. Desktop Table Layout Specification

### 1.1 Column Layout

**New Column Order** (narrative flow: When → What → Who → Where → Details):

| Column | Min Width | Max Width | Content | Padding | Justification |
|--------|-----------|-----------|---------|---------|---------------|
| Expand Icon | 48px | 48px | Chevron button | `px-2` (8px) | Larger touch target |
| Timestamp | 160px | 180px | Local datetime | `px-4` (16px) | When action occurred |
| Action | 140px | 160px | Badge component | `px-4` (16px) | What happened |
| Actor | 180px | 240px | Avatar + name/ID | `px-4` (16px) | Who performed it |
| Target | 120px | 140px | Target type | `px-4` (16px) | What was affected |
| Guild | 140px | 180px | Guild name or "System" | `px-4` (16px) | Where it occurred |

**Total width calculation:**
- Minimum: 48 + 160 + 140 + 180 + 120 + 140 = **788px** (fits 1024px viewport)
- Maximum: 48 + 180 + 160 + 240 + 140 + 180 = **948px** (fits 1280px viewport comfortably)

**Changes from current:**
- Remove **Details** column (redundant with expandable row)
- Reduce padding from `px-6` (24px) to `px-4` (16px) on all columns except expand icon
- Reorder columns to follow narrative flow: Timestamp → Action → Actor → Target → Guild
- Increase expand button column from 40px to 48px for better touch targets

### 1.2 Responsive Column Visibility

**Breakpoint behavior:**

```html
<!-- Mobile (< 768px): Hide table, show cards -->
<div class="hidden md:block">
  <!-- Desktop table -->
</div>
<div class="md:hidden">
  <!-- Mobile cards -->
</div>

<!-- Tablet (768px - 1024px): Hide Guild column if needed -->
<th class="hidden lg:table-cell">Guild</th>

<!-- Desktop (1024px+): Show all columns -->
```

**Column priority for responsive display:**
1. Essential (always visible): Timestamp, Action, Actor
2. Important (hide on tablet): Target, Guild
3. Context (expandable row only): Details, metadata

### 1.3 Table Header Specification

```html
<thead class="bg-bg-tertiary">
  <tr>
    <!-- Expand Icon Column -->
    <th scope="col" class="w-12 px-2 py-3"></th>

    <!-- Timestamp -->
    <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase tracking-wider">
      Timestamp
    </th>

    <!-- Action -->
    <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase tracking-wider">
      Action
    </th>

    <!-- Actor -->
    <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase tracking-wider">
      Actor
    </th>

    <!-- Target -->
    <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase tracking-wider">
      Target
    </th>

    <!-- Guild -->
    <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase tracking-wider hidden lg:table-cell">
      Guild
    </th>
  </tr>
</thead>
```

---

## 2. Actor Display Specification

### 2.1 Actor Display Logic

The Actor column must intelligently display the most useful information available. Priority order:

```
1. User with display name → Show display name with avatar initials
2. User with GUID only → Show "User" + truncated GUID with link to user details
3. System actor → Show "System" with system icon
4. Bot actor → Show "Bot" with bot icon
5. Null/Unknown → Show "Unknown" with question mark icon
```

### 2.2 Actor Display Patterns

#### Pattern 1: User with Display Name

**When:** `ActorDisplayName` is not null/empty AND `ActorType` is User

**Display:**
```html
<div class="flex items-center gap-2">
  <div class="h-8 w-8 rounded-full bg-accent-blue text-white flex items-center justify-center">
    <span class="font-medium text-xs">JD</span>
  </div>
  <span class="text-sm text-text-primary">John Doe</span>
</div>
```

**Avatar initials logic:**
- Two-word name: First letter of first word + first letter of second word (e.g., "John Doe" → "JD")
- One-word name: First two letters (e.g., "Admin" → "AD")
- Fallback: First letter only (e.g., "X" → "X")

#### Pattern 2: User with GUID Only

**When:** `ActorDisplayName` is null/empty AND `ActorId` is a GUID AND `ActorType` is User

**Display:**
```html
<div class="flex items-center gap-2">
  <div class="h-8 w-8 rounded-full bg-accent-blue text-white flex items-center justify-center">
    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
    </svg>
  </div>
  <div class="flex flex-col">
    <a href="/Admin/Users/Details?id=@ActorId" class="text-sm text-accent-blue hover:text-accent-blue-hover transition-colors">
      User
    </a>
    <span class="text-xs font-mono text-text-tertiary">@ActorId.Substring(0, 8)...</span>
  </div>
</div>
```

**Truncation:** Show first 8 characters of GUID + ellipsis (e.g., `12208c2b...`)

**Link behavior:** Clicking navigates to `/Admin/Users/Details?id={ActorId}` to view full user profile

#### Pattern 3: System Actor

**When:** `ActorType` is System

**Display:**
```html
<div class="flex items-center gap-2">
  <div class="h-8 w-8 rounded-full bg-bg-tertiary text-text-secondary flex items-center justify-center">
    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" />
    </svg>
  </div>
  <span class="text-sm text-text-secondary">System</span>
</div>
```

**Icon:** Heroicons "cpu-chip" (outline)

#### Pattern 4: Bot Actor

**When:** `ActorType` is Bot

**Display:**
```html
<div class="flex items-center gap-2">
  <div class="h-8 w-8 rounded-full bg-bg-tertiary text-text-secondary flex items-center justify-center">
    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" />
    </svg>
  </div>
  <span class="text-sm text-text-secondary">Bot</span>
</div>
```

**Icon:** Heroicons "cpu-chip" (outline)

#### Pattern 5: Unknown Actor

**When:** `ActorDisplayName` is null AND `ActorId` is null

**Display:**
```html
<div class="flex items-center gap-2">
  <div class="h-8 w-8 rounded-full bg-bg-tertiary text-text-tertiary flex items-center justify-center">
    <span class="font-medium text-xs">?</span>
  </div>
  <span class="text-sm text-text-tertiary">Unknown</span>
</div>
```

**Visual treatment:** Dimmed colors to indicate incomplete data

### 2.3 Avatar Color Coding

**User actors:** `bg-accent-blue` (#098ecf) - Consistent with user-initiated actions
**System/Bot actors:** `bg-bg-tertiary` (#2f3336) - Neutral, non-user color
**Unknown actors:** `bg-bg-tertiary` with `text-text-tertiary` - Visually muted

**Accessibility:** All combinations meet WCAG AA contrast requirements (4.5:1 minimum)

---

## 3. Mobile Responsive Card Layout

### 3.1 Mobile Layout Strategy

Match the proven pattern from CommandLogs page (`/CommandLogs/Index.cshtml` lines 316-392):
- Display cards on screens < 768px (`md:hidden`)
- Show desktop table on screens ≥ 768px (`hidden md:block`)
- Each card represents one audit log entry with expandable details

### 3.2 Mobile Card Structure

```html
<div class="md:hidden space-y-4">
  @if (Model.ViewModel.Logs.Any())
  {
    @foreach (var log in Model.ViewModel.Logs)
    {
      <div class="bg-bg-secondary border border-border-primary rounded-lg p-4">

        <!-- Header: Action + Timestamp -->
        <div class="flex items-start justify-between mb-3">
          <div>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold @log.ActionBadgeClass">
              @log.Action
            </span>
            <p class="text-xs text-text-tertiary mt-1" data-utc="@log.TimestampUtcIso" data-format="datetime-seconds">
            </p>
          </div>
        </div>

        <!-- Actor (prominent) -->
        <div class="mb-3">
          <span class="text-xs text-text-tertiary">Actor:</span>
          <div class="mt-1">
            <!-- Actor display pattern from section 2.2 -->
          </div>
        </div>

        <!-- Metadata Grid -->
        <div class="grid grid-cols-2 gap-3 text-sm">
          <div>
            <span class="text-text-tertiary">Target:</span>
            <span class="text-text-primary ml-1">@log.TargetType</span>
          </div>
          <div>
            <span class="text-text-tertiary">Guild:</span>
            <span class="text-text-primary ml-1">@(log.GuildName ?? "System")</span>
          </div>
        </div>

        <!-- Details Summary (if available) -->
        @if (log.HasDetails)
        {
          <div class="mt-3 pt-3 border-t border-border-secondary">
            <p class="text-xs text-text-tertiary mb-1">Details:</p>
            <p class="text-xs text-text-secondary line-clamp-2">@log.DetailsSummary</p>
          </div>
        }

        <!-- View Details Link -->
        <div class="mt-3 pt-3 border-t border-border-secondary">
          <a asp-page="/Admin/AuditLogs/Details" asp-route-id="@log.Id"
             class="text-accent-blue hover:text-accent-blue-hover text-sm font-medium transition-colors inline-flex items-center gap-1">
            View Details
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
            </svg>
          </a>
        </div>

      </div>
    }
  }
  else
  {
    <!-- Empty state -->
    <div class="bg-bg-secondary border border-border-primary rounded-lg p-8">
      @{
        var hasFilters = Model.ViewModel.Filters.HasActiveFilters;
        var emptyTitle = hasFilters ? "No logs found" : "No audit logs yet";
        var emptyDesc = hasFilters ? "Try adjusting your filters" : "Audit logs will appear here as actions are performed";
      }
      <partial name="Shared/Components/_EmptyState" model="new DiscordBot.Bot.ViewModels.Components.EmptyStateViewModel {
        Type = DiscordBot.Bot.ViewModels.Components.EmptyStateType.NoResults,
        Title = emptyTitle,
        Description = emptyDesc
      }" />
    </div>
  }
</div>
```

### 3.3 Mobile Card Visual Hierarchy

**Priority 1 (Most prominent):**
- Action badge (colored, semantic)
- Timestamp (when it happened)

**Priority 2 (Secondary information):**
- Actor (who did it) - larger display with avatar
- Target type (what was affected)
- Guild (where it happened)

**Priority 3 (Expandable/optional):**
- Details summary (truncated to 2 lines with `line-clamp-2`)
- Full details link

### 3.4 Mobile Touch Targets

All interactive elements must meet 44x44px minimum touch target size:

| Element | Size | Notes |
|---------|------|-------|
| View Details link | Full width of card, min-height 44px | Entire bottom section is tappable |
| Actor link (for GUIDs) | Inline link with 44px height padding | Adequate vertical padding |
| Expand button (future) | 44x44px minimum | If inline expansion added |

---

## 4. Accessibility Improvements

### 4.1 Touch Target Sizes

**Current issue:** Chevron expand button is 24x24px (too small for touch)

**Fix:** Increase to 44x44px minimum (WCAG 2.1 AAA standard)

```html
<!-- OLD (24x24px total) -->
<button type="button" class="expand-btn p-1 hover:bg-bg-hover rounded transition-colors">
  <svg class="chevron-icon w-4 h-4 text-text-secondary">...</svg>
</button>

<!-- NEW (44x44px total) -->
<button type="button" class="expand-btn w-11 h-11 flex items-center justify-center hover:bg-bg-hover rounded transition-colors">
  <svg class="chevron-icon w-5 h-5 text-text-secondary">...</svg>
</button>
```

**Button dimensions:**
- Width: `w-11` (44px)
- Height: `h-11` (44px)
- Icon: `w-5 h-5` (20px) for better visibility
- Centering: `flex items-center justify-center`

### 4.2 ARIA Labels and Semantic Markup

#### Expand Button

```html
<button type="button"
        class="expand-btn w-11 h-11 flex items-center justify-center hover:bg-bg-hover rounded transition-colors"
        aria-label="Expand audit log details for @log.Action at @log.Timestamp"
        aria-expanded="false"
        aria-controls="details-@log.Id">
  <svg class="chevron-icon w-5 h-5 text-text-secondary" aria-hidden="true">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
  </svg>
</button>
```

**Attributes:**
- `aria-label`: Descriptive label identifying the specific row
- `aria-expanded`: Boolean state (false = collapsed, true = expanded)
- `aria-controls`: ID of the expandable region
- `aria-hidden="true"` on icon SVG (icon is decorative)

#### Expandable Content Row

```html
<tr class="expand-content-row hidden"
    data-parent-id="@log.Id"
    id="details-@log.Id"
    role="region"
    aria-labelledby="action-@log.Id">
  <td colspan="6" class="px-4 py-0">
    <div class="expand-content bg-bg-primary border-l-2 @log.ActionBorderClass pl-4 py-3 my-2 rounded-r-md">
      <!-- Details content -->
    </div>
  </td>
</tr>
```

**Attributes:**
- `role="region"`: Identifies expandable section
- `aria-labelledby`: References the action badge for context
- `id`: Matches `aria-controls` on expand button

#### Table Headers

```html
<th scope="col" class="px-4 py-3 text-left...">
  Timestamp
</th>
```

**All column headers must have `scope="col"` for screen reader navigation**

### 4.3 Keyboard Navigation

**Requirements:**
1. Expand buttons must be keyboard accessible (already focusable with `<button>`)
2. Enter and Space keys must toggle expansion (current implementation is correct)
3. Tab order must be logical: Filter controls → Table expand buttons → Pagination
4. Focus visible ring must be clear on all interactive elements

**Focus ring specification:**
```css
.expand-btn:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}
```

**Keyboard event handler (existing, keep as-is):**
```javascript
document.querySelectorAll('.expand-btn').forEach(btn => {
  btn.addEventListener('keydown', function(e) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      this.click();
    }
  });
});
```

### 4.4 Color Contrast Requirements

All text/background combinations must meet WCAG AA standards (4.5:1 for normal text, 3:1 for large text):

| Element | Foreground | Background | Ratio | Pass |
|---------|------------|------------|-------|------|
| Primary text on bg-primary | #d7d3d0 | #1d2022 | 10.8:1 | AAA |
| Secondary text on bg-primary | #a8a5a3 | #1d2022 | 5.9:1 | AA |
| Action badges (all) | white | semantic color | > 4.5:1 | AA |
| Actor name | #d7d3d0 | #1d2022 | 10.8:1 | AAA |
| Blue link text | #098ecf | #1d2022 | 4.6:1 | AA |

**All current colors meet requirements - no changes needed**

---

## 5. Guild Display Specification

### 5.1 Guild Column Values

**Current issue:** Shows dash "-" for null guild values (inconsistent with semantic meaning)

**Fix:** Show semantic text instead of punctuation

| Condition | Display Value | Visual Treatment |
|-----------|---------------|------------------|
| GuildName is not null | Guild name (e.g., "My Server") | `text-text-primary` |
| GuildName is null AND action is system-wide | "System" | `text-text-secondary italic` |
| GuildName is null AND action is not system-wide | "Unknown" | `text-text-tertiary italic` |

### 5.2 Guild Display Logic

```csharp
// In AuditLogListItem.FromDto() or view rendering
public string GetGuildDisplay()
{
    if (!string.IsNullOrEmpty(GuildName))
        return GuildName;

    // Determine if this is a system-wide action
    var systemActions = new[] {
        AuditLogAction.Login,
        AuditLogAction.Logout,
        AuditLogAction.SettingChanged,
        AuditLogCategory.System
    };

    bool isSystemAction = systemActions.Contains(Action) || Category == AuditLogCategory.System;

    return isSystemAction ? "System" : "Unknown";
}
```

### 5.3 Guild Display HTML

```html
<!-- Desktop table -->
<td class="px-4 py-4 whitespace-nowrap text-sm hidden lg:table-cell">
  @if (!string.IsNullOrEmpty(log.GuildName))
  {
    <span class="text-text-primary">@log.GuildName</span>
  }
  else
  {
    <span class="text-text-secondary italic">@log.GetGuildDisplay()</span>
  }
</td>

<!-- Mobile card -->
<div>
  <span class="text-text-tertiary">Guild:</span>
  @if (!string.IsNullOrEmpty(log.GuildName))
  {
    <span class="text-text-primary ml-1">@log.GuildName</span>
  }
  else
  {
    <span class="text-text-secondary italic ml-1">@log.GetGuildDisplay()</span>
  }
</div>
```

**Visual treatment:**
- Guild name: Normal weight, primary color
- "System": Italic, secondary color (indicates system-wide action)
- "Unknown": Italic, tertiary color (indicates missing data)

---

## 6. Implementation Checklist

### Phase 1: Desktop Table (Critical - P0)

- [ ] Remove Details column from table header and body rows
- [ ] Change column order to: Expand → Timestamp → Action → Actor → Target → Guild
- [ ] Reduce column padding from `px-6` to `px-4` on all data columns
- [ ] Increase expand button column width from `w-10` (40px) to `w-12` (48px)
- [ ] Update expand button size to `w-11 h-11` (44x44px) with `w-5 h-5` icon
- [ ] Add responsive column hiding: `hidden lg:table-cell` on Guild column
- [ ] Implement actor display logic with 5 patterns (user with name, user with GUID, system, bot, unknown)
- [ ] Add user details link for GUID-only actors: `/Admin/Users/Details?id={ActorId}`
- [ ] Implement guild display logic (guild name, "System", or "Unknown")
- [ ] Update expandable row colspan from `7` to `6` (one fewer column)

### Phase 2: Mobile Responsive (Critical - P0)

- [ ] Add mobile card layout container: `<div class="md:hidden space-y-4">`
- [ ] Hide desktop table on mobile: add `hidden md:block` to table container
- [ ] Implement card structure matching CommandLogs pattern
- [ ] Display action badge and timestamp in card header
- [ ] Show actor with avatar/icon in prominent position
- [ ] Add target and guild in 2-column grid
- [ ] Show truncated details summary with `line-clamp-2`
- [ ] Add "View Details" link to details page
- [ ] Implement empty state for mobile with appropriate messaging

### Phase 3: Accessibility (Major - P1)

- [ ] Add `aria-label` to expand buttons with descriptive text
- [ ] Add `aria-expanded` state management to expand buttons
- [ ] Add `aria-controls` linking button to expandable region
- [ ] Add `role="region"` and `aria-labelledby` to expandable rows
- [ ] Add `scope="col"` to all table headers
- [ ] Add `aria-hidden="true"` to decorative SVG icons
- [ ] Test keyboard navigation: Tab order, Enter/Space on expand buttons
- [ ] Test focus visible rings on all interactive elements
- [ ] Verify 44x44px touch targets on mobile cards

### Phase 4: ViewModel Updates (Supporting)

- [ ] Add `GetGuildDisplay()` method to `AuditLogListItem`
- [ ] Add `GetActorDisplayHtml()` helper method for actor rendering
- [ ] Add `ActorLinkUrl` property for GUID-only users
- [ ] Add `TruncatedActorId` property (first 8 chars of GUID)
- [ ] Add `IsSystemActor`, `IsBotActor`, `IsUserActor` boolean properties
- [ ] Update `FromDto()` method to populate new properties

### Phase 5: Testing & Validation

- [ ] Test table on 1024px viewport (no horizontal scroll)
- [ ] Test table on 1280px viewport (no horizontal scroll)
- [ ] Test responsive column hiding at 1024px breakpoint
- [ ] Test mobile card layout on 375px viewport (iPhone SE)
- [ ] Test mobile card layout on 768px viewport (iPad)
- [ ] Verify all actor types display correctly (user, system, bot, unknown)
- [ ] Verify GUID truncation and user details links work
- [ ] Verify guild display logic (name, "System", "Unknown")
- [ ] Test expand/collapse on desktop with keyboard
- [ ] Test touch targets on mobile (44x44px minimum)
- [ ] Run accessibility audit with axe DevTools or Lighthouse
- [ ] Test with screen reader (NVDA or JAWS)

---

## 7. Before/After Comparison

### Desktop Table (1280px viewport)

**BEFORE:**
```
| [▶] | Timestamp            | Action     | Actor                                  | Target  | Details (truncated...)         | Guild      |
|-----|----------------------|------------|----------------------------------------|---------|--------------------------------|------------|
|     | 2025-12-29 10:30 AM  | Created    | 12208c2b-db81-4683-9095-ee82d82b5a9e  | User    | {"username":"john.doe",...}    | -          |
```
- Columns: 7 (including expand)
- Padding: `px-6` (24px) per column = 144px total horizontal padding waste
- Actor: Shows unusable GUID
- Guild: Shows dash for null
- **Requires horizontal scrolling on 1280px viewport**

**AFTER:**
```
| [▶]  | Timestamp            | Action     | Actor              | Target  | Guild     |
|------|----------------------|------------|--------------------|---------|-----------|
|      | 2025-12-29 10:30 AM  | Created    | [👤] User          | User    | System    |
|      |                      |            | 12208c2b...        |         |           |
```
- Columns: 6 (removed Details)
- Padding: `px-4` (16px) per column = 64px total horizontal padding
- Actor: Shows "User" with truncated GUID and link
- Guild: Shows "System" instead of dash
- **Fits comfortably in 1280px viewport without horizontal scrolling**

### Mobile View (375px viewport)

**BEFORE:**
- No mobile layout
- Desktop table with horizontal scrolling
- Touch targets too small
- Difficult to scan and read

**AFTER:**
```
┌─────────────────────────────────────┐
│ [Created]           10:30 AM        │
│                                     │
│ Actor:                              │
│ [👤] User                           │
│     12208c2b...                     │
│                                     │
│ Target: User    │ Guild: System    │
│                                     │
│ Details:                            │
│ Username changed from...            │
│                                     │
│ ─────────────────────────────────  │
│ View Details →                      │
└─────────────────────────────────────┘
```
- Card-based layout (matches CommandLogs pattern)
- Large touch targets
- Easy to scan
- Important information prominently displayed
- Details accessible via link

---

## 8. Design Tokens Reference

### Colors Used

```css
/* Backgrounds */
--color-bg-primary: #1d2022;
--color-bg-secondary: #262a2d;
--color-bg-tertiary: #2f3336;
--color-bg-hover: #363a3e;

/* Text */
--color-text-primary: #d7d3d0;
--color-text-secondary: #a8a5a3;
--color-text-tertiary: #7a7876;

/* Accents */
--color-accent-blue: #098ecf;
--color-accent-blue-hover: #0ba3ea;

/* Borders */
--color-border-primary: #3f4447;
--color-border-secondary: #2f3336;
--color-border-focus: #098ecf;
```

### Spacing

```css
--space-2: 0.5rem;   /* 8px - icon padding */
--space-3: 0.75rem;  /* 12px - card sections */
--space-4: 1rem;     /* 16px - column padding */
--space-6: 1.5rem;   /* 24px - card padding */
--space-8: 2rem;     /* 32px - section spacing */
```

### Touch Targets

```css
--touch-target-minimum: 44px;  /* WCAG 2.1 Level AAA */
--icon-size-medium: 20px;      /* w-5 h-5 */
--icon-size-small: 16px;       /* w-4 h-4 */
```

---

## 9. Related Documentation

- **Design System:** `docs/articles/design-system.md` - Full color palette, typography, component specs
- **CommandLogs Reference:** `src/DiscordBot.Bot/Pages/CommandLogs/Index.cshtml` - Mobile card pattern (lines 316-392)
- **Current Implementation:** `src/DiscordBot.Bot/Pages/Admin/AuditLogs/Index.cshtml`
- **ViewModel:** `src/DiscordBot.Bot/ViewModels/Pages/AuditLogListViewModel.cs`
- **DTO:** `src/DiscordBot.Core/DTOs/AuditLogDto.cs`

---

## 10. Open Questions

1. **User Details Page Route:** Confirm that `/Admin/Users/Details?id={ActorId}` is the correct route for linking GUID-only actors. If the route is different, update the specification.

2. **System Action Detection:** The specification assumes system-wide actions (Login, Logout, SettingChanged) should display "System" instead of "Unknown" when GuildName is null. Confirm this business logic is correct.

3. **Mobile Inline Expansion:** Should mobile cards have inline expandable details (like desktop), or should users always navigate to the details page? Current spec uses "View Details" link only.

4. **Actor GUID Truncation:** Specification uses 8-character truncation (e.g., `12208c2b...`). Is 8 characters sufficient for uniqueness in the UI context, or should we show more/fewer characters?

5. **Pagination on Mobile:** Current specification assumes pagination component works correctly on mobile. Verify that `_Pagination` component is mobile-responsive.

---

## Appendix A: Actor Display Decision Tree

```
Is ActorType == User?
├─ Yes
│  ├─ Is ActorDisplayName not null/empty?
│  │  ├─ Yes → Show avatar with initials + display name
│  │  └─ No
│  │     ├─ Is ActorId a GUID?
│  │     │  ├─ Yes → Show user icon + "User" + truncated GUID + link
│  │     │  └─ No → Show "Unknown" (fallback)
│  │     └─
│  └─
├─ Is ActorType == System?
│  └─ Yes → Show CPU icon + "System"
├─ Is ActorType == Bot?
│  └─ Yes → Show CPU icon + "Bot"
└─ Fallback → Show "?" icon + "Unknown"
```

---

## Appendix B: Column Width Calculations

### Current Implementation (BEFORE)

| Column | Width | Padding | Total |
|--------|-------|---------|-------|
| Expand | 40px | 12px (px-3) | 52px |
| Timestamp | ~180px | 48px (px-6 * 2) | 228px |
| Action | ~100px | 48px | 148px |
| Actor | ~280px | 48px | 328px |
| Target | ~100px | 48px | 148px |
| Details | ~320px | 48px | 368px |
| Guild | ~120px | 48px | 168px |
| **TOTAL** | | | **1440px** |

**Result:** Horizontal scrolling required on 1280px viewports

### Proposed Implementation (AFTER)

| Column | Width | Padding | Total |
|--------|-------|---------|-------|
| Expand | 48px | 8px (px-2 * 2) | 56px |
| Timestamp | ~180px | 32px (px-4 * 2) | 212px |
| Action | ~160px | 32px | 192px |
| Actor | ~240px | 32px | 272px |
| Target | ~140px | 32px | 172px |
| Guild | ~180px | 32px | 212px |
| **TOTAL** | | | **1116px** |

**Result:** Fits comfortably in 1280px viewport with **164px of horizontal space remaining**

**Space savings:** 1440px - 1116px = **324px reduction** (22.5% smaller)

---

## Appendix C: Heroicons SVG Reference

### User Icon (for GUID-only actors)

```html
<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
</svg>
```

### CPU Chip Icon (for System/Bot actors)

```html
<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" />
</svg>
```

### Chevron Right Icon (for expand button and links)

```html
<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
</svg>
```

---

**End of Specification**
