# OpenTelemetry Metrics Collection

**Version:** 1.0
**Last Updated:** 2025-12-24
**Target Framework:** .NET 8 with OpenTelemetry SDK

---

## Overview

The Discord Bot Management System implements comprehensive metrics collection using OpenTelemetry and exports metrics in Prometheus format. This provides observability into bot performance, command execution, API usage, and system health.

Metrics work alongside [distributed tracing](tracing.md) to provide the complete observability picture: metrics tell you **what** is happening (rates, durations, counts), while tracing tells you **how** it's happening (request flows, dependencies, bottlenecks).

### Key Features

- **Command Metrics**: Track Discord command execution rates, durations, and success/failure status
- **API Metrics**: Monitor HTTP request rates, response times, and error rates
- **Component Metrics**: Measure interactive component (button, select menu, modal) performance
- **System Metrics**: Observe runtime metrics (GC, thread pool, memory)
- **Rate Limit Tracking**: Count and analyze rate limit violations
- **Guild & User Metrics**: Monitor active guilds and user counts

### Architecture

The system uses `System.Diagnostics.Metrics` for metric definitions, which is the .NET 8 native metrics API. OpenTelemetry collects these metrics and exports them via Prometheus exporter at the `/metrics` endpoint.

```
┌─────────────────┐
│  Bot Commands   │──┐
├─────────────────┤  │
│ API Endpoints   │──┼──> BotMetrics / ApiMetrics
├─────────────────┤  │   (System.Diagnostics.Metrics)
│ Rate Limiters   │──┘
└─────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  OpenTelemetry MeterProvider│
├─────────────────────────────┤
│ - Custom meters             │
│ - ASP.NET Core metrics      │
│ - HTTP client metrics       │
│ - Runtime metrics           │
└─────────────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│   Prometheus Exporter       │
│   /metrics endpoint         │
└─────────────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Prometheus (scraper)       │
│  Grafana (visualization)    │
└─────────────────────────────┘
```

---

## Available Metrics

### Bot Metrics (Discord Commands)

**Meter Name:** `DiscordBot.Bot`

All custom bot metrics use the `discordbot.` prefix for namespacing in Prometheus.

#### discordbot.command.count

**Type:** Counter
**Unit:** `{commands}`
**Description:** Total number of Discord slash commands executed

**Labels:**
- `command` - Command name (e.g., "ping", "status", "verify")
- `status` - Execution status: "success" or "failure"

**Example Query (PromQL):**
```promql
# Total commands per minute by status
sum(rate(discordbot_command_count[1m])) by (status)

# Command success rate (last 5 minutes)
sum(rate(discordbot_command_count{status="success"}[5m]))
/
sum(rate(discordbot_command_count[5m])) * 100
```

**Usage:** Track command execution trends, identify failing commands, measure overall bot activity.

---

#### discordbot.command.duration

**Type:** Histogram
**Unit:** `ms` (milliseconds)
**Description:** Duration of command execution from invocation to completion

**Labels:**
- `command` - Command name
- `status` - Execution status: "success" or "failure"

**Histogram Buckets:** `5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000` (milliseconds)

**Example Query (PromQL):**
```promql
# p95 latency by command
histogram_quantile(0.95,
  sum(rate(discordbot_command_duration_bucket[5m])) by (le, command)
)

# p50 (median) latency
histogram_quantile(0.50,
  sum(rate(discordbot_command_duration_bucket[5m])) by (le)
)

# Average latency by command
sum(rate(discordbot_command_duration_sum[5m])) by (command)
/
sum(rate(discordbot_command_duration_count[5m])) by (command)
```

**Usage:** Identify slow commands, detect performance regressions, set SLA targets.

---

#### discordbot.command.active

**Type:** UpDownCounter (Gauge)
**Unit:** `{commands}`
**Description:** Number of currently executing commands (concurrent command count)

**Labels:**
- `command` - Command name

**Example Query (PromQL):**
```promql
# Current active commands by type
sum(discordbot_command_active) by (command)

# Peak concurrent commands (last hour)
max_over_time(sum(discordbot_command_active)[1h])
```

**Usage:** Monitor command concurrency, detect stuck commands, capacity planning.

---

#### discordbot.guilds.active

**Type:** ObservableGauge
**Unit:** `{guilds}`
**Description:** Number of guilds (servers) the bot is currently connected to

