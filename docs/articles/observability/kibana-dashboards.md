# Kibana Dashboards and Visualizations

**Version:** 2.0
**Last Updated:** 2026-02-03
**Status:** Active
**Kibana Version:** 8.12.0
**Instance URL:** https://kibana.cpike.ca

---

## Overview

Comprehensive Kibana dashboards and visualizations for monitoring the Discord bot application. The monitoring strategy provides visibility into operational health, performance metrics, command analytics, audio features, and external service integrations (Discord API, Azure Services, Anthropic AI).

### Key Monitoring Areas

| Area | Dashboards | Purpose |
|------|-----------|---------|
| **Operations** | Overview, Operations Overview | System health, error tracking, log analysis |
| **Performance** | APM Performance, Command Performance, Database Performance, External HTTP | Latency, throughput, bottleneck identification |
| **Features** | Soundboard & Audio, AI Assistant | Feature usage analytics and cost tracking |
| **Activity** | Guild Activity, Commands & Interactions | User engagement, command distribution |
| **Health** | Service Health | Component-level monitoring, error classification |

### Architecture

The monitoring system uses:
- **Elasticsearch** - Data storage and indexing
- **Kibana Lens** - Modern visualization framework (all dashboards)
- **Elastic APM** - Distributed tracing and performance metrics
- **Serilog** - Structured logging from application
- **Index Patterns** - Log aggregation and APM data separation

---

## Accessing Kibana

### Instance Details

- **URL:** https://kibana.cpike.ca
- **Version:** 8.12.0
- **Access:** Requires browser access; use API key for programmatic access
- **Backup Location:** Production dashboards exported weekly

### Navigation Tips

1. **Home Dashboard:** Click "Kibana" logo (top-left) → Choose from favorites
2. **All Dashboards:** Analytics → Dashboards → Search by name
3. **Saved Searches:** Analytics → Discover → Open saved search dropdown
4. **Alerting:** Stack Management → Rules and Connectors → Rules

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `g` then `d` | Go to Dashboards |
| `g` then `a` | Go to Analytics |
| `ctrl` + `K` | Command palette |

---

## Data Views (Index Patterns)

Source data for all visualizations and dashboards:

| Data View ID | Index Pattern | Retention | Purpose | Environment |
|--------------|---------------|-----------|---------|-------------|
| `discordbot-logs-all` | `logs-discordbot-*` | 30 days | Aggregated logs from all environments | Dev/Staging/Prod |
| `discordbot-logs-prod` | `logs-discordbot-production` | 90 days | Production logs only | Production |
| `discordbot-logs-staging` | `logs-discordbot-staging` | 30 days | Staging logs only | Staging |
| `discordbot-logs-dev` | `logs-discordbot-development` | 14 days | Development logs only | Development |
| `discord-apm-traces` | `traces-apm-*` | 14 days | APM trace data and spans | All Environments |
| `apm-data` | `apm-*` | 14 days | General APM metrics and transactions | All Environments |

**Timestamp Field:** All data views use `@timestamp` for time-series filtering.

---

## Dashboards (11 Total)

### 1. Discord Bot - Overview
**ID:** `discord-overview` | **Time Range:** Last 7 days | **Refresh:** 30s

Comprehensive overview dashboard providing high-level system health status across all operational areas.

#### Panels (9)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Avg Duration | `discord-apm-perf-avg-duration` | Mean transaction time (ms) | Transaction performance baseline |
| Success Rate | `discord-apm-perf-success-rate` | % successful transactions | Overall system reliability |
| Commands | `discord-apm-cmd-total-commands` | Total slash commands executed | Command usage volume |
| DB Queries | `discord-apm-db-query-count` | Total database operations | Data layer activity |
| AI Requests | `discord-apm-ai-total-requests` | Total AI service calls | AI assistant usage |
| AI Cost | `discord-apm-ai-total-cost` | Cumulative API costs (USD) | Budget tracking |
| Top Sounds | `discord-sound-top` | Most-played audio clips | Audio feature popularity |
| TTS Voices | `discord-tts-voices` | Voice model distribution | Text-to-speech usage |
| Duration Timeline | `discord-apm-perf-duration-timeline` | Latency trend over time | Performance trending |

**Use Case:** Morning standup, shift handoff, daily operational review.

---

### 2. Discord Bot - Operations Overview
**ID:** `discord-ops-dashboard` | **Time Range:** Last 24 hours | **Refresh:** 5s

Operational health monitoring dashboard focused on log analysis and error tracking.

