# Environment-Specific Configuration

This document describes the environment-specific configuration files and their intended use for Development, Staging, and Production environments.

## Quick Reference

| Section | Class | Key Settings |
|---------|-------|--------------|
| `Application` | `ApplicationOptions` | Title, BaseUrl, ContactEmail |
| `Discord` | `DiscordOAuthOptions` | ClientId, ClientSecret, Token |
| `AudioCache` | `AudioCacheOptions` | MaxCacheSizeMb, CacheExpirationMinutes |
| `Soundboard` | `SoundboardOptions` | MaxFileSizeMb, AllowedExtensions, MaxSoundsPerGuild |
| `VoiceChannel` | `VoiceChannelOptions` | AutoDisconnectSeconds, MaxQueueSize, DefaultVolume |
| `AzureSpeech` | `AzureSpeechOptions` | SubscriptionKey, Region, DefaultVoice |
| `Moderation` | `ModerationOptions` | DefaultMuteDurationMinutes, MaxWarnBeforeBan, LogRetentionDays |
| `AutoModeration` | `AutoModerationOptions` | Enabled, SpamThreshold, RaidJoinThreshold |
| `RatWatch` | `RatWatchOptions` | DefaultDurationHours, VoteThreshold, CooldownHours |
| `Reminders` | `ReminderOptions` | MaxRemindersPerUser, MinimumIntervalMinutes, MaxFutureYears |
| `ScheduledMessages` | `ScheduledMessagesOptions` | MaxMessagesPerGuild, MinimumIntervalMinutes |
| `AnalyticsRetention` | `AnalyticsRetentionOptions` | HourlySnapshotDays, DailySnapshotDays, WeeklySnapshotDays |
| `AuditLogRetention` | `AuditLogRetentionOptions` | RetentionDays, CleanupIntervalHours |
| `MessageLogRetention` | `MessageLogRetentionOptions` | RetentionDays, CleanupIntervalHours |
| `SoundPlayLogRetention` | `SoundPlayLogRetentionOptions` | RetentionDays, CleanupIntervalHours |
| `UserActivityEventRetention` | `UserActivityEventRetentionOptions` | RetentionDays, CleanupIntervalHours |
| `NotificationRetention` | `NotificationRetentionOptions` | RetentionDays, CleanupIntervalHours |
| `PerformanceMetrics` | `PerformanceMetricsOptions` | CollectionIntervalSeconds, HistoryRetentionMinutes |
| `PerformanceAlerts` | `PerformanceAlertOptions` | LatencyWarningMs, CpuWarningPercent, MemoryWarningPercent |
| `PerformanceBroadcast` | `PerformanceBroadcastOptions` | IntervalSeconds, Enabled |
| `HistoricalMetrics` | `HistoricalMetricsOptions` | AggregationIntervalMinutes, RetentionDays |
| `Sampling` | `SamplingOptions` | CommandSampleRate, MetricsSampleRate |
| `Caching` | `CachingOptions` | DefaultExpirationMinutes, SlidingExpiration, MaxCacheSize |
| `GuildMembershipCache` | `GuildMembershipCacheOptions` | ExpirationMinutes, RefreshThresholdMinutes |
| `Anthropic` | `AnthropicOptions` | ApiKey, Model, MaxTokens |
| `Assistant` | `AssistantOptions` | Enabled, DefaultSystemPrompt, MaxContextMessages |
| `IdentityConfig` | `IdentityConfigOptions` | RequireEmailConfirmation, LockoutEnabled, MaxFailedAccessAttempts |
| `Verification` | `VerificationOptions` | CodeLength, ExpirationMinutes, MaxAttempts |
| `Notifications` | `NotificationOptions` | MaxPerUser, DefaultExpirationDays |
| `BackgroundServices` | `BackgroundServicesOptions` | HealthCheckIntervalSeconds, ReminderCheckIntervalSeconds |
| `Observability` | `ObservabilityOptions` | EnableElasticApm, EnableOpenTelemetry, ElasticsearchUrl, SeqUrl |

