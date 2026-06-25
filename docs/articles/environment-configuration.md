# Environment-Specific Configuration

This document describes the environment-specific configuration files and their intended use for Development, Staging, and Production environments.

## Quick Reference

> The `Section` column is the appsettings.json section key; the `Class` column is the `IOptions<T>` type that binds it. All property names and defaults below were taken directly from the options classes in `src/DiscordBot.Core/Configuration` (plus `DatabaseSettings` in `src/DiscordBot.Infrastructure/Configuration`).

| Section | Class | Key Settings |
|---------|-------|--------------|
| `Application` | `ApplicationOptions` | Title, BaseUrl, ContactEmail, Version |
| `Database` | `DatabaseSettings` | Provider (`Sqlite` \| `PostgreSql`), SlowQueryThresholdMs, LogQueryParameters |
| `Discord` | *(bot host options)* | TestGuildId, DefaultRateLimitInvokes, DefaultRateLimitPeriodSeconds, AdditionalOwnerIds |
| `Discord:OAuth` | `DiscordOAuthOptions` | ClientId, ClientSecret, Scopes |
| `AudioCache` | `AudioCacheOptions` | Enabled, CachePath, MaxCacheSizeBytes, MaxEntries, CleanupIntervalMinutes |
| `Soundboard` | `SoundboardOptions` | BasePath, DefaultMaxFileSizeBytes, DefaultMaxSoundsPerGuild, SupportedFormats |
| `Vox` | `VoxOptions` | BasePath, DefaultWordGapMs, MaxMessageWords, MaxMessageLength |
| `VoiceChannel` | `VoiceChannelOptions` | AutoLeaveTimeoutSeconds, CheckIntervalSeconds |
| `AzureSpeech` | `AzureSpeechOptions` | SubscriptionKey, Region, DefaultVoice, MaxTextLength |
| `AzureSpeech:Ssml` | `AzureSpeechSsmlOptions` | EnableValidation, StrictMode, MaxComplexityScore, EnableSanitization |
| `Moderation` | `ModerationOptions` | DefaultTempBanDurationDays, MaxPurgeMessages, CaseHistoryPageSize, LogActionsToAudit |
| `AutoModeration` | `AutoModerationOptions` | DetectionCacheExpiryMinutes, MaxCachedGuilds, FlaggedEventRetentionDays |
| `RatWatch` | `RatWatchOptions` | CheckIntervalSeconds, DefaultVotingDurationMinutes, DefaultMaxAdvanceHours |
| `Reminder` | `ReminderOptions` | CheckIntervalSeconds, MaxRemindersPerUser, MaxAdvanceDays, MinAdvanceMinutes |
| `ScheduledMessages` | `ScheduledMessagesOptions` | CheckIntervalSeconds, MaxConcurrentExecutions, ExecutionTimeoutSeconds |
| `AnalyticsRetention` | `AnalyticsRetentionOptions` | HourlyRetentionDays, DailyRetentionDays, CleanupBatchSize |
| `AuditLogRetention` | `AuditLogRetentionOptions` | RetentionDays, CleanupBatchSize, CleanupIntervalHours, Enabled |
| `MessageLogRetention` | `MessageLogRetentionOptions` | RetentionDays, CleanupBatchSize, CleanupIntervalHours, Enabled |
| `SoundPlayLogRetention` | `SoundPlayLogRetentionOptions` | RetentionDays, CleanupBatchSize, CleanupIntervalHours, Enabled |
| `UserActivityEventRetention` | `UserActivityEventRetentionOptions` | RetentionDays, CleanupBatchSize, CleanupIntervalHours, Enabled |
| `NotificationRetention` | `NotificationRetentionOptions` | DismissedRetentionDays, ReadRetentionDays, UnreadRetentionDays |
| `PerformanceMetrics` | `PerformanceMetricsOptions` | LatencySampleIntervalSeconds, SlowQueryThresholdMs, CpuSampleIntervalSeconds |
| `PerformanceAlerts` | `PerformanceAlertOptions` | CheckIntervalSeconds, ConsecutiveBreachesRequired, IncidentRetentionDays |
| `PerformanceBroadcast` | `PerformanceBroadcastOptions` | Enabled, HealthMetricsIntervalSeconds, CommandMetricsIntervalSeconds |
| `HistoricalMetrics` | `HistoricalMetricsOptions` | SampleIntervalSeconds, RetentionDays, CleanupIntervalHours, Enabled |
| `OpenTelemetry:Tracing:Sampling` | `SamplingOptions` | DefaultRate, ErrorRate, SlowThresholdMs, HighPriorityRate, LowPriorityRate |
| `Caching` | `CachingOptions` | GuildMembershipDurationMinutes, DiscordUserInfoDurationMinutes, GuildMemberListDurationMinutes |
| `GuildMembershipCache` | `GuildMembershipCacheOptions` | StoredGuildMembershipDurationMinutes |
| `Anthropic` | `AnthropicOptions` | ApiKey, DefaultModel, MaxRetries, TimeoutSeconds, RetryBaseDelayMs |
| `Assistant` | `AssistantOptions` | GloballyEnabled, DefaultRateLimit, RateLimitWindowMinutes, Model, MaxTokens |
| `DmAssistant` | `DmAssistantOptions` | Enabled, Model, MaxTokens, MaxConversationMessages, EnableCodeExecution |
| `Mogwai` | `MogwaiOptions` | Enabled, ClaudeCliPath, WorkingDirectory, AllowedTools, MaxBudgetUsd, MaxTurns |
| `FeatureRequests` | `FeatureRequestsOptions` | Enabled, MinDescriptionLength, MaxDescriptionLength, RequirementsGatheringModel |
| `NotX` | `NotXOptions` | RequestTimeoutSeconds, MaxResponseBytes, UserAgent |
| `Identity` | `IdentityConfigOptions` | RequireDigit, RequiredLength, MaxFailedAccessAttempts, LockoutTimeSpanMinutes |
| `Verification` | `VerificationOptions` | CodeCharset, CodeLength, CodeExpiryMinutes, MaxCodesPerHour |
| `Notification` | `NotificationOptions` | EnablePerformanceAlerts, EnableGuildEvents, DuplicateSuppressionMinutes |
| `BackgroundServices` | `BackgroundServicesOptions` | TokenRefreshIntervalMinutes, MemberSyncEnabled, MetricsUpdateIntervalSeconds |
| `LogSanitization` | `LogSanitizationOptions` | Enabled, CustomPatterns, AdditionalSensitiveKeys |
| `Observability` | `ObservabilityOptions` | KibanaUrl, SeqUrl |
| `OpenTelemetry` | *(bound in `OpenTelemetryExtensions`)* | ServiceName, Metrics, Tracing |

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

