# Roadmap

This document outlines the development roadmap for the Discord Bot Management System. It provides a high-level view of completed work, current focus areas, and future enhancements.

**Current Version:** v0.5.0-dev

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

## In Progress (v0.4.0)

### Documentation Overhaul ([#303](https://github.com/cpike5/discordbot/issues/303))
Comprehensive documentation update for clarity and discoverability.

- [ ] Update ROADMAP.md to reflect current state ([#306](https://github.com/cpike5/discordbot/issues/306))
- [ ] Consolidate and organize feature documentation ([#307](https://github.com/cpike5/discordbot/issues/307))
- [ ] Create deployment and production setup guide ([#308](https://github.com/cpike5/discordbot/issues/308))
- [ ] Update README.md with current feature highlights ([#309](https://github.com/cpike5/discordbot/issues/309))
- [ ] Review and update all existing documentation ([#310](https://github.com/cpike5/discordbot/issues/310))

---

## Planned Features (v0.5.0)

The following features are prioritized for the v0.5.0 release:

### Moderation System ([#291](https://github.com/cpike5/discordbot/issues/291))
Comprehensive moderation tools with auto-detection and manual actions.

- Manual moderation commands: `/warn`, `/kick`, `/ban`, `/mute`, `/purge`
- Mod notes system for tracking user history
- Auto-moderation with configurable rules (spam, profanity, raid detection)
- Flagged content review queue
- Cross-guild analytics dashboard
- Per-guild configuration UI

### Utility Commands ([#292](https://github.com/cpike5/discordbot/issues/292))
General-purpose utility slash commands.

- User info and server info commands
- Role management utilities
- Channel utilities
- Timestamp and timezone tools

### Custom Commands ([#293](https://github.com/cpike5/discordbot/issues/293))
Guild-specific custom slash commands configured via admin UI.

- Custom command builder
- Variable substitution support
- Embed customization
- Permission-based command access

### Audit Dashboard & Analytics ([#294](https://github.com/cpike5/discordbot/issues/294))
Enhanced analytics and reporting for audit logs.

- Trend analysis and visualizations
- Custom date range filtering
- Export functionality (CSV/JSON)
- Scheduled reports

### Bot Performance Dashboard ([#295](https://github.com/cpike5/discordbot/issues/295))
Real-time monitoring of bot health and performance metrics.

- Resource usage tracking (CPU, memory, network)
- Command latency metrics
- Discord API rate limit monitoring
- Error rate tracking and alerting

### Member Directory ([#296](https://github.com/cpike5/discordbot/issues/296))
Searchable directory of guild members with role filtering.

- Member search and filtering
- Role assignment tracking
- Join date and activity metrics
- Export member lists

---

## Future Considerations (v0.6.0+)

The following features are candidates for future development but not yet formally planned:

### Production Readiness
- **Docker Support** - Containerized deployment with docker-compose
- **Health Checks** - Kubernetes/orchestration readiness and liveness probes
- **Configuration Hot Reload** - Apply settings changes without restart

### Admin UI Enhancements
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

---

## Version History

| Version | Date | Highlights |
|---------|------|------------|
| v0.4.0 | 2025-12-30 | Documentation overhaul, deployment guides |
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