**Labels:** None

**Update Frequency:** Every 30 seconds (via `MetricsUpdateService`)

**Example Query (PromQL):**
```promql
# Current guild count
discordbot_guilds_active

# Guild count growth over time
delta(discordbot_guilds_active[1d])
```

**Usage:** Track bot adoption, detect unexpected disconnections, capacity planning.

---

#### discordbot.users.unique

**Type:** ObservableGauge
**Unit:** `{users}`
**Description:** Estimated unique users across all guilds (sum of guild member counts)

**Labels:** None

**Update Frequency:** Every 30 seconds (via `MetricsUpdateService`)

**Notes:**
- This is an estimate based on guild member counts
- May include duplicate users across guilds
- For accurate unique user count, implement a separate tracking mechanism

**Example Query (PromQL):**
```promql
# Current estimated user count
discordbot_users_unique

# User growth rate (per hour)
rate(discordbot_users_unique[1h]) * 3600
```

**Usage:** Estimate user reach, track user growth trends.

---

#### discordbot.ratelimit.violations

**Type:** Counter
**Unit:** `{violations}`
**Description:** Number of rate limit violations detected by `RateLimitAttribute`

**Labels:**
- `command` - Command that triggered the rate limit
- `target` - Rate limit scope: "user", "guild", or "global"

**Example Query (PromQL):**
```promql
# Rate limit violations per minute
sum(rate(discordbot_ratelimit_violations[1m])) by (command, target)

# Most rate-limited commands
topk(5, sum(rate(discordbot_ratelimit_violations[5m])) by (command))
```

**Usage:** Identify abuse patterns, tune rate limit thresholds, detect bots or spam.

---

#### discordbot.component.count

**Type:** Counter
**Unit:** `{interactions}`
**Description:** Total number of interactive component interactions (buttons, select menus, modals)

**Labels:**
- `component_type` - Type of component: "button", "select_menu", or "modal"
- `status` - Execution status: "success" or "failure"

**Example Query (PromQL):**
```promql
# Component interactions per minute by type
sum(rate(discordbot_component_count[1m])) by (component_type)

# Component success rate
sum(rate(discordbot_component_count{status="success"}[5m]))
/
sum(rate(discordbot_component_count[5m])) * 100
```

**Usage:** Track user engagement with interactive components, identify failing component handlers.

---

#### discordbot.component.duration

**Type:** Histogram
**Unit:** `ms` (milliseconds)
**Description:** Duration of component interaction handling

**Labels:**
- `component_type` - Type of component: "button", "select_menu", or "modal"
- `status` - Execution status: "success" or "failure"

**Histogram Buckets:** `5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000` (milliseconds)

**Example Query (PromQL):**
```promql
# p95 latency by component type
histogram_quantile(0.95,
  sum(rate(discordbot_component_duration_bucket[5m])) by (le, component_type)
)
```

**Usage:** Measure component responsiveness, optimize slow component handlers.

---

### API Metrics (HTTP Endpoints)

**Meter Name:** `DiscordBot.Api`

Custom API metrics supplement the built-in ASP.NET Core metrics with bot-specific measurements.

#### discordbot.api.request.count

**Type:** Counter
**Unit:** `{requests}`
**Description:** Total number of HTTP requests to API endpoints

**Labels:**
- `endpoint` - Normalized endpoint path (e.g., "/api/guilds/{id}")
- `method` - HTTP method (GET, POST, PUT, DELETE)
- `status_code` - HTTP status code (200, 404, 500, etc.)

**Example Query (PromQL):**
```promql
# Request rate by endpoint
sum(rate(discordbot_api_request_count[5m])) by (endpoint)

# Error rate (5xx responses)
sum(rate(discordbot_api_request_count{status_code=~"5.."}[5m]))
/
sum(rate(discordbot_api_request_count[5m])) * 100

# Requests by HTTP method
sum(rate(discordbot_api_request_count[5m])) by (method)
```

**Usage:** Monitor API traffic, identify hot endpoints, detect errors and anomalies.

---

#### discordbot.api.request.duration

**Type:** Histogram
**Unit:** `ms` (milliseconds)
**Description:** Duration of API request handling from receipt to response

**Labels:**
- `endpoint` - Normalized endpoint path
- `method` - HTTP method
- `status_code` - HTTP status code

**Histogram Buckets:** `1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500` (milliseconds)