## Database Provider Configuration

### Provider Selection

The `Database:Provider` key controls which database engine EF Core uses. If omitted, the provider is auto-detected from the connection string.

| Value | Provider | Detection heuristic |
|-------|----------|---------------------|
| `Sqlite` | SQLite (file-based) | `Data Source` with a file path |
| `PostgreSql` | PostgreSQL (Npgsql) | Connection string containing `Host=` or `Server=` |
| *(omitted)* | Auto-detected | See heuristics above |

**appsettings.json:**
```json
{
  "Database": {
    "Provider": "PostgreSql"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=discordbot;Username=discordbot;Password=your-password"
  }
}
```

**Environment variable form:**
```bash
Database__Provider=PostgreSql
ConnectionStrings__DefaultConnection="Host=localhost;Database=discordbot;Username=discordbot;Password=your-password"
```

### Connection String Examples

**SQLite (default — development and single-server):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/discordbot.db"
  }
}
```

**PostgreSQL (production):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=discordbot;Username=discordbot;Password=your-password"
  }
}
```

**PostgreSQL via Docker Compose (`postgres` profile):**
```bash
ConnectionStrings__DefaultConnection="Host=db;Database=discordbot;Username=discordbot;Password=your-password"
```

### Detection Logic

Provider selection follows a two-tier priority:

1. **Explicit config** — if `Database:Provider` is set to `Sqlite` or `PostgreSql`, that value is used unconditionally.
2. **Connection string heuristic** — if `Database:Provider` is absent or null, the connection string is inspected:
   - Contains `Host=` or `Server=` (without a file path extension) → PostgreSQL
   - Contains `Data Source` with a file path → SQLite

This means you can omit `Database:Provider` in most cases and rely on the connection string alone to determine the provider.

### Environment Recommendations

| Environment | Provider | Rationale |
|-------------|----------|-----------|
| Development | SQLite | Zero-config, file-based, no server required |
| Staging | PostgreSQL | Match production provider to catch provider-specific issues |
| Production | PostgreSQL | Concurrent writes, connection pooling, WAL, backup tooling |

---

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
| Database Provider | `Database__Provider` | `Sqlite` or `PostgreSql` (or omit for auto-detect) |
| Database Connection | `ConnectionStrings__DefaultConnection` | Full connection string for the selected provider |
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

> Property names and defaults below match the options classes exactly. Only a representative subset of properties is shown for the larger classes — see the source class for the full set.

