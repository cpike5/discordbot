# Guild Setting Header Standardization - Implementation Plan

## Executive Summary

This implementation plan outlines the work required to standardize headers, navigation, and breadcrumbs across all guild-related pages in the admin UI. The goal is to create a consistent, cohesive user experience by implementing a shared layout system that all guild pages will inherit from.

**Key Deliverables:**
- Shared `_GuildLayout.cshtml` that all guild pages use
- Reusable partial components for breadcrumbs, header, and navigation
- Supporting ViewModels and configuration infrastructure
- Migration of 20+ existing guild pages to the new layout
- CSS and JavaScript for navigation behavior

**Estimated Effort:** Large (7-10 days of implementation work)

**Target Milestone:** v0.12.0 - Guild Setting Header Standardization

---

## Implementation Phases

### Phase 1: Infrastructure (Foundation)
Create the core ViewModels, configuration classes, and partial views that will be reused across all guild pages.

**Dependencies:** None (can start immediately)

**Deliverables:**
- ViewModels for breadcrumb, header, and navigation components
- Static configuration class for guild navigation tabs
- Partial view components
- Shared layout file
- CSS and JavaScript for navigation behavior

### Phase 2: Pilot Migration (Validation)
Migrate 2-3 representative pages to validate the infrastructure and identify any issues before full migration.

**Dependencies:** Phase 1 complete

**Deliverables:**
- Guild Overview (Details) migrated
- Welcome Settings migrated
- Audio/Soundboard migrated
- Documentation of migration process

### Phase 3: Bulk Migration (Main Settings Pages)
Migrate the remaining primary guild settings pages.

**Dependencies:** Phase 2 complete and validated

**Deliverables:**
- Members page migrated
- Moderation Settings migrated
- Scheduled Messages migrated
- Rat Watch migrated
- Reminders migrated
- Assistant Settings migrated
- TTS page migrated
- Audio Settings migrated

### Phase 4: Sub-Pages Migration
Migrate sub-pages and specialized pages.

**Dependencies:** Phase 3 complete

**Deliverables:**
- Guild Analytics pages migrated
- Rat Watch Analytics migrated
- Rat Watch Incidents migrated
- Member Moderation migrated
- Flagged Events pages migrated
- Scheduled Message Create/Edit migrated
- Guild Edit migrated
- Assistant Metrics migrated

### Phase 5: Testing & Refinement
Comprehensive testing across all migrated pages and refinement of the implementation.

**Dependencies:** Phase 4 complete

**Deliverables:**
- Responsive behavior tested on mobile/tablet/desktop
- Accessibility audit (keyboard navigation, ARIA labels, screen readers)
- Navigation consistency verification
- Action buttons functionality verification
- Cross-browser testing
- Documentation updates

---

## Detailed Tasks

### Task 1: Create Core ViewModels
**Phase:** 1 (Infrastructure)
**Complexity:** Small
**Dependencies:** None

**Files to Create:**
- `src/DiscordBot.Bot/ViewModels/Components/GuildBreadcrumbViewModel.cs`
- `src/DiscordBot.Bot/ViewModels/Components/GuildHeaderViewModel.cs`
- `src/DiscordBot.Bot/ViewModels/Components/GuildNavBarViewModel.cs`

**Description:**
Create the three core ViewModel classes that will be used by the partial components:

1. **GuildBreadcrumbViewModel**: Contains a list of `BreadcrumbItem` objects representing the navigation hierarchy
2. **GuildHeaderViewModel**: Contains guild info, page title/description, and action buttons
3. **GuildNavBarViewModel**: Contains guild ID, active tab identifier, and list of navigation tabs

Follow the specifications from `docs/articles/guild-layout-spec.md` (lines 58-242).

**Acceptance Criteria:**
- All three ViewModels created with properties matching the spec
- `HeaderAction` and `HeaderActionStyle` enum defined
- `BreadcrumbItem` record defined
- `GuildNavItem` record defined
- XML documentation comments on all public members

---

### Task 2: Create Guild Navigation Configuration
**Phase:** 1 (Infrastructure)
**Complexity:** Small
**Dependencies:** Task 1

**Files to Create:**
- `src/DiscordBot.Bot/Configuration/GuildNavigationConfig.cs`

**Description:**
Create a static configuration class that defines the standard navigation tabs for guild pages. This centralizes tab order and makes it easy to reorder tabs without touching markup.

Reference the navigation configuration example in `docs/articles/guild-layout-spec.md` (lines 246-283).

**Navigation Tabs (Default Order):**
1. Overview (`Details` page)
2. Members (`Members/Index` page)
3. Moderation (`ModerationSettings` page)
4. Messages (`ScheduledMessages` page)
5. Audio (`Soundboard` page)
6. Rat Watch (`RatWatch` page)
7. Reminders (`Reminders` page)
8. Welcome (`Welcome` page)
9. Assistant (`AssistantSettings` page)