## Overview

The Discord Bot uses ASP.NET Core's configuration system which automatically loads environment-specific settings based on the `ASPNETCORE_ENVIRONMENT` environment variable. Configuration files are loaded in the following order (later files override earlier ones):

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific overrides)
3. User secrets (development only)
4. Environment variables

## Environment Files

| File | Environment | Purpose |
|------|-------------|---------|
| `appsettings.json` | All | Base configuration with sensible defaults |
| `appsettings.Development.json` | Development | Debug-level logging, development-friendly settings |
| `appsettings.Staging.json` | Staging | Pre-production testing with moderate logging |
| `appsettings.Production.json` | Production | Optimized for performance and reduced log volume |

## Log Level Configuration

### Development

- **Default Level:** Debug
- **Purpose:** Maximum visibility for debugging
- **Use Case:** Local development and troubleshooting

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Discord": "Debug"
      }
    }
  }
}
```

### Staging

- **Default Level:** Information
- **DiscordBot Namespace:** Debug (for pre-production debugging)
- **File Retention:** 14 days
- **Purpose:** Pre-production validation with enhanced application logging

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Information",
        "DiscordBot": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "retainedFileCountLimit": 14
        }
      }
    ]
  }
}
```

### Production

- **Default Level:** Warning
- **DiscordBot Namespace:** Information (important business events only)
- **File Retention:** 30 days
- **Buffered Writing:** Enabled for performance
- **Purpose:** Minimal logging overhead, focus on warnings and errors

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Warning",
        "DiscordBot": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "retainedFileCountLimit": 30,
          "buffered": true,
          "flushToDiskInterval": "00:00:01"
        }
      }
    ]
  }
}
```

## Database Configuration

Database query logging thresholds vary by environment:

| Setting | Development | Staging | Production |
|---------|-------------|---------|------------|
| `SlowQueryThresholdMs` | 100ms | 200ms | 500ms |
| `LogQueryParameters` | true | false | false |

- **Development:** Low threshold to catch potential performance issues early; parameters logged for debugging
- **Staging:** Moderate threshold; parameters hidden for security
- **Production:** Higher threshold to reduce noise; parameters never logged

## Setting the Environment

### Local Development

The environment defaults to `Development` when running locally with `dotnet run`.

### Command Line

```bash
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT="Staging"
dotnet run --project src/DiscordBot.Bot

# Windows CMD
set ASPNETCORE_ENVIRONMENT=Staging
dotnet run --project src/DiscordBot.Bot

# Linux/macOS
ASPNETCORE_ENVIRONMENT=Staging dotnet run --project src/DiscordBot.Bot
```

### Docker

```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production
```

### Azure App Service

Set the `ASPNETCORE_ENVIRONMENT` application setting in the Azure Portal or via ARM template.

### IIS

Set the environment variable in the application pool's environment variables or in `web.config`:

```xml
<aspNetCore processPath="dotnet" arguments=".\DiscordBot.Bot.dll">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  </environmentVariables>
</aspNetCore>
```

## Startup Logging

On application startup, the current environment is logged for verification:

```
[12:00:00 INF] Starting Discord bot application
[12:00:00 INF] Environment: Production
[12:00:00 INF] ContentRootPath: /app
```

This helps confirm that the expected configuration is being loaded.

## Log Retention Summary

| Environment | Retention | Buffered | Purpose |
|-------------|-----------|----------|---------|
| Development | 7 days | No | Quick iteration, immediate visibility |
| Staging | 14 days | No | Pre-production debugging |
| Production | 30 days | Yes | Compliance, performance |

## Centralized Log Aggregation (Seq)

The Discord Bot integrates with Seq for centralized log aggregation, providing powerful structured log querying and real-time analysis capabilities. Seq works alongside file and console logging to provide a unified observability solution.

### Overview

Seq is a structured logging server that enables querying logs by correlation IDs, guild IDs, user IDs, trace IDs, and other custom properties. Unlike traditional log aggregation that treats logs as plain text, Seq understands the structured nature of Serilog events, enabling powerful filtering and analysis.

### Configuration by Environment

| Environment | Seq Server | Batch Limit | Period | API Key | Use Case |
|-------------|------------|-------------|--------|---------|----------|
| Development | http://localhost:5341 | 100 | 2s | Not required | Local debugging with real-time feedback |
| Staging | http://seq-staging:5341 | 500 | 2s | Required (env var/secrets) | Pre-production validation with moderate throughput |
| Production | https://seq.yourdomain.com | 1000 | 5s | Required (env var/secrets) | High-efficiency production logging with batching |

**Configuration Details:**

**Development (appsettings.Development.json):**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ]
  }
}
```

