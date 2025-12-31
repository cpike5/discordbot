# Environment-Specific Configuration

This document describes the environment-specific configuration files and their intended use for Development, Staging, and Production environments.

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

## Best Practices

1. **Never commit secrets** - Use user secrets for development and environment variables for production
2. **Verify environment on deploy** - Check startup logs to confirm the correct environment is loaded
3. **Adjust thresholds as needed** - The provided thresholds are starting points; tune based on your traffic patterns
4. **Monitor log volume** - Production logging should be minimal; if logs are too verbose, adjust overrides
5. **Use structured logging** - All logging uses Serilog structured logging for queryability
6. **Retention alignment** - Ensure audit log and message log retention aligns with your privacy policy and compliance requirements
7. **Background service tuning** - Monitor scheduled messages and Rat Watch execution times; adjust timeouts and concurrency based on observed behavior

## Related Documentation

- [Identity Configuration](identity-configuration.md) - Authentication setup per environment
- [Distributed Tracing](tracing.md) - OpenTelemetry distributed tracing setup
- [Audit Log System](audit-log-system.md) - Comprehensive audit logging documentation
- [Message Logging](message-logging.md) - Message logging and consent system
- [Scheduled Messages](scheduled-messages.md) - Scheduled message feature documentation
- [Rat Watch](rat-watch.md) - Rat Watch accountability feature
