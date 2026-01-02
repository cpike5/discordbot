# Historical Metrics System Design Specification

**Version:** 1.0
**Created:** 2026-01-02
**Status:** Draft
**Parent Epic:** #295 - Bot Performance Dashboard
**Related Issue:** #610 - System Health page charts display placeholder data

---

## Table of Contents

- [Overview](#overview)
- [Problem Statement](#problem-statement)
- [Design Goals](#design-goals)
- [Scope](#scope)
- [Data Model](#data-model)
- [Collection Service Design](#collection-service-design)
- [API Endpoints](#api-endpoints)
- [UI Updates](#ui-updates)
- [Timezone Handling](#timezone-handling)
- [Configuration](#configuration)
- [Migration Strategy](#migration-strategy)
- [Implementation Phases](#implementation-phases)
- [Acceptance Criteria](#acceptance-criteria)
- [Risks and Mitigations](#risks-and-mitigations)

---

## Overview

The Historical Metrics System provides persistent storage and retrieval of system performance metrics over time, enabling the System Health and other performance dashboard pages to display real historical data instead of placeholder values. This system captures database query times, memory usage, cache statistics, and other system health indicators at regular intervals and stores them for trend analysis.

### Current State

The existing performance monitoring infrastructure provides:

- **Real-time snapshot metrics**: Current memory, GC stats, cache hit rates (in-memory only)
- **Database metrics collector**: Tracks query counts, avg query time, slow queries (in-memory only)
- **Background service registry**: Service health states (in-memory only)
- **Command performance aggregator**: Aggregates from CommandLog table (persisted)
- **Latency history service**: Circular buffer of gateway latency samples (in-memory only)
- **Connection state service**: Connection events stored in-memory

The problem is that **all time-series system health data is lost on application restart** and charts display randomly generated placeholder data.

### Proposed Solution

Implement a persistent historical metrics collection system that:

1. Periodically samples system health metrics and stores them in the database
2. Provides API endpoints for retrieving time-series data with time range filtering
3. Updates UI charts to consume real data with proper time range selectors
4. Handles data retention and cleanup to prevent unbounded growth

---

## Problem Statement

Users viewing the System Health page (`/Admin/Performance/System`) expect to see real historical trends for:

- Database query performance over time
- Memory usage patterns
- Cache effectiveness trends
- Background service uptime

Instead, they see randomly generated data with confusing time labels that don't correspond to actual times. This undermines trust in the monitoring system and provides no actionable insights.

**Root Cause Analysis:**

```javascript
// Current code in SystemHealth.cshtml (lines 597-598, 659-661)
const hours = Array.from({ length: 12 }, (_, i) => (i * 2).toString().padStart(2, '0') + ':00');
const queryTimes = Array.from({ length: 12 }, () => Math.random() * 30 + 10);

const workingSetData = Array.from({ length: 12 }, () => @Model.ViewModel.WorkingSetMB + (Math.random() * 20 - 10));
const heapData = Array.from({ length: 12 }, () => @Model.ViewModel.HeapSizeMB + (Math.random() * 15 - 7.5));
```

The charts generate fake data client-side because no historical data storage exists.

---

## Design Goals

1. **Minimal Impact**: Integrate with existing services without breaking changes
2. **Efficient Storage**: Store aggregated samples, not raw data, to minimize database growth
3. **Configurable Retention**: Allow administrators to tune retention periods
4. **Query Performance**: Support efficient time-range queries with proper indexing
5. **Extensibility**: Design for easy addition of new metric types
6. **Consistency**: Follow existing architectural patterns (repository pattern, Options pattern)
7. **Timezone Awareness**: Store in UTC, display in user's local timezone

---

## Scope

### In Scope

| Category | Items |
|----------|-------|
| Metrics to Store | Database avg query time, Memory (working set, heap), GC collection counts, Cache hit rates, Background service health snapshots |
| Time Ranges | Last 1 hour, 6 hours, 24 hours, 7 days, 30 days |
| Pages Affected | System Health (`/Admin/Performance/System`) |
| New Components | MetricSnapshot entity, MetricSnapshotRepository, MetricsCollectionService, API endpoints |

### Out of Scope (Future Work)

- Real-time streaming updates via SignalR (use page refresh for now)
- Custom metric definitions via UI
- Export to external monitoring systems (Prometheus, Grafana)
- Alert threshold auto-tuning based on historical baselines
- Cross-deployment metric aggregation

---

## Data Model

### New Entity: MetricSnapshot

Stores periodic samples of system metrics. One row per sample period (default: every 60 seconds).

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a point-in-time snapshot of system health metrics.
/// Collected periodically by MetricsCollectionService.
/// </summary>
public class MetricSnapshot
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Timestamp when the snapshot was taken (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    // ===== Database Metrics =====

    /// <summary>
    /// Average database query time in milliseconds at snapshot time.
    /// </summary>
    public double DatabaseAvgQueryTimeMs { get; set; }

    /// <summary>
    /// Total database queries executed since application start.
    /// </summary>
    public long DatabaseTotalQueries { get; set; }

    /// <summary>
    /// Number of slow queries detected in the sample period.
    /// </summary>
    public int DatabaseSlowQueryCount { get; set; }

    // ===== Memory Metrics =====

    /// <summary>
    /// Process working set memory in megabytes.
    /// </summary>
    public long WorkingSetMB { get; set; }

    /// <summary>
    /// Private memory in megabytes.
    /// </summary>
    public long PrivateMemoryMB { get; set; }

    /// <summary>
    /// GC heap size in megabytes.
    /// </summary>
    public long HeapSizeMB { get; set; }

    // ===== GC Metrics =====

    /// <summary>
    /// Gen 0 garbage collection count since process start.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gen 1 garbage collection count since process start.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gen 2 garbage collection count since process start.
    /// </summary>
    public int Gen2Collections { get; set; }

    // ===== Cache Metrics =====

    /// <summary>
    /// Overall cache hit rate as percentage (0-100).
    /// </summary>
    public double CacheHitRatePercent { get; set; }

    /// <summary>
    /// Total cache entries at snapshot time.
    /// </summary>
    public int CacheTotalEntries { get; set; }

    /// <summary>
    /// Total cache hits since application start.
    /// </summary>
    public long CacheTotalHits { get; set; }

    /// <summary>
    /// Total cache misses since application start.
    /// </summary>
    public long CacheTotalMisses { get; set; }

    // ===== Service Health =====

    /// <summary>
    /// Number of background services in "Running" state.
    /// </summary>
    public int ServicesRunningCount { get; set; }

    /// <summary>
    /// Number of background services in error or stopped state.
    /// </summary>
    public int ServicesErrorCount { get; set; }

    /// <summary>
    /// Total registered background services.
    /// </summary>
    public int ServicesTotalCount { get; set; }
}
```

### Database Schema

```sql
CREATE TABLE MetricSnapshots (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL,

    -- Database metrics
    DatabaseAvgQueryTimeMs REAL NOT NULL DEFAULT 0,
    DatabaseTotalQueries INTEGER NOT NULL DEFAULT 0,
    DatabaseSlowQueryCount INTEGER NOT NULL DEFAULT 0,

    -- Memory metrics
    WorkingSetMB INTEGER NOT NULL DEFAULT 0,
    PrivateMemoryMB INTEGER NOT NULL DEFAULT 0,
    HeapSizeMB INTEGER NOT NULL DEFAULT 0,

    -- GC metrics
    Gen0Collections INTEGER NOT NULL DEFAULT 0,
    Gen1Collections INTEGER NOT NULL DEFAULT 0,
    Gen2Collections INTEGER NOT NULL DEFAULT 0,

    -- Cache metrics
    CacheHitRatePercent REAL NOT NULL DEFAULT 0,
    CacheTotalEntries INTEGER NOT NULL DEFAULT 0,
    CacheTotalHits INTEGER NOT NULL DEFAULT 0,
    CacheTotalMisses INTEGER NOT NULL DEFAULT 0,

    -- Service health
    ServicesRunningCount INTEGER NOT NULL DEFAULT 0,
    ServicesErrorCount INTEGER NOT NULL DEFAULT 0,
    ServicesTotalCount INTEGER NOT NULL DEFAULT 0
);

-- Index for efficient time-range queries
CREATE INDEX IX_MetricSnapshots_Timestamp ON MetricSnapshots (Timestamp DESC);
```

### Retention and Aggregation Strategy

| Time Range | Raw Sample Retention | Aggregation Strategy |
|------------|---------------------|---------------------|
| 0-24 hours | Keep all samples (60s intervals) | None - return raw samples |
| 24h-7 days | Keep all samples | Aggregate to 5-minute buckets for display |
| 7-30 days | Keep all samples | Aggregate to 1-hour buckets for display |
| 30+ days | Delete old samples | Cleanup service removes data beyond retention |

**Default Retention Period:** 30 days (configurable via `HistoricalMetricsOptions.RetentionDays`)

**Sample Frequency:** 60 seconds (configurable via `HistoricalMetricsOptions.SampleIntervalSeconds`)

**Estimated Storage:**
- 1 sample = ~200 bytes
- 60 samples/hour * 24 hours * 30 days = 43,200 samples
- ~8.6 MB for 30 days of data (acceptable for SQLite)

---

## Collection Service Design

### MetricsCollectionService

A background service that periodically samples system metrics and persists them.

```csharp
namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that collects system metrics at regular intervals
/// and persists them to the database for historical analysis.
/// </summary>
public class MetricsCollectionService : MonitoredBackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<HistoricalMetricsOptions> _options;
    private readonly ILogger<MetricsCollectionService> _logger;

    public MetricsCollectionService(
        IServiceProvider serviceProvider,
        IBackgroundServiceHealthRegistry healthRegistry,
        IOptions<HistoricalMetricsOptions> options,
        ILogger<MetricsCollectionService> logger)
        : base(healthRegistry, logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override string ServiceName => "MetricsCollectionService";

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.Value.SampleIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectAndPersistMetricsAsync(stoppingToken);
                await CleanupOldSnapshotsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect metrics snapshot");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CollectAndPersistMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricSnapshotRepository>();
        var databaseMetrics = scope.ServiceProvider.GetRequiredService<IDatabaseMetricsCollector>();
        var cache = scope.ServiceProvider.GetRequiredService<IInstrumentedCache>();
        var serviceRegistry = scope.ServiceProvider.GetRequiredService<IBackgroundServiceHealthRegistry>();

        var process = Process.GetCurrentProcess();
        var dbMetrics = databaseMetrics.GetMetrics();
        var cacheStats = cache.GetStatistics();
        var services = serviceRegistry.GetAllHealth();

        var totalCacheHits = cacheStats.Sum(c => c.Hits);
        var totalCacheMisses = cacheStats.Sum(c => c.Misses);
        var totalCacheAccesses = totalCacheHits + totalCacheMisses;

        var snapshot = new MetricSnapshot
        {
            Timestamp = DateTime.UtcNow,

            // Database
            DatabaseAvgQueryTimeMs = dbMetrics.AvgQueryTimeMs,
            DatabaseTotalQueries = dbMetrics.TotalQueries,
            DatabaseSlowQueryCount = dbMetrics.SlowQueryCount,

            // Memory
            WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
            PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
            HeapSizeMB = GC.GetTotalMemory(false) / 1024 / 1024,

            // GC
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),

            // Cache
            CacheHitRatePercent = totalCacheAccesses > 0
                ? (double)totalCacheHits / totalCacheAccesses * 100
                : 0,
            CacheTotalEntries = cacheStats.Sum(c => c.Size),
            CacheTotalHits = totalCacheHits,
            CacheTotalMisses = totalCacheMisses,

            // Services
            ServicesRunningCount = services.Count(s =>
                s.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)),
            ServicesErrorCount = services.Count(s =>
                s.Status.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                s.Status.Equals("Stopped", StringComparison.OrdinalIgnoreCase)),
            ServicesTotalCount = services.Count
        };

        await repository.AddAsync(snapshot, cancellationToken);

        _logger.LogTrace(
            "Metrics snapshot collected: WorkingSet={WorkingSetMB}MB, AvgQueryTime={AvgQueryTimeMs}ms",
            snapshot.WorkingSetMB,
            snapshot.DatabaseAvgQueryTimeMs);
    }

    private async Task CleanupOldSnapshotsAsync(CancellationToken cancellationToken)
    {
        // Run cleanup once per hour (check if last cleanup was >1 hour ago)
        // Implementation uses a static DateTime? to track last cleanup

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricSnapshotRepository>();

        var cutoffDate = DateTime.UtcNow.AddDays(-_options.Value.RetentionDays);
        var deletedCount = await repository.DeleteOlderThanAsync(cutoffDate, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Cleaned up {DeletedCount} metric snapshots older than {CutoffDate:yyyy-MM-dd}",
                deletedCount,
                cutoffDate);
        }
    }
}
```

### IMetricSnapshotRepository Interface

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository for persisting and retrieving historical metric snapshots.
/// </summary>
public interface IMetricSnapshotRepository
{
    /// <summary>
    /// Adds a new metric snapshot.
    /// </summary>
    Task AddAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots within a time range, optionally aggregated.
    /// </summary>
    /// <param name="startTime">Start of the time range (UTC).</param>
    /// <param name="endTime">End of the time range (UTC).</param>
    /// <param name="aggregationMinutes">
    /// If > 0, aggregate samples into buckets of this size (in minutes).
    /// If 0, return raw samples.
    /// </param>
    Task<IReadOnlyList<MetricSnapshotDto>> GetRangeAsync(
        DateTime startTime,
        DateTime endTime,
        int aggregationMinutes = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent snapshot.
    /// </summary>
    Task<MetricSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes snapshots older than the specified date.
    /// </summary>
    /// <returns>Number of deleted records.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of snapshots in a time range.
    /// </summary>
    Task<int> GetCountAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);
}
```

### MetricSnapshotDto

DTO for API responses with aggregated data.

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO representing a metric snapshot or aggregated bucket.
/// </summary>
public record MetricSnapshotDto
{
    /// <summary>
    /// Timestamp of the snapshot or bucket start time (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Average database query time in milliseconds.
    /// </summary>
    public double DatabaseAvgQueryTimeMs { get; init; }

    /// <summary>
    /// Working set memory in MB.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Heap size in MB.
    /// </summary>
    public long HeapSizeMB { get; init; }

    /// <summary>
    /// Cache hit rate percentage.
    /// </summary>
    public double CacheHitRatePercent { get; init; }

    /// <summary>
    /// Number of running background services.
    /// </summary>
    public int ServicesRunningCount { get; init; }

    /// <summary>
    /// Gen 0 GC collections (delta for aggregated buckets).
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Gen 1 GC collections (delta for aggregated buckets).
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Gen 2 GC collections (delta for aggregated buckets).
    /// </summary>
    public int Gen2Collections { get; init; }
}
```

---

## API Endpoints

### New Endpoints in PerformanceMetricsController

Add to `src/DiscordBot.Bot/Controllers/PerformanceMetricsController.cs`:

#### GET /api/metrics/system/history

Retrieves historical metric snapshots for charting.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `hours` | int | 24 | Time range in hours (1-720) |
| `metric` | string | "all" | Metric type: "database", "memory", "cache", "services", or "all" |

**Response:**

```json
{
  "startTime": "2026-01-01T00:00:00Z",
  "endTime": "2026-01-02T00:00:00Z",
  "granularity": "5m",
  "snapshots": [
    {
      "timestamp": "2026-01-01T00:00:00Z",
      "databaseAvgQueryTimeMs": 12.5,
      "workingSetMB": 256,
      "heapSizeMB": 128,
      "cacheHitRatePercent": 94.2,
      "servicesRunningCount": 8,
      "gen0Collections": 15,
      "gen1Collections": 3,
      "gen2Collections": 0
    }
  ]
}
```

**Aggregation Logic:**

| Hours Requested | Aggregation |
|----------------|-------------|
| 1-6 | Raw samples (no aggregation) |
| 7-24 | 5-minute buckets |
| 25-168 (7 days) | 15-minute buckets |
| 169-720 (30 days) | 1-hour buckets |

#### GET /api/metrics/system/history/database

Retrieves database-specific historical metrics.

**Response:**

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

#### GET /api/metrics/system/history/memory

Retrieves memory-specific historical metrics.

**Response:**

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
    }
  ],
  "statistics": {
    "avgWorkingSetMB": 248,
    "maxWorkingSetMB": 312,
    "avgHeapSizeMB": 125
  }
}
```

---

## UI Updates

### SystemHealth.cshtml Changes

Replace placeholder data generation with API calls.

#### Time Range Selector Component

Add a time range selector above the charts:

```html
<!-- Time Range Selector -->
<div class="flex items-center gap-2 mb-4">
    <span class="text-sm text-text-secondary">Time Range:</span>
    <div class="inline-flex rounded-lg border border-border-primary overflow-hidden">
        <button class="time-range-btn active" data-hours="1">1h</button>
        <button class="time-range-btn" data-hours="6">6h</button>
        <button class="time-range-btn" data-hours="24">24h</button>
        <button class="time-range-btn" data-hours="168">7d</button>
        <button class="time-range-btn" data-hours="720">30d</button>
    </div>
</div>
```

#### JavaScript Updates

```javascript
let selectedHours = 24;
let queryTimeChart;
let memoryChart;

async function initQueryTimeChart() {
    const ctx = document.getElementById('queryTimeChart');
    if (!ctx) return;

    const data = await fetchDatabaseHistory(selectedHours);

    queryTimeChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: data.samples.map(s => formatTimestamp(s.timestamp)),
            datasets: [{
                label: 'Avg Query Time',
                data: data.samples.map(s => s.avgQueryTimeMs),
                borderColor: '#098ecf',
                backgroundColor: 'rgba(9, 142, 207, 0.1)',
                fill: true,
                tension: 0.4,
                pointRadius: selectedHours <= 6 ? 3 : 0
            }]
        },
        options: {
            // ... existing options
        }
    });
}

async function fetchDatabaseHistory(hours) {
    const response = await fetch(`/api/metrics/system/history/database?hours=${hours}`);
    if (!response.ok) throw new Error('Failed to fetch database history');
    return await response.json();
}

async function fetchMemoryHistory(hours) {
    const response = await fetch(`/api/metrics/system/history/memory?hours=${hours}`);
    if (!response.ok) throw new Error('Failed to fetch memory history');
    return await response.json();
}

function formatTimestamp(isoString) {
    const date = new Date(isoString);
    // Convert UTC to local timezone for display
    if (selectedHours <= 24) {
        return date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        });
    } else if (selectedHours <= 168) {
        return date.toLocaleDateString('en-US', {
            weekday: 'short',
            hour: '2-digit',
            hour12: false
        });
    } else {
        return date.toLocaleDateString('en-US', {
            month: 'short',
            day: 'numeric'
        });
    }
}

// Time range button handlers
document.querySelectorAll('.time-range-btn').forEach(btn => {
    btn.addEventListener('click', async function() {
        document.querySelectorAll('.time-range-btn').forEach(b => b.classList.remove('active'));
        this.classList.add('active');
        selectedHours = parseInt(this.dataset.hours);
        await refreshCharts();
    });
});

async function refreshCharts() {
    const [dbData, memData] = await Promise.all([
        fetchDatabaseHistory(selectedHours),
        fetchMemoryHistory(selectedHours)
    ]);

    updateQueryTimeChart(dbData);
    updateMemoryChart(memData);
}
```

### Chart Configuration Updates

- Add loading spinner while fetching data
- Show "No data available" state when API returns empty results
- Add error handling with retry option
- Update tooltip to show full timestamp with timezone

---

## Timezone Handling

### Storage

- All timestamps stored in UTC in the `MetricSnapshot.Timestamp` column
- EF Core configuration ensures UTC handling:
  ```csharp
  entity.Property(e => e.Timestamp)
      .HasConversion(
          v => v.ToUniversalTime(),
          v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
  ```

### API Response

- All timestamps returned in ISO 8601 format with Z suffix (UTC)
- Example: `"timestamp": "2026-01-02T14:30:00Z"`

### Display Layer

- JavaScript converts UTC timestamps to user's local timezone using `Date.toLocaleString()`
- Chart labels show times in user's local timezone
- Tooltip shows full datetime with timezone indicator
- Example display: "Jan 02, 2:30 PM" (user sees their local time)

### Acceptance Criteria for Timezone

- [ ] Timestamps stored as UTC in database
- [ ] API returns ISO 8601 UTC timestamps
- [ ] Chart X-axis shows times in user's local timezone
- [ ] Tooltip shows full timestamp with timezone context
- [ ] Time range queries work correctly across timezone boundaries

---

## Configuration

### HistoricalMetricsOptions

```csharp
namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for historical metrics collection.
/// </summary>
public class HistoricalMetricsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "HistoricalMetrics";

    /// <summary>
    /// Interval between metric samples in seconds.
    /// Default: 60 seconds.
    /// </summary>
    public int SampleIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Number of days to retain historical snapshots.
    /// Default: 30 days.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether to enable historical metrics collection.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hours between cleanup runs.
    /// Default: 6 hours.
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 6;
}
```

### appsettings.json

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

---

## Migration Strategy

### EF Core Migration

```csharp
public partial class AddMetricSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MetricSnapshots",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                DatabaseAvgQueryTimeMs = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0),
                DatabaseTotalQueries = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0),
                DatabaseSlowQueryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                WorkingSetMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0),
                PrivateMemoryMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0),
                HeapSizeMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0),
                Gen0Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                Gen1Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                Gen2Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CacheHitRatePercent = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0),
                CacheTotalEntries = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CacheTotalHits = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0),
                CacheTotalMisses = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0),
                ServicesRunningCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                ServicesErrorCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                ServicesTotalCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MetricSnapshots", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MetricSnapshots_Timestamp",
            table: "MetricSnapshots",
            column: "Timestamp",
            descending: new[] { true });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MetricSnapshots");
    }
}
```

---

## Implementation Phases

### Phase 1: Data Infrastructure (Task)

**Estimated Effort:** 4 hours

- [ ] Create `MetricSnapshot` entity
- [ ] Create EF Core migration
- [ ] Add `IMetricSnapshotRepository` interface
- [ ] Implement `MetricSnapshotRepository`
- [ ] Add `HistoricalMetricsOptions` configuration class
- [ ] Register services in DI

**Deliverables:**
- Entity and migration files
- Repository interface and implementation
- Configuration class
- Unit tests for repository

### Phase 2: Collection Service (Task)

**Estimated Effort:** 3 hours

- [ ] Implement `MetricsCollectionService` background service
- [ ] Add cleanup logic for old snapshots
- [ ] Register with `IBackgroundServiceHealthRegistry`
- [ ] Add integration tests

**Deliverables:**
- Background service implementation
- Service registration
- Integration tests

### Phase 3: API Endpoints (Task)

**Estimated Effort:** 3 hours

- [ ] Add `/api/metrics/system/history` endpoint
- [ ] Add `/api/metrics/system/history/database` endpoint
- [ ] Add `/api/metrics/system/history/memory` endpoint
- [ ] Implement aggregation logic
- [ ] Add API tests

**Deliverables:**
- Three new API endpoints
- Aggregation helper methods
- API documentation in Swagger
- API tests

### Phase 4: UI Updates (Task)

**Estimated Effort:** 4 hours

- [ ] Add time range selector component
- [ ] Update Database Performance chart to use real data
- [ ] Update Memory & GC chart to use real data
- [ ] Add loading states and error handling
- [ ] Implement timezone conversion in JavaScript
- [ ] Update chart tooltips with full timestamps

**Deliverables:**
- Updated SystemHealth.cshtml
- Updated JavaScript chart initialization
- CSS for time range selector

### Phase 5: Documentation & Testing (Task)

**Estimated Effort:** 2 hours

- [ ] Update `bot-performance-dashboard.md` documentation
- [ ] Add historical metrics section to docs
- [ ] End-to-end testing
- [ ] Performance testing with 30 days of data

**Deliverables:**
- Updated documentation
- Test results summary

---

## Acceptance Criteria

### Functional Requirements

1. **Data Collection**
   - [ ] Metrics collected every 60 seconds (configurable)
   - [ ] All specified metrics captured: database, memory, GC, cache, services
   - [ ] Collection service registers with health registry
   - [ ] Service handles errors gracefully without crashing

2. **Data Retention**
   - [ ] Snapshots older than retention period (30 days default) are deleted
   - [ ] Cleanup runs periodically without impacting performance
   - [ ] Retention period is configurable

3. **API Endpoints**
   - [ ] `/api/metrics/system/history` returns aggregated data
   - [ ] Time range parameter (hours) works correctly
   - [ ] Aggregation granularity adjusts based on time range
   - [ ] Endpoints return 200 OK with empty array when no data
   - [ ] Endpoints return 400 Bad Request for invalid parameters

4. **UI Updates**
   - [ ] Time range selector with 1h, 6h, 24h, 7d, 30d options
   - [ ] Charts display real historical data
   - [ ] Charts show loading state while fetching
   - [ ] Charts handle errors with user-friendly message
   - [ ] X-axis labels show times in user's local timezone
   - [ ] Tooltips show full timestamp information

5. **Timezone Handling**
   - [ ] All timestamps stored as UTC in database
   - [ ] API returns ISO 8601 UTC timestamps
   - [ ] UI converts to local timezone for display

### Non-Functional Requirements

1. **Performance**
   - [ ] API response time < 500ms for 30-day queries
   - [ ] Collection service CPU impact < 1%
   - [ ] Database growth < 10MB per month

2. **Reliability**
   - [ ] Collection continues after transient database errors
   - [ ] Missing data periods handled gracefully in charts
   - [ ] Service auto-recovers after application restart

---

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Database growth exceeds expectations | Low | Medium | Configurable retention, cleanup service, monitoring |
| Collection service impacts bot performance | Low | High | Lightweight sampling, async operations, configurable interval |
| Time range queries slow on large datasets | Medium | Medium | Proper indexing, aggregation for long ranges |
| Chart rendering slow with many data points | Low | Low | Client-side aggregation, point reduction |
| Timezone confusion in UI | Medium | Medium | Clear tooltip formatting, consistent UTC storage |

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-02 | Initial specification |
