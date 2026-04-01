# Grafana Dashboards and Prometheus Alerts Specification

## Overview

This specification documents the Grafana dashboards and Prometheus alerting rules created for monitoring the Discord bot's operational health, business metrics, and Service Level Objectives (SLOs).

**Location:** `docs/grafana/`

**Files Created:**
- `discordbot-dashboard.json` (updated with SLO row)
- `discordbot-business-dashboard.json` (new)
- `discordbot-slo-dashboard.json` (new)
- `prometheus-alerts.yml` (new)
- `README.md` (new)

## Design Principles

### 1. Hierarchical Information Architecture

Dashboards are organized from general to specific:

1. **Main Dashboard** (`discordbot-dashboard.json`) - Primary operational view with quick SLO check
2. **Specialized Dashboards** - Deep dives into specific areas:
   - **Business Dashboard** - User engagement and growth metrics
   - **SLO Dashboard** - Detailed SLO compliance and error budget tracking

### 2. Consistent Visualization Standards

**Color Coding:**
- **Green** - Healthy, above target
- **Yellow** - Warning, approaching threshold
- **Red** - Critical, below threshold or exceeding limit
- **Blue** - Informational (guilds joined, active metrics)
- **Orange** - Negative trend (guilds left, churn)

**Panel Types:**
- **Gauge** - Single value SLO metrics with thresholds
- **Stat** - Counters and current values with optional sparklines
- **Timeseries** - Trends over time, supports stacking and multi-series
- **Table** - Tabular data for quick reference

**Threshold Conventions:**
- SLO gauges use target-based thresholds (99%, 99.9%)
- Latency uses performance tiers (green < 500ms, yellow < 1000ms, red > 1000ms)
- Error rates use severity levels (green < 1%, yellow < 5%, red > 5%)

### 3. User Experience

**Information Density:**
- Top row always shows most critical metrics
- Rows are collapsible for focused analysis
- Panel descriptions provide context and metric definitions

**Navigation:**
- Panel titles reference related dashboards
- Descriptions include links to full dashboards (e.g., `/d/discordbot-slo-dashboard`)
- Consistent UID naming for predictable URLs

**Refresh Rates:**
- **10 seconds** - Operational dashboards (main, SLO)
- **30 seconds** - Business dashboards (less time-sensitive)

## Main Dashboard Updates

### SLO Compliance Row (New)

Added as the **top row** (row ID: 99) to provide immediate SLO visibility.

**Position:** y=0 (top of dashboard)

**Panels:**

#### 1. Command Success Rate SLO (Panel ID: 15)
- **Type:** Gauge
- **Size:** 6 columns wide, 6 units tall
- **Metric:** `discordbot_slo_command_success_rate_24h`
- **Target:** 99%
- **Thresholds:**
  - Red: < 99%
  - Yellow: 99% - 99.5%
  - Green: ≥ 99.5%
- **Description:** Links to full SLO dashboard

#### 2. Error Budget Remaining (Panel ID: 16)
- **Type:** Gauge
- **Size:** 6 columns wide, 6 units tall
- **Metric:** `discordbot_slo_error_budget_remaining`
- **Target:** > 0%
- **Thresholds:**
  - Red: < 10%
  - Yellow: 10% - 50%
  - Green: ≥ 50%
- **Description:** Links to full SLO dashboard

#### 3. SLO Quick View (Panel ID: 17)
- **Type:** Table
- **Size:** 12 columns wide, 6 units tall
- **Metrics:**
  - Command Success Rate (24h)
  - API Success Rate (24h)
  - Command p99 Latency (1h)
  - Uptime (30d)
- **Format:** Instant queries formatted as table
- **Title:** Includes link to full dashboard

**Layout Impact:**
- All existing rows shifted down by 7 units (1 for row header + 6 for panel height)
- Updated y-positions for all panels below the new row

## Business Metrics Dashboard

**File:** `docs/grafana/discordbot-business-dashboard.json`

**UID:** `discordbot-business-metrics`

**Purpose:** Track user engagement, guild growth, and feature adoption to inform product decisions.

### Dashboard Structure

#### Row 1: Guild Activity (Row ID: 200)

**Panels:**

