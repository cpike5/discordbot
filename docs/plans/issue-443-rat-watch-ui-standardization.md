# Implementation Plan: Issue #443 - Rat Watch UI Standardization

**GitHub Issue:** #443 - Rat Watch UI Standardization and Consistency
**Created:** 2025-12-30
**Status:** Ready for Implementation

---

## 1. Requirement Summary

The Rat Watch Analytics, Incidents, and Index pages have significant UI inconsistencies that create a poor user experience. This plan addresses standardization across all pages to align with the design system and each other.

### Issue Hierarchy

| Issue | Title | Priority | Type |
|-------|-------|----------|------|
| #443 | Rat Watch UI Standardization and Consistency | High | Feature (Epic) |
| #444 | Fix breadcrumb navigation inconsistency | Critical | Task |
| #445 | Add timezone conversion for UTC timestamps | Critical | Task |
| #446 | Replace hardcoded colors with design tokens | Critical | Task |
| #447 | Standardize filter panel UX | High | Task |
| #448 | Unify date preset button behavior | High | Task |
| #449 | Create shared empty state component | High | Task |
| #450 | Create shared modal component | High | Task |
| #451 | Standardize guilty rate color thresholds | Medium | Task |
| #452 | Replace custom pagination with shared component | Medium | Task |
| #453 | Create shared status badge component | Medium | Task |
| #454 | Standardize stat card spacing and breakpoints | Low | Task |

---

## 2. Implementation Phases

### Phase 1: Critical Fixes (Quick Wins)

These can be done independently and have immediate impact.

#### Task 1.1: Fix Breadcrumb (#444)
**File:** `src/DiscordBot.Bot/Pages/Guilds/RatWatch/Analytics.cshtml`
**Change:** Line 13 - Replace "Dashboard" with "Home"

```html
<!-- Before -->
<a asp-page="/Index" class="text-text-secondary hover:text-accent-blue transition-colors">Dashboard</a>

<!-- After -->
<a asp-page="/Index" class="text-text-secondary hover:text-accent-blue transition-colors">Home</a>
```

#### Task 1.2: Add Timezone Conversion (#445)
**Files:** All three Rat Watch pages + potentially a shared script

1. Verify `timezone-utils.js` or equivalent exists in `wwwroot/js/`
2. Add script reference to each page's `@section Scripts` block
3. Initialize timezone conversion on page load
4. Handle dynamic content in modals

**Key locations needing conversion:**
- Analytics.cshtml: Lines 296, 511
- Incidents.cshtml: Lines 296, 511, 515
- Index.cshtml: Lines 404, 409

#### Task 1.3: Replace Hardcoded Colors (#446)
**Files:**
- `Analytics.cshtml` - Remove CSS block at line 761
- `rat-watch-analytics.js` - Read colors from CSS custom properties

**JavaScript approach:**
```javascript
function getDesignToken(name) {
    return getComputedStyle(document.documentElement)
        .getPropertyValue(`--color-${name}`).trim();
}
```

---

### Phase 2: Shared Component Creation

Create reusable components before refactoring pages.

#### Task 2.1: Empty State Component (#449)

**Create:** `src/DiscordBot.Bot/Pages/Shared/Components/_EmptyState.cshtml`
**Create:** `src/DiscordBot.Bot/ViewModels/Components/EmptyStateViewModel.cs`

```csharp
public class EmptyStateViewModel
{
    public string IconSvg { get; set; } = string.Empty;
    public string Title { get; set; } = "No Data";
    public string Message { get; set; } = "No items to display.";
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}
```

**Component structure:**
- 16x16 icon container with bg-bg-tertiary rounded-full
- h3 title: text-lg font-semibold text-text-primary
- p message: text-sm text-text-secondary max-w-sm mx-auto
- Optional action link

#### Task 2.2: Modal Component (#450)

**Create:** `src/DiscordBot.Bot/Pages/Shared/Components/_Modal.cshtml`
**Create:** `src/DiscordBot.Bot/ViewModels/Components/ModalViewModel.cs`
**Create:** `src/DiscordBot.Bot/wwwroot/js/modal-utils.js`

**Features:**
- Backdrop with blur effect
- Escape key to close
- Click backdrop to close
- ARIA accessibility attributes
- Sizes: sm, md, lg, xl

#### Task 2.3: Status Badge Component (#453)

**Create:** `src/DiscordBot.Bot/Pages/Shared/Components/_StatusBadge.cshtml`
**Create:** `src/DiscordBot.Bot/ViewModels/Components/StatusBadgeViewModel.cs`

**Status color mappings:**
| Status | Background | Text | Dot |
|--------|------------|------|-----|
| Active | bg-accent-blue/20 | text-accent-blue | bg-accent-blue |
| Voting | bg-warning/20 | text-warning | bg-warning |
| Completed | bg-success/20 | text-success | bg-success |
| Cancelled | bg-text-tertiary/20 | text-text-tertiary | bg-text-tertiary |
| Guilty | bg-error/20 | text-error | bg-error |
| NotGuilty | bg-success/20 | text-success | bg-success |

---

### Phase 3: UX Standardization

#### Task 3.1: Filter Panel UX (#447)

**Target pattern (from Analytics):**
- Collapsible with chevron rotation
- "Active" badge when filters applied
- `toggleFilterPanel()` JavaScript function
- Accordion with max-height transition

