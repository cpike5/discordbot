# Documentation Accuracy Audit

**Date:** 2026-06-24
**Scope:** Published documentation (`docs/articles/*`, `docs/specs/*`), `README.md`, `CLAUDE.md`, `CLAUDE-REFERENCE.md`, `.env.example` — audited against the current `src/` implementation.
**Method:** Ten domain-focused passes, each cross-checking docs against actual command modules, options classes, controllers, hubs, entities, migrations, and services. Only discrepancies confirmed against code (with `file:line` evidence) are listed.

This is an **audit only** — no documentation or code was changed.

---

## How to read this

Findings are bucketed into three kinds:

- **Inaccuracy** — the doc states something that contradicts the code (wrong name, value, path, behavior).
- **Phantom** — the doc describes something that does not exist in code (removed, renamed, or never built).
- **Missing** — something real in code that the docs don't cover.

Severity reflects user impact: **Critical** = a user/operator following the doc will hit a hard failure (command not found, 404, config that does nothing, wrong secret key); **High** = significantly misleading; **Medium/Low** = drift, omissions, cosmetic.

---

## Top priorities (fix these first)

These are the highest-impact, highest-confidence problems across all areas:

1. **Moderation command names are wrong everywhere.** README and `command-configuration.md` document `/mod-history`, `/mod-stats`, `/mod-notes`, `/mod-tag`, `/verify` — the real commands are `/modlog`, `/modstats`, `/modnote`, `/modtag`, `/verify-account`. A user copying these names gets "command not found."
2. **Wrong Anthropic API key config in `ai-assistant.md`.** Docs say `Claude:ApiKey`; the bot only reads `Anthropic:ApiKey`. Following the doc silently disables the assistant.
3. **`environment-configuration.md` documents an almost entirely fictional config surface** — fabricated property names and defaults for ~15 options classes (Anthropic, Assistant, Soundboard, VoiceChannel, AudioCache, Moderation, AutoModeration, AnalyticsRetention, Observability, PerformanceAlerts, Sampling, Notifications, Caching, Identity, Verification). Treat the whole "Additional Configuration Sections" + Quick Reference table as unreliable.
4. **`alerting-system.md`: every seeded alert threshold is wrong** (e.g. gateway latency documented 500/1000 ms, actually 100/200; `api_rate_limit_usage` documented as a count >3600, actually a percentage 85/95).
5. **`log-aggregation.md`: the entire Elasticsearch/Seq config model is fictional.** No `ElasticOptions` class; real keys are `ElasticSearch:Url`, `ElasticSearch:ApiKey`, `Observability:SeqUrl`, using an Elastic data stream `logs-discordbot-{env}` (not the documented `Elastic:*` / `Serilog:WriteTo[n]` keys or `discordbot-logs-{date}` index format).
6. **`scheduled-messages.md`: all cron examples are 5-field but the parser requires 6-field** (`CronFormat.IncludeSeconds`). Every documented cron string throws `CronFormatException`.
7. **`api-endpoints.md` says "Authentication: None (MVP)"** — false. Nearly every controller enforces `[Authorize(Policy=…)]`. Plus the health path is `/health`, not the documented `/api/health`, and ~10 entire controllers are undocumented.
8. **`bot-verification.md` describes the verification flow backwards** — and `consent-privacy.md` says data export is "not implemented" when `/privacy export-data` + the web export ship today.

---

## 1. Commands (README, admin-commands.md, utility-commands.md, permissions.md, command-configuration.md)

