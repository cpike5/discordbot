# Observability Audit — Findings & Implementation Plan

**Audit date:** 2026-02-23
**Branch:** `worktree-obersvability-audit`
**Scope:** Logging (Serilog), APM/Tracing (Elastic APM + OpenTelemetry), Metrics, Health Checks, Background Services

---

## Executive Summary

The observability stack is **well-architected overall** — structured logging is correctly implemented across 3,000+ call sites with zero string interpolation anti-patterns, OTel metrics use proper cardinality controls, and high-cardinality span names are correctly normalized. However, the audit identified **2 high-severity**, **6 medium-severity**, and **10 low-severity** issues across the three pillars.

---

## Critical / High Severity

### H1. Non-thread-safe `Random` in both samplers

**Files:**
- `src/DiscordBot.Bot/Tracing/PrioritySampler.cs:19`
- `src/DiscordBot.Bot/Extensions/ElasticApmExtensions.cs:19` (`ElasticApmTransactionFilter`)

**Problem:** Both classes use `private readonly Random _random = new()` and are called concurrently (OTel sampler from pipeline threads; APM filter as a singleton). `Random` is not thread-safe — concurrent calls can corrupt the PRNG state, causing `NextDouble()` to return 0.0 repeatedly. This leads to biased sampling decisions under load (either sampling everything or nothing).

**Fix:** Replace `_random.NextDouble()` with `Random.Shared.NextDouble()` (thread-safe, available since .NET 6). Remove the `_random` field entirely.

**Impact:** Sampling correctness in production.

---

### H2. Production OTel sampling configuration is silently ignored

**File:** `appsettings.Production.json:53`

**Problem:** The Production config has:
```json
"OpenTelemetry": {
  "Tracing": {
    "SamplingRatio": 0.1
  }
}
```
But `AddOpenTelemetryTracing()` binds to `OpenTelemetry:Tracing:Sampling` (the `SamplingOptions` class). The key `SamplingRatio` at the wrong nesting level is never read. The sampler falls back to code defaults (10% via `PostConfigure`), which happens to match the intended value — but only by coincidence.

**Fix:** Move the config to the correct path: `OpenTelemetry:Tracing:Sampling:DefaultRate: 0.1` (or whichever property name `SamplingOptions` expects). Verify the binding matches the class.

**Impact:** Configuration correctness; future changes to sampling rates would silently fail.

---

## Medium Severity

### M1. `BackgroundServiceHealthRegistry` not exposed via ASP.NET Core health checks

**File:** `src/DiscordBot.Bot/Services/BackgroundServiceHealthRegistry.cs`

**Problem:** The registry tracks background service heartbeats with a 5-minute staleness threshold, but `GetOverallStatus()` is never surfaced through an `IHealthCheck` implementation. Kubernetes liveness/readiness probes hitting `/health` will not detect stalled background services.

**Fix:** Create a `BackgroundServiceHealthCheck : IHealthCheck` that calls `GetOverallStatus()` and returns `Degraded`/`Unhealthy` for stale services. Register it in `HealthCheckExtensions.cs`.

---

### M2. Hardcoded Infrastructure ActivitySource name

**File:** `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs:200`

**Problem:** The string literal `"DiscordBot.Infrastructure"` is used instead of `InfrastructureActivitySource.SourceName`. If the constant changes, the OTel pipeline won't pick up Infrastructure spans.

**Fix:** Replace the string literal with the `InfrastructureActivitySource.SourceName` constant reference.

---

### M3. `LogSanitizationOptions.Enabled` is dead configuration

**Files:**
- `src/DiscordBot.Core/Utilities/LogSanitizationOptions.cs`
- `appsettings.json:94-98`, `appsettings.Production.json:48-50`

**Problem:** The `Enabled` flag is configured in appsettings but never consulted by any middleware, enricher, or sink. `LogSanitizer` requires explicit opt-in per call site (only 3 call sites). The flag gives a false sense of security.

**Fix (option A — lightweight):** Remove `LogSanitizationOptions.Enabled` and document that sanitization is call-site explicit. Update the 3 existing call sites to not check the flag.

**Fix (option B — comprehensive):** Implement a Serilog `IDestructuringPolicy` or sink wrapper that applies `LogSanitizer` patterns to all string properties when `Enabled = true`. Register it in the Serilog pipeline.

