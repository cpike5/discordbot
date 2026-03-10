# Kibana Dashboards Specification — DiscordBot Production

## Infrastructure

- **Elasticsearch 8.12.0** at `http://cpike.ca:9200`
- **Kibana** at `https://kibana.cpike.ca`
- **API Key**: `MmpKUjJKd0JlQW1kS3JIdFRDVXI6OThCMWpGd01TR3lkVzEwYjdEcE9tZw==`
- **ES auth**: `Authorization: ApiKey <key>` over HTTP
- **Kibana auth**: Same API key over HTTPS, mutations require `kbn-xsrf: true`

## Data Sources

| Data View ID | Name | Pattern | Use |
|-------------|------|---------|-----|
| `discordbot-logs-prod` | DiscordBot Logs - Production | `logs-discordbot-production` | Application logs |
| `apm_static_data_view_id_default` | APM | `traces-apm*,logs-apm*,metrics-apm*` | APM traces/spans/errors |

### Global Filters (all dashboards)

- `service.environment: production` on APM panels
- `service.name: discordbot` (lowercase) on APM panels to exclude casing variant

### Log Document Structure (ECS 8.11.0 via Elastic.CommonSchema.Serilog)

Key fields:
- `@timestamp` — event time
- `message` — rendered log message
- `log.level` — Information, Warning, Error, Fatal
- `log.logger` — full .NET namespace (e.g., `DiscordBot.Bot.Services.PlaybackService`)
- `trace.id`, `transaction.id`, `span.id` — APM correlation
- `service.name`, `service.version` — service identity
- `host.name`, `host.hostname` — host identity
- `process.thread.name`, `process.thread.id` — thread info
- `metadata.*` — structured properties from Serilog (varies per logger)
- `labels.MessageTemplate` — Serilog message template
- `labels.ElasticApmTraceId`, `labels.ElasticApmTransactionId` — APM correlation (redundant with trace.id)
- `event.severity` — numeric log level

### APM Document Structure

Traces (`traces-apm-default`):
- `processor.event` — `transaction` or `span`
- `transaction.name` — e.g., `GET /Account/Login`, `POST PortalTts/SendTts {guildId}`
- `transaction.type` — `request`, `background`, `unknown`, `discord.interaction`
- `transaction.duration.us` — microseconds
- `event.outcome` — `success` or `failure`
- `span.type` — `db`, `external`, `app`, `unknown`
- `span.subtype` — `sqlite`, `http`, `internal`
- `span.duration.us` — microseconds
- `service.target.name` — dependency name (e.g., `main`, `localhost:9200`, `discord.com:443`)
- `http.response.status_code` — HTTP status

---

## Dashboard 1: Operations Overview

**Purpose**: At-a-glance daily monitoring. Default dashboard.
**Default time range**: Last 24 hours, auto-refresh 30s.

| # | Title | Viz Type | Data Source | Query / Aggregation | Notes |
|---|-------|----------|-------------|---------------------|-------|
| 1 | Service Restarts | Metric | Logs | `log.logger: "Microsoft.Hosting.Lifetime" AND message: "Application started"` count | Shows restart count |
| 2 | Current Version | Metric | Logs | Latest `service.version` value (top_hits, size 1, sort @timestamp desc) | |
| 3 | Log Volume by Level | Stacked area | Logs | date_histogram on `@timestamp`, split by `log.level` | Colors: Info=blue, Warn=yellow, Error=red, Fatal=purple |
| 4 | Error Count | Metric + trend sparkline | Logs | `log.level: "Error"` count | Compare to previous period |
| 5 | Request Throughput | Line | APM traces | `processor.event: transaction AND transaction.type: request` count per interval | Transactions/min |
| 6 | Request Latency (p50/p95) | Line (dual) | APM traces | Percentiles of `transaction.duration.us` for request type | Convert to ms for display |
| 7 | Dependency Health | Horizontal bar | APM spans | Avg `span.duration.us` grouped by `service.target.name` | Filter: `processor.event: span` |
| 8 | Error Rate by Outcome | Donut | APM traces | `event.outcome` terms for transactions | success vs failure |
| 9 | Recent Errors | Table | Logs | `log.level: "Error"`, sort desc, columns: @timestamp, log.logger, message, trace.id | Last 20 rows |

---

## Dashboard 2: Error & Exception Analysis

