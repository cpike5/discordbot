# Lessons Learned: Issue #566 - Discord API & Rate Limit Monitoring

**Date:** 2026-01-01
**Issue:** [#566 - Feature: Discord API & Rate Limit Monitoring](https://github.com/cpike5/discordbot/issues/566)
**PR:** [#587](https://github.com/cpike5/discordbot/pull/587)

---

## Summary

Implementation of the API & Rate Limit Monitoring dashboard leveraged existing infrastructure but required careful attention to navigation consistency across the performance dashboard pages.

---

## Issues Encountered

### 1. Missing Navigation Tab

**Symptom:** After implementation, the API Metrics page was missing the "Alerts" tab that appeared on other performance pages and in the prototype.

**Root Cause:** When implementing the page, the navigation tabs were copied from an existing page that didn't have the Alerts tab yet. The prototype (`docs/prototypes/features/bot-performance/api-rate-limits.html`) included 6 tabs, but only 5 were implemented.

**Fix:** Added the missing Alerts tab to match the prototype and other pages:

```html
<a href="/Admin/Performance/Alerts" class="performance-tab">
    <svg class="tab-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
    </svg>
    Alerts
</a>
```

**Lesson:** When implementing pages with shared navigation, always verify against:
1. The prototype design
2. All sibling pages to ensure consistency
3. The navigation should link to all pages in the section, even if some aren't implemented yet

---

### 2. Circular Buffer Design for Time-Series Data

**Challenge:** Storing latency samples efficiently without unbounded memory growth.

**Solution:** Implemented a circular buffer pattern with 5-minute aggregation buckets:

```csharp
private const int MaxLatencySamples = 288; // 24 hours at 5-minute intervals
private readonly Queue<(DateTime Timestamp, double AvgLatencyMs, double P95LatencyMs)> _latencySamples = new();
```

**Trade-offs:**
- **Pro:** Fixed memory footprint regardless of request volume
- **Pro:** Natural 24-hour sliding window
- **Con:** Loses granularity for very short time periods
- **Con:** 5-minute buckets mean recent data appears "delayed"

**Lesson:** For monitoring dashboards, 5-minute aggregation is usually sufficient. Real-time microsecond precision is rarely needed for operational dashboards.

---

### 3. Thread Safety for Concurrent Metrics Collection

**Challenge:** The `ApiRequestTracker` is a singleton accessed by multiple threads simultaneously.

**Solution:** Used a combination of thread-safe patterns:

```csharp
// Lock for latency sample collection
private readonly object _latencyLock = new();

// Interlocked for simple counters
Interlocked.Increment(ref categoryData.RequestCount);

// ConcurrentDictionary for category-level data
private readonly ConcurrentDictionary<string, CategoryStats> _stats = new();
```

**Lesson:** Use the right synchronization primitive for each case:
- `lock` for complex operations (percentile calculations)
- `Interlocked` for simple atomic operations
- `ConcurrentDictionary` for thread-safe collections with atomic updates

---

### 4. Percentile Calculations

**Implementation:** Added P50, P95, P99 percentile calculations for latency statistics:

```csharp
private static double CalculatePercentile(List<int> sortedData, double percentile)
{
    if (sortedData.Count == 0) return 0;
    if (sortedData.Count == 1) return sortedData[0];

    var index = (percentile / 100.0) * (sortedData.Count - 1);
    var lower = (int)Math.Floor(index);
    var upper = (int)Math.Ceiling(index);
    var weight = index - lower;

    if (upper >= sortedData.Count) return sortedData[^1];
    return sortedData[lower] * (1 - weight) + sortedData[upper] * weight;
}
```

**Note:** Linear interpolation between adjacent values provides smoother percentile estimates than simple index lookup.

---

## Architectural Observations

### Leveraging Existing Infrastructure

The implementation reused the existing `IApiRequestTracker` interface rather than creating a new service. This approach:
- **Pro:** No new service registration needed
- **Pro:** Existing request tracking code didn't need modification
- **Con:** Had to extend the interface, which affects all implementations

### ViewModel Health Status Calculation

Moved health status calculation to the ViewModel rather than the service:

```csharp
public string GetHealthStatus()
{
    if (RateLimitHits > 10 || P95LatencyMs > 500)
        return "critical";
    if (RateLimitHits > 0 || P95LatencyMs > 200)
        return "warning";
    return "healthy";
}
```

This keeps presentation logic in the presentation layer and allows the thresholds to be easily adjusted without changing the service.

---

## Testing Approach

Created 45 unit tests covering:
- Basic request recording
- Latency statistics calculation
- Thread safety with concurrent access
- Edge cases (empty data, single sample, boundary conditions)
- Percentile accuracy

**Key test patterns:**
- `[Theory]` with `[InlineData]` for parameterized latency threshold tests
- Parallel task execution for thread safety tests
- Tolerance-based assertions for floating-point comparisons

---

## Checklist for Navigation Consistency

When adding new pages to an existing section:

- [ ] Check the prototype for the complete navigation structure
- [ ] Verify all sibling pages have the same navigation tabs
- [ ] Include links to pages that will be implemented later (e.g., Alerts)
- [ ] Mark the current page as `active` in the navigation
- [ ] Test that all navigation links work (even if some lead to 404s for now)

---

## Files Changed

| File | Change |
|------|--------|
| `src/DiscordBot.Core/Interfaces/IApiRequestTracker.cs` | Added latency tracking methods |
| `src/DiscordBot.Core/DTOs/PerformanceMetricsDtos.cs` | Added new DTOs for latency data |
| `src/DiscordBot.Bot/Services/ApiRequestTracker.cs` | Implemented latency tracking with circular buffer |
| `src/DiscordBot.Bot/Controllers/PerformanceMetricsController.cs` | Added `/api/metrics/api/latency` endpoint |
| `src/DiscordBot.Bot/ViewModels/Pages/ApiRateLimitsViewModel.cs` | New ViewModel with health status |
| `src/DiscordBot.Bot/Pages/Admin/Performance/ApiMetrics.cshtml` | New Razor page |
| `src/DiscordBot.Bot/Pages/Admin/Performance/ApiMetrics.cshtml.cs` | Page model |
| `src/DiscordBot.Bot/wwwroot/js/api-metrics-chart.js` | Chart.js integration |
| `tests/DiscordBot.Tests/Services/ApiRequestTrackerTests.cs` | 45 unit tests |

---

## Related Documentation

- [Bot Performance Dashboard](../articles/bot-performance-dashboard.md)
- [Issue #566](https://github.com/cpike5/discordbot/issues/566)
- [Prototype](../prototypes/features/bot-performance/api-rate-limits.html)
