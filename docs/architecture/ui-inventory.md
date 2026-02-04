# UI Inventory

**Version:** 1.0
**Last Updated:** 2026-02-03
**Target Framework:** .NET 8 Razor Pages with Tailwind CSS

---

## Overview

This document provides a comprehensive inventory of all UI components, pages, and layouts in the Discord bot project. Use this as a quick reference to understand what UI building blocks exist without diving into the codebase.

For detailed component documentation, see [Component API Usage Guide](../articles/component-api.md).

---

## Razor Page Routes

### Public/Landing Pages

| Route | File | Purpose |
|-------|------|---------|
| `/` | `Pages/Landing.cshtml` | Unauthenticated landing page |
| `/index` | `Pages/Index.cshtml` | Authenticated home/dashboard |

### Account Pages

| Route | File | Purpose |
|-------|------|---------|
| `/account/login` | `Pages/Account/Login.cshtml` | OAuth login with Discord |
| `/account/external-login` | `Pages/Account/ExternalLogin.cshtml` | External OAuth flow handler |
| `/account/link-discord` | `Pages/Account/LinkDiscord.cshtml` | Link Discord account to profile |
| `/account/logout` | `Pages/Account/Logout.cshtml` | Sign out handler |
| `/account/access-denied` | `Pages/Account/AccessDenied.cshtml` | Authorization failure page |
| `/account/lockout` | `Pages/Account/Lockout.cshtml` | Account lockout notification |
| `/account/privacy` | `Pages/Account/Privacy.cshtml` | Privacy policy page |
| `/account/profile` | `Pages/Account/Profile.cshtml` | User profile settings |

### Admin Pages

| Route | File | Purpose |
|-------|------|---------|
| `/admin/users` | `Pages/Admin/Users/Index.cshtml` | User management list |
| `/admin/users/create` | `Pages/Admin/Users/Create.cshtml` | Create new user |
| `/admin/users/edit/{id}` | `Pages/Admin/Users/Edit.cshtml` | Edit user details |
| `/admin/users/{id}` | `Pages/Admin/Users/Details.cshtml` | User details view |
| `/admin/audit-logs` | `Pages/Admin/AuditLogs/Index.cshtml` | Audit log viewer |
| `/admin/audit-logs/{id}` | `Pages/Admin/AuditLogs/Details.cshtml` | Audit log entry details |
| `/admin/message-logs` | `Pages/Admin/MessageLogs/Index.cshtml` | Message log viewer |
| `/admin/message-logs/{id}` | `Pages/Admin/MessageLogs/Details.cshtml` | Message details |
| `/admin/performance` | `Pages/Admin/Performance/Index.cshtml` | Performance metrics dashboard (tabbed) |
| `/admin/logs` | `Pages/Admin/Logs/Index.cshtml` | System logs viewer |
| `/admin/notifications` | `Pages/Admin/Notifications/Index.cshtml` | Notification center |
| `/admin/bulk-purge` | `Pages/Admin/BulkPurge.cshtml` | Bulk user/data purge tool |
| `/admin/user-purge` | `Pages/Admin/UserPurge.cshtml` | User purge utility |
| `/admin/ratwatch-analytics` | `Pages/Admin/RatWatchAnalytics.cshtml` | RatWatch analytics dashboard |

### Guild Pages (Per-Server Management)

