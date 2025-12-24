# Issue #104 - OpenTelemetry Metrics Collection

## Implementation Plan

**Document Version:** 1.0
**Date:** 2025-12-24
**Issue Reference:** GitHub Issue #104
**Priority:** P2 - Medium
**Effort:** Large (1-2 days)
**Dependencies:** Issue #100: Correlation ID Middleware (COMPLETED)

---

## 1. Requirement Summary

Implement comprehensive OpenTelemetry metrics collection for the Discord bot application. The system currently has zero visibility into operational metrics. This implementation will add observability for:

- Discord command execution rates and durations
- Command success/failure tracking
- API request rates and response times
- Active guild and user counts
- Rate limit violation tracking
- System runtime metrics (GC, thread pool, etc.)

Metrics will be exported in Prometheus format at the `/metrics` endpoint for integration with monitoring tools like Grafana.

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Relevance |
|-----------|----------|-----------|
| `CorrelationIdMiddleware` | `src/DiscordBot.Bot/Middleware/` | Provides correlation IDs for request tracing - metrics should include correlation |
| `InteractionHandler` | `src/DiscordBot.Bot/Handlers/` | Primary instrumentation point for command metrics |
| `RateLimitAttribute` | `src/DiscordBot.Bot/Preconditions/` | Already logs rate limit violations - needs metric emission |
| `BotHostedService` | `src/DiscordBot.Bot/Services/` | Source for active guilds gauge via `DiscordSocketClient` |
| `Program.cs` | `src/DiscordBot.Bot/` | Service configuration - OpenTelemetry registration point |
| `Serilog` | Configured in `Program.cs` | Existing logging infrastructure to complement |

### 2.2 Integration Requirements

1. **OpenTelemetry SDK Integration**
   - Use .NET 8 native metrics support where available
   - Configure OpenTelemetry MeterProvider for custom metrics
   - Export to Prometheus format via `/metrics` endpoint

2. **Correlation ID Integration**
   - Include correlation ID as metric tag where appropriate
   - Ensure HTTP request metrics inherit correlation context

3. **Discord.NET Integration**
   - Access `DiscordSocketClient.Guilds` for active guild count
   - Track unique users from command executions

4. **Minimal Performance Impact**
   - Use efficient histogram buckets for latency measurements
   - Avoid high-cardinality labels that could cause metric explosion

### 2.3 Architectural Patterns to Follow

Based on the existing codebase patterns:

```
Pattern: Static metrics classes with System.Diagnostics.Metrics
Example: BotMetrics.CommandCounter.Add(1, tags)

Pattern: Singleton metrics services injected where needed
Example: IBotMetrics injected into InteractionHandler

Pattern: Configuration via appsettings.json sections
Example: "OpenTelemetry": { "ServiceName": "discordbot" }
```

### 2.4 OpenTelemetry vs System.Diagnostics.Metrics

.NET 8 includes native metrics support via `System.Diagnostics.Metrics`. OpenTelemetry for .NET uses these APIs under the hood. The implementation strategy:

1. Define metrics using `System.Diagnostics.Metrics.Meter`
2. Use OpenTelemetry SDK to collect and export these metrics
3. This provides flexibility to switch exporters without changing metric definitions

### 2.5 Security Considerations

| Risk | Mitigation |
|------|------------|
| Metric endpoint exposure | Protect `/metrics` with appropriate authorization |
| High cardinality labels | Avoid user IDs/names in labels; use aggregated counts |
| Sensitive data in labels | Never include tokens, messages, or PII in metric labels |
| Resource exhaustion | Configure appropriate histogram bucket limits |

### 2.6 Performance Considerations

| Concern | Approach |
|---------|----------|
| Metric collection overhead | Use efficient counters and histograms |
| Memory usage | Limit histogram bucket count to essential percentiles |
| CPU overhead | Async metric collection where possible |
| Cardinality explosion | Careful label design - command names OK, user IDs NOT |

---

## 3. NuGet Package Versions

Based on current stable releases, add the following packages to `src/DiscordBot.Bot/DiscordBot.Bot.csproj`:

```xml
<!-- OpenTelemetry Core -->
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.14.0" />

<!-- ASP.NET Core Instrumentation (HTTP metrics) -->
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.14.0" />

<!-- HttpClient Instrumentation (outgoing HTTP calls) -->
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.14.0" />

<!-- Runtime Instrumentation (GC, ThreadPool, etc.) -->
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.14.0" />

<!-- Prometheus Exporter for ASP.NET Core -->
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.14.0-beta.1" />
```

