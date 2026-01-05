# Centralized Log Aggregation

**Version:** 2.1
**Last Updated:** 2026-01-05
**Target Framework:** .NET 8 with Serilog, Elasticsearch, Seq, and Elastic APM
**Status:** Phase 2 Implementation (Elasticsearch primary with APM tracing, Seq optional)

---

## Overview

The Discord Bot Management System implements centralized log aggregation using **Elasticsearch as the primary logging backend** for production and staging environments. Elasticsearch provides powerful querying, analysis, and long-term storage of structured logs. **Seq is optionally available for development environments** as an alternative to Elasticsearch due to its lighter resource requirements.

**Elastic APM** provides distributed tracing capabilities that complement the centralized logging infrastructure. APM traces track requests across multiple services and operations, providing visibility into performance bottlenecks, dependency relationships, and transaction flows. Logs and traces are correlated via `trace.id` and `CorrelationId` fields for end-to-end observability.

### Architecture Overview

- **Development**: Elasticsearch + Elastic APM + optional Seq (dual-write support)
- **Staging**: Elasticsearch + Elastic APM + optional Seq (dual-write support)
- **Production**: Elasticsearch + Elastic APM only (optimized for scale)

### What is Elasticsearch?

Elasticsearch is a distributed search and analytics engine that indexes structured log data for fast querying and analysis. Unlike traditional logging systems, Elasticsearch can handle massive log volumes (millions of events per second) while maintaining sub-second query response times.

### What is Seq?

Seq is a lightweight, developer-friendly log server designed specifically for structured logging. It provides a simpler UI and lower operational overhead than Elasticsearch, making it ideal for development environments.

### What is Elastic APM?

Elastic APM (Application Performance Monitoring) is a distributed tracing solution that captures detailed performance data about your application. APM tracks transactions (e.g., HTTP requests, Discord command executions) and spans (individual operations within a transaction) to provide end-to-end visibility into application behavior. APM data is stored in Elasticsearch alongside logs and visualized in Kibana's APM UI.

### Key Features

**Elasticsearch:**
- **Scalable Ingestion**: Handle millions of log events per second across multiple nodes
- **Fast Full-Text Search**: Sub-second queries across billions of log events
- **Advanced Analytics**: Aggregations, time-series analysis, anomaly detection
- **Long-Term Retention**: Cost-effective storage for years of historical logs
- **Automatic Rollover**: Index management via Index Lifecycle Management (ILM)
- **Distributed Architecture**: High availability and redundancy

**Seq:**
- **Structured Log Search**: Query logs by any structured property (correlation IDs, guild IDs, user IDs, etc.)
- **Real-Time Streaming**: View logs as they arrive with live tail functionality
- **Signal-Based Alerting**: Create alerts based on log patterns and thresholds
- **Lightweight**: Single-container deployment, minimal resource requirements
- **Dashboard Visualizations**: Build custom dashboards for operational metrics

**Elastic APM:**
- **Distributed Tracing**: Track requests across multiple services and operations
- **Performance Monitoring**: Identify slow transactions and bottlenecks
- **Service Maps**: Visualize dependencies and service relationships
- **Error Tracking**: Automatic error capture with stack traces
- **Custom Spans**: Instrument critical code paths for detailed visibility
- **Priority-Based Sampling**: Intelligent sampling to reduce storage costs while capturing critical transactions

### Benefits for This Project

**Elasticsearch (Production/Staging):**
- **Scalability**: Handle growth from 1 to 1000+ Discord guilds without infrastructure changes
- **Cost-Effective Retention**: Store years of historical logs for compliance and auditing
- **Advanced Analytics**: Analyze patterns across millions of events (command usage trends, error rates, performance metrics)
- **Integration**: Native support for APM (Application Performance Monitoring) and distributed tracing
- **Operational Metrics**: Built-in tools for monitoring bot health, API usage, and performance

**Seq (Development):**
- **Quick Setup**: Single Docker container, no complex cluster management
- **Real-Time Feedback**: Instant log streaming during development
- **Easy Debugging**: Simple UI for filtering by correlation ID, guild ID, user ID
- **Low Overhead**: Minimal CPU/memory usage on development machines

**Elastic APM:**
- **End-to-End Visibility**: Correlate logs with trace spans for comprehensive debugging
- **Performance Optimization**: Identify slow database queries, API calls, and business operations
- **Error Root Cause Analysis**: Trace errors through the entire request flow
- **Production Insights**: Understand real-world performance patterns and bottlenecks
- **Cost-Effective**: Priority-based sampling ensures critical transactions are always captured while reducing storage costs

### Integration with Existing Logging

Elasticsearch and Seq complement the existing file-based logging infrastructure:

- **File Logs**: Remain as a local backup and for scenarios where centralized logging is unavailable
- **Console Logs**: Continue to provide immediate feedback during development
- **Elasticsearch Sink**: Asynchronously sends structured logs to Elasticsearch without impacting performance
- **Seq Sink** (optional): Available for development environments as a lightweight alternative

All logging outputs (console, file, Elasticsearch, and optionally Seq) receive the same structured log events, ensuring consistency across observability channels.

---

## Architecture

### Serilog Data Flow

```
Discord Interaction / HTTP Request
        │
        ▼
┌────────────────────────────────────────────────────────┐
│ Application Code                                       │
│ - InteractionHandler, Controllers, Services           │
│                                                        │
│   ILogger<T>.LogInformation("Command executed",       │
│       new { CorrelationId, GuildId, UserId })         │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ Serilog Pipeline                                       │
│                                                        │
│  ┌──────────────┐   ┌──────────────┐   ┌───────────┐ │
│  │ Enrichers    │ → │ Sanitizers   │ → │ Sinks     │ │
│  │              │   │              │   │           │ │
│  │ - Correlation│   │ - Token      │   │ Console   │ │
│  │ - TraceId    │   │   Sanitizer  │   │ File      │ │
│  │ - SpanId     │   │ - PII        │   │ Elastic   │ │
│  │              │   │              │   │ Seq*      │ │
│  └──────────────┘   └──────────────┘   └───────────┘ │
└────────────────────────┬───────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
         ▼                               ▼
┌─────────────────────┐        ┌──────────────────────┐
│ Elasticsearch Sink  │        │ Seq Sink (optional)  │
│ (Async Batch)       │        │ (Async Batch)        │
│                     │        │                      │
│ - Queue events      │        │ - Queue events       │
│ - Batch & send      │        │ - Batch & send       │
│ - Auto-retry        │        │ - Auto-retry         │
└─────────────┬───────┘        └──────────────┬───────┘
              │                               │
              ▼ HTTP POST (Bulk JSON)         ▼ HTTP POST (JSON)
    ┌─────────────────────┐        ┌─────────────────────┐
    │ Elasticsearch       │        │ Seq Server          │
    │ Cluster             │        │ (Development only)  │
    │                     │        │                     │
    │ - Distributed       │        │ - Single Container  │
    │ - Indexes by date   │        │ - Real-time UI      │
    │ - Queries: Kibana   │        │ - Queries: Seq UI   │
    └─────────────────────┘        └─────────────────────┘

* Seq is optional and only enabled in Development/Staging environments.
```

### Index Naming Convention

Elasticsearch automatically creates indices with the following naming pattern:

```
discordbot-logs-{ENVIRONMENT}-{DATE}

Examples:
- discordbot-logs-dev-2026.01.05       (Development)
- discordbot-logs-staging-2026.01.05   (Staging)
- discordbot-logs-prod-2026.01.05      (Production)
```

This naming scheme enables:
- **Automatic Rollover**: New index created daily via ILM policies
- **Retention Policies**: Delete old indices after X days
- **Environment Isolation**: Separate indices for each environment
- **Query Filtering**: Easy filtering by environment via index name

### Structured Properties

Every log event sent to Elasticsearch and Seq includes the following structured properties:

