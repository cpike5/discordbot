# Distributed Tracing with OpenTelemetry

**Version:** 1.1
**Last Updated:** 2026-01-03
**Target Framework:** .NET 8 with OpenTelemetry SDK

---

## Overview

The Discord Bot Management System implements distributed tracing using OpenTelemetry to provide end-to-end visibility into Discord command execution flows. Tracing works alongside [metrics collection](metrics.md) to provide comprehensive observability into bot operations.

### Key Features

- **Command Tracing**: Track Discord slash command execution from invocation to completion
- **Component Tracing**: Monitor interactive component interactions (buttons, select menus, modals)
- **Database Tracing**: Instrument repository operations and Entity Framework Core queries
- **Context Propagation**: Maintain trace context across async boundaries
- **Correlation ID Integration**: Link correlation IDs to trace IDs for unified request tracking
- **Flexible Export**: Support for Jaeger, Azure Application Insights, and other OTLP-compatible backends

### Benefits

- Visualize request flows across Discord commands, application logic, and database operations
- Identify performance bottlenecks with span timing data
- Debug complex async workflows with parent-child span relationships
- Correlate traces with structured logs via correlation IDs
- Analyze system behavior in production with configurable sampling

---

## Architecture

### Trace Flow

```
Discord Interaction
        │
        ▼
┌───────────────────────────────────────────────────────────┐
│ Parent Span: discord.command {commandName}                │
│ TraceId: abc123... | SpanId: def456... | CorrelationId   │
│                                                            │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ Child Span: db.select GuildSettings                   │ │
│  │ Attributes: db.operation=SELECT, db.entity.type=...  │ │
│  │                                                       │ │
│  │  ┌─────────────────────────────────────────────────┐ │ │
│  │  │ EF Core Span: Microsoft.EntityFrameworkCore     │ │ │
│  │  │ Attributes: db.system=sqlite, db.statement=...  │ │ │
│  │  └─────────────────────────────────────────────────┘ │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                            │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ Child Span: db.insert CommandLog                      │ │
│  │ Attributes: db.operation=INSERT, db.entity.type=...  │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                            │
└───────────────────────────────────────────────────────────┘
        │
        ▼
   OTLP Exporter → Jaeger / Application Insights
```

### Activity Sources

The system uses two `ActivitySource` instances for span creation:

| Source Name | Location | Purpose |
|-------------|----------|---------|
| `DiscordBot.Bot` | `src/DiscordBot.Bot/Tracing/BotActivitySource.cs` | Discord command and component spans |
| `DiscordBot.Infrastructure` | `src/DiscordBot.Infrastructure/Tracing/InfrastructureActivitySource.cs` | Repository and database spans |

Both sources follow the singleton pattern and are automatically registered with the OpenTelemetry `TracerProvider`.

---

## Span Conventions

### Span Naming

Following OpenTelemetry semantic conventions, span names use static patterns with dynamic data in attributes:

| Span Name Pattern | Example | When Used |
|-------------------|---------|-----------|
| `discord.command {name}` | `discord.command ping` | Slash command execution |
| `discord.component {type}` | `discord.component button` | Interactive component interaction |
| `db.{operation} {entity}` | `db.select GuildSettings` | Repository database operations |

**Rationale:** Static span names prevent cardinality explosion while attributes provide filtering and aggregation capabilities.

### Standard Attributes

#### Discord Command Spans

| Attribute | Type | Example | Description |
|-----------|------|---------|-------------|
| `discord.command.name` | string | "ping" | Command name |
| `discord.guild.id` | string | "123456789012345678" or "dm" | Guild snowflake ID or "dm" for direct messages |
| `discord.user.id` | string | "987654321098765432" | User snowflake ID (for tracking, not PII) |
| `discord.interaction.id` | string | "111222333444555666" | Discord interaction ID |
| `correlation.id` | string | "a1b2c3d4e5f6g7h8" | Application correlation ID |

#### Discord Component Spans