**Note:** The Prometheus exporter is currently in beta (1.14.0-beta.1). This is stable enough for production use and is the recommended approach for Prometheus integration.

---

## 4. Metrics Specification

### 4.1 Bot Metrics (Discord Commands)

**Meter Name:** `DiscordBot.Bot`

| Metric Name | Type | Unit | Labels | Description |
|-------------|------|------|--------|-------------|
| `discordbot.command.count` | Counter | `{commands}` | `command`, `status`, `guild_id` | Total commands executed |
| `discordbot.command.duration` | Histogram | `ms` | `command`, `status` | Command execution duration |
| `discordbot.command.active` | UpDownCounter | `{commands}` | `command` | Currently executing commands |
| `discordbot.guilds.active` | ObservableGauge | `{guilds}` | - | Connected guild count |
| `discordbot.users.unique` | ObservableGauge | `{users}` | - | Unique users (rolling 24h) |
| `discordbot.ratelimit.violations` | Counter | `{violations}` | `command`, `target` | Rate limit violations |
| `discordbot.component.count` | Counter | `{interactions}` | `component_type`, `status` | Component interactions |
| `discordbot.component.duration` | Histogram | `ms` | `component_type`, `status` | Component execution duration |

### 4.2 API Metrics (HTTP Endpoints)

**Meter Name:** `DiscordBot.Api`

| Metric Name | Type | Unit | Labels | Description |
|-------------|------|------|--------|-------------|
| `discordbot.api.request.count` | Counter | `{requests}` | `endpoint`, `method`, `status_code` | Total API requests |
| `discordbot.api.request.duration` | Histogram | `ms` | `endpoint`, `method`, `status_code` | Request duration |
| `discordbot.api.request.active` | UpDownCounter | `{requests}` | - | Active concurrent requests |

**Note:** ASP.NET Core built-in metrics (via `OpenTelemetry.Instrumentation.AspNetCore`) provide HTTP metrics automatically. Custom API metrics are supplementary for Discord bot-specific endpoints.

### 4.3 Histogram Bucket Configuration

For latency histograms, use buckets optimized for command/API response times:

```csharp
// Command duration buckets (milliseconds)
private static readonly double[] CommandDurationBuckets = new[]
{
    5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000
};

// API request duration buckets (milliseconds)
private static readonly double[] ApiDurationBuckets = new[]
{
    1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500
};
```

### 4.4 Label Guidelines

**Allowed Labels (low cardinality):**
- `command` - Command name (e.g., "ping", "help", "verify")
- `status` - "success" or "failure"
- `method` - HTTP method (GET, POST, etc.)
- `status_code` - HTTP status code (200, 400, 500, etc.)
- `endpoint` - Normalized endpoint path (e.g., "/api/guilds", "/api/commands")
- `guild_id` - Guild snowflake ID (acceptable cardinality for most bots)
- `component_type` - "button", "select_menu", "modal"
- `target` - Rate limit target ("user", "guild", "global")

**Prohibited Labels (high cardinality - DO NOT USE):**
- User IDs or usernames
- Message content
- Correlation IDs (use tracing instead)
- Timestamps
- Full request paths with IDs

---

## 5. File Structure

### 5.1 New Files to Create

```
src/DiscordBot.Bot/
  Metrics/
    BotMetrics.cs              # Discord command metrics definitions
    ApiMetrics.cs              # API request metrics definitions
    MetricsConstants.cs        # Shared metric names and labels
  Middleware/
    ApiMetricsMiddleware.cs    # Custom middleware for API metrics
  Extensions/
    OpenTelemetryExtensions.cs # Service registration extension
```

### 5.2 Files to Modify

| File | Changes |
|------|---------|
| `src/DiscordBot.Bot/DiscordBot.Bot.csproj` | Add OpenTelemetry package references |
| `src/DiscordBot.Bot/Program.cs` | Configure OpenTelemetry, add middleware, map `/metrics` |
| `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` | Inject and record command metrics |
| `src/DiscordBot.Bot/Preconditions/RateLimitAttribute.cs` | Record rate limit violation metrics |
| `src/DiscordBot.Bot/appsettings.json` | Add OpenTelemetry configuration section |

---

## 6. Implementation Details

### 6.1 BotMetrics Class