### Audio Configuration

#### AudioCacheOptions — section `AudioCache`

Caches FFmpeg-processed PCM audio to reduce playback latency.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | true | Enable audio caching |
| `CachePath` | string | "./cache/audio" | Base path for cached audio files |
| `MaxCacheSizeBytes` | long | 524288000 | Max total cache size in bytes (500 MB) |
| `MaxEntries` | int | 1000 | Max number of cached entries |
| `EntryTtlHours` | int | 168 | TTL for cache entries (7 days) |
| `MaxCacheDurationSeconds` | int | 60 | Max sound duration eligible for caching |
| `CleanupIntervalMinutes` | int | 60 | Interval between cache cleanup runs |

```json
{
  "AudioCache": {
    "Enabled": true,
    "CachePath": "./cache/audio",
    "MaxCacheSizeBytes": 524288000,
    "MaxEntries": 1000,
    "EntryTtlHours": 168,
    "MaxCacheDurationSeconds": 60,
    "CleanupIntervalMinutes": 60
  }
}
```

#### SoundboardOptions — section `Soundboard`

Controls soundboard limits and FFmpeg integration.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BasePath` | string | "./sounds" | Base path for sound file storage (per-guild subfolders) |
| `FfmpegPath` | string? | null | Path to FFmpeg executable (null = use system PATH) |
| `FfprobePath` | string? | null | Path to FFprobe executable (null = use system PATH) |
| `DefaultMaxDurationSeconds` | int | 30 | Default max sound duration |
| `DefaultMaxFileSizeBytes` | long | 10485760 | Default max file size (10 MB) |
| `DefaultMaxSoundsPerGuild` | int | 100 | Default max sounds per guild |
| `DefaultMaxStorageBytes` | long | 524288000 | Default total storage per guild (500 MB) |
| `DefaultAutoLeaveTimeoutMinutes` | int | 0 | Auto-leave timeout (0 = stay indefinitely) |
| `SupportedFormats` | string[] | ["mp3", "wav", "ogg"] | Supported audio file formats |

```json
{
  "Soundboard": {
    "BasePath": "./sounds",
    "FfmpegPath": null,
    "FfprobePath": null,
    "DefaultMaxDurationSeconds": 30,
    "DefaultMaxFileSizeBytes": 10485760,
    "DefaultMaxSoundsPerGuild": 100,
    "DefaultMaxStorageBytes": 524288000,
    "DefaultAutoLeaveTimeoutMinutes": 0,
    "SupportedFormats": ["mp3", "wav", "ogg"]
  }
}
```

#### VoxOptions — section `Vox`

Controls the VOX clip library (Half-Life-style announcements).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BasePath` | string | "./sounds" | Base path for VOX audio files |
| `DefaultWordGapMs` | int | 50 | Default gap between words in milliseconds |
| `MaxMessageWords` | int | 50 | Max words allowed in a VOX message |
| `MaxMessageLength` | int | 500 | Max character length of a VOX message |

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

#### VoiceChannelOptions — section `VoiceChannel`

Controls automatic voice-channel disconnection.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AutoLeaveTimeoutSeconds` | int | 300 | Auto-leave timeout when the bot is alone (0 = stay indefinitely) |
| `CheckIntervalSeconds` | int | 30 | Interval between auto-leave condition checks |

```json
{
  "VoiceChannel": {
    "AutoLeaveTimeoutSeconds": 300,
    "CheckIntervalSeconds": 30
  }
}
```

#### AzureSpeechOptions — section `AzureSpeech`

Configures Azure Cognitive Services Speech for text-to-speech.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SubscriptionKey` | string? | null | Azure subscription key (SECRET; via user secrets) |
| `Region` | string | "eastus" | Azure region |
| `DefaultVoice` | string | "en-US-JennyNeural" | Default TTS voice name |
| `MaxTextLength` | int | 500 | Max text length for synthesis |
| `DefaultSpeed` | double | 1.0 | Default speech rate multiplier (0.5–2.0) |
| `DefaultPitch` | double | 1.0 | Default pitch adjustment (0.5–1.5) |
| `DefaultVolume` | double | 0.8 | Default volume level (0.0–1.0) |

```json
{
  "AzureSpeech": {
    "SubscriptionKey": null,
    "Region": "eastus",
    "DefaultVoice": "en-US-JennyNeural",
    "MaxTextLength": 500,
    "DefaultSpeed": 1.0,
    "DefaultPitch": 1.0,
    "DefaultVolume": 0.8
  }
}
```

**Note:** Store `SubscriptionKey` in User Secrets or environment variables.

