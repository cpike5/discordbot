# Jaeger and Loki Setup on Linux VPS

**Version:** 1.0
**Last Updated:** 2025-12-31
**Target:** Ubuntu 22.04 LTS / Debian 12

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Option 1: Docker Compose Setup (Recommended)](#option-1-docker-compose-setup-recommended)
- [Option 2: Native Installation](#option-2-native-installation)
- [Configuring the Discord Bot](#configuring-the-discord-bot)
- [Grafana Dashboards](#grafana-dashboards)
- [Querying Logs in Loki](#querying-logs-in-loki)
- [Viewing Traces in Jaeger](#viewing-traces-in-jaeger)
- [Production Hardening](#production-hardening)
- [Troubleshooting](#troubleshooting)
- [Maintenance](#maintenance)
- [Related Documentation](#related-documentation)

---

## Overview

This guide covers installing and configuring **Jaeger** (distributed tracing) and **Loki** (log aggregation) on a Linux VPS to provide comprehensive observability for the Discord Bot Management System.

### What You'll Get

| Component | Purpose | Port |
|-----------|---------|------|
| **Jaeger** | Distributed tracing - view request flows, identify bottlenecks | 16686 (UI), 4317 (OTLP gRPC), 4318 (OTLP HTTP) |
| **Loki** | Log aggregation - search and filter structured logs | 3100 |
| **Promtail** | Log collector - ships logs from files to Loki | N/A |
| **Grafana** | Visualization - dashboards for logs, traces, and metrics | 3000 |

### Why Jaeger + Loki?

- **Jaeger**: Already supported by the application via OpenTelemetry. See [tracing.md](tracing.md) for how tracing is implemented.
- **Loki**: Cost-effective alternative to Seq for log aggregation. Uses the same query patterns as Prometheus (LogQL).
- **Grafana**: Unified visualization for logs, traces, and metrics in one place.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Linux VPS                                       │
│                                                                              │
│  ┌────────────────────┐      ┌────────────────────┐                        │
│  │   Discord Bot      │      │   Promtail         │                        │
│  │   (.NET 8 App)     │      │   (Log Shipper)    │                        │
│  │                    │      │                    │                        │
│  │  ┌──────────────┐  │      │  Reads:            │                        │
│  │  │ OpenTelemetry│──┼──────┼─▶ /var/log/        │                        │
│  │  │ OTLP Export  │  │      │   discordbot/*.log │                        │
│  │  └──────────────┘  │      └─────────┬──────────┘                        │
│  │                    │                │                                    │
│  └─────────┬──────────┘                │                                    │
│            │                           │                                    │
│            │ OTLP (gRPC/HTTP)          │ HTTP Push                          │
│            ▼                           ▼                                    │
│  ┌────────────────────┐      ┌────────────────────┐                        │
│  │      Jaeger        │      │       Loki         │                        │
│  │   (Tracing)        │      │   (Log Storage)    │                        │
│  │                    │      │                    │                        │
│  │  Port 16686 (UI)   │      │  Port 3100 (API)   │                        │
│  │  Port 4317 (gRPC)  │      │                    │                        │
│  │  Port 4318 (HTTP)  │      │                    │                        │
│  └─────────┬──────────┘      └─────────┬──────────┘                        │
│            │                           │                                    │
│            │                           │                                    │
│            └──────────┬────────────────┘                                    │
│                       │                                                     │
│                       ▼                                                     │
│            ┌────────────────────┐                                           │
│            │      Grafana       │                                           │
│            │   (Visualization)  │                                           │
│            │                    │                                           │
│            │   Port 3000 (UI)   │                                           │
│            └────────────────────┘                                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

### Server Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| RAM | 2 GB | 4 GB+ |
| Disk | 20 GB | 50 GB+ |
| CPU | 2 vCPU | 4 vCPU |

**Note:** These are in addition to the Discord Bot's own requirements.

### Required Software

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker and Docker Compose
sudo apt install -y docker.io docker-compose-v2

# Add your user to docker group (re-login required)
sudo usermod -aG docker $USER

# Verify installation
docker --version
docker compose version
```

### Firewall Configuration

```bash
# If using UFW
sudo ufw allow 3000/tcp   # Grafana UI
sudo ufw allow 16686/tcp  # Jaeger UI (optional - can restrict to internal)
```

---

## Option 1: Docker Compose Setup (Recommended)

This is the easiest way to get everything running.

### Create Directory Structure

```bash
# Create observability stack directory
sudo mkdir -p /opt/observability
sudo chown $USER:$USER /opt/observability
cd /opt/observability

# Create data directories
mkdir -p data/jaeger
mkdir -p data/loki
mkdir -p data/grafana
mkdir -p config
```

### Create Docker Compose File

```bash
nano /opt/observability/docker-compose.yml
```

```yaml
version: "3.8"

networks:
  observability:
    driver: bridge

volumes:
  grafana-data:
  loki-data:

services:
  # ============================================
  # JAEGER - Distributed Tracing
  # ============================================
  jaeger:
    image: jaegertracing/all-in-one:1.54
    container_name: jaeger
    restart: unless-stopped
    environment:
      - COLLECTOR_OTLP_ENABLED=true
      - SPAN_STORAGE_TYPE=badger
      - BADGER_EPHEMERAL=false
      - BADGER_DIRECTORY_VALUE=/badger/data
      - BADGER_DIRECTORY_KEY=/badger/key
    ports:
      - "16686:16686"   # Jaeger UI
      - "4317:4317"     # OTLP gRPC receiver
      - "4318:4318"     # OTLP HTTP receiver
    volumes:
      - ./data/jaeger:/badger
    networks:
      - observability
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:16686"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ============================================
  # LOKI - Log Aggregation
  # ============================================
  loki:
    image: grafana/loki:2.9.4
    container_name: loki
    restart: unless-stopped
    ports:
      - "3100:3100"
    volumes:
      - ./config/loki-config.yml:/etc/loki/local-config.yaml:ro
      - loki-data:/loki
    command: -config.file=/etc/loki/local-config.yaml
    networks:
      - observability
    healthcheck:
      test: ["CMD-SHELL", "wget --spider -q http://localhost:3100/ready || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ============================================
  # PROMTAIL - Log Collector
  # ============================================
  promtail:
    image: grafana/promtail:2.9.4
    container_name: promtail
    restart: unless-stopped
    volumes:
      - ./config/promtail-config.yml:/etc/promtail/config.yml:ro
      - /var/log/discordbot:/var/log/discordbot:ro
      - /var/run/docker.sock:/var/run/docker.sock:ro
    command: -config.file=/etc/promtail/config.yml
    networks:
      - observability
    depends_on:
      - loki

  # ============================================
  # GRAFANA - Visualization
  # ============================================
  grafana:
    image: grafana/grafana:10.3.1
    container_name: grafana
    restart: unless-stopped
    environment:
      - GF_SECURITY_ADMIN_USER=admin
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
      - GF_SERVER_ROOT_URL=http://localhost:3000
    ports:
      - "3000:3000"
    volumes:
      - grafana-data:/var/lib/grafana
      - ./config/grafana-datasources.yml:/etc/grafana/provisioning/datasources/datasources.yml:ro
    networks:
      - observability
    depends_on:
      - loki
      - jaeger
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:3000/api/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

### Create Loki Configuration

```bash
nano /opt/observability/config/loki-config.yml
```

```yaml
auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096

common:
  instance_addr: 127.0.0.1
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    kvstore:
      store: inmemory

query_range:
  results_cache:
    cache:
      embedded_cache:
        enabled: true
        max_size_mb: 100

schema_config:
  configs:
    - from: 2024-01-01
      store: tsdb
      object_store: filesystem
      schema: v13
      index:
        prefix: index_
        period: 24h

ruler:
  alertmanager_url: http://localhost:9093

limits_config:
  # Retention settings
  retention_period: 30d

  # Query limits
  max_query_length: 721h
  max_query_parallelism: 32

  # Ingestion limits
  ingestion_rate_mb: 10
  ingestion_burst_size_mb: 20
  per_stream_rate_limit: 5MB
  per_stream_rate_limit_burst: 15MB

compactor:
  working_directory: /loki/compactor
  shared_store: filesystem
  retention_enabled: true
  retention_delete_delay: 2h
  compaction_interval: 10m
```

### Create Promtail Configuration

```bash
nano /opt/observability/config/promtail-config.yml
```

```yaml
server:
  http_listen_port: 9080
  grpc_listen_port: 0

positions:
  filename: /tmp/positions.yaml

clients:
  - url: http://loki:3100/loki/api/v1/push

scrape_configs:
  # Discord Bot Application Logs
  - job_name: discordbot
    static_configs:
      - targets:
          - localhost
        labels:
          job: discordbot
          app: discordbot
          environment: production
          __path__: /var/log/discordbot/*.log

    pipeline_stages:
      # Parse Serilog JSON format
      - json:
          expressions:
            timestamp: "@t"
            level: "@l"
            message: "@m"
            exception: "@x"
            source_context: SourceContext
            correlation_id: CorrelationId
            trace_id: TraceId
            span_id: SpanId
            guild_id: GuildId
            user_id: UserId
            command_name: CommandName

      # Set timestamp from log entry
      - timestamp:
          source: timestamp
          format: RFC3339Nano
          fallback_formats:
            - "2006-01-02T15:04:05.999999999Z07:00"

      # Map Serilog levels to Loki labels
      - labels:
          level:
          source_context:

      # Create structured labels for filtering
      - labels:
          correlation_id:
          trace_id:
          guild_id:
          command_name:

      # Format the output
      - output:
          source: message

  # Docker container logs (optional - for observability stack itself)
  - job_name: docker
    docker_sd_configs:
      - host: unix:///var/run/docker.sock
        refresh_interval: 5s
    relabel_configs:
      - source_labels: ['__meta_docker_container_name']
        regex: '/(.*)'
        target_label: 'container'
      - source_labels: ['__meta_docker_container_label_com_docker_compose_service']
        target_label: 'service'
```

### Create Grafana Datasources Configuration

```bash
nano /opt/observability/config/grafana-datasources.yml
```

```yaml
apiVersion: 1

datasources:
  # Loki for logs
  - name: Loki
    type: loki
    access: proxy
    url: http://loki:3100
    isDefault: true
    editable: false
    jsonData:
      maxLines: 1000
      derivedFields:
        - name: TraceID
          matcherRegex: '"TraceId":"([a-f0-9]+)"'
          url: 'http://localhost:16686/trace/$${__value.raw}'
          datasourceUid: jaeger

  # Jaeger for traces
  - name: Jaeger
    type: jaeger
    access: proxy
    url: http://jaeger:16686
    uid: jaeger
    editable: false
    jsonData:
      tracesToLogsV2:
        datasourceUid: 'loki'
        filterByTraceID: true
        filterBySpanID: true
        customQuery: true
        query: '{job="discordbot"} | json | trace_id="${__trace.traceId}"'
```

### Start the Stack

```bash
cd /opt/observability

# Start all services
docker compose up -d

# Check status
docker compose ps

# View logs
docker compose logs -f
```

### Verify Installation

```bash
# Check Jaeger UI
curl -s http://localhost:16686 | head -1

# Check Loki health
curl -s http://localhost:3100/ready

# Check Grafana
curl -s http://localhost:3000/api/health
```

---

## Option 2: Native Installation

If you prefer not to use Docker, install the components natively.

### Install Jaeger

```bash
# Download Jaeger
cd /tmp
JAEGER_VERSION=1.54.0
wget https://github.com/jaegertracing/jaeger/releases/download/v${JAEGER_VERSION}/jaeger-${JAEGER_VERSION}-linux-amd64.tar.gz

# Extract
tar -xzf jaeger-${JAEGER_VERSION}-linux-amd64.tar.gz
sudo mv jaeger-${JAEGER_VERSION}-linux-amd64 /opt/jaeger

# Create data directory
sudo mkdir -p /var/lib/jaeger
sudo chown $USER:$USER /var/lib/jaeger

# Create systemd service
sudo nano /etc/systemd/system/jaeger.service
```

```ini
[Unit]
Description=Jaeger All-in-One
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/opt/jaeger
ExecStart=/opt/jaeger/jaeger-all-in-one \
    --collector.otlp.enabled=true \
    --span-storage.type=badger \
    --badger.ephemeral=false \
    --badger.directory-key=/var/lib/jaeger/key \
    --badger.directory-value=/var/lib/jaeger/data
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable jaeger
sudo systemctl start jaeger
sudo systemctl status jaeger
```

### Install Loki

```bash
# Download Loki
cd /tmp
LOKI_VERSION=2.9.4
wget https://github.com/grafana/loki/releases/download/v${LOKI_VERSION}/loki-linux-amd64.zip

# Extract
unzip loki-linux-amd64.zip
sudo mv loki-linux-amd64 /opt/loki

# Create directories
sudo mkdir -p /var/lib/loki
sudo mkdir -p /etc/loki
sudo chown $USER:$USER /var/lib/loki

# Copy config (create loki-config.yml as shown above)
sudo cp /opt/observability/config/loki-config.yml /etc/loki/config.yml

# Create systemd service
sudo nano /etc/systemd/system/loki.service
```

```ini
[Unit]
Description=Loki Log Aggregation
After=network.target

[Service]
Type=simple
User=root
ExecStart=/opt/loki -config.file=/etc/loki/config.yml
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

### Install Promtail

```bash
# Download Promtail
cd /tmp
wget https://github.com/grafana/loki/releases/download/v${LOKI_VERSION}/promtail-linux-amd64.zip

# Extract
unzip promtail-linux-amd64.zip
sudo mv promtail-linux-amd64 /opt/promtail

# Create config directory
sudo mkdir -p /etc/promtail

# Copy config (update the Loki URL to localhost:3100)
sudo nano /etc/promtail/config.yml
```

Update the Promtail config for native installation:

```yaml
server:
  http_listen_port: 9080
  grpc_listen_port: 0

positions:
  filename: /tmp/positions.yaml

clients:
  - url: http://localhost:3100/loki/api/v1/push

scrape_configs:
  - job_name: discordbot
    static_configs:
      - targets:
          - localhost
        labels:
          job: discordbot
          app: discordbot
          environment: production
          __path__: /var/log/discordbot/*.log

    pipeline_stages:
      - json:
          expressions:
            timestamp: "@t"
            level: "@l"
            message: "@m"
            source_context: SourceContext
            correlation_id: CorrelationId
            trace_id: TraceId
            guild_id: GuildId
      - timestamp:
          source: timestamp
          format: RFC3339Nano
      - labels:
          level:
          source_context:
          correlation_id:
          trace_id:
          guild_id:
      - output:
          source: message
```

```bash
# Create systemd service
sudo nano /etc/systemd/system/promtail.service
```

```ini
[Unit]
Description=Promtail Log Collector
After=network.target loki.service

[Service]
Type=simple
User=root
ExecStart=/opt/promtail -config.file=/etc/promtail/config.yml
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start all services
sudo systemctl daemon-reload
sudo systemctl enable loki promtail
sudo systemctl start loki promtail
```

### Install Grafana

```bash
# Add Grafana repository
sudo apt install -y apt-transport-https software-properties-common
wget -q -O - https://packages.grafana.com/gpg.key | sudo apt-key add -
echo "deb https://packages.grafana.com/oss/deb stable main" | sudo tee /etc/apt/sources.list.d/grafana.list

# Install
sudo apt update
sudo apt install -y grafana

# Enable and start
sudo systemctl enable grafana-server
sudo systemctl start grafana-server
```

---

## Configuring the Discord Bot

### Update appsettings.Production.json

Update the application configuration to send traces to Jaeger:

```bash
sudo nano /opt/discordbot/appsettings.Production.json
```

Add or update the OpenTelemetry section:

```json
{
  "OpenTelemetry": {
    "ServiceName": "discordbot",
    "ServiceVersion": "0.5.0",
    "Tracing": {
      "Enabled": true,
      "SamplingRatio": 0.1,
      "EnableConsoleExporter": false,
      "OtlpEndpoint": "http://localhost:4317",
      "OtlpProtocol": "grpc"
    },
    "Metrics": {
      "Enabled": true,
      "IncludeRuntimeMetrics": true,
      "IncludeHttpMetrics": true
    }
  }
}
```

### Configure Serilog for JSON Output

Ensure logs are in JSON format for Promtail parsing. Update the Serilog configuration:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/discordbot/discordbot-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Restart the Bot

```bash
sudo systemctl restart discordbot
```

---

## Grafana Dashboards

### Access Grafana

1. Navigate to `http://your-server:3000`
2. Login with `admin` / `admin` (change password immediately)
3. Data sources should be automatically configured

### Create Discord Bot Dashboard

Navigate to **Dashboards** > **New** > **New Dashboard** and add these panels:

#### Panel 1: Log Volume by Level

```logql
sum by (level) (count_over_time({job="discordbot"} [5m]))
```

**Visualization:** Time series with stacked area

#### Panel 2: Error Logs

```logql
{job="discordbot", level="Error"} | json
```

**Visualization:** Logs panel

#### Panel 3: Command Executions

```logql
{job="discordbot"} | json | command_name != "" | line_format "{{.command_name}}"
```

**Visualization:** Logs panel

#### Panel 4: Logs by Guild

```logql
sum by (guild_id) (count_over_time({job="discordbot"} | json | guild_id != "" [1h]))
```

**Visualization:** Bar gauge

#### Panel 5: Recent Traces Link

Add a **Text** panel with markdown:

```markdown
## Recent Traces

[View in Jaeger](http://localhost:16686/search?service=discordbot&limit=20)
```

### Import Pre-built Dashboards

Search Grafana.com dashboards for:
- **Loki Dashboard** (ID: 13639)
- **Jaeger Dashboard** (ID: 16888)

---

## Querying Logs in Loki

### LogQL Basics

Access logs via Grafana's Explore feature or directly query Loki.

#### Find All Discord Bot Logs

```logql
{job="discordbot"}
```

#### Filter by Log Level

```logql
{job="discordbot", level="Error"}
```

```logql
{job="discordbot"} | level =~ "Warning|Error"
```

#### Search Log Content

```logql
{job="discordbot"} |= "Command executed"
```

```logql
{job="discordbot"} |~ "(?i)exception"
```

#### Parse JSON and Filter

```logql
{job="discordbot"} | json | guild_id = "123456789012345678"
```

```logql
{job="discordbot"} | json | command_name = "ping"
```

#### Filter by Correlation ID

```logql
{job="discordbot"} | json | correlation_id = "a1b2c3d4e5f6g7h8"
```

#### Filter by Trace ID (Link to Jaeger)

```logql
{job="discordbot"} | json | trace_id = "abc123def456..."
```

### Aggregation Queries

#### Count Logs by Level

```logql
sum by (level) (count_over_time({job="discordbot"} [1h]))
```

#### Error Rate Over Time

```logql
sum(rate({job="discordbot", level="Error"} [5m]))
```

#### Top Error Messages

```logql
topk(10, sum by (message) (count_over_time({job="discordbot", level="Error"} | json [24h])))
```

#### Logs Per Guild

```logql
sum by (guild_id) (count_over_time({job="discordbot"} | json | guild_id != "" [1h]))
```

---

## Viewing Traces in Jaeger

### Access Jaeger UI

Navigate to `http://your-server:16686`

### Search for Traces

1. Select **Service**: `discordbot`
2. Select **Operation**: (e.g., `discord.command ping`)
3. Set **Lookback**: Last hour
4. Click **Find Traces**

### Filter by Tags

Use the Tags field to filter:

```
discord.guild.id=123456789012345678
```

```
correlation.id=a1b2c3d4e5f6g7h8
```

```
discord.command.name=verify
```

### Analyze Slow Traces

1. Set **Min Duration**: `500ms`
2. Find traces taking longer than expected
3. Expand spans to identify bottlenecks

### Correlate with Logs

1. Note the `TraceId` from a trace
2. In Grafana, query Loki:
   ```logql
   {job="discordbot"} | json | trace_id = "<your-trace-id>"
   ```

---

## Production Hardening

### Secure Grafana

Update Grafana configuration:

```bash
sudo nano /etc/grafana/grafana.ini
```

```ini
[security]
admin_password = YOUR_SECURE_PASSWORD
secret_key = YOUR_SECRET_KEY

[users]
allow_sign_up = false

[auth.anonymous]
enabled = false
```

### Restrict Jaeger Access

Jaeger UI should not be publicly accessible. Use a reverse proxy with authentication:

```nginx
# /etc/nginx/sites-available/observability
server {
    listen 443 ssl;
    server_name observability.yourdomain.com;

    ssl_certificate /etc/ssl/certs/your-cert.crt;
    ssl_certificate_key /etc/ssl/private/your-key.key;

    # Basic auth for Jaeger
    location /jaeger/ {
        auth_basic "Observability";
        auth_basic_user_file /etc/nginx/.htpasswd;

        proxy_pass http://localhost:16686/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # Grafana (has its own auth)
    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

Create htpasswd file:

```bash
sudo apt install apache2-utils
sudo htpasswd -c /etc/nginx/.htpasswd admin
```

### Configure Data Retention

#### Loki Retention

Already configured in `loki-config.yml`:

```yaml
limits_config:
  retention_period: 30d
```

Adjust based on disk space and requirements.

#### Jaeger Retention

For Badger storage, add to the Jaeger command:

```bash
--badger.span-store-ttl=168h  # 7 days
```

### Enable TLS for OTLP

For production, enable TLS on OTLP endpoints:

```yaml
# In docker-compose.yml, add to jaeger service:
environment:
  - COLLECTOR_OTLP_ENABLED=true
  - COLLECTOR_OTLP_TLS_ENABLED=true
  - COLLECTOR_OTLP_TLS_CERT=/certs/server.crt
  - COLLECTOR_OTLP_TLS_KEY=/certs/server.key
volumes:
  - ./certs:/certs:ro
```

Update bot configuration:

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "OtlpEndpoint": "https://localhost:4317"
    }
  }
}
```

### Resource Limits

Add to docker-compose.yml:

```yaml
services:
  jaeger:
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: '1.0'
        reservations:
          memory: 512M

  loki:
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: '1.0'
        reservations:
          memory: 512M
```

---

## Troubleshooting

### Traces Not Appearing in Jaeger

1. **Check OTLP endpoint:**
   ```bash
   # Test connection
   curl -v http://localhost:4317
   ```

2. **Verify bot configuration:**
   ```bash
   sudo grep -r "OtlpEndpoint" /opt/discordbot/appsettings*.json
   ```

3. **Check Jaeger logs:**
   ```bash
   docker compose logs jaeger
   # or
   sudo journalctl -u jaeger -f
   ```

4. **Verify tracing is enabled:**
   ```json
   {
     "OpenTelemetry": {
       "Tracing": {
         "Enabled": true
       }
     }
   }
   ```

### Logs Not Appearing in Loki

1. **Check Promtail logs:**
   ```bash
   docker compose logs promtail
   ```

2. **Verify log file path:**
   ```bash
   ls -la /var/log/discordbot/
   ```

3. **Check Promtail can read logs:**
   ```bash
   docker compose exec promtail cat /var/log/discordbot/discordbot-$(date +%Y-%m-%d).log | head
   ```

4. **Verify Loki is receiving data:**
   ```bash
   curl -s 'http://localhost:3100/loki/api/v1/labels' | jq
   ```

5. **Check log format:**
   Ensure logs are in JSON format (CompactJsonFormatter).

### Grafana Can't Connect to Data Sources

1. **Check service connectivity:**
   ```bash
   docker compose exec grafana wget -qO- http://loki:3100/ready
   docker compose exec grafana wget -qO- http://jaeger:16686
   ```

2. **Verify datasource configuration:**
   Check `/etc/grafana/provisioning/datasources/datasources.yml`

3. **Restart Grafana:**
   ```bash
   docker compose restart grafana
   ```

### High Memory Usage

1. **Check Loki limits:**
   Review `limits_config` in Loki configuration

2. **Reduce retention:**
   ```yaml
   limits_config:
     retention_period: 7d  # Reduce from 30d
   ```

3. **Add resource limits:**
   Use Docker Compose resource limits as shown above

### Disk Space Issues

1. **Check disk usage:**
   ```bash
   df -h
   du -sh /opt/observability/data/*
   ```

2. **Clear old data:**
   ```bash
   # For Loki (data is in Docker volume)
   docker volume rm observability_loki-data
   docker compose up -d loki
   ```

3. **Enable compaction:**
   Verify `compactor` settings in Loki config

---

## Maintenance

### Backup

```bash
# Stop services
cd /opt/observability
docker compose stop

# Backup data
sudo tar -czvf /backup/observability-$(date +%Y%m%d).tar.gz \
    /opt/observability/data \
    /opt/observability/config

# Restart services
docker compose start
```

### Update Components

```bash
cd /opt/observability

# Pull latest images
docker compose pull

# Recreate containers
docker compose up -d

# Verify
docker compose ps
```

### Monitor Stack Health

Create a simple health check script:

```bash
nano /opt/observability/healthcheck.sh
```

```bash
#!/bin/bash

echo "Checking Observability Stack Health..."

# Jaeger
if curl -sf http://localhost:16686 > /dev/null; then
    echo "✓ Jaeger: OK"
else
    echo "✗ Jaeger: FAILED"
fi

# Loki
if curl -sf http://localhost:3100/ready > /dev/null; then
    echo "✓ Loki: OK"
else
    echo "✗ Loki: FAILED"
fi

# Grafana
if curl -sf http://localhost:3000/api/health > /dev/null; then
    echo "✓ Grafana: OK"
else
    echo "✗ Grafana: FAILED"
fi

# Docker containers
echo ""
echo "Container Status:"
docker compose ps --format "table {{.Name}}\t{{.Status}}"
```

```bash
chmod +x /opt/observability/healthcheck.sh
```

### Log Rotation for Promtail Positions

```bash
# Add to crontab
crontab -e
```

```
0 0 * * * find /tmp -name "positions.yaml" -mtime +7 -delete
```

---

## Related Documentation

- [Distributed Tracing](tracing.md) - How tracing is implemented in the bot
- [OpenTelemetry Metrics](metrics.md) - Metrics collection and Prometheus export
- [Log Aggregation with Seq](log-aggregation.md) - Alternative log aggregation solution
- [Linux VPS Deployment](linux-deployment.md) - Base deployment guide
- [Grafana Dashboards Specification](grafana-dashboards-specification.md) - Dashboard design patterns

### External Resources

- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Loki Documentation](https://grafana.com/docs/loki/latest/)
- [Promtail Documentation](https://grafana.com/docs/loki/latest/clients/promtail/)
- [LogQL Reference](https://grafana.com/docs/loki/latest/logql/)
- [Grafana Documentation](https://grafana.com/docs/grafana/latest/)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-31 | Initial documentation |

---

*Last Updated: December 31, 2025*