Include Hero Icons SVG paths for each tab (outline and solid versions).

**Acceptance Criteria:**
- `GuildNavigationConfig` static class with `GetTabs()` method
- Returns `IReadOnlyList<GuildNavItem>` with all 9 tabs
- Each tab has correct ID, label, page name, area, order, and icons
- Icons sourced from Hero Icons (https://heroicons.com)

---

### Task 3: Create Breadcrumb Partial Component
**Phase:** 1 (Infrastructure)
**Complexity:** Small
**Dependencies:** Task 1

**Files to Create:**
- `src/DiscordBot.Bot/Pages/Shared/Components/_GuildBreadcrumb.cshtml`

**Description:**
Create the reusable breadcrumb navigation component that displays the page hierarchy.

Follow markup pattern from `docs/articles/guild-layout-spec.md` (lines 74-103).

**Key Features:**
- Renders list of breadcrumb items with separators
- Links to parent pages (except current page)
- Current page shown in bold without link
- Chevron separators between items
- Tailwind classes matching design tokens

**Acceptance Criteria:**
- Partial view accepts `GuildBreadcrumbViewModel`
- Renders semantic `<nav>` with `aria-label="Breadcrumb"`
- Uses `<ol>` for breadcrumb list
- Chevron SVG separators between items
- Current page has no link and different styling

---

### Task 4: Create Header Partial Component
**Phase:** 1 (Infrastructure)
**Complexity:** Medium
**Dependencies:** Task 1

**Files to Create:**
- `src/DiscordBot.Bot/Pages/Shared/Components/_GuildHeader.cshtml`

**Description:**
Create the reusable guild header component that displays guild icon, page title, description, and action buttons.

Follow markup pattern from `docs/articles/guild-layout-spec.md` (lines 149-216).

**Key Features:**
- Guild icon (image or fallback initials with gradient)
- Page title and optional description
- Action buttons slot (right-aligned on desktop, full-width on mobile)
- Responsive flex layout
- Three button styles: Primary (orange), Secondary (border), Link (text)

**Acceptance Criteria:**
- Partial view accepts `GuildHeaderViewModel`
- Displays guild icon with fallback for missing images
- Renders page title and description
- Renders action buttons with correct styles based on `HeaderActionStyle`
- Responsive behavior: stacks on mobile, horizontal on desktop
- Uses design tokens for colors and spacing

---

### Task 5: Create Navigation Bar Partial Component
**Phase:** 1 (Infrastructure)
**Complexity:** Large
**Dependencies:** Task 1, Task 2

**Files to Create:**
- `src/DiscordBot.Bot/Pages/Shared/Components/_GuildNavBar.cshtml`
- `src/DiscordBot.Bot/wwwroot/js/guild-nav.js`
- CSS added to existing site stylesheet

**Description:**
Create the navigation bar component with desktop (horizontal tabs) and mobile (dropdown) variants.

Follow markup pattern from `docs/articles/guild-layout-spec.md` (lines 286-497).

**Key Features:**
- Desktop: Horizontal tab bar with icons and labels
- Mobile: Dropdown menu (hidden by default)
- Active tab indicator (background change, icon color change)
- Outline icons for inactive tabs, solid icons for active tab
- URL generation using ASP.NET Tag Helpers
- JavaScript for dropdown toggle behavior

**CSS Requirements:**
Add guild navigation styles to site stylesheet (see lines 348-473 of spec).

**JavaScript Requirements:**
Create `guild-nav.js` with dropdown toggle logic and click-outside-to-close behavior (see lines 477-497 of spec).

**Acceptance Criteria:**
- Partial view accepts `GuildNavBarViewModel`
- Desktop tabs show on screens >= 640px
- Mobile dropdown shows on screens < 640px
- Active tab has correct styling (background, icon color)
- Tabs link to correct pages using `asp-page` and `asp-route-guildId`
- Dropdown toggles on button click
- Dropdown closes on click outside
- Accessible ARIA attributes on all elements

---

### Task 6: Create Shared Guild Layout
**Phase:** 1 (Infrastructure)
**Complexity:** Small
**Dependencies:** Task 3, Task 4, Task 5

**Files to Create:**
- `src/DiscordBot.Bot/Pages/Shared/_GuildLayout.cshtml`

**Description:**
Create the shared layout that wraps all guild pages and provides the common structure.

Follow structure from `docs/articles/guild-layout-spec.md` (lines 22-43).

**Key Features:**
- Inherits from `_Layout.cshtml`
- Max-width container with responsive padding
- Renders breadcrumb, header, and navigation partials
- Content area for `@RenderBody()`
- Expects PageModel to have `Breadcrumb`, `Header`, and `Navigation` properties

**Acceptance Criteria:**
- Layout file created in `Pages/Shared/`
- Sets `Layout = "_Layout.cshtml"`
- Includes container div with max-width and padding
- Renders three partial components in order
- Renders page content via `@RenderBody()`

---

### Task 7: Create Base PageModel Class (Optional)
**Phase:** 1 (Infrastructure)
**Complexity:** Small
**Dependencies:** Task 1

**Files to Create:**
- `src/DiscordBot.Bot/Pages/Guilds/GuildPageModelBase.cs`

**Description:**
Create an optional base class that guild page models can inherit from to reduce boilerplate. This provides common properties and helper methods for building breadcrumb, header, and navigation ViewModels.

**Key Features:**
- Abstract base class inheriting from `PageModel`
- Properties for `Breadcrumb`, `Header`, `Navigation`
- Helper methods for building standard breadcrumbs (e.g., `BuildBreadcrumb(guildId, guildName, pageName)`)
- Helper method for building navigation (e.g., `BuildNavigation(guildId, activeTabId)`)

**Acceptance Criteria:**
- Base class created with required properties
- Helper methods reduce code duplication
- Well-documented with XML comments
- Guild pages can optionally inherit from this base class

---

### Task 8: Migrate Guild Overview (Details) Page
**Phase:** 2 (Pilot Migration)
**Complexity:** Medium
**Dependencies:** Phase 1 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Details.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/Details.cshtml.cs`

**Description:**
Migrate the Guild Overview/Details page to use the new shared layout. This is the first pilot migration to validate the infrastructure.

**Current State:**
- Has custom breadcrumb markup
- Has guild header with action buttons (Active badge, Sync, Members link, More dropdown, Edit Settings)
- Has custom tab panel for in-page navigation (Overview, Members, Messages, Rat Watch, Reminders, Activity tabs)

**Migration Steps:**
1. Update `.cshtml.cs` PageModel to populate `Breadcrumb`, `Header`, and `Navigation` properties
2. Update `.cshtml` to set `Layout = "Shared/_GuildLayout.cshtml"`
3. Remove custom breadcrumb and header markup (now provided by layout)
4. Keep existing tab panel for in-page sections (Overview, Members, etc.) - this is separate from the guild-level navigation
5. Move action buttons to `Header.Actions` collection

**Important Notes:**
- The Details page has TWO levels of tabs:
  - **Guild-level navigation** (provided by new layout): Overview, Members, Moderation, etc.
  - **In-page tabs** (existing, keep as-is): Overview tab content, Members tab content, etc.
- The guild-level navigation should highlight "Overview" as active
- The existing in-page tab panel remains within the page content area

**Acceptance Criteria:**
- Page uses `_GuildLayout.cshtml`
- Breadcrumb: Home > Servers > [Guild Name]
- Header shows guild icon, "Overview" title, description, and action buttons
- Guild navigation bar shows with "Overview" active
- Existing in-page tab panel still works correctly
- All functionality preserved (Sync button, Edit Settings, etc.)

---

### Task 9: Migrate Welcome Settings Page
**Phase:** 2 (Pilot Migration)
**Complexity:** Small
**Dependencies:** Task 8 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Welcome.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/Welcome.cshtml.cs`

**Description:**
Migrate the Welcome Settings page to use the new shared layout.

**Current State:**
- Has breadcrumb: Home > Servers > [Guild Name] > Welcome Settings
- Has guild header with icon, title, and description (clean implementation - this was the baseline)
- No action buttons currently

**Migration Steps:**
1. Update PageModel to populate layout properties
2. Set layout to `_GuildLayout.cshtml`
3. Remove custom breadcrumb and header markup
4. Set active tab to "welcome" in navigation

**Acceptance Criteria:**
- Page uses `_GuildLayout.cshtml`
- Breadcrumb: Home > Servers > [Guild Name] > Welcome Settings
- Header shows guild icon, "Welcome Settings" title, description
- Guild navigation bar shows with "Welcome" tab active
- Form and all functionality preserved

---

### Task 10: Migrate Audio/Soundboard Page
**Phase:** 2 (Pilot Migration)
**Complexity:** Medium
**Dependencies:** Task 8 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Soundboard.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/Soundboard.cshtml.cs`

**Description:**
Migrate the Soundboard page to use the new shared layout.

**Current State:**
- Has title + description subtitle
- Has internal tab navigation (Soundboard/TTS tabs) using existing TabPanel component
- No guild context visible (no icon, no breadcrumb)

**Migration Steps:**
1. Update PageModel to populate layout properties
2. Set layout to `_GuildLayout.cshtml`
3. Add breadcrumb navigation
4. Set active tab to "audio" in guild navigation
5. Keep existing internal tab panel (Soundboard/TTS tabs)

**Important Notes:**
- Similar to Details page, this has TWO levels of tabs:
  - **Guild-level navigation** (new): Overview, Members, Moderation, Audio, etc.
  - **Internal page tabs** (existing, keep as-is): Soundboard, TTS
- Guild-level navigation should highlight "Audio" as active

**Acceptance Criteria:**
- Page uses `_GuildLayout.cshtml`
- Breadcrumb: Home > Servers > [Guild Name] > Audio
- Header shows guild icon, "Audio" title, description
- Guild navigation bar shows with "Audio" tab active
- Existing Soundboard/TTS tab panel still works
- All functionality preserved

---

### Task 11: Document Migration Process
**Phase:** 2 (Pilot Migration)
**Complexity:** Small
**Dependencies:** Task 8, Task 9, Task 10 complete

**Files to Create:**
- `docs/guides/guild-page-migration-guide.md`

**Description:**
Document the migration process for converting existing guild pages to use the new shared layout. This will serve as a reference for bulk migration tasks.

**Content Should Include:**
- Step-by-step migration checklist
- Code examples for populating ViewModels
- Common patterns for breadcrumbs (2-level, 3-level)
- How to configure action buttons
- How to set active tab
- Troubleshooting common issues
- Special cases (pages with internal tabs)

**Acceptance Criteria:**
- Migration guide created with clear steps
- Includes code examples from pilot migrations
- Covers edge cases (internal tabs, no action buttons, etc.)
- Well-formatted and easy to follow

---

### Task 12: Migrate Members Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Members/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/Members/Index.cshtml.cs`

**Current State:**
- Simple text title with count badge
- Export CSV button
- No guild context visible

**Migration:**
- Add guild icon and description via new header
- Add breadcrumb: Home > Servers > [Guild Name] > Members
- Set active tab to "members"
- Move Export CSV to header actions
- Add member count to description

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Shows guild context in header
- Navigation highlights "Members" tab
- Export CSV button in header actions

---

### Task 13: Migrate Moderation Settings Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/ModerationSettings.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/ModerationSettings.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Moderation
- Add guild header with icon, title, description
- Set active tab to "moderation"
- Add any relevant action buttons

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Moderation" tab
- All moderation settings functionality preserved

---

### Task 14: Migrate Scheduled Messages Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Index.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Messages
- Add guild header
- Set active tab to "messages"
- Add "Create Message" action button

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Messages" tab
- Create button in header actions

---

### Task 15: Migrate Rat Watch Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/RatWatch/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/RatWatch/Index.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Rat Watch
- Add guild header
- Set active tab to "ratwatch"
- Add action buttons if needed

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Rat Watch" tab
- Rat Watch functionality preserved

---

### Task 16: Migrate Reminders Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Reminders/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/Reminders/Index.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Reminders
- Add guild header
- Set active tab to "reminders"

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Reminders" tab

---

### Task 17: Migrate Assistant Settings Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/AssistantSettings.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/AssistantSettings.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Assistant
- Add guild header
- Set active tab to "assistant"

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Assistant" tab

---

### Task 18: Migrate TTS Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/TextToSpeech/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/TextToSpeech/Index.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Audio > TTS
- Add guild header
- Set active tab to "audio" (TTS is a sub-page of Audio)

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Audio" tab (parent section)
- TTS functionality preserved

---

### Task 19: Migrate Audio Settings Page
**Phase:** 3 (Bulk Migration - Main Settings)
**Complexity:** Small
**Dependencies:** Phase 2 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/AudioSettings/Index.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/AudioSettings/Index.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Audio > Settings
- Add guild header
- Set active tab to "audio" (sub-page of Audio)

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Audio" tab

---

### Task 20: Migrate Guild Analytics Pages
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Medium
**Dependencies:** Phase 3 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Analytics/Index.cshtml` (and .cs)
- `src/DiscordBot.Bot/Pages/Guilds/Analytics/Engagement.cshtml` (and .cs)
- `src/DiscordBot.Bot/Pages/Guilds/Analytics/Moderation.cshtml` (and .cs)

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > [Parent Section] > Analytics
- Add guild header
- Set appropriate active tab based on parent section

**Acceptance Criteria:**
- All analytics pages use `_GuildLayout.cshtml`
- Correct navigation tab highlighted
- Charts and analytics functionality preserved

---

### Task 21: Migrate Rat Watch Sub-Pages
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Small
**Dependencies:** Phase 3 complete, Task 15

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/RatWatch/Analytics.cshtml` (and .cs)
- `src/DiscordBot.Bot/Pages/Guilds/RatWatch/Incidents.cshtml` (and .cs)

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Rat Watch > [Sub-Page]
- Add guild header
- Set active tab to "ratwatch"

**Acceptance Criteria:**
- Both pages use `_GuildLayout.cshtml`
- Navigation highlights "Rat Watch" tab
- Sub-page functionality preserved

---

### Task 22: Migrate Member Moderation Page
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Small
**Dependencies:** Phase 3 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Members/Moderation.cshtml` (and .cs)

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Members > [Username] > Moderation
- Add guild header
- Set active tab to "members"

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Members" tab
- Moderation history displayed correctly

---

### Task 23: Migrate Flagged Events Pages
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Small
**Dependencies:** Phase 3 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/FlaggedEvents/Index.cshtml` (and .cs)
- `src/DiscordBot.Bot/Pages/Guilds/FlaggedEvents/Details.cshtml` (and .cs)

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Moderation > Flagged Events
- Add guild header
- Set active tab to "moderation"

**Acceptance Criteria:**
- Both pages use `_GuildLayout.cshtml`
- Navigation highlights "Moderation" tab

---

### Task 24: Migrate Scheduled Message Create/Edit Pages
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Small
**Dependencies:** Phase 3 complete, Task 14

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Create.cshtml` (and .cs)
- `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Edit.cshtml` (and .cs)

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Messages > [Create/Edit]
- Add guild header
- Set active tab to "messages"

**Acceptance Criteria:**
- Both pages use `_GuildLayout.cshtml`
- Navigation highlights "Messages" tab
- Form functionality preserved

---

### Task 25: Migrate Guild Edit Page
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Small
**Dependencies:** Phase 3 complete

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Edit Settings
- Add guild header
- Set active tab to "overview"

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Overview" tab
- Edit form functionality preserved

---

### Task 26: Migrate Assistant Metrics Page
**Phase:** 4 (Sub-Pages Migration)
**Complexity:** Small
**Dependencies:** Phase 3 complete, Task 17

**Files to Modify:**
- `src/DiscordBot.Bot/Pages/Guilds/AssistantMetrics.cshtml`
- `src/DiscordBot.Bot/Pages/Guilds/AssistantMetrics.cshtml.cs`

**Migration:**
- Add breadcrumb: Home > Servers > [Guild Name] > Assistant > Metrics
- Add guild header
- Set active tab to "assistant"

**Acceptance Criteria:**
- Uses `_GuildLayout.cshtml`
- Navigation highlights "Assistant" tab
- Metrics display correctly

---

### Task 27: Responsive Testing
**Phase:** 5 (Testing & Refinement)
**Complexity:** Medium
**Dependencies:** Phase 4 complete

**Description:**
Test all migrated pages on various screen sizes and devices to ensure responsive behavior works correctly.

**Test Cases:**
- Desktop (1920px, 1440px, 1024px): Horizontal tab navigation visible
- Tablet (768px): Horizontal tabs with possible scroll
- Mobile (640px, 375px): Dropdown navigation visible
- Header stacks correctly on mobile
- Action buttons stack on mobile, horizontal on desktop
- Guild icon and text layout responsive
- Breadcrumb navigation wraps appropriately

**Browsers:**
- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

**Acceptance Criteria:**
- All pages render correctly on all screen sizes
- Navigation switches from tabs to dropdown at correct breakpoint
- No horizontal scroll on mobile devices
- Touch interactions work on mobile (dropdown toggle)

---

### Task 28: Accessibility Audit
**Phase:** 5 (Testing & Refinement)
**Complexity:** Medium
**Dependencies:** Phase 4 complete

**Description:**
Conduct comprehensive accessibility testing to ensure the new layout system meets WCAG 2.1 AA standards.

**Test Cases:**
- Keyboard navigation through breadcrumbs, tabs, and action buttons
- Tab key moves focus in logical order
- Enter/Space activates tabs and buttons
- Escape closes mobile dropdown
- Screen reader announces breadcrumb navigation correctly
- Screen reader announces tabs with aria-label
- Active tab state announced (aria-selected)
- Focus indicators visible on all interactive elements
- Color contrast meets WCAG AA (4.5:1 for text)

**Tools:**
- axe DevTools browser extension
- NVDA or JAWS screen reader
- Keyboard-only navigation testing
- Chrome Lighthouse accessibility audit

**Acceptance Criteria:**
- Zero critical accessibility issues
- All interactive elements keyboard accessible
- All ARIA attributes correct
- Focus indicators visible
- Screen reader announces context correctly
- Lighthouse accessibility score >= 95

---

### Task 29: Navigation Consistency Verification
**Phase:** 5 (Testing & Refinement)
**Complexity:** Small
**Dependencies:** Phase 4 complete

**Description:**
Verify that navigation is consistent across all guild pages and that tab highlighting works correctly.

**Test Cases:**
- Visit each guild page and verify correct tab is highlighted
- Click each navigation tab and verify correct page loads
- Verify breadcrumb shows correct hierarchy on each page
- Verify back/forward browser navigation works
- Verify guild icon and name consistent across pages

**Acceptance Criteria:**
- Active tab indicator correct on all pages
- Navigation links work correctly
- Breadcrumbs accurate on all pages
- No navigation inconsistencies found

---

### Task 30: Update Documentation
**Phase:** 5 (Testing & Refinement)
**Complexity:** Small
**Dependencies:** Phase 4 complete

**Files to Modify:**
- `docs/articles/guild-layout-spec.md` (mark as implemented)
- `docs/requirements/guild-header-standardization.md` (mark as complete)
- `CLAUDE.md` (update UI page routes if needed)

**Files to Create:**
- Add usage examples to migration guide
- Add troubleshooting section

**Acceptance Criteria:**
- All documentation updated
- Implementation notes added to spec
- Migration guide includes lessons learned
- CLAUDE.md reflects current state

---

## Suggested Issue Breakdown

### Epic Issue: Guild Setting Header Standardization

**Title:** Epic: Standardize headers, navigation, and breadcrumbs across guild pages

**Description:**
Implement a shared layout system for all guild-related pages to create a consistent, cohesive user experience.

**Deliverables:**
- Shared `_GuildLayout.cshtml` with breadcrumbs, header, and navigation
- ViewModels and configuration infrastructure
- Migration of 20+ guild pages
- CSS and JavaScript for navigation behavior
- Comprehensive testing and documentation

**Estimated Effort:** Large (7-10 days)

**Labels:** `milestone:v0.12.0`, `epic`, `ui`, `enhancement`, `priority:high`

---

### Sub-Issue 1: Infrastructure - Core Components

**Title:** Create ViewModels and partial components for guild layout system

**Description:**
Create the foundational infrastructure for the guild layout standardization:

- **ViewModels**: `GuildBreadcrumbViewModel`, `GuildHeaderViewModel`, `GuildNavBarViewModel`
- **Config**: `GuildNavigationConfig` static class with tab definitions
- **Partials**: `_GuildBreadcrumb.cshtml`, `_GuildHeader.cshtml`, `_GuildNavBar.cshtml`
- **Layout**: `_GuildLayout.cshtml`
- **Assets**: CSS styles and JavaScript for navigation

**Tasks Included:** Task 1-7

**Acceptance Criteria:**
- All ViewModels created with properties matching spec
- Navigation config has 9 tabs with icons
- All three partial components render correctly in isolation
- Shared layout integrates all components
- CSS styles match design tokens
- JavaScript dropdown behavior works

**Estimated Effort:** Medium (2-3 days)

**Labels:** `milestone:v0.12.0`, `ui`, `component`, `priority:high`

**Dependencies:** Requires design spec review

---

### Sub-Issue 2: Pilot Migration - Validation Pages

**Title:** Migrate pilot pages to validate guild layout infrastructure

**Description:**
Migrate 3 representative pages to validate the new infrastructure and identify any issues:

1. **Guild Overview (Details)** - Complex page with in-page tabs
2. **Welcome Settings** - Clean baseline implementation
3. **Audio/Soundboard** - Page with internal navigation

Also document the migration process for bulk work.

**Tasks Included:** Task 8-11

**Acceptance Criteria:**
- All 3 pages successfully migrated
- Guild layout components working correctly
- All page functionality preserved
- No regressions identified
- Migration guide documented

**Estimated Effort:** Medium (2 days)

**Labels:** `milestone:v0.12.0`, `ui`, `migration`, `priority:high`

**Dependencies:** Sub-Issue 1 complete

---

### Sub-Issue 3: Bulk Migration - Main Guild Settings Pages

**Title:** Migrate main guild settings pages to shared layout

**Description:**
Migrate the remaining primary guild settings pages to use the shared layout:

- Members
- Moderation Settings
- Scheduled Messages
- Rat Watch
- Reminders
- Assistant Settings
- TTS
- Audio Settings

**Tasks Included:** Task 12-19

**Acceptance Criteria:**
- All 8 pages migrated to shared layout
- Navigation tabs highlight correctly
- Breadcrumbs show correct hierarchy
- Action buttons positioned correctly
- All functionality preserved

**Estimated Effort:** Medium (2 days)

**Labels:** `milestone:v0.12.0`, `ui`, `migration`, `priority:medium`

**Dependencies:** Sub-Issue 2 complete

---

### Sub-Issue 4: Bulk Migration - Sub-Pages and Specialized Pages

**Title:** Migrate guild sub-pages to shared layout

**Description:**
Migrate sub-pages and specialized pages:

- Guild Analytics (3 pages)
- Rat Watch Analytics & Incidents
- Member Moderation
- Flagged Events (2 pages)
- Scheduled Message Create/Edit
- Guild Edit
- Assistant Metrics

**Tasks Included:** Task 20-26

**Acceptance Criteria:**
- All 11 sub-pages migrated
- Multi-level breadcrumbs correct
- Parent section navigation highlighted
- All functionality preserved

**Estimated Effort:** Medium (2 days)

**Labels:** `milestone:v0.12.0`, `ui`, `migration`, `priority:medium`

**Dependencies:** Sub-Issue 3 complete

---

### Sub-Issue 5: Testing, Accessibility, and Documentation

**Title:** Comprehensive testing and documentation for guild layout system

**Description:**
Final testing, accessibility audit, and documentation updates:

- Responsive testing (desktop, tablet, mobile)
- Accessibility audit (WCAG 2.1 AA)
- Navigation consistency verification
- Documentation updates
- Migration guide refinement

**Tasks Included:** Task 27-30

**Acceptance Criteria:**
- All pages tested on multiple screen sizes
- Accessibility audit passes (Lighthouse >= 95)
- Navigation consistent across all pages
- Documentation complete and accurate
- Migration guide includes troubleshooting

**Estimated Effort:** Small (1 day)

**Labels:** `milestone:v0.12.0`, `testing`, `documentation`, `accessibility`, `priority:medium`

**Dependencies:** Sub-Issue 4 complete

---

## Migration Strategy

### Order of Migration

**Phase 1 - Infrastructure (Do First):**
1. Create all ViewModels and configuration
2. Create all partial components
3. Create shared layout
4. Add CSS and JavaScript

**Phase 2 - Pilot (Validate Infrastructure):**
1. Guild Overview/Details (complex, has in-page tabs)
2. Welcome Settings (baseline, clean implementation)
3. Audio/Soundboard (has internal tabs)
4. Document migration process

**Phase 3 - Main Settings Pages (Bulk Work):**
1. Members
2. Moderation Settings
3. Scheduled Messages
4. Rat Watch
5. Reminders
6. Assistant Settings
7. TTS
8. Audio Settings

**Phase 4 - Sub-Pages (Final Migration):**
1. Guild Analytics pages
2. Rat Watch Analytics & Incidents
3. Member Moderation
4. Flagged Events pages
5. Scheduled Message Create/Edit
6. Guild Edit
7. Assistant Metrics

**Phase 5 - Testing & Refinement:**
1. Responsive testing
2. Accessibility audit
3. Navigation consistency check
4. Documentation updates

---

## Migration Checklist (Per Page)

For each page being migrated:

**Planning:**
- [ ] Identify current breadcrumb structure
- [ ] Identify current header elements (title, description, actions)
- [ ] Determine which guild navigation tab should be active
- [ ] Note any special features (internal tabs, complex layouts)

**Implementation:**
1. **Update PageModel (.cshtml.cs):**
   - [ ] Add properties: `Breadcrumb`, `Header`, `Navigation`
   - [ ] Populate `Breadcrumb` with appropriate hierarchy
   - [ ] Populate `Header` with guild info, page title, description, actions
   - [ ] Populate `Navigation` with guild ID and active tab ID
   - [ ] Optional: Inherit from `GuildPageModelBase` if created

2. **Update View (.cshtml):**
   - [ ] Set `Layout = "Shared/_GuildLayout.cshtml"`
   - [ ] Remove custom breadcrumb markup
   - [ ] Remove custom header markup
   - [ ] Keep page-specific content and functionality
   - [ ] Move action buttons to `Header.Actions` if applicable

3. **Testing:**
   - [ ] Page renders correctly
   - [ ] Breadcrumb shows correct hierarchy
   - [ ] Header shows guild icon, title, description
   - [ ] Action buttons work correctly
   - [ ] Guild navigation shows with correct tab active
   - [ ] All page functionality preserved
   - [ ] Responsive behavior correct (mobile/desktop)

---

## Testing Considerations

### Unit Testing
- ViewModel validation (required properties, defaults)
- Navigation config returns correct tabs
- Breadcrumb builder creates correct hierarchy

### Integration Testing
- Guild layout components render together correctly
- Navigation links generate correct URLs
- Action buttons have correct styles based on enum

### Visual Testing
- Compare screenshots before/after migration
- Verify consistent spacing and alignment
- Check mobile dropdown appearance
- Verify active tab indicator styling

### Accessibility Testing
- Keyboard navigation through all elements
- Screen reader announces context correctly
- ARIA attributes correct (tablist, aria-selected, etc.)
- Focus indicators visible

### Browser Testing
- Test on Chrome, Firefox, Safari, Edge
- Mobile browsers (iOS Safari, Chrome Android)
- Different screen resolutions

### Responsive Testing
- Desktop: 1920px, 1440px, 1024px
- Tablet: 768px
- Mobile: 640px, 375px, 320px

---

## Risks and Mitigations

### Risk: Pages with Internal Tabs May Have Conflicts
**Impact:** Medium
**Probability:** Medium

**Description:** Pages like Guild Details and Soundboard have their own internal tab navigation using the existing `TabPanel` component. There may be confusion between the two levels of navigation.

**Mitigation:**
- Clearly document the two-level navigation pattern
- Use different styling for guild-level vs. page-level tabs
- Test these pages first in the pilot phase
- Guild-level navigation is for switching between different guild sections
- Page-level tabs are for switching content within a single page

---

### Risk: Action Button Positioning Inconsistent Across Pages
**Impact:** Low
**Probability:** Medium

**Description:** Different pages have different action buttons in different positions. Standardizing may feel unfamiliar to users.

**Mitigation:**
- Follow consistent pattern: action buttons always in header, right-aligned
- Use appropriate button styles (primary for main action, secondary for auxiliary)
- Maintain existing button functionality, just relocate visually
- Test with stakeholders during pilot phase

---

### Risk: Mobile Dropdown May Be Hard to Discover
**Impact:** Low
**Probability:** Low

**Description:** Users on mobile may not realize the navigation dropdown is available.

**Mitigation:**
- Use clear visual affordance (chevron icon indicates dropdown)
- Consider adding subtle animation on page load
- Ensure dropdown button has sufficient contrast
- Test with real mobile devices

---

### Risk: Performance Impact from Rendering All Navigation on Every Page
**Impact:** Low
**Probability:** Low

**Description:** Rendering breadcrumbs, header, and navigation on every page may add overhead.

**Mitigation:**
- Components are simple partials with minimal logic
- Navigation config is static (no database calls)
- Razor partial rendering is highly optimized
- Monitor page load times during testing

---

## Success Metrics

### Functional Metrics
- **100%** of guild pages use shared layout
- **Zero** broken navigation links
- **Zero** regression bugs
- **All** action buttons functional

### Quality Metrics
- Lighthouse accessibility score **>= 95** on all pages
- **Zero** WCAG 2.1 AA violations
- **Consistent** breadcrumb format across all pages
- **Consistent** header format across all pages

### User Experience Metrics
- Navigation tab highlighting **100%** accurate
- Mobile dropdown functional on **all** devices
- Responsive breakpoints work at **all** screen sizes
- Guild icon displays correctly (or fallback) on **all** pages

---

## Timeline Estimate

| Phase | Estimated Duration | Notes |
|-------|-------------------|-------|
| Phase 1: Infrastructure | 2-3 days | ViewModels, partials, layout, CSS, JS |
| Phase 2: Pilot Migration | 2 days | 3 pages + migration guide |
| Phase 3: Bulk Migration (Main) | 2 days | 8 main settings pages |
| Phase 4: Sub-Pages Migration | 2 days | 11 sub-pages |
| Phase 5: Testing & Refinement | 1 day | Testing, accessibility, docs |
| **Total** | **7-10 days** | Depends on complexity encountered |

---

## Future Enhancements (Out of Scope)

These are not part of v0.12.0 but could be considered for future versions:

- **Sticky Navigation:** Make guild navigation bar sticky on scroll
- **Tab Ordering Preferences:** Allow users to customize tab order
- **Keyboard Shortcuts:** Add keyboard shortcuts for tab navigation (e.g., Alt+1 for Overview)
- **Search in Navigation:** Add search/filter for guilds with many sections
- **Mobile App Bar:** Replace dropdown with bottom navigation bar on mobile
- **Breadcrumb Actions:** Add action menu to breadcrumb items
- **Tab Badges:** Add live notification badges to tabs (e.g., pending Rat Watch incidents)

---

## References

- **Requirements:** `docs/requirements/guild-header-standardization.md`
- **Design Spec:** `docs/articles/guild-layout-spec.md`
- **HTML Prototype:** `docs/prototypes/features/v0.12.0-guild-header/guild-layout-prototype.html`
- **Design System:** `docs/articles/design-system.md`
- **Existing Tab Panel:** `src/DiscordBot.Bot/ViewModels/Components/TabPanelViewModel.cs`
- **CLAUDE.md - UI Routes:** Line 122-171 in `CLAUDE.md`

---

## Notes

- **Discord Snowflake IDs:** Remember to always pass `GuildId` as string in JavaScript contexts to avoid precision loss
- **Icon Library:** Use Hero Icons (https://heroicons.com) for all navigation icons
- **Tailwind Classes:** Follow existing design token conventions from `docs/articles/design-system.md`
- **Base PageModel:** Consider creating an optional base class to reduce boilerplate in PageModel code
- **Testing:** Test early and often, especially responsive behavior and accessibility
- **Documentation:** Keep migration guide updated with lessons learned during implementation

---

_This implementation plan was created 2026-01-16 for milestone v0.12.0 - Guild Setting Header Standardization._