| Route | File | Purpose |
|-------|------|---------|
| `/guild/{guildId}` | `Pages/Guilds/Index.cshtml` | Guild overview/dashboard |
| `/guild/{guildId}/welcome` | `Pages/Guilds/Welcome.cshtml` | Guild welcome page |
| `/guild/{guildId}/edit` | `Pages/Guilds/Edit.cshtml` | Guild settings editor |
| `/guild/{guildId}/members` | `Pages/Guilds/Members/Index.cshtml` | Member directory |
| `/guild/{guildId}/members/moderation/{memberId}` | `Pages/Guilds/Members/Moderation.cshtml` | Member moderation actions |
| `/guild/{guildId}/members/{memberId}` | `Pages/Guilds/Members/_MemberDetailModal.cshtml` | Member detail popup |
| `/guild/{guildId}/moderation-settings` | `Pages/Guilds/ModerationSettings/Index.cshtml` | Moderation rules configuration |
| `/guild/{guildId}/reminders` | `Pages/Guilds/Reminders/Index.cshtml` | Scheduled reminders manager |
| `/guild/{guildId}/scheduled-messages` | `Pages/Guilds/ScheduledMessages/Index.cshtml` | Scheduled messages list |
| `/guild/{guildId}/scheduled-messages/create` | `Pages/Guilds/ScheduledMessages/Create.cshtml` | Create scheduled message |
| `/guild/{guildId}/scheduled-messages/edit/{id}` | `Pages/Guilds/ScheduledMessages/Edit.cshtml` | Edit scheduled message |
| `/guild/{guildId}/analytics` | `Pages/Guilds/Analytics/Index.cshtml` | Analytics overview |
| `/guild/{guildId}/analytics/engagement` | `Pages/Guilds/Analytics/Engagement.cshtml` | Engagement metrics |
| `/guild/{guildId}/analytics/moderation` | `Pages/Guilds/Analytics/Moderation.cshtml` | Moderation analytics |
| `/guild/{guildId}/flagged-events` | `Pages/Guilds/FlaggedEvents/Index.cshtml` | Flagged events/alerts |
| `/guild/{guildId}/flagged-events/{id}` | `Pages/Guilds/FlaggedEvents/Details.cshtml` | Flagged event details |
| `/guild/{guildId}/ratwatch` | `Pages/Guilds/RatWatch/Index.cshtml` | RatWatch monitoring |
| `/guild/{guildId}/assistant-settings` | `Pages/Guilds/AssistantSettings.cshtml` | AI assistant configuration |
| `/guild/{guildId}/assistant-metrics` | `Pages/Guilds/AssistantMetrics.cshtml` | Assistant usage metrics |
| `/guild/{guildId}/soundboard` | `Pages/Guilds/Soundboard/Index.cshtml` | Soundboard management |
| `/guild/{guildId}/audio-settings` | `Pages/Guilds/AudioSettings/Index.cshtml` | Audio feature settings |
| `/guild/{guildId}/text-to-speech` | `Pages/Guilds/TextToSpeech/Index.cshtml` | TTS configuration |
| `/guild/{guildId}/vox` | `Pages/Guilds/VOX/Index.cshtml` | VOX clip management |
| `/guild/{guildId}/leaderboard` | `Pages/Guilds/PublicLeaderboard.cshtml` | Public member leaderboard |

### Commands Pages

| Route | File | Purpose |
|-------|------|---------|
| `/commands` | `Pages/Commands/Index.cshtml` | Command reference & documentation |
| `/command-logs` | `Pages/CommandLogs/Index.cshtml` | Command execution logs |

### Portal Pages (User Self-Service)

| Route | File | Purpose |
|-------|------|---------|
| `/portal/soundboard` | `Pages/Portal/Soundboard/Index.cshtml` | Public soundboard player |
| `/portal/tts` | `Pages/Portal/TTS/Index.cshtml` | Public TTS interface |
| `/portal/vox` | `Pages/Portal/VOX/Index.cshtml` | Public VOX clip player |

### Error Pages

| Route | File | Purpose |
|-------|------|---------|
| `/error/403` | `Pages/Error/403.cshtml` | Access forbidden |
| `/error/404` | `Pages/Error/404.cshtml` | Page not found |
| `/error/500` | `Pages/Error/500.cshtml` | Server error |

---

## Layouts

All layouts are located in `Pages/Shared/`.

| Layout | File | Purpose | Used By |
|--------|------|---------|---------|
| **Main Layout** | `_Layout.cshtml` | Default authenticated layout with navbar, sidebar, footer | Most admin/guild pages |
| **Landing Layout** | `_LayoutLanding.cshtml` | Unauthenticated layout for public pages | Landing, Login pages |
| **Guild Layout** | `_GuildLayout.cshtml` | Guild-specific layout with guild header/context | Guild pages under `/guild/{guildId}/*` |

### Layout Components

