# CLAUDE-REFERENCE.md

Auto-generated lookup tables for Discord bot management system. Regenerate with `/update-instructions tables`.

**Last updated:** 2026-04-06

## Configuration Options

The application uses `IOptions<T>` pattern for strongly-typed configuration. All options have sensible defaults in `appsettings.json`.

| Options Class | appsettings Section | Purpose |
|--------------|---------------------|---------|
| `AnalyticsRetentionOptions` | `AnalyticsRetention` | Analytics data retention and aggregation (hourly/daily snapshots) |
| `AnthropicOptions` | `Anthropic` | Anthropic Claude API configuration (API key, model, retries, timeout, prompt caching) |
| `ApplicationOptions` | `Application` | App metadata (title, base URL, version, contact email) |
| `AssistantOptions` | `Assistant` | AI assistant settings, grouped into nested sub-options: `Sampling` (model/tokens/temperature/timeout), `RateLimits` (rate limit + bypass role), `Messages` (question/response length, error text, retry), `Tools` (doc tools, prompt/doc paths), `Cost` (cost tracking, prompt caching), `Privacy` (consent, logging, retention). Historical flat keys (e.g. `Assistant:MaxTokens`) still bind via obsolete forwarding properties and take precedence over the nested key (e.g. `Assistant:Sampling:MaxTokens`) if both are set. |
| `AuditLogRetentionOptions` | `AuditLogRetention` | Audit log cleanup policies |
| `AudioCacheOptions` | `AudioCache` | FFmpeg-processed PCM audio cache (size, TTL, cleanup intervals) |
| `AutoModerationOptions` | `AutoModeration` | Auto-moderation rules, spam/raid detection thresholds |
| `AzureSpeechOptions` | `AzureSpeech` | Azure TTS service settings (voice, speed, pitch, volume) |
| `AzureSpeechSsmlOptions` | `AzureSpeech:Ssml` | SSML validation and style presets for Azure Speech |
| `BackgroundServicesOptions` | `BackgroundServices` | Background task intervals (token refresh, member sync, metrics, cleanup) |
| `BotConfiguration` | `Discord` | Discord bot settings (token, test guild, rate limits, owner IDs) |
| `CachingOptions` | `Caching` | In-memory cache durations for guild, user, and search data |
| `DatabaseSettings` | `Database` | Query performance logging (slow query threshold, parameter logging) |
| `DmAssistantOptions` | `DmAssistant` | DM-based AI assistant (owner-only): model, conversation history, cost tracking, code execution |
| *(string key)* | `Database:Provider` | Database provider selection: `Sqlite`, `PostgreSql`, or omit for auto-detection from connection string |
| `DiscordOAuthOptions` | `Discord:OAuth` | OAuth client credentials (use user secrets) |
| `GuildMembershipCacheOptions` | `GuildMembershipCache` | Guild membership database cache duration |
| `HistoricalMetricsOptions` | `HistoricalMetrics` | Historical metrics collection (sample interval, retention) |
| `IdentityConfigOptions` | `Identity` | ASP.NET Identity settings (passwords, lockout, cookies, default admin) |
| `LogSanitizationOptions` | `LogSanitization` | Log sanitization patterns and sensitive key redaction |
| `MessageLogRetentionOptions` | `MessageLogRetention` | Message log cleanup policies |
| `FeatureRequestsOptions` | `FeatureRequests` | Feature request command settings (description limits, conversation timeout, AI model, doc gen, injection patterns) |
| `MogwaiOptions` | `Mogwai` | Claude Code CLI integration for coding tasks via owner DM (disabled by default; used only by the Mogwai Docker instance) |
| `ModerationOptions` | `Moderation` | Moderation system settings (temp bans, purge limits, case history) |
| `NotXOptions` | `NotX` | X/Twitter link preview settings (HTTP timeout, max response size, user agent) |
| `NotificationOptions` | `Notification` | Admin notification event filters and deduplication |
| `NotificationRetentionOptions` | `NotificationRetention` | Notification cleanup by status (dismissed, read, unread retention) |
| `ObservabilityOptions` | `Observability` | External observability tool URLs (Kibana, Seq) |
| `PerformanceAlertOptions` | `PerformanceAlerts` | Alert thresholds, notification settings, retention |
| `PerformanceBroadcastOptions` | `PerformanceBroadcast` | SignalR broadcast intervals for real-time metrics |
| `PerformanceMetricsOptions` | `PerformanceMetrics` | Performance metrics collection (latency, queries, cache, CPU) |
| `RatWatchOptions` | `RatWatch` | Rat Watch feature settings (voting, timeouts, background service) |
| `ReminderOptions` | `Reminder` | Reminder system (check interval, delivery, per-user limits) |
| `SamplingOptions` | `OpenTelemetry:Tracing:Sampling` | OpenTelemetry trace sampling rates (priority-based) |
| `ScheduledMessagesOptions` | `ScheduledMessages` | Scheduled message execution intervals and concurrency |
| `SoundboardOptions` | `Soundboard` | Audio/soundboard settings (FFmpeg path, file limits, supported formats) |
| `SoundPlayLogRetentionOptions` | `SoundPlayLogRetention` | Sound play log cleanup policies |
| `UserActivityEventRetentionOptions` | `UserActivityEventRetention` | Anonymous activity event retention (consent-free analytics) |
| `VerificationOptions` | `Verification` | Verification code generation and validation |
| `VoiceChannelOptions` | `VoiceChannel` | Voice channel auto-leave timeout and check intervals |
| `VoxOptions` | `Vox` | VOX clip library settings (base path, word gap, message limits) |
| `ElasticApm:*` | `ElasticApm` | Elastic APM distributed tracing configuration |