| Attribute | Type | Example | Description |
|-----------|------|---------|-------------|
| `discord.component.type` | string | "button" | Component type: button, select_menu, modal |
| `discord.component.id` | string | "verify:accept" | Sanitized custom ID (handler:action only) |
| `discord.guild.id` | string | "123456789012345678" | Guild snowflake ID |
| `discord.user.id` | string | "987654321098765432" | User snowflake ID |
| `discord.interaction.id` | string | "111222333444555666" | Discord interaction ID |
| `correlation.id` | string | "a1b2c3d4e5f6g7h8" | Application correlation ID |

#### Database Operation Spans

| Attribute | Type | Example | Description |
|-----------|------|---------|-------------|
| `db.operation` | string | "SELECT" | Database operation: SELECT, INSERT, UPDATE, DELETE, COUNT, EXISTS |
| `db.entity.type` | string | "GuildSettings" | Entity class name |
| `db.entity.id` | string | "123456789012345678" | Entity ID (if applicable) |
| `db.duration.ms` | double | 15.234 | Operation duration in milliseconds |
| `correlation.id` | string | "a1b2c3d4e5f6g7h8" | Inherited from parent span baggage |

#### Entity Framework Core Spans (Auto-Instrumented)

| Attribute | Type | Example | Description |
|-----------|------|---------|-------------|
| `db.system` | string | "sqlite" | Database system: sqlite, sqlserver, postgresql |
| `db.name` | string | "discordbot.db" | Database name |
| `db.statement` | string | "SELECT ... FROM GuildSettings WHERE Id = @p0" | SQL query text (dev mode only) |
| `db.connection_string` | string | (redacted) | Connection string (sanitized) |

**Security Note:** SQL statements are only included in non-production environments via `SetDbStatementForText` configuration.

### Span Status

| Scenario | Status | Description |
|----------|--------|-------------|
| Command succeeds | `Ok` | Normal completion |
| Command fails (user error) | `Ok` | User-facing errors are not infrastructure failures |
| Command fails (exception) | `Error` | System/infrastructure failures |
| Database operation succeeds | `Ok` | Normal completion |
| Database operation fails | `Error` | Includes exception details via `activity.AddException()` |

**Guideline:** Use `Error` status for infrastructure/system failures, not business logic errors.

---

## Configuration

### appsettings.json

Tracing configuration is defined under the `OpenTelemetry:Tracing` section:

```json
{
  "OpenTelemetry": {
    "ServiceName": "discordbot",
    "ServiceVersion": "0.1.0",
    "Tracing": {
      "Enabled": true,
      "EnableConsoleExporter": false,
      "OtlpEndpoint": null,
      "OtlpProtocol": "grpc",
      "Sampling": {
        "DefaultRate": 0.1,
        "ErrorRate": 1.0,
        "SlowThresholdMs": 5000,
        "HighPriorityRate": 0.5,
        "LowPriorityRate": 0.01
      }
    }
  }
}
```

### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | true | Enable/disable distributed tracing |
| `EnableConsoleExporter` | bool | false | Export traces to console output |
| `OtlpEndpoint` | string | null | OTLP collector endpoint (e.g., "http://jaeger:4317") |
| `OtlpProtocol` | string | "grpc" | OTLP protocol: "grpc" or "http" |
| `Sampling` | object | see below | Priority-based sampling configuration (configured via `SamplingOptions`) |

#### Sampling Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DefaultRate` | double | 0.1 | Default sampling rate for normal operations (0.0-1.0). Recommended: 1.0 for dev, 0.1 for prod |
| `ErrorRate` | double | 1.0 | Sampling rate for operations with errors or critical conditions (0.0-1.0). Always sample errors by default |
| `SlowThresholdMs` | int | 5000 | Threshold in milliseconds that defines a slow operation (sampled at ErrorRate) |
| `HighPriorityRate` | double | 0.5 | Sampling rate for high-priority operations (0.0-1.0). Includes moderation, welcome flow, Rat Watch, scheduled messages |
| `LowPriorityRate` | double | 0.01 | Sampling rate for low-priority operations (0.0-1.0). Includes health checks, metrics endpoints, cache operations |

### Environment-Specific Configuration

**appsettings.Development.json:**

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "EnableConsoleExporter": true,
      "Sampling": {
        "DefaultRate": 1.0
      }
    }
  }
}
```

**appsettings.Production.json:**

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "EnableConsoleExporter": false,
      "OtlpEndpoint": "http://jaeger:4317",
      "Sampling": {
        "DefaultRate": 0.1,
        "ErrorRate": 1.0,
        "HighPriorityRate": 0.5,
        "LowPriorityRate": 0.01
      }
    }
  }
}
```

