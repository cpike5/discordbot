# CLAUDE-REFERENCE.md

Auto-generated lookup tables for Discord bot management system. Regenerate with `/update-instructions tables`.

**Last updated:** 2026-02-03

## Configuration Options

The application uses `IOptions<T>` pattern for strongly-typed configuration. All options have sensible defaults in `appsettings.json`.

| Options Class | appsettings Section | Purpose |
|--------------|---------------------|---------|
| `ApplicationOptions` | `Application` | App metadata (title, base URL, version) |
| `AnalyticsRetentionOptions` | `AnalyticsRetention` | Analytics data retention settings |
| `AnthropicOptions` | `Anthropic` | Anthropic Claude API configuration (API key, model, retries, timeout, prompt caching - use user secrets for ApiKey) |
| `AssistantOptions` | `Assistant` | AI assistant settings (Claude API integration, rate limiting, prompt caching - use user secrets for ApiKey) |
| `AuditLogRetentionOptions` | `AuditLogRetention` | Audit log cleanup settings |
| `AudioCacheOptions` | `AudioCache` | Audio PCM cache settings (cache size, TTL, cleanup intervals) |
| `AutoModerationOptions` | `AutoModeration` | Auto-moderation rules and thresholds |
| `AzureSpeechOptions` | `AzureSpeech` | Azure TTS service settings (use user secrets for SubscriptionKey) |
| `BackgroundServicesOptions` | `BackgroundServices` | Background task intervals and delays |
| `CachingOptions` | `Caching` | Cache duration settings for various services |
| `DatabaseSettings` | `Database` | Query performance logging (slow query threshold, parameter logging) |
| `DiscordOAuthOptions` | `Discord:OAuth` | OAuth client credentials (use user secrets) |
| `GuildMembershipCacheOptions` | `GuildMembershipCache` | Guild membership cache duration settings |
| `HistoricalMetricsOptions` | `HistoricalMetrics` | Historical metrics collection (sample interval, retention) |
| `IdentityConfigOptions` | `Identity` | ASP.NET Identity settings (use user secrets for DefaultAdmin) |
| `MessageLogRetentionOptions` | `MessageLogRetention` | Message log cleanup settings |
| `ModerationOptions` | `Moderation` | Moderation system settings |
| `NotificationOptions` | `Notification` | Admin notification toggles and deduplication settings |
| `NotificationRetentionOptions` | `NotificationRetention` | Notification cleanup policies (dismissed, read, unread retention) |
| `ObservabilityOptions` | `Observability` | External observability tool URLs (Kibana, Seq) |
| `PerformanceAlertOptions` | `PerformanceAlerts` | Alert thresholds and notification settings |
| `PerformanceBroadcastOptions` | `PerformanceBroadcast` | SignalR broadcast intervals for real-time metrics |
| `PerformanceMetricsOptions` | `PerformanceMetrics` | Performance metrics collection settings |
| `RatWatchOptions` | `RatWatch` | Rat Watch feature settings (voting, timeouts) |
| `ReminderOptions` | `Reminder` | Reminder system settings (polling, delivery, limits) |
| `SamplingOptions` | `OpenTelemetry:Tracing:Sampling` | OpenTelemetry trace sampling rates (priority-based sampling) |
| `ScheduledMessagesOptions` | `ScheduledMessages` | Scheduled message delivery settings |
| `SoundboardOptions` | `Soundboard` | Audio/soundboard settings (FFmpeg path, file limits, supported formats) |
| `SoundPlayLogRetentionOptions` | `SoundPlayLogRetention` | Sound play log cleanup settings |
| `UserActivityEventRetentionOptions` | `UserActivityEventRetention` | Anonymous activity event retention (consent-free analytics) |
| `VerificationOptions` | `Verification` | Verification code generation settings |
| `VoiceChannelOptions` | `VoiceChannel` | Voice channel auto-leave settings (timeout, check interval) |
| `VoxOptions` | `Vox` | VOX clip library settings (base path, word gap, message limits) |
| `ElasticApm:*` | `ElasticApm` | Elastic APM distributed tracing configuration (see appsettings.json for full options) |

## UI Page Routes