##### Guilds Joined Today (Panel ID: 201)
- **Type:** Stat with area graph
- **Metric:** `discordbot_business_guilds_joined_today`
- **Color:** Blue
- **Size:** 6 columns × 8 units
- **Aggregation:** Last value

##### Guilds Left Today (Panel ID: 202)
- **Type:** Stat with area graph
- **Metric:** `discordbot_business_guilds_left_today`
- **Color:** Orange
- **Size:** 6 columns × 8 units
- **Aggregation:** Last value

##### Active Guilds (24h) (Panel ID: 203)
- **Type:** Stat with area graph
- **Metric:** `discordbot_business_guilds_active_daily`
- **Color:** Green
- **Size:** 6 columns × 8 units
- **Aggregation:** Last value

##### Guild Growth Rate (Panel ID: 204)
- **Type:** Timeseries
- **Query:** `sum(increase(discordbot_business_guild_join[1d])) - sum(increase(discordbot_business_guild_leave[1d]))`
- **Size:** 6 columns × 8 units
- **Features:** Centered zero axis, shows net growth/decline
- **Legend:** Mean and last value

##### Guild Join/Leave Trends (Panel ID: 205)
- **Type:** Timeseries
- **Size:** 24 columns × 8 units (full width)
- **Series:**
  - Joins (blue) - `sum(increase(discordbot_business_guild_join[1h]))`
  - Leaves (orange) - `sum(increase(discordbot_business_guild_leave[1h]))`
- **Legend:** Sum and mean calculations

#### Row 2: User Engagement (Row ID: 201)

**Panels:**

##### 7-Day Active Users (Panel ID: 206)
- **Type:** Stat with area graph
- **Metric:** `discordbot_business_users_active_7d`
- **Color:** Blue
- **Size:** 6 columns × 8 units

##### Commands Today (Panel ID: 207)
- **Type:** Stat with area graph
- **Metric:** `discordbot_business_commands_today`
- **Color:** Green
- **Size:** 6 columns × 8 units

##### Command Usage Trends (Panel ID: 208)
- **Type:** Timeseries (stacked)
- **Query:** `sum(rate(discordbot_command_count[1h])) by (command)`
- **Size:** 12 columns × 8 units
- **Features:** Stacked area chart, shows relative command popularity
- **Legend:** Mean and sum calculations

##### Active User Trend (Panel ID: 209)
- **Type:** Timeseries
- **Metric:** `discordbot_business_users_active_7d`
- **Size:** 12 columns × 8 units
- **Features:** Line chart with 2px width

##### Daily Command Volume (Panel ID: 210)
- **Type:** Timeseries
- **Metric:** `discordbot_business_commands_today`
- **Size:** 12 columns × 8 units

#### Row 3: Feature Adoption (Row ID: 202)

**Panels:**

##### Feature Usage (24h) (Panel ID: 211)
- **Type:** Timeseries (bars)
- **Query:** `sum(increase(discordbot_business_feature_usage[24h])) by (feature)`
- **Size:** 12 columns × 8 units
- **Features:** 100% fill bar chart
- **Legend:** Sum calculation

##### Feature Usage Rate (Panel ID: 212)
- **Type:** Timeseries
- **Query:** `sum(rate(discordbot_business_feature_usage[5m])) by (feature)`
- **Size:** 12 columns × 8 units
- **Features:** Line chart showing usage rate over time
- **Legend:** Mean and sum calculations

### Technical Details

**Schema Version:** 38 (Grafana 10.x compatible)

**Time Range:** Last 24 hours (appropriate for daily business metrics)

**Refresh Rate:** 30 seconds (business metrics change slower than operational metrics)

**Datasource Variable:** `${DS_PROMETHEUS}` (allows flexible datasource selection)

**Tags:** `discord`, `bot`, `business-metrics`

## SLO Dashboard

**File:** `docs/grafana/discordbot-slo-dashboard.json`

**UID:** `discordbot-slo-dashboard`

**Purpose:** Monitor Service Level Objectives, track error budgets, and ensure compliance with service targets.

### Dashboard Structure

#### Row 1: SLO Compliance (Row ID: 300)

**Panels:**