| Property | Type | Example | Source | Description |
|----------|------|---------|--------|-------------|
| `Timestamp` | DateTime | 2025-12-24T10:30:45.123Z | Serilog | Event timestamp (UTC) |
| `Level` | string | Information | Serilog | Log level: Verbose, Debug, Information, Warning, Error, Fatal |
| `MessageTemplate` | string | "Command {CommandName} executed" | Application | Structured message template |
| `RenderedMessage` | string | "Command ping executed" | Serilog | Rendered message with values |
| `SourceContext` | string | "DiscordBot.Bot.Handlers.InteractionHandler" | Serilog | Logger source class |
| `CorrelationId` | string | "a1b2c3d4e5f6g7h8" | Middleware/Handler | Request correlation ID |
| `TraceId` | string | "abc123def456..." | OpenTelemetry | Distributed trace ID |
| `SpanId` | string | "def456abc123..." | OpenTelemetry | Active span ID |
| `GuildId` | ulong | 123456789012345678 | Discord Context | Discord guild snowflake ID |
| `UserId` | ulong | 987654321098765432 | Discord Context | Discord user snowflake ID |
| `InteractionId` | ulong | 111222333444555666 | Discord Context | Discord interaction ID |
| `Exception` | object | { Type, Message, StackTrace } | Serilog | Structured exception data |

**Custom Properties:**

Application code can add additional structured properties via log context:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["CommandName"] = "ping",
    ["ExecutionTimeMs"] = 25.3
}))
{
    _logger.LogInformation("Command execution completed");
}
```

These properties become queryable fields in Seq.

### Asynchronous Batch Posting

To minimize performance impact, the Seq sink uses asynchronous batch posting:

1. **Queue**: Log events are added to an in-memory queue (non-blocking, <5 microseconds)
2. **Batch**: Events are batched up to `batchPostingLimit` size
3. **Post**: Batches are posted to Seq every `period` seconds via background thread
4. **Retry**: Failed batches are retried with exponential backoff
5. **Overflow**: If queue exceeds `queueSizeLimit`, oldest events are dropped

**Performance Characteristics:**

- **Log call overhead**: <5 microseconds (queue add operation)
- **Background posting**: 10-50ms per HTTP POST (does not block application threads)
- **Memory usage**: ~10 bytes per queued event (100,000 queue = ~1MB memory)
- **Network bandwidth**: ~1-5KB per log event (JSON payload)

---

## Configuration

### ElasticOptions Configuration Class

The `ElasticOptions` class (located in `src/DiscordBot.Core/Configuration/ElasticOptions.cs`) provides strongly-typed configuration for Elasticsearch:

```csharp
public class ElasticOptions
{
    // Elastic Cloud ID (for managed Elasticsearch Cloud)
    public string? CloudId { get; set; }

    // API key for authentication
    public string? ApiKey { get; set; }

    // Self-hosted Elasticsearch endpoints
    public string[] Endpoints { get; set; } = [];

    // Index naming format (supports date placeholders)
    public string IndexFormat { get; set; } = "discordbot-logs-{0:yyyy.MM.dd}";

    // Elastic APM server URL (for distributed tracing)
    public string? ApmServerUrl { get; set; }

    // APM secret token
    public string? ApmSecretToken { get; set; }

    // Environment name (development, staging, production)
    public string Environment { get; set; } = "development";
}
```

### Base Configuration (appsettings.json)

The base configuration file defines Elasticsearch endpoints and index format:

```json
{
  "Elastic": {
    "CloudId": null,
    "ApiKey": null,
    "Endpoints": [],
    "IndexFormat": "discordbot-logs-{0:yyyy.MM.dd}",
    "ApmServerUrl": null,
    "ApmSecretToken": null,
    "Environment": "development"
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {}
      },
      {
        "Name": "File",
        "Args": {}
      }
    ]
  }
}
```

### Elasticsearch Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `CloudId` | string | null | Elastic Cloud ID (e.g., "deployment:hash"). Takes precedence over Endpoints if set. |
| `ApiKey` | string | null | Elasticsearch API key for authentication. Required for production. Use user secrets or environment variables. |
| `Endpoints` | string[] | [] | Self-hosted Elasticsearch node URLs (e.g., "http://localhost:9200"). Required if CloudId not set. |
| `IndexFormat` | string | discordbot-logs-{0:yyyy.MM.dd} | Index naming pattern. Date placeholder `{0:yyyy.MM.dd}` creates daily indices. |
| `ApmServerUrl` | string | null | Elastic APM server URL for distributed tracing integration. |
| `ApmSecretToken` | string | null | APM secret token for authentication. |
| `Environment` | string | development | Environment name to distinguish logs by deployment stage. |

### Environment-Specific Configuration

#### Development (appsettings.Development.json)

**Option 1: Elasticsearch Only**

```json
{
  "Elastic": {
    "Endpoints": [ "http://localhost:9200" ],
    "IndexFormat": "discordbot-logs-dev-{0:yyyy.MM.dd}",
    "Environment": "development"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File", "Elastic.Serilog.Sinks" ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {}
      },
      {
        "Name": "File",
        "Args": {}
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://localhost:9200",
          "indexFormat": "discordbot-logs-dev-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true,
          "batchPostingLimit": 50,
          "period": "00:00:02"
        }
      }
    ]
  }
}
```

**Option 2: Elasticsearch + Seq (Dual-Write)**

For developers who prefer Seq's lighter resource requirements or real-time UI, configure both sinks:

```json
{
  "Elastic": {
    "Endpoints": [ "http://localhost:9200" ],
    "Environment": "development"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File", "Serilog.Sinks.Seq", "Elastic.Serilog.Sinks" ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {}
      },
      {
        "Name": "File",
        "Args": {}
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341",
          "batchPostingLimit": 100,
          "period": "00:00:02"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://localhost:9200",
          "indexFormat": "discordbot-logs-dev-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true,
          "batchPostingLimit": 50,
          "period": "00:00:02"
        }
      }
    ]
  }
}
```

**Configuration Details:**

- **Elasticsearch Endpoints**: Points to local Elasticsearch instance (see [Local Development Setup](#local-development-setup))
- **Elasticsearch batchPostingLimit**: 50 events (smaller batches for faster feedback during development)
- **Elasticsearch period**: 2 seconds (more frequent posting for near real-time logs)
- **Seq serverUrl**: Optional, only if using Seq (see [Local Development Setup](#local-development-setup) for setup instructions)
- **ApiKey**: Not required for local development

#### Staging (appsettings.Staging.json)

```json
{
  "Elastic": {
    "Endpoints": [ "http://elasticsearch-staging:9200" ],
    "Environment": "staging"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File", "Serilog.Sinks.Seq", "Elastic.Serilog.Sinks" ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {}
      },
      {
        "Name": "File",
        "Args": {}
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq-staging:5341"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch-staging:9200",
          "indexFormat": "discordbot-logs-staging-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true,
          "batchPostingLimit": 100,
          "period": "00:00:05"
        }
      }
    ]
  }
}
```

**Configuration Details:**

- **Elasticsearch Endpoints**: Internal DNS name for staging Elasticsearch cluster
- **Elasticsearch batchPostingLimit**: 100 events (balanced throughput)
- **Elasticsearch ApiKey**: Configured via environment variable or secrets management (required)
- **Seq serverUrl**: Optional, for staging testing alongside Elasticsearch
- **Seq apiKey**: Configured via environment variable (optional, only if using Seq)

#### Production (appsettings.Production.json)

```json
{
  "Elastic": {
    "Endpoints": [ "{ELASTICSEARCH_URL}" ],
    "ApiKey": null,
    "Environment": "production"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File", "Elastic.Serilog.Sinks" ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {}
      },
      {
        "Name": "File",
        "Args": {}
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "{ELASTICSEARCH_URL}",
          "indexFormat": "discordbot-logs-prod-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true,
          "batchPostingLimit": 100,
          "period": "00:00:05"
        }
      }
    ]
  }
}
```

**Configuration Details:**

- **Elasticsearch Endpoints**: Production cluster URL (via environment variable `ELASTICSEARCH_URL`)
- **Elasticsearch batchPostingLimit**: 100 events (optimal efficiency for production workloads)
- **Elasticsearch period**: 5 seconds (reduces HTTP request frequency)
- **Elasticsearch ApiKey**: Configured via environment variable `ELASTICSEARCH_API_KEY` (required)
- **Seq**: NOT used in production (Elasticsearch only)
- **Index Format**: `discordbot-logs-prod-YYYY.MM.DD` for environment isolation

### Environment Variable Overrides

For staging and production deployments, override sensitive configuration via environment variables:

**Elasticsearch Configuration:**

```bash
# Linux/macOS
export Elastic__Endpoints__0="https://elasticsearch.yourdomain.com:9200"
export Elastic__ApiKey="your-elasticsearch-api-key"
export Elastic__ApmServerUrl="https://apm.yourdomain.com:8200"
export Elastic__ApmSecretToken="your-apm-secret-token"

