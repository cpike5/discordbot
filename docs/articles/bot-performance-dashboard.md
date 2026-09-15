# Bot Performance Dashboard

**Last Updated:** 2026-09-11
**Feature Reference:** Issue #295 (Epic); consolidated into a single tabbed page by
issue #722 (Phase 0 UI cleanup).

## Overview

The Bot Performance Dashboard is a single admin page, `/Admin/Performance`, that
shows the bot's gateway health, command throughput and latency, Discord API usage,
system resources, and performance alerts. It replaced five earlier single-purpose
pages (`HealthMetrics`, `SystemHealth`, `Commands`, `ApiMetrics`, `Alerts`), which
were deleted; their routes now permanently redirect here (see
[Route and tab model](#route-and-tab-model)).

The page is a tab shell: one Razor Page (`Index.cshtml` / `IndexModel`) that lazily
loads each tab's content as an HTML partial over AJAX, backed by a single
aggregator service and a family of `/api/metrics/*` and `/api/alerts/*` REST
endpoints. Three of the six tabs (Health, Commands, System) also have a SignalR
broadcast path, though as documented in
[Real-time updates](#real-time-updates), no tab's client code currently subscribes
to it.

## Route and tab model

| Route | Behavior |
| --- | --- |
| `/Admin/Performance` | The dashboard. `#overview` (default), `#health`, `#commands`, `#api`, `#system`, `#alerts` select a tab via URL hash. |
| `/Admin/Performance/HealthMetrics` | 301 redirect to `/Admin/Performance#health` |
| `/Admin/Performance/SystemHealth` | 301 redirect to `/Admin/Performance#system` |
| `/Admin/Performance/Commands` | 301 redirect to `/Admin/Performance#commands` |
| `/Admin/Performance/ApiMetrics` | 301 redirect to `/Admin/Performance#api` |
| `/Admin/Performance/Alerts` | 301 redirect to `/Admin/Performance#alerts` |

The redirects are registered in
`src/DiscordBot.Bot/Extensions/LegacyRedirectExtensions.cs`
(`MapLegacyRouteRedirects`), gated behind `RequireViewer`, and exist purely to
keep old bookmarks and shared links working — the five page files themselves are
gone. `wwwroot/js/performance/dashboard.js` reads `location.hash` on load to pick
the starting tab and updates it (via `history.pushState`) as the user switches
tabs, so a deep link like `/Admin/Performance#alerts` opens directly on that tab.

Every page and endpoint under Performance requires the `RequireViewer` policy;
editing alert thresholds and acknowledging incidents additionally require
`RequireAdmin` (see [Alert endpoints](#alert-configuration-and-incident-endpoints)).

## Server side

### Page model and tab partials

`Pages/Admin/Performance/Index.cshtml.cs` (`IndexModel`) is a thin router. `OnGet`
renders the shell (header, tab nav, time-range buttons, empty tab-content
container) using the Overview tab's data. `OnGetPartialAsync` — the
`?handler=Partial&tabId={tab}&hours={hours}` endpoint the client calls for every
tab load and every tab switch — validates `tabId` and `hours` (only 24, 168, or
720 are accepted; anything else falls back to 24) and returns the matching
partial view:

| `tabId` | Partial | Aggregator method |
| --- | --- | --- |
| `overview` | `Tabs/_OverviewTab.cshtml` | `BuildOverviewAsync(hours)` |
| `health` | `Tabs/_HealthTab.cshtml` | `BuildHealthMetricsAsync()` |
| `commands` | `Tabs/_CommandsTab.cshtml` | `BuildCommandPerformanceAsync(hours)` |
| `api` | `Tabs/_ApiTab.cshtml` | `BuildApiRateLimits(hours)` |
| `system` | `Tabs/_SystemTab.cshtml` | `BuildSystemHealth()` |
| `alerts` | `Tabs/_AlertsTab.cshtml` | `BuildAlertsPageAsync(User)` |

An unrecognized `tabId` returns 404. Note: `Pages/Admin/Performance/Tabs/` also
contains two unreferenced partials, `_HealthMetricsTab.cshtml` and
`_ApiMetricsTab.cshtml` — leftovers from before the tab conversion that nothing
routes to; they are not part of the live page.

A second, parallel entry point exists for the same six partials:
`Controllers/PerformanceTabsController.cs` exposes `GET /api/performance/tabs/{tab}`
(`overview`, `health`, `commands`, `api`, `system`, `alerts`), calling the same
`IPerformanceDashboardAggregator` methods and rendering the same partial views,
with an inline HTML error fragment on failure instead of a JSON error. The
shipped dashboard does not call it — `dashboard.js` only ever fetches
`/Admin/Performance?handler=Partial&...` — so this controller is currently dead
from the browser's perspective (unused by any JS, no dedicated tests).

### Aggregator service

`Services/Performance/PerformanceDashboardAggregator.cs` implements
`IPerformanceDashboardAggregator` and is the only thing the page model and
`PerformanceTabsController` talk to for tab content. It pulls together
`IConnectionStateService`, `ILatencyHistoryService`, `ICpuHistoryService`,
`ICommandPerformanceAggregator`, `IApiRequestTracker`,
`IBackgroundServiceHealthRegistry`, `IMemoryDiagnosticsService`,
`IDatabaseMetricsCollector`, `IInstrumentedCache`, `IPerformanceAlertService`, and
`IAuthorizationService` (the last to compute whether the current user can edit
alert configuration) into the six tab view models plus the shell view model. See
`docs/architecture/service-catalog.md` for what each underlying service does.

### Metrics endpoints (per tab)

`Controllers/PerformanceMetricsController.cs` (`api/metrics`, `RequireViewer`) is
what the tab JavaScript modules actually call to refresh chart/table data after
the initial partial has rendered:

| Endpoint | Used by |
| --- | --- |
| `GET /api/metrics/health` | Overview health badge |
| `GET /api/metrics/health/latency?hours=` | Health tab latency chart |
| `GET /api/metrics/health/cpu?hours=` | Health tab CPU chart |
| `GET /api/metrics/health/connections?days=` | Health tab connection history |
| `GET /api/metrics/commands/performance?hours=` | Overview and Commands tabs |
| `GET /api/metrics/commands/slowest?limit=&hours=` | Commands tab |
| `GET /api/metrics/commands/throughput?hours=&granularity=` | Overview and Commands tabs |
| `GET /api/metrics/commands/errors?hours=&limit=` | Commands tab |
| `GET /api/metrics/api/usage?hours=` | API tab |
| `GET /api/metrics/api/rate-limits?hours=` | API tab |
| `GET /api/metrics/api/latency?hours=` | API tab |
| `GET /api/metrics/system/database?limit=` | System tab |
| `GET /api/metrics/system/services` | System tab |
| `GET /api/metrics/system/cache` | System tab |
| `GET /api/metrics/system/history?hours=` | System tab (historical trend) |
| `GET /api/metrics/system/history/database?hours=` | System tab database chart |
| `GET /api/metrics/system/history/memory?hours=` | System tab memory/GC chart |

Aggregation logic for the error-breakdown, cache-summary, and historical-metrics
endpoints is delegated to `IPerformanceMetricsQueryService`; everything else
(health, latency, CPU, connections, command performance/slowest/throughput, API
usage/rate-limits/latency, database metrics, service health) reads directly from
the collector/tracker services listed above. Full request/response shapes are in
`docs/articles/api-endpoints.md`.

### Alert configuration and incident endpoints

`Controllers/AlertsController.cs` (`api/alerts`, `Produces: application/json`)
backs the Alerts tab:

| Endpoint | Policy |
| --- | --- |
| `GET /api/alerts/config` | `RequireViewer` |
| `GET /api/alerts/config/{metricName}` | `RequireViewer` |
| `PUT /api/alerts/config/{metricName}` | `RequireAdmin` |
| `GET /api/alerts/active` | `RequireViewer` |
| `GET /api/alerts/incidents` | `RequireViewer` |
| `GET /api/alerts/incidents/{id:guid}` | `RequireViewer` |
| `POST /api/alerts/incidents/{id:guid}/acknowledge` | `RequireAdmin` |
| `POST /api/alerts/incidents/acknowledge-all` | `RequireAdmin` |
| `GET /api/alerts/summary` | `RequireViewer` |
| `GET /api/alerts/stats` | `RequireViewer` |

Threshold updates and acknowledgments go through `IPerformanceAlertService`,
which also calls `IPerformanceNotifier` to broadcast the change (see
[Real-time updates](#real-time-updates)) — that broadcast is the only real-time
signal these endpoints produce; the tab itself does not listen for it.
`AlertMonitoringService` (a hosted service) independently evaluates
`PerformanceAlertOptions`-configured thresholds and raises/resolves incidents;
it is unrelated to the controller beyond sharing the same repository.

## Client side

### dashboard.js (tab shell)

`wwwroot/js/performance/dashboard.js` (global `window.PerformanceTabs`, aliased
`window.Performance.Dashboard`) owns tab switching for the whole page:

- Reads the initial tab from `location.hash`, falls back to the active tab link,
  then to `overview`.
- On tab switch: cancels any in-flight fetch, plays a fade-out/fade-in
  transition, fetches `/Admin/Performance?handler=Partial&tabId={tab}&hours={hours}`,
  swaps the returned HTML into `#tabContent`, re-executes any `<script>` tags in
  it, and calls a tab's `init{Tab}Tab(hours)` / `destroy{Tab}Tab()` global
  functions if the tab module defines them.
- Caches each tab's rendered HTML in memory for 5 minutes (`cacheTimeout`) keyed
  by tab + hours, so switching back to a recently-viewed tab doesn't re-fetch.
- Debounces browser history updates and handles `popstate` so back/forward moves
  between tabs.
- Manages the live region announcements for screen readers, and Chart.js
  instance cleanup (`chart.destroy()`) when leaving a tab.

### Time range persistence

Time range (24h / 7d / 30d, i.e. `hours=24|168|720`) is shared across tabs and
persisted in `localStorage` under `performance-dashboard-time-range` in two
places that do the same job: `dashboard.js` itself (`restoreTimeRange` /
`saveTimeRange`) and the standalone `wwwroot/js/performance/time-range.js`
module (`window.Performance.TimeRange`), which dispatches a `timeRangeChanged`
DOM event that `dashboard.js` also listens for. Changing the range clears the
tab cache and reloads the active tab with the new `hours` value.

### Per-tab modules

Each tab has its own file under `wwwroot/js/performance/tabs/`, loaded once in
`Index.cshtml` and driven by `dashboard.js`'s `init*Tab` / `destroy*Tab`
convention. All of them fetch their chart/table data from the `/api/metrics/*`
or `/api/alerts/*` endpoints above via `fetch()` — none of them use SignalR (see
[Real-time updates](#real-time-updates)), and none of them poll on a timer; data
is fetched once per tab activation or time-range change.

| File | Renders |
| --- | --- |
| `tabs/overview.js` | Response-time and throughput charts, quick-status cards, resource bars |
| `tabs/health.js` | Latency gauge/chart, CPU chart, connection history |
| `tabs/commands.js` | Response-time/throughput/error-rate charts, slowest-commands table, timeout table |
| `tabs/api.js` | API latency chart, rate-limit log, usage-by-category table |
| `tabs/system.js` | Database and memory/GC historical charts, background-service status, cache hit-rate bars |
| `tabs/alerts.js` | Active alerts list, threshold config table, incident history, alert-frequency chart, acknowledge/acknowledge-all actions |

### Shared utilities

- `wwwroot/js/performance/components/chart-utils.js` (`window.Performance.ChartUtils`) —
  shared Chart.js defaults/theme and helper builders so every tab's charts look
  the same in dark mode.
- `wwwroot/js/performance/components/timestamp-utils.js` (`window.Performance.TimestampUtils`) —
  converts `[data-utc-time]` elements to the viewer's local time; `dashboard.js`
  also has its own inline copy of this conversion that runs after every tab load.
- `wwwroot/js/realtime-ui.js` — small generic helpers (`animateValueChange`,
  `updateMetrics`, `setLiveIndicatorState`) for flashing a value when it changes
  and toggling a "live" indicator's paused state. Generic utility functions only;
  it does not itself open or manage a SignalR connection.

## Real-time updates

SignalR real-time updates exist end-to-end on the **server**, but as of this
branch **no Performance tab's client code subscribes to them**. This is a real
gap, not a documentation lag — grep `wwwroot/js/performance/` for `signalR`,
`Hub`, or `connection.on` and the only hit is an inert placeholder block in
`Index.cshtml` (a `RealtimeUI` object with the comment "Will be wired up to
SignalR when DashboardHub is extended"). The dashboard works entirely by
fetching on tab activation and time-range change; it does not auto-refresh and
does not update in place when new data arrives server-side.

### What exists server-side

`Hubs/DashboardHub.cs` (`RequireViewer`) defines three relevant groups and their
join/leave methods:

| Group constant | Join method | Events sent to it |
| --- | --- | --- |
| `PerformanceGroupName` ("performance") | `JoinPerformanceGroup` | `HealthMetricsUpdate`, `CommandPerformanceUpdate` |
| `SystemHealthGroupName` ("system-health") | `JoinSystemHealthGroup` | `SystemMetricsUpdate` |
| `AlertsGroupName` ("alerts") | `JoinAlertsGroup` | `OnAlertTriggered`, `OnAlertResolved`, `OnAlertAcknowledged`, `OnActiveAlertCountChanged` |

`Services/PerformanceMetricsBroadcastService.cs` (a `MonitoredBackgroundService`)
runs three independent `PeriodicTimer` loops and, only when
`IPerformanceSubscriptionTracker` reports at least one client in the relevant
group, broadcasts:

- `HealthMetricsUpdate` to `performance` every `HealthMetricsIntervalSeconds` (default 5s)
- `CommandPerformanceUpdate` to `performance` every `CommandMetricsIntervalSeconds` (default 30s)
- `SystemMetricsUpdate` to `system-health` every `SystemMetricsIntervalSeconds` (default 10s)

`Services/PerformanceNotifier.cs` (`IPerformanceNotifier`) sends the four alert
events to the `alerts` group whenever `IPerformanceAlertService` or
`AlertMonitoringService` triggers, resolves, or acknowledges an incident, or
whenever the active count changes.

`wwwroot/js/dashboard-hub.js` (loaded globally via `_Layout.cshtml`, connected on
every authenticated page) already exposes `joinPerformanceGroup`,
`joinSystemHealthGroup`, `joinAlertsGroup` and their `leave*` counterparts plus
`getCurrentPerformanceMetrics` / `getCurrentSystemHealth` / `getActiveAlertCount`
hub-method wrappers — the client plumbing is present and reusable. Nothing on
`/Admin/Performance` calls any of them.

### Known gap

Concretely: opening the Health, Commands, System, or Alerts tab does **not** join
the corresponding hub group, so the broadcast service treats those groups as
empty and skips broadcasting to them entirely (`PerformanceGroupClientCount` /
`SystemHealthGroupClientCount` stay at zero from Performance-page traffic). The
Alerts tab in particular has no live incident push: a new incident triggered
while an admin is looking at the tab will not appear, and an acknowledgment made
by someone else will not be reflected, until the tab is manually reloaded or
switched away from and back. `docs/articles/alerting-system.md` documents this
same gap for the Alerts tab specifically (its former `alerts-realtime.js` client
was removed as dead code in the Phase 0 cleanup, precisely because it wasn't
wired to anything).

## Configuration

Four options classes affect this feature; see
`docs/articles/configuration-guide.md` for the full table:

- `PerformanceMetrics` (`PerformanceMetricsOptions`) — latency/CPU sampling
  intervals and retention, slow-query threshold, command aggregation cache TTL.
- `PerformanceBroadcast` (`PerformanceBroadcastOptions`) — `Enabled` (default
  `true`) and the three broadcast intervals above.
- `PerformanceAlerts` (`PerformanceAlertOptions`) — `CheckIntervalSeconds` (30),
  `ConsecutiveBreachesRequired` (2), `ConsecutiveNormalRequired` (3),
  `IncidentRetentionDays` (90).
- `HistoricalMetrics` (`HistoricalMetricsOptions`) — sample interval (60s),
  retention (30 days), and cleanup interval for the System tab's historical
  charts, collected by `MetricsCollectionService`.

Setting `PerformanceBroadcast:Enabled` to `false` stops the broadcast loops
entirely; since no tab currently listens anyway, this has no visible effect on
the dashboard today beyond reducing background CPU/SignalR traffic.

## Troubleshooting

**A tab shows "Failed to Load Content" / a 500 from `?handler=Partial`.**
Check server logs for the matching aggregator method
(`BuildHealthMetricsAsync`, `BuildSystemHealth`, etc.) — `IndexModel` and
`PerformanceTabsController` both log the failing tab and swallow the exception
into a rendered error state, so the stack trace is in the log, not the response.
Use the tab's "Retry" button, which calls `PerformanceTabs.retryCurrentTab()`
and forces a fresh fetch (bypassing the 5-minute cache).

**Data looks stale after leaving the tab open for a while.**
Expected today — see [Real-time updates](#real-time-updates). Switch tabs or
reload the page to force a refetch; changing the time range also forces one.

**A metric chart or table is empty with no error.**
Most `/api/metrics/*` endpoints return an empty collection rather than an error
when there's no data in the window (new deployment, quiet period) — check the
selected time range (24h/7d/30d) before assuming something is broken.

**403/redirect loop hitting an old bookmarked URL.**
Confirm the request actually reaches `LegacyRedirectExtensions` — a reverse
proxy or CDN caching the old page's HTML would serve a stale response instead
of the 301. The redirect itself requires `RequireViewer`, same as the
destination, so a 403 there means the session isn't authenticated as a viewer.

## History

This feature shipped in stages under epic #295, then was consolidated:

- Issue #565 — command performance analytics. See
  `docs/lessons-learned/issue-565-command-performance-analytics.md`.
- Issue #566 — Discord API and rate-limit monitoring. See
  `docs/lessons-learned/issue-566-api-rate-limit-monitoring.md`.
- Issue #570 — performance alerts and incidents, including the
  lazy-service-resolution fix for `AlertMonitoringService`'s circular DI. See
  `docs/lessons-learned/issue-570-performance-alerts.md`.
- Issue #573 — original five-page dashboard UI. See
  `docs/lessons-learned/issue-573-performance-dashboard-ui.md`.
- Issue #722 — Phase 0 UI cleanup: converted the five standalone pages into the
  single tabbed `/Admin/Performance` dashboard described above, added the
  legacy redirects, and deleted the old page files and their per-page
  `*-realtime.js` modules. See
  `docs/lessons-learned/issue-722-performance-tab-conversion.md`.

## Related Documentation

- [API Endpoints](api-endpoints.md) — full REST request/response documentation
- [Configuration Guide](configuration-guide.md) — all options classes
- [Alerting System](alerting-system.md) — alert evaluation, thresholds, and the
  Alerts-tab real-time gap in more detail
- [Design System](design-system.md) — UI component specifications and color tokens
- `docs/architecture/service-catalog.md` — every service referenced above