### Sampling Strategy

The bot implements **priority-based sampling** to intelligently balance observability needs with infrastructure costs and performance overhead. This head-based sampling approach categorizes operations into different priority tiers, each with configurable sampling rates.

#### Sampling Priority Tiers

| Priority | Default Rate | When Applied | Operations Included |
|----------|--------------|--------------|---------------------|
| **Always Sample** | 100% (1.0) | Critical errors and important security events | Discord API errors, rate limit hits (remaining=0), auto-moderation detections (spam, raids, content filtering) |
| **High Priority** | 50% (0.5) | Important business operations requiring good visibility | Welcome flow (new member joins), moderation actions (/warn, /kick, /ban, /mute), Rat Watch operations, scheduled message executions |
| **Default** | 10% (0.1) prod<br>100% (1.0) dev | Standard operations | General Discord commands, database operations, API requests |
| **Low Priority** | 1% (0.01) | High-frequency, low-value operations | Health check endpoints (/health), metrics scraping (/metrics), cache get/set operations |

**Environment Defaults:**

| Environment | DefaultRate | Rationale |
|-------------|-------------|-----------|
| Development | 100% (1.0) | Full visibility for debugging and local testing |
| Production | 10% (0.1) | Balance observability with overhead, storage costs, and network bandwidth |

#### Implementation Details

**Head-Based Sampling:**

Sampling decisions are made at span creation time using the custom `PrioritySampler` class (`src/DiscordBot.Bot/Tracing/PrioritySampler.cs`). The sampler inspects span names and attributes to determine the appropriate sampling rate.

**Parent-Child Trace Continuity:**

When a parent span is sampled, all child spans are automatically sampled to maintain complete trace context. This ensures that sampled traces provide end-to-end visibility without gaps.

```csharp
// If parent was sampled, sample this span too
if (parentContext.TraceId != default && (parentContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
{
    return new SamplingResult(SamplingDecision.RecordAndSample);
}
```

**Pattern Matching:**

The sampler uses span name pattern matching and attribute inspection to classify operations:

```csharp
// Always sample: Discord API errors
if (attributes.ContainsKey("discord.api.error_code"))
{
    return 1.0; // 100% sampling
}

// High priority: Moderation commands
if (spanName.Contains("/warn") || spanName.Contains("/ban"))
{
    return 0.5; // 50% sampling (HighPriorityRate)
}

// Low priority: Health checks
if (spanName.Contains("/health"))
{
    return 0.01; // 1% sampling (LowPriorityRate)
}
```

**Adjusting Rates Per Environment:**

Override sampling rates in environment-specific configuration files:

```json
// appsettings.Staging.json - More aggressive sampling for pre-production testing
{
  "OpenTelemetry": {
    "Tracing": {
      "Sampling": {
        "DefaultRate": 0.5,
        "HighPriorityRate": 1.0,
        "LowPriorityRate": 0.1
      }
    }
  }
}
```

#### Custom Sampler Reference

**Location:** `src/DiscordBot.Bot/Tracing/PrioritySampler.cs`

The `PrioritySampler` class implements `OpenTelemetry.Trace.Sampler` and provides three classification methods:

- `IsAlwaysSampleOperation()` - Identifies critical operations (errors, rate limits, security events)
- `IsHighPriorityOperation()` - Identifies important business operations (moderation, welcome, Rat Watch)
- `IsLowPriorityOperation()` - Identifies high-frequency monitoring operations (health checks, metrics)

See the source file for complete pattern matching logic and extensibility points.

---

## Viewing Traces in Jaeger

### Local Jaeger Setup

For local development, run Jaeger using Docker:

```bash
# Start Jaeger all-in-one container
docker run -d --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:latest

# Access Jaeger UI at http://localhost:16686
```

**Port Reference:**
- `16686` - Jaeger UI
- `4317` - OTLP gRPC receiver
- `4318` - OTLP HTTP receiver

### Configuring Bot to Export to Jaeger