**Update:** `Incidents.cshtml` to match Analytics pattern

#### Task 3.2: Date Preset Behavior (#448)

**Standard behavior:** Auto-submit on date preset click

**Update:** `Incidents.cshtml` date preset buttons to auto-submit:
```javascript
function setDatePreset(days) {
    // Set date values
    document.getElementById('startDate').value = startDate.toISOString().split('T')[0];
    document.getElementById('endDate').value = today.toISOString().split('T')[0];

    // Auto-submit
    document.getElementById('filterForm').submit();
}
```

---

### Phase 4: Minor Fixes

#### Task 4.1: Guilty Rate Thresholds (#451)

**Standard thresholds:**
- >= 70%: `text-error` (red)
- >= 50%: `text-warning` (amber)
- < 50%: `text-success` (green)

**Update:** `Index.cshtml` line 227 to match Analytics

#### Task 4.2: Pagination Component (#452)

**Update:** `Index.cshtml` to use shared `_Pagination` component instead of custom inline pagination

#### Task 4.3: Stat Card Spacing (#454)

**Standard grid classes:**
```html
<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6 mb-8">
```

---

## 3. File Change Summary

### Files to Create

| File | Purpose |
|------|---------|
| `Pages/Shared/Components/_EmptyState.cshtml` | Empty state partial |
| `ViewModels/Components/EmptyStateViewModel.cs` | Empty state model |
| `Pages/Shared/Components/_Modal.cshtml` | Modal partial |
| `ViewModels/Components/ModalViewModel.cs` | Modal model |
| `Pages/Shared/Components/_StatusBadge.cshtml` | Status badge partial |
| `ViewModels/Components/StatusBadgeViewModel.cs` | Status badge model |
| `wwwroot/js/modal-utils.js` | Modal JavaScript utilities |

### Files to Modify

| File | Changes |
|------|---------|
| `Pages/Guilds/RatWatch/Analytics.cshtml` | Breadcrumb, remove hardcoded CSS, use shared components |
| `Pages/Guilds/RatWatch/Analytics.cshtml.cs` | Add ViewModels for shared components if needed |
| `Pages/Guilds/RatWatch/Incidents.cshtml` | Filter panel UX, date presets, shared components, timezone |
| `Pages/Guilds/RatWatch/Incidents.cshtml.cs` | Add ViewModels for shared components if needed |
| `Pages/Guilds/RatWatch/Index.cshtml` | Pagination, modals, badges, thresholds, spacing, timezone |
| `Pages/Guilds/RatWatch/Index.cshtml.cs` | Add pagination properties if missing |
| `wwwroot/js/rat-watch-analytics.js` | Read colors from CSS custom properties |

---

## 4. Implementation Order

Recommended order for minimal conflicts:

1. **#444** - Breadcrumb fix (1 line change)
2. **#451** - Guilty rate thresholds (1 line change)
3. **#454** - Stat card spacing (few line changes)
4. **#446** - Hardcoded colors (CSS/JS cleanup)
5. **#445** - Timezone conversion (script additions)
6. **#449** - Empty state component (new component + refactor)
7. **#453** - Status badge component (new component + refactor)
8. **#450** - Modal component (new component + refactor)
9. **#452** - Pagination standardization (refactor)
10. **#447** - Filter panel UX (JS/HTML refactor)
11. **#448** - Date preset behavior (JS change)

---

## 5. Testing Checklist

### Visual Consistency
- [ ] All pages use "Home" in breadcrumb
- [ ] All timestamps display in local timezone
- [ ] No hardcoded colors visible in browser dev tools
- [ ] Filter panels behave identically
- [ ] Empty states look identical
- [ ] Modals have consistent styling
- [ ] Status badges are consistent
- [ ] Pagination is consistent
- [ ] Stat cards have consistent spacing

### Functionality
- [ ] Date presets auto-submit on both pages
- [ ] Modal Escape key works
- [ ] Modal backdrop click closes
- [ ] Pagination navigates correctly
- [ ] Timezone conversion works across timezones

### Responsive
- [ ] All pages work on mobile (375px)
- [ ] All pages work on tablet (768px)
- [ ] All pages work on desktop (1280px+)

### Accessibility
- [ ] Modals have proper ARIA attributes
- [ ] Tab navigation works correctly
- [ ] Screen reader announces status changes

---

## 6. Success Criteria

From issue #443:

- [ ] All pages use consistent breadcrumb navigation
- [ ] All UTC timestamps display in user's local timezone
- [ ] No hardcoded colors in CSS or JavaScript
- [ ] Filter panels behave identically across pages
- [ ] Empty states use shared component
- [ ] Modals use shared component
- [ ] Status badges use shared component
- [ ] Pagination uses shared `_Pagination` component
- [ ] Stat card color thresholds are consistent

---

## 7. Notes

### Existing Components to Leverage

The project already has shared components in `Pages/Shared/Components/`:
- `_Alert.cshtml`
- `_Badge.cshtml`
- `_Button.cshtml`
- `_Card.cshtml`
- `_Pagination.cshtml`

Review these before creating new components to ensure consistency and avoid duplication.

### Design System Reference

All implementations should follow `docs/articles/design-system.md` for:
- Color tokens
- Typography scale
- Spacing values
- Border radii
- Shadow styles
