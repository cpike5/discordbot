# Grafana Dashboards

This directory contains Grafana dashboard definitions for monitoring the Discord bot using Loki logs and Jaeger traces.

## Dashboard Files

### 1. discordbot-logs-dashboard.json
**Log-focused dashboard** - Analyze logs from Loki with powerful filtering and search.

**Sections:**
- **Log Overview** - High-level log statistics
  - Total Logs stat
  - Errors stat
  - Warnings stat
  - Error Rate gauge
  - Log Volume by Level stacked bar chart
  - Logs by Source Context chart
- **Error Analysis** - Deep dive into errors
  - Error & Warning Logs viewer with JSON parsing
  - Top Error Messages table (by frequency)
  - Error Trend chart
- **Discord Commands** - Command execution tracking
  - Command Execution Logs viewer
  - Commands by Type chart (from logs)
  - Logs by Guild chart
- **Request Tracing** - Correlation and trace investigation
  - Logs by Correlation ID viewer
  - Logs with Trace Context viewer
- **Live Log Stream** - Real-time log monitoring
  - All Logs (Filtered) viewer with search variable

**Variables:**
- `search` - Free text search filter
- `command` - Filter by command name
- `correlation_id` - Filter by correlation ID

**Data Sources:** Loki
**Refresh Rate:** 10 seconds
**Default Time Range:** Last 1 hour

### 2. discordbot-observability-dashboard.json
**Unified observability dashboard** - Correlate logs and traces in one view.

**Sections:**
- **Overview** - At-a-glance metrics derived from logs
  - Total Logs stat
  - Commands (from log count) stat
  - Errors stat
  - Warnings stat
  - Traces (unique TraceId count) stat
  - Active Guilds (unique GuildId count) stat
- **Log Analysis** - Log volume and trends
  - Log Volume by Level chart
  - Logs by Source Context chart
- **Trace Investigation** - Deep dive into distributed traces
  - Logs for Trace ID viewer (variable-based)
  - Error Traces table with clickable TraceId links to Jaeger
- **Guild/User Investigation** - Context-specific log analysis
  - Logs for Guild ID viewer
  - Logs for User ID viewer
- **Error Deep Dive** - Exception analysis
  - Exceptions with Stack Traces viewer

**Variables:**
- `trace_id` - Trace ID from Jaeger to view related logs
- `guild_id` - Discord Guild ID filter
- `user_id` - Discord User ID filter

**Data Sources:** Loki + Jaeger (traces via links)
**Refresh Rate:** 10 seconds
**Default Time Range:** Last 1 hour

## Prerequisites

### Required Data Sources

| Data Source | URL | Purpose |
|-------------|-----|---------|
| Loki | `http://localhost:3100` | Log aggregation and querying |
| Jaeger | `http://localhost:16686` | Distributed tracing (optional, for trace links) |

## Installation

### Grafana Dashboard Import

1. **Log into Grafana** and navigate to Dashboards > Import
2. **Copy the JSON content** from the desired dashboard file
3. **Paste into the import field** or upload the JSON file
4. **Select your Loki data source** in the import wizard
5. **Click Import** to create the dashboard

**Automated Import (Grafana API):**

```bash
# Set your Grafana credentials
GRAFANA_URL="http://localhost:3000"
GRAFANA_API_KEY="your-api-key-here"

# Import Logs dashboard
curl -X POST -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @discordbot-logs-dashboard.json \
  "$GRAFANA_URL/api/dashboards/db"

# Import Observability dashboard
curl -X POST -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @discordbot-observability-dashboard.json \
  "$GRAFANA_URL/api/dashboards/db"
```

## Loki Data Source Setup

### Adding Loki Data Source

1. **Navigate to:** Configuration > Data Sources > Add data source
2. **Select:** Loki
3. **Configure:**
   - **Name:** `Loki`
   - **URL:** `http://localhost:3100` (or your Loki endpoint)
4. **Click:** Save & Test

### Correlating Logs with Traces

The Observability dashboard supports click-through from logs to Jaeger traces:

1. **Ensure Jaeger data source is configured** with the name `Jaeger`
2. **Logs with TraceId** will show clickable links in the "Error Traces" table
3. **Click the TraceId** to open the trace in Grafana's Explore view

### Required Loki Labels

The dashboards expect the following labels on logs (configured in `appsettings.Production.json`):

| Label | Value | Purpose |
|-------|-------|---------|
| `app` | `discordbot` | Filter logs by application |
| `level` | `Error`, `Warning`, `Information`, etc. | Filter by severity |

### Structured Log Properties

For full functionality, logs should include these JSON properties (configured via Serilog enrichment):

| Property | Description | Dashboard Usage |
|----------|-------------|-----------------|
| `SourceContext` | Logger name (e.g., `DiscordBot.Bot.Services.BotHostedService`) | Log breakdown by component |
| `CommandName` | Discord command being executed | Command tracking |
| `GuildId` | Discord guild ID | Guild-specific filtering |
| `UserId` | Discord user ID | User-specific filtering |
| `TraceId` | OpenTelemetry trace ID | Trace correlation |
| `CorrelationId` | Request correlation ID | Request flow tracing |
| `Exception` | Exception details with stack trace | Error analysis |

## Dashboard Navigation

The dashboards are designed for navigation flow:

```
discordbot-logs-dashboard
    ├── Trace links → Jaeger Explore
    ├── Variable filters for deep investigation
    └── Links → discordbot-observability

discordbot-observability-dashboard
    ├── Logs from Loki
    ├── Trace links → Jaeger Explore
    └── Guild/User investigation
```

## Customization

### Adding Variables

To add additional filtering variables:

1. Navigate to **Dashboard Settings > Variables**
2. Add a new variable:
   - **Name:** `source_context`
   - **Type:** Query
   - **Data source:** Loki
   - **Query:** `label_values({app="discordbot"}, SourceContext)`
3. Update panel queries to use the variable:
   ```logql
   {app="discordbot"} | json | SourceContext =~ "$source_context"
   ```

### Changing Refresh Rates

Edit the dashboard JSON's `refresh` field:

```json
"refresh": "10s",  // Options: "5s", "10s", "30s", "1m", "5m", "15m", "30m", "1h"
```

## Troubleshooting

### No Data Appearing in Panels

**Check Loki data source:**
1. Grafana > Configuration > Data Sources > Loki
2. Click "Test" to verify connectivity
3. Verify Loki URL is correct

**Verify logs are being sent:**
```bash
# Query Loki directly
curl -G 'http://localhost:3100/loki/api/v1/query' \
  --data-urlencode 'query={app="discordbot"}'
```

**Verify label names match:**
- Check your Serilog Loki sink configuration in `appsettings.Production.json`
- Ensure labels `app` and `level` are configured

### Dashboard Panels Show "N/A"

**Time range issue:**
- Adjust dashboard time range to include data
- Check if logs have recent timestamps

**Missing labels:**
- Some panels filter by labels that may not exist in your data
- Remove or adjust label filters in panel queries

**JSON parsing:**
- Ensure logs are formatted as JSON
- Check that expected properties exist in log entries

## Related Documentation

- [Log Aggregation](../articles/log-aggregation.md) - Seq and Loki integration for log analysis
- [Jaeger/Loki Setup](../articles/jaeger-loki-setup.md) - Full observability stack deployment

## Support

For questions or issues with these dashboards:
- Open an issue on GitHub
- Check the Discord bot documentation
- Review Grafana documentation: https://grafana.com/docs/