**Purpose**: Deep dive during error spikes.
**Default time range**: Last 7 days.

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Errors Over Time | Stacked area | Logs | `log.level: "Error"` date_histogram, split by `log.logger` (top 10) |
| 2 | Error Sources | Treemap | Logs | `log.level: "Error"`, terms on `log.logger` |
| 3 | Exception Types | Horizontal bar | Logs | `log.level: "Error"`, terms on `metadata.ExceptionDetail.Type` |
| 4 | Error Messages | Table | Logs | `log.level: "Error"`, terms on `message` (top 20) |
| 5 | Warning Trend | Line | Logs | `log.level: "Warning"` count over time |
| 6 | Warning Sources | Pie | Logs | `log.level: "Warning"`, terms on `log.logger` (top 10) |
| 7 | APM Error Events | Table | APM errors | From `logs-apm.error-default`: error.exception.type, error.exception.message, trace.id |
| 8 | Correlated Traces | Saved search link | — | Drilldown from trace.id to APM trace view |

### Key error sources (production):
- PlaybackService: 190 (FFmpeg failures)
- SoundFileService: 125
- EF Core Query: 56
- Antiforgery: 34
- ExceptionHandlerMiddleware: 28
- QueryPerformanceInterceptor: 27

---

## Dashboard 3: Web Portal Performance

**Purpose**: ASP.NET Core web portal monitoring.
**Default time range**: Last 24 hours.

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Top Endpoints by Throughput | Horizontal bar | APM traces | `transaction.type: request`, terms on `transaction.name`, sorted by count |
| 2 | Slowest Endpoints (p95) | Horizontal bar | APM traces | `transaction.type: request`, terms on `transaction.name`, p95 of `transaction.duration.us` |
| 3 | Login Activity | Line | APM traces | Filter: `transaction.name: "GET /Account/Login" OR "POST /Account/Login"`, count over time |
| 4 | TTS Endpoint Latency | Line | APM traces | Filter names: GetStatus, SendTts, GetVoiceCapabilities — p50/p95 duration |
| 5 | Soundboard Endpoint Latency | Line | APM traces | Filter: PlaySound, Soundboard/Index — p50/p95 duration |
| 6 | SignalR Hub Activity | Metric + line | APM traces | `/hubs/dashboard/negotiate` + `CONNECT /hubs/dashboard` volume |
| 7 | SQLite Query Latency | Line | APM spans | `span.type: db AND span.subtype: sqlite` avg + p95 of `span.duration.us` |
| 8 | HTTP Error Responses | Line | APM traces | `http.response.status_code >= 400` count over time |
| 9 | Suspicious Traffic | Table | APM traces | Filter: transaction.name matches `/wp-login.php`, `/.env`, etc. |

---

## Dashboard 4: Bot Background Services

**Purpose**: Discord bot worker health.
**Default time range**: Last 24 hours.

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Background Service Log Volume | Stacked area | Logs | Filter key loggers: BotHostedService, BackgroundServiceHealthRegistry, ChannelActivityAggregation, MemberActivityAggregation, AlertMonitoring. date_histogram split by logger |
| 2 | Command Performance Metrics | Line | Logs | CommandLogRepository: `metadata.SuccessRate` avg over time |
| 3 | Command Success/Failure | Metric | Logs | CommandLogRepository: sum of `metadata.SuccessCount` and `metadata.FailureCount` |
| 4 | Member Sync Activity | Line | Logs | MemberSyncService log volume over time |
| 5 | Audit Log Processing | Line | Logs | AuditLogQueueProcessor + AuditLogRepository volume over time |
| 6 | Bot Lifecycle Events | Table | APM traces | `transaction.name: bot.lifecycle.*` — timestamps and durations |
| 7 | Connection State | Line | Logs | ConnectionStateService volume, highlight warnings (disconnects) |
| 8 | Discord API Calls | Line | APM spans | Spans targeting `discord.com:443` and `gateway*.discord.gg` — count + avg latency |

---

## Dashboard 5: External Dependencies

**Purpose**: Health of all outbound connections.
**Default time range**: Last 24 hours.

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Dependency Map | Donut | APM spans | `processor.event: span`, terms on `service.target.name` |
| 2 | SQLite Performance | Line (dual axis) | APM spans | `span.subtype: sqlite` — count (throughput) + avg/p95 duration. Currently avg 2.5ms |
| 3 | Elasticsearch Self-Writes | Line | APM spans | `service.target.name: "localhost:9200"` — count + avg duration. Currently avg 21.7ms |
| 4 | Discord API Latency | Line | APM spans | `service.target.name: "discord.com:443"` — avg + p95 duration |
| 5 | Discord Gateway | Metric + line | APM spans | `service.target.name: gateway*.discord.gg*` — connection count + latency |
| 6 | Discord Voice | Table | APM spans | `service.target.name: *.discord.media*` — voice server connections |
| 7 | Failed External Calls | Table | APM spans | `event.outcome: failure AND span.type: external` |
| 8 | ES Bulk Write Errors | Line | APM errors | HTTP errors targeting `_bulk` endpoint |