**Staging (appsettings.Staging.json):**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq-staging:5341",
          "batchPostingLimit": 500
        }
      }
    ]
  }
}
```

**Production (appsettings.Production.json):**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "https://seq.yourdomain.com",
          "batchPostingLimit": 1000,
          "period": "00:00:05"
        }
      }
    ]
  }
}
```

### API Key Configuration

**Security Best Practice:** NEVER commit API keys to configuration files. Always use user secrets (development) or environment variables/secrets management (staging/production).

**Development (User Secrets):**
```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Serilog:WriteTo:2:Args:apiKey" "your-dev-api-key"
```

**Staging/Production (Environment Variables):**
```bash
# Linux/macOS
export Serilog__WriteTo__2__Args__apiKey="your-api-key"

# Windows PowerShell
$env:Serilog__WriteTo__2__Args__apiKey="your-api-key"

# Docker
docker run -e Serilog__WriteTo__2__Args__apiKey="your-api-key" discordbot:latest
```

**Kubernetes Secrets:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: seq-api-key
type: Opaque
data:
  apiKey: <base64-encoded-key>
---
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: discordbot
        env:
        - name: Serilog__WriteTo__2__Args__apiKey
          valueFrom:
            secretKeyRef:
              name: seq-api-key
              key: apiKey
```

### Performance Characteristics

| Configuration | Events/Batch | Posting Frequency | HTTP Requests/Hour | Real-Time Delay |
|---------------|--------------|-------------------|-------------------|-----------------|
| Development | 100 | Every 2s | Up to 1,800 | ~2 seconds |
| Staging | 500 | Every 2s | Up to 360 | ~2 seconds |
| Production | 1000 | Every 5s | Up to 180 | ~5 seconds |

**Async Batch Posting:**

- Log events are queued in memory (non-blocking, <5 microseconds per log call)
- Background thread posts batches to Seq asynchronously
- Application threads never block on HTTP requests to Seq
- Total performance impact: <1% CPU, <5MB memory (typical workloads)

### Local Development Setup

Run Seq locally using Docker:

```bash
# Start Seq container
docker run -d \
  --name seq \
  -p 5341:80 \
  -e ACCEPT_EULA=Y \
  -v seq-data:/data \
  datalust/seq:latest

# Access Seq UI at http://localhost:5341
```

**Verification:**

1. Start Seq container (see command above)
2. Run the Discord Bot: `dotnet run --project src/DiscordBot.Bot`
3. Execute a Discord command (e.g., `/ping`)
4. Open Seq UI at `http://localhost:5341`
5. Logs should appear within 2 seconds

### Common Queries

**By Correlation ID (track specific interaction):**
```
CorrelationId = 'a1b2c3d4e5f6g7h8'
```

**By Guild ID (all logs for a Discord server):**
```
GuildId = 123456789012345678
```

**By User ID (user-specific logs):**
```
UserId = 987654321098765432
```

**By Trace ID (link to distributed traces):**
```
TraceId = 'abc123def456...'
```