**Note:** `TtsOptions` in `DiscordBot.Core/Models/` is a runtime DTO (not bound from config). `DefaultAdminOptions` is nested within `IdentityConfigOptions`.

## UI Page Routes

| Page | URL Pattern | Description |
|------|-------------|-------------|
| **Root** | | |
| Dashboard | `/` | Main dashboard with bot status, metrics, activity feed |
| Components | `/Components` | Component gallery/showcase (dev) |
| **Account** | | |
| Login | `/Account/Login` | Sign in with email/password or Discord OAuth |
| Logout | `/Account/Logout` | Sign out |
| Profile | `/Account/Profile` | User profile and theme preferences |
| Privacy | `/Account/Privacy` | Privacy settings and data consent |
| Link Discord | `/Account/LinkDiscord` | Link/unlink Discord account |
| External Login | `/Account/ExternalLogin` | External login callback handler |
| Access Denied | `/Account/AccessDenied` | 403 access denied page |
| Lockout | `/Account/Lockout` | Account lockout notification |
| **Commands** | | |
| Commands | `/Commands` | Command list, execution logs, analytics (tabbed) |
| Command Log Details | `/CommandLogs/Details/{id:guid}` | Single command log entry |
| **Guilds** | | |
| Guilds | `/Guilds` | Connected Discord servers list |
| Guild Details | `/Guilds/Details?id={id}` | Single guild overview |
| Guild Edit | `/Guilds/Edit/{id:long}` | Edit guild settings |
| Guild Analytics | `/Guilds/{guildId:long}/Analytics` | Guild analytics overview |
| Guild Engagement | `/Guilds/{guildId:long}/Analytics/Engagement` | Member engagement metrics |
| Guild Moderation Analytics | `/Guilds/{guildId:long}/Analytics/Moderation` | Moderation activity analytics |
| Audio Settings | `/Guilds/AudioSettings/{guildId:long}` | Guild audio/voice configuration |
| Assistant Settings | `/Guilds/AssistantSettings/{guildId:long}` | AI assistant configuration |
| Assistant Metrics | `/Guilds/AssistantMetrics/{guildId:long}` | AI assistant usage metrics |
| Member Directory | `/Guilds/{guildId:long}/Members` | Guild member list with search/filter |
| Member Moderation | `/Guilds/{guildId:long}/Members/{userId:long}/Moderation` | Member moderation history |
| Moderation Settings | `/Guilds/{guildId:long}/ModerationSettings` | Guild auto-moderation config |
| Flagged Events | `/Guilds/{guildId:long}/FlaggedEvents` | Auto-moderation flagged events |
| Flagged Event Details | `/Guilds/{guildId:long}/FlaggedEvents/{id:guid}` | Single flagged event |
| Feature Requests | `/Guilds/{guildId:long}/FeatureRequests` | Guild feature request submissions |
| Feature Request Details | `/Guilds/{guildId:long}/FeatureRequests/{id:guid}` | Single feature request with admin actions |
| Audio Moderation Log | `/Guilds/{guildId:long}/AudioModerationLog` | Audio playback event log (soundboard, TTS, VOX) |
| Soundboard | `/Guilds/Soundboard/{guildId:long}` | Guild soundboard management |
| Rat Watch | `/Guilds/RatWatch/{guildId:long}` | Rat Watch management |
| Rat Watch Analytics | `/Guilds/RatWatch/Analytics/{guildId:long}` | Rat Watch analytics and metrics |
| Rat Watch Incidents | `/Guilds/RatWatch/Incidents/{guildId:long}` | Incident browser with filtering |
| Reminders | `/Guilds/{guildId:long}/Reminders` | Guild reminders management |
| Scheduled Messages | `/Guilds/ScheduledMessages/{guildId:long}` | Guild scheduled messages |
| Scheduled Message Create | `/Guilds/ScheduledMessages/Create/{guildId:long}` | New scheduled message |
| Scheduled Message Edit | `/Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}` | Edit scheduled message |
| Public Leaderboard | `/Guilds/{guildId:long}/Leaderboard` | Public Rat Watch leaderboard (no auth) |
| **Portals** | | |
| TTS Portal | `/Portal/TTS/{guildId:long}` | TTS message composer for guild members (OAuth required) |
| Soundboard Portal | `/Portal/Soundboard/{guildId:long}` | Soundboard player for guild members (OAuth required) |
| VOX Portal | `/Portal/VOX/{guildId:long}` | VOX announcement composer for guild members (OAuth required) |
| **Admin** | | |
| Settings | `/Admin/Settings` | Application settings (General, Features, Commands, Advanced, Bot Control, Appearance) |
| Users | `/Admin/Users` | User management (SuperAdmin) |
| User Details | `/Admin/Users/Details?id={id}` | User profile and roles |
| User Create | `/Admin/Users/Create` | Create new user |
| User Edit | `/Admin/Users/Edit?id={id}` | Edit user |
| User Purge | `/Admin/UserPurge` | Purge user data (GDPR) |
| Bulk Purge | `/Admin/BulkPurge` | Bulk purge utility |
| Logs | `/Admin/Logs` | Combined logs view (Messages and Audit tabs) |
| Audit Logs | `/Admin/AuditLogs` | System audit trail |
| Audit Log Details | `/Admin/AuditLogs/Details/{id:long}` | Single audit entry |
| Message Logs | `/Admin/MessageLogs` | Discord message history |
| Message Log Details | `/Admin/MessageLogs/Details/{id:long}` | Single message entry |
| Notifications | `/Admin/Notifications` | Manage system notifications |
| Rat Watch Analytics | `/Admin/RatWatchAnalytics` | Cross-guild Rat Watch metrics (Admin+) |
| Performance Dashboard | `/Admin/Performance` | Performance overview dashboard |
| Health Metrics | `/Admin/Performance/HealthMetrics` | Bot health metrics |
| Command Performance | `/Admin/Performance/Commands` | Command response times, throughput, errors |
| System Health | `/Admin/Performance/SystemHealth` | Database, cache, service monitoring |
| API Metrics | `/Admin/Performance/ApiMetrics` | Discord API usage and rate limits |
| Performance Alerts | `/Admin/Performance/Alerts` | Alert thresholds and incident management |
| **Error Pages** | | |
| Error 403 | `/Error/403` | Forbidden |
| Error 404 | `/Error/404` | Not found |
| Error 500 | `/Error/500` | Server error |