#### AzureSpeechSsmlOptions — section `AzureSpeech:Ssml`

Controls SSML validation and style-preset behavior for TTS.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableValidation` | bool | true | Validate SSML before sending to Azure |
| `StrictMode` | bool | false | Reject invalid SSML (false = fall back to plain text) |
| `MaxComplexityScore` | int | 50 | Max allowed SSML complexity score |
| `MaxDocumentLength` | int | 5000 | Max SSML document length in characters |
| `EnableSanitization` | bool | true | Attempt automatic sanitization of invalid SSML |
| `EnableStylePresets` | bool | true | Enable the style-presets feature |
| `CacheVoiceCapabilities` | bool | true | Cache voice-capability metadata |
| `CacheDurationMinutes` | int | 1440 | Voice-capability cache duration (24 hours) |

```json
{
  "AzureSpeech": {
    "Ssml": {
      "EnableValidation": true,
      "StrictMode": false,
      "MaxComplexityScore": 50,
      "MaxDocumentLength": 5000,
      "EnableSanitization": true,
      "EnableStylePresets": true,
      "CacheVoiceCapabilities": true,
      "CacheDurationMinutes": 1440
    }
  }
}
```

---

### Moderation Configuration

#### ModerationOptions — section `Moderation`

Core moderation system settings.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultTempBanDurationDays` | int | 7 | Default duration for temporary bans |
| `MaxPurgeMessages` | int | 100 | Max messages purged in a single operation |
| `CaseHistoryPageSize` | int | 10 | Moderation cases per page in case history |
| `LogActionsToAudit` | bool | true | Log moderation actions to the audit log system |

```json
{
  "Moderation": {
    "DefaultTempBanDurationDays": 7,
    "MaxPurgeMessages": 100,
    "CaseHistoryPageSize": 10,
    "LogActionsToAudit": true
  }
}
```

#### AutoModerationOptions — section `AutoModeration`

Automatic moderation settings for spam and raid detection.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DetectionCacheExpiryMinutes` | int | 5 | Minutes before cached detection results expire |
| `MaxCachedGuilds` | int | 1000 | Max guilds to cache auto-mod configs for |
| `FlaggedEventRetentionDays` | int | 90 | Days to retain flagged event records |
| `EnableDebugLogging` | bool | false | Enable debug logging for detection |
| `MaxMessagesPerUser` | int | 200 | Max messages tracked per user for spam detection |
| `MaxJoinsPerGuild` | int | 500 | Max joins tracked per guild for raid detection |

```json
{
  "AutoModeration": {
    "DetectionCacheExpiryMinutes": 5,
    "MaxCachedGuilds": 1000,
    "FlaggedEventRetentionDays": 90,
    "EnableDebugLogging": false,
    "MaxMessagesPerUser": 200,
    "MaxJoinsPerGuild": 500
  }
}
```

---

### Analytics & Retention Configuration

#### AnalyticsRetentionOptions — section `AnalyticsRetention`

Controls retention periods for aggregated analytics snapshots.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HourlyRetentionDays` | int | 14 | Retention for hourly snapshots |
| `DailyRetentionDays` | int | 365 | Retention for daily snapshots |
| `Enabled` | bool | true | Enable analytics aggregation |
| `CleanupBatchSize` | int | 1000 | Max records per cleanup operation |
| `CleanupIntervalHours` | int | 24 | Hours between cleanup operations |

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

#### SoundPlayLogRetentionOptions — section `SoundPlayLogRetention`

Controls sound playback history retention.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RetentionDays` | int | 90 | Days to retain sound play logs |
| `Enabled` | bool | true | Enable cleanup |
| `CleanupBatchSize` | int | 1000 | Max records per cleanup operation |
| `CleanupIntervalHours` | int | 24 | Hours between cleanup operations |

```json
{
  "SoundPlayLogRetention": {
    "RetentionDays": 90,
    "Enabled": true,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24
  }
}
```

#### UserActivityEventRetentionOptions — section `UserActivityEventRetention`

Controls user activity event log retention.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RetentionDays` | int | 90 | Days to retain activity events |
| `CleanupBatchSize` | int | 1000 | Max events per cleanup operation |
| `CleanupIntervalHours` | int | 24 | Hours between cleanup operations |
| `Enabled` | bool | true | Enable cleanup |