**Example Query (PromQL):**
```promql
# p99 latency by endpoint
histogram_quantile(0.99,
  sum(rate(discordbot_api_request_duration_bucket[5m])) by (le, endpoint)
)

# Average response time
sum(rate(discordbot_api_request_duration_sum[5m]))
/
sum(rate(discordbot_api_request_duration_count[5m]))
```

**Usage:** Set API SLAs, identify slow endpoints, detect performance degradation.

---

#### discordbot.api.request.active

**Type:** UpDownCounter (Gauge)
**Unit:** `{requests}`
**Description:** Number of currently active API requests (concurrent request count)

**Labels:** None

**Example Query (PromQL):**
```promql
# Current concurrent requests
discordbot_api_request_active

# Peak concurrency (last hour)
max_over_time(discordbot_api_request_active[1h])
```

**Usage:** Monitor API load, detect traffic spikes, capacity planning.

---

### Built-In OpenTelemetry Metrics

In addition to custom metrics, OpenTelemetry automatically collects these standard metrics:

#### ASP.NET Core Instrumentation

| Metric Name | Type | Description |
|-------------|------|-------------|
| `http.server.request.duration` | Histogram | HTTP request duration |
| `http.server.active_requests` | UpDownCounter | Active HTTP requests |
| `http.server.request.body.size` | Histogram | Request body size |
| `http.server.response.body.size` | Histogram | Response body size |

**Labels:** `http.request.method`, `http.response.status_code`, `http.route`, `network.protocol.name`

#### HTTP Client Instrumentation

| Metric Name | Type | Description |
|-------------|------|-------------|
| `http.client.request.duration` | Histogram | Outgoing HTTP request duration |
| `http.client.request.body.size` | Histogram | Outgoing request body size |
| `http.client.response.body.size` | Histogram | Incoming response body size |

**Labels:** `http.request.method`, `http.response.status_code`, `server.address`, `server.port`

#### Runtime Instrumentation

| Metric Name | Type | Description |
|-------------|------|-------------|
| `process.runtime.dotnet.gc.collections.count` | Counter | GC collection count by generation |
| `process.runtime.dotnet.gc.heap.size` | UpDownCounter | GC heap size by generation |
| `process.runtime.dotnet.gc.allocations.size` | Counter | Allocated bytes |
| `process.runtime.dotnet.jit.il_compiled.size` | Counter | IL bytes compiled |
| `process.runtime.dotnet.jit.methods_compiled.count` | Counter | Methods compiled |
| `process.runtime.dotnet.monitor.lock_contentions.count` | Counter | Monitor lock contentions |
| `process.runtime.dotnet.thread_pool.threads.count` | UpDownCounter | Thread pool thread count |
| `process.runtime.dotnet.thread_pool.completed_items.count` | Counter | Thread pool work items |
| `process.runtime.dotnet.thread_pool.queue.length` | UpDownCounter | Thread pool queue length |
| `process.runtime.dotnet.timer.count` | UpDownCounter | Active timer count |
| `process.runtime.dotnet.assemblies.count` | UpDownCounter | Loaded assembly count |

**Usage:** Monitor runtime health, detect memory leaks, tune GC settings, identify threading issues.

---

## Prometheus Setup

### Scrape Configuration

To collect metrics from the bot, configure Prometheus to scrape the `/metrics` endpoint.

**prometheus.yml:**

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'discordbot'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5001']
    scheme: https
    tls_config:
      insecure_skip_verify: true  # For development with self-signed certs
    metrics_path: '/metrics'
```

**Production Configuration:**

For production deployments:

```yaml
scrape_configs:
  - job_name: 'discordbot'
    scrape_interval: 15s
    static_configs:
      - targets: ['discordbot.yourdomain.com:443']
    scheme: https
    tls_config:
      # Use proper TLS verification
      ca_file: /etc/prometheus/certs/ca.crt
    metrics_path: '/metrics'
    # Optional: Add basic auth if metrics endpoint is protected
    # basic_auth:
    #   username: 'prometheus'
    #   password_file: /etc/prometheus/secrets/metrics_password
```

### Docker Compose Example

For local development with Docker:

**docker-compose.yml:**

```yaml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
    networks:
      - monitoring

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning
    depends_on:
      - prometheus
    networks:
      - monitoring

volumes:
  prometheus-data:
  grafana-data:

networks:
  monitoring:
    driver: bridge
```

### Testing Prometheus Connection

After configuring Prometheus:

1. **Start Prometheus:**
   ```bash
   # Using Docker
   docker-compose up -d prometheus

   # Or standalone
   prometheus --config.file=prometheus.yml
   ```

2. **Access Prometheus UI:**
   - Navigate to `http://localhost:9090`
   - Go to Status → Targets
   - Verify `discordbot` target is UP

3. **Query Metrics:**
   - Go to Graph tab
   - Enter query: `discordbot_command_count`
   - Click "Execute"
   - Verify data appears

4. **Check Metrics Endpoint Directly:**
   ```bash
   curl https://localhost:5001/metrics
   ```

   You should see output like:
   ```
   # HELP discordbot_command_count Total number of Discord commands executed
   # TYPE discordbot_command_count counter
   discordbot_command_count{command="ping",status="success"} 42
   ```

---

## Grafana Dashboard

### Dashboard Import

A sample Grafana dashboard is provided at `docs/grafana/discordbot-dashboard.json`.

**To import:**

1. Open Grafana (default: `http://localhost:3000`)
2. Log in (default: admin/admin)
3. Navigate to Dashboards → Import
4. Upload `discordbot-dashboard.json`
5. Select Prometheus data source
6. Click "Import"

### Dashboard Panels

The sample dashboard includes:

| Panel | Query | Description |
|-------|-------|-------------|
| **Command Success Rate** | `sum(rate(discordbot_command_count{status="success"}[5m])) / sum(rate(discordbot_command_count[5m])) * 100` | Percentage of successful commands |
| **Command Rate** | `sum(rate(discordbot_command_count[5m])) by (command)` | Commands per second by type |
| **Command Latency (p95)** | `histogram_quantile(0.95, sum(rate(discordbot_command_duration_bucket[5m])) by (le, command))` | 95th percentile latency |
| **Active Guilds** | `discordbot_guilds_active` | Current guild count over time |
| **Rate Limit Violations** | `sum(rate(discordbot_ratelimit_violations[1m])) by (command)` | Rate limits per minute |
| **API Request Rate** | `sum(rate(discordbot_api_request_count[5m])) by (endpoint)` | API requests per second |
| **API Error Rate** | `sum(rate(discordbot_api_request_count{status_code=~"5.."}[5m])) / sum(rate(discordbot_api_request_count[5m])) * 100` | Percentage of 5xx errors |
| **Active Requests** | `discordbot_api_request_active` | Concurrent API requests |
| **GC Pause Time** | `rate(process_runtime_dotnet_gc_collections_count[5m]) * 1000` | GC collections per second |
| **Thread Pool Queue** | `process_runtime_dotnet_thread_pool_queue_length` | Thread pool queue depth |

### Example Dashboard Queries

#### Command Performance Dashboard

```promql
# Total command throughput (commands/sec)
sum(rate(discordbot_command_count[5m]))

# Command breakdown by name
sum(rate(discordbot_command_count[5m])) by (command)

# Failed commands
sum(rate(discordbot_command_count{status="failure"}[5m])) by (command)

# Slowest commands (avg latency)
topk(5,
  sum(rate(discordbot_command_duration_sum[5m])) by (command)
  /
  sum(rate(discordbot_command_duration_count[5m])) by (command)
)
```

#### API Health Dashboard

```promql
# Request throughput by status code
sum(rate(discordbot_api_request_count[5m])) by (status_code)

# Slowest endpoints
topk(5,
  histogram_quantile(0.95,
    sum(rate(discordbot_api_request_duration_bucket[5m])) by (le, endpoint)
  )
)

# Error budget (99.9% target)
100 - (
  sum(rate(discordbot_api_request_count{status_code=~"5.."}[7d]))
  /
  sum(rate(discordbot_api_request_count[7d]))
  * 100
)
```

#### System Health Dashboard

```promql
# GC pressure (Gen2 collections)
rate(process_runtime_dotnet_gc_collections_count{generation="gen2"}[5m])

# Memory allocations per second
rate(process_runtime_dotnet_gc_allocations_size[5m])

# Thread pool saturation
process_runtime_dotnet_thread_pool_queue_length > 0
```

---

## Application Configuration

### appsettings.json

