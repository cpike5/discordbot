# Lessons Learned: Issue #565 - Command Performance Analytics

**Date:** 2026-01-01
**Issue:** [#565 - Feature: Command Performance Analytics](https://github.com/cpike5/discordbot/issues/565)
**PR:** [#586](https://github.com/cpike5/discordbot/pull/586)

---

## Summary

Implementation of the Command Performance Analytics dashboard page revealed several issues with the existing infrastructure and highlighted important considerations for building data-driven dashboard pages.

---

## Issues Encountered

### 1. GetAggregatesAsync Ignored the `hours` Parameter

**Symptom:** Users selecting 7d or 30d time ranges saw the same data as the 24h view.

**Root Cause:** The `CommandPerformanceAggregator.GetAggregatesAsync(int hours)` method completely ignored the `hours` parameter. It always returned cached data that was calculated for a fixed 24-hour window.

```csharp
// BEFORE (broken)
public async Task<IReadOnlyList<CommandPerformanceAggregateDto>> GetAggregatesAsync(int hours = 24)
{
    await EnsureCacheValidAsync();
    // ... return cached data regardless of 'hours' parameter
}
```

**Fix:** Modified the method to use the cache only for 24-hour requests, and query the database directly for other time windows:

```csharp
// AFTER (fixed)
public async Task<IReadOnlyList<CommandPerformanceAggregateDto>> GetAggregatesAsync(int hours = 24)
{
    if (hours == 24)
    {
        await EnsureCacheValidAsync();
        // ... return cached data
    }

    // For other time windows, query directly from the database
    using var scope = _serviceScopeFactory.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();
    // ... query with correct time window
}
```

**Lesson:** When consuming existing service methods, verify that parameters are actually being used, not just accepted. The method signature suggested time-range support that didn't exist.

---

### 2. Cache Timing Caused "No Data Available" on First Load

**Symptom:** User ran Discord commands, then immediately checked the Command Performance page and saw "No command data available" despite commands being logged.

**Root Cause:** The `CommandPerformanceAggregator` background service:
1. Waits 30 seconds before first execution (`await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken)`)
2. Has a 5-minute cache TTL before auto-refresh

When a user accesses the page before the first cache refresh completes, they see empty data even though the database has records.

**Evidence from logs:**
```
22:51:51 [DBG] Command Performance page accessed... TotalCommands=0
22:54:09 [INF] Command performance cache refreshed: 3 commands
```

The page was accessed 2+ minutes before the cache was populated.

**Lesson:** Background service caching creates a "cold start" problem. Consider:
- Triggering an immediate cache refresh on first API request
- Showing a "data loading" state instead of "no data" when cache is empty but service is initializing
- Reducing initial delay for services that provide user-facing data

---

### 3. Debugging Data Flow Required Log Analysis

**Symptom:** Page showed empty state, but it wasn't clear if commands were being logged, if the cache was working, or if the page had a bug.

**How I diagnosed it:**
1. Checked logs for `CommandPerformanceAggregator` - confirmed cache was refreshing with data
2. Checked logs for `CommandsModel` page access - confirmed page was loading before cache refresh
3. Cross-referenced timestamps to identify the timing gap

**Lesson:** Good logging at key points (service startup, cache refresh, page load with metrics) made debugging possible. The existing debug-level logging in the aggregator and repository was invaluable:
```
[DBG] Refreshing command performance cache
[INF] Command performance cache refreshed: 3 commands, cache valid until...
[DBG] Command Performance page accessed by user..., hours=24
[DBG] Command Performance ViewModel loaded: TotalCommands=0...
```

---

## Architectural Observations

### Caching Strategy Trade-offs

The aggregator uses a background service pattern with periodic cache refresh:
- **Pro:** Reduces database load, consistent response times
- **Con:** Stale data, cold start problem, time-range parameter complexity

For time-series data where users may want different windows (24h, 7d, 30d), consider:
- Per-window caching (cache key includes time range)
- Shorter TTL for smaller windows
- On-demand refresh capability

### Page Model vs API Consistency

The page model (`CommandsModel`) called the aggregator service directly rather than going through the API controller. This is fine but means:
- The page and API could potentially show different data if implementations diverge
- API caching/rate-limiting doesn't apply to page loads
- Harder to test the full stack

The API endpoints in `PerformanceMetricsController` use the same aggregator, so in this case behavior is consistent.

---

## Testing Gaps

The unit tests I wrote verified the page model and view model logic, but didn't catch the aggregator bug because:
1. Tests mocked `ICommandPerformanceAggregator` - they didn't test the real implementation
2. No integration tests verified end-to-end data flow from database to page

**Recommendation:** Add integration tests that:
- Insert test command logs into the database
- Call the real aggregator (not mocked)
- Verify correct data appears for different time windows

---

## Checklist for Future Dashboard Pages

Based on this experience, when building data-driven dashboard pages:

- [ ] Verify service methods actually use all their parameters
- [ ] Test with empty data, fresh data, and stale data scenarios
- [ ] Check for cold-start issues with cached/background services
- [ ] Ensure logging exists at key points (cache refresh, page load with counts)
- [ ] Consider what happens if user accesses page before services initialize
- [ ] Test all time range options, not just the default
- [ ] Add integration tests for data flow, not just unit tests for components

---

## Files Changed

| File | Change |
|------|--------|
| `src/DiscordBot.Bot/Services/CommandPerformanceAggregator.cs` | Fixed `GetAggregatesAsync` to respect `hours` parameter |

---

## Related Documentation

- [Bot Performance Dashboard](../articles/bot-performance-dashboard.md)
- [Issue #565](https://github.com/cpike5/discordbot/issues/565)