```json
{
  "UserActivityEventRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

#### NotificationRetentionOptions — section `NotificationRetention`

Controls user notification retention by state.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DismissedRetentionDays` | int | 7 | Days to retain dismissed notifications |
| `ReadRetentionDays` | int | 30 | Days to retain read notifications |
| `UnreadRetentionDays` | int | 90 | Days to retain unread notifications (0 = never delete) |
| `CleanupBatchSize` | int | 1000 | Max records per cleanup operation |
| `CleanupIntervalHours` | int | 24 | Hours between cleanup operations |
| `Enabled` | bool | true | Enable cleanup |

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

---

### Performance Monitoring Configuration

#### PerformanceMetricsOptions — section `PerformanceMetrics`

Controls performance metrics collection.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LatencySampleIntervalSeconds` | int | 30 | Interval between latency samples |
| `LatencyRetentionHours` | int | 24 | Latency history retention |
| `ConnectionEventRetentionDays` | int | 7 | Connection event history retention |
| `ApiRequestTrackingEnabled` | bool | true | Enable Discord API request tracking |
| `SlowQueryThresholdMs` | int | 100 | Threshold defining a slow database query |
| `SlowQueryMaxStored` | int | 100 | Max slow queries kept in memory |
| `CacheStatisticsEnabled` | bool | true | Enable cache statistics tracking |
| `CommandAggregationCacheTtlMinutes` | int | 5 | TTL for cached command aggregations |
| `MaxApiCategories` | int | 100 | Max API categories to track |
| `CpuSampleIntervalSeconds` | int | 5 | Interval between CPU samples |
| `CpuRetentionHours` | int | 24 | CPU history retention |

```json
{
  "PerformanceMetrics": {
    "LatencySampleIntervalSeconds": 30,
    "LatencyRetentionHours": 24,
    "ConnectionEventRetentionDays": 7,
    "ApiRequestTrackingEnabled": true,
    "SlowQueryThresholdMs": 100,
    "SlowQueryMaxStored": 100,
    "CacheStatisticsEnabled": true,
    "CommandAggregationCacheTtlMinutes": 5,
    "MaxApiCategories": 100,
    "CpuSampleIntervalSeconds": 5,
    "CpuRetentionHours": 24
  }
}
```

#### PerformanceAlertOptions — section `PerformanceAlerts`

Controls alert evaluation behavior and incident retention. Alert thresholds themselves are seeded per-metric in the database (not in this options class); see [Alerting System](alerting-system.md).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CheckIntervalSeconds` | int | 30 | Interval between metric checks |
| `ConsecutiveBreachesRequired` | int | 2 | Consecutive breaches before raising an alert |
| `ConsecutiveNormalRequired` | int | 3 | Consecutive normal readings before auto-resolving |
| `IncidentRetentionDays` | int | 90 | Days to retain resolved incidents |

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

#### PerformanceBroadcastOptions — section `PerformanceBroadcast`

Controls SignalR broadcast intervals for dashboard metrics.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HealthMetricsIntervalSeconds` | int | 5 | Interval for health metrics (latency, memory, CPU) |
| `CommandMetricsIntervalSeconds` | int | 30 | Interval for command performance metrics |
| `SystemMetricsIntervalSeconds` | int | 10 | Interval for system health metrics |
| `Enabled` | bool | true | Enable performance broadcasting |

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

#### HistoricalMetricsOptions — section `HistoricalMetrics`

Controls historical metrics sampling and retention.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SampleIntervalSeconds` | int | 60 | Interval between metric samples |
| `RetentionDays` | int | 30 | Days to retain historical snapshots |
| `Enabled` | bool | true | Enable historical metrics collection |
| `CleanupIntervalHours` | int | 6 | Hours between cleanup runs |
| `InitialDelaySeconds` | double | 10 | Initial delay before starting collection |
| `ErrorRetryDelaySeconds` | double | 30 | Delay before retrying after an error |

```json
{
  "HistoricalMetrics": {
    "SampleIntervalSeconds": 60,
    "RetentionDays": 30,
    "Enabled": true,
    "CleanupIntervalHours": 6
  }
}
```

#### SamplingOptions — section `OpenTelemetry:Tracing:Sampling`