### Inaccuracies
- **[Critical]** `/mod-history` → real command is **`/modlog`**. `README.md:267`, `command-configuration.md:46` vs `ModerationHistoryModule.cs:42`.
- **[Critical]** `/mod-stats` → **`/modstats`**. `README.md:268`, `command-configuration.md:46` vs `ModStatsModule.cs:36`. Param is `[moderator]` + `[timeframe]` (24h/7d/30d/all, default 30d), not `[user]`.
- **[Critical]** `/mod-notes add/list/delete` → group is **`/modnote`** (singular), delete subcommand is **`remove`**. `README.md:269`, `command-configuration.md:48` vs `ModNoteModule.cs:17,178`.
- **[Critical]** `/mod-tag` → **`/modtag`**; also has undocumented `create`/`delete` subcommands. `README.md:270` vs `ModTagModule.cs:19,288,387`.
- **[Critical]** `/verify` → **`/verify-account`**. `command-configuration.md:87` vs `VerifyAccountModule.cs:28`. (README is correct here.)
- **[High]** `/remind delete` → subcommand is **`cancel`**. `command-configuration.md:59` vs `ReminderModule.cs:246`. (README correct.)
- **[Medium]** `/welcome setup/test/disable` is wrong — real subcommands are `show/enable/disable/channel/message/test`. `command-configuration.md:36` vs `WelcomeModule.cs`. (README correct.)
- **[Medium]** `/schedule-message create/list/delete/edit` doesn't exist — real commands are five separate `schedule-list/create/delete/toggle/run`. `command-configuration.md:37` vs `ScheduleModule.cs`. (README correct.)
- **[Medium]** `/ban` documents only `<user> [reason]` but also accepts `duration` (temp ban) and `delete_messages`. `README.md:262` vs `ModerationActionModule.cs:475`.
- **[Medium]** `/mute` requires Discord `ModerateMembers` permission, not noted (README notes the equivalent for kick/ban). `README.md:263` vs `ModerationActionModule.cs:730`.
- **[Low]** `/purge` also accepts an optional `user` filter. `README.md:264` vs `ModerationActionModule.cs:875`.

### Missing
- **[High]** Entire **`/notx`** command group undocumented (enable/disable/status/sensitive-only, channel set/clear, monitor add/remove/clear + "Fetch Tweet" context menu). `NotXCommandModule.cs`, `NotXContextMenuModule.cs`.
- **[High]** `/unban` undocumented in README moderation list. `ModerationActionModule.cs:640`.
- **[Medium]** `/feature-request` undocumented. `FeatureRequestModule.cs:46`.
- **[Medium]** `/tts-styled` undocumented in README / tts-support command table. `TtsModule.cs:288`.
- **[Medium]** Moderation history subcommands `/case`, `/reason`, `/modexport` undocumented. `ModerationHistoryModule.cs:176,254,323`.
- **[Medium]** `/privacy export-data` subcommand omitted from README & command-configuration. `PrivacyModule.cs:114`.
- **[Low]** "Warn User" right-click message command undocumented. `ModerationActionModule.cs:38`.

### Phantom
- **[Medium]** `/setup` referenced as a guild-activation command in `utility-commands.md:464` — no such command exists.

---

## 2. AI Assistant (ai-assistant.md, assistant-feature-updates.md, mogwai.md, assistant-tool-catalog.md)

### Inaccuracies
- **[Critical]** Wrong API-key config key: `Claude:ApiKey` documented, real key is **`Anthropic:ApiKey`**. `ai-assistant.md:32,351` vs `AnthropicOptions.cs:19`, `AssistantServiceExtensions.cs:39`.
- **[High]** Stale model IDs: docs say `claude-3-5-sonnet-20241022` / `claude-3-opus-20240229` / `claude-3-haiku-20240307`; code defaults to **`claude-sonnet-4-20250514`** (available: opus-4/sonnet-4/haiku-4 `-20250514`). `ai-assistant.md:223-232` vs `AssistantOptions.cs:91`, `AnthropicOptions.cs:28-31`.
- **[Medium]** mogwai CLI invocation inaccurate: prompt is passed via `--print` (stdin) not `-p "{prompt}"`; `--verbose` is passed; **`--max-budget-usd` is never passed** despite `MogwaiOptions.MaxBudgetUsd` existing (the spend cap is not actually enforced). `mogwai.md:116-126` vs `ClaudeCodeToolProvider.cs:160-198`.

### Phantom (assistant-tool-catalog.md)
- **[High]** Documents providers/tools that don't exist: `list_guild_members`, `PermissionToolProvider` (`check_user_permissions`, `check_oauth_status`), `BotConfigToolProvider`, `ModerationToolProvider` (`get_moderation_log`/`get_muted_users`/`get_audit_log`), `DiagnosticsToolProvider`. Catalog `:469-868` vs actual providers.
- **[Medium]** `get_user_profile` / `get_user_roles` documented params (require `user_id`, take `guild_id`) don't match code (params optional, no `guild_id`). Catalog `:271-403` vs `UserGuildInfoTools.cs`.
- **[Medium]** `AssistantOptions.ToolRateLimits` (per-role tool rate limits) doesn't exist. Catalog `:914-925` vs `AssistantOptions.cs`.
- **[Medium]** `ToolExecutionContext` / `UserRole` enum don't exist; real DTO is `ToolContext`. Catalog `:945-964` vs `ToolContext.cs`.

