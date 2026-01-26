# Bot Performance Dashboard

**Last Updated:** 2026-01-08
**Feature Reference:** Issue #295 (Epic)
**Status:** Completed (9 of 9 sub-issues completed)

---

## Table of Contents

- [Overview](#overview)
- [Completed Sub-Issues](#completed-sub-issues)
  - [HTML Prototypes (#580)](#html-prototypes-issue-580)
  - [Performance Metrics Collection Infrastructure (#571)](#performance-metrics-collection-infrastructure-issue-571)
  - [Performance Dashboard API Endpoints (#572)](#performance-dashboard-api-endpoints-issue-572)
  - [Bot Health Metrics Dashboard (#563)](#bot-health-metrics-dashboard-issue-563)
  - [System Health Monitoring (#568)](#system-health-monitoring-issue-568)
  - [Command Performance Analytics (#565)](#command-performance-analytics-issue-565)
  - [Discord API & Rate Limit Monitoring (#566)](#discord-api--rate-limit-monitoring-issue-566)
  - [Performance Alerts & Incidents (#570)](#performance-alerts--incidents-issue-570)
  - [Performance Dashboard UI Implementation (#573)](#performance-dashboard-ui-implementation-issue-573)
  - [Historical Metrics System (Feature #613)](#historical-metrics-system-feature-613)
- [Real-Time Updates](#real-time-updates)
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
| `/api/metrics/api/latency` | GET | API latency history and statistics | `ApiLatencyHistoryDto` |

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

**GET /api/metrics/api/latency**

Returns Discord API latency samples and statistics for charting and analysis.

Query Parameters:
- `hours` (optional, default: 24): Number of hours of history (1-168)

Response:
```json
{
  "samples": [
    {
      "timestamp": "2026-01-01T10:00:00Z",
      "avgLatencyMs": 85.5,
      "p95LatencyMs": 125.0
    },
    {
      "timestamp": "2026-01-01T11:00:00Z",
      "avgLatencyMs": 82.3,
      "p95LatencyMs": 118.5
    }
  ],
  "statistics": {
    "avgLatencyMs": 82.3,
    "minLatencyMs": 45.0,
    "maxLatencyMs": 250.0,
    "p50LatencyMs": 75.0,
    "p95LatencyMs": 125.0,
    "p99LatencyMs": 200.0,
    "sampleCount": 288
  }
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

### System Health Monitoring (Issue #568)

**URL:** `/Admin/Performance/System`
**Authorization:** `RequireViewer` policy
**Page Model:** `src/DiscordBot.Bot/Pages/Admin/Performance/SystemHealth.cshtml.cs`

The System Health Dashboard provides monitoring for database performance, background services, cache effectiveness, and overall system health.

#### Features

**Database Performance Card**
- Average query execution time with color-coded status:
  - Green: < 50ms
  - Yellow: 50-100ms
  - Red: > 100ms
- Total query count since application start
- Queries per second rate
- Slow query count (queries exceeding threshold)
- Query time trend chart (Chart.js line chart)

**Slow Queries Table**
- Recent slow queries (queries exceeding 100ms threshold)
- Query text with truncation for long queries
- Duration in milliseconds with color coding
- Timestamp of execution
- Query parameters (sanitized)

**Background Services Status**
- List of all registered background services
- Real-time status indicators:
  - Green with pulse: Running
  - Yellow: Starting
  - Gray: Stopped
  - Red: Error
- Last heartbeat time with relative time display
- Error messages for failed services
- Monitored services include:
  - BotHostedService (Discord gateway)
  - ReminderExecutionService
  - ScheduledMessageExecutionService
  - MessageLogCleanupService
  - RatWatchExecutionService
  - CommandPerformanceAggregator
  - And other background services

**Cache Performance Panel**
- Per-prefix hit rate with progress bars:
  - Green: ≥ 90% hit rate
  - Yellow: 70-90% hit rate
  - Red: < 70% hit rate
- Hits and misses count per prefix
- Cache entry count per prefix
- Overall cache statistics summary:
  - Total hits
  - Total misses
  - Total entries

**Memory & GC Statistics**
- Working Set memory (MB)
- Private Bytes (MB)
- Heap Size (GC total memory, MB)
- GC collection counts by generation (Gen 0, 1, 2)
- Memory usage chart over time (Chart.js line chart)

#### View Model

```csharp
public record SystemHealthViewModel
{
    // Database metrics
    public DatabaseMetricsDto DatabaseMetrics { get; init; }
    public IReadOnlyList<SlowQueryDto> SlowQueries { get; init; }
    public double QueriesPerSecond { get; init; }
    public int DatabaseErrorCount { get; init; }

    // Background services
    public IReadOnlyList<BackgroundServiceHealthDto> BackgroundServices { get; init; }

    // Cache statistics
    public CacheStatisticsDto OverallCacheStats { get; init; }
    public IReadOnlyList<CacheStatisticsDto> CacheStatsByPrefix { get; init; }

    // Memory & GC
    public long WorkingSetMB { get; init; }
    public long PrivateMemoryMB { get; init; }
    public long HeapSizeMB { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }

    // Overall status
    public string SystemStatus { get; init; }
    public string SystemStatusClass { get; init; }

    // Helper methods
    public static string GetQueryTimeStatusClass(double avgQueryTimeMs);
    public static string GetCacheHitRateClass(double hitRate);
    public static string GetServiceStatusClass(string status);
    public static string GetSystemStatus(
        IReadOnlyList<BackgroundServiceHealthDto> services,
        double avgQueryTimeMs,
        int errorCount);
}
```

#### API Endpoints Used

The page consumes these existing API endpoints:
- `GET /api/metrics/system/database` - Database metrics and slow queries
- `GET /api/metrics/system/services` - Background service health
- `GET /api/metrics/system/cache` - Cache statistics by prefix

#### Auto-Refresh

The page automatically refreshes every 30 seconds to display current system health. Charts are updated in-place without full page reload for query time and memory metrics.

---

### Command Performance Analytics (Issue #565)

**URL:** `/Admin/Performance/Commands`
**Authorization:** `RequireViewer` policy
**Page Model:** `src/DiscordBot.Bot/Pages/Admin/Performance/Commands.cshtml.cs`

The Command Performance Analytics Dashboard provides comprehensive monitoring and analysis of Discord bot command execution, including response time metrics, throughput analysis, error tracking, and timeout detection.

#### Features

**Summary Metric Cards**

The page displays four key performance metrics at the top:
- **Average Response Time**: Mean command execution time across all commands with trend indicator
- **P50 (Median)**: 50th percentile response time, representing typical execution speed
- **P95**: 95th percentile response time, capturing slower executions
- **P99**: 99th percentile response time, identifying worst-case performance

All metrics include color-coded status indicators:
- Green: < 100ms (excellent)
- Yellow: 100-500ms (acceptable)
- Red: > 500ms (needs attention)

**Response Times Over Time Chart**

Interactive line chart showing command performance trends:
- Three data series: Average, P95, and P99 response times
- Time buckets based on selected range (hourly for 24h/7d, daily for 30d)
- Chart.js line chart with smooth curves
- Hover tooltips showing exact millisecond values
- Legend positioned at bottom for readability

**Command Throughput Chart**

Bar chart displaying command execution volume:
- Commands executed per hour (24h/7d ranges) or per day (30d range)
- Orange bars with rounded corners matching design system
- Helps identify peak usage periods
- Updates dynamically based on time range selection

**Error Rate Trend Chart**

Line chart showing command failure percentage over time:
- Red filled area chart for visual emphasis
- Y-axis automatically scales to error rate + buffer
- Displays overall error rate distributed across time buckets
- Helps identify error spikes and patterns

**Slowest Commands Table**

Server-side rendered table showing top 10 slowest individual command executions:
- Command name (displayed as inline code)
- Duration in milliseconds with color coding
- Execution timestamp (converted to user's local timezone)
- User ID who triggered the command
- Guild ID (or "DM" for direct messages)
- Sortable and filterable view of performance outliers

**Commands with Timeouts**

Critical performance monitoring table for commands exceeding Discord's 3-second interaction limit:
- Command name
- Timeout count (number of occurrences)
- Last timeout timestamp
- Average response time before timeout
- Status badge:
  - "Investigating" (orange) if timeout occurred within last 2 hours
  - "Resolved" (green) if timeout is older than 2 hours
- Empty state message if no timeouts detected
- Severity badge in header showing total timeout count

#### Time Range Selector

Toggle buttons allowing users to view metrics for different time periods:
- **24h**: Last 24 hours (hourly granularity)
- **7d**: Last 7 days (hourly granularity)
- **30d**: Last 30 days (daily granularity)

Active selection highlighted with blue background and border.

#### View Model

```csharp
public record CommandPerformanceViewModel
{
    // Summary metrics
    public int TotalCommands { get; init; }
    public double AvgResponseTimeMs { get; init; }
    public double ErrorRate { get; init; }
    public double P99ResponseTimeMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }

    // Tables and lists
    public IReadOnlyList<SlowestCommandDto> SlowestCommands { get; init; }
    public int TimeoutCount { get; init; }
    public IReadOnlyList<CommandTimeoutDto> RecentTimeouts { get; init; }

    // Trends (for future implementation - currently set to 0)
    public double AvgResponseTimeTrend { get; init; }
    public double ErrorRateTrend { get; init; }
    public double P99Trend { get; init; }

    // Helper methods
    public static string GetTrendClass(double trend);
    public static string FormatTrend(double trend, string unit = "ms");
    public static string GetLatencyClass(double ms);
    public static string GetErrorRateClass(double rate);
}

public record CommandTimeoutDto
{
    public string CommandName { get; init; }
    public int TimeoutCount { get; init; }
    public DateTime LastTimeout { get; init; }
    public double AvgResponseBeforeTimeout { get; init; }
    public string Status { get; init; } // "Investigating" or "Resolved"
}
```

#### API Endpoints Used

The page consumes these existing API endpoints documented in [Performance Dashboard API Endpoints](#performance-dashboard-api-endpoints-issue-572):
- `GET /api/metrics/commands/performance?hours={hours}` - Aggregated command performance metrics
- `GET /api/metrics/commands/slowest?limit=10&hours={hours}` - Slowest command executions
- `GET /api/metrics/commands/throughput?hours={hours}&granularity={granularity}` - Command execution counts over time

#### Timeout Detection

Commands are flagged as timeouts based on Discord's interaction response limit:
- **Threshold**: 3000ms (3 seconds)
- **Rationale**: Discord requires interactions to receive an initial response within 3 seconds
- **Status Logic**:
  - Timeouts within last 2 hours: Status = "Investigating"
  - Older timeouts: Status = "Resolved"
- Calculated from slowest commands data by filtering executions > 3000ms

#### Auto-Refresh

The page implements automatic refresh functionality:
- Charts refresh every 30 seconds
- Refresh pauses when browser tab is hidden
- Refresh resumes when tab becomes visible again
- No full page reload - charts update in-place via API calls

#### Empty State Handling

When no command data is available for the selected time period:
- Lightning bolt icon (command symbol)
- Message: "No command data available"
- Description: "Commands will appear here once the bot processes Discord interactions in the selected time period."

#### Navigation

The page includes a shared performance dashboard navigation tab bar:
- Overview
- Health Metrics
- **Commands** (active)
- API & Rate Limits
- System
- Alerts

---

### Discord API & Rate Limit Monitoring (Issue #566)

**URL:** `/Admin/Performance/ApiMetrics`
**Authorization:** `RequireViewer` policy
**Page Model:** `src/DiscordBot.Bot/Pages/Admin/Performance/ApiMetrics.cshtml.cs`

The Discord API & Rate Limit Monitoring Dashboard provides comprehensive tracking of Discord API usage, latency metrics, and rate limit events to help administrators optimize bot performance and avoid rate limiting.

#### Features

**Summary Metric Cards**

The page displays four key API performance metrics at the top:

- **Total API Requests**: Aggregate count of all API requests across REST and Gateway categories within the selected time window
- **Average Latency**: Mean API response time with color-coded status:
  - Green: < 100ms (excellent)
  - Yellow: 100-200ms (acceptable)
  - Red: > 200ms (needs attention)
- **Rate Limit Hits**: Number of rate limit events encountered in the time window with severity badge
- **P95 Latency**: 95th percentile latency for SLA monitoring and performance guarantees

**API Latency Over Time Chart**

Interactive dual-line chart showing API performance trends:
- Two data series: Average latency and P95 latency
- Chart.js line chart with dark theme styling matching design system
- Time buckets based on selected range (hourly for 24h/7d, daily for 30d)
- Hover tooltips showing exact millisecond values and timestamps
- Responsive time formatting based on selected range
- Auto-refresh every 30 seconds without full page reload

**Rate Limit Hits Log**

Scrollable event list displaying recent rate limit encounters:
- Event timestamp with relative time display
- Endpoint or bucket that triggered the rate limit
- Retry-after duration in milliseconds
- Global rate limit flag indicator (distinguishes global vs. per-route limits)
- Color-coded severity indicators:
  - Orange: Standard rate limit hit
  - Red: Global rate limit hit
- Empty state message if no rate limits encountered
- Maximum 50 most recent events displayed

**Usage by Category Table**

Breakdown of API requests by category with performance metrics:
- **Category**: REST (HTTP API calls) and Gateway (WebSocket events)
- **Request Count**: Total requests in category
- **Average Latency**: Mean response time for category
- **Error Count**: Failed requests in category
- **Error Rate**: Percentage of requests that failed (calculated as errors/requests × 100)

Table includes color-coded status indicators:
- Green: Error rate < 1%
- Yellow: Error rate 1-5%
- Red: Error rate > 5%

#### Time Range Selector

Toggle buttons allowing users to view metrics for different time periods:
- **24h**: Last 24 hours (hourly granularity for latency samples)
- **7d**: Last 7 days (hourly granularity for latency samples)
- **30d**: Last 30 days (daily granularity for latency samples)

Active selection highlighted with blue background and border. Time range affects all charts, tables, and summary metrics.

#### View Model

```csharp
public class ApiRateLimitsViewModel
{
    // Summary metrics
    public long TotalRequests { get; set; }
    public int RateLimitHits { get; set; }
    public double AvgLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }

    // Data collections
    public IReadOnlyList<ApiUsageDto> UsageByCategory { get; set; }
    public IReadOnlyList<RateLimitEventDto> RecentRateLimitEvents { get; set; }
    public ApiLatencyStatsDto? LatencyStats { get; set; }

    // Time range selection
    public int Hours { get; set; } = 24;

    // Helper methods
    public string GetHealthStatus();        // Returns "healthy", "warning", or "critical"
    public string GetHealthStatusText();    // Returns display text for status
    public string GetHealthStatusClass();   // Returns Tailwind CSS class for status
}
```

#### API Endpoints Used

The page consumes these API endpoints documented in [Performance Dashboard API Endpoints](#performance-dashboard-api-endpoints-issue-572):

- `GET /api/metrics/api/latency?hours={hours}` - API latency history with samples and statistics
- `GET /api/metrics/api/usage?hours={hours}` - API usage statistics by category
- `GET /api/metrics/api/rate-limits?hours={hours}` - Rate limit events with details

#### New DTOs

The following DTOs were added to support API metrics visualization:

**ApiRequestVolumeDto**
```csharp
public record ApiRequestVolumeDto
{
    public DateTime Timestamp { get; init; }
    public long RequestCount { get; init; }
    public string Category { get; init; }  // "REST" or "Gateway"
}
```

**ApiLatencySampleDto**
```csharp
public record ApiLatencySampleDto
{
    public DateTime Timestamp { get; init; }
    public double AvgLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
}
```

**ApiLatencyStatsDto**
```csharp
public record ApiLatencyStatsDto
{
    public double AvgLatencyMs { get; init; }
    public double MinLatencyMs { get; init; }
    public double MaxLatencyMs { get; init; }
    public double P50LatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
    public int SampleCount { get; init; }
}
```

**ApiLatencyHistoryDto**
```csharp
public record ApiLatencyHistoryDto
{
    public IReadOnlyList<ApiLatencySampleDto> Samples { get; init; }
    public ApiLatencyStatsDto Statistics { get; init; }
}
```

#### Enhanced IApiRequestTracker Methods

The `IApiRequestTracker` interface was enhanced with new methods for latency tracking:

```csharp
// Record an API request with latency measurement
void RecordRequest(string category, int latencyMs);

// Get aggregated latency statistics for a time window
ApiLatencyStatsDto GetLatencyStatistics(int hours = 24);

// Get time-series latency samples for charting
IReadOnlyList<ApiLatencySampleDto> GetLatencySamples(int hours = 24);

// Get request volume breakdown by category
IReadOnlyList<ApiRequestVolumeDto> GetRequestVolume(int hours = 24);
```

#### Health Status Calculation

The dashboard calculates overall API health status based on rate limits and latency:

- **Critical** (Red): Rate limit hits > 10 OR avg latency > 500ms
  - Indicates severe API performance degradation
  - Immediate attention required
- **Warning** (Yellow): Rate limit hits > 0 OR avg latency > 200ms
  - Indicates potential performance issues
  - Monitoring recommended
- **Healthy** (Green): No rate limit hits AND avg latency < 200ms
  - Normal operating conditions
  - API performance within acceptable thresholds

Health status displayed in summary card with color-coded badge and descriptive text.

#### Auto-Refresh

The page implements automatic refresh functionality to provide near real-time monitoring:
- Charts and metrics refresh every 30 seconds
- API calls made to `/api/metrics/api/latency` endpoint
- No full page reload required - charts update in-place
- Refresh pauses when browser tab is hidden (visibility API)
- Refresh resumes when tab becomes visible again
- JavaScript console logs refresh activity for debugging

#### Empty State Handling

When no API request data is available for the selected time period:
- Cloud icon (API symbol) displayed in center
- Message: "No API data available"
- Description: "API metrics will appear here once the bot makes Discord API requests in the selected time period."
- Helpful for new deployments or low-traffic periods

#### Navigation

The page includes a shared performance dashboard navigation tab bar:
- Overview
- Health Metrics
- Commands
- **API & Rate Limits** (active)
- System
- Alerts

---

### Performance Alerts & Incidents (Issue #570)

**URL:** `/Admin/Performance/Alerts`
**Authorization:** `RequireViewer` policy (Admin required for acknowledge actions)
**Page Model:** `src/DiscordBot.Bot/Pages/Admin/Performance/Alerts.cshtml.cs`

The Performance Alerts & Incidents Dashboard provides comprehensive monitoring and management of performance-related alerts, including threshold configuration, active incident tracking, and historical incident analysis.

#### Features

**Active Alerts Card**

Displays currently active (unresolved) performance incidents with real-time status indicators:
- Incident severity badges (Critical, Warning, Info) with color coding:
  - Red (Critical): Metrics exceeding critical thresholds
  - Yellow (Warning): Metrics exceeding warning thresholds
  - Blue (Info): Informational alerts
- Metric name and descriptive message
- Current metric value vs. threshold value
- Triggered timestamp with relative time display
- Acknowledge button for Admin+ users (individual incident acknowledgment)
- "Acknowledge All" button to bulk-acknowledge all active incidents
- Empty state when no active alerts exist

**Alert Threshold Configuration Table**

Interactive table for managing alert thresholds per metric:
- Metric display name with description tooltip
- Current metric value with real-time updates
- Warning threshold input field
- Critical threshold input field
- Threshold unit display (ms, %, MB, count, event)
- Enabled/disabled toggle switch
- Save button to persist threshold changes (Admin+ only)
- Visual indicators for metrics currently in alert state

Configurable metrics include:
- Gateway Latency
- Command P95 Latency
- Error Rate
- Memory Usage
- API Rate Limit Usage
- Database Query Time
- Bot Disconnected (event-based)
- Service Failure (event-based)

**Incident History Timeline**

Chronological list of recent incidents (last 10 by default) with full details:
- Incident severity badge
- Metric name and descriptive message
- Triggered timestamp
- Resolved timestamp (if resolved)
- Incident duration in human-readable format
- Acknowledged status with acknowledging user and timestamp
- Administrator notes (if provided during acknowledgment)
- Status badge (Active, Acknowledged, Resolved)

**Auto-Recovery Event Log**

Displays recent automatic incident resolutions:
- Recovery timestamp with relative time
- Metric name that auto-recovered
- Issue description (e.g., "Command response time exceeded 1000ms")
- Action taken (e.g., "Metric returned to normal range")
- Result details (e.g., "Incident auto-resolved after 3 consecutive normal readings")
- Incident duration before recovery
- Shows last 10 auto-recovery events

**Alert Frequency Chart**

Stacked bar chart showing daily alert counts by severity (Chart.js):
- Last 30 days of incident frequency
- Stacked bars with color coding:
  - Red: Critical incidents
  - Yellow: Warning incidents
  - Blue: Info incidents
- Hover tooltips showing exact counts per severity
- Helps identify trending alert patterns and problem periods

#### View Model

```csharp
public record AlertsPageViewModel
{
    public IReadOnlyList<PerformanceIncidentDto> ActiveIncidents { get; init; }
    public IReadOnlyList<AlertConfigDto> AlertConfigs { get; init; }
    public IReadOnlyList<PerformanceIncidentDto> RecentIncidents { get; init; }
    public IReadOnlyList<AutoRecoveryEventDto> AutoRecoveryEvents { get; init; }
    public IReadOnlyList<AlertFrequencyDataDto> AlertFrequencyData { get; init; }
    public ActiveAlertSummaryDto AlertSummary { get; init; }
}
```

#### API Endpoints Used

The page consumes these API endpoints:
- `GET /api/alerts/config` - Get all alert configurations with current metric values
- `GET /api/alerts/config/{metricName}` - Get specific alert configuration
- `PUT /api/alerts/config/{metricName}` - Update alert threshold configuration (Admin+)
- `GET /api/alerts/active` - Get currently active incidents
- `GET /api/alerts/incidents` - Get paginated incident history
- `GET /api/alerts/incidents/{id}` - Get specific incident by ID
- `POST /api/alerts/incidents/{id}/acknowledge` - Acknowledge single incident (Admin+)
- `POST /api/alerts/incidents/acknowledge-all` - Acknowledge all active incidents (Admin+)
- `GET /api/alerts/summary` - Get active alert summary statistics
- `GET /api/alerts/stats` - Get alert frequency statistics for charting

#### New DTOs Introduced

**Alert Configuration DTOs**
```csharp
public record AlertConfigDto
{
    public int Id { get; init; }
    public string MetricName { get; init; }
    public string DisplayName { get; init; }
    public string? Description { get; init; }
    public double? WarningThreshold { get; init; }
    public double? CriticalThreshold { get; init; }
    public string ThresholdUnit { get; init; }
    public bool IsEnabled { get; init; }
    public double? CurrentValue { get; init; }
}

public record AlertConfigUpdateDto
{
    public double? WarningThreshold { get; init; }
    public double? CriticalThreshold { get; init; }
    public bool? IsEnabled { get; init; }
}
```

**Incident DTOs**
```csharp
public record PerformanceIncidentDto
{
    public Guid Id { get; init; }
    public string MetricName { get; init; }
    public AlertSeverity Severity { get; init; }
    public IncidentStatus Status { get; init; }
    public DateTime TriggeredAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public double ThresholdValue { get; init; }
    public double ActualValue { get; init; }
    public string Message { get; init; }
    public bool IsAcknowledged { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public string? Notes { get; init; }
    public double? DurationSeconds { get; init; }
}

public record IncidentQueryDto
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public AlertSeverity? Severity { get; init; }
    public IncidentStatus? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? MetricName { get; init; }
}

public record IncidentPagedResultDto
{
    public IReadOnlyList<PerformanceIncidentDto> Items { get; init; }
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record AcknowledgeIncidentDto
{
    public string? Notes { get; init; }
}
```

**Summary & Statistics DTOs**
```csharp
public record ActiveAlertSummaryDto
{
    public int ActiveCount { get; init; }
    public int CriticalCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
}

public record AlertFrequencyDataDto
{
    public DateTime Date { get; init; }
    public int CriticalCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
}

public record AutoRecoveryEventDto
{
    public DateTime Timestamp { get; init; }
    public string MetricName { get; init; }
    public string Issue { get; init; }
    public string Action { get; init; }
    public string Result { get; init; }
    public double DurationSeconds { get; init; }
}
```

#### Background Service: AlertMonitoringService

**Type:** Background Hosted Service
**Interface:** `IBackgroundServiceHealth`
**Location:** `src/DiscordBot.Bot/Services/AlertMonitoringService.cs`

The AlertMonitoringService continuously monitors performance metrics and creates or resolves incidents based on configured thresholds.

**Key Responsibilities:**
- Evaluates all enabled alert configurations every 30 seconds (configurable)
- Tracks consecutive threshold breaches and normal readings
- Creates new incidents when consecutive breach threshold is met
- Auto-resolves incidents when consecutive normal reading threshold is met
- Broadcasts incident state changes via SignalR to connected dashboard clients
- Registers with background service health registry for monitoring

> **⚠️ Critical Implementation Note: Lazy Service Resolution**
>
> This service uses **lazy service resolution** to avoid a circular DI dependency that would cause application startup to hang indefinitely. The `ICommandPerformanceAggregator` is resolved via a factory that enumerates all `IHostedService` instances - injecting it directly in a hosted service constructor creates a deadlock.
>
> **Pattern Used:**
> ```csharp
> // Constructor only injects IServiceProvider
> public AlertMonitoringService(
>     IServiceProvider serviceProvider,
>     IHubContext<DashboardHub> hubContext,
>     ILogger<AlertMonitoringService> logger,
>     IOptions<PerformanceAlertOptions> options)
>
> // Services resolved lazily in ExecuteAsync after Task.Yield()
> protected override async Task ExecuteAsync(CancellationToken stoppingToken)
> {
>     await Task.Yield();  // Ensure host startup completes
>     ResolveServices();   // Now safe to resolve dependencies
>     // ... monitoring loop
> }
> ```
>
> See [Issue #570 Lessons Learned](../lessons-learned/issue-570-performance-alerts.md) for detailed analysis.

**Breach Detection Logic:**
- **Consecutive Breaches Required:** 2 (default) - prevents alert noise from temporary spikes
- **Consecutive Normal Required:** 3 (default) - ensures metric has stabilized before auto-resolution
- Tracks breach/normal counts per metric in-memory using `ConcurrentDictionary`
- Resets counters when metric crosses threshold boundary

**Monitored Metrics:**
- `gateway_latency` - Discord gateway heartbeat latency (from `ILatencyHistoryService`)
- `command_p95_latency` - 95th percentile command response time (from `ICommandPerformanceAggregator`)
- `error_rate` - Percentage of failed commands (from `ICommandPerformanceAggregator`)
- `memory_usage` - Working set memory in MB (from `Process.GetCurrentProcess()`)
- `api_rate_limit_usage` - Calculated from API request tracker
- `database_query_time` - Average query time (from `IDatabaseMetricsCollector`)
- `bot_disconnected` - Event-based (from `IConnectionStateService`)
- `service_failure` - Event-based (from `IBackgroundServiceHealthRegistry`)

**SignalR Notifications:**

Broadcasts real-time updates to connected clients via `DashboardHub`:
- `IncidentCreated` - New incident triggered
- `IncidentResolved` - Incident auto-resolved
- Allows dashboard to update without polling

**Health Tracking:**
- Records heartbeat every monitoring cycle
- Tracks last error and status (Initializing, Running, Stopped)
- Registered with `IBackgroundServiceHealthRegistry` for system health monitoring

#### Configuration: PerformanceAlertOptions

**Configuration Section:** `PerformanceAlerts`
**Location:** `src/DiscordBot.Core/Configuration/PerformanceAlertOptions.cs`

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

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CheckIntervalSeconds` | int | 30 | Interval between metric evaluations |
| `ConsecutiveBreachesRequired` | int | 2 | Consecutive threshold breaches before creating incident |
| `ConsecutiveNormalRequired` | int | 3 | Consecutive normal readings before auto-resolving incident |
| `IncidentRetentionDays` | int | 90 | Days to retain resolved incidents before cleanup |

#### Default Alert Thresholds

The following default thresholds are seeded during database migration:

| Metric Name | Display Name | Warning Threshold | Critical Threshold | Unit | Enabled |
|-------------|--------------|-------------------|-------------------|------|---------|
| `gateway_latency` | Gateway Latency | 100 | 200 | ms | Yes |
| `command_p95_latency` | Command P95 Latency | 300 | 500 | ms | Yes |
| `error_rate` | Error Rate | 1.0 | 5.0 | % | Yes |
| `memory_usage` | Memory Usage | 400 | 480 | MB | Yes |
| `api_rate_limit_usage` | API Rate Limit | 85 | 95 | % | Yes |
| `database_query_time` | Database Query Time | 50 | 100 | ms | Yes |
| `bot_disconnected` | Bot Disconnected | - | 1 | event | Yes |
| `service_failure` | Service Failure | - | 1 | event | Yes |

**Notes:**
- Event-based metrics (bot_disconnected, service_failure) only have critical thresholds
- Thresholds can be customized per deployment via the Alerts page UI
- All metrics are enabled by default but can be disabled individually

#### Incident Lifecycle

**1. Metric Breach Detected**
- AlertMonitoringService evaluates metric every 30 seconds
- If metric exceeds threshold, breach counter increments
- Normal counter resets to 0

**2. Incident Triggered**
- After 2 consecutive breaches (default), new incident created
- Incident status: Active
- Severity determined by threshold type (Warning or Critical)
- SignalR notification sent to dashboard clients

**3. Metric Returns to Normal**
- When metric drops below threshold, normal counter increments
- Breach counter resets to 0

**4. Auto-Resolution**
- After 3 consecutive normal readings (default), incident auto-resolves
- Incident status: Resolved
- ResolvedAt timestamp recorded
- Auto-recovery event created for display in event log
- SignalR notification sent to dashboard clients

**5. Manual Acknowledgment (Optional)**
- Admin+ users can acknowledge incidents before resolution
- Incident status: Acknowledged (if still active) or remains Resolved
- AcknowledgedBy, AcknowledgedAt, and Notes recorded
- Does not affect auto-resolution logic

**6. Retention & Cleanup**
- Resolved incidents retained for 90 days (default)
- Cleanup task removes old incidents beyond retention period

#### Authorization

- **Viewer+**: Can view all alerts, incidents, and configurations
- **Admin+**: Can update alert thresholds and acknowledge incidents
- **SuperAdmin**: Full access to all alert management features

#### Empty State Handling

When no active incidents exist:
- Bell icon with checkmark (indicating system healthy)
- Message: "No active alerts"
- Description: "All performance metrics are within normal thresholds."

#### Navigation

The page includes a shared performance dashboard navigation tab bar:
- Overview
- Health Metrics
- Commands
- API & Rate Limits
- System
- **Alerts** (active)

---

### Performance Dashboard UI Implementation (Issue #573)

**URL:** `/Admin/Performance`
**Authorization:** `RequireViewer` policy
**Page Model:** `src/DiscordBot.Bot/Pages/Admin/Performance/Index.cshtml.cs`

The Performance Dashboard Overview provides a unified view of all bot performance metrics with quick navigation to detailed dashboards.

#### Features

**Overall Health Status Badge**

Displays current system health with visual indicators:
- **Healthy** (Green with pulse animation): All systems operational
- **Warning** (Yellow): Some metrics degraded but functional
- **Critical** (Red with pulse): Severe performance issues detected
- Calculated from background service status and active alerts

**Quick Status Cards**

Five clickable cards linking to detailed dashboards:
- **Bot Health** → `/Admin/Performance/HealthMetrics` - Connection state, uptime, latency
- **Avg Response** → `/Admin/Performance/Commands` - Command performance metrics
- **API Status** → `/Admin/Performance/ApiMetrics` - Discord API usage and rate limits
- **System** → `/Admin/Performance/System` - Database, services, cache health
- **Active Alerts** → `/Admin/Performance/Alerts` - Performance incidents

Each card displays:
- Icon representing the metric category
- Primary metric value with color coding
- Descriptive label
- Status indicator (Healthy/Warning/Critical badge)

**Key Metrics Row**

Four metric cards showing 24-hour rolling statistics:
- **Uptime %**: 30-day rolling average uptime percentage
- **Avg Latency**: Current gateway heartbeat latency in milliseconds
- **Commands Today**: 24-hour command execution count
- **Error Rate**: 24-hour error percentage

All metrics include:
- Large display value
- Unit label
- Status icon (checkmark, warning triangle, etc.)
- Color-coded status (green/yellow/red)

**Charts**

Two interactive Chart.js visualizations:

**Response Time Trend**
- Line chart showing hourly average command response times
- Last 24 hours of data
- Y-axis: milliseconds (0-500ms typical range)
- X-axis: time buckets (hourly)
- Smooth curve rendering
- Color-coded threshold lines (green < 100ms, yellow < 500ms, red > 500ms)

**Command Throughput**
- Bar chart showing commands executed per hour
- Last 24 hours of data
- Y-axis: command count
- X-axis: time buckets (hourly)
- Orange bars with rounded corners matching design system
- Hover tooltips showing exact counts

**Resource Usage**

Four progress bars displaying current resource utilization:

**Memory Usage**
- Progress bar showing working set memory vs. maximum allocated
- Value displayed: `{current}MB / {max}MB`
- Color coding:
  - Green: < 70% utilization
  - Yellow: 70-85% utilization
  - Red: > 85% utilization

**CPU Usage**
- Progress bar showing current CPU percentage
- Value displayed: `{percent}%`
- Color coding: Green < 70%, Yellow 70-90%, Red > 90%

**Database Connections**
- Progress bar showing active connections vs. connection pool max
- Value displayed: `{used} / {max} connections`
- Color coding based on utilization percentage

**API Rate Limit Headroom**
- Progress bar showing percentage of rate limit consumed
- Value displayed: `{percent}% remaining`
- Inverted color scheme (high remaining = green, low = red)

**Recent Alerts**

Scrollable list of up to 5 most recent active performance incidents:
- Severity badge (Critical/Warning/Info) with color coding
- Metric name (e.g., "Gateway Latency", "Error Rate")
- Descriptive message (e.g., "Gateway latency exceeded 200ms")
- Triggered timestamp with relative time display (e.g., "5 minutes ago")
- "Acknowledge" link for Admin+ users
- Empty state when no active alerts exist

**Quick Actions**

Four action buttons providing common administrative tasks:
- **Restart Bot**: Links to `/Admin/Settings` (Bot Control tab) for bot restart
- **Clear Cache**: Placeholder for cache clearing functionality
- **Export Metrics**: Placeholder for metric export functionality
- **Configure Alerts**: Links to `/Admin/Performance/Alerts` threshold configuration

#### View Model

```csharp
public class PerformanceOverviewViewModel
{
    // Overall status
    public string OverallHealthStatus { get; set; }         // "Healthy", "Warning", "Critical"
    public string OverallHealthStatusClass { get; set; }    // CSS class for badge color

    // Bot health
    public string ConnectionState { get; set; }             // "Connected", "Disconnected", etc.
    public int CurrentLatencyMs { get; set; }               // Current gateway latency
    public double UptimePercentage { get; set; }            // 30-day rolling uptime %

    // Command metrics
    public int CommandsToday { get; set; }                  // 24-hour command count
    public double AvgResponseTimeMs { get; set; }           // 24-hour avg response time
    public double ErrorRate { get; set; }                   // 24-hour error percentage

    // Active alerts
    public int ActiveAlertCount { get; set; }               // Count of active incidents
    public IReadOnlyList<PerformanceIncidentDto> RecentAlerts { get; set; }  // Up to 5 recent

    // Resource usage
    public double MemoryUsagePercent { get; set; }          // Working set / max memory %
    public long MemoryUsageMB { get; set; }                 // Current memory in MB
    public long MaxMemoryMB { get; set; }                   // Max allocated memory
    public double CpuUsagePercent { get; set; }             // Current CPU %
    public int DbConnectionsUsed { get; set; }              // Active DB connections
    public int DbConnectionsMax { get; set; }               // Max DB connection pool size
    public double ApiRateLimitPercent { get; set; }         // % of rate limit consumed
}
```

#### API Endpoints Used

The page consumes these existing API endpoints:
- `GET /api/metrics/health` - Overall health status and connection state
- `GET /api/metrics/commands/performance?hours=24` - Command aggregates for today
- `GET /api/metrics/commands/throughput?hours=24&granularity=hour` - Throughput data for chart
- `GET /api/alerts/active` - Active incidents for recent alerts section
- `GET /api/alerts/summary` - Active alert counts

#### Overall Health Calculation

The overall health status is calculated based on multiple factors:

**Critical** (Red with pulse):
- Bot disconnected (`ConnectionState != "Connected"`)
- Any critical severity alerts active
- Error rate > 5% in last 24 hours
- Memory usage > 90%
- Gateway latency > 200ms

**Warning** (Yellow):
- Any warning severity alerts active
- Error rate > 1% in last 24 hours
- Memory usage > 70%
- Gateway latency > 100ms
- Background service degraded or unhealthy

**Healthy** (Green with pulse):
- Bot connected
- No active alerts
- All metrics within normal thresholds
- Background services healthy

#### Auto-Refresh

The page implements automatic refresh functionality:
- Charts refresh every 30 seconds
- Status cards update without full page reload
- Uses `fetch()` API to retrieve latest data from endpoints
- Refresh pauses when browser tab is hidden
- Refresh resumes when tab becomes visible again
- Error handling for failed API requests

JavaScript refresh implementation:
```javascript
let refreshInterval;

function startAutoRefresh() {
    refreshInterval = setInterval(async () => {
        if (!document.hidden) {
            await refreshCharts();
            await refreshMetrics();
            await refreshAlerts();
        }
    }, 30000); // 30 seconds
}

async function refreshCharts() {
    const response = await fetch('/api/metrics/commands/throughput?hours=24&granularity=hour');
    const data = await response.json();
    updateChart(throughputChart, data);
}
```

#### Navigation

The sidebar "Bot Performance" link points to this Overview page as the main entry point. All performance dashboard pages include a shared tab navigation bar:

- **Overview** (active) - `/Admin/Performance`
- Health Metrics - `/Admin/Performance/HealthMetrics`
- Commands - `/Admin/Performance/Commands`
- API & Rate Limits - `/Admin/Performance/ApiMetrics`
- System - `/Admin/Performance/System`
- Alerts - `/Admin/Performance/Alerts`

The tab bar is implemented as a shared partial view: `Pages/Admin/Performance/Shared/_PerformanceNav.cshtml`

#### Empty State Handling

When the bot has no data (new deployment, no commands executed):
- Charts display empty state message: "No data available yet"
- Metric cards show `0` or `N/A` with neutral gray styling
- Recent alerts section shows: "No active alerts - all systems operational"

#### Responsive Design

The dashboard is fully responsive:
- Desktop (≥1024px): 4-column grid for metric cards, side-by-side charts
- Tablet (768-1023px): 2-column grid, stacked charts
- Mobile (<768px): Single column layout, reduced chart heights

All components use Tailwind CSS responsive classes for fluid scaling.

---

### Historical Metrics System (Feature #613)

The Historical Metrics System provides persistent storage and retrieval of system performance metrics over time, enabling real historical data visualization on the System Health page instead of placeholder values.

#### Problem Solved

Previously, all time-series system health data was lost on application restart and charts displayed randomly generated placeholder data. This system stores periodic metric snapshots in the database for trend analysis.

#### MetricsCollectionService

**Type:** Background Service (MonitoredBackgroundService)
**Location:** `src/DiscordBot.Bot/Services/MetricsCollectionService.cs`

Collects system metrics at regular intervals and persists them to the database.

**Collected Metrics:**
- Database: Average query time, total queries, slow query count
- Memory: Working set (MB), private memory (MB), heap size (MB)
- GC: Gen 0, Gen 1, Gen 2 collection counts
- Cache: Hit rate percentage, total entries, hits, misses
- Services: Running count, error count, total registered services

**Key Features:**
- Periodic sampling at configurable interval (default: 60 seconds)
- Automatic cleanup of old snapshots beyond retention period
- Registers with IBackgroundServiceHealthRegistry for health monitoring
- Error-resilient: continues collection after transient failures

#### Data Retention

| Time Range | Sample Retention | Aggregation for Display |
|------------|------------------|------------------------|
| 0-24 hours | All samples (60s intervals) | Raw for 0-6h, 5-min buckets for 7-24h |
| 24h-7 days | All samples | 15-minute buckets |
| 7-30 days | All samples | 1-hour buckets |
| 30+ days | Deleted | Cleanup service removes data |

**Default Retention Period:** 30 days (configurable)
**Estimated Storage:** ~8.6 MB for 30 days of data

#### MetricSnapshot Entity

Stored in `MetricSnapshots` table with index on Timestamp for efficient time-range queries.

```csharp
public class MetricSnapshot
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }  // UTC

    // Database metrics
    public double DatabaseAvgQueryTimeMs { get; set; }
    public long DatabaseTotalQueries { get; set; }
    public int DatabaseSlowQueryCount { get; set; }

    // Memory metrics
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long HeapSizeMB { get; set; }

    // GC metrics
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }

    // Cache metrics
    public double CacheHitRatePercent { get; set; }
    public int CacheTotalEntries { get; set; }
    public long CacheTotalHits { get; set; }
    public long CacheTotalMisses { get; set; }

    // Service health
    public int ServicesRunningCount { get; set; }
    public int ServicesErrorCount { get; set; }
    public int ServicesTotalCount { get; set; }
}
```

#### API Endpoints

See [API Endpoints Reference](api-endpoints.md#historical-metrics-endpoints) for full documentation:

| Endpoint | Description |
|----------|-------------|
| `GET /api/metrics/system/history` | All historical metrics with aggregation |
| `GET /api/metrics/system/history/database` | Database-specific metrics with statistics |
| `GET /api/metrics/system/history/memory` | Memory-specific metrics with statistics |

#### UI Integration

The System Health page (`/Admin/Performance/System`) includes:

**Time Range Selector**
- Buttons: 1h, 6h, 24h, 7d, 30d
- Updates all charts dynamically without page reload
- Active selection highlighted with blue styling

**Charts Updated:**
- **Database Query Time Chart**: Real historical avg query time data
- **Memory & GC Chart**: Real working set and heap size over time

**Timezone Handling:**
- All timestamps stored in UTC
- JavaScript converts to user's local timezone for display
- Chart tooltips show full timestamp with local time

#### Configuration

See `HistoricalMetricsOptions` in the [Configuration](#configuration) section.

---

## Real-Time Updates

The Performance Dashboard supports real-time updates via SignalR, enabling live monitoring without page refresh. This section documents the real-time update integration across performance pages.

### Overview

Real-time updates are provided by the `PerformanceMetricsBroadcastService` background service, which broadcasts metrics at configurable intervals to subscribed SignalR clients. Pages connect to the `DashboardHub` and join specific groups to receive updates.

### Architecture

```
┌─────────────────────────────────┐
│   Performance Dashboard Page    │
│   (Health, Commands, System)    │
└──────────────┬──────────────────┘
               │ SignalR Events
               ▼
┌─────────────────────────────────┐
│         DashboardHub            │
│    /hubs/dashboard endpoint     │
└──────────────┬──────────────────┘
               │ IHubContext<T>
               ▼
┌─────────────────────────────────┐
│ PerformanceMetricsBroadcast     │
│         Service                 │
│   (Background Service)          │
└─────────────┬───────────────────┘
              │ Collects metrics from
              ▼
┌─────────────────────────────────────────────────────────┐
│ ILatencyHistoryService  │ ICommandPerformanceAggregator │
│ IConnectionStateService │ IDatabaseMetricsCollector     │
│ IInstrumentedCache      │ IBackgroundServiceHealthRegistry│
└─────────────────────────────────────────────────────────┘
```

### SignalR Groups

| Group | Pages | Events Received |
|-------|-------|-----------------|
| `performance` | Health Metrics, Commands, Overview | `HealthMetricsUpdate`, `CommandPerformanceUpdate` |
| `system-health` | System Health | `SystemMetricsUpdate` |
| `alerts` | Alerts, Overview | `OnAlertTriggered`, `OnAlertResolved`, `OnAlertAcknowledged`, `OnActiveAlertCountChanged` |

### Broadcast Intervals

| Event | Default Interval | Configurable |
|-------|------------------|--------------|
| `HealthMetricsUpdate` | 5 seconds | `PerformanceBroadcast:HealthMetricsIntervalSeconds` |
| `CommandPerformanceUpdate` | 30 seconds | `PerformanceBroadcast:CommandMetricsIntervalSeconds` |
| `SystemMetricsUpdate` | 10 seconds | `PerformanceBroadcast:SystemMetricsIntervalSeconds` |
| Alert events | On event | N/A (event-driven) |

### Page Integration

#### Health Metrics Page (`/Admin/Performance/HealthMetrics`)

**JavaScript Module:** `wwwroot/js/performance/health-metrics-realtime.js`

**SignalR Usage:**
```javascript
// Connect and subscribe
await DashboardHub.connect();
await DashboardHub.invoke('JoinPerformanceGroup');

// Handle real-time updates
DashboardHub.on('HealthMetricsUpdate', (data) => {
    updateLatencyGauge(data.latencyMs);
    updateMemoryStats(data.workingSetMB, data.privateMemoryMB);
    updateConnectionState(data.connectionState);
    addLatencySample(data.latencyMs, data.timestamp);
});

// Get initial snapshot
const metrics = await DashboardHub.invoke('GetCurrentPerformanceMetrics');
```

**Data Updated:**
- Latency gauge and trend line
- Memory usage bars
- Connection state indicator
- Thread count and GC statistics

---

#### System Health Page (`/Admin/Performance/SystemHealth`)

**JavaScript Module:** `wwwroot/js/performance/system-health-realtime.js`

**SignalR Usage:**
```javascript
// Connect and subscribe
await DashboardHub.connect();
await DashboardHub.invoke('JoinSystemHealthGroup');

// Handle real-time updates
DashboardHub.on('SystemMetricsUpdate', (data) => {
    updateDatabaseMetrics(data.avgQueryTimeMs, data.slowQueryCount);
    updateCacheStatistics(data.cacheStats);
    updateServiceHealth(data.backgroundServices);
    addQueryTimeSample(data.avgQueryTimeMs, data.timestamp);
});

// Get initial snapshot
const health = await DashboardHub.invoke('GetCurrentSystemHealth');
```

**Data Updated:**
- Database query time chart
- Cache hit rate progress bars
- Background service status indicators
- Slow query count badge

---

#### Alerts Page (`/Admin/Performance/Alerts`)

**JavaScript Module:** `wwwroot/js/performance/alerts-realtime.js`

**SignalR Usage:**
```javascript
// Connect and subscribe
await DashboardHub.connect();
await DashboardHub.invoke('JoinAlertsGroup');

// Handle real-time updates
DashboardHub.on('OnAlertTriggered', (incident) => {
    addActiveAlert(incident);
    showNotificationToast(incident);
});

DashboardHub.on('OnAlertResolved', (incident) => {
    moveToResolvedList(incident);
    removeFromActiveAlerts(incident.id);
});

DashboardHub.on('OnAlertAcknowledged', (data) => {
    updateAlertAcknowledgedStatus(data.incidentId, data.acknowledgedBy);
});

DashboardHub.on('OnActiveAlertCountChanged', (summary) => {
    updateAlertBadge(summary.activeCount, summary.criticalCount);
});

// Get initial alert count
const summary = await DashboardHub.invoke('GetActiveAlertCount');
```

**Data Updated:**
- Active alerts list (real-time additions/removals)
- Alert severity badges
- Incident history timeline
- Navigation badge counts

---

### Connection Status Indicator

Each performance page displays a connection status indicator showing the SignalR connection state:

| State | Indicator | Description |
|-------|-----------|-------------|
| Connected | 🟢 Green dot | Real-time updates active |
| Reconnecting | 🟡 Yellow dot with pulse | Connection lost, attempting reconnect |
| Disconnected | 🔴 Red dot | Connection failed, data may be stale |

The indicator is updated via the `dashboard-hub.js` connection events:

```javascript
DashboardHub.on('connected', () => setConnectionStatus('connected'));
DashboardHub.on('reconnecting', () => setConnectionStatus('reconnecting'));
DashboardHub.on('disconnected', () => setConnectionStatus('disconnected'));
```

### Fallback Behavior

When SignalR connection fails or is unavailable:

1. **Initial Load:** Pages load with server-rendered data from PageModel
2. **Auto-Refresh Fallback:** Polling via `fetch()` API every 30 seconds
3. **Reconnection:** SignalR automatically attempts reconnection with exponential backoff
4. **Data Recovery:** On reconnection, pages call `GetCurrent*` hub methods to fetch fresh data

```javascript
DashboardHub.on('reconnected', async () => {
    // Fetch fresh data after reconnection
    const metrics = await DashboardHub.invoke('GetCurrentPerformanceMetrics');
    refreshAllCharts(metrics);
});
```

### Subscription Optimization

The `PerformanceMetricsBroadcastService` uses an `IPerformanceSubscriptionTracker` to optimize broadcasts:

- **Skip Empty Groups:** No broadcasts sent when no clients are subscribed
- **Reference Counting:** Tracks client count per group for efficient resource usage
- **Cleanup on Disconnect:** Automatically removes subscriptions when clients disconnect

This prevents unnecessary CPU and memory usage when no dashboard pages are open.

### Configuration

Real-time updates are configured via `PerformanceBroadcastOptions`:

```json
{
  "PerformanceBroadcast": {
    "Enabled": true,
    "HealthMetricsIntervalSeconds": 5,
    "CommandMetricsIntervalSeconds": 30,
    "SystemMetricsIntervalSeconds": 10
  }
}
```

Set `Enabled: false` to disable all real-time broadcasts (pages will fall back to polling).

### Troubleshooting

#### Charts not updating in real-time

1. Check browser console for SignalR errors
2. Verify `PerformanceBroadcast:Enabled` is `true`
3. Ensure user has at least Viewer role (required for hub access)
4. Check server logs for `PerformanceMetricsBroadcastService` errors

#### High memory usage on client

1. Implement sliding window for chart data (max 100 data points)
2. Clear old chart data on page navigation
3. Consider reducing broadcast frequency for less critical metrics

#### Stale data after reconnection

1. Ensure reconnection handler calls `GetCurrent*` hub methods
2. Check for race conditions between reconnect and event handlers
3. Verify timestamp comparison logic in update handlers

See [SignalR Real-Time Documentation](signalr-realtime.md#performance-troubleshooting) for additional troubleshooting guidance.

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
| `CpuSampleIntervalSeconds` | int | 5 | Interval between CPU samples |
| `CpuRetentionHours` | int | 24 | Hours of CPU history to retain in memory |
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

> **⚠️ Warning: Circular DI Dependencies with Factory-Resolved Services**
>
> The `ICommandPerformanceAggregator` factory above calls `GetServices<IHostedService>()` to find the aggregator instance. Any `BackgroundService` that directly injects `ICommandPerformanceAggregator` in its constructor will create a circular dependency deadlock during startup.
>
> **Solution:** Use lazy service resolution - inject only `IServiceProvider` in the constructor and resolve dependencies in `ExecuteAsync()` after calling `Task.Yield()`. See `AlertMonitoringService` for the pattern.

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
| 1.8 | 2026-01-08 | Added Real-Time Updates section documenting SignalR integration for performance pages (#622, #632) |
| 1.7 | 2026-01-02 | Added Historical Metrics System (#613) documentation; MetricsCollectionService, data retention, API endpoints |
| 1.6 | 2026-01-02 | Marked Performance Dashboard UI (#573) as completed; added Overview page implementation documentation; all 9 sub-issues completed |
| 1.5 | 2026-01-02 | Marked Performance Alerts (#570) as completed; added circular DI dependency fix documentation |
| 1.4 | 2026-01-02 | Added Performance Alerts & Incidents (#570) documentation |
| 1.3 | 2026-01-01 | Added Discord API & Rate Limit Monitoring (#566) documentation |
| 1.2 | 2026-01-01 | Added Command Performance Analytics (#565) documentation |
| 1.1 | 2026-01-01 | Added System Health Monitoring (#568) documentation |
| 1.0 | 2026-01-01 | Initial documentation (Issues #580, #571, #572, #563 completed) |