| Page | URL Pattern | Description |
|------|-------------|-------------|
| Landing | `/landing` | Public landing page (no auth) |
| Dashboard | `/` | Main dashboard with bot status, stats |
| Commands | `/Commands` | Registered slash commands list (with tabs: overview, logs, analytics) |
| Command Log Details | `/CommandLogs/{id:guid}` | Single command log entry |
| Guilds | `/Guilds` | Connected Discord servers list |
| Guild Details | `/Guilds/Details?id={id}` | Single guild overview |
| Guild Edit | `/Guilds/Edit/{id:long}` | Edit guild settings |
| Guild Welcome | `/Guilds/Welcome/{id:long}` | Welcome message config |
| Guild Moderation Settings | `/Guilds/{guildId:long}/ModerationSettings` | Guild auto-moderation config |
| Guild Analytics | `/Guilds/{guildId:long}/Analytics` | Guild analytics overview |
| Guild Engagement Analytics | `/Guilds/{guildId:long}/Analytics/Engagement` | Member engagement metrics |
| Guild Moderation Analytics | `/Guilds/{guildId:long}/Analytics/Moderation` | Moderation activity analytics |
| Scheduled Messages | `/Guilds/ScheduledMessages/{guildId:long}` | Guild scheduled messages |
| Scheduled Message Create | `/Guilds/ScheduledMessages/Create/{guildId:long}` | New scheduled message |
| Scheduled Message Edit | `/Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}` | Edit scheduled message |
| Rat Watch | `/Guilds/RatWatch/{guildId:long}` | Rat Watch management |
| Rat Watch Analytics | `/Guilds/RatWatch/Analytics/{guildId:long}` | Rat Watch analytics and metrics |
| Rat Watch Incidents | `/Guilds/RatWatch/Incidents/{guildId:long}` | Incident browser with filtering |
| Member Directory | `/Guilds/{guildId:long}/Members` | Guild member list with search/filter |
| Member Moderation | `/Guilds/{guildId:long}/Members/{userId:long}/Moderation` | Member moderation history |
| Flagged Events | `/Guilds/{guildId:long}/FlaggedEvents` | Auto-moderation flagged events |
| Flagged Event Details | `/Guilds/{guildId:long}/FlaggedEvents/{id:guid}` | Single flagged event |
| Reminders | `/Guilds/{guildId:long}/Reminders` | Guild reminders management |
| Soundboard | `/Guilds/Soundboard/{guildId:long}` | Guild soundboard management |
| Audio Settings | `/Guilds/AudioSettings/{guildId:long}` | Guild audio configuration |
| Assistant Settings | `/Guilds/AssistantSettings/{guildId:long}` | AI assistant configuration |
| Assistant Metrics | `/Guilds/AssistantMetrics/{guildId:long}` | AI assistant usage metrics |
| Text-to-Speech | `/Guilds/TextToSpeech/{guildId:long}` | Guild TTS message management |
| TTS Portal | `/Portal/TTS/{guildId:long}` | TTS message composer for guild members (OAuth required) |
| Soundboard Portal | `/Portal/Soundboard/{guildId:long}` | Soundboard player for guild members (OAuth required) |
| VOX Portal | `/Portal/VOX/{guildId:long}` | VOX announcement composer for guild members (OAuth required) |
| Public Leaderboard | `/Guilds/{guildId:long}/Leaderboard` | Public Rat Watch leaderboard (no auth) |
| Global Rat Watch Analytics | `/Admin/RatWatchAnalytics` | Cross-guild Rat Watch metrics (Admin+) |
| Performance Dashboard | `/Admin/Performance` | Performance overview dashboard |
| Health Metrics | `/Admin/Performance/HealthMetrics` | Bot health metrics dashboard |
| Command Performance | `/Admin/Performance/Commands` | Command response times, throughput, errors |
| System Health | `/Admin/Performance/SystemHealth` | Database, cache, and service monitoring |
| API Metrics | `/Admin/Performance/ApiMetrics` | Discord API usage and rate limits |
| Performance Alerts | `/Admin/Performance/Alerts` | Alert thresholds and incident management |
| Users | `/Admin/Users` | User management (SuperAdmin) |
| User Details | `/Admin/Users/Details?id={id}` | User profile and roles |
| User Create | `/Admin/Users/Create` | Create new user |
| User Edit | `/Admin/Users/Edit?id={id}` | Edit user |
| User Purge | `/Admin/UserPurge` | Purge user data (GDPR) |
| Audit Logs | `/Admin/AuditLogs` | System audit trail |
| Audit Log Details | `/Admin/AuditLogs/Details/{id:long}` | Single audit entry |
| Message Logs | `/Admin/MessageLogs` | Discord message history |
| Message Log Details | `/Admin/MessageLogs/Details/{id:long}` | Single message entry |
| Settings | `/Admin/Settings` | Application settings (includes Bot Control tab) |
| Login | `/Account/Login` | User authentication |
| Logout | `/Account/Logout` | Sign out |
| Link Discord | `/Account/LinkDiscord` | OAuth account linking |
| Search | `/Search` | Global search |
| Components | `/Components` | Component showcase (dev) |
| Error 403 | `/Error/403` | Access denied error page |
| Error 404 | `/Error/404` | Not found error page |
| Error 500 | `/Error/500` | Server error page |
| Privacy | `/Account/Privacy` | Privacy information |
| Profile | `/Account/Profile` | User profile and theme preferences |
| Account Lockout | `/Account/Lockout` | Account lockout page |
| Access Denied | `/Account/AccessDenied` | Access denied page |