---

## Dashboard 6: Log Deep Dive (Saved Search)

**Purpose**: Ad-hoc log investigation with preset columns.
**Type**: Discover saved search, not a Lens dashboard.

| Column | Field |
|--------|-------|
| Timestamp | `@timestamp` |
| Level | `log.level` |
| Logger | `log.logger` |
| Message | `message` |
| Trace ID | `trace.id` |
| Thread | `process.thread.name` |
| Version | `service.version` |

**Pre-configured filter**: `log.level` is not `Information` (show only Warn/Error/Fatal by default).
**Data view**: `discordbot-logs-prod`.

---

## Dashboard 7: Audio & Voice Overview

**Purpose**: Unified Soundboard, TTS, and VOX monitoring.
**Default time range**: Last 7 days.

### Audio Logger Inventory
- PlaybackService: 2,926 events (190 errors — 6.5% failure rate)
- SoundService: 1,598
- PortalSoundboardController: 1,550
- PortalTtsController: 1,418
- AzureTtsService: 1,156
- VoxClipLibrary: 856
- SoundPlayLogRetentionService: 781
- VoiceAutoLeaveService: 782
- AudioService: 457
- SoundFileService: 426 (125 errors)
- PortalVoxController: 410
- SoundboardOrchestrationService: 284
- SoundboardModule: 255
- VoxClipLibraryInitializer: 253
- TtsHistoryService: 200
- VoxService: 197
- TtsPlaybackService: 174
- SoundCacheService: 170
- AudioCacheCleanupService: 160
- SoundsController: 164
- AudioController: 157
- SoundPlayLogRepository: 141
- VoxConcatenationService: 100
- TtsModule: 97

### Structured Metadata Available

**PlaybackService**: `metadata.GuildId`, `metadata.SoundId`, `metadata.Filter`
**AzureTtsService**: `metadata.SizeBytes` (synthesis output), `metadata.Speed`, `metadata.Pitch`, `metadata.Volume`
**VoxConcatenationService**: `metadata.ClipCount`, `metadata.AudioBytes`, `metadata.ConcatenationMs`
**SoundboardOrchestrationService**: `metadata.GuildId`, `metadata.SoundId`, `metadata.UserId`
**PlaybackService errors**: `metadata.ExceptionDetail.Type` = InvalidOperationException, `metadata.ExceptionDetail.Message` = "FFmpeg playback failed for sound {name}"

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Audio Plays Over Time | Stacked area | Logs | Filter: PlaybackService "Starting playback" + TtsPlaybackService + VoxService, date_histogram split by subsystem |
| 2 | Soundboard Plays by Sound | Horizontal bar | Logs | PlaybackService "Starting playback", terms on `metadata.SoundId` (top 15). Sound names are in the message |
| 3 | Playback Success vs Error | Donut | Logs | PlaybackService log.level distribution |
| 4 | FFmpeg Failures | Table | Logs | PlaybackService errors: @timestamp, message, metadata.SoundId, metadata.GuildId |
| 5 | TTS Synthesis Volume | Line | Logs | AzureTtsService "Speech synthesis completed" count over time |
| 6 | TTS Audio Size | Line (avg) | Logs | AzureTtsService `metadata.SizeBytes` avg over time |
| 7 | TTS Voice Usage | Pie | Logs | AzureTtsService "Built SSML" messages, parse voice name from message |
| 8 | VOX Concatenation Performance | Line | Logs | VoxConcatenationService: `metadata.ConcatenationMs` avg + p95 |
| 9 | VOX Clip Count Distribution | Bar | Logs | VoxConcatenationService: `metadata.ClipCount` histogram |
| 10 | VOX Audio Output Size | Metric (avg) | Logs | VoxConcatenationService: `metadata.AudioBytes` avg |
| 11 | Soundboard Orchestration Activity | Line | Logs | SoundboardOrchestrationService count over time |
| 12 | Portal Controller Activity | Stacked area | Logs | PortalSoundboardController vs PortalTtsController vs PortalVoxController |
| 13 | Voice Channel Auto-Leave | Metric | Logs | VoiceAutoLeaveService event count |
| 14 | Audio Cache Health | Line | Logs | SoundCacheService + AudioCacheCleanupService volume |
| 15 | Audio Errors by Source | Treemap | Logs | Error-level logs from all audio loggers |
| 16 | TTS Endpoint Latency | Line | APM traces | `transaction.name` matching TTS endpoints — p50/p95 |
| 17 | Soundboard Endpoint Latency | Line | APM traces | `transaction.name` matching Soundboard endpoints — p50/p95 |
| 18 | Discord Voice Server Connections | Table | APM spans | `service.target.name: *.discord.media*` |

