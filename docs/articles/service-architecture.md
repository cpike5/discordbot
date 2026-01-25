# Service Architecture

**Version:** 1.0
**Last Updated:** 2026-01-25

## Overview

The system uses interface-based dependency injection following the Dependency Inversion Principle. All services implement interfaces defined in the Core layer, organized across 18 extension method files in the Bot layer. This document catalogs all 104+ service interfaces by domain.

**Architecture:** Three-layer clean architecture with interfaces in Core, implementations in Infrastructure/Bot, and DI registration in Bot extensions.

---

## Table of Contents

1. [Core Bot Services](#core-bot-services)
2. [Command System](#command-system)
3. [Logging & Audit](#logging--audit)
4. [Repository Interfaces](#repository-interfaces)
5. [User & Guild Services](#user--guild-services)
6. [Authentication & Security](#authentication--security)
7. [Moderation Services](#moderation-services)
8. [Detection Services](#detection-services)
9. [RatWatch Services](#ratwatch-services)
10. [Scheduled Operation Services](#scheduled-operation-services)
11. [Audio & Voice Services](#audio--voice-services)
12. [Analytics Services](#analytics-services)
13. [Performance & Monitoring Services](#performance--monitoring-services)
14. [Real-time Update Services](#real-time-update-services)
15. [Utility Services](#utility-services)
16. [Interface Naming Conventions](#interface-naming-conventions)
17. [Service Lifetimes](#service-lifetimes)
18. [DI Extension Architecture](#di-extension-architecture)

---

## Core Bot Services

Core services manage bot control, guild management, user operations, and application lifecycle.

**Location:** Interfaces in `src/DiscordBot.Core/Interfaces/`, implementations in `src/DiscordBot.Bot/Services/`

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IBotService` | Bot control and lifecycle management | `BotService` | Scoped | ApplicationServiceExtensions |
| `IGuildService` | Guild CRUD operations | `GuildService` | Scoped | ApplicationServiceExtensions |
| `IUserManagementService` | User management operations | `UserManagementService` | Scoped | ApplicationServiceExtensions |
| `IVersionService` | Application version tracking | `VersionService` | Singleton | ApplicationServiceExtensions |
| `ISettingsService` | Dynamic settings management | `SettingsService` | Singleton | ServiceCollectionExtensions |

### IVersionService Example

```csharp
public interface IVersionService
{
    string Version { get; }
    string BuildDate { get; }
    string Environment { get; }
}
```

---

## Command System

Services for command registration, logging, analytics, and metadata introspection.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `ICommandLogService` | Command execution logging | `CommandLogService` | Scoped | ApplicationServiceExtensions |
| `ICommandAnalyticsService` | Command usage analytics | `CommandAnalyticsService` | Scoped | AnalyticsServiceExtensions |
| `ICommandMetadataService` | Command introspection/metadata | `CommandMetadataService` | Scoped | ApplicationServiceExtensions |
| `ICommandRegistrationService` | Discord slash command registration | `CommandRegistrationService` | Scoped | ApplicationServiceExtensions |
| `ICommandPerformanceAggregator` | Command performance metrics aggregation | `CommandPerformanceAggregator` | Singleton | PerformanceMetricsServiceExtensions |

---

## Logging & Audit

Services for message logging, audit trails, and the fluent builder pattern for audit entries.

**Cross-Reference:** See [audit-log-system.md](audit-log-system.md) for detailed audit logging patterns.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IMessageLogService` | Message logging service | `MessageLogService` | Scoped | LoggingServiceExtensions |
| `IAuditLogService` | Audit logging with fluent builder | `AuditLogService` | Scoped | LoggingServiceExtensions |
| `IAuditLogBuilder` | Fluent builder for audit entries | `AuditLogBuilder` | Scoped | LoggingServiceExtensions |

### IAuditLogBuilder Fluent Pattern

```csharp
// Typical usage flow
await _auditLog.Action(AuditLogAction.UserBanned)
    .InGuild(guildId)
    .ByUser(moderatorId)
    .OnTarget(userId, "User")
    .WithChange("Reason", null, reason)
    .WithMetadata("Duration", "7 days")
    .WithCorrelationId(correlationId)
    .SaveAsync();
```

**Key Methods:**
- `.Action(AuditLogAction)` - Set the action type
- `.InGuild(ulong)` - Set target guild
- `.ByUser(ulong)` - Set acting user (moderator)
- `.BySystem()` - Mark as system action
- `.OnTarget(string, string)` - Set target (id and type)
- `.WithChange(string, string?, string?)` - Add field change
- `.WithMetadata(string, object)` - Add custom metadata
- `.WithCorrelationId(string)` - Add correlation ID
- `.SaveAsync()` - Persist to database

---

## Repository Interfaces

All repositories follow the Repository pattern with a generic base. Repositories provide data access abstraction for entities stored in the database.

**Cross-Reference:** See [repository-pattern.md](repository-pattern.md) for detailed patterns and examples.

### Core Repositories (11)

| Interface | Description | Entity | Lifetime | Extension |
|-----------|-------------|--------|----------|-----------|
| `IRepository<T>` | Generic base repository | All entities | Scoped | ServiceCollectionExtensions |
| `ISettingsRepository` | Settings data access | `ApplicationSetting` | Scoped | ServiceCollectionExtensions |
| `ICommandLogRepository` | Command log queries | `CommandLog` | Scoped | ServiceCollectionExtensions |
| `IMessageLogRepository` | Message log queries | `MessageLog` | Scoped | ServiceCollectionExtensions |
| `IAuditLogRepository` | Audit log queries | `AuditLog` | Scoped | ServiceCollectionExtensions |
| `IGuildRepository` | Guild data access | `Guild` | Scoped | ServiceCollectionExtensions |
| `IUserRepository` | User data access | `User` | Scoped | ServiceCollectionExtensions |
| `IGuildMemberRepository` | Guild member data access | `GuildMember` | Scoped | ServiceCollectionExtensions |
| `IUserConsentRepository` | Consent data access | `UserConsent` | Scoped | ServiceCollectionExtensions |
| `IWelcomeConfigurationRepository` | Welcome config data access | `WelcomeConfiguration` | Scoped | ServiceCollectionExtensions |
| `IScheduledMessageRepository` | Scheduled message data access | `ScheduledMessage` | Scoped | ServiceCollectionExtensions |
| `IReminderRepository` | Reminder data access | `Reminder` | Scoped | ServiceCollectionExtensions |

### Moderation Repositories (6)

| Interface | Description | Entity | Lifetime | Extension |
|-----------|-------------|--------|----------|-----------|
| `IModerationCaseRepository` | Moderation case queries | `ModerationCase` | Scoped | ServiceCollectionExtensions |
| `IModNoteRepository` | Mod note queries | `ModNote` | Scoped | ServiceCollectionExtensions |
| `IModTagRepository` | Mod tag queries | `ModTag` | Scoped | ServiceCollectionExtensions |
| `IGuildModerationConfigRepository` | Moderation config queries | `GuildModerationConfig` | Scoped | ServiceCollectionExtensions |
| `IFlaggedEventRepository` | Flagged event queries | `FlaggedEvent` | Scoped | ServiceCollectionExtensions |
| `IWatchlistRepository` | Watchlist queries | `Watchlist` | Scoped | ServiceCollectionExtensions |

### RatWatch Repositories (3)

| Interface | Description | Entity | Lifetime | Extension |
|-----------|-------------|--------|----------|-----------|
| `IRatWatchRepository` | RatWatch queries | `RatWatch` | Scoped | ServiceCollectionExtensions |
| `IRatVoteRepository` | Vote queries | `RatVote` | Scoped | ServiceCollectionExtensions |
| `IRatRecordRepository` | Record queries | `RatRecord` | Scoped | ServiceCollectionExtensions |

### Audio Repositories (5)

| Interface | Description | Entity | Lifetime | Extension |
|-----------|-------------|--------|----------|-----------|
| `ISoundRepository` | Sound queries | `Sound` | Scoped | ServiceCollectionExtensions |
| `ISoundPlayLogRepository` | Play log queries | `SoundPlayLog` | Scoped | ServiceCollectionExtensions |
| `IGuildAudioSettingsRepository` | Audio settings queries | `GuildAudioSettings` | Scoped | ServiceCollectionExtensions |
| `ITtsMessageRepository` | TTS message queries | `TtsMessage` | Scoped | ServiceCollectionExtensions |
| `IGuildTtsSettingsRepository` | TTS settings queries | `GuildTtsSettings` | Scoped | ServiceCollectionExtensions |

### Analytics Repositories (6)

| Interface | Description | Entity | Lifetime | Extension |
|-----------|-------------|--------|----------|-----------|
| `IMetricSnapshotRepository` | Metric snapshot queries | `MetricSnapshot` | Scoped | ServiceCollectionExtensions |
| `IMemberActivityRepository` | Member activity queries | `MemberActivitySnapshot` | Scoped | ServiceCollectionExtensions |
| `IChannelActivityRepository` | Channel activity queries | `ChannelActivitySnapshot` | Scoped | ServiceCollectionExtensions |
| `IGuildMetricsRepository` | Guild metrics queries | `GuildMetricsSnapshot` | Scoped | ServiceCollectionExtensions |
| `IPerformanceAlertRepository` | Alert config queries | `PerformanceAlertConfig` | Scoped | ServiceCollectionExtensions |
| `IUserActivityEventRepository` | Activity event queries | `UserActivityEvent` | Scoped | ServiceCollectionExtensions |

### Other Repositories (7)

| Interface | Description | Entity | Lifetime | Extension |
|-----------|-------------|--------|----------|-----------|
| `ICommandModuleConfigurationRepository` | Module config queries | `CommandModuleConfiguration` | Scoped | ServiceCollectionExtensions |
| `IConnectionEventRepository` | Connection event queries | `ConnectionEvent` | Scoped | ServiceCollectionExtensions |
| `IThemeRepository` | Theme queries | `Theme` | Scoped | ServiceCollectionExtensions |
| `INotificationRepository` | Notification queries | `UserNotification` | Scoped | ServiceCollectionExtensions |
| `IAssistantInteractionLogRepository` | AI interaction queries | `AssistantInteractionLog` | Scoped | ServiceCollectionExtensions |
| `IAssistantUsageMetricsRepository` | AI usage queries | `AssistantUsageMetrics` | Scoped | ServiceCollectionExtensions |
| `IAssistantGuildSettingsRepository` | AI settings queries | `AssistantGuildSettings` | Scoped | ServiceCollectionExtensions |

### Generic Repository Pattern

All repositories implement this generic base pattern:

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}
```

---

## User & Guild Services

Services for managing guild members, memberships, user-guild relationships, and consent.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IGuildMemberService` | Guild member operations | `GuildMemberService` | Scoped | ApplicationServiceExtensions |
| `IGuildMembershipService` | Membership tracking | `GuildMembershipService` | Scoped | ApplicationServiceExtensions |
| `IUserDiscordGuildService` | User-guild relationships | `UserDiscordGuildService` | Scoped | ApplicationServiceExtensions |
| `IConsentService` | Consent management | `ConsentService` | Scoped | ApplicationServiceExtensions |

---

## Authentication & Security

Services for Discord OAuth token management, user info retrieval, and verification.

**Cross-Reference:** See [identity-configuration.md](identity-configuration.md) for OAuth setup and troubleshooting.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IDiscordTokenService` | OAuth token management | `DiscordTokenService` | Scoped | DiscordServiceExtensions |
| `IDiscordUserInfoService` | Discord user info retrieval | `DiscordUserInfoService` | Scoped | DiscordServiceExtensions |
| `IVerificationService` | Email/phone verification | `VerificationService` | Scoped | VerificationServiceExtensions |

---

## Moderation Services

Services for moderation operations, mod notes, tags, watchlists, and flagged event handling.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IModerationService` | Moderation operations (ban, kick, mute) | `ModerationService` | Scoped | ModerationServiceExtensions |
| `IModNoteService` | Moderator notes management | `ModNoteService` | Scoped | ModerationServiceExtensions |
| `IModTagService` | Moderator tag management | `ModTagService` | Scoped | ModerationServiceExtensions |
| `IGuildModerationConfigService` | Per-guild moderation config | `GuildModerationConfigService` | Scoped | ModerationServiceExtensions |
| `IFlaggedEventService` | Flagged event handling | `FlaggedEventService` | Scoped | ModerationServiceExtensions |
| `IWatchlistService` | Watchlist management | `WatchlistService` | Scoped | ModerationServiceExtensions |
| `IModerationAnalyticsService` | Moderation statistics | `ModerationAnalyticsService` | Scoped | AnalyticsServiceExtensions |

---

## Detection Services

Singleton services for detecting raid patterns and spam. Require singleton lifetime to maintain in-memory state across requests.

**Important:** These services maintain real-time detection state and must never be scoped or transient.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IRaidDetectionService` | Raid pattern detection | `RaidDetectionService` | Singleton | ModerationServiceExtensions |
| `ISpamDetectionService` | Spam pattern detection | `SpamDetectionService` | Singleton | ModerationServiceExtensions |
| `IContentFilterService` | Content filtering rules | `ContentFilterService` | Scoped | ModerationServiceExtensions |

---

## RatWatch Services

Services for the RatWatch feature, including management and status tracking.

**Cross-Reference:** See [rat-watch.md](rat-watch.md) for feature documentation.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IRatWatchService` | RatWatch management | `RatWatchService` | Scoped | RatWatchServiceExtensions |
| `IRatWatchStatusService` | Status tracking | `RatWatchStatusService` | Scoped | RatWatchServiceExtensions |

---

## Scheduled Operation Services

Services for managing reminders, scheduled messages, and welcome messages.

**Cross-References:**
- [reminder-system.md](reminder-system.md) - Reminder system architecture
- [scheduled-messages.md](scheduled-messages.md) - Scheduled message delivery
- [welcome-system.md](welcome-system.md) - Welcome message configuration

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IReminderService` | Reminder CRUD | `ReminderService` | Scoped | ScheduledServicesExtensions |
| `ITimeParsingService` | Natural language time parsing | `TimeParsingService` | Scoped | ScheduledServicesExtensions |
| `IWelcomeService` | Welcome message service | `WelcomeService` | Scoped | ScheduledServicesExtensions |

---

## Audio & Voice Services

Services for audio playback, soundboard, TTS, and audio settings management.

**Cross-Reference:** See [audio-dependencies.md](audio-dependencies.md) for FFmpeg and codec dependencies.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `ISoundService` | Soundboard management | `SoundService` | Scoped | VoiceServiceExtensions |
| `ISoundFileService` | Sound file I/O operations | `SoundFileService` | Scoped | VoiceServiceExtensions |
| `ISoundCacheService` | Sound caching | `SoundCacheService` | Singleton | VoiceServiceExtensions |
| `ITtsService` | Text-to-speech synthesis | `AzureTtsService` | Scoped | VoiceServiceExtensions |
| `ITtsHistoryService` | TTS history management | `TtsHistoryService` | Scoped | VoiceServiceExtensions |
| `ITtsSettingsService` | TTS settings management | `TtsSettingsService` | Scoped | VoiceServiceExtensions |
| `IGuildAudioSettingsService` | Per-guild audio config | `GuildAudioSettingsService` | Scoped | VoiceServiceExtensions |

**Additional Services (Non-Interface):**
- `AudioService` (Singleton) - Core audio engine
- `PlaybackService` (Singleton) - Audio playback control

---

## Analytics Services

Services for server analytics, engagement metrics, and command analytics.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IServerAnalyticsService` | Server-wide analytics | `ServerAnalyticsService` | Scoped | AnalyticsServiceExtensions |
| `IEngagementAnalyticsService` | User engagement metrics | `EngagementAnalyticsService` | Scoped | AnalyticsServiceExtensions |
| `ICommandAnalyticsService` | Command usage analytics | `CommandAnalyticsService` | Scoped | AnalyticsServiceExtensions |

---

## Performance & Monitoring Services

Services for performance alerting, connection state, health monitoring, and metrics collection. Most are singleton to maintain shared state and efficiency.

**Cross-Reference:** See [metrics.md](metrics.md) for performance metrics documentation.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IPerformanceAlertService` | Performance alerting | `PerformanceAlertService` | Scoped | PerformanceMetricsServiceExtensions |
| `IConnectionStateService` | Connection state tracking | `ConnectionStateService` | Singleton | PerformanceMetricsServiceExtensions |
| `IBackgroundServiceHealth` | Single service health | (interface) | N/A | N/A |
| `IBackgroundServiceHealthRegistry` | Health registry | `BackgroundServiceHealthRegistry` | Singleton | PerformanceMetricsServiceExtensions |
| `IDatabaseMetricsCollector` | Database metrics | `DatabaseMetricsCollector` | Singleton | PerformanceMetricsServiceExtensions |
| `ILatencyHistoryService` | Latency tracking | `LatencyHistoryService` | Singleton | PerformanceMetricsServiceExtensions |
| `IApiRequestTracker` | API request metrics | `ApiRequestTracker` | Singleton | PerformanceMetricsServiceExtensions |
| `IInstrumentedCache` | Cache with metrics | `InstrumentedMemoryCache` | Singleton | WebServiceExtensions |
| `IMemoryDiagnosticsService` | Memory diagnostics | `MemoryDiagnosticsService` | Singleton | PerformanceMetricsServiceExtensions |
| `ICpuSamplingService` | CPU sampling | `CpuSamplingService` | Singleton | PerformanceMetricsServiceExtensions |
| `ICpuHistoryService` | CPU history | `CpuHistoryService` | Singleton | PerformanceMetricsServiceExtensions |
| `IMemoryReportable` | Memory reporting interface | Multiple implementations | N/A | N/A |

### Critical Singleton Requirement

Performance and metrics services **must be singleton** because they:
- Maintain historical state (latency history, CPU history)
- Aggregate data across requests
- Provide shared caching across the application
- Track connection and health state continuously

---

## Real-time Update Services

Services for real-time notifications via SignalR, dashboard updates, and subscription tracking.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `IDashboardNotifier` | Dashboard SignalR notifications | `DashboardNotifier` | Singleton | SignalRServiceExtensions |
| `IDashboardUpdateService` | Dashboard update aggregation | `DashboardUpdateService` | Singleton | SignalRServiceExtensions |
| `IAudioNotifier` | Audio state notifications | `AudioNotifier` | Singleton | VoiceServiceExtensions |
| `IPerformanceNotifier` | Performance notifications | `PerformanceNotifier` | Singleton | PerformanceMetricsServiceExtensions |
| `IPerformanceSubscriptionTracker` | WebSocket subscription tracking | `PerformanceSubscriptionTracker` | Singleton | PerformanceMetricsServiceExtensions |

### SignalR Hub Pattern

```csharp
public interface IDashboardNotifier
{
    Task NotifyGuildUpdatedAsync(ulong guildId);
    Task NotifyCommandExecutedAsync(CommandLogDto log);
    Task NotifyBotStatusChangedAsync(BotStatus status);
    Task NotifyPerformanceMetricAsync(string metric, double value);
}
```

**Hub Registration in Program.cs:**
```csharp
app.MapHub<DashboardHub>("/hubs/dashboard");
```

---

## Utility Services

Miscellaneous services for search, page metadata, interaction state, and command module configuration.

### Service Table

| Interface | Description | Implementation | Lifetime | Extension |
|-----------|-------------|----------------|----------|-----------|
| `ISearchService` | Search functionality | `SearchService` | Scoped | ApplicationServiceExtensions |
| `IPageMetadataService` | Page metadata | `PageMetadataService` | Scoped | ApplicationServiceExtensions |
| `IInteractionStateService` | Discord interaction state | `InteractionStateService` | Scoped | DiscordServiceExtensions |
| `IDiscordMessage` | Message abstraction | `DiscordMessageAdapter` | Scoped | DiscordServiceExtensions |
| `ICommandModuleConfigurationService` | Command module enable/disable | `CommandModuleConfigurationService` | Singleton | ServiceCollectionExtensions |

---

## Interface Naming Conventions

The codebase follows consistent naming patterns for interface classification:

| Pattern | Purpose | Examples |
|---------|---------|----------|
| `I{Name}Service` | Business logic services | `IModerationService`, `IUserManagementService`, `ISearchService` |
| `I{Name}Repository` | Data access layer | `IModerationCaseRepository`, `IGuildRepository`, `IUserRepository` |
| `I{Name}Notifier` | Real-time notifications (SignalR) | `IDashboardNotifier`, `IAudioNotifier`, `IPerformanceNotifier` |
| `I{Name}Handler` | Event handlers | `MessageLoggingHandler`, `DiscordEventHandler` |

---

## Service Lifetimes

ASP.NET Core provides three lifetime options for dependency injection. The choice affects performance, memory usage, and correct behavior.

### Lifetime Comparison

| Lifetime | Created | Shared | Best For | Risks |
|----------|---------|--------|----------|-------|
| **Singleton** | Once at startup | Across all requests | Shared state, expensive resources, real-time notifiers | Thread safety, memory leaks if not careful |
| **Scoped** | Per HTTP request or command | Within same scope | Per-request operations, DbContext-bound services | Performance if overused |
| **Transient** | Every injection | Never shared | Stateless utilities | High memory/GC pressure if over-used |

### Singleton Services

These services must be singleton for correct operation:

| Service | Reason |
|---------|--------|
| `DiscordSocketClient` | Single bot connection to Discord gateway |
| `InteractionService` | Slash command handler, expensive initialization |
| `IVersionService` | Immutable application metadata |
| `ISettingsService` | Holds restart flag state across requests |
| `ICommandModuleConfigurationService` | Module state must persist across requests |
| `IDashboardNotifier` | SignalR hub context must be shared |
| `IAudioNotifier` | Audio state notifications shared |
| `IPerformanceNotifier` | Performance notifications shared |
| `IRaidDetectionService` | In-memory state for raid detection patterns |
| `ISpamDetectionService` | In-memory state for spam detection patterns |
| `ISoundCacheService` | Shared audio cache |
| `IInstrumentedCache` | Application-wide memory cache |
| `IConnectionStateService` | Persistent connection state |
| `IBackgroundServiceHealthRegistry` | Tracks all background service health |
| `ILatencyHistoryService` | Maintains latency history |
| `IApiRequestTracker` | Aggregate API metrics |
| `IDatabaseMetricsCollector` | Aggregate database metrics |
| `IMemoryDiagnosticsService` | System-wide memory diagnostics |
| `ICpuSamplingService` | CPU sampling across time |
| `ICpuHistoryService` | Historical CPU data |
| `ICommandPerformanceAggregator` | Command performance across requests |
| `IPerformanceSubscriptionTracker` | WebSocket subscription tracking |
| `IDashboardUpdateService` | Aggregates dashboard updates |

### Scoped Services

Most application services are scoped:

| Service Category | Reason | Examples |
|------------------|--------|----------|
| **Repositories** | DbContext is scoped; each repo instance uses same context | `IGuildRepository`, `IUserRepository` |
| **Entity Operations** | Per-request data operations | `IGuildService`, `IUserManagementService`, `IModerationService` |
| **Business Logic** | Operates on per-request data | `ICommandLogService`, `ISearchService` |

### Lifetime Decision Tree

```
Is the service stateless and cheap to create?
    → YES → Transient (rarely used)
    → NO  → Does it need shared state?
        → YES → Is it thread-safe and memory-efficient?
            → YES → Singleton (metrics, caches, notifiers)
            → NO  → Design issue - reconsider architecture
        → NO → Scoped (DbContext-dependent, most services)
```

---

## DI Extension Architecture

Services are organized into 18 feature-based extension method files in `src/DiscordBot.Bot/Extensions/`.

### Extension Files and Responsibilities

| File | Purpose | Services Registered |
|------|---------|---------------------|
| `ServiceCollectionExtensions.cs` | Infrastructure layer | DbContext, all repositories, `ISettingsService`, `ICommandModuleConfigurationService` |
| `ApplicationServiceExtensions.cs` | Core app services | Bot, guild, user, command log, search, page metadata services |
| `DiscordServiceExtensions.cs` | Discord integration | `DiscordSocketClient`, `InteractionService`, token service, user info service |
| `IdentityServiceExtensions.cs` | Identity & OAuth | ASP.NET Identity, Discord OAuth, authorization policies |
| `LoggingServiceExtensions.cs` | Logging & audit | Message logging, audit logging, audit builder |
| `NotificationServiceExtensions.cs` | User notifications | Notification service, retention cleanup service |
| `ScheduledServicesExtensions.cs` | Scheduled operations | Reminders, scheduled messages, time parsing, welcome service |
| `VoiceServiceExtensions.cs` | Audio & voice | Soundboard, TTS, audio settings, voice notifier |
| `AssistantServiceExtensions.cs` | AI assistant | Assistant service, Anthropic client, tool registry |
| `AnalyticsServiceExtensions.cs` | Analytics | Server analytics, engagement metrics, activity aggregation services |
| `ModerationServiceExtensions.cs` | Moderation | Moderation operations, mod notes/tags, detection, watchlist |
| `PerformanceMetricsServiceExtensions.cs` | Performance monitoring | All metrics, health, latency, and CPU services |
| `RatWatchServiceExtensions.cs` | RatWatch feature | RatWatch service and status service |
| `VerificationServiceExtensions.cs` | Email verification | Verification service and cleanup |
| `WebServiceExtensions.cs` | Web infrastructure | Controllers, Razor Pages, HTTP client, memory cache |
| `SignalRServiceExtensions.cs` | Real-time updates | SignalR, dashboard notifier, dashboard update service |
| `SwaggerServiceExtensions.cs` | API documentation | Swagger/OpenAPI configuration |
| `OpenTelemetryExtensions.cs` | Observability | Distributed tracing and telemetry |
| `ElasticApmExtensions.cs` | Elastic APM | Distributed tracing via Elastic APM |

### Registration Order in Program.cs

```csharp
// Correct registration order (dependencies resolved properly):
builder.Services
    .AddInfrastructureServices(builder.Configuration)  // DbContext, repositories
    .AddApplicationServices()                          // Core services
    .AddDiscordServices(builder.Configuration)         // Discord client
    .AddIdentityServices(builder.Configuration)        // Identity & OAuth
    .AddLoggingServices()                              // Message/audit logging
    .AddNotificationServices(builder.Configuration)    // Notifications
    .AddScheduledServices(builder.Configuration)       // Reminders, messages
    .AddVoiceServices(builder.Configuration)           // Audio, TTS
    .AddAssistantServices(builder.Configuration)       // AI assistant
    .AddAnalyticsServices()                            // Analytics
    .AddModerationServices()                           // Moderation, detection
    .AddRatWatchServices()                             // RatWatch
    .AddPerformanceMetricsServices(builder.Configuration) // Metrics & health
    .AddVerificationServices()                         // Verification codes
    .AddWebServices()                                  // Web infrastructure
    .AddSignalRServices()                              // Real-time updates
    .AddSwaggerServices()                              // API docs
    .AddOpenTelemetryServices(builder.Configuration)   // Tracing
    .AddElasticApmServices(builder.Configuration);     // Elastic APM
```

### Extension Method Pattern Example

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddSingleton<IVersionService, VersionService>();
    services.AddScoped<IBotService, BotService>();
    services.AddScoped<IGuildService, GuildService>();
    services.AddScoped<IUserManagementService, UserManagementService>();
    services.AddScoped<ICommandLogService, CommandLogService>();
    services.AddScoped<ISearchService, SearchService>();

    return services;
}
```

---

## Interface Pattern Examples

### Repository Pattern

```csharp
// Generic base
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}

// Specialized example
public interface IGuildRepository : IRepository<Guild>
{
    Task<Guild?> GetByDiscordIdAsync(ulong discordId);
    Task<IReadOnlyList<Guild>> GetActiveGuildsAsync();
    Task<Guild> UpsertAsync(Guild guild);
}
```

### Service Pattern

```csharp
public interface IGuildService
{
    Task<GuildDto?> GetGuildAsync(ulong guildId);
    Task<IEnumerable<GuildDto>> GetAllGuildsAsync();
    Task<GuildDto> CreateGuildAsync(GuildCreateDto dto);
    Task UpdateGuildAsync(ulong guildId, GuildUpdateDto dto);
    Task DeleteGuildAsync(ulong guildId);
}
```

### Fluent Builder Pattern

```csharp
public interface IAuditLogBuilder
{
    IAuditLogBuilder Action(AuditLogAction action);
    IAuditLogBuilder InGuild(ulong guildId);
    IAuditLogBuilder ByUser(ulong userId);
    IAuditLogBuilder OnTarget(string targetId, string targetType);
    IAuditLogBuilder WithChange(string field, string? oldValue, string? newValue);
    IAuditLogBuilder WithMetadata(string key, object value);
    Task SaveAsync();
}
```

### Notifier Pattern

```csharp
public interface IDashboardNotifier
{
    Task NotifyGuildUpdatedAsync(ulong guildId);
    Task NotifyCommandExecutedAsync(CommandLogDto log);
    Task NotifyBotStatusChangedAsync(BotStatus status);
    Task NotifyPerformanceMetricAsync(string metric, double value);
}
```

---

## Best Practices

### DI Usage

**DO:**
- Use interface injection: `public MyService(IRepository<T> repo)`
- Register services in appropriate extension methods
- Return immutable collections: `Task<IReadOnlyList<T>>`
- Use cancellation tokens: `CancellationToken cancellationToken = default`

**DON'T:**
- Store scoped services in singletons
- Inject DbContext directly (use repositories)
- Create long-lived instances in scoped services
- Ignore proper lifetime assignment

### Singleton Services

**DO:**
- Use for stateless, thread-safe utilities
- Use for expensive-to-create resources
- Use for real-time notifiers
- Document why the service requires singleton

**DON'T:**
- Store request-specific data in singletons
- Use non-thread-safe code in singletons
- Create large objects that won't be released
- Assume singleton means thread-safe

### Service Composition

**DO:**
- Depend on interfaces, not implementations
- Keep service responsibilities focused
- Use extension methods for organization
- Add logging to service operations

**DON'T:**
- Create services with too many dependencies
- Mix concerns across services
- Hardcode dependencies
- Ignore logging and diagnostics

---

## Related Documentation

- [repository-pattern.md](repository-pattern.md) - Detailed repository patterns and examples
- [audit-log-system.md](audit-log-system.md) - Audit logging with fluent builder
- [identity-configuration.md](identity-configuration.md) - OAuth and identity setup
- [authorization-policies.md](authorization-policies.md) - Role-based authorization
- [metrics.md](metrics.md) - Performance metrics documentation
- [audio-dependencies.md](audio-dependencies.md) - Audio system requirements
- [rat-watch.md](rat-watch.md) - RatWatch feature documentation
- [reminder-system.md](reminder-system.md) - Reminder architecture
- [scheduled-messages.md](scheduled-messages.md) - Scheduled message system
- [welcome-system.md](welcome-system.md) - Welcome message system

---

*Document Version: 1.0*
*Last Updated: 2026-01-25*
*Author: Claude Documentation*
