# Issue #105 - Distributed Tracing with OpenTelemetry

## Implementation Plan

**Document Version:** 1.0
**Date:** 2025-12-24
**Issue Reference:** GitHub Issue #105
**Priority:** P2 - Medium
**Effort:** Large (1-2 days)
**Dependencies:**
- Issue #100: Correlation ID Middleware (COMPLETED)
- Issue #104: OpenTelemetry Metrics (COMPLETED)

---

## 1. Requirement Summary

Implement distributed tracing with OpenTelemetry to provide end-to-end visibility into Discord command execution flows. The system currently has:
- Correlation ID middleware for request tracking
- OpenTelemetry metrics collection with Prometheus export

This implementation will add:
- Parent spans for Discord command execution in `InteractionHandler`
- Child spans for repository database operations
- EF Core query instrumentation
- Trace context propagation across async boundaries
- Correlation ID to trace ID linking
- Configurable trace sampling (100% dev, 10% prod)
- OTLP exporter support for Jaeger/Application Insights

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Relevance |
|-----------|----------|-----------|
| `OpenTelemetryExtensions` | `src/DiscordBot.Bot/Extensions/` | Extend with tracing configuration |
| `CorrelationIdMiddleware` | `src/DiscordBot.Bot/Middleware/` | Link correlation IDs to trace context |
| `InteractionHandler` | `src/DiscordBot.Bot/Handlers/` | Primary instrumentation point for command spans |
| `Repository<T>` | `src/DiscordBot.Infrastructure/Data/Repositories/` | Add child spans for DB operations |
| `BotMetrics` | `src/DiscordBot.Bot/Metrics/` | Pattern reference for singleton activity source |
| `BotDbContext` | `src/DiscordBot.Infrastructure/Data/` | EF Core instrumentation target |
| `Program.cs` | `src/DiscordBot.Bot/` | Service configuration point |
| `appsettings.json` | `src/DiscordBot.Bot/` | Tracing configuration section |

### 2.2 OpenTelemetry Tracing Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Discord Command Flow                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ Parent Span: "discord.command {commandName}"                          │   │
│  │ TraceId: abc123... | SpanId: def456... | CorrelationId: 1a2b3c4d...  │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "Repository.GetByIdAsync<GuildSettings>"           │  │   │
│  │  │ Attributes: db.operation=SELECT, entity.type=GuildSettings     │  │   │
│  │  │                                                                 │  │   │
│  │  │  ┌─────────────────────────────────────────────────────────┐   │  │   │
│  │  │  │ Child Span: EF Core Query (auto-instrumented)           │   │  │   │
│  │  │  │ Attributes: db.system=sqlite, db.statement=SELECT...    │   │  │   │
│  │  │  └─────────────────────────────────────────────────────────┘   │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "Repository.AddAsync<CommandLog>"                  │  │   │
│  │  │ Attributes: db.operation=INSERT, entity.type=CommandLog        │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Integration Requirements

1. **OpenTelemetry SDK Integration**
   - Use `System.Diagnostics.Activity` API (W3C trace context)
   - Configure `TracerProvider` for trace collection
   - Register custom `ActivitySource` for Discord bot spans

2. **Correlation ID Integration**
   - Link correlation ID as a baggage/attribute on spans
   - Propagate trace context through middleware pipeline
   - Enable correlation between logs and traces

3. **Discord.NET Integration**
   - Create spans at interaction creation
   - Propagate context through async command execution
   - Handle component interactions (buttons, modals) with proper parent context

4. **Entity Framework Core Integration**
   - Use EF Core instrumentation package for query spans
   - Include SQL statements in development mode only
   - Filter out sensitive query parameters

### 2.4 Trace Sampling Strategy

| Environment | Sampling Rate | Rationale |
|-------------|---------------|-----------|
| Development | 100% | Full visibility for debugging |
| Production | 10% | Balance visibility vs. overhead |

Sampling is configured via environment variable or appsettings.json:
```csharp
var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
var samplingRatio = isProduction ? 0.1 : 1.0;
```

### 2.5 Security Considerations

| Risk | Mitigation |
|------|------------|
| SQL statements in traces | Only include in development; filter parameters |
| User data in spans | Never include Discord usernames, message content, or PII |
| Trace data exfiltration | Use authenticated OTLP endpoints; TLS for data in transit |
| High cardinality span names | Use static span names with attributes for variability |

### 2.6 Performance Considerations

| Concern | Approach |
|---------|----------|
| Span creation overhead | Minimal (~microseconds per span) |
| Memory per span | ~500 bytes; batched export reduces memory pressure |
| Network overhead (OTLP) | Batch exports every 5 seconds |
| EF Core query capturing | Disable in production or sample aggressively |

---

## 3. NuGet Packages

Add the following packages to `src/DiscordBot.Bot/DiscordBot.Bot.csproj`:

```xml
<!-- OpenTelemetry Tracing (extends existing hosting package) -->
<!-- Already have: OpenTelemetry.Extensions.Hosting 1.14.0 -->

<!-- Entity Framework Core Instrumentation -->
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.15" />

<!-- OTLP Exporter (for Jaeger, Tempo, Azure Monitor, etc.) -->
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.14.0" />

<!-- Optional: Console exporter for development -->
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.14.0" />
```