#### Panels (7)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Total Logs | `discord-total-metric` | Log event count | Overall activity volume |
| Errors | `discord-errors-metric` | Error count | Issue detection |
| Warnings | `discord-warnings-metric` | Warning count | Potential problems |
| Log Level Distribution | `discord-pie` | Breakdown by severity | Log composition |
| Log Volume Timeline | `discord-timeline` | Events per minute (5-min buckets) | Activity trends |
| Top Log Sources | `discord-top-sources` | Highest volume components | Where activity concentrates |
| Top Error Sources | `discord-error-sources` | Components with most errors | Problem areas |

**Use Case:** Real-time operational monitoring, error alerting, incident response.

---

### 3. Discord Bot - Service Health
**ID:** `discord-service-health-dashboard` | **Time Range:** Default | **Refresh:** 10s

Service-level health monitoring with component breakdown and error classification.

#### Panels (5)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Average Latency | `discord-svc-latency` | Mean response time (ms) | Service responsiveness |
| Errors Over Time | `discord-svc-errors-timeline` | Error trend (hourly) | Error pattern detection |
| Warnings Over Time | `discord-svc-warnings-timeline` | Warning trend (hourly) | Emerging issues |
| Service Breakdown by Logger | `discord-svc-breakdown` | Event counts per component | Component contribution |
| Error Types Distribution | `discord-svc-error-types` | Error categorization | Root cause patterns |

**Use Case:** Component health assessment, SLA tracking, error pattern analysis.

---

### 4. Discord Bot - Guild Activity
**ID:** `discord-guilds-dashboard` | **Time Range:** Default | **Refresh:** 30s

Multi-tenant activity overview showing per-guild metrics and engagement patterns.

#### Panels (4)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Activity by Guild | `discord-guild-activity` | Event count per guild | Guild engagement levels |
| Guild Activity Timeline | `discord-guild-timeline` | Activity trend per guild | Guild usage patterns |
| Errors by Guild | `discord-guild-errors` | Error distribution across guilds | Problem guild identification |
| Commands per Guild | `discord-guild-commands` | Command count per guild | Feature adoption |

**Use Case:** Multi-tenant troubleshooting, feature adoption tracking, high-activity guild identification.

---

### 5. Discord Bot - Commands & Interactions
**ID:** `discord-commands-dashboard` | **Time Range:** Last 7 days | **Refresh:** 30s

Detailed command analytics showing usage patterns, distribution, and performance.

#### Panels (5)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Total Commands | `discord-cmd-total` | Execution count | Overall command volume |
| Interaction Types | `discord-cmd-types` | Breakdown by interaction type | Feature engagement |
| Top Commands | `discord-cmd-top` | Most-executed commands (top 10) | Popular features |
| Commands Over Time | `discord-cmd-timeline` | Command trend (daily) | Usage growth analysis |
| Commands by Guild | `discord-cmd-guilds` | Guild command distribution | Guild preferences |

**Use Case:** Feature popularity analysis, command adoption tracking, usage trend forecasting.

---

### 6. Discord Bot - Soundboard & Audio
**ID:** `discord-soundboard-dashboard` | **Time Range:** Default | **Refresh:** 10s

Audio feature monitoring for soundboard, text-to-speech, and VOX (Half-Life concatenated audio) functionality.

#### Panels (10)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Top Sounds Played | `discord-sound-top` | Most-used sound clips (top 15) | Content popularity |
| Sounds by Guild | `discord-sound-guilds` | Sound usage distribution | Guild audio preferences |
| Sound Plays Over Time | `discord-sound-timeline` | Audio playback trend (hourly) | Feature engagement |
| TTS Voices Used | `discord-tts-voices` | Voice model distribution | Voice preference analysis |
| Audio Error Count | `discord-audio-errors` | Playback/generation failures | Audio quality issues |
| VOX Commands | `discord-vox-commands-total` | Total VOX commands executed | VOX feature usage |
| VOX by Group | `discord-vox-by-group` | Distribution across VOX/FVOX/HGRUNT | Clip group popularity |
| VOX Match Rate | `discord-vox-match-rate` | Average word-to-clip match percentage | Vocabulary coverage |
| VOX Over Time | `discord-vox-timeline` | VOX command trend by group | VOX usage patterns |
| VOX Errors | `discord-vox-errors` | VOX command failures | VOX error tracking |

**Use Case:** Audio feature analytics, content management, TTS service health, VOX usage monitoring.

---

### 7. Discord Bot - APM Performance
**ID:** `discord-apm-performance` | **Time Range:** Last 24 hours | **Refresh:** 30s

