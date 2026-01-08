# CLAUDE-REFERENCE.md

Auto-generated lookup tables for agents. Regenerate with `/update-instructions tables`.

**Last updated:** [Run /update-instructions tables to generate]

---

## Configuration Options

Options classes are in `src/DiscordBot.Core/Configuration/` (except `DatabaseSettings` in `src/DiscordBot.Infrastructure/Configuration/`).

| Options Class | appsettings Section | Purpose |
|--------------|---------------------|---------|
| `ApplicationOptions` | `Application` | App metadata (title, base URL, version) |
| `AnalyticsRetentionOptions` | `AnalyticsRetention` | Analytics data retention settings |
| `AuditLogRetentionOptions` | `AuditLogRetention` | Audit log cleanup settings |
| `AutoModerationOptions` | `AutoModeration` | Auto-moderation rules and thresholds |
| `BackgroundServicesOptions` | `BackgroundServices` | Background task intervals and delays |
| `CachingOptions` | `Caching` | Cache duration settings |
| `DatabaseSettings` | `Database` | Query performance logging |
| `DiscordOAuthOptions` | `Discord:OAuth` | OAuth client credentials |
| `HistoricalMetricsOptions` | `HistoricalMetrics` | Metrics collection settings |
| `IdentityConfigOptions` | `Identity` | ASP.NET Identity settings |
| `MessageLogRetentionOptions` | `MessageLogRetention` | Message log cleanup |
| `ModerationOptions` | `Moderation` | Moderation system settings |
| `ObservabilityOptions` | `Observability` | External tool URLs (Kibana, Seq) |
| `PerformanceAlertOptions` | `PerformanceAlerts` | Alert thresholds |
| `PerformanceBroadcastOptions` | `PerformanceBroadcast` | SignalR broadcast intervals |
| `PerformanceMetricsOptions` | `PerformanceMetrics` | Metrics collection settings |
| `RatWatchOptions` | `RatWatch` | Rat Watch feature settings |
| `ReminderOptions` | `Reminder` | Reminder system settings |
| `SamplingOptions` | `OpenTelemetry:Tracing:Sampling` | Trace sampling rates |
| `ScheduledMessagesOptions` | `ScheduledMessages` | Scheduled message delivery |
| `VerificationOptions` | `Verification` | Verification code settings |

---

## UI Page Routes

| Page | URL Pattern | Description |
|------|-------------|-------------|
| Landing | `/landing` | Public landing page (no auth) |
| Dashboard | `/` | Main dashboard with bot status |
| Commands | `/Commands` | Registered slash commands list |
| Command Logs | `/CommandLogs` | Command execution history |
| Command Log Details | `/CommandLogs/{id:guid}` | Single command log entry |
| Command Analytics | `/CommandLogs/Analytics` | Usage analytics |
| Guilds | `/Guilds` | Connected Discord servers |
| Guild Details | `/Guilds/Details?id={id}` | Single guild overview |
| Guild Edit | `/Guilds/Edit/{id:long}` | Edit guild settings |
| Guild Welcome | `/Guilds/Welcome/{id:long}` | Welcome message config |
| Guild Moderation | `/Guilds/{guildId:long}/ModerationSettings` | Auto-moderation config |
| Guild Analytics | `/Guilds/{guildId:long}/Analytics` | Guild analytics |
| Guild Engagement | `/Guilds/{guildId:long}/Analytics/Engagement` | Member engagement |
| Guild Mod Analytics | `/Guilds/{guildId:long}/Analytics/Moderation` | Moderation analytics |
| Scheduled Messages | `/Guilds/ScheduledMessages/{guildId:long}` | Guild scheduled messages |
| Scheduled Create | `/Guilds/ScheduledMessages/Create/{guildId:long}` | New scheduled message |
| Scheduled Edit | `/Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}` | Edit scheduled message |
| Rat Watch | `/Guilds/RatWatch/{guildId:long}` | Rat Watch management |
| Rat Watch Analytics | `/Guilds/RatWatch/{guildId:long}/Analytics` | Rat Watch metrics |
| Rat Watch Incidents | `/Guilds/RatWatch/{guildId:long}/Incidents` | Incident browser |
| Member Directory | `/Guilds/{guildId:long}/Members` | Guild member list |
| Member Moderation | `/Guilds/{guildId:long}/Members/{userId:long}/Moderation` | Member mod history |
| Flagged Events | `/Guilds/{guildId:long}/FlaggedEvents` | Auto-mod flagged events |
| Flagged Details | `/Guilds/{guildId:long}/FlaggedEvents/{id:guid}` | Single flagged event |
| Reminders | `/Guilds/{guildId:long}/Reminders` | Guild reminders |
| Public Leaderboard | `/Guilds/{guildId:long}/Leaderboard` | Public Rat Watch leaderboard |
| Global Rat Analytics | `/Admin/RatWatchAnalytics` | Cross-guild metrics |
| Performance | `/Admin/Performance` | Performance dashboard |
| Health Metrics | `/Admin/Performance/HealthMetrics` | Bot health |
| Command Performance | `/Admin/Performance/Commands` | Command metrics |
| System Health | `/Admin/Performance/SystemHealth` | DB, cache monitoring |
| API Metrics | `/Admin/Performance/ApiMetrics` | Discord API usage |
| Performance Alerts | `/Admin/Performance/Alerts` | Alert management |
| Users | `/Admin/Users` | User management |
| User Details | `/Admin/Users/Details?id={id}` | User profile |
| User Create | `/Admin/Users/Create` | Create user |
| User Edit | `/Admin/Users/Edit?id={id}` | Edit user |
| Audit Logs | `/Admin/AuditLogs` | System audit trail |
| Audit Details | `/Admin/AuditLogs/Details/{id:long}` | Single audit entry |
| Message Logs | `/Admin/MessageLogs` | Discord message history |
| Message Details | `/Admin/MessageLogs/Details/{id:long}` | Single message |
| Bot Control | `/Admin/BotControl` | Start/stop/restart bot |
| Settings | `/Admin/Settings` | Application settings |
| Login | `/Account/Login` | User authentication |
| Logout | `/Account/Logout` | Sign out |
| Link Discord | `/Account/LinkDiscord` | OAuth account linking |
| Search | `/Search` | Global search |