**Package Notes:**
- EF Core instrumentation is in beta but stable for production use
- OTLP exporter supports both gRPC and HTTP protocols
- Console exporter is useful for local development without infrastructure

Add to `src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj`:

```xml
<!-- Required for ActivitySource in Repository layer -->
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.1" />
```

**Note:** This package may already be a transitive dependency. Adding it explicitly ensures version consistency.

---

## 4. Tracing Specification

### 4.1 Activity Source Definitions

| Source Name | Location | Purpose |
|-------------|----------|---------|
| `DiscordBot.Bot` | `src/DiscordBot.Bot/Tracing/BotActivitySource.cs` | Discord command and component spans |
| `DiscordBot.Infrastructure` | `src/DiscordBot.Infrastructure/Tracing/InfrastructureActivitySource.cs` | Repository and database spans |

### 4.2 Span Naming Conventions

Following OpenTelemetry semantic conventions:

| Span Name Pattern | Example | When Used |
|-------------------|---------|-----------|
| `discord.command {name}` | `discord.command ping` | Slash command execution |
| `discord.component {type}` | `discord.component button` | Button/select/modal interaction |
| `db.{operation} {entity}` | `db.query GuildSettings` | Repository operations |

### 4.3 Standard Span Attributes

**Discord Command Spans:**

| Attribute | Type | Description |
|-----------|------|-------------|
| `discord.command.name` | string | Command name (e.g., "ping", "verify") |
| `discord.guild.id` | string | Guild snowflake ID |
| `discord.user.id` | string | User snowflake ID (for tracing, not PII) |
| `discord.interaction.id` | string | Discord interaction ID |
| `correlation.id` | string | Application correlation ID |
| `otel.status_code` | string | "OK" or "ERROR" |
| `error.message` | string | Error description if failed |

**Database Spans:**

| Attribute | Type | Description |
|-----------|------|-------------|
| `db.system` | string | "sqlite", "sqlserver", "postgresql" |
| `db.operation` | string | "SELECT", "INSERT", "UPDATE", "DELETE" |
| `db.entity.type` | string | Entity class name |
| `db.entity.id` | string | Entity ID (if applicable) |
| `db.duration.ms` | double | Operation duration |

### 4.4 Span Status Mapping

| Scenario | Status | Description |
|----------|--------|-------------|
| Command succeeds | `Ok` | Normal completion |
| Command fails (user error) | `Ok` | User-facing errors are not trace errors |
| Command fails (exception) | `Error` | System/infrastructure failures |
| DB operation succeeds | `Ok` | Normal completion |
| DB operation fails | `Error` | Include exception details |

---

## 5. File Structure

### 5.1 New Files to Create

```
src/DiscordBot.Bot/
  Tracing/
    BotActivitySource.cs           # Singleton ActivitySource for bot spans
    TracingConstants.cs            # Span names, attribute keys, source names

src/DiscordBot.Infrastructure/
  Tracing/
    InfrastructureActivitySource.cs # ActivitySource for infrastructure spans

docs/
  articles/
    distributed-tracing.md          # Tracing documentation
```

### 5.2 Files to Modify

| File | Changes |
|------|---------|
| `src/DiscordBot.Bot/DiscordBot.Bot.csproj` | Add tracing packages |
| `src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj` | Add DiagnosticSource package |
| `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs` | Add tracing configuration |
| `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` | Add command spans with context propagation |
| `src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs` | Add DB operation spans |
| `src/DiscordBot.Bot/Middleware/CorrelationIdMiddleware.cs` | Link correlation ID to trace context |
| `src/DiscordBot.Bot/Program.cs` | Register tracing services |
| `src/DiscordBot.Bot/appsettings.json` | Add tracing configuration |
| `src/DiscordBot.Bot/appsettings.Development.json` | Dev-specific tracing settings |

---

## 6. Implementation Details

### 6.1 BotActivitySource Class

**Location:** `src/DiscordBot.Bot/Tracing/BotActivitySource.cs`