Update `appsettings.Development.json`:

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "OtlpEndpoint": "http://localhost:4317"
    }
  }
}
```

Restart the bot application. Traces will now be exported to Jaeger.

### Using the Jaeger UI

1. **Access Jaeger:**
   - Navigate to `http://localhost:16686`

2. **Find Traces:**
   - Select **Service**: `discordbot`
   - Set **Lookback**: Last hour or appropriate timeframe
   - Click **Find Traces**

3. **Filter Traces:**
   - **By Operation**: Select specific operation (e.g., `discord.command ping`)
   - **By Tags**: Filter by attributes (e.g., `discord.guild.id=123456789012345678`)
   - **By Min Duration**: Find slow traces (e.g., min duration > 1000ms)

4. **View Trace Details:**
   - Click on a trace to see the span hierarchy
   - Expand spans to view attributes and timing
   - Look for red spans indicating errors

### Example Trace in Jaeger

**Trace Structure:**

```
discord.command ping                     [2.5ms] (100%)
├── db.select GuildSettings              [1.2ms] (48%)
│   └── Microsoft.EntityFrameworkCore    [0.8ms] (32%)
└── db.insert CommandLog                 [0.5ms] (20%)
    └── Microsoft.EntityFrameworkCore    [0.3ms] (12%)
```

**Span Attributes (discord.command ping):**

```
discord.command.name: ping
discord.guild.id: 123456789012345678
discord.user.id: 987654321098765432
discord.interaction.id: 111222333444555666
correlation.id: a1b2c3d4e5f6g7h8
otel.status_code: OK
```

**Span Attributes (db.select GuildSettings):**

```
db.operation: SELECT
db.entity.type: GuildSettings
db.entity.id: 123456789012345678
db.duration.ms: 1.2
correlation.id: a1b2c3d4e5f6g7h8
```

---

## Correlation ID to Trace ID Linking

### Overview

The system links **correlation IDs** (application-level request tracking) with **trace IDs** (distributed tracing context) for unified observability:

- **Correlation ID**: 16-character hex string (e.g., `a1b2c3d4e5f6g7h8`) generated per Discord interaction or HTTP request
- **Trace ID**: 32-character hex string (e.g., `abc123def456...`) generated by OpenTelemetry
- **Span ID**: 16-character hex string (e.g., `def456abc123...`) unique to each span

### Integration Points

#### 1. CorrelationIdMiddleware

For HTTP requests, the `CorrelationIdMiddleware` links correlation IDs to the active trace:

```csharp
var activity = Activity.Current;
if (activity != null)
{
    activity.SetTag("correlation.id", correlationId);
    activity.AddBaggage("correlation-id", correlationId);
}
```

**Log Context Enhancement:**

```csharp
using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("TraceId", activity?.TraceId.ToString() ?? "none"))
using (LogContext.PushProperty("SpanId", activity?.SpanId.ToString() ?? "none"))
{
    await _next(context);
}
```

#### 2. InteractionHandler

For Discord interactions, the `InteractionHandler` creates a correlation ID and associates it with the command span:

```csharp
var correlationId = Guid.NewGuid().ToString("N")[..16];

var activity = BotActivitySource.StartCommandActivity(
    commandName: commandName,
    guildId: interaction.GuildId,
    userId: interaction.User.Id,
    interactionId: interaction.Id,
    correlationId: correlationId);

// Logs include both correlation ID and trace ID
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["InteractionId"] = interaction.Id,
    ["TraceId"] = activity?.TraceId.ToString() ?? "none"
}))
{
    // Execute command...
}
```

#### 3. Baggage Propagation

Correlation IDs are added as **baggage** to spans, allowing child spans to inherit the correlation ID:

```csharp
// In BotActivitySource.StartCommandActivity:
activity.AddBaggage(TracingConstants.Baggage.CorrelationId, correlationId);

// In InfrastructureActivitySource.StartRepositoryActivity:
var correlationId = Activity.Current?.GetBaggageItem("correlation-id");
if (!string.IsNullOrEmpty(correlationId))
{
    activity.SetTag("correlation.id", correlationId);
}
```

**Result:** All spans in a trace share the same correlation ID, enabling cross-cutting queries.

### Querying by Correlation ID

**In Jaeger:**

