# Roadmap

This document outlines the development roadmap for the Discord Bot Management System. It provides a high-level view of completed work, current focus areas, and future enhancements.

**Current Version:** v0.5.4-dev

---

## Completed (v0.2.0)

### Core Foundation
- [x] Discord bot with slash command framework (Discord.NET 3.18.0)
- [x] Three-layer clean architecture (Core, Infrastructure, Bot)
- [x] Entity Framework Core with SQLite (production-ready for MSSQL/MySQL/PostgreSQL)
- [x] Repository pattern for data access
- [x] Serilog structured logging with console and file sinks

### Discord Bot Features
- [x] Automatic command discovery and registration
- [x] Interactive components (buttons, select menus, modals)
- [x] Component state management with expiry
- [x] Permission preconditions (RequireAdmin, RequireOwner, RateLimit)
- [x] Commands: `/ping`, `/status`, `/guilds`, `/verify`, `/shutdown`, `/admin info|kick|ban`

### Admin Web UI
- [x] Razor Pages with Tailwind CSS styling
- [x] Dashboard with bot status, stats, and activity feed
- [x] Guild management (list, detail, edit, sync)
- [x] Command log viewer and analytics
- [x] User management with CRUD operations (SuperAdmin)
- [x] Bot control panel (start/stop/restart)
- [x] Application settings page
- [x] Custom error pages (403, 404, 500)

### Authentication & Authorization
- [x] ASP.NET Core Identity integration
- [x] Discord OAuth external login
- [x] Discord account verification via bot command
- [x] Role hierarchy: SuperAdmin > Admin > Moderator > Viewer
- [x] Policy-based authorization with guild access control

### REST API
- [x] Health, Bot, Guilds, CommandLogs endpoints
- [x] Swagger/OpenAPI documentation