##### Command Success Rate SLO (24h) (Panel ID: 301)
- **Type:** Gauge
- **Metric:** `discordbot_slo_command_success_rate_24h`
- **Size:** 6 columns × 8 units
- **Target:** 99%
- **Thresholds:**
  - Red: < 99%
  - Yellow: 99% - 99.5%
  - Green: ≥ 99.5%
- **Range:** 0% - 100%
- **Description:** Includes SLO target and dashboard context

##### API Success Rate SLO (24h) (Panel ID: 302)
- **Type:** Gauge
- **Metric:** `discordbot_slo_api_success_rate_24h`
- **Size:** 6 columns × 8 units
- **Target:** 99.9%
- **Thresholds:**
  - Red: < 99.9%
  - Yellow: 99.9% - 99.95%
  - Green: ≥ 99.95%
- **Range:** 99% - 100% (zoomed for precision)
- **Description:** Stricter SLO for API reliability

##### Command p99 Latency SLO (1h) (Panel ID: 303)
- **Type:** Gauge
- **Metric:** `discordbot_slo_command_p99_latency_1h`
- **Size:** 6 columns × 8 units
- **Target:** < 1000ms
- **Thresholds:**
  - Green: ≤ 500ms
  - Yellow: 500ms - 1000ms
  - Red: > 1000ms
- **Range:** 0ms - 2000ms
- **Unit:** Milliseconds

##### Error Budget Remaining (Panel ID: 304)
- **Type:** Gauge
- **Metric:** `discordbot_slo_error_budget_remaining`
- **Size:** 6 columns × 8 units
- **Target:** > 0%
- **Thresholds:**
  - Red: < 10%
  - Yellow: 10% - 50%
  - Green: ≥ 50%
- **Range:** 0% - 100%
- **Description:** Explains error budget concept

##### SLO Summary (Panel ID: 305)
- **Type:** Table
- **Size:** 12 columns × 8 units
- **Data:** All SLO metrics in tabular format
- **Queries:**
  - Command Success Rate (instant)
  - API Success Rate (instant)
  - Command p99 Latency (instant)
  - Error Budget (instant)
- **Format:** Color-coded cells based on thresholds

##### Error Budget Over Time (Panel ID: 306)
- **Type:** Timeseries
- **Metric:** `discordbot_slo_error_budget_remaining`
- **Size:** 12 columns × 8 units
- **Features:**
  - Threshold line at 0% (critical)
  - 2px line width
  - Shows budget depletion trend
- **Legend:** Mean and last value

#### Row 2: SLO Trends (Row ID: 301)

**Panels:**

##### 30-Day Uptime (Panel ID: 307)
- **Type:** Stat with area graph
- **Metric:** `discordbot_slo_uptime_percentage_30d`
- **Size:** 6 columns × 8 units
- **Thresholds:**
  - Red: < 99%
  - Yellow: 99% - 99.9%
  - Green: ≥ 99.9%

##### Success Rate Trends (Panel ID: 308)
- **Type:** Timeseries
- **Size:** 18 columns × 8 units
- **Series:**
  - Command Success Rate SLO (blue, 2px)
  - API Success Rate SLO (green, 2px)
  - Command Target 99% (red dashed, 1px)
  - API Target 99.9% (red dashed, 1px)
- **Features:**
  - Threshold lines show targets
  - Multi-series comparison
  - Table legend with mean, last, and min values
- **Purpose:** Visualize SLO compliance over time with target lines

##### Latency SLO Trend (Panel ID: 309)
- **Type:** Timeseries
- **Size:** 12 columns × 8 units
- **Series:**
  - Command p99 Latency (2px)
  - SLO Threshold 1000ms (red dashed, 1px)
- **Threshold Line:** Shows at 1000ms
- **Legend:** Mean, last, and max values

##### Uptime Trend (Panel ID: 310)
- **Type:** Timeseries
- **Metric:** `discordbot_slo_uptime_percentage_30d`
- **Size:** 12 columns × 8 units
- **Thresholds:**
  - Red: < 99%
  - Yellow: 99% - 99.9%
  - Green: ≥ 99.9%
- **Legend:** Mean, last, and min values

### Technical Details

**Schema Version:** 38

**Time Range:** Last 6 hours (focused on recent SLO performance)

**Refresh Rate:** 10 seconds (SLOs require real-time monitoring)

**Datasource Variable:** `${DS_PROMETHEUS}`