1. Go to **Search** tab
2. Select **Service**: `discordbot`
3. In **Tags** field, enter: `correlation.id=a1b2c3d4e5f6g7h8`
4. Click **Find Traces**

**In Logs:**

All structured logs include `CorrelationId`, `TraceId`, and `SpanId` properties, enabling queries like:

```
CorrelationId="a1b2c3d4e5f6g7h8"
```

This finds all log entries for that request, regardless of tracing sampling.

---

## Instrumentation Examples

### Command Execution Tracing

**Location:** `src/DiscordBot.Bot/Handlers/InteractionHandler.cs`

```csharp
var correlationId = Guid.NewGuid().ToString("N")[..16];
Activity? activity = null;

if (interaction is SocketSlashCommand slashCommand)
{
    activity = BotActivitySource.StartCommandActivity(
        commandName: slashCommand.CommandName,
        guildId: interaction.GuildId,
        userId: interaction.User.Id,
        interactionId: interaction.Id,
        correlationId: correlationId);
}

try
{
    // Execute command logic...
    await _interactionService.ExecuteCommandAsync(context, _serviceProvider);

    BotActivitySource.SetSuccess(activity);
}
catch (Exception ex)
{
    BotActivitySource.RecordException(activity, ex);
    throw;
}
finally
{
    activity?.Dispose();
}
```

### Repository Operation Tracing

**Location:** `src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs`

```csharp
public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
{
    using var activity = InfrastructureActivitySource.StartRepositoryActivity(
        operationName: "GetByIdAsync",
        entityType: _entityTypeName,
        dbOperation: "SELECT",
        entityId: id?.ToString());

    var stopwatch = Stopwatch.StartNew();

    try
    {
        var result = await DbSet.FindAsync(new[] { id }, cancellationToken);
        stopwatch.Stop();

        InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);
        throw;
    }
}
```

### Component Interaction Tracing

**Location:** `src/DiscordBot.Bot/Handlers/InteractionHandler.cs`

```csharp
if (interaction is SocketMessageComponent component)
{
    var componentType = component.Data.Type switch
    {
        ComponentType.Button => "button",
        ComponentType.SelectMenu => "select_menu",
        _ => "unknown"
    };

    activity = BotActivitySource.StartComponentActivity(
        componentType: componentType,
        customId: component.Data.CustomId,
        guildId: interaction.GuildId,
        userId: interaction.User.Id,
        interactionId: interaction.Id,
        correlationId: correlationId);
}
```

**Custom ID Sanitization:**

Component custom IDs follow the pattern `{handler}:{action}:{userId}:{correlationId}:{data}`. To prevent high cardinality, only the `handler:action` portion is included in the span attribute:

```csharp
// Original: verify:accept:123456789:a1b2c3d4:roleId
// Sanitized: verify:accept
```

---

## Troubleshooting

### Traces Not Appearing in Jaeger

**Problem:** No traces appear in Jaeger UI after executing commands.

**Solutions:**

1. **Verify OTLP Endpoint Configuration:**
   ```json
   {
     "OpenTelemetry": {
       "Tracing": {
         "OtlpEndpoint": "http://localhost:4317"
       }
     }
   }
   ```

2. **Check Jaeger Container:**
   ```bash
   docker ps | grep jaeger
   docker logs jaeger
   ```

3. **Verify Sampling:**
   - Development should have `Sampling.DefaultRate: 1.0` for 100% sampling
   - Check if your request was sampled based on priority tier
   - Low priority operations (health checks) may not be sampled even at default rate

4. **Test Console Exporter:**
   ```json
   {
     "OpenTelemetry": {
       "Tracing": {
         "EnableConsoleExporter": true
       }
     }
   }
   ```
   Traces should appear in console output if tracing is working.

5. **Check Service Registration:**
   ```csharp
   // In Program.cs
   builder.Services.AddOpenTelemetryTracing(builder.Configuration);
   ```

### Spans Missing Attributes

**Problem:** Spans appear but are missing expected attributes like `correlation.id`.

**Solutions:**

1. **Verify Attribute Setting:**
   ```csharp
   activity.SetTag("correlation.id", correlationId);
   ```

2. **Check Baggage Propagation:**
   ```csharp
   activity.AddBaggage("correlation-id", correlationId);
   ```