### UI/UX Improvements
- [x] Design system with dark theme and color tokens
- [x] 24 reusable Razor partial components
- [x] Toast notification system ([#87](https://github.com/cpike5/discordbot/issues/87))
- [x] Loading states for async operations ([#88](https://github.com/cpike5/discordbot/issues/88))
- [x] WCAG 2.1 AA accessibility compliance ([#89](https://github.com/cpike5/discordbot/issues/89))

### UI Polish ([#168](https://github.com/cpike5/discordbot/issues/168))
- [x] Remove/fix placeholder navigation links in sidebar ([#161](https://github.com/cpike5/discordbot/issues/161))
- [x] Fix placeholder links in user dropdown menu ([#162](https://github.com/cpike5/discordbot/issues/162))
- [x] Remove or implement global search ([#163](https://github.com/cpike5/discordbot/issues/163))
- [x] Fix placeholder links on login page ([#164](https://github.com/cpike5/discordbot/issues/164))
- [x] Make bot version in sidebar dynamic ([#165](https://github.com/cpike5/discordbot/issues/165))
- [x] Remove/replace decorative notifications bell ([#166](https://github.com/cpike5/discordbot/issues/166))
- [x] Restrict Components showcase to developers ([#167](https://github.com/cpike5/discordbot/issues/167))

### Observability Enhancement ([#159](https://github.com/cpike5/discordbot/issues/159))
Comprehensive logging, metrics, and distributed tracing for production readiness.

- [x] Repository logging with performance tracking ([#99](https://github.com/cpike5/discordbot/issues/99))
- [x] Correlation ID middleware for API requests ([#100](https://github.com/cpike5/discordbot/issues/100))
- [x] EF Core query performance logging ([#101](https://github.com/cpike5/discordbot/issues/101))
- [x] Log sanitization for sensitive data ([#102](https://github.com/cpike5/discordbot/issues/102))
- [x] Rate limit logging for abuse detection ([#103](https://github.com/cpike5/discordbot/issues/103))
- [x] OpenTelemetry metrics collection ([#104](https://github.com/cpike5/discordbot/issues/104))
- [x] Distributed tracing with OpenTelemetry ([#105](https://github.com/cpike5/discordbot/issues/105))
- [x] Centralized log aggregation - Seq ([#106](https://github.com/cpike5/discordbot/issues/106))
- [x] CommandLogService optimization ([#107](https://github.com/cpike5/discordbot/issues/107))
- [x] Environment-specific configuration ([#108](https://github.com/cpike5/discordbot/issues/108))
- [x] Advanced metrics and business KPIs ([#109](https://github.com/cpike5/discordbot/issues/109))

### User Consent & Privacy ([#130](https://github.com/cpike5/discordbot/issues/130))
GDPR-compliant privacy framework with user consent management.

- [x] Consent domain model & repository ([#132](https://github.com/cpike5/discordbot/issues/132))
- [x] Consent slash commands - `/consent`, `/privacy` ([#133](https://github.com/cpike5/discordbot/issues/133))
- [x] Consent check service integration ([#135](https://github.com/cpike5/discordbot/issues/135))
- [x] Consent web UI management ([#134](https://github.com/cpike5/discordbot/issues/134))

### Message Logging System ([#136](https://github.com/cpike5/discordbot/issues/136))
Event-driven message capture with consent integration.

- [x] Message log domain model & repository ([#137](https://github.com/cpike5/discordbot/issues/137))
- [x] Message received event handler ([#138](https://github.com/cpike5/discordbot/issues/138))
- [x] Message log admin UI ([#139](https://github.com/cpike5/discordbot/issues/139))
- [x] Message log API endpoints ([#140](https://github.com/cpike5/discordbot/issues/140))
- [x] Message log retention & cleanup ([#141](https://github.com/cpike5/discordbot/issues/141))

### APM & Tracing ([#92](https://github.com/cpike5/discordbot/issues/92))
Application Performance Monitoring with Elastic APM integration.

- [x] APM foundation setup ([#93](https://github.com/cpike5/discordbot/issues/93))
- [x] Discord interaction tracing ([#94](https://github.com/cpike5/discordbot/issues/94))
- [x] Service layer span instrumentation ([#95](https://github.com/cpike5/discordbot/issues/95))
- [x] Bot lifecycle event tracing ([#96](https://github.com/cpike5/discordbot/issues/96))
- [x] Custom metrics collection ([#97](https://github.com/cpike5/discordbot/issues/97))
- [x] Production optimization & alerting ([#98](https://github.com/cpike5/discordbot/issues/98))

### Infrastructure & Documentation
- [x] Strongly-typed configuration options classes ([#131](https://github.com/cpike5/discordbot/issues/131))
- [x] Versioning strategy for builds and releases ([#175](https://github.com/cpike5/discordbot/issues/175))
- [x] DocFX API documentation setup ([#111](https://github.com/cpike5/discordbot/issues/111))
- [x] Component API usage guide ([#115](https://github.com/cpike5/discordbot/issues/115))

---

## Completed (v0.3.0)

### Welcome System ([#216](https://github.com/cpike5/discordbot/issues/216))
Configurable welcome messages and role assignment for new members.

- [x] UI Prototype ([#212](https://github.com/cpike5/discordbot/issues/212))
- [x] Entity and database schema ([#224](https://github.com/cpike5/discordbot/issues/224))
- [x] Repository ([#225](https://github.com/cpike5/discordbot/issues/225))
- [x] DTOs ([#226](https://github.com/cpike5/discordbot/issues/226))
- [x] WelcomeService ([#227](https://github.com/cpike5/discordbot/issues/227))
- [x] Event handler ([#228](https://github.com/cpike5/discordbot/issues/228))
- [x] API controller ([#229](https://github.com/cpike5/discordbot/issues/229))
- [x] Admin configuration page ([#230](https://github.com/cpike5/discordbot/issues/230))
- [x] `/welcome` slash command ([#231](https://github.com/cpike5/discordbot/issues/231))
- [x] Tests ([#232](https://github.com/cpike5/discordbot/issues/232))

### Scheduled Messages ([#217](https://github.com/cpike5/discordbot/issues/217))
Timed announcements and recurring message scheduling.

- [x] UI Prototype ([#213](https://github.com/cpike5/discordbot/issues/213))
- [x] Entity and database schema ([#233](https://github.com/cpike5/discordbot/issues/233))
- [x] Repository ([#234](https://github.com/cpike5/discordbot/issues/234))
- [x] DTOs ([#235](https://github.com/cpike5/discordbot/issues/235))
- [x] ScheduledMessageService ([#236](https://github.com/cpike5/discordbot/issues/236))
- [x] Background executor service ([#237](https://github.com/cpike5/discordbot/issues/237))
- [x] API controller ([#238](https://github.com/cpike5/discordbot/issues/238))
- [x] List admin page ([#239](https://github.com/cpike5/discordbot/issues/239))
- [x] Create/edit admin pages ([#240](https://github.com/cpike5/discordbot/issues/240))
- [x] `/schedule` slash commands ([#241](https://github.com/cpike5/discordbot/issues/241))
- [x] Tests ([#242](https://github.com/cpike5/discordbot/issues/242))

### Real-time Dashboard ([#218](https://github.com/cpike5/discordbot/issues/218))
SignalR-powered live updates for dashboard statistics.

- [x] UI Prototype ([#214](https://github.com/cpike5/discordbot/issues/214))
- [x] SignalR infrastructure ([#243](https://github.com/cpike5/discordbot/issues/243))
- [x] DTOs ([#244](https://github.com/cpike5/discordbot/issues/244))
- [x] Dashboard update service ([#245](https://github.com/cpike5/discordbot/issues/245))
- [x] Bot event integration ([#246](https://github.com/cpike5/discordbot/issues/246))
- [x] SignalR JavaScript client ([#247](https://github.com/cpike5/discordbot/issues/247))
- [x] Dashboard page updates ([#248](https://github.com/cpike5/discordbot/issues/248))
- [x] Stats API endpoint ([#249](https://github.com/cpike5/discordbot/issues/249))
- [x] Tests ([#250](https://github.com/cpike5/discordbot/issues/250))

### Audit Log Viewer ([#219](https://github.com/cpike5/discordbot/issues/219))
Comprehensive activity tracking with filtering and search.

- [x] UI Prototype ([#215](https://github.com/cpike5/discordbot/issues/215))
- [x] Entity and database schema ([#251](https://github.com/cpike5/discordbot/issues/251))
- [x] Repository ([#252](https://github.com/cpike5/discordbot/issues/252))
- [x] DTOs ([#253](https://github.com/cpike5/discordbot/issues/253))
- [x] AuditLogService with fluent builder ([#254](https://github.com/cpike5/discordbot/issues/254))
- [x] Integration into existing services ([#255](https://github.com/cpike5/discordbot/issues/255))
- [x] API controller ([#256](https://github.com/cpike5/discordbot/issues/256))
- [x] Index admin page ([#257](https://github.com/cpike5/discordbot/issues/257))
- [x] Details admin page ([#258](https://github.com/cpike5/discordbot/issues/258))
- [x] Dashboard widget ([#259](https://github.com/cpike5/discordbot/issues/259))
- [x] Retention background service ([#260](https://github.com/cpike5/discordbot/issues/260))
- [x] Tests ([#261](https://github.com/cpike5/discordbot/issues/261))

### Commands Page ([#202](https://github.com/cpike5/discordbot/issues/202))
Admin UI page to display loaded bot command modules.

- [x] Design specification ([#203](https://github.com/cpike5/discordbot/issues/203))
- [x] Core layer DTOs and interfaces ([#204](https://github.com/cpike5/discordbot/issues/204))
- [x] CommandMetadataService ([#205](https://github.com/cpike5/discordbot/issues/205))
- [x] ViewModels ([#206](https://github.com/cpike5/discordbot/issues/206))
- [x] Razor Page implementation ([#207](https://github.com/cpike5/discordbot/issues/207))
- [x] Sidebar navigation update ([#208](https://github.com/cpike5/discordbot/issues/208))
- [x] Unit tests ([#209](https://github.com/cpike5/discordbot/issues/209))
- [x] Documentation ([#210](https://github.com/cpike5/discordbot/issues/210))

### Rat Watch Accountability System ([#404](https://github.com/cpike5/discordbot/issues/404))
Community-driven accountability system for tracking commitments.

- [x] Context menu message command integration
- [x] Natural language time parsing (10m, 2h, 10pm, etc.)
- [x] Early check-in system with interactive buttons
- [x] Community voting system with majority verdict
- [x] Leaderboard and user statistics tracking
- [x] Guild-specific configuration (timezone, voting duration, limits)
- [x] Admin management UI at `/Guilds/RatWatch/{guildId}`
- [x] Public leaderboard view
- [x] Analytics and reporting
- [x] Bot status updates during active watches ([#412](https://github.com/cpike5/discordbot/issues/412))

### Bug Fixes
- [x] Command Logs: Date filter issues ([#201](https://github.com/cpike5/discordbot/issues/201))
- [x] Command Analytics: Quick date range auto-apply ([#200](https://github.com/cpike5/discordbot/issues/200))
- [x] Settings page save button feedback ([#411](https://github.com/cpike5/discordbot/issues/411))

---

## Completed (v0.4.0)

### Documentation Overhaul ([#303](https://github.com/cpike5/discordbot/issues/303))
Comprehensive documentation update for clarity and discoverability.

- [x] Update ROADMAP.md to reflect current state
- [x] Consolidate and organize feature documentation
- [x] Create deployment and production setup guide
- [x] Update README.md with current feature highlights
- [x] Review and update all existing documentation

---

## Completed (v0.5.0 - v0.5.3)

### Moderation System ([#291](https://github.com/cpike5/discordbot/issues/291))
Comprehensive moderation tools with auto-detection and manual actions.

- [x] Manual moderation commands: `/warn`, `/kick`, `/ban`, `/mute`, `/purge`
- [x] Mod notes system for tracking user history
- [x] Auto-moderation with configurable rules (spam, profanity, raid detection)
- [x] Flagged content review queue
- [x] Cross-guild analytics dashboard
- [x] Per-guild configuration UI
- [x] Investigation command `/investigate`
- [x] Watchlist management `/watchlist`
- [x] Mod tagging system `/mod-tag`

### Utility Commands ([#292](https://github.com/cpike5/discordbot/issues/292))
General-purpose utility slash commands.

- [x] `/userinfo` - User profile and activity information
- [x] `/serverinfo` - Server statistics and configuration
- [x] `/roleinfo` - Role details and member counts

### Reminder System
Personal reminder system with natural language time parsing.

- [x] `/remind set` - Create personal reminders with natural language times
- [x] `/remind list` - View pending reminders
- [x] `/remind delete` - Remove reminders
- [x] Admin UI for reminder management per guild

### Audit Dashboard & Analytics ([#294](https://github.com/cpike5/discordbot/issues/294))
Enhanced analytics and reporting for audit logs.

- [x] Trend analysis and visualizations
- [x] Custom date range filtering
- [x] Guild-specific analytics pages

### Bot Performance Dashboard ([#295](https://github.com/cpike5/discordbot/issues/295))
Real-time monitoring of bot health and performance metrics.

- [x] Overview dashboard with key metrics
- [x] Health metrics page with resource usage tracking
- [x] Command performance page with latency metrics
- [x] System health page for database, cache, and service monitoring
- [x] API metrics page for Discord API usage and rate limits
- [x] Performance alerts page with threshold configuration
- [x] Historical metrics system ([#613](https://github.com/cpike5/discordbot/issues/613))

### Member Directory ([#296](https://github.com/cpike5/discordbot/issues/296))
Searchable directory of guild members with role filtering.

- [x] Member search and filtering
- [x] Role assignment tracking
- [x] Join date and activity metrics
- [x] Member moderation history view

### Admin/Performance Pages UI Polish ([#647](https://github.com/cpike5/discordbot/issues/647))
Design system compliance and accessibility improvements.

- [x] Mobile navigation improvements
- [x] Touch target size fixes
- [x] ARIA roles and accessibility labels
- [x] Shared component standardization
- [x] Color contrast fixes
- [x] Time range selector standardization

### Distributed Tracing with Jaeger ([#662](https://github.com/cpike5/discordbot/issues/662))
Comprehensive OpenTelemetry tracing implementation.

- [x] Bot lifecycle and Discord gateway event tracing ([#663](https://github.com/cpike5/discordbot/issues/663))
- [x] Background service instrumentation ([#664](https://github.com/cpike5/discordbot/issues/664))
- [x] Business service layer tracing ([#665](https://github.com/cpike5/discordbot/issues/665))
- [x] Discord API call tracing enhancement ([#666](https://github.com/cpike5/discordbot/issues/666))
- [x] Priority-based sampling strategy optimization ([#667](https://github.com/cpike5/discordbot/issues/667))

---

## In Progress (v0.6.0)

### SignalR Real-Time Performance Dashboard ([#622](https://github.com/cpike5/discordbot/issues/622))
Real-time streaming updates for performance monitoring.

- [x] Visual feedback components for real-time updates ([#631](https://github.com/cpike5/discordbot/issues/631))
- [x] Performance Dashboard shell layout ([#721](https://github.com/cpike5/discordbot/issues/721))
- [ ] DTOs for SignalR performance metrics streaming ([#623](https://github.com/cpike5/discordbot/issues/623))
- [ ] Extend DashboardHub with performance monitoring methods ([#624](https://github.com/cpike5/discordbot/issues/624))
- [ ] PerformanceMetricsBroadcastService for scheduled publishing ([#625](https://github.com/cpike5/discordbot/issues/625))
- [ ] Alert triggers and resolutions via SignalR ([#626](https://github.com/cpike5/discordbot/issues/626))
- [ ] Replace polling with SignalR on Health Metrics page ([#627](https://github.com/cpike5/discordbot/issues/627))
- [ ] Real-time alert updates on Alerts page ([#628](https://github.com/cpike5/discordbot/issues/628))
- [ ] Replace full page reload with SignalR on System Health page ([#629](https://github.com/cpike5/discordbot/issues/629))
- [ ] Real-time streaming on Command Performance page ([#630](https://github.com/cpike5/discordbot/issues/630))
- [ ] Refactor to component-based architecture ([#661](https://github.com/cpike5/discordbot/issues/661))

### User/Guild Preview Popups ([#671](https://github.com/cpike5/discordbot/issues/671))
Hover previews for user and guild information across the admin UI.

- [x] Preview ViewModels and API endpoints ([#698](https://github.com/cpike5/discordbot/issues/698), [#699](https://github.com/cpike5/discordbot/issues/699))
- [x] Preview popup partial views ([#700](https://github.com/cpike5/discordbot/issues/700))
- [x] JavaScript module for popup handling ([#701](https://github.com/cpike5/discordbot/issues/701))
- [x] CSS styling for popups ([#702](https://github.com/cpike5/discordbot/issues/702))
- [x] Integration into Performance Dashboard ([#703](https://github.com/cpike5/discordbot/issues/703))
- [x] Integration into Command Logs ([#704](https://github.com/cpike5/discordbot/issues/704))
- [x] Integration into Audit Logs ([#705](https://github.com/cpike5/discordbot/issues/705))
- [x] Integration into Member Directory ([#706](https://github.com/cpike5/discordbot/issues/706))
- [x] Integration into Moderation pages ([#707](https://github.com/cpike5/discordbot/issues/707))
- [x] Keyboard and touch accessibility ([#708](https://github.com/cpike5/discordbot/issues/708))
- [x] Client-side caching ([#709](https://github.com/cpike5/discordbot/issues/709))

### Notification System
In-app notification center for admin UI.

- [x] Notification service and data layer ([#739](https://github.com/cpike5/discordbot/issues/739), [#748](https://github.com/cpike5/discordbot/issues/748))
- [ ] Integrate notifications with event sources ([#740](https://github.com/cpike5/discordbot/issues/740))
- [ ] SignalR notification broadcasting ([#741](https://github.com/cpike5/discordbot/issues/741))
- [ ] Notification bell dropdown UI ([#742](https://github.com/cpike5/discordbot/issues/742))
- [ ] Notification History page ([#743](https://github.com/cpike5/discordbot/issues/743))
- [ ] Notification retention cleanup service ([#744](https://github.com/cpike5/discordbot/issues/744))

### Performance Dashboard Tab System
AJAX-based tab loading for improved performance.

- [ ] Convert pages to partial views ([#722](https://github.com/cpike5/discordbot/issues/722), [#759](https://github.com/cpike5/discordbot/issues/759)-[#764](https://github.com/cpike5/discordbot/issues/764))
- [ ] Create PerformanceTabViewModels ([#758](https://github.com/cpike5/discordbot/issues/758))
- [ ] AJAX tab loading system ([#723](https://github.com/cpike5/discordbot/issues/723))
- [ ] Browser history and deep linking ([#724](https://github.com/cpike5/discordbot/issues/724))
- [ ] CSS transitions and loading states ([#725](https://github.com/cpike5/discordbot/issues/725))
- [ ] Time range selector consolidation ([#726](https://github.com/cpike5/discordbot/issues/726))
- [ ] JavaScript module migration ([#727](https://github.com/cpike5/discordbot/issues/727))
- [ ] Consolidate shared styles ([#765](https://github.com/cpike5/discordbot/issues/765))
- [ ] Integration tests ([#731](https://github.com/cpike5/discordbot/issues/731))

### Custom Commands ([#293](https://github.com/cpike5/discordbot/issues/293))
Guild-specific custom slash commands configured via admin UI.

- [ ] Core domain layer ([#564](https://github.com/cpike5/discordbot/issues/564))
- [ ] Infrastructure layer ([#567](https://github.com/cpike5/discordbot/issues/567))
- [ ] Service layer ([#569](https://github.com/cpike5/discordbot/issues/569))
- [ ] Discord slash commands ([#574](https://github.com/cpike5/discordbot/issues/574))
- [ ] Admin UI pages ([#575](https://github.com/cpike5/discordbot/issues/575))
- [ ] Documentation ([#576](https://github.com/cpike5/discordbot/issues/576))

### Other v0.6.0 Tasks
- [ ] Accurate CPU metrics collection ([#642](https://github.com/cpike5/discordbot/issues/642))

---

## Planned (v0.7.0)

### Audio Support & Soundboard ([#749](https://github.com/cpike5/discordbot/issues/749))
Voice channel integration with soundboard functionality.

- [ ] Voice channel infrastructure ([#750](https://github.com/cpike5/discordbot/issues/750))
- [ ] Soundboard core data layer ([#751](https://github.com/cpike5/discordbot/issues/751))
- [ ] Discord slash commands for audio ([#752](https://github.com/cpike5/discordbot/issues/752))
- [ ] Admin UI - Soundboard page ([#753](https://github.com/cpike5/discordbot/issues/753))
- [ ] Admin UI - Audio settings page ([#754](https://github.com/cpike5/discordbot/issues/754))
- [ ] Guild settings integration ([#755](https://github.com/cpike5/discordbot/issues/755))
- [ ] Documentation ([#756](https://github.com/cpike5/discordbot/issues/756))
- [ ] Cross-platform audio dependencies ([#757](https://github.com/cpike5/discordbot/issues/757))

---

## Planned (v0.8.0)

### Docker Containerization ([#766](https://github.com/cpike5/discordbot/issues/766))
Production-ready containerized deployment.

- [ ] Multi-stage Dockerfile ([#767](https://github.com/cpike5/discordbot/issues/767))
- [ ] docker-compose.yml configuration ([#768](https://github.com/cpike5/discordbot/issues/768))
- [ ] Environment configuration template ([#769](https://github.com/cpike5/discordbot/issues/769))
- [ ] Health check endpoint ([#770](https://github.com/cpike5/discordbot/issues/770))
- [ ] GitHub Actions Docker build workflow ([#771](https://github.com/cpike5/discordbot/issues/771))
- [ ] Docker deployment guide ([#772](https://github.com/cpike5/discordbot/issues/772))

---

## Backlog

### User Data Privacy & Compliance ([#777](https://github.com/cpike5/discordbot/issues/777))
GDPR compliance enhancements for user data management.

- [ ] User data purge functionality ([#778](https://github.com/cpike5/discordbot/issues/778))
- [ ] Bulk data purge operations ([#779](https://github.com/cpike5/discordbot/issues/779))
- [ ] Message logging consent clarification ([#780](https://github.com/cpike5/discordbot/issues/780))

### Log Viewer Page ([#747](https://github.com/cpike5/discordbot/issues/747))
Unified log viewer with multi-source support.

### Other Backlog Items
- [ ] Evaluate observability strategy: Migrate to Elastic Stack ([#787](https://github.com/cpike5/discordbot/issues/787))
- [ ] Display channel names instead of IDs on MessageLogs page ([#786](https://github.com/cpike5/discordbot/issues/786))
- [ ] Add user preview popups to Analytics pages ([#782](https://github.com/cpike5/discordbot/issues/782))
- [ ] Fix Discord ID precision loss in JavaScript ([#781](https://github.com/cpike5/discordbot/issues/781))
- [ ] Add LeftAt field to Guild entity ([#746](https://github.com/cpike5/discordbot/issues/746))
- [ ] Fix flaky MetricsCollectionService test ([#636](https://github.com/cpike5/discordbot/issues/636))

---

## Future Considerations

The following features are candidates for future development but not yet formally planned:

### Admin UI Enhancements
- **Configuration Hot Reload** - Apply settings changes without restart
- **Backup & Restore** - Export/import guild configurations
- **Multi-language Support** - Localization for admin UI

### Bot Functionality
- **Reaction Roles** - Self-assignable roles via message reactions
- **Moderation Queue** - Review and approve/reject flagged messages

### API & Integration
- **Webhook Support** - Event notifications to external systems
- **Plugin System** - Extensible command/feature architecture
- **OAuth Scopes** - Fine-grained API access control
- **Rate Limiting API** - Configurable throttling per endpoint

### Infrastructure
- **Database Migrations UI** - Admin interface for schema updates
- **Kubernetes Support** - Helm charts and orchestration configurations

---

## Version History

| Version | Date | Highlights |
|---------|------|------------|
| v0.5.3 | 2026-01-03 | Distributed tracing with Jaeger, priority-based sampling |
| v0.5.2 | 2026-01-03 | Per-service memory diagnostics, toast action buttons |
| v0.5.1 | 2026-01-03 | Preview popup accessibility and caching |
| v0.5.0 | 2026-01-03 | Moderation system, performance dashboard, member directory, utility commands, reminder system |
| v0.4.0 | 2025-12-31 | Documentation overhaul, deployment guides |
| v0.3.11 | 2025-12-30 | Rat Watch navigation improvements |
| v0.3.10 | 2025-12 | Rat Watch analytics and public leaderboards |
| v0.3.0 | 2025-12 | Welcome system, scheduled messages, real-time dashboard, audit log viewer, Rat Watch accountability system |
| v0.2.1 | 2025-12 | Bug fixes, UI prototype preparation for v0.3.0 features |
| v0.2.0 | 2025-12 | Observability, consent system, message logging, APM tracing, UI polish |
| v0.1.0 | 2025-12 | Initial pre-release: core bot, admin UI, authentication, API |

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on submitting feature requests and pull requests.

To propose a new feature:
1. Check existing [GitHub Issues](https://github.com/cpike5/discordbot/issues) for duplicates
2. Open a new issue with the `feature` label
3. Provide use case, expected behavior, and implementation ideas

---

## Related Documentation

- [README.md](README.md) - Project overview and quick start
- [CLAUDE.md](CLAUDE.md) - AI assistant guidance
- [docs/articles/architecture-history.md](docs/articles/architecture-history.md) - Original implementation plan (historical)
- [docs/articles/requirements.md](docs/articles/requirements.md) - Technical requirements