**Note:** Use `Guilds/` not `Servers/` for guild-related pages (Discord API terminology).

## Discord Command Modules

Using Discord.NET 3.18.0 - slash commands only, registered via `InteractionHandler`.

| Module | Commands |
|--------|----------|
| `GeneralModule` | `/ping` |
| `AdminModule` | `/status`, `/guilds`, `/shutdown` |
| `VerifyAccountModule` | `/verify-account` |
| `RatWatchModule` | Rat Watch (context menu), `/rat-clear`, `/rat-stats`, `/rat-leaderboard`, `/rat-settings` |
| `ScheduleModule` | `/schedule-list`, `/schedule-create`, `/schedule-delete`, `/schedule-toggle`, `/schedule-run` |
| `WelcomeModule` | `/welcome show`, `/welcome enable`, `/welcome disable`, `/welcome channel`, `/welcome message`, `/welcome test` |
| `ReminderModule` | `/remind set`, `/remind list`, `/remind cancel` |
| `UtilityModule` | `/userinfo`, `/serverinfo`, `/roleinfo` |
| `ModerationActionModule` | `/warn`, `/kick`, `/ban`, `/mute`, `/purge` |
| `ModerationHistoryModule` | `/mod-history` |
| `ModStatsModule` | `/mod-stats` |
| `ModNoteModule` | `/mod-notes add/list/delete` |
| `ModTagModule` | `/mod-tag add/remove/list` |
| `WatchlistModule` | `/watchlist add/remove/list` |
| `InvestigateModule` | `/investigate` |
| `ConsentModule` | `/consent grant/revoke/status` |
| `PrivacyModule` | `/privacy preview-delete/delete-data` |
| `TtsModule` | `/tts <message> [voice]` |
| `SoundboardModule` | `/play <sound>`, `/sounds`, `/stop` |
| `VoiceModule` | `/join`, `/join-channel <channel>`, `/leave` |
| `VoxModule` | `/vox <message> [gap]`, `/fvox <message> [gap]`, `/hgrunt <message> [gap]` |

### Interactive Components Pattern

- Use `ComponentIdBuilder` to create custom IDs: `{handler}:{action}:{userId}:{correlationId}:{data}`
- Store component state via `IInteractionStateService` (15-min default expiry)
- Component handlers in separate `*ComponentModule` classes with `[ComponentInteraction]` attribute

### Command Preconditions

`RequireAdminAttribute`, `RequireOwnerAttribute`, `RateLimitAttribute`, `RequireRatWatchEnabledAttribute`, `RequireGuildActive`, `RequireModerationEnabled`, `RequireModerator`, `RequireAudioEnabled`, `RequireVoiceChannel`, `RequireTtsEnabled`, `RequireKickMembersAttribute`, `RequireBanMembersAttribute`

### Adding a New Command

1. Create module in `Commands/` inheriting from `InteractionModuleBase<SocketInteractionContext>`
2. Use `[SlashCommand("name", "description")]` attribute
3. Inject dependencies via constructor
4. If using buttons/components, create separate `*ComponentModule` handler