**Errors and warnings only:**
```
@Level in ['Warning', 'Error', 'Fatal']
```

**Slow database queries:**
```
ExecutionTimeMs > 500
```

### Related Documentation

For comprehensive Seq setup, querying, production deployment options, and troubleshooting, see:

- **[Centralized Log Aggregation (Seq)](log-aggregation.md)** - Complete Seq integration guide

## Secrets Management

Sensitive values must be stored securely - never commit them to version control. Use the methods appropriate for your environment.

### Development (User Secrets)

Store secrets locally using the User Secrets manager:

```bash
# User Secrets ID: 7b84433c-c2a8-46db-a8bf-58786ea4f28e

dotnet user-secrets set "Discord:Token" "your-bot-token"
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
dotnet user-secrets set "Anthropic:ApiKey" "your-api-key"
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "your-subscription-key"
```

### Staging/Production (Environment Variables)

Use secure environment variable management:

| Secret | Environment Variable | Description |
|--------|----------------------|-------------|
| Bot Token | `Discord__Token` | Discord bot authentication token |
| OAuth Client ID | `Discord__OAuth__ClientId` | Discord OAuth2 client ID |
| OAuth Client Secret | `Discord__OAuth__ClientSecret` | Discord OAuth2 client secret |
| Anthropic API Key | `Anthropic__ApiKey` | Anthropic/Claude API key |
| Azure Speech Key | `AzureSpeech__SubscriptionKey` | Azure Speech Services subscription key |
| Seq API Key | `Serilog__WriteTo__2__Args__apiKey` | Seq log aggregation API key |

**Best Practices:**
- Use a secrets management system (Azure Key Vault, HashiCorp Vault, sealed Kubernetes secrets, etc.)
- Never log or display secret values
- Rotate secrets regularly
- Use different secrets for each environment
- Audit access to secrets

---

## Feature-Specific Configuration

### Audit Log Retention

Controls automatic cleanup of audit log entries for compliance and storage management.

| Setting | appsettings Section | Default | Description |
|---------|---------------------|---------|-------------|
| `Enabled` | `AuditLogRetention:Enabled` | `true` | Enable/disable automatic cleanup |
| `RetentionDays` | `AuditLogRetention:RetentionDays` | `90` | Days to retain audit logs |
| `CleanupBatchSize` | `AuditLogRetention:CleanupBatchSize` | `1000` | Max records per cleanup operation |
| `CleanupIntervalHours` | `AuditLogRetention:CleanupIntervalHours` | `24` | Hours between cleanup runs |

```json
{
  "AuditLogRetention": {
    "Enabled": true,
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24
  }
}
```

**Environment Recommendations:**
- **Development:** 30-day retention with smaller batch sizes for faster iteration
- **Staging:** 60-day retention to validate cleanup behavior
- **Production:** 90+ day retention for compliance; increase batch size for high-volume systems

### Message Log Retention

Controls automatic cleanup of Discord message logs (requires user consent).

| Setting | appsettings Section | Default | Description |
|---------|---------------------|---------|-------------|
| `Enabled` | `MessageLogRetention:Enabled` | `true` | Enable/disable automatic cleanup |
| `RetentionDays` | `MessageLogRetention:RetentionDays` | `90` | Days to retain message logs |
| `CleanupBatchSize` | `MessageLogRetention:CleanupBatchSize` | `1000` | Max records per cleanup operation |
| `CleanupIntervalHours` | `MessageLogRetention:CleanupIntervalHours` | `24` | Hours between cleanup runs |

```json
{
  "MessageLogRetention": {
    "Enabled": true,
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24
  }
}
```

**GDPR Considerations:** Message log retention should align with your data retention policy. Users can request data deletion via `/consent revoke` which bypasses retention settings.

### Scheduled Messages

Controls the background service that executes scheduled messages.