Application Performance Monitoring dashboard for end-to-end transaction analysis.

#### Panels (5)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Avg Duration | `discord-apm-perf-avg-duration` | Mean transaction latency (ms) | Performance baseline |
| Success Rate | `discord-apm-perf-success-rate` | % successful transactions | Reliability metric |
| Duration by Type | `discord-apm-perf-duration-by-type` | Latency per transaction type | Bottleneck identification |
| Duration Timeline | `discord-apm-perf-duration-timeline` | Latency trend (5-min buckets) | Performance degradation detection |
| Top 15 Slowest Transactions | `discord-apm-perf-slowest-txns` | Worst-performing transactions | Priority optimization targets |

**Use Case:** Performance optimization, SLA verification, bottleneck identification.

---

### 8. Discord Bot - Command Performance
**ID:** `discord-apm-commands` | **Time Range:** Last 7 days | **Refresh:** 30s

Slash command execution performance with per-command breakdown.

#### Panels (5)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Total Commands | `discord-apm-cmd-total-commands` | Command execution count | Command volume |
| Avg Command Duration | `discord-apm-cmd-avg-duration` | Mean command execution time (ms) | Command performance |
| Command Timeline | `discord-apm-cmd-timeline` | Command trend (hourly) | Usage patterns |
| Avg Duration by Command | `discord-apm-cmd-by-name` | Per-command latency | Performance profile |
| Commands by Guild | `discord-apm-cmd-by-guild` | Guild command distribution | Feature adoption |

**Use Case:** Command-level optimization, performance regression detection, command-specific troubleshooting.

---

### 9. Discord Bot - Database Performance
**ID:** `discord-apm-database` | **Time Range:** Last 24 hours | **Refresh:** 30s

Database query metrics from APM span data.

#### Panels (4)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| DB Query Count | `discord-apm-db-query-count` | Total database operations | Data layer activity |
| DB Avg Query Duration | `discord-apm-db-avg-duration` | Mean query execution time (ms) | Query performance |
| Query Volume Over Time | `discord-apm-db-volume-timeline` | Query rate trend (5-min buckets) | Load analysis |
| Slowest Queries Top 20 | `discord-apm-db-slowest-queries` | Worst-performing queries | Optimization targets |

**Use Case:** Database optimization, N+1 query detection, slow query identification.

---

### 10. Discord Bot - External HTTP
**ID:** `discord-apm-http` | **Time Range:** Last 24 hours | **Refresh:** 30s

External HTTP call monitoring including Discord API, Azure Services, and Anthropic AI latency.

#### Panels (5)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Total Calls | `discord-apm-http-total-calls` | HTTP request count | External API usage |
| Avg Latency | `discord-apm-http-avg-latency` | Mean response time (ms) | External service performance |
| Timeline | `discord-apm-http-timeline` | Request rate trend (5-min buckets) | Traffic patterns |
| Calls by Destination | `discord-apm-http-by-destination` | Distribution across endpoints | Service usage breakdown |
| Latency by Destination | `discord-apm-http-latency-by-dest` | Per-endpoint response times | Service performance ranking |

**Use Case:** External dependency monitoring, API quota tracking, service degradation detection.

---

### 11. Discord Bot - AI Assistant
**ID:** `discord-apm-ai` | **Time Range:** Last 7 days | **Refresh:** 30s

AI Assistant feature monitoring including Anthropic API costs, latency, and token usage.

#### Panels (5)
| Panel | Visualization | Metric | Purpose |
|-------|---------------|--------|---------|
| Total Requests | `discord-apm-ai-total-requests` | AI API call count | Feature usage |
| Total Cost | `discord-apm-ai-total-cost` | Cumulative API charges (USD) | Budget tracking |
| Avg Latency | `discord-apm-ai-avg-latency` | Mean response time (ms) | AI service responsiveness |
| Cost Over Time | `discord-apm-ai-cost-timeline` | Daily cost trend | Budget trending |
| Token Usage Over Time | `discord-apm-ai-token-usage` | Hourly token consumption | Quota monitoring |

**Use Case:** AI feature usage tracking, cost management, quota monitoring, rate-limit detection.

---

## Visualizations (70 Total)

All visualizations use Kibana Lens (modern visualization type). Organized by category:

### Operations & Logs Category (7)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-timeline` | Log Volume Timeline | Line | logs-discordbot-* | Event rate trending |
| `discord-pie` | Log Level Distribution | Pie | logs-discordbot-* | Log composition |
| `discord-warnings-metric` | Warnings Metric | Metric | logs-discordbot-* | Warning count |
| `discord-errors-metric` | Errors Metric | Metric | logs-discordbot-* | Error count |
| `discord-total-metric` | Total Logs Metric | Metric | logs-discordbot-* | Overall event count |
| `discord-error-sources` | Top Error Sources | Bar | logs-discordbot-* | Error source ranking |
| `discord-top-sources` | Top Log Sources | Bar | logs-discordbot-* | Component activity |

### Soundboard & Audio Category (10)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-sound-top` | Top Sounds Played | Bar | logs-discordbot-* | Popular audio clips |
| `discord-sound-guilds` | Sounds by Guild | Table | logs-discordbot-* | Guild audio usage |
| `discord-sound-timeline` | Sound Plays Over Time | Line | logs-discordbot-* | Audio usage trending |
| `discord-tts-voices` | TTS Voices Used | Pie | logs-discordbot-* | Voice preference |
| `discord-audio-errors` | Audio Error Count | Metric | logs-discordbot-* | Playback failures |
| `discord-vox-commands-total` | VOX Commands Total | Metric | logs-discordbot-* | VOX command count |
| `discord-vox-by-group` | VOX Commands by Group | Donut | logs-discordbot-* | VOX/FVOX/HGRUNT distribution |
| `discord-vox-match-rate` | VOX Average Match Rate | Metric | logs-discordbot-* | Word-to-clip match % |
| `discord-vox-timeline` | VOX Commands Over Time | Line | logs-discordbot-* | VOX usage trending |
| `discord-vox-errors` | VOX Errors | Metric | logs-discordbot-* | VOX command failures |

### Guild Activity Category (4)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-guild-activity` | Activity by Guild | Table | logs-discordbot-* | Guild metrics summary |
| `discord-guild-timeline` | Guild Activity Timeline | Line | logs-discordbot-* | Guild activity trends |
| `discord-guild-errors` | Errors by Guild | Bar | logs-discordbot-* | Guild error distribution |
| `discord-guild-commands` | Commands per Guild | Table | logs-discordbot-* | Guild command counts |

### Service Health Category (5)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-svc-latency` | Average Latency | Metric | logs-discordbot-* | Service responsiveness |
| `discord-svc-breakdown` | Service Breakdown by Logger | Pie | logs-discordbot-* | Component distribution |
| `discord-svc-errors-timeline` | Errors Over Time | Line | logs-discordbot-* | Error trending |
| `discord-svc-warnings-timeline` | Warnings Over Time | Line | logs-discordbot-* | Warning trending |
| `discord-svc-error-types` | Error Types Distribution | Pie | logs-discordbot-* | Error categorization |

### Commands Category (5)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-cmd-total` | Total Commands | Metric | logs-discordbot-* | Command volume |
| `discord-cmd-types` | Interaction Types | Pie | logs-discordbot-* | Interaction distribution |
| `discord-cmd-top` | Top Commands | Bar | logs-discordbot-* | Popular commands |
| `discord-cmd-timeline` | Commands Over Time | Line | logs-discordbot-* | Command usage trending |
| `discord-cmd-guilds` | Commands by Guild | Table | logs-discordbot-* | Guild command usage |

### APM Performance Category (9)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-apm-perf-avg-duration` | Avg Transaction Duration | Metric | apm-* | Mean latency |
| `discord-apm-perf-success-rate` | Success Rate | Metric | apm-* | Reliability metric |
| `discord-apm-perf-p95-latency` | P95 Latency | Metric | apm-* | 95th percentile latency |
| `discord-apm-perf-duration-timeline` | Duration Timeline | Line | apm-* | Latency trending |
| `discord-apm-perf-duration-by-type` | Duration by Type | Bar | apm-* | Per-type latency |
| `discord-apm-perf-slowest-txns` | Slowest Transactions | Table | apm-* | Top slow transactions |
| `discord-apm-perf-duration-over-time` | Duration Over Time | Line | apm-* | Long-term latency |
| `discord-apm-perf-slowest-transactions` | Slowest Transactions (Alt) | Table | apm-* | Optimization targets |
| `discord-apm-perf-success-failure-time` | Success vs Failure | Line | apm-* | Success/failure trend |

