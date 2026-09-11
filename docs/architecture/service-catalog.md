# Service Catalog

Quick reference catalog of all services in the Discord bot system. Organized by domain area for easy discovery and understanding of service relationships.

**Last Updated**: April 2026
**Codebase Version**: v1.5.1-dev

---

## Table of Contents

- [Audio & Voice Services](#audio--voice-services)
- [Soundboard & Audio Playback](#soundboard--audio-playback)
- [VOX System](#vox-system)
- [Text-to-Speech Services](#text-to-speech-services)
- [Audio Moderation & User Preferences](#audio-moderation--user-preferences)
- [Not-X (Tweet Previews)](#not-x-tweet-previews)
- [Feature Requests](#feature-requests)
- [Discord Integration Services](#discord-integration-services)
- [User & Guild Management](#user--guild-management)
- [Moderation & Enforcement](#moderation--enforcement)
- [Rat Watch System](#rat-watch-system)
- [Virtual Currency](#virtual-currency)
- [Analytics & Metrics](#analytics--metrics)
- [Performance Monitoring](#performance-monitoring)
- [Background Services](#background-services)
- [Notification & Alerting](#notification--alerting)
- [Data & Repository Services](#data--repository-services)
- [AI Assistant & Tools](#ai-assistant--tools)
- [Configuration & Settings](#configuration--settings)
- [Base Classes](#base-classes)
- [Helpers & Utilities](#helpers--utilities)
- [Utility & Support](#utility--support)

---

## Audio & Voice Services

Services handling voice channel connection, audio streaming, and voice state management.

| Service | Location | Purpose |
|---------|----------|---------|
| `IAudioService` | Core Interfaces | Voice channel connection and disconnection management with per-guild locking |
| `AudioService` | Bot/Services | Manages Discord audio connections, maintains thread-safe state using semaphore locks. Leaves via `IVoiceChannel.DisconnectAsync` (never `IAudioClient.StopAsync` alone) and reconciles tracked state against the bot's gateway voice state |
| `IAudioNotifier` | Core Interfaces | Broadcasts audio state changes via SignalR to connected dashboards |
| `AudioNotifier` | Bot/Services | SignalR hub adapter for audio event notifications |
| `VoiceAutoLeaveService` | Bot/Services | Background service that auto-disconnects bot from voice channels after inactivity |

---

## Soundboard & Audio Playback

Services for managing sound effects, playback queuing, and audio file caching.

| Service | Location | Purpose |
|----------|----------|---------|
| `ISoundService` | Core Interfaces | High-level sound management (CRUD operations, metadata) |
| `SoundService` | Bot/Services | Implements sound entity management and Discord snowflake conversion |
| `IPlaybackService` | Bot Interfaces | Sound playback orchestration with queue/replace modes |
| `PlaybackService` | Bot/Services | Orchestrates audio playback; delegates streaming to IAudioStreamer and transcoding to IFfmpegTranscoder (~500 lines) |
| `IAudioStreamer` | Bot Interfaces | Audio streaming protocol and implementation |
| `AudioStreamer` | Bot/Services | Handles audio data streaming to Discord voice channels |
| `IFfmpegTranscoder` | Bot Interfaces | FFmpeg transcoding operations |
| `FfmpegTranscoder` | Bot/Services | FFmpeg-based audio format conversion and processing |
| `ISoundCacheService` | Core Interfaces | Caches processed audio blobs to reduce FFmpeg reprocessing |
| `SoundCacheService` | Bot/Services | Memory-based audio cache with file I/O fallback and LRU eviction |
| `ISoundFileService` | Core Interfaces | File system operations (upload, retrieval, validation) |
| `SoundFileService` | Bot/Services | Physical sound file handling with size validation |
| `SoundPlayLogRetentionService` | Bot/Services | Background service purging old play log records |
| `SoundboardOrchestrationService` | Bot/Services | High-level soundboard command coordination |
| `ISoundboardOrchestrationService` | Core Interfaces | Orchestrates soundboard feature workflows |

---

## VOX System

Half-Life style concatenated clip announcements (VOX, FVOX, HGRUNT).

| Service | Location | Purpose |
|---------|----------|---------|
| `IVoxService` | Core Interfaces | High-level VOX playback orchestration |
| `VoxService` | Bot/Services | Coordinates tokenization → clip lookup → concatenation → playback |
| `IVoxClipLibrary` | Core Interfaces | Inventory of available VOX clips, scanned at startup |
| `VoxClipLibraryInitializer` | Bot/Services | Initializes clip library by scanning VOX directory |
| `IVoxConcatenationService` | Core Interfaces | FFmpeg-based audio concatenation with configurable silence gaps |

---

## Text-to-Speech Services

Services for Azure Cognitive Services TTS and SSML generation.

| Service | Location | Purpose |
|---------|----------|---------|
| `ITtsService` | Core Interfaces | Text-to-speech conversion using Azure Cognitive Services |
| `AzureTtsService` | Bot/Services | Azure TTS implementation with caching and error handling |
| `ITtsPlaybackService` | Bot Interfaces | TTS playback orchestration: PCM streaming, duration calculation, history logging, and observability |
| `ITtsSettingsService` | Core Interfaces | Guild-level TTS configuration (voices, styles, rates) |
| `ITtsHistoryService` | Core Interfaces | Tracks TTS message playback history |
| `IStylePresetProvider` | Core Interfaces | Provides predefined TTS voice style presets (Emotional, Professional, Character, Assistant categories) |
| `StylePresetProvider` | Bot/Services | Singleton implementation with 12 built-in presets across 4 categories; supports lookup by ID or category |
| `ISsmlBuilder` | Core Interfaces | SSML markup generation for advanced TTS features |
| `ISsmlValidator` | Core Interfaces | SSML markup validation |

---

## Audio Moderation & User Preferences

Services for cross-feature audio playback logging and per-user preference storage.

| Service | Location | Purpose |
|---------|----------|---------|
| `IAudioModerationLogService` | Core Interfaces | Fire-and-forget audio playback event logging |
| `AudioModerationLogService` | Bot/Services | Logs playback events via `IBackgroundTaskRunner`; truncates content names to 200 chars |
| `IAudioPlaybackLogRepository` | Core Interfaces | Data access for `AudioPlaybackLog` entities |
| `AudioPlaybackLogRepository` | Infrastructure/Data/Repositories | EF Core repository for audio playback log persistence |
| `UserPreferencesController` | Bot/Controllers | REST API (`api/portal/preferences/{guildId}`) for per-user, per-guild preference CRUD |

---

## Not-X (Tweet Previews)

Services for automatic and manual X/Twitter link preview embeds via the fxtwitter API.

| Service | Location | Purpose |
|---------|----------|---------|
| `INotXService` | Core Interfaces | Orchestrates tweet processing: settings gate, fetch, embed, post |
| `NotXService` | Bot/Services/NotX | Implements tweet processing pipeline with guild settings checks; refuses to post when `NotX:Enabled` or `Features:NotXEnabled` is false, including for manual context-menu fetches |
| `IFxTwitterClient` | Core Interfaces | HTTP client contract for fxtwitter API calls |
| `FxTwitterClient` | Bot/Services/NotX | Fetches tweet metadata and media from fxtwitter API with configurable timeout and response size limits |
| `NotXEmbedBuilder` | Bot/Services/NotX | Static helper building Discord embeds from tweet data |
| `TweetUrlExtractor` | Bot/Services/NotX | Static helper parsing X/Twitter URLs from message content |
| `NotXMessageHandler` | Bot/Handlers | Auto-detects tweet URLs in incoming messages and routes to `INotXService`; ignores all messages when `NotX:Enabled` is false |

---

## Feature Requests

Services for AI-powered feature request submission with multi-step DM conversation and safety filtering.

| Service | Location | Purpose |
|---------|----------|---------|
| `IFeatureRequestService` | Core Interfaces | Feature request CRUD, status updates, doc-gen result tracking |
| `FeatureRequestService` | Infrastructure/Services | EF Core implementation of feature request persistence |
| `FeatureRequestConversationService` | Bot/Services/FeatureRequests | Multi-turn DM conversation orchestrator for AI requirements gathering |
| `IInputValidationService` | Core Interfaces | Input validation contract for feature request text |
| `InputValidationService` | Bot/Services/FeatureRequests | Validates description length and content constraints |
| `PromptInjectionFilter` | Bot/Services/FeatureRequests | Regex-based prompt injection detection using configurable patterns from `FeatureRequestsOptions` |
| `FeatureRequestToolProvider` | Infrastructure/Services/FeatureRequests | `IDmToolProvider` (Infrastructure/Abstractions/LLM) implementation exposing feature-request tools to the AI agent |
| `FeatureRequestDmHandler` | Bot/Handlers | Handles DM messages during active feature request conversations |

---

## Discord Integration Services

Services for Discord client operations, authentication, and token management.

| Service | Location | Purpose |
|---------|----------|---------|
| `IBotService` | Core Interfaces | High-level bot operations (status updates, shutdown) |
| `BotService` | Bot/Services | Bot control plane operations |
| `IBotStatusService` | Core Interfaces | Manages bot presence/status with priority-based sources |
| `BotStatusService` | Bot/Services | Tracks bot status from multiple sources (Voice, RatWatch, etc.) |
| `IDiscordTokenService` | Core Interfaces | OAuth token validation and refresh |
| `DiscordTokenService` | Bot/Services | Discord OAuth2 token handling |
| `DiscordTokenRefreshService` | Bot/Services | Background service for periodic OAuth token refresh |
| `IDiscordUserInfoService` | Core Interfaces | Fetch Discord user metadata (avatar, status, etc.) |
| `DiscordUserInfoService` | Bot/Services | Retrieves user info from Discord API with caching |
| `IDiscordChannelResolver` | Bot Interfaces | Resolves Discord channel names to IChannel objects |
| `DiscordChannelResolver` | Bot/Services/DiscordIntegration | Channel name resolution with caching (used by 8+ pages/services) |
| `IDiscordUserResolver` | Bot Interfaces | Resolves Discord user info with caching |
| `DiscordUserResolver` | Bot/Services/DiscordIntegration | Discord user info resolution with IMemoryCache (used by ModerationService, WatchlistService) |
| `DiscordClientMemoryReporter` | Bot/Services | Reports Discord client memory usage to diagnostics |
| `DiscordMessageAdapter` | Bot/Services | Adapts Discord.Net IMessage to internal IDiscordMessage interface |

---

## User & Guild Management

Services for user lifecycle, guild membership, and user data operations.

| Service | Location | Purpose |
|---------|----------|---------|
| `IUserManagementService` | Core Interfaces | User creation, updates, and deletion (999 lines) |
| `UserManagementService` | Bot/Services | Full user lifecycle management with audit logging |
| `IGuildMembershipService` | Core Interfaces | Guild membership tracking and sync |
| `IGuildMemberService` | Core Interfaces | Individual member details and operations |
| `IUserDiscordGuildService` | Core Interfaces | Cross-mapping of user/guild relationships |
| `UserDiscordGuildService` | Bot/Services | Manages user presence across multiple Discord guilds |
| `IGuildService` | Core Interfaces | Guild-level operations and metadata |
| `MemberSyncService` | Bot/Services | Background service: full guild member sync on startup + daily reconciliation |
| `MemberSyncQueue` | Bot/Services | Queues pending member sync operations |
| `MemberEventHandler` | (Handler) | Reacts to member join/leave/update events |
| `IUserPurgeService` | Core Interfaces | Bulk user deletion and data cleanup |
| `UserPurgeService` | Bot/Services | GDPR-compliant user data deletion |
| `BulkPurgeService` | Bot/Services | Coordinates bulk user purge operations |
| `IUserDataExportService` | Core Interfaces | Export user data (for GDPR/privacy requests) |
| `UserDataExportService` | Bot/Services | Generates user data export packages |

---

## Moderation & Enforcement

Services for moderation cases, notes, tags, and enforcement actions.

| Service | Location | Purpose |
|---------|----------|---------|
| `IModerationService` | Core Interfaces | High-level moderation case management |
| `IGuildModerationConfigService` | Core Interfaces | Guild moderation settings (warnings, timeouts, etc.) |
| `IModNoteService` | Core Interfaces | User moderation notes (admins document issues) |
| `IModTagService` | Core Interfaces | Moderation tag management (categorization) |
| `IWatchlistService` | Core Interfaces | User watchlist for monitoring suspicious accounts |
| `WatchlistService` | Bot/Services | Manages watchlist entries and notifications |
| `IContentFilterService` | Core Interfaces | Message content scanning and policy enforcement |
| `ContentFilterService` | Bot/Services | Validates messages against content policies |
| `ISpamDetectionService` | Core Interfaces | Detects spam patterns |
| `SpamDetectionService` | Bot/Services | Message frequency/pattern spam analysis |
| `IRaidDetectionService` | Core Interfaces | Detects coordinated member join raids |
| `RaidDetectionService` | Bot/Services | Monitors for bot raids and coordinated attacks |
| `IFlaggedEventService` | Core Interfaces | Tracks moderation-relevant events |
| `FlaggedEventService` | Bot/Services | Records and queries flagged events (join/message/role changes) |
| `IInvestigationService` | Core Interfaces | Coordinates investigation workflows |
| `InvestigationService` | Bot/Services | Manages moderation investigations and case escalation |

---

## Rat Watch System

Accountability tracker system for tracking member commitments and voting.

| Service | Location | Purpose |
|---------|----------|---------|
| `IRatWatchService` | Core Interfaces | Watch creation, voting, execution, and statistics (1,159 lines) |
| `RatWatchService` | Bot/Services/RatWatch | Full Rat Watch lifecycle management |
| `IRatWatchStatusService` | Core Interfaces | Manages bot status during active watches |
| `RatWatchStatusService` | Bot/Services/RatWatch | Coordinates Rat Watch state → bot presence mapping |
| `RatWatchExecutionService` | Bot/Services/RatWatch | Background service executing completed watches |

---

## Virtual Currency

Ledger-backed wallets, minting, and the charge seam every priced feature calls. Registered by
`AddCurrency` only when `Currency:Enabled` is true.

| Service | Location | Purpose |
|---------|----------|---------|
| `ICurrencyService` | Core Interfaces/Currency | Currency rules, mint authorities, prices, reconciliation |
| `CurrencyService` | Infrastructure/Services/Currency | Enforces name uniqueness, debt floors, and price scope |
| `IWalletService` | Core Interfaces/Currency | Balance rules: mint, spend, transfer, fine, adjust, history |
| `WalletService` | Infrastructure/Services/Currency | The balance rules, audited for fines and adjustments |
| `IChargeService` | Core Interfaces/Currency | Hold / commit / release / refund, the seam priced features use |
| `ChargeService` | Bot/Services/Currency | Price lookup, role exemption, reservation, spend on commit |
| `IChargeHoldStore` | Core Interfaces/Currency | Where open holds live |
| `ChargeHoldStore` | Bot/Services/Currency | In-process holds over `IInstrumentedCache`, prefix `currency:hold:` |
| `IMintService` | Core Interfaces/Currency | Mint authority check in front of `IWalletService.MintAsync` |
| `MintService` | Bot/Services/Currency | User, role, and system principals; audits every mint |
| `ILedgerRepository` | Core Interfaces/Currency | The single write path: idempotency key and cached balance |
| `ICurrencyAccessService` | Bot/Interfaces | What a portal user may do with one currency (None / Read / Moderate / Administer) |
| `CurrencyAccessService` | Bot/Authorization | Maps the signed-in user onto a currency's scope, mirroring `GuildAccessHandler` |

---

## Analytics & Metrics

Services for collecting, aggregating, and reporting analytics data.

| Service | Location | Purpose |
|---------|----------|---------|
| `ICommandAnalyticsService` | Core Interfaces | Slash command execution metrics |
| `CommandAnalyticsService` | Infrastructure/Services | Command frequency and performance tracking |
| `IEngagementAnalyticsService` | Core Interfaces | Server engagement metrics (active users, messages) |
| `EngagementAnalyticsService` | Bot/Services | Engagement analysis and trends |
| `IModerationAnalyticsService` | Core Interfaces | Moderation action statistics |
| `IServerAnalyticsService` | Core Interfaces | Guild-wide metrics and health indicators |
| `ServerAnalyticsService` | Bot/Services | Aggregates guild health metrics |
| `ICommandMetadataService` | Core Interfaces | Command registration and metadata tracking |
| `ICommandLogService` | Core Interfaces | Records command execution logs |
| `MessageLogService` | Bot/Services | Persists message history for analysis |
| `ChannelActivityAggregationService` | Bot/Services | Background service aggregating channel activity snapshots |
| `MemberActivityAggregationService` | Bot/Services | Background service tracking per-member activity metrics |
| `BusinessMetricsUpdateService` | Bot/Services | Background service aggregating business KPIs |
| `AnalyticsRetentionService` | Bot/Services | Background service purging old analytics records |
| `SearchService` | Bot/Services | Search orchestrator (~200 lines); delegates to ISearchProvider implementations |
| `ISearchProvider` | Bot Interfaces | Pluggable search provider abstraction |
| `AuditLogsSearchProvider` | Bot/Services/Search | Searches audit logs |
| `CommandLogsSearchProvider` | Bot/Services/Search | Searches command execution logs |
| `CommandsSearchProvider` | Bot/Services/Search | Searches registered commands |
| `GuildsSearchProvider` | Bot/Services/Search | Searches guild data |
| `MessageLogsSearchProvider` | Bot/Services/Search | Searches message history |
| `PagesSearchProvider` | Bot/Services/Search | Searches portal pages |
| `RemindersSearchProvider` | Bot/Services/Search | Searches reminders |
| `ScheduledMessagesSearchProvider` | Bot/Services/Search | Searches scheduled messages |
| `UsersSearchProvider` | Bot/Services/Search | Searches user data |

---

## Performance Monitoring

Services for tracking performance metrics, latency, and system health.

| Service | Location | Purpose |
|---------|----------|---------|
| `IApiRequestTracker` | Core Interfaces | Tracks Discord API request frequency and rates |
| `ApiRequestTracker` | Bot/Services | Per-guild request counting with SlidingWindow algorithm |
| `ILatencyHistoryService` | Core Interfaces | Tracks bot latency over time |
| `LatencyHistoryService` | Bot/Services | Maintains latency histogram with memory reporting |
| `IConnectionStateService` | Core Interfaces | Tracks Discord connection state changes |
| `ConnectionStateService` | Bot/Services | Connection event recording and diagnostics |
| `IMemoryDiagnosticsService` | Core Interfaces | System memory usage analysis |
| `MemoryDiagnosticsService` | Bot/Services | Gathers memory metrics from all reporters |
| `IDatabaseMetricsCollector` | Core Interfaces | Database operation metrics |
| `DatabaseMetricsCollector` | Bot/Services | Collects EF Core query metrics |
| `IPerformanceAlertService` | Core Interfaces | SLO violation detection and alerting |
| `PerformanceAlertService` | Bot/Services | Monitors performance thresholds and triggers alerts |
| `ICommandPerformanceAggregator` | Core Interfaces | Aggregates command execution performance |
| `MetricsCollectionService` | Bot/Services | Central metrics collection orchestrator |
| `MetricsUpdateService` | Bot/Services | Background service periodically collecting metrics |
| `PerformanceMetricsBroadcastService` | Bot/Services | Broadcasts metrics via SignalR to dashboards |
| `CpuSamplingService` | Bot/Services | Background service CPU utilization sampling |
| `CpuHistoryService` | Bot/Services | Maintains CPU usage history with memory reporting |
| `AlertMonitoringService` | Bot/Services | Orchestrator (~270 lines); delegates to IMetricValueCollector and IAlertIncidentManager |
| `IMetricValueCollector` | Bot Interfaces | Collects alert metric values |
| `MetricValueCollector` | Bot/Services | Gathers metric data for alert evaluation |
| `IAlertIncidentManager` | Bot Interfaces | Manages alert incident creation and transitions |
| `AlertIncidentManager` | Bot/Services | Creates and manages alert incidents |

---

## Background Services

Long-running services that execute periodic or event-driven tasks.

| Service | Location | Purpose |
|---------|----------|---------|
| `BotHostedService` | Bot/Services | Main bot lifecycle (startup, login, handlers) (739 lines) |
| `MonitoredBackgroundService` | Bot/Services | Base class for background services with health tracking |
| `IBackgroundTaskRunner` | Bot Interfaces | Safe fire-and-forget task execution abstraction |
| `BackgroundTaskRunner` | Bot/Services | Manages background task execution with error handling (used by 13+ call sites) |
| `AuditLogQueueProcessor` | Bot/Services | Processes audit log entries from IAuditLogQueue in batches for efficient bulk insertion |
| `AuditLogRetentionService` | Bot/Services | Periodically purges old audit log records per configured retention policy |
| `GuildMetricsAggregationService` | Bot/Services | Aggregates daily guild-level metrics into GuildMetricsSnapshot records |
| `CommandPerformanceAggregator` | Bot/Services | Aggregates command performance metrics from command logs; implements ICommandPerformanceAggregator |
| `InteractionStateCleanupService` | Bot/Services | Cleanup expired interaction state objects |
| `VerificationCleanupService` | Bot/Services | Cleanup expired verification tokens |
| `MessageLogCleanupService` | Bot/Services | Purge old message logs |
| `NotificationRetentionService` | Bot/Services | Purge old user notifications |
| `AudioCacheCleanupService` | Bot/Services | Cleanup stale cached audio files |
| `ScheduledMessageExecutionService` | Bot/Services | Execute scheduled messages on schedule |
| `ReminderExecutionService` | Bot/Services | Execute pending reminders |
| `VoiceAutoLeaveService` | Bot/Services | Auto-disconnect from idle voice channels |

---

## Notification & Alerting

Services for user notifications, performance alerts, and subscriptions.

| Service | Location | Purpose |
|---------|----------|---------|
| `INotificationService` | Core Interfaces | Notification CRUD operations |
| `NotificationService` | Bot/Services | Notification persistence and delivery (~469 lines after split) |
| `INotificationBroadcaster` | Bot Interfaces | Broadcasts notifications to clients |
| `NotificationBroadcaster` | Bot/Services | Real-time notification broadcasting |
| `NotificationMapper` | Bot/Services | Maps between notification domain and DTO models |
| `IPerformanceNotifier` | Core Interfaces | Sends performance alert notifications |
| `PerformanceNotifier` | Bot/Services | Notifies users of SLO violations |
| `IPerformanceSubscriptionTracker` | Core Interfaces | Manage alert subscriptions |
| `PerformanceSubscriptionTracker` | Bot/Services | Tracks who receives which alerts |
| `IDashboardNotifier` | Core Interfaces | Broadcast updates to dashboard clients |
| `DashboardNotifier` | Bot/Services | SignalR hub for real-time dashboard updates |
| `IDashboardUpdateService` | Core Interfaces | Publish update events for dashboard |
| `DashboardUpdateService` | Bot/Services | Publishes status/metric updates to SignalR |

---

## Data & Repository Services

Services for data access, persistence, and querying.

| Service | Location | Purpose |
|---------|----------|---------|
| `IRepository<T>` | Core Interfaces | Generic async CRUD operations |
| `ISettingsService` | Core Interfaces | Global bot settings persistence |
| `SettingsService` | Infrastructure/Services | Settings key-value store |
| `ICommandModuleConfigurationService` | Core Interfaces | Per-guild command module enable/disable |
| `CommandModuleConfigurationService` | Infrastructure/Services | Command module configuration state |
| `IGuildAudioSettingsService` | Core Interfaces | Guild-specific audio preferences |
| `IScheduledMessageService` | Core Interfaces | Scheduled message management (702 lines) |
| `ScheduledMessageService` | Bot/Services | Schedule creation, execution, cancellation |
| `IReminderService` | Core Interfaces | User reminder management |
| `ReminderService` | Bot/Services | Reminder creation and execution |
| `IConsentService` | Core Interfaces | User consent tracking (GDPR) |
| `ConsentService` | Bot/Services | Manages user data consent records |
| `IWelcomeService` | Core Interfaces | Welcome message configuration |
| `WelcomeService` | Bot/Services | Welcome message sending and tracking |
| `IVerificationService` | Core Interfaces | Account verification workflow |
| `VerificationService` | Bot/Services | Verification token management |

---

## AI Assistant & Tools

Services for AI-powered chat, tool execution, and LLM integration.

| Service | Location | Purpose |
|---------|----------|---------|
| `IAssistantService` | Core Interfaces | High-level AI assistant orchestration |
| `AssistantService` | Infrastructure/Services | Rate limiting, consent, delegate to agent runner |
| `IAssistantGuildSettingsService` | Core Interfaces | Guild-specific assistant configuration |
| `AssistantGuildSettingsService` | Infrastructure/Services | Assistant settings per guild |
| `IAgentRunner` | Agents/Abstractions | LLM message routing and tool execution |
| `AgentRunner` | Agents | The agentic loop — iterates model call, tool dispatch, and history until a final response |
| `ILlmClient` | Agents/Abstractions | Provider-agnostic LLM completion calls |
| `OpenRouterLlmClient` | Agents/OpenRouter | `ILlmClient` over OpenRouter's OpenAI-compatible chat completions — owned typed `HttpClient` and wire records, no LLM SDK; retry, prompt-cache breakpoints, usage and billed cost |
| `OpenRouterParameterSupportCache` | Agents/OpenRouter | Singleton memo of slugs that reject `temperature` (reasoning models); `OpenRouterLlmClient` resends once without it on OpenRouter's "no endpoints for the requested parameters" 404 and records the slug here so later requests omit it up front |
| `OpenRouterMessageMapper` | Agents/OpenRouter | Maps `Llm*` DTOs to and from the OpenAI-compatible wire shape |
| `IToolRegistry` | Agents/Abstractions | Available tools registry |
| `ToolRegistry` | Agents | Default registry — aggregates the registered `IToolProvider`s and dispatches a call to the owning provider |
| `FilteredToolRegistry` | Agents | Decorator narrowing a registry to a named allow-list; refuses a call outside the set as well as hiding it, so a tool remembered from an earlier cached prefix cannot be invoked |
| `IToolAccessResolver` | Core Interfaces/LLM | Resolves a guild's allowed tool set from `AssistantGuildSettings.EnabledTools`, falling back to the house default set |
| `ToolAccessResolver` | Infrastructure/Services/LLM | Implementation over the settings repository and `IMemoryCache`; invalidated by `AssistantGuildSettingsService` on save |
| `ToolCatalog` | Core/Models/Llm | Static name → category/label/description/scope table behind the settings checklist and the per-tool metrics table; an uncatalogued tool falls into a visible **Other** bucket. Also **routes** an `IAgentTool` to its surface, so a tool without an entry is advertised nowhere |
| `IToolProvider` | Agents/Abstractions | The contract a tool group implements; implementations live in Infrastructure and Bot, beside the domain services they call. Still right for tools that share expensive state; `IAgentTool` is the default for everything else |
| `IAgentTool` | Agents/Abstractions | One tool, one file — definition plus `InvokeAsync`, with `Mutation` declaring a write. The authoring unit; no provider class and no DI line |
| `AgentToolProvider` | Agents | The single adapter from a set of `IAgentTool`s to one `IToolProvider`, and where the `ToolContext.CanMutate` refusal happens (before the tool is entered) |
| `AgentToolRegistration.AddAgentTools` | Agents | Scans the assemblies it is handed for `IAgentTool` implementations and registers each scoped via `TryAddEnumerable`, ordered by full type name; `[OptInTool("Key")]` types only when that flag is set |
| `ToolInput` / `ToolResults` / `ToolJson` | Agents | Argument reading and schema building; the house result shapes (`Error`/`NotFound` satisfy `ToolOutcomes.Classify` by construction); the compact serializer settings |
| `CataloguedAgentToolProvider` | Infrastructure/Services/LLM/Providers | Base for the two surfaces — keeps the scanned tools whose `ToolCatalog` scopes include its own, warns about an uncatalogued one, and refuses forbidden writes in this bot's voice |
| `GuildAgentToolProvider` / `DmAgentToolProvider` | Infrastructure/Services/LLM/Providers | The guild and DM surfaces over individually authored tools, registered as `IToolProvider` and `IDmToolProvider` respectively |
| `IPromptTemplate` | Agents/Abstractions | System prompt and context template |
| `PromptTemplate` | Agents | Loads a template from disk (memory-cached) and renders `{{variable}}` substitutions |
| `RatWatchToolProvider` | Bot/Services/LLM | RatWatch-specific tool provider for assistant |
| `SaveNoteTool` / `SearchNotesTool` / `GetNoteTool` / `ListNotesTool` / `DeleteNoteTool` | Infrastructure/Services/LLM/Tools | The DM assistant's memory tools, the first conversion to `IAgentTool` (they replace `MemoryToolProvider` and `MemoryTools`) |
| `RatWatchTools` | Infrastructure/Services/LLM | Tool implementations for RatWatch queries |
| `UserGuildInfoToolProvider` | Bot/Services/LLM | Tool provider exposing get_user_profile, get_guild_info, and get_user_roles tools; resolves data from Discord client and database |
| `ILlmModelCatalogService` | Core Interfaces/LLM | Local OpenRouter model catalog: refresh, filtered/sorted listing, and the enable/disable allowlist. A refresh never touches `IsEnabled` except the one-time first-ever-refresh bootstrap. |
| `LlmModelCatalogService` | Infrastructure/Services/LLM | Implementation — audits refreshes and enable/disable changes |
| `IOpenRouterModelCatalogClient` | Core Interfaces/LLM | Fetches OpenRouter's `GET /models` directory |
| `OpenRouterModelCatalogClient` | Infrastructure/Services/LLM/OpenRouter | Second typed `HttpClient` against OpenRouter (separate from `OpenRouterLlmClient`), same auth/attribution headers, no retry loop |
| `ILlmModelRepository` | Core Interfaces | `LlmModel` persistence — filtered query, enabled list, last-refresh timestamp, mark-unavailable |
| `LlmModelRepository` | Infrastructure/Data/Repositories | EF Core implementation |
| `LlmCatalogRefreshService` | Bot/Services/LLM | `MonitoredBackgroundService`; periodic catalog refresh on `Llm:CatalogRefreshHours` (default 24, `0` disables), registered only when `OpenRouter:ApiKey` is present |
| `LlmModelsController` | Bot/Controllers | `api/admin/llm-models` — list/filter, refresh, enable/disable, per-mode defaults (`RequireAdmin`); `GetDefaults` delegates to `ILlmModelResolver` |
| `ILlmModelResolver` | Core Interfaces/LLM | Resolves each `LlmMode`'s effective model slug (DB setting → bound options → `OpenRouter:DefaultModel`), with per-mode caching invalidated on `ISettingsService.SettingsChanged`. The single resolution path — the guild/DM assistant context factories, `FeatureRequestConversationService`, and `LlmModelsController.GetDefaults` all call it instead of reading options or settings directly. Also resolves catalog pricing (`LlmCatalogPricing`) for the cost fallback. |
| `LlmModelResolver` | Infrastructure/Services/LLM | Singleton implementation; resolves scoped `ILlmModelRepository` via `IServiceScopeFactory` per call, same pattern as `SettingsService`; logs a once-per-slug warning when the resolved slug is not enabled |
| `AssistantInteractionLogRetentionService` | Bot/Services/LLM | `MonitoredBackgroundService`; daily sweep (`Llm:RetentionSweepIntervalHours`, default 24, `0` disables) of three tables nobody was cleaning up before: guild `AssistantInteractionLog` and the `LlmUsageRecord` ledger by `Assistant:Privacy:InteractionLogRetentionDays`, and DM `DmAssistantInteractionLog` by `DmAssistant:InteractionLogRetentionDays`; a table's sweep is skipped when its retention is `0` or less; registered ungated, batch size `Llm:RetentionBatchSize` (default 1000) |

---

## Configuration & Settings

Services for managing application configuration and options.

| Service | Location | Purpose |
|---------|----------|---------|
| `BotConfiguration` | Bot/Services | Central configuration options holder |
| `DiscordOAuthSettings` | Bot/Services | OAuth2 configuration container |
| `ISettingsRepository` | Core Interfaces | Settings persistence layer |

---

## Base Classes

Reusable base classes for controllers, page models, and API abstractions.

| Class | Location | Purpose |
|-------|----------|---------|
| `ApiControllerBase` | Bot/Controllers | Base controller with error helpers and common response patterns (used by 5 controllers) |
| `GuildPageModelBase` | Bot/Pages/Guilds | Base page model with PopulateGuildLayout() method (used by 24 guild pages) |
| `PaginatedPageModel` | Bot/Pages | Base class for paginated page models |
| `PaginatedGuildPageModel` | Bot/Pages/Guilds | Base for paginated guild pages (used by 6 pages) |

---

## Helpers & Utilities

Lightweight helper classes for common formatting, validation, and calculation tasks.

| Helper | Location | Purpose |
|--------|----------|---------|
| `EmbedHelper` | Bot/Helpers | Standardized embed factory methods for Discord command responses |
| `PaginationHelper` | Bot/Helpers | Page calculation and button building for paginated commands |
| `VoiceChannelHelper` | Bot/Helpers | Voice channel validation for command modules |
| `SearchDisplayHelper` | Bot/Helpers | Search result display formatting and presentation |
| `SearchScoringHelper` | Bot/Helpers | Search result relevance scoring and ranking |
| `ServiceActivityHelper` | Bot/Tracing | Eliminates ~757 lines of tracing boilerplate across 10 services |

---

## Utility & Support

Miscellaneous utility and support services.

| Service | Location | Purpose |
|---------|----------|---------|
| `IVersionService` | Core Interfaces | Application version tracking |
| `VersionService` | Bot/Services | Returns current app version |
| `ITimeParsingService` | Core Interfaces | Natural language time parsing |
| `TimeParsingService` | Bot/Services | Converts text to time spans/dates (598 lines) |
| `IPageMetadataService` | Core Interfaces | Portal page metadata (titles, descriptions) |
| `PageMetadataService` | Bot/Services | Returns page SEO metadata |
| `IThemeService` | Core Interfaces | Portal theme/style management |
| `ThemeService` | Bot/Services | Theme preference persistence |
| `IInteractionStateService` | Core Interfaces | Discord interaction state tracking |
| `InteractionStateService` | Bot/Services | Component state for multi-step interactions |
| `InteractionState` | Bot/Services | Per-user interaction context holder |
| `InstrumentedMemoryCache` | Bot/Services | Memory cache with metrics instrumentation |
| `BackgroundServiceHealthRegistry` | Bot/Services | Central registry of background service health |

---

## Service Dependency Diagram

```mermaid
graph TD
    BotService["BotService"]
    BotHostedService["BotHostedService<br/>(IHostedService)"]
    AudioService["AudioService"]
    PlaybackService["PlaybackService"]
    VoxService["VoxService"]
    RatWatchService["RatWatchService"]
    UserManagementService["UserManagementService"]
    MemberSyncService["MemberSyncService<br/>(Background)"]

    ModerationService["ModerationService"]
    SpamDetectionService["SpamDetectionService<br/>(Memory)"]
    RaidDetectionService["RaidDetectionService<br/>(Memory)"]

    AzureTtsService["AzureTtsService"]
    PlaybackService2["PlaybackService"]

    PerformanceMonitoring["Performance Monitoring<br/>(Latency, CPU, API)"]
    Analytics["Analytics Services<br/>(Engagement, Command, Server)"]

    DashboardNotifier["DashboardNotifier<br/>(SignalR)"]
    PerformanceAlertService["PerformanceAlertService"]

    BotHostedService -->|controls| AudioService
    BotHostedService -->|manages| RatWatchService
    BotHostedService -->|calls| BotStatusService["BotStatusService"]

    AudioService -->|sends state| PlaybackService
    PlaybackService -->|uses| SoundCacheService["SoundCacheService"]
    AudioService -->|notifies| AudioNotifier["AudioNotifier<br/>(SignalR)"]

    VoxService -->|uses| AudioService
    VoxService -->|uses| VoxClipLibrary["VoxClipLibrary"]

    AzureTtsService -->|plays to| PlaybackService2

    RatWatchService -->|updates status via| RatWatchStatusService["RatWatchStatusService"]
    RatWatchStatusService -->|notifies| BotStatusService

    UserManagementService -->|syncs via| MemberSyncService

    ModerationService -->|detects via| SpamDetectionService
    ModerationService -->|detects via| RaidDetectionService

    PerformanceMonitoring -->|reports via| PerformanceAlertService
    PerformanceAlertService -->|notifies via| PerformanceNotifier["PerformanceNotifier"]

    Analytics -->|publishes via| DashboardNotifier
    PerformanceAlertService -->|publishes via| DashboardNotifier

    DashboardNotifier -->|broadcasts| Dashboard["Dashboard Clients"]

    BotHostedService -->|coordinates| MemberSyncService
    BotHostedService -->|coordinates| ScheduledMessageService["ScheduledMessageService<br/>(Background)"]
    BotHostedService -->|coordinates| ReminderExecutionService["ReminderExecutionService<br/>(Background)"]

    style BotHostedService fill:#ff9999
    style MemberSyncService fill:#99ccff
    style PerformanceMonitoring fill:#99ff99
    style Analytics fill:#99ff99
    style DashboardNotifier fill:#ffcc99
    style Dashboard fill:#cccccc
```

---

## Key Service Patterns

### Pluggable Provider Pattern

Search and monitoring services delegate domain-specific logic to pluggable providers:

- `SearchService` orchestrates 9 `ISearchProvider` implementations (AuditLogsSearchProvider, CommandLogsSearchProvider, CommandsSearchProvider, GuildsSearchProvider, MessageLogsSearchProvider, PagesSearchProvider, RemindersSearchProvider, ScheduledMessagesSearchProvider, UsersSearchProvider)
- `AlertMonitoringService` orchestrates `IMetricValueCollector` and `IAlertIncidentManager` for alert evaluation and incident management

### Service Activity Helper Pattern

`ServiceActivityHelper` eliminates boilerplate tracing code across services:

- Standardizes operation timing, error logging, and observability
- Reduces ~757 lines of duplicate instrumentation code across 10+ services
- Provides consistent diagnostic output for monitoring and debugging

### Hosted Services

Long-running background processes that start with the application:

- `BotHostedService` - Main bot lifecycle orchestrator
- `MemberSyncService` - Guild member synchronization
- `ScheduledMessageExecutionService` - Periodic scheduled message delivery
- `ReminderExecutionService` - Reminder trigger execution
- `MetricsUpdateService` - Periodic metrics collection
- `VoiceAutoLeaveService` - Auto-disconnect from idle channels
- And 10+ other cleanup/monitoring services

### Instrumented Memory Services

Services that implement `IMemoryReportable` to provide memory usage diagnostics:

- `CpuHistoryService`
- `LatencyHistoryService`
- `InteractionStateService`
- `RaidDetectionService`
- `SpamDetectionService`

### Per-Guild Locking Pattern

Services using `ConcurrentDictionary<ulong, SemaphoreSlim>` for thread-safe per-guild operations:

- `AudioService` - Voice connections
- `PlaybackService` - Sound playback state
- `SpamDetectionService` - Spam tracking
- `RaidDetectionService` - Raid detection state

### SignalR Broadcasting

Services publishing real-time updates to connected dashboard clients:

- `DashboardNotifier` - Central SignalR hub
- `AudioNotifier` - Audio state changes
- `PerformanceMetricsBroadcastService` - Performance metrics
- `AlertMonitoringService` - Alert notifications

### Health Registry

`IBackgroundServiceHealth` registration for monitoring service health:

```csharp
// All background services register with
BackgroundServiceHealthRegistry.Register(this);
```

Provides dashboard visibility into service health status.

---

## Service Lifecycle

1. **Startup** - `BotHostedService.StartAsync()`
   - Initializes Discord client
   - Attaches event handlers
   - Starts background services
   - Performs initial data syncs

2. **Running** - Background services execute periodically or on-demand
   - Analytics aggregation
   - Member synchronization
   - Message scheduling
   - Performance monitoring

3. **Shutdown** - `BotHostedService.StopAsync()`
   - Gracefully disconnects from Discord
   - Flushes pending data
   - Stops background services

---

## Discovery Tips for Developers

When adding a new feature:

1. **Check existing services first** - Many cross-cutting concerns (logging, tracing, metrics) are already handled
2. **Follow the three-layer architecture** - Put interfaces in Core, implementations in Bot/Infrastructure
3. **Use dependency injection** - Services are registered in DI and injected where needed
4. **Implement background service pattern** - For periodic/async work, extend `MonitoredBackgroundService`
5. **Register with health registry** - Background services should call `BackgroundServiceHealthRegistry.Register(this)`
6. **Consider per-guild locking** - If managing guild-specific state with concurrent access, use the semaphore pattern
7. **Add SignalR notifications** - If dashboard needs real-time updates, broadcast via `DashboardNotifier`

---

## Related Documentation

- [Component API Reference](../articles/component-api.md)
- [Vox System Specification](../articles/vox-system-spec.md)
- [Soundboard Feature Guide](../articles/soundboard.md)
- [Audio Dependencies](../articles/audio-dependencies.md)
- [Database Schema](../articles/database-schema.md)
- [Testing Guide](../articles/testing-guide.md)