### Missing
- **[High]** Most registered tool providers undocumented: `RatWatchToolProvider` (available to the **guild** assistant), `FeatureRequestToolProvider`, and the entire DM-assistant provider set (`MemoryToolProvider`, `ConversationToolProvider`, `WebFetchToolProvider`, `CodeExecutionToolProvider`, `DmAnalyticsToolProvider`, `DmModerationToolProvider`, `BotManagementToolProvider`). 26 tools registered, only ~2 providers documented.
- **[Medium]** Entire `DmAssistant` options section (23 properties) undocumented. `DmAssistantOptions.cs`.
- **[Low]** `IDmToolProvider` interface / two-track (guild vs DM) provider model undocumented.

> Note: `dave-implementation.md` is about voice E2EE (MLS/libdave), not the LLM assistant — no assistant content to audit there.

---

## 3. Audio & Voice (tts-support.md, ssml-support.md, soundboard.md, unified-now-playing.md, voice-capability-system.md, vox-*.md, voice-favorites/selector-spec.md)

### Inaccuracies / Phantom
- **[Critical]** `ssml-support.md` API routes use wrong prefix `/api/portal-tts/…` — real routes are `/api/portal/tts/…` (slash, not hyphen). Every such route (`:312,353,388,435,612`) 404s. `PortalTtsController.cs`.
- **[High]** `ssml-support.md:612` documents `GET /api/portal-tts/voices` — no such list endpoint exists.
- **[High]** `ssml-support.md` API examples use inverted preset IDs (`cheerful-jenny`/`newscast-guy`); real IDs are `jenny-cheerful`/`guy-newscast`. `:366-374` vs `StylePresetProvider.cs:28,98`. (The doc's own preset tables are correct — the API section contradicts them.)
- **[High]** `voice-favorites-spec.md` documents an **unbuilt** feature (no `VoiceFavorites` module, no localStorage key, no UI) with no "not implemented" banner — reads as shipped.
- **[High]** `voice-capability-system.md` is a completed plan presented as pending work ("expand registry from 4 to 34 voices") — all 34 are already registered (`VoiceCapabilityProvider.cs`). Reframe as shipped/archive.
- **[Medium]** `tts-support.md:516` claims Azure TTS output is WAV transcoded by FFmpeg — TTS emits PCM directly; no FFmpeg in the TTS path. `AzureTtsService.cs`.
- **[Medium]** `ssml-support.md` Style Compatibility Matrix contradicts the shipped capability registry (Aria styles wrong; matrix lists 4 voices, code ships 6 en-US + 20+ others). `:128-144` vs `VoiceCapabilityProvider.cs`.
- **[Medium]** `soundboard.md` per-guild defaults wrong (doc implies 10MB/100/500MB/auto-leave 0; `GuildAudioSettings.cs` = 5MB/50/100MB/5min). Portal play/join/leave/status response shapes (`status`, `userCount`, `position/duration/queueLength`) are phantom; upload returns 201 not 200, `name` is required; `m4a` listed but unsupported (`SupportedFormats=[mp3,wav,ogg]`).
- **[Medium]** `unified-now-playing.md` Portal Usage Matrix + all three PageModel examples claim Soundboard uses `IsCompact=false`/`ShowProgress=true` with a progress bar; code sets `IsCompact=true`/`ShowProgress=false`. SSR examples call `GetCurrentlyPlayingAsync`/`_queueService.GetQueueAsync` — neither exists.
- **[Low]** `ssml-support.md:307` says portal-tts endpoints require `ModeratorAccess`; actual policy is `PortalGuildMember`.

### Missing
- **[Medium]** `ITtsService` doc shows 3 of 6 interface members (omits second `SynthesizeSpeechAsync` overload, `ValidateSsml`, `GetCuratedVoices`). `tts-support.md:431` vs `ITtsService.cs`.
- **[Medium]** Soundboard sound-categories and user-favorites API surfaces undocumented.
- **[Medium]** VOX history/favorites (`VoxMessageHistory` + 5 endpoints) ship but are listed under "Future Expansion"; portal-source telemetry documented but unbuilt.
- **[Low]** `AzureSpeechSsmlOptions` config section undocumented.

> `audio-dependencies.md`, `vox-system-spec.md` core, and `voice-selector-spec.md` are largely accurate (minor: VOX tokenization maps `,`/`.` rather than stripping punctuation; voice counts cite 32/37 where actual is 34).

---

## 4. Scheduling & Notifications (reminder-system.md, scheduled-messages.md, notification-system.md, timezone-handling.md)

### Inaccuracies
- **[Critical]** `scheduled-messages.md` cron syntax: parser uses `CronFormat.IncludeSeconds` (6-field, seconds-first); every documented 5-field example (`0 9 * * 1-5`, etc.) throws. `:90,232-240` vs `ScheduledMessageService.cs:544,596`.
- **[Critical]** `reminder-system.md` repeatedly claims reminder times parse in the guild's timezone; `ReminderModule.cs:83` hardcodes `"UTC"`. Guild-timezone behavior does not exist.
- **[High]** Reminder Admin UI route documented `/Guilds/{guildId}/Reminders`; actual is `/Guilds/Reminders/{guildId}`. `reminder-system.md:401` vs `Pages/Guilds/Reminders/Index.cshtml`.
- **[Medium]** `12/31 3pm` (numeric month, no year) documented as supported — parser requires alphabetic month or full slash-date+year. `reminder-system.md:211,295` vs `TimeParsingService.cs:317`.
- **[Medium]** Claims full date-time uses `DateTime.TryParse()` with server culture — actually hand-rolled regexes, culture-independent. `reminder-system.md:317-322`.
- **[Medium]** `notification-system.md:209` cites `AlertMonitoringService.cs:545-580` for the PerformanceAlert call — it's in `AlertIncidentManager.cs:190`. Other cited line numbers (`BotHostedService`, `InteractionHandler`) are stale by 15-50 lines.

### Phantom
- **[High]** `scheduled-messages.md:852-873` says "no dedicated REST API endpoints (future)" — a full CRUD controller exists at `ScheduledMessagesController.cs` (`/api/guilds/{guildId}/scheduled-messages`, incl. `validate-cron`).
- **[High]** `ReminderOptions.ExecutionTimeoutSeconds` documented (`:441,455,493`) — doesn't exist on `ReminderOptions.cs`.
- **[High]** `timezone-handling.md` form example uses `ScheduledMessage.ScheduledAt`/`IsActive` and `GetActiveMessagesAsync()` — real fields are `NextExecutionAt`/`IsEnabled`; no such method.

### Missing
- **[Medium]** SignalR notifier services (`PerformanceNotifier`, `AudioNotifier`, `DashboardNotifier`, `DashboardUpdateService`) absent from notification-system.md.
- **[Medium]** `TimeParsingService` keywords (`noon`/`midnight`/`morning`/…, `today`, abbreviated weekdays) missing from the format reference (yet `Dec 31 noon` is used in an example).

> Notification types/options, timezone helper signatures, and scheduled-message entity/options are otherwise accurate. Client/server inconsistency worth noting: `TimezoneHelper.GetTimezoneAbbreviation` (C#) returns full names while `timezone.js` returns true abbreviations.

---

## 5. Identity, Auth & Privacy (identity-configuration.md, authorization-policies.md, user-management.md, consent-privacy.md, bot-verification.md)

### Inaccuracies
- **[Critical]** `bot-verification.md` describes the flow backwards: doc says generate code in web UI then enter via `/verify-account code:…`; reality is run `/verify-account` (no params) in Discord to generate the code, then enter it on the profile page. `:62-116` vs `VerifyAccountModule.cs:28`, `IVerificationService`.
- **[High]** Verification config wrong: doc uses section `Discord:AccountVerification` with keys `CodeExpiryMinutes`/`RateLimitCodesPerHour`/`BackgroundCleanupIntervalMinutes`; real section is `Verification` with `CodeCharset`/`CodeLength`/`CodeExpiryMinutes`/`MaxCodesPerHour`/`OldCodeCleanupHours`. `:594-617` vs `VerificationOptions.cs`.
- **[High]** Verification entities fabricated: doc cites `DiscordAccountVerification` + `DiscordVerificationRateLimit` + `DiscordVerificationCleanupService`; real are `VerificationCode` (plaintext `Code`, not hashed) + `VerificationCleanupService` (rate limiting is a `[RateLimit]` attribute, no table).
- **[High]** Cookie settings wrong: doc says `Always`/`Strict`/24h; code uses `SameAsRequest`/`Lax`/`CookieExpireDays` (default 7). `identity-configuration.md:347-366` vs `IdentityServiceExtensions.cs:76-86`. (`Lax` is deliberate for the OAuth redirect.)
- **[High]** `authorization-policies.md` misdescribes guild auth: claims `GuildAccessHandler` queries `UserGuildAccess` and compares access levels; the DI-registered `GuildAccessHandler` checks **live Discord membership** and requires Discord `Administrator` for the Admin role — it never reads `UserGuildAccess`. `:225-260,778-1010` vs `GuildAccessHandler.cs`.
- **[Medium]** `RequiredUniqueChars` default documented 4; actual 1 (the "invalid" example `AAAAA1!a` would pass). `identity-configuration.md:254-290` vs `IdentityConfigOptions.cs`.
- **[Medium]** No hardcoded default admin fallback (`admin@example.com`/`Admin@123456`) exists; if `Identity:DefaultAdmin` is unset, no admin is seeded. `identity-configuration.md:597-642` vs `IdentitySeeder.cs:73-80`.
- **[Medium]** `IdentitySeeder` is in `Extensions/` (method `SeedIdentityAsync`), not `Data/IdentitySeeder.cs` (`SeedAsync`).
- **[Medium]** OAuth scopes: code requests `identify`, **`email`**, `guilds`; doc omits `email` and lists `guilds.members.read` (not requested). `DiscordOAuthOptions.Scopes` is never read (scopes hardcoded).
- **[Medium]** `user-management.md:191` says min password length 6; actual 8 (contradicts identity-configuration.md too).
- **[Low]** Role "Level" numbering is opposite between identity-configuration.md and authorization-policies.md; code has no numeric levels. Policies use `IdentitySeeder.Roles`, not the cited `Core/Authorization/Roles.cs`.

### Phantom
- **[High]** `consent-privacy.md:721-742` says Data Export is "Not implemented (planned)" — it ships: `/privacy export-data` + web "Export My Data" → `UserDataExportService` (ZIP, 7-day link).
- **[Medium]** `UserGuildAccess`/`GuildAccessLevel`/`MinimumLevel` presented as the live authorization mechanism; the entity exists but no handler consumes it.
- **[Low]** `GuildAdmin` policy (`authorization-policies.md:880`) not registered. `<authorize negate="true">` tag helper (`identity-configuration.md:208`) unsupported (only `policy`/`roles`). `if-role` attribute helper documented but doesn't exist.

### Missing
- **[Medium]** `PortalGuildMember` policy + requirement/handler undocumented.
- **[Medium]** `ConsentType.AssistantUsage` (the real 2nd consent type) undocumented; doc's planned `Analytics`/`LLMInteraction`/`PersonalizedFeatures` don't exist in the enum.

---

## 6. Data & Infrastructure (database-schema.md, repository-pattern.md, background-services.md, search.md, service-architecture.md, audit-log-system.md, message-logging.md)

### Inaccuracies
- **[High]** `search.md` is stale: describes a monolithic 919-line `SearchService`; reality is a 206-line orchestrator + 9 `ISearchProvider` implementations. `:74,794` vs `SearchService.cs` + `Services/Search/`.
- **[Medium]** `search.md` `SearchCategory` enum order wrong (`CommandLogs`/`Users`/`Commands` positions differ → wrong int values). `:88-101` vs `SearchCategory.cs`.
- **[Medium]** `background-services.md` wrong retention defaults/intervals: MessageLog shown 30 days/60 min, actual 90 days/24 h; SoundPlayLog 30→90 days; RatWatch interval 5 min→30 s.
- **[Medium]** `background-services.md` wrong config keys: `ExecutionIntervalSeconds`→`CheckIntervalSeconds`; Reminder section `"Reminders"`→`"Reminder"`.
- **[Low]** Services inherit `MonitoredBackgroundService` (which wires the health registry), not `BackgroundService` as stated. `:10`.
- **[Low]** `audit-log-system.md` cites audit service paths without the `Services/Audit/` subfolder (all files are under `Audit/`).
- **[Low]** `message-logging.md` says `MessageLog.ChannelName` is "not stored in database" — it is stored and mapped (`MessageLogService.cs:374`).

### Phantom
- **[High]** `service-architecture.md:99-119` documents a non-existent `IAuditLogBuilder` API (`.Action()/.WithChange()/.WithMetadata()/.SaveAsync()`); real API is `.ForCategory()/.WithAction()/…/.LogAsync()/.Enqueue()`. Contradicts audit-log-system.md (which is correct).
- **[Medium]** `background-services.md` phantom config keys across MessageLogRetention, AuditLogRetention, PerformanceAlerts, Reminders, ScheduledMessages, RatWatch, VoiceChannel (see config audit §8 for the same classes).
- **[Low]** `message-logging.md:970` queries a `Settings` table; real table is `ApplicationSettings`.

### Missing
- **[High]** `database-schema.md` omits 16 real entities/tables (e.g. `SoundCategory`, `DiscordOAuthToken`, `ConnectionEvent`, `DmConversationMessage`, `DmAssistantNote`, `VoxMessageHistory`, `TtsMessageHistory`, `UserTtsPreset`, `AudioPlaybackLog`, `UserPreference`, `FeatureRequest`, `NotXGuildSettings`, `UserSoundFavorite`, …). 61 entities exist; 46 documented. Also omits `MessageLogs.ChannelName` column.
- **[High]** `background-services.md` omits ~10 hosted services (`DiscordTokenRefreshService`, `CpuSamplingService`, `MemberSyncService`, `VoxClipLibraryInitializer`, `CommandPerformanceAggregator`, `ElasticApmFilterRegistrationService`, …).
- **[Low]** `audit-log-system.md` omits `AuditLogAction` 20-22 (`UserDataPurged`, `BulkDataPurged`, `UserDataExported`).

> `repository-pattern.md` and the core of `audit-log-system.md` are accurate.

---

## 7. Web Portal — REST API / SignalR / Components (api-endpoints.md, signalr-realtime.md, interactive-components.md)

### Inaccuracies
- **[High]** `api-endpoints.md:13` "Authentication: None (MVP)" is false — nearly all controllers enforce `[Authorize(Policy=…)]` (RequireViewer/Moderator/Admin/SuperAdmin/PortalGuildMember). The doc even contradicts itself with per-section auth notes.
- **[High]** Health endpoint documented `GET /api/health`; actual route is `/health`. `:21,125` vs `HealthCheckExtensions.cs:41`.
- **[Medium]** Commands API documented as `RequireModerator`; actual `RequireViewer`. `:2324` vs `CommandsApiController.cs:14`.

### Phantom
- **[High]** SignalR `GuildUpdated` event documented (`signalr-realtime.md:172`) — doesn't exist; real guild event is `GuildActivity`.
- **[Low]** `help:select` component handler (`interactive-components.md:43`) — no `help` handler exists (illustrative).

### Missing
- **[High]** ~10 entire controllers undocumented: `AnalyticsController` (18 endpoints), `AudioController`, `NotificationsController`, `PerformanceTabsController`, `PreviewController`, `PortalSoundboardController`, `PortalTtsController`, `UserPreferencesController`, `BulkPurgeController`, `SoundsController`.
- **[High]** SignalR notification hub methods (5: `GetNotificationSummary`/`GetNotifications`/`MarkNotificationRead`/`MarkAllNotificationsRead`/`DismissNotification`) and bulk-purge group methods undocumented; `GuildActivity`/`StatsUpdated` events undocumented.
- **[Medium]** Individual undocumented endpoints within documented controllers (e.g. `GET /api/bot/dashboard-stats`, `GET /api/metrics/api/latency`, CommandLogs analytics sub-endpoints, VOX history/favorites).

> `interactive-components.md` is accurate (ID format, expiry, cleanup all verified). Coverage note: `api-endpoints.md` is ~7,100 lines — endpoint existence/routes/verbs were verified for all 31 controllers; field-level request/response drift was not exhaustively checked.

---

## 8. Configuration & Deployment (README, configuration-guide.md, environment-configuration.md, docker-deployment.md, linux-deployment.md, CLAUDE-REFERENCE.md, .env.example)

### Inaccuracies
- **[Critical]** `environment-configuration.md` documents fabricated property names/defaults for ~15 options classes (Anthropic, Assistant, Soundboard, VoiceChannel, AudioCache, Moderation, AutoModeration, AnalyticsRetention, Observability, PerformanceAlerts, PerformanceBroadcast, Sampling, PerformanceMetrics, Notification, Caching, Identity, Verification). Section keys also wrong (`Reminders`→`Reminder`, `IdentityConfig`→`Identity`, `Notifications`→`Notification`, `DatabaseOptions`→`DatabaseSettings`). The entire "Additional Configuration Sections" + Quick Reference table is unreliable.
- **[Critical]** README Dependencies: Anthropic package is **`Anthropic` v12.2.0**, not "Anthropic.SDK 5.8.0". `README.md:645` vs `DiscordBot.Infrastructure.csproj:14`.
- **[High]** `docker-deployment.md` Seq UI port is 7301 (doc says 5341); sounds mount is `:rw` in `docker-compose.yml` (doc says `:ro`); troubleshooting health check uses `/api/health/live` (real endpoint is `/health`). `:109,217,429`.
- **[Medium]** README Discord.Net listed `3.19.0-beta.1`; actual is the forked split packages `Discord.Net.*` at `3.19.0-fork`. `README.md:639`, also `CLAUDE-REFERENCE.md:189`.
- **[Medium]** README version badge `v1.0.0`; `Directory.Build.props` is `1.5.1-dev`.
- **[Medium]** README appsettings line-number citations (AzureSpeech, Soundboard, OpenTelemetry sections) are stale.
- **[Medium]** README OpusDotNet package id is `OpusDotNet.opus.win-x64` (Windows-native).

### Phantom / Missing
- **[High]** `Elastic:ApiKey` documented as a secret (`README.md:166,606`; `configuration-guide.md:113`) but not bound by any options class or appsettings (the Serilog sink reads `ElasticSearch:ApiKey` — see §9).
- **[Low]** Real config sections missing from environment-configuration.md Quick Reference: `Mogwai`, `DmAssistant`, `FeatureRequests`, `NotX`, `LogSanitization`, `Vox`, `AzureSpeech:Ssml`, `OpenTelemetry`. Discord keys `DefaultRateLimitInvokes`/`DefaultRateLimitPeriodSeconds`/`AdditionalOwnerIds` absent from README config reference.

> **Accurate:** `CLAUDE.md`, `CLAUDE-REFERENCE.md` config table, `configuration-guide.md`, `linux-deployment.md`, `.env.example`, and the README Quick Start/Project Structure/Production Deployment sections all check out. The single highest-impact fix is **`environment-configuration.md`** — consider regenerating it from the actual options classes.

---

## 9. Observability (metrics.md, tracing.md, log-aggregation.md, alerting-system.md, elastic-apm.md)

### Inaccuracies
- **[Critical]** `alerting-system.md` default thresholds wrong for every metric (seeded values in `AddPerformanceAlerts.cs:97-104`): gateway_latency 500/1000→**100/200**; command_p95 1000/1500→**300/500**; error_rate 3/5→**1/5**; memory 400/512→**400/480**; `api_rate_limit_usage` documented count >3600 but seeded as **% (85/95)**; bot_disconnected/service_failure documented 1.0/1.0 but seeded null/1.0. `:268-277,1126-1135`.
- **[Critical]** `log-aggregation.md` Elasticsearch sink config is fictional: no `ElasticOptions` class; real keys `ElasticSearch:Url`/`ElasticSearch:ApiKey`, data stream `logs-discordbot-{env}` (not `Elastic:*`, `Serilog:WriteTo[n]`, or `discordbot-logs-{date}`). Seq uses `Observability:SeqUrl`, not `Serilog:WriteTo[n]:Args:serverUrl`. `:239-573` vs `Program.cs:57-104`.
- **[High]** `metrics.md:512` `discordbot.business.feature.usage` unit `{usages}`; actual `{events}`. `BusinessMetrics.cs:42`.
- **[Medium]** `discordbot.users.unique` description mismatch (doc "sum of guild member counts" vs registered "Estimated unique users in the last 24 hours"). `BotMetrics.cs:70`.
- **[Medium]** `alerting-system.md:478` references `DuplicateSuppressionMinutes` on `PerformanceAlertOptions` — doesn't exist (the snippet wouldn't compile).
- **[Low]** `tracing.md:140` claims user-error commands set span status `Ok`; `BotActivitySource.cs` always sets `Error` on exception (no user/system discrimination).

### Phantom / Missing
- **[Medium]** `metrics.md:913-936` `OpenTelemetry:Metrics` toggles (`Enabled`/`IncludeRuntimeMetrics`/`IncludeHttpMetrics`) have no effect — instrumentation is added unconditionally; only `OpenTelemetry:ServiceName` is read.
- **[Medium]** Entire `DiscordBot.Vox` meter (10 instruments + 5 histogram views) undocumented in metrics.md despite it claiming to be the comprehensive reference. `VoxMetrics.cs`.

> Metric/instrument names & units (BotMetrics/ApiMetrics/SloMetrics), histogram buckets, tracing source/span/attribute names, `/metrics` endpoint, Elastic APM config, and PerformanceAlertOptions defaults are otherwise accurate.

---

## 10. Moderation, Rat Watch, Welcome, Member Directory (rat-watch.md, welcome-system.md, member-directory.md, README sections)

(Command-name issues are consolidated in §1.)

### Inaccuracies / Phantom
- **[High]** `/welcome test [user]` param is phantom — `TestAsync()` takes no params, always tests the invoker. `README.md:349`, `welcome-system.md:299` vs `WelcomeModule.cs:422`.
- **[Medium]** `member-directory.md` CSV columns wrong: omits `Discriminator`, wrong column order, and uses two semicolon-delimited `RoleIds`/`RoleNames` columns (not one pipe-delimited `Roles`). `:257-271` vs `GuildMemberService.cs:261-277`.
- **[Medium]** Member cache config wrong: doc `CachingOptions.MemberCacheDuration` default 15 min; real `GuildMemberListDurationMinutes` default 5. `:283-286` vs `CachingOptions.cs:49`.
- **[Medium]** `member-directory.md:241` documents a page-level export route `GET /Guilds/{guildId}/Members/Export` with no handler (only the API endpoint exists).
- **[Low]** Member-directory page-size options omit `10`; `selectAll()` JS function doesn't exist (event-listener wired); welcome default template uses `{memberCount}` camelCase vs documented `{membercount}`.

> **Accurate:** Rat Watch (commands, options, voting/execution, leaderboard, component IDs), welcome system (subcommands, placeholders, config, embed validation, rate limit), and member-directory routes/policies/filters are otherwise correct.

---

## Cross-cutting observations

- **Specs vs reference docs.** Several files under `docs/articles/` are really design specs (`voice-favorites-spec.md`, parts of `vox-*-spec.md`, `voice-capability-system.md`). When a spec ships, it should be relabeled or moved; when it's unbuilt, it needs a clear "Proposed / Not Implemented" banner. Multiple audits flagged specs being read as shipped behavior.
- **Config docs should be generated, not hand-written.** The fabrication in `environment-configuration.md` (and the phantom keys in `background-services.md`) suggests these were written from memory. A small generator that reflects over the `*Options` classes would keep them honest. `configuration-guide.md` and `CLAUDE-REFERENCE.md` — which track real classes — were accurate.
- **Line-number citations rot fast.** Many docs cite exact `file:line`; a large fraction are now stale. Prefer symbol/route references over line numbers where possible.
- **The agent definition in `.claude/agents/` for data-infrastructure is itself stale** ("72 DbSets" / "919-line SearchService" / wrong `IAuditLogBuilder` API). Per the project's agent-maintenance rule, it should be refreshed alongside these docs.
