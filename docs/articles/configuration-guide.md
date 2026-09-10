# Configuration Guide

**Version:** 1.0
**Last Updated:** 2026-02-19

Unified reference for how the Discord Bot Management System is configured. Covers all three configuration mechanisms, when to use each, and how they interact.

---

## Configuration Architecture Overview

The application uses three distinct configuration mechanisms, each serving a different audience and change frequency:

| Mechanism | Storage | Changed By | Runtime-Changeable? | Restart Required? |
|-----------|---------|------------|---------------------|-------------------|
| **appsettings.json** (`IOptions<T>`) | JSON files, env vars, user secrets | Developer / DevOps | No | Yes |
| **Application Settings** (`ISettingsService`) | `ApplicationSettings` DB table | Admin via web UI | Yes | No (most) |
| **Per-Guild Settings** (dedicated entities) | Dedicated DB tables | Admin via web UI or slash commands | Yes | No |

A fourth mechanism, **Command Module Configuration**, controls which Discord slash command modules are loaded at startup.

### Decision Guide: Where Should My New Setting Live?

```
Is this a secret or credential?
  → User Secrets / environment variables (never committed)

Is this infrastructure config (DB connection, file paths, external service URLs)?
  → appsettings.json (IOptions<T>)

Should an admin change this at runtime without a deploy?
  → ApplicationSettings database table (ISettingsService)

Does this vary per Discord guild (server)?
  → Per-guild settings entity (e.g., GuildAudioSettings)

Is this a tuning knob for background services, retention, or performance?
  → appsettings.json (IOptions<T>) — changed per-environment, not at runtime
```

### Visual: Configuration Priority Chain

For settings that exist in the `ApplicationSettings` system, values are resolved in this order (highest priority first):

```
┌─────────────────────────────────┐
│ 1. Database (ApplicationSettings │  ← Admin UI overrides
│    table)                        │
├─────────────────────────────────┤
│ 2. IConfiguration                │  ← appsettings.json / env vars /
│    (appsettings.json stack)      │     user secrets
├─────────────────────────────────┤
│ 3. SettingDefinition.DefaultValue│  ← Hardcoded in SettingDefinitions.cs
├─────────────────────────────────┤
│ 4. default(T) / null            │  ← Fallback if no definition exists
└─────────────────────────────────┘
```

Resetting a setting in the Admin UI **deletes the database row**, causing the system to fall through to the appsettings.json value or the hardcoded default.

---

## 1. appsettings.json Configuration (`IOptions<T>`)

This is the primary configuration mechanism for infrastructure, service tuning, and deployment-specific settings. It uses the standard ASP.NET Core configuration system.

### Loading Order

Configuration files are loaded in this order (later sources override earlier ones):

1. `appsettings.json` — base configuration with sensible defaults
2. `appsettings.{Environment}.json` — environment-specific overrides
3. User Secrets — development only (UserSecretsId: `7b84433c-c2a8-46db-a8bf-58786ea4f28e`)
4. Environment variables — `__` (double underscore) as separator

Set the environment with `ASPNETCORE_ENVIRONMENT` (`Development`, `Staging`, `Production`).

### Reload Behavior

All Options classes are consumed via `IOptions<T>` (not `IOptionsMonitor<T>` or `IOptionsSnapshot<T>`). This means values are **read once when the service is resolved from DI** and do not hot-reload. A process restart is required for any appsettings change to take effect.

### Validation Strategy

Two security-critical classes validate on start — the app will fail to start if required values
are missing:

- `BotConfiguration` — `.ValidateOnStart()` with `BotConfigurationValidator`
  (`IValidateOptions<BotConfiguration>`), which requires `Token` only when `Enabled` is true. See
  **Discord:Enabled (web-only mode)** below.
- `IdentityConfigOptions` — `ValidateDataAnnotations().ValidateOnStart()`.