**Note:** Use `Guilds/` not `Servers/` for guild-related pages (Discord API terminology).

## Portal API Endpoints

REST API for Portal functionality. All endpoints require `[Authorize(Policy = "PortalGuildMember")]`.

### VOX Portal (`PortalVoxController.cs`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/portal/vox/{guildId}/clips` | GET | Get clips for a group (`?group=vox\|fvox\|hgrunt&search=`) |
| `/api/portal/vox/{guildId}/preview` | GET | Tokenize message and show clip matches (`?message=&group=`) |
| `/api/portal/vox/{guildId}/play` | POST | Play announcement (`{message, group, wordGapMs}`) |
| `/api/portal/vox/{guildId}/stop` | POST | Stop current playback |

### Soundboard Portal (`PortalSoundboardController.cs`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/portal/soundboard/{guildId}/sounds` | GET | Get available sounds |
| `/api/portal/soundboard/{guildId}/sounds` | POST | Search/filter sounds |
| `/api/portal/soundboard/{guildId}/play/{soundId}` | POST | Play a sound |
| `/api/portal/soundboard/{guildId}/channels` | GET | Get voice channels |
| `/api/portal/soundboard/{guildId}/channel` | POST | Set active voice channel |
| `/api/portal/soundboard/{guildId}/channel` | DELETE | Disconnect from channel |
| `/api/portal/soundboard/{guildId}/stop` | POST | Stop playback |
| `/api/portal/soundboard/{guildId}/status` | GET | Get playback status |

