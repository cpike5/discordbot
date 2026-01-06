# Lessons Learned: Converting Performance Dashboard Pages to Tab Partials

**Related Issues:** #722, #759, #760, #761, #762, #763, #764, #765
**Implementation Date:** January 2026

## Overview

This document captures the patterns and lessons learned from converting the Performance Dashboard pages into partial views that can be loaded both standalone (direct page navigation) and via AJAX (tab switching within the shell).

## Architecture Pattern

### Partial View Structure

Each tab has a corresponding partial view in `Pages/Admin/Performance/Tabs/`:

```
Pages/Admin/Performance/
├── Index.cshtml              # Performance Overview (main shell when using AJAX tabs)
├── Commands.cshtml           # Standalone Commands page
├── HealthMetrics.cshtml      # Standalone Health page
├── ApiMetrics.cshtml         # Standalone API page
├── SystemHealth.cshtml       # Standalone System page
├── Alerts.cshtml             # Standalone Alerts page
└── Tabs/
    ├── _OverviewTab.cshtml   # Overview tab content
    ├── _CommandsTab.cshtml   # Commands tab content
    ├── _HealthMetricsTab.cshtml  # Health tab content
    ├── _ApiMetricsTab.cshtml # API tab content
    ├── _SystemHealthTab.cshtml   # System tab content
    └── _AlertsTab.cshtml     # Alerts tab content
```

### Key Components of a Tab Partial

Each `_*Tab.cshtml` partial should contain:

1. **Model Directive** - Reference the specific ViewModel, not the PageModel:
   ```razor
   @model DiscordBot.Bot.ViewModels.Pages.CommandPerformanceViewModel
   @using DiscordBot.Bot.ViewModels.Pages
   ```

2. **HTML Content** - All visual content (cards, charts, tables, etc.)
   - No page header
   - No tab navigation
   - No time range selector (owned by shell/main page)

3. **Inline JavaScript** - Chart initialization and tab lifecycle functions:
   ```javascript
   <script>
   (function() {
       'use strict';

       // Chart references
       let myChart;

       // Initialize function - called when tab loads
       async function init() {
           // Get time range from shell or default
           const hours = getSelectedHours();

           // Initialize charts
           await initMyChart(hours);

           // Convert timestamps
           convertTimestampsToLocal();
       }

       // Expose init and destroy functions globally for tab system
       window.initMyTabTab = init;
       window.destroyMyTabTab = function() {
           if (myChart) myChart.destroy();
       };
   })();
   </script>
   ```

### Main Page Pattern (Standalone)

Each main page (`Commands.cshtml`, etc.) should:

1. Keep page metadata (title, breadcrumbs)
2. Keep page header with title and time range selector
3. Keep tab navigation component
4. Render the partial with the ViewModel (NOT the PageModel):
   ```razor
   @await Html.PartialAsync("Tabs/_CommandsTab", Model.ViewModel)
   ```
5. Include Chart.js CDN in Scripts section
6. Handle time range selector changes
7. Call the partial's init function on page load

Example main page structure:
```razor
@page
@model CommandsModel
@{
    ViewData["Title"] = "Command Performance";
    // ... breadcrumbs
}

@section Styles { ... }

<!-- Page Header with time range selector -->
<div class="...">
    <h1>Command Performance</h1>
    <!-- Time Range Buttons -->
</div>

<!-- Tab Navigation -->
@await Html.PartialAsync("Components/_PerformanceTabs", ...)

<!-- Tab Content -->
@await Html.PartialAsync("Tabs/_CommandsTab", Model.ViewModel)

@section Scripts {
    <script src="chart.js"></script>
    <script>
        // Initialize tab and handle time range changes
        document.addEventListener('DOMContentLoaded', async function() {
            if (typeof window.initCommandsTab === 'function') {
                await window.initCommandsTab();
            }
        });
    </script>
}
```

## Naming Conventions

### Canvas Element IDs

To avoid conflicts when multiple tabs might be loaded, prefix canvas IDs with the tab name:

| Tab | Canvas ID Pattern |
|-----|-------------------|
| Overview | `overviewResponseTimeChart`, `overviewThroughputChart` |
| Commands | `commandsResponseTimeChart`, `commandsThroughputChart`, `commandsErrorRateChart` |
| Health | `latencyGauge`, `memoryGauge`, `cpuGauge`, `latencyHistoryChart` |

### JavaScript Function Names

Follow the pattern `init{TabName}Tab` and `destroy{TabName}Tab`:

- `initOverviewTab()` / `destroyOverviewTab()`
- `initCommandsTab()` / `destroyCommandsTab()`
- `initHealthTab()` / `destroyHealthTab()`

### Other Element IDs

Prefix dynamic element IDs to avoid conflicts:
- `commandsThroughputSubtitle` instead of `throughputSubtitle`
- `overviewCpuUsageText` instead of `cpuUsageText`

## Time Range Handling

### Shell Provides Time Range

When loaded via AJAX in the shell (`Index.cshtml`):
- The shell manages the time range selector
- Shell exposes `window.PerformanceShell.getCurrentHours()`
- Partials should check for this first

### Standalone Page Provides Time Range

When loaded as standalone page:
- The main page has its own time range selector
- Time range is passed via query string: `?hours=24`
- Main page initializes `selectedHours` from server-side model

### Getting Hours in Partial

```javascript
function getSelectedHours() {
    // Try shell first (AJAX mode)
    if (window.PerformanceShell) {
        return window.PerformanceShell.getCurrentHours();
    }
    // Fallback to default
    return 24;
}
```

## Chart.js Considerations

### Dark Theme Defaults

