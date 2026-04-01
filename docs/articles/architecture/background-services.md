# Background Services

**Version:** 1.0
**Last Updated:** 2026-01-25

## Overview

The system uses ASP.NET Core's `IHostedService` and `BackgroundService` patterns for long-running background tasks. All background services are registered in `Program.cs` via domain-specific extension methods in `src/DiscordBot.Bot/Extensions/`.

**Architecture:** Services inherit from `BackgroundService`, execute on separate threads, and report health via `IBackgroundServiceHealthRegistry`.

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
| **Type** | Background Service |
| **Interval** | Periodic (5 minutes) |
| **Batch Size** | 1000 states per cleanup |

**Responsibilities:**
- Monitor interaction state TTL
- Remove states older than threshold
- Log cleanup statistics

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
| **Type** | Background Service |
| **Interval** | Configurable (default: hourly) |
| **Default Retention** | 30 days |
| **Batch Size** | Configurable |

**Configuration:**
```json
{
  "MessageLogRetention": {
    "RetentionDays": 30,
    "CleanupIntervalMinutes": 60,
    "BatchSize": 1000,
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
// Main thread enqueues
await _auditLog.Action(AuditLogAction.UserBanned)
    .InGuild(guildId)
    .ByUser(moderatorId)
    .SaveAsync();  // Enqueued, returns immediately

// Background service dequeues and persists
// Protects against DB bottlenecks
```

### AuditLogRetentionService

**Purpose:** Removes old audit logs based on retention configuration.