**Location:** `src/DiscordBot.Bot/Metrics/BotMetrics.cs`

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for Discord bot command execution and status.
/// Uses System.Diagnostics.Metrics which is collected by OpenTelemetry.
/// </summary>
public sealed class BotMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Bot";

    private readonly Meter _meter;
    private readonly Counter<long> _commandCounter;
    private readonly Histogram<double> _commandDuration;
    private readonly UpDownCounter<long> _activeCommands;
    private readonly Counter<long> _rateLimitViolations;
    private readonly Counter<long> _componentCounter;
    private readonly Histogram<double> _componentDuration;

    // Observable gauges require callbacks
    private long _activeGuildCount;
    private long _uniqueUserCount;

    public BotMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _commandCounter = _meter.CreateCounter<long>(
            name: "discordbot.command.count",
            unit: "{commands}",
            description: "Total number of Discord commands executed");

        _commandDuration = _meter.CreateHistogram<double>(
            name: "discordbot.command.duration",
            unit: "ms",
            description: "Duration of command execution in milliseconds");

        _activeCommands = _meter.CreateUpDownCounter<long>(
            name: "discordbot.command.active",
            unit: "{commands}",
            description: "Number of currently executing commands");

        _rateLimitViolations = _meter.CreateCounter<long>(
            name: "discordbot.ratelimit.violations",
            unit: "{violations}",
            description: "Number of rate limit violations");

        _componentCounter = _meter.CreateCounter<long>(
            name: "discordbot.component.count",
            unit: "{interactions}",
            description: "Total number of component interactions");

        _componentDuration = _meter.CreateHistogram<double>(
            name: "discordbot.component.duration",
            unit: "ms",
            description: "Duration of component interaction handling");

        _meter.CreateObservableGauge(
            name: "discordbot.guilds.active",
            observeValue: () => _activeGuildCount,
            unit: "{guilds}",
            description: "Number of guilds the bot is connected to");

        _meter.CreateObservableGauge(
            name: "discordbot.users.unique",
            observeValue: () => _uniqueUserCount,
            unit: "{users}",
            description: "Estimated unique users in the last 24 hours");
    }

    public void RecordCommandExecution(
        string commandName,
        bool success,
        double durationMs,
        ulong? guildId = null)
    {
        var tags = new TagList
        {
            { "command", commandName },
            { "status", success ? "success" : "failure" }
        };

        if (guildId.HasValue)
        {
            tags.Add("guild_id", guildId.Value.ToString());
        }

        _commandCounter.Add(1, tags);
        _commandDuration.Record(durationMs, tags);
    }

    public void IncrementActiveCommands(string commandName)
    {
        _activeCommands.Add(1, new TagList { { "command", commandName } });
    }

    public void DecrementActiveCommands(string commandName)
    {
        _activeCommands.Add(-1, new TagList { { "command", commandName } });
    }

    public void RecordRateLimitViolation(string commandName, string target)
    {
        _rateLimitViolations.Add(1, new TagList
        {
            { "command", commandName },
            { "target", target }
        });
    }

    public void RecordComponentInteraction(
        string componentType,
        bool success,
        double durationMs)
    {
        var tags = new TagList
        {
            { "component_type", componentType },
            { "status", success ? "success" : "failure" }
        };

        _componentCounter.Add(1, tags);
        _componentDuration.Record(durationMs, tags);
    }

    public void UpdateActiveGuildCount(long count) => _activeGuildCount = count;

    public void UpdateUniqueUserCount(long count) => _uniqueUserCount = count;

    public void Dispose() => _meter.Dispose();
}
```

### 6.2 ApiMetrics Class

**Location:** `src/DiscordBot.Bot/Metrics/ApiMetrics.cs`

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for API request tracking.
/// Supplements ASP.NET Core built-in metrics with Discord bot-specific measurements.
/// </summary>
public sealed class ApiMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Api";

    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly UpDownCounter<long> _activeRequests;

    public ApiMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _requestCounter = _meter.CreateCounter<long>(
            name: "discordbot.api.request.count",
            unit: "{requests}",
            description: "Total number of API requests");

        _requestDuration = _meter.CreateHistogram<double>(
            name: "discordbot.api.request.duration",
            unit: "ms",
            description: "Duration of API request handling");

        _activeRequests = _meter.CreateUpDownCounter<long>(
            name: "discordbot.api.request.active",
            unit: "{requests}",
            description: "Number of currently active API requests");
    }

    public void RecordRequest(
        string endpoint,
        string method,
        int statusCode,
        double durationMs)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "method", method },
            { "status_code", statusCode.ToString() }
        };

        _requestCounter.Add(1, tags);
        _requestDuration.Record(durationMs, tags);
    }

    public void IncrementActiveRequests() => _activeRequests.Add(1);

    public void DecrementActiveRequests() => _activeRequests.Add(-1);

    public void Dispose() => _meter.Dispose();
}
```

