# Grafana Dashboards and Prometheus Alerts

This directory contains Grafana dashboard definitions and Prometheus alerting rules for monitoring the Discord bot's performance, business metrics, and SLO compliance.

## Dashboard Files

### 1. discordbot-dashboard.json
**Main operational dashboard** - Comprehensive view of bot health and performance.

**Sections:**
- **SLO Compliance** - Quick view of key SLO metrics with links to full SLO dashboard
  - Command Success Rate SLO gauge
  - Error Budget Remaining gauge
  - SLO Quick View table
- **Bot Overview** - High-level bot statistics
  - Command success rate
  - Active guilds
  - Command rate by type
- **Command Performance** - Command execution metrics
  - Command latency percentiles (p95, p50)
  - Active concurrent commands
- **Rate Limiting & Errors** - Error tracking
  - Rate limit violations
  - Failed commands by type
- **API Performance** - HTTP API monitoring
  - Request rate by endpoint
  - API error rate (5xx)
  - Active API requests
  - API latency (p99)
  - Requests by status code
- **System Health** - .NET runtime metrics
  - GC collections per second
  - Thread pool queue depth

**Refresh Rate:** 10 seconds
**Default Time Range:** Last 1 hour

### 2. discordbot-business-dashboard.json
**Business metrics dashboard** - Track user engagement, guild growth, and feature adoption.

**Sections:**
- **Guild Activity**
  - Guilds joined today
  - Guilds left today
  - Active guilds (24h)
  - Guild growth rate (joins - leaves per day)
  - Guild join/leave trends
- **User Engagement**
  - 7-day active users
  - Commands executed today
  - Command usage trends (stacked by command type)
  - Active user trend
  - Daily command volume
- **Feature Adoption**
  - Feature usage (24h) bar chart
  - Feature usage rate over time

**Refresh Rate:** 30 seconds
**Default Time Range:** Last 24 hours

### 3. discordbot-slo-dashboard.json
**SLO monitoring dashboard** - Track Service Level Objectives and error budgets.

**Sections:**
- **SLO Compliance**
  - Command Success Rate SLO (24h) - Target: 99%
  - API Success Rate SLO (24h) - Target: 99.9%
  - Command p99 Latency SLO (1h) - Target: < 1000ms
  - Error Budget Remaining - Target: > 0%
  - SLO Summary table
  - Error Budget Over Time
- **SLO Trends**
  - 30-day uptime percentage
  - Success rate trends (command & API with target lines)
  - Latency SLO trend with threshold line
  - Uptime trend (30d)

**Refresh Rate:** 10 seconds
**Default Time Range:** Last 6 hours

## Alerting Rules

### prometheus-alerts.yml
Prometheus alerting rules for proactive monitoring and incident detection.

**Alert Groups:**

#### 1. discordbot-slo-alerts
SLO violation and compliance monitoring:

| Alert | Expression | Duration | Severity | Description |
|-------|-----------|----------|----------|-------------|
| `CommandSuccessRateLow` | Success rate < 99% | 5m | Critical | Command success rate below SLO |
| `ApiSuccessRateLow` | Success rate < 99.9% | 5m | Critical | API success rate below SLO |
| `HighCommandLatency` | p99 latency > 1000ms | 5m | Warning | Command latency exceeds SLO |
| `ErrorBudgetExhausted` | Budget < 10% | 1m | Critical | Error budget nearly exhausted |
| `ErrorBudgetWarning` | Budget < 25% | 5m | Warning | Error budget running low |
| `LowUptime` | 30d uptime < 99.9% | 1m | Warning | 30-day uptime below target |

#### 2. discordbot-business-alerts
Business metric monitoring:

| Alert | Expression | Duration | Severity | Description |
|-------|-----------|----------|----------|-------------|
| `NoGuildActivity` | Active guilds = 0 | 1h | Warning | No guild activity detected |
| `GuildChurn` | Leaves > Joins | 1d | Info | More guilds leaving than joining |
| `HighGuildChurn` | Leaves > (Joins * 2) | 6h | Warning | Significantly more guilds leaving |
| `LowActiveUsers` | 7d users < 50% avg | 2h | Warning | Active users dropped significantly |
| `NoCommandsExecuted` | Commands today = 0 | 30m | Warning | No commands executed recently |

