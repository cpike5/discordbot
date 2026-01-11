# Elastic APM Integration

**Version:** 1.0
**Last Updated:** 2026-01-06
**Status:** Active

---

## Overview

Elastic APM (Application Performance Monitoring) provides distributed tracing for the Discord bot, enabling performance monitoring, error tracking, and end-to-end request visibility. The bot uses the official [Elastic APM .NET Agent](https://www.elastic.co/guide/en/apm/agent/dotnet/current/index.html) to automatically instrument HTTP requests, database queries, and Discord operations.

**Key Features:**

- Automatic transaction creation for each Discord command and interaction
- Priority-based sampling to optimize storage costs while capturing critical operations
- Rich contextual labels (command name, user ID, guild ID, correlation ID)
- Custom span attributes for Discord API calls, auto-moderation events, and background services
- Correlation between logs and traces via `trace.id` field
- Performance analytics and error tracking in Kibana APM UI

---

## How APM Works

Elastic APM captures application performance data through three core concepts:

### Transactions

**Transactions** represent complete units of work, such as handling a Discord command or processing an HTTP request. Each transaction has:

- **Name:** Identifies the operation (e.g., `discord.command ping`, `GET /api/guilds`)
- **Type:** Categorizes the transaction (e.g., `discord.command`, `request`, `background`)
- **Duration:** Total time from start to finish
- **Result:** Success, failure, or HTTP status code
- **Labels:** Key-value pairs for filtering and grouping (command name, user ID, guild ID)

**Example Discord Command Transaction:**

```
Name: discord.command rat-stats
Type: discord.command
Duration: 234ms
Result: success
Labels:
  - command_name: rat-stats
  - correlation_id: a1b2c3d4e5f6g7h8
  - user_id: 123456789012345678
  - guild_id: 987654321098765432
```

### Spans

**Spans** represent individual operations within a transaction, such as database queries, Discord API calls, or service method invocations. Spans provide granular visibility into where time is spent during transaction execution.

**Example Span Hierarchy:**

```
Transaction: discord.command rat-stats (234ms)
├─ Span: service.ratwatch.get_stats (180ms)
│  ├─ Span: db.select RatWatchIncident (120ms)
│  └─ Span: db.select RatWatchVote (55ms)
└─ Span: discord.api.POST /interactions/{id}/callback (50ms)
```

### Trace Correlation

All transactions and spans within a request flow share a **trace ID**, enabling correlation between:

- Application logs (Serilog with `trace.id` field)
- APM transactions and spans
- Distributed operations across services (future multi-service deployments)

---

## Configuration

### appsettings.json Options

The `ElasticApm` section in `appsettings.json` configures the APM agent:

```json
{
  "ElasticApm": {
    "ServerUrl": null,
    "ServiceName": "discordbot",
    "Environment": "development",
    "SecretToken": null,
    "TransactionSampleRate": 1.0,
    "CaptureBody": "off",
    "CaptureHeaders": true,
    "StackTraceLimit": 50,
    "SpanStackTraceMinDuration": "5ms",
    "Recording": true,
    "Enabled": true,
    "TransactionIgnoreUrls": "/health*,/metrics*,/swagger*",
    "UseElasticTraceparentHeader": true
  }
}
```

> **Note:** `ServiceVersion` is automatically derived from the assembly version defined in `Directory.Build.props`. Do not hardcode it in configuration files.

**Configuration Options:**

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ServerUrl` | string | null | APM server URL (e.g., `http://localhost:8200`). **Required** if APM is enabled. |
| `SecretToken` | string | null | APM secret token for authentication. Use user secrets or environment variables in production. |
| `ServiceName` | string | `discordbot` | Service name displayed in Kibana APM UI. |
| `ServiceVersion` | string | (assembly) | Service version for deployment tracking. **Automatically set from assembly version.** |
| `Environment` | string | `development` | Environment name (`development`, `staging`, `production`). |
| `TransactionSampleRate` | double | 1.0 | Global sampling rate (0.0 to 1.0). **Note:** Overridden by priority-based filter. |
| `CaptureBody` | string | `off` | Capture HTTP request/response bodies: `off`, `errors`, `transactions`, `all`. |
| `CaptureHeaders` | bool | true | Capture HTTP request/response headers. |
| `StackTraceLimit` | int | 50 | Maximum stack trace frames to capture for errors. |
| `SpanStackTraceMinDuration` | string | `5ms` | Minimum span duration to capture stack traces (reduces overhead). |
| `Recording` | bool | true | Enable/disable recording of transactions and spans. |
| `Enabled` | bool | true | Master switch for APM agent. Set to `false` to completely disable APM. |
| `TransactionIgnoreUrls` | string | `/health*,/metrics*,/swagger*` | Comma-separated URL patterns to ignore (wildcards supported). |
| `UseElasticTraceparentHeader` | bool | true | Use Elastic-specific `traceparent` header for distributed tracing. |

### User Secrets (Development)

Configure APM credentials via user secrets for local development:

```bash
cd src/DiscordBot.Bot

# APM Server URL (local APM server via Docker)
dotnet user-secrets set "ElasticApm:ServerUrl" "http://localhost:8200"

# APM Secret Token (if authentication is enabled on APM server)
dotnet user-secrets set "ElasticApm:SecretToken" "your-apm-secret-token"
```

**Disable APM Locally (Optional):**

If you don't want to run APM server during local development, disable it in `appsettings.Development.json`:

```json
{
  "ElasticApm": {
    "Enabled": false
  }
}
```

### Environment-Specific Configuration

#### Development (appsettings.Development.json)

```json
{
  "ElasticApm": {
    "ServerUrl": "http://localhost:8200",
    "ServiceName": "discordbot",
    "Environment": "development",
    "TransactionSampleRate": 1.0,
    "Enabled": true,
    "Recording": true
  },
  "OpenTelemetry": {
    "Tracing": {
      "Sampling": {
        "DefaultRate": 1.0
      }
    }
  }
}
```

**Development defaults:**
- Sample 100% of transactions for full visibility during debugging
- Point to local APM server on `localhost:8200`

#### Production (Environment Variables)

Use environment variables to override sensitive configuration in production:

```bash
# Linux/macOS
export ElasticApm__ServerUrl="https://apm.yourdomain.com:8200"
export ElasticApm__SecretToken="your-production-apm-token"
export ElasticApm__Environment="production"
export ElasticApm__TransactionSampleRate="0.1"

# Windows PowerShell
$env:ElasticApm__ServerUrl="https://apm.yourdomain.com:8200"
$env:ElasticApm__SecretToken="your-production-apm-token"
$env:ElasticApm__Environment="production"
$env:ElasticApm__TransactionSampleRate="0.1"

# Docker Compose
services:
  discordbot:
    image: discordbot:latest
    environment:
      - ElasticApm__ServerUrl=https://apm.yourdomain.com:8200
      - ElasticApm__SecretToken=your-production-apm-token
      - ElasticApm__Environment=production
      - ElasticApm__TransactionSampleRate=0.1
```

**Production recommendations:**
- Reduce sampling rate to 10% (`DefaultRate: 0.1`) to lower storage costs
- Use HTTPS for APM server URL
- Store secret token in environment variables or secrets manager (never in appsettings.json)

---

## Priority-Based Sampling

The application implements **priority-based sampling** via `ElasticApmTransactionFilter` to optimize APM storage costs while ensuring critical transactions are always captured. This filter uses the same sampling logic as the OpenTelemetry `PrioritySampler` for consistency across observability platforms.

### Sampling Tiers

| Priority | Sample Rate | Operations |
|----------|-------------|------------|
| **Always Sample** | 100% | Rate limit hits (`discord.api.rate_limit.remaining == 0`), Discord API errors (`discord.api.error.*`), auto-moderation detections (`automod.*`) |
| **High Priority** | 50% (configurable) | Welcome flow (`member.joined`, `welcome.*`), moderation actions (`/warn`, `/kick`, `/ban`, `/mute`), Rat Watch operations (`ratwatch`, `rat-*`), scheduled messages |
| **Default** | 10% (configurable) | Normal operations (most Discord commands, API requests, database queries) |
| **Low Priority** | 1% (configurable) | Health checks (`/health`), metrics scraping (`/metrics`), high-frequency cache operations |

### How It Works

1. **Transaction Filter Registration:** The `ElasticApmFilterRegistrationService` background service registers the `ElasticApmTransactionFilter` with the APM agent on application startup.

2. **Sampling Decision:** For each transaction, the filter inspects the transaction name, type, and labels to determine priority tier.

3. **Random Sampling:** A random number is generated and compared against the tier's sampling rate. If the number exceeds the rate, the transaction is dropped.

4. **Label Addition:** Sampled transactions receive `sampling.rate` and `sampling.decision` labels for observability.

**Filter Implementation (ElasticApmTransactionFilter.cs):**

```csharp
// Always sample critical operations (100%)
if (IsAlwaysSampleOperation(name, transaction))
{
    return 1.0;
}

// High priority operations (50% by default)
if (IsHighPriorityOperation(name, type, transaction))
{
    return _options.HighPriorityRate;
}

// Low priority operations (1% by default)
if (IsLowPriorityOperation(name, type))
{
    return _options.LowPriorityRate;
}

// Default rate (10% in production, 100% in dev)
return _options.DefaultRate;
```

### Configuring Sampling Rates

Sampling rates are configured via the `OpenTelemetry:Tracing:Sampling` section (shared with OpenTelemetry sampler):

```json
{
  "OpenTelemetry": {
    "Tracing": {
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

**Sampling Options:**

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DefaultRate` | double | 0.1 | Sampling rate for normal operations (10% in production, 100% in dev). |
| `ErrorRate` | double | 1.0 | Sampling rate for error transactions (always 100%). |
| `SlowThresholdMs` | int | 5000 | Slow operation threshold in milliseconds. Operations exceeding this are sampled at `ErrorRate`. |
| `HighPriorityRate` | double | 0.5 | Sampling rate for high-priority operations (50%). |
| `LowPriorityRate` | double | 0.01 | Sampling rate for low-priority operations (1%). |

### Benefits of Priority-Based Sampling

- **Cost Optimization:** Reduces APM storage costs by 90% in production while maintaining observability.
- **Critical Event Coverage:** Ensures 100% capture of errors, rate limits, and security events.
- **Business Operation Visibility:** High sampling rate for important user flows (welcome, moderation, Rat Watch).
- **Noise Reduction:** Minimizes storage of high-frequency, low-value operations (health checks, cache hits).

---

## Discord Command Transactions

Each Discord command execution creates its own Elastic APM transaction, enabling independent tracking and analysis of command performance. This is implemented in the `InteractionHandler` class:

```csharp
// Start Elastic APM transaction for this command execution
// This ensures each command gets its own transaction rather than sharing the bot startup transaction
apmTransaction = Agent.Tracer.StartTransaction(transactionName, transactionType);
apmTransaction.SetLabel("correlation_id", correlationId);
apmTransaction.SetLabel("interaction_id", interaction.Id.ToString());
apmTransaction.SetLabel("user_id", interaction.User.Id.ToString());
apmTransaction.SetLabel("guild_id", guildId);
```

### Transaction Naming

| Interaction Type | Transaction Name | Transaction Type |
|-----------------|------------------|------------------|
| Slash Command | `discord.command {commandName}` | `discord.command` |
| Button Click | `discord.component button` | `discord.component` |
| Select Menu | `discord.component select_menu` | `discord.component` |
| Modal Submit | `discord.component modal` | `discord.component` |
| Other | `discord.interaction {type}` | `discord.interaction` |

**Examples:**

- `/ping` command → `discord.command ping`
- `/rat-stats` command → `discord.command rat-stats`
- Rat Watch voting button → `discord.component button`

### APM Labels on Command Transactions

Every Discord command transaction includes the following labels for filtering and analysis:

| Label | Description | Example Value |
|-------|-------------|---------------|
| `command_name` | The slash command name | `ping`, `rat-stats`, `admin info` |
| `correlation_id` | Application correlation ID for request tracing | `a1b2c3d4e5f6g7h8` |
| `interaction_id` | Discord interaction snowflake ID | `1234567890123456789` |
| `user_id` | Discord user snowflake ID | `987654321098765432` |
| `guild_id` | Discord guild snowflake ID (or `dm` for direct messages) | `111222333444555666` |

### Querying Command Transactions in Kibana

Navigate to **Observability → APM → Services → discordbot → Transactions** and use these filters:

```
# Find all executions of a specific command
labels.command_name: "rat-stats"

# Find commands in a specific guild
labels.guild_id: "123456789012345678"

# Find failed command transactions
transaction.result: "failure"

# Find slow commands (>500ms)
transaction.duration.us > 500000

# Find commands by a specific user
labels.user_id: "987654321098765432"
```

### Benefits

- **Isolated Analysis:** Each command has a unique APM transaction ID for independent performance tracking.
- **Granular Visibility:** Discord API calls, database queries, and service operations appear as spans within the command transaction.
- **Performance Metrics:** Track command throughput, latency percentiles (p50, p95, p99), and error rates.
- **Service Maps:** Visualize dependencies for each command type (database, Discord API, external services).

---

## Trace Context Isolation

Each Discord command, component interaction, gateway event, and background service execution creates an **independent root trace** with no parent context. This ensures clean trace graphs in Elastic APM where each operation can be analyzed in isolation.

### Why This Matters

In .NET, `Activity.Current` automatically propagates across async boundaries. Without proper isolation, all operations would inherit the bot startup activity as their parent, creating a tangled trace graph where thousands of commands appear as children of a single long-running startup trace.

**Problem (contaminated traces):**
```
bot.lifecycle.start (startup activity - never ends)
├─ discord.command ping
├─ discord.command rat-stats
├─ discord.gateway.connected
├─ background.service.reminder_execution
└─ ... (all operations as children)
```

**Solution (independent traces):**
```
bot.lifecycle.start (ends after startup)
discord.command ping (root)
discord.command rat-stats (root)
discord.gateway.connected (root)
background.service.reminder_execution (root)
```

Each operation now has its own trace ID and can be analyzed independently in Kibana APM.

### How Root Activities Are Created

Root activities are created by passing an explicit `ActivityContext` with random trace/span IDs to suppress the default parent inheritance:

```csharp
var rootContext = new ActivityContext(
    ActivityTraceId.CreateRandom(),
    ActivitySpanId.CreateRandom(),
    ActivityTraceFlags.None);

var activity = Source.StartActivity(name, ActivityKind.Server, rootContext);
```

The `BotActivitySource` helper methods handle this automatically. All entry-point activity methods accept an optional `asRootSpan` parameter (default `true`):

- `StartCommandActivity()` - Slash commands
- `StartComponentActivity()` - Buttons, select menus, modals
- `StartGatewayActivity()` - Gateway events (connected, disconnected)
- `StartEventActivity()` - Discord events (message received, member joined)
- `StartBackgroundServiceActivity()` - Background service execution cycles
- `StartBackgroundBatchActivity()` - Batch processing operations
- `StartBackgroundCleanupActivity()` - Cleanup operations

### When to Use Child Spans

Internal service operations that are part of a parent request should NOT create root spans. These methods intentionally inherit `Activity.Current`:

- `StartLifecycleActivity()` - Bot lifecycle (startup, shutdown)
- `StartServiceActivity()` - Service layer operations within a command

This allows you to see the full operation hierarchy when a command calls multiple services.

---

## Custom Attributes

The bot adds custom attributes to spans for rich contextual filtering and analysis. These attributes follow OpenTelemetry semantic conventions where applicable.

### Discord Attributes (`discord.*`)

| Attribute | Type | Description |
|-----------|------|-------------|
| `discord.command.name` | string | Slash command name (e.g., `ping`, `rat-stats`) |
| `discord.guild.id` | string | Discord guild snowflake ID |
| `discord.user.id` | string | Discord user snowflake ID |
| `discord.interaction.id` | string | Discord interaction snowflake ID |
| `discord.component.type` | string | Component type (`button`, `select_menu`, `modal`) |
| `discord.component.id` | string | Custom component ID |
| `discord.channel.id` | string | Discord channel snowflake ID |
| `discord.message.id` | string | Discord message snowflake ID |
| `discord.connection.latency_ms` | int | Gateway connection latency in milliseconds |
| `discord.connection.state` | string | Gateway connection state (`connected`, `disconnected`, `connecting`) |
| `discord.member.is_bot` | bool | Whether the member is a bot account |
| `discord.member.account_age_days` | int | Age of the Discord account in days |
| `discord.member.update.type` | string | Type of member update (`role_added`, `nickname_changed`, etc.) |
| `discord.member.role.id` | string | Discord role snowflake ID |
| `discord.guilds.count` | int | Number of guilds the bot is connected to |
| `discord.event.type` | string | Discord gateway event type (`message_received`, `member_joined`, etc.) |

**Example Span with Discord Attributes:**

```json
{
  "span.name": "discord.event.member.joined",
  "attributes": {
    "discord.guild.id": "123456789012345678",
    "discord.user.id": "987654321098765432",
    "discord.member.is_bot": false,
    "discord.member.account_age_days": 7
  }
}
```

### Auto-Moderation Attributes (`automod.*`)

| Attribute | Type | Description |
|-----------|------|-------------|
| `automod.rule.type` | string | Auto-mod rule type (`spam`, `raid`, `content_filter`) |
| `automod.rule.id` | string | Database ID of the auto-mod rule |
| `automod.severity` | string | Detection severity (`low`, `medium`, `high`) |
| `automod.action.type` | string | Action taken (`delete_message`, `timeout_member`, `notify_moderators`) |
| `automod.detection.confidence` | double | Detection confidence score (0.0 to 1.0) |

**Example Auto-Moderation Span:**

```json
{
  "span.name": "discord.event.automod.spam_detected",
  "attributes": {
    "automod.rule.type": "spam",
    "automod.severity": "high",
    "automod.action.type": "delete_message",
    "automod.detection.confidence": 0.95,
    "discord.user.id": "123456789012345678"
  }
}
```

### Database Attributes (`db.*`)

Following [OpenTelemetry Database Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/database/database-spans/):

| Attribute | Type | Description |
|-----------|------|-------------|
| `db.system` | string | Database system (`sqlite`, `mysql`, `postgresql`) |
| `db.operation` | string | Database operation (`SELECT`, `INSERT`, `UPDATE`, `DELETE`) |
| `db.entity.type` | string | Entity type being queried (`Guild`, `RatWatchIncident`, etc.) |
| `db.entity.id` | string | Entity ID (for single-entity operations) |
| `db.duration.ms` | int | Query execution duration in milliseconds |

**Example Database Span:**

```json
{
  "span.name": "db.select RatWatchIncident",
  "attributes": {
    "db.system": "sqlite",
    "db.operation": "SELECT",
    "db.entity.type": "RatWatchIncident",
    "db.duration.ms": 45
  }
}
```

### Discord API Attributes (`discord.api.*`)

| Attribute | Type | Description |
|-----------|------|-------------|
| `discord.api.endpoint` | string | API endpoint path (e.g., `/channels/{id}/messages`) |
| `discord.api.method` | string | HTTP method (`GET`, `POST`, `PUT`, `DELETE`) |
| `discord.api.response.status` | int | HTTP response status code |
| `discord.api.error.code` | int | Discord API error code |
| `discord.api.error.message` | string | Discord API error message |

**Rate Limit Attributes:**

| Attribute | Type | Description |
|-----------|------|-------------|
| `discord.api.rate_limit.limit` | int | Rate limit ceiling for the endpoint |
| `discord.api.rate_limit.remaining` | int | Remaining requests before rate limit |
| `discord.api.rate_limit.reset` | string | Timestamp when rate limit resets |
| `discord.api.rate_limit.reset_after` | int | Seconds until rate limit reset |
| `discord.api.rate_limit.bucket` | string | Rate limit bucket identifier |
| `discord.api.rate_limit.global` | bool | Whether this is a global rate limit |

**Retry Attributes:**

| Attribute | Type | Description |
|-----------|------|-------------|
| `discord.api.retry.attempt` | int | Retry attempt number |
| `discord.api.retry.backoff_ms` | int | Backoff duration in milliseconds |

### Background Service Attributes (`background.*`)

| Attribute | Type | Description |
|-----------|------|-------------|
| `background.service.name` | string | Name of the background service |
| `background.execution.cycle` | int | Execution cycle number |
| `background.batch.size` | int | Number of items in the batch |
| `background.records.processed` | int | Number of records processed |
| `background.records.deleted` | int | Number of records deleted |
| `background.duration.ms` | int | Operation duration in milliseconds |
| `background.interval` | int | Service execution interval in seconds |
| `background.item.id` | string | ID of the item being processed |
| `background.item.type` | string | Type of the item being processed |

### Service Layer Attributes (`service.*`)

| Attribute | Type | Description |
|-----------|------|-------------|
| `service.name` | string | Service class name |
| `service.operation` | string | Service operation name |
| `service.entity.type` | string | Entity type being operated on |
| `service.entity.id` | string | Entity ID (for single-entity operations) |
| `service.records.returned` | int | Number of records returned from query |
| `service.operation.success` | bool | Whether the operation succeeded |

---

## Correlating Logs and Traces

APM transactions and spans are automatically correlated with Serilog logs via the `trace.id` field, enabling seamless navigation between logs and traces in Kibana.

### Correlation Fields

| Field | Description |
|-------|-------------|
| `trace.id` | Unique trace identifier (shared between logs and APM) |
| `transaction.id` | APM transaction ID |
| `CorrelationId` | Application-level correlation ID (added as APM label) |

### Querying Correlated Data in Kibana

#### 1. Find Logs for a Specific Trace

In **Kibana Discover**:

```
trace.id: "abc123def456..."
```

This returns all log entries associated with the trace, including logs from database queries, Discord API calls, and service operations.

#### 2. Find APM Transactions for a Correlation ID

In **APM → Transactions**:

```
labels.CorrelationId: "a1b2c3d4e5f6g7h8"
```

This returns the APM transaction for the request, including all spans (database, API, service calls).

#### 3. Navigate from Log to Trace

1. In **Kibana Discover**, expand a log entry
2. Click on the `trace.id` field value
3. Select **"Show in APM"** to jump to the corresponding transaction
4. View the transaction timeline and span waterfall

### Example Correlation Workflow

**Scenario:** User reports slow `/rat-stats` command execution.

1. **Search Logs for Command:**
   ```
   CommandName: "rat-stats" AND Level: "Error"
   ```

2. **Find Correlation ID:**
   Expand the log entry and copy the `CorrelationId` value (e.g., `a1b2c3d4e5f6g7h8`).

3. **Search APM for Transaction:**
   Navigate to **APM → Transactions** and filter:
   ```
   labels.CorrelationId: "a1b2c3d4e5f6g7h8"
   ```

4. **Analyze Transaction Timeline:**
   View the span waterfall to identify slow operations:
   - Database query took 1.2s (slow query)
   - Discord API callback took 50ms (normal)

5. **Optimize Identified Bottleneck:**
   Add database index on `RatWatchIncident.GuildId` to speed up query.

6. **Verify Fix:**
   Re-run command and compare transaction duration in APM.

---

## Viewing APM Data in Kibana

### Accessing APM UI

1. Navigate to **Kibana** (e.g., `http://localhost:5601`)
2. Click **Observability** in the left sidebar
3. Click **APM** → **Services**
4. Select **discordbot** service

### Service Overview

The service overview dashboard displays:

- **Throughput:** Transactions per minute (TPM)
- **Latency:** p50, p95, p99 percentiles
- **Error Rate:** Percentage of failed transactions
- **Service Map:** Dependencies (database, Discord API)

### Transaction Analysis

Click on a transaction type (e.g., `discord.command rat-stats`) to view:

- **Latency Distribution:** Histogram of transaction durations
- **Throughput Over Time:** Transactions per minute timeline
- **Failed Transaction Rate:** Error percentage over time
- **Sample Transactions:** List of individual transaction traces

### Span Waterfall View

Click on an individual transaction to view the **span waterfall**:

- **Timeline:** Visual representation of span durations
- **Span Details:** Duration, attributes, stack traces
- **Dependencies:** Database queries, API calls, service operations
- **Critical Path:** Longest spans contributing to transaction duration

**Example Span Waterfall:**

```
discord.command rat-stats (234ms)
├─ service.ratwatch.get_stats (180ms)
│  ├─ db.select RatWatchIncident (120ms) ← Slow query
│  └─ db.select RatWatchVote (55ms)
└─ discord.api.POST /interactions/{id}/callback (50ms)
```

### Error Tracking

The **Errors** tab displays:

- **Error Grouping:** Errors grouped by exception type and message
- **Error Timeline:** Error occurrences over time
- **Stack Traces:** Full stack traces with source code context
- **Affected Transactions:** Transactions impacted by the error

Click on an error group to view:

- **Error Details:** Exception type, message, stack trace
- **Distribution:** When and where errors occurred
- **Sample Occurrences:** Individual error instances with full context

### Metrics and Analytics

The **Metrics** tab provides:

- **Transaction Breakdown:** Time spent by transaction type
- **Span Breakdown:** Time spent in database, external services, etc.
- **Throughput by Endpoint:** Most frequently called operations
- **Slowest Operations:** Operations with highest average duration

---

## Troubleshooting

### Traces Not Appearing in Kibana

**Symptoms:**
- No transactions visible in APM UI
- Service not listed in APM services

**Possible Causes:**

1. **APM Agent Disabled:**
   - Check `ElasticApm:Enabled` is `true` in configuration
   - Check logs for: `"Elastic APM is disabled via configuration"`

2. **APM Server URL Not Configured:**
   - Ensure `ElasticApm:ServerUrl` is set (e.g., `http://localhost:8200`)
   - Check logs for: `"APM server at 'not configured'"`

3. **APM Server Not Running:**
   - Verify APM server is running: `curl http://localhost:8200`
   - Check Docker container: `docker ps | grep apm-server`

4. **Network Connectivity Issues:**
   - Check firewall rules allowing outbound connection to APM server
   - Verify APM server is reachable from bot container/host

5. **Transaction Sampling:**
   - Transactions may be dropped by sampling filter (low priority operations)
   - Check transaction matches sampling criteria (see Priority-Based Sampling)

**Solution:**

```bash
# Check APM configuration in logs
grep "Elastic APM transaction filter registered" logs/discordbot-*.log

# Verify APM server connectivity
curl -X GET "http://localhost:8200/" -H "Authorization: Bearer your-secret-token"

# Temporarily disable sampling to test
# In appsettings.Development.json:
{
  "OpenTelemetry": {
    "Tracing": {
      "Sampling": {
        "DefaultRate": 1.0,
        "HighPriorityRate": 1.0,
        "LowPriorityRate": 1.0
      }
    }
  }
}
```

### Missing Labels on Transactions

**Symptoms:**
- Transactions appear in APM but lack `command_name`, `guild_id`, or other labels
- Cannot filter by Discord-specific attributes

**Possible Causes:**

1. **APM Agent Version Mismatch:**
   - Using outdated Elastic APM agent version
   - Labels API changed between versions

2. **Label Set After Transaction Completion:**
   - Labels must be set before transaction ends
   - Check `InteractionHandler.cs` for proper label assignment

**Solution:**

```bash
# Check APM agent version
dotnet list package | grep Elastic.Apm

# Upgrade to latest version
dotnet add package Elastic.Apm.NetCoreAll --version 1.28.0
```

### High APM Overhead

**Symptoms:**
- Increased CPU usage
- Increased memory consumption
- Slower response times

**Possible Causes:**

1. **100% Sampling in Production:**
   - Default rate set to 1.0 (100%) instead of 0.1 (10%)
   - Every transaction and span being captured

2. **Stack Trace Capture on All Spans:**
   - `SpanStackTraceMinDuration` set too low
   - Capturing stack traces for fast operations (wasteful)

3. **Excessive Span Creation:**
   - Too many custom spans being created manually
   - High-frequency operations instrumented unnecessarily

**Solution:**

```json
// appsettings.Production.json
{
  "ElasticApm": {
    "TransactionSampleRate": 0.1,
    "SpanStackTraceMinDuration": "10ms",
    "Recording": true
  },
  "OpenTelemetry": {
    "Tracing": {
      "Sampling": {
        "DefaultRate": 0.1,
        "LowPriorityRate": 0.01
      }
    }
  }
}
```

### APM Transaction Filter Not Applying

**Symptoms:**
- All transactions sampled at global rate instead of priority-based rates
- Sampling labels (`sampling.rate`, `sampling.decision`) not present

**Possible Causes:**

1. **Filter Registration Failed:**
   - Check logs for: `"Failed to register Elastic APM transaction filter"`
   - Exception during `ElasticApmFilterRegistrationService` startup

2. **APM Agent Not Initialized:**
   - `UseAllElasticApm()` not called in `Program.cs`
   - APM middleware not in request pipeline

**Solution:**

```bash
# Check filter registration in logs
grep "Elastic APM transaction filter registered successfully" logs/discordbot-*.log

# Verify Program.cs has:
app.UseAllElasticApm(builder.Configuration);

# Ensure extension is called before app.Run():
builder.Services.AddElasticApmWithPrioritySampling(builder.Configuration);
```

---

## Related Documentation

- **[Log Aggregation](log-aggregation.md)** - Elasticsearch and Serilog logging setup, APM configuration details
- **[Bot Performance Dashboard](bot-performance-dashboard.md)** - Performance metrics and monitoring dashboard
- **[Kibana Dashboards](kibana-dashboards.md)** - Guide to using Kibana for log analysis and APM monitoring

---

## Additional Resources

- [Elastic APM .NET Agent Documentation](https://www.elastic.co/guide/en/apm/agent/dotnet/current/index.html)
- [Elastic APM Server Reference](https://www.elastic.co/guide/en/apm/guide/current/apm-server.html)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [Kibana APM UI Guide](https://www.elastic.co/guide/en/kibana/current/apm-ui.html)