### TTS Portal (`PortalTtsController.cs`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/portal/tts/{guildId}/status` | GET | Get playback status |
| `/api/portal/tts/{guildId}/send` | POST | Send TTS message |
| `/api/portal/tts/{guildId}/channels` | GET | Get voice channels |
| `/api/portal/tts/{guildId}/channel` | POST | Set active voice channel |
| `/api/portal/tts/{guildId}/channel` | DELETE | Disconnect from channel |
| `/api/portal/tts/{guildId}/stop` | POST | Stop playback |
| `/api/portal/tts/validate-ssml` | POST | Validate SSML markup |
| `/api/portal/tts/{guildId}/synthesize-ssml` | POST | Synthesize SSML to audio |
| `/api/portal/tts/build-ssml` | POST | Build SSML from parameters |
| `/api/portal/tts/voices/{voiceName}/capabilities` | GET | Get voice capabilities |
| `/api/portal/tts/presets` | GET | Get SSML style presets |

### User Preferences (`UserPreferencesController.cs`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/portal/preferences/{guildId}` | GET | Get all preferences for current user in guild |
| `/api/portal/preferences/{guildId}/{key}` | GET | Get single preference by key |
| `/api/portal/preferences/{guildId}/{key}` | PUT | Set preference value |
| `/api/portal/preferences/{guildId}/{key}` | DELETE | Delete preference |

## Hosted Service Startup Order

The Generic Host runs every registered `IHostedService.StartAsync` **sequentially, in
registration order** (shutdown runs in reverse). Program.cs builds up services through a
chain of `AddXxx(configuration)` extension methods; each one may itself register hosted
services, so the effective order is the order those `AddXxx` calls appear in `Program.cs`,
and — within `DiscordServiceExtensions.AddDiscordBot` — the order of the `AddHostedService`
calls there.

Only a handful of these ~28 hosted services have a real ordering constraint; everything
else is order-agnostic background work (retention/cleanup jobs, metrics aggregators, queue
processors). The constrained ones, in the order they must run:

| # | Hosted service | Registered in | Constraint |
|---|---|---|---|
| 1 | `MemberSyncService` | `DiscordServiceExtensions.AddDiscordBot` | None — queue processor, listed first only by convention. |
| 2 | `SlashCommandRegistrationService` | `DiscordServiceExtensions.AddDiscordBot` | **Must start before `BotHostedService`.** Discovers/loads interaction modules and subscribes to `DiscordSocketClient.Ready` before the gateway logs in, so modules are guaranteed loaded by the time Ready can fire. |
| 3 | `BotHostedService` | `DiscordServiceExtensions.AddDiscordBot` | **Must start after `SlashCommandRegistrationService` (2).** Logs in and starts the gateway (`LoginAsync`/`StartAsync`). Every other Discord-dependent hosted service registered later in `Program.cs` implicitly depends on the client being logged in by the time it runs. |
| 4 | `InteractionStateCleanupService` | `DiscordServiceExtensions.AddDiscordBot` | None — periodic cleanup, kept after login for consistency. |

All other hosted services (`MetricsUpdateService`, `BusinessMetricsUpdateService`,
`AuditLogQueueProcessor`, `AuditLogRetentionService`, `MessageLogCleanupService`,
`NotificationRetentionService`, `RatWatchExecutionService`, `ScheduledMessageExecutionService`,
`ReminderExecutionService`, `VerificationCleanupService`, `VoiceAutoLeaveService`,
`SoundPlayLogRetentionService`, `AudioCacheCleanupService`, `VoxClipLibraryInitializer`,
`AnalyticsRetentionService`, the analytics aggregation services, the performance-metrics
services, `DiscordTokenRefreshService`, `ElasticApmFilterRegistrationService`) run after the
Discord services above (their `Add*` extension methods are called later in `Program.cs`) but
have no ordering requirement among themselves — they only need the client to exist as a
singleton, not to have already logged in.