| Component | File | Purpose |
|-----------|------|---------|
| Navbar | `_Navbar.cshtml` | Top navigation bar with user menu |
| Sidebar | `_Sidebar.cshtml` | Left sidebar with navigation (admin/authenticated) |
| Toast Container | `_ToastContainer.cshtml` | Global toast notification container |
| Mobile Search | `_MobileSearchOverlay.cshtml` | Mobile-friendly search overlay |
| Validation Scripts | `_ValidationScriptsPartial.cshtml` | Client-side validation script inclusion |
| Breadcrumb | `_Breadcrumb.cshtml` | Navigation breadcrumb trail |
| Sort Dropdown | `_SortDropdown.cshtml` | Sort control for tables/lists |
| Setting Field | `_SettingField.cshtml` | Form field wrapper for settings pages |
| ViewStart | `_ViewStart.cshtml` | Shared view initialization |
| ViewImports | `_ViewImports.cshtml` | Shared imports (models, directives) |
| ViewStart (Portal) | `Portal/_ViewStart.cshtml` | Portal-specific view initialization |
| ViewImports (Portal) | `Portal/_ViewImports.cshtml` | Portal-specific imports |

---

## Reusable Components

All components are located in `Pages/Shared/Components/` unless noted otherwise.

### Form Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Form Input** | `_FormInput.cshtml` | Text input fields (text, email, password, search, url, tel) | `FormInputViewModel` |
| **Form Select** | `_FormSelect.cshtml` | Dropdown selection with option groups | `FormSelectViewModel` |
| **Form Toggle** | `_FormToggle.cshtml` | Toggle/checkbox switch control | `FormToggleViewModel` |
| **Autocomplete Input** | `_AutocompleteInput.cshtml` | Text input with autocomplete suggestions | `AutocompleteInputViewModel` |

### Layout & Container Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Card** | `_Card.cshtml` | Flexible container with header/body/footer | `CardViewModel` |
| **Enhanced Card** | `_EnhancedCard.cshtml` | Advanced card with additional styling options | `EnhancedCardViewModel` |
| **Guild Stats Card** | `_GuildStatsCard.cshtml` | Guild statistics display card | `GuildStatsCardViewModel` |
| **Hero Metric Card** | `_HeroMetricCard.cshtml` | Large metric/stat card for dashboards | `HeroMetricCardViewModel` |

### Navigation & Tabs

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **NavTabs** | `_NavTabs.cshtml` | Multi-tab navigation (page/in-page/AJAX modes) | `NavTabsViewModel` |
| **Tab Panel** | `_TabPanel.cshtml` | Individual tab content panel | `TabPanelViewModel` |
| **Guild Breadcrumb** | `_GuildBreadcrumb.cshtml` | Guild context breadcrumb | `GuildBreadcrumbViewModel` |
| **Command Breadcrumb** | `_CommandBreadcrumb.cshtml` | Command context breadcrumb | `CommandBreadcrumbViewModel` |

### Status & Indicators

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Status Indicator** | `_StatusIndicator.cshtml` | Online/offline/idle/busy status dot | `StatusIndicatorViewModel` |
| **Status Badge** | `_StatusBadge.cshtml` | Status displayed as badge | `StatusBadgeViewModel` |
| **Severity Badge** | `_SeverityBadge.cshtml` | Severity level indicator (error/warning/info) | `SeverityBadgeViewModel` |
| **Bot Status Card** | `_BotStatusCard.cshtml` | Bot online status display | `BotStatusCardViewModel` |
| **Bot Status Banner** | `_BotStatusBanner.cshtml` | Bot status banner for page top | `BotStatusBannerViewModel` |
| **Connection Status** | `_ConnectionStatus.cshtml` | WebSocket/API connection status | `ConnectionStatusViewModel` |
| **Restart Banner** | `_RestartBanner.cshtml` | Bot restart in-progress banner | `RestartBannerViewModel` |

### Data Display Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Badge** | `_Badge.cshtml` | Small labeled tag/status indicator | `BadgeViewModel` |
| **Rule Type Icon** | `_RuleTypeIcon.cshtml` | Rule type visual indicator | `RuleTypeIconViewModel` |
| **Pagination** | `_Pagination.cshtml` | Page navigation with first/prev/next/last | `PaginationViewModel` |
| **Activity Feed** | `_ActivityFeed.cshtml` | List of activity/event items | `ActivityFeedViewModel` |
| **Activity Feed Timeline** | `_ActivityFeedTimeline.cshtml` | Vertical timeline of activities | `ActivityFeedTimelineViewModel` |
| **Audit Log Card** | `_AuditLogCard.cshtml` | Audit log entry display card | `AuditLogCardViewModel` |
| **Recent Activity Card** | `_RecentActivityCard.cshtml` | Recent activity summary widget | `RecentActivityCardViewModel` |
| **Command Stats Card** | `_CommandStatsCard.cshtml` | Command execution statistics | `CommandStatsCardViewModel` |
| **Connected Servers Widget** | `_ConnectedServersWidget.cshtml` | List of connected Discord servers | `ConnectedServersWidgetViewModel` |