`DiscordOAuthOptions` (`Discord:OAuth:ClientId`/`ClientSecret`) is **not** validated at startup and
has no `[Required]` fields: they are optional, and `IdentityServiceExtensions.AddDiscordOAuth`
skips registering the Discord authentication scheme entirely when either is blank. This matters
beyond convenience — the underlying OAuth handler validates `ClientId`/`ClientSecret` on *every*
request (not just Discord sign-in requests), so registering the scheme with blank credentials
would 500 every page load, not just Discord login. `DiscordOAuthSettings.IsConfigured` (a
singleton) reflects whether both are actually set; `Login`/`LinkDiscord` use it to hide or disable
the Discord option. When either is blank, `AddDiscordOAuth` also logs one startup Warning naming
the missing keys, so an unconfigured production deployment doesn't lose Discord login silently.

All other Options classes rely on in-class defaults and do not validate at startup.

#### Discord:Enabled (web-only mode)

`Discord:Enabled` (bool, default `true`) controls whether the process logs in to Discord at all.
Set it to `false` to run the web portal — Razor Pages, REST controllers, SignalR, Identity,
migrations — without a bot token or gateway connection. Used for browser/UI testing (Playwright)
and for running the admin portal without a bot. When `false`:

- `Discord:Token` is not required (`BotConfigurationValidator` skips the check).
- `BotHostedService` logs one Information line ("Discord bot disabled by configuration; running
  web-only") and returns without wiring gateway events, initializing the interaction handler, or
  calling `LoginAsync`/`StartAsync` on the client.
- `SlashCommandRegistrationService` logs and returns without discovering/registering commands.
- `DiscordSocketClient` is still registered in DI (many page models and services inject it) — it
  simply never connects, so `ConnectionState` stays `Disconnected` and `/health`'s `discord` check
  reports `Degraded` (still HTTP 200; only `Unhealthy` returns 503).
- Background services that read the gateway (`ReminderExecutionService`,
  `VoiceAutoLeaveService`) poll `ConnectionState` every 5s and simply wait forever rather than
  erroring; others (`MemberSyncService`, `MetricsUpdateService`, `RatWatchExecutionService`) treat
  an empty/never-populated client cache as "nothing to do".

```bash
Discord__Enabled=false dotnet run --project src/DiscordBot.Bot
```

### Secrets Management

**Never commit secrets to source control.** Use User Secrets for development and environment variables for production.

#### Required Secrets (app fails to start without these)

| Secret Key | Purpose |
|------------|---------|
| `Discord:Token` | Bot authentication token — required only when `Discord:Enabled` is `true` (the default); see **Discord:Enabled (web-only mode)** above |

#### Optional Secrets

| Secret Key | Purpose | Behavior if Absent |
|------------|---------|-------------------|
| `Discord:Enabled` | Whether the bot logs in to Discord | Defaults to `true`; set `false` to run web-only without a token |
| `Discord:OAuth:ClientId` / `Discord:OAuth:ClientSecret` | OAuth2 credentials for admin UI Discord login | Discord login option hidden/disabled; other auth (local accounts) unaffected |
| `Discord:TestGuildId` | Instant command registration to test guild | Commands register globally (up to 1hr delay) |
| `Identity:DefaultAdmin:Email` | Email for seeded admin account | No admin created on first run |
| `Identity:DefaultAdmin:Password` | Password for seeded admin account | No admin created on first run |
| `OpenRouter:ApiKey` | OpenRouter API key for AI assistant (https://openrouter.ai/keys) | Assistant feature disabled |
| `AzureSpeech:SubscriptionKey` | Azure Speech Services key for TTS | TTS feature disabled |
| `Elastic:ApiKey` | Elasticsearch ingestion API key | No Elasticsearch log shipping |
| `ElasticApm:ServerUrl` | Elastic APM server URL | APM disabled |

#### Setting Secrets

```bash
# Development (User Secrets)
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token"
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"

# Production (Environment Variables)
Discord__Token="your-bot-token"
Discord__OAuth__ClientId="your-client-id"
Discord__OAuth__ClientSecret="your-client-secret"
```

### Complete IOptions Reference

Every Options class lives in `DiscordBot.Core.Configuration` (except where noted) and defines a `public const string SectionName`. All are registered via `services.Configure<T>(configuration.GetSection(...))` in DI extension methods.

#### Core / Shared

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `ApplicationOptions` | `Application` | `Program.cs` | `Title`, `BaseUrl`, `ContactEmail`, `Version` |
| `CachingOptions` | `Caching` | `Program.cs` | 9 TTL properties for guild, user, interaction, consent, dashboard caches |
| `GuildMembershipCacheOptions` | `GuildMembershipCache` | `Program.cs` | `StoredGuildMembershipDurationMinutes` (30) |
| `BackgroundServicesOptions` | `BackgroundServices` | `Program.cs` | 20+ properties for all background service intervals, batch sizes, delays |
| `ObservabilityOptions` | `Observability` | `Program.cs` | `KibanaUrl`, `SeqUrl` |
| `VerificationOptions` | `Verification` | `Program.cs` | `CodeCharset`, `CodeLength`, `CodeExpiryMinutes`, `MaxCodesPerHour` |

#### Discord Bot / Authentication

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `BotConfiguration`* | `Discord` | `DiscordServiceExtensions` | `Enabled` (default `true`), `Token` [required only when `Enabled`], `TestGuildId`, rate limit defaults, `AdditionalOwnerIds` |
| `DiscordOAuthOptions` | `Discord:OAuth` | `IdentityServiceExtensions` | `ClientId`, `ClientSecret` (both optional — scheme is skipped when either is blank), `Scopes` |
| `IdentityConfigOptions`* | `Identity` | `IdentityServiceExtensions` | Password rules, lockout settings, cookie settings, `DefaultAdmin` sub-object |

\* Uses `.ValidateOnStart()` — fail-fast on missing required values (see Validation Strategy above
for `BotConfiguration`'s conditional check).

#### AI Assistant

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `OpenRouterOptions` | `OpenRouter` | `AssistantServiceExtensions` | `ApiKey` (secret), `BaseUrl`, `DefaultModel` (OpenRouter slug), `MaxRetries`, `TimeoutSeconds`, `RetryBaseDelayMs`, `EnablePromptCachingByDefault`, `AppUrl`, `AppTitle` |
| `AssistantOptions` | `Assistant` | `AssistantServiceExtensions` | Grouped into nested sub-options: `Sampling` (model/tokens/temperature/timeout), `RateLimits` (rate limit + bypass role), `Messages` (question/response length, error text, retry), `Tools` (doc tools, prompt/doc paths), `Cost` (cost tracking, prompt caching), `Privacy` (consent, logging, retention). Historical flat keys (e.g. `Assistant:MaxTokens`) still bind via obsolete forwarding properties and take precedence over the nested key (e.g. `Assistant:Sampling:MaxTokens`) if both are set. |

#### Audio / Voice

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `VoiceChannelOptions` | `VoiceChannel` | `VoiceServiceExtensions` | `AutoLeaveTimeoutSeconds` (300), `CheckIntervalSeconds` (30) |
| `SoundboardOptions` | `Soundboard` | `VoiceServiceExtensions` | `BasePath`, `FfmpegPath`, per-guild defaults for limits |
| `AudioCacheOptions` | `AudioCache` | `VoiceServiceExtensions` | `Enabled`, `CachePath`, `MaxCacheSizeBytes`, `EntryTtlHours` |
| `SoundPlayLogRetentionOptions` | `SoundPlayLogRetention` | `VoiceServiceExtensions` | `RetentionDays` (90), `CleanupBatchSize`, `Enabled` |
| `AzureSpeechOptions` | `AzureSpeech` | `VoiceServiceExtensions` | `SubscriptionKey` (secret), `Region`, `DefaultVoice` |
| `AzureSpeechSsmlOptions` | `AzureSpeech:Ssml` | `VoiceServiceExtensions` | `EnableValidation`, `StrictMode`, `MaxComplexityScore` |
| `VoxOptions` | `Vox` | `VoiceServiceExtensions` | `BasePath`, `DefaultWordGapMs` (50), `MaxMessageWords` (50) |

#### Scheduling / Notifications

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `ScheduledMessagesOptions` | `ScheduledMessages` | `ScheduledServicesExtensions` | `CheckIntervalSeconds` (60), `MaxConcurrentExecutions` (5) |
| `ReminderOptions` | `Reminder` | `ScheduledServicesExtensions` | `CheckIntervalSeconds` (30), `MaxRemindersPerUser` (25) |
| `NotificationOptions` | `Notification` | `NotificationServiceExtensions` | Event type toggles, `DuplicateSuppressionMinutes` (5) |
| `NotificationRetentionOptions` | `NotificationRetention` | `NotificationServiceExtensions` | Per-state retention days (dismissed/read/unread), cleanup config |

#### Moderation / Community

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `ModerationOptions` | `Moderation` | `ModerationServiceExtensions` | `DefaultTempBanDurationDays` (7), `MaxPurgeMessages` (100) |
| `AutoModerationOptions` | `AutoModeration` | `ModerationServiceExtensions` | `DetectionCacheExpiryMinutes`, `FlaggedEventRetentionDays` |
| `RatWatchOptions` | `RatWatch` | `RatWatchServiceExtensions` | `CheckIntervalSeconds` (30), `DefaultVotingDurationMinutes` (5) |

#### Data Retention / Logging

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `AuditLogRetentionOptions` | `AuditLogRetention` | `LoggingServiceExtensions` | `RetentionDays` (90), `CleanupBatchSize`, `Enabled` |
| `MessageLogRetentionOptions` | `MessageLogRetention` | `LoggingServiceExtensions` | `RetentionDays` (90), `CleanupBatchSize`, `Enabled` |

#### Analytics / Performance / Observability

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `AnalyticsRetentionOptions` | `AnalyticsRetention` | `AnalyticsServiceExtensions` | `HourlyRetentionDays` (14), `DailyRetentionDays` (365) |
| `HistoricalMetricsOptions` | `HistoricalMetrics` | `AnalyticsServiceExtensions` | `SampleIntervalSeconds` (60), `RetentionDays` (30) |
| `PerformanceMetricsOptions` | `PerformanceMetrics` | `PerformanceMetricsServiceExtensions` | Latency sampling, slow query tracking, API tracking |
| `PerformanceAlertOptions` | `PerformanceAlerts` | `PerformanceMetricsServiceExtensions` | `CheckIntervalSeconds`, `ConsecutiveBreachesRequired` |
| `PerformanceBroadcastOptions` | `PerformanceBroadcast` | `PerformanceMetricsServiceExtensions` | Per-metric-category SignalR broadcast intervals |
| `SamplingOptions` | `OpenTelemetry:Tracing:Sampling` | `OpenTelemetryExtensions` | `DefaultRate` (0.1), `ErrorRate` (1.0), `SlowThresholdMs` |

#### Infrastructure / Database

| Options Class | Section Key | Registered In | Key Properties |
|--------------|-------------|---------------|----------------|
| `DatabaseSettings`** | `Database` | `ServiceCollectionExtensions` | `SlowQueryThresholdMs`, `LogQueryParameters`, `Provider` |

\*\* Lives in `DiscordBot.Infrastructure.Configuration` (the only Options class not in Core). Also eagerly read at startup via `.Get<DatabaseSettings>()` to select the DB provider before DbContext registration.

### Environment-Specific Overrides

| Setting Category | Development | Staging | Production |
|-----------------|-------------|---------|------------|
| Log level (default) | Debug | Information | Warning |
| Log level (DiscordBot) | Debug | Debug | Information |
| Log retention | 7 days | 14 days | 30 days |
| Log buffering | No | No | Yes |
| Slow query threshold | 100ms | 200ms | 500ms |
| Query param logging | Yes | No | No |
| OTel sampling | Default rates | Default rates | May override via env |

---

## 2. Application Settings (Database-Stored, Admin UI)

Runtime-adjustable operational settings that administrators change through the web UI at `/Admin/Settings`. These override appsettings.json values for the same keys.

### Architecture

- **Entity:** `ApplicationSetting` — stored in the `ApplicationSettings` table
- **Definitions:** `SettingDefinitions.cs` — static registry of all known settings with metadata
- **Service:** `ISettingsService` / `SettingsService` (Singleton) — reads, merges, validates, and persists
- **Repository:** `ISettingsRepository` / `SettingsRepository` (Scoped) — EF Core data access
- **UI:** `/Admin/Settings` Razor Page with tabbed categories

The service is a **Singleton** to maintain the `IsRestartPending` flag and `SettingsChanged` event across HTTP requests. It uses `IServiceScopeFactory` to resolve the scoped repository per call.

### All Database-Stored Settings

| Key | Category | Type | Default | Description |
|-----|----------|------|---------|-------------|
| `General:DefaultTimezone` | General | Dropdown | `UTC` | Timezone for scheduled tasks and displays |
| `General:StatusMessage` | General | String | `""` | Custom Discord bot status message |
| `General:BotEnabled` | General | Boolean | `true` | Soft-disable bot without stopping the service |
| `Features:MessageLoggingEnabled` | Features | Boolean | `true` | Global message logging toggle |
| `Features:WelcomeMessagesEnabled` | Features | Boolean | `true` | Global welcome messages toggle |
| `Features:RatWatchEnabled` | Features | Boolean | `true` | Global Rat Watch toggle |
| `Features:AudioEnabled` | Features | Boolean | `true` | Global audio (soundboard, TTS, voice) toggle |
| `Assistant:GloballyEnabled` | Features | Boolean | `false` | Global AI assistant toggle |
| `Advanced:MessageLogRetentionDays` | Advanced | Integer | `90` | Message log retention (range: 1-365) |
| `Advanced:AuditLogRetentionDays` | Advanced | Integer | `90` | Audit log retention (range: 1-365) |
| `Appearance:DefaultThemeId` | Appearance | Integer | `""` | Default UI theme (SuperAdmin only) |

### Settings Categories and UI Tabs

| Category | UI Tab | Authorization | Content |
|----------|--------|---------------|---------|
| General | General | Admin+ | Timezone, status, bot enabled |
| Features | Features | Admin+ | Feature toggle switches |
| — | Commands | Admin+ | Command module enable/disable (separate system) |
| Advanced | Advanced | Admin+ | Data retention policies |
| — | Bot Control | Admin+ | Bot restart/shutdown, live status |
| Appearance | Appearance | SuperAdmin only | Theme selection |

### Real-Time Updates

When settings change, `SettingsService` fires a `SettingsChanged` event. Currently only `General:StatusMessage` implements real-time updates — `BotHostedService` subscribes and immediately updates the Discord bot status. Other settings take effect on next use or restart.

### Adding a New Database-Stored Setting

1. Add a `SettingDefinition` to `SettingDefinitions.cs` (Infrastructure layer)
2. Optionally add a default to `appsettings.json`
3. The setting automatically appears in the Admin UI under its category
4. Read it in code: `await _settingsService.GetSettingValueAsync<bool>("Features:MyFeature")`
5. Optionally subscribe to `SettingsChanged` for real-time reaction

See [Settings Page documentation](settings-page.md) for detailed implementation examples.

---

## 3. Per-Guild Settings (Dedicated Database Entities)

Per-guild settings allow each Discord server to customize feature behavior independently. Each feature area has its own entity and table, following a "get-or-create with defaults" pattern — a row is lazily created when first accessed.

### Guild-Level Entities

| Entity | Table | Key Fields | Managed Via |
|--------|-------|------------|-------------|
| `Guild` | `Guilds` | `IsActive` (kill-switch), `Prefix`, `Settings` (reserved JSON) | Auto-created on bot join |
| `GuildAudioSettings` | `GuildAudioSettings` | `AudioEnabled`, `AutoLeaveTimeoutMinutes`, `QueueEnabled`, `MaxDurationSeconds`, `MaxFileSizeBytes`, `MaxSoundsPerGuild`, `EnableMemberPortal`, `SilentPlayback` | Admin UI (Guild Audio Settings page) |
| `GuildTtsSettings` | `GuildTtsSettings` | `TtsEnabled`, `DefaultVoice`, `DefaultSpeed/Pitch/Volume`, `MaxMessageLength`, `RateLimitPerMinute`, `SsmlEnabled`, `StrictSsmlValidation` | Admin UI (Guild TTS Settings page) |
| `GuildRatWatchSettings` | `GuildRatWatchSettings` | `IsEnabled`, `Timezone`, `MaxAdvanceHours`, `VotingDurationMinutes`, `PublicLeaderboardEnabled` | Admin UI |
| `GuildModerationConfig` | `GuildModerationConfigs` | `Mode` (Simple/Advanced), `SimplePreset`, `SpamConfig` (JSON), `ContentFilterConfig` (JSON), `RaidProtectionConfig` (JSON) | Admin UI |
| `AssistantGuildSettings` | `AssistantGuildSettings` | `IsEnabled`, `AllowedChannelIds` (JSON), `RateLimitOverride` | Admin UI |
| `WelcomeConfiguration` | `WelcomeConfigurations` | `IsEnabled`, `WelcomeChannelId`, `WelcomeMessage` (template), `IncludeAvatar`, `UseEmbed`, `EmbedColor` | Slash commands + Admin UI |

### How Defaults Work

Per-guild entities define sensible defaults in their class properties. When a guild is first accessed for a feature:

1. The service calls the repository to fetch the guild's settings
2. If no row exists, a new entity is created with class-defined defaults and saved
3. The entity is returned

This means the "default" for per-guild settings is always the C# property initializer on the entity class, not appsettings.json.

---

## 4. Command Module Configuration

Controls which Discord slash command modules are registered at bot startup. This is a **separate system** from Application Settings, stored in the `CommandModuleConfigurations` table.

### How It Works

1. At startup, `InteractionHandler.InitializeAsync()` calls `SyncModulesAsync()` to seed missing module DB rows
2. Only modules whose `IsEnabled == true` in the database are registered with Discord.NET via `AddModuleAsync()`
3. Disabled modules produce no slash commands at all — they are not loaded

**Important:** Enabling/disabling a module requires a bot restart. The Admin UI communicates this via `RequiresRestart` flags.

### Module Categories

| Category | Modules | Can Disable? |
|----------|---------|-------------|
| Core | GeneralModule, VerifyAccountModule, ConsentModule | No (protected) |
| Admin | AdminModule, WelcomeModule, ScheduleModule | Yes |
| Moderation | ModerationActionModule, ModerationHistoryModule, ModStatsModule, ModNoteModule, ModTagModule, WatchlistModule, InvestigateModule | Yes |
| Features | RatWatchModule, ReminderModule, PrivacyModule | Yes |
| Audio | TtsModule, SoundboardModule, VoiceModule | Yes |
| Utility | UtilityModule | Yes |

### Service

`ICommandModuleConfigurationService` (Singleton) — manages module state, enforces Core module protection, and maintains its own `IsRestartPending` flag and `ConfigurationChanged` event.

---

## 5. The Three-Axis Control Model

Feature availability is controlled by three independent axes that are evaluated in order. All must pass for a feature to be available:

```
Axis A: Module Loading (global, requires restart)
  │  Is the command module registered with Discord.NET?
  │  Controlled by: CommandModuleConfigurations table
  │  Checked at: Bot startup (InteractionHandler.InitializeAsync)
  │
  ▼
Axis B: Feature Flag (global, hot-swappable)
  │  Is the feature globally enabled?
  │  Controlled by: ApplicationSettings table (ISettingsService)
  │  Checked at: Every command invocation (precondition attributes)
  │
  ▼
Axis C: Per-Guild Settings (hot-swappable)
  │  Is the feature enabled for this specific guild?
  │  Controlled by: Per-guild settings tables
  │  Checked at: Every command invocation (precondition attributes)
  │
  ▼
  Feature is available to the user
```

### Examples by Feature

**Audio (Soundboard, TTS, Voice):**
```
Axis A: SoundboardModule / TtsModule / VoiceModule enabled in CommandModuleConfigurations?
Axis B: ISettingsService → "Features:AudioEnabled" == true?
Axis C: GuildAudioSettings.AudioEnabled == true? (and GuildTtsSettings.TtsEnabled for TTS)
```

**AI Assistant:**
```
Axis A: (No dedicated module — always loaded)
Axis B: ISettingsService → "Assistant:GloballyEnabled" == true?
Axis C: AssistantGuildSettings.IsEnabled == true?
  └─ Bonus: AssistantGuildSettings.AllowedChannelIds (empty = all channels)
  └─ Bonus: AssistantGuildSettings.RateLimitOverride ?? AssistantOptions.DefaultRateLimit
```

**Rat Watch:**
```
Axis A: RatWatchModule enabled in CommandModuleConfigurations?
Axis B: ISettingsService → "Features:RatWatchEnabled" == true?
Axis C: (GuildRatWatchSettings.IsEnabled exists but is not currently wired into the precondition)
```

**Guild-Level Kill Switch:**

`Guild.IsActive == false` disables the bot entirely for that guild, bypassing all other settings. This is enforced by the `RequireGuildActiveAttribute` precondition.

---

## 6. Comparison: When to Use Each Mechanism

| Question | appsettings.json | ApplicationSettings DB | Per-Guild Entity | Command Module Config |
|----------|-----------------|----------------------|-----------------|----------------------|
| **Who changes it?** | Developer/DevOps | Admin (web UI) | Admin (web UI / commands) | Admin (web UI) |
| **When does it take effect?** | After restart | Immediately (most) | Immediately | After restart |
| **Scope** | Global (per-environment) | Global | Per-guild | Global (per-module) |
| **Examples** | DB connection, file paths, service intervals, API keys, retention policies | Feature on/off toggles, bot status, default timezone | Audio limits, TTS voice, welcome messages, moderation mode | Enable/disable entire command groups |
| **Storage** | JSON files + env vars | `ApplicationSettings` table | Dedicated feature tables | `CommandModuleConfigurations` table |
| **Read via** | `IOptions<T>` | `ISettingsService.GetSettingValueAsync<T>()` | Feature-specific service (e.g., `IGuildAudioSettingsService`) | `ICommandModuleConfigurationService` |
| **Hot-reload?** | No | Yes | Yes | No (requires restart) |

### Rules of Thumb

1. **If it's a credential or external URL** → User Secrets / environment variable
2. **If only a developer should change it** → `appsettings.json` with an `IOptions<T>` class
3. **If an admin should change it without deploying** → `ApplicationSettings` (database)
4. **If it varies per Discord server** → Per-guild settings entity
5. **If it controls whether an entire command group exists** → Command Module Configuration

---

## 7. Key Files Reference

### Configuration Classes

| Path | Purpose |
|------|---------|
| `src/DiscordBot.Core/Configuration/` | All 33+ `IOptions<T>` classes |
| `src/DiscordBot.Infrastructure/Configuration/DatabaseSettings.cs` | Database provider selection options |

### Database Settings System

| Path | Purpose |
|------|---------|
| `src/DiscordBot.Core/Entities/ApplicationSetting.cs` | Database-stored setting entity |
| `src/DiscordBot.Infrastructure/Services/SettingDefinitions.cs` | Static registry of all known settings |
| `src/DiscordBot.Core/Interfaces/ISettingsService.cs` | Service contract with `SettingsChanged` event |
| `src/DiscordBot.Infrastructure/Services/SettingsService.cs` | Three-tier merge logic, validation, persistence |
| `src/DiscordBot.Core/Enums/SettingCategory.cs` | Setting categories (General, Logging, Features, Advanced, Appearance) |
| `src/DiscordBot.Core/Enums/SettingDataType.cs` | Data types (String, Integer, Boolean, Decimal, Json) |

### Command Module Configuration

| Path | Purpose |
|------|---------|
| `src/DiscordBot.Core/Entities/CommandModuleConfiguration.cs` | Module enable/disable entity |
| `src/DiscordBot.Infrastructure/Services/CommandModuleConfigurationService.cs` | Module management with Core protection |
| `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` | Startup enforcement — skips disabled modules |

### Per-Guild Settings

| Path | Purpose |
|------|---------|
| `src/DiscordBot.Core/Entities/Guild.cs` | Guild entity with `IsActive` kill-switch |
| `src/DiscordBot.Core/Entities/GuildAudioSettings.cs` | Per-guild audio configuration |
| `src/DiscordBot.Core/Entities/GuildTtsSettings.cs` | Per-guild TTS configuration |
| `src/DiscordBot.Core/Entities/GuildRatWatchSettings.cs` | Per-guild Rat Watch configuration |
| `src/DiscordBot.Core/Entities/GuildModerationConfig.cs` | Per-guild moderation configuration |
| `src/DiscordBot.Core/Entities/AssistantGuildSettings.cs` | Per-guild AI assistant configuration |
| `src/DiscordBot.Core/Entities/WelcomeConfiguration.cs` | Per-guild welcome message configuration |

### DI Registration

| Path | Registers |
|------|-----------|
| `src/DiscordBot.Bot/Program.cs` (lines 138-149) | Core/shared Options classes |
| `src/DiscordBot.Bot/Extensions/DiscordServiceExtensions.cs` | `BotConfiguration` (validated) |
| `src/DiscordBot.Bot/Extensions/IdentityServiceExtensions.cs` | `DiscordOAuthOptions`, `IdentityConfigOptions` (validated) |
| `src/DiscordBot.Bot/Extensions/VoiceServiceExtensions.cs` | All 7 audio/voice Options classes |
| `src/DiscordBot.Bot/Extensions/AssistantServiceExtensions.cs` | `OpenRouterOptions`, `AssistantOptions` |
| `src/DiscordBot.Bot/Extensions/ScheduledServicesExtensions.cs` | `ScheduledMessagesOptions`, `ReminderOptions` |
| `src/DiscordBot.Bot/Extensions/NotificationServiceExtensions.cs` | `NotificationOptions`, `NotificationRetentionOptions` |
| `src/DiscordBot.Bot/Extensions/ModerationServiceExtensions.cs` | `ModerationOptions`, `AutoModerationOptions` |
| `src/DiscordBot.Bot/Extensions/RatWatchServiceExtensions.cs` | `RatWatchOptions` |
| `src/DiscordBot.Bot/Extensions/AnalyticsServiceExtensions.cs` | `AnalyticsRetentionOptions`, `HistoricalMetricsOptions` |
| `src/DiscordBot.Bot/Extensions/PerformanceMetricsServiceExtensions.cs` | Performance metrics and broadcast Options |
| `src/DiscordBot.Bot/Extensions/LoggingServiceExtensions.cs` | `AuditLogRetentionOptions`, `MessageLogRetentionOptions` |
| `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs` | `SamplingOptions` |
| `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs` | `DatabaseSettings`, `ISettingsRepository`, `ISettingsService` |

### Admin UI

| Path | Purpose |
|------|---------|
| `src/DiscordBot.Bot/Pages/Admin/Settings.cshtml` | Settings page view (all tabs) |
| `src/DiscordBot.Bot/Pages/Admin/Settings.cshtml.cs` | Page handlers for save/reset/restart |
| `src/DiscordBot.Bot/Pages/Shared/_SettingField.cshtml` | DataType-to-input-control rendering |

---

## Related Documentation

- [Environment Configuration](environment-configuration.md) — Detailed per-environment appsettings overrides, log levels, Seq integration
- [Settings Page](settings-page.md) — Admin UI for database-stored settings (UI components, JavaScript API, testing)
- [Command Module Configuration](command-module-configuration.md) — Dynamic module enable/disable with per-guild overrides
- [Identity Configuration](identity-configuration.md) — Authentication setup and Discord OAuth
- [Docker Deployment](docker-deployment.md) — Environment variable configuration for containers
