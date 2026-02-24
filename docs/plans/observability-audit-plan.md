# Observability Audit — Implementation Plan

**Date:** 2026-02-23
**Branch:** blazor-migration
**Scope:** Logging, tracing, metrics, and correlation infrastructure

---

## Audit Summary

The observability stack is mature and well-integrated: Serilog with structured logging, Elastic APM + OpenTelemetry dual-write tracing, five custom metric meters with Prometheus export, SignalR-based real-time dashboards, priority-based sampling, and multi-layer log-trace correlation. The audit found no critical security issues but identified several gaps worth addressing.

---

## Findings & Remediation Plan

### HIGH Priority

#### 1. Unregistered `DiscordBot.Vox` ActivitySource — all Vox spans silently dropped

**Files:**
- `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs` (lines 199-200)
- `src/DiscordBot.Bot/Services/VoxService.cs` (line 29)
- `src/DiscordBot.Infrastructure/Services/Vox/VoxConcatenationService.cs` (line 18)

**Problem:** Both `VoxService` and `VoxConcatenationService` create an `ActivitySource("DiscordBot.Vox")`, but `AddOpenTelemetryTracing()` only registers `DiscordBot.Bot` and `DiscordBot.Infrastructure` via `tracing.AddSource(...)`. The OTel SDK silently drops all activities from unregistered sources — `StartActivity()` returns `null`.

**Fix:** Add `tracing.AddSource("DiscordBot.Vox")` in `OpenTelemetryExtensions.cs` alongside the other two sources.

**Validation:** After fix, trigger a `/vox` command and verify spans appear in the OTLP exporter or console exporter output.

---

### MEDIUM Priority

#### 2. Repository spans don't record exceptions

**Files:**
- `src/DiscordBot.Infrastructure/Data/Repositories/UserActivityEventRepository.cs` (all 5 methods)
- Likely pattern repeated across other repositories

**Problem:** Repository methods use `using var activity = InfrastructureActivitySource.StartRepositoryActivity(...)` but have no try/catch to call `InfrastructureActivitySource.RecordException(activity, ex, durationMs)` on failure. When EF Core throws, the span ends with `Unset` status instead of `Error`. The `RecordException` helper exists but is unused in these repositories.

**Fix:** Audit all repository files for the pattern. Where activities are started, wrap the body in try/catch that calls `RecordException()` and re-throws. Consider a helper method or wrapper to reduce boilerplate.

**Scope check:** Grep for `StartRepositoryActivity` across all repository files to determine full extent.

---

#### 3. `PlaybackLoopAsync` fire-and-forget orphans trace context

**File:** `src/DiscordBot.Bot/Services/PlaybackService.cs` (line 146)

**Problem:** `_ = PlaybackLoopAsync(guildId)` is launched fire-and-forget. The parent interaction's activity/transaction is disposed in the `finally` block, so any spans started inside `PlaybackLoopAsync` become orphaned root spans with no connection to the triggering command.

**Fix:** Capture the current `Activity.Current` trace context before the fire-and-forget, and start `PlaybackLoopAsync` as a linked (not child) root activity referencing the original trace ID. Alternatively, use `BotActivitySource.StartBackgroundServiceActivity("PlaybackLoop", ...)` at the top of the loop to create a clean independent trace.

---

#### 4. Email addresses logged at `Information` level (GDPR concern)

**File:** `src/DiscordBot.Bot/Services/UserManagementService.cs` (lines 177, 192, 224, 265)

**Problem:** User email addresses are logged at `Information`/`Warning` level and flow to all sinks (file, Seq, Elasticsearch). Example: `"Attempt to create user with existing email: {Email}"`.

**Fix:** Either:
- Replace email with a hash/masked value: `{Email}` → `LogSanitizer.MaskEmail(email)`
- Or add a Serilog destructuring policy that automatically masks email-shaped values

---

#### 5. `LogSanitizer` not wired into the Serilog pipeline

**Files:**
- `src/DiscordBot.Core/Utilities/LogSanitizer.cs`
- Only 2 call sites: `CommandExecutionLogger.cs` (line 80-81), `GuildService.cs` (line 294)

**Problem:** `LogSanitizer` exists with robust regex patterns for tokens, cards, emails, phones, and key-value secrets — but it's only called manually at 2 locations. Any new log call capturing user input would need to remember to sanitize manually.