**Tags:** `discord`, `bot`, `slo`, `service-level-objectives`

## Prometheus Alerting Rules

**File:** `docs/grafana/prometheus-alerts.yml`

**Purpose:** Proactive monitoring and incident detection based on SLO violations, business anomalies, and system health.

### Alert Group Structure

#### 1. discordbot-slo-alerts

**Interval:** 1 minute

**Purpose:** Monitor SLO compliance and error budget consumption

**Alerts:**

##### CommandSuccessRateLow
```yaml
expr: discordbot_slo_command_success_rate_24h < 99
for: 5m
severity: critical
```
- **Trigger:** Success rate drops below 99% SLO
- **Duration:** Must persist for 5 minutes
- **Impact:** Commands failing at unacceptable rate
- **Action:** Check logs, review recent deployments

##### ApiSuccessRateLow
```yaml
expr: discordbot_slo_api_success_rate_24h < 99.9
for: 5m
severity: critical
```
- **Trigger:** API success rate below 99.9% SLO
- **Duration:** 5 minutes
- **Impact:** API endpoints experiencing elevated errors
- **Action:** Check backend services, database connectivity

##### HighCommandLatency
```yaml
expr: discordbot_slo_command_p99_latency_1h > 1000
for: 5m
severity: warning
```
- **Trigger:** p99 latency exceeds 1000ms
- **Duration:** 5 minutes
- **Impact:** Degraded user experience
- **Action:** Check database query performance, Discord API latency

##### ErrorBudgetExhausted
```yaml
expr: discordbot_slo_error_budget_remaining < 10
for: 1m
severity: critical
```
- **Trigger:** Less than 10% of error budget remains
- **Duration:** 1 minute
- **Impact:** SLO at risk, may require feature freeze
- **Action:** Stop risky deployments, focus on stability

##### ErrorBudgetWarning
```yaml
expr: discordbot_slo_error_budget_remaining < 25
for: 5m
severity: warning
```
- **Trigger:** Error budget below 25%
- **Duration:** 5 minutes
- **Impact:** Approaching SLO limits
- **Action:** Monitor closely, prepare incident response

##### LowUptime
```yaml
expr: discordbot_slo_uptime_percentage_30d < 99.9
for: 1m
severity: warning
```
- **Trigger:** 30-day uptime below target
- **Duration:** 1 minute
- **Impact:** Historical availability degraded
- **Action:** Review incident patterns, improve resilience

#### 2. discordbot-business-alerts

**Interval:** 1 minute

**Purpose:** Detect business metric anomalies and user engagement issues

**Alerts:**

##### NoGuildActivity
```yaml
expr: discordbot_business_guilds_active_daily == 0
for: 1h
severity: warning
```
- **Trigger:** No active guilds for 1 hour
- **Impact:** Bot may be offline or inaccessible
- **Action:** Check bot connectivity, Discord API status

##### GuildChurn
```yaml
expr: increase(discordbot_business_guild_leave[1d]) > increase(discordbot_business_guild_join[1d])
for: 1d
severity: info
```
- **Trigger:** More guilds leaving than joining over 24h
- **Impact:** Negative growth trend
- **Action:** Review user feedback, check for service issues

##### HighGuildChurn
```yaml
expr: increase(discordbot_business_guild_leave[1d]) > (increase(discordbot_business_guild_join[1d]) * 2)
for: 6h
severity: warning
```
- **Trigger:** Guild leaves exceed joins by 2x
- **Impact:** Severe user dissatisfaction
- **Action:** Immediate investigation, may indicate major issue

##### LowActiveUsers
```yaml
expr: |
  discordbot_business_users_active_7d <
  (avg_over_time(discordbot_business_users_active_7d[7d]) * 0.5)
for: 2h
severity: warning
```
- **Trigger:** Active users drop below 50% of 7-day average
- **Impact:** Significant engagement decline
- **Action:** Check for service degradation, review recent changes

##### NoCommandsExecuted
```yaml
expr: discordbot_business_commands_today == 0
for: 30m
severity: warning
```
- **Trigger:** Zero commands in 30 minutes
- **Impact:** Bot may be unresponsive
- **Action:** Check bot status, command registration

#### 3. discordbot-performance-alerts

**Interval:** 1 minute