### 6.3 OpenTelemetry Configuration Extension

**Location:** `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs`

```csharp
using DiscordBot.Bot.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry metrics collection.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry metrics with Prometheus exporter to the service collection.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "discordbot";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"]
            ?? typeof(OpenTelemetryExtensions).Assembly
                .GetName().Version?.ToString() ?? "1.0.0";

        // Register custom metrics classes as singletons
        services.AddSingleton<BotMetrics>();
        services.AddSingleton<ApiMetrics>();

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithMetrics(metrics =>
            {
                // Add custom meters
                metrics.AddMeter(BotMetrics.MeterName);
                metrics.AddMeter(ApiMetrics.MeterName);

                // Add ASP.NET Core instrumentation
                metrics.AddAspNetCoreInstrumentation();

                // Add HTTP client instrumentation
                metrics.AddHttpClientInstrumentation();

                // Add runtime instrumentation (GC, ThreadPool, etc.)
                metrics.AddRuntimeInstrumentation();

                // Configure histogram bucket boundaries
                metrics.AddView(
                    instrumentName: "discordbot.command.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000
                        }
                    });

                metrics.AddView(
                    instrumentName: "discordbot.api.request.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500
                        }
                    });

                metrics.AddView(
                    instrumentName: "discordbot.component.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000
                        }
                    });

                // Add Prometheus exporter
                metrics.AddPrometheusExporter();
            });

        return services;
    }

    /// <summary>
    /// Maps the Prometheus metrics scraping endpoint.
    /// </summary>
    public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder app)
    {
        // Map the /metrics endpoint for Prometheus scraping
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
```

### 6.4 API Metrics Middleware

**Location:** `src/DiscordBot.Bot/Middleware/ApiMetricsMiddleware.cs`

```csharp
using System.Diagnostics;
using DiscordBot.Bot.Metrics;

namespace DiscordBot.Bot.Middleware;

/// <summary>
/// Middleware that records API request metrics for Discord bot-specific endpoints.
/// Complements ASP.NET Core built-in HTTP metrics with custom measurements.
/// </summary>
public class ApiMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiMetrics _metrics;
    private readonly ILogger<ApiMetricsMiddleware> _logger;

    public ApiMetricsMiddleware(
        RequestDelegate next,
        ApiMetrics metrics,
        ILogger<ApiMetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only track API endpoints (skip static files, metrics endpoint, etc.)
        var path = context.Request.Path.Value ?? "";
        if (!ShouldTrackRequest(path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _metrics.IncrementActiveRequests();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _metrics.DecrementActiveRequests();

            var endpoint = NormalizeEndpoint(path);
            _metrics.RecordRequest(
                endpoint: endpoint,
                method: context.Request.Method,
                statusCode: context.Response.StatusCode,
                durationMs: stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static bool ShouldTrackRequest(string path)
    {
        // Track API endpoints and key Razor pages
        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Admin/", StringComparison.OrdinalIgnoreCase)
            || path == "/" || path == "/Index";
    }

    /// <summary>
    /// Normalizes endpoint paths to prevent cardinality explosion from IDs.
    /// </summary>
    private static string NormalizeEndpoint(string path)
    {
        // Replace GUIDs
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            path,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "{id}");

        // Replace numeric IDs (Discord snowflakes, etc.)
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"/\d{15,20}(?=/|$)",
            "/{id}");

        // Replace short numeric IDs
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"/\d+(?=/|$)",
            "/{id}");

        return normalized;
    }
}

/// <summary>
/// Extension methods for registering API metrics middleware.
/// </summary>
public static class ApiMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseApiMetrics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiMetricsMiddleware>();
    }
}
```

### 6.5 InteractionHandler Instrumentation

Modify `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` to record metrics:

```csharp
// Add to constructor parameters:
private readonly BotMetrics _botMetrics;

public InteractionHandler(
    DiscordSocketClient client,
    InteractionService interactionService,
    IServiceProvider serviceProvider,
    IOptions<BotConfiguration> config,
    ILogger<InteractionHandler> logger,
    ICommandExecutionLogger commandExecutionLogger,
    BotMetrics botMetrics)  // <-- Add this
{
    // ... existing assignments ...
    _botMetrics = botMetrics;
}

// In OnInteractionCreatedAsync, wrap the execution:
private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
{
    var correlationId = Guid.NewGuid().ToString("N")[..16];
    var stopwatch = Stopwatch.StartNew();

    string? commandName = null;

    // Extract command name for metrics
    if (interaction is SocketSlashCommand slashCommand)
    {
        commandName = slashCommand.CommandName;
        _botMetrics.IncrementActiveCommands(commandName);
    }

    _executionContext.Value = new ExecutionContext
    {
        CorrelationId = correlationId,
        Stopwatch = stopwatch,
        CommandName = commandName  // Add this property
    };

    try
    {
        var context = new SocketInteractionContext(_client, interaction);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["InteractionId"] = interaction.Id
        }))
        {
            _logger.LogDebug(
                "Executing interaction {InteractionType} with correlation ID {CorrelationId}",
                interaction.Type,
                correlationId);

            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        // Record failure metric
        if (commandName != null)
        {
            _botMetrics.RecordCommandExecution(
                commandName,
                success: false,
                durationMs: stopwatch.ElapsedMilliseconds,
                guildId: interaction.GuildId);
        }

        // ... existing error handling ...
    }
    finally
    {
        if (commandName != null)
        {
            _botMetrics.DecrementActiveCommands(commandName);
        }
        _executionContext.Value = null!;
    }
}

// In OnSlashCommandExecutedAsync:
private async Task OnSlashCommandExecutedAsync(
    SlashCommandInfo commandInfo,
    IInteractionContext context,
    Discord.Interactions.IResult result)
{
    var execContext = _executionContext.Value;
    var correlationId = execContext?.CorrelationId ?? "unknown";
    var stopwatch = execContext?.Stopwatch;

    stopwatch?.Stop();
    var executionTimeMs = (int)(stopwatch?.ElapsedMilliseconds ?? 0);

    // Record command metrics
    _botMetrics.RecordCommandExecution(
        commandInfo.Name,
        result.IsSuccess,
        executionTimeMs,
        context.Guild?.Id);

    // ... rest of existing logic ...
}

// In OnComponentCommandExecutedAsync:
private async Task OnComponentCommandExecutedAsync(
    ComponentCommandInfo commandInfo,
    IInteractionContext context,
    Discord.Interactions.IResult result)
{
    var execContext = _executionContext.Value;
    var stopwatch = execContext?.Stopwatch;

    stopwatch?.Stop();
    var executionTimeMs = (int)(stopwatch?.ElapsedMilliseconds ?? 0);

    // Record component metrics
    _botMetrics.RecordComponentInteraction(
        componentType: GetComponentType(context.Interaction),
        success: result.IsSuccess,
        durationMs: executionTimeMs);

    // ... rest of existing logic ...
}

private static string GetComponentType(IDiscordInteraction interaction)
{
    return interaction switch
    {
        IComponentInteraction { Type: InteractionType.MessageComponent } comp
            => comp.Data.Type switch
            {
                ComponentType.Button => "button",
                ComponentType.SelectMenu => "select_menu",
                _ => "unknown"
            },
        IModalInteraction => "modal",
        _ => "unknown"
    };
}
```

### 6.6 RateLimitAttribute Instrumentation

Modify `src/DiscordBot.Bot/Preconditions/RateLimitAttribute.cs`:

```csharp
// Add metrics recording when rate limit is exceeded:
public override Task<PreconditionResult> CheckRequirementsAsync(
    IInteractionContext context,
    ICommandInfo commandInfo,
    IServiceProvider services)
{
    var now = DateTime.UtcNow;
    var key = GetRateLimitKey(context, commandInfo);
    var commandName = commandInfo.Name;

    // Get services
    var loggerFactory = services.GetService<ILoggerFactory>();
    var logger = loggerFactory?.CreateLogger<RateLimitAttribute>();
    var botMetrics = services.GetService<BotMetrics>();  // <-- Add this

    var invocations = _invocations.GetOrAdd(key, _ => new List<DateTime>());

    lock (invocations)
    {
        invocations.RemoveAll(time => (now - time).TotalSeconds > _periodSeconds);

        if (invocations.Count >= _times)
        {
            var oldestInvocation = invocations.Min();
            var timeUntilReset = _periodSeconds - (now - oldestInvocation).TotalSeconds;

            // Record rate limit violation metric
            botMetrics?.RecordRateLimitViolation(
                commandName,
                _target.ToString().ToLowerInvariant());

            logger?.LogWarning(
                "Rate limit exceeded for user {UserId} on command {CommandName}...",
                // ... existing log ...);

            return Task.FromResult(
                PreconditionResult.FromError(
                    $"Rate limit exceeded. Please wait {timeUntilReset:F1} seconds before using this command again."
                )
            );
        }

        invocations.Add(now);
    }

    return Task.FromResult(PreconditionResult.FromSuccess());
}
```

