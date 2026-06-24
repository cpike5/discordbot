# Background Services

**Version:** 1.0
**Last Updated:** 2026-01-25

## Overview

The system uses ASP.NET Core's `IHostedService` and `BackgroundService` patterns for long-running background tasks. All background services are registered in `Program.cs` via domain-specific extension methods in `src/DiscordBot.Bot/Extensions/`.

**Architecture:** Most services inherit from `MonitoredBackgroundService` (a base class that extends `BackgroundService`), which automatically registers the service with `IBackgroundServiceHealthRegistry` and provides heartbeat, status, and error-recording helpers. A handful of services (for example `BotHostedService`, `VoxClipLibraryInitializer`, `AudioCacheCleanupService`, `ElasticApmFilterRegistrationService`, and `AlertMonitoringService`) inherit from bare `BackgroundService`/`IHostedService` or implement health reporting directly. All background services are registered in `Program.cs` via the domain-specific extension methods in `src/DiscordBot.Bot/Extensions/`.

---

## Table of Contents

1. [Bot Lifecycle](#bot-lifecycle)
2. [Logging & Audit](#logging--audit)
3. [Scheduled Operations](#scheduled-operations)
4. [Audio & Voice](#audio--voice)
5. [Analytics & Aggregation](#analytics--aggregation)
6. [Performance & Monitoring](#performance--monitoring)
7. [Retention & Cleanup](#retention--cleanup)
8. [Health Monitoring](#health-monitoring)
9. [Configuration Reference](#configuration-reference)
10. [Troubleshooting](#troubleshooting)

---

## Bot Lifecycle

Services managing Discord bot connection, initialization, and lifecycle events.

### BotHostedService

**Purpose:** Manages Discord bot startup, connection, event registration, and graceful shutdown.

**Extension:** `DiscordServiceExtensions.cs`

**Lifetime:** Hosted Service (singleton-like, starts with application)

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Event-driven (no polling) |
| **Dependencies** | `DiscordSocketClient`, `InteractionService`, command modules |

**Responsibilities:**
- Initialize Discord client with gateway intents
- Register all Discord event handlers
- Load and register command modules
- Sync slash commands to Discord
- Handle graceful bot disconnection

**Startup Sequence:**
```
Application Start
  ↓
BotHostedService.StartAsync()
  ↓
Login to Discord with token
  ↓
Ready event fires
  ↓
Load command modules
  ↓
Register slash commands
  ↓
Service fully operational
```

**Configuration:**
```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN",          // Use user secrets
    "TestGuildId": 123456789,           // Optional: for instant command registration
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  }
}
```

### InteractionStateCleanupService

**Purpose:** Removes expired Discord interaction states that exceed retention time.

**Extension:** `DiscordServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `BackgroundServices:InteractionStateCleanupIntervalMinutes` (default: 1) |

**Responsibilities:**
- Monitor interaction state TTL
- Remove states older than threshold
- Log cleanup statistics

### MemberSyncService

**Purpose:** Synchronizes Discord guild members into the local database (initial full sync plus periodic reconciliation).

**Extension:** `DiscordServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Initial Delay** | `MemberSyncInitialDelayMinutes` (default: 2) |
| **Reconciliation Interval** | `MemberSyncReconciliationIntervalHours` (default: 24) |
| **Enabled** | `MemberSyncEnabled` (default: true) |

**Configuration:** Section `BackgroundServices`, bound to `BackgroundServicesOptions`.
```json
{
  "BackgroundServices": {
    "MemberSyncEnabled": true,
    "MemberSyncInitialDelayMinutes": 2,
    "MemberSyncReconciliationIntervalHours": 24,
    "MemberSyncBatchSize": 500,
    "MemberSyncApiDelayMs": 1100,
    "MemberSyncMaxRetries": 3
  }
}
```

### DiscordTokenRefreshService

**Purpose:** Proactively refreshes portal users' Discord OAuth tokens before they expire.

**Extension:** `IdentityServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `TokenRefreshIntervalMinutes` (default: 30) |
| **Expiry Threshold** | `TokenExpirationThresholdHours` (default: 1) |

**Configuration:** Section `BackgroundServices`, bound to `BackgroundServicesOptions`.
```json
{
  "BackgroundServices": {
    "TokenRefreshIntervalMinutes": 30,
    "TokenExpirationThresholdHours": 1,
    "TokenRefreshDelaySeconds": 1,
    "TokenRefreshInitialDelayMinutes": 1
  }
}
```

---

## Logging & Audit

Services for managing message logs, audit logs, and log retention.

**Cross-Reference:** See [audit-log-system.md](audit-log-system.md) for detailed audit architecture.

### MessageLogCleanupService

**Purpose:** Removes old message logs based on retention configuration.

**Extension:** `LoggingServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CleanupIntervalHours` (default: 24) |
| **Default Retention** | 90 days |
| **Batch Size** | `CleanupBatchSize` (default: 1000) |

**Configuration:** Section `MessageLogRetention`, bound to `MessageLogRetentionOptions`.
```json
{
  "MessageLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

### AuditLogQueueProcessor

**Purpose:** Processes audit log entries from an async queue for high-throughput logging.

**Extension:** `LoggingServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Continuous (no delay) |
| **Queue** | `AuditLogQueue` (configurable capacity) |
| **Batch Processing** | Enabled |

**Pattern:**
```csharp
// Main thread enqueues (fire-and-forget)
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Security)
    .WithAction(AuditLogAction.UserBanned)
    .InGuild(guildId)
    .ByUser(moderatorId)
    .Enqueue();  // Returns immediately

// Background service dequeues and persists in batches
// Protects against DB bottlenecks
```

### AuditLogRetentionService

**Purpose:** Removes old audit logs based on retention configuration.

**Extension:** `LoggingServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CleanupIntervalHours` (default: 24) |
| **Default Retention** | 90 days |
| **Batch Size** | `CleanupBatchSize` (default: 1000) |

**Configuration:** Section `AuditLogRetention`, bound to `AuditLogRetentionOptions`.
```json
{
  "AuditLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

---

## Scheduled Operations

Services for executing reminders, scheduled messages, and RatWatch voting periods.

### ReminderExecutionService

**Purpose:** Delivers due reminders to users at their specified times.

**Extension:** `ScheduledServicesExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CheckIntervalSeconds` (default: 30) |
| **Concurrency** | `MaxConcurrentDeliveries` (default: 5) |
| **Dependencies** | `IReminderRepository`, Discord client |

**Execution Flow:**
```csharp
// Runs every CheckIntervalSeconds
var dueReminders = await _reminderRepository
    .FindAsync(r => r.DueAt <= DateTime.UtcNow && !r.Delivered);

foreach (var reminder in dueReminders)
{
    await SendReminderAsync(reminder.UserId, reminder.Message);
    reminder.Delivered = true;
}
```

**Configuration:** Section `Reminder`, bound to `ReminderOptions`.
```json
{
  "Reminder": {
    "CheckIntervalSeconds": 30,
    "MaxConcurrentDeliveries": 5,
    "MaxDeliveryAttempts": 3,
    "RetryDelayMinutes": 5,
    "MaxRemindersPerUser": 25,
    "MaxAdvanceDays": 365,
    "MinAdvanceMinutes": 1
  }
}
```

### ScheduledMessageExecutionService

**Purpose:** Sends scheduled messages to channels at their configured times.

**Extension:** `ScheduledServicesExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CheckIntervalSeconds` (default: 60) |
| **Concurrency** | `MaxConcurrentExecutions` (default: 5) |
| **Dependencies** | `IScheduledMessageService`, Discord client |

**Configuration:** Section `ScheduledMessages`, bound to `ScheduledMessagesOptions`.
```json
{
  "ScheduledMessages": {
    "CheckIntervalSeconds": 60,
    "MaxConcurrentExecutions": 5,
    "ExecutionTimeoutSeconds": 30
  }
}
```

### RatWatchExecutionService

**Purpose:** Processes RatWatch voting periods and determines results.

**Extension:** `RatWatchServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CheckIntervalSeconds` (default: 30) |
| **Dependencies** | `IRatWatchService`, Discord client |

**Configuration:** Section `RatWatch`, bound to `RatWatchOptions`.
```json
{
  "RatWatch": {
    "CheckIntervalSeconds": 30,
    "MaxConcurrentExecutions": 5,
    "ExecutionTimeoutSeconds": 30,
    "DefaultVotingDurationMinutes": 5,
    "DefaultMaxAdvanceHours": 24
  }
}
```

---

## Audio & Voice

Services for audio cache maintenance, sound file cleanup, and voice channel management.

**Cross-Reference:** See [audio-dependencies.md](audio-dependencies.md) for FFmpeg and codec requirements.

### SoundPlayLogRetentionService

**Purpose:** Removes old sound play logs based on retention configuration.

**Extension:** `VoiceServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CleanupIntervalHours` (default: 24) |
| **Default Retention** | 90 days |
| **Batch Size** | `CleanupBatchSize` (default: 1000) |

**Configuration:** Section `SoundPlayLogRetention`, bound to `SoundPlayLogRetentionOptions`.
```json
{
  "SoundPlayLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

### AudioCacheCleanupService

**Purpose:** Cleans up expired audio cache entries to manage memory usage.

**Extension:** `VoiceServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | `CleanupIntervalMinutes` (default: 60) |
| **Dependency** | `ISoundCacheService` |

**Configuration:** Section `AudioCache`, bound to `AudioCacheOptions`.
```json
{
  "AudioCache": {
    "CachePath": "./cache/audio",
    "MaxCacheSizeBytes": 524288000,
    "MaxCacheDurationSeconds": 60,
    "CleanupIntervalMinutes": 60
  }
}
```

### VoiceAutoLeaveService

**Purpose:** Automatically disconnects bot from voice channels when inactive.

**Extension:** `VoiceServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CheckIntervalSeconds` (default: 30) |
| **Inactivity Timeout** | `AutoLeaveTimeoutSeconds` (default: 300) |

**Configuration:** Section `VoiceChannel`, bound to `VoiceChannelOptions`.
```json
{
  "VoiceChannel": {
    "AutoLeaveTimeoutSeconds": 300,
    "CheckIntervalSeconds": 30
  }
}
```

### VoxClipLibraryInitializer

**Purpose:** Loads and indexes the VOX (Half-Life announcer) clip library after application startup without blocking the startup path.

**Extension:** `VoiceServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service (runs once at startup) |
| **Interval** | One-shot initialization |

This service does not poll; it performs a single initialization pass and then completes.

---

## Analytics & Aggregation

Services for aggregating user activity, channel activity, and guild metrics into snapshots.

### MemberActivityAggregationService

**Purpose:** Aggregates member activity (messages, commands) into time-based snapshots.

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `BackgroundServices:HourlyAggregationIntervalMinutes` (default: 60) |
| **Granularity** | Hourly, daily, weekly, monthly |

**Snapshot Lifecycle:**
```
Raw Activity Events (real-time)
  ↓
Hourly Aggregation (1 hour after last activity)
  ↓
Daily Rollup (aggregates hourly snapshots)
  ↓
Weekly Rollup (aggregates daily snapshots)
  ↓
Monthly Rollup (aggregates weekly snapshots)
  ↓
Old snapshots deleted per retention policy
```

### ChannelActivityAggregationService

**Purpose:** Aggregates channel activity (messages, reactions) into snapshots.

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `BackgroundServices:HourlyAggregationIntervalMinutes` (default: 60) |
| **Granularity** | Hourly, daily, weekly, monthly |

### GuildMetricsAggregationService

**Purpose:** Aggregates guild-wide metrics (active members, total messages, commands).

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `BackgroundServices:DailyAggregationIntervalMinutes` (default: 1440) |
| **Scope** | Per-guild aggregation |

### AnalyticsRetentionService

**Purpose:** Removes old analytics snapshots based on granularity-specific retention.

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CleanupIntervalHours` (default: 24) |

**Configuration:** Section `AnalyticsRetention`, bound to `AnalyticsRetentionOptions`.
```json
{
  "AnalyticsRetention": {
    "HourlyRetentionDays": 14,
    "DailyRetentionDays": 365,
    "Enabled": true,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24
  }
}
```

---

## Performance & Monitoring

Services for collecting system metrics, tracking alerts, and broadcasting performance data.

**Cross-Reference:** See [metrics.md](metrics.md) for performance metrics architecture.

### AlertMonitoringService

**Purpose:** Monitors metrics against configured alert thresholds and triggers alerts.

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service (implements `IBackgroundServiceHealth`) |
| **Interval** | `CheckIntervalSeconds` (default: 30) |
| **Breach Hysteresis** | `ConsecutiveBreachesRequired` / `ConsecutiveNormalRequired` |

**Configuration:** Section `PerformanceAlerts`, bound to `PerformanceAlertOptions`.
```json
{
  "PerformanceAlerts": {
    "CheckIntervalSeconds": 30,
    "ConsecutiveBreachesRequired": 2,
    "ConsecutiveNormalRequired": 3,
    "IncidentRetentionDays": 90
  }
}
```

### MetricsCollectionService

**Purpose:** Collects system metrics (CPU, memory, database queries, API calls).

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `SampleIntervalSeconds` (default: 60) |
| **Metrics Collected** | System health metrics persisted to the database |

**Configuration:** Section `HistoricalMetrics`, bound to `HistoricalMetricsOptions`.
```json
{
  "HistoricalMetrics": {
    "SampleIntervalSeconds": 60,
    "RetentionDays": 30,
    "Enabled": true,
    "CleanupIntervalHours": 6,
    "InitialDelaySeconds": 10,
    "ErrorRetryDelaySeconds": 30
  }
}
```

### MetricsUpdateService

**Purpose:** Periodically refreshes observable gauge metrics (e.g. active guild count, estimated unique users).

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `MetricsUpdateIntervalSeconds` (default: 30) |

**Configuration:** Section `BackgroundServices`, bound to `BackgroundServicesOptions`.
```json
{
  "BackgroundServices": {
    "MetricsUpdateIntervalSeconds": 30
  }
}
```

### BusinessMetricsUpdateService

**Purpose:** Updates business metrics (commands executed, guilds active, users online).

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `BusinessMetricsUpdateIntervalMinutes` (default: 5) |
| **Metrics** | Business and SLO metrics |

**Configuration:** Section `BackgroundServices`, bound to `BackgroundServicesOptions`.
```json
{
  "BackgroundServices": {
    "BusinessMetricsUpdateIntervalMinutes": 5,
    "BusinessMetricsInitialDelaySeconds": 30
  }
}
```

### PerformanceMetricsBroadcastService

**Purpose:** Broadcasts real-time metrics to connected dashboard clients via SignalR.

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | Per-stream timers (health/command/system) |
| **Hub** | DashboardHub |

**Configuration:** Section `PerformanceBroadcast`, bound to `PerformanceBroadcastOptions`.
```json
{
  "PerformanceBroadcast": {
    "HealthMetricsIntervalSeconds": 5,
    "CommandMetricsIntervalSeconds": 30,
    "SystemMetricsIntervalSeconds": 10,
    "Enabled": true
  }
}
```

### CpuSamplingService

**Purpose:** Samples process CPU usage at a fixed interval (via processor-time deltas) and records it to the metrics history.

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CpuSampleIntervalSeconds` (default: 5) |

**Configuration:** Section `PerformanceMetrics`, bound to `PerformanceMetricsOptions`.
```json
{
  "PerformanceMetrics": {
    "CpuSampleIntervalSeconds": 5,
    "CpuRetentionHours": 24
  }
}
```

### CommandPerformanceAggregator

**Purpose:** Periodically aggregates command performance metrics from the command log and caches the results for dashboard queries.

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Cache TTL** | `CommandAggregationCacheTtlMinutes` (default: 5) |

**Configuration:** Section `PerformanceMetrics`, bound to `PerformanceMetricsOptions`.
```json
{
  "PerformanceMetrics": {
    "CommandAggregationCacheTtlMinutes": 5
  }
}
```

### ElasticApmFilterRegistrationService

**Purpose:** Registers the priority-based transaction sampling filter with the Elastic APM agent after startup.

**Extension:** `ElasticApmExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service (runs once at startup) |
| **Interval** | One-shot registration |

This service performs a single registration pass once the APM agent is available; it does not poll. Sampling rates are configured via the `Sampling` section (`SamplingOptions`).

---

## Retention & Cleanup

Summary of all data retention services and their cleanup schedules.

### Retention Services Overview

| Service | Target Data | Default Retention | Interval | Configuration Section |
|---------|-------------|-------------------|----------|----------------------|
| `MessageLogCleanupService` | Message logs | 90 days | 24 hours | `MessageLogRetention` |
| `AuditLogRetentionService` | Audit logs | 90 days | 24 hours | `AuditLogRetention` |
| `NotificationRetentionService` | User notifications | 7 days (dismissed) | 24 hours | `NotificationRetention` |
| `SoundPlayLogRetentionService` | Sound play logs | 90 days | 24 hours | `SoundPlayLogRetention` |
| `AnalyticsRetentionService` | Analytics snapshots | 14 days hourly / 365 days daily | 24 hours | `AnalyticsRetention` |
| `VerificationCleanupService` | Verification codes | Code-expiry based | 5 minutes | `BackgroundServices` |

### NotificationRetentionService

**Purpose:** Removes old user notification records based on status.

**Extension:** `NotificationServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `CleanupIntervalHours` (default: 24) |

**Configuration:** Section `NotificationRetention`, bound to `NotificationRetentionOptions`.
```json
{
  "NotificationRetention": {
    "DismissedRetentionDays": 7,
    "ReadRetentionDays": 30,
    "UnreadRetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

### VerificationCleanupService

**Purpose:** Removes expired verification codes (email, phone).

**Extension:** `VerificationServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Monitored Background Service |
| **Interval** | `VerificationCleanupIntervalMinutes` (default: 5) |
| **Code TTL** | Determined by each verification code's expiry timestamp |

**Configuration:** Section `BackgroundServices`, bound to `BackgroundServicesOptions`.
```json
{
  "BackgroundServices": {
    "VerificationCleanupIntervalMinutes": 5
  }
}
```

### Batch Processing Pattern

All retention services follow this common pattern:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            int deletedCount = 0;
            DateTime cutoffDate = DateTime.UtcNow
                .AddDays(-_options.RetentionDays);

            // Process in batches to avoid memory issues
            while (true)
            {
                var batch = await _repository
                    .FindAsync(x => x.CreatedAt < cutoffDate)
                    .Take(_options.BatchSize)
                    .ToListAsync(stoppingToken);

                if (batch.Count == 0)
                    break;

                foreach (var item in batch)
                {
                    await _repository.DeleteAsync(item);
                }

                deletedCount += batch.Count;
            }

            _logger.LogInformation(
                "{ServiceName} deleted {DeletedCount} records",
                nameof(MessageLogCleanupService),
                deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during retention cleanup");
        }

        await Task.Delay(
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
            stoppingToken);
    }
}
```

---

## Health Monitoring

All background services report health status via `IBackgroundServiceHealthRegistry`.

### IBackgroundServiceHealth Interface

```csharp
public interface IBackgroundServiceHealth
{
    string ServiceName { get; }
    bool IsHealthy { get; }
    DateTime? LastExecutionTime { get; }
    TimeSpan? LastExecutionDuration { get; }
    string? LastError { get; }
}
```

### Health Registry

**Service:** `IBackgroundServiceHealthRegistry` (Singleton)

**Purpose:** Tracks all background service health status.

**Location:** Accessible via Performance Dashboard at `/Admin/Performance/HealthMetrics`

**Monitoring:**
- Automatic exception logging when service fails
- Last execution time and duration tracking
- Dashboard display of service status
- Alerts triggered on repeated failures

**Usage Example:**
```csharp
var health = _healthRegistry.GetServiceHealth("MessageLogCleanupService");

if (!health.IsHealthy)
{
    _logger.LogWarning(
        "Service {Name} unhealthy. Last error: {Error}",
        health.ServiceName,
        health.LastError);
}
```

---

## Configuration Reference

### Quick Reference Table

| Service | Config Section | Key Settings | Extension |
|---------|----------------|--------------|-----------|
| `BotHostedService` | `Discord` | `Token`, `TestGuildId`, `ClientId` | DiscordServiceExtensions |
| `InteractionStateCleanupService` | `BackgroundServices` | `InteractionStateCleanupIntervalMinutes` | DiscordServiceExtensions |
| `MemberSyncService` | `BackgroundServices` | `MemberSyncEnabled`, `MemberSyncReconciliationIntervalHours` | DiscordServiceExtensions |
| `DiscordTokenRefreshService` | `BackgroundServices` | `TokenRefreshIntervalMinutes`, `TokenExpirationThresholdHours` | IdentityServiceExtensions |
| `MessageLogCleanupService` | `MessageLogRetention` | `RetentionDays`, `CleanupIntervalHours`, `CleanupBatchSize` | LoggingServiceExtensions |
| `AuditLogQueueProcessor` | Built-in | (async queue) | LoggingServiceExtensions |
| `AuditLogRetentionService` | `AuditLogRetention` | `RetentionDays`, `CleanupIntervalHours`, `CleanupBatchSize` | LoggingServiceExtensions |
| `NotificationRetentionService` | `NotificationRetention` | `*RetentionDays`, `CleanupIntervalHours` | NotificationServiceExtensions |
| `ReminderExecutionService` | `Reminder` | `CheckIntervalSeconds`, `MaxConcurrentDeliveries` | ScheduledServicesExtensions |
| `ScheduledMessageExecutionService` | `ScheduledMessages` | `CheckIntervalSeconds`, `MaxConcurrentExecutions` | ScheduledServicesExtensions |
| `RatWatchExecutionService` | `RatWatch` | `CheckIntervalSeconds`, `DefaultVotingDurationMinutes` | RatWatchServiceExtensions |
| `SoundPlayLogRetentionService` | `SoundPlayLogRetention` | `RetentionDays`, `CleanupIntervalHours`, `CleanupBatchSize` | VoiceServiceExtensions |
| `AudioCacheCleanupService` | `AudioCache` | `MaxCacheDurationSeconds`, `CleanupIntervalMinutes` | VoiceServiceExtensions |
| `VoiceAutoLeaveService` | `VoiceChannel` | `AutoLeaveTimeoutSeconds`, `CheckIntervalSeconds` | VoiceServiceExtensions |
| `VoxClipLibraryInitializer` | (one-shot) | n/a | VoiceServiceExtensions |
| `MemberActivityAggregationService` | `BackgroundServices` | `HourlyAggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `ChannelActivityAggregationService` | `BackgroundServices` | `HourlyAggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `GuildMetricsAggregationService` | `BackgroundServices` | `DailyAggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `AnalyticsRetentionService` | `AnalyticsRetention` | `HourlyRetentionDays`, `DailyRetentionDays`, `CleanupIntervalHours` | AnalyticsServiceExtensions |
| `AlertMonitoringService` | `PerformanceAlerts` | `CheckIntervalSeconds`, `ConsecutiveBreachesRequired` | PerformanceMetricsServiceExtensions |
| `MetricsCollectionService` | `HistoricalMetrics` | `SampleIntervalSeconds`, `RetentionDays`, `CleanupIntervalHours` | PerformanceMetricsServiceExtensions |
| `MetricsUpdateService` | `BackgroundServices` | `MetricsUpdateIntervalSeconds` | PerformanceMetricsServiceExtensions |
| `BusinessMetricsUpdateService` | `BackgroundServices` | `BusinessMetricsUpdateIntervalMinutes` | PerformanceMetricsServiceExtensions |
| `PerformanceMetricsBroadcastService` | `PerformanceBroadcast` | `HealthMetricsIntervalSeconds`, `Enabled` | PerformanceMetricsServiceExtensions |
| `CpuSamplingService` | `PerformanceMetrics` | `CpuSampleIntervalSeconds` | PerformanceMetricsServiceExtensions |
| `CommandPerformanceAggregator` | `PerformanceMetrics` | `CommandAggregationCacheTtlMinutes` | PerformanceMetricsServiceExtensions |
| `ElasticApmFilterRegistrationService` | `Sampling` | (one-shot registration) | ElasticApmExtensions |
| `VerificationCleanupService` | `BackgroundServices` | `VerificationCleanupIntervalMinutes` | VerificationServiceExtensions |

### Complete Configuration Example

```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN",
    "TestGuildId": 123456789,
    "ClientId": "YOUR_CLIENT_ID"
  },
  "BackgroundServices": {
    "TokenRefreshIntervalMinutes": 30,
    "TokenExpirationThresholdHours": 1,
    "VerificationCleanupIntervalMinutes": 5,
    "InteractionStateCleanupIntervalMinutes": 1,
    "MetricsUpdateIntervalSeconds": 30,
    "BusinessMetricsUpdateIntervalMinutes": 5,
    "MemberSyncEnabled": true,
    "MemberSyncReconciliationIntervalHours": 24,
    "HourlyAggregationIntervalMinutes": 60,
    "DailyAggregationIntervalMinutes": 1440
  },
  "MessageLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  },
  "AuditLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  },
  "NotificationRetention": {
    "DismissedRetentionDays": 7,
    "ReadRetentionDays": 30,
    "UnreadRetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  },
  "SoundPlayLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  },
  "AudioCache": {
    "CachePath": "./cache/audio",
    "MaxCacheSizeBytes": 524288000,
    "MaxCacheDurationSeconds": 60,
    "CleanupIntervalMinutes": 60
  },
  "VoiceChannel": {
    "AutoLeaveTimeoutSeconds": 300,
    "CheckIntervalSeconds": 30
  },
  "AnalyticsRetention": {
    "HourlyRetentionDays": 14,
    "DailyRetentionDays": 365,
    "Enabled": true,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24
  },
  "PerformanceAlerts": {
    "CheckIntervalSeconds": 30,
    "ConsecutiveBreachesRequired": 2,
    "ConsecutiveNormalRequired": 3,
    "IncidentRetentionDays": 90
  },
  "HistoricalMetrics": {
    "SampleIntervalSeconds": 60,
    "RetentionDays": 30,
    "Enabled": true,
    "CleanupIntervalHours": 6
  },
  "PerformanceMetrics": {
    "CpuSampleIntervalSeconds": 5,
    "CpuRetentionHours": 24,
    "CommandAggregationCacheTtlMinutes": 5
  },
  "PerformanceBroadcast": {
    "HealthMetricsIntervalSeconds": 5,
    "CommandMetricsIntervalSeconds": 30,
    "SystemMetricsIntervalSeconds": 10,
    "Enabled": true
  },
  "Reminder": {
    "CheckIntervalSeconds": 30,
    "MaxConcurrentDeliveries": 5,
    "MaxDeliveryAttempts": 3
  },
  "ScheduledMessages": {
    "CheckIntervalSeconds": 60,
    "MaxConcurrentExecutions": 5,
    "ExecutionTimeoutSeconds": 30
  },
  "RatWatch": {
    "CheckIntervalSeconds": 30,
    "DefaultVotingDurationMinutes": 5,
    "DefaultMaxAdvanceHours": 24
  }
}
```

---

## Troubleshooting

### Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Service not starting | Configuration missing | Verify config section exists in appsettings.json |
| Service stops unexpectedly | Unhandled exception | Check logs for exception details, add try-catch |
| High memory usage | Large batch sizes | Reduce `CleanupBatchSize` in retention config |
| Slow retention cleanup | Large datasets + small batch | Increase `CleanupBatchSize` (test first) |
| Alerts not firing | Service unhealthy | Check `IBackgroundServiceHealthRegistry` on dashboard |
| Reminders not delivered | Check interval too long | Reduce `Reminder:CheckIntervalSeconds` |
| Scheduled messages missed | Check interval too long | Reduce `ScheduledMessages:CheckIntervalSeconds` |
| Analytics not aggregating | Service not running | Check health dashboard, verify configuration |

### Checking Service Health

**Via Dashboard:** Navigate to `/Admin/Performance/HealthMetrics`

**Via Code:**
```csharp
public class DiagnosticsController : ControllerBase
{
    private readonly IBackgroundServiceHealthRegistry _healthRegistry;

    public DiagnosticsController(IBackgroundServiceHealthRegistry healthRegistry)
    {
        _healthRegistry = healthRegistry;
    }

    [HttpGet("health")]
    public IActionResult GetServiceHealth()
    {
        var services = _healthRegistry.GetAllServiceHealth();
        var unhealthy = services.Where(s => !s.IsHealthy).ToList();

        if (unhealthy.Any())
        {
            return BadRequest(new
            {
                healthy = services.Count - unhealthy.Count,
                unhealthy = unhealthy.Count,
                details = unhealthy.Select(s => new
                {
                    s.ServiceName,
                    s.LastError,
                    s.LastExecutionTime
                })
            });
        }

        return Ok(new { status = "all services healthy" });
    }
}
```

### Common Configuration Errors

**Error:** Service never executes
```
→ Check: Is CleanupIntervalHours (or the relevant interval key) set to 0?
→ Fix: Set to a reasonable value (e.g., 24)
```

**Error:** Rapid service failures
```
→ Check: Is CleanupBatchSize too large?
→ Fix: Reduce CleanupBatchSize and spread load
```

**Error:** High CPU usage
```
→ Check: Are intervals too short?
→ Fix: Increase interval (e.g., 300 → 600 seconds)
```

**Error:** Database locks
```
→ Check: Are retention services running simultaneously?
→ Fix: Stagger intervals to prevent overlap
```

---

## Best Practices

### Service Implementation

**DO:**
- Use the `MonitoredBackgroundService` base class (wires up health-registry reporting automatically)
- Implement proper exception handling
- Report health status
- Use cancellation tokens
- Log important events
- Honor configured intervals

**DON'T:**
- Throw unhandled exceptions (crashes service)
- Ignore cancellation tokens (prevents graceful shutdown)
- Use blocking operations (use async/await)
- Make assumptions about execution order
- Hard-code intervals (use configuration)

### Configuration

**DO:**
- Use reasonable default intervals (60+ seconds for cleanup)
- Make batch sizes configurable
- Allow enabling/disabling services
- Document all configuration options
- Test configuration changes before deploying

**DON'T:**
- Set intervals too short (high CPU usage)
- Set batch sizes too large (memory spikes)
- Leave services running unnecessarily
- Assume users will configure correctly

### Monitoring

**DO:**
- Check health dashboard regularly
- Set up alerts for service failures
- Monitor memory and CPU usage
- Review logs for errors
- Test retention policies

**DON'T:**
- Ignore service failures
- Assume services are running without verification
- Set retention too short (data loss)
- Skip testing configuration changes

---

## Related Documentation

- [audit-log-system.md](audit-log-system.md) - Audit logging architecture
- [metrics.md](metrics.md) - Performance metrics system
- [audio-dependencies.md](audio-dependencies.md) - Audio system requirements
- [rat-watch.md](rat-watch.md) - RatWatch feature documentation
- [reminder-system.md](reminder-system.md) - Reminder architecture
- [scheduled-messages.md](scheduled-messages.md) - Scheduled message system
- [service-architecture.md](service-architecture.md) - Service interface documentation

---

*Document Version: 1.0*
*Last Updated: 2026-01-25*
*Author: Claude Documentation*