# Windows PowerShell
$env:Elastic__Endpoints__0="https://elasticsearch.yourdomain.com:9200"
$env:Elastic__ApiKey="your-elasticsearch-api-key"
$env:Elastic__ApmServerUrl="https://apm.yourdomain.com:8200"
$env:Elastic__ApmSecretToken="your-apm-secret-token"

# Docker
docker run -e Elastic__Endpoints__0="https://elasticsearch.yourdomain.com:9200" \
           -e Elastic__ApiKey="your-elasticsearch-api-key" \
           discordbot:latest
```

**Alternative: Elastic Cloud**

For Elastic Cloud deployments, use CloudId instead of individual endpoints:

```bash
# Linux/macOS
export Elastic__CloudId="deployment-id:hash"
export Elastic__ApiKey="your-cloud-api-key"

# Docker
docker run -e Elastic__CloudId="deployment-id:hash" \
           -e Elastic__ApiKey="your-cloud-api-key" \
           discordbot:latest
```

**Seq Configuration (Staging Only):**

```bash
# Linux/macOS
export Serilog__WriteTo__3__Args__serverUrl="http://seq-staging:5341"
export Serilog__WriteTo__3__Args__apiKey="your-seq-api-key"

# Windows PowerShell
$env:Serilog__WriteTo__3__Args__serverUrl="http://seq-staging:5341"
$env:Serilog__WriteTo__3__Args__apiKey="your-seq-api-key"
```

**Note:** `WriteTo__3` corresponds to the fourth sink (0-indexed: Console=0, File=1, Seq=2, Elasticsearch=3) in dual-write configurations.

### User Secrets (Development)

For local development with API key authentication:

```bash
cd src/DiscordBot.Bot

# Elasticsearch API Key (if needed for local testing)
dotnet user-secrets set "Elastic:ApiKey" "your-dev-api-key"

# Seq API Key (optional, if using Seq)
dotnet user-secrets set "Serilog:WriteTo:2:Args:apiKey" "your-seq-api-key"
```

**Security Best Practice:** NEVER commit API keys to configuration files. Always use user secrets (development) or environment variables/secrets management (staging/production).

### Elastic APM Configuration

Elastic APM is configured via the `ElasticApm` section in `appsettings.json` and uses the official Elastic APM agent for .NET.

#### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ServerUrl` | string | null | APM server URL (e.g., "http://localhost:8200"). Required if APM is enabled. |
| `SecretToken` | string | null | APM secret token for authentication. Use user secrets or environment variables. |
| `ServiceName` | string | "discordbot" | Service name displayed in Kibana APM UI. |
| `ServiceVersion` | string | "0.5.4" | Service version for deployment tracking. |
| `Environment` | string | "development" | Environment name (development, staging, production). |
| `TransactionSampleRate` | double | 1.0 | Global sampling rate (0.0 to 1.0). Overridden by priority-based filter. |
| `CaptureBody` | string | "off" | Capture HTTP request/response bodies ("off", "errors", "transactions", "all"). |
| `CaptureHeaders` | bool | true | Capture HTTP request/response headers. |
| `StackTraceLimit` | int | 50 | Maximum stack trace frames to capture. |
| `SpanStackTraceMinDuration` | string | "5ms" | Minimum span duration to capture stack traces. |
| `Recording` | bool | true | Enable/disable recording of transactions and spans. |
| `Enabled` | bool | true | Master switch for APM agent. Set to false to completely disable APM. |
| `TransactionIgnoreUrls` | string | "/health*,/metrics*,/swagger*" | Comma-separated URL patterns to ignore (wildcards supported). |
| `UseElasticTraceparentHeader` | bool | true | Use Elastic-specific traceparent header for distributed tracing. |

#### User Secrets (Development)

Configure APM credentials via user secrets for local development:

```bash
cd src/DiscordBot.Bot

# APM Server URL (local APM server)
dotnet user-secrets set "ElasticApm:ServerUrl" "http://localhost:8200"

# APM Secret Token (if authentication is enabled)
dotnet user-secrets set "ElasticApm:SecretToken" "your-apm-secret-token"
```

For local development with APM disabled, set `ElasticApm:Enabled` to `false` in `appsettings.Development.json`.

#### Environment-Specific Configuration

**Development (appsettings.Development.json):**

```json
{
  "ElasticApm": {
    "ServerUrl": "http://localhost:8200",
    "ServiceName": "discordbot",
    "ServiceVersion": "0.5.4",
    "Environment": "development",
    "TransactionSampleRate": 1.0,
    "Enabled": true,
    "Recording": true
  }
}
```

**Staging/Production:**

Use environment variables to override sensitive configuration:

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

# Docker
docker run -e ElasticApm__ServerUrl="https://apm.yourdomain.com:8200" \
           -e ElasticApm__SecretToken="your-production-apm-token" \
           -e ElasticApm__Environment="production" \
           discordbot:latest