```csharp
using System.Diagnostics;

namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Provides the ActivitySource for Discord bot tracing.
/// Follows the singleton pattern to ensure consistent source naming.
/// </summary>
public static class BotActivitySource
{
    /// <summary>
    /// The name of the activity source for Discord bot operations.
    /// </summary>
    public const string SourceName = "DiscordBot.Bot";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public static readonly string Version = typeof(BotActivitySource).Assembly
        .GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// The singleton ActivitySource instance for bot tracing.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, Version);

    /// <summary>
    /// Starts an activity for a Discord slash command execution.
    /// </summary>
    /// <param name="commandName">The name of the command being executed.</param>
    /// <param name="guildId">The guild ID where the command was invoked.</param>
    /// <param name="userId">The user ID who invoked the command.</param>
    /// <param name="interactionId">The Discord interaction ID.</param>
    /// <param name="correlationId">The application correlation ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartCommandActivity(
        string commandName,
        ulong? guildId,
        ulong userId,
        ulong interactionId,
        string correlationId)
    {
        var activity = Source.StartActivity(
            name: $"discord.command {commandName}",
            kind: ActivityKind.Server);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.CommandName, commandName);
        activity.SetTag(TracingConstants.Attributes.GuildId, guildId?.ToString() ?? "dm");
        activity.SetTag(TracingConstants.Attributes.UserId, userId.ToString());
        activity.SetTag(TracingConstants.Attributes.InteractionId, interactionId.ToString());
        activity.SetTag(TracingConstants.Attributes.CorrelationId, correlationId);

        // Add correlation ID as baggage for downstream propagation
        activity.AddBaggage(TracingConstants.Baggage.CorrelationId, correlationId);

        return activity;
    }

    /// <summary>
    /// Starts an activity for a Discord component interaction (button, select, modal).
    /// </summary>
    /// <param name="componentType">The type of component (button, select_menu, modal).</param>
    /// <param name="customId">The custom ID of the component.</param>
    /// <param name="guildId">The guild ID where the interaction occurred.</param>
    /// <param name="userId">The user ID who triggered the interaction.</param>
    /// <param name="interactionId">The Discord interaction ID.</param>
    /// <param name="correlationId">The application correlation ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartComponentActivity(
        string componentType,
        string customId,
        ulong? guildId,
        ulong userId,
        ulong interactionId,
        string correlationId)
    {
        var activity = Source.StartActivity(
            name: $"discord.component {componentType}",
            kind: ActivityKind.Server);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.ComponentType, componentType);
        activity.SetTag(TracingConstants.Attributes.ComponentId, SanitizeCustomId(customId));
        activity.SetTag(TracingConstants.Attributes.GuildId, guildId?.ToString() ?? "dm");
        activity.SetTag(TracingConstants.Attributes.UserId, userId.ToString());
        activity.SetTag(TracingConstants.Attributes.InteractionId, interactionId.ToString());
        activity.SetTag(TracingConstants.Attributes.CorrelationId, correlationId);

        return activity;
    }

    /// <summary>
    /// Records an error on the activity and sets error status.
    /// </summary>
    /// <param name="activity">The activity to record the error on.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.RecordException(exception);
    }

    /// <summary>
    /// Marks the activity as successful.
    /// </summary>
    /// <param name="activity">The activity to mark as successful.</param>
    public static void SetSuccess(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Sanitizes custom IDs to remove potentially sensitive data like user-specific correlation IDs.
    /// Extracts the handler:action portion for tracing.
    /// </summary>
    private static string SanitizeCustomId(string customId)
    {
        // Custom ID format: {handler}:{action}:{userId}:{correlationId}:{data}
        // We only want handler:action for the span attribute
        var parts = customId.Split(':');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}:{parts[1]}";
        }
        return customId;
    }
}
```

### 6.2 TracingConstants Class

**Location:** `src/DiscordBot.Bot/Tracing/TracingConstants.cs`

```csharp
namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Constants for distributed tracing span names, attributes, and baggage keys.
/// </summary>
public static class TracingConstants
{
    /// <summary>
    /// Activity source names.
    /// </summary>
    public static class Sources
    {
        public const string Bot = "DiscordBot.Bot";
        public const string Infrastructure = "DiscordBot.Infrastructure";
    }

    /// <summary>
    /// Span attribute keys following OpenTelemetry semantic conventions.
    /// </summary>
    public static class Attributes
    {
        // Discord-specific attributes
        public const string CommandName = "discord.command.name";
        public const string GuildId = "discord.guild.id";
        public const string UserId = "discord.user.id";
        public const string InteractionId = "discord.interaction.id";
        public const string ComponentType = "discord.component.type";
        public const string ComponentId = "discord.component.id";

        // Database attributes (following OTel semantic conventions)
        public const string DbSystem = "db.system";
        public const string DbOperation = "db.operation";
        public const string DbEntityType = "db.entity.type";
        public const string DbEntityId = "db.entity.id";
        public const string DbDurationMs = "db.duration.ms";

        // Application-specific
        public const string CorrelationId = "correlation.id";
        public const string ErrorMessage = "error.message";
    }

    /// <summary>
    /// Baggage keys for context propagation.
    /// </summary>
    public static class Baggage
    {
        public const string CorrelationId = "correlation-id";
    }

    /// <summary>
    /// Database operation names.
    /// </summary>
    public static class DbOperations
    {
        public const string Select = "SELECT";
        public const string Insert = "INSERT";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Count = "COUNT";
        public const string Exists = "EXISTS";
    }
}
```

### 6.3 InfrastructureActivitySource Class

**Location:** `src/DiscordBot.Infrastructure/Tracing/InfrastructureActivitySource.cs`