OpenTelemetry configuration is defined in `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "ServiceName": "discordbot",
    "ServiceVersion": "0.1.0",
    "Metrics": {
      "Enabled": true,
      "IncludeRuntimeMetrics": true,
      "IncludeHttpMetrics": true
    }
  }
}
```

**Configuration Options:**

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ServiceName` | string | "discordbot" | Service identifier in metrics |
| `ServiceVersion` | string | Assembly version | Service version tag |
| `Metrics.Enabled` | bool | true | Enable/disable metrics collection |
| `Metrics.IncludeRuntimeMetrics` | bool | true | Include .NET runtime metrics |
| `Metrics.IncludeHttpMetrics` | bool | true | Include HTTP client/server metrics |

### Environment-Specific Configuration

For different environments, use `appsettings.{Environment}.json`:

**appsettings.Development.json:**
```json
{
  "OpenTelemetry": {
    "ServiceName": "discordbot-dev"
  }
}
```

**appsettings.Production.json:**
```json
{
  "OpenTelemetry": {
    "ServiceName": "discordbot-prod",
    "ServiceVersion": "1.0.0"
  }
}
```

---

## Implementation Details

### Metric Collection Points

Metrics are recorded at these key integration points:

#### Command Execution

**Location:** `InteractionHandler.cs`

```csharp
// Before command execution
_botMetrics.IncrementActiveCommands(commandName);

// After command execution
stopwatch.Stop();
_botMetrics.RecordCommandExecution(
    commandName,
    result.IsSuccess,
    stopwatch.ElapsedMilliseconds,
    context.Guild?.Id);
_botMetrics.DecrementActiveCommands(commandName);
```

#### Component Interactions

**Location:** `InteractionHandler.cs` (component handler)

```csharp
stopwatch.Stop();
_botMetrics.RecordComponentInteraction(
    componentType: "button",  // or "select_menu", "modal"
    success: result.IsSuccess,
    durationMs: stopwatch.ElapsedMilliseconds);
```

#### Rate Limit Violations

**Location:** `RateLimitAttribute.cs`

```csharp
if (invocations.Count >= _times)
{
    _botMetrics?.RecordRateLimitViolation(
        commandName,
        _target.ToString().ToLowerInvariant());

    return PreconditionResult.FromError($"Rate limit exceeded...");
}
```

#### API Requests

**Location:** `ApiMetricsMiddleware.cs`

```csharp
_metrics.IncrementActiveRequests();
var stopwatch = Stopwatch.StartNew();

try
{
    await _next(context);
}
finally
{
    stopwatch.Stop();
    _metrics.DecrementActiveRequests();

    _metrics.RecordRequest(
        endpoint: NormalizeEndpoint(context.Request.Path),
        method: context.Request.Method,
        statusCode: context.Response.StatusCode,
        durationMs: stopwatch.Elapsed.TotalMilliseconds);
}
```

#### Guild & User Counts

**Location:** `MetricsUpdateService.cs` (background service)

```csharp
// Runs every 30 seconds
var guildCount = _client.Guilds.Count;
_botMetrics.UpdateActiveGuildCount(guildCount);

