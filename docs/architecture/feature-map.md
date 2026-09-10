# Discord Bot Feature Map

**Purpose**: Quick reference for understanding feature boundaries and component composition.

This document maps all major features to their supporting components: Discord commands, services, UI pages, and database entities.

---

## Table of Contents

1. [Audio Features](#audio-features) - Soundboard, VOX, TTS, Audio Moderation Log, User Preferences
2. [Moderation Features](#moderation-features) - Warnings, bans, notes, watchlist
3. [Community Features](#community-features) - Reminders, Rat Watch, Scheduled Messages, Not-X, Feature Requests
4. [Administrative Features](#administrative-features) - Guild management, settings, monitoring, verification
5. [System Features](#system-features) - Authentication, logging, notifications, activity tracking, DM assistant

---

## Audio Features

### Soundboard

The soundboard system allows guild members to play pre-uploaded audio files in voice channels, with optional audio filtering and queue management.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/play`, `/sounds`, `/stop` (SoundboardModule) |
| **Services** | `IAudioService`, `IPlaybackService`, `ISoundService`, `ISoundboardOrchestrationService`, `IGuildAudioSettingsService`, `IAudioNotifier` |
| **UI Pages** | Portal: Soundboard player page; Admin: Sounds management (`SoundsController`) |
| **Database Entities** | `Sound`, `SoundPlayLog`, `GuildAudioSettings`, `AudioPlaybackLog` |
| **Storage** | Audio files on disk (configurable path) |
| **Key Features** | Queue management, audio filtering (distortion, echo, pitch shift), silent playback mode, auto-leave voice channels |
| **Rate Limiting** | 5 commands per 10 seconds |

**Architecture Flow**:
```
User invokes /play → SoundboardModule →
  SoundService (fetch sound) →
  SoundboardOrchestrationService (handle queue/filter) →
  PlaybackService (manage playback queue) →
  AudioService (join channel, play audio) →
  SoundPlayLog (record usage)
```

---

### VOX System

Half-Life style concatenated clip announcements using scientist (VOX), female scientist (FVOX), and military radio (HGRUNT) voice groups. Clips are auto-scanned at startup and concatenated with configurable word gaps.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/vox`, `/fvox`, `/hgrunt` (VoxModule) |
| **Services** | `IVoxService`, `IVoxClipLibrary`, `IVoxConcatenationService`, `VoxClipLibraryInitializer` |
| **UI Pages** | Portal: VOX player page (`PortalVoxController`) |
| **Database Entities** | None (clips are files only) |
| **Storage** | VOX clips on disk organized by group (`sounds/vox/`, `sounds/fvox/`, `sounds/hgrunt/`) |
| **Configuration** | `VoxOptions` in appsettings.json |
| **Key Features** | Word-level clipping, configurable gap between words (20-200ms), autocomplete for available clips, clip group filtering |
| **Rate Limiting** | 5 commands per 10 seconds |

**Configuration**:
```json
{
  "Vox": {
    "BasePath": "./sounds",
    "DefaultWordGapMs": 50,
    "MaxMessageWords": 50,
    "MaxMessageLength": 500
  }
}
```

**Preconditions**: `[RequireGuildActive]`, `[RequireAudioEnabled]`, `[RequireVoiceChannel]`

---

### Text-to-Speech (TTS)

Converts text messages to speech using Azure Cognitive Services and plays them in voice channels. Supports voice selection, SSML styling, and guild-level settings.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/tts <message> [voice]` (TtsModule) |
| **Services** | `IAudioService`, `ITtsService`, `ITtsSettingsService`, `ITtsPlaybackService`, `IAzureTtsService`, `ISsmlBuilder`, `IStylePresetProvider` |
| **UI Pages** | Portal: TTS player; Admin: TTS settings configuration |
| **Database Entities** | `TtsMessage`, `GuildTtsSettings` |
| **External Services** | Azure Cognitive Services (Speech API) |
| **Key Features** | Voice presets (male/female), SSML support for emotion/style control, guild-level enable/disable, user consent tracking |
| **Rate Limiting** | 5 commands per 10 seconds |

**Preconditions**: `[RequireGuildActive]`, `[RequireTtsEnabled]`, `[RequireVoiceChannel]`

---

### Audio Moderation Log

Unified playback log tracking all audio feature usage (soundboard, TTS, VOX) for moderation and auditing. Admin UI page with filtering by user, feature type, and date range.

| Aspect | Components |
|--------|------------|
| **Services** | `IAudioModerationLogService`, `IAudioPlaybackLogRepository` |
| **UI Pages** | Admin: Audio moderation log (`/guilds/{guildId}/audio-moderation-log`) |
| **Database Entities** | `AudioPlaybackLog` |
| **Enums** | `AudioFeatureType` (Soundboard, Tts, Vox) |
| **Key Features** | Fire-and-forget logging via `IBackgroundTaskRunner`, content name truncation (200 chars), per-guild/per-user filtering |

**Architecture Flow**:
```
Audio command (Soundboard/TTS/VOX) →
  Orchestration service calls IAudioModerationLogService.LogPlayback() →
  BackgroundTaskRunner (fire-and-forget) →
  IAudioPlaybackLogRepository.AddAsync() →
  Admin UI queries AudioPlaybackLog table
```

---

### User Preferences

Per-user, per-guild preference storage with REST API and client-side localStorage cache. Supports arbitrary key-value preferences (e.g., selected TTS voice, playback mode).

| Aspect | Components |
|--------|------------|
| **Controllers** | `UserPreferencesController` (`api/portal/preferences/{guildId}`) |
| **Client JS** | `user-preferences.js` |
| **Database Entities** | `UserPreference` |
| **Key Features** | GET/PUT/DELETE per key, localStorage sync, 100-char key limit, 2000-char value limit, guild-scoped |
| **Authorization** | `PortalGuildMember` policy |

---

## Moderation Features

### Direct Moderation Actions

Slash and context menu commands for immediate moderation actions (warn, kick, ban, unban, mute, purge) with logging and reason tracking.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/warn`, `/kick`, `/ban`, `/unban`, `/mute`, `/purge`, `Warn User` (context menu) (ModerationActionModule) |
| **Services** | `IModerationService`, `IAuditLogService` |
| **UI Pages** | Admin: Moderation case history pages |
| **Database Entities** | `ModerationCase`, `AuditLog` |
| **Key Features** | Reason tracking, case numbering, member audit trail, soft bans, mute duration configuration |

**Preconditions**: `[RequireGuildActive]`, `[RequireModerationEnabled]`, `[RequireModerator]`

---

### Moderator Notes

Private annotations on users visible only to moderators. Used for tracking behavioral concerns, observations, and context without formal action.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/modnote add`, `/modnote view`, `/modnote delete`, `/modnote list` (ModNoteModule) |
| **Services** | `IModNoteService` |
| **UI Pages** | Admin: Mod notes management |
| **Database Entities** | `ModNote` |
| **Key Features** | Per-user note history, moderator attribution, timestamps, full-text searchable, private to moderators only |

**Preconditions**: `[RequireGuildActive]`, `[RequireModerationEnabled]`, `[RequireModerator]`

---

### Moderator Tags

Tag system for categorizing user behavioral concerns and issues (e.g., "Spam", "Harassment", "Ban Evasion").

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/modtag add`, `/modtag remove`, `/modtag list` (ModTagModule) |
| **Services** | `IModTagService` |
| **UI Pages** | Admin: Tag management page |
| **Database Entities** | `ModTag`, `UserModTag` |
| **Key Features** | Reusable tag library, bulk assignment, color coding, quick filtering |

**Preconditions**: `[RequireGuildActive]`, `[RequireModerationEnabled]`, `[RequireModerator]`

---

### Watchlist

Moderator watchlist to flag users for closer monitoring without taking action. Includes optional reasons.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/watchlist add`, `/watchlist remove`, `/watchlist view` (WatchlistModule) |
| **Services** | `IWatchlistService` |
| **UI Pages** | Admin: Watchlist management |
| **Database Entities** | `Watchlist` |
| **Key Features** | Reason tracking, timestamp recording, moderator-only visibility |

**Preconditions**: `[RequireGuildActive]`, `[RequireModerationEnabled]`, `[RequireModerator]`

---

### Moderation History

Query and view historical moderation cases with filtering by user, action type, date range.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/history user`, `/history stats`, context menu integration (ModerationHistoryModule, ModerationHistoryComponentModule) |
| **Services** | `IModerationService`, `ISearchService` |
| **UI Pages** | Admin: Detailed moderation history with filtering and export |
| **Database Entities** | `ModerationCase` |
| **Key Features** | Date range filtering, user search, action type grouping, pagination |

---

### Moderation Statistics

Quick statistics on moderator performance and guild moderation trends.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/modstats` (ModStatsModule) |
| **Services** | `IModerationService` |
| **Database Entities** | `ModerationCase` |
| **Key Features** | Moderator action counts, case resolution tracking, trend analysis |

---

### Investigation Tools

Detailed user investigation interface showing moderation history, activity patterns, and risk assessment.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/investigate` (InvestigateModule) |
| **Services** | `IInvestigationService`, `IModerationService`, `ISearchService` |
| **UI Pages** | Admin: Investigation dashboard |
| **Database Entities** | `ModerationCase`, `ModNote`, `Watchlist`, `MessageLog`, `CommandLog` |
| **Key Features** | Cross-source data aggregation (messages, commands, cases), timeline view, risk scoring |

---

## Community Features

### Reminders

Personal reminders delivered via DM with flexible time parsing (relative and absolute).

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/remind set`, `/remind list`, `/remind cancel` (ReminderModule) |
| **Services** | `IReminderService`, `ITimeParsingService`, `ReminderExecutionService` |
| **Database Entities** | `Reminder` |
| **Configuration** | `ReminderOptions` (max reminders per user, min/max advance time) |
| **Key Features** | Time parsing (10m, 2h, tomorrow 3pm, etc.), DM delivery, pagination |
| **Rate Limiting** | Per-user reminder limit enforced |

**Time Format Examples**:
- `10m` - 10 minutes from now
- `2h` - 2 hours from now
- `tomorrow 3pm` - Tomorrow at 3 PM
- `friday 10am` - Next Friday at 10 AM

**Preconditions**: `[RequireGuildActive]`

---

### Rat Watch

Community-driven accountability system where users flag suspicious messages for community vote. Uses voting to determine "guilty" or "not guilty" verdicts.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `Rat Watch` (context menu), `/rat-clear`, `/rat-stats`, `/rat-settings`, `/rat-leaderboard` (RatWatchModule, RatWatchComponentModule) |
| **Services** | `IRatWatchService`, `IRatWatchStatusService`, `IDashboardUpdateService` |
| **UI Pages** | Portal: Rat Watch analytics and leaderboard; Admin: Rat Watch analytics (`RatWatchAnalytics.cshtml`) |
| **Database Entities** | `RatWatch`, `RatRecord`, `RatVote`, `GuildRatWatchSettings` |
| **Configuration** | Timezone support, voting duration, max advance hours, feature enable/disable |
| **Key Features** | Modal-based watch creation, "I'm Here!" check-in button, voting system, record keeping, leaderboard, timezone-aware scheduling |

**Workflow**:
1. User right-clicks message → "Rat Watch"
2. Modal shows with time input and optional message
3. System schedules check-in at specified time
4. Accused can click "I'm Here!" to pre-clear
5. At scheduled time, voting window opens
6. Results tallied and recorded

**Preconditions**: `[RequireGuildActive]`, `[RequireRatWatchEnabled]`

---

### Scheduled Messages

Admin-configurable recurring messages sent to specified channels. Supports cron expressions and multiple frequencies.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/schedule-list`, `/schedule-create`, `/schedule-edit`, `/schedule-delete` (ScheduleModule, ScheduleComponentModule) |
| **Services** | `IScheduledMessageService`, `ScheduledMessageExecutionService`, `IInteractionStateService` |
| **UI Pages** | Admin: Scheduled messages management with CRUD operations |
| **Database Entities** | `ScheduledMessage` |
| **Key Features** | Cron expression support, frequency options (daily, weekly, monthly, custom), enable/disable, pagination |

**Supported Frequencies**: Daily, Weekly, Monthly, Custom (cron expression)

**Preconditions**: `[RequireAdmin]`, `[RequireGuildActive]`

---

### Not-X (Twitter/X Link Previews)

Automatic or manual preview embeds for X/Twitter links, using the fxtwitter API to fetch tweet data. Supports sensitive-only filtering, channel routing, and per-guild configuration.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/notx enable`, `/notx disable`, `/notx status`, `/notx sensitive-only`, `/notx channel set`, `/notx channel clear`, `/notx monitor add`, `/notx monitor remove`, `/notx monitor clear` (NotXCommandModule); `Fetch Tweet` context menu (NotXContextMenuModule) |
| **Handlers** | `NotXMessageHandler` (auto-detects tweet URLs in messages) |
| **Services** | `INotXService`, `FxTwitterClient`, `NotXEmbedBuilder`, `TweetUrlExtractor` |
| **Database Entities** | `NotXGuildSettings` |
| **Configuration** | `NotXOptions` (`NotX` section) |
| **External Services** | fxtwitter API (tweet metadata and media) |
| **Key Features** | Auto-detect tweet URLs, sensitive-only mode, output channel routing, monitored channel filtering, context menu manual fetch, guild-level kill-switch |
| **Rate Limiting** | 5 commands per 60 seconds |

**Architecture Flow**:
```
Tweet URL posted (or context menu) →
  NotXMessageHandler / NotXContextMenuModule →
  TweetUrlExtractor (parse URL) →
  NotXService (settings gate check) →
  FxTwitterClient (fetch tweet data) →
  NotXEmbedBuilder (build Discord embed) →
  Post embed to output channel
```

**Preconditions**: `[RequireGuildActive]`, `[RequireUserPermission(ManageGuild)]` (config commands)

---

### Feature Requests

AI-powered feature request submission with optional multi-step DM conversation for requirements gathering. Includes prompt injection detection and configurable conversation flow.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/feature-request` (FeatureRequestModule, FeatureRequestComponentModule) |
| **Handlers** | `FeatureRequestDmHandler` (multi-step DM conversation) |
| **Services** | `IFeatureRequestService`, `FeatureRequestConversationService`, `InputValidationService`, `PromptInjectionFilter`, `FeatureRequestToolProvider` |
| **Database Entities** | `FeatureRequest`, `FeatureRequestRejection` |
| **Configuration** | `FeatureRequestsOptions` (`FeatureRequests` section) |
| **Key Features** | Direct submit for detailed requests (100+ chars), multi-step DM conversation for brief requests, AI requirements gathering, prompt injection filtering, configurable conversation timeout and turn limits, doc generation integration |
| **Rate Limiting** | 3 per hour |

**Architecture Flow**:
```
User invokes /feature-request →
  FeatureRequestModule (validate input) →
  Short description? → Button: "Refine in DM" or "Submit as-is"
    → DM path: FeatureRequestDmHandler →
      FeatureRequestConversationService (AI conversation) →
      PromptInjectionFilter (safety check) →
      Consolidated summary generated
    → Direct path: Submit immediately
  → FeatureRequestService.SubmitAsync() →
  FeatureRequest entity persisted
```

**Preconditions**: `[RequireGuildActive]`

---

## Administrative Features

### Guild Management

Dashboard for managing guild-wide settings, enabling/disabling features, member management.

| Aspect | Components |
|--------|------------|
| **UI Pages** | Admin: Guild settings, feature flags, member directory |
| **Services** | `IGuildService`, `IUserDiscordGuildService`, `IPermissionService` |
| **Database Entities** | `Guild`, `UserDiscordGuild`, `GuildMember` |
| **Key Features** | Feature toggles, member sync, role-based access control |

---

### Welcome System

Automated welcome messages and member verification upon joining guild.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | Welcome command integration via `WelcomeModule` |
| **Services** | `IWelcomeService` |
| **UI Pages** | Admin: Welcome configuration page |
| **Database Entities** | `WelcomeConfiguration`, `VerificationCode` |
| **Controllers** | `WelcomeController` (API for configuration updates) |
| **Key Features** | Custom welcome messages, member verification codes, on-join automation |

---

### Verification System

Allows users to link their Discord account to their web portal account by running a slash command with a short-lived code generated from the portal UI.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | `/verify-account` (VerifyAccountModule) |
| **Services** | `IVerificationService`, `VerificationCleanupService` |
| **UI Pages** | Account: Link Discord page (`Account/LinkDiscord`) |
| **Database Entities** | `VerificationCode` |
| **Key Features** | 15-minute code TTL, status tracking (Pending/Completed/Expired/Cancelled), IP address capture, automatic cleanup of expired codes |

**Workflow**:
1. User visits Account > Link Discord in the portal; a `VerificationCode` is created with `Status = Pending`
2. Portal displays the 6-character code (e.g., `ABC-123`) and a 15-minute countdown
3. User runs `/verify-account ABC123` in any Discord server the bot is in
4. Bot resolves the code, sets `DiscordUserId` on the `VerificationCode`, links accounts, and sets `Status = Completed`
5. `VerificationCleanupService` periodically purges records with `Status = Expired`

---

### User Management

Administrative interface for user CRUD, role assignment, consent management.

| Aspect | Components |
|--------|------------|
| **UI Pages** | Admin: User list, create, edit, details pages (`Users/Index.cshtml`, `Users/Create.cshtml`, etc.) |
| **Services** | `IUserManagementService`, `IConsentService` |
| **Database Entities** | `ApplicationUser`, `UserConsent` |
| **Controllers** | No dedicated controller; integrated in Razor Pages |
| **Key Features** | User CRUD, Discord OAuth integration, consent tracking |

---

### Audit Logging

Comprehensive audit trail of administrative actions with filtering, search, and export.

| Aspect | Components |
|--------|------------|
| **Discord Commands** | Automatic logging on action |
| **Services** | `IAuditLogService` (with fluent builder API) |
| **UI Pages** | Admin: Audit log viewer with filtering, search, export |
| **Database Entities** | `AuditLog` |
| **Controllers** | `AuditLogsController` (API for querying, filtering, export) |
| **Key Features** | User action attribution, timestamp recording, resource tracking, full-text search, CSV export |

**Fluent Builder Example**:
```csharp
await _auditLogService
  .ForGuild(guildId)
  .WithAction("UserBanned")
  .WithActor(moderatorId)
  .WithTarget(userId, "User")
  .WithReason("Spamming")
  .RecordAsync();
```

---

### Command Logging

Tracks all Discord slash command invocations with parameters, execution time, and outcomes.

| Aspect | Components |
|--------|------------|
| **Services** | `ICommandExecutionLogger`, `MessageLoggingHandler` |
| **UI Pages** | Admin: Command logs viewer with filtering, search, analytics |
| **Database Entities** | `CommandLog` |
| **Controllers** | `CommandLogsController` (API for querying, analytics) |
| **Key Features** | Parameter tracking (redacted for sensitive data), execution time, error recording, per-user stats, per-command stats |

---

### Message Logging

Optional comprehensive message logging for auditing and investigation.

| Aspect | Components |
|--------|------------|
| **Services** | `IMessageLogService`, `MessageLoggingHandler` |
| **UI Pages** | Admin: Message log viewer with search and filtering |
| **Database Entities** | `MessageLog` |
| **Controllers** | `MessagesController` (API for querying) |
| **Key Features** | Message content tracking, edit/delete history, author attribution, searchable content, retention policies |

**Note**: Requires explicit enable; can generate large data volume.

---

## System Features

### Authentication & Authorization

Discord OAuth integration with role-based access control (RBAC).

| Aspect | Components |
|--------|------------|
| **Services** | `IDiscordTokenService`, `DiscordTokenRefreshService`, `DiscordUserInfoService` |
| **Authorization** | `GuildAccessHandler`, `DiscordClaimsTransformation` |
| **Database Entities** | `ApplicationUser`, `DiscordOAuthToken`, `UserGuildAccess` |
| **Policies** | Role hierarchy: SuperAdmin > Admin > Moderator > Viewer |
| **Key Features** | OAuth token refresh, claim-based authorization, guild-level access control |

---

### Notification System

In-app and real-time notifications for important events.

| Aspect | Components |
|--------|------------|
| **Services** | `INotificationService`, `AlertMonitoringService`, `NotificationRetentionService` |
| **UI Pages** | Admin: Notifications inbox |
| **Database Entities** | `UserNotification` |
| **Controllers** | `NotificationsController` (API for querying, marking read) |
| **Real-time** | SignalR for live notification push |
| **Key Features** | Event-driven notifications, user preferences, retention policies, read/unread state |

---

### Performance Monitoring

Real-time monitoring of bot performance, API usage, system health.

| Aspect | Components |
|--------|------------|
| **Services** | `MetricsCollectionService`, `PerformanceMetricsBroadcastService`, `AlertMonitoringService`, `PerformanceAlertService`, `CpuSamplingService` |
| **UI Pages** | Admin: Performance dashboard with multiple tabs (System Health, API Metrics, Commands, Alerts) |
| **Database Entities** | `MetricSnapshot`, `PerformanceIncident`, `PerformanceAlertConfig` |
| **Controllers** | `PerformanceMetricsController`, `PerformanceTabsController`, `AlertsController` |
| **External Services** | Prometheus metrics, Elastic Stack integration |
| **Key Features** | Real-time latency tracking, API call metrics, CPU/memory sampling, alert configuration and threshold management |

---

### Activity Event Tracking

Lightweight, content-free event recording for aggregate analytics and engagement metrics. Designed to operate without requiring user consent because no message content is captured.

| Aspect | Components |
|--------|------------|
| **Handlers** | `ActivityEventTrackingHandler` |
| **Database Entities** | `UserActivityEvent` |
| **Event Types** | Message, Reaction, VoiceJoin, VoiceLeave, GuildJoin, GuildLeave |
| **Key Features** | Consent-free (metadata only, no content), high-volume `long` PK, indexed on `GuildId`/`UserId`/`Timestamp` for analytics queries |

**Notes**:
- `UserActivityEvent` records only: who acted, where, when, and what type of event. No message text or reaction content is stored.
- Distinct from `MessageLog`, which stores full content and requires explicit guild consent to enable.
- `ActivityEventType` enum: `Message`, `Reaction`, `VoiceJoin`, `VoiceLeave`, `GuildJoin`, `GuildLeave`.

---

### Search & Filtering

Full-text search across logs, audit trails, and moderation cases.

| Aspect | Components |
|--------|------------|
| **Services** | `ISearchService` (abstracted for multiple backends) |
| **Backends** | Elasticsearch, SQL full-text search fallback |
| **Controllers** | `AutocompleteController` (search suggestions) |
| **Database Entities** | Various (CommandLog, AuditLog, MessageLog, ModerationCase) |
| **Key Features** | Full-text search, faceted filtering, pagination, result ranking |

---

### Logging & Telemetry

Structured logging with Serilog and optional Seq/Elasticsearch aggregation.

| Aspect | Components |
|--------|------------|
| **Logging** | Serilog (structured logs) |
| **Aggregation** | Seq (optional), Elasticsearch/Kibana (optional) |
| **Tracing** | Jaeger/OpenTelemetry (optional) |
| **Key Services** | `ILogger<T>` dependency injection throughout |
| **Key Features** | Structured logging, correlation IDs, performance profiling, log aggregation |

---

### DM Assistant & Mogwai

Owner-only DM assistant with multi-turn conversation history and optional Claude Code CLI delegation (Mogwai).

| Aspect | Components |
|--------|------------|
| **Discord Entry Point** | `DmAssistantMessageHandler` — handles incoming DM messages, routes to `IDmAssistantService` |
| **Services** | `IDmAssistantService`, `IAgentRunner`, `ILlmClient` (`OpenRouterLlmClient` — OpenAI-compatible chat completions via an owned typed `HttpClient`, no LLM SDK), `IToolRegistry` |
| **Tool Providers** | `ClaudeCodeToolProvider` (Mogwai only — `IDmToolProvider`) |
| **Database Entities** | `DmConversationMessage`, `DmAssistantInteractionLog`, `DmAssistantUsageMetrics` |
| **Configuration** | `DmAssistantOptions` (`DmAssistant` section — `Model` is an OpenRouter slug, default `anthropic/claude-sonnet-4`; the Mogwai compose file overrides it to `anthropic/claude-haiku-4.5`), `OpenRouterOptions` (`OpenRouter` section — API key, base URL, retries), `MogwaiOptions` (`Mogwai` section) |
| **Docker** | `Dockerfile.mogwai`, `docker-compose.mogwai.yml`, `.env.mogwai` |
| **Agent Prompt** | `docs/agents/dm-owner-agent.md` |
| **Key Features** | Owner-only access (Discord API check), sliding-window conversation history, response chunking (split / `.md` attachment), Claude Code subprocess spawning, per-user session continuity, concurrency guard |
| **Access Control** | Owner identified via `GetApplicationInfoAsync()`. Non-owners receive placeholder response. |
| **Mogwai toggle** | `Mogwai__Enabled=false` by default — zero impact on all other bot instances when unset |

**Architecture Flow (Mogwai path)**:
```
Owner DMs Mogwai bot →
  DmAssistantMessageHandler →
  DmAssistantService (history load + LLM call) →
  AgentRunner (agentic loop) →
  Model (Haiku via OpenRouter) decides: answer directly OR call run_claude_code tool →
  ClaudeCodeToolProvider.ExecuteAsync() →
  claude CLI subprocess (--output-format json) →
  JSON response parsed, session ID stored →
  Response chunked and sent via DmAssistantMessageHandler
```

**Preconditions**: Owner check via Discord API. `DmAssistant__Enabled=true` and `Mogwai__Enabled=true` required in configuration.

---

### Background Services

Long-running background tasks for maintenance and scheduled operations.

| Aspect | Components |
|--------|------------|
| **Services** | `BotHostedService` (main bot lifecycle), `ReminderExecutionService`, `ScheduledMessageExecutionService`, `AnalyticsRetentionService`, `InteractionStateCleanupService`, `MessageLogCleanupService`, `AudioCacheCleanupService`, `VerificationCleanupService`, `SoundPlayLogRetentionService` |
| **Monitoring** | `MonitoredBackgroundService` (base class with health checks), `BackgroundServiceHealthRegistry` |
| **Key Features** | Lifecycle management, health monitoring, graceful shutdown |

---

### Configuration & Settings

Application settings management with environment-based configuration.

| Aspect | Components |
|--------|------------|
| **Configuration** | `appsettings.json`, `appsettings.{Environment}.json`, User Secrets |
| **Options Pattern** | `IOptions<T>` dependency injection |
| **Configuration Classes** | `BotConfiguration`, `VoxOptions`, `ReminderOptions`, `DiscordOAuthSettings`, `NotXOptions`, `FeatureRequestsOptions`, `MogwaiOptions`, `DmAssistantOptions`, `OpenRouterOptions` |
| **Key Features** | Environment-specific settings, feature flags, service configuration |

---

## Feature Dependency Graph

```
┌─────────────────────────────────────────────────────────────────┐
│                    Core Infrastructure                          │
│  (Auth, Logging, Config, Background Services, Database)        │
└─────────────────────────────────────────────────────────────────┘
                              ↓
    ┌─────────────────────────┼──────────────────────────────┐
    ↓                         ↓                              ↓
┌─────────────────┐  ┌──────────────────┐  ┌───────────────────────┐
│  Audio Features │  │ Moderation       │  │ Community Features    │
│                 │  │ Features         │  │                       │
│ • Soundboard    │  │ • Actions        │  │ • Reminders           │
│ • VOX/FVOX      │  │ • Notes          │  │ • Rat Watch           │
│ • TTS           │  │ • Tags           │  │ • Scheduled Messages  │
│ • Moderation Log│  │ • Watchlist      │  │ • Not-X               │
│ • User Prefs    │  │ • History        │  │ • Feature Requests    │
└─────────────────┘  │ • Investigation  │  └───────────────────────┘
                     │ • Logging        │
                     └──────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                  Administrative Features                        │
│  (Guild Mgmt, Welcome, Verification, User Mgmt, Audit Logs)    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    System Features                              │
│  (Auth, Notifications, Performance, Activity Tracking, Search) │
└─────────────────────────────────────────────────────────────────┘
```

---

## Service Architecture Layers

### Core Layer (`src/DiscordBot.Core/`)
- **Entities**: Database models (`Sound`, `Reminder`, `RatWatch`, `ModerationCase`, etc.)
- **Interfaces**: Service contracts (`IAudioService`, `IReminderService`, etc.)
- **DTOs**: Data transfer objects for API/Discord interactions
- **Configuration**: Options classes (`VoxOptions`, `ReminderOptions`, etc.)
- **Enums**: Domain enumerations (`AudioFilter`, `RatWatchStatus`, `ModerationAction`, etc.)

### Infrastructure Layer (`src/DiscordBot.Infrastructure/`)
- **DbContext**: Entity Framework Core database context
- **Repositories**: Data access implementations
- **Migrations**: Database schema versioning

### Application Layer (`src/DiscordBot.Bot/`)
- **Commands**: Discord slash/context menu commands (26 modules)
- **Services**: Business logic and orchestration (80+ services)
- **Controllers**: REST API endpoints (30+ controllers)
- **Pages**: Razor Pages for admin UI

---

## Cross-Cutting Concerns

### Rate Limiting

Applied via `[RateLimit(N, seconds)]` attribute on command modules:
- Audio commands: 5 per 10 seconds
- Moderation commands: Configurable per action
- General commands: Mostly unrestricted

### Preconditions

Custom authorization attributes enforce feature availability:
- `[RequireGuildActive]`: Guild must be registered
- `[RequireAudioEnabled]`: Audio features enabled
- `[RequireVoiceChannel]`: User in voice channel
- `[RequireModerationEnabled]`: Guild has moderation enabled
- `[RequireModerator]`: User is moderator+
- `[RequireAdmin]`: User is admin+
- `[RequireTtsEnabled]`: TTS configured

### State Management

- **Discord Interactions**: `IInteractionStateService` for modal/component state
- **Cached Data**: `IMemoryCache` for frequently accessed data
- **Real-time Updates**: SignalR for dashboard notifications

---

## Data Retention

| Entity | Retention | Service |
|--------|-----------|---------|
| CommandLog | 90 days | `MessageLogCleanupService` |
| MessageLog | 365 days | `MessageLogCleanupService` |
| SoundPlayLog | 90 days | `SoundPlayLogRetentionService` |
| UserNotification | 30 days | `NotificationRetentionService` |
| MetricSnapshot | 90 days | `AnalyticsRetentionService` |
| InteractionState | Ephemeral | `InteractionStateCleanupService` |

---

## Future Expansion Points

1. **Assistant Integration**: LLM-powered command suggestions and automation
2. **Advanced Analytics**: Predictive moderation and community health scoring
3. **Custom Commands**: User-defined slash commands
4. **Voice Capabilities**: Advanced voice channel management and voice activity tracking
5. **Integrations**: Third-party service webhooks (GitHub, monitoring systems, etc.)

---

## Quick Navigation by Use Case

### "I want to add a new audio feature"
See: [Audio Features](#audio-features), `src/DiscordBot.Bot/Services/AudioService.cs`, `src/DiscordBot.Bot/Commands/SoundboardModule.cs`

### "I need to understand moderation"
See: [Moderation Features](#moderation-features), `docs/articles/audit-log-system.md`, `docs/requirements/moderation-system.md`

### "How do reminders work?"
See: [Reminders](#reminders), `ReminderModule.cs`, `ReminderService.cs`

### "Where's the performance monitoring?"
See: [Performance Monitoring](#performance-monitoring), Admin dashboard > Performance tab

### "How do I add a new command?"
1. Create module in `src/DiscordBot.Bot/Commands/`
2. Implement slash commands inheriting `InteractionModuleBase<SocketInteractionContext>`
3. Inject required services via constructor
4. Add precondition attributes for authorization
5. Register in `BotHostedService` with `interactionHandler.AddModulesAsync()`

---

## Related Documentation

- [Service Architecture](service-architecture.md)
- [Database Schema](database-schema.md)
- [Authorization Policies](authorization-policies.md)
- [Audit Log System](audit-log-system.md)
- [Testing Guide](testing-guide.md)
- [VOX System Specification](vox-system-spec.md)
- [Soundboard Documentation](soundboard.md)
- [TTS Support](tts-support.md)