Set Chart.js defaults before creating any charts:
```javascript
Chart.defaults.color = '#a8a5a3';
Chart.defaults.borderColor = '#3f4447';
```

### Chart Destruction

Always destroy charts before recreating:
```javascript
if (myChart) myChart.destroy();
myChart = new Chart(ctx, config);
```

### Error Handling

Show user-friendly error messages in chart containers:
```javascript
function showChartError(chartId, message) {
    const container = document.getElementById(chartId)?.parentElement;
    if (container) {
        container.innerHTML = `
            <div class="error-state">
                <div>Failed to load chart</div>
                <div>${message}</div>
            </div>`;
    }
}
```

## Checklist for Converting a Tab

- [ ] Create partial in `Tabs/_*Tab.cshtml`
- [ ] Use correct `@model` directive (ViewModel, not PageModel)
- [ ] Move HTML content from main page (excluding header, tabs, time range)
- [ ] Prefix all canvas IDs with tab name
- [ ] Create `init{Tab}Tab()` function
- [ ] Create `destroy{Tab}Tab()` function
- [ ] Handle time range via `getSelectedHours()`
- [ ] Include `convertTimestampsToLocal()` call
- [ ] Update main page to render partial
- [ ] Test standalone page load
- [ ] Test AJAX tab switching (if shell supports it)
- [ ] Verify charts initialize correctly
- [ ] Verify time range changes work
- [ ] Verify auto-refresh works

## Common Pitfalls

1. **Model Type Mismatch**: The partial uses the ViewModel directly (`CommandPerformanceViewModel`), not the PageModel wrapper (`CommandsModel`). Pass `Model.ViewModel` from the main page.

2. **Canvas ID Conflicts**: If using AJAX tabs, canvas IDs must be unique across all tabs to prevent Chart.js conflicts.

3. **Missing Chart.js**: The partial doesn't include the Chart.js CDN - the main page or shell must include it in the Scripts section.

4. **Time Range State**: Time range is managed at the page/shell level, not in the partial. Don't add time range selectors inside partials.

5. **Auto-Refresh Conflicts**: When switching tabs via AJAX, make sure to call `destroy{Tab}Tab()` to clean up intervals and charts from the previous tab.

6. **selectedHours Not Accessible to Partial**: The partial's JavaScript runs in its own IIFE scope. If the main page has `let selectedHours = @Model.Hours;`, the partial cannot access it. **Solution**: Use `window.selectedHours = @Model.Hours;` in the main page, and have the partial check `window.selectedHours` as a fallback:
   ```javascript
   function getSelectedHours() {
       if (window.PerformanceShell) return window.PerformanceShell.getCurrentHours();
       if (typeof window.selectedHours !== 'undefined') return window.selectedHours;
       return 24; // Default fallback
   }
   ```

7. **CRITICAL: Init Function Must Accept Hours Parameter**: When `performance-tabs.js` loads a tab via AJAX, it passes the current time range (hours) as a parameter to the init function:
   ```javascript
   // In performance-tabs.js line 517:
   initFn(this.state.currentHours);
   ```

   **If your init function ignores this parameter and calls `getSelectedHours()` instead, it will use the default 24 hours!** This causes charts to display empty or incorrect data when the user has selected 7d (168) or 30d (720).

   **Solution**: Always accept and use the hours parameter in your init function:
   ```javascript
   // WRONG - ignores the passed parameter:
   async function init() {
       const hours = getSelectedHours(); // Returns 24 by default!
   }

   // CORRECT - uses passed parameter with fallback:
   async function init(hours) {
       hours = hours || getSelectedHours(); // Uses 720 when passed by performance-tabs.js
   }
   ```

   This is the most common cause of "charts show no data" bugs when using AJAX tabs.

## Files Modified in Issue #761

- `Pages/Admin/Performance/Commands.cshtml` - Updated to use partial
- `docs/lessons-learned/issue-722-performance-tab-conversion.md` - This document (new)

The `Tabs/_CommandsTab.cshtml` partial was already complete from a previous implementation.

## Files Modified in Issue #762

- `Pages/Admin/Performance/ApiMetrics.cshtml` - Updated to use partial, removed inline styles, added button-based time range selector
- `Pages/Admin/Performance/Tabs/_ApiMetricsTab.cshtml` - Added `initApiTab(hours)` and `destroyApiTab()` functions with chart initialization
- `docs/lessons-learned/issue-722-performance-tab-conversion.md` - Updated with #762 notes

**Note:** The API Metrics tab uses `initApiTab` (not `initApiMetricsTab`) to match the performance-tabs.js pattern where the tab ID is `api` and it looks for `init{Capitalize(tabId)}Tab`. Both function names are exposed for compatibility.

### Bug Fix: API Latency Chart Property Names

During testing, the API Latency chart was not displaying data. The issue was that `_ApiTab.cshtml` (used by the AJAX tab system) was using incorrect property names when mapping chart data:

**Bug:**
```javascript
data: data.samples.map(s => s.latencyMs)  // Wrong! Property doesn't exist
```

**Fix:**
```javascript
datasets: [
    {
        label: 'Average Latency',
        data: data.samples.map(s => s.avgLatencyMs),  // Correct property name
        // ...
    },
    {
        label: 'P95 Latency',
        data: data.samples.map(s => s.p95LatencyMs),  // Correct property name
        // ...
    }
]
```

The API endpoint `/api/metrics/api/latency` returns samples with `avgLatencyMs` and `p95LatencyMs` properties (matching the `ApiLatencySampleDto` class), not `latencyMs`.

**Lesson:** Always verify that JavaScript property names match the C# DTO property names after camelCase serialization. C# `AvgLatencyMs` becomes `avgLatencyMs` in JSON.