```

#### Priority-Based Sampling

The application implements priority-based sampling via `ElasticApmTransactionFilter` to optimize APM storage costs while ensuring critical transactions are always captured. This filter uses the same sampling logic as the OpenTelemetry `PrioritySampler` for consistency across observability platforms.

**Sampling Tiers:**

| Priority | Sample Rate | Operations |
|----------|-------------|------------|
| **Always Sample** | 100% | Rate limit hits, API errors, auto-moderation detections |
| **High Priority** | 50% (configurable) | Welcome flow, moderation actions, Rat Watch, scheduled messages |
| **Default** | 10% (configurable) | Normal operations, Discord commands, API requests |
| **Low Priority** | 1% (configurable) | Health checks, metrics scraping, cache operations |

**Configuration:**

Sampling rates are configured via `OpenTelemetry:Tracing:Sampling` section (shared with OpenTelemetry sampler):

```json
{
  "OpenTelemetry": {
    "Tracing": {
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

**Benefits:**

- Critical transactions (errors, rate limits, security events) are always captured
- Important business operations (moderation, welcome flow) are sampled at higher rates
- High-frequency, low-value operations (health checks, cache) are sampled minimally
- Reduces APM storage costs by 90% in production while maintaining observability

#### Correlation Between Logs and Traces

APM transactions and spans are automatically correlated with logs via the following fields:

| Field | Description |
|-------|-------------|
| `trace.id` | Unique trace identifier (shared between logs and APM) |
| `transaction.id` | APM transaction ID |
| `CorrelationId` | Application-level correlation ID (added as APM label) |

**Querying Correlated Data in Kibana:**

1. **Find logs for a specific trace:**
   ```
   trace.id: "abc123def456..."
   ```

2. **Find APM transactions for a correlation ID:**
   - Go to APM → Transactions
   - Filter by label: `labels.CorrelationId: "a1b2c3d4e5f6g7h8"`

3. **Navigate from log to trace:**
   - In Kibana Discover, expand a log entry
   - Click on `trace.id` field
   - Select "Show in APM" to jump to the corresponding transaction

**Example Correlation Workflow:**

1. User reports slow command execution for `/rat-stats`
2. Search logs for command: `CommandName: "rat-stats"`
3. Find `CorrelationId` from log entry
4. Search APM for label: `labels.CorrelationId: "{correlationId}"`
5. View transaction timeline to identify slow spans (database queries, Discord API calls)
6. Optimize identified bottlenecks

---

## Local Development Setup

### Running Elasticsearch Locally with Docker

**Minimal Setup (Single Node):**

```bash
# Pull and run Elasticsearch container
docker run -d \
  --name elasticsearch \
  -p 9200:9200 \
  -p 9300:9300 \
  -e discovery.type=single-node \
  -e xpack.security.enabled=false \
  -e "ES_JAVA_OPTS=-Xms512m -Xmx512m" \
  -v elasticsearch-data:/usr/share/elasticsearch/data \
  docker.elastic.co/elasticsearch/elasticsearch:latest

# Access Elasticsearch API at http://localhost:9200
# Check cluster status: curl http://localhost:9200/_cluster/health
```

**With Kibana UI (Recommended for Querying):**

```bash
# Run Elasticsearch and Kibana together
docker run -d \
  --name elasticsearch \
  -p 9200:9200 \
  -p 9300:9300 \
  -e discovery.type=single-node \
  -e xpack.security.enabled=false \
  -e "ES_JAVA_OPTS=-Xms512m -Xmx512m" \
  -v elasticsearch-data:/usr/share/elasticsearch/data \
  docker.elastic.co/elasticsearch/elasticsearch:latest

docker run -d \
  --name kibana \
  -p 5601:5601 \
  -e ELASTICSEARCH_HOSTS=http://elasticsearch:9200 \
  --link elasticsearch \
  docker.elastic.co/kibana/kibana:latest

# Access Kibana at http://localhost:5601
# Kibana automatically discovers Elasticsearch indices
```

**Docker Compose Integration:**

For projects using Docker Compose, add Elasticsearch and Kibana to your `docker-compose.yml`:

```yaml
version: '3.8'

services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:latest
    container_name: elasticsearch
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - ES_JAVA_OPTS=-Xms512m -Xmx512m
    ports:
      - "9200:9200"
      - "9300:9300"
    volumes:
      - elasticsearch-data:/usr/share/elasticsearch/data
    restart: unless-stopped

  kibana:
    image: docker.elastic.co/kibana/kibana:latest
    container_name: kibana
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    ports:
      - "5601:5601"
    depends_on:
      - elasticsearch
    restart: unless-stopped

  discordbot:
    image: discordbot:latest
    depends_on:
      - elasticsearch
    environment:
      - Elastic__Endpoints__0=http://elasticsearch:9200
    # ... other bot configuration

volumes:
  elasticsearch-data:
```

**Port Mapping:**
- `9200:9200` - Elasticsearch REST API
- `9300:9300` - Elasticsearch node communication (needed for multi-node clusters)
- `5601:5601` - Kibana UI (optional, but recommended for querying logs)

**Volume Mounting:**
- `-v elasticsearch-data:/usr/share/elasticsearch/data` - Persist indices across container restarts
- Data stored in Docker volume named `elasticsearch-data`

**Security Settings:**
- `xpack.security.enabled=false` - Disables authentication for development (use API keys in production)
- `discovery.type=single-node` - Single-node cluster for development

**Memory Configuration:**
- `-e "ES_JAVA_OPTS=-Xms512m -Xmx512m"` - Allocate 512MB JVM heap (adjust based on available memory)

### Running Seq Locally with Docker (Optional)

For developers who prefer Seq's lightweight UI over Kibana:

```bash
# Pull and run Seq container
docker run -d \
  --name seq \
  -p 5341:80 \
  -e ACCEPT_EULA=Y \
  -v seq-data:/data \
  datalust/seq:latest

# Access Seq UI at http://localhost:5341
```

**Port Mapping:**
- `5341:80` - Maps container port 80 to host port 5341
- Port 5341 chosen to match default Seq convention (not 80 which may conflict)

**Volume Mounting:**
- `-v seq-data:/data` - Persist logs across container restarts
- Data stored in Docker volume named `seq-data`

**EULA Acceptance:**
- `-e ACCEPT_EULA=Y` - Accept Seq license (required)

### Running Elastic APM Server Locally with Docker

For local development with distributed tracing support:

```bash
# Pull and run APM Server container
docker run -d \
  --name apm-server \
  -p 8200:8200 \
  --link elasticsearch \
  -e output.elasticsearch.hosts=["http://elasticsearch:9200"] \
  -e apm-server.host="0.0.0.0:8200" \
  -e apm-server.secret_token="" \
  docker.elastic.co/apm/apm-server:latest

# Access APM Server at http://localhost:8200
```

**Port Mapping:**
- `8200:8200` - APM Server API endpoint

**Configuration:**
- `output.elasticsearch.hosts` - Elasticsearch cluster URL (uses linked container)
- `apm-server.host` - Bind to all interfaces for external access
- `apm-server.secret_token` - Empty for development (use secret tokens in production)

**Note:** APM Server requires Elasticsearch to be running. Start Elasticsearch before starting APM Server.

**Docker Compose Integration (Recommended):**

Add APM Server to your `docker-compose.yml` for integrated setup:

```yaml
version: '3.8'

services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:latest
    container_name: elasticsearch
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - ES_JAVA_OPTS=-Xms512m -Xmx512m
    ports:
      - "9200:9200"
      - "9300:9300"
    volumes:
      - elasticsearch-data:/usr/share/elasticsearch/data
    restart: unless-stopped

  kibana:
    image: docker.elastic.co/kibana/kibana:latest
    container_name: kibana
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    ports:
      - "5601:5601"
    depends_on:
      - elasticsearch
    restart: unless-stopped

  apm-server:
    image: docker.elastic.co/apm/apm-server:latest
    container_name: apm-server
    environment:
      - output.elasticsearch.hosts=["http://elasticsearch:9200"]
      - apm-server.host=0.0.0.0:8200
      - apm-server.secret_token=
    ports:
      - "8200:8200"
    depends_on:
      - elasticsearch
    restart: unless-stopped

  discordbot:
    image: discordbot:latest
    depends_on:
      - elasticsearch
      - apm-server
    environment:
      - Elastic__Endpoints__0=http://elasticsearch:9200
      - ElasticApm__ServerUrl=http://apm-server:8200
    # ... other bot configuration

volumes:
  elasticsearch-data:
```

**Benefits:**
- Elasticsearch, Kibana, and APM Server start together with `docker-compose up`
- APM Server automatically connects to Elasticsearch
- Bot can send traces to APM Server via internal Docker network
- All data persists across restarts

### Docker Compose Integration

For projects using Docker Compose, add Seq to your `docker-compose.yml`:

```yaml
version: '3.8'

services:
  seq:
    image: datalust/seq:latest
    container_name: seq
    ports:
      - "5341:80"
    environment:
      - ACCEPT_EULA=Y
    volumes:
      - seq-data:/data
    restart: unless-stopped

  discordbot:
    image: discordbot:latest
    depends_on:
      - seq
    environment:
      - Serilog__WriteTo__2__Args__serverUrl=http://seq:80
    # ... other bot configuration

volumes:
  seq-data:
```

**Benefits:**
- Seq starts automatically with `docker-compose up`
- Bot uses internal Docker network to connect to Seq
- Data persists across restarts via named volume

### First-Time Elasticsearch and APM Setup

1. **Start Elasticsearch, Kibana, and APM Server Containers:**
   ```bash
   # Start Elasticsearch
   docker run -d \
     --name elasticsearch \
     -p 9200:9200 \
     -p 9300:9300 \
     -e discovery.type=single-node \
     -e xpack.security.enabled=false \
     -e "ES_JAVA_OPTS=-Xms512m -Xmx512m" \
     -v elasticsearch-data:/usr/share/elasticsearch/data \
     docker.elastic.co/elasticsearch/elasticsearch:latest

   # Wait for Elasticsearch to start, then start Kibana
   sleep 10

   docker run -d \
     --name kibana \
     -p 5601:5601 \
     -e ELASTICSEARCH_HOSTS=http://localhost:9200 \
     docker.elastic.co/kibana/kibana:latest

   # Wait a few more seconds, then start APM Server
   sleep 5

   docker run -d \
     --name apm-server \
     -p 8200:8200 \
     --link elasticsearch \
     -e output.elasticsearch.hosts=["http://elasticsearch:9200"] \
     -e apm-server.host="0.0.0.0:8200" \
     -e apm-server.secret_token="" \
     docker.elastic.co/apm/apm-server:latest
   ```

2. **Verify Services are Ready:**
   ```bash
   # Check Elasticsearch cluster health
   curl http://localhost:9200/_cluster/health
   # Should return green or yellow status

   # Check APM Server health
   curl http://localhost:8200/
   # Should return APM Server version info
   ```

3. **Access Kibana UI:**
   - Navigate to `http://localhost:5601`
   - Wait 1-2 minutes for Kibana to fully initialize

4. **Configure Bot:**
   - Ensure `appsettings.Development.json` has Elasticsearch and APM configuration
   - Configure user secrets for APM Server URL:
     ```bash
     cd src/DiscordBot.Bot
     dotnet user-secrets set "ElasticApm:ServerUrl" "http://localhost:8200"
     ```
   - Run the bot: `dotnet run --project src/DiscordBot.Bot`

5. **Verify Logs and Traces Arriving:**
   - Execute a Discord command (e.g., `/ping`)

   **Logs:**
   - In Kibana, go to Discovery → Create Index Pattern
   - Index pattern: `discordbot-logs-dev-*`
   - Timestamp field: `@timestamp`
   - Logs should appear within 2 seconds

   **Traces:**
   - In Kibana, go to Observability → APM → Services
   - You should see the "discordbot" service
   - Click on the service to view transactions and traces

6. **Explore APM Features:**
   - **Transactions**: View all HTTP requests and Discord command executions
   - **Service Map**: See how components interact (database, Discord API, etc.)
   - **Errors**: Automatically captured exceptions with stack traces
   - **Metrics**: Service-level performance metrics (throughput, response times, error rates)

7. **Correlate Logs and Traces:**
   - In APM transaction view, click on a transaction
   - Note the `trace.id` value
   - Go to Discovery and search: `trace.id: "your-trace-id"`
   - All logs for that transaction will appear

8. **Create Kibana Dashboards (Optional):**
   - Go to Analytics → Dashboards
   - Create custom dashboards for command execution, errors, performance metrics

### First-Time Seq Setup (Optional)

For developers who prefer Seq's simpler interface:

1. **Start Seq Container:**
   ```bash
   docker run -d --name seq -p 5341:80 -e ACCEPT_EULA=Y -v seq-data:/data datalust/seq:latest
   ```

2. **Access Seq UI:**
   - Navigate to `http://localhost:5341`
   - No authentication required for local development

3. **Configure Bot:**
   - Update `appsettings.Development.json` to include Seq sink (see [Development Configuration](#development-appsettingsdevelopmentjson))
   - Run the bot: `dotnet run --project src/DiscordBot.Bot`

4. **Verify Logs Arriving:**
   - Execute a Discord command (e.g., `/ping`)
   - Refresh Seq UI - logs should appear within 2 seconds

### Stopping and Managing Containers

**Elasticsearch:**

```bash
# Stop Elasticsearch and Kibana (data persists in volumes)
docker stop elasticsearch kibana

# Restart containers
docker start elasticsearch kibana

# Remove containers (data still persists in volumes)
docker rm elasticsearch kibana

# Remove containers AND data (WARNING: deletes all logs and indices)
docker rm elasticsearch kibana
docker volume rm elasticsearch-data
```

**Seq:**

```bash
# Stop Seq container (data persists in volume)
docker stop seq

# Restart Seq container
docker start seq

# Remove Seq container (data still persists in volume)
docker rm seq

# Remove Seq AND its data (WARNING: deletes all logs)
docker rm seq
docker volume rm seq-data
```

---

## Querying Logs

### Querying in Elasticsearch (Kibana)

#### Creating an Index Pattern

1. Open Kibana: `http://localhost:5601`
2. Go to Management → Index Patterns (or Stack Management → Index Patterns)
3. Click "Create index pattern"
4. Index pattern: `discordbot-logs-*` (matches all environments)
5. Timestamp field: `@timestamp`
6. Click "Create index pattern"

#### Using Discovery Tab

The Discovery tab provides a real-time log browser similar to Seq:

1. Go to Analytics → Discover
2. Select the `discordbot-logs-*` index pattern
3. View logs in chronological order with expandable details

**Filtering Logs:**

Click the filter icon next to a field value to create a filter:

```
SourceContext: "DiscordBot.Bot.Handlers.InteractionHandler"
Level: "Error"
GuildId: "123456789012345678"
```

#### Using Kibana Query Language (KQL)

KQL provides SQL-like filtering:

```
# All errors
Level: "Error"

# Errors from InteractionHandler
Level: "Error" AND SourceContext: "DiscordBot.Bot.Handlers.InteractionHandler"

# Logs for specific guild with slow execution
GuildId: "123456789012345678" AND ExecutionTimeMs > 500

# All logs within time range (use time picker UI instead for easier)
@timestamp >= "2026-01-05T10:00:00" AND @timestamp < "2026-01-05T11:00:00"
```

#### Creating Visualizations

1. Go to Analytics → Visualize
2. Click "Create visualization"
3. Select visualization type (metric, line chart, bar chart, etc.)
4. Choose the `discordbot-logs-*` index
5. Use aggregations to summarize data:
   - **Count of errors by hour**: X-axis: Date histogram (@timestamp, 1h), Y-axis: Count
   - **Top commands**: Terms aggregation on CommandName, sorted by count
   - **Average response time**: Average of ExecutionTimeMs by CommandName

#### Creating Dashboards

Combine multiple visualizations into a dashboard:

1. Go to Analytics → Dashboards
2. Click "Create dashboard"
3. Click "Add panel"
4. Select existing visualizations or create new ones
5. Arrange panels on the dashboard
6. Save the dashboard

**Example Dashboard for DiscordBot:**
- Command execution count (line chart, 1h buckets)
- Error rate (percentage gauge)
- Top 10 commands (bar chart)
- Average command latency (metric)
- Recent errors (data table)

#### Elasticsearch Query DSL (Advanced)

For complex queries, use Elasticsearch's Query DSL directly:

1. Go to Dev Tools → Console
2. Write DSL queries:

```json
GET discordbot-logs-dev-*/_search
{
  "query": {
    "bool": {
      "must": [
        { "term": { "Level": "Error" } },
        { "range": { "@timestamp": {
          "gte": "2026-01-05T10:00:00",
          "lte": "2026-01-05T11:00:00"
        }}}
      ]
    }
  },
  "aggs": {
    "errors_by_source": {
      "terms": { "field": "SourceContext" }
    }
  }
}
```

### Querying in Seq

#### Basic Search

**Navigate to Events:**

1. Open Seq UI (`http://localhost:5341`)
2. Click "Events" in left sidebar
3. View live stream of log events

**Free-Text Search:**

Type directly in the search box to search rendered messages:

```
Command executed
```

Finds all log events containing "Command executed" in the rendered message.

### Querying by Structured Properties

Seq's power comes from querying structured properties using SQL-like syntax:

#### By Correlation ID

Track all log events for a specific Discord interaction:

```
CorrelationId = 'a1b2c3d4e5f6g7h8'
```

**Use Case:** Debug a specific command execution end-to-end.

#### By Guild ID

View all logs for a specific Discord guild:

```
GuildId = 123456789012345678
```

**Use Case:** Investigate issues reported by a specific server.

#### By User ID

Filter logs for a specific Discord user:

```
UserId = 987654321098765432
```

**Use Case:** Debug user-specific issues or investigate suspicious activity.

#### By Trace ID

Link logs to a distributed trace:

```
TraceId = 'abc123def456...'
```

**Use Case:** Correlate logs with trace spans in Jaeger or Application Insights.

#### By Log Level

Find errors and warnings:

```
@Level = 'Error'
```

```
@Level in ['Warning', 'Error', 'Fatal']
```

**Use Case:** Surface production issues and anomalies.

#### By Source Context

Filter logs from specific classes or namespaces:

```
SourceContext like 'DiscordBot.Bot.Handlers%'
```

```
SourceContext = 'DiscordBot.Bot.Handlers.InteractionHandler'
```

**Use Case:** Focus on specific components during debugging.

#### By Exception Type

Find all logs with exceptions:

```
Exception is not null
```

Find specific exception types:

```
Exception.Type = 'System.InvalidOperationException'
```

**Use Case:** Identify error patterns and recurring exceptions.

### Combining Filters

Use `and`, `or`, and parentheses to build complex queries:

```
GuildId = 123456789012345678 and @Level = 'Error'
```

```
(SourceContext like 'DiscordBot.Bot%' or SourceContext like 'DiscordBot.Infrastructure%')
  and @Level in ['Warning', 'Error']
```

```
CorrelationId is not null and Exception is not null
```

### Time Ranges

Use the time picker in the UI or query by timestamp:

```
@Timestamp > DateTime('2025-12-24T10:00:00Z')
```

```
@Timestamp >= DateTime('2025-12-24') and @Timestamp < DateTime('2025-12-25')
```

**Relative Time:**

```
@Timestamp > Now() - 1h
```

```
@Timestamp > Now() - 15m
```

### Aggregations and Statistics

**Count events:**

```
select count(*) from stream
where @Level = 'Error'
group by time(1h)
```

**Find slow queries:**

```
select avg(ExecutionTimeMs), max(ExecutionTimeMs)
from stream
where ExecutionTimeMs is not null
group by time(5m)
```

**Top error messages:**

```
select count(*) as ErrorCount
from stream
where @Level = 'Error'
group by @MessageTemplate
order by ErrorCount desc
```

### Saving Queries as Signals

Frequently used queries can be saved as **Signals** for quick access:

1. Enter query in search box
2. Click "Save as Signal"
3. Name: "Production Errors"
4. Optional: Configure alerts (email, Slack, etc.)
5. Signal appears in left sidebar for one-click access

### Example Queries for This Project

**All Discord command executions:**

```
MessageTemplate like 'Discord command%'
```

**Slow database queries:**

```
ExecutionTimeMs > 500
```

**Failed command executions:**

```
CorrelationId is not null and Exception is not null
```

**User activity for specific guild:**

```
GuildId = 123456789012345678 and UserId is not null
```

---

## Production Deployment Options

### Elasticsearch Deployment Options

#### Option 1: Elastic Cloud (SaaS - Recommended)

Elastic Cloud is the official managed Elasticsearch service:

**Pros:**
- Zero infrastructure management
- Automatic backups and updates
- High availability (multi-region support)
- Integrated Kibana included
- Elastic APM support
- Scales automatically with workload

**Setup:**

1. Sign up at https://cloud.elastic.co
2. Create a deployment (select region)
3. Note the CloudId and API key
4. Configure bot with:
   ```json
   {
     "Elastic": {
       "CloudId": "deployment-id:hash",
       "ApiKey": "elastic-cloud-api-key",
       "ApmServerUrl": "https://apm-server-url:8200",
       "Environment": "production"
     }
   }
   ```

**Pricing:**
- Free tier: 14-day trial
- Paid tiers: Based on data ingestion and storage
- Estimate: ~$50-200/month for small to medium bots

#### Option 2: Self-Hosted Kubernetes

Deploy Elasticsearch to a Kubernetes cluster:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: elasticsearch-config
data:
  elasticsearch.yml: |
    cluster.name: discordbot
    node.name: ${HOSTNAME}
    discovery.seed_hosts: ["elasticsearch-0.elasticsearch", "elasticsearch-1.elasticsearch"]
    cluster.initial_master_nodes: ["elasticsearch-0", "elasticsearch-1", "elasticsearch-2"]
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: elasticsearch
spec:
  serviceName: elasticsearch
  replicas: 3
  selector:
    matchLabels:
      app: elasticsearch
  template:
    metadata:
      labels:
        app: elasticsearch
    spec:
      containers:
      - name: elasticsearch
        image: docker.elastic.co/elasticsearch/elasticsearch:latest
        ports:
        - containerPort: 9200
        - containerPort: 9300
        env:
        - name: node.roles
          value: "[master,data,ingest]"
        - name: ES_JAVA_OPTS
          value: "-Xms512m -Xmx512m"
        resources:
          limits:
            memory: "1Gi"
            cpu: "500m"
        volumeMounts:
        - name: data
          mountPath: /usr/share/elasticsearch/data
  volumeClaimTemplates:
  - metadata:
      name: data
    spec:
      accessModes: ["ReadWriteOnce"]
      storageClassName: standard
      resources:
        requests:
          storage: 50Gi
---
apiVersion: v1
kind: Service
metadata:
  name: elasticsearch
spec:
  clusterIP: None
  selector:
    app: elasticsearch
  ports:
  - port: 9200
    name: api
  - port: 9300
    name: node-communication
```

**Advantages:**
- Full control over infrastructure
- No vendor lock-in
- Can optimize for specific workloads
- Integrates with existing Kubernetes environment

**Disadvantages:**
- Requires operational expertise
- Need to manage backups and disaster recovery
- Responsible for patching and updates
- Higher upfront infrastructure costs

#### Option 3: Docker Compose (Staging/Small Production)

For staging or small production deployments:

```yaml
version: '3.8'

services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:latest
    environment:
      - cluster.name=discordbot
      - discovery.type=single-node
      - xpack.security.enabled=true
      - ELASTIC_PASSWORD=your-secure-password
      - "ES_JAVA_OPTS=-Xms1g -Xmx1g"
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch-data:/usr/share/elasticsearch/data
    restart: unless-stopped

  kibana:
    image: docker.elastic.co/kibana/kibana:latest
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
      - ELASTICSEARCH_USERNAME=elastic
      - ELASTICSEARCH_PASSWORD=your-secure-password
    ports:
      - "5601:5601"
    depends_on:
      - elasticsearch
    restart: unless-stopped

volumes:
  elasticsearch-data:
```

### Seq Deployment (Optional - Staging Only)

For staging environments that want to use Seq alongside Elasticsearch:

**Pros:**
- No infrastructure management
- Automatic backups and updates
- High availability
- Scalable ingestion

**Cons:**
- Monthly subscription cost
- Data egress costs
- Less control over retention policies

**Setup:**

1. Sign up at https://datalust.co
2. Create workspace
3. Obtain ingestion URL and API key
4. Configure bot with provided endpoint

#### Azure Container Instances

Deploy Seq to Azure Container Instances:

```bash
az container create \
  --resource-group discordbot-rg \
  --name seq \
  --image datalust/seq:latest \
  --dns-name-label seq-discordbot \
  --ports 80 443 \
  --environment-variables ACCEPT_EULA=Y \
  --azure-file-volume-account-name seqstorage \
  --azure-file-volume-account-key <storage-key> \
  --azure-file-volume-share-name seq-data \
  --azure-file-volume-mount-path /data
```

**Benefits:**
- Pay-per-use (no idle costs)
- Integrated with Azure services
- Persistent storage via Azure Files

#### Kubernetes Deployment

Deploy Seq to Kubernetes cluster:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: seq
spec:
  replicas: 1
  selector:
    matchLabels:
      app: seq
  template:
    metadata:
      labels:
        app: seq
    spec:
      containers:
      - name: seq
        image: datalust/seq:latest
        ports:
        - containerPort: 80
        env:
        - name: ACCEPT_EULA
          value: "Y"
        volumeMounts:
        - name: seq-data
          mountPath: /data
      volumes:
      - name: seq-data
        persistentVolumeClaim:
          claimName: seq-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: seq
spec:
  selector:
    app: seq
  ports:
  - port: 80
    targetPort: 80
  type: LoadBalancer
```

---

## Performance Considerations

### Batch Posting Efficiency

| Batch Size | HTTP Requests/Hour | Network Overhead | Real-Time Delay |
|------------|-------------------|------------------|-----------------|
| 100 | 1,800 (at max throughput) | Moderate | 2 seconds |
| 500 | 360 | Low | 2 seconds |
| 1000 | 180 | Minimal | 5 seconds |

**Recommendation:** Use larger batch sizes in production (1000) to minimize HTTP overhead. Use smaller batch sizes in development (100) for more real-time feedback.

### Queue Management

The in-memory queue (`queueSizeLimit: 100000`) prevents memory exhaustion during Seq outages:

**Scenarios:**

1. **Normal Operation:**
   - Queue size: 0-500 events (well below limit)
   - Events posted every 2-5 seconds

2. **Seq Temporary Outage:**
   - Queue size grows as events accumulate
   - Sink retries connection with exponential backoff
   - Once Seq recovers, backlog drains rapidly

3. **Seq Extended Outage (queue full):**
   - Oldest events dropped to prevent memory exhaustion
   - Warning logged: "Seq queue limit exceeded, dropping oldest events"
   - File logs remain intact as backup

**Monitoring Queue Health:**

Enable diagnostic logging for Serilog (not recommended for production):

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Seq"],
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "diagnosticLogging": true
        }
      }
    ]
  }
}
```

### Application Performance Impact

| Operation | Overhead | Notes |
|-----------|----------|-------|
| Log method call | <5 microseconds | Adding event to in-memory queue |
| Batch HTTP POST | 10-50ms | Asynchronous background thread, non-blocking |
| Memory per queued event | ~10 bytes | Negligible (100,000 events = ~1MB) |
| CPU impact | <0.1% | Minimal serialization overhead |

**Total Overhead:** Negligible for typical workloads. Even high-throughput applications (1000 logs/sec) experience <1% performance impact.

### Network Bandwidth

**Typical Log Event Size:**

- Compact JSON (CLEF): ~500 bytes per event
- Standard JSON: ~750 bytes per event

**Bandwidth Calculation:**

```
Events per hour: 10,000
Average size: 500 bytes
Bandwidth = 10,000 * 500 bytes = 5MB/hour ≈ 120MB/day
```

**Production Optimization:**

- Use `compact: true` to reduce payload size by ~30%
- Increase batch size to reduce HTTP header overhead
- Consider log level filtering (Warning+ only)

---

## Troubleshooting

### Logs Not Appearing in Elasticsearch

**Problem:** No logs appear in Kibana after running the application.

**Solutions:**

1. **Verify Elasticsearch Cluster is Running:**
   ```bash
   # Check cluster status
   curl http://localhost:9200/_cluster/health

   # Should return:
   # {"cluster_name":"docker-cluster","status":"green"...}
   ```

2. **Check Configuration:**
   - Verify `Elastic:Endpoints` in `appsettings.Development.json`
   - Ensure URL is correct: `http://localhost:9200` (without trailing slash)
   - Check for typos in configuration keys

