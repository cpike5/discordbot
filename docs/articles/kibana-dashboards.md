# Kibana Dashboards and Alerting

**Version:** 1.0
**Last Updated:** 2026-01-06
**Status:** Active

---

## Overview

This document describes the Kibana dashboards, saved searches, and alerting rules for monitoring the Discord Bot application. These observability resources provide visibility into bot operations, performance, and error conditions.

### Prerequisites

- Kibana 8.x or later
- Elasticsearch with DiscordBot logs indexed (see [Log Aggregation](log-aggregation.md))
- Elastic APM configured (optional, for APM dashboard)

---

## Quick Start

### Automated Setup

Use the provided PowerShell scripts to automatically create all Kibana objects:

```powershell
# Navigate to scripts directory
cd scripts/kibana

# Import dashboards, visualizations, and saved searches
./Import-KibanaObjects.ps1 -KibanaUrl "http://localhost:5601"

# Set up alerting rules
./Setup-AlertRules.ps1 -KibanaUrl "http://localhost:5601"
```

### Script Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-KibanaUrl` | Base URL of Kibana instance | Required |
| `-ApiKey` | API key for authentication | None |
| `-SpaceName` | Kibana space to import into | `default` |
| `-Overwrite` | Overwrite existing objects | `false` |
| `-DryRun` | Preview changes without applying | `false` |

### Examples

```powershell
# Local development (no auth)
./Import-KibanaObjects.ps1 -KibanaUrl "http://localhost:5601"

# Production with API key
./Import-KibanaObjects.ps1 -KibanaUrl "https://kibana.example.com" -ApiKey "your-api-key"

# Import to specific space
./Import-KibanaObjects.ps1 -KibanaUrl "http://localhost:5601" -SpaceName "discordbot"

# Overwrite existing objects
./Import-KibanaObjects.ps1 -KibanaUrl "http://localhost:5601" -Overwrite

# Preview alert rules without creating
./Setup-AlertRules.ps1 -KibanaUrl "http://localhost:5601" -DryRun
```

---

## Data Views (Index Patterns)

The following index patterns are created for querying log data:

| Data View | Index Pattern | Description |
|-----------|---------------|-------------|
| DiscordBot Logs - All Environments | `discordbot-logs-*` | All logs across dev/staging/prod |
| DiscordBot Logs - Production | `discordbot-logs-prod-*` | Production logs only |
| DiscordBot Logs - Staging | `discordbot-logs-staging-*` | Staging logs only |
| DiscordBot Logs - Development | `discordbot-logs-dev-*` | Development logs only |
| APM Data | `apm-*` | APM transaction and span data |
| Metricbeat Data | `metricbeat-*` | Prometheus metrics (if using Metricbeat) |

All data views use `@timestamp` as the time field.

---

## Saved Searches

Pre-configured searches for common troubleshooting scenarios:

### Production Errors

**Query:** `level:Error`

Displays all error-level log events. Useful for:
- Quick error detection
- Error rate monitoring
- Root cause analysis

**Columns:** @timestamp, level, SourceContext, message, Exception

### Slow Queries

**Query:** `ExecutionTimeMs:>500`

Identifies slow operations taking longer than 500ms. Useful for:
- Performance troubleshooting
- Identifying bottlenecks
- Database query optimization

**Columns:** @timestamp, SourceContext, CommandName, ExecutionTimeMs, message

### Discord Command Executions

**Query:** `SourceContext:*InteractionHandler* AND CorrelationId:*`

Tracks Discord slash command executions. Useful for:
- Command usage analysis
- User activity tracking
- Command debugging

**Columns:** @timestamp, CommandName, GuildId, UserId, CorrelationId, ExecutionTimeMs

### Rate Limit Warnings

**Query:** `message:*rate limit* OR discord.api.rate_limit.remaining:0`

Monitors Discord API rate limit hits. Useful for:
- API usage optimization
- Rate limit avoidance
- Identifying problematic operations

**Columns:** @timestamp, SourceContext, message, discord.api.rate_limit.remaining, discord.api.rate_limit.reset

### Guild Activity (Template)

**Query:** `GuildId:*` (filter by specific GuildId)

Template search for investigating guild-specific activity. Add a filter for the specific GuildId to use.

**Columns:** @timestamp, level, SourceContext, UserId, CommandName, message