### Feedback Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Alert** | `_Alert.cshtml` | Info/success/warning/error message banner | `AlertViewModel` |
| **Button** | `_Button.cshtml` | Interactive button (primary/secondary/danger/ghost) | `ButtonViewModel` |
| **Loading Spinner** | `_LoadingSpinner.cshtml` | Loading indicator (simple/dots/pulse) | `LoadingSpinnerViewModel` |
| **Skeleton** | `_Skeleton.cshtml` | Content placeholder during loading | `SkeletonViewModel` |
| **Skeleton Card** | `_SkeletonCard.cshtml` | Card-shaped skeleton loader | `SkeletonCardViewModel` |
| **Page Loading Overlay** | `_PageLoadingOverlay.cshtml` | Full-page loading overlay with backdrop | `PageLoadingOverlayViewModel` |
| **Empty State** | `_EmptyState.cshtml` | No data/no results/error state display | `EmptyStateViewModel` |
| **Confirmation Modal** | `_ConfirmationModal.cshtml` | Confirmation dialog with yes/no actions | `ConfirmationModalViewModel` |
| **Typed Confirmation Modal** | `_TypedConfirmationModal.cshtml` | Enhanced confirmation requiring text input | `TypedConfirmationModalViewModel` |
| **Pause Modal** | `_PauseModal.cshtml` | Pause/resume action dialog | `PauseModalViewModel` |

### Specialized Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Command Header** | `_CommandHeader.cshtml` | Command title/description header | `CommandHeaderViewModel` |
| **Command Log Details Modal** | `_CommandLogDetailsModal.cshtml` | Modal for command log entry details | `CommandLogDetailsModalViewModel` |
| **Dashboard Widget** | `_DashboardWidget.cshtml` | Generic dashboard widget container | `DashboardWidgetViewModel` |
| **Quick Actions Card** | `_QuickActionsCard.cshtml` | Card with action buttons/links | `QuickActionsCardViewModel` |
| **Guild Header** | `_GuildHeader.cshtml` | Guild name/icon header | `GuildHeaderViewModel` |
| **Voice Channel Panel** | `_VoiceChannelPanel.cshtml` | Voice channel list/control panel | `VoiceChannelPanelViewModel` |
| **Toast Container** | `_ToastContainer.cshtml` | Global toast notification area | `ToastContainerViewModel` |

### TTS & Audio Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **SSML Preview** | `_SsmlPreview.cshtml` | SSML text preview and editor | `SsmlPreviewViewModel` |
| **Mode Switcher** | `_ModeSwitcher.cshtml` | Switch between text/SSML modes | `ModeSwitcherViewModel` |
| **Style Selector** | `_StyleSelector.cshtml` | TTS voice style selector | `StyleSelectorViewModel` |
| **Preset Bar** | `_PresetBar.cshtml` | Preset voice/style quick selector | `PresetBarViewModel` |
| **Emphasis Toolbar** | `_EmphasisToolbar.cshtml` | SSML emphasis/prosody editor toolbar | `EmphasisToolbarViewModel` |

### User/Guild Preview Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **User Preview Popup** | `_UserPreviewPopup.cshtml` | User card preview (name, avatar, info) | `UserPreviewPopupViewModel` |
| **Guild Preview Popup** | `_GuildPreviewPopup.cshtml` | Guild card preview (name, icon, stats) | `GuildPreviewPopupViewModel` |
| **Preview Popup Loading** | `_PreviewPopupLoading.cshtml` | Loading state for preview popup | `PreviewPopupLoadingViewModel` |
| **Preview Popup Error** | `_PreviewPopupError.cshtml` | Error state for preview popup | `PreviewPopupErrorViewModel` |

---

## Component Groups by Feature

### Admin Dashboard

**Pages:**
- `/admin/performance` - Main dashboard with tabbed metrics

**Components:**
- NavTabs (tabbed interface)
- HealthTab, HealthMetricsTab, OverviewTab
- ApiTab, ApiMetricsTab, CommandsTab
- Card, HeroMetricCard, GuildStatsCard

**Purpose:** System health, performance metrics, command stats, API usage monitoring

### User Management