---

## Dashboard 8: AI Assistant Overview

**Purpose**: Cost tracking, usage analytics, and agent performance for the Anthropic-powered assistant.
**Default time range**: Last 7 days.

### AI Assistant Logger Inventory
- ToolRegistry: 545 (tool provider registrations)
- AnthropicLlmClient: 171 (API completions)
- AgentRunner: 97 (agent loop completions)
- DmAssistantMessageHandler: 65 (DM message delivery)
- DmAssistantService: 64 (DM response metrics)
- AssistantSettingsModel: 46 (settings page views)
- AssistantMessageHandler: 42 (guild message delivery)
- AssistantService: 42 (guild response metrics)
- AssistantGuildSettingsService: 30 (guild config)
- AssistantUsageMetricsRepository: 24 (usage metrics persistence)
- AssistantMetricsModel: 2 (metrics page views)
- PromptTemplate: 2

### Structured Metadata Available

**DmAssistantService**: `metadata.InputTokens`, `metadata.OutputTokens`, `metadata.CachedTokens`, `metadata.Cost`, `metadata.LatencyMs`, `metadata.ToolCalls`, `metadata.Loops`, `metadata.UserId`
**AssistantService**: `metadata.TotalTokens`, `metadata.LatencyMs`, `metadata.Cost`
**AnthropicLlmClient**: `metadata.InputTokens`, `metadata.OutputTokens`, `metadata.CachedTokens`
**AgentRunner**: `metadata.TotalTokens`, `metadata.Iterations`, `metadata.ToolCalls`
**AssistantUsageMetricsRepository**: `metadata.GuildId`, `metadata.Date`, `metadata.Id`

### Tool Providers Registered
- Memory: 5 tools
- BotManagement: 4 tools
- Documentation: 4 tools
- Conversation: 2 tools
- DmModeration: 2 tools
- DmAnalytics: 2 tools
- CodeExecution: 1 tool
- WebFetch: 1 tool

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Conversations Over Time | Line | Logs | DmAssistantService + AssistantService response messages, date_histogram |
| 2 | Total Cost (period) | Metric (sum) | Logs | `metadata.Cost` from DmAssistantService + AssistantService |
| 3 | Cost Over Time | Stacked area | Logs | `metadata.Cost` sum per day, stacked: DmAssistantService vs AssistantService |
| 4 | Token Usage Breakdown | Stacked bar | Logs | AnthropicLlmClient: sum of `metadata.InputTokens`, `metadata.OutputTokens`, `metadata.CachedTokens` per day |
| 5 | Cache Hit Efficiency | Line | Logs | AnthropicLlmClient: `metadata.CachedTokens / (metadata.InputTokens + metadata.CachedTokens) * 100` |
| 6 | Response Latency (p50/p95) | Line | Logs | DmAssistantService + AssistantService: percentiles of `metadata.LatencyMs` |
| 7 | Agentic Loops per Request | Bar (histogram) | Logs | AgentRunner: `metadata.Iterations` value distribution |
| 8 | Tool Calls per Request | Bar | Logs | AgentRunner: `metadata.ToolCalls` value distribution |
| 9 | Tool Providers | Table | Logs | ToolRegistry: provider name, tool count from most recent registration set |
| 10 | DM vs Guild Split | Donut | Logs | DmAssistantService count vs AssistantService count |
| 11 | Active Users | Metric (cardinality) | Logs | Unique `metadata.UserId` from DmAssistantMessageHandler |
| 12 | Per-User Usage | Horizontal bar | Logs | `metadata.UserId` from DmAssistantService, top users by count |
| 13 | Cost per Conversation (avg) | Line | Logs | Avg `metadata.Cost` over time from DmAssistantService + AssistantService |
| 14 | Errors | Table | Logs | Error/Warning level logs from all LLM/Assistant loggers |
| 15 | Guild Settings Activity | Metric | Logs | AssistantGuildSettingsService + AssistantSettingsModel count |

---

## Dashboard 9: Moderation & Safety

**Purpose**: Raid detection, moderation configuration, safety features.
**Default time range**: Last 7 days.