| Setting | appsettings Section | Default | Description |
|---------|---------------------|---------|-------------|
| `CheckIntervalSeconds` | `ScheduledMessages:CheckIntervalSeconds` | `60` | Seconds between due message checks |
| `MaxConcurrentExecutions` | `ScheduledMessages:MaxConcurrentExecutions` | `5` | Max concurrent message executions |
| `ExecutionTimeoutSeconds` | `ScheduledMessages:ExecutionTimeoutSeconds` | `30` | Timeout per message execution |

```json
{
  "ScheduledMessages": {
    "CheckIntervalSeconds": 60,
    "MaxConcurrentExecutions": 5,
    "ExecutionTimeoutSeconds": 30
  }
}
```

**Environment Recommendations:**
- **Development:** Lower interval (30s) for faster testing feedback
- **Staging:** Default values for realistic behavior
- **Production:** Increase `MaxConcurrentExecutions` for high-volume bots; consider longer timeouts for rate-limited APIs

### Rat Watch

Controls the Rat Watch accountability feature's background processing.

| Setting | appsettings Section | Default | Description |
|---------|---------------------|---------|-------------|
| `CheckIntervalSeconds` | `RatWatch:CheckIntervalSeconds` | `30` | Seconds between watch/voting checks |
| `MaxConcurrentExecutions` | `RatWatch:MaxConcurrentExecutions` | `5` | Max concurrent watch executions |
| `ExecutionTimeoutSeconds` | `RatWatch:ExecutionTimeoutSeconds` | `30` | Timeout per watch execution |
| `DefaultVotingDurationMinutes` | `RatWatch:DefaultVotingDurationMinutes` | `5` | Default voting period for guilds |
| `DefaultMaxAdvanceHours` | `RatWatch:DefaultMaxAdvanceHours` | `24` | Max hours in advance to schedule |

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

**Environment Recommendations:**
- **Development:** Lower check interval (15s) for faster testing
- **Staging:** Default values; test voting expiration behavior
- **Production:** Adjust `DefaultVotingDurationMinutes` based on user feedback; consider shorter intervals for time-sensitive accountability

## Additional Configuration Sections

### Audio Configuration

#### AudioCacheOptions

Controls audio file caching for improved playback performance.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxCacheSizeMb` | int | 500 | Maximum total cache size in MB |
| `CacheExpirationMinutes` | int | 60 | Minutes before unused cache entries expire |
| `CleanupIntervalMinutes` | int | 15 | Interval between cache cleanup operations |

```json
{
  "AudioCache": {
    "MaxCacheSizeMb": 500,
    "CacheExpirationMinutes": 60,
    "CleanupIntervalMinutes": 15
  }
}
```

#### SoundboardOptions

Controls the soundboard feature limits and behavior.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxFileSizeMb` | int | 10 | Maximum upload file size in MB |
| `AllowedExtensions` | string[] | [".mp3", ".wav", ".ogg"] | Allowed audio file extensions |
| `MaxSoundsPerGuild` | int | 100 | Maximum sounds per guild |
| `StoragePath` | string | "sounds" | Directory for sound file storage |

```json
{
  "Soundboard": {
    "MaxFileSizeMb": 10,
    "AllowedExtensions": [".mp3", ".wav", ".ogg"],
    "MaxSoundsPerGuild": 100,
    "StoragePath": "sounds"
  }
}
```

#### VoiceChannelOptions

Controls voice channel behavior and queue management.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AutoDisconnectSeconds` | int | 300 | Auto-disconnect after idle time (0 = disabled) |
| `MaxQueueSize` | int | 50 | Maximum playback queue size |
| `DefaultVolume` | double | 0.5 | Default playback volume (0-1 range) |

```json
{
  "VoiceChannel": {
    "AutoDisconnectSeconds": 300,
    "MaxQueueSize": 50,
    "DefaultVolume": 0.5
  }
}
```

#### AzureSpeechOptions

Configures Azure Speech Services for text-to-speech functionality.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SubscriptionKey` | string | - | Azure subscription key (SECRET) |
| `Region` | string | "eastus" | Azure region |
| `DefaultVoice` | string | "en-US-JennyNeural" | Default TTS voice name |

