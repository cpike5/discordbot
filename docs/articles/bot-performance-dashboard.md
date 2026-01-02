# Bot Performance Dashboard

**Last Updated:** 2026-01-01
**Feature Reference:** Issue #295 (Epic)
**Status:** In Progress

---

## Table of Contents

- [Overview](#overview)
- [Completed Sub-Issues](#completed-sub-issues)
  - [HTML Prototypes (#580)](#html-prototypes-issue-580)
  - [Performance Metrics Collection Infrastructure (#571)](#performance-metrics-collection-infrastructure-issue-571)
  - [Performance Dashboard API Endpoints (#572)](#performance-dashboard-api-endpoints-issue-572)
  - [Bot Health Metrics Dashboard (#563)](#bot-health-metrics-dashboard-issue-563)
- [Open Sub-Issues (To Be Implemented)](#open-sub-issues-to-be-implemented)
  - [System Health Monitoring (#568)](#system-health-monitoring-issue-568)
  - [Command Performance Analytics (#565)](#command-performance-analytics-issue-565)
  - [Discord API & Rate Limit Monitoring (#566)](#discord-api--rate-limit-monitoring-issue-566)
  - [Performance Alerts & Incidents (#570)](#performance-alerts--incidents-issue-570)
  - [Performance Dashboard UI Implementation (#573)](#performance-dashboard-ui-implementation-issue-573)
- [Configuration](#configuration)
- [Service Registration](#service-registration)
- [Integration with Bot Events](#integration-with-bot-events)
- [Related Documentation](#related-documentation)
- [Changelog](#changelog)

---

## Overview

The Bot Performance Dashboard is a comprehensive monitoring and analytics system that provides real-time and historical insights into the Discord bot's health, performance, and resource utilization. The dashboard enables administrators to identify bottlenecks, track system health, optimize command execution, and monitor Discord API usage.

### Key Features

- **Bot Health Monitoring**: Connection state, uptime tracking, heartbeat latency, session history
- **Command Performance Analytics**: Execution times, throughput, slowest commands, error tracking
- **Discord API Usage**: Request tracking, rate limit monitoring, API latency
- **System Health**: Database performance, background service health, cache statistics
- **Performance Alerts**: Configurable thresholds, incident tracking, alerting workflows

### Architecture

The performance dashboard follows a layered architecture:

1. **Collection Layer**: Services that collect and aggregate metrics in real-time
2. **Storage Layer**: In-memory buffers and database persistence for historical data
3. **API Layer**: REST endpoints exposing metrics to the UI
4. **Presentation Layer**: Razor Pages and interactive charts for visualization

---

## Completed Sub-Issues

### HTML Prototypes (Issue #580)

HTML prototypes are located in `docs/prototypes/features/bot-performance/`:

| File | Purpose |
|------|---------|
| `index.html` | Dashboard overview with navigation |
| `health-metrics.html` | Connection state, uptime, latency visualization |
| `command-performance.html` | Command analytics, response times, throughput |
| `api-rate-limits.html` | API usage tracking, rate limit events |
| `system-health.html` | Database, services, cache monitoring |
| `alerts-incidents.html` | Alert configuration, incident management |

These prototypes use Chart.js for data visualization and follow the design system defined in `docs/articles/design-system.md`.

### Performance Metrics Collection Infrastructure (Issue #571)

Core services for collecting and aggregating performance data:

#### ConnectionStateService

**Type:** Singleton Service
**Interface:** `IConnectionStateService`
**Location:** `src/DiscordBot.Bot/Services/ConnectionStateService.cs`

Tracks Discord gateway connection state changes and calculates uptime metrics.

**Key Methods:**
```csharp
void RecordConnected();
void RecordDisconnected(Exception? exception);
GatewayConnectionState GetCurrentState();
DateTime? GetLastConnectedTime();
DateTime? GetLastDisconnectedTime();
TimeSpan GetCurrentSessionDuration();
double GetUptimePercentage(TimeSpan period);
IReadOnlyList<ConnectionEventDto> GetConnectionEvents(int days = 7);
ConnectionStatsDto GetConnectionStats(int days = 7);
```

**Features:**
- Thread-safe event recording with internal locking
- Automatic cleanup of old connection events (configurable retention)
- Uptime percentage calculation for arbitrary time periods
- Session duration tracking for current connection

#### LatencyHistoryService

**Type:** Singleton Service
**Interface:** `ILatencyHistoryService`
**Location:** `src/DiscordBot.Bot/Services/LatencyHistoryService.cs`

Maintains a circular buffer of Discord gateway latency samples with statistical analysis.

**Key Methods:**
```csharp
void RecordSample(int latencyMs);
int GetCurrentLatency();
IReadOnlyList<LatencySampleDto> GetSamples(int hours = 24);
LatencyStatisticsDto GetStatistics(int hours = 24);
```

**Features:**
- Circular buffer with configurable capacity (default: 24 hours at 30-second intervals = 2,880 samples)
- Real-time statistical analysis (average, min, max, P50, P95, P99)
- Thread-safe sample recording and retrieval
- Efficient percentile calculation using sorted arrays

**Configuration:**
```json
{
  "PerformanceMetrics": {
    "LatencyRetentionHours": 24,
    "LatencySampleIntervalSeconds": 30
  }
}
```

#### CommandPerformanceAggregator

**Type:** Background Service + Service Interface
**Interface:** `ICommandPerformanceAggregator`
**Location:** `src/DiscordBot.Bot/Services/CommandPerformanceAggregator.cs`

Aggregates command execution data from the command log repository with caching to avoid expensive recalculations.

**Key Methods:**
```csharp
Task<IReadOnlyList<CommandPerformanceAggregateDto>> GetAggregatesAsync(int hours = 24);
Task<IReadOnlyList<SlowestCommandDto>> GetSlowestCommandsAsync(int limit = 10, int hours = 24);
Task<IReadOnlyList<CommandThroughputDto>> GetThroughputAsync(int hours = 24, string granularity = "hour");
Task<IReadOnlyList<CommandErrorBreakdownDto>> GetErrorBreakdownAsync(int hours = 24, int limit = 50);
void InvalidateCache();
```

**Features:**
- Background task that periodically refreshes aggregated metrics
- Cached aggregates with configurable TTL (default: 5 minutes)
- Performance statistics per command: execution count, avg/min/max/P50/P95/P99 response times, error rate
- Throughput analysis by hour or day
- Error breakdown by command and error message

**Aggregate DTO:**
```csharp
public class CommandPerformanceAggregateDto
{
    public string CommandName { get; set; }
    public int ExecutionCount { get; set; }
    public double AvgMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double ErrorRate { get; set; }
}
```

#### BackgroundServiceHealthRegistry

**Type:** Singleton Service
**Interface:** `IBackgroundServiceHealthRegistry`
**Location:** `src/DiscordBot.Bot/Services/BackgroundServiceHealthRegistry.cs`

Tracks the health status of all background services (hosted services).

**Key Methods:**
```csharp
void RegisterService(string serviceName);
void RecordHeartbeat(string serviceName);
void RecordError(string serviceName, Exception exception);
IReadOnlyList<BackgroundServiceHealthDto> GetAllHealth();
string GetOverallStatus();
```

**Features:**
- Service registration on startup
- Heartbeat tracking with configurable timeout detection
- Error tracking with last error message and timestamp
- Overall health status calculation (Healthy, Degraded, Unhealthy)
- Thread-safe concurrent dictionary for service state

**Background Service Health DTO:**
```csharp
public class BackgroundServiceHealthDto
{
    public string ServiceName { get; set; }
    public string Status { get; set; } // "Healthy", "Degraded", "Unhealthy"
    public DateTime LastHeartbeat { get; set; }
    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
}
```

#### InstrumentedMemoryCache

**Type:** Singleton Service
**Interface:** `IInstrumentedCache` (extends `IMemoryCache`)
**Location:** `src/DiscordBot.Bot/Services/InstrumentedMemoryCache.cs`

Wrapper around `IMemoryCache` that tracks hit/miss statistics by key prefix.

**Key Methods:**
```csharp
// IMemoryCache interface methods (Get, Set, Remove, etc.)
IReadOnlyList<CacheStatisticsDto> GetStatistics();
void ResetStatistics();
```

**Features:**
- Transparent cache instrumentation with no code changes required
- Statistics tracking by key prefix (e.g., "guilds:", "users:", "commands:")
- Hit rate calculation per prefix
- Cache size tracking (entry count)
- Thread-safe concurrent dictionary for statistics

**Cache Statistics DTO:**
```csharp
public class CacheStatisticsDto
{
    public string KeyPrefix { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public double HitRate { get; set; }
    public int Size { get; set; }
}
```

### Performance Dashboard API Endpoints (Issue #572)

REST API controller located at `src/DiscordBot.Bot/Controllers/PerformanceMetricsController.cs`.

**Base Route:** `/api/metrics`
**Authorization:** All endpoints require `RequireViewer` policy

#### Health Endpoints

| Endpoint | Method | Description | Response DTO |
|----------|--------|-------------|--------------|
| `/api/metrics/health` | GET | Current performance health snapshot | `PerformanceHealthDto` |
| `/api/metrics/health/latency` | GET | Latency history with statistics | `LatencyHistoryDto` |
| `/api/metrics/health/connections` | GET | Connection event history | `ConnectionHistoryDto` |

**GET /api/metrics/health**

Returns overall performance health status.

Query Parameters: None

Response:
```json
{
  "status": "Healthy",
  "uptime": "1.12:34:56",
  "latencyMs": 85,
  "connectionState": "Connected",
  "timestamp": "2026-01-01T12:00:00Z"
}
```

**GET /api/metrics/health/latency**

Returns latency samples and statistics for a time window.

Query Parameters:
- `hours` (optional, default: 24): Number of hours of history (1-168)

Response:
```json
{
  "samples": [
    { "timestamp": "2026-01-01T11:00:00Z", "latencyMs": 82 },
    { "timestamp": "2026-01-01T11:00:30Z", "latencyMs": 85 }
  ],
  "statistics": {
    "average": 83.5,
    "min": 75,
    "max": 120,
    "p50": 82,
    "p95": 105,
    "p99": 115,
    "sampleCount": 2880
  }
}
```

**GET /api/metrics/health/connections**

Returns connection events and statistics.

Query Parameters:
- `days` (optional, default: 7): Number of days of history (1-30)

Response:
```json
{
  "events": [
    {
      "eventType": "Connected",
      "timestamp": "2026-01-01T10:00:00Z",
      "reason": null,
      "details": null
    },
    {
      "eventType": "Disconnected",
      "timestamp": "2026-01-01T09:00:00Z",
      "reason": "Gateway timeout",
      "details": "TimeoutException"
    }
  ],
  "statistics": {
    "totalEvents": 12,
    "reconnectionCount": 5,
    "averageSessionDuration": "4:30:00",
    "uptimePercentage": 98.5
  }
}
```

#### Command Performance Endpoints

| Endpoint | Method | Description | Response DTO |
|----------|--------|-------------|--------------|
| `/api/metrics/commands/performance` | GET | Aggregated command performance metrics | `List<CommandPerformanceAggregateDto>` |
| `/api/metrics/commands/slowest` | GET | Slowest command executions | `List<SlowestCommandDto>` |
| `/api/metrics/commands/throughput` | GET | Command execution throughput over time | `List<CommandThroughputDto>` |
| `/api/metrics/commands/errors` | GET | Command error breakdown | `CommandErrorsDto` |

**GET /api/metrics/commands/performance**

Returns aggregated performance metrics for all commands.

Query Parameters:
- `hours` (optional, default: 24): Number of hours to aggregate (1-168)

Response:
```json
[
  {
    "commandName": "ping",
    "executionCount": 1250,
    "avgMs": 45.2,
    "minMs": 12,
    "maxMs": 320,
    "p50Ms": 38,
    "p95Ms": 95,
    "p99Ms": 180,
    "errorRate": 0.8
  }
]
```

**GET /api/metrics/commands/slowest**

Returns the slowest individual command executions.

Query Parameters:
- `limit` (optional, default: 10): Max results (1-100)
- `hours` (optional, default: 24): Time window (1-168)

Response:
```json
[
  {
    "commandName": "stats",
    "executedAt": "2026-01-01T11:30:00Z",
    "durationMs": 4523,
    "userId": 123456789,
    "guildId": 987654321
  }
]
```

**GET /api/metrics/commands/throughput**

Returns command execution counts grouped by time buckets.

Query Parameters:
- `hours` (optional, default: 24): Time window (1-168)
- `granularity` (optional, default: "hour"): Bucket size ("hour" or "day")

Response:
```json
[
  {
    "timestamp": "2026-01-01T10:00:00Z",
    "count": 245,
    "granularity": "hour"
  },
  {
    "timestamp": "2026-01-01T11:00:00Z",
    "count": 312,
    "granularity": "hour"
  }
]
```

**GET /api/metrics/commands/errors**

Returns error breakdown by command and error type.

Query Parameters:
- `hours` (optional, default: 24): Time window (1-168)
- `limit` (optional, default: 50): Max commands to return (1-100)

Response:
```json
{
  "errorRate": 2.5,
  "byType": [
    {
      "commandName": "ban",
      "errorCount": 12,
      "errorMessages": {
        "Missing Permissions": 8,
        "User not found": 4
      }
    }
  ],
  "recentErrors": [
    {
      "timestamp": "2026-01-01T11:45:00Z",
      "commandName": "ban",
      "errorMessage": "Missing Permissions",
      "guildId": 987654321
    }
  ]
}
```

#### API Usage Endpoints

| Endpoint | Method | Description | Response DTO |
|----------|--------|-------------|--------------|
| `/api/metrics/api/usage` | GET | Discord API usage statistics | `ApiUsageSummaryDto` |
| `/api/metrics/api/rate-limits` | GET | Rate limit events | `RateLimitSummaryDto` |

**GET /api/metrics/api/usage**

Returns Discord API request statistics grouped by category.

Query Parameters:
- `hours` (optional, default: 24): Time window (1-168)

Response:
```json
{
  "totalRequests": 15420,
  "byCategory": [
    {
      "category": "Messages",
      "requestCount": 8500,
      "avgLatencyMs": 125
    },
    {
      "category": "Users",
      "requestCount": 3200,
      "avgLatencyMs": 95
    }
  ],
  "rateLimitHits": 3
}
```

**GET /api/metrics/api/rate-limits**

Returns rate limit events with details.

Query Parameters:
- `hours` (optional, default: 24): Time window (1-168)

Response:
```json
{
  "hitCount": 3,
  "events": [
    {
      "timestamp": "2026-01-01T11:20:00Z",
      "endpoint": "/channels/{id}/messages",
      "bucket": "messages:create",
      "retryAfterMs": 2500
    }
  ]
}
```

#### System Health Endpoints

| Endpoint | Method | Description | Response DTO |
|----------|--------|-------------|--------------|
| `/api/metrics/system/database` | GET | Database performance metrics | `DatabaseMetricsSummaryDto` |
| `/api/metrics/system/services` | GET | Background service health | `List<BackgroundServiceHealthDto>` |
| `/api/metrics/system/cache` | GET | Cache statistics | `CacheSummaryDto` |

**GET /api/metrics/system/database**

Returns database metrics and slow query information.

Query Parameters:
- `limit` (optional, default: 20): Max slow queries to return (1-100)

Response:
```json
{
  "metrics": {
    "totalQueries": 25430,
    "avgQueryTimeMs": 12.5,
    "slowQueryThresholdMs": 100,
    "slowQueryCount": 15
  },
  "recentSlowQueries": [
    {
      "query": "SELECT * FROM CommandLogs WHERE...",
      "durationMs": 245,
      "executedAt": "2026-01-01T11:30:00Z"
    }
  ]
}
```

**GET /api/metrics/system/services**

Returns health status of all background services.

Query Parameters: None

Response:
```json
[
  {
    "serviceName": "RatWatchExecutionService",
    "status": "Healthy",
    "lastHeartbeat": "2026-01-01T11:59:45Z",
    "errorCount": 0,
    "lastError": null
  },
  {
    "serviceName": "CommandPerformanceAggregator",
    "status": "Healthy",
    "lastHeartbeat": "2026-01-01T11:59:50Z",
    "errorCount": 0,
    "lastError": null
  }
]
```

**GET /api/metrics/system/cache**

Returns cache hit/miss statistics by key prefix.

Query Parameters: None

Response:
```json
{
  "overall": {
    "keyPrefix": "Overall",
    "hits": 45230,
    "misses": 3210,
    "hitRate": 93.4,
    "size": 1250
  },
  "byType": [
    {
      "keyPrefix": "guilds:",
      "hits": 25000,
      "misses": 1500,
      "hitRate": 94.3,
      "size": 450
    },
    {
      "keyPrefix": "users:",
      "hits": 20230,
      "misses": 1710,
      "hitRate": 92.2,
      "size": 800
    }
  ]
}
```

#### Alert Endpoints

| Endpoint | Method | Description | Response DTO |
|----------|--------|-------------|--------------|
| `/api/alerts/active` | GET | Currently active alerts | `List<ActiveAlertDto>` |
| `/api/alerts/config` | GET | Alert configuration rules | `List<AlertConfigDto>` |
| `/api/alerts/config/{metricName}` | PUT | Update alert configuration | `AlertConfigDto` |
| `/api/alerts/incidents` | GET | Alert incident history | `List<AlertIncidentDto>` |
| `/api/alerts/incidents/{id}/acknowledge` | POST | Acknowledge an incident | `AlertIncidentDto` |

> **Note:** Alert endpoints are defined in the API specification but will be fully implemented in Issue #570 (Performance Alerts & Incidents).

### Bot Health Metrics Dashboard (Issue #563)

**URL:** `/Admin/Performance/HealthMetrics`
**Authorization:** `RequireViewer` policy
**Page Model:** `src/DiscordBot.Bot/Pages/Admin/Performance/HealthMetrics.cshtml.cs`

The Health Metrics Dashboard provides real-time monitoring of bot health and performance.

#### Features

**Connection Status Card**
- Current gateway connection state with color coding:
  - Green: Connected
  - Yellow: Connecting/Reconnecting
  - Red: Disconnected
- Session start time
- Current session duration

**Uptime Metrics**
- Current session uptime (formatted as "Xd Xh Xm")
- Uptime percentage over 24 hours, 7 days, 30 days
- Color coding based on percentage:
  - Green: ≥ 99%
  - Yellow: 95-99%
  - Red: < 95%

**Heartbeat Latency**
- Current latency with gauge visualization using Chart.js doughnut chart
- Color coding:
  - Green: < 100ms
  - Yellow: 100-200ms
  - Red: > 200ms
- Statistical summary (avg, min, max, P95, P99) over 24 hours
- Recent latency samples sparkline (last 10 samples)

**Connection History**
- Timeline of connection/disconnection events (last 7 days)
- Event details: timestamp, event type, reason, exception type
- Connection statistics: total events, reconnection count, average session duration

**System Resources**
- Working set memory (MB)
- Private memory (MB)
- Max allocated memory (MB)
- Memory utilization percentage
- GC Gen 2 collection count
- Active thread count
- CPU usage percentage (placeholder for future implementation)

#### View Model

```csharp
public class HealthMetricsViewModel
{
    public PerformanceHealthDto Health { get; set; }
    public LatencyStatisticsDto LatencyStats { get; set; }
    public ConnectionStatsDto ConnectionStats { get; set; }
    public IReadOnlyList<ConnectionEventDto> RecentConnectionEvents { get; set; }
    public IReadOnlyList<LatencySampleDto> RecentLatencySamples { get; set; }

    // Formatted display strings
    public string UptimeFormatted { get; set; }
    public string Uptime24HFormatted { get; set; }
    public string Uptime7DFormatted { get; set; }
    public string Uptime30DFormatted { get; set; }
    public string ConnectionStateClass { get; set; }
    public string LatencyHealthClass { get; set; }
    public string SessionStartFormatted { get; set; }

    // System resource metrics
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long MaxAllocatedMemoryMB { get; set; }
    public double MemoryUtilizationPercent { get; set; }
    public int Gen2Collections { get; set; }
    public int ThreadCount { get; set; }
    public double CpuUsagePercent { get; set; }
}
```

#### Real-Time Updates

The Health Metrics page supports real-time updates via SignalR (future implementation):
- Connection state changes broadcast immediately
- Latency updates every 30 seconds (aligned with sample interval)
- Automatic uptime recalculation
- No page refresh required

---

## Open Sub-Issues (To Be Implemented)

### System Health Monitoring (Issue #568)

> **Status:** To be documented when implemented

*This section will be updated when [Issue #568](https://github.com/cpike5/discordbot/issues/568) is completed.*

**Planned Features:**
- Database performance monitoring with slow query tracking
- Background service health status dashboard
- Cache performance metrics and hit rate analysis
- System resource alerts and threshold configuration

### Command Performance Analytics (Issue #565)

> **Status:** To be documented when implemented

*This section will be updated when [Issue #565](https://github.com/cpike5/discordbot/issues/565) is completed.*

**Planned Features:**
- Command response time analytics dashboard
- Slowest commands visualization
- Command throughput charts (hourly/daily)
- Error rate tracking by command
- Performance regression detection

### Discord API & Rate Limit Monitoring (Issue #566)

> **Status:** To be documented when implemented

*This section will be updated when [Issue #566](https://github.com/cpike5/discordbot/issues/566) is completed.*

**Planned Features:**
- Discord API request tracking by endpoint
- Rate limit event monitoring and alerts
- API latency distribution charts
- Request category breakdown
- Rate limit bucket utilization

### Performance Alerts & Incidents (Issue #570)

> **Status:** To be documented when implemented

*This section will be updated when [Issue #570](https://github.com/cpike5/discordbot/issues/570) is completed.*

**Planned Features:**
- Alert configuration UI with threshold management
- Active alert dashboard
- Incident tracking and acknowledgement workflow
- Alert history and analytics
- Notification integration (Discord webhooks, email)

### Performance Dashboard UI Implementation (Issue #573)

> **Status:** To be documented when implemented

*This section will be updated when [Issue #573](https://github.com/cpike5/discordbot/issues/573) is completed.*

**Planned Features:**
- Complete Razor Pages implementation for all dashboard views
- Chart.js integration for all metric visualizations
- Real-time updates via SignalR
- Responsive design with mobile support
- Dashboard navigation and layout

---

## Configuration

Performance metrics configuration is managed through the `PerformanceMetricsOptions` class.

### appsettings.json

```json
{
  "PerformanceMetrics": {
    "LatencyRetentionHours": 24,
    "LatencySampleIntervalSeconds": 30,
    "ConnectionEventRetentionDays": 30,
    "CommandAggregationCacheTtlMinutes": 5,
    "SlowQueryThresholdMs": 100,
    "ApiUsageRetentionHours": 168,
    "BackgroundServiceHealthTimeoutMinutes": 5
  }
}
```

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LatencyRetentionHours` | int | 24 | Hours of latency history to retain in circular buffer |
| `LatencySampleIntervalSeconds` | int | 30 | Interval between latency samples |
| `ConnectionEventRetentionDays` | int | 30 | Days of connection events to retain |
| `CommandAggregationCacheTtlMinutes` | int | 5 | Cache TTL for aggregated command metrics |
| `SlowQueryThresholdMs` | int | 100 | Threshold for flagging slow database queries |
| `ApiUsageRetentionHours` | int | 168 | Hours of API usage history to retain (7 days) |
| `BackgroundServiceHealthTimeoutMinutes` | int | 5 | Minutes without heartbeat before service marked unhealthy |

---

## Service Registration

Services are registered in `Program.cs` via extension methods:

```csharp
// Performance metrics collection services
builder.Services.AddSingleton<IConnectionStateService, ConnectionStateService>();
builder.Services.AddSingleton<ILatencyHistoryService, LatencyHistoryService>();
builder.Services.AddSingleton<IBackgroundServiceHealthRegistry, BackgroundServiceHealthRegistry>();
builder.Services.AddSingleton<IInstrumentedCache, InstrumentedMemoryCache>();

// Command performance aggregator (BackgroundService + interface)
builder.Services.AddHostedService<CommandPerformanceAggregator>();
builder.Services.AddSingleton<ICommandPerformanceAggregator>(sp =>
    sp.GetServices<IHostedService>()
        .OfType<CommandPerformanceAggregator>()
        .First());

// API request tracking (when implemented)
builder.Services.AddSingleton<IApiRequestTracker, ApiRequestTracker>();
builder.Services.AddSingleton<IDatabaseMetricsCollector, DatabaseMetricsCollector>();
```

---

## Integration with Bot Events

Performance metrics are updated in response to Discord.NET client events:

### BotHostedService

The bot hosted service integrates with collection services:

```csharp
// In BotHostedService.cs
_client.Connected += OnConnected;
_client.Disconnected += OnDisconnected;
_client.LatencyUpdated += OnLatencyUpdated;

private Task OnConnected()
{
    _connectionStateService.RecordConnected();
    _backgroundServiceHealthRegistry.RecordHeartbeat("BotHostedService");
    return Task.CompletedTask;
}

private Task OnDisconnected(Exception exception)
{
    _connectionStateService.RecordDisconnected(exception);
    if (exception != null)
    {
        _backgroundServiceHealthRegistry.RecordError("BotHostedService", exception);
    }
    return Task.CompletedTask;
}

private Task OnLatencyUpdated(int oldLatency, int newLatency)
{
    _latencyHistoryService.RecordSample(newLatency);
    return Task.CompletedTask;
}
```

### InteractionHandler

Command execution metrics are recorded via the command log repository (existing functionality):

```csharp
var commandLog = new CommandLog
{
    CommandName = interaction.Data.Name,
    ExecutedAt = DateTime.UtcNow,
    ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
    Success = !hasError,
    ErrorMessage = errorMessage,
    // ... other properties
};

await _commandLogRepository.AddAsync(commandLog);
```

The `CommandPerformanceAggregator` background service periodically queries the command log repository to build aggregated metrics.

---

## Related Documentation

- [API Endpoints](api-endpoints.md) - Complete REST API documentation
- [Design System](design-system.md) - UI component specifications and color tokens
- [Authorization Policies](authorization-policies.md) - Access control for admin pages
- [Database Schema](database-schema.md) - CommandLogs table schema

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-01 | Initial documentation (Issues #580, #571, #572, #563 completed) |