The ordering constraint and its reasoning is also documented as a comment block at the
registration site: `DiscordServiceExtensions.AddDiscordBot` in
`src/DiscordBot.Bot/Extensions/DiscordServiceExtensions.cs`.

### Lifecycle split: BotHostedService / SlashCommandRegistrationService / BotStatusBroadcaster / InteractionHandler

These four classes used to be two (`BotHostedService`, `InteractionHandler`); each now owns
one lifecycle concern:

- **`BotHostedService`** — gateway login/logout only: wires gateway/message event handlers,
  validates the token, calls `LoginAsync`/`StartAsync`, and reverses that in `StopAsync`.
- **`SlashCommandRegistrationService`** (`ICommandRegistrar`) — discovers command modules
  from the assembly, filters them by `ICommandModuleConfigurationService` configuration, adds
  them to `InteractionService`, and registers commands with Discord (test guild if
  `Discord:TestGuildId` is set, otherwise globally) once `Ready` fires.
- **`BotStatusBroadcaster`** (`IBotStatusBroadcaster`) — publishes `BotStatusUpdateDto` to
  dashboard clients on connect/disconnect/latency-update, and drives Discord presence
  (registers the `CustomStatus` source with `IBotStatusService`, refreshes it on settings
  changes and Rat Watch events).
- **`InteractionHandler`** — interaction dispatch and error handling only: routes
  `InteractionCreated` to the right command, and reports results
  (`SlashCommandExecuted`/`ComponentCommandExecuted`) to logging, metrics, and the dashboard.

## Discord Command Modules

Using Discord.NET 3.19.0-beta.1 - slash commands only, discovered and registered via
`SlashCommandRegistrationService`; dispatched via `InteractionHandler`.

| Module | Commands |
|--------|----------|
| `GeneralModule` | `/ping` |
| `AdminModule` | `/status`, `/guilds`, `/shutdown` |
| `VerifyAccountModule` | `/verify-account` |
| `UtilityModule` | `/userinfo`, `/serverinfo`, `/roleinfo` |
| `VoiceModule` | `/join`, `/join-channel <channel>`, `/leave` |
| `SoundboardModule` | `/play <sound>`, `/sounds`, `/stop` |
| `VoxModule` | `/vox <message> [gap]`, `/fvox <message> [gap]`, `/hgrunt <message> [gap]` |
| `TtsModule` | `/tts <message> [voice]`, `/tts-styled <message> [style]` |
| `RatWatchModule` | Rat Watch (context menu), `/rat-clear`, `/rat-stats`, `/rat-leaderboard`, `/rat-settings` |
| `ReminderModule` | `/remind set`, `/remind list`, `/remind cancel` |
| `ScheduleModule` | `/schedule-list`, `/schedule-create`, `/schedule-delete`, `/schedule-toggle`, `/schedule-run` |
| `WelcomeModule` | `/welcome show/enable/disable/channel/message/test` |
| `ConsentModule` | `/consent grant/revoke/status` |
| `PrivacyModule` | `/privacy preview-delete/export-data/delete-data` |
| `ModerationActionModule` | `/warn`, `/kick`, `/ban`, `/unban`, `/mute`, `/purge`, Warn User (context menu) |
| `ModerationHistoryModule` | `/modlog`, `/case`, `/reason`, `/modexport` |
| `ModStatsModule` | `/modstats` |
| `ModNoteModule` | `/modnote add/list/remove` |
| `ModTagModule` | `/modtag add/remove/list/create/delete` |
| `WatchlistModule` | `/watchlist add/remove/list` |
| `InvestigateModule` | `/investigate` |
| `NotXCommandModule` | `/notx enable`, `/notx disable`, `/notx status`, `/notx sensitive-only`, `/notx channel set`, `/notx channel clear`, `/notx monitor add`, `/notx monitor remove`, `/notx monitor clear` |
| `NotXContextMenuModule` | `Fetch Tweet` (message context menu) |
| `FeatureRequestModule` | `/feature-request` |