```json
{
  "AzureSpeech": {
    "SubscriptionKey": "(user-secrets)",
    "Region": "eastus",
    "DefaultVoice": "en-US-JennyNeural"
  }
}
```

**Note:** Store `SubscriptionKey` in User Secrets or environment variables.

---

### Moderation Configuration

#### ModerationOptions

Core moderation system settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultMuteDurationMinutes` | int | 60 | Default mute duration |
| `MaxWarnBeforeBan` | int | 3 | Warnings before automatic ban |
| `LogRetentionDays` | int | 90 | Days to retain moderation logs |

```json
{
  "Moderation": {
    "DefaultMuteDurationMinutes": 60,
    "MaxWarnBeforeBan": 3,
    "LogRetentionDays": 90
  }
}
```

#### AutoModerationOptions

Automatic moderation settings for spam and raid detection.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | true | Enable auto-moderation |
| `SpamThreshold` | int | 5 | Messages before spam detection |
| `SpamIntervalSeconds` | int | 10 | Time window for spam detection |
| `RaidJoinThreshold` | int | 10 | Joins before raid detection |
| `RaidJoinIntervalSeconds` | int | 30 | Time window for raid detection |

```json
{
  "AutoModeration": {
    "Enabled": true,
    "SpamThreshold": 5,
    "SpamIntervalSeconds": 10,
    "RaidJoinThreshold": 10,
    "RaidJoinIntervalSeconds": 30
  }
}
```

---

### Analytics & Retention Configuration

#### AnalyticsRetentionOptions

Controls retention periods for aggregated analytics snapshots.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HourlySnapshotDays` | int | 7 | Retention for hourly snapshots |
| `DailySnapshotDays` | int | 90 | Retention for daily snapshots |
| `WeeklySnapshotDays` | int | 365 | Retention for weekly snapshots |

```json
{
  "AnalyticsRetention": {
    "HourlySnapshotDays": 7,
    "DailySnapshotDays": 90,
    "WeeklySnapshotDays": 365
  }
}
```

#### SoundPlayLogRetentionOptions

Controls sound playback history retention.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RetentionDays` | int | 90 | Days to retain sound play logs |
| `CleanupIntervalHours` | int | 24 | Hours between cleanup operations |

```json
{
  "SoundPlayLogRetention": {
    "RetentionDays": 90,
    "CleanupIntervalHours": 24
  }
}
```

#### UserActivityEventRetentionOptions

Controls user activity event log retention.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RetentionDays` | int | 90 | Days to retain activity events |
| `CleanupIntervalHours` | int | 24 | Hours between cleanup operations |

```json
{
  "UserActivityEventRetention": {
    "RetentionDays": 90,
    "CleanupIntervalHours": 24
  }
}
```

#### NotificationRetentionOptions

Controls user notification retention.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RetentionDays` | int | 30 | Days to retain notifications |
| `CleanupIntervalHours` | int | 6 | Hours between cleanup operations |

```json
{
  "NotificationRetention": {
    "RetentionDays": 30,
    "CleanupIntervalHours": 6
  }
}
```

---

### Performance Monitoring Configuration

#### PerformanceMetricsOptions

Controls performance metrics collection.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CollectionIntervalSeconds` | int | 60 | Metrics collection interval |
| `HistoryRetentionMinutes` | int | 1440 | Metrics history retention (1440 = 24h) |

```json
{
  "PerformanceMetrics": {
    "CollectionIntervalSeconds": 60,
    "HistoryRetentionMinutes": 1440
  }
}
```

#### PerformanceAlertOptions

