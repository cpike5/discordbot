# Centralized Log Aggregation with Seq

**Version:** 1.0
**Last Updated:** 2025-12-24
**Target Framework:** .NET 8 with Serilog and Seq

---

## Overview

The Discord Bot Management System implements centralized log aggregation using Seq, a structured logging server that provides powerful querying and analysis capabilities for application logs. Seq integrates seamlessly with Serilog to provide real-time log search, filtering, and visualization.

### What is Seq?

Seq is a centralized log server designed specifically for structured logging. Unlike traditional log aggregation tools that treat logs as plain text, Seq understands the structured properties of log events, enabling powerful queries across fields like `CorrelationId`, `GuildId`, `UserId`, `TraceId`, and custom properties.

### Key Features

- **Structured Log Search**: Query logs by any structured property (correlation IDs, guild IDs, user IDs, etc.)
- **Real-Time Streaming**: View logs as they arrive with live tail functionality
- **Signal-Based Alerting**: Create alerts based on log patterns and thresholds
- **Retention Policies**: Automatic log retention management and archival
- **Dashboard Visualizations**: Build custom dashboards for operational metrics
- **Correlation with Tracing**: Link logs to distributed traces via correlation IDs and trace IDs

### Benefits for This Project

- **Unified Log View**: Aggregate logs from multiple bot instances in a single interface
- **Request Tracking**: Trace complete Discord interaction flows via correlation IDs
- **User-Specific Debugging**: Filter logs by specific Discord users or guilds
- **Performance Analysis**: Identify slow queries and performance bottlenecks
- **Production Troubleshooting**: Debug production issues without SSH access to servers
- **Compliance and Auditing**: Retain and search historical logs for compliance requirements

### Integration with Existing Logging

Seq complements the existing file-based logging infrastructure:

- **File Logs**: Remain as a local backup and for scenarios where Seq is unavailable
- **Console Logs**: Continue to provide immediate feedback during development
- **Seq Sink**: Asynchronously sends structured logs to Seq without impacting performance

All three logging outputs (console, file, Seq) receive the same structured log events, ensuring consistency across observability channels.

---

## Architecture

### Serilog to Seq Data Flow

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
│  │ - Correlation│   │ - Token      │   │ - Console │ │
│  │ - TraceId    │   │   Sanitizer  │   │ - File    │ │
│  │ - SpanId     │   │ - PII        │   │ - Seq     │ │
│  └──────────────┘   └──────────────┘   └───────────┘ │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ Seq Sink (Async Batch Posting)                        │
│                                                        │
│  - Queue log events (queueSizeLimit: 100,000)         │
│  - Batch events (batchPostingLimit)                   │
│  - Post every N seconds (period)                      │
│  - Retry failed batches with backoff                  │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼ HTTP POST (JSON)
┌────────────────────────────────────────────────────────┐
│ Seq Server                                             │
│                                                        │
│  - Ingests structured log events                      │
│  - Indexes properties for fast queries                │
│  - Applies retention policies                         │
│  - Provides query API and web UI                      │
└────────────────────────────────────────────────────────┘
                         │
                         ▼
                   Seq Web UI
              (Query, Filter, Visualize)
```

### Structured Properties

Every log event sent to Seq includes the following structured properties:

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

### Base Configuration (appsettings.json)

The base configuration file defines default Seq settings with Seq disabled (null serverUrl):

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": null,
          "apiKey": null,
          "batchPostingLimit": 100,
          "period": "00:00:02",
          "compact": true,
          "queueSizeLimit": 100000
        }
      }
    ]
  }
}
```

### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `serverUrl` | string | null | Seq server URL (e.g., "http://localhost:5341"). When null, Seq sink is disabled. |
| `apiKey` | string | null | Seq API key for authentication. Required for production, optional for local. |
| `batchPostingLimit` | int | 100 | Maximum events per batch. Higher = more efficient, but larger HTTP payloads. |
| `period` | TimeSpan | 00:00:02 | Batch posting interval. Lower = more real-time, but more HTTP requests. |
| `compact` | bool | true | Use compact JSON format (CLEF). Reduces payload size by ~30%. |
| `queueSizeLimit` | int | 100000 | Maximum queued events. When exceeded, oldest events are dropped. |

**Compact Format (CLEF):**

When `compact: true`, Seq uses the Compact Log Event Format (CLEF), which is more efficient than standard JSON:

```json
{"@t":"2025-12-24T10:30:45.123Z","@m":"Command ping executed","@l":"Information","CorrelationId":"a1b2c3d4"}
```

vs. standard JSON:

```json
{
  "Timestamp": "2025-12-24T10:30:45.123Z",
  "MessageTemplate": "Command {CommandName} executed",
  "Level": "Information",
  "Properties": {
    "CommandName": "ping",
    "CorrelationId": "a1b2c3d4"
  }
}
```

### Environment-Specific Configuration

#### Development (appsettings.Development.json)

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ]
  }
}
```

**Configuration Details:**

- **serverUrl**: Points to local Seq instance (see [Local Development Setup](#local-development-setup))
- **apiKey**: Not required for local development
- **batchPostingLimit**: Inherits default (100 events)
- **period**: Inherits default (2 seconds)

#### Staging (appsettings.Staging.json)

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq-staging:5341",
          "batchPostingLimit": 500
        }
      }
    ]
  }
}
```

**Configuration Details:**

- **serverUrl**: Internal DNS name for staging Seq server
- **apiKey**: Configured via environment variable or user secrets (required)
- **batchPostingLimit**: 500 events (higher throughput for staging testing)
- **period**: Inherits default (2 seconds)

#### Production (appsettings.Production.json)

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "https://seq.yourdomain.com",
          "batchPostingLimit": 1000,
          "period": "00:00:05"
        }
      }
    ]
  }
}
```

**Configuration Details:**

- **serverUrl**: Public HTTPS endpoint (replace with actual domain)
- **apiKey**: Configured via environment variable or secrets management (required)
- **batchPostingLimit**: 1000 events (maximum efficiency for high-volume production)
- **period**: 5 seconds (reduces HTTP request frequency)

### Environment Variable Overrides

For production deployments, override sensitive configuration via environment variables:

```bash
# Linux/macOS
export Serilog__WriteTo__2__Args__serverUrl="https://seq.yourdomain.com"
export Serilog__WriteTo__2__Args__apiKey="your-api-key-here"

# Windows PowerShell
$env:Serilog__WriteTo__2__Args__serverUrl="https://seq.yourdomain.com"
$env:Serilog__WriteTo__2__Args__apiKey="your-api-key-here"

# Docker
docker run -e Serilog__WriteTo__2__Args__serverUrl="https://seq.yourdomain.com" \
           -e Serilog__WriteTo__2__Args__apiKey="your-api-key-here" \
           discordbot:latest
```

**Note:** `WriteTo__2` corresponds to the third sink (0-indexed: Console, File, Seq).

### User Secrets (Development)

For local development with API key authentication:

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Serilog:WriteTo:2:Args:apiKey" "your-dev-api-key"
```

**Security Best Practice:** NEVER commit API keys to configuration files. Always use user secrets (development) or environment variables/secrets management (staging/production).

---

## Local Development Setup

### Running Seq Locally with Docker

The easiest way to run Seq locally is using Docker:

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

### First-Time Seq Setup

1. **Start Seq Container:**
   ```bash
   docker run -d --name seq -p 5341:80 -e ACCEPT_EULA=Y -v seq-data:/data datalust/seq:latest
   ```

2. **Access Seq UI:**
   - Navigate to `http://localhost:5341`
   - No authentication required for local development

3. **Configure Bot:**
   - Ensure `appsettings.Development.json` has `serverUrl: "http://localhost:5341"`
   - Run the bot: `dotnet run --project src/DiscordBot.Bot`

4. **Verify Logs Arriving:**
   - Execute a Discord command (e.g., `/ping`)
   - Refresh Seq UI - logs should appear within 2 seconds

5. **(Optional) Create API Key:**
   - In Seq UI, go to Settings → API Keys
   - Click "Add API Key"
   - Title: "Development Bot"
   - Click "Save Changes"
   - Copy API key and add to user secrets

### Stopping and Restarting Seq

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

## Querying Logs in Seq

### Basic Search

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

### Self-Hosted Seq

#### Docker Deployment

**Standalone Container:**

```bash
docker run -d \
  --name seq \
  --restart unless-stopped \
  -p 5341:80 \
  -e ACCEPT_EULA=Y \
  -e SEQ_API_CANONICALURI=https://seq.yourdomain.com \
  -v /var/seq/data:/data \
  datalust/seq:latest
```