**Purpose:** Monitor performance degradation and resource issues

**Alerts:**

##### HighCommandErrorRate
```yaml
expr: |
  sum(rate(discordbot_command_count{status="failure"}[5m])) /
  sum(rate(discordbot_command_count[5m])) * 100 > 5
for: 5m
severity: warning
```
- **Trigger:** Command error rate exceeds 5%
- **Impact:** Elevated failure rate
- **Action:** Check logs for error patterns

##### HighApiErrorRate
```yaml
expr: |
  sum(rate(discordbot_api_request_count{status_code=~"5.."}[5m])) /
  sum(rate(discordbot_api_request_count[5m])) * 100 > 1
for: 5m
severity: critical
```
- **Trigger:** 5xx error rate exceeds 1%
- **Impact:** Backend service issues
- **Action:** Check database, external APIs

##### ThreadPoolStarvation
```yaml
expr: process_runtime_dotnet_thread_pool_queue_length > 100
for: 5m
severity: warning
```
- **Trigger:** Thread pool queue exceeds 100 items
- **Impact:** Request processing delays
- **Action:** Check for blocking operations, increase thread pool size

##### ExcessiveGarbageCollection
```yaml
expr: rate(process_runtime_dotnet_gc_collections_count{generation="2"}[5m]) > 0.5
for: 10m
severity: warning
```
- **Trigger:** Gen 2 GC rate exceeds 0.5/sec
- **Impact:** Memory pressure, performance degradation
- **Action:** Check for memory leaks, optimize allocations

#### 4. discordbot-availability-alerts

**Interval:** 1 minute

**Purpose:** Detect service outages and connectivity issues

**Alerts:**

##### BotDisconnected
```yaml
expr: up{job="discordbot"} == 0
for: 2m
severity: critical
```
- **Trigger:** Bot unreachable for 2 minutes
- **Impact:** Complete service outage
- **Action:** Check process, container, network

##### NoActiveGuilds
```yaml
expr: discordbot_guilds_active == 0
for: 5m
severity: critical
```
- **Trigger:** Bot has zero guild connections
- **Impact:** Not serving any servers
- **Action:** Check Discord token, gateway connectivity

##### HighRateLimitViolations
```yaml
expr: sum(rate(discordbot_ratelimit_violations[5m])) > 0.1
for: 5m
severity: warning
```
- **Trigger:** Rate limit violations exceed 0.1/sec
- **Impact:** Discord API throttling
- **Action:** Review request patterns, implement backoff

### Alert Annotations

Each alert includes:

**Labels:**
- `severity` - Critical, warning, or info
- `service` - Always "discordbot"
- `alert_type` - Category (slo_violation, business_metric, performance, availability, error_budget)

**Annotations:**
- `summary` - Brief alert description
- `description` - Detailed explanation with current value
- `dashboard` - Link to relevant Grafana dashboard (placeholder)
- `runbook` - Link to runbook documentation (placeholder)

### Alert Routing Strategy

**Recommended Alertmanager Configuration:**

1. **Critical Alerts** - Immediate notification via PagerDuty, Slack, Discord
2. **Warning Alerts** - Notification during business hours, Discord webhook
3. **Info Alerts** - Logged for review, no immediate action

**Grouping:**
- Group by `alertname` and `service`
- 10-second wait before sending
- 10-second interval between updates
- 12-hour repeat interval

## Integration with Existing Metrics

### Required Metrics

The dashboards expect these metrics to be exposed by the bot:

#### SLO Metrics (New)
```
discordbot_slo_command_success_rate_24h
discordbot_slo_api_success_rate_24h
discordbot_slo_command_p99_latency_1h
discordbot_slo_error_budget_remaining
discordbot_slo_uptime_percentage_30d
```

#### Business Metrics (New)
```
discordbot_business_guilds_joined_today
discordbot_business_guilds_left_today
discordbot_business_guilds_active_daily
discordbot_business_guild_join (counter)
discordbot_business_guild_leave (counter)
discordbot_business_users_active_7d
discordbot_business_commands_today
discordbot_business_feature_usage (counter with "feature" label)
```