### 6.7 Program.cs Modifications

Add OpenTelemetry configuration to `src/DiscordBot.Bot/Program.cs`:

```csharp
// Add using statements:
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Metrics;

// After builder.Services.AddInfrastructure(builder.Configuration):

// Add OpenTelemetry metrics
builder.Services.AddOpenTelemetryMetrics(builder.Configuration);

// ... (rest of existing service registrations) ...

// After app.UseCorrelationId():
app.UseApiMetrics();

// After app.MapRazorPages():
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Or use the extension method:
// app.UsePrometheusMetrics();
```

### 6.8 Configuration Updates

Add to `src/DiscordBot.Bot/appsettings.json`:

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

### 6.9 Guild Count Updates

Add a background service to periodically update guild count metrics.

**Location:** `src/DiscordBot.Bot/Services/MetricsUpdateService.cs`

```csharp
using Discord.WebSocket;
using DiscordBot.Bot.Metrics;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically updates observable gauge metrics.
/// </summary>
public class MetricsUpdateService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly BotMetrics _botMetrics;
    private readonly ILogger<MetricsUpdateService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(30);

    public MetricsUpdateService(
        DiscordSocketClient client,
        BotMetrics botMetrics,
        ILogger<MetricsUpdateService> logger)
    {
        _client = client;
        _botMetrics = botMetrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics update service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                UpdateMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    private void UpdateMetrics()
    {
        // Update active guild count
        var guildCount = _client.Guilds.Count;
        _botMetrics.UpdateActiveGuildCount(guildCount);

        // Estimate unique users (sum of all guild member counts, may have duplicates)
        // For accurate count, you'd need to track unique user IDs
        var estimatedUsers = _client.Guilds.Sum(g => g.MemberCount);
        _botMetrics.UpdateUniqueUserCount(estimatedUsers);

        _logger.LogTrace(
            "Updated metrics: Guilds={GuildCount}, EstimatedUsers={UserCount}",
            guildCount,
            estimatedUsers);
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddHostedService<MetricsUpdateService>();
```

---

## 7. Subagent Task Plan

### 7.1 dotnet-specialist

**Primary implementer for all development tasks:**

| Task | Description | Effort |
|------|-------------|--------|
| 7.1.1 | Add NuGet packages to DiscordBot.Bot.csproj | 15 min |
| 7.1.2 | Create `Metrics/BotMetrics.cs` | 1 hour |
| 7.1.3 | Create `Metrics/ApiMetrics.cs` | 30 min |
| 7.1.4 | Create `Extensions/OpenTelemetryExtensions.cs` | 1 hour |
| 7.1.5 | Create `Middleware/ApiMetricsMiddleware.cs` | 45 min |
| 7.1.6 | Modify `InteractionHandler.cs` for command metrics | 1 hour |
| 7.1.7 | Modify `RateLimitAttribute.cs` for violation metrics | 30 min |
| 7.1.8 | Update `Program.cs` with OpenTelemetry configuration | 30 min |
| 7.1.9 | Update `appsettings.json` with configuration section | 15 min |
| 7.1.10 | Create `MetricsUpdateService.cs` for gauge updates | 45 min |
| 7.1.11 | Add unit tests for metrics classes | 2 hours |
| 7.1.12 | Integration testing with Prometheus | 1 hour |

**Total estimated effort:** 9-10 hours

### 7.2 docs-writer

| Task | Description | Effort |
|------|-------------|--------|
| 7.2.1 | Document available metrics in `docs/articles/metrics.md` | 2 hours |
| 7.2.2 | Add Prometheus/Grafana setup guide | 1 hour |
| 7.2.3 | Update `api-endpoints.md` with `/metrics` endpoint | 30 min |
| 7.2.4 | Create sample Grafana dashboard JSON | 1 hour |

**Total estimated effort:** 4-5 hours

### 7.3 design-specialist

Not required for this feature - no UI changes.