**Behind Reverse Proxy (Nginx):**

```nginx
server {
    listen 443 ssl;
    server_name seq.yourdomain.com;

    ssl_certificate /etc/ssl/certs/seq.crt;
    ssl_certificate_key /etc/ssl/private/seq.key;

    location / {
        proxy_pass http://localhost:5341;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

#### Windows Service

1. Download Seq installer from https://datalust.co/download
2. Run installer (installs as Windows Service)
3. Configure data storage path
4. Set up HTTPS binding (IIS or reverse proxy)
5. Configure authentication (API keys)

**Service Management:**

```powershell
# Start Seq service
Start-Service Seq

# Stop Seq service
Stop-Service Seq

# Check status
Get-Service Seq
```

### Cloud-Hosted Seq

#### Seq Cloud (SaaS)

Datalust offers managed Seq hosting at https://datalust.co/pricing:

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
   - Verify `serverUrl` in `appsettings.Development.json` or environment-specific config
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

4. **Check Serilog Configuration:**
   - Ensure Seq sink is registered in `appsettings.json`
   - Verify sink index: `WriteTo[2]` should be Seq (0=Console, 1=File, 2=Seq)

5. **Enable Diagnostic Logging:**
   ```json
   {
     "Serilog": {
       "WriteTo": [
         {
           "Name": "Seq",
           "Args": {
             "serverUrl": "http://localhost:5341",
             "diagnosticLogging": true
           }
         }
       ]
     }
   }
   ```
   Check console output for Seq sink errors.

6. **Check Minimum Log Level:**
   - Verify application logs are at or above configured minimum level
   - Development default: Debug (all logs)
   - Production default: Warning (only warnings and errors)

### Authentication Failures (401 Unauthorized)

**Problem:** Seq returns 401 errors, logs not ingested.

**Solutions:**

1. **Verify API Key Configuration:**
   ```bash
   # Check user secrets
   dotnet user-secrets list --project src/DiscordBot.Bot

   # Should show: Serilog:WriteTo:2:Args:apiKey = your-key
   ```

2. **Validate API Key in Seq:**
   - Log into Seq UI
   - Go to Settings → API Keys
   - Verify API key exists and is active
   - Check permissions (should allow "Ingest")

3. **Check Environment Variable Override:**
   ```bash
   # Verify environment variable is set correctly
   echo $Serilog__WriteTo__2__Args__apiKey  # Linux/macOS
   echo $env:Serilog__WriteTo__2__Args__apiKey  # PowerShell
   ```

4. **Test API Key Manually:**
   ```bash
   curl -X POST http://localhost:5341/api/events/raw \
     -H "Content-Type: application/vnd.serilog.clef" \
     -H "X-Seq-ApiKey: your-api-key" \
     -d '{"@t":"2025-12-24T10:00:00Z","@m":"Test","@l":"Information"}'
   ```
   Should return 201 Created if API key is valid.

### High Memory Usage

**Problem:** Application memory usage increases significantly with Seq enabled.

**Solutions:**

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

**Problem:** Application slows down after enabling Seq.

**Solutions:**

1. **Verify Asynchronous Posting:**
   - Seq sink uses async posting by default
   - If seeing blocking behavior, check for configuration errors

2. **Increase Batch Period:**
   ```json
   {
     "Serilog": {
       "WriteTo": [
         {
           "Name": "Seq",
           "Args": {
             "period": "00:00:05"
           }
         }
       ]
     }
   }
   ```
   Reduces HTTP request frequency.

3. **Check Network Latency:**
   - If Seq server has high latency, HTTP posts will be slow
   - Consider deploying Seq closer to application (same data center/region)

4. **Reduce Log Volume:**
   - Increase minimum log level to Warning in production
   - Filter out noisy namespaces via `Override` configuration

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

- [Seq Documentation](https://docs.datalust.co/docs) - Official Seq documentation
- [Serilog.Sinks.Seq](https://github.com/serilog/serilog-sinks-seq) - Seq sink for Serilog
- [Compact Log Event Format (CLEF)](https://docs.datalust.co/docs/posting-raw-events) - JSON log format specification
- [Seq Query Language](https://docs.datalust.co/docs/the-seq-query-language) - SQL-like query syntax

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-24 | Initial log aggregation documentation (Issue #106) |

---

*Last Updated: December 24, 2025*