3. **Verify Network Connectivity:**
   ```bash
   # From application host, test connection to Elasticsearch
   curl -X POST http://localhost:9200/_health
   ```
   If this fails, Elasticsearch is not reachable from the application.

4. **Check Serilog Configuration:**
   - Ensure Elasticsearch sink is registered in `appsettings.Development.json`
   - Verify sink name: `WriteTo[3]` should be Elasticsearch (0=Console, 1=File, 2=Seq, 3=Elasticsearch)

5. **Verify Index Creation:**
   ```bash
   # List all indices
   curl http://localhost:9200/_cat/indices

   # Search for discordbot indices
   curl "http://localhost:9200/_cat/indices?v" | grep discordbot
   ```

6. **Check Application Logs:**
   - Look at console or file logs for Elasticsearch sink errors
   - Check for connection timeouts or authentication failures
   - Verify that the application is actually logging messages

7. **Create Index Pattern in Kibana:**
   - If logs are present but not showing in Kibana:
   - Go to Management → Index Patterns
   - Create new index pattern: `discordbot-logs-*`
   - Set timestamp field to `@timestamp`

### Logs Not Appearing in Seq

**Problem:** No logs appear in Seq UI after running the application.

**Solutions:**

1. **Verify Seq Server is Running:**
   ```bash
   docker ps | grep seq
   # Should show running seq container

   curl http://localhost:5341
   # Should return Seq UI HTML
   ```