---

### M4. Unused `Serilog.Sinks.Grafana.Loki` NuGet package

**File:** `src/DiscordBot.Bot/DiscordBot.Bot.csproj:49`

**Problem:** The Loki sink package is referenced but never configured. Adds ~200KB to build output for no benefit.

**Fix:** Remove the `<PackageReference Include="Serilog.Sinks.Grafana.Loki" ... />` line.

---

### M5. `DashboardHub` `discord.user.id` tag contains username, not snowflake

**File:** `src/DiscordBot.Bot/Hubs/DashboardHub.cs` (lines 149, 182, 236, etc.)

**Problem:** `activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name)` stores the ASP.NET Core identity name (a username string), not the Discord snowflake ID. The tag name `discord.user.id` implies a numeric ID, creating a semantic mismatch in trace queries.

**Fix:** Resolve the actual Discord user ID from claims (e.g., `Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value`) or rename the tag to `discord.user.name`.

---

### M6. Inline tag key strings conflict with `TracingConstants`

**Files:** `VoxService.cs`, `BulkPurgeService.cs`, `UserPurgeService.cs`, `AzureTtsService.cs`, `PlaybackService.cs`, `PerformanceMetricsBroadcastService.cs`, `BotHostedService.cs`

**Problem:** ~10 services use raw string literals for span tag keys instead of `TracingConstants.Attributes` constants. Some conflict with canonical names (e.g., `"guild_id"` vs `"discord.guild.id"`), making trace queries unreliable.

**Fix:** Replace inline strings with `TracingConstants.Attributes.*` references. Add missing constants to `TracingConstants` for domain-specific tags (e.g., `Vox.*`, `Purge.*`, `Broadcast.*`).

---

## Low Severity

### L1. `System.Net.Http` namespace has no minimum level override

**Files:** All `appsettings*.json`

**Problem:** `HttpClient` logs at `Information` by default. With frequent Discord REST calls and token refreshes, this can be noisy.

**Fix:** Add `"System.Net.Http": "Warning"` to the `Override` section in `appsettings.json`.

---

### L2. Deprecated `UseAllElasticApm()` API

**File:** `Program.cs:232`

**Problem:** `app.UseAllElasticApm()` is deprecated (`CS0618`). The recommended replacement is `services.AddAllElasticApm()` in DI registration.

**Fix:** Move APM registration from middleware to `IServiceCollection`. Note: verify the Elastic APM NuGet version supports the `AddAllElasticApm` API.

---

### L3. `DiscordTokenService` and `DiscordTokenRefreshService` use unnamed HTTP client

**Files:**
- `src/DiscordBot.Bot/Services/DiscordTokenService.cs:333`
- `src/DiscordBot.Bot/Services/DiscordTokenRefreshService.cs:242`

**Problem:** `_httpClientFactory.CreateClient()` bypasses the named `"Discord"` client and its `DiscordApiTracingHandler`. Token refresh calls to Discord's OAuth2 endpoint are not traced with Discord-specific enrichment.

**Fix:** Use `_httpClientFactory.CreateClient("Discord")` or register a separate named client for OAuth token operations.

---

### L4. `CorrelationIdMiddleware` uses `"none"` string for missing trace IDs

**File:** `src/DiscordBot.Bot/Middleware/CorrelationIdMiddleware.cs:86-87`

**Problem:** When no `Activity` is active, `TraceId` and `SpanId` are set to the literal string `"none"`. This makes it impossible to filter for log entries with actual trace correlation using `TraceId IS NOT NULL` in Seq/Elasticsearch.

**Fix:** Use `string.Empty` or omit the property entirely when no activity is active.

---

### L5. Three background services bypass `MonitoredBackgroundService`

**Files:**
- `src/DiscordBot.Bot/Services/AudioCacheCleanupService.cs` — periodic service, should be monitored
- `src/DiscordBot.Bot/Services/VoxClipLibraryInitializer.cs` — one-shot, acceptable
- `src/DiscordBot.Bot/Extensions/ElasticApmExtensions.cs` (`ElasticApmFilterRegistrationService`) — one-shot, acceptable