#### Existing Metrics
```
discordbot_command_count (counter with "status", "command" labels)
discordbot_command_duration_bucket (histogram)
discordbot_command_active (gauge)
discordbot_guilds_active (gauge)
discordbot_api_request_count (counter with "endpoint", "status_code" labels)
discordbot_api_request_duration_bucket (histogram)
discordbot_api_request_active (gauge)
discordbot_ratelimit_violations (counter with "command", "target" labels)
process_runtime_dotnet_gc_collections_count (counter with "generation" label)
process_runtime_dotnet_thread_pool_queue_length (gauge)
up (Prometheus scrape success indicator)
```

### Metric Calculation Methods

#### SLO Metrics
These are **computed metrics** that should be calculated and exposed by a dedicated metrics service or recording rules:

**Command Success Rate (24h):**
```promql
sum(rate(discordbot_command_count{status="success"}[24h])) /
sum(rate(discordbot_command_count[24h])) * 100
```

**API Success Rate (24h):**
```promql
sum(rate(discordbot_api_request_count{status_code!~"5.."}[24h])) /
sum(rate(discordbot_api_request_count[24h])) * 100
```

**Command p99 Latency (1h):**
```promql
histogram_quantile(0.99,
  sum(rate(discordbot_command_duration_bucket[1h])) by (le)
)
```

**Error Budget Remaining:**
```
Calculation:
1. Define SLO target (e.g., 99% success rate)
2. Calculate error allowance: (1 - target) * total_requests
3. Calculate actual errors: failed_requests
4. Remaining budget: (error_allowance - actual_errors) / error_allowance * 100
```

**30-Day Uptime:**
```promql
avg_over_time(up{job="discordbot"}[30d]) * 100
```

#### Business Metrics
These are **instrumented metrics** that should be incremented/set by the bot code:

- **Guilds Joined/Left Today** - Reset daily at midnight UTC
- **Active Guilds Daily** - Count guilds with activity in last 24h
- **7-Day Active Users** - HyperLogLog or similar cardinality estimator
- **Commands Today** - Counter reset daily
- **Feature Usage** - Counter incremented on feature use

## Deployment Checklist

### Pre-Deployment

- [ ] Verify Prometheus is scraping bot metrics
- [ ] Confirm all required metrics are being exposed
- [ ] Test metric queries in Prometheus UI
- [ ] Review and adjust SLO targets if needed
- [ ] Update alert annotation URLs (dashboard, runbook)

### Dashboard Import

- [ ] Import `discordbot-dashboard.json` (updated)
- [ ] Import `discordbot-business-dashboard.json` (new)
- [ ] Import `discordbot-slo-dashboard.json` (new)
- [ ] Verify datasource selection
- [ ] Test panel queries return data
- [ ] Set appropriate permissions (viewer, editor)

### Alert Configuration

- [ ] Copy `prometheus-alerts.yml` to Prometheus config directory
- [ ] Update `prometheus.yml` to include rule file
- [ ] Reload Prometheus configuration
- [ ] Verify rules are loaded in Prometheus UI
- [ ] Configure Alertmanager routing
- [ ] Test alert firing with controlled failure

### Post-Deployment

- [ ] Monitor dashboards for 24 hours
- [ ] Adjust thresholds based on actual data
- [ ] Document runbook procedures
- [ ] Train team on dashboard usage
- [ ] Set up scheduled reviews of SLO compliance

## Maintenance and Evolution

### Regular Review Schedule

**Weekly:**
- Review SLO compliance
- Check for alert fatigue
- Verify business metric trends

**Monthly:**
- Adjust alert thresholds if needed
- Review error budget burn rate
- Update runbook documentation

**Quarterly:**
- Evaluate SLO targets
- Review dashboard effectiveness
- Gather user feedback on visualization

### Dashboard Versioning

**Version Control:**
- Store dashboard JSON files in Git
- Use descriptive commit messages
- Tag releases (e.g., `dashboards-v1.0.0`)

**Change Management:**
- Test changes in dev/staging Grafana instance
- Document breaking changes
- Communicate updates to team

### Metric Evolution

When adding new metrics:
1. Update dashboard JSON files
2. Document metric in specification
3. Update alert rules if relevant
4. Increment dashboard version
5. Update README.md with usage examples

## References