var estimatedUsers = _client.Guilds.Sum(g => g.MemberCount);
_botMetrics.UpdateUniqueUserCount(estimatedUsers);
```

### Endpoint Normalization

To prevent cardinality explosion from dynamic IDs in URLs, the `ApiMetricsMiddleware` normalizes endpoint paths:

**Normalization Rules:**

| Original Path | Normalized Path |
|--------------|-----------------|
| `/api/guilds/123456789012345678` | `/api/guilds/{id}` |
| `/api/commandlogs/a1b2c3d4-e5f6-7890-abcd-ef1234567890` | `/api/commandlogs/{id}` |
| `/Admin/Users/Edit/abc123` | `/Admin/Users/Edit/{id}` |

**Implementation:**
```csharp
private static string NormalizeEndpoint(string path)
{
    // Replace GUIDs
    var normalized = Regex.Replace(path,
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        "{id}");

    // Replace Discord snowflakes (15-20 digit IDs)
    normalized = Regex.Replace(normalized, @"/\d{15,20}(?=/|$)", "/{id}");

    // Replace numeric IDs
    normalized = Regex.Replace(normalized, @"/\d+(?=/|$)", "/{id}");

    return normalized;
}
```

---

## Best Practices

### Label Cardinality

**DO:**
- Use low-cardinality labels: command names, status codes, HTTP methods
- Normalize dynamic values (user IDs → aggregated counts)
- Limit label value sets to under 100 unique values per label

**DON'T:**
- Add user IDs, usernames, or session IDs as labels
- Include timestamps or correlation IDs as labels
- Use full request paths without normalization

**Example - Good:**
```csharp
// Low cardinality - command names are finite
_commandCounter.Add(1, new TagList {
    { "command", "ping" },
    { "status", "success" }
});
```

**Example - Bad:**
```csharp
// High cardinality - creates metric per user!
_commandCounter.Add(1, new TagList {
    { "user_id", userId.ToString() },  // DON'T DO THIS
    { "correlation_id", correlationId }  // DON'T DO THIS
});
```

### Histogram Bucket Selection

Choose histogram buckets based on expected latency distribution:

**Command Duration Buckets:**
```csharp
// Most commands: 10-250ms
// Slow commands: 500-2500ms
// Timeout threshold: 5000ms
Boundaries = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
```

**API Request Buckets:**
```csharp
// Most requests: 5-50ms
// Slow requests: 100-500ms
Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500]
```

### Performance Considerations

**Metric Collection Overhead:**
- Counters: ~50-100 nanoseconds per increment
- Histograms: ~200-500 nanoseconds per record
- ObservableGauges: Polled on demand (zero overhead when not scraped)

**Memory Usage:**
- Each unique label combination creates a new time series
- Estimate: ~1KB per time series in Prometheus
- With proper label cardinality controls: <50MB for typical bot

**Scraping Impact:**
- Prometheus scrape interval: 15 seconds (default)
- Scrape duration: <100ms for ~500 time series
- Negligible impact on application performance

---

## Troubleshooting

### Metrics Not Appearing

**Problem:** Metrics don't appear in Prometheus or `/metrics` endpoint returns empty.

**Solutions:**

1. **Verify OpenTelemetry is configured:**
   ```csharp
   // In Program.cs
   builder.Services.AddOpenTelemetryMetrics(builder.Configuration);
   app.UsePrometheusMetrics();
   ```

2. **Check metrics endpoint:**
   ```bash
   curl https://localhost:5001/metrics
   ```
   Should return Prometheus text format output.

3. **Verify service registration:**
   ```csharp
   // BotMetrics and ApiMetrics must be registered
   builder.Services.AddSingleton<BotMetrics>();
   builder.Services.AddSingleton<ApiMetrics>();
   ```

4. **Check configuration:**
   ```json
   {
     "OpenTelemetry": {
       "Metrics": {
         "Enabled": true
       }
     }
   }
   ```

### High Cardinality Warnings

**Problem:** Prometheus warns about high cardinality or memory usage spikes.

**Solutions:**

1. **Audit metric labels:**
   ```promql
   # Check cardinality by metric
   count by (__name__) ({job="discordbot"})
   ```

2. **Remove high-cardinality labels:**
   - User IDs → aggregate counts
   - Guild IDs → remove if >1000 guilds
   - Correlation IDs → use tracing instead

3. **Implement label value limits:**
   ```csharp
   // Limit unique values per label
   if (commandNames.Count > 100)
   {
       commandName = "other";
   }
   ```

### Incorrect Histogram Percentiles

**Problem:** Histogram percentiles (p95, p99) seem inaccurate.

**Solutions:**

1. **Review bucket boundaries:**
   ```csharp
   // Ensure buckets cover expected latency range
   Boundaries = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
   ```

2. **Add more granular buckets:**
   ```csharp
   // If most values fall in 10-50ms range, add more buckets there
   Boundaries = [5, 10, 15, 20, 25, 30, 40, 50, 75, 100, ...]
   ```

3. **Verify query syntax:**
   ```promql
   # Correct
   histogram_quantile(0.95, sum(rate(discordbot_command_duration_bucket[5m])) by (le))

   # Incorrect (missing rate)
   histogram_quantile(0.95, sum(discordbot_command_duration_bucket) by (le))
   ```

### Missing Built-In Metrics

**Problem:** ASP.NET Core or runtime metrics don't appear.

**Solutions:**

1. **Verify instrumentation is enabled:**
   ```csharp
   // In OpenTelemetryExtensions.cs
   metrics.AddAspNetCoreInstrumentation();
   metrics.AddHttpClientInstrumentation();
   metrics.AddRuntimeInstrumentation();
   ```

2. **Check NuGet packages:**
   ```xml
   <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.14.0" />
   <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.14.0" />
   <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.14.0" />
   ```

3. **Restart application:**
   Runtime metrics require app restart to register.

---

## Security Considerations

### Metrics Endpoint Protection

The `/metrics` endpoint is publicly accessible by default. Consider these security measures for production:

**Option 1: IP Whitelist**

```csharp
// In Program.cs
app.MapWhen(
    context => context.Request.Path.StartsWithSegments("/metrics"),
    metricsApp =>
    {
        metricsApp.Use(async (context, next) =>
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            var allowedIps = new[] { "127.0.0.1", "10.0.0.0/8" };

            if (!IsIpAllowed(remoteIp, allowedIps))
            {
                context.Response.StatusCode = 403;
                return;
            }

            await next();
        });

        metricsApp.UseOpenTelemetryPrometheusScrapingEndpoint();
    });