**Fix:** Create a Serilog `IDestructuringPolicy` or sink wrapper that runs `LogSanitizer.SanitizeString()` on string property values automatically. Register it in the Serilog pipeline in `Program.cs`.

**Trade-off:** This adds per-log-event regex overhead. Consider applying only to `Warning`+ levels, or only to specific property names.

---

### LOW Priority

#### 6. Missing minimum level overrides for noisy namespaces

**File:** `appsettings.json` (base)

**Problem:** These namespaces are not overridden in the base config and rely on environment overlays:
- `System.Net.Http` — verbose HttpClient lifecycle logs
- `Microsoft.AspNetCore.SignalR` — chatty hub logs
- `Microsoft.IdentityModel` — token validation noise

**Fix:** Add `Warning` overrides in base `appsettings.json`:
```json
"System.Net.Http": "Warning",
"Microsoft.AspNetCore.SignalR": "Warning",
"Microsoft.IdentityModel": "Warning"
```

---

#### 7. `VoxService` cancelled playback doesn't set activity status

**File:** `src/DiscordBot.Bot/Services/VoxService.cs` (lines 257-267)

**Problem:** `OperationCanceledException` catch block returns without setting activity status. The span ends with `Unset` status, indistinguishable from success in trace UIs.

**Fix:** Add `activity?.SetStatus(ActivityStatusCode.Error, "Cancelled")` or use a dedicated `Cancelled` semantic convention in the catch block.

---

#### 8. `LogQueryParameters` defaults to `true` in base config

**File:** `appsettings.json` (line 91), `DatabaseSettings.cs` (line 29)

**Problem:** Query parameter logging defaults to enabled. Production and Staging overlays correctly set it to `false`, but a developer running without an environment overlay gets parameter logging. Parameters are sanitized by `QueryPerformanceInterceptor`, but this is defense-in-depth.

**Fix:** Change the default in `DatabaseSettings.cs` from `true` to `false`. Developers who want parameter logging can opt in via `appsettings.Development.json` or user secrets.

---

#### 9. `GuildMemberService` leftover debug logging

**File:** `src/DiscordBot.Bot/Services/GuildMemberService.cs` (line 151)

**Problem:** `"GetMemberAsync: Found in DB - Username={Username}"` at `Information` level looks like leftover investigation logging. The informal message format is inconsistent with the rest of the codebase, and it logs the Discord username.

**Fix:** Either remove the log line or downgrade to `Debug` level with a standard message template.

---

### INFORMATIONAL (No action needed)

| Finding | Status |
|---------|--------|
| No string interpolation in log messages | Clean — all templates use `{Property}` correctly |
| `Console.WriteLine` only in `MigrateDataCommand.cs` | Acceptable — CLI tool without `ILogger` |
| `CaptureBody: "off"` in APM config | Correct |
| `CaptureHeaders: true` with auto-redaction | Safe — Elastic APM redacts `Authorization`/`Cookie` |
| `ExternalLogin` logs boolean presence, not tokens | Safe |
| `Agent.Tracer` static usage (not `ITracer` DI) | Standard Elastic APM .NET pattern, untestable but functional |
| Dual priority sampling (OTel + APM) in sync | Well-designed |
| Five-layer log-trace correlation | Comprehensive |
| Health check request logging demoted to `Verbose` | Correct |
| SignalR broadcast skips when no subscribers | Efficient |

---

## Architecture Observations

**Strengths:**
- Dual-write tracing (OTel + Elastic APM) with synchronized priority sampling
- Five-layer correlation: APM enricher → CorrelationIdMiddleware → OTel span enrichment → APM labels → LogContext
- Custom histogram bucket boundaries tuned to domain-specific latency profiles
- `BotActivitySource` centralizes all span creation with typed factory methods
- `PrioritySampler` with four tiers prevents trace storage explosion while preserving important events
- `ApiMetricsMiddleware` normalizes endpoints via compiled regex to prevent cardinality issues
- `DiscordApiTracingHandler` normalizes Discord API paths (snowflakes → `{id}`)
- Health checks use `Degraded` (not `Unhealthy`) for reconnecting gateway to avoid container restarts

**Areas for future consideration:**
- Consider moving from `Agent.Tracer` static to `ITracer` DI for testability
- Consider a centralized "observability test" integration test that verifies spans are collected for key code paths
- The `MigrateDataCommand` console output could be lost in CI — consider adding a simple file logger for non-interactive migration runs