Thresholds for performance alerting.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LatencyWarningMs` | int | 200 | Latency warning threshold (ms) |
| `LatencyCriticalMs` | int | 500 | Latency critical threshold (ms) |
| `CpuWarningPercent` | int | 80 | CPU usage warning threshold (%) |
| `CpuCriticalPercent` | int | 95 | CPU usage critical threshold (%) |
| `MemoryWarningPercent` | int | 80 | Memory usage warning threshold (%) |
| `MemoryCriticalPercent` | int | 95 | Memory usage critical threshold (%) |

```json
{
  "PerformanceAlerts": {
    "LatencyWarningMs": 200,
    "LatencyCriticalMs": 500,
    "CpuWarningPercent": 80,
    "CpuCriticalPercent": 95,
    "MemoryWarningPercent": 80,
    "MemoryCriticalPercent": 95
  }
}
```

#### PerformanceBroadcastOptions

Controls dashboard performance metrics broadcasting.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IntervalSeconds` | int | 5 | Broadcast interval |
| `Enabled` | bool | true | Enable broadcasting |

```json
{
  "PerformanceBroadcast": {
    "IntervalSeconds": 5,
    "Enabled": true
  }
}
```

#### HistoricalMetricsOptions

Controls historical metrics aggregation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AggregationIntervalMinutes` | int | 5 | Aggregation interval |
| `RetentionDays` | int | 30 | Historical data retention |

```json
{
  "HistoricalMetrics": {
    "AggregationIntervalMinutes": 5,
    "RetentionDays": 30
  }
}
```

#### SamplingOptions

Controls telemetry and metrics sampling rates.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CommandSampleRate` | double | 1.0 | Command sampling rate (0-1) |
| `MetricsSampleRate` | double | 1.0 | Metrics sampling rate (0-1) |

```json
{
  "Sampling": {
    "CommandSampleRate": 1.0,
    "MetricsSampleRate": 1.0
  }
}
```

---

### Caching Configuration

#### CachingOptions

General memory cache settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultExpirationMinutes` | int | 5 | Default cache entry expiration |
| `SlidingExpiration` | bool | true | Use sliding expiration |
| `MaxCacheSize` | int | 1000 | Maximum cache entries |

```json
{
  "Caching": {
    "DefaultExpirationMinutes": 5,
    "SlidingExpiration": true,
    "MaxCacheSize": 1000
  }
}
```

#### GuildMembershipCacheOptions

Guild membership cache settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExpirationMinutes` | int | 30 | Cache entry expiration |
| `RefreshThresholdMinutes` | int | 5 | Refresh threshold before expiration |

```json
{
  "GuildMembershipCache": {
    "ExpirationMinutes": 30,
    "RefreshThresholdMinutes": 5
  }
}
```

---

### AI Assistant Configuration

#### AnthropicOptions

Anthropic/Claude API configuration.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApiKey` | string | - | Anthropic API key (SECRET) |
| `Model` | string | "claude-3-sonnet-20240229" | Model identifier |
| `MaxTokens` | int | 4096 | Maximum response tokens |

```json
{
  "Anthropic": {
    "ApiKey": "(user-secrets)",
    "Model": "claude-3-sonnet-20240229",
    "MaxTokens": 4096
  }
}
```

**Note:** Store `ApiKey` in User Secrets or environment variables.

#### AssistantOptions

AI Assistant feature configuration.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | true | Enable AI assistant |
| `DefaultSystemPrompt` | string | "You are a helpful Discord bot assistant." | Default system prompt |
| `MaxContextMessages` | int | 10 | Context window size |
| `RateLimitPerMinute` | int | 5 | Rate limit per user per minute |

```json
{
  "Assistant": {
    "Enabled": true,
    "DefaultSystemPrompt": "You are a helpful Discord bot assistant.",
    "MaxContextMessages": 10,
    "RateLimitPerMinute": 5
  }
}
```

---

### Identity Configuration

#### IdentityConfigOptions

ASP.NET Core Identity configuration.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequireEmailConfirmation` | bool | false | Require email confirmation for registration |
| `LockoutEnabled` | bool | true | Enable account lockout after failed attempts |
| `MaxFailedAccessAttempts` | int | 5 | Max failed login attempts before lockout |
| `LockoutTimeSpanMinutes` | int | 15 | Lockout duration in minutes |

