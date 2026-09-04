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
- **SignalR Hub:** `DashboardHub` (Bot/Hubs/) — kept as a single hub/URL so the JS client (`wwwroot/js/dashboard-hub.js` etc.) and all `IHubContext<DashboardHub>` broadcasters keep working unchanged. The hub itself is now thin: it owns only connection/group lifecycle (Join/LeaveGuildGroup, Alerts/Performance/SystemHealth/BulkPurge/GuildAudio groups, OnConnected/OnDisconnected) and the authenticated-user short-circuit for notification methods. Everything else is delegated to per-feature services in `Bot/Services/Dashboard/`:
  - `IDashboardMetricsService` / `DashboardMetricsService` — bot status, health, active alert count, performance metrics, system health, command performance (GetCurrentStatus, GetHealthStatus, GetActiveAlertCount, GetCurrentPerformanceMetrics, GetCurrentSystemHealth, GetCurrentCommandPerformance)
  - `IDashboardAudioStatusService` / `DashboardAudioStatusService` — guild voice/audio status (GetCurrentAudioStatus)
  - `IDashboardNotificationQueryService` / `DashboardNotificationQueryService` — per-user notification summary/list/mark-read/dismiss, resolving the scoped `INotificationService` from a fresh DI scope per call
  Registered as scoped services in `PerformanceMetricsServiceExtensions.AddPerformanceMetrics`. Tests split accordingly: `Hubs/DashboardHubTests.cs` (connection/group lifecycle + auth short-circuit) plus `Services/Dashboard/DashboardMetricsServiceTests.cs` and `Services/Dashboard/DashboardNotificationQueryServiceTests.cs`.
- **Services:** `DashboardUpdateService`, `PerformanceMetricsBroadcastService`

### Observability Infrastructure
- **Serilog:** `LoggingServiceExtensions`
- **OpenTelemetry:** `OpenTelemetryExtensions`, `Bot/Tracing/`
- **Elastic APM:** `ElasticApmExtensions`, `Infrastructure/Tracing/`
- **API Tracing:** `DiscordApiTracingHandler`, `ApiRequestTracker` (584 lines)

### Controllers & Pages
- `PerformanceMetricsController` (1,173 lines), `AnalyticsController` (698), `PerformanceTabsController` (199), `AlertsController` (560), `CommandsApiController`
- Performance pages: `Admin/Performance/` (Index + 5 sub-pages + 8 tab partials)
- Guild analytics: `Guilds/Analytics/` (Index, Engagement, Moderation)
- **Single source of truth for the Performance dashboard's view-model building is `IPerformanceDashboardAggregator` / `PerformanceDashboardAggregator`** (`Bot/Interfaces/IPerformanceDashboardAggregator.cs`, `Bot/Services/Performance/PerformanceDashboardAggregator.cs`). It owns every builder (`BuildOverviewAsync`, `BuildHealthMetricsAsync`, `BuildCommandPerformanceAsync`, `BuildApiRateLimits`, `BuildSystemHealth`, `BuildAlertsPageAsync`) including the `Process.GetCurrentProcess()` working-set/GC reads and the "Critical"/`IsLive=false` failure fallbacks. Three call sites route through it and hold no view-model-building logic of their own: `Pages/Admin/Performance/IndexModel` (AJAX partial-tab handler) and the five sibling tab page models (`HealthMetricsModel`, `SystemHealthModel`, `ApiMetricsModel`, `AlertsModel`, `CommandsModel`, each a thin `OnGet(Async)` that calls the matching builder and assigns `ViewModel`), plus `PerformanceTabsController` (the HTMX/AJAX partial-view loader under `api/performance/tabs`). When a tab needs new data, add it to the aggregator — never rebuild it a second time in a page model or the controller.

## Gotchas

- **Very large controllers** — always search for specific methods, never full reads
- **SignalR connection state:** Disconnections cause stale dashboard UI
- **Background services must register** with `BackgroundServiceHealthRegistry` for health monitoring
- **APM is optional:** Elastic APM and OpenTelemetry are opt-in via config; don't make them required dependencies
- **Metrics pipeline:** Background services collect → aggregate → store snapshots → broadcast via SignalR
- **Tab-based UI:** Performance dashboard uses partial views loaded via HTMX