```csharp
using System.Diagnostics;

namespace DiscordBot.Infrastructure.Tracing;

/// <summary>
/// Provides the ActivitySource for infrastructure-level tracing (repositories, database operations).
/// </summary>
public static class InfrastructureActivitySource
{
    /// <summary>
    /// The name of the activity source for infrastructure operations.
    /// </summary>
    public const string SourceName = "DiscordBot.Infrastructure";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public static readonly string Version = typeof(InfrastructureActivitySource).Assembly
        .GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// The singleton ActivitySource instance for infrastructure tracing.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, Version);

    /// <summary>
    /// Attribute keys for database operations.
    /// </summary>
    public static class Attributes
    {
        public const string DbSystem = "db.system";
        public const string DbOperation = "db.operation";
        public const string DbEntityType = "db.entity.type";
        public const string DbEntityId = "db.entity.id";
        public const string DbDurationMs = "db.duration.ms";
    }

    /// <summary>
    /// Starts an activity for a repository operation.
    /// </summary>
    /// <param name="operationName">The repository method name (e.g., "GetByIdAsync").</param>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="dbOperation">The database operation (SELECT, INSERT, UPDATE, DELETE).</param>
    /// <param name="entityId">Optional entity ID for the operation.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartRepositoryActivity(
        string operationName,
        string entityType,
        string dbOperation,
        string? entityId = null)
    {
        var activity = Source.StartActivity(
            name: $"db.{dbOperation.ToLowerInvariant()} {entityType}",
            kind: ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(Attributes.DbOperation, dbOperation);
        activity.SetTag(Attributes.DbEntityType, entityType);

        if (!string.IsNullOrEmpty(entityId))
        {
            activity.SetTag(Attributes.DbEntityId, entityId);
        }

        // Inherit correlation ID from parent activity baggage
        var correlationId = Activity.Current?.GetBaggageItem("correlation-id");
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity.SetTag("correlation.id", correlationId);
        }

        return activity;
    }

    /// <summary>
    /// Records the duration and marks the activity as complete.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="durationMs">The operation duration in milliseconds.</param>
    public static void CompleteActivity(Activity? activity, double durationMs)
    {
        if (activity is null)
            return;

        activity.SetTag(Attributes.DbDurationMs, durationMs);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records an exception and marks the activity as failed.
    /// </summary>
    /// <param name="activity">The activity to record the error on.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="durationMs">The operation duration in milliseconds.</param>
    public static void RecordException(Activity? activity, Exception exception, double durationMs)
    {
        if (activity is null)
            return;

        activity.SetTag(Attributes.DbDurationMs, durationMs);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.RecordException(exception);
    }
}
```

### 6.4 OpenTelemetryExtensions Update

**Location:** `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs`

Add the following method alongside the existing `AddOpenTelemetryMetrics`:

```csharp
using DiscordBot.Bot.Tracing;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

// ... existing using statements and code ...

/// <summary>
/// Adds OpenTelemetry distributed tracing to the service collection.
/// </summary>
/// <param name="services">The service collection.</param>
/// <param name="configuration">The application configuration.</param>
/// <returns>The service collection for chaining.</returns>
public static IServiceCollection AddOpenTelemetryTracing(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "discordbot";
    var serviceVersion = configuration["OpenTelemetry:ServiceVersion"]
        ?? typeof(OpenTelemetryExtensions).Assembly
            .GetName().Version?.ToString() ?? "1.0.0";

    // Determine sampling ratio based on environment
    var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
    var samplingRatio = configuration.GetValue<double?>("OpenTelemetry:Tracing:SamplingRatio")
        ?? (isProduction ? 0.1 : 1.0);

    var enableConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:Tracing:EnableConsoleExporter");
    var otlpEndpoint = configuration["OpenTelemetry:Tracing:OtlpEndpoint"];

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName))
        .WithTracing(tracing =>
        {
            // Add custom activity sources
            tracing.AddSource(BotActivitySource.SourceName);
            tracing.AddSource("DiscordBot.Infrastructure");

            // Add ASP.NET Core instrumentation for HTTP requests
            tracing.AddAspNetCoreInstrumentation(options =>
            {
                // Filter out health checks, metrics, and static files
                options.Filter = httpContext =>
                {
                    var path = httpContext.Request.Path.Value ?? "";
                    return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                        && !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
                        && !path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
                        && !path.Contains('.'); // Filter static files
                };

                // Enrich spans with additional request info
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    // Add correlation ID if present
                    if (request.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
                    {
                        activity.SetTag("correlation.id", correlationId?.ToString());
                    }
                };
            });

            // Add HTTP client instrumentation for outgoing calls (Discord API, etc.)
            tracing.AddHttpClientInstrumentation(options =>
            {
                // Redact sensitive headers
                options.FilterHttpRequestMessage = request =>
                {
                    // Filter out internal health checks
                    return request.RequestUri?.Host != "localhost";
                };
            });

            // Add Entity Framework Core instrumentation
            tracing.AddEntityFrameworkCoreInstrumentation(options =>
            {
                // Only include SQL text in non-production for security
                options.SetDbStatementForText = !isProduction;
                options.SetDbStatementForStoredProcedure = !isProduction;
            });

            // Configure sampler
            if (samplingRatio < 1.0)
            {
                tracing.SetSampler(new TraceIdRatioBasedSampler(samplingRatio));
            }

            // Add console exporter for development
            if (enableConsoleExporter)
            {
                tracing.AddConsoleExporter();
            }

            // Add OTLP exporter if configured
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);

                    // Use gRPC by default, can be configured via settings
                    var protocol = configuration["OpenTelemetry:Tracing:OtlpProtocol"];
                    if (protocol?.Equals("http", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    }
                });
            }
        });

    return services;
}
```

### 6.5 InteractionHandler Instrumentation

**Location:** `src/DiscordBot.Bot/Handlers/InteractionHandler.cs`

Modify the `OnInteractionCreatedAsync` method to wrap execution in a trace span:

```csharp
using DiscordBot.Bot.Tracing;

// ... existing code ...

/// <summary>
/// Called when an interaction is created (slash command, button click, etc.).
/// Creates a context and executes the corresponding command.
/// </summary>
private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
{
    // Generate correlation ID for tracking this request
    var correlationId = Guid.NewGuid().ToString("N")[..16];
    var stopwatch = Stopwatch.StartNew();

    string? commandName = null;
    Activity? activity = null;

    // Extract command name and start tracing activity
    if (interaction is SocketSlashCommand slashCommand)
    {
        commandName = slashCommand.CommandName;
        _botMetrics.IncrementActiveCommands(commandName);

        // Start tracing activity for the command
        activity = BotActivitySource.StartCommandActivity(
            commandName: commandName,
            guildId: interaction.GuildId,
            userId: interaction.User.Id,
            interactionId: interaction.Id,
            correlationId: correlationId);
    }
    else if (interaction is SocketMessageComponent component)
    {
        // Start tracing activity for component interaction
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
    else if (interaction is SocketModal modal)
    {
        // Start tracing activity for modal submission
        activity = BotActivitySource.StartComponentActivity(
            componentType: "modal",
            customId: modal.Data.CustomId,
            guildId: interaction.GuildId,
            userId: interaction.User.Id,
            interactionId: interaction.Id,
            correlationId: correlationId);
    }

    // Store execution context for use in OnSlashCommandExecutedAsync
    _executionContext.Value = new ExecutionContext
    {
        CorrelationId = correlationId,
        Stopwatch = stopwatch,
        CommandName = commandName
    };

    try
    {
        // Create an execution context
        var context = new SocketInteractionContext(_client, interaction);

        // Use logging scope for correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["InteractionId"] = interaction.Id,
            ["TraceId"] = activity?.TraceId.ToString() ?? "none"
        }))
        {
            _logger.LogDebug(
                "Executing interaction {InteractionType} with correlation ID {CorrelationId}, TraceId {TraceId}",
                interaction.Type,
                correlationId,
                activity?.TraceId.ToString() ?? "none");

            // Execute the command
            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }

        // Mark activity as successful if no exceptions
        BotActivitySource.SetSuccess(activity);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        // Record exception on the tracing activity
        BotActivitySource.RecordException(activity, ex);

        // Record failure metric
        if (commandName != null)
        {
            _botMetrics.RecordCommandExecution(
                commandName,
                success: false,
                durationMs: stopwatch.Elapsed.TotalMilliseconds,
                guildId: interaction.GuildId);
        }

        _logger.LogError(
            ex,
            "Error executing interaction {InteractionId}, CorrelationId: {CorrelationId}, TraceId: {TraceId}",
            interaction.Id,
            correlationId,
            activity?.TraceId.ToString() ?? "none");

        // If the interaction hasn't been responded to, send an error message
        if (interaction.Type == InteractionType.ApplicationCommand)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while executing this command.")
                .WithColor(Color.Red)
                .WithFooter($"Correlation ID: {correlationId}")
                .WithCurrentTimestamp()
                .Build();

            if (interaction.HasResponded)
            {
                await interaction.FollowupAsync(embed: embed, ephemeral: true);
            }
            else
            {
                await interaction.RespondAsync(embed: embed, ephemeral: true);
            }
        }
    }
    finally
    {
        // Dispose the activity (completes the span)
        activity?.Dispose();

        // Decrement active command count
        if (commandName != null)
        {
            _botMetrics.DecrementActiveCommands(commandName);
        }

        // Clear execution context
        _executionContext.Value = null!;
    }
}
```

### 6.6 Repository Instrumentation

**Location:** `src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs`

Modify repository methods to include tracing spans. Here's the updated `GetByIdAsync` as an example:

```csharp
using DiscordBot.Infrastructure.Tracing;

// ... existing code ...

public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
{
    // Start tracing activity
    using var activity = InfrastructureActivitySource.StartRepositoryActivity(
        operationName: "GetByIdAsync",
        entityType: _entityTypeName,
        dbOperation: "SELECT",
        entityId: id?.ToString());

    var stopwatch = Stopwatch.StartNew();
    Logger.LogDebug("Repository<{EntityType}>.GetByIdAsync starting. Id={Id}", _entityTypeName, id);

    try
    {
        var result = await DbSet.FindAsync(new[] { id }, cancellationToken);
        stopwatch.Stop();

        Logger.LogDebug(
            "Repository<{EntityType}>.GetByIdAsync completed in {ElapsedMs}ms. Found={Found}",
            _entityTypeName, stopwatch.ElapsedMilliseconds, result != null);

        if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
        {
            Logger.LogWarning(
                "Repository<{EntityType}>.GetByIdAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Id={Id}",
                _entityTypeName, stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, id);
        }

        // Complete tracing activity with success
        InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        // Record exception on tracing activity
        InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

        Logger.LogError(ex,
            "Repository<{EntityType}>.GetByIdAsync failed. Id={Id}, ElapsedMs={ElapsedMs}, Error={Error}",
            _entityTypeName, id, stopwatch.ElapsedMilliseconds, ex.Message);
        throw;
    }
}

// Similar pattern for other methods:
// - GetAllAsync: operation=SELECT, no entityId
// - FindAsync: operation=SELECT, no entityId
// - AddAsync: operation=INSERT, entityId from GetEntityId
// - UpdateAsync: operation=UPDATE, entityId from GetEntityId
// - DeleteAsync: operation=DELETE, entityId from GetEntityId
// - ExistsAsync: operation=EXISTS, no entityId
// - CountAsync: operation=COUNT, no entityId
```