2. **Check Configuration:**
   - Verify `serverUrl` in Serilog WriteTo configuration
   - Ensure URL is correct: `http://localhost:5341` (not `http://localhost:5341/`)
   - Check for typos in configuration keys

3. **Verify Network Connectivity:**
   ```bash
   # From application host, test connection to Seq
   curl -X POST http://localhost:5341/api/events/raw \
     -H "Content-Type: application/vnd.serilog.clef" \
     -d '{"@t":"2025-12-24T10:00:00Z","@m":"Test","@l":"Information"}'
   ```
   If this fails, Seq is not reachable from the application.

4. **Check Minimum Log Level:**
   - Verify application logs are at or above configured minimum level
   - Development default: Debug (all logs)
   - Production default: Warning (only warnings and errors)

### Authentication Failures (401/403 Unauthorized)

**Problem:** Elasticsearch/Seq returns 401/403 errors, logs not ingested.

**Elasticsearch Solutions:**

1. **Verify API Key Configuration:**
   ```bash
   # Check user secrets
   dotnet user-secrets list --project src/DiscordBot.Bot

   # Should show: Elastic:ApiKey = your-key
   ```

2. **Validate API Key in Elasticsearch:**
   ```bash
   # Test API key authentication
   curl -H "Authorization: ApiKey base64-encoded-key" http://localhost:9200/_cluster/health
   ```

