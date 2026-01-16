# API Endpoints Reference

This document provides comprehensive reference documentation for the Discord Bot Management System REST API.

## Overview

The REST API provides programmatic access to bot status, guild management, and command log analytics. All endpoints return JSON responses and use standard HTTP status codes.

**Base URL:** `http://localhost:5000/api` (development)

**API Version:** 1.0

**Authentication:** None (MVP - authentication to be added in future releases)

---

## Quick Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check with database connectivity |
| `/metrics` | GET | OpenTelemetry metrics (Prometheus format) |
| `/api/metrics/health` | GET | Overall bot health status |
| `/api/metrics/health/latency` | GET | Latency history with statistics |
| `/api/metrics/health/cpu` | GET | CPU usage history with statistics |
| `/api/metrics/health/connections` | GET | Connection event history |
| `/api/metrics/commands/performance` | GET | Aggregated command performance metrics |
| `/api/metrics/commands/slowest` | GET | Slowest commands by execution time |
| `/api/metrics/commands/throughput` | GET | Command throughput over time |
| `/api/metrics/commands/errors` | GET | Command error breakdown |
| `/api/metrics/api/usage` | GET | Discord API request statistics |
| `/api/metrics/api/rate-limits` | GET | Rate limit hit events |
| `/api/metrics/system/database` | GET | Database query metrics |
| `/api/metrics/system/services` | GET | Background service health status |
| `/api/metrics/system/cache` | GET | Cache hit/miss statistics |
| `/api/metrics/system/history` | GET | Historical system metrics |
| `/api/metrics/system/history/database` | GET | Historical database metrics |
| `/api/metrics/system/history/memory` | GET | Historical memory metrics |
| `/api/alerts/config` | GET | All alert configurations |
| `/api/alerts/config/{metricName}` | GET | Specific alert configuration |
| `/api/alerts/config/{metricName}` | PUT | Update alert configuration |
| `/api/alerts/active` | GET | Currently active incidents |
| `/api/alerts/incidents` | GET | Paginated incident history |
| `/api/alerts/incidents/{id}` | GET | Specific incident by ID |
| `/api/alerts/incidents/{id}/acknowledge` | POST | Acknowledge incident |
| `/api/alerts/incidents/acknowledge-all` | POST | Acknowledge all active incidents |
| `/api/alerts/summary` | GET | Active alert summary statistics |
| `/api/alerts/stats` | GET | Alert frequency statistics |
| `/api/bot/status` | GET | Bot status (uptime, latency, guilds) |
| `/api/bot/guilds` | GET | Connected guilds from Discord |
| `/api/bot/restart` | POST | Restart bot (not supported) |
| `/api/bot/shutdown` | POST | Graceful shutdown |
| `/api/guilds` | GET | All guilds (DB + Discord merged) |
| `/api/guilds/{id}` | GET | Specific guild by ID |
| `/api/guilds/{id}` | PUT | Update guild settings |
| `/api/guilds/{id}/sync` | POST | Sync guild from Discord to DB |
| `/api/guilds/{guildId}/welcome` | GET | Get welcome configuration |
| `/api/guilds/{guildId}/welcome` | PUT | Update welcome configuration |
| `/api/guilds/{guildId}/welcome/preview` | POST | Preview welcome message |
| `/api/guilds/{guildId}/scheduled-messages` | GET | List scheduled messages |
| `/api/guilds/{guildId}/scheduled-messages/{id}` | GET | Get scheduled message |
| `/api/guilds/{guildId}/scheduled-messages` | POST | Create scheduled message |
| `/api/guilds/{guildId}/scheduled-messages/{id}` | PUT | Update scheduled message |
| `/api/guilds/{guildId}/scheduled-messages/{id}` | DELETE | Delete scheduled message |
| `/api/guilds/{guildId}/scheduled-messages/{id}/execute` | POST | Execute scheduled message immediately |
| `/api/guilds/{guildId}/scheduled-messages/validate-cron` | POST | Validate cron expression |
| `/api/guilds/{guildId}/members` | GET | List guild members (filtered, paginated) |
| `/api/guilds/{guildId}/members/{userId}` | GET | Get specific guild member by user ID |
| `/api/guilds/{guildId}/members/export` | GET | Export guild members to CSV |
| `/api/commandlogs` | GET | Query command logs (filtered, paginated) |
| `/api/commandlogs/stats` | GET | Command usage statistics |
| `/api/auditlogs` | GET | Query audit logs (filtered, paginated) |
| `/api/auditlogs/{id}` | GET | Get specific audit log by ID |
| `/api/auditlogs/stats` | GET | Audit log statistics |
| `/api/auditlogs/by-correlation/{correlationId}` | GET | Get audit logs by correlation ID |
| `/api/messages` | GET | Query message logs (filtered, paginated) |
| `/api/messages/{id}` | GET | Get specific message log by ID |
| `/api/messages/stats` | GET | Message statistics |
| `/api/messages/user/{userId}` | DELETE | Delete all messages for a user (GDPR) |
| `/api/messages/cleanup` | POST | Manually trigger message cleanup |
| `/api/messages/export` | GET | Export messages to CSV |
| `/api/guilds/{guildId}/flagged-events` | GET | List pending flagged events |
| `/api/guilds/{guildId}/flagged-events/{id}` | GET | Get specific flagged event |
| `/api/guilds/{guildId}/flagged-events/{id}/dismiss` | POST | Dismiss flagged event |
| `/api/guilds/{guildId}/flagged-events/{id}/acknowledge` | POST | Acknowledge flagged event |
| `/api/guilds/{guildId}/flagged-events/{id}/action` | POST | Take action on flagged event |
| `/api/guilds/{guildId}/cases` | GET | List moderation cases |
| `/api/guilds/{guildId}/cases/{caseId}` | GET | Get case by GUID ID |
| `/api/guilds/{guildId}/cases/number/{caseNumber}` | GET | Get case by case number |
| `/api/guilds/{guildId}/cases` | POST | Create moderation case |
| `/api/guilds/{guildId}/cases/number/{caseNumber}/reason` | PATCH | Update case reason |
| `/api/guilds/{guildId}/users/{userId}/cases` | GET | Get user's cases |
| `/api/guilds/{guildId}/users/{userId}/notes` | GET | Get user's mod notes |
| `/api/guilds/{guildId}/users/{userId}/notes` | POST | Create mod note |
| `/api/guilds/{guildId}/users/{userId}/flags` | GET | Get user's flagged events |
| `/api/guilds/{guildId}/users/{userId}/tags` | GET | Get user's tags |
| `/api/guilds/{guildId}/tags` | GET | List guild tags |
| `/api/guilds/{guildId}/tags` | POST | Create tag |
| `/api/guilds/{guildId}/tags/{tagName}` | DELETE | Delete tag |
| `/api/guilds/{guildId}/tags/import-templates` | POST | Import template tags |
| `/api/guilds/{guildId}/users/{userId}/tags/{tagName}` | POST | Apply tag to user |
| `/api/guilds/{guildId}/users/{userId}/tags/{tagName}` | DELETE | Remove tag from user |
| `/api/guilds/{guildId}/watchlist` | GET | List watchlist |
| `/api/guilds/{guildId}/watchlist` | POST | Add user to watchlist |
| `/api/guilds/{guildId}/watchlist/{userId}` | DELETE | Remove user from watchlist |
| `/api/guilds/{guildId}/moderation-config` | GET | Get moderation config |
| `/api/guilds/{guildId}/moderation-config` | PUT | Update moderation config |
| `/api/guilds/{guildId}/moderation-config/preset` | POST | Apply config preset |
| `/api/autocomplete/users` | GET | Search users by username |
| `/api/autocomplete/guilds` | GET | Search guilds by name |
| `/api/autocomplete/channels` | GET | Search channels by name within a guild |
| `/api/autocomplete/commands` | GET | Search registered commands by name |
| `/api/theme/available` | GET | List all active themes |
| `/api/theme/current` | GET | Get user's current effective theme |
| `/api/theme/user` | POST | Set user's theme preference |
| `/api/theme/default` | POST | Set system default theme (SuperAdmin) |

---

## Health Endpoints

### GET /api/health

Returns the health status of the application including database connectivity.

**Response: 200 OK**

```json
{
  "status": "Healthy",
  "timestamp": "2024-12-08T15:30:00Z",
  "version": "1.0.0.0",
  "checks": {
    "Database": "Healthy"
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | Overall status: "Healthy" or "Degraded" |
| `timestamp` | datetime | UTC timestamp of health check |
| `version` | string | Application version |
| `checks` | object | Individual health check results |

**Status Values:**
- `Healthy`: All checks passed
- `Degraded`: One or more checks failed (still operational)

---

## Metrics Endpoints

### GET /metrics

Returns OpenTelemetry metrics in Prometheus text exposition format for monitoring and observability.

**Response: 200 OK (text/plain)**

**Content-Type:** `text/plain; version=0.0.4`

**Response Format:** Prometheus text format

```
# HELP discordbot_command_count Total number of Discord commands executed
# TYPE discordbot_command_count counter
discordbot_command_count{command="ping",status="success"} 1250
discordbot_command_count{command="status",status="success"} 85
discordbot_command_count{command="verify",status="failure"} 12

# HELP discordbot_command_duration Duration of command execution in milliseconds
# TYPE discordbot_command_duration histogram
discordbot_command_duration_bucket{command="ping",status="success",le="5"} 0
discordbot_command_duration_bucket{command="ping",status="success",le="10"} 250
discordbot_command_duration_bucket{command="ping",status="success",le="25"} 1200
discordbot_command_duration_bucket{command="ping",status="success",le="+Inf"} 1250
discordbot_command_duration_sum{command="ping",status="success"} 15234.5
discordbot_command_duration_count{command="ping",status="success"} 1250

# HELP discordbot_guilds_active Number of guilds the bot is connected to
# TYPE discordbot_guilds_active gauge
discordbot_guilds_active 5

# HELP process_runtime_dotnet_gc_collections_count Number of garbage collections
# TYPE process_runtime_dotnet_gc_collections_count counter
process_runtime_dotnet_gc_collections_count{generation="gen0"} 42
process_runtime_dotnet_gc_collections_count{generation="gen1"} 18
process_runtime_dotnet_gc_collections_count{generation="gen2"} 3
```

**Metric Categories:**

| Category | Prefix | Description |
|----------|--------|-------------|
| Bot Commands | `discordbot.command.*` | Command execution metrics |
| Components | `discordbot.component.*` | Interactive component metrics |
| API Requests | `discordbot.api.*` | HTTP request metrics |
| Rate Limits | `discordbot.ratelimit.*` | Rate limit violation tracking |
| Bot Status | `discordbot.guilds.*`, `discordbot.users.*` | Guild and user counts |
| ASP.NET Core | `http.server.*` | Built-in HTTP server metrics |
| Runtime | `process.runtime.dotnet.*` | .NET runtime metrics (GC, threads, etc.) |

**Example Usage:**

```bash
# Fetch metrics directly
curl http://localhost:5000/metrics