### APM Commands Category (8)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-apm-cmd-total-commands` | Total Commands | Metric | apm-* | Command volume |
| `discord-apm-cmd-avg-duration` | Avg Command Duration | Metric | apm-* | Command latency |
| `discord-apm-cmd-success-rate` | Command Success Rate | Metric | apm-* | Command reliability |
| `discord-apm-cmd-timeline` | Command Timeline | Line | apm-* | Command rate trending |
| `discord-apm-cmd-over-time` | Commands Over Time | Line | apm-* | Long-term trends |
| `discord-apm-cmd-by-name` | Duration by Command | Bar | apm-* | Per-command latency |
| `discord-apm-cmd-duration-by-name` | Duration by Name | Bar | apm-* | Comparative latency |
| `discord-apm-cmd-by-guild` | Commands by Guild | Table | apm-* | Guild distribution |

### APM Database Category (9)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-apm-db-query-count` | Query Count | Metric | apm-* | Database activity |
| `discord-apm-db-avg-duration` | Avg Query Duration | Metric | apm-* | Query performance |
| `discord-apm-db-total-queries` | Total Queries | Metric | apm-* | Long-term load |
| `discord-apm-db-p95-latency` | P95 Query Latency | Metric | apm-* | 95th percentile |
| `discord-apm-db-volume-timeline` | Query Volume Timeline | Line | apm-* | Load trending |
| `discord-apm-db-volume-over-time` | Volume Over Time | Line | apm-* | Long-term trending |
| `discord-apm-db-duration-over-time` | Duration Over Time | Line | apm-* | Latency trending |
| `discord-apm-db-duration-distribution` | Duration Distribution | Histogram | apm-* | Query time buckets |
| `discord-apm-db-slowest-queries` | Slowest Queries | Table | apm-* | Top optimization targets |

### APM External HTTP Category (5)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-apm-http-total-calls` | Total Calls | Metric | apm-* | API usage |
| `discord-apm-http-avg-latency` | Avg Latency | Metric | apm-* | Service performance |
| `discord-apm-http-timeline` | Timeline | Line | apm-* | Call rate trending |
| `discord-apm-http-by-destination` | Calls by Destination | Bar | apm-* | Service distribution |
| `discord-apm-http-latency-by-dest` | Latency by Destination | Bar | apm-* | Per-service latency |

### APM AI Assistant Category (9)

| ID | Name | Type | Data View | Purpose |
|----|------|------|-----------|---------|
| `discord-apm-ai-total-requests` | Total Requests | Metric | apm-* | Feature usage |
| `discord-apm-ai-total-cost` | Total Cost | Metric | apm-* | Budget tracking |
| `discord-apm-ai-avg-latency` | Avg Latency | Metric | apm-* | Service responsiveness |
| `discord-apm-ai-cost-timeline` | Cost Timeline | Line | apm-* | Daily cost trending |
| `discord-apm-ai-cost-over-time` | Cost Over Time | Line | apm-* | Long-term budget |
| `discord-apm-ai-token-usage` | Token Usage | Line | apm-* | Quota monitoring |
| `discord-apm-ai-success-rate` | Success vs Rate Limited | Bar | apm-* | Rate limit detection |
| `discord-apm-ai-latency-distribution` | Latency Distribution | Histogram | apm-* | Latency buckets |
| `discord-apm-ai-tool-calls` | Tool Calls | Metric | apm-* | Function usage |

---

## Dashboard Selection Guide

### Choosing the Right Dashboard

**For Different Roles:**

| Role | Primary Dashboard | Secondary | Purpose |
|------|-------------------|-----------|---------|
| **On-Call Engineer** | Operations Overview | Service Health | Real-time issue detection |
| **DevOps/SRE** | APM Performance | Service Health | Infrastructure & performance |
| **Product Manager** | Commands & Interactions | Soundboard & Audio | Feature usage & adoption |
| **Backend Developer** | Database Performance | APM Commands | Performance optimization |
| **Audio/TTS Developer** | Soundboard & Audio | Command Performance | Feature-specific monitoring (Soundboard, TTS, VOX) |
| **AI Product Owner** | AI Assistant | Overview | Cost tracking & usage |
| **Manager/Leadership** | Overview | Guild Activity | High-level health status |

**For Different Scenarios:**

| Scenario | Dashboard | Action |
|----------|-----------|--------|
| **Incident Response** | Operations Overview (look for error spike) → Guild Activity (identify affected guilds) | Narrow down issue scope |
| **Performance Degradation** | APM Performance (identify slow transaction) → Database Performance (query optimization) → External HTTP (dependency check) | Root cause analysis |
| **Billing/Budget Review** | AI Assistant (cost tracking) | Cost management |
| **Feature Adoption** | Commands & Interactions (top commands) + Soundboard & Audio (audio/VOX usage) | Feature analytics |
| **Daily Standup** | Overview (7-day view) | Quick health check |
| **Post-Mortems** | All APM dashboards + Operations Overview | Comprehensive analysis |