3. **Check Environment Variable Override:**
   ```bash
   # Verify environment variable is set correctly
   echo $Elastic__ApiKey  # Linux/macOS
   echo $env:Elastic__ApiKey  # PowerShell
   ```

4. **Test Authentication Manually:**
   ```bash
   # Using API key (preferred for production)
   curl -H "Authorization: ApiKey YOUR_API_KEY" http://localhost:9200

   # Using basic auth (development only)
   curl -u elastic:password http://localhost:9200/_cluster/health
   ```

**Seq Solutions:**

1. **Verify API Key Configuration:**
   ```bash
   # Check user secrets (if using API key)
   dotnet user-secrets list --project src/DiscordBot.Bot

   # Should show: Serilog:WriteTo:X:Args:apiKey = your-key
   ```

2. **Validate API Key in Seq:**
   - Log into Seq UI
   - Go to Settings → API Keys
   - Verify API key exists and is active
   - Check permissions (should allow "Ingest")

3. **Test API Key Manually:**
   ```bash
   curl -X POST http://localhost:5341/api/events/raw \
     -H "Content-Type: application/vnd.serilog.clef" \
     -H "X-Seq-ApiKey: your-api-key" \
     -d '{"@t":"2026-01-05T10:00:00Z","@m":"Test","@l":"Information"}'
   ```
   Should return 201 Created if API key is valid.

### High Memory Usage

**Problem:** Application memory usage increases significantly with centralized logging enabled.

**Elasticsearch Solutions:**

1. **Check Batch Posting Configuration:**
   - Reduce `batchPostingLimit` to post smaller batches more frequently
   - Ensure `period` is not too long (should be 2-5 seconds)

2. **Fix Elasticsearch Connectivity:**
   - Ensure Elasticsearch cluster is reachable
   - Check network connectivity and latency
   - Verify cluster has sufficient resources

3. **Monitor Index Growth:**
   ```bash
   # Check index sizes
   curl "http://localhost:9200/_cat/indices?v" | grep discordbot
   ```
   - Consider implementing ILM policies to delete old indices

4. **Disable Elasticsearch Temporarily:**
   ```json
   {
     "Elastic": {
       "Endpoints": []
     }
   }
   ```

**Seq Solutions:**