#### 3. discordbot-performance-alerts
Performance and resource monitoring:

| Alert | Expression | Duration | Severity | Description |
|-------|-----------|----------|----------|-------------|
| `HighCommandErrorRate` | Error rate > 5% | 5m | Warning | High command failure rate |
| `HighApiErrorRate` | 5xx rate > 1% | 5m | Critical | High API error rate |
| `ThreadPoolStarvation` | Queue length > 100 | 5m | Warning | Thread pool backing up |
| `ExcessiveGarbageCollection` | Gen 2 GC > 0.5/sec | 10m | Warning | High memory pressure |

#### 4. discordbot-availability-alerts
Service availability monitoring:

| Alert | Expression | Duration | Severity | Description |
|-------|-----------|----------|----------|-------------|
| `BotDisconnected` | up = 0 | 2m | Critical | Bot is unreachable |
| `NoActiveGuilds` | Active guilds = 0 | 5m | Critical | Bot has no guild connections |
| `HighRateLimitViolations` | Violations > 0.1/sec | 5m | Warning | Excessive rate limit hits |

## Installation

### Grafana Dashboard Import

1. **Log into Grafana** and navigate to Dashboards > Import
2. **Copy the JSON content** from the desired dashboard file
3. **Paste into the import field** or upload the JSON file
4. **Select your Prometheus data source** in the import wizard
5. **Click Import** to create the dashboard

**Automated Import (Grafana API):**

```bash
# Set your Grafana credentials
GRAFANA_URL="http://localhost:3000"
GRAFANA_API_KEY="your-api-key-here"

# Import main dashboard
curl -X POST -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @discordbot-dashboard.json \
  "$GRAFANA_URL/api/dashboards/db"

# Import business dashboard
curl -X POST -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @discordbot-business-dashboard.json \
  "$GRAFANA_URL/api/dashboards/db"

# Import SLO dashboard
curl -X POST -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @discordbot-slo-dashboard.json \
  "$GRAFANA_URL/api/dashboards/db"
```

### Prometheus Alerting Rules Setup

1. **Copy `prometheus-alerts.yml`** to your Prometheus configuration directory (e.g., `/etc/prometheus/`)

2. **Update `prometheus.yml`** to include the alerting rules:

```yaml
rule_files:
  - "prometheus-alerts.yml"
```

3. **Update alert annotations** (optional):
   - Replace `https://grafana.example.com` with your Grafana URL
   - Replace `https://docs.example.com/runbooks` with your runbook documentation URL

4. **Configure Alertmanager** to route alerts:

```yaml
# alertmanager.yml
global:
  resolve_timeout: 5m

route:
  group_by: ['alertname', 'service']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'discord-webhook'

  routes:
    - match:
        severity: critical
      receiver: 'pagerduty'
      continue: true

    - match:
        severity: warning
      receiver: 'discord-webhook'

receivers:
  - name: 'discord-webhook'
    discord_configs:
      - webhook_url: 'https://discord.com/api/webhooks/YOUR_WEBHOOK_ID/YOUR_WEBHOOK_TOKEN'
        title: '{{ .GroupLabels.alertname }}'
        message: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'

  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_KEY'
```

5. **Reload Prometheus configuration:**

```bash
# Send SIGHUP to Prometheus process
kill -HUP $(pidof prometheus)

# Or use systemd
systemctl reload prometheus
```

6. **Verify rules are loaded:**

```bash
# Check Prometheus UI: http://localhost:9090/rules
# Or via API
curl http://localhost:9090/api/v1/rules
```

## Dashboard Links

Configure inter-dashboard navigation by updating the UID references:

**From main dashboard to SLO dashboard:**
- Panel descriptions contain: `/d/discordbot-slo-dashboard`

**From SLO dashboard back to main:**
- Add a link variable or panel link pointing to `/d/discordbot-metrics`

## Customization

### Adjusting SLO Targets

To modify SLO thresholds, update the gauge panels' threshold configurations:

