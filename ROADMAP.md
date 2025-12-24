# Roadmap

This document outlines the development roadmap for the Discord Bot Management System. It provides a high-level view of completed work, current focus areas, and future enhancements.

**Current Version:** v0.1.0 (initial pre-release)

---

## Completed (v0.1.0)

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

---

## In Progress

### UI Polish ([#168](https://github.com/cpike5/discordbot/issues/168))
Fixing broken links, placeholders, and incomplete features before stable release.

| Issue | Description | Priority |
|-------|-------------|----------|
| [#161](https://github.com/cpike5/discordbot/issues/161) | Remove/fix placeholder navigation links in sidebar | High |
| [#162](https://github.com/cpike5/discordbot/issues/162) | Fix placeholder links in user dropdown menu | High |
| [#163](https://github.com/cpike5/discordbot/issues/163) | Remove or implement global search | Medium |
| [#164](https://github.com/cpike5/discordbot/issues/164) | Fix placeholder links on login page | Medium |
| [#165](https://github.com/cpike5/discordbot/issues/165) | Make bot version in sidebar dynamic | Low |
| [#166](https://github.com/cpike5/discordbot/issues/166) | Remove/replace decorative notifications bell | Low |
| [#167](https://github.com/cpike5/discordbot/issues/167) | Restrict Components showcase to developers | Low |

---

## Planned Features

### Phase 1: Observability Enhancement ([#159](https://github.com/cpike5/discordbot/issues/159))
Comprehensive logging, metrics, and distributed tracing for production readiness.

**Foundation (Critical):**
| Issue | Description |
|-------|-------------|
| [#99](https://github.com/cpike5/discordbot/issues/99) | Repository logging with performance tracking |
| [#100](https://github.com/cpike5/discordbot/issues/100) | Correlation ID middleware for API requests |
| [#101](https://github.com/cpike5/discordbot/issues/101) | EF Core query performance logging |

**Security & Abuse Detection (High):**
| Issue | Description |
|-------|-------------|
| [#102](https://github.com/cpike5/discordbot/issues/102) | Log sanitization for sensitive data |
| [#103](https://github.com/cpike5/discordbot/issues/103) | Rate limit logging for abuse detection |

**Advanced Observability (Medium):**
| Issue | Description |
|-------|-------------|
| [#104](https://github.com/cpike5/discordbot/issues/104) | OpenTelemetry metrics collection |
| [#105](https://github.com/cpike5/discordbot/issues/105) | Distributed tracing with OpenTelemetry |
| [#106](https://github.com/cpike5/discordbot/issues/106) | Centralized log aggregation (Seq/Application Insights) |
| [#107](https://github.com/cpike5/discordbot/issues/107) | CommandLogService optimization |
| [#108](https://github.com/cpike5/discordbot/issues/108) | Environment-specific configuration |
| [#109](https://github.com/cpike5/discordbot/issues/109) | Advanced metrics and business KPIs |

---

### Phase 2: User Consent & Privacy ([#130](https://github.com/cpike5/discordbot/issues/130))
GDPR-compliant privacy framework with user consent management.

| Issue | Description | Priority |
|-------|-------------|----------|
| [#132](https://github.com/cpike5/discordbot/issues/132) | Consent domain model & repository | High |
| [#133](https://github.com/cpike5/discordbot/issues/133) | Consent slash commands (`/consent`, `/privacy`) | High |
| [#135](https://github.com/cpike5/discordbot/issues/135) | Consent check service integration | High |
| [#134](https://github.com/cpike5/discordbot/issues/134) | Consent web UI management | Medium |

**Key Features:**
- User opt-in/opt-out for data collection
- Privacy preference storage per guild
- Data deletion requests
- Consent audit trail

---

### Phase 3: Message Logging System ([#136](https://github.com/cpike5/discordbot/issues/136))
Event-driven message capture with consent integration.

| Issue | Description | Priority |
|-------|-------------|----------|
| [#137](https://github.com/cpike5/discordbot/issues/137) | Message log domain model & repository | Medium |
| [#138](https://github.com/cpike5/discordbot/issues/138) | Message received event handler | Medium |
| [#139](https://github.com/cpike5/discordbot/issues/139) | Message log admin UI | Medium |
| [#140](https://github.com/cpike5/discordbot/issues/140) | Message log API endpoints | Medium |
| [#141](https://github.com/cpike5/discordbot/issues/141) | Message log retention & cleanup | Low |

**Prerequisites:** Consent system must be implemented first.

---

### Phase 4: APM & Tracing ([#92](https://github.com/cpike5/discordbot/issues/92))
Application Performance Monitoring with Elastic APM integration.

| Issue | Description | Priority |
|-------|-------------|----------|
| [#93](https://github.com/cpike5/discordbot/issues/93) | APM foundation setup | High |
| [#94](https://github.com/cpike5/discordbot/issues/94) | Discord interaction tracing | High |
| [#95](https://github.com/cpike5/discordbot/issues/95) | Service layer span instrumentation | Medium |
| [#96](https://github.com/cpike5/discordbot/issues/96) | Bot lifecycle event tracing | Medium |
| [#97](https://github.com/cpike5/discordbot/issues/97) | Custom metrics collection | Medium |
| [#98](https://github.com/cpike5/discordbot/issues/98) | Production optimization & alerting | Low |

---

## Future Considerations

The following features are candidates for future development but not yet formally planned:

### Bot Functionality
- **Moderation Queue** - Review and approve/reject flagged messages
- **Auto-moderation** - Configurable rules for spam, profanity, raid detection
- **Scheduled Messages** - Timed announcements and reminders
- **Custom Commands** - Guild-specific custom slash commands via admin UI
- **Reaction Roles** - Self-assignable roles via message reactions
- **Welcome System** - Configurable join messages and role assignment

### Admin UI Enhancements
- **Real-time Dashboard** - SignalR/WebSocket live updates
- **Audit Log Viewer** - Detailed activity history with filtering
- **Guild Configuration UI** - Per-guild settings management
- **Backup & Restore** - Export/import guild configurations
- **Multi-language Support** - Localization for admin UI

### API & Integration
- **Webhook Support** - Event notifications to external systems
- **Plugin System** - Extensible command/feature architecture
- **OAuth Scopes** - Fine-grained API access control
- **Rate Limiting API** - Configurable throttling per endpoint

### Infrastructure
- **Docker Support** - Containerized deployment
- **Health Checks** - Kubernetes/orchestration readiness probes
- **Database Migrations UI** - Admin interface for schema updates
- **Configuration Hot Reload** - Apply settings without restart

### Analytics & Reporting
- **Usage Reports** - Scheduled email summaries
- **Custom Dashboards** - User-configurable metric displays
- **Export Functionality** - CSV/JSON data exports
- **Trend Analysis** - Historical command/activity patterns

---

## Version History

| Version | Date | Highlights |
|---------|------|------------|
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