**Component-only modules** (handle button/select interactions, no slash commands): `AdminComponentModule`, `RatWatchComponentModule`, `ScheduleComponentModule`, `ModerationHistoryComponentModule`, `FlaggedEventComponentModule`, `FeatureRequestComponentModule`

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

### Articles

| Doc | Purpose |
|-----|---------|
| [admin-commands.md](docs/articles/admin-commands.md) | Admin command documentation |
| [ai-assistant.md](docs/articles/ai-assistant.md) | Claude-powered conversational assistant |
| [alerting-system.md](docs/articles/alerting-system.md) | Performance alerting and incident management |
| [api-endpoints.md](docs/articles/api-endpoints.md) | REST API documentation |
| [architecture-history.md](docs/articles/architecture-history.md) | Architecture evolution (archived) |
| [assistant-feature-updates.md](docs/articles/assistant-feature-updates.md) | Updating AI assistant knowledge for new features |
| [audit-log-system.md](docs/articles/audit-log-system.md) | Audit logging with fluent builder API |
| [audio-dependencies.md](docs/articles/audio-dependencies.md) | FFmpeg, libsodium, libopus setup |
| [authorization-policies.md](docs/articles/authorization-policies.md) | Role hierarchy, guild access policies |
| [autocomplete-component.md](docs/articles/autocomplete-component.md) | Autocomplete UI component |
| [background-services.md](docs/articles/background-services.md) | Background hosted services, retention, aggregation |
| [bot-performance-dashboard.md](docs/articles/bot-performance-dashboard.md) | Performance monitoring dashboard |
| [bot-verification.md](docs/articles/bot-verification.md) | Bot verification as OAuth alternative |
| [configuration-guide.md](docs/articles/configuration-guide.md) | Application configuration reference |
| [command-configuration.md](docs/articles/command-configuration.md) | Command module enable/disable |
| [commands-page-design.md](docs/articles/commands-page-design.md) | Commands page design spec |
| [commands-page.md](docs/articles/commands-page.md) | Commands page feature |
| [component-api.md](docs/articles/component-api.md) | Razor UI component library reference |
| [consent-privacy.md](docs/articles/consent-privacy.md) | GDPR-compliant consent and privacy management |
| [database-schema.md](docs/articles/database-schema.md) | Entity relationships and schema |
| [design-system.md](docs/articles/design-system.md) | UI tokens, color palette, theming |
| [docker-deployment.md](docs/articles/docker-deployment.md) | Docker deployment guide (includes Mogwai container) |
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
| [mogwai.md](docs/articles/mogwai.md) | Mogwai — Claude Code CLI integration via DM (owner-only) |
| [message-logging.md](docs/articles/message-logging.md) | Message logging system |
| [metrics.md](docs/articles/metrics.md) | OpenTelemetry metrics collection |
| [nav-tabs-component.md](docs/articles/nav-tabs-component.md) | Navigation Tabs component guide |
| [nav-tabs-design-spec.md](docs/articles/nav-tabs-design-spec.md) | Navigation Tabs design specification |
| [nav-tabs-migration.md](docs/articles/nav-tabs-migration.md) | Navigation Tabs migration guide |
| [nav-tabs-spec.md](docs/articles/nav-tabs-spec.md) | Navigation component unification spec |
| [not-x/](docs/articles/not-x/) | Not-X feature: X/Twitter link preview embeds (spec, BRD, PRD, user stories, test constraints, feature reference) |
| [feature-request-command/](docs/articles/feature-request-command/) | Feature request command: AI-powered submission with DM conversation and doc gen (BRD, PRD, user stories, architecture, reference) |
| [notification-system.md](docs/articles/notification-system.md) | Real-time notifications with SignalR |
| [permissions.md](docs/articles/permissions.md) | Precondition attribute system |
| [rat-watch.md](docs/articles/rat-watch.md) | Rat Watch accountability feature |
| [razor-components.md](docs/articles/razor-components.md) | Razor component library |
| [reminder-system.md](docs/articles/reminder-system.md) | Personal reminders with natural language parsing |
| [repository-pattern.md](docs/articles/repository-pattern.md) | Repository pattern implementation |
| [requirements.md](docs/articles/requirements.md) | Project requirements |
| [scheduled-messages.md](docs/articles/scheduled-messages.md) | Scheduled/recurring messages |
| [search.md](docs/articles/search.md) | Global search across portal data |
| [service-architecture.md](docs/articles/service-architecture.md) | Service interfaces, DI registration, lifetimes |
| [settings-page.md](docs/articles/settings-page.md) | Settings page and real-time updates |
| [signalr-realtime.md](docs/articles/signalr-realtime.md) | SignalR real-time updates |
| [soundboard.md](docs/articles/soundboard.md) | Soundboard feature |
| [ssml-support.md](docs/articles/ssml-support.md) | SSML markup support for TTS |
| [testing-guide.md](docs/articles/testing-guide.md) | Testing patterns and fixtures |
| [timezone-handling.md](docs/articles/timezone-handling.md) | Timezone handling |
| [tracing.md](docs/articles/tracing.md) | Distributed tracing with OpenTelemetry |
| [troubleshooting-guide.md](docs/articles/troubleshooting-guide.md) | Common issues and solutions |
| [tts-support.md](docs/articles/tts-support.md) | Text-to-Speech with Azure Cognitive Services |
| [unified-command-pages.md](docs/articles/unified-command-pages.md) | Unified command pages architecture |
| [unified-now-playing.md](docs/articles/unified-now-playing.md) | Unified Now Playing component (SignalR, SSR) |
| [user-management.md](docs/articles/user-management.md) | User management system |
| [utility-commands.md](docs/articles/utility-commands.md) | Utility commands (/userinfo, /serverinfo, /roleinfo) |
| [versioning-strategy.md](docs/articles/versioning-strategy.md) | SemVer versioning and release process |
| [voice-capability-system.md](docs/articles/voice-capability-system.md) | Voice capability-aware UI system |
| [voice-favorites-spec.md](docs/articles/voice-favorites-spec.md) | Voice favorites specification |
| [voice-selector-spec.md](docs/articles/voice-selector-spec.md) | Voice selector component (Language → Voice dropdown) |
| [vox-system-spec.md](docs/articles/vox-system-spec.md) | VOX/FVOX/HGRUNT clip library architecture |
| [vox-telemetry-spec.md](docs/articles/vox-telemetry-spec.md) | VOX system telemetry specification |
| [vox-ui-spec.md](docs/articles/vox-ui-spec.md) | VOX Portal UI/UX specification |
| [welcome-system.md](docs/articles/welcome-system.md) | Welcome message configuration |