Controls OpenTelemetry distributed-tracing sampling. **Note:** despite the class's `SectionName = "Sampling"`, it is bound under `OpenTelemetry:Tracing:Sampling` in `OpenTelemetryExtensions`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultRate` | double | 0.1 | Sampling rate for normal operations (0.0–1.0) |
| `ErrorRate` | double | 1.0 | Sampling rate for error operations |
| `SlowThresholdMs` | int | 5000 | Threshold (ms) defining a slow operation |
| `HighPriorityRate` | double | 0.5 | Sampling rate for high-priority operations |
| `LowPriorityRate` | double | 0.01 | Sampling rate for low-priority operations |

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "Sampling": {
        "DefaultRate": 0.1,
        "ErrorRate": 1.0,
        "SlowThresholdMs": 5000,
        "HighPriorityRate": 0.5,
        "LowPriorityRate": 0.01
      }
    }
  }
}
```

---

### Caching Configuration

#### CachingOptions — section `Caching`

In-memory cache durations across the application.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GuildMembershipDurationMinutes` | int | 5 | Guild membership cache duration |
| `DiscordUserInfoDurationMinutes` | int | 15 | Discord user info cache duration |
| `InteractionStateExpiryMinutes` | int | 15 | Interaction state expiry |
| `ConsentCacheDurationMinutes` | int | 5 | User consent cache duration |
| `DashboardStatsCacheDurationSeconds` | int | 5 | Dashboard statistics cache duration |
| `GuildMemberListDurationMinutes` | int | 5 | Guild member list cache duration |
| `GuildMemberDetailDurationMinutes` | int | 1 | Single member detail cache duration |
| `SearchResultsCacheDurationSeconds` | int | 30 | Search results cache duration |
| `CommandMetadataCacheDurationMinutes` | int | 60 | Command metadata cache duration |
| `PageMetadataCacheDurationMinutes` | int | 60 | Page metadata cache duration |

```json
{
  "Caching": {
    "GuildMembershipDurationMinutes": 5,
    "DiscordUserInfoDurationMinutes": 15,
    "InteractionStateExpiryMinutes": 15,
    "ConsentCacheDurationMinutes": 5
  }
}
```

#### GuildMembershipCacheOptions — section `GuildMembershipCache`

Controls caching of stored guild membership data (separate from in-memory API caching above).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `StoredGuildMembershipDurationMinutes` | int | 30 | Cache duration for stored guild membership (authorization) |

```json
{
  "GuildMembershipCache": {
    "StoredGuildMembershipDurationMinutes": 30
  }
}
```

---

### AI Assistant Configuration

#### AnthropicOptions — section `Anthropic`

Anthropic/Claude API client configuration.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApiKey` | string? | null | Anthropic API key (SECRET; via user secrets) |
| `DefaultModel` | string | "claude-sonnet-4-20250514" | Default Claude model |
| `MaxRetries` | int | 3 | Max retry attempts for transient failures |
| `TimeoutSeconds` | int | 300 | Request timeout in seconds |
| `RetryBaseDelayMs` | int | 1000 | Base delay (ms) for exponential backoff |
| `EnablePromptCachingByDefault` | bool | true | Enable automatic prompt caching by default |

```json
{
  "Anthropic": {
    "ApiKey": null,
    "DefaultModel": "claude-sonnet-4-20250514",
    "MaxRetries": 3,
    "TimeoutSeconds": 300,
    "RetryBaseDelayMs": 1000,
    "EnablePromptCachingByDefault": true
  }
}
```

**Note:** Store `ApiKey` in User Secrets or environment variables.

#### AssistantOptions — section `Assistant`

Guild AI-assistant feature configuration (representative subset; see the class for the full set including cost-tracking, prompt-caching, and tool settings).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GloballyEnabled` | bool | false | Enable the assistant feature globally |
| `EnabledByDefaultForNewGuilds` | bool | false | Enable for new guilds by default |
| `DefaultRateLimit` | int | 5 | Default questions per rate-limit window |
| `RateLimitWindowMinutes` | int | 5 | Rate-limit window in minutes |
| `RateLimitBypassRole` | string? | "Admin" | Minimum role that bypasses rate limits |
| `MaxQuestionLength` | int | 500 | Max user question length |
| `MaxResponseLength` | int | 1800 | Max response length in characters |
| `Model` | string | "claude-sonnet-4-20250514" | Claude model identifier |
| `MaxTokens` | int | 512 | Max response tokens |
| `Temperature` | double | 0.7 | Response temperature (0.0–1.0) |

```json
{
  "Assistant": {
    "GloballyEnabled": false,
    "EnabledByDefaultForNewGuilds": false,
    "DefaultRateLimit": 5,
    "RateLimitWindowMinutes": 5,
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 512,
    "Temperature": 0.7
  }
}
```

#### DmAssistantOptions — section `DmAssistant`

DM-based AI-assistant feature (independent from the guild assistant; representative subset).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | false | Enable the DM assistant feature |
| `OwnerSystemPromptPath` | string | "docs/agents/dm-owner-agent.md" | Path to the owner system prompt |
| `MaxConversationMessages` | int | 20 | Conversation messages retained per user |
| `Model` | string | "claude-sonnet-4-20250514" | Claude model identifier |
| `MaxTokens` | int | 4096 | Max response tokens |
| `Temperature` | double | 0.7 | Response temperature |
| `EnableCodeExecution` | bool | false | Enable the Python code-execution tool |
| `EnablePromptCaching` | bool | true | Enable prompt caching for the system prompt |