---

## Common KQL Queries

Query Language (KQL) for filtering dashboard data or using in Discover.

### Error Investigation

```kql
# Find all errors
level:Error

# Errors from specific component (example: bot initialization)
level:Error AND SourceContext:*BotHostedService*

# Errors for specific guild
level:Error AND GuildId:"123456789012345678"

# Errors containing specific keywords
level:Error AND (message:*database* OR message:*timeout* OR message:*permission*)

# Errors with stack traces
level:Error AND Exception:*

# Errors from last 1 hour (use time picker for range)
level:Error AND @timestamp:>now-1h
```

### Command Performance Analysis

```kql
# All command executions
SourceContext:*InteractionHandler*

# Specific command performance (example: /vox)
CommandName:"vox" AND SourceContext:*InteractionHandler*

# Slow commands (>500ms)
ExecutionTimeMs:>500 AND CommandName:*

# Commands by guild
GuildId:"123456789012345678" AND SourceContext:*InteractionHandler*

# Command errors
SourceContext:*InteractionHandler* AND level:Error
```

### Database Query Analysis

```kql
# All database queries
SourceContext:*Repository*

# Slow queries (>1 second)
SourceContext:*Repository* AND ExecutionTimeMs:>1000

# Specific operation (example: guild lookup)
SourceContext:*GuildRepository*

# Query timeouts
SourceContext:*Repository* AND message:*timeout*

# Database connection errors
SourceContext:*Database* AND level:Error
```

### Audio/Soundboard Monitoring

```kql
# Soundboard playback events
SourceContext:*Soundboard* OR message:*sound*

# TTS generation
SourceContext:*TextToSpeech* OR message:*tts*

# Audio errors
(SourceContext:*Audio* OR SourceContext:*Soundboard*) AND level:Error

# Guild audio usage
GuildId:"123456789012345678" AND (SourceContext:*Audio* OR SourceContext:*Soundboard*)
```

### VOX System Monitoring

```kql
# All VOX commands (started)
message:"VOX_COMMAND_STARTED"

# Successful VOX completions
message:"VOX_COMMAND_COMPLETED"

# VOX failures
message:"VOX_COMMAND_FAILED"

# VOX by specific group (VOX, FVOX, HGRUNT)
message:"VOX_COMMAND_COMPLETED" AND labels.Group:"FVOX"

# Low match rate commands (<50%)
message:"VOX_COMMAND_COMPLETED" AND labels.MatchPercentage:<50

# VOX concatenation events
message:"VOX_CONCATENATION_COMPLETED"

# VOX errors by type
message:"VOX_COMMAND_FAILED" AND labels.ErrorType:"NoClipsMatched"

# Slow VOX commands (>2 seconds)
message:"VOX_COMMAND_COMPLETED" AND labels.DurationMs:>2000

# VOX portal vs slash command usage
message:"VOX_COMMAND_STARTED" AND labels.Source:"Portal"
```

### External Service Monitoring

```kql
# Discord API calls
SourceContext:*DiscordClient*

# Azure Speech Service
SourceContext:*AzureSpeech* OR message:*azure*

# Anthropic AI Service
SourceContext:*Anthropic* OR message:*claude*

# Rate limiting
message:*rate limit* OR discord.api.rate_limit.remaining:0

# Service timeouts
message:*timeout* OR message:*deadline*
```

### User/Guild Activity

```kql
# All activity for specific user
UserId:"987654321098765432"

# All activity for specific guild
GuildId:"123456789012345678"

# Commands by user in guild
GuildId:"123456789012345678" AND UserId:"987654321098765432" AND CommandName:*

# User errors
UserId:"987654321098765432" AND level:Error
```

### Correlation & Tracing

```kql
# Track by correlation ID (all logs for single request)
CorrelationId:"a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6"

# Track by APM trace ID
trace.id:"abc123def456ghi789"

# Track by APM transaction ID
transaction.id:"xyz789uvw456"

# Multi-step request debugging
CorrelationId:* AND GuildId:"123456789012345678"
```

### Log Level Filtering

```kql
# Only warnings
level:Warning

# Only critical
level:Critical

# Exclude debug messages
level:* AND NOT level:Debug

# Info and above (warning, error, critical)
level:(Information OR Warning OR Error OR Critical)
```

---

## Filtering Dashboard Data

All dashboards support filtering using:

1. **Time Picker** (top-right): Change time range
   - Relative: "Last 1 hour", "Last 7 days"
   - Absolute: Specific date range
   - Auto-refresh: 5s, 10s, 30s, 1m

2. **Filter Panel** (left sidebar when in edit mode): Add custom filters
   - Click "Add filter"
   - Select field (e.g., `GuildId`, `SourceContext`, `level`)
   - Choose operator (is, is not, contains)
   - Enter value

3. **Search Bar** (top): Enter KQL query for advanced filtering
   - Overwrites all filters when used
   - Returns to filter mode when cleared

4. **Visualization Interaction**: Click legend items to filter
   - Example: Click a guild name in "Guild Activity" to filter to that guild
   - Example: Click "Error" in "Log Level Distribution" to show only errors

---

## Interpreting Dashboard Visualizations

### Key Metrics Explained

**Performance Metrics**

| Metric | Typical Value | Warning Threshold | Critical Threshold | Improvement |
|--------|---------------|-------------------|-------------------|-------------|
| Avg Duration (ms) | 50-200ms | >500ms | >2000ms | Reduce DB queries, cache responses |
| P95 Latency (ms) | 100-500ms | >1000ms | >5000ms | Profile slow endpoints |
| Success Rate (%) | 95-99% | <95% | <90% | Fix application errors |
| Error Rate (%) | <1% | >2% | >5% | Investigate error sources |

**Throughput Metrics**

| Metric | Typical Value | Scaling Indicator | Action |
|--------|---------------|-------------------|--------|
| Commands/minute | 10-50 | >200/min sustained | Monitor capacity |
| DB Queries/minute | 100-500 | >1000/min | Optimize queries |
| API Calls/minute | 50-100 | >500/min | Rate limit risk |

**Cost Metrics (AI Assistant)**

| Metric | Typical Value | Warning | Critical |
|--------|---------------|---------|----------|
| Daily Cost | $0.10-1.00 | >$5.00/day | >$10.00/day |
| Monthly Cost | $3-30 | >$150 | >$300 |
| Tokens/day | 1M-10M | >50M | >100M |

### Common Visualization Patterns

**"No Data" Condition**
- Check time range (expand to last 7 days if last 24h shows nothing)
- Verify data is being sent (check Operations Overview for errors)
- Check time zone settings

**Sudden Spikes**
- Look at Operations Overview "Top Error Sources" to identify cause
- Filter by guild to see if issue is localized
- Check for scheduled jobs or batch operations

**Flat Line/No Activity**
- Verify bot is running (`docker ps` or check service status)
- Check for log level filtering (may be suppressing logs)
- Review recent deployments or configuration changes

---

## Dashboard Interaction Examples

### Example 1: Investigating High Error Rate

1. Open **Operations Overview** dashboard
2. Notice "Errors" metric increased significantly
3. Click "Top Error Sources" to identify problematic component
4. Click component name to filter
5. Open new tab with **Service Health** dashboard (already filtered)
6. Review "Error Types Distribution" for categorization
7. Click on error type to narrow further
8. Go to **Discover** and run detailed KQL query: `level:Error AND SourceContext:"your-component"`
9. Review error messages and stack traces for root cause

### Example 2: Performance Optimization

1. Open **APM Performance** dashboard
2. Identify high "Avg Duration" or spike in "Duration Timeline"
3. Go to **Database Performance** dashboard
4. Check if DB query latency correlates with performance spike
5. If yes: Look at "Slowest Queries" and optimize SQL
6. If no: Go to **External HTTP** dashboard
7. Check if external API calls are slow
8. If issue not found: Go to **APM Commands** and filter by specific command
9. Review command-level metrics and add application-level profiling

### Example 3: AI Feature Cost Management

1. Open **AI Assistant** dashboard
2. Review "Total Cost" and "Cost Over Time" panels
3. If trending upward, click "Total Requests" to see usage correlation
4. Filter by guild to identify high-usage guilds
5. Go to **Commands & Interactions** dashboard
6. Filter by guild to see if specific commands drive AI usage
7. Review conversation with product team about usage optimization

---

## Alerting Configuration

### Alert Status

**Current Configuration:** 0 alerting rules active (no alerts currently configured)

**To Create Alerts:**

1. Go to **Stack Management → Rules and Connectors → Rules**
2. Click "Create Rule"
3. Select condition type (e.g., Elasticsearch query)
4. Set up query and threshold
5. Configure actions (email, Slack, PagerDuty)
6. Enable rule

### Recommended Alert Rules

