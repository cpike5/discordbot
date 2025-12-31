# Moderation System Prototype Specification

**Version:** 1.0
**Created:** 2025-12-30
**Epic:** #291 - Moderation System

---

## Overview

HTML prototypes to build for the Moderation System before implementing Razor Pages.

## Prototypes Required

### 1. Flagged Events List (`flagged-events-list.html`)

**Location:** `docs/prototypes/features/moderation/flagged-events-list.html`

**Elements:**
- Page header with pending count badge
- Filter bar: Rule type dropdown, Severity dropdown, Status dropdown, Date range
- Desktop table with columns: Checkbox, Severity, User, Rule, Channel, Timestamp, Status, Actions
- Mobile card layout (show below md breakpoint)
- Bulk action bar (appears when items selected)
- Pagination

**Interactions:**
- Row hover state
- Checkbox selection (individual + select all)
- Action menu dropdown

### 2. Flagged Event Detail (`flagged-event-detail.html`)

**Location:** `docs/prototypes/features/moderation/flagged-event-detail.html`

**Elements:**
- Back link to list
- Event header: Severity badge, rule type, timestamp, status
- User card: Avatar, username, account age, tags
- Evidence section: Collapsible list of messages with content highlighted
- User history summary: Previous flags count, cases count
- Action panel: Dismiss, Acknowledge, Action dropdown (Warn/Kick/Ban/Mute), Add Note, Apply Tag

**States to show:**
- Pending event (actions available)
- Actioned event (read-only, shows action taken)

### 3. Guild Moderation Settings (`moderation-settings.html`)

**Location:** `docs/prototypes/features/moderation/moderation-settings.html`

**Elements:**
- Tab navigation: Overview | Spam | Content | Raid | Tags
- Overview tab content: Mode toggle, preset selector, quick stats cards
- Spam tab: Enable toggle, threshold inputs, auto-action dropdown
- Content tab: Enable toggle, blocklist tag input, regex textarea, template checkboxes
- Raid tab: Enable toggle, threshold inputs, auto-action dropdown
- Tags tab: Tag list with edit/delete, add form, import button

### 4. User Moderation Profile (`user-moderation-profile.html`)

**Location:** `docs/prototypes/features/moderation/user-moderation-profile.html`

**Elements:**
- User header: Avatar, name, join date, account age
- Tags section: Tag badges with X remove, Add tag dropdown
- Tab navigation: Cases | Notes | Flags
- Cases tab: Table of moderation cases
- Notes tab: List of mod notes with add form
- Flags tab: Table of flagged events for user

---

## Shared Components

### Severity Badge
```html
<span class="severity-badge severity-low">Low</span>
<span class="severity-badge severity-medium">Medium</span>
<span class="severity-badge severity-high">High</span>
<span class="severity-badge severity-critical">
  <span class="pulse-dot"></span>Critical
</span>
```

### Status Badge
```html
<span class="status-badge status-pending">Pending</span>
<span class="status-badge status-dismissed">Dismissed</span>
<span class="status-badge status-actioned">Actioned</span>
```

### Rule Type Icon
```html
<span class="rule-icon rule-spam"><!-- Heroicon: envelope --></span>
<span class="rule-icon rule-content"><!-- Heroicon: shield-exclamation --></span>
<span class="rule-icon rule-raid"><!-- Heroicon: users --></span>
```

---

## CSS Requirements

- Use existing `docs/prototypes/css/shared.css`
- Add `moderation.css` for moderation-specific styles
- Severity colors: Low=#gray, Medium=#amber, High=#red, Critical=#red+pulse
- Follow design-system.md color tokens

---

## Build Order

1. Flagged Events List (most complex, establishes patterns)
2. Flagged Event Detail (builds on list patterns)
3. Moderation Settings (configuration forms)
4. User Moderation Profile (combines elements from above)