**Pages:**
- `/admin/users` - User list with pagination
- `/admin/users/create` - Create user form
- `/admin/users/edit/{id}` - Edit user form
- `/admin/users/{id}` - User details view

**Components:**
- FormInput, FormSelect, FormToggle
- Button, Alert
- Badge (for status/roles)
- Pagination
- Card

**Purpose:** CRUD operations for system users

### Guild Management

**Pages:**
- `/guild/{guildId}` - Guild dashboard
- `/guild/{guildId}/members` - Member directory with filters
- `/guild/{guildId}/edit` - Guild settings
- `/guild/{guildId}/moderation-settings` - Moderation configuration
- `/guild/{guildId}/analytics/*` - Guild analytics with multiple views

**Components:**
- Guild layout with context
- NavTabs for multi-section pages
- FormInput, FormSelect (settings forms)
- Card, EnhancedCard
- StatusIndicator, StatusBadge
- EmptyState, LoadingSpinner
- Pagination (for member lists)
- ActivityFeed, AuditLogCard

**Purpose:** Per-server configuration and analytics

### Audio/Soundboard

**Pages:**
- `/guild/{guildId}/soundboard` - Soundboard manager
- `/guild/{guildId}/text-to-speech` - TTS settings
- `/guild/{guildId}/vox` - VOX clip library
- `/portal/soundboard` - Public soundboard
- `/portal/tts` - Public TTS player
- `/portal/vox` - Public VOX player

**Components:**
- FormInput, FormSelect, FormToggle
- Button (play/record/delete actions)
- Card (sound item display)
- SSML-related components (StyleSelector, ModeSwitcher, EmphasisToolbar, SsmlPreview)
- PresetBar, ModeSwitch
- StatusIndicator (playback state)

**Purpose:** Audio playback, TTS configuration, VOX management

### Logging & Analytics

**Pages:**
- `/admin/audit-logs` - Audit log viewer
- `/admin/audit-logs/{id}` - Audit log details
- `/admin/message-logs` - Message logs
- `/admin/message-logs/{id}` - Message details
- `/commands` - Command documentation
- `/command-logs` - Command execution logs
- `/guild/{guildId}/analytics/engagement` - Engagement metrics
- `/guild/{guildId}/analytics/moderation` - Moderation analytics
- `/guild/{guildId}/ratwatch` - RatWatch monitoring
- `/admin/ratwatch-analytics` - RatWatch analytics

**Components:**
- NavTabs (multi-view analytics)
- Card, HeroMetricCard, CommandStatsCard
- AuditLogCard
- Pagination
- EmptyState, LoadingSpinner
- StatusBadge, SeverityBadge, Badge
- ActivityFeed, ActivityFeedTimeline
- FilterPanel (date range, category filters)

**Purpose:** Activity tracking, metrics visualization, moderation logs

---

## Page Hierarchy Diagram

```
/
├── Landing (unauthenticated)
│
├── Account
│   ├── Login
│   ├── ExternalLogin
│   ├── LinkDiscord
│   ├── Logout
│   ├── Profile
│   ├── Privacy
│   ├── AccessDenied
│   └── Lockout
│
├── Index (authenticated home)
│
├── Admin (SuperAdmin role)
│   ├── Users
│   │   ├── Index (list)
│   │   ├── Create
│   │   ├── Edit
│   │   └── Details
│   ├── AuditLogs
│   │   ├── Index
│   │   └── Details
│   ├── MessageLogs
│   │   ├── Index
│   │   └── Details
│   ├── Performance (tabbed)
│   │   ├── Overview
│   │   ├── Health
│   │   ├── HealthMetrics
│   │   ├── API
│   │   ├── APIMetrics
│   │   └── Commands
│   ├── Logs
│   ├── Notifications
│   ├── BulkPurge
│   ├── UserPurge
│   └── RatWatchAnalytics
│
├── Guild/{guildId} (per-server pages)
│   ├── Index
│   ├── Welcome
│   ├── Edit
│   ├── Members
│   │   ├── Index
│   │   ├── Moderation
│   │   └── _MemberDetailModal
│   ├── ModerationSettings
│   ├── Reminders
│   ├── ScheduledMessages
│   │   ├── Index
│   │   ├── Create
│   │   └── Edit
│   ├── Analytics
│   │   ├── Index
│   │   ├── Engagement
│   │   └── Moderation
│   ├── FlaggedEvents
│   │   ├── Index
│   │   └── Details
│   ├── RatWatch
│   ├── AssistantSettings
│   ├── AssistantMetrics
│   ├── Soundboard
│   ├── AudioSettings
│   ├── TextToSpeech
│   ├── VOX
│   ├── PublicLeaderboard
│
├── Commands
│   ├── Index
│
├── CommandLogs
│   ├── Index
│   └── _CommandLogDetailsContent
│
├── Portal (public/user pages)
│   ├── Soundboard
│   ├── TTS
│   └── VOX
│
└── Error
    ├── 403
    ├── 404
    └── 500
```

