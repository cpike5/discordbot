# Elastic Stack Setup

**Version:** 1.0
**Last Updated:** 2026-01-06
**Status:** Active

---

## Overview

Quick-start guide for setting up Elasticsearch, Kibana, and Elastic APM Server for local development. This provides centralized logging, log search/analysis, and distributed tracing capabilities.

For comprehensive logging documentation, see [Log Aggregation](log-aggregation.md).

---

## Prerequisites

- Docker Desktop installed and running
- At least 4GB RAM available for Docker
- Ports 9200, 5601, 8200 available

---

## Quick Start with Docker Compose

The easiest way to get started is using Docker Compose to start all services together.

### 1. Create docker-compose.yml

Create a file named `docker-compose.yml` in your project root or a dedicated folder:

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

volumes:
  elasticsearch-data:
```

**Configuration Notes:**
- `discovery.type=single-node` - Single-node cluster for development
- `xpack.security.enabled=false` - Disables authentication (development only)
- `ES_JAVA_OPTS=-Xms512m -Xmx512m` - Allocates 512MB heap (adjust if needed)
- `elasticsearch-data` - Persists indices across restarts

### 2. Start the Stack

```bash
# Start all services in background
docker-compose up -d

# View logs (optional)
docker-compose logs -f
```

### 3. Wait for Services to Start

Elasticsearch takes 30-60 seconds to become ready. Monitor startup:

```bash
# Check service status
docker-compose ps

# Watch Elasticsearch logs
docker-compose logs -f elasticsearch
```

### 4. Verify Services are Running

```bash
# Test Elasticsearch
curl http://localhost:9200/_cluster/health
# Should return: {"cluster_name":"docker-cluster","status":"green"...}

# Test Kibana (in browser)
# Open: http://localhost:5601

# Test APM Server
curl http://localhost:8200
# Should return: {"ok":{"build_date":"...","version":"..."}}
```

**Service URLs:**
- Elasticsearch: `http://localhost:9200`
- Kibana: `http://localhost:5601`
- APM Server: `http://localhost:8200`

---

## Individual Container Setup

If you prefer not to use Docker Compose, you can run each service individually.

### Start Elasticsearch

```bash
docker run -d \
  --name elasticsearch \
  -p 9200:9200 \
  -p 9300:9300 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  -e "ES_JAVA_OPTS=-Xms512m -Xmx512m" \
  -v elasticsearch-data:/usr/share/elasticsearch/data \
  docker.elastic.co/elasticsearch/elasticsearch:latest
```

### Start Kibana

```bash
docker run -d \
  --name kibana \
  -p 5601:5601 \
  -e ELASTICSEARCH_HOSTS=http://elasticsearch:9200 \
  --link elasticsearch \
  docker.elastic.co/kibana/kibana:latest
```

### Start APM Server

```bash
docker run -d \
  --name apm-server \
  -p 8200:8200 \
  --link elasticsearch \
  -e output.elasticsearch.hosts=["http://elasticsearch:9200"] \
  -e apm-server.host="0.0.0.0:8200" \
  -e apm-server.secret_token="" \
  docker.elastic.co/apm/apm-server:latest
```

**Note:** Start Elasticsearch first, wait for it to be ready, then start Kibana and APM Server.

---

## Configuring the Bot

### 1. Configure APM Server URL (User Secrets)

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "ElasticApm:ServerUrl" "http://localhost:8200"
```

### 2. Configure Elasticsearch Sink (appsettings.Development.json)

Add or verify the Elasticsearch sink configuration in `src/DiscordBot.Bot/appsettings.Development.json`:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/discordbot-.log",
          "rollingInterval": "Day"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://localhost:9200",
          "indexFormat": "discordbot-logs-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true,
          "autoRegisterTemplateVersion": "ESv7"
        }
      }
    ]
  }
}
```

**Configuration Notes:**
- `nodeUris` - Elasticsearch endpoint (no trailing slash)
- `indexFormat` - Daily indices for easier management
- `autoRegisterTemplate` - Automatically creates index templates

### 3. Verify Configuration (Optional)

For production or if authentication is needed:

```bash
# Set Elasticsearch API key (if needed)
dotnet user-secrets set "Elastic:ApiKey" "your-dev-api-key"

# Set APM secret token (if authentication is enabled)
dotnet user-secrets set "ElasticApm:SecretToken" "your-apm-secret-token"
```

---

## Verifying the Setup

### 1. Run the Bot

```bash
dotnet run --project src/DiscordBot.Bot
```

### 2. Execute a Discord Command

In Discord, run a simple command:
```
/ping
```

### 3. Check Logs in Kibana

1. Open Kibana: `http://localhost:5601`
2. Go to **Management → Stack Management → Data Views**
3. Create data view:
   - Name: `DiscordBot Logs`
   - Index pattern: `discordbot-logs-*`
   - Timestamp field: `@timestamp`
4. Go to **Analytics → Discover**
5. Select the `discordbot-logs-*` data view
6. You should see log entries from the bot

### 4. Check Traces in APM UI

1. In Kibana, go to **Observability → APM**
2. You should see `DiscordBot` service listed
3. Click on the service to view:
   - Transaction timeline
   - Response times
   - Error rates
   - Service map showing dependencies

### 5. Verify Indices in Elasticsearch

```bash
# List all indices
curl http://localhost:9200/_cat/indices?v

# Search for discordbot indices
curl http://localhost:9200/_cat/indices?v | grep discordbot
```

You should see indices like `discordbot-logs-2026.01.06`.

---

## Troubleshooting

### Elasticsearch Won't Start

**Problem:** Container exits immediately or shows out-of-memory errors.

**Solutions:**

1. **Increase Docker memory allocation:**
   - Docker Desktop → Settings → Resources
   - Increase memory to at least 4GB