### Related Documentation
- [OpenTelemetry Metrics Specification](./opentelemetry-metrics.md)
- [SLO Definition and Calculation](./slo-specification.md)
- [Monitoring Architecture](./monitoring-architecture.md)

### External Resources
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/dashboards/build-dashboards/best-practices/)
- [Prometheus Alerting Best Practices](https://prometheus.io/docs/practices/alerting/)
- [SRE Workbook - SLO Engineering](https://sre.google/workbook/implementing-slos/)
- [The Four Golden Signals](https://sre.google/sre-book/monitoring-distributed-systems/)

## Appendix: Panel ID Reference

### Main Dashboard (discordbot-dashboard.json)

| Panel ID | Panel Name | Type | Row |
|----------|-----------|------|-----|
| 99 | SLO Compliance | Row | - |
| 15 | Command Success Rate SLO | Gauge | SLO Compliance |
| 16 | Error Budget Remaining | Gauge | SLO Compliance |
| 17 | SLO Quick View | Table | SLO Compliance |
| 100 | Bot Overview | Row | - |
| 1 | Command Success Rate | Gauge | Bot Overview |
| 2 | Active Guilds | Stat | Bot Overview |
| 3 | Command Rate by Type | Timeseries | Bot Overview |
| 101 | Command Performance | Row | - |
| 4 | Command Latency Percentiles | Timeseries | Command Performance |
| 5 | Active Commands (Concurrent) | Timeseries | Command Performance |
| 102 | Rate Limiting & Errors | Row | - |
| 6 | Rate Limit Violations | Timeseries | Rate Limiting & Errors |
| 7 | Failed Commands | Timeseries | Rate Limiting & Errors |
| 103 | API Performance | Row | - |
| 8 | API Request Rate by Endpoint | Timeseries | API Performance |
| 9 | API Error Rate (5xx) | Gauge | API Performance |
| 10 | Active API Requests | Stat | API Performance |
| 11 | API Latency (p99) | Timeseries | API Performance |
| 12 | API Requests by Status Code | Timeseries | API Performance |
| 104 | System Health | Row | - |
| 13 | GC Collections per Second | Timeseries | System Health |
| 14 | Thread Pool Queue | Timeseries | System Health |

### Business Dashboard (discordbot-business-dashboard.json)

| Panel ID | Panel Name | Type | Row |
|----------|-----------|------|-----|
| 200 | Guild Activity | Row | - |
| 201 | Guilds Joined Today | Stat | Guild Activity |
| 202 | Guilds Left Today | Stat | Guild Activity |
| 203 | Active Guilds (24h) | Stat | Guild Activity |
| 204 | Guild Growth Rate | Timeseries | Guild Activity |
| 205 | Guild Join/Leave Trends | Timeseries | Guild Activity |
| 201 | User Engagement | Row | - |
| 206 | 7-Day Active Users | Stat | User Engagement |
| 207 | Commands Today | Stat | User Engagement |
| 208 | Command Usage Trends | Timeseries | User Engagement |
| 209 | Active User Trend | Timeseries | User Engagement |
| 210 | Daily Command Volume | Timeseries | User Engagement |
| 202 | Feature Adoption | Row | - |
| 211 | Feature Usage (24h) | Timeseries (bars) | Feature Adoption |
| 212 | Feature Usage Rate | Timeseries | Feature Adoption |

### SLO Dashboard (discordbot-slo-dashboard.json)

| Panel ID | Panel Name | Type | Row |
|----------|-----------|------|-----|
| 300 | SLO Compliance | Row | - |
| 301 | Command Success Rate SLO (24h) | Gauge | SLO Compliance |
| 302 | API Success Rate SLO (24h) | Gauge | SLO Compliance |
| 303 | Command p99 Latency SLO (1h) | Gauge | SLO Compliance |
| 304 | Error Budget Remaining | Gauge | SLO Compliance |
| 305 | SLO Summary | Table | SLO Compliance |
| 306 | Error Budget Over Time | Timeseries | SLO Compliance |
| 301 | SLO Trends | Row | - |
| 307 | 30-Day Uptime | Stat | SLO Trends |
| 308 | Success Rate Trends | Timeseries | SLO Trends |
| 309 | Latency SLO Trend | Timeseries | SLO Trends |
| 310 | Uptime Trend | Timeseries | SLO Trends |