**Fix:** Migrate `AudioCacheCleanupService` to extend `MonitoredBackgroundService`. The two one-shot services are acceptable as-is; add a code comment documenting the decision.

---

### L6. Fire-and-forget calls lose trace context

**Files:**
- `src/DiscordBot.Bot/Services/AlertMonitoringService.cs:293,345`
- `src/DiscordBot.Bot/Services/BotHostedService.cs` (lines 338, 341, 359, 432, 451, 473, 527, 530, 533, 550, 554, 557)
- `src/DiscordBot.Bot/Services/AudioService.cs:113,164`

**Problem:** `_ = SomeAsync()` discards the task and any spans created inside produce orphaned root spans. The parent activity has already ended by the time the fire-and-forget completes.

**Fix:** For high-value paths (`AlertMonitoringService` notifications), capture and link the parent trace context. For low-value broadcast calls, document the decision as intentional.

---

### L7. `LogSanitizer.SanitizeObject` nullable annotation inconsistency

**File:** `src/DiscordBot.Core/Utilities/LogSanitizer.cs:127,132`

**Problem:** `CS8603` warning — `SanitizeString()` returns `string?` but `SanitizeObject` signature expects `string`.

**Fix:** Add a null-coalescing fallback or fix the nullability annotations.

---

### L8. APM label keys inconsistent with OTel attribute keys

**File:** `src/DiscordBot.Bot/Tracing/BotActivitySource.cs:387-393`

**Problem:** APM labels use `"service.name"`, `"execution.cycle"`, `"correlation_id"` while OTel uses `"background.service.name"`, `"background.execution.cycle"`, `"correlation.id"`. Querying the same field requires knowing which system you're in.

**Fix:** Align APM labels to match OTel attribute names from `TracingConstants`.

---

### L9. Repository tracing coverage is minimal

**Problem:** Only 3 of 41 repositories use `InfrastructureActivitySource` instrumentation. The other 38 rely solely on EF Core-level query spans, which don't identify the repository method.

**Fix:** This is a large effort. Defer to a follow-up issue. Consider adding spans only to repositories with complex multi-query operations (analytics aggregation, search, bulk operations).

---

### L10. `DatabaseMetricsCollector` not exported as OTel metrics

**File:** `src/DiscordBot.Bot/Services/DatabaseMetricsCollector.cs`

**Problem:** In-memory query aggregates (counts, latency histograms) are only consumed by the dashboard SignalR pipeline, not exported through the OTel Prometheus endpoint.

**Fix:** Defer to follow-up — would require bridging `DatabaseMetricsCollector` data into OTel `Meter` instruments or relying on the existing EF Core OTel instrumentation for Prometheus.

---

## What's Working Well

- **Zero string interpolation** in 3,000+ log call sites
- **Structured logging** with PascalCase properties throughout
- **Cardinality management** — `ApiMetricsMiddleware` normalizes paths, `BotMetrics` excludes `guild_id`, `NormalizeEndpoint()` strips snowflake IDs
- **Span lifecycle** — correct try/finally/using patterns with exception capture before span end
- **Bootstrap logger** → full Serilog pipeline (two-phase init)
- **APM-log correlation** via `WithElasticApmCorrelationInfo()` + `CorrelationIdMiddleware`
- **5 well-structured OTel Meters** with explicit histogram bucket boundaries
- **`MonitoredBackgroundService`** base class for consistent health tracking
- **`LogSanitizer`** utility with comprehensive regex patterns
- **`SerilogRequestLogging`** correctly suppresses `/health` path noise

---

## Implementation Priority

| Phase | Issues | Effort | Impact |
|-------|--------|--------|--------|
| **Phase 1 — Quick fixes** | H1, H2, M2, M4, L1, L4, L7 | ~1-2 hours | Thread safety, config correctness, dead code removal |
| **Phase 2 — Health & monitoring** | M1, L5 | ~2-3 hours | Background service visibility in health checks |
| **Phase 3 — Tracing consistency** | M5, M6, L3, L8 | ~3-4 hours | Tag name consistency across tracing backends |
| **Phase 4 — Sanitization & hardening** | M3, L2, L6 | ~3-4 hours | Log sanitization wiring, APM API migration |
| **Phase 5 — Follow-up issues** | L9, L10 | Deferred | Repository tracing coverage, DB metrics OTel bridge |