2. **Reduce Elasticsearch heap size:**
   ```yaml
   environment:
     - ES_JAVA_OPTS=-Xms256m -Xmx256m  # Lower heap for resource-constrained systems
   ```

3. **Check Docker logs:**
   ```bash
   docker logs elasticsearch
   ```

### Kibana Can't Connect to Elasticsearch

**Problem:** Kibana shows "Unable to connect to Elasticsearch" error.

**Solutions:**

1. **Verify Elasticsearch is running:**
   ```bash
   curl http://localhost:9200/_cluster/health
   ```

2. **Check Kibana logs:**
   ```bash
   docker logs kibana
   ```

3. **Restart Kibana after Elasticsearch is ready:**
   ```bash
   docker-compose restart kibana
   ```

### APM Traces Not Appearing

**Problem:** No traces appear in APM UI after executing commands.

**Solutions:**

1. **Verify APM Server is running:**
   ```bash
   curl http://localhost:8200
   # Should return: {"ok":{...}}
   ```

2. **Check APM configuration in user secrets:**
   ```bash
   dotnet user-secrets list --project src/DiscordBot.Bot
   # Should show: ElasticApm:ServerUrl = http://localhost:8200
   ```

3. **Verify APM is enabled in appsettings:**
   - Check `appsettings.Development.json` for `ElasticApm:Enabled = true`
   - If disabled, set to `true` or remove the setting (default is enabled)

4. **Check APM Server logs:**
   ```bash
   docker logs apm-server
   ```

5. **Verify the bot is sending traces:**
   - Look for APM-related log messages in bot console output
   - Check for connection errors or timeout messages

### Logs Not Appearing in Kibana

**Problem:** Elasticsearch is running but no logs appear in Kibana.

**Solutions:**

1. **Verify index creation:**
   ```bash
   curl http://localhost:9200/_cat/indices?v | grep discordbot
   ```

2. **Check Serilog configuration:**
   - Verify Elasticsearch sink is configured in `appsettings.Development.json`
   - Ensure `nodeUris` matches Elasticsearch endpoint

3. **Create index pattern manually:**
   - Kibana → Management → Data Views
   - Create new data view: `discordbot-logs-*`
   - Set timestamp field: `@timestamp`

4. **Check bot application logs:**
   - Look for Elasticsearch connection errors in console or file logs
   - Verify network connectivity: `curl http://localhost:9200/_cluster/health`

5. **Verify time range in Kibana Discover:**
   - Click the time picker (top-right)
   - Set to "Last 15 minutes" or "Today"
   - Logs may be outside your selected time range

---

## Resource Usage

### Typical Development Setup

| Service | Memory | CPU | Disk |
|---------|--------|-----|------|
| Elasticsearch | 512MB-1GB | 10-20% | 1-5GB (logs) |
| Kibana | 256MB-512MB | 5-10% | Minimal |
| APM Server | 128MB-256MB | 5% | Minimal |
| **Total** | **~1-2GB** | **20-35%** | **1-5GB** |

**Notes:**
- Resource usage scales with log volume and query complexity
- Elasticsearch disk usage grows daily; consider retention policies for long-term development
- Multi-core systems will distribute CPU load better

### Resource Optimization

**For resource-constrained systems:**

1. **Reduce Elasticsearch heap:**
   ```yaml
   ES_JAVA_OPTS=-Xms256m -Xmx256m
   ```

2. **Stop services when not actively debugging:**
   ```bash
   docker-compose stop
   ```

3. **Use log level filtering:**
   - Set minimum log level to `Warning` in `appsettings.Development.json`
   - Reduces log volume and indexing overhead

4. **Disable APM if not needed:**
   ```json
   {
     "ElasticApm": {
       "Enabled": false
     }
   }
   ```

---

## Stopping and Cleaning Up

### Stop All Services (Preserve Data)

```bash
# Stop all services
docker-compose stop

# Start again later
docker-compose start
```

### Stop and Remove Containers (Preserve Data)

```bash
# Stop and remove containers
docker-compose down

# Data persists in volumes
# Start again with: docker-compose up -d
```

### Remove Everything (Including Data)

```bash
# Stop and remove containers, networks, and volumes
docker-compose down -v

# WARNING: This deletes all logs and indices
```

### Individual Container Management

```bash
# Stop a specific service
docker stop elasticsearch
docker stop kibana
docker stop apm-server

# Remove a specific container
docker rm elasticsearch

# Remove a specific volume (deletes data)
docker volume rm discordbot_elasticsearch-data
```

### Clean Up Old Indices

```bash
# List indices with sizes
curl http://localhost:9200/_cat/indices?v

# Delete old indices (e.g., older than 30 days)
curl -X DELETE http://localhost:9200/discordbot-logs-2025.12.*
```

---

## Related Documentation

- [Log Aggregation](log-aggregation.md) - Comprehensive logging documentation, Serilog configuration, query examples
- [Bot Performance Dashboard](bot-performance-dashboard.md) - Performance monitoring and metrics dashboard
- [Audit Log System](audit-log-system.md) - Audit logging with fluent builder API

---

## Next Steps

1. **Create Kibana Dashboards:**
   - Visualize command usage, error rates, and response times
   - See [Kibana Dashboards Guide](kibana-dashboards.md) for detailed instructions

2. **Set Up Alerts:**
   - Configure alerts for error spikes or performance degradation
   - Use Kibana Alerting or Elastic Watcher

3. **Explore APM Features:**
   - Service maps showing component dependencies
   - Transaction correlation across services
   - Error tracking and stack traces

4. **Configure Retention Policies:**
   - Set up Index Lifecycle Management (ILM) for automatic cleanup
   - See [Log Aggregation - Data Retention](log-aggregation.md#data-retention-strategies) for guidance
