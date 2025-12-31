# Moderation System UI Design Specification

**Version:** 1.0
**Created:** 2025-12-30
**Status:** Draft
**Epic:** #291 - Moderation System

---

## Overview

UI design spec for the Moderation System admin pages. Follows established design-system.md patterns.

## New Pages Required

### 1. Flagged Events Page (`/Guilds/{guildId}/FlaggedEvents`)

**Purpose:** Central view of auto-detected moderation events requiring review.

#### Layout
- **Header:** Title with badge count (pending events), guild selector dropdown
- **Filters bar:** Rule type, severity, status, date range (collapsed on mobile)
- **Table/Card list:** Responsive like CommandLogs - table on desktop, cards on mobile

#### Table Columns (Desktop)
| Column | Width | Content |
|--------|-------|---------|
| Checkbox | 40px | Bulk actions |
| Severity | 80px | Color-coded badge (Low/Med/High/Critical) |
| User | 180px | Avatar + username |
| Rule | 140px | Icon + type (Spam/Content/Raid) |
| Channel | 140px | Channel name |
| Timestamp | 140px | Relative time |
| Status | 100px | Badge (Pending/Dismissed/Actioned) |
| Actions | 120px | Quick action buttons |

#### Severity Badge Colors
- Low: `text-secondary` gray
- Medium: `warning` amber
- High: `error` red
- Critical: `error` red with pulsing dot animation

#### Mobile Card Layout
```
+----------------------------------+
| [Severity] [Status]      2h ago  |
| @Username in #channel            |
| Spam: Message flood detected     |
| [Dismiss] [View] [Action ▼]     |
+----------------------------------+
```

#### Detail Modal/Page
- Event summary header with severity indicator
- User info card: avatar, name, account age, existing tags
- Evidence section: collapsible message list with highlights
- User history: previous flags count, mod actions summary
- Action panel: Dismiss, Acknowledge, Take Action dropdown, Add Note, Apply Tag

### 2. Guild Moderation Settings (`/Guilds/{guildId}/ModerationSettings`)

**Purpose:** Configure auto-moderation rules for the guild.

#### Layout
- Tab navigation: Overview | Spam | Content | Raid | Tags

#### Overview Tab
- Mode toggle: Simple (presets) vs Advanced
- If Simple: Radio buttons for Relaxed/Moderate/Strict presets
- Quick stats: Events flagged (24h), Auto-actions taken, Active rules

#### Spam Detection Tab
- Enable toggle at top
- Threshold sliders/inputs for each parameter
- New account sensitivity toggle + days input
- Auto-action dropdown (None/Mute/Kick/Ban)

#### Content Filter Tab
- Enable toggle
- Custom blocklist: tag input for words/phrases
- Regex patterns: textarea (advanced users)
- Template checkboxes: Slurs, Common Spam, etc.
- Auto-action dropdown

#### Raid Protection Tab
- Enable toggle
- Mass join threshold + window inputs
- New account flag days input
- Auto-action dropdown (None/Lockdown/Alert)

#### Tags Tab
- List of guild-defined tags with edit/delete
- Add new tag form
- Import from templates button

### 3. User Moderation Profile (`/Guilds/{guildId}/Members/{userId}/Moderation`)

**Purpose:** View complete moderation history for a user.

#### Layout
- User header: Avatar, name, join date, account age
- Tags section: Current tags with remove buttons, add tag dropdown
- Tab navigation: Cases | Notes | Flags | Activity

#### Cases Tab
Table of moderation actions (warns, kicks, bans, mutes)

#### Notes Tab
List of mod notes with timestamps and authors

#### Flags Tab
List of auto-mod flags for this user

#### Activity Tab
Recent message activity chart (if message logging enabled)

---

## Component Patterns

### Severity Badge Component
Reuse existing `_Badge.cshtml` partial with severity-specific variants.

### Quick Action Buttons
Consistent icon buttons: Eye (view), X (dismiss), Shield (action menu)

### Confirmation Modals
For destructive actions (kick/ban): require reason input, show user info summary.

---

## Mobile Responsiveness

All pages must:
- Hide table, show cards below `md` breakpoint
- Collapse filters into expandable drawer
- Stack action buttons vertically in cards
- Use bottom sheet for action menus

---

## Implementation Notes

- Follow form patterns from `docs/articles/form-implementation-standards.md`
- Use existing page layout from Rat Watch pages as template
- Integrate with design system colors and components
