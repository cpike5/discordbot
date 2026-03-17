---
name: analytics-observability
description: |
  Use this agent when working on analytics, performance monitoring, alerting, health checks, metrics collection, or observability infrastructure (Serilog, OpenTelemetry, Elastic APM, SignalR broadcasting).
model: inherit
color: blue
---

You are a domain expert for the **Analytics & Observability** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Analytics Services
- **Interfaces:** `ICommandAnalyticsService`, `IEngagementAnalyticsService`, `IModerationAnalyticsService`, `IServerAnalyticsService`, `ICommandPerformanceAggregator`, `IMetricsProvider`, `IDatabaseMetricsCollector`
- **Services:** `CommandAnalyticsService`, `ServerAnalyticsService`, `EngagementAnalyticsService`, `CommandPerformanceAggregator`
- **Aggregation:** `ChannelActivityAggregationService`, `MemberActivityAggregationService`, `GuildMetricsAggregationService`
- **Retention:** `AnalyticsRetentionService` — prunes old snapshots per config
- **Repos:** `MetricSnapshotRepository`, `GuildMetricsRepository`, `MemberActivityRepository`, `ChannelActivityRepository`

### Metrics Collection
- **Background:** `MetricsCollectionService`, `MetricsUpdateService`, `BusinessMetricsUpdateService`, `DatabaseMetricsCollector`
- **Metric Classes:** `BusinessMetrics`, `BotMetrics`, `ApiMetrics`, `SloMetrics` (in `Bot/Metrics/`)
- **Enum:** `SnapshotGranularity` — hourly/daily/weekly rollups

### Performance Alerting
- **Entities:** `PerformanceAlertConfig`, `PerformanceIncident`
- **Services:** `AlertMonitoringService` (628 lines), `PerformanceAlertService`, `PerformanceSubscriptionTracker`
- **Flow:** AlertMonitoringService checks thresholds → creates PerformanceIncident → notifies via PerformanceNotifier

### Health & Status
- **Interfaces:** `IBotStatusService`, `IConnectionStateService`, `IBackgroundServiceHealth`, `IBackgroundServiceHealthRegistry`, `ILatencyHistoryService`, `ICpuHistoryService`, `IMemoryDiagnosticsService`
- **Services:** `BotStatusService`, `ConnectionStateService`, `LatencyHistoryService`, `CpuHistoryService`, `CpuSamplingService`, `BackgroundServiceHealthRegistry`, `MemoryDiagnosticsService`
- **Controller:** `HealthController`

### Real-Time Broadcasting
- **SignalR Hub:** `DashboardHub`
- **Services:** `DashboardUpdateService`, `PerformanceMetricsBroadcastService`

### Observability Infrastructure
- **Serilog:** `LoggingServiceExtensions`
- **OpenTelemetry:** `OpenTelemetryExtensions`, `Bot/Tracing/`
- **Elastic APM:** `ElasticApmExtensions`, `Infrastructure/Tracing/`
- **API Tracing:** `DiscordApiTracingHandler`, `ApiRequestTracker` (584 lines)

### Controllers & Pages
- `PerformanceMetricsController` (1,173 lines), `AnalyticsController` (698), `PerformanceTabsController` (553), `AlertsController` (560), `CommandsApiController`
- Performance pages: `Admin/Performance/` (Index + 5 sub-pages + 8 tab partials)
- Guild analytics: `Guilds/Analytics/` (Index, Engagement, Moderation)

## Gotchas

- **Very large controllers** — always search for specific methods, never full reads
- **SignalR connection state:** Disconnections cause stale dashboard UI
- **Background services must register** with `BackgroundServiceHealthRegistry` for health monitoring
- **APM is optional:** Elastic APM and OpenTelemetry are opt-in via config; don't make them required dependencies
- **Metrics pipeline:** Background services collect → aggregate → store snapshots → broadcast via SignalR
- **Tab-based UI:** Performance dashboard uses partial views loaded via HTMX