| Rule | Condition | Severity | Action |
|------|-----------|----------|--------|
| High Error Rate | Error count > 10/min for 5 min | High | Slack + Email |
| Slow Transactions | P99 latency > 5 seconds | Medium | Slack |
| Database Errors | Any `SourceContext:*Database*` AND level:Error | High | Email |
| AI Cost Spike | Daily cost > $10 | Medium | Email |
| Bot Offline | No logs for 5 minutes | Critical | PagerDuty + Email |

See [elastic-stack-setup.md](elastic-stack-setup.md) for alerting setup instructions.

---

## Troubleshooting

### Dashboard Issues

**Dashboards Show "No Data"**

1. Verify time range is appropriate (try "Last 7 days")
2. Check if indexes exist: Kibana → Stack Management → Data Views
3. Verify bot is running and sending logs
4. Check Elasticsearch connection: `curl https://elasticsearch:9200/_cluster/health`

**Visualizations Loading Slowly**

1. Reduce time range (change from 30 days to 7 days)
2. Add filters to reduce data scanned
3. Check Elasticsearch performance: `curl https://elasticsearch:9200/_nodes/stats`
4. Consider archiving old data with Index Lifecycle Management (ILM)

**Specific Visualization Not Working**

1. Click visualization in dashboard
2. Check data view is correct and has required fields
3. Verify query syntax in visualization editor
4. Check for missing fields: Go to Discover, select data view, see available fields

### APM Issues

**APM Data Not Appearing**

1. Verify APM Server is running
2. Check application configuration: `ElasticApm:ServerUrl` in user secrets
3. Review bot logs for APM errors
4. Verify service name matches (should be "discordbot")

**Traces Show Zero Transactions**

1. Check bot application is actually handling requests
2. Verify APM sampler rate isn't too low
3. Look at Operations Overview to ensure other data is arriving
4. Check `apm-*` index exists in Elasticsearch

### Index/Data View Issues

**Index Pattern Not Found**

1. Kibana → Stack Management → Data Views
2. Click "Create data view"
3. Enter index pattern (e.g., `logs-discordbot-*`)
4. Select timestamp field: `@timestamp`
5. Save and try dashboard again

**Fields Not Visible in Visualizations**

1. Go to Stack Management → Data Views
2. Open the data view
3. Scroll to fields section
4. Search for missing field
5. If not found, verify logs actually contain that field (use Discover)

---

## Best Practices

### Dashboard Usage

- **Daily Review:** Check Overview dashboard each morning
- **Incident Response:** Use Operations Overview for real-time status
- **Performance Analysis:** Review APM dashboards weekly
- **Feature Analytics:** Check Commands & Interactions monthly
- **Cost Tracking:** Review AI Assistant dashboard weekly

### Creating Custom Dashboards

1. Start with Discover to build and test queries
2. Save as Saved Search
3. Create visualization from saved search if needed
4. Add to new dashboard
5. Use consistent naming: `[Discord] Feature - Aspect`
6. Document purpose in dashboard description

### Query Performance

- Avoid wildcards at start of queries (e.g., `*Bot*` is slow)
- Use more specific time ranges when possible
- Filter by guild or user early in the query
- Use index patterns that exclude unneeded environments

---

## Related Documentation

- [Log Aggregation](log-aggregation.md) - Serilog configuration, structured logging, query examples
- [Elastic Stack Setup](elastic-stack-setup.md) - Docker setup, Elasticsearch configuration, verification
- [Elastic APM](elastic-apm.md) - Application instrumentation, trace collection, span analysis
- [Bot Performance Dashboard](bot-performance-dashboard.md) - Application-level metrics and dashboards
- [VOX Telemetry Spec](vox-telemetry-spec.md) - VOX metrics, tracing, and logging specification

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.1 | 2026-02-04 | Added VOX telemetry visualizations (5 new) to Soundboard & Audio dashboard, VOX KQL queries |
| 2.0 | 2026-02-03 | Complete dashboard inventory (11 dashboards, 65 visualizations), comprehensive filtering guide, troubleshooting section, best practices |
| 1.0 | 2026-01-06 | Initial release with basic dashboard overview and alerting configuration |

---

## Support & Access

**Production Instance:** https://kibana.cpike.ca (v8.12.0)

**For Access Issues:**
- Verify you have Kibana credentials
- Check API key hasn't expired (if using API access)
- Verify network connectivity to kibana.cpike.ca

**For Data Issues:**
- Review [Log Aggregation](log-aggregation.md) for logging configuration
- Check Elasticsearch cluster health
- Verify bot is configured with correct environment

---

*Last Updated: February 4, 2026*