### Architecture

| Doc | Purpose |
|-----|---------|
| [data-model.md](docs/architecture/data-model.md) | Data model quick reference |
| [feature-map.md](docs/architecture/feature-map.md) | Feature-to-component mapping |
| [patterns.md](docs/architecture/patterns.md) | Recurring patterns and conventions |
| [service-catalog.md](docs/architecture/service-catalog.md) | Service catalog by domain area |
| [system-overview.md](docs/architecture/system-overview.md) | High-level system architecture |
| [ui-inventory.md](docs/architecture/ui-inventory.md) | UI pages and components inventory |

### Specs

| Doc | Purpose |
|-----|---------|
| [ajax-sort-dropdown.md](docs/specs/ajax-sort-dropdown.md) | AJAX SortDropdown component spec |
| [assistant-tool-catalog.md](docs/specs/assistant-tool-catalog.md) | AI assistant tool catalog |
| [connected-servers-widget-design.md](docs/specs/connected-servers-widget-design.md) | Connected servers widget design |
| [connected-servers-widget-spec.md](docs/specs/connected-servers-widget-spec.md) | Connected servers widget spec |
| [issue-319-timezone-fix.md](docs/specs/issue-319-timezone-fix.md) | Timezone fix implementation plan |
| [llm-abstraction-architecture.md](docs/specs/llm-abstraction-architecture.md) | LLM-agnostic abstraction layer |
| [soundboard-export-feature.md](docs/specs/soundboard-export-feature.md) | Soundboard export/import spec |
| [ssml-implementation-spec.md](docs/specs/ssml-implementation-spec.md) | SSML support implementation |
| [ssml-ui-spec.md](docs/specs/ssml-ui-spec.md) | SSML enhancement interface UI/UX |
| [unified-now-playing-spec.md](docs/specs/unified-now-playing-spec.md) | Unified Now Playing component spec |

### Requirements