# Use with Prometheus scrape configuration
# See docs/articles/metrics.md for full setup guide
```

**Notes:**
- Metrics are updated in real-time as bot operations occur
- Prometheus scraping is recommended with 15-second intervals
- See [Metrics Documentation](metrics.md) for complete metric definitions and Grafana dashboard setup
- Observable gauges (guild count, user count) are updated every 30 seconds

**Security Considerations:**
- In production, consider protecting this endpoint with IP whitelisting or authentication
- Metrics do not contain sensitive user data or message content
- Guild IDs and user IDs are not included in metrics to prevent cardinality explosion

**Related Documentation:**
- [Metrics Documentation](metrics.md) - Complete metrics reference and setup guide
- [Distributed Tracing](tracing.md) - OpenTelemetry distributed tracing setup

---

## Performance Metrics API

The Performance Metrics API provides structured JSON endpoints for monitoring bot performance, health, and operational metrics. All endpoints require authentication with the `RequireViewer` policy (minimum Viewer role).

**Base Path:** `/api/metrics`

**Authorization:** All endpoints require authentication (minimum Viewer role)

---

### Health Endpoints

#### GET /api/metrics/health

Returns overall bot health status including uptime, latency, CPU usage, and connection state.

**Response: 200 OK**

```json
{
  "status": "Healthy",
  "uptime": "2.15:34:22",
  "latencyMs": 45,
  "cpuUsagePercent": 12.5,
  "connectionState": "Connected",
  "timestamp": "2024-12-08T15:30:00Z"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | Overall health status (Healthy, Degraded, Unhealthy) |
| `uptime` | string | Bot uptime in TimeSpan format (d.hh:mm:ss) |
| `latencyMs` | int | Current gateway latency in milliseconds |
| `cpuUsagePercent` | double | Current CPU usage percentage (0-100) |
| `connectionState` | string | Discord gateway connection state |
| `timestamp` | string | ISO 8601 timestamp of the response |

---

#### GET /api/metrics/health/latency

Returns latency history with samples and statistical analysis.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-168 | Time range for latency history |

**Example Request:**

```bash
GET /api/metrics/health/latency?hours=48
```

**Response: 200 OK**

```json
{
  "samples": [
    {
      "timestamp": "2024-12-08T15:30:00Z",
      "latencyMs": 45
    },
    {
      "timestamp": "2024-12-08T15:29:00Z",
      "latencyMs": 42
    }
  ],
  "statistics": {
    "average": 43.5,
    "minimum": 38,
    "maximum": 52,
    "p50": 43,
    "p95": 50,
    "p99": 52
  },
  "timeRange": {
    "start": "2024-12-06T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 48
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `samples` | array | Latency samples with timestamp and value |
| `statistics.average` | double | Average latency in milliseconds |
| `statistics.minimum` | int | Minimum latency observed |
| `statistics.maximum` | int | Maximum latency observed |
| `statistics.p50` | double | 50th percentile (median) latency |
| `statistics.p95` | double | 95th percentile latency |
| `statistics.p99` | double | 99th percentile latency |
| `timeRange` | object | Time range parameters of the query |

---

#### GET /api/metrics/health/cpu

Returns CPU usage history with samples and statistical analysis.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-720 | Time range for CPU history |

**Example Request:**

```bash
GET /api/metrics/health/cpu?hours=24
```

**Response: 200 OK**

```json
{
  "samples": [
    {
      "timestamp": "2024-12-08T15:30:00Z",
      "cpuPercent": 12.5
    },
    {
      "timestamp": "2024-12-08T15:29:55Z",
      "cpuPercent": 10.2
    }
  ],
  "statistics": {
    "average": 11.3,
    "min": 2.1,
    "max": 45.6,
    "p50": 10.5,
    "p95": 35.2,
    "p99": 42.1,
    "sampleCount": 17280
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `samples` | array | CPU samples with timestamp and percentage |
| `samples[].timestamp` | string | ISO 8601 timestamp when sample was recorded |
| `samples[].cpuPercent` | double | CPU usage percentage (0-100) |
| `statistics.average` | double | Average CPU usage percentage |
| `statistics.min` | double | Minimum CPU usage observed |
| `statistics.max` | double | Maximum CPU usage observed |
| `statistics.p50` | double | 50th percentile (median) CPU usage |
| `statistics.p95` | double | 95th percentile CPU usage |
| `statistics.p99` | double | 99th percentile CPU usage |
| `statistics.sampleCount` | int | Number of samples in the time range |

---

#### GET /api/metrics/health/connections

Returns connection event history with statistics.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `days` | int | No | 7 | 1-30 | Time range for connection events |

**Example Request:**

```bash
GET /api/metrics/health/connections?days=14
```

**Response: 200 OK**

```json
{
  "events": [
    {
      "timestamp": "2024-12-08T10:15:32Z",
      "eventType": "Disconnected",
      "reason": "Gateway timeout"
    },
    {
      "timestamp": "2024-12-08T10:15:45Z",
      "eventType": "Connected",
      "reason": "Reconnection successful"
    }
  ],
  "statistics": {
    "totalEvents": 12,
    "disconnections": 6,
    "reconnections": 6,
    "currentUptime": "5.08:22:15",
    "averageUptime": "18:45:32"
  },
  "timeRange": {
    "start": "2024-11-24T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "days": 14
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `events` | array | Connection events (Connected, Disconnected) |
| `statistics.totalEvents` | int | Total number of connection events |
| `statistics.disconnections` | int | Number of disconnection events |
| `statistics.reconnections` | int | Number of reconnection events |
| `statistics.currentUptime` | string | Current continuous uptime |
| `statistics.averageUptime` | string | Average uptime between disconnections |
| `timeRange` | object | Time range parameters of the query |

---

### Command Performance Endpoints

#### GET /api/metrics/commands/performance

Returns aggregated command performance metrics with percentile analysis.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-168 | Time range for performance data |

**Example Request:**

```bash
GET /api/metrics/commands/performance?hours=24
```

**Response: 200 OK**

```json
{
  "commands": [
    {
      "commandName": "ping",
      "totalExecutions": 1250,
      "successfulExecutions": 1248,
      "failedExecutions": 2,
      "averageDurationMs": 12.3,
      "p50DurationMs": 11.0,
      "p95DurationMs": 18.5,
      "p99DurationMs": 25.2,
      "minDurationMs": 8,
      "maxDurationMs": 45
    }
  ],
  "summary": {
    "totalCommands": 5,
    "totalExecutions": 3420,
    "overallSuccessRate": 99.2
  },
  "timeRange": {
    "start": "2024-12-07T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 24
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `commands` | array | Per-command performance metrics |
| `commandName` | string | Name of the slash command |
| `totalExecutions` | int | Total number of executions |
| `successfulExecutions` | int | Number of successful executions |
| `failedExecutions` | int | Number of failed executions |
| `averageDurationMs` | double | Average execution duration |
| `p50DurationMs` | double | Median execution duration |
| `p95DurationMs` | double | 95th percentile duration |
| `p99DurationMs` | double | 99th percentile duration |
| `summary.overallSuccessRate` | double | Overall success rate percentage |

---

#### GET /api/metrics/commands/slowest

Returns the slowest commands by execution time.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `limit` | int | No | 10 | 1-100 | Maximum number of results |
| `hours` | int | No | 24 | 1-168 | Time range for analysis |

**Example Request:**

```bash
GET /api/metrics/commands/slowest?limit=5&hours=24
```

**Response: 200 OK**

```json
{
  "slowestCommands": [
    {
      "commandName": "analyze",
      "executionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "durationMs": 2345,
      "timestamp": "2024-12-08T14:22:15Z",
      "userId": "123456789012345678",
      "guildId": "987654321098765432",
      "successful": true
    }
  ],
  "timeRange": {
    "start": "2024-12-07T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 24
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `slowestCommands` | array | Slowest command executions |
| `commandName` | string | Name of the command |
| `executionId` | string | Unique execution identifier (GUID) |
| `durationMs` | int | Execution duration in milliseconds |
| `timestamp` | string | ISO 8601 timestamp of execution |
| `userId` | string | Discord user ID who executed the command |
| `guildId` | string | Discord guild ID where command was executed |
| `successful` | bool | Whether the command completed successfully |

---

#### GET /api/metrics/commands/throughput

Returns command throughput over time with configurable granularity.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-168 | Time range for throughput data |
| `granularity` | string | No | "hour" | "hour", "day" | Time bucket granularity |

**Example Request:**

```bash
GET /api/metrics/commands/throughput?hours=48&granularity=hour
```

**Response: 200 OK**

```json
{
  "dataPoints": [
    {
      "timestamp": "2024-12-08T15:00:00Z",
      "commandCount": 142,
      "successCount": 140,
      "errorCount": 2
    },
    {
      "timestamp": "2024-12-08T14:00:00Z",
      "commandCount": 156,
      "successCount": 155,
      "errorCount": 1
    }
  ],
  "summary": {
    "totalCommands": 3420,
    "averagePerPeriod": 142.5,
    "peakPeriod": {
      "timestamp": "2024-12-08T10:00:00Z",
      "count": 287
    }
  },
  "timeRange": {
    "start": "2024-12-06T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 48,
    "granularity": "hour"
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `dataPoints` | array | Throughput measurements per time bucket |
| `timestamp` | string | Start of the time bucket |
| `commandCount` | int | Total commands in this bucket |
| `successCount` | int | Successful commands in this bucket |
| `errorCount` | int | Failed commands in this bucket |
| `summary.averagePerPeriod` | double | Average commands per time bucket |
| `summary.peakPeriod` | object | Highest throughput period |

---

#### GET /api/metrics/commands/errors

Returns command error breakdown by type and recent error details.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-168 | Time range for error analysis |
| `limit` | int | No | 50 | 1-100 | Maximum recent errors to return |

**Example Request:**

```bash
GET /api/metrics/commands/errors?hours=24&limit=10
```

**Response: 200 OK**

```json
{
  "errorSummary": [
    {
      "errorType": "ValidationException",
      "count": 12,
      "percentage": 48.0
    },
    {
      "errorType": "TimeoutException",
      "count": 8,
      "percentage": 32.0
    }
  ],
  "recentErrors": [
    {
      "timestamp": "2024-12-08T15:22:45Z",
      "commandName": "verify",
      "errorType": "ValidationException",
      "errorMessage": "Invalid verification code format",
      "userId": "123456789012345678",
      "guildId": "987654321098765432"
    }
  ],
  "summary": {
    "totalErrors": 25,
    "uniqueErrorTypes": 4,
    "errorRate": 0.73
  },
  "timeRange": {
    "start": "2024-12-07T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 24
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `errorSummary` | array | Error counts grouped by type |
| `errorType` | string | Exception type or error category |
| `count` | int | Number of errors of this type |
| `percentage` | double | Percentage of total errors |
| `recentErrors` | array | Most recent error details |
| `summary.totalErrors` | int | Total number of errors |
| `summary.errorRate` | double | Errors as percentage of total executions |

---

### API Usage Endpoints

#### GET /api/metrics/api/usage

Returns Discord API request statistics by category.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-168 | Time range for API usage |

**Example Request:**

```bash
GET /api/metrics/api/usage?hours=24
```

**Response: 200 OK**

```json
{
  "categories": [
    {
      "category": "Messages",
      "requestCount": 4523,
      "averageLatencyMs": 145,
      "errorCount": 3,
      "errorRate": 0.07
    },
    {
      "category": "Guilds",
      "requestCount": 256,
      "averageLatencyMs": 98,
      "errorCount": 0,
      "errorRate": 0.0
    }
  ],
  "summary": {
    "totalRequests": 8934,
    "totalErrors": 12,
    "overallErrorRate": 0.13,
    "averageLatencyMs": 132
  },
  "timeRange": {
    "start": "2024-12-07T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 24
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `categories` | array | API usage grouped by endpoint category |
| `category` | string | API category (Messages, Guilds, Users, etc.) |
| `requestCount` | int | Number of requests to this category |
| `averageLatencyMs` | double | Average response time in milliseconds |
| `errorCount` | int | Number of failed requests |
| `errorRate` | double | Error rate as percentage |
| `summary.totalRequests` | int | Total API requests across all categories |
| `summary.overallErrorRate` | double | Overall error rate percentage |

---

#### GET /api/metrics/api/rate-limits

Returns Discord API rate limit hit events.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `hours` | int | No | 24 | 1-168 | Time range for rate limit events |

**Example Request:**

```bash
GET /api/metrics/api/rate-limits?hours=24
```

**Response: 200 OK**

```json
{
  "events": [
    {
      "timestamp": "2024-12-08T14:30:22Z",
      "endpoint": "/channels/{id}/messages",
      "bucket": "channel_messages",
      "retryAfterMs": 1250,
      "wasGlobal": false
    }
  ],
  "summary": {
    "totalHits": 5,
    "uniqueBuckets": 2,
    "globalRateLimits": 0,
    "averageRetryMs": 1150
  },
  "timeRange": {
    "start": "2024-12-07T15:30:00Z",
    "end": "2024-12-08T15:30:00Z",
    "hours": 24
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `events` | array | Rate limit hit events |
| `timestamp` | string | When the rate limit was hit |
| `endpoint` | string | Discord API endpoint path |
| `bucket` | string | Discord rate limit bucket identifier |
| `retryAfterMs` | int | Milliseconds to wait before retry |
| `wasGlobal` | bool | Whether this was a global rate limit |
| `summary.totalHits` | int | Total rate limit hits |
| `summary.globalRateLimits` | int | Number of global rate limits hit |

---

### System Health Endpoints

#### GET /api/metrics/system/database

Returns database query metrics and recent slow queries.

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `limit` | int | No | 20 | 1-100 | Maximum slow queries to return |

**Example Request:**

```bash
GET /api/metrics/system/database?limit=10
```

**Response: 200 OK**

```json
{
  "queryMetrics": {
    "totalQueries": 12450,
    "averageDurationMs": 8.5,
    "slowQueryThresholdMs": 100,
    "slowQueryCount": 15
  },
  "slowQueries": [
    {
      "query": "SELECT * FROM CommandLogs WHERE GuildId = @p0 ORDER BY ExecutedAt DESC",
      "durationMs": 245,
      "timestamp": "2024-12-08T15:10:32Z",
      "database": "discordbot"
    }
  ],
  "connectionPool": {
    "activeConnections": 3,
    "idleConnections": 7,
    "maxPoolSize": 100,
    "waitingRequests": 0
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `queryMetrics.totalQueries` | int | Total queries executed |
| `queryMetrics.averageDurationMs` | double | Average query duration |
| `queryMetrics.slowQueryCount` | int | Queries exceeding threshold |
| `slowQueries` | array | Recent slow query details |
| `connectionPool.activeConnections` | int | Active database connections |
| `connectionPool.maxPoolSize` | int | Maximum connection pool size |

---

#### GET /api/metrics/system/services

Returns background service health status.

**Response: 200 OK**

```json
{
  "services": [
    {
      "serviceName": "BotHostedService",
      "status": "Running",
      "uptime": "2.15:34:22",
      "lastError": null,
      "lastErrorTimestamp": null
    },
    {
      "serviceName": "ScheduledMessageService",
      "status": "Running",
      "uptime": "2.15:34:18",
      "lastError": null,
      "lastErrorTimestamp": null
    },
    {
      "serviceName": "MessageCleanupService",
      "status": "Running",
      "uptime": "2.15:34:15",
      "lastError": null,
      "lastErrorTimestamp": null
    }
  ],
  "summary": {
    "totalServices": 3,
    "runningServices": 3,
    "stoppedServices": 0,
    "faultedServices": 0
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `services` | array | Individual service status |
| `serviceName` | string | Name of the background service |
| `status` | string | Current status (Running, Stopped, Faulted) |
| `uptime` | string | Service uptime duration |
| `lastError` | string | Most recent error message (if any) |
| `lastErrorTimestamp` | string | When the last error occurred |
| `summary.runningServices` | int | Count of healthy running services |
| `summary.faultedServices` | int | Count of services in error state |

---

#### GET /api/metrics/system/cache

Returns cache hit/miss statistics by key prefix.

**Response: 200 OK**

```json
{
  "cacheStats": [
    {
      "keyPrefix": "guild:",
      "hits": 8542,
      "misses": 342,
      "hitRate": 96.2,
      "totalKeys": 45,
      "evictions": 12
    },
    {
      "keyPrefix": "user:",
      "hits": 15234,
      "misses": 892,
      "hitRate": 94.5,
      "totalKeys": 128,
      "evictions": 34
    }
  ],
  "summary": {
    "totalHits": 24876,
    "totalMisses": 1456,
    "overallHitRate": 94.5,
    "totalKeys": 245,
    "memoryUsageMb": 42.5
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `cacheStats` | array | Cache statistics grouped by key prefix |
| `keyPrefix` | string | Cache key prefix (guild:, user:, etc.) |
| `hits` | int | Number of cache hits |
| `misses` | int | Number of cache misses |
| `hitRate` | double | Hit rate as percentage |
| `totalKeys` | int | Number of cached keys |
| `evictions` | int | Number of cache evictions |
| `summary.overallHitRate` | double | Overall cache hit rate |
| `summary.memoryUsageMb` | double | Approximate cache memory usage |

---

#### GET /api/metrics/system/history

Retrieves historical system metric snapshots for charting and trend analysis.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `hours` | int | 24 | Time range in hours (1-720) |
| `metric` | string | "all" | Metric filter: "database", "memory", "cache", "services", or "all" |

**Aggregation Logic:**

The endpoint automatically aggregates data based on the time range requested to optimize response size while maintaining meaningful granularity:

| Hours Requested | Aggregation Strategy |
|----------------|---------------------|
| 1-6 hours | Raw samples (no aggregation) |
| 7-24 hours | 5-minute buckets |
| 25-168 hours (7 days) | 15-minute buckets |
| 169-720 hours (30 days) | 1-hour buckets |

**Response: 200 OK**

```json
{
  "startTime": "2026-01-01T00:00:00Z",
  "endTime": "2026-01-02T00:00:00Z",
  "granularity": "5m",
  "snapshots": [
    {
      "timestamp": "2026-01-01T00:00:00Z",
      "databaseAvgQueryTimeMs": 12.5,
      "databaseTotalQueries": 45230,
      "databaseSlowQueryCount": 3,
      "workingSetMB": 256,
      "privateMemoryMB": 312,
      "heapSizeMB": 128,
      "gen0Collections": 15,
      "gen1Collections": 3,
      "gen2Collections": 0,
      "cacheHitRatePercent": 94.2,
      "cacheTotalEntries": 450,
      "cacheTotalHits": 25000,
      "cacheTotalMisses": 1500,
      "servicesRunningCount": 8,
      "servicesErrorCount": 0,
      "servicesTotalCount": 8
    }
  ]
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `startTime` | datetime | UTC start of time range |
| `endTime` | datetime | UTC end of time range |
| `granularity` | string | Aggregation granularity ("raw", "5m", "15m", "1h") |
| `snapshots` | array | Array of metric snapshots |
| `timestamp` | datetime | UTC timestamp of snapshot |
| `databaseAvgQueryTimeMs` | double | Average database query time in milliseconds |
| `databaseTotalQueries` | int | Total database queries executed |
| `databaseSlowQueryCount` | int | Number of slow queries (>100ms) |
| `workingSetMB` | double | Working set memory in MB |
| `privateMemoryMB` | double | Private memory in MB |
| `heapSizeMB` | double | Managed heap size in MB |
| `gen0Collections` | int | Generation 0 garbage collections |
| `gen1Collections` | int | Generation 1 garbage collections |
| `gen2Collections` | int | Generation 2 garbage collections |
| `cacheHitRatePercent` | double | Cache hit rate percentage |
| `cacheTotalEntries` | int | Total cache entries |
| `cacheTotalHits` | long | Total cache hits |
| `cacheTotalMisses` | long | Total cache misses |
| `servicesRunningCount` | int | Count of running services |
| `servicesErrorCount` | int | Count of services in error state |
| `servicesTotalCount` | int | Total count of services |

**Error Responses:**

**400 Bad Request** - Invalid query parameters

```json
{
  "error": "Bad Request",
  "message": "Parameter 'hours' must be between 1 and 720",
  "timestamp": "2026-01-02T15:30:00Z"
}
```

```json
{
  "error": "Bad Request",
  "message": "Invalid metric parameter. Must be one of: database, memory, cache, services, all",
  "timestamp": "2026-01-02T15:30:00Z"
}
```

---

#### GET /api/metrics/system/history/database

Retrieves historical database performance metrics with aggregated statistics.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `hours` | int | 24 | Time range in hours (1-720) |

**Response: 200 OK**

```json
{
  "startTime": "2026-01-01T00:00:00Z",
  "endTime": "2026-01-02T00:00:00Z",
  "samples": [
    {
      "timestamp": "2026-01-01T12:00:00Z",
      "avgQueryTimeMs": 15.2,
      "totalQueries": 45230,
      "slowQueryCount": 3
    },
    {
      "timestamp": "2026-01-01T13:00:00Z",
      "avgQueryTimeMs": 18.5,
      "totalQueries": 48120,
      "slowQueryCount": 5
    }
  ],
  "statistics": {
    "avgQueryTimeMs": 14.8,
    "minQueryTimeMs": 5.2,
    "maxQueryTimeMs": 85.3,
    "totalSlowQueries": 12
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `startTime` | datetime | UTC start of time range |
| `endTime` | datetime | UTC end of time range |
| `samples` | array | Array of database metric samples |
| `timestamp` | datetime | UTC timestamp of sample |
| `avgQueryTimeMs` | double | Average query execution time in milliseconds |
| `totalQueries` | int | Total number of queries executed |
| `slowQueryCount` | int | Number of slow queries (>100ms) |
| `statistics` | object | Aggregated statistics over the time range |
| `statistics.avgQueryTimeMs` | double | Average query time across all samples |
| `statistics.minQueryTimeMs` | double | Minimum query time observed |
| `statistics.maxQueryTimeMs` | double | Maximum query time observed |
| `statistics.totalSlowQueries` | int | Total slow query count |

**Error Responses:**

**400 Bad Request** - Invalid query parameters

```json
{
  "error": "Bad Request",
  "message": "Parameter 'hours' must be between 1 and 720",
  "timestamp": "2026-01-02T15:30:00Z"
}
```

---

#### GET /api/metrics/system/history/memory

Retrieves historical memory usage metrics with aggregated statistics.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `hours` | int | 24 | Time range in hours (1-720) |

**Response: 200 OK**

```json
{
  "startTime": "2026-01-01T00:00:00Z",
  "endTime": "2026-01-02T00:00:00Z",
  "samples": [
    {
      "timestamp": "2026-01-01T12:00:00Z",
      "workingSetMB": 256,
      "heapSizeMB": 128,
      "privateMemoryMB": 312
    },
    {
      "timestamp": "2026-01-01T13:00:00Z",
      "workingSetMB": 268,
      "heapSizeMB": 135,
      "privateMemoryMB": 325
    }
  ],
  "statistics": {
    "avgWorkingSetMB": 248,
    "maxWorkingSetMB": 312,
    "avgHeapSizeMB": 125
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `startTime` | datetime | UTC start of time range |
| `endTime` | datetime | UTC end of time range |
| `samples` | array | Array of memory metric samples |
| `timestamp` | datetime | UTC timestamp of sample |
| `workingSetMB` | double | Working set memory in MB |
| `heapSizeMB` | double | Managed heap size in MB |
| `privateMemoryMB` | double | Private memory in MB |
| `statistics` | object | Aggregated statistics over the time range |
| `statistics.avgWorkingSetMB` | double | Average working set memory |
| `statistics.maxWorkingSetMB` | double | Maximum working set memory observed |
| `statistics.avgHeapSizeMB` | double | Average heap size |

**Error Responses:**

**400 Bad Request** - Invalid query parameters

```json
{
  "error": "Bad Request",
  "message": "Parameter 'hours' must be between 1 and 720",
  "timestamp": "2026-01-02T15:30:00Z"
}
```

---

**Common Error Responses:**

All Performance Metrics API endpoints may return the following error responses:

**400 Bad Request** - Invalid query parameters

```json
{
  "error": "Bad Request",
  "message": "Parameter 'hours' must be between 1 and 168",
  "timestamp": "2024-12-08T15:30:00Z"
}
```

**401 Unauthorized** - Missing or invalid authentication

```json
{
  "error": "Unauthorized",
  "message": "Authentication required",
  "timestamp": "2024-12-08T15:30:00Z"
}
```

**403 Forbidden** - Insufficient permissions

```json
{
  "error": "Forbidden",
  "message": "Viewer role or higher required",
  "timestamp": "2024-12-08T15:30:00Z"
}
```

**500 Internal Server Error** - Server-side error

```json
{
  "error": "Internal Server Error",
  "message": "An error occurred while processing the request",
  "timestamp": "2024-12-08T15:30:00Z"
}
```

---

### Alerts Endpoints

Performance alert configuration and incident management endpoints.

**Base Path:** `/api/alerts`

#### GET /api/alerts/config

Returns all alert configurations with their current metric values.

**Authorization:** `RequireViewer` policy

**Query Parameters:** None

**Response: 200 OK**

```json
[
  {
    "id": 1,
    "metricName": "gateway_latency",
    "displayName": "Gateway Latency",
    "description": "Discord gateway heartbeat latency",
    "warningThreshold": 100.0,
    "criticalThreshold": 200.0,
    "thresholdUnit": "ms",
    "isEnabled": true,
    "currentValue": 85.5
  },
  {
    "metricName": "command_p95_latency",
    "displayName": "Command P95 Latency",
    "description": "95th percentile command response time",
    "warningThreshold": 300.0,
    "criticalThreshold": 500.0,
    "thresholdUnit": "ms",
    "isEnabled": true,
    "currentValue": 245.3
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | int | Configuration ID |
| `metricName` | string | Internal metric identifier |
| `displayName` | string | Human-readable metric name |
| `description` | string | Description of what triggers alerts |
| `warningThreshold` | double? | Warning-level threshold (null if not set) |
| `criticalThreshold` | double? | Critical-level threshold (null if not set) |
| `thresholdUnit` | string | Unit of measurement (ms, %, MB, count, event) |
| `isEnabled` | bool | Whether alerts are enabled for this metric |
| `currentValue` | double? | Current metric value (null if unavailable) |

---

#### GET /api/alerts/config/{metricName}

Returns a specific alert configuration by metric name.

**Authorization:** `RequireViewer` policy

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `metricName` | string | Internal metric identifier |

**Example Request:**

```bash
GET /api/alerts/config/gateway_latency
```

**Response: 200 OK**

```json
{
  "id": 1,
  "metricName": "gateway_latency",
  "displayName": "Gateway Latency",
  "description": "Discord gateway heartbeat latency",
  "warningThreshold": 100.0,
  "criticalThreshold": 200.0,
  "thresholdUnit": "ms",
  "isEnabled": true,
  "currentValue": 85.5
}
```

**Response: 404 Not Found** - Configuration not found

---

#### PUT /api/alerts/config/{metricName}

Updates an alert configuration with new threshold values or enabled state.

**Authorization:** `RequireAdmin` policy

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `metricName` | string | Internal metric identifier to update |

**Request Body:**

```json
{
  "warningThreshold": 150.0,
  "criticalThreshold": 250.0,
  "isEnabled": true
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `warningThreshold` | double? | No | New warning threshold (null to keep existing) |
| `criticalThreshold` | double? | No | New critical threshold (null to keep existing) |
| `isEnabled` | bool? | No | Enable/disable alerts (null to keep existing) |

**Response: 200 OK**

```json
{
  "id": 1,
  "metricName": "gateway_latency",
  "displayName": "Gateway Latency",
  "description": "Discord gateway heartbeat latency",
  "warningThreshold": 150.0,
  "criticalThreshold": 250.0,
  "thresholdUnit": "ms",
  "isEnabled": true,
  "currentValue": 85.5
}
```

**Response: 400 Bad Request** - Invalid threshold values

**Response: 404 Not Found** - Configuration not found

---

#### GET /api/alerts/active

Returns all currently active (unresolved) incidents.

**Authorization:** `RequireViewer` policy

**Query Parameters:** None

**Response: 200 OK**

```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "metricName": "command_p95_latency",
    "severity": "Warning",
    "status": "Active",
    "triggeredAt": "2026-01-02T10:30:00Z",
    "resolvedAt": null,
    "thresholdValue": 300.0,
    "actualValue": 345.2,
    "message": "Command P95 latency exceeded warning threshold",
    "isAcknowledged": false,
    "acknowledgedBy": null,
    "acknowledgedAt": null,
    "notes": null,
    "durationSeconds": null
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | guid | Unique incident identifier |
| `metricName` | string | Metric that triggered the incident |
| `severity` | string | Alert severity (Info, Warning, Critical) |
| `status` | string | Incident status (Active, Acknowledged, Resolved) |
| `triggeredAt` | datetime | When incident was created (UTC) |
| `resolvedAt` | datetime? | When incident was resolved (UTC, null if active) |
| `thresholdValue` | double | Configured threshold that was breached |
| `actualValue` | double | Actual metric value at trigger time |
| `message` | string | Descriptive incident message |
| `isAcknowledged` | bool | Whether an admin has acknowledged the incident |
| `acknowledgedBy` | string? | User ID who acknowledged (null if not acknowledged) |
| `acknowledgedAt` | datetime? | When acknowledged (UTC, null if not acknowledged) |
| `notes` | string? | Admin notes about the incident |
| `durationSeconds` | double? | Duration from trigger to resolution (null if active) |

---

#### GET /api/alerts/incidents

Returns paginated incident history with optional filtering.

**Authorization:** `RequireViewer` policy

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `pageNumber` | int | No | 1 | Page number (1-based) |
| `pageSize` | int | No | 20 | Items per page |
| `severity` | string | No | - | Filter by severity (Info, Warning, Critical) |
| `status` | string | No | - | Filter by status (Active, Acknowledged, Resolved) |
| `startDate` | datetime | No | - | Filter incidents triggered on or after this date (UTC) |
| `endDate` | datetime | No | - | Filter incidents triggered before this date (UTC) |
| `metricName` | string | No | - | Filter by metric name |

**Example Request:**

```bash
GET /api/alerts/incidents?pageNumber=1&pageSize=20&severity=Critical&status=Resolved
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "metricName": "memory_usage",
      "severity": "Critical",
      "status": "Resolved",
      "triggeredAt": "2026-01-02T08:15:00Z",
      "resolvedAt": "2026-01-02T08:22:30Z",
      "thresholdValue": 480.0,
      "actualValue": 495.3,
      "message": "Memory usage exceeded critical threshold",
      "isAcknowledged": true,
      "acknowledgedBy": "admin@example.com",
      "acknowledgedAt": "2026-01-02T08:18:00Z",
      "notes": "Memory spike during cache refresh, resolved automatically",
      "durationSeconds": 450
    }
  ],
  "totalCount": 125,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 7
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Collection of incidents for current page |
| `totalCount` | int | Total incidents matching query (all pages) |
| `pageNumber` | int | Current page number |
| `pageSize` | int | Items per page |
| `totalPages` | int | Total number of pages |

---

#### GET /api/alerts/incidents/{id}

Returns a specific incident by ID.

**Authorization:** `RequireViewer` policy

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Incident identifier |

**Example Request:**

```bash
GET /api/alerts/incidents/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Response: 200 OK**

Returns single `PerformanceIncidentDto` object (see structure in `/api/alerts/active`).

**Response: 404 Not Found** - Incident not found

---

#### POST /api/alerts/incidents/{id}/acknowledge

Acknowledges an incident, marking it as reviewed by an administrator.

**Authorization:** `RequireAdmin` policy

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Incident identifier to acknowledge |

**Request Body:**

```json
{
  "notes": "Investigated and identified root cause. Threshold adjusted."
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `notes` | string? | No | Optional notes about investigation or resolution |

**Response: 200 OK**

Returns updated `PerformanceIncidentDto` with acknowledgment fields populated.

**Response: 404 Not Found** - Incident not found

---

#### POST /api/alerts/incidents/acknowledge-all

Acknowledges all currently active incidents at once.

**Authorization:** `RequireAdmin` policy

**Request Body:** None required

**Response: 200 OK**

```json
{
  "acknowledgedCount": 5
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `acknowledgedCount` | int | Number of incidents acknowledged |

---

#### GET /api/alerts/summary

Returns active alert summary statistics for dashboard display.

**Authorization:** `RequireViewer` policy

**Query Parameters:** None

**Response: 200 OK**

```json
{
  "activeCount": 3,
  "criticalCount": 1,
  "warningCount": 2,
  "infoCount": 0
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `activeCount` | int | Total number of active incidents |
| `criticalCount` | int | Number of critical-severity active incidents |
| `warningCount` | int | Number of warning-severity active incidents |
| `infoCount` | int | Number of info-severity active incidents |

---

#### GET /api/alerts/stats

Returns alert frequency statistics for charts. Daily incident counts by severity.

**Authorization:** `RequireViewer` policy

**Query Parameters:**

| Parameter | Type | Required | Default | Range | Description |
|-----------|------|----------|---------|-------|-------------|
| `days` | int | No | 30 | 1-365 | Number of days to include |

**Example Request:**

```bash
GET /api/alerts/stats?days=30
```

**Response: 200 OK**

```json
[
  {
    "date": "2026-01-02",
    "criticalCount": 2,
    "warningCount": 5,
    "infoCount": 1
  },
  {
    "date": "2026-01-01",
    "criticalCount": 0,
    "warningCount": 3,
    "infoCount": 0
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `date` | date | Date for this data point (YYYY-MM-DD) |
| `criticalCount` | int | Number of critical incidents on this date |
| `warningCount` | int | Number of warning incidents on this date |
| `infoCount` | int | Number of info incidents on this date |

**Response: 400 Bad Request** - Invalid days parameter

---

## Bot Management Endpoints

### GET /api/bot/status

Returns current bot status including uptime, latency, and connection information.

**Response: 200 OK**

```json
{
  "uptime": "2.15:30:45",
  "guildCount": 5,
  "latencyMs": 42,
  "startTime": "2024-12-06T00:00:00Z",
  "botUsername": "MyDiscordBot#1234",
  "connectionState": "Connected"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `uptime` | TimeSpan | Time since bot started (format: d.HH:mm:ss) |
| `guildCount` | integer | Number of connected guilds |
| `latencyMs` | integer | Gateway latency in milliseconds |
| `startTime` | datetime | UTC timestamp when bot started |
| `botUsername` | string | Bot's Discord username with discriminator |
| `connectionState` | string | Discord connection state |

**Connection States:**
- `Connected`: Bot is connected and operational
- `Connecting`: Bot is establishing connection
- `Disconnected`: Bot is offline
- `Disconnecting`: Bot is shutting down

---

### GET /api/bot/guilds

Returns list of guilds currently connected to the bot via Discord gateway.

**Response: 200 OK**

```json
[
  {
    "id": 123456789012345678,
    "name": "My Awesome Server",
    "memberCount": 1250,
    "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
  },
  {
    "id": 987654321098765432,
    "name": "Dev Testing Server",
    "memberCount": 5,
    "iconUrl": null
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Discord snowflake ID |
| `name` | string | Guild name |
| `memberCount` | integer | Number of members |
| `iconUrl` | string? | CDN URL for guild icon (null if none) |

---

### POST /api/bot/restart

Restarts the bot. **Note:** Currently not supported and will return 500 error.

**Response: 202 Accepted**

```json
(Empty response body on success)
```

**Response: 500 Internal Server Error**

```json
{
  "message": "Restart operation is not supported",
  "detail": "Bot restart is not implemented in the current version",
  "statusCode": 500,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/bot/shutdown

Initiates graceful shutdown of the bot.

**Response: 202 Accepted**

```json
{
  "message": "Shutdown initiated"
}
```

**Notes:**
- Shutdown is asynchronous; the API will remain available briefly
- All pending commands will be completed before shutdown
- Database connections are closed gracefully

---

## Guild Management Endpoints

### GET /api/guilds

Returns all guilds with merged data from database and live Discord information.

**Response: 200 OK**

```json
[
  {
    "id": 123456789012345678,
    "name": "My Awesome Server",
    "joinedAt": "2024-01-15T10:30:00Z",
    "isActive": true,
    "prefix": "!",
    "settings": "{\"welcomeChannel\":\"general\"}",
    "memberCount": 1250,
    "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Discord snowflake ID |
| `name` | string | Guild name (from Discord) |
| `joinedAt` | datetime | When bot joined the guild |
| `isActive` | boolean | Whether guild is active in database |
| `prefix` | string? | Custom command prefix (nullable) |
| `settings` | string? | JSON-encoded guild settings (nullable) |
| `memberCount` | integer? | Current member count from Discord (nullable if offline) |
| `iconUrl` | string? | Guild icon URL from Discord (nullable) |

---

### GET /api/guilds/{id}

Returns detailed information for a specific guild by ID.

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
{
  "id": 123456789012345678,
  "name": "My Awesome Server",
  "joinedAt": "2024-01-15T10:30:00Z",
  "isActive": true,
  "prefix": "!",
  "settings": "{\"welcomeChannel\":\"general\",\"modRole\":\"Moderator\"}",
  "memberCount": 1250,
  "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### PUT /api/guilds/{id}

Updates guild settings in the database.

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "prefix": "?",
  "settings": "{\"welcomeChannel\":\"lobby\",\"modRole\":\"Staff\"}",
  "isActive": true
}
```

**Request Fields:** (all optional)

| Field | Type | Description |
|-------|------|-------------|
| `prefix` | string? | Custom command prefix (null = no change) |
| `settings` | string? | JSON-encoded settings (null = no change) |
| `isActive` | boolean? | Active status (null = no change) |

**Response: 200 OK**

```json
{
  "id": 123456789012345678,
  "name": "My Awesome Server",
  "joinedAt": "2024-01-15T10:30:00Z",
  "isActive": true,
  "prefix": "?",
  "settings": "{\"welcomeChannel\":\"lobby\",\"modRole\":\"Staff\"}",
  "memberCount": 1250,
  "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Request body cannot be null.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/guilds/{id}/sync

Synchronizes guild data from Discord to the database. Creates or updates the guild record with current Discord information.

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
{
  "message": "Guild synced successfully",
  "guildId": 123456789012345678
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 is connected to the bot.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Guild must be currently connected to the bot
- Creates new database record if guild doesn't exist
- Updates name and other Discord-sourced fields if record exists

---

## Guild Members Endpoints

### GET /api/guilds/{guildId}/members

Retrieves a paginated, filtered, and sorted list of guild members. Supports comprehensive filtering by search term, roles, join date, activity date, and active status.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `searchTerm` | string? | null | Search by username, global display name, or nickname (case-insensitive) |
| `roleIds` | List<ulong>? | null | Filter by role IDs (multi-select). Members must have ALL specified roles. |
| `joinedAtStart` | datetime? | null | Filter by join date range start (inclusive) |
| `joinedAtEnd` | datetime? | null | Filter by join date range end (inclusive) |
| `lastActiveAtStart` | datetime? | null | Filter by last active date range start (inclusive) |
| `lastActiveAtEnd` | datetime? | null | Filter by last active date range end (inclusive) |
| `isActive` | boolean? | true | Filter by active status. Null for all, true for active only, false for inactive only. |
| `sortBy` | string | "JoinedAt" | Field to sort by: "Username", "DisplayName", "JoinedAt", or "LastActiveAt" |
| `sortDescending` | boolean | false | Sort in descending order if true |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 25 | Items per page (max: 100) |

**Example Request:**

```
GET /api/guilds/123456789012345678/members?searchTerm=john&page=1&pageSize=50&sortBy=DisplayName&sortDescending=false
```

**Example Request with Role Filtering:**

```
GET /api/guilds/123456789012345678/members?roleIds=111222333444555666&roleIds=777888999000111222&isActive=true
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "userId": 987654321098765432,
      "username": "johndoe",
      "discriminator": "0",
      "globalDisplayName": "John Doe",
      "nickname": "Johnny",
      "avatarHash": "a1b2c3d4e5f6g7h8i9j0",
      "joinedAt": "2024-01-15T10:30:00Z",
      "lastActiveAt": "2024-12-29T18:45:00Z",
      "accountCreatedAt": "2020-03-10T14:20:00Z",
      "roleIds": [111222333444555666, 777888999000111222],
      "roles": [
        {
          "id": 111222333444555666,
          "name": "Moderator",
          "color": 3447003,
          "position": 5
        },
        {
          "id": 777888999000111222,
          "name": "Member",
          "color": 0,
          "position": 1
        }
      ],
      "isActive": true,
      "lastCachedAt": "2024-12-30T08:00:00Z",
      "displayName": "Johnny"
    },
    {
      "userId": 111222333444555777,
      "username": "janesmith",
      "discriminator": "0",
      "globalDisplayName": "Jane Smith",
      "nickname": null,
      "avatarHash": "z9y8x7w6v5u4t3s2r1q0",
      "joinedAt": "2024-02-20T14:15:00Z",
      "lastActiveAt": "2024-12-30T09:30:00Z",
      "accountCreatedAt": "2021-06-15T10:00:00Z",
      "roleIds": [777888999000111222],
      "roles": [
        {
          "id": 777888999000111222,
          "name": "Member",
          "color": 0,
          "position": 1
        }
      ],
      "isActive": true,
      "lastCachedAt": "2024-12-30T08:00:00Z",
      "displayName": "Jane Smith"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of GuildMemberDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**GuildMemberDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `userId` | ulong | Discord user snowflake ID |
| `username` | string | Discord username |
| `discriminator` | string | Discord discriminator (legacy, may be "0" for new usernames) |
| `globalDisplayName` | string? | Discord global display name (nullable) |
| `nickname` | string? | Guild-specific nickname (nullable) |
| `avatarHash` | string? | Discord avatar hash for URL construction (nullable) |
| `joinedAt` | datetime | When the user joined the guild |
| `lastActiveAt` | datetime? | Most recent activity timestamp (nullable) |
| `accountCreatedAt` | datetime? | When the Discord account was created (nullable) |
| `roleIds` | List<ulong> | List of role IDs assigned to this member |
| `roles` | List<GuildRoleDto> | Full role information with names, colors, and positions |
| `isActive` | boolean | Whether the member is currently active in the guild |
| `lastCachedAt` | datetime | When the member data was last synchronized |
| `displayName` | string | Effective display name (nickname > globalDisplayName > username) |

**GuildRoleDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Discord role snowflake ID |
| `name` | string | Role name |
| `color` | uint | Role color as RGB integer |
| `position` | integer | Role position in hierarchy |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid page size",
  "detail": "Page size must be between 1 and 100.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Avatar URL pattern: `https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png`
- Default `isActive` filter is `true` (active members only)
- Role filtering requires ALL specified roles (AND logic, not OR)
- Search term matches username, global display name, or nickname
- Default sort is by `JoinedAt` ascending (oldest first)

---

### GET /api/guilds/{guildId}/members/{userId}

Returns detailed information for a specific guild member by user ID.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | Discord user snowflake ID |

**Response: 200 OK**

```json
{
  "userId": 987654321098765432,
  "username": "johndoe",
  "discriminator": "0",
  "globalDisplayName": "John Doe",
  "nickname": "Johnny",
  "avatarHash": "a1b2c3d4e5f6g7h8i9j0",
  "joinedAt": "2024-01-15T10:30:00Z",
  "lastActiveAt": "2024-12-29T18:45:00Z",
  "accountCreatedAt": "2020-03-10T14:20:00Z",
  "roleIds": [111222333444555666, 777888999000111222],
  "roles": [
    {
      "id": 111222333444555666,
      "name": "Moderator",
      "color": 3447003,
      "position": 5
    },
    {
      "id": 777888999000111222,
      "name": "Member",
      "color": 0,
      "position": 1
    }
  ],
  "isActive": true,
  "lastCachedAt": "2024-12-30T08:00:00Z",
  "displayName": "Johnny"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Member not found",
  "detail": "No member with user ID 987654321098765432 exists in guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/guilds/{guildId}/members/export

Exports guild members matching the query criteria to a CSV file for external analysis or archival. Limited to 10,000 rows maximum.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

Uses the same query parameters as `GET /api/guilds/{guildId}/members` (excluding `page` and `pageSize`):

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `searchTerm` | string? | null | Search by username, global display name, or nickname (case-insensitive) |
| `roleIds` | List<ulong>? | null | Filter by role IDs (multi-select). Members must have ALL specified roles. |
| `joinedAtStart` | datetime? | null | Filter by join date range start (inclusive) |
| `joinedAtEnd` | datetime? | null | Filter by join date range end (inclusive) |
| `lastActiveAtStart` | datetime? | null | Filter by last active date range start (inclusive) |
| `lastActiveAtEnd` | datetime? | null | Filter by last active date range end (inclusive) |
| `isActive` | boolean? | true | Filter by active status. Null for all, true for active only, false for inactive only. |
| `sortBy` | string | "JoinedAt" | Field to sort by: "Username", "DisplayName", "JoinedAt", or "LastActiveAt" |
| `sortDescending` | boolean | false | Sort in descending order if true |

**Example Request:**

```
GET /api/guilds/123456789012345678/members/export?isActive=true&sortBy=JoinedAt&sortDescending=false
```

**Response: 200 OK**

```csv
UserId,Username,Discriminator,GlobalDisplayName,Nickname,AvatarHash,JoinedAt,LastActiveAt,AccountCreatedAt,RoleIds,RoleNames,IsActive,LastCachedAt,DisplayName
987654321098765432,johndoe,0,John Doe,Johnny,a1b2c3d4e5f6g7h8i9j0,2024-01-15T10:30:00Z,2024-12-29T18:45:00Z,2020-03-10T14:20:00Z,"111222333444555666,777888999000111222","Moderator,Member",true,2024-12-30T08:00:00Z,Johnny
111222333444555777,janesmith,0,Jane Smith,,z9y8x7w6v5u4t3s2r1q0,2024-02-20T14:15:00Z,2024-12-30T09:30:00Z,2021-06-15T10:00:00Z,777888999000111222,Member,true,2024-12-30T08:00:00Z,Jane Smith
```

**Response Headers:**

| Header | Value |
|--------|-------|
| `Content-Type` | text/csv |
| `Content-Disposition` | attachment; filename="members-{guildId}-{timestamp}.csv" |

**CSV Columns:**

| Column | Description |
|--------|-------------|
| `UserId` | Discord user snowflake ID |
| `Username` | Discord username |
| `Discriminator` | Discord discriminator (legacy) |
| `GlobalDisplayName` | Discord global display name |
| `Nickname` | Guild-specific nickname |
| `AvatarHash` | Discord avatar hash |
| `JoinedAt` | When the user joined the guild |
| `LastActiveAt` | Most recent activity timestamp |
| `AccountCreatedAt` | When the Discord account was created |
| `RoleIds` | Comma-separated list of role IDs |
| `RoleNames` | Comma-separated list of role names |
| `IsActive` | Whether the member is currently active |
| `LastCachedAt` | When the member data was last synchronized |
| `DisplayName` | Effective display name |

**Notes:**
- Filename includes guild ID and UTC timestamp: `members-{guildId}-yyyyMMdd-HHmmss.csv`
- Export is limited to 10,000 rows maximum
- Pagination parameters are ignored for exports
- All filtering, searching, and sorting parameters are respected
- Large exports may take time to generate
- Role IDs and role names are comma-separated in the CSV

---

## Command Log Endpoints

### GET /api/commandlogs

Retrieves command execution logs with optional filtering and pagination.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `guildId` | ulong? | null | Filter by guild ID |
| `userId` | ulong? | null | Filter by user ID |
| `commandName` | string? | null | Filter by command name (case-sensitive) |
| `startDate` | datetime? | null | Filter logs after this date (inclusive) |
| `endDate` | datetime? | null | Filter logs before this date (inclusive) |
| `successOnly` | boolean? | null | If true, only show successful commands |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 50 | Items per page (max: 100) |

**Example Request:**

```
GET /api/commandlogs?guildId=123456789012345678&successOnly=true&page=1&pageSize=20
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "userId": 987654321098765432,
      "username": "JohnDoe#1234",
      "commandName": "ping",
      "parameters": "{}",
      "executedAt": "2024-12-08T15:30:00Z",
      "responseTimeMs": 42,
      "success": true,
      "errorMessage": null
    },
    {
      "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "userId": 111222333444555666,
      "username": "JaneSmith#5678",
      "commandName": "status",
      "parameters": "{}",
      "executedAt": "2024-12-08T15:25:00Z",
      "responseTimeMs": 156,
      "success": true,
      "errorMessage": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of CommandLogDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**CommandLogDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique log entry identifier |
| `guildId` | ulong? | Guild ID where command was executed (null for DMs) |
| `guildName` | string? | Guild name (null for DMs) |
| `userId` | ulong | User who executed the command |
| `username` | string? | Username of executor |
| `commandName` | string | Name of the command |
| `parameters` | string? | JSON-encoded command parameters |
| `executedAt` | datetime | UTC timestamp of execution |
| `responseTimeMs` | integer | Command execution time in milliseconds |
| `success` | boolean | Whether command succeeded |
| `errorMessage` | string? | Error message if command failed (null if success) |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/commandlogs/stats

Returns command usage statistics, optionally filtered by date.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `since` | datetime? | null | Only count commands since this date (null = all time) |

**Example Request:**

```
GET /api/commandlogs/stats?since=2024-12-01T00:00:00Z
```

**Response: 200 OK**

```json
{
  "ping": 1250,
  "status": 85,
  "shutdown": 12,
  "guilds": 42
}
```

**Response Format:**

Dictionary mapping command names (string) to usage counts (integer).

**Notes:**
- Returns all commands that have been executed
- Counts include both successful and failed executions
- Empty object `{}` returned if no commands match filter

---

## Welcome Configuration Endpoints

### GET /api/guilds/{guildId}/welcome

Returns the welcome configuration for a specific guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
{
  "guildId": 123456789012345678,
  "isEnabled": true,
  "welcomeChannelId": 111222333444555666,
  "welcomeMessage": "Welcome {user} to {guild}! You are member #{memberCount}.",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#5865F2",
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-12-08T15:30:00Z"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `isEnabled` | boolean | Whether welcome messages are enabled |
| `welcomeChannelId` | ulong? | Channel ID where welcome messages are sent (nullable) |
| `welcomeMessage` | string | Welcome message template with placeholders |
| `includeAvatar` | boolean | Whether to include user's avatar |
| `useEmbed` | boolean | Whether to send as embed (rich message) |
| `embedColor` | string? | Hex color code for embed (nullable) |
| `createdAt` | datetime | Configuration creation timestamp |
| `updatedAt` | datetime | Configuration last update timestamp |

**Template Placeholders:**

The `welcomeMessage` field supports the following placeholders:
- `{user}` - User mention (@username)
- `{guild}` - Guild name
- `{memberCount}` - Total member count
- Additional placeholders may be supported

**Response: 404 Not Found**

```json
{
  "message": "Welcome configuration not found",
  "detail": "No welcome configuration exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### PUT /api/guilds/{guildId}/welcome

Updates the welcome configuration for a specific guild. Creates a new configuration if one doesn't exist.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "isEnabled": true,
  "welcomeChannelId": 111222333444555666,
  "welcomeMessage": "Welcome {user} to {guild}! 🎉",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#5865F2"
}
```

**Request Fields:** (all optional for partial update)

| Field | Type | Description |
|-------|------|-------------|
| `isEnabled` | boolean? | Enable/disable welcome messages (null = no change) |
| `welcomeChannelId` | ulong? | Channel ID for welcome messages (null = no change) |
| `welcomeMessage` | string? | Welcome message template (null = no change) |
| `includeAvatar` | boolean? | Include user avatar (null = no change) |
| `useEmbed` | boolean? | Send as embed (null = no change) |
| `embedColor` | string? | Hex color code for embed (null = no change) |

**Response: 200 OK**

```json
{
  "guildId": 123456789012345678,
  "isEnabled": true,
  "welcomeChannelId": 111222333444555666,
  "welcomeMessage": "Welcome {user} to {guild}! 🎉",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#5865F2",
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-12-08T15:30:00Z"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Request body cannot be null.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/guilds/{guildId}/welcome/preview

Generates a preview of the welcome message with template variables replaced. Useful for testing message templates before enabling welcome messages.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "previewUserId": 987654321098765432
}
```

**Request Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `previewUserId` | ulong | Discord user ID to use for preview (required) |

**Response: 200 OK**

```json
{
  "message": "Welcome @JohnDoe to My Awesome Server! You are member #1250."
}
```

**Response: 404 Not Found**

```json
{
  "message": "Welcome configuration not found",
  "detail": "No welcome configuration exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "PreviewUserId must be a valid Discord user ID.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

## Scheduled Messages Endpoints

### GET /api/guilds/{guildId}/scheduled-messages

Returns all scheduled messages for a guild with pagination.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 20 | Items per page (max: 100) |

**Example Request:**

```
GET /api/guilds/123456789012345678/scheduled-messages?page=1&pageSize=20
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "channelId": 111222333444555666,
      "title": "Daily Reminder",
      "content": "Don't forget to check the announcements!",
      "cronExpression": "0 9 * * *",
      "frequency": 3,
      "isEnabled": true,
      "lastExecutedAt": "2024-12-08T09:00:00Z",
      "nextExecutionAt": "2024-12-09T09:00:00Z",
      "createdAt": "2024-01-15T10:30:00Z",
      "createdBy": "user123",
      "updatedAt": "2024-12-08T15:30:00Z",
      "timeUntilNext": "17:30:00"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of ScheduledMessageDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**ScheduledMessageDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique scheduled message identifier |
| `guildId` | ulong | Guild ID where message will be sent |
| `guildName` | string? | Guild name for display (nullable) |
| `channelId` | ulong | Channel ID where message will be sent |
| `title` | string | Message title/name |
| `content` | string | Message content (max 2000 chars) |
| `cronExpression` | string? | Cron expression for custom schedules (nullable) |
| `frequency` | ScheduleFrequency | Schedule frequency enum (see below) |
| `isEnabled` | boolean | Whether message is active |
| `lastExecutedAt` | datetime? | Last execution timestamp (nullable) |
| `nextExecutionAt` | datetime? | Next execution timestamp (nullable) |
| `createdAt` | datetime | Creation timestamp |
| `createdBy` | string | User ID who created the message |
| `updatedAt` | datetime | Last update timestamp |
| `timeUntilNext` | TimeSpan? | Time remaining until next execution (nullable) |

**ScheduleFrequency Enum:**

| Value | Name | Description |
|-------|------|-------------|
| 1 | Once | Send only once at specified time |
| 2 | Hourly | Send every hour |
| 3 | Daily | Send once per day |
| 4 | Weekly | Send once per week |
| 5 | Custom | Use custom cron expression |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid page size",
  "detail": "Page size must be between 1 and 100.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/guilds/{guildId}/scheduled-messages/{id}

Returns a specific scheduled message by ID.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Scheduled message unique identifier |

**Response: 200 OK**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "guildId": 123456789012345678,
  "guildName": "My Awesome Server",
  "channelId": 111222333444555666,
  "title": "Daily Reminder",
  "content": "Don't forget to check the announcements!",
  "cronExpression": "0 9 * * *",
  "frequency": 3,
  "isEnabled": true,
  "lastExecutedAt": "2024-12-08T09:00:00Z",
  "nextExecutionAt": "2024-12-09T09:00:00Z",
  "createdAt": "2024-01-15T10:30:00Z",
  "createdBy": "user123",
  "updatedAt": "2024-12-08T15:30:00Z",
  "timeUntilNext": "17:30:00"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Scheduled message not found",
  "detail": "No scheduled message with ID a1b2c3d4-e5f6-7890-abcd-ef1234567890 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/guilds/{guildId}/scheduled-messages

Creates a new scheduled message.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "guildId": 123456789012345678,
  "channelId": 111222333444555666,
  "title": "Daily Reminder",
  "content": "Don't forget to check the announcements!",
  "cronExpression": "0 9 * * *",
  "frequency": 3,
  "isEnabled": true,
  "nextExecutionAt": "2024-12-09T09:00:00Z",
  "createdBy": "user123"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `guildId` | ulong | Yes | Guild ID (overridden by route parameter) |
| `channelId` | ulong | Yes | Channel ID where message will be sent |
| `title` | string | Yes | Message title (max 200 chars) |
| `content` | string | Yes | Message content (max 2000 chars) |
| `cronExpression` | string? | No | Cron expression (required if frequency is Custom) |
| `frequency` | ScheduleFrequency | Yes | Schedule frequency |
| `isEnabled` | boolean | No | Active status (default: true) |
| `nextExecutionAt` | datetime | Yes | First execution time |
| `createdBy` | string | Yes | User ID creating the message |

**Response: 201 Created**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "guildId": 123456789012345678,
  "guildName": "My Awesome Server",
  "channelId": 111222333444555666,
  "title": "Daily Reminder",
  "content": "Don't forget to check the announcements!",
  "cronExpression": "0 9 * * *",
  "frequency": 3,
  "isEnabled": true,
  "lastExecutedAt": null,
  "nextExecutionAt": "2024-12-09T09:00:00Z",
  "createdAt": "2024-12-08T15:30:00Z",
  "createdBy": "user123",
  "updatedAt": "2024-12-08T15:30:00Z",
  "timeUntilNext": "17:30:00"
}
```

**Response Headers:**

```
Location: /api/guilds/123456789012345678/scheduled-messages/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Request body cannot be null.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### PUT /api/guilds/{guildId}/scheduled-messages/{id}

Updates an existing scheduled message.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Scheduled message unique identifier |

**Request Body:**

```json
{
  "channelId": 111222333444555666,
  "title": "Updated Daily Reminder",
  "content": "New reminder content!",
  "cronExpression": "0 10 * * *",
  "frequency": 3,
  "isEnabled": true,
  "nextExecutionAt": "2024-12-09T10:00:00Z"
}
```

**Request Fields:** (all optional for partial update)

| Field | Type | Description |
|-------|------|-------------|
| `channelId` | ulong? | New channel ID (null = no change) |
| `title` | string? | New title (null = no change, max 200 chars) |
| `content` | string? | New content (null = no change, max 2000 chars) |
| `cronExpression` | string? | New cron expression (null = no change) |
| `frequency` | ScheduleFrequency? | New frequency (null = no change) |
| `isEnabled` | boolean? | New active status (null = no change) |
| `nextExecutionAt` | datetime? | New next execution time (null = no change) |

**Response: 200 OK**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "guildId": 123456789012345678,
  "guildName": "My Awesome Server",
  "channelId": 111222333444555666,
  "title": "Updated Daily Reminder",
  "content": "New reminder content!",
  "cronExpression": "0 10 * * *",
  "frequency": 3,
  "isEnabled": true,
  "lastExecutedAt": "2024-12-08T09:00:00Z",
  "nextExecutionAt": "2024-12-09T10:00:00Z",
  "createdAt": "2024-01-15T10:30:00Z",
  "createdBy": "user123",
  "updatedAt": "2024-12-08T16:00:00Z",
  "timeUntilNext": "18:00:00"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Scheduled message not found",
  "detail": "No scheduled message with ID a1b2c3d4-e5f6-7890-abcd-ef1234567890 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Content cannot exceed 2000 characters (Discord message limit).",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### DELETE /api/guilds/{guildId}/scheduled-messages/{id}

Deletes a scheduled message.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Scheduled message unique identifier |

**Response: 204 No Content**

(Empty response body on success)

**Response: 404 Not Found**

```json
{
  "message": "Scheduled message not found",
  "detail": "No scheduled message with ID a1b2c3d4-e5f6-7890-abcd-ef1234567890 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/guilds/{guildId}/scheduled-messages/{id}/execute

Executes a scheduled message immediately, regardless of its scheduled time. Useful for testing messages or triggering them on demand.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Scheduled message unique identifier |

**Response: 200 OK**

```json
{
  "message": "Scheduled message executed successfully",
  "messageId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Scheduled message not found",
  "detail": "No scheduled message with ID a1b2c3d4-e5f6-7890-abcd-ef1234567890 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 500 Internal Server Error**

```json
{
  "message": "Execution failed",
  "detail": "Failed to execute scheduled message a1b2c3d4-e5f6-7890-abcd-ef1234567890. Check logs for details.",
  "statusCode": 500,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- The message is sent immediately to the configured channel
- `lastExecutedAt` is updated to the current time
- `nextExecutionAt` is recalculated based on the frequency
- Execution errors are logged for troubleshooting

---

### POST /api/guilds/{guildId}/scheduled-messages/validate-cron

Validates a cron expression for correctness before creating or updating a scheduled message.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID (required for route but not used) |

**Request Body:**

```json
{
  "cronExpression": "0 9 * * *"
}
```

**Request Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `cronExpression` | string | Cron expression to validate (required) |

**Response: 200 OK (Valid)**

```json
{
  "isValid": true,
  "message": "Cron expression is valid",
  "cronExpression": "0 9 * * *"
}
```

**Response: 400 Bad Request (Invalid)**

```json
{
  "message": "Invalid cron expression",
  "detail": "Cron expression must have 5 or 6 parts (minute, hour, day, month, day of week, [year]).",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Validates cron expression syntax and format
- Does not validate that the channel or guild exists
- Use this endpoint before creating/updating scheduled messages with custom frequencies

---

## Member Directory Endpoints

### GET /api/guilds/{guildId}/members

Returns a paginated list of guild members with advanced filtering, searching, and sorting capabilities.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SearchTerm` | string? | null | Search by username, display name, or user ID |
| `RoleIds` | ulong[]? | null | Filter by role IDs (members must have ALL roles) |
| `JoinedAtStart` | datetime? | null | Filter members who joined on or after this date |
| `JoinedAtEnd` | datetime? | null | Filter members who joined on or before this date |
| `LastActiveAtStart` | datetime? | null | Filter members active on or after this date |
| `LastActiveAtEnd` | datetime? | null | Filter members active on or before this date |
| `IsActive` | bool? | true | Filter by active status (null = all, true = active, false = inactive) |
| `Page` | integer | 1 | Page number (1-based) |
| `PageSize` | integer | 25 | Items per page (max 100) |
| `SortBy` | string | "JoinedAt" | Sort field: `Username`, `DisplayName`, `JoinedAt`, or `LastActiveAt` |
| `SortDescending` | bool | false | Sort in descending order if true |
| `UserIds` | ulong[]? | null | Filter by specific user IDs (used for bulk export) |

**Example Request:**

```
GET /api/guilds/123456789012345678/members?SearchTerm=john&RoleIds=111222333444555666&Page=1&PageSize=25&SortBy=JoinedAt&SortDescending=true
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "userId": 987654321098765432,
      "username": "johndoe",
      "discriminator": "0",
      "globalDisplayName": "John Doe",
      "nickname": "JD",
      "avatarHash": "abc123def456",
      "joinedAt": "2024-01-15T10:30:00Z",
      "lastActiveAt": "2024-12-08T15:30:00Z",
      "accountCreatedAt": "2020-05-10T08:00:00Z",
      "roleIds": [111222333444555666, 222333444555666777],
      "roles": [
        {
          "id": 111222333444555666,
          "name": "Admin",
          "color": 16711680,
          "position": 10
        },
        {
          "id": 222333444555666777,
          "name": "Moderator",
          "color": 3447003,
          "position": 5
        }
      ],
      "isActive": true,
      "lastCachedAt": "2024-12-08T16:00:00Z",
      "displayName": "JD"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 150,
  "totalPages": 6,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of GuildMemberDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of members matching filters |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**GuildMemberDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `userId` | ulong | Discord user snowflake ID |
| `username` | string | Discord username |
| `discriminator` | string | Discord discriminator (legacy, "0" for new usernames) |
| `globalDisplayName` | string? | User's global display name (nullable) |
| `nickname` | string? | Guild-specific nickname (nullable) |
| `avatarHash` | string? | Discord avatar hash for CDN URL construction (nullable) |
| `joinedAt` | datetime | When the user joined the guild |
| `lastActiveAt` | datetime? | Most recent message activity (nullable) |
| `accountCreatedAt` | datetime? | When the Discord account was created (nullable) |
| `roleIds` | array | Array of role IDs assigned to member |
| `roles` | array | Array of GuildRoleDto objects with full role details |
| `isActive` | boolean | Whether the member is currently active |
| `lastCachedAt` | datetime | When the member data was last synchronized |
| `displayName` | string | Effective display name (nickname > globalDisplayName > username) |

**GuildRoleDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Discord role snowflake ID |
| `name` | string | Role name |
| `color` | uint | Role color as RGB integer (0 = default gray) |
| `position` | integer | Role position in hierarchy |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid page size",
  "detail": "Page size must be between 1 and 100.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Search term matches username, global display name, nickname, and user ID (case-insensitive)
- Role filter uses AND logic (member must have ALL specified roles)
- Avatar URL pattern: `https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png`
- Members are cached per guild for performance (default 15 minutes)

---

### GET /api/guilds/{guildId}/members/{userId}

Returns detailed information for a specific guild member.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | Discord user snowflake ID |

**Example Request:**

```
GET /api/guilds/123456789012345678/members/987654321098765432
```

**Response: 200 OK**

```json
{
  "userId": 987654321098765432,
  "username": "johndoe",
  "discriminator": "0",
  "globalDisplayName": "John Doe",
  "nickname": "JD",
  "avatarHash": "abc123def456",
  "joinedAt": "2024-01-15T10:30:00Z",
  "lastActiveAt": "2024-12-08T15:30:00Z",
  "accountCreatedAt": "2020-05-10T08:00:00Z",
  "roleIds": [111222333444555666, 222333444555666777],
  "roles": [
    {
      "id": 111222333444555666,
      "name": "Admin",
      "color": 16711680,
      "position": 10
    },
    {
      "id": 222333444555666777,
      "name": "Moderator",
      "color": 3447003,
      "position": 5
    }
  ],
  "isActive": true,
  "lastCachedAt": "2024-12-08T16:00:00Z",
  "displayName": "JD"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Member not found",
  "detail": "No member with user ID 987654321098765432 exists in guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Returns member data from cache if available
- If member not cached, fetches from Discord and caches
- Used by the member detail modal in the admin UI

---

### GET /api/guilds/{guildId}/members/export

Exports guild members to a CSV file with optional filtering. Pagination is ignored; all matching members are exported up to the 10,000 row limit.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

Same query parameters as the list endpoint (see `GET /api/guilds/{guildId}/members`), except:
- `Page` and `PageSize` are ignored (exports all matching members)
- `UserIds` can be used to export specific members (bulk export)

**Example Request (All Members):**

```
GET /api/guilds/123456789012345678/members/export
```

**Example Request (Filtered):**

```
GET /api/guilds/123456789012345678/members/export?RoleIds=111222333444555666&JoinedAtStart=2024-01-01T00:00:00Z
```

**Example Request (Selected Members):**

```
GET /api/guilds/123456789012345678/members/export?UserIds=123&UserIds=456&UserIds=789
```

**Response: 200 OK**

Returns a CSV file with the following columns:

| Column | Description |
|--------|-------------|
| UserId | Discord user snowflake ID |
| Username | Discord username |
| DisplayName | Effective display name (nickname > globalDisplayName > username) |
| Nickname | Guild-specific nickname (empty if none) |
| GlobalDisplayName | Global display name (empty if none) |
| JoinedAt | Date joined the guild (ISO 8601 UTC) |
| LastActiveAt | Last message activity (ISO 8601 UTC, empty if never active) |
| AccountCreatedAt | Discord account creation date (ISO 8601 UTC) |
| Roles | Pipe-delimited list of role names (e.g., "Admin\|Moderator") |
| IsActive | Active status (true/false) |

**Example CSV Content:**

```csv
UserId,Username,DisplayName,Nickname,GlobalDisplayName,JoinedAt,LastActiveAt,AccountCreatedAt,Roles,IsActive
987654321098765432,johndoe,JD,JD,John Doe,2024-01-15T10:30:00Z,2024-12-08T15:30:00Z,2020-05-10T08:00:00Z,Admin|Moderator,true
111222333444555666,janedoe,Jane,Jane,,2024-02-20T14:00:00Z,2024-12-07T12:00:00Z,2021-03-15T10:00:00Z,Member,true
```

**Response Headers:**

- `Content-Type: text/csv`
- `Content-Disposition: attachment; filename="members-{guildId}-{timestamp}.csv"`

**Filename Format:** `members-{guildId}-{timestamp}.csv`

**Timestamp Format:** `yyyyMMdd-HHmmss` (UTC)

**Example Filename:** `members-123456789012345678-20241208-153045.csv`

**Notes:**
- Maximum 10,000 rows exported to prevent timeouts
- Large exports may take several seconds
- Filters are applied before export (only matching members are exported)
- CSV uses UTF-8 encoding with BOM for Excel compatibility
- All dates are in ISO 8601 format (UTC)

---

## Audit Log Endpoints

### GET /api/auditlogs

Retrieves audit log entries with optional filtering and pagination. Provides comprehensive tracking of system actions for security and compliance.

**Authorization:** SuperAdmin only

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `category` | AuditLogCategory? | null | Filter by category |
| `action` | AuditLogAction? | null | Filter by action |
| `actorId` | string? | null | Filter by actor ID |
| `actorType` | AuditLogActorType? | null | Filter by actor type |
| `targetType` | string? | null | Filter by target entity type |
| `targetId` | string? | null | Filter by target entity ID |
| `guildId` | ulong? | null | Filter by guild ID |
| `startDate` | datetime? | null | Filter entries after this date (inclusive) |
| `endDate` | datetime? | null | Filter entries before this date (inclusive) |
| `correlationId` | string? | null | Filter by correlation ID |
| `searchTerm` | string? | null | Free-text search in details field |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 20 | Items per page (max: 100) |
| `sortBy` | string | "Timestamp" | Sort field |
| `sortDescending` | boolean | true | Sort direction (default: newest first) |

**Example Request:**

```
GET /api/auditlogs?category=3&actorType=1&page=1&pageSize=20&sortDescending=true
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": 1,
      "timestamp": "2024-12-08T15:30:00Z",
      "category": 3,
      "categoryName": "Configuration",
      "action": 2,
      "actionName": "Updated",
      "actorId": "user123",
      "actorType": 1,
      "actorTypeName": "User",
      "actorDisplayName": "admin@example.com",
      "targetType": "WelcomeConfiguration",
      "targetId": "123456789012345678",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "details": "{\"isEnabled\":true,\"welcomeChannelId\":111222333444555666}",
      "ipAddress": "192.168.1.100",
      "correlationId": "abc123-def456"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of AuditLogDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**AuditLogDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | long | Unique audit log entry identifier |
| `timestamp` | datetime | When the action occurred (UTC) |
| `category` | AuditLogCategory | Category enum value |
| `categoryName` | string | Category name as string |
| `action` | AuditLogAction | Action enum value |
| `actionName` | string | Action name as string |
| `actorId` | string? | ID of the actor who performed the action (nullable) |
| `actorType` | AuditLogActorType | Actor type enum value |
| `actorTypeName` | string | Actor type name as string |
| `actorDisplayName` | string? | Display name of actor (nullable) |
| `targetType` | string? | Entity type that was affected (nullable) |
| `targetId` | string? | ID of affected entity (nullable) |
| `guildId` | ulong? | Guild ID associated with action (nullable) |
| `guildName` | string? | Guild name for display (nullable) |
| `details` | string? | Additional context as JSON string (nullable) |
| `ipAddress` | string? | IP address of actor (nullable) |
| `correlationId` | string? | Correlation ID for related entries (nullable) |

**AuditLogCategory Enum:**

| Value | Name | Description |
|-------|------|-------------|
| 1 | User | User-related actions (login, profile updates, ban, kick) |
| 2 | Guild | Guild-related actions (settings, channel management) |
| 3 | Configuration | Configuration-related actions (bot settings, feature toggles) |
| 4 | Security | Security-related actions (permission changes, role modifications) |
| 5 | Moderation | Moderation actions (warnings, mutes, bans) |
| 6 | System | System-level events (startup, shutdown, errors) |

**AuditLogAction Enum:**

| Value | Name | Description |
|-------|------|-------------|
| 1 | Created | A new entity was created |
| 2 | Updated | An existing entity was updated |
| 3 | Deleted | An entity was deleted |
| 4 | Login | A user logged in |
| 5 | Logout | A user logged out |
| 6 | PasswordReset | Password was reset |
| 7 | PermissionGranted | Permission was granted |
| 8 | PermissionRevoked | Permission was revoked |
| 9 | RoleAssigned | Role was assigned |
| 10 | RoleRemoved | Role was removed |

**AuditLogActorType Enum:**

| Value | Name | Description |
|-------|------|-------------|
| 1 | User | Action was performed by a user (authenticated human) |
| 2 | System | Action was performed by the system (automated process, scheduled task) |
| 3 | Bot | Action was performed by the Discord bot itself |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- All filter parameters are optional
- Combine filters for complex queries
- `correlationId` groups related actions together
- `details` field contains JSON-encoded additional information

---

### GET /api/auditlogs/{id}

Returns a specific audit log entry by ID.

**Authorization:** SuperAdmin only

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Audit log entry identifier |

**Response: 200 OK**

```json
{
  "id": 1,
  "timestamp": "2024-12-08T15:30:00Z",
  "category": 3,
  "categoryName": "Configuration",
  "action": 2,
  "actionName": "Updated",
  "actorId": "user123",
  "actorType": 1,
  "actorTypeName": "User",
  "actorDisplayName": "admin@example.com",
  "targetType": "WelcomeConfiguration",
  "targetId": "123456789012345678",
  "guildId": 123456789012345678,
  "guildName": "My Awesome Server",
  "details": "{\"isEnabled\":true,\"welcomeChannelId\":111222333444555666}",
  "ipAddress": "192.168.1.100",
  "correlationId": "abc123-def456"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Audit log not found",
  "detail": "No audit log entry with ID 1 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/auditlogs/stats

Returns comprehensive audit log statistics including counts, breakdowns by category/action/actor, and temporal metrics.

**Authorization:** SuperAdmin only

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `guildId` | ulong? | null | Filter stats by guild (null = global statistics) |

**Example Request:**

```
GET /api/auditlogs/stats?guildId=123456789012345678
```

**Response: 200 OK**

```json
{
  "totalEntries": 15420,
  "last24Hours": 250,
  "last7Days": 1850,
  "last30Days": 7200,
  "byCategory": {
    "1": 3500,
    "2": 2100,
    "3": 4800,
    "4": 3200,
    "5": 1500,
    "6": 320
  },
  "byAction": {
    "1": 2200,
    "2": 8500,
    "3": 1100,
    "4": 1850,
    "5": 1650,
    "6": 120
  },
  "byActorType": {
    "1": 12000,
    "2": 2800,
    "3": 620
  },
  "topActors": {
    "user123": 850,
    "user456": 720,
    "user789": 650,
    "system": 2800,
    "bot": 620
  },
  "oldestEntry": "2024-01-15T10:30:00Z",
  "newestEntry": "2024-12-08T15:30:00Z"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `totalEntries` | long | Total number of audit log entries |
| `last24Hours` | integer | Entries in the last 24 hours |
| `last7Days` | integer | Entries in the last 7 days |
| `last30Days` | integer | Entries in the last 30 days |
| `byCategory` | Dictionary<AuditLogCategory, int> | Breakdown by category (key is enum value) |
| `byAction` | Dictionary<AuditLogAction, int> | Breakdown by action (key is enum value) |
| `byActorType` | Dictionary<AuditLogActorType, int> | Breakdown by actor type (key is enum value) |
| `topActors` | Dictionary<string, int> | Top 10 most active actors (key is actor ID) |
| `oldestEntry` | datetime? | Timestamp of oldest entry (nullable) |
| `newestEntry` | datetime? | Timestamp of newest entry (nullable) |

**Notes:**
- If `guildId` is specified, statistics are filtered to that guild only
- Dictionary keys are enum integer values, not string names
- `topActors` includes user IDs, "system", and "bot" as keys
- Null timestamps indicate no entries exist

---

### GET /api/auditlogs/by-correlation/{correlationId}

Returns all audit log entries related by correlation ID. Used to trace related events that are part of the same operation or transaction.

**Authorization:** SuperAdmin only

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `correlationId` | string | Correlation ID to search for |

**Example Request:**

```
GET /api/auditlogs/by-correlation/abc123-def456
```

**Response: 200 OK**

```json
[
  {
    "id": 1,
    "timestamp": "2024-12-08T15:30:00Z",
    "category": 3,
    "categoryName": "Configuration",
    "action": 2,
    "actionName": "Updated",
    "actorId": "user123",
    "actorType": 1,
    "actorTypeName": "User",
    "actorDisplayName": "admin@example.com",
    "targetType": "WelcomeConfiguration",
    "targetId": "123456789012345678",
    "guildId": 123456789012345678,
    "guildName": "My Awesome Server",
    "details": "{\"isEnabled\":true}",
    "ipAddress": "192.168.1.100",
    "correlationId": "abc123-def456"
  },
  {
    "id": 2,
    "timestamp": "2024-12-08T15:30:01Z",
    "category": 3,
    "categoryName": "Configuration",
    "action": 2,
    "actionName": "Updated",
    "actorId": "user123",
    "actorType": 1,
    "actorTypeName": "User",
    "actorDisplayName": "admin@example.com",
    "targetType": "WelcomeConfiguration",
    "targetId": "123456789012345678",
    "guildId": 123456789012345678,
    "guildName": "My Awesome Server",
    "details": "{\"welcomeChannelId\":111222333444555666}",
    "ipAddress": "192.168.1.100",
    "correlationId": "abc123-def456"
  }
]
```

**Response Format:**

Returns an array of `AuditLogDto` objects ordered by timestamp (chronological order).

**Notes:**
- Correlation IDs group related audit entries together
- Useful for tracing multi-step operations
- Returns empty array `[]` if no entries match the correlation ID
- Entries are ordered chronologically to show the sequence of events

---

## Message Log Endpoints

### GET /api/messages

Retrieves message logs with optional filtering and pagination. Provides access to logged Discord messages for analytics and moderation.

**Authorization:** Admin+

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `authorId` | ulong? | null | Filter by message author ID |
| `guildId` | ulong? | null | Filter by guild ID |
| `channelId` | ulong? | null | Filter by channel ID |
| `source` | MessageSource? | null | Filter by source (DirectMessage or ServerChannel) |
| `startDate` | datetime? | null | Filter messages after this date (inclusive) |
| `endDate` | datetime? | null | Filter messages before this date (inclusive) |
| `searchTerm` | string? | null | Search message content (case-insensitive) |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 25 | Items per page (max: 100) |

**Example Request:**

```
GET /api/messages?guildId=123456789012345678&page=1&pageSize=50&startDate=2024-12-01T00:00:00Z
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": 1,
      "discordMessageId": 1234567890123456789,
      "authorId": 987654321098765432,
      "authorUsername": "JohnDoe#1234",
      "channelId": 111222333444555666,
      "channelName": "general",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "source": "ServerChannel",
      "content": "Hello, world!",
      "timestamp": "2024-12-08T15:30:00Z",
      "loggedAt": "2024-12-08T15:30:01Z",
      "hasAttachments": false,
      "hasEmbeds": false,
      "replyToMessageId": null
    },
    {
      "id": 2,
      "discordMessageId": 9876543210987654321,
      "authorId": 111222333444555666,
      "authorUsername": "JaneSmith#5678",
      "channelId": 111222333444555666,
      "channelName": "announcements",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "source": "ServerChannel",
      "content": "Check out this cool link!",
      "timestamp": "2024-12-08T15:25:00Z",
      "loggedAt": "2024-12-08T15:25:00Z",
      "hasAttachments": true,
      "hasEmbeds": true,
      "replyToMessageId": 1234567890123456789
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of MessageLogDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**MessageLogDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | long | Unique database identifier |
| `discordMessageId` | ulong | Discord snowflake message ID |
| `authorId` | ulong | User who authored the message |
| `authorUsername` | string? | Username for display (nullable) |
| `channelId` | ulong | Channel ID where message was sent |
| `channelName` | string? | Channel name for display (nullable) |
| `guildId` | ulong? | Guild ID (null for DMs) |
| `guildName` | string? | Guild name for display (nullable) |
| `source` | MessageSource | DirectMessage or ServerChannel |
| `content` | string | Message content/text |
| `timestamp` | datetime | When message was sent on Discord |
| `loggedAt` | datetime | When message was logged to database |
| `hasAttachments` | boolean | Whether message has attachments |
| `hasEmbeds` | boolean | Whether message has embeds |
| `replyToMessageId` | ulong? | ID of message this is replying to (nullable) |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/messages/{id}

Returns detailed information for a specific message log by ID.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Message log database ID |

**Response: 200 OK**

```json
{
  "id": 1,
  "discordMessageId": 1234567890123456789,
  "authorId": 987654321098765432,
  "authorUsername": "JohnDoe#1234",
  "channelId": 111222333444555666,
  "channelName": "general",
  "guildId": 123456789012345678,
  "guildName": "My Awesome Server",
  "source": "ServerChannel",
  "content": "Hello, world!",
  "timestamp": "2024-12-08T15:30:00Z",
  "loggedAt": "2024-12-08T15:30:01Z",
  "hasAttachments": false,
  "hasEmbeds": false,
  "replyToMessageId": null
}
```

**Response: 404 Not Found**

```json
{
  "message": "Message not found",
  "detail": "No message log with ID 1 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/messages/stats

Returns comprehensive message statistics including counts, breakdowns by source, and daily trends.

**Authorization:** Admin+

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `guildId` | ulong? | null | Filter stats by guild (null = global statistics) |

**Example Request:**

```
GET /api/messages/stats?guildId=123456789012345678
```

**Response: 200 OK**

```json
{
  "totalMessages": 15420,
  "dmMessages": 250,
  "serverMessages": 15170,
  "uniqueAuthors": 156,
  "messagesByDay": [
    {
      "date": "2024-12-08",
      "count": 450
    },
    {
      "date": "2024-12-07",
      "count": 380
    },
    {
      "date": "2024-12-06",
      "count": 420
    }
  ],
  "oldestMessage": "2024-11-01T08:15:00Z",
  "newestMessage": "2024-12-08T15:30:00Z"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `totalMessages` | long | Total number of messages logged |
| `dmMessages` | long | Count of direct messages |
| `serverMessages` | long | Count of server channel messages |
| `uniqueAuthors` | long | Number of unique message authors |
| `messagesByDay` | array | Daily message counts for last 7 days |
| `oldestMessage` | datetime? | Timestamp of oldest message (null if none) |
| `newestMessage` | datetime? | Timestamp of newest message (null if none) |

**DailyMessageCount Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `date` | DateOnly | The date for this count |
| `count` | long | Number of messages on this date |

**Notes:**
- `messagesByDay` contains the last 7 days of data
- If `guildId` is specified, statistics are filtered to that guild only
- Null timestamps indicate no messages exist

---

### DELETE /api/messages/user/{userId}

Deletes all message logs for a specific user. Used for GDPR compliance and user data deletion requests.

**Authorization:** SuperAdmin only

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `userId` | ulong | Discord user ID whose messages should be deleted |

**Response: 200 OK**

```json
{
  "deletedCount": 42
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `deletedCount` | integer | Number of message logs deleted |

**Notes:**
- This operation is permanent and cannot be undone
- All messages authored by the specified user are deleted
- Used for GDPR "right to be forgotten" compliance
- Operation is logged for audit purposes

---

### POST /api/messages/cleanup

Manually triggers cleanup of old message logs according to the configured retention policy. Deletes messages older than the retention period in batches.

**Authorization:** SuperAdmin only

**Response: 200 OK**

```json
{
  "deletedCount": 1250
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `deletedCount` | integer | Total number of message logs deleted |

**Notes:**
- Cleanup is normally performed automatically by a background service
- This endpoint allows manual triggering for administrative purposes
- Messages are deleted in batches to avoid database overload
- Only messages older than the configured retention period are deleted
- Operation is logged for audit purposes

---

### GET /api/messages/export

Exports message logs matching the query criteria to a CSV file for external analysis or archival.

**Authorization:** Admin+

**Query Parameters:**

Uses the same query parameters as `GET /api/messages`:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `authorId` | ulong? | null | Filter by message author ID |
| `guildId` | ulong? | null | Filter by guild ID |
| `channelId` | ulong? | null | Filter by channel ID |
| `source` | MessageSource? | null | Filter by source (DirectMessage or ServerChannel) |
| `startDate` | datetime? | null | Filter messages after this date (inclusive) |
| `endDate` | datetime? | null | Filter messages before this date (inclusive) |
| `searchTerm` | string? | null | Search message content (case-insensitive) |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 25 | Items per page (max: 100) |

**Example Request:**

```
GET /api/messages/export?guildId=123456789012345678&startDate=2024-12-01T00:00:00Z
```

**Response: 200 OK**

```csv
Id,DiscordMessageId,AuthorId,AuthorUsername,ChannelId,ChannelName,GuildId,GuildName,Source,Content,Timestamp,LoggedAt,HasAttachments,HasEmbeds,ReplyToMessageId
1,1234567890123456789,987654321098765432,JohnDoe#1234,111222333444555666,general,123456789012345678,My Awesome Server,ServerChannel,"Hello, world!",2024-12-08T15:30:00Z,2024-12-08T15:30:01Z,false,false,
2,9876543210987654321,111222333444555666,JaneSmith#5678,111222333444555666,announcements,123456789012345678,My Awesome Server,ServerChannel,Check out this cool link!,2024-12-08T15:25:00Z,2024-12-08T15:25:00Z,true,true,1234567890123456789
```

**Response Headers:**

| Header | Value |
|--------|-------|
| `Content-Type` | text/csv |
| `Content-Disposition` | attachment; filename="message-logs-yyyyMMddHHmmss.csv" |

**Notes:**
- Filename includes UTC timestamp for uniqueness
- CSV includes all fields from MessageLogDto
- Export respects the same filters as the GET endpoint
- Large exports may take time to generate
- Consider using pagination to limit export size

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

## Theme API Endpoints

The Theme API enables theme preference management for users and administrators. Themes are applied via CSS custom properties with a `data-theme` attribute on the HTML element.

**Authentication:** All theme endpoints require authentication.

### GET /api/theme/available

Returns all active themes available for selection.

**Authorization:** Authenticated users

**Response: 200 OK**

```json
[
  {
    "id": 1,
    "themeKey": "discord-dark",
    "displayName": "Discord Dark",
    "description": "Default dark theme inspired by Discord's interface",
    "colorDefinition": "{\"bgPrimary\":\"#1d2022\",\"bgSecondary\":\"#262a2d\",...}",
    "isActive": true
  },
  {
    "id": 2,
    "themeKey": "purple-dusk",
    "displayName": "Purple Dusk",
    "description": "Light theme with warm beige backgrounds and purple/pink accents",
    "colorDefinition": "{\"bgPrimary\":\"#E8E3DF\",\"bgSecondary\":\"#DAD4D0\",...}",
    "isActive": true
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | integer | Unique theme identifier |
| `themeKey` | string | Programmatic key for the theme (e.g., "discord-dark") |
| `displayName` | string | Human-readable theme name |
| `description` | string? | Optional description of the theme |
| `colorDefinition` | string | JSON object with color palette definitions |
| `isActive` | boolean | Whether the theme is available for selection |

---

### GET /api/theme/current

Returns the current user's effective theme with its source.

**Authorization:** Authenticated users

**Response: 200 OK**

```json
{
  "theme": {
    "id": 1,
    "themeKey": "discord-dark",
    "displayName": "Discord Dark",
    "description": "Default dark theme inspired by Discord's interface",
    "colorDefinition": "{...}",
    "isActive": true
  },
  "source": "User"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `theme` | ThemeDto | The effective theme |
| `source` | string | Source of the theme preference |

**Source Values:**

| Value | Description |
|-------|-------------|
| `User` | User explicitly selected this theme |
| `Admin` | Theme is the admin-configured default |
| `System` | Theme is the system-wide fallback default |

**Response: 401 Unauthorized**

Returned when the request is not authenticated.

---

### POST /api/theme/user

Sets the current user's theme preference. Also sets a cookie for server-side rendering on subsequent page loads.

**Authorization:** Authenticated users

**Request Body:**

```json
{
  "themeId": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `themeId` | integer? | No | Theme ID to set, or null to clear preference |

**Response: 200 OK**

```json
{
  "themeKey": "purple-dusk",
  "message": "Theme preference updated successfully"
}
```

**Response: 200 OK (clearing preference)**

When `themeId` is null, clears the user's preference and returns the default theme:

```json
{
  "themeKey": "discord-dark",
  "message": "Theme preference cleared"
}
```

**Response: 400 Bad Request**

```json
{
  "statusCode": 400,
  "message": "Theme not found or not available"
}
```

**Response: 401 Unauthorized**

Returned when the request is not authenticated.

---

### POST /api/theme/default

Sets the system default theme. Only SuperAdmins can modify the default theme.

**Authorization:** SuperAdmin only

**Request Body:**

```json
{
  "themeId": 1
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `themeId` | integer | Yes | Theme ID to set as system default |

**Response: 200 OK**

```json
{
  "message": "System default theme updated successfully"
}
```

**Response: 400 Bad Request**

```json
{
  "statusCode": 400,
  "message": "Theme not found or not available"
}
```

**Response: 403 Forbidden**

Returned when the user does not have SuperAdmin role.

---

## Moderation Endpoints

The moderation system provides comprehensive tools for managing flagged events, moderation cases, user notes, tags, and watchlists. All endpoints require Admin authorization.

**Authorization:** All moderation endpoints require `RequireAdmin` policy.

### Flagged Events Endpoints

Flagged events are automatically detected potential violations (spam, toxicity, etc.) that require moderator review.

#### GET /api/guilds/{guildId}/flagged-events

Returns all pending flagged events for a guild with pagination.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 20 | Items per page (max: 100) |

**Example Request:**

```
GET /api/guilds/123456789012345678/flagged-events?page=1&pageSize=20
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "guildId": 123456789012345678,
      "userId": 987654321098765432,
      "username": "SpamUser#1234",
      "eventType": "Spam",
      "severity": "Medium",
      "description": "Repeated message detected: 5 identical messages in 10 seconds",
      "detectedAt": "2024-12-08T15:30:00Z",
      "status": "Pending",
      "reviewedById": null,
      "reviewedAt": null,
      "actionTaken": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of FlaggedEventDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**FlaggedEventDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique flagged event identifier |
| `guildId` | ulong | Guild ID where event occurred |
| `userId` | ulong | User ID who triggered the event |
| `username` | string? | Username for display (nullable) |
| `eventType` | string | Type of violation (Spam, Toxicity, etc.) |
| `severity` | string | Severity level (Low, Medium, High, Critical) |
| `description` | string | Event description |
| `detectedAt` | datetime | When event was detected |
| `status` | string | Review status (Pending, Dismissed, Acknowledged, Actioned) |
| `reviewedById` | ulong? | Moderator who reviewed (nullable) |
| `reviewedAt` | datetime? | Review timestamp (nullable) |
| `actionTaken` | string? | Action description if actioned (nullable) |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid page size",
  "detail": "Page size must be between 1 and 100.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

#### GET /api/guilds/{guildId}/flagged-events/{id}

Returns a specific flagged event by ID.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Flagged event unique identifier |

**Response: 200 OK**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "guildId": 123456789012345678,
  "userId": 987654321098765432,
  "username": "SpamUser#1234",
  "eventType": "Spam",
  "severity": "Medium",
  "description": "Repeated message detected: 5 identical messages in 10 seconds",
  "detectedAt": "2024-12-08T15:30:00Z",
  "status": "Pending",
  "reviewedById": null,
  "reviewedAt": null,
  "actionTaken": null
}
```

**Response: 404 Not Found**

```json
{
  "message": "Flagged event not found",
  "detail": "No flagged event with ID a1b2c3d4-e5f6-7890-abcd-ef1234567890 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

#### POST /api/guilds/{guildId}/flagged-events/{id}/dismiss

Dismisses a flagged event (marks as not requiring action).

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Flagged event unique identifier |

**Request Body:**

```json
{
  "reviewerId": 111222333444555666
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reviewerId` | ulong | Yes | Moderator's Discord user ID |

**Response: 200 OK**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "guildId": 123456789012345678,
  "userId": 987654321098765432,
  "username": "SpamUser#1234",
  "eventType": "Spam",
  "severity": "Medium",
  "description": "Repeated message detected: 5 identical messages in 10 seconds",
  "detectedAt": "2024-12-08T15:30:00Z",
  "status": "Dismissed",
  "reviewedById": 111222333444555666,
  "reviewedAt": "2024-12-08T16:00:00Z",
  "actionTaken": null
}
```

---

#### POST /api/guilds/{guildId}/flagged-events/{id}/acknowledge

Acknowledges a flagged event (marks as seen but not yet actioned).

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Flagged event unique identifier |

**Request Body:**

```json
{
  "reviewerId": 111222333444555666
}
```

**Response: 200 OK**

Returns updated FlaggedEventDto with status "Acknowledged".

---

#### POST /api/guilds/{guildId}/flagged-events/{id}/action

Takes action on a flagged event (marks as actioned and records action taken).

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `id` | Guid | Flagged event unique identifier |

**Request Body:**

```json
{
  "reviewerId": 111222333444555666,
  "action": "User warned and message deleted"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reviewerId` | ulong | Yes | Moderator's Discord user ID |
| `action` | string | Yes | Description of action taken |

**Response: 200 OK**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "guildId": 123456789012345678,
  "userId": 987654321098765432,
  "username": "SpamUser#1234",
  "eventType": "Spam",
  "severity": "Medium",
  "description": "Repeated message detected: 5 identical messages in 10 seconds",
  "detectedAt": "2024-12-08T15:30:00Z",
  "status": "Actioned",
  "reviewedById": 111222333444555666,
  "reviewedAt": "2024-12-08T16:00:00Z",
  "actionTaken": "User warned and message deleted"
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Action description is required.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### Moderation Cases Endpoints

Moderation cases track disciplinary actions (warnings, mutes, kicks, bans) with sequential case numbers per guild.

#### GET /api/guilds/{guildId}/cases

Returns moderation cases for a guild with optional filters and pagination.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `type` | CaseType? | null | Filter by case type (Warning, Mute, Kick, Ban, Unmute, Unban) |
| `targetUserId` | ulong? | null | Filter by target user ID |
| `moderatorUserId` | ulong? | null | Filter by moderator user ID |
| `startDate` | datetime? | null | Filter by start date (UTC) |
| `endDate` | datetime? | null | Filter by end date (UTC) |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 20 | Items per page (max: 100) |

**Example Request:**

```
GET /api/guilds/123456789012345678/cases?type=Warning&page=1&pageSize=20
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "guildId": 123456789012345678,
      "caseNumber": 42,
      "type": "Warning",
      "targetUserId": 987654321098765432,
      "targetUsername": "RuleBreaker#5678",
      "moderatorUserId": 111222333444555666,
      "moderatorUsername": "ModeratorName#1234",
      "reason": "Excessive profanity in general chat",
      "createdAt": "2024-12-08T14:30:00Z",
      "expiresAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**ModerationCaseDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique case identifier |
| `guildId` | ulong | Guild ID |
| `caseNumber` | long | Sequential case number within guild |
| `type` | CaseType | Case type enum (Warning, Mute, Kick, Ban, etc.) |
| `targetUserId` | ulong | User receiving the action |
| `targetUsername` | string? | Target username for display (nullable) |
| `moderatorUserId` | ulong | Moderator who issued the action |
| `moderatorUsername` | string? | Moderator username (nullable) |
| `reason` | string | Reason for action |
| `createdAt` | datetime | When case was created |
| `expiresAt` | datetime? | Expiration for temporary actions (nullable) |

**CaseType Enum:**

| Value | Name | Description |
|-------|------|-------------|
| 1 | Warning | Verbal warning |
| 2 | Mute | Temporary or permanent mute |
| 3 | Kick | User removed from server |
| 4 | Ban | User banned from server |
| 5 | Unmute | Mute revoked |
| 6 | Unban | Ban revoked |

---

#### GET /api/guilds/{guildId}/cases/{caseId}

Returns a specific moderation case by its GUID ID.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `caseId` | Guid | Case unique GUID identifier |

**Response: 200 OK**

Returns ModerationCaseDto object.

**Response: 404 Not Found**

```json
{
  "message": "Moderation case not found",
  "detail": "No moderation case with ID b2c3d4e5-f6a7-8901-bcde-f12345678901 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

#### GET /api/guilds/{guildId}/cases/number/{caseNumber}

Returns a specific moderation case by its sequential case number within the guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `caseNumber` | long | Sequential case number within guild |

**Example Request:**

```
GET /api/guilds/123456789012345678/cases/number/42
```

**Response: 200 OK**

Returns ModerationCaseDto object.

**Response: 404 Not Found**

```json
{
  "message": "Moderation case not found",
  "detail": "No moderation case #42 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

#### POST /api/guilds/{guildId}/cases

Creates a new moderation case.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "guildId": 123456789012345678,
  "type": 1,
  "targetUserId": 987654321098765432,
  "moderatorUserId": 111222333444555666,
  "reason": "Excessive profanity in general chat",
  "expiresAt": null
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `guildId` | ulong | Yes | Guild ID (overridden by route parameter) |
| `type` | CaseType | Yes | Case type (1=Warning, 2=Mute, 3=Kick, 4=Ban, etc.) |
| `targetUserId` | ulong | Yes | User receiving the action |
| `moderatorUserId` | ulong | Yes | Moderator issuing the action |
| `reason` | string | Yes | Reason for action |
| `expiresAt` | datetime? | No | Expiration for temporary actions (nullable) |

**Response: 201 Created**

```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "guildId": 123456789012345678,
  "caseNumber": 42,
  "type": "Warning",
  "targetUserId": 987654321098765432,
  "targetUsername": "RuleBreaker#5678",
  "moderatorUserId": 111222333444555666,
  "moderatorUsername": "ModeratorName#1234",
  "reason": "Excessive profanity in general chat",
  "createdAt": "2024-12-08T14:30:00Z",
  "expiresAt": null
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Reason is required.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

#### PATCH /api/guilds/{guildId}/cases/number/{caseNumber}/reason

Updates the reason for an existing moderation case.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `caseNumber` | long | Sequential case number within guild |

**Request Body:**

```json
{
  "reason": "Updated reason: Repeated violations after previous warning",
  "moderatorId": 111222333444555666
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | New reason for the case |
| `moderatorId` | ulong | Yes | Moderator making the update |

**Response: 200 OK**

Returns updated ModerationCaseDto object.

**Response: 404 Not Found**

```json
{
  "message": "Moderation case not found",
  "detail": "No moderation case #42 exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### User Moderation Endpoints

User-specific moderation endpoints provide access to all moderation data for a particular user.

#### GET /api/guilds/{guildId}/users/{userId}/cases

Returns all moderation cases for a specific user with pagination.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 10 | Items per page (max: 100) |

**Response: 200 OK**

Returns paginated ModerationCaseDto objects for the user.

---

#### GET /api/guilds/{guildId}/users/{userId}/notes

Returns all moderator notes for a specific user.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |

**Response: 200 OK**

```json
[
  {
    "id": "c3d4e5f6-a7b8-9012-cdef-123456789012",
    "guildId": 123456789012345678,
    "userId": 987654321098765432,
    "content": "User has been cooperative after warning",
    "authorUserId": 111222333444555666,
    "authorUsername": "ModeratorName#1234",
    "createdAt": "2024-12-08T15:00:00Z"
  }
]
```

**ModNoteDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique note identifier |
| `guildId` | ulong | Guild ID |
| `userId` | ulong | User the note is about |
| `content` | string | Note content |
| `authorUserId` | ulong | Moderator who created the note |
| `authorUsername` | string? | Author username (nullable) |
| `createdAt` | datetime | When note was created |

---

#### POST /api/guilds/{guildId}/users/{userId}/notes

Creates a new moderator note for a user.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |

**Request Body:**

```json
{
  "content": "User has been cooperative after warning",
  "authorUserId": 111222333444555666
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `content` | string | Yes | Note content |
| `authorUserId` | ulong | Yes | Moderator creating the note |

**Response: 201 Created**

Returns created ModNoteDto object.

---

#### GET /api/guilds/{guildId}/users/{userId}/flags

Returns all flagged events for a specific user.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |

**Response: 200 OK**

Returns array of FlaggedEventDto objects for the user.

---

#### GET /api/guilds/{guildId}/users/{userId}/tags

Returns all tags applied to a specific user.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |

**Response: 200 OK**

```json
[
  {
    "id": "d4e5f6a7-b8c9-0123-def0-123456789013",
    "guildId": 123456789012345678,
    "userId": 987654321098765432,
    "tagId": "e5f6a7b8-c9d0-1234-ef01-234567890134",
    "tagName": "Watch-Spam",
    "appliedById": 111222333444555666,
    "appliedAt": "2024-12-08T14:00:00Z"
  }
]
```

**UserModTagDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique user tag association ID |
| `guildId` | ulong | Guild ID |
| `userId` | ulong | User the tag is applied to |
| `tagId` | Guid | Tag's unique identifier |
| `tagName` | string | Tag name |
| `appliedById` | ulong | Moderator who applied the tag |
| `appliedAt` | datetime | When tag was applied |

---

### Mod Tags Endpoints

Mod tags are customizable labels that can be applied to users (e.g., "Watch-Spam", "Repeat-Offender").

#### GET /api/guilds/{guildId}/tags

Returns all mod tags for a guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
[
  {
    "id": "e5f6a7b8-c9d0-1234-ef01-234567890134",
    "guildId": 123456789012345678,
    "name": "Watch-Spam",
    "color": "#FF5733",
    "createdAt": "2024-12-01T10:00:00Z"
  }
]
```

**ModTagDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique tag identifier |
| `guildId` | ulong | Guild ID |
| `name` | string | Tag name (unique within guild) |
| `color` | string? | Hex color code (nullable) |
| `createdAt` | datetime | When tag was created |

---

#### POST /api/guilds/{guildId}/tags

Creates a new mod tag for a guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "guildId": 123456789012345678,
  "name": "Watch-Spam",
  "color": "#FF5733"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `guildId` | ulong | Yes | Guild ID (overridden by route parameter) |
| `name` | string | Yes | Tag name (unique within guild) |
| `color` | string? | No | Hex color code (nullable) |

**Response: 201 Created**

Returns created ModTagDto object.

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Tag name is required.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

#### DELETE /api/guilds/{guildId}/tags/{tagName}

Deletes a mod tag by name.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `tagName` | string | Tag name to delete |

**Response: 204 No Content**

Tag deleted successfully.

**Response: 404 Not Found**

```json
{
  "message": "Mod tag not found",
  "detail": "No mod tag with name 'Watch-Spam' exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

#### POST /api/guilds/{guildId}/tags/import-templates

Imports predefined template tags for a guild. Template tags are predefined tags with consistent naming and colors that can be quickly imported into a guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
[
  "Watch-Spam",
  "Watch-Harassment",
  "Repeat-Offender"
]
```

**Request Fields:**

An array of template tag names to import. Available template names are defined by the system (e.g., "Watch-Spam", "Watch-Harassment", "Repeat-Offender", "Warning-Issued", "Needs-Attention").

**Response: 200 OK**

```json
3
```

Returns the number of tags successfully imported.

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Template names array cannot be empty.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

Or if invalid template names provided:

```json
{
  "message": "Invalid request",
  "detail": "Unknown template tag name: InvalidTagName",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Only predefined template tag names are accepted
- Duplicate tags (already existing in guild) are skipped
- Returns count of newly imported tags (not including duplicates)
- Template tags have predefined colors that match across guilds

---

#### POST /api/guilds/{guildId}/users/{userId}/tags/{tagName}

Applies a tag to a user.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |
| `tagName` | string | Tag name to apply |

**Request Body:**

```json
{
  "appliedById": 111222333444555666
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `appliedById` | ulong | Yes | Moderator applying the tag |

**Response: 201 Created**

Returns UserModTagDto object.

**Response: 404 Not Found**

```json
{
  "message": "Mod tag not found",
  "detail": "No mod tag with name 'Watch-Spam' exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

#### DELETE /api/guilds/{guildId}/users/{userId}/tags/{tagName}

Removes a tag from a user.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID |
| `tagName` | string | Tag name to remove |

**Response: 204 No Content**

Tag removed successfully.

**Response: 404 Not Found**

```json
{
  "message": "User tag not found",
  "detail": "User 987654321098765432 does not have tag 'Watch-Spam' in guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### Watchlist Endpoints

The watchlist tracks users who require closer monitoring by moderators.

#### GET /api/guilds/{guildId}/watchlist

Returns all watchlist entries for a guild with pagination.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 20 | Items per page (max: 100) |

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "f6a7b8c9-d0e1-2345-f012-345678901235",
      "guildId": 123456789012345678,
      "userId": 987654321098765432,
      "username": "SuspiciousUser#9999",
      "reason": "Multiple spam warnings",
      "addedById": 111222333444555666,
      "addedByUsername": "ModeratorName#1234",
      "addedAt": "2024-12-08T13:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**WatchlistEntryDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique watchlist entry identifier |
| `guildId` | ulong | Guild ID |
| `userId` | ulong | User on watchlist |
| `username` | string? | Username for display (nullable) |
| `reason` | string | Reason for watchlist |
| `addedById` | ulong | Moderator who added to watchlist |
| `addedByUsername` | string? | Moderator username (nullable) |
| `addedAt` | datetime | When user was added to watchlist |

---

#### POST /api/guilds/{guildId}/watchlist

Adds a user to the watchlist.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "guildId": 123456789012345678,
  "userId": 987654321098765432,
  "reason": "Multiple spam warnings",
  "addedByUserId": 111222333444555666
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `guildId` | ulong | Yes | Guild ID (overridden by route parameter) |
| `userId` | ulong | Yes | User to add to watchlist |
| `reason` | string | No | Reason for watchlist (optional) |
| `addedByUserId` | ulong | Yes | Moderator adding to watchlist |

**Response: 201 Created**

Returns WatchlistEntryDto object.

---

#### DELETE /api/guilds/{guildId}/watchlist/{userId}

Removes a user from the watchlist.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |
| `userId` | ulong | User's Discord snowflake ID to remove |

**Response: 204 No Content**

User removed from watchlist successfully.

**Response: 404 Not Found**

```json
{
  "message": "Watchlist entry not found",
  "detail": "User 987654321098765432 is not on the watchlist for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### Moderation Config Endpoints

Moderation configuration controls auto-detection thresholds and behavior per guild.

#### GET /api/guilds/{guildId}/moderation-config

Returns the moderation configuration for a guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
{
  "guildId": 123456789012345678,
  "mode": "Moderate",
  "spamDetectionEnabled": true,
  "spamThreshold": 5,
  "spamTimeWindowSeconds": 10,
  "toxicityDetectionEnabled": true,
  "toxicityThreshold": 0.7,
  "capsDetectionEnabled": true,
  "capsThresholdPercent": 70,
  "updatedAt": "2024-12-08T12:00:00Z"
}
```

**GuildModerationConfigDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `guildId` | ulong | Guild ID |
| `mode` | string | Preset mode (Off, Relaxed, Moderate, Strict, Custom) |
| `spamDetectionEnabled` | boolean | Whether spam detection is enabled |
| `spamThreshold` | integer | Number of messages to trigger spam flag |
| `spamTimeWindowSeconds` | integer | Time window for spam detection |
| `toxicityDetectionEnabled` | boolean | Whether toxicity detection is enabled |
| `toxicityThreshold` | decimal | Toxicity score threshold (0.0-1.0) |
| `capsDetectionEnabled` | boolean | Whether caps lock detection is enabled |
| `capsThresholdPercent` | integer | Percentage of caps to trigger flag (0-100) |
| `updatedAt` | datetime | Last update timestamp |

---

#### PUT /api/guilds/{guildId}/moderation-config

Updates the moderation configuration for a guild.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "guildId": 123456789012345678,
  "mode": "Custom",
  "spamDetectionEnabled": true,
  "spamThreshold": 3,
  "spamTimeWindowSeconds": 5,
  "toxicityDetectionEnabled": true,
  "toxicityThreshold": 0.8,
  "capsDetectionEnabled": false,
  "capsThresholdPercent": 70
}
```

**Response: 200 OK**

Returns updated GuildModerationConfigDto object.

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Spam threshold must be greater than 0.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

#### POST /api/guilds/{guildId}/moderation-config/preset

Applies a preset configuration to a guild (Relaxed, Moderate, or Strict).

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `guildId` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "presetName": "Strict"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `presetName` | string | Yes | Preset name (Relaxed, Moderate, Strict) |

**Preset Configurations:**

| Preset | Spam Threshold | Spam Window | Toxicity Threshold | Caps Threshold |
|--------|---------------|-------------|-------------------|----------------|
| Relaxed | 10 msgs | 30s | 0.9 | 90% |
| Moderate | 5 msgs | 10s | 0.7 | 70% |
| Strict | 3 msgs | 5s | 0.5 | 50% |

**Response: 200 OK**

Returns updated GuildModerationConfigDto object with preset applied.

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Invalid preset name. Valid options: Relaxed, Moderate, Strict.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

## Error Response Format

All error responses follow a consistent format using `ApiErrorDto`.

**Structure:**

```json
{
  "message": "Brief error description",
  "detail": "Detailed explanation of what went wrong",
  "statusCode": 400,
  "traceId": "00-abc123def456-789012-00"
}
```

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `message` | string | Short, user-friendly error message |
| `detail` | string? | Detailed error explanation (optional) |
| `statusCode` | integer | HTTP status code |
| `traceId` | string? | Correlation ID for log tracing (optional) |

**Common Status Codes:**

| Code | Meaning | Common Causes |
|------|---------|---------------|
| 400 | Bad Request | Invalid input, validation failure, malformed JSON |
| 404 | Not Found | Guild not found, resource doesn't exist |
| 500 | Internal Server Error | Unexpected server error, database connection failure |
| 202 | Accepted | Async operation initiated (shutdown, restart) |

---

## Accessing Swagger UI

The API includes interactive Swagger/OpenAPI documentation for testing endpoints directly in your browser.

**URL:** `http://localhost:5000/swagger`

**Features:**
- Interactive endpoint testing with request/response examples
- Schema definitions for all DTOs
- Try-it-out functionality for immediate testing
- Automatic request validation

**Development vs Production:**
- Swagger is enabled by default in Development environment
- Consider disabling Swagger in Production for security

---

## Rate Limiting

**Current Status:** Not implemented in MVP

**Future Considerations:**
- Per-IP rate limiting for API endpoints
- Separate rate limits for read vs write operations
- Rate limit headers in responses (`X-RateLimit-Limit`, `X-RateLimit-Remaining`)

---

## CORS Configuration

**Current Status:** CORS is configured to allow all origins in Development

**Configuration Location:** `Program.cs`

**Future Considerations:**
- Restrict allowed origins in Production
- Configure allowed methods and headers
- Implement credential support for authenticated requests

---

## User Management Service Interface

The User Management Service provides programmatic access to user administration operations. This is a service-layer interface (not a REST API endpoint) used by the admin web UI.

**Interface:** `IUserManagementService`

**Location:** `DiscordBot.Core.Interfaces.IUserManagementService`

**Authorization:** All operations enforce role-based authorization and self-protection rules. See [User Management Guide](user-management.md) for details.

### Service Methods

#### Query Operations

##### GetUsersAsync

Retrieves a paginated list of users with search and filter capabilities.

```csharp
Task<PaginatedResponseDto<UserDto>> GetUsersAsync(
    UserSearchQueryDto query,
    CancellationToken cancellationToken = default);
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `query` | UserSearchQueryDto | Search, filter, and pagination parameters |
| `cancellationToken` | CancellationToken | Cancellation token (optional) |

**UserSearchQueryDto Fields:**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SearchTerm` | string? | null | Search by email, display name, or Discord username |
| `Role` | string? | null | Filter by role (SuperAdmin, Admin, Moderator, Viewer) |
| `IsActive` | bool? | null | Filter by active status |
| `IsDiscordLinked` | bool? | null | Filter by Discord link status |
| `Page` | int | 1 | Page number (1-based) |
| `PageSize` | int | 20 | Items per page |
| `SortBy` | string | "CreatedAt" | Sort field (Email, DisplayName, CreatedAt, LastLoginAt) |
| `SortDescending` | bool | true | Sort direction |

**Returns:** `PaginatedResponseDto<UserDto>` containing user list and pagination metadata.

##### GetUserByIdAsync

Retrieves detailed information for a single user.

```csharp
Task<UserDto?> GetUserByIdAsync(
    string userId,
    CancellationToken cancellationToken = default);
```

**Returns:** `UserDto` if found, `null` otherwise.

##### GetAvailableRolesAsync

Gets the list of roles that the current user is authorized to assign.

```csharp
Task<IReadOnlyList<string>> GetAvailableRolesAsync(
    string currentUserId,
    CancellationToken cancellationToken = default);
```

**Role Assignment Rules:**
- SuperAdmin can assign: SuperAdmin, Admin, Moderator, Viewer
- Admin can assign: Admin, Moderator, Viewer (not SuperAdmin)
- Other roles cannot assign roles

**Returns:** List of role names the user can assign.

#### Create Operations

##### CreateUserAsync

Creates a new user account with the specified email, password, and role.

```csharp
Task<UserManagementResult> CreateUserAsync(
    UserCreateDto request,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**UserCreateDto Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Email` | string | Yes | User email address (must be unique) |
| `DisplayName` | string? | No | Display name (optional) |
| `Password` | string | Yes | Initial password (must meet complexity requirements) |
| `ConfirmPassword` | string | Yes | Password confirmation (must match) |
| `Role` | string | Yes | Initial role (default: "Viewer") |
| `SendWelcomeEmail` | bool | No | Send welcome email (default: true) |

**Validation:**
- Email must be unique
- Password must meet ASP.NET Identity complexity requirements
- Actor must have permission to assign the specified role

**Activity Logged:** `UserCreated` with email and assigned role

**Returns:** `UserManagementResult` with created user data on success.

#### Update Operations

##### UpdateUserAsync

Updates user information including email, display name, active status, and role.

```csharp
Task<UserManagementResult> UpdateUserAsync(
    string userId,
    UserUpdateDto request,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**UserUpdateDto Fields:** (all optional)

| Field | Type | Description |
|-------|------|-------------|
| `DisplayName` | string? | New display name |
| `Email` | string? | New email address |
| `IsActive` | bool? | Active status |
| `Role` | string? | New role |

**Self-Protection:**
- Users cannot change their own active status
- Users cannot change their own role

**Activity Logged:** `UserUpdated` with changed fields

**Returns:** `UserManagementResult` with updated user data.

##### SetUserActiveStatusAsync

Enables or disables a user account.

```csharp
Task<UserManagementResult> SetUserActiveStatusAsync(
    string userId,
    bool isActive,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Self-Protection:** Users cannot disable their own account.

**Activity Logged:** `UserEnabled` or `UserDisabled`

**Returns:** `UserManagementResult` with updated user data.

##### AssignRoleAsync

Assigns a role to a user (removes existing roles).

```csharp
Task<UserManagementResult> AssignRoleAsync(
    string userId,
    string role,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Authorization:**
- Actor must have permission to assign the target role
- SuperAdmin required to assign SuperAdmin role
- Only SuperAdmin can manage other SuperAdmins

**Self-Protection:** Users cannot change their own role.

**Activity Logged:** `RoleAssigned` with old and new roles

**Returns:** `UserManagementResult` with updated user data.

##### RemoveRoleAsync

Removes a specific role from a user.

```csharp
Task<UserManagementResult> RemoveRoleAsync(
    string userId,
    string role,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Self-Protection:** Users cannot remove their own roles.

**Activity Logged:** `RoleRemoved` with removed role

**Returns:** `UserManagementResult` with updated user data.

#### Password Operations

##### ResetPasswordAsync

Performs an admin-initiated password reset, generating a secure temporary password.

```csharp
Task<UserManagementResult> ResetPasswordAsync(
    string userId,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Behavior:**
- Generates a secure 16-character temporary password
- Password includes uppercase, lowercase, digits, and special characters
- Temporary password is returned in the result (`GeneratedPassword` property)
- Old password is immediately invalidated

**Security:** The password is never logged or stored in plain text.

**Activity Logged:** `PasswordReset` (no password details included)

**Returns:** `UserManagementResult` with `GeneratedPassword` containing the temporary password.

#### Discord Linking Operations

##### UnlinkDiscordAccountAsync

Removes the Discord account association from a user.

```csharp
Task<UserManagementResult> UnlinkDiscordAccountAsync(
    string userId,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Behavior:**
- Removes Discord user ID, username, and avatar URL
- User can still log in with email/password after unlinking
- User must re-link Discord account to access Discord-only features

**Activity Logged:** `DiscordUnlinked` with previous Discord username

**Returns:** `UserManagementResult` with updated user data.

#### Activity Log

##### GetActivityLogAsync

Retrieves the audit log of user management actions.

```csharp
Task<PaginatedResponseDto<UserActivityLogDto>> GetActivityLogAsync(
    string? userId,
    int page = 1,
    int pageSize = 50,
    CancellationToken cancellationToken = default);
```

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `userId` | string? | null | Filter by user (null = all users) |
| `page` | int | 1 | Page number |
| `pageSize` | int | 50 | Items per page |

**Returns:** Paginated list of `UserActivityLogDto` entries, ordered by timestamp (newest first).

#### Validation

##### CanManageUserAsync

Checks if an actor has permission to manage a target user.

```csharp
Task<bool> CanManageUserAsync(
    string actorUserId,
    string targetUserId,
    CancellationToken cancellationToken = default);
```

**Rules:**
- Users cannot manage themselves (returns `false`)
- Only SuperAdmins can manage other SuperAdmins
- Admins and SuperAdmins can manage users below SuperAdmin

**Returns:** `true` if actor can manage target user, `false` otherwise.

---

## User Management DTOs

### UserDto

```csharp
{
  "id": string,                    // User ID
  "email": string,                 // Email address
  "displayName": string?,          // Display name (nullable)
  "isActive": bool,                // Active status
  "emailConfirmed": bool,          // Email confirmation status
  "createdAt": DateTime,           // Account creation timestamp
  "lastLoginAt": DateTime?,        // Last login timestamp (nullable)
  "isLockedOut": bool,             // Lockout status (computed)
  "lockoutEnd": DateTimeOffset?,   // Lockout end time (nullable)
  "isDiscordLinked": bool,         // Discord link status
  "discordUserId": ulong?,         // Discord user ID (nullable)
  "discordUsername": string?,      // Discord username (nullable)
  "discordAvatarUrl": string?,     // Discord avatar URL (nullable)
  "roles": string[],               // Assigned roles
  "highestRole": string            // Highest priority role (computed)
}
```

### UserCreateDto

```csharp
{
  "email": string,                 // Email address (required)
  "displayName": string?,          // Display name (optional)
  "password": string,              // Password (required)
  "confirmPassword": string,       // Password confirmation (required)
  "role": string,                  // Initial role (default: "Viewer")
  "sendWelcomeEmail": bool         // Send welcome email (default: true)
}
```

### UserUpdateDto

```csharp
{
  "displayName": string?,          // New display name (optional)
  "email": string?,                // New email (optional)
  "isActive": bool?,               // New active status (optional)
  "role": string?                  // New role (optional)
}
```

### UserManagementResult

```csharp
{
  "succeeded": bool,               // Operation success flag
  "errorCode": string?,            // Error code (nullable)
  "errorMessage": string?,         // Error message (nullable)
  "user": UserDto?,                // Updated user data (nullable)
  "generatedPassword": string?     // Temporary password (password reset only)
}
```

**Common Error Codes:**
- `USER_NOT_FOUND` - User does not exist
- `SELF_MODIFICATION_DENIED` - Cannot modify own account
- `INSUFFICIENT_PERMISSIONS` - Insufficient role privileges
- `INVALID_ROLE` - Role does not exist
- `EMAIL_ALREADY_EXISTS` - Email already in use
- `PASSWORD_VALIDATION_FAILED` - Password does not meet requirements
- `DISCORD_NOT_LINKED` - No Discord account linked

### UserActivityLogDto

```csharp
{
  "id": Guid,                      // Log entry ID
  "actorUserId": string,           // User who performed action
  "actorEmail": string,            // Actor's email
  "targetUserId": string?,         // User affected (nullable)
  "targetEmail": string?,          // Target's email (nullable)
  "action": UserActivityAction,    // Action type (enum)
  "details": string?,              // JSON details (nullable)
  "timestamp": DateTime,           // Action timestamp
  "ipAddress": string?             // Actor's IP address (nullable)
}
```

**UserActivityAction Enum:**
- `UserCreated` - User account created
- `UserUpdated` - User information updated
- `UserDeleted` - User account deleted
- `UserEnabled` - User account enabled
- `UserDisabled` - User account disabled
- `RoleAssigned` - Role assigned to user
- `RoleRemoved` - Role removed from user
- `PasswordReset` - Password reset by admin
- `DiscordLinked` - Discord account linked
- `DiscordUnlinked` - Discord account unlinked
- `AccountLocked` - Account locked (failed login attempts)
- `AccountUnlocked` - Account unlocked
- `LoginSuccess` - Successful login
- `LoginFailed` - Failed login attempt

### UserSearchQueryDto

```csharp
{
  "searchTerm": string?,           // Search filter (optional)
  "role": string?,                 // Role filter (optional)
  "isActive": bool?,               // Active status filter (optional)
  "isDiscordLinked": bool?,        // Discord link filter (optional)
  "page": int,                     // Page number (default: 1)
  "pageSize": int,                 // Items per page (default: 20)
  "sortBy": string,                // Sort field (default: "CreatedAt")
  "sortDescending": bool           // Sort direction (default: true)
}
```

---

## Data Transfer Objects (DTOs)

### HealthResponseDto

```csharp
{
  "status": string,           // "Healthy" or "Degraded"
  "timestamp": DateTime,      // UTC timestamp
  "version": string,          // Application version
  "checks": Dictionary<string, string>  // Check name -> status
}
```

### BotStatusDto

```csharp
{
  "uptime": TimeSpan,         // Time since start
  "guildCount": int,          // Number of guilds
  "latencyMs": int,           // Gateway latency
  "startTime": DateTime,      // Start timestamp
  "botUsername": string,      // Bot username#discriminator
  "connectionState": string   // Discord connection state
}
```

### GuildInfoDto

```csharp
{
  "id": ulong,                // Discord snowflake ID
  "name": string,             // Guild name
  "memberCount": int,         // Member count
  "iconUrl": string?          // Icon URL (nullable)
}
```

### GuildDto

```csharp
{
  "id": ulong,                // Discord snowflake ID
  "name": string,             // Guild name
  "joinedAt": DateTime,       // Join timestamp
  "isActive": bool,           // Active status
  "prefix": string?,          // Custom prefix (nullable)
  "settings": string?,        // JSON settings (nullable)
  "memberCount": int?,        // Live member count (nullable)
  "iconUrl": string?          // Live icon URL (nullable)
}
```

### GuildUpdateRequestDto

```csharp
{
  "prefix": string?,          // New prefix (null = no change)
  "settings": string?,        // New settings JSON (null = no change)
  "isActive": bool?           // New active status (null = no change)
}
```

### CommandLogDto

```csharp
{
  "id": Guid,                 // Unique identifier
  "guildId": ulong?,          // Guild ID (nullable for DMs)
  "guildName": string?,       // Guild name (nullable)
  "userId": ulong,            // User ID
  "username": string?,        // Username (nullable)
  "commandName": string,      // Command name
  "parameters": string?,      // Parameters JSON (nullable)
  "executedAt": DateTime,     // Execution timestamp
  "responseTimeMs": int,      // Response time
  "success": bool,            // Success flag
  "errorMessage": string?     // Error message (nullable)
}
```

### CommandLogQueryDto

```csharp
{
  "guildId": ulong?,          // Filter by guild
  "userId": ulong?,           // Filter by user
  "commandName": string?,     // Filter by command
  "startDate": DateTime?,     // Filter by start date
  "endDate": DateTime?,       // Filter by end date
  "successOnly": bool?,       // Filter successes only
  "page": int,                // Page number (default: 1)
  "pageSize": int             // Page size (default: 50, max: 100)
}
```

### PaginatedResponseDto&lt;T&gt;

```csharp
{
  "items": IReadOnlyList<T>,  // Page items
  "page": int,                // Current page (1-based)
  "pageSize": int,            // Items per page
  "totalCount": int,          // Total items
  "totalPages": int,          // Total pages (calculated)
  "hasNextPage": bool,        // Has next page (calculated)
  "hasPreviousPage": bool     // Has previous page (calculated)
}
```

### MessageLogDto

```csharp
{
  "id": long,                       // Unique database identifier
  "discordMessageId": ulong,        // Discord snowflake message ID
  "authorId": ulong,                // User who authored the message
  "authorUsername": string?,        // Username for display (nullable)
  "channelId": ulong,               // Channel ID where message was sent
  "channelName": string?,           // Channel name for display (nullable)
  "guildId": ulong?,                // Guild ID (null for DMs)
  "guildName": string?,             // Guild name for display (nullable)
  "source": MessageSource,          // DirectMessage or ServerChannel
  "content": string,                // Message content/text
  "timestamp": DateTime,            // When message was sent on Discord
  "loggedAt": DateTime,             // When message was logged to database
  "hasAttachments": bool,           // Whether message has attachments
  "hasEmbeds": bool,                // Whether message has embeds
  "replyToMessageId": ulong?        // ID of message this is replying to (nullable)
}
```

**MessageSource Enum:**
- `DirectMessage` - Message sent in a DM
- `ServerChannel` - Message sent in a guild channel

### MessageLogQueryDto

```csharp
{
  "authorId": ulong?,          // Filter by author ID
  "guildId": ulong?,           // Filter by guild ID
  "channelId": ulong?,         // Filter by channel ID
  "source": MessageSource?,    // Filter by source
  "startDate": DateTime?,      // Filter by start date
  "endDate": DateTime?,        // Filter by end date
  "searchTerm": string?,       // Search message content
  "page": int,                 // Page number (default: 1)
  "pageSize": int              // Page size (default: 25, max: 100)
}
```

### MessageLogStatsDto

```csharp
{
  "totalMessages": long,                        // Total number of messages logged
  "dmMessages": long,                           // Count of direct messages
  "serverMessages": long,                       // Count of server channel messages
  "uniqueAuthors": long,                        // Number of unique message authors
  "messagesByDay": List<DailyMessageCount>,     // Daily message counts for last 7 days
  "oldestMessage": DateTime?,                   // Timestamp of oldest message (nullable)
  "newestMessage": DateTime?                    // Timestamp of newest message (nullable)
}
```

### DailyMessageCount

```csharp
{
  "date": DateOnly,            // The date for this count
  "count": long                // Number of messages on this date
}
```

### ApiErrorDto

```csharp
{
  "message": string,          // Error message
  "detail": string?,          // Error details (nullable)
  "statusCode": int,          // HTTP status code
  "traceId": string?          // Trace ID (nullable)
}
```

---

## Integration Examples

### Example: Get Bot Status

```bash
curl -X GET "http://localhost:5000/api/bot/status" -H "accept: application/json"
```

### Example: Update Guild Settings

```bash
curl -X PUT "http://localhost:5000/api/guilds/123456789012345678" \
  -H "Content-Type: application/json" \
  -d '{
    "prefix": "!",
    "isActive": true,
    "settings": "{\"welcomeChannel\":\"general\"}"
  }'
```

### Example: Query Command Logs

```bash
curl -X GET "http://localhost:5000/api/commandlogs?guildId=123456789012345678&page=1&pageSize=10&successOnly=true" \
  -H "accept: application/json"
```

### Example: Get Command Statistics

```bash
curl -X GET "http://localhost:5000/api/commandlogs/stats?since=2024-12-01T00:00:00Z" \
  -H "accept: application/json"
```

### Example: Sync Guild from Discord

```bash
curl -X POST "http://localhost:5000/api/guilds/123456789012345678/sync" \
  -H "accept: application/json"
```

### Example: Query Message Logs

```bash
curl -X GET "http://localhost:5000/api/messages?guildId=123456789012345678&page=1&pageSize=50&startDate=2024-12-01T00:00:00Z" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

### Example: Get Message Statistics

```bash
curl -X GET "http://localhost:5000/api/messages/stats?guildId=123456789012345678" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

### Example: Export Messages to CSV

```bash
curl -X GET "http://localhost:5000/api/messages/export?guildId=123456789012345678&startDate=2024-12-01T00:00:00Z" \
  -H "accept: text/csv" \
  -H "Authorization: Bearer your-token-here" \
  --output messages.csv
```

### Example: Delete User Messages (GDPR)

```bash
curl -X DELETE "http://localhost:5000/api/messages/user/987654321098765432" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-superadmin-token-here"
```

### Example: Manually Trigger Cleanup

```bash
curl -X POST "http://localhost:5000/api/messages/cleanup" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-superadmin-token-here"
```

### Example: Get Welcome Configuration

```bash
curl -X GET "http://localhost:5000/api/guilds/123456789012345678/welcome" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

### Example: Update Welcome Configuration

```bash
curl -X PUT "http://localhost:5000/api/guilds/123456789012345678/welcome" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-token-here" \
  -d '{
    "isEnabled": true,
    "welcomeChannelId": 111222333444555666,
    "welcomeMessage": "Welcome {user} to {guild}! 🎉",
    "useEmbed": true,
    "embedColor": "#5865F2"
  }'
```

### Example: Create Scheduled Message

```bash
curl -X POST "http://localhost:5000/api/guilds/123456789012345678/scheduled-messages" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-token-here" \
  -d '{
    "guildId": 123456789012345678,
    "channelId": 111222333444555666,
    "title": "Daily Reminder",
    "content": "Don't forget to check the announcements!",
    "frequency": 3,
    "isEnabled": true,
    "nextExecutionAt": "2024-12-09T09:00:00Z",
    "createdBy": "user123"
  }'
```

### Example: Query Audit Logs

```bash
curl -X GET "http://localhost:5000/api/auditlogs?category=3&actorType=1&page=1&pageSize=20" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-superadmin-token-here"
```

### Example: Get Audit Log Statistics

```bash
curl -X GET "http://localhost:5000/api/auditlogs/stats?guildId=123456789012345678" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-superadmin-token-here"
```

### Example: Validate Cron Expression

```bash
curl -X POST "http://localhost:5000/api/guilds/123456789012345678/scheduled-messages/validate-cron" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-token-here" \
  -d '{
    "cronExpression": "0 9 * * *"
  }'
```

### Example: Execute Scheduled Message Immediately

```bash
curl -X POST "http://localhost:5000/api/guilds/123456789012345678/scheduled-messages/a1b2c3d4-e5f6-7890-abcd-ef1234567890/execute" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

---

## Autocomplete API

The Autocomplete API provides search endpoints used by UI filter components to provide type-ahead suggestions. All endpoints return JSON arrays of suggestion objects.

**Base Path:** `/api/autocomplete`

**Authorization:** All endpoints require authentication (minimum Viewer role)

**Common Response Format:**

```json
[
  {
    "id": "123456789012345678",
    "displayText": "Display Name"
  }
]
```

---

### GET /api/autocomplete/users

Search for users by username in message logs.

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `search` | string | Yes | Search term to match against usernames (min 1 character) |
| `guildId` | ulong | No | Optional guild ID to filter results to a specific guild |

**Example Request:**

```bash
GET /api/autocomplete/users?search=john
GET /api/autocomplete/users?search=john&guildId=123456789012345678
```

**Response: 200 OK**

```json
[
  {
    "id": "123456789012345678",
    "displayText": "JohnDoe"
  },
  {
    "id": "234567890123456789",
    "displayText": "Johnny123"
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Discord user ID (snowflake as string) |
| `displayText` | string | Username to display |

**Error Responses:**

| Status | Condition |
|--------|-----------|
| 400 Bad Request | Search term is empty or whitespace |
| 401 Unauthorized | Not authenticated |
| 403 Forbidden | Insufficient permissions (requires Viewer role) |

**Notes:**
- Results are limited to 25 entries
- Search is case-insensitive and matches partial usernames
- Only returns users who have message log entries

---

### GET /api/autocomplete/guilds

Search for guilds by name.

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `search` | string | Yes | Search term to match against guild names |

**Example Request:**

```bash
GET /api/autocomplete/guilds?search=gaming
```

**Response: 200 OK**

```json
[
  {
    "id": "123456789012345678",
    "displayText": "Gaming Community"
  },
  {
    "id": "234567890123456789",
    "displayText": "Gaming Hub"
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Discord guild ID (snowflake as string) |
| `displayText` | string | Guild name |

**Error Responses:**

| Status | Condition |
|--------|-----------|
| 400 Bad Request | Search term is empty or whitespace |
| 401 Unauthorized | Not authenticated |
| 403 Forbidden | Insufficient permissions (requires Viewer role) |

**Notes:**
- Results are limited to 25 entries
- Search is case-insensitive and matches partial guild names
- Only returns guilds stored in the database

---

### GET /api/autocomplete/channels

Search for channels by name within a specific guild.

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `search` | string | Yes | Search term to match against channel names |
| `guildId` | ulong | Yes | Guild ID to search channels in |

**Example Request:**

```bash
GET /api/autocomplete/channels?search=general&guildId=123456789012345678
```

**Response: 200 OK**

```json
[
  {
    "id": "111222333444555666",
    "displayText": "general",
    "channelType": "Text"
  },
  {
    "id": "222333444555666777",
    "displayText": "general-voice",
    "channelType": "Voice"
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Discord channel ID (snowflake as string) |
| `displayText` | string | Channel name |
| `channelType` | string | Channel type: Text, Voice, News, Stage, Public Thread, Private Thread, Category |

**Error Responses:**

| Status | Condition |
|--------|-----------|
| 400 Bad Request | Search term is empty or guild ID not provided |
| 401 Unauthorized | Not authenticated |
| 403 Forbidden | Insufficient permissions (requires Viewer role) |
| 404 Not Found | Guild not connected to the bot |

**Notes:**
- Results are limited to 25 entries
- Search is case-insensitive and matches partial channel names
- Results are sorted alphabetically by channel name
- Returns text channels, voice channels, news channels, stage channels, and threads

---

### GET /api/autocomplete/commands

Search for registered slash commands by name.

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `search` | string | Yes | Search term to match against command names |

**Example Request:**

```bash
GET /api/autocomplete/commands?search=ping
```

**Response: 200 OK**

```json
[
  {
    "id": "ping",
    "displayText": "/ping - Check bot latency and response time"
  },
  {
    "id": "admin ping",
    "displayText": "/admin ping - Admin-only ping command"
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Full command name (e.g., "ping" or "admin info") |
| `displayText` | string | Command name with description (e.g., "/ping - Description") |

**Error Responses:**

| Status | Condition |
|--------|-----------|
| 400 Bad Request | Search term is empty or whitespace |
| 401 Unauthorized | Not authenticated |
| 403 Forbidden | Insufficient permissions (requires Viewer role) |

**Notes:**
- Results are limited to 25 entries
- Search is case-insensitive and matches partial command names
- Includes subcommands (e.g., "admin info", "admin kick")
- Results are sorted alphabetically by full command name

---

### Example: Search Users

```bash
curl -X GET "http://localhost:5000/api/autocomplete/users?search=john" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

### Example: Search Channels in a Guild

```bash
curl -X GET "http://localhost:5000/api/autocomplete/channels?search=general&guildId=123456789012345678" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.5 | 2026-01-02 | Added Autocomplete API documentation (Issue #554) |
| 1.4 | 2025-12-30 | Added Welcome, Scheduled Messages, and Audit Log endpoint documentation (Issue #308) |
| 1.3 | 2025-12-24 | Added Message Log endpoints documentation (Issue #140) |
| 1.2 | 2025-12-24 | Added `/metrics` endpoint documentation (Issue #104) |
| 1.1 | 2024-12-09 | Added User Management Service interface documentation (Issue #66) |
| 1.0 | 2024-12-08 | Initial API implementation (Phase 4 MVP) |

---

## Related Documentation

- [Metrics Documentation](metrics.md) - OpenTelemetry metrics and Prometheus setup
- [User Management Guide](user-management.md) - Comprehensive user administration guide
- [MVP Implementation Plan](mvp-plan.md) - Full development roadmap
- [Database Schema](database-schema.md) - Entity definitions and relationships
- [Repository Pattern](repository-pattern.md) - Data access implementation
- [Admin Commands](admin-commands.md) - Discord slash command reference
- [Authorization Policies](authorization-policies.md) - Role-based authorization

---

*Last Updated: January 2, 2026*