---

## Layout Usage Map

| Layout | Routes | Key Feature |
|--------|--------|-------------|
| **_LayoutLanding** | `/`, `/account/login`, `/account/external-login`, `/account/link-discord` | Public pages with minimal chrome |
| **_Layout** | Admin, Command, Home pages | Full nav + sidebar authenticated layout |
| **_GuildLayout** | All `/guild/{guildId}/*` routes | Guild context header + nav |
| **Portal (_ViewStart)** | `/portal/*` routes | Portal-specific initialization |

---

## Key Component Relationships

### Form Validation Flow

1. **FormInput/FormSelect** capture user input
2. **Button** (type="submit") submits the form
3. **Alert** (Variant=Error) displays server validation errors
4. **ValidationScriptsPartial** enables client-side validation

### Data Display Flow

1. **Pagination** divides data into pages
2. **NavTabs** or filter controls change data view
3. **Card** containers display individual items
4. **Badge/StatusIndicator** annotate items with metadata
5. **EmptyState** shows when no data available
6. **LoadingSpinner** indicates async operations

### Navigation Flow

1. **Navbar** provides top-level navigation
2. **Sidebar** provides admin/authenticated navigation
3. **NavTabs** handle section navigation within pages
4. **Breadcrumb** shows navigation context
5. **Button** with `OnClick` triggers page navigation

---

## Shared Utilities

### JavaScript Modules

| Module | Location | Purpose |
|--------|----------|---------|
| Filter Panel | `wwwroot/js/shared/filter-panel.js` | Collapsible filters + date presets |
| NavTabs | `wwwroot/js/shared/nav-tabs.js` | Tab switching (page/in-page/AJAX) |
| Toast System | `wwwroot/js/shared/toast.js` | Toast notifications API |
| Preview Popup | `wwwroot/js/shared/preview-popup.js` | User/guild preview cards |

### CSS Framework

- **Tailwind CSS** for all styling
- **Design System tokens** in `design-system.md`
- **Color classes:** `bg-accent-orange`, `bg-bg-primary`, `text-text-secondary`, etc.
- **Spacing:** Tailwind standard scale (1 = 4px)

---

## Component Statistics

| Category | Count |
|----------|-------|
| Razor Pages | 60+ |
| Layout Templates | 3 |
| Reusable Components | 45+ |
| Form Components | 4 |
| Data Display Components | 12 |
| Feedback Components | 10 |
| Navigation Components | 4 |
| Specialized Components | 7+ |
| Preview/Popup Components | 4 |

---

## Navigation Reference

### For AI Agents (Claude)

When working on features, use these pages as entry points:

- **Need to add a form?** Look at `/admin/users/create` or `/guild/{guildId}/edit`
- **Need to display a list?** Look at `/admin/users` or `/admin/audit-logs`
- **Need tabbed navigation?** See `/admin/performance` or `/guild/{guildId}/analytics`
- **Need modals/popups?** See `_MemberDetailModal.cshtml` or `_ConfirmationModal.cshtml`
- **Need real-time status?** See `_StatusIndicator` and `_BotStatusCard`
- **Need to show activity?** See `_ActivityFeed` and `_AuditLogCard`

---

## See Also

- **[Component API Usage Guide](../articles/component-api.md)** - Detailed component documentation with examples
- **[Design System](../articles/design-system.md)** - Color palette, typography, tokens
- **[Form Implementation Standards](../articles/form-implementation-standards.md)** - Form patterns and validation
- **[Authorization Policies](../articles/authorization-policies.md)** - Role-based access control
- **[Interactive Components](../articles/interactive-components.md)** - Discord button interactions

---

**Maintained by:** UI Development Team
**Last Updated:** 2026-02-03
**Status:** Complete