---

## Command Modules

| Module | Commands |
|--------|----------|
| `GeneralModule` | `/ping` |
| `AdminModule` | `/admin info`, `/admin kick`, `/admin ban` |
| `VerifyAccountModule` | `/verify` |
| `RatWatchModule` | Context menu, `/rat-clear`, `/rat-stats`, `/rat-leaderboard`, `/rat-settings` |
| `ScheduleModule` | `/schedule-message create/list/delete/edit` |
| `WelcomeModule` | `/welcome setup/test/disable` |
| `ReminderModule` | `/remind set/list/delete` |
| `UtilityModule` | `/userinfo`, `/serverinfo`, `/roleinfo` |
| `ModerationActionModule` | `/warn`, `/kick`, `/ban`, `/mute`, `/purge` |
| `ModerationHistoryModule` | `/mod-history` |
| `ModStatsModule` | `/mod-stats` |
| `ModNoteModule` | `/mod-notes add/list/delete` |
| `ModTagModule` | `/mod-tag add/remove/list` |
| `WatchlistModule` | `/watchlist add/remove/list` |
| `InvestigateModule` | `/investigate` |
| `ConsentModule` | `/consent`, `/privacy` |

---

## Documentation Index

| Doc | Purpose |
|-----|---------|
| [design-system.md](docs/articles/design-system.md) | UI tokens, color palette, component specs |
| [commands-page.md](docs/articles/commands-page.md) | Commands page and metadata service |
| [settings-page.md](docs/articles/settings-page.md) | Settings page, real-time updates |
| [interactive-components.md](docs/articles/interactive-components.md) | Discord button/component patterns |
| [identity-configuration.md](docs/articles/identity-configuration.md) | Authentication setup |
| [authorization-policies.md](docs/articles/authorization-policies.md) | Role hierarchy, guild access |
| [api-endpoints.md](docs/articles/api-endpoints.md) | REST API documentation |
| [log-aggregation.md](docs/articles/log-aggregation.md) | Elasticsearch/Seq logging |
| [elastic-stack-setup.md](docs/articles/elastic-stack-setup.md) | Local Elastic Stack setup |
| [kibana-dashboards.md](docs/articles/kibana-dashboards.md) | Kibana dashboards |
| [elastic-apm.md](docs/articles/elastic-apm.md) | Distributed tracing |
| [versioning-strategy.md](docs/articles/versioning-strategy.md) | SemVer, CI/CD, releases |
| [issue-tracking-process.md](docs/articles/issue-tracking-process.md) | Issue hierarchy, labels |
| [rat-watch.md](docs/articles/rat-watch.md) | Rat Watch feature |
| [member-directory.md](docs/articles/member-directory.md) | Member Directory feature |
| [form-implementation-standards.md](docs/articles/form-implementation-standards.md) | Razor form patterns |
| [reminder-system.md](docs/articles/reminder-system.md) | Reminders with natural language |
| [utility-commands.md](docs/articles/utility-commands.md) | Utility commands |
| [bot-performance-dashboard.md](docs/articles/bot-performance-dashboard.md) | Performance monitoring |
| [scheduled-messages.md](docs/articles/scheduled-messages.md) | Scheduled message system |
| [audit-log-system.md](docs/articles/audit-log-system.md) | Audit logging API |
| [signalr-realtime.md](docs/articles/signalr-realtime.md) | SignalR real-time updates |
| [welcome-system.md](docs/articles/welcome-system.md) | Welcome message config |
| [consent-privacy.md](docs/articles/consent-privacy.md) | User consent/privacy |
| [database-schema.md](docs/articles/database-schema.md) | Entity relationships |
| [testing-guide.md](docs/articles/testing-guide.md) | Testing patterns |
| [troubleshooting-guide.md](docs/articles/troubleshooting-guide.md) | Common issues |
| [discord-bot-setup.md](docs/articles/discord-bot-setup.md) | Discord Developer Portal |