```json
{
  "IdentityConfig": {
    "RequireEmailConfirmation": false,
    "LockoutEnabled": true,
    "MaxFailedAccessAttempts": 5,
    "LockoutTimeSpanMinutes": 15
  }
}
```

#### VerificationOptions

Verification code settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CodeLength` | int | 6 | Verification code length |
| `ExpirationMinutes` | int | 15 | Code expiration time |
| `MaxAttempts` | int | 3 | Max verification attempts |

```json
{
  "Verification": {
    "CodeLength": 6,
    "ExpirationMinutes": 15,
    "MaxAttempts": 3
  }
}
```

---

### Notification Configuration

#### NotificationOptions

User notification settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxPerUser` | int | 100 | Maximum notifications per user |
| `DefaultExpirationDays` | int | 7 | Default notification retention |

```json
{
  "Notifications": {
    "MaxPerUser": 100,
    "DefaultExpirationDays": 7
  }
}
```

---

### Background Services Configuration

#### BackgroundServicesOptions

Background service execution intervals.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HealthCheckIntervalSeconds` | int | 30 | Health check interval |
| `ReminderCheckIntervalSeconds` | int | 30 | Reminder execution check interval |
| `ScheduledMessageCheckIntervalSeconds` | int | 60 | Scheduled message check interval |

```json
{
  "BackgroundServices": {
    "HealthCheckIntervalSeconds": 30,
    "ReminderCheckIntervalSeconds": 30,
    "ScheduledMessageCheckIntervalSeconds": 60
  }
}
```

---

### Observability Configuration

#### ObservabilityOptions

Logging and observability platform settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableElasticApm` | bool | false | Enable Elastic APM |
| `EnableOpenTelemetry` | bool | false | Enable OpenTelemetry |
| `ElasticsearchUrl` | string | "http://localhost:9200" | Elasticsearch endpoint |
| `SeqUrl` | string | "http://localhost:5341" | Seq log server endpoint |

```json
{
  "Observability": {
    "EnableElasticApm": false,
    "EnableOpenTelemetry": false,
    "ElasticsearchUrl": "http://localhost:9200",
    "SeqUrl": "http://localhost:5341"
  }
}
```

---

## Best Practices

1. **Never commit secrets** - Use user secrets for development and environment variables for production
2. **Verify environment on deploy** - Check startup logs to confirm the correct environment is loaded
3. **Adjust thresholds as needed** - The provided thresholds are starting points; tune based on your traffic patterns
4. **Monitor log volume** - Production logging should be minimal; if logs are too verbose, adjust overrides
5. **Use structured logging** - All logging uses Serilog structured logging for queryability
6. **Retention alignment** - Ensure audit log and message log retention aligns with your privacy policy and compliance requirements
7. **Background service tuning** - Monitor scheduled messages and Rat Watch execution times; adjust timeouts and concurrency based on observed behavior
8. **Environment parity** - Keep development, staging, and production configurations aligned to catch issues early
9. **Documentation** - Document any environment-specific settings and the rationale behind them

## Related Documentation

- [Identity Configuration](identity-configuration.md) - Authentication setup per environment
- [Distributed Tracing](tracing.md) - OpenTelemetry distributed tracing setup
- [Audit Log System](audit-log-system.md) - Comprehensive audit logging documentation
- [Message Logging](message-logging.md) - Message logging and consent system
- [Scheduled Messages](scheduled-messages.md) - Scheduled message feature documentation
- [Rat Watch](rat-watch.md) - Rat Watch accountability feature
