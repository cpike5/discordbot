# Roadmap

This document outlines the development roadmap for the Discord Bot Management System. It provides a high-level view of completed work, current focus areas, and future enhancements.

**Current Version:** v0.2.1 (pre-release)

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

## In Progress (v0.3.0)

The following features are actively being developed for the v0.3.0 release. UI prototypes for all major features have been completed.

### Welcome System ([#216](https://github.com/cpike5/discordbot/issues/216))
Configurable welcome messages and role assignment for new members.

- [x] UI Prototype ([#212](https://github.com/cpike5/discordbot/issues/212))
- [ ] Entity and database schema ([#224](https://github.com/cpike5/discordbot/issues/224))
- [ ] Repository ([#225](https://github.com/cpike5/discordbot/issues/225))
- [ ] DTOs ([#226](https://github.com/cpike5/discordbot/issues/226))
- [ ] WelcomeService ([#227](https://github.com/cpike5/discordbot/issues/227))
- [ ] Event handler ([#228](https://github.com/cpike5/discordbot/issues/228))
- [ ] API controller ([#229](https://github.com/cpike5/discordbot/issues/229))
- [ ] Admin configuration page ([#230](https://github.com/cpike5/discordbot/issues/230))
- [ ] `/welcome` slash command ([#231](https://github.com/cpike5/discordbot/issues/231))
- [ ] Tests ([#232](https://github.com/cpike5/discordbot/issues/232))

### Scheduled Messages ([#217](https://github.com/cpike5/discordbot/issues/217))
Timed announcements and recurring message scheduling.

- [x] UI Prototype ([#213](https://github.com/cpike5/discordbot/issues/213))
- [ ] Entity and database schema ([#233](https://github.com/cpike5/discordbot/issues/233))
- [ ] Repository ([#234](https://github.com/cpike5/discordbot/issues/234))
- [ ] DTOs ([#235](https://github.com/cpike5/discordbot/issues/235))
- [ ] ScheduledMessageService ([#236](https://github.com/cpike5/discordbot/issues/236))
- [ ] Background executor service ([#237](https://github.com/cpike5/discordbot/issues/237))
- [ ] API controller ([#238](https://github.com/cpike5/discordbot/issues/238))
- [ ] List admin page ([#239](https://github.com/cpike5/discordbot/issues/239))
- [ ] Create/edit admin pages ([#240](https://github.com/cpike5/discordbot/issues/240))
- [ ] `/schedule` slash commands ([#241](https://github.com/cpike5/discordbot/issues/241))
- [ ] Tests ([#242](https://github.com/cpike5/discordbot/issues/242))

### Real-time Dashboard ([#218](https://github.com/cpike5/discordbot/issues/218))
SignalR-powered live updates for dashboard statistics.

- [x] UI Prototype ([#214](https://github.com/cpike5/discordbot/issues/214))
- [ ] SignalR infrastructure ([#243](https://github.com/cpike5/discordbot/issues/243))
- [ ] DTOs ([#244](https://github.com/cpike5/discordbot/issues/244))
- [ ] Dashboard update service ([#245](https://github.com/cpike5/discordbot/issues/245))
- [ ] Bot event integration ([#246](https://github.com/cpike5/discordbot/issues/246))
- [ ] SignalR JavaScript client ([#247](https://github.com/cpike5/discordbot/issues/247))
- [ ] Dashboard page updates ([#248](https://github.com/cpike5/discordbot/issues/248))
- [ ] Stats API endpoint ([#249](https://github.com/cpike5/discordbot/issues/249))
- [ ] Tests ([#250](https://github.com/cpike5/discordbot/issues/250))

### Audit Log Viewer ([#219](https://github.com/cpike5/discordbot/issues/219))
Comprehensive activity tracking with filtering and search.

- [x] UI Prototype ([#215](https://github.com/cpike5/discordbot/issues/215))
- [ ] Entity and database schema ([#251](https://github.com/cpike5/discordbot/issues/251))
- [ ] Repository ([#252](https://github.com/cpike5/discordbot/issues/252))
- [ ] DTOs ([#253](https://github.com/cpike5/discordbot/issues/253))
- [ ] AuditLogService with fluent builder ([#254](https://github.com/cpike5/discordbot/issues/254))
- [ ] Integration into existing services ([#255](https://github.com/cpike5/discordbot/issues/255))
- [ ] API controller ([#256](https://github.com/cpike5/discordbot/issues/256))
- [ ] Index admin page ([#257](https://github.com/cpike5/discordbot/issues/257))
- [ ] Details admin page ([#258](https://github.com/cpike5/discordbot/issues/258))
- [ ] Dashboard widget ([#259](https://github.com/cpike5/discordbot/issues/259))
- [ ] Retention background service ([#260](https://github.com/cpike5/discordbot/issues/260))
- [ ] Tests ([#261](https://github.com/cpike5/discordbot/issues/261))

### Commands Page ([#202](https://github.com/cpike5/discordbot/issues/202))
Admin UI page to display loaded bot command modules.

- [ ] Design specification ([#203](https://github.com/cpike5/discordbot/issues/203))
- [ ] Core layer DTOs and interfaces ([#204](https://github.com/cpike5/discordbot/issues/204))
- [ ] CommandMetadataService ([#205](https://github.com/cpike5/discordbot/issues/205))
- [ ] ViewModels ([#206](https://github.com/cpike5/discordbot/issues/206))
- [ ] Razor Page implementation ([#207](https://github.com/cpike5/discordbot/issues/207))
- [ ] Sidebar navigation update ([#208](https://github.com/cpike5/discordbot/issues/208))
- [ ] Unit tests ([#209](https://github.com/cpike5/discordbot/issues/209))
- [ ] Documentation ([#210](https://github.com/cpike5/discordbot/issues/210))

### Bug Fixes
- [ ] Command Logs: Date filter issues ([#201](https://github.com/cpike5/discordbot/issues/201))
- [ ] Command Analytics: Quick date range auto-apply ([#200](https://github.com/cpike5/discordbot/issues/200))

---

## Planned Features (v0.4.0+)

The following features are prioritized for future development:

### Production Readiness
- **Docker Support** - Containerized deployment with docker-compose
- **Health Checks** - Kubernetes/orchestration readiness and liveness probes
- **Configuration Hot Reload** - Apply settings changes without restart

### Bot Enhancements
- **Auto-moderation** - Configurable rules for spam, profanity, raid detection

### Admin UI Enhancements
- **Guild Configuration UI** - Per-guild settings management

---

## Future Considerations

The following features are candidates for future development but not yet formally planned:

### Bot Functionality
- **Moderation Queue** - Review and approve/reject flagged messages
- **Custom Commands** - Guild-specific custom slash commands via admin UI
- **Reaction Roles** - Self-assignable roles via message reactions

### Admin UI Enhancements
- **Backup & Restore** - Export/import guild configurations
- **Multi-language Support** - Localization for admin UI

### API & Integration
- **Webhook Support** - Event notifications to external systems
- **Plugin System** - Extensible command/feature architecture
- **OAuth Scopes** - Fine-grained API access control
- **Rate Limiting API** - Configurable throttling per endpoint

### Infrastructure
- **Database Migrations UI** - Admin interface for schema updates

### Analytics & Reporting
- **Usage Reports** - Scheduled email summaries
- **Custom Dashboards** - User-configurable metric displays
- **Export Functionality** - CSV/JSON data exports
- **Trend Analysis** - Historical command/activity patterns

---

## Version History

| Version | Date | Highlights |
|---------|------|------------|
| v0.3.0 | TBD | Welcome system, scheduled messages, real-time dashboard, audit log viewer |
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
- [docs/articles/mvp-plan.md](docs/articles/mvp-plan.md) - Original MVP implementation plan
- [docs/articles/requirements.md](docs/articles/requirements.md) - Technical requirements