```

**Option 2: Basic Authentication**

```csharp
// Add authentication middleware before metrics endpoint
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/metrics"))
    {
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (!IsValidMetricsAuth(authHeader))
        {
            context.Response.StatusCode = 401;
            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Metrics\"";
            return;
        }
    }

    await next();
});
```

**Option 3: Network Isolation**

- Expose `/metrics` on separate port (e.g., 9090)
- Use firewall rules to restrict access
- Only allow Prometheus server IP

### Data Privacy

**Metrics DO NOT contain:**
- User messages or content
- Discord tokens or secrets
- Personal identifiable information (PII)
- User passwords or credentials

**Metrics DO contain:**
- Command names (e.g., "ping", "verify")
- HTTP status codes
- Request counts and durations
- System resource usage

**Note:** Guild IDs and user IDs were intentionally removed from metrics to prevent privacy concerns and cardinality explosion.

---

## Observability: Metrics + Tracing

Metrics and distributed tracing are complementary observability pillars that work together to provide comprehensive system insights.

### When to Use Metrics vs. Tracing

| Scenario | Use Metrics | Use Tracing |
|----------|-------------|-------------|
| "How many commands are executing?" | Yes - `discordbot.command.count` | No |
| "What's the p95 latency of the ping command?" | Yes - `discordbot.command.duration` | No |
| "Why is this specific command slow?" | No | Yes - view span hierarchy |
| "Which database query is causing slowness?" | No | Yes - inspect child spans |
| "What's the error rate over time?" | Yes - `discordbot.command.count{status="failure"}` | No |
| "What caused this specific error?" | No | Yes - exception details in span |

### Correlation Between Metrics and Traces

Both metrics and traces include **correlation IDs**, enabling you to:

1. **Identify anomalies in metrics** (e.g., spike in command duration)
2. **Find representative traces** using Jaeger filters
3. **Debug root cause** with span-level detail
4. **Validate fix** by observing metrics return to baseline

**Example Workflow:**

```promql
# 1. Detect anomaly in Grafana
histogram_quantile(0.95, rate(discordbot_command_duration_bucket{command="verify"}[5m])) > 1000

# 2. Find slow traces in Jaeger
Service: discordbot
Operation: discord.command verify
Min Duration: 1000ms

# 3. Analyze span attributes and timing
# 4. Fix root cause (e.g., missing database index)
# 5. Verify fix in metrics dashboard
```

### Three Pillars of Observability

| Pillar | Purpose | Implementation |
|--------|---------|----------------|
| **Metrics** | Aggregated statistics over time | OpenTelemetry + Prometheus + Grafana |
| **Tracing** | Request-level flow and timing | OpenTelemetry + Jaeger / Application Insights |
| **Logging** | Detailed event context | Serilog with structured logging |

**Integration:** All three share **correlation IDs**, enabling unified request tracking across logs, metrics, and traces.

For distributed tracing implementation details, see [Distributed Tracing Documentation](tracing.md).

---

## Related Documentation

- [Distributed Tracing](tracing.md) - OpenTelemetry tracing with Jaeger (complementary observability pillar)
- [API Endpoints Reference](api-endpoints.md) - REST API documentation including `/metrics` endpoint
- [Authorization Policies](authorization-policies.md) - Role-based access control for admin UI
- [Interactive Components](interactive-components.md) - Discord button and component patterns

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-24 | Initial metrics documentation (Issue #104) |

---

*Last Updated: December 24, 2025*