### 7.4 html-prototyper

Not required for this feature - no new pages.

---

## 8. Timeline / Dependency Map

```
Day 1 (Development)
├── 7.1.1 NuGet packages (no dependencies)
├── 7.1.2 BotMetrics.cs (depends on packages)
├── 7.1.3 ApiMetrics.cs (depends on packages)
├── 7.1.4 OpenTelemetryExtensions.cs (depends on metrics classes)
├── 7.1.5 ApiMetricsMiddleware.cs (depends on ApiMetrics)
└── 7.1.8 Program.cs updates (depends on extensions + middleware)

Day 2 (Instrumentation + Testing)
├── 7.1.6 InteractionHandler instrumentation (depends on BotMetrics)
├── 7.1.7 RateLimitAttribute instrumentation (depends on BotMetrics)
├── 7.1.9 appsettings.json (no dependencies)
├── 7.1.10 MetricsUpdateService.cs (depends on BotMetrics)
├── 7.1.11 Unit tests (depends on metrics classes)
└── 7.1.12 Integration testing (depends on all above)

Day 2-3 (Documentation - can run in parallel)
├── 7.2.1 Metrics documentation
├── 7.2.2 Prometheus/Grafana guide
├── 7.2.3 API endpoint docs update
└── 7.2.4 Sample Grafana dashboard
```

**Parallelization Opportunities:**
- Documentation can begin once metrics are defined (Day 1 afternoon)
- ApiMetrics and BotMetrics can be developed in parallel
- Unit tests can be written alongside implementation

---

## 9. Acceptance Criteria

### 9.1 Command Metrics

- [ ] All slash command executions are tracked with success/failure status
- [ ] Command duration is recorded in milliseconds
- [ ] Active command count gauge reflects currently executing commands
- [ ] Metrics include command name and guild ID labels
- [ ] Component interactions (buttons, select menus) are tracked separately

### 9.2 API Metrics

- [ ] All API requests to `/api/*` endpoints are tracked
- [ ] Request duration is recorded in milliseconds
- [ ] Status codes are captured as labels
- [ ] Endpoint paths are normalized to prevent cardinality explosion
- [ ] Active request count gauge reflects concurrent requests

### 9.3 Rate Limiting Metrics

- [ ] Rate limit violations are counted with command name and target labels
- [ ] Metrics match existing log entries for violations

### 9.4 System Metrics

- [ ] Runtime metrics (GC, thread pool) are available
- [ ] Active guild count is reported as a gauge
- [ ] Unique user count (estimated) is reported

### 9.5 Prometheus Export

- [ ] Metrics are available at `/metrics` endpoint
- [ ] Output is in Prometheus text format
- [ ] Metric names follow Prometheus naming conventions (snake_case)
- [ ] Metric names include `discordbot.` prefix for namespacing
- [ ] Histogram buckets are appropriate for expected latency ranges

### 9.6 Documentation

- [ ] All custom metrics are documented with name, type, labels, and description
- [ ] Prometheus scrape configuration is documented
- [ ] Sample Grafana dashboard is provided
- [ ] Troubleshooting guide for common issues

### 9.7 Performance

- [ ] Metrics collection adds less than 1ms overhead per request
- [ ] Memory usage increase is less than 10MB
- [ ] No metric cardinality explosion from high-cardinality labels

---

## 10. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Prometheus package beta stability | Low | Medium | Pin to specific beta version; monitor for issues |
| High cardinality from labels | Medium | High | Strict label guidelines; endpoint normalization |
| Performance overhead | Low | Medium | Use efficient counters; benchmark before/after |
| Missing metrics from async flows | Medium | Low | Careful AsyncLocal usage; integration testing |
| Duplicate metrics from retries | Low | Low | Ensure single recording per logical operation |
| `/metrics` endpoint security | Medium | Medium | Consider authorization or IP filtering |

---

## 11. Testing Strategy

### 11.1 Unit Tests

**Location:** `tests/DiscordBot.Tests/Metrics/`