### 6.7 CorrelationIdMiddleware Update

**Location:** `src/DiscordBot.Bot/Middleware/CorrelationIdMiddleware.cs`

Add trace context linking:

```csharp
using System.Diagnostics;
using Serilog.Context;

namespace DiscordBot.Bot.Middleware;

/// <summary>
/// Middleware that extracts or generates correlation IDs for API requests and propagates them through logs, response headers, and trace context.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract correlation ID from request headers or generate a new one
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = GenerateCorrelationId();
            _logger.LogTrace("Generated new correlation ID: {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogTrace("Using existing correlation ID from request: {CorrelationId}", correlationId);
        }

        // Store correlation ID in HttpContext.Items for access by controllers and other middleware
        context.Items[ItemKey] = correlationId;

        // Add correlation ID to response headers
        context.Response.Headers[HeaderName] = correlationId;

        // Link correlation ID to current trace activity if one exists
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation.id", correlationId);
            activity.AddBaggage("correlation-id", correlationId);
        }

        // Push correlation ID and trace info to Serilog LogContext for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", activity?.TraceId.ToString() ?? "none"))
        using (LogContext.PushProperty("SpanId", activity?.SpanId.ToString() ?? "none"))
        {
            await _next(context);
        }
    }

    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }
}
```

### 6.8 Program.cs Modifications

**Location:** `src/DiscordBot.Bot/Program.cs`

Add tracing configuration after the metrics configuration:

```csharp
// Add OpenTelemetry metrics
builder.Services.AddOpenTelemetryMetrics(builder.Configuration);

// Add OpenTelemetry tracing
builder.Services.AddOpenTelemetryTracing(builder.Configuration);
```

### 6.9 Configuration Updates

**Location:** `src/DiscordBot.Bot/appsettings.json`

Add tracing section to the OpenTelemetry configuration:

```json
{
  "OpenTelemetry": {
    "ServiceName": "discordbot",
    "ServiceVersion": "0.1.0",
    "Metrics": {
      "Enabled": true,
      "IncludeRuntimeMetrics": true,
      "IncludeHttpMetrics": true
    },
    "Tracing": {
      "Enabled": true,
      "SamplingRatio": 1.0,
      "EnableConsoleExporter": false,
      "OtlpEndpoint": null,
      "OtlpProtocol": "grpc"
    }
  }
}
```

**Location:** `src/DiscordBot.Bot/appsettings.Development.json`

Add development-specific tracing settings:

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 1.0,
      "EnableConsoleExporter": true
    }
  }
}
```

**Location:** `src/DiscordBot.Bot/appsettings.Production.json`

Add production-specific tracing settings:

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 0.1,
      "EnableConsoleExporter": false,
      "OtlpEndpoint": "http://jaeger:4317"
    }
  }
}
```

---

## 7. Subagent Task Plan

### 7.1 dotnet-specialist

**Primary implementer for all development tasks:**

| Task | Description | Effort |
|------|-------------|--------|
| 7.1.1 | Add NuGet packages to csproj files | 15 min |
| 7.1.2 | Create `Tracing/BotActivitySource.cs` | 45 min |
| 7.1.3 | Create `Tracing/TracingConstants.cs` | 20 min |
| 7.1.4 | Create `Tracing/InfrastructureActivitySource.cs` (Infrastructure project) | 30 min |
| 7.1.5 | Add `AddOpenTelemetryTracing()` to OpenTelemetryExtensions.cs | 1 hour |
| 7.1.6 | Instrument `InteractionHandler.cs` with command spans | 1.5 hours |
| 7.1.7 | Instrument `Repository.cs` with database spans (all 7 methods) | 1.5 hours |
| 7.1.8 | Update `CorrelationIdMiddleware.cs` for trace context linking | 30 min |
| 7.1.9 | Update `Program.cs` with tracing configuration | 15 min |
| 7.1.10 | Update configuration files (appsettings.json, .Development.json, .Production.json) | 20 min |
| 7.1.11 | Add unit tests for BotActivitySource | 1 hour |
| 7.1.12 | Add unit tests for InfrastructureActivitySource | 45 min |
| 7.1.13 | Integration testing with Jaeger/console exporter | 1 hour |

**Total estimated effort:** 9-10 hours

### 7.2 docs-writer

| Task | Description | Effort |
|------|-------------|--------|
| 7.2.1 | Create `docs/articles/distributed-tracing.md` documentation | 2 hours |
| 7.2.2 | Add Jaeger setup guide for local development | 1 hour |
| 7.2.3 | Add Application Insights setup guide for Azure deployment | 1 hour |
| 7.2.4 | Update API documentation with trace context headers | 30 min |

**Total estimated effort:** 4-5 hours

### 7.3 design-specialist

Not required for this feature - no UI changes.

### 7.4 html-prototyper

Not required for this feature - no new pages.

---

## 8. Timeline / Dependency Map