**Command Success Rate (99% target):**
```json
"thresholds": {
  "steps": [
    {"color": "red", "value": null},
    {"color": "yellow", "value": 99},
    {"color": "green", "value": 99.5}
  ]
}
```

**API Success Rate (99.9% target):**
```json
"thresholds": {
  "steps": [
    {"color": "red", "value": null},
    {"color": "yellow", "value": 99.9},
    {"color": "green", "value": 99.95}
  ]
}
```

**Command Latency (1000ms target):**
```json
"thresholds": {
  "steps": [
    {"color": "green", "value": null},
    {"color": "yellow", "value": 500},
    {"color": "red", "value": 1000}
  ]
}
```

### Adding Variables

To filter dashboards by guild, command, or other dimensions:

1. Navigate to **Dashboard Settings > Variables**
2. Add a new variable:
   - **Name:** `guild_id`
   - **Type:** Query
   - **Data source:** Prometheus
   - **Query:** `label_values(discordbot_command_count, guild_id)`
3. Update panel queries to use the variable:
   ```promql
   sum(rate(discordbot_command_count{guild_id="$guild_id"}[5m])) by (command)
   ```

### Changing Refresh Rates

Edit the dashboard JSON's `refresh` field:

```json
"refresh": "10s",  // Options: "5s", "10s", "30s", "1m", "5m", "15m", "30m", "1h"
```

## Troubleshooting

### No Data Appearing in Panels

**Check Prometheus data source:**
1. Grafana > Configuration > Data Sources > Prometheus
2. Click "Test" to verify connectivity
3. Verify Prometheus URL is correct

**Verify metrics are being scraped:**
```bash
# Check Prometheus targets: http://localhost:9090/targets
# Query metrics directly
curl 'http://localhost:9090/api/v1/query?query=discordbot_command_count'
```

**Verify metric names match:**
- Check your OpenTelemetry exporter configuration
- Ensure meter names in the bot code match dashboard queries

### Alerts Not Firing

**Check Prometheus rules:**
```bash
# View active alerts: http://localhost:9090/alerts
# Check for rule evaluation errors in Prometheus logs
journalctl -u prometheus -f
```

**Verify Alertmanager connectivity:**
```bash
# Check Alertmanager is running: http://localhost:9093
# View Alertmanager status in Prometheus: http://localhost:9090/status
```

**Test alert expression manually:**
```promql
# Copy alert expression from prometheus-alerts.yml
# Run in Prometheus query browser to see if it returns results
discordbot_slo_command_success_rate_24h < 99
```

### Dashboard Panels Show "N/A"

**Time range issue:**
- Adjust dashboard time range to include data
- Check if metrics have recent timestamps

**Missing labels:**
- Some panels filter by labels that may not exist in your data
- Remove or adjust label filters in panel queries

**Metric aggregation:**
- If using a recording rule, ensure it's being evaluated
- Check for typos in metric names or label matchers

## Best Practices

### Dashboard Organization
- Use the **SLO Compliance row** in the main dashboard for at-a-glance health
- Link to specialized dashboards (business, SLO) for deeper analysis
- Keep refresh rates reasonable to avoid overloading Prometheus

### Alert Configuration
- **Start conservative** - Higher thresholds, longer durations
- **Monitor alert fatigue** - If alerts fire too often, adjust thresholds
- **Document runbooks** - Every critical alert should have a runbook
- **Use severity appropriately** - Reserve "critical" for true emergencies

### SLO Management
- **Review SLOs quarterly** - Adjust targets based on historical performance
- **Track error budget burn rate** - Fast burns indicate systemic issues
- **Use SLO violations to prioritize work** - Feature freeze when budget exhausted

## Related Documentation

- [OpenTelemetry Metrics](../articles/opentelemetry-metrics.md) - Metric definitions and instrumentation
- [Log Aggregation](../articles/log-aggregation.md) - Seq integration for log analysis
- [Monitoring Setup](../articles/monitoring-setup.md) - Full monitoring stack deployment

## Support

For questions or issues with these dashboards:
- Open an issue on GitHub
- Check the Discord bot documentation
- Review Grafana documentation: https://grafana.com/docs/