3. **Verify Parent-Child Relationship:**
   - Child spans inherit baggage from parent spans
   - Ensure child spans are created within parent span context

### EF Core Spans Missing SQL Statements

**Problem:** Entity Framework Core spans don't show SQL query text.

**Solutions:**

1. **Check Environment:**
   - SQL statements are only included in non-production environments
   - Verify `ASPNETCORE_ENVIRONMENT` is not "Production"

2. **Verify EF Core Instrumentation:**
   ```csharp
   tracing.AddEntityFrameworkCoreInstrumentation(options =>
   {
       options.SetDbStatementForText = !isProduction;
   });
   ```

3. **Check NuGet Package:**
   ```xml
   <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.15" />
   ```

### High Memory Usage from Tracing

**Problem:** Application memory usage increases significantly with tracing enabled.

**Solutions:**

1. **Reduce Sampling Rates:**
   ```json
   {
     "OpenTelemetry": {
       "Tracing": {
         "Sampling": {
           "DefaultRate": 0.05,
           "HighPriorityRate": 0.25,
           "LowPriorityRate": 0.001
         }
       }
     }
   }
   ```

2. **Disable Verbose Instrumentation:**
   ```csharp
   // Remove or disable EF Core instrumentation in production
   tracing.AddEntityFrameworkCoreInstrumentation();
   ```

3. **Check Span Attributes:**
   - Avoid adding large strings or objects as attributes
   - Use static attribute values when possible

### Correlation ID Not Propagating

**Problem:** Child spans don't have the correlation ID attribute.

**Solutions:**

1. **Verify Baggage Addition:**
   ```csharp
   activity.AddBaggage("correlation-id", correlationId);
   ```

2. **Check Baggage Retrieval:**
   ```csharp
   var correlationId = Activity.Current?.GetBaggageItem("correlation-id");
   ```

3. **Ensure Proper Async Context:**
   - Use `async`/`await` for all async operations
   - Don't use `Task.Run()` which can break `Activity.Current` context

---

## Performance Considerations

### Tracing Overhead

| Component | Overhead | Notes |
|-----------|----------|-------|
| Span creation | ~5-10 microseconds | Minimal CPU impact |
| Attribute setting | ~1-2 microseconds per attribute | Negligible |
| OTLP export (batched) | ~10-50ms per batch | Asynchronous, non-blocking |
| EF Core instrumentation | ~50-100 microseconds per query | Only in non-production |

**Total Overhead:** <1% CPU, <50MB memory for typical workloads with 10% sampling.

### Sampling Impact

| Sampling Configuration | Approximate Traces Exported | Memory Usage | Network Bandwidth |
|----------------------|---------------------------|--------------|-------------------|
| **Development** (DefaultRate: 1.0) | ~95% of all requests | ~48MB per 10k requests | High |
| **Production Default** (DefaultRate: 0.1, High: 0.5, Low: 0.01) | ~15-25% of all requests | ~8-12MB per 10k requests | Low |
| **Production Conservative** (DefaultRate: 0.05, High: 0.25, Low: 0.001) | ~8-12% of all requests | ~4-6MB per 10k requests | Minimal |

**Notes:**
- "Always Sample" operations (errors, rate limits, auto-moderation) are sampled at 100% regardless of configuration
- Actual export percentages depend on the mix of operation types in your workload
- High-priority operations (moderation, welcome flow) significantly impact export volume in active communities

**Recommendation:** Use priority-based sampling with DefaultRate 1.0 in development, 0.1 in production. Monitor actual costs and adjust rates based on traffic volume and budget.

### Memory Management

- Spans are batched and exported every 5 seconds (default)
- Batch size limited to 512 spans
- Exported spans are immediately garbage collected
- No long-term span retention in application memory

---

## Security Considerations

### Data Privacy

**Tracing DOES NOT include:**
- Discord message content
- User passwords or credentials
- Bot tokens or secrets
- Personally identifiable information (PII)

**Tracing DOES include:**
- Discord user IDs and guild IDs (snowflake IDs, not PII per Discord TOS)
- Command names and interaction types
- Database entity types and IDs
- SQL query text (development only, no user data in queries)

### SQL Statement Security