**Extension:** `LoggingServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: daily) |
| **Default Retention** | 90 days |
| **Batch Size** | Configurable |

**Configuration:**
```json
{
  "AuditLogRetention": {
    "RetentionDays": 90,
    "CleanupIntervalMinutes": 1440,
    "BatchSize": 1000,
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
| **Type** | Background Service |
| **Interval** | Every 30 seconds |
| **Batch Size** | Up to 100 reminders per check |
| **Dependencies** | `IReminderRepository`, Discord client |

**Execution Flow:**
```csharp
// Runs every 30 seconds
var dueReminders = await _reminderRepository
    .FindAsync(r => r.DueAt <= DateTime.UtcNow && !r.Delivered);

foreach (var reminder in dueReminders)
{
    await SendReminderAsync(reminder.UserId, reminder.Message);
    reminder.Delivered = true;
}
```

**Configuration:**
```json
{
  "Reminders": {
    "ExecutionIntervalSeconds": 30,
    "MaxRemindersPerBatch": 100,
    "DmNotificationEnabled": true
  }
}
```

### ScheduledMessageExecutionService

**Purpose:** Sends scheduled messages to channels at their configured times.

**Extension:** `ScheduledServicesExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Every 60 seconds |
| **Batch Size** | Up to 50 messages per check |
| **Dependencies** | `IScheduledMessageService`, Discord client |

**Configuration:**
```json
{
  "ScheduledMessages": {
    "ExecutionIntervalSeconds": 60,
    "MaxMessagesPerBatch": 50,
    "TimezoneAware": true
  }
}
```

### RatWatchExecutionService

**Purpose:** Processes RatWatch voting periods and determines results.

**Extension:** `RatWatchServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: 5 minutes) |
| **Dependencies** | `IRatWatchService`, Discord client |

**Configuration:**
```json
{
  "RatWatch": {
    "ExecutionIntervalSeconds": 300,
    "VotingPeriodMinutes": 60,
    "MinimumVotesRequired": 5
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
| **Type** | Background Service |
| **Interval** | Configurable (default: daily) |
| **Default Retention** | 30 days |
| **Batch Size** | 1000 logs |

**Configuration:**
```json
{
  "SoundPlayLogRetention": {
    "RetentionDays": 30,
    "CleanupIntervalMinutes": 1440,
    "BatchSize": 1000,
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
| **Interval** | Configurable (default: 15 minutes) |
| **Dependency** | `ISoundCacheService` |

**Configuration:**
```json
{
  "AudioCache": {
    "MaxCacheSizeMb": 500,
    "CacheExpirationMinutes": 60,
    "CleanupIntervalMinutes": 15
  }
}
```

### VoiceAutoLeaveService

**Purpose:** Automatically disconnects bot from voice channels when inactive.

**Extension:** `VoiceServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: every 60 seconds) |
| **Inactivity Timeout** | Configurable (default: 5 minutes) |

**Configuration:**
```json
{
  "VoiceChannel": {
    "AutoLeaveEnabled": true,
    "AutoLeaveDelaySeconds": 300,
    "CheckIntervalSeconds": 60
  }
}
```

---

## Analytics & Aggregation

Services for aggregating user activity, channel activity, and guild metrics into snapshots.

### MemberActivityAggregationService

**Purpose:** Aggregates member activity (messages, commands) into time-based snapshots.

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: hourly) |
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
| **Type** | Background Service |
| **Interval** | Configurable (default: hourly) |
| **Granularity** | Hourly, daily, weekly, monthly |

### GuildMetricsAggregationService

**Purpose:** Aggregates guild-wide metrics (active members, total messages, commands).

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: daily) |
| **Scope** | Per-guild aggregation |

### AnalyticsRetentionService

**Purpose:** Removes old analytics snapshots based on granularity-specific retention.

**Extension:** `AnalyticsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: daily) |

**Configuration:**
```json
{
  "AnalyticsRetention": {
    "HourlyRetentionDays": 7,
    "DailyRetentionDays": 30,
    "WeeklyRetentionDays": 90,
    "MonthlyRetentionDays": 365,
    "AggregationIntervalMinutes": 60
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
| **Type** | Background Service |
| **Interval** | Configurable (default: 30 seconds) |
| **Cooldown** | Prevents alert spam (default: 5 minutes) |

**Configuration:**
```json
{
  "PerformanceAlerts": {
    "Enabled": true,
    "CheckIntervalSeconds": 30,
    "AlertCooldownMinutes": 5
  }
}
```

### MetricsCollectionService

**Purpose:** Collects system metrics (CPU, memory, database queries, API calls).

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | 60 seconds |
| **Metrics Collected** | CPU, memory, DB time, API latency |

**Configuration:**
```json
{
  "PerformanceMetrics": {
    "CollectionIntervalSeconds": 60,
    "EnableDetailedMetrics": true
  }
}
```

### MetricsUpdateService

**Purpose:** Pushes collected metrics to the dashboard via SignalR.

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | 5 seconds |
| **Target** | Dashboard subscribers |

**Configuration:**
```json
{
  "PerformanceMetrics": {
    "UpdateIntervalSeconds": 5
  }
}
```

### BusinessMetricsUpdateService

**Purpose:** Updates business metrics (commands executed, guilds active, users online).

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: 5 minutes) |
| **Metrics** | Commands, guilds, users, activity rates |

**Configuration:**
```json
{
  "PerformanceMetrics": {
    "BusinessMetricsIntervalSeconds": 300
  }
}
```

### PerformanceMetricsBroadcastService

**Purpose:** Broadcasts real-time metrics to connected dashboard clients via SignalR.

**Extension:** `PerformanceMetricsServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | 5 seconds |
| **Hub** | DashboardHub |

**Configuration:**
```json
{
  "PerformanceBroadcast": {
    "Enabled": true,
    "IntervalSeconds": 5
  }
}
```

---

## Retention & Cleanup

Summary of all data retention services and their cleanup schedules.

### Retention Services Overview

| Service | Target Data | Default Retention | Interval | Configuration Section |
|---------|-------------|-------------------|----------|----------------------|
| `MessageLogCleanupService` | Message logs | 30 days | 60 minutes | `MessageLogRetention` |
| `AuditLogRetentionService` | Audit logs | 90 days | 1440 minutes (daily) | `AuditLogRetention` |
| `NotificationRetentionService` | User notifications | 30 days (dismissed) | 1440 minutes | `NotificationRetention` |
| `SoundPlayLogRetentionService` | Sound play logs | 30 days | 1440 minutes | `SoundPlayLogRetention` |
| `AnalyticsRetentionService` | Analytics snapshots | Varies by granularity | 1440 minutes | `AnalyticsRetention` |
| `VerificationCleanupService` | Verification codes | 24 hours | 60 minutes | `Verification` |

### NotificationRetentionService

**Purpose:** Removes old user notification records based on status.

**Extension:** `NotificationServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: daily) |

**Configuration:**
```json
{
  "NotificationRetention": {
    "DismissedRetentionDays": 30,
    "ReadRetentionDays": 90,
    "UnreadRetentionDays": 365,
    "CleanupIntervalMinutes": 1440
  }
}
```

### VerificationCleanupService

**Purpose:** Removes expired verification codes (email, phone).

**Extension:** `VerificationServiceExtensions.cs`

**Lifetime:** Hosted Service

| Property | Value |
|----------|-------|
| **Type** | Background Service |
| **Interval** | Configurable (default: hourly) |
| **Code TTL** | 24 hours |

**Configuration:**
```json
{
  "Verification": {
    "CodeExpirationMinutes": 1440,
    "CleanupIntervalMinutes": 60
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
| `InteractionStateCleanupService` | Built-in | (hardcoded 5 min) | DiscordServiceExtensions |
| `MessageLogCleanupService` | `MessageLogRetention` | `RetentionDays`, `CleanupIntervalMinutes` | LoggingServiceExtensions |
| `AuditLogQueueProcessor` | Built-in | (async queue) | LoggingServiceExtensions |
| `AuditLogRetentionService` | `AuditLogRetention` | `RetentionDays`, `CleanupIntervalMinutes` | LoggingServiceExtensions |
| `NotificationRetentionService` | `NotificationRetention` | `*RetentionDays`, `CleanupIntervalMinutes` | NotificationServiceExtensions |
| `ReminderExecutionService` | `Reminders` | `ExecutionIntervalSeconds` | ScheduledServicesExtensions |
| `ScheduledMessageExecutionService` | `ScheduledMessages` | `ExecutionIntervalSeconds` | ScheduledServicesExtensions |
| `RatWatchExecutionService` | `RatWatch` | `ExecutionIntervalSeconds`, `VotingPeriodMinutes` | RatWatchServiceExtensions |
| `SoundPlayLogRetentionService` | `SoundPlayLogRetention` | `RetentionDays`, `CleanupIntervalMinutes` | VoiceServiceExtensions |
| `AudioCacheCleanupService` | `AudioCache` | `CacheExpirationMinutes`, `CleanupIntervalMinutes` | VoiceServiceExtensions |
| `VoiceAutoLeaveService` | `VoiceChannel` | `AutoLeaveDelaySeconds`, `CheckIntervalSeconds` | VoiceServiceExtensions |
| `MemberActivityAggregationService` | `AnalyticsRetention` | `AggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `ChannelActivityAggregationService` | `AnalyticsRetention` | `AggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `GuildMetricsAggregationService` | `AnalyticsRetention` | `AggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `AnalyticsRetentionService` | `AnalyticsRetention` | `*RetentionDays`, `AggregationIntervalMinutes` | AnalyticsServiceExtensions |
| `AlertMonitoringService` | `PerformanceAlerts` | `CheckIntervalSeconds`, `AlertCooldownMinutes` | PerformanceMetricsServiceExtensions |
| `MetricsCollectionService` | `PerformanceMetrics` | `CollectionIntervalSeconds` | PerformanceMetricsServiceExtensions |
| `MetricsUpdateService` | `PerformanceMetrics` | `UpdateIntervalSeconds` | PerformanceMetricsServiceExtensions |
| `BusinessMetricsUpdateService` | `PerformanceMetrics` | `BusinessMetricsIntervalSeconds` | PerformanceMetricsServiceExtensions |
| `PerformanceMetricsBroadcastService` | `PerformanceBroadcast` | `IntervalSeconds`, `Enabled` | PerformanceMetricsServiceExtensions |
| `VerificationCleanupService` | `Verification` | `CodeExpirationMinutes`, `CleanupIntervalMinutes` | VerificationServiceExtensions |

### Complete Configuration Example

```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN",
    "TestGuildId": 123456789,
    "ClientId": "YOUR_CLIENT_ID"
  },
  "MessageLogRetention": {
    "RetentionDays": 30,
    "CleanupIntervalMinutes": 60,
    "BatchSize": 1000,
    "Enabled": true
  },
  "AuditLogRetention": {
    "RetentionDays": 90,
    "CleanupIntervalMinutes": 1440,
    "BatchSize": 1000,
    "Enabled": true
  },
  "NotificationRetention": {
    "DismissedRetentionDays": 30,
    "ReadRetentionDays": 90,
    "UnreadRetentionDays": 365,
    "CleanupIntervalMinutes": 1440
  },
  "AudioCache": {
    "MaxCacheSizeMb": 500,
    "CacheExpirationMinutes": 60,
    "CleanupIntervalMinutes": 15
  },
  "VoiceChannel": {
    "AutoLeaveEnabled": true,
    "AutoLeaveDelaySeconds": 300,
    "CheckIntervalSeconds": 60
  },
  "AnalyticsRetention": {
    "HourlyRetentionDays": 7,
    "DailyRetentionDays": 30,
    "WeeklyRetentionDays": 90,
    "MonthlyRetentionDays": 365,
    "AggregationIntervalMinutes": 60
  },
  "PerformanceAlerts": {
    "Enabled": true,
    "CheckIntervalSeconds": 30,
    "AlertCooldownMinutes": 5
  },
  "PerformanceMetrics": {
    "CollectionIntervalSeconds": 60,
    "UpdateIntervalSeconds": 5,
    "BusinessMetricsIntervalSeconds": 300,
    "EnableDetailedMetrics": true
  },
  "PerformanceBroadcast": {
    "Enabled": true,
    "IntervalSeconds": 5
  },
  "Reminders": {
    "ExecutionIntervalSeconds": 30,
    "MaxRemindersPerBatch": 100
  },
  "ScheduledMessages": {
    "ExecutionIntervalSeconds": 60,
    "MaxMessagesPerBatch": 50
  },
  "RatWatch": {
    "ExecutionIntervalSeconds": 300,
    "VotingPeriodMinutes": 60
  },
  "Verification": {
    "CodeExpirationMinutes": 1440,
    "CleanupIntervalMinutes": 60
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
| High memory usage | Large batch sizes | Reduce `BatchSize` in retention config |
| Slow retention cleanup | Large datasets + small batch | Increase `BatchSize` (test first) |
| Alerts not firing | Service unhealthy | Check `IBackgroundServiceHealthRegistry` on dashboard |
| Reminders not delivered | Execution interval too long | Reduce `ExecutionIntervalSeconds` |
| Scheduled messages missed | Interval too long | Reduce `ExecutionIntervalSeconds` |
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
→ Check: Is CleanupIntervalMinutes set to 0?
→ Fix: Set to reasonable value (e.g., 60)
```

**Error:** Rapid service failures
```
→ Check: Is BatchSize too large?
→ Fix: Reduce BatchSize and spread load
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
- Use `BackgroundService` base class
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