| Doc | Purpose |
|-----|---------|
| [assistant-implementation-plan.md](docs/requirements/assistant-implementation-plan.md) | AI assistant implementation plan |
| [assistant-requirements.md](docs/requirements/assistant-requirements.md) | AI assistant system requirements |
| [audio-support.md](docs/requirements/audio-support.md) | Audio support & soundboard requirements |
| [command-pages-refactor.md](docs/requirements/command-pages-refactor.md) | Command pages refactoring |
| [dm-assistant-requirements.md](docs/requirements/dm-assistant-requirements.md) | DM chat assistant requirements (Draft) |
| [docker-containerization.md](docs/requirements/docker-containerization.md) | Docker containerization support |
| [guild-header-implementation-plan.md](docs/requirements/guild-header-implementation-plan.md) | Guild header standardization plan |
| [guild-header-standardization.md](docs/requirements/guild-header-standardization.md) | Guild header standardization requirements |
| [landing-page.md](docs/requirements/landing-page.md) | Public landing page requirements |
| [moderation-system.md](docs/requirements/moderation-system.md) | Moderation system requirements |
| [rat-watch-analytics.md](docs/requirements/rat-watch-analytics.md) | Rat Watch analytics requirements |
| [rat-watch-feature.md](docs/requirements/rat-watch-feature.md) | Rat Watch feature requirements |
| [soundboard-member-portal.md](docs/requirements/soundboard-member-portal.md) | Soundboard member portal requirements |
| [theming-system.md](docs/requirements/theming-system.md) | Theming system requirements |
| [tts-portal-implementation-plan.md](docs/requirements/tts-portal-implementation-plan.md) | TTS Portal implementation plan |
| [tts-portal.md](docs/requirements/tts-portal.md) | TTS Portal requirements |

### Other

| Doc | Purpose |
|-----|---------|
| [docs/index.md](docs/index.md) | Documentation index/welcome page |
| [docs/agents/assistant-agent.md](docs/agents/assistant-agent.md) | AI assistant agent prompt |
| [docs/agents/assistant-agent-evil.md](docs/agents/assistant-agent-evil.md) | Evil AI assistant agent (easter egg) |
| [docs/agents/dm-owner-agent.md](docs/agents/dm-owner-agent.md) | DM assistant owner agent prompt |
| [docs/TEST_COVERAGE_GAPS.md](docs/TEST_COVERAGE_GAPS.md) | Test coverage gap analysis and recommended minimum cases |
| [docs/articles/user-profile-extraction/](docs/articles/user-profile-extraction/) | User profile extraction proposal (BRD, PRD, user stories, reference) |
| [docs/changelogs/CHANGELOG-v0.5.0.md](docs/changelogs/CHANGELOG-v0.5.0.md) | v0.5.0 changelog |
| [docs/changelogs/CHANGELOG-v0.5.1.md](docs/changelogs/CHANGELOG-v0.5.1.md) | v0.5.1 changelog |
| [docs/lessons-learned/](docs/lessons-learned/) | 8 post-implementation lessons learned docs |
| [docs/prototypes/](docs/prototypes/) | HTML prototypes (dashboard redesign, forms) |

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

### Blazor (`src/DiscordBot.Bot/Blazor/`)

An early/experimental interactive-component track, **not** the primary UI pattern and **not** a
migration target for existing Razor Pages — those stay plain Razor Pages + vanilla JS.

- **Wired in for real:** `WebServiceExtensions.cs` calls `services.AddServerSideBlazor()`, and
  `Program.cs` calls `app.MapBlazorHub()` (`/_blazor` circuit hub, coexists with the SignalR
  dashboard hub). `Pages/Components.cshtml` (the shared-component showcase page) renders
  `Blazor/Pages/FoundationProbe.razor` via `<component type="typeof(...FoundationProbe)"
  render-mode="ServerPrerendered" />` — that is currently the only place a Blazor component is
  hosted.
- **Contents:** `Blazor/Pages/FoundationProbe.razor` (probe/demo page), `Blazor/Shared/UiButton.razor`
  and `UiToggle.razor` (component prototypes), `Blazor/Interop/ToastInterop.cs` /
  `ThemeInterop.cs` (JS interop for toasts/theme), `Blazor/Common/Debouncer.cs`,
  `Blazor/_Imports.razor`. `wwwroot/js/blazor-interop.js` backs the interop calls.
- **Do not delete** — it is a live, low-traffic experiment used to evaluate the pattern, kept
  isolated to `Pages/Components.cshtml` so it can't affect the rest of the site.