```json
{
  "DmAssistant": {
    "Enabled": false,
    "MaxConversationMessages": 20,
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 4096,
    "EnableCodeExecution": false
  }
}
```

#### MogwaiOptions — section `Mogwai`

Claude Code CLI integration for coding tasks (disabled by default; see [Mogwai feature guide](mogwai.md)).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | false | Enable the Mogwai feature |
| `ClaudeCliPath` | string | "claude" | Path to the Claude CLI binary |
| `WorkingDirectory` | string | "." | Working directory for Claude Code sessions |
| `AllowedTools` | string | "Bash,Read,Glob,Grep,Write,Edit" | Comma-separated allowed tools |
| `MaxBudgetUsd` | decimal | 5.00 | Max budget (USD) per invocation |
| `MaxTurns` | int | 10 | Max turns per invocation |
| `TimeoutSeconds` | int | 300 | Process timeout in seconds |
| `SkipPermissions` | bool | false | Use `--dangerously-skip-permissions` |

```json
{
  "Mogwai": {
    "Enabled": false,
    "ClaudeCliPath": "claude",
    "WorkingDirectory": ".",
    "AllowedTools": "Bash,Read,Glob,Grep,Write,Edit",
    "MaxBudgetUsd": 5.00,
    "MaxTurns": 10,
    "TimeoutSeconds": 300
  }
}
```

#### FeatureRequestsOptions — section `FeatureRequests`

Controls the `/feature-request` command and its AI requirements-gathering flow.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | true | Enable the `/feature-request` command globally |
| `MinDescriptionLength` | int | 20 | Minimum valid description length |
| `MaxDescriptionLength` | int | 500 | Maximum description length |
| `DirectSubmitThreshold` | int | 100 | Length at which the conversation flow is bypassed |
| `ConversationTimeoutMinutes` | int | 30 | Minutes before an in-progress DM conversation expires |
| `RequirementsGatheringModel` | string | "claude-sonnet-4-20250514" | Model for the requirements conversation |
| `MaxConversationTurns` | int | 10 | Max conversation turns before forcing end |

```json
{
  "FeatureRequests": {
    "Enabled": true,
    "MinDescriptionLength": 20,
    "MaxDescriptionLength": 500,
    "DirectSubmitThreshold": 100,
    "ConversationTimeoutMinutes": 30,
    "RequirementsGatheringModel": "claude-sonnet-4-20250514",
    "MaxConversationTurns": 10
  }
}
```

#### NotXOptions — section `NotX`

Controls the not-X feature (X/Twitter link preview via fxtwitter).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequestTimeoutSeconds` | int | 5 | HTTP timeout for fxtwitter API calls |
| `MaxResponseBytes` | int | 262144 | Max bytes read from the fxtwitter response (256 KB) |
| `UserAgent` | string | "DiscordBot/1.0 (+not-x)" | User-Agent header sent to fxtwitter |

```json
{
  "NotX": {
    "RequestTimeoutSeconds": 5,
    "MaxResponseBytes": 262144,
    "UserAgent": "DiscordBot/1.0 (+not-x)"
  }
}
```

---

### Identity Configuration

#### IdentityConfigOptions — section `Identity`

ASP.NET Core Identity configuration (representative subset; see the class for cookie and sign-in settings).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequireDigit` | bool | true | Require at least one digit in passwords |
| `RequireLowercase` | bool | true | Require a lowercase letter |
| `RequireUppercase` | bool | true | Require an uppercase letter |
| `RequireNonAlphanumeric` | bool | true | Require a non-alphanumeric character |
| `RequiredLength` | int | 8 | Minimum password length |
| `LockoutTimeSpanMinutes` | int | 15 | Lockout duration in minutes |
| `MaxFailedAccessAttempts` | int | 5 | Failed login attempts before lockout |
| `RequireConfirmedEmail` | bool | false | Require confirmed email to sign in |
| `CookieExpireDays` | int | 7 | Days before auth cookies expire |

```json
{
  "Identity": {
    "RequireDigit": true,
    "RequiredLength": 8,
    "MaxFailedAccessAttempts": 5,
    "LockoutTimeSpanMinutes": 15,
    "RequireConfirmedEmail": false,
    "CookieExpireDays": 7
  }
}
```