```csharp
// BotMetricsTests.cs
[Fact]
public void RecordCommandExecution_IncrementsCounter()
{
    // Arrange
    var meterFactory = new TestMeterFactory();
    var metrics = new BotMetrics(meterFactory);

    // Act
    metrics.RecordCommandExecution("ping", true, 50.0, 123456789);

    // Assert
    var counter = meterFactory.GetCounter("discordbot.command.count");
    Assert.Equal(1, counter.Value);
}

[Fact]
public void RecordRateLimitViolation_IncludesCorrectLabels()
{
    // Arrange
    var meterFactory = new TestMeterFactory();
    var metrics = new BotMetrics(meterFactory);

    // Act
    metrics.RecordRateLimitViolation("verify", "user");

    // Assert
    var counter = meterFactory.GetCounter("discordbot.ratelimit.violations");
    Assert.Equal("verify", counter.Tags["command"]);
    Assert.Equal("user", counter.Tags["target"]);
}
```

### 11.2 Integration Tests

```csharp
// PrometheusExportTests.cs
[Fact]
public async Task MetricsEndpoint_ReturnsPrometheusFormat()
{
    // Arrange
    await using var application = new WebApplicationFactory<Program>();
    var client = application.CreateClient();

    // Act
    var response = await client.GetAsync("/metrics");
    var content = await response.Content.ReadAsStringAsync();

    // Assert
    response.EnsureSuccessStatusCode();
    Assert.Contains("# TYPE discordbot_command_count counter", content);
    Assert.Contains("# TYPE discordbot_guilds_active gauge", content);
}
```

### 11.3 Manual Testing Checklist

- [ ] Start application and verify `/metrics` endpoint returns data
- [ ] Execute slash commands and verify `discordbot_command_count` increases
- [ ] Trigger rate limit and verify `discordbot_ratelimit_violations` increases
- [ ] Make API requests and verify `discordbot_api_request_count` increases
- [ ] Check that histogram buckets contain correct distributions
- [ ] Verify no high-cardinality labels appear in metric output
- [ ] Configure Prometheus to scrape `/metrics` and verify data appears
- [ ] Import sample Grafana dashboard and verify graphs render

---

## 12. File Summary

### New Files to Create

| File | Purpose |
|------|---------|
| `src/DiscordBot.Bot/Metrics/BotMetrics.cs` | Discord command and bot metrics |
| `src/DiscordBot.Bot/Metrics/ApiMetrics.cs` | API request metrics |
| `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs` | OpenTelemetry configuration |
| `src/DiscordBot.Bot/Middleware/ApiMetricsMiddleware.cs` | API metrics middleware |
| `src/DiscordBot.Bot/Services/MetricsUpdateService.cs` | Background gauge updates |
| `docs/articles/metrics.md` | Metrics documentation |
| `docs/grafana/discordbot-dashboard.json` | Sample Grafana dashboard |
| `tests/DiscordBot.Tests/Metrics/BotMetricsTests.cs` | Unit tests |
| `tests/DiscordBot.Tests/Metrics/ApiMetricsTests.cs` | Unit tests |

### Files to Modify

| File | Changes |
|------|---------|
| `src/DiscordBot.Bot/DiscordBot.Bot.csproj` | Add OpenTelemetry packages |
| `src/DiscordBot.Bot/Program.cs` | Configure OpenTelemetry, add middleware |
| `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` | Inject BotMetrics, record command metrics |
| `src/DiscordBot.Bot/Preconditions/RateLimitAttribute.cs` | Record rate limit violation metrics |
| `src/DiscordBot.Bot/appsettings.json` | Add OpenTelemetry configuration section |
| `docs/articles/api-endpoints.md` | Document `/metrics` endpoint |

---

## 13. Prometheus Configuration Example

For monitoring infrastructure setup, add this Prometheus scrape config:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'discordbot'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5001']
    scheme: https
    tls_config:
      insecure_skip_verify: true  # For development only
    metrics_path: '/metrics'
```

---

## 14. Grafana Dashboard Queries

Sample PromQL queries for key metrics:

```promql
# Command success rate (last 5 minutes)
sum(rate(discordbot_command_count{status="success"}[5m]))
/
sum(rate(discordbot_command_count[5m])) * 100

# Command latency p95
histogram_quantile(0.95,
  sum(rate(discordbot_command_duration_bucket[5m])) by (le, command)
)

# Active guilds
discordbot_guilds_active

# Rate limit violations per minute
sum(rate(discordbot_ratelimit_violations[1m])) by (command)

# API request rate by endpoint
sum(rate(discordbot_api_request_count[5m])) by (endpoint)

# Error rate (5xx responses)
sum(rate(discordbot_api_request_count{status_code=~"5.."}[5m]))
/
sum(rate(discordbot_api_request_count[5m])) * 100
```

---

*Document prepared by: Systems Architect Agent*
*Review status: Ready for implementation*
