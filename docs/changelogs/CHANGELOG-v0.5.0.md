# Changelog: v0.4.0 → v0.5.0

**Release Date:** TBD (Development)
**PRs Merged:** 63

---

## Highlights

This release introduces major new features including a comprehensive **Bot Performance Dashboard**, **Enhanced Global Search**, **Reminder System**, **Member Directory**, and a full **Moderation System** with auto-detection capabilities.

---

## New Features

### Bot Performance Dashboard (Epic #295)
A comprehensive monitoring and alerting system for bot health and performance.

- **Health Metrics Dashboard** (`/Admin/Performance/HealthMetrics`) - Real-time bot health monitoring with uptime, latency, and connection status (#584)
- **Command Performance Analytics** (`/Admin/Performance/Commands`) - Response time tracking (Avg, P50, P95, P99), throughput charts, slowest commands, and timeout detection (#586)
- **Discord API & Rate Limit Monitoring** (`/Admin/Performance/ApiMetrics`) - API latency tracking, rate limit hit tracking, and usage by category (#587)
- **System Health Monitoring** (`/Admin/Performance/SystemHealth`) - Database performance, memory/GC metrics, cache statistics, and background service health (#585)
- **Performance Alerts & Incidents** (`/Admin/Performance/Alerts`) - Configurable threshold-based alerting with incident lifecycle management and auto-recovery (#588)
- **Performance Dashboard Overview** (`/Admin/Performance`) - Unified view of all performance metrics with quick navigation (#589)
- **Historical Metrics System** (#613) - Complete time-series metrics infrastructure:
  - Data layer for time-series metric storage with aggregation support (#614)
  - `MetricsCollectionService` background service for periodic metrics capture (#615)
  - REST API endpoints for historical metrics queries (#616)
  - System Health UI charts updated to display real historical data (#617)
  - Comprehensive system documentation (#618)
- **MonitoredBackgroundService Base Class** - Unified health tracking for all 17 background services (#602)

### Enhanced Global Search (#328)
Comprehensive search across all application resources.

- Search across 7 categories: Guilds, Commands, Command Logs, Users, Audit Logs, Message Logs, and Navigation Pages (#590)
- Keyboard shortcuts: `Ctrl+K` / `Cmd+K` to focus, `/` when not in text field, `Escape` to clear
- XSS-safe search result highlighting with custom tag helper
- Recent searches stored in localStorage (up to 5)
- Mobile-friendly full-screen search overlay
- Authorization-aware filtering (admin categories hidden from non-admins)

### Reminder System (#296)
User-configurable reminders with natural language time parsing.

- **`/remind` Command** - Create reminders with natural language time parsing ("in 2 hours", "tomorrow at 3pm") (#537)
- **Time Parsing Service** - Robust parsing for relative and absolute time expressions (#536)
- **Reminder Execution Service** - Background service for scheduled reminder delivery (#536)
- **Reminders Admin UI** (`/Guilds/{guildId}/Reminders`) - Guild reminder management page (#538)
- **Reminder Entity & Repository** - Data layer with proper EF Core configuration (#534)

### Member Directory (#296)
Guild member management and synchronization.

- **Member Directory UI** (`/Guilds/{guildId}/Members`) - Searchable member list with filtering and detail modal (#484)
- **Member API Endpoints** - REST API for member queries and management (#480)
- **GuildMember Service Layer** - Business logic for member operations (#478)
- **Member Sync Background Service** - Automatic synchronization from Discord gateway events (#477)
- **GuildMember Entity** - Data model with Discord metadata support (#475)

### Moderation System (Epic #474)
Comprehensive moderation tools with auto-detection.

- **Moderation Commands** - `/warn`, `/mute`, `/kick`, `/ban` with note/tag support (#497)
- **Auto-Detection Services** - Spam detection, banned word filtering, raid protection (#497)
- **Flagged Events UI** (`/Guilds/{guildId}/FlaggedEvents`) - Review and action flagged content (#500)
- **User Moderation Profile** - View user moderation history and notes (#501)
- **Guild Moderation Settings** (`/Guilds/{guildId}/ModerationSettings`) - Configure auto-mod rules (#502)
- **Moderation REST API** - Endpoints for programmatic moderation access (#499)
- **Moderation Data Layer** - Entities, repositories, and migrations (#496)

### Analytics Dashboards (#558)
Data visualization for guild activity.

- **Analytics Infrastructure** - Aggregation services for metrics rollup (#577)
- **Server Analytics** - Guild-level activity metrics and trends (#578)
- **Moderation Analytics** - Moderation action trends and patterns (#578)
- **Engagement Analytics** - User engagement and activity metrics (#578)

### Utility Commands (#527)
Informational slash commands.

- **`/userinfo`** - Display user profile information (#532)
- **`/serverinfo`** - Display guild information and statistics (#532)
- **`/roleinfo`** - Display role details and permissions (#532)

### Rat Watch Enhancements
Additional features for the accountability system.

- **Global Rat Watch Analytics** (`/Admin/RatWatchAnalytics`) - Cross-guild metrics (#458)
- **Public Leaderboard** (`/Guilds/{guildId}/Leaderboard`) - Public-facing accountability leaderboard (#458)
- **CSV Export** - Export incident data for external analysis (#459)
- **Enhanced Incidents Browser** - Improved filtering and navigation (#457)

---

## Improvements

### UI/UX Improvements
- Settings page save button with visual feedback (#461)
- Navigation updates for Rat Watch Analytics pages (#460)
- Semantic CSS and accessibility improvements for moderation pages (#516, #517)
- SettingDefinitions fallback handling (#482)

### Performance & Reliability
- Performance metrics collection infrastructure with API endpoints (#583)
- Command performance API extended to support 30-day time range (#601)
- Fixed singleton detection services DI scoping (#498)

### Developer Experience
- Bot Performance Dashboard HTML prototypes (#582)
- Member Directory HTML prototypes (#476)
- Moderation UI prototypes (#495)
- Utility command designs and prototypes (#530)

---

## Bug Fixes

### Performance Dashboard Fixes
- Fix timezone display on all Performance dashboard pages (#604, #605)
- Fix Alert Frequency chart JSON casing issue (#620)
- Fix current metric values display in Alert Configuration (#606)
- Fix missing icons on Quick Actions buttons (#600)
- Remove tab scrollbar and add sample data indicator (#621)
- Fix alert icons rendering too large in Recent Alerts section (#646)
- Fix error rate chart Y-axis percentage values not rounded (#646)

### Search Fixes
- Fix search querystring parameter mismatch (`q` vs `query`) (#619)
- Fix command search display issues (#619)

### Analytics Fixes
- Fix Server Analytics showing 0 messages while Engagement shows correct count (#581)

### Logging Fixes
- Fix grouped slash commands logging with full path (#557)

### Moderation Fixes
- Resolve moderation settings page bugs (#515)

---

## Testing

- Utility Commands Test Suite - 30 new tests for reminder system (#640)

---

## Documentation

- Documentation overhaul for v0.4.0 (#462)
- Member Directory feature documentation (#494)
- Utility Commands and Reminder System documentation (#645)
- Historical Metrics System documentation (#639)
- Bot Performance Dashboard documentation
- Updated API endpoints documentation

---

## Technical Details

### New Pages Added
| Route | Description |
|-------|-------------|
| `/Admin/Performance` | Performance Dashboard Overview |
| `/Admin/Performance/HealthMetrics` | Bot Health Metrics |
| `/Admin/Performance/Commands` | Command Performance Analytics |
| `/Admin/Performance/ApiMetrics` | API Rate Limit Monitoring |
| `/Admin/Performance/SystemHealth` | System Health Monitoring |
| `/Admin/Performance/Alerts` | Performance Alerts & Incidents |
| `/Guilds/{guildId}/Members` | Member Directory |
| `/Guilds/{guildId}/Reminders` | Guild Reminders Management |
| `/Guilds/{guildId}/ModerationSettings` | Guild Moderation Settings |
| `/Guilds/{guildId}/FlaggedEvents` | Flagged Events Admin |
| `/Guilds/{guildId}/Leaderboard` | Public Rat Watch Leaderboard |
| `/Admin/RatWatchAnalytics` | Global Rat Watch Analytics |
| `/Search` | Enhanced Global Search |

### New Commands Added
| Command | Description |
|---------|-------------|
| `/remind` | Set a reminder with natural language time |
| `/userinfo` | Display user profile information |
| `/serverinfo` | Display guild information |
| `/roleinfo` | Display role details |
| `/warn` | Issue a warning to a user |
| `/mute` | Mute a user |
| `/kick` | Kick a user from the guild |
| `/ban` | Ban a user from the guild |

### New Background Services
- `ReminderExecutionService` - Scheduled reminder delivery
- `MemberSyncService` - Discord member synchronization
- `AlertMonitoringService` - Performance alert monitoring
- `MetricsCollectionService` - Periodic historical metrics capture
- Various analytics aggregation services

### Infrastructure
- `MonitoredBackgroundService` base class for unified health tracking
- `ISearchService` for centralized search orchestration
- `IMetricsProvider` interface for exposing metrics
- `MetricsCollectionService` for periodic metrics capture (configurable interval, defaults to 5 minutes)
- Historical metrics data layer with `HistoricalMetric` entity and `IHistoricalMetricsRepository`
- `IHistoricalMetricsService` for querying aggregated time-series data

---

## Breaking Changes

None

---

## Contributors

- @cpike5

---

## Full PR List

| PR | Title |
|----|-------|
| #646 | fix: Performance page UI fixes for alert icons and chart Y-axis |
| #645 | docs: Add Utility Commands and Reminder System documentation |
| #640 | Testing: Utility Commands Test Suite |
| #639 | docs: Add Historical Metrics System documentation |
| #638 | feat(ui): Update System Health charts to display real historical data |
| #637 | feat(api): Add historical metrics API endpoints |
| #635 | feat(metrics): Add metrics collection background service |
| #634 | feat(metrics): Add historical metrics data infrastructure |
| #621 | fix(ui): Remove tab scrollbar and add sample data indicator |
| #620 | fix(alerts): Use camelCase JSON serialization for Alert Frequency chart |
| #619 | fix(search): Resolve querystring parameter mismatch and command display issues |
| #606 | fix(ui): Display current metric values and add save button for Alert Configuration |
| #605 | fix(ui): Fix timezone display on all Performance dashboard pages |
| #604 | fix(ui): Display Alerts page timestamps in user's local timezone |
| #602 | fix(services): Implement MonitoredBackgroundService base class for unified health tracking |
| #601 | fix(api): Extend command performance API to support 30-day time range |
| #600 | fix(ui): Missing icons on Quick Actions buttons in Performance page |
| #590 | feat: Enhanced Global Search across all resources (#328) |
| #589 | feat: Add Performance Dashboard Overview page |
| #588 | feat: Add Performance Alerts & Incidents dashboard (#570) |
| #587 | feat: Add Discord API & Rate Limit Monitoring dashboard |
| #586 | feat: Add Command Performance Analytics dashboard page |
| #585 | feat: System Health Monitoring Dashboard |
| #584 | feat: Add Bot Health Metrics Dashboard page |
| #583 | feat: Add performance metrics collection infrastructure and API endpoints |
| #582 | feat: Add Bot Performance Dashboard HTML prototypes |
| #581 | fix: Server Analytics shows 0 messages while Engagement shows correct count |
| #578 | feat: Add analytics dashboards for server, moderation, and engagement metrics |
| #577 | feat: Add analytics infrastructure for data aggregation (issue #558) |
| #557 | fix: Log grouped slash commands with full path |
| #538 | feat: Add Reminders admin page for guild reminder management |
| #537 | feat: Implement /remind command module |
| #536 | feat: Add Time Parsing Service and Reminder Execution Service |
| #535 | feat: Utility Commands - /userinfo, /serverinfo, /roleinfo and Reminder Infrastructure |
| #534 | Implement Reminder Entity, Repository, and Configuration |
| #533 | Prototype: Reminders Admin UI Page |
| #532 | feat: Implement utility commands (/userinfo, /serverinfo, /roleinfo) |
| #530 | docs: Add utility command designs and prototypes |
| #517 | fix: Polish moderation UI with semantic CSS, accessibility, and empty states |
| #516 | fix: Improve accessibility for moderation pages |
| #515 | fix: Resolve moderation settings page bugs |
| #502 | Feature: Guild Moderation Settings UI |
| #501 | feat: Add User Moderation Profile UI page |
| #500 | feat: Flagged Events Admin UI |
| #499 | Feature: Moderation REST API Endpoints |
| #498 | fix: Use IServiceScopeFactory in singleton detection services |
| #497 | feat: Add moderation commands, notes/tags, and auto-detection services |
| #496 | Feature: Data Layer - Moderation Entities & Migrations |
| #495 | Task: Create Moderation UI Prototypes |
| #494 | docs: Document Member Directory feature |
| #484 | feat: Add member directory Razor page and detail modal (#470, #471) |
| #482 | fix: Add SettingDefinitions fallback in GetSettingValueAsync |
| #480 | feat: Add member directory API endpoints (#468) |
| #478 | feat: Add GuildMember service layer for member directory (#467) |
| #477 | feat: Add member sync background service and gateway event handlers |
| #476 | Create Member Directory HTML prototypes |
| #475 | feat: Add GuildMember entity and User Discord metadata |
| #462 | docs: Documentation Overhaul for v0.4.0 |
| #461 | Enhance settings page save button with visual feedback |
| #460 | [Feature] Update navigation for Rat Watch Analytics pages |
| #459 | [Feature] Implement CSV export for Rat Watch Incidents |
| #458 | feat(ratwatch): Add global analytics and public leaderboard pages |
| #457 | feat(ratwatch): Rat Watch enhancements - Analytics, Incidents, and UI standardization |