```
Day 1 (Core Implementation)
├── 7.1.1 NuGet packages (no dependencies)
├── 7.1.2 BotActivitySource.cs (depends on packages)
├── 7.1.3 TracingConstants.cs (no dependencies)
├── 7.1.4 InfrastructureActivitySource.cs (depends on packages)
├── 7.1.5 OpenTelemetryExtensions.cs (depends on activity sources)
└── 7.1.9 Program.cs updates (depends on extensions)

Day 2 (Instrumentation + Configuration)
├── 7.1.6 InteractionHandler instrumentation (depends on BotActivitySource)
├── 7.1.7 Repository instrumentation (depends on InfrastructureActivitySource)
├── 7.1.8 CorrelationIdMiddleware update (no dependencies)
├── 7.1.10 Configuration files (no dependencies)
└── 7.1.13 Integration testing (depends on all above)

Day 2-3 (Testing + Documentation - can run in parallel)
├── 7.1.11 BotActivitySource unit tests
├── 7.1.12 InfrastructureActivitySource unit tests
├── 7.2.1 Distributed tracing documentation
├── 7.2.2 Jaeger setup guide
├── 7.2.3 Application Insights guide
└── 7.2.4 API documentation update
```

**Parallelization Opportunities:**
- BotActivitySource and InfrastructureActivitySource can be developed in parallel
- Documentation can begin once core implementation is complete
- Unit tests can be written alongside implementation
- Configuration files can be updated at any time

---

## 9. Acceptance Criteria

### 9.1 Activity Sources

- [ ] `BotActivitySource` is a static singleton with `DiscordBot.Bot` source name
- [ ] `InfrastructureActivitySource` is a static singleton with `DiscordBot.Infrastructure` source name
- [ ] Both sources provide helper methods for starting activities with proper attributes

### 9.2 Command Tracing

- [ ] All slash command executions create a parent span
- [ ] Span includes command name, guild ID, user ID, interaction ID
- [ ] Correlation ID is attached as span attribute and baggage
- [ ] Span status is set to OK on success, ERROR on exception
- [ ] Exception details are recorded on failure

### 9.3 Component Tracing

- [ ] Button interactions create spans with component type "button"
- [ ] Select menu interactions create spans with component type "select_menu"
- [ ] Modal submissions create spans with component type "modal"
- [ ] Custom IDs are sanitized to remove user-specific data

### 9.4 Database Tracing

- [ ] All repository methods create child spans
- [ ] Spans include operation type (SELECT, INSERT, UPDATE, DELETE)
- [ ] Spans include entity type and entity ID where applicable
- [ ] Duration is recorded on span attributes
- [ ] EF Core queries are automatically traced (development mode)

### 9.5 Context Propagation

- [ ] Correlation ID flows from middleware to spans
- [ ] Trace context propagates through async command execution
- [ ] Child spans correctly parent to command spans
- [ ] Baggage items are accessible in downstream services

### 9.6 Sampling

- [ ] 100% sampling in development environment
- [ ] 10% sampling in production environment (configurable)
- [ ] Sampling ratio is configurable via appsettings.json

### 9.7 Export

- [ ] Console exporter works for local development
- [ ] OTLP exporter connects to Jaeger successfully
- [ ] Traces appear in Jaeger UI with correct hierarchy
- [ ] Azure Application Insights receives traces (if configured)

### 9.8 Documentation

- [ ] Distributed tracing architecture is documented
- [ ] All span attributes are documented
- [ ] Jaeger local setup guide is provided
- [ ] Azure Application Insights setup is documented
- [ ] Troubleshooting guide for common issues

---

## 10. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| EF Core instrumentation beta instability | Low | Medium | Pin to specific beta version; monitor for issues |
| Trace context lost across async boundaries | Medium | High | Use Activity.Current and proper async patterns |
| High trace volume in production | Medium | Medium | Aggressive sampling (10%); adjust based on costs |
| Sensitive data in traces | Low | High | Never include PII; sanitize custom IDs |
| Performance overhead from tracing | Low | Medium | Spans are lightweight (~microseconds each) |
| OTLP endpoint connection failures | Medium | Low | Graceful degradation; traces are best-effort |
| Cardinality explosion from attributes | Low | Medium | Use static span names; attributes for variability |

---

## 11. Testing Strategy

### 11.1 Unit Tests

**Location:** `tests/DiscordBot.Tests/Tracing/`

```csharp
// BotActivitySourceTests.cs
[Fact]
public void StartCommandActivity_CreatesActivityWithCorrectAttributes()
{
    // Arrange
    using var listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == BotActivitySource.SourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    };
    ActivitySource.AddActivityListener(listener);

    // Act
    using var activity = BotActivitySource.StartCommandActivity(
        commandName: "ping",
        guildId: 123456789,
        userId: 987654321,
        interactionId: 111222333,
        correlationId: "abc123");

    // Assert
    Assert.NotNull(activity);
    Assert.Equal("discord.command ping", activity.OperationName);
    Assert.Equal("ping", activity.GetTagItem("discord.command.name"));
    Assert.Equal("123456789", activity.GetTagItem("discord.guild.id"));
    Assert.Equal("abc123", activity.GetTagItem("correlation.id"));
}

[Fact]
public void RecordException_SetsErrorStatusAndRecordsEvent()
{
    // Arrange
    using var listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == BotActivitySource.SourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    };
    ActivitySource.AddActivityListener(listener);

    using var activity = BotActivitySource.StartCommandActivity(
        "test", null, 1, 1, "corr");

    var exception = new InvalidOperationException("Test error");

    // Act
    BotActivitySource.RecordException(activity, exception);

    // Assert
    Assert.Equal(ActivityStatusCode.Error, activity!.Status);
    Assert.Contains(activity.Events, e => e.Name == "exception");
}
```