**Note:** The optional `Identity:DefaultAdmin` (`Email`, `Password`) sub-section seeds an initial admin account on first run — store it in User Secrets.

#### VerificationOptions — section `Verification`

Verification code generation and validation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CodeCharset` | string | "ABCDEFGHJKLMNPQRSTUVWXYZ23456789" | Character set for generated codes |
| `CodeLength` | int | 6 | Length of generated verification codes |
| `CodeExpiryMinutes` | int | 15 | Minutes before a code expires |
| `MaxCodesPerHour` | int | 3 | Max codes a user can request per hour |
| `OldCodeCleanupHours` | int | 24 | Age threshold for cleaning up old codes |

```json
{
  "Verification": {
    "CodeCharset": "ABCDEFGHJKLMNPQRSTUVWXYZ23456789",
    "CodeLength": 6,
    "CodeExpiryMinutes": 15,
    "MaxCodesPerHour": 3,
    "OldCodeCleanupHours": 24
  }
}
```

---

### Notification Configuration

#### NotificationOptions — section `Notification`

Controls which events generate admin notifications.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnablePerformanceAlerts` | bool | true | Notify on performance alerts |
| `EnableBotStatusChanges` | bool | true | Notify on bot connect/disconnect |
| `EnableGuildEvents` | bool | true | Notify on guild joined/left |
| `EnableCommandErrors` | bool | true | Notify on unhandled command errors |
| `DuplicateSuppressionMinutes` | int | 5 | Window for duplicate notification suppression |

```json
{
  "Notification": {
    "EnablePerformanceAlerts": true,
    "EnableBotStatusChanges": true,
    "EnableGuildEvents": true,
    "EnableCommandErrors": true,
    "DuplicateSuppressionMinutes": 5
  }
}
```

---

### Background Services Configuration

#### BackgroundServicesOptions — section `BackgroundServices`

Background-service execution intervals and member-sync tuning (representative subset; see the class for all 24 properties).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TokenRefreshIntervalMinutes` | int | 30 | Interval between Discord token refresh checks |
| `TokenExpirationThresholdHours` | int | 1 | Refresh tokens expiring within this window |
| `MetricsUpdateIntervalSeconds` | int | 30 | Interval between real-time metrics updates |
| `BusinessMetricsUpdateIntervalMinutes` | int | 5 | Interval between business metrics calculations |
| `MemberSyncEnabled` | bool | true | Enable member sync |
| `MemberSyncReconciliationIntervalHours` | int | 24 | Interval between full reconciliation syncs |
| `MemberSyncBatchSize` | int | 500 | Database upsert batch size |
| `MemberSyncApiDelayMs` | int | 1100 | Delay between Discord API requests (rate-limit) |
| `HourlyAggregationIntervalMinutes` | int | 60 | Interval between hourly analytics aggregations |
| `DailyAggregationHourUtc` | int | 0 | UTC hour for daily aggregation |

```json
{
  "BackgroundServices": {
    "TokenRefreshIntervalMinutes": 30,
    "MetricsUpdateIntervalSeconds": 30,
    "MemberSyncEnabled": true,
    "MemberSyncReconciliationIntervalHours": 24,
    "MemberSyncBatchSize": 500,
    "MemberSyncApiDelayMs": 1100
  }
}
```

---

### Log Sanitization Configuration

#### LogSanitizationOptions — section `LogSanitization`

Controls redaction of sensitive values in log output.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | true | Enable log sanitization |
| `CustomPatterns` | object | {} | Named custom regex patterns (`Pattern` + `Replacement`) |
| `AdditionalSensitiveKeys` | string[] | [] | Additional key names to fully redact |

```json
{
  "LogSanitization": {
    "Enabled": true,
    "CustomPatterns": {},
    "AdditionalSensitiveKeys": []
  }
}
```

---

### Observability Configuration

#### ObservabilityOptions — section `Observability`

Optional links to external observability dashboards. Both URLs are nullable with no defaults; leave null/empty to hide the corresponding admin-sidebar link.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KibanaUrl` | string? | null | URL to the Kibana dashboard |
| `SeqUrl` | string? | null | URL to the Seq log-aggregation dashboard (also read by the Serilog Seq sink) |

```json
{
  "Observability": {
    "KibanaUrl": null,
    "SeqUrl": null
  }
}
```

**Note:** Elastic APM is configured under the separate `ElasticApm` section (e.g. `ElasticApm:ServerUrl`), and Elasticsearch log shipping is configured via `ElasticSearch:Url` / `ElasticSearch:ApiKey` (read directly in `Program.cs`), not via `ObservabilityOptions`.

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
