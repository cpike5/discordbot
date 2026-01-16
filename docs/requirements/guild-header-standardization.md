# Guild Setting Header Standardization

## Overview

Standardize headers, navigation, and breadcrumbs across all guild-related pages in the admin UI to create a consistent, cohesive user experience.

**Milestone**: v0.12.0 - Guild Setting Header Standardization

## Problem Statement

Guild settings pages currently have inconsistent header styles, navigation patterns, and breadcrumb formats:

- **Guild Overview**: Rich header with guild icon, name, ID, action buttons (Active badge, Sync, Members, More dropdown, Edit Settings)
- **Members**: Simple text title with count badge, Export CSV button, no guild context visible
- **Welcome Settings**: Guild icon + title + description subtitle - clean and informative
- **Audio/Soundboard**: Title + description subtitle, internal tab navigation, no guild icon

This inconsistency makes the UI feel disjointed and harder to navigate between related guild pages.

## Solution

Create a standardized guild page layout system consisting of:

1. A shared Razor layout (`_GuildLayout.cshtml`)
2. Reusable partial views for header and navigation components
3. Consistent breadcrumb formatting

## Requirements

### 1. Shared Guild Layout (`_GuildLayout.cshtml`)

A new layout that guild pages can inherit from, providing:

- Standardized breadcrumb structure
- Guild header partial (icon, title, description, actions)
- Guild navigation bar partial
- Content area for page-specific content

### 2. Guild Header Partial

**Structure:**
```
[Guild Icon] [Page Title]                    [Action Buttons]
             [Description subtitle]
```

**Components:**
- **Guild Icon** (left): The guild's Discord icon/avatar
- **Page Title** (center-left): Page name (e.g., "Welcome Settings", "Members", "Audio")
- **Description Subtitle**: Brief description of the page's purpose
- **Action Buttons Slot** (right): Area for page-specific actions (e.g., "Export CSV", "Edit Settings")

**Design Reference**: Use the Welcome Settings page header as the baseline style.

### 3. Guild Navigation Bar Partial

**Structure:**
- Horizontal tab bar (same visual style as Soundboard page tabs)
- Links to all guild settings/sub-pages
- Visual indicator for current/active page
- Appears below the header, above page content

**Tabs (Default Order):**
1. Overview
2. Members
3. Moderation
4. Messages
5. Audio
6. Rat Watch
7. Reminders
8. Welcome
9. Assistant

**Configuration:**
- Tab order should be easily configurable in code
- No count badges (keep it simple)

**Responsive Behavior:**
- Desktop: Horizontal tab bar
- Mobile: Collapse to dropdown menu

### 4. Standardized Breadcrumbs

Consistent breadcrumb format across all guild pages:

```
Home > Servers > [Guild Name] > [Page Name]
```

For sub-pages:
```
Home > Servers > [Guild Name] > [Parent Page] > [Sub-Page Name]
```

### 5. Scope

**All guild-related pages**, including:

**Main Settings Pages:**
- Guild Details/Overview (`/Guilds/Details`)
- Members (`/Guilds/{guildId}/Members`)
- Moderation Settings (`/Guilds/{guildId}/ModerationSettings`)
- Scheduled Messages (`/Guilds/ScheduledMessages/{guildId}`)
- Audio/Soundboard (`/Guilds/Soundboard/{guildId}`)
- Rat Watch (`/Guilds/RatWatch/{guildId}`)
- Reminders (`/Guilds/{guildId}/Reminders`)
- Welcome (`/Guilds/Welcome/{id}`)
- Assistant Settings (`/Guilds/AssistantSettings/{guildId}`)

**Sub-Pages (also in scope):**
- Rat Watch Analytics (`/Guilds/RatWatch/{guildId}/Analytics`)
- Rat Watch Incidents (`/Guilds/RatWatch/{guildId}/Incidents`)
- Guild Analytics pages (`/Guilds/{guildId}/Analytics/*`)
- Member Moderation (`/Guilds/{guildId}/Members/{userId}/Moderation`)
- Scheduled Message Create/Edit pages
- Flagged Events pages
- Text-to-Speech page (`/Guilds/TextToSpeech/{guildId}`)
- Audio Settings page (`/Guilds/AudioSettings/{guildId}`)
- Assistant Metrics (`/Guilds/AssistantMetrics/{guildId}`)
- Guild Edit (`/Guilds/Edit/{id}`)

## Technical Approach

### Implementation Strategy

1. **Create shared layout**: `Pages/Shared/_GuildLayout.cshtml`
   - Inherits from `_Layout.cshtml`
   - Includes breadcrumb partial
   - Includes guild header partial
   - Includes guild nav bar partial
   - Defines sections for page content and actions

2. **Create partials**:
   - `Pages/Shared/Components/_GuildHeader.cshtml` - Header with icon, title, description
   - `Pages/Shared/Components/_GuildNavBar.cshtml` - Navigation tabs
   - `Pages/Shared/Components/_GuildBreadcrumb.cshtml` - Standardized breadcrumbs (if not handled by layout directly)

3. **Create ViewModels**:
   - `GuildPageViewModel` or similar base class with common properties (GuildId, GuildName, GuildIconUrl, PageTitle, PageDescription, etc.)
   - Navigation configuration (list of tabs, current tab identifier)

4. **Migrate existing pages**:
   - Update each guild page to use the new layout
   - Move page-specific actions to the designated section/slot
   - Remove redundant header markup

### Configuration

Navigation tabs should be defined in a central location (e.g., a static class or configuration) to allow easy reordering:

```csharp
public static class GuildNavigationConfig
{
    public static IReadOnlyList<GuildNavItem> Tabs => new[]
    {
        new GuildNavItem("Overview", "Details", "Guilds", 1),
        new GuildNavItem("Members", "Index", "Members", 2),
        new GuildNavItem("Moderation", "ModerationSettings", "Guilds", 3),
        // ... etc
    };
}
```

## Out of Scope

- Changes to non-guild pages
- Adding new features/functionality to existing pages (beyond header/nav standardization)
- Refactoring page content areas
- Changing the left sidebar navigation

## Success Criteria

1. All guild-related pages use the standardized layout
2. Consistent visual appearance across all guild pages
3. Users can navigate between guild settings pages via the nav bar
4. Mobile users see a collapsed dropdown navigation
5. Breadcrumbs follow consistent format on all guild pages

## Design Deliverables

- Design spec for `_GuildLayout.cshtml`
- Design spec for `_GuildHeader.cshtml` partial
- Design spec for `_GuildNavBar.cshtml` partial
- Mobile responsive behavior specification
- HTML prototype demonstrating the standardized layout

## Open Questions

None at this time.

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Use Welcome Settings header as baseline | Cleanest current implementation with icon, title, and description |
| No badges on nav tabs | Keep navigation simple and clean |
| Collapse to dropdown on mobile | Cleaner than horizontal scroll for many tabs |
| Include all guild pages in scope | Consistent experience across the entire guild context |
| Configurable tab order in code | Flexibility without runtime complexity |