### 11.2 Integration Tests

```csharp
// TracingIntegrationTests.cs
[Fact]
public async Task CommandExecution_CreatesTraceWithChildSpans()
{
    // Arrange
    var exportedActivities = new List<Activity>();
    using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource(BotActivitySource.SourceName)
        .AddSource("DiscordBot.Infrastructure")
        .AddInMemoryExporter(exportedActivities)
        .Build();

    // Act - Simulate command execution with repository call
    using var commandActivity = BotActivitySource.StartCommandActivity(
        "settings", 123, 456, 789, "corr123");

    using var dbActivity = InfrastructureActivitySource.StartRepositoryActivity(
        "GetByIdAsync", "GuildSettings", "SELECT", "123");

    InfrastructureActivitySource.CompleteActivity(dbActivity, 15.0);
    BotActivitySource.SetSuccess(commandActivity);

    // Force export
    tracerProvider.ForceFlush();

    // Assert
    Assert.Equal(2, exportedActivities.Count);

    var parentSpan = exportedActivities.First(a => a.OperationName.Contains("discord.command"));
    var childSpan = exportedActivities.First(a => a.OperationName.Contains("db.select"));

    Assert.Equal(parentSpan.TraceId, childSpan.TraceId);
    Assert.Equal(parentSpan.SpanId, childSpan.ParentSpanId);
}
```

### 11.3 Manual Testing Checklist

- [ ] Start application and verify no startup errors related to tracing
- [ ] Execute slash command and verify console shows trace output (dev mode)
- [ ] Check that trace includes command name, guild ID, correlation ID
- [ ] Execute command that queries database and verify child spans appear
- [ ] Trigger an error and verify exception is recorded on span
- [ ] Start Jaeger locally and verify traces appear in UI
- [ ] Verify trace hierarchy: command span -> repository span -> EF Core span
- [ ] Check sampling by switching to production mode and verifying 10% rate
- [ ] Verify correlation ID appears in both logs and traces

---

## 12. File Summary

### New Files to Create

| File | Purpose |
|------|---------|
| `src/DiscordBot.Bot/Tracing/BotActivitySource.cs` | Static ActivitySource for bot spans |
| `src/DiscordBot.Bot/Tracing/TracingConstants.cs` | Constants for span names and attributes |
| `src/DiscordBot.Infrastructure/Tracing/InfrastructureActivitySource.cs` | Static ActivitySource for infrastructure spans |
| `docs/articles/distributed-tracing.md` | Tracing documentation |
| `tests/DiscordBot.Tests/Tracing/BotActivitySourceTests.cs` | Unit tests |
| `tests/DiscordBot.Tests/Tracing/InfrastructureActivitySourceTests.cs` | Unit tests |

### Files to Modify

| File | Changes |
|------|---------|
| `src/DiscordBot.Bot/DiscordBot.Bot.csproj` | Add OTLP exporter, console exporter, EF Core instrumentation packages |
| `src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj` | Add DiagnosticSource package (if not transitive) |
| `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs` | Add `AddOpenTelemetryTracing()` method |
| `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` | Add command and component span creation |
| `src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs` | Add spans to all 7 repository methods |
| `src/DiscordBot.Bot/Middleware/CorrelationIdMiddleware.cs` | Link correlation ID to trace context |
| `src/DiscordBot.Bot/Program.cs` | Call `AddOpenTelemetryTracing()` |
| `src/DiscordBot.Bot/appsettings.json` | Add Tracing configuration section |
| `src/DiscordBot.Bot/appsettings.Development.json` | Enable console exporter |
| `src/DiscordBot.Bot/appsettings.Production.json` | Configure sampling and OTLP endpoint |

---

## 13. Jaeger Local Development Setup

For local development with Jaeger:

```bash
# Start Jaeger all-in-one container
docker run -d --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:latest

# Access Jaeger UI at http://localhost:16686
```

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

---

## 14. Azure Application Insights Setup

For Azure deployment with Application Insights:

```bash
# Install Azure Monitor exporter
dotnet add package Azure.Monitor.OpenTelemetry.Exporter
```

Add to `OpenTelemetryExtensions.cs`:
```csharp
var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    tracing.AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
```

---

## 15. Example Trace Output

When viewing a trace in Jaeger, you should see:

```
discord.command ping [2.5ms]
├── discord.guild.id: 123456789012345678
├── discord.user.id: 987654321098765432
├── discord.interaction.id: 111222333444555666
├── correlation.id: a1b2c3d4e5f6g7h8
│
└── db.select GuildSettings [1.2ms]
    ├── db.operation: SELECT
    ├── db.entity.type: GuildSettings
    ├── db.entity.id: 123456789012345678
    ├── db.duration.ms: 1.2
    │
    └── Microsoft.EntityFrameworkCore [0.8ms]
        ├── db.system: sqlite
        ├── db.statement: SELECT ... FROM GuildSettings WHERE Id = @p0
        └── db.name: discordbot.db
```

---

*Document prepared by: Systems Architect Agent*
*Review status: Ready for implementation*