SQL statements are excluded from traces in production environments:

```csharp
tracing.AddEntityFrameworkCoreInstrumentation(options =>
{
    // Only include SQL text in non-production for security
    options.SetDbStatementForText = !isProduction;
    options.SetDbStatementForStoredProcedure = !isProduction;
});
```

**Rationale:** SQL statements may contain sensitive query parameters in some cases. Excluding them in production prevents accidental data exposure.

### Custom ID Sanitization

Component custom IDs are sanitized before being added to spans:

```csharp
// Original: verify:accept:123456789:a1b2c3d4:roleId
// Sanitized: verify:accept
```

This removes user-specific correlation IDs and data, preventing sensitive information in traces.

### OTLP Endpoint Security

For production deployments:

- Use TLS for OTLP endpoints: `https://jaeger:4318` (HTTP/Protobuf)
- Implement authentication if exporting to external services
- Use network isolation for internal exporters
- Consider using Azure Application Insights with managed authentication

---

## Azure Application Insights Integration

### Setup

1. **Install NuGet Package:**
   ```bash
   dotnet add package Azure.Monitor.OpenTelemetry.Exporter
   ```

2. **Update OpenTelemetryExtensions.cs:**
   ```csharp
   // Add to OpenTelemetryExtensions.AddOpenTelemetryTracing method
   var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
   if (!string.IsNullOrEmpty(appInsightsConnectionString))
   {
       tracing.AddAzureMonitorTraceExporter(options =>
       {
           options.ConnectionString = appInsightsConnectionString;
       });
   }
   ```

3. **Configure Connection String:**
   ```json
   {
     "ApplicationInsights": {
       "ConnectionString": "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westus2-2.in.applicationinsights.azure.com/"
     }
   }
   ```

### Viewing Traces in Application Insights

1. Navigate to your Application Insights resource in Azure Portal
2. Go to **Transaction Search** or **End-to-end Transaction Details**
3. Filter by:
   - **Operation Name**: `discord.command {commandName}`
   - **Custom Properties**: `correlation.id`, `discord.guild.id`, etc.

**Benefits:**
- Integrated with Azure Monitor ecosystem
- Automatic alerting and anomaly detection
- Long-term retention (90 days default)
- Application Map for dependency visualization

---

## Advanced Usage

### Creating Custom Spans

For custom instrumentation beyond commands and repositories:

```csharp
using var activity = BotActivitySource.Source.StartActivity(
    name: "custom.operation",
    kind: ActivityKind.Internal);

if (activity != null)
{
    activity.SetTag("custom.attribute", "value");

    try
    {
        // Your operation logic...
        activity.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddException(ex);
        throw;
    }
}
```

### Adding Events to Spans

Events add timestamped annotations to spans:

```csharp
activity?.AddEvent(new ActivityEvent(
    name: "cache.hit",
    timestamp: DateTimeOffset.UtcNow,
    tags: new ActivityTagsCollection
    {
        { "cache.key", cacheKey },
        { "cache.hit", true }
    }));
```

### Linking Spans

For distributed workflows that span multiple traces:

```csharp
var linkedContext = new ActivityContext(
    traceId: ActivityTraceId.CreateFromString("abc123..."),
    spanId: ActivitySpanId.CreateFromString("def456..."),
    traceFlags: ActivityTraceFlags.Recorded);

using var activity = BotActivitySource.Source.StartActivity(
    name: "linked.operation",
    kind: ActivityKind.Internal,
    links: new[] { new ActivityLink(linkedContext) });
```

---

## Related Documentation

- [OpenTelemetry Metrics](metrics.md) - Metrics collection and Prometheus export (complementary observability pillar)
- [Correlation ID Middleware](../implementation-plans/issue-100-correlation-id-middleware.md) - Correlation ID implementation details
- [API Endpoints Reference](api-endpoints.md) - REST API documentation
- [Interactive Components](interactive-components.md) - Discord button and component patterns

### External Resources

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=net)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.1 | 2026-01-03 | Added priority-based sampling strategy documentation with `SamplingOptions`, updated configuration examples |
| 1.0 | 2025-12-24 | Initial distributed tracing documentation (Issue #105) |

---

*Last Updated: January 3, 2026*