## Documentation Index

Build and serve locally: `.\build-docs.ps1 -Serve` (http://localhost:8080)

| Doc | Purpose |
|-----|---------|
| [admin-commands.md](docs/articles/admin-commands.md) | Admin command documentation |
| [ai-assistant.md](docs/articles/ai-assistant.md) | AI assistant implementation |
| [api-endpoints.md](docs/articles/api-endpoints.md) | REST API documentation |
| [architecture-history.md](docs/articles/architecture-history.md) | Architecture evolution |
| [audit-log-system.md](docs/articles/audit-log-system.md) | Audit logging with fluent builder API |
| [audio-dependencies.md](docs/articles/audio-dependencies.md) | FFmpeg, libsodium, libopus setup |
| [authorization-policies.md](docs/articles/authorization-policies.md) | Role hierarchy, guild access policies |
| [autocomplete-component.md](docs/articles/autocomplete-component.md) | Autocomplete UI component |
| [background-services.md](docs/articles/background-services.md) | Background hosted services, retention, aggregation |
| [bot-performance-dashboard.md](docs/articles/bot-performance-dashboard.md) | Performance monitoring dashboard |
| [bot-verification.md](docs/articles/bot-verification.md) | Bot verification process |
| [command-configuration.md](docs/articles/command-configuration.md) | Command module enable/disable |
| [command-module-configuration.md](docs/articles/command-module-configuration.md) | Module configuration system |
| [commands-page-design.md](docs/articles/commands-page-design.md) | Commands page design spec |
| [commands-page.md](docs/articles/commands-page.md) | Commands page feature |
| [component-api.md](docs/articles/component-api.md) | Component API reference |
| [consent-privacy.md](docs/articles/consent-privacy.md) | User consent and privacy management |
| [database-schema.md](docs/articles/database-schema.md) | Entity relationships and schema |
| [design-system.md](docs/articles/design-system.md) | UI tokens, color palette, theming |
| [discord-bot-setup.md](docs/articles/discord-bot-setup.md) | Discord Developer Portal setup |
| [elastic-apm.md](docs/articles/elastic-apm.md) | Elastic APM distributed tracing |
| [elastic-stack-setup.md](docs/articles/elastic-stack-setup.md) | Local Elastic Stack setup |
| [environment-configuration.md](docs/articles/environment-configuration.md) | Environment configuration |
| [form-implementation-standards.md](docs/articles/form-implementation-standards.md) | Razor Pages form patterns |
| [grafana-dashboards-specification.md](docs/articles/grafana-dashboards-specification.md) | Grafana dashboards spec |
| [guild-layout-spec.md](docs/articles/guild-layout-spec.md) | Guild layout specification |
| [identity-configuration.md](docs/articles/identity-configuration.md) | Authentication setup |
| [interactive-components.md](docs/articles/interactive-components.md) | Discord button/component patterns |
| [issue-tracking-process.md](docs/articles/issue-tracking-process.md) | GitHub issue workflow |
| [jaeger-loki-setup.md](docs/articles/jaeger-loki-setup.md) | Jaeger and Loki setup |
| [kibana-dashboards.md](docs/articles/kibana-dashboards.md) | Kibana dashboards and alerting |
| [linux-deployment.md](docs/articles/linux-deployment.md) | Linux deployment guide |
| [log-aggregation.md](docs/articles/log-aggregation.md) | Elasticsearch and Seq logging |
| [login-page-design-spec.md](docs/articles/login-page-design-spec.md) | Login page design |
| [loki-production-setup.md](docs/articles/loki-production-setup.md) | Loki production setup |
| [member-directory.md](docs/articles/member-directory.md) | Member Directory feature |
| [message-logging.md](docs/articles/message-logging.md) | Message logging system |
| [metrics.md](docs/articles/metrics.md) | Metrics collection |
| [notification-system.md](docs/articles/notification-system.md) | Admin notifications, SignalR broadcasting |
| [permissions.md](docs/articles/permissions.md) | Permission system |
| [rat-watch.md](docs/articles/rat-watch.md) | Rat Watch accountability feature |
| [razor-components.md](docs/articles/razor-components.md) | Razor component library |
| [reminder-system.md](docs/articles/reminder-system.md) | Personal reminders with natural language parsing |
| [repository-pattern.md](docs/articles/repository-pattern.md) | Repository pattern implementation |
| [search.md](docs/articles/search.md) | Global search across portal data |
| [requirements.md](docs/articles/requirements.md) | Project requirements |
| [service-architecture.md](docs/articles/service-architecture.md) | Service interfaces, DI registration, lifetimes |
| [scheduled-messages.md](docs/articles/scheduled-messages.md) | Scheduled/recurring messages |
| [settings-page.md](docs/articles/settings-page.md) | Settings page and real-time updates |
| [signalr-realtime.md](docs/articles/signalr-realtime.md) | SignalR real-time updates |
| [soundboard.md](docs/articles/soundboard.md) | Soundboard feature |
| [soundboard-export-feature.md](docs/specs/soundboard-export-feature.md) | Soundboard export/import specification |
| [testing-guide.md](docs/articles/testing-guide.md) | Testing patterns and fixtures |
| [timezone-handling.md](docs/articles/timezone-handling.md) | Timezone handling |
| [tracing.md](docs/articles/tracing.md) | Distributed tracing |
| [troubleshooting-guide.md](docs/articles/troubleshooting-guide.md) | Common issues and solutions |
| [tts-support.md](docs/articles/tts-support.md) | Text-to-Speech with Azure Cognitive Services |
| [tts-portal.md](docs/requirements/tts-portal.md) | TTS Portal requirements and design |
| [tts-portal-implementation-plan.md](docs/requirements/tts-portal-implementation-plan.md) | TTS Portal implementation plan (4 phases) |
| [unified-command-pages.md](docs/articles/unified-command-pages.md) | Unified command pages architecture |
| [user-management.md](docs/articles/user-management.md) | User management system |
| [utility-commands.md](docs/articles/utility-commands.md) | Utility commands (/userinfo, /serverinfo, /roleinfo) |
| [versioning-strategy.md](docs/articles/versioning-strategy.md) | SemVer versioning and release process |
| [voice-favorites-spec.md](docs/articles/voice-favorites-spec.md) | Voice favorites specification |
| [vox-system-spec.md](docs/articles/vox-system-spec.md) | VOX/FVOX/HGRUNT clip library architecture (v2.0) |
| [vox-ui-spec.md](docs/articles/vox-ui-spec.md) | VOX Portal UI/UX specification (v2.0) |
| [welcome-system.md](docs/articles/welcome-system.md) | Welcome message configuration |

## HTML Prototypes

All prototypes in `docs/prototypes/` - open directly in browser to preview.

| Folder | Purpose |
|--------|---------|
| `docs/prototypes/` | Component showcases, feedback patterns, dashboard layouts |
| `docs/prototypes/components/` | Data display components (cards, tables, lists, badges) |
| `docs/prototypes/forms/` | Form components and validation patterns |
| `docs/prototypes/pages/` | Full page prototypes (servers, settings, commands) |
| `docs/prototypes/features/` | Issue-specific feature prototypes organized by version/feature |
| `docs/prototypes/css/` | Shared CSS infrastructure and Tailwind config |

**When creating prototypes:** Place in `docs/prototypes/features/` organized by issue, use shared CSS from `docs/prototypes/css/`.

## Admin UI (Razor Pages)

Located in `src/DiscordBot.Bot/Pages/`:
- Dashboard (`Index.cshtml`) - Bot status, guild stats, command stats
- Commands (`Pages/Commands/Index.cshtml`) - Slash commands browser
- Account pages - Login, logout, Discord OAuth, account linking
- Admin/Users - Full CRUD (SuperAdmin only)

**Shared Components:** `Pages/Shared/Components/` with ViewModels in `ViewModels/Components/`

**Authorization:**
- Role hierarchy: SuperAdmin > Admin > Moderator > Viewer
- Guild-specific access via `GuildAccessRequirement` and `GuildAccessHandler`

**Adding Pages:**
1. Create `.cshtml` + `.cshtml.cs` in `Pages/`
2. Use `[Authorize(Policy = "RequireAdmin")]` or appropriate policy
3. Inject services via constructor in PageModel
4. Use shared components via `@Html.Partial("Components/_ComponentName", viewModel)`