### Relevant Loggers
- RatWatch.RatWatchService: 1,037 events
- RaidDetectionService: 203 events (`metadata.MaxJoins`)
- GuildModerationConfigService: config updates (`metadata.GuildId`)
- ModerationSettings.IndexModel: page views
- Guilds.Members.ModerationModel: 20 warnings

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Raid Detection Initializations | Line | Logs | RaidDetectionService volume over time (restarts show re-init) |
| 2 | Rat Watch Events | Line | Logs | RatWatchService: `metadata.Count` values over time |
| 3 | Moderation Config Changes | Table | Logs | GuildModerationConfigService: @timestamp, message, metadata.GuildId |
| 4 | Moderation Page Activity | Line | Logs | ModerationModel + ModerationSettings.IndexModel volume |

---

## Dashboard 10: Scheduling & Notifications

**Purpose**: Scheduled messages, reminders, notification delivery reliability.
**Default time range**: Last 7 days.

### Relevant Loggers
- NotificationService: 1,899
- NotificationRepository: 1,988
- ReminderExecutionService: 845 (`metadata.IntervalSeconds`, `metadata.MaxConcurrent`, `metadata.MaxAttempts`)
- NotificationRetentionService: 687
- ScheduledMessageService: 319 (`metadata.GuildId`, `metadata.Count`, `metadata.Total`)
- ScheduledMessageExecutionService: 219

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Notification Volume | Line | Logs | NotificationService + NotificationRepository volume over time |
| 2 | Reminder Service Health | Line | Logs | ReminderExecutionService volume, colored by log.level |
| 3 | Scheduled Messages | Line | Logs | ScheduledMessageService + ScheduledMessageExecutionService volume |
| 4 | Scheduled Message Count | Metric | Logs | ScheduledMessageService: latest `metadata.Total` |
| 5 | Notification Retention | Metric | Logs | NotificationRetentionService event count |
| 6 | Errors | Table | Logs | Error level from any scheduling/notification logger |

---

## Dashboard 11: Data & Retention Health

**Purpose**: Background housekeeping and data pipeline health.
**Default time range**: Last 7 days.

### Relevant Loggers
- AuditLogQueueProcessor: 2,865
- AuditLogRepository: 2,599
- MemberSyncService: 2,053
- GuildMemberRepository: 1,673
- AnalyticsRetentionService: 1,011
- AuditLogRetentionService: 977
- MessageLogCleanupService: 977
- SoundPlayLogRetentionService: 781
- MetricsCollectionService: 761
- NotificationRetentionService: 687
- AuditLogService: 665
- MetricSnapshotRepository: 372
- ChannelActivityRepository: 340
- MemberActivityRepository: 340
- GuildMetricsAggregationService: 1,080
- BusinessMetricsUpdateService: 428
- InteractionStateCleanupService: 428
- VerificationCleanupService: 428

| # | Title | Viz Type | Data Source | Query / Aggregation |
|---|-------|----------|-------------|---------------------|
| 1 | Retention Services | Stacked area | Logs | AnalyticsRetention + AuditLogRetention + MessageLogCleanup + SoundPlayLogRetention + NotificationRetention — date_histogram split by logger |
| 2 | Audit Log Pipeline | Line (dual) | Logs | AuditLogQueueProcessor (intake) + AuditLogRepository (persist) — count over time |
| 3 | Member Sync Pipeline | Line | Logs | MemberSyncService + GuildMemberRepository volume |
| 4 | Metrics Collection | Line | Logs | MetricsCollectionService + MetricSnapshotRepository + GuildMetricsAggregation |
| 5 | Activity Tracking | Line | Logs | ChannelActivityRepository + MemberActivityRepository |
| 6 | Cleanup Services | Stacked bar | Logs | VerificationCleanup + InteractionStateCleanup + BusinessMetricsUpdate — per day |
| 7 | Errors in Data Pipeline | Table | Logs | Error level from any repository or retention logger |

---

## Implementation Notes

### Kibana API Approach
- Use **Saved Objects API** (`POST /api/saved_objects/_import`) with NDJSON format
- Each dashboard = 1 dashboard object + N visualization (Lens) objects
- Lens visualizations use `visualizationState` JSON with layer configurations
- Reference Kibana data views by ID (`discordbot-logs-prod`, `apm_static_data_view_id_default`)

### Panel Layout Convention
- 48-column grid (Kibana default)
- Row 0: KPI metrics (h=8, w=12 each, 4 across)
- Rows below: charts (h=15, full or half width)
- Tables at bottom (h=12, full width)

### Color Conventions
- Log levels: Information=#6DCCB1, Warning=#E8C44A, Error=#E7664C, Fatal=#920000
- Subsystems: Soundboard=#54B399, TTS=#6092C0, VOX=#D36086
- Success=#54B399, Failure=#E7664C

### Naming Convention
- Dashboard: `[DiscordBot] {Name}`
- Visualization: `[DiscordBot] {Dashboard} — {Panel Title}`