### User Activity (Template)

**Query:** `UserId:*` (filter by specific UserId)

Template search for investigating user-specific activity. Add a filter for the specific UserId to use.

**Columns:** @timestamp, level, SourceContext, GuildId, CommandName, message

### Auto-Moderation Detections

**Query:** `automod.rule.triggered:true`

Shows auto-moderation rule triggers. Useful for:
- Moderation monitoring
- Rule effectiveness analysis
- Spam detection

**Columns:** @timestamp, GuildId, UserId, automod.rule.name, automod.action, message

### Welcome Flow Errors

**Query:** `SourceContext:*Welcome* AND level:Error`

Identifies errors in the welcome/onboarding flow. Useful for:
- New member experience issues
- Welcome message failures
- DM delivery problems

**Columns:** @timestamp, GuildId, UserId, level, message, Exception

---

## Dashboards

### Bot Overview Dashboard

The main operational dashboard providing a comprehensive view of bot health and activity.

**Panels:**

| Panel | Type | Description |
|-------|------|-------------|
| Log Volume Over Time | Line Chart | Event count over time, split by log level |
| Error Rate | Metric | Percentage of events that are errors |
| Top Commands | Pie Chart | Distribution of command usage (top 10) |
| Response Time Distribution | Histogram | Command execution time buckets |
| Guild Activity | Data Table | Event counts and unique users per guild |
| Recent Errors | Saved Search | Latest error logs |

**Default Time Range:** Last 24 hours
**Refresh Interval:** 30 seconds

**Use Cases:**
- Daily operational monitoring
- Error detection and triage
- Usage pattern analysis
- Performance overview

### APM Service Overview Dashboard

Performance-focused dashboard using Elastic APM data.

**Panels:**

| Panel | Type | Description |
|-------|------|-------------|
| Transaction Throughput | Line Chart | Requests per minute |
| Transaction Duration | Line Chart | p50, p95, p99 latency percentiles |
| APM Error Rate | Metric | Failed transaction percentage |
| Discord API Performance | Data Table | External call duration by endpoint |
| Database Query Performance | Data Table | DB operation duration by action type |

**Default Time Range:** Last 1 hour
**Refresh Interval:** 30 seconds

**Use Cases:**
- Performance troubleshooting
- Latency monitoring
- External dependency health
- Database optimization

---

## Alerting Rules

### Alert Configuration

Six alerting rules are created to monitor critical conditions:

| Alert Name | Severity | Condition | Throttle |
|------------|----------|-----------|----------|
| High Error Rate | High | Error > 10/min for 5 min | 5 min |
| Slow Transactions | Medium | APM p99 > 5000ms for 10 min | 10 min |
| Database Errors | High | Any db.error detected | 5 min |
| Discord Rate Limit Hit | Medium | Rate limit remaining = 0 | 15 min |
| Bot Disconnection | Critical | No BotHostedService logs for 5 min | 5 min |
| Auto-Moderation Spike | Low | Automod > 50 events/hour | 1 hour |

### Configuring Alert Actions

Alert rules are created without actions by default. To receive notifications:

1. **Create Connectors** (Stack Management > Rules and Connectors > Connectors):
   - **Email**: Requires SMTP server configuration
   - **Slack**: Requires webhook URL from Slack
   - **PagerDuty**: Requires routing key (optional, for critical alerts)

2. **Add Actions to Rules** (Stack Management > Rules and Connectors > Rules):
   - Edit each rule
   - Click "Add action"
   - Select connector and configure message template

### Recommended Action Configuration

| Alert | Recommended Actions |
|-------|---------------------|
| Bot Disconnection (Critical) | PagerDuty + Email |
| High Error Rate (High) | Email + Slack |
| Database Errors (High) | Email + Slack |
| Slow Transactions (Medium) | Slack |
| Discord Rate Limit Hit (Medium) | Slack |
| Auto-Moderation Spike (Low) | Slack |

### Alert Message Templates

Example Slack message template for High Error Rate:

```
:warning: *High Error Rate Alert*

*Time:* {{context.date}}
*Rule:* {{rule.name}}
*Condition:* Error count > 10/minute for 5 minutes

{{#context.hits}}
*Recent Errors:*
{{#hits}}
- `{{_source.SourceContext}}`: {{_source.message}}
{{/hits}}
{{/context.hits}}

<{{kibanaBaseUrl}}/app/discover#/?_g=(filters:!(),refreshInterval:(pause:!t,value:0),time:(from:now-1h,to:now))&_a=(columns:!(message,SourceContext,Exception),filters:!(),index:'discordbot-logs-all',query:(language:kuery,query:'level:Error'))|View in Kibana>
```

---

## Common KQL Queries

### Error Investigation

```kql
# All errors in the last hour
level:Error

# Errors from specific component
level:Error AND SourceContext:*InteractionHandler*

# Errors for specific guild
level:Error AND GuildId:123456789012345678

# Errors with exceptions
level:Error AND Exception:*
```

### Performance Analysis

```kql
# Slow operations (>1 second)
ExecutionTimeMs:>1000

# Commands taking longest
CommandName:* AND ExecutionTimeMs:*

# Database performance issues
SourceContext:*Repository* AND ExecutionTimeMs:>500
```

### User/Guild Activity

```kql
# All activity for a guild
GuildId:123456789012345678

# All activity for a user
UserId:987654321098765432

# Commands executed by user in guild
GuildId:123456789012345678 AND UserId:987654321098765432 AND CommandName:*
```

### Correlation Tracking

```kql
# Track request by correlation ID
CorrelationId:a1b2c3d4e5f6g7h8

# Track by trace ID (APM)
trace.id:abc123def456

# Find all logs for an APM transaction
transaction.id:xyz789
```

---

## Customization

### Adding New Visualizations

1. Go to Analytics > Visualize Library
2. Create New Visualization
3. Select visualization type
4. Choose appropriate data view
5. Configure aggregations and metrics
6. Save to library
7. Add to dashboard

### Modifying Dashboards

1. Open dashboard in edit mode
2. Resize/rearrange panels as needed
3. Add panels from library or create new
4. Configure time range and refresh interval
5. Save changes

### Creating Custom Saved Searches

1. Go to Analytics > Discover
2. Select appropriate data view
3. Enter KQL query
4. Configure columns to display
5. Save search with descriptive name
6. Optionally add to dashboard as panel

---

## Troubleshooting

### Objects Not Importing

**Symptoms:** Import script fails or objects don't appear in Kibana.

**Solutions:**
1. Verify Kibana connection: `curl http://localhost:5601/api/status`
2. Check authentication if using API key
3. Try with `-Overwrite` flag if objects already exist
4. Check Kibana logs for detailed errors

### Dashboards Show No Data

**Symptoms:** Dashboards load but visualizations are empty.

**Solutions:**
1. Verify index pattern matches your log indices
2. Check time range (default is last 24h)
3. Verify logs are being ingested to Elasticsearch
4. Test query in Discover tab first

### Alerts Not Triggering

**Symptoms:** Conditions are met but no alerts fire.

**Solutions:**
1. Check rule is enabled in Stack Management > Rules
2. Verify rule conditions match actual log field names
3. Check if throttle period is preventing alerts
4. Review rule execution logs in Kibana

### APM Dashboard Issues

**Symptoms:** APM dashboard panels show errors or no data.

**Solutions:**
1. Verify APM Server is running and connected
2. Check `apm-*` index pattern exists
3. Verify application is sending APM data
4. Check APM service name matches "discordbot"

---

## File Reference

| File | Description |
|------|-------------|
| `scripts/kibana/Import-KibanaObjects.ps1` | Main import script |
| `scripts/kibana/Setup-AlertRules.ps1` | Alerting rules setup |
| `scripts/kibana/objects/data-views.ndjson` | Index pattern definitions |
| `scripts/kibana/objects/saved-searches.ndjson` | Saved search definitions |
| `scripts/kibana/objects/visualizations.ndjson` | Visualization definitions |
| `scripts/kibana/objects/dashboards.ndjson` | Dashboard definitions |
| `scripts/kibana/objects/alert-rules.json` | Alerting rule configurations |

---

## Related Documentation

- [Log Aggregation](log-aggregation.md) - Elasticsearch and Serilog configuration
- [Bot Performance Dashboard](bot-performance-dashboard.md) - Application-level performance monitoring

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-06 | Initial release - dashboards, saved searches, and alerting (Issue #795) |

---

*Last Updated: January 6, 2026*