1. **Check Queue Size:**
   - If Seq is unavailable, queue grows to `queueSizeLimit` (100,000 events)
   - Monitor queue health via diagnostic logging

2. **Reduce Queue Limit:**
   ```json
   {
     "Serilog": {
       "WriteTo": [
         {
           "Name": "Seq",
           "Args": {
             "queueSizeLimit": 10000
           }
         }
       ]
     }
   }
   ```

3. **Fix Seq Connectivity:**
   - Ensure Seq server is reachable
   - Check network connectivity
   - Verify DNS resolution (if using domain names)

4. **Disable Seq Temporarily:**
   ```json
   {
     "Serilog": {
       "WriteTo": [
         {
           "Name": "Seq",
           "Args": {
             "serverUrl": null
           }
         }
       ]
     }
   }
   ```

### Slow Application Performance

**Problem:** Application slows down after enabling centralized logging.

**Solutions:**

1. **Verify Asynchronous Posting:**
   - Both Elasticsearch and Seq sinks use async posting by default
   - If seeing blocking behavior, check for configuration errors
   - Verify that Serilog configuration has async enabled

2. **Check Network Latency:**
   ```bash
   # Test latency to logging backend
   time curl -I http://localhost:9200  # Elasticsearch
   time curl -I http://localhost:5341  # Seq
   ```
   - If latency is high (>100ms), consider deploying closer to application
   - Use same data center/region for production deployments

3. **Optimize Batch Configuration:**
   ```json
   {
     "Serilog": {
       "WriteTo": [
         {
           "Name": "Elasticsearch",
           "Args": {
             "batchPostingLimit": 200,
             "period": "00:00:05"
           }
         }
       ]
     }
   }
   ```
   - Larger batches = fewer HTTP requests = less overhead
   - Longer periods = higher latency but lower throughput

4. **Reduce Log Volume:**
   - Increase minimum log level to Warning in production
   - Filter out noisy namespaces via `Override` configuration
   - Example: Suppress Debug logs from Microsoft.* namespaces

5. **Monitor Elasticsearch Performance:**
   ```bash
   # Check for slow bulk requests
   curl "http://localhost:9200/_stats"

   # Check cluster health
   curl "http://localhost:9200/_cluster/health?pretty"
   ```

### Missing Structured Properties

**Problem:** Logs appear in Seq but don't have expected properties like `CorrelationId` or `GuildId`.

**Solutions:**

1. **Verify Enrichers are Configured:**
   - `CorrelationIdMiddleware` should be registered in middleware pipeline
   - `InteractionHandler` should set correlation IDs for Discord interactions

2. **Check Log Scope:**
   ```csharp
   // Ensure properties are set via BeginScope or structured logging
   using (_logger.BeginScope(new Dictionary<string, object>
   {
       ["CorrelationId"] = correlationId
   }))
   {
       _logger.LogInformation("Event logged");
   }
   ```

3. **Verify Property Names:**
   - Property names are case-sensitive
   - Use exact names: `CorrelationId`, `GuildId`, `UserId`

4. **Check Sanitization:**
   - Log sanitization may be removing properties (unlikely, but possible)
   - Review `LogSanitizer` configuration

---

## Security

### API Key Management

**Development:**

- Store API keys in user secrets (never commit to repository)
- Use separate API keys for each developer

**Staging/Production:**

- Store API keys in environment variables or secrets management systems:
  - Azure Key Vault
  - AWS Secrets Manager
  - Kubernetes Secrets
  - HashiCorp Vault

**API Key Rotation:**

1. Create new API key in Seq UI
2. Update environment variable or secrets management system
3. Deploy updated configuration
4. Delete old API key after confirming logs still arrive

### Transport Security (TLS)

**Development:**

- HTTP is acceptable for localhost (`http://localhost:5341`)

**Staging/Production:**

- ALWAYS use HTTPS for Seq endpoints (`https://seq.yourdomain.com`)
- Configure TLS termination at reverse proxy (Nginx, Cloudflare, Azure Front Door)
- Validate TLS certificates to prevent man-in-the-middle attacks

**Example HTTPS Configuration:**

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "https://seq.yourdomain.com",
          "apiKey": "your-api-key"
        }
      }
    ]
  }
}
```

### Data Privacy and PII

**What Gets Logged to Seq:**

- Discord user IDs and guild IDs (snowflake IDs, not PII per Discord TOS)
- Command names and interaction types
- Error messages and stack traces
- Custom application properties

**What Does NOT Get Logged:**

- Discord message content (sanitized by application logic)
- Bot tokens or credentials (sanitized by `LogSanitizer`)
- User passwords or secrets
- Personally identifiable information (PII) beyond Discord IDs

**Log Sanitization:**

The `LogSanitizer` automatically removes sensitive data from logs before they reach Seq. See `appsettings.json`:

```json
{
  "LogSanitization": {
    "Enabled": true,
    "CustomPatterns": {},
    "AdditionalSensitiveKeys": []
  }
}
```

**Security Best Practice:** Regularly audit log output to ensure no sensitive data is accidentally logged.

### Access Control

**Seq Authentication:**

1. Enable authentication in Seq UI:
   - Settings → Authentication
   - Enable "Require authentication"
   - Create user accounts for team members

2. Configure role-based access:
   - **Administrator**: Full access to settings, API keys, retention policies
   - **User**: Query logs, create signals, no administrative access
   - **Read-Only**: View logs only, no signals or settings

3. Use API keys for application ingestion:
   - Create dedicated API key for bot application
   - Grant "Ingest" permission only
   - Rotate API keys quarterly

### Network Isolation

**Production Architecture:**

```
┌─────────────────┐
│ Public Internet │
└────────┬────────┘
         │ HTTPS
┌────────▼────────┐
│ Reverse Proxy   │
│ (Nginx/CF)      │
└────────┬────────┘
         │ HTTP (internal network)
┌────────▼────────┐
│ Seq Server      │
│ (Private IP)    │
└─────────────────┘
```

**Benefits:**

- Seq server not directly exposed to internet
- TLS termination at reverse proxy
- Internal HTTP traffic only within trusted network

---

## Related Documentation

- [Environment-Specific Configuration](environment-configuration.md) - Configuration per environment (Development, Staging, Production)
- [Distributed Tracing](tracing.md) - OpenTelemetry tracing integration (complementary to log aggregation)
- [OpenTelemetry Metrics](metrics.md) - Metrics collection with Prometheus export
- [API Endpoints Reference](api-endpoints.md) - REST API documentation

### External Resources

**Elasticsearch:**
- [Elasticsearch Documentation](https://www.elastic.co/guide/en/elasticsearch/reference/current/index.html) - Official Elasticsearch docs
- [Kibana User Guide](https://www.elastic.co/guide/en/kibana/current/index.html) - Official Kibana documentation
- [Serilog.Sinks.Elasticsearch](https://github.com/serilog-contrib/serilog-sinks-elasticsearch) - Elasticsearch sink for Serilog
- [Elastic Cloud](https://cloud.elastic.co) - Managed Elasticsearch hosting
- [Elasticsearch Query DSL](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl.html) - Query language reference

**Seq:**
- [Seq Documentation](https://docs.datalust.co/docs) - Official Seq documentation
- [Serilog.Sinks.Seq](https://github.com/serilog/serilog-sinks-seq) - Seq sink for Serilog
- [Compact Log Event Format (CLEF)](https://docs.datalust.co/docs/posting-raw-events) - JSON log format specification
- [Seq Query Language](https://docs.datalust.co/docs/the-seq-query-language) - SQL-like query syntax

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.1 | 2026-01-05 | Elasticsearch Phase 2 - Added Elastic APM distributed tracing with priority-based sampling (Issue #791) |
| 2.0 | 2026-01-05 | Elasticsearch Phase 1 migration - Elasticsearch as primary backend, Seq as optional alternative (Issue #791) |
| 1.0 | 2025-12-24 | Initial log aggregation documentation with Seq (Issue #106) |

---

*Last Updated: January 5, 2026*
