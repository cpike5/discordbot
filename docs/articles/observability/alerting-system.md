# Performance Alerting System

**Version:** 1.0
**Last Updated:** 2025-01-28
**Related Issue:** [#1337](https://github.com/cpike5/discordbot/issues/1337)

---

## Table of Contents

1. [Overview](#overview)
2. [Core Concepts](#core-concepts)
3. [Architecture](#architecture)
4. [Configuration](#configuration)
5. [Monitored Metrics](#monitored-metrics)
6. [Alert Flow](#alert-flow)
7. [Notification Channels](#notification-channels)
8. [API Endpoints](#api-endpoints)
9. [Database Schema](#database-schema)
10. [UI Components](#ui-components)
11. [Thresholds Management](#thresholds-management)
12. [Auto-Recovery](#auto-recovery)
13. [Key Architectural Decisions](#key-architectural-decisions)
14. [Integration Patterns](#integration-patterns)
15. [Testing Strategy](#testing-strategy)
16. [Troubleshooting](#troubleshooting)
17. [Future Enhancements](#future-enhancements)
18. [Related Documentation](#related-documentation)

---

## Overview

The Performance Alerting System monitors bot performance metrics in real-time, detects threshold breaches, and notifies administrators of potential issues. It combines configurable threshold detection with multi-channel notifications (real-time SignalR updates and in-app notifications) to provide visibility into system health.

### Key Features

- **Configurable Thresholds**: Define warning and critical thresholds for each metric
- **Intelligent Alert Filtering**: Consecutive breach/normal counters prevent alert noise
- **Automatic Recovery**: Incidents auto-resolve when metrics normalize
- **Real-Time Notifications**: SignalR hub broadcasts incidents to connected admin dashboards
- **In-App Notifications**: Fire-and-forget notifications for all admin users
- **Incident Tracking**: Full lifecycle tracking with acknowledgment capability
- **Historical Analysis**: Query and filter past incidents with pagination
- **Admin Dashboard**: Web UI for monitoring, configuration, and incident management

### System Health Benefits

- **Proactive Issue Detection**: Identifies performance degradation before users are impacted
- **Reduced Alert Noise**: Consecutive breach counters and auto-recovery prevent notification fatigue
- **Admin Awareness**: Real-time updates keep administrators informed of system state
- **Compliance**: Incident history enables post-incident analysis and SLA tracking

---

## Core Concepts

### Severity Levels

Incidents are classified into three severity levels based on which threshold was exceeded:

| Severity | Value | Description | Threshold | Example |
|----------|-------|-------------|-----------|---------|
| **Info** | 0 | Informational - metric within range but noteworthy | None | For custom metrics or reporting |
| **Warning** | 1 | Metric exceeded warning threshold | Warning threshold | Command latency at 800ms (warning: 1000ms) |
| **Critical** | 2 | Metric exceeded critical threshold | Critical threshold | Memory usage at 95% (critical: 90%) |

**Enum Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Enums\AlertSeverity.cs`

### Incident Status

Each incident progresses through a lifecycle of states:

| Status | Value | Description | Recovery Method |
|--------|-------|-------------|-----------------|
| **Active** | 0 | Currently unresolved, requires attention | Metric normalizes to Resolved; or manually acknowledged |
| **Acknowledged** | 2 | Reviewed by administrator but not yet resolved | Metric normalizes to Resolved |
| **Resolved** | 1 | Auto-recovered (metrics normalized) or manually closed | Final state |

**Enum Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Enums\IncidentStatus.cs`

### Alert Configuration

An alert configuration defines threshold behavior for a single metric:

```csharp
public class PerformanceAlertConfig
{
    public int Id { get; set; }
    public string MetricName { get; set; }           // "gateway_latency"
    public string DisplayName { get; set; }          // "Gateway Latency"
    public string? Description { get; set; }         // User-friendly description
    public double? WarningThreshold { get; set; }    // 500.0
    public double? CriticalThreshold { get; set; }   // 1000.0
    public string ThresholdUnit { get; set; }        // "ms"
    public bool IsEnabled { get; set; }              // true/false
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }           // User who last changed thresholds
}
```

**Entity Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Entities\PerformanceAlertConfig.cs`

### Performance Incident

An incident represents a single threshold breach event:

```csharp
public class PerformanceIncident
{
    public Guid Id { get; set; }                     // Unique incident ID
    public string MetricName { get; set; }           // "command_p95_latency"
    public AlertSeverity Severity { get; set; }      // Warning or Critical
    public IncidentStatus Status { get; set; }       // Active, Acknowledged, Resolved
    public DateTime TriggeredAt { get; set; }        // When breach occurred
    public DateTime? ResolvedAt { get; set; }        // When metric normalized
    public double ThresholdValue { get; set; }       // Configured threshold at trigger time
    public double ActualValue { get; set; }          // Actual metric value that triggered it
    public string Message { get; set; }              // Human-readable description
    public bool IsAcknowledged { get; set; }         // Admin reviewed it
    public string? AcknowledgedBy { get; set; }      // User ID of acknowledger
    public DateTime? AcknowledgedAt { get; set; }    // When acknowledged
    public string? Notes { get; set; }               // Admin findings/context
}
```

**Entity Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Entities\PerformanceIncident.cs`

---

## Architecture

The alerting system follows the three-layer clean architecture:

### Layer Breakdown

```
Core (Domain)
├── AlertSeverity enum
├── IncidentStatus enum
├── PerformanceAlertConfig entity
├── PerformanceIncident entity
├── PerformanceAlertOptions (config)
└── IPerformanceAlertRepository interface

Infrastructure (Data Access)
├── PerformanceAlertRepository
│   ├── GetAllAsync() - all configs
│   ├── GetEnabledAsync() - only enabled configs
│   ├── GetByMetricNameAsync() - single config
│   ├── GetActiveIncidentsAsync() - unresolved incidents
│   ├── CreateIncidentAsync() - new incident
│   ├── UpdateIncidentAsync() - update status/notes
│   └── DeleteOldIncidentsAsync() - cleanup
├── EF Core DbContext
│   ├── DbSet<PerformanceAlertConfig>
│   └── DbSet<PerformanceIncident>
└── Database tables
    ├── PerformanceAlertConfigs
    └── PerformanceIncidents

Bot (Application/UI)
├── PerformanceAlertService
│   ├── GetConfigurationsAsync()
│   ├── GetActiveIncidentsAsync()
│   ├── GetIncidentsAsync() - paginated query
│   ├── AcknowledgeIncidentAsync()
│   ├── AcknowledgeAllIncidentsAsync()
│   └── UpdateConfigurationAsync()
├── AlertMonitoringService (IHostedService)
│   ├── Runs background loop at CheckIntervalSeconds
│   ├── Evaluates each enabled metric
│   ├── Tracks consecutive breach/normal counts
│   ├── Creates PerformanceIncident via repository
│   ├── Broadcasts via SignalR DashboardHub
│   └── Fires notifications via INotificationService
├── PerformanceNotifier
│   ├── OnAlertTriggered
│   ├── OnAlertResolved
│   ├── OnAlertAcknowledged
│   └── OnActiveAlertCountChanged
├── AlertsController (REST API)
│   ├── GET /api/alerts/config
│   ├── GET /api/alerts/config/{metricName}
│   ├── PUT /api/alerts/config/{metricName}
│   ├── GET /api/alerts/active
│   ├── GET /api/alerts/incidents
│   ├── GET /api/alerts/incidents/{id}
│   ├── POST /api/alerts/incidents/{id}/acknowledge
│   ├── POST /api/alerts/incidents/acknowledge-all
│   ├── GET /api/alerts/summary
│   ├── GET /api/alerts/frequency
│   └── GET /api/alerts/auto-recovery
├── Alerts Page (UI)
│   └── /Admin/Performance/Alerts.cshtml
└── Alerts Configuration Tab
    └── /Admin/Performance/Tabs/_AlertsTab.cshtml
```

### Component Interactions

```
External Metrics (every 30s)
    ↓
AlertMonitoringService reads metric values
    ↓
Compares against PerformanceAlertConfig thresholds
    ↓
Tracks consecutive breaches/normal readings
    ├─→ ConsecutiveBreachesRequired (e.g., 2)
    │   ├─→ Creates PerformanceIncident
    │   ├─→ Persists via IPerformanceAlertRepository
    │   ├─→ Broadcasts via IPerformanceNotifier → DashboardHub (SignalR)
    │   └─→ Creates UserNotification via INotificationService (fire-and-forget)
    │
    └─→ ConsecutiveNormalRequired (e.g., 3) on Active incident
        └─→ Marks PerformanceIncident as Resolved
            ├─→ Broadcasts resolution via DashboardHub
            └─→ Creates UserNotification if Critical (fire-and-forget)
```

---

## Configuration

### PerformanceAlertOptions

Configuration is controlled via the `PerformanceAlertOptions` class and can be set in `appsettings.json`.

**Options Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Configuration\PerformanceAlertOptions.cs`

#### Configuration Parameters

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `CheckIntervalSeconds` | `int` | 30 | How often the monitoring service evaluates metrics (in seconds) |
| `ConsecutiveBreachesRequired` | `int` | 2 | Number of consecutive breaches needed before creating an incident (anti-noise) |
| `ConsecutiveNormalRequired` | `int` | 3 | Number of consecutive normal readings before auto-resolving active incidents |
| `IncidentRetentionDays` | `int` | 90 | Days to keep resolved incidents in the database before cleanup |

#### appsettings.json Example

```json
{
  "PerformanceAlerts": {
    "CheckIntervalSeconds": 30,
    "ConsecutiveBreachesRequired": 2,
    "ConsecutiveNormalRequired": 3,
    "IncidentRetentionDays": 90
  }
}
```

#### Configuration Rationale

- **CheckIntervalSeconds = 30**: Balances responsiveness (detect issues quickly) vs load (don't check too frequently)
- **ConsecutiveBreachesRequired = 2**: Prevents single-spike false positives without being too slow to detect real issues
- **ConsecutiveNormalRequired = 3**: Ensures metric has truly stabilized before auto-recovery
- **IncidentRetentionDays = 90**: Standard compliance window; adjust based on regulatory requirements

---

## Monitored Metrics

The `AlertMonitoringService` monitors 8 performance metrics across different subsystems:

| Metric Name | Display Name | Unit | Source Service | Critical Value |
|------------|--------------|------|-----------------|-----------------|
| `gateway_latency` | Gateway Latency | ms | `ILatencyHistoryService` | > 1000ms |
| `command_p95_latency` | Command P95 Latency | ms | `ICommandPerformanceAggregator` | > 1500ms |
| `error_rate` | Error Rate | % | `ICommandPerformanceAggregator` | > 5% |
| `memory_usage` | Memory Usage | MB | `Process.GetCurrentProcess()` | > 512MB |
| `api_rate_limit_usage` | API Rate Limit Usage | count | `IApiRequestTracker` | > 3600 |
| `database_query_time` | Database Query Time | ms | `IDatabaseMetricsCollector` | > 100ms |
| `bot_disconnected` | Bot Connection Status | 1.0/0.0 | `IConnectionStateService` | 1.0 (disconnected) |
| `service_failure` | Service Health | 1.0/0.0 | `IBackgroundServiceHealthRegistry` | 1.0 (failing) |

### Metric Description Examples

**gateway_latency**: Measures the round-trip time between bot and Discord's WebSocket gateway. Elevated latency indicates network issues or gateway overload.

**command_p95_latency**: 95th percentile response time for Discord slash commands. Measures user-perceived performance.

**error_rate**: Percentage of commands that fail or throw exceptions. Elevated rates indicate code or infrastructure issues.

**memory_usage**: Current process memory in megabytes. High memory usage can cause performance degradation and crashes.

**api_rate_limit_usage**: Count of API requests against Discord rate limits. Tracks API quota consumption for the rate limit window.

**database_query_time**: Average or P95 database query execution time in milliseconds. Identifies slow queries impacting performance.

**bot_disconnected**: Binary flag (1.0 = disconnected, 0.0 = connected). Indicates bot is not connected to Discord gateway.

**service_failure**: Binary flag (1.0 = any background service failing, 0.0 = all healthy). Indicates a background service (e.g., message processing, rate watch) has entered error state.

---

## Alert Flow

### Complete Incident Lifecycle

```
1. METRIC CHECK PHASE (every 30 seconds)
   └─ AlertMonitoringService wakes up
      ├─ Fetches current metric value from source
      └─ Compares against PerformanceAlertConfig thresholds

2. THRESHOLD EVALUATION
   ├─ IF metric < warning threshold AND < critical threshold:
   │  └─ Mark as "normal" reading
   │     └─ Increment normal counter (if Active incident exists)
   │
   ├─ IF metric >= critical threshold:
   │  └─ Mark as "breach" reading (Critical severity)
   │     └─ Increment breach counter
   │
   └─ IF metric >= warning threshold BUT < critical threshold:
      └─ Mark as "breach" reading (Warning severity)
         └─ Increment breach counter

3. INCIDENT CREATION PHASE
   ├─ IF breach counter >= ConsecutiveBreachesRequired (e.g., 2):
   │  ├─ Check if Active incident already exists for this metric
   │  │  └─ If YES: skip (don't create duplicate)
   │  │  └─ If NO: proceed to step 3a
   │  │
   │  3a. Create PerformanceIncident entity:
   │      ├─ MetricName = metric identifier
   │      ├─ Severity = Warning or Critical
   │      ├─ Status = Active
   │      ├─ TriggeredAt = now (UTC)
   │      ├─ ThresholdValue = configured threshold
   │      ├─ ActualValue = measured value
   │      └─ Message = "metric_name exceeded X: 1500ms > 1000ms"
   │
   │  3b. Persist to database via IPerformanceAlertRepository.CreateIncidentAsync()
   │
   │  3c. Broadcast via IPerformanceNotifier → DashboardHub
   │      └─ Client method: OnAlertTriggered(incident)
   │
   │  3d. Create in-app notification (fire-and-forget)
   │      └─ IF Severity >= Warning:
   │         └─ INotificationService.CreateForAllAdminsAsync()
   │            (only Warning+ severity creates notifications)
   │
   └─ IF normal counter >= ConsecutiveNormalRequired (e.g., 3):
      └─ Proceed to step 4

4. AUTO-RECOVERY PHASE
   ├─ IF Active incident exists for this metric:
   │  ├─ Retrieve incident from database
   │  ├─ Set Status = Resolved
   │  ├─ Set ResolvedAt = now (UTC)
   │  ├─ Persist update via repository.UpdateIncidentAsync()
   │  ├─ Reset normal counter to 0
   │  ├─ Reset breach counter to 0
   │  │
   │  ├─ Broadcast via IPerformanceNotifier → DashboardHub
   │  │  └─ Client method: OnAlertResolved(incident)
   │  │
   │  └─ Create in-app notification (fire-and-forget)
   │     └─ IF Severity == Critical:
   │        └─ INotificationService.CreateForAllAdminsAsync()
   │           (only Critical resolution notifies)
   │
   └─ Continue monitoring at next interval

5. ADMIN ACKNOWLEDGMENT PHASE
   ├─ Admin visits /Admin/Performance/Alerts page
   ├─ Views incident details
   ├─ Clicks "Acknowledge" button
   │  └─ Sends POST /api/alerts/incidents/{id}/acknowledge
   │
   ├─ PerformanceAlertService.AcknowledgeIncidentAsync() executes:
   │  ├─ Retrieve incident from database
   │  ├─ Set IsAcknowledged = true
   │  ├─ Set AcknowledgedBy = current user ID
   │  ├─ Set AcknowledgedAt = now (UTC)
   │  ├─ Optionally set Notes from request body
   │  ├─ Persist via repository.UpdateIncidentAsync()
   │  │
   │  ├─ Broadcast via IPerformanceNotifier → DashboardHub
   │  │  └─ Client method: OnAlertAcknowledged(incident)
   │  │
   │  └─ Return 200 OK with updated incident
   │
   └─ Incident remains Active until auto-recovery occurs
```

### Counter Reset Rules

| Scenario | Breach Counter | Normal Counter | Next Step |
|----------|---|---|-----------|
| Normal metric reading | Reset to 0 | +1 | If normal counter >= ConsecutiveNormalRequired → auto-resolve |
| Threshold breach | +1 | Reset to 0 | If breach counter >= ConsecutiveBreachesRequired → create incident |
| Incident created | Reset to 0 | Reset to 0 | Continue monitoring |
| Incident auto-resolved | N/A | Reset to 0 | Continue monitoring |

---

## Notification Channels

The alerting system notifies admins through two independent channels:

### 1. SignalR Real-Time Updates (DashboardHub)

**Real-time, low-latency updates** to connected admin dashboards and Alerts pages.

#### Hub Events (Server → Client)

Broadcast via `IPerformanceNotifier` interface:

```csharp
public interface IPerformanceNotifier
{
    // Called when a new incident is created
    Task OnAlertTriggered(PerformanceIncidentDto incident);

    // Called when an incident auto-recovers to Resolved
    Task OnAlertResolved(PerformanceIncidentDto incident);

    // Called when admin acknowledges an incident
    Task OnAlertAcknowledged(PerformanceIncidentDto incident);

    // Called when active incident count changes (for badge)
    Task OnActiveAlertCountChanged(int activeCount);
}
```

#### Client Implementation

```javascript
// In wwwroot/js/performance/alerts-realtime.js
connection.on("OnAlertTriggered", (incident) => {
    // Add incident to UI list
    // Play notification sound
    // Update badge count
    // Show toast alert
});

connection.on("OnAlertResolved", (incident) => {
    // Mark incident as resolved in UI
    // Update badge count
    // Show resolution toast
});

connection.on("OnAlertAcknowledged", (incident) => {
    // Update incident in UI with admin info
    // Show acknowledgment toast
});

connection.on("OnActiveAlertCountChanged", (count) => {
    // Update badge number in navbar
});
```

**Benefits**: Instant feedback, no polling required, updates visible immediately as they happen

### 2. In-App Notifications (UserNotification)

**Persistent notifications** visible in the navbar notification bell. Created via `INotificationService` using fire-and-forget pattern.

#### Filtering Rules

| Severity | Action | Creates Notification | Reason |
|----------|--------|---------------------|--------|
| Info | Created | NO | Too noisy, info-level doesn't require notification |
| Warning | Created | YES | Important enough to notify all admins |
| Critical | Created | YES | Definitely needs notification |
| Any | Resolved (auto-recovery) | YES if Critical | Only Critical resolution warrants notification |

#### Creation Pattern

```csharp
// In AlertMonitoringService.MonitorMetricsAsync()
if (incident.Severity >= AlertSeverity.Warning) {
    var deduplicationWindow = TimeSpan.FromMinutes(
        _options.Value.DuplicateSuppressionMinutes ?? 5);

    // Fire-and-forget - don't block alert creation if notification fails
    _ = Task.Run(async () => {
        try {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider
                .GetRequiredService<INotificationService>();

            await notificationService.CreateForAllAdminsAsync(
                NotificationType.PerformanceAlert,
                title: $"{incident.MetricName} {incident.Severity}",
                message: incident.Message,
                linkUrl: "/Admin/Performance/Alerts",
                relatedEntityType: "PerformanceIncident",
                relatedEntityId: incident.Id.ToString(),
                deduplicationWindow: deduplicationWindow);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to create notification");
        }
    }, cancellationToken);
}
```

**Benefits**: Persistent record, visible in notification bell sidebar, survives page refresh

### Channel Comparison

| Aspect | SignalR | In-App Notification |
|--------|---------|-------------------|
| Delivery | Real-time | Within seconds (background service) |
| Persistence | Only while page open | Persists until dismissed |
| Visibility | Active pages only | Visible in navbar bell |
| Deduplication | Per-broadcast | Based on relatedEntityType + entityId |
| Noise Level | Very responsive | Filtered by severity |
| User Actions | Auto-dismiss on page leave | Manual dismiss or mark read |

---

## API Endpoints

The `AlertsController` exposes REST endpoints for programmatic access to alert configuration and incident history.

**Controller Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\AlertsController.cs`

### GET /api/alerts/config

Retrieve all alert configurations.

**Authorization:** Requires `SuperAdmin` role

**Response: 200 OK**

```json
[
  {
    "id": 1,
    "metricName": "gateway_latency",
    "displayName": "Gateway Latency",
    "description": "Measures round-trip time to Discord gateway",
    "warningThreshold": 500.0,
    "criticalThreshold": 1000.0,
    "thresholdUnit": "ms",
    "isEnabled": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-15T12:30:00Z",
    "updatedBy": "admin-user-id"
  },
  {
    "id": 2,
    "metricName": "memory_usage",
    "displayName": "Memory Usage",
    "description": "Current process memory consumption",
    "warningThreshold": 400.0,
    "criticalThreshold": 512.0,
    "thresholdUnit": "MB",
    "isEnabled": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": null,
    "updatedBy": null
  }
]
```

### GET /api/alerts/config/{metricName}

Retrieve a specific alert configuration by metric name.

**Authorization:** Requires `SuperAdmin` role

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `metricName` | `string` | Internal metric identifier (e.g., "gateway_latency") |

**Response: 200 OK**

```json
{
  "id": 1,
  "metricName": "gateway_latency",
  "displayName": "Gateway Latency",
  "description": "Measures round-trip time to Discord gateway",
  "warningThreshold": 500.0,
  "criticalThreshold": 1000.0,
  "thresholdUnit": "ms",
  "isEnabled": true,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-15T12:30:00Z",
  "updatedBy": "admin-user-id"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Alert configuration not found",
  "detail": "No configuration exists for metric: gateway_latency",
  "statusCode": 404
}
```

### PUT /api/alerts/config/{metricName}

Update thresholds for a specific alert configuration.

**Authorization:** Requires `SuperAdmin` role

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `metricName` | `string` | Internal metric identifier |

**Request Body:**

```json
{
  "warningThreshold": 600.0,
  "criticalThreshold": 1200.0,
  "isEnabled": true
}
```

**Response: 200 OK**

Returns the updated configuration with `updatedAt` and `updatedBy` fields refreshed.

**Response: 400 Bad Request**

```json
{
  "message": "Invalid threshold values",
  "detail": "Critical threshold must be greater than warning threshold",
  "statusCode": 400
}
```

### GET /api/alerts/active

Get all currently active (unresolved) incidents.

**Authorization:** Requires `Admin` role

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `severity` | `AlertSeverity?` | Filter by severity (Info=0, Warning=1, Critical=2) |

**Response: 200 OK**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "metricName": "command_p95_latency",
    "displayName": "Command P95 Latency",
    "severity": 2,
    "severityName": "Critical",
    "status": 0,
    "statusName": "Active",
    "triggeredAt": "2025-01-28T10:15:30Z",
    "resolvedAt": null,
    "thresholdValue": 1500.0,
    "actualValue": 2100.0,
    "message": "Command P95 Latency exceeded critical threshold: 2100ms > 1500ms",
    "isAcknowledged": false,
    "acknowledgedBy": null,
    "acknowledgedAt": null,
    "notes": null
  }
]
```

### GET /api/alerts/incidents

Query incident history with filtering and pagination.

**Authorization:** Requires `Admin` role

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `metricName` | `string?` | Filter by metric |
| `severity` | `AlertSeverity?` | Filter by severity |
| `status` | `IncidentStatus?` | Filter by status (Active=0, Resolved=1, Acknowledged=2) |
| `isAcknowledged` | `bool?` | Filter by acknowledgment status |
| `startDate` | `DateTime?` | Filter incidents from this date (UTC) |
| `endDate` | `DateTime?` | Filter incidents up to this date (UTC) |
| `page` | `int` | Page number (default: 1) |
| `pageSize` | `int` | Items per page (default: 20, max: 100) |

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "metricName": "gateway_latency",
      "displayName": "Gateway Latency",
      "severity": 1,
      "severityName": "Warning",
      "status": 1,
      "statusName": "Resolved",
      "triggeredAt": "2025-01-28T09:30:00Z",
      "resolvedAt": "2025-01-28T09:35:15Z",
      "durationSeconds": 315,
      "thresholdValue": 500.0,
      "actualValue": 650.0,
      "message": "Gateway Latency exceeded warning threshold: 650ms > 500ms",
      "isAcknowledged": true,
      "acknowledgedBy": "admin-user-id",
      "acknowledgedAt": "2025-01-28T09:35:16Z",
      "notes": "Network blip, resolved automatically"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### GET /api/alerts/incidents/{id}

Retrieve a single incident by ID.

**Authorization:** Requires `Admin` role

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `Guid` | Incident unique identifier |

**Response: 200 OK**

Returns full incident details (same structure as above).

**Response: 404 Not Found**

```json
{
  "message": "Incident not found",
  "detail": "No incident found with ID: 550e8400-e29b-41d4-a716-446655440000",
  "statusCode": 404
}
```

### POST /api/alerts/incidents/{id}/acknowledge

Acknowledge an active incident (mark as reviewed by admin).

**Authorization:** Requires `Admin` role

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `Guid` | Incident unique identifier |

**Request Body:**

```json
{
  "notes": "Investigated: caused by traffic spike from feature launch. Scaled up container, resolved."
}
```

**Response: 200 OK**

Returns updated incident with `isAcknowledged` set to true, `status` changed to `Acknowledged`, and timestamps populated.

**Response: 404 Not Found**

If incident doesn't exist.

**Response: 400 Bad Request**

```json
{
  "message": "Invalid state transition",
  "detail": "Cannot acknowledge a resolved incident",
  "statusCode": 400
}
```

### POST /api/alerts/incidents/acknowledge-all

Acknowledge all active incidents at once.

**Authorization:** Requires `Admin` role

**Request Body:**

```json
{
  "notes": "Batch acknowledgment: all issues under investigation"
}
```

**Response: 200 OK**

```json
{
  "acknowledgedCount": 5,
  "message": "Successfully acknowledged 5 incidents"
}
```

### GET /api/alerts/summary

Get count summary of active incidents by severity.

**Authorization:** Requires `Admin` role

**Response: 200 OK**

```json
{
  "totalActive": 3,
  "criticalCount": 1,
  "warningCount": 2,
  "infoCount": 0,
  "acknowledgedCount": 1,
  "unacknowledgedCount": 2
}
```

### GET /api/alerts/frequency

Get daily incident frequency over the last 30 days.

**Authorization:** Requires `Admin` role

**Response: 200 OK**

```json
[
  {
    "date": "2025-01-28",
    "count": 5
  },
  {
    "date": "2025-01-27",
    "count": 2
  },
  {
    "date": "2025-01-26",
    "count": 0
  }
]
```

Used for dashboard charts showing incident trends.

### GET /api/alerts/auto-recovery

Get recent auto-recovery events.

**Authorization:** Requires `Admin` role

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `days` | `int` | How many days back to query (default: 7) |
| `limit` | `int` | Max results (default: 20) |

**Response: 200 OK**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440001",
    "metricName": "memory_usage",
    "displayName": "Memory Usage",
    "severity": 2,
    "triggeredAt": "2025-01-28T08:15:00Z",
    "resolvedAt": "2025-01-28T08:22:45Z",
    "durationSeconds": 465,
    "thresholdValue": 512.0,
    "peakActualValue": 545.0,
    "recoveredValue": 380.0
  }
]
```

---

## Database Schema

### PerformanceAlertConfigs Table

Stores threshold configuration for each monitored metric.

| Column | Type | Nullable | Key | Description |
|--------|------|----------|-----|-------------|
| `Id` | `int` | No | PK | Primary key, identity |
| `MetricName` | `nvarchar(100)` | No | UX | Unique metric identifier |
| `DisplayName` | `nvarchar(200)` | No | | User-friendly name |
| `Description` | `nvarchar(max)` | Yes | | What the metric measures |
| `WarningThreshold` | `float` | Yes | | Warning threshold value |
| `CriticalThreshold` | `float` | Yes | | Critical threshold value |
| `ThresholdUnit` | `nvarchar(50)` | No | | Unit (ms, %, MB, count, etc.) |
| `IsEnabled` | `bit` | No | | Is monitoring enabled |
| `CreatedAt` | `datetime2` | No | | UTC creation timestamp |
| `UpdatedAt` | `datetime2` | Yes | | UTC last update timestamp |
| `UpdatedBy` | `nvarchar(450)` | Yes | FK | User ID of last updater |

**Indexes:**
- `UX_PerformanceAlertConfigs_MetricName` - Unique index on MetricName for fast lookup

**Sample Data:**

```sql
INSERT INTO PerformanceAlertConfigs
(MetricName, DisplayName, Description, WarningThreshold, CriticalThreshold,
 ThresholdUnit, IsEnabled, CreatedAt)
VALUES
('gateway_latency', 'Gateway Latency', 'Discord gateway round-trip latency', 500.0, 1000.0, 'ms', 1, GETUTCDATE()),
('command_p95_latency', 'Command P95 Latency', '95th percentile command response time', 1000.0, 1500.0, 'ms', 1, GETUTCDATE()),
('memory_usage', 'Memory Usage', 'Current process memory consumption', 400.0, 512.0, 'MB', 1, GETUTCDATE());
```

### PerformanceIncidents Table

Stores incident history (triggered threshold breaches).

| Column | Type | Nullable | Key | Description |
|--------|------|----------|-----|-------------|
| `Id` | `uniqueidentifier` | No | PK | Primary key, NEWID() |
| `MetricName` | `nvarchar(100)` | No | FK | References PerformanceAlertConfigs.MetricName |
| `Severity` | `int` | No | | AlertSeverity enum (0=Info, 1=Warning, 2=Critical) |
| `Status` | `int` | No | IX | IncidentStatus enum (0=Active, 1=Resolved, 2=Acknowledged) |
| `TriggeredAt` | `datetime2` | No | IX | UTC timestamp when triggered |
| `ResolvedAt` | `datetime2` | Yes | | UTC timestamp when resolved (auto or manual) |
| `ThresholdValue` | `float` | No | | Configured threshold at trigger time |
| `ActualValue` | `float` | No | | Actual metric value that triggered incident |
| `Message` | `nvarchar(max)` | No | | Human-readable description |
| `IsAcknowledged` | `bit` | No | | Has admin reviewed this |
| `AcknowledgedBy` | `nvarchar(450)` | Yes | FK | User ID of acknowledger |
| `AcknowledgedAt` | `datetime2` | Yes | | UTC acknowledgment timestamp |
| `Notes` | `nvarchar(max)` | Yes | | Admin notes/findings |

**Indexes:**
- `IX_PerformanceIncidents_Status` - For active incident queries
- `IX_PerformanceIncidents_MetricName_Status` - For metric-specific active incidents
- `IX_PerformanceIncidents_TriggeredAt` - For date range queries and cleanup
- `IX_PerformanceIncidents_IsAcknowledged` - For filtering acknowledged incidents

**Cleanup Strategy:**
- Background cleanup task runs daily
- Deletes Resolved incidents older than `IncidentRetentionDays` (default: 90 days)
- Executes in batches to avoid long transactions
- Preserves Active and Acknowledged incidents regardless of age

**Sample Query (Active Incidents):**

```sql
SELECT * FROM PerformanceIncidents
WHERE Status = 0  -- Active
ORDER BY TriggeredAt DESC;
```

**Sample Query (Recent Resolutions):**

```sql
SELECT * FROM PerformanceIncidents
WHERE Status = 1  -- Resolved
AND ResolvedAt >= DATEADD(DAY, -7, GETUTCDATE())
ORDER BY ResolvedAt DESC;
```

---

## UI Components

### Alerts Page

**Route:** `/Admin/Performance/Alerts`
**Authorization:** Requires `Admin` or `SuperAdmin` role
**Page Model:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Admin\Performance\Alerts.cshtml.cs`
**View:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Admin\Performance\Alerts.cshtml`

#### Features

- **Tabs**: Active Incidents, All Incidents, Configuration
- **Real-Time Updates**: SignalR integration updates incident list and badge count
- **Filtering**: By metric, severity, status, date range
- **Incident Actions**:
  - View full details
  - Acknowledge individual incidents
  - Acknowledge all active incidents at once
- **Incident Details Modal**: Shows full context including duration, threshold comparison, and admin notes
- **Badge Count**: Shows active incident count in navbar (updated in real-time via SignalR)

#### Active Incidents Tab

Displays currently unresolved incidents with immediate actions:

```
┌────────────────────────────────────────────────┐
│ Active Incidents (3)                    Refresh │
├────────────────────────────────────────────────┤
│ [CRITICAL] Memory Usage                       │
│ Triggered: 2 minutes ago                       │
│ Value: 545 MB > 512 MB (Critical)             │
│ [Acknowledge] [View Details]                   │
│                                                 │
│ [WARNING] Gateway Latency                      │
│ Triggered: 5 minutes ago                       │
│ Value: 720 ms > 500 ms (Warning)              │
│ [Acknowledge] [View Details]                   │
│                                                 │
│ [WARNING] Command P95 Latency                  │
│ Triggered: 12 minutes ago                      │
│ Value: 1250 ms > 1000 ms (Warning)            │
│ [Acknowledge] [View Details]                   │
└────────────────────────────────────────────────┘
```

#### All Incidents Tab

Historical incident browser with pagination:

- **Default View**: Last 30 days
- **Filters**: Metric, severity, status, date range
- **Sorting**: By triggered date (newest first)
- **Pagination**: 20 incidents per page
- **Export**: Download filtered results as CSV

#### Configuration Tab

Threshold management interface:

```
┌─────────────────────────────────────────────────┐
│ Alert Thresholds                                │
├─────────────────────────────────────────────────┤
│ Metric: Gateway Latency                        │
│ Description: Discord gateway round-trip time   │
│                                                 │
│ ☑ Enable Alerts                                 │
│                                                 │
│ Warning Threshold: 500 [ms] ───────────────    │
│ Critical Threshold: 1000 [ms] ──────────────    │
│                                                 │
│                          [Save] [Cancel]       │
│                                                 │
│ Last Updated: 2025-01-15 by admin@example.com │
├─────────────────────────────────────────────────┤
│ Metric: Memory Usage                           │
│ ... (similar layout for each metric)           │
└─────────────────────────────────────────────────┘
```

### Real-Time JavaScript Integration

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\wwwroot\js\performance\alerts-realtime.js`

```javascript
// Connect to DashboardHub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/dashboard")
    .withAutomaticReconnect()
    .build();

// Listen for new incidents
connection.on("OnAlertTriggered", (incident) => {
    // Add to incident list at top
    addIncidentToUI(incident);

    // Update badge count
    updateActiveAlertBadge();

    // Show toast notification
    showToastAlert(incident);

    // Play notification sound (if enabled)
    if (userPreferences.soundEnabled) {
        playAlertSound();
    }
});

// Listen for resolutions
connection.on("OnAlertResolved", (incident) => {
    // Move incident from active to historical
    removeFromActiveList(incident.id);

    // Update badge
    updateActiveAlertBadge();

    // Show resolution toast
    showResolvedToast(incident);
});

// Listen for acknowledgments
connection.on("OnAlertAcknowledged", (incident) => {
    // Update UI with ack info
    updateIncidentUI(incident);
});

// Listen for badge count changes
connection.on("OnActiveAlertCountChanged", (count) => {
    updateBadgeNumber(count);
});

await connection.start();
```

---

## Thresholds Management

### Default Thresholds

Alert configurations come pre-seeded during database initialization:

| Metric | Warning | Critical | Unit | Rationale |
|--------|---------|----------|------|-----------|
| gateway_latency | 500 | 1000 | ms | Discord gateway > 500ms indicates network/infrastructure issues |
| command_p95_latency | 1000 | 1500 | ms | User-facing latency >1s hurts user experience |
| error_rate | 3 | 5 | % | 3-5% error rate indicates code/infrastructure problems |
| memory_usage | 400 | 512 | MB | High memory can trigger OOM or performance degradation |
| api_rate_limit_usage | 3000 | 3600 | count | 3000+ out of 3600 requests means near rate limit |
| database_query_time | 50 | 100 | ms | Slow DB queries block other operations |
| bot_disconnected | 1.0 | 1.0 | (binary) | Any disconnection is Critical |
| service_failure | 1.0 | 1.0 | (binary) | Any service failure is Critical |

### Adjusting Thresholds at Runtime

Admins can modify thresholds via the web UI without restarting the application:

1. Navigate to `/Admin/Performance/Alerts` → Configuration tab
2. Adjust Warning/Critical threshold values
3. Click Save
4. Changes take effect immediately

### Validation Rules

When updating thresholds:

- Warning threshold must be less than Critical threshold
- Thresholds must be positive numbers
- Unit must match original configuration

Violation returns HTTP 400 with validation error message.

---

## Auto-Recovery

The alerting system implements intelligent auto-recovery to reduce false positives and alert fatigue.

### Recovery Mechanism

```
Incident Active
    ↓
Monitor consecutive "normal" readings (metric within thresholds)
    ↓
Count reaches ConsecutiveNormalRequired (default: 3)
    ↓
Mark incident as Resolved
    ├─ Set ResolvedAt = now
    ├─ Set Status = Resolved
    ├─ Reset normal counter
    └─ Reset breach counter
    ↓
Broadcast resolution via SignalR
    ↓
Create in-app notification (if Critical severity)
```

### Example Timeline

**CheckIntervalSeconds = 30, ConsecutiveNormalRequired = 3:**

```
T+0s:   Metric value: 1100 (exceeds critical 1000) → BREACH #1
T+30s:  Metric value: 950 (exceeds warning 500) → BREACH #2 → Incident created
T+60s:  Metric value: 480 (normal) → NORMAL #1
T+90s:  Metric value: 450 (normal) → NORMAL #2
T+120s: Metric value: 400 (normal) → NORMAL #3 → Incident auto-resolved

Incident Duration: 120 seconds from creation to resolution
```

### Disabling Auto-Recovery

To prevent auto-recovery and require manual resolution:

1. Don't set any normal threshold (not yet implemented)
2. Or manually set `ConsecutiveNormalRequired` to very high value (e.g., 999999)

This is useful for critical issues that should always require admin review before clearing.

---

## Key Architectural Decisions

### 1. Lazy Service Resolution in AlertMonitoringService

**Decision**: Use `IServiceScopeFactory` to resolve scoped services (repository, notification service) inside the monitoring loop.

**Rationale**:
- Monitoring service is a singleton (long-lived background task)
- Repository and notification service are scoped
- Cannot inject scoped services into singleton directly (would create circular dependency)
- Creating a new scope each check ensures clean DbContext and proper DI lifetime

**Code Pattern**:
```csharp
using var scope = _scopeFactory.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<IPerformanceAlertRepository>();
var incident = await repository.GetActiveIncidentAsync(metricName);
```

**Related Issue**: [#570](https://github.com/cpike5/discordbot/issues/570) - Dependency Injection Patterns

### 2. Fire-and-Forget Notifications

**Decision**: Notification creation failures don't block incident creation.

**Rationale**:
- Alerting and notification are separate concerns
- Notification infrastructure might be slower or temporarily unavailable
- Incident data must be persisted regardless of notification delivery
- Admin can view incidents via web UI even if notifications fail

**Code Pattern**:
```csharp
_ = Task.Run(async () => {
    try {
        await notificationService.CreateForAllAdminsAsync(...);
    }
    catch (Exception ex) {
        _logger.LogWarning(ex, "Failed to notify, continuing");
    }
}, cancellationToken);
```

### 3. In-Memory Breach/Normal Counters

**Decision**: Track consecutive breaches/normal readings in memory using `ConcurrentDictionary`.

**Rationale**:
- Prevents database writes for every metric check
- Counters reset when application restarts (acceptable - transient state)
- Fast O(1) lookups and updates
- No coordination needed between multiple instances (each runs independently)

**Trade-off**: Scaled/clustered deployments will have independent counters per instance. To synchronize across instances, would need to persist counters to database (future enhancement).

### 4. Selective Notification Filtering

**Decision**: Only Warning+ severity creates notifications on incident creation; only Critical creates notifications on resolution.

**Rationale**:
- Info incidents are internal/debug-level
- Warning incidents warrant admin attention
- Critical resolutions are important to broadcast (critical issue is fixed)
- Warning resolutions are less important (just a metric returning to normal)
- Reduces notification volume and prevents alert fatigue

### 5. Severity-Based Alert Filtering

**Decision**: Severity levels (Info, Warning, Critical) determine notification behavior, not incident type.

**Rationale**:
- Single unified severity scale across all metrics
- Easy to adjust notification behavior globally
- Allows metrics to be configured at different severity levels as understanding improves
- Admin can configure which severity levels trigger notifications

---

## Integration Patterns

### Adding a New Metric to Monitor

To add a new metric to the alerting system:

1. **Create or use existing metric source** (e.g., `ICommandPerformanceAggregator`, `ILatencyHistoryService`)
2. **Add configuration row** to `PerformanceAlertConfigs` seeding in migration
3. **Add monitoring code** in `AlertMonitoringService.MonitorMetricsAsync()`:

```csharp
// In AlertMonitoringService.cs
private async Task MonitorMyNewMetricAsync(CancellationToken cancellationToken)
{
    try {
        var config = await _repository.GetByMetricNameAsync("my_new_metric", cancellationToken);
        if (config == null || !config.IsEnabled) return;

        // Get current metric value from your service
        var currentValue = await _myMetricService.GetCurrentValueAsync(cancellationToken);

        // Check against thresholds
        if (currentValue >= config.CriticalThreshold) {
            await EvaluateThresholdAsync(config, AlertSeverity.Critical, currentValue,
                cancellationToken);
        }
        else if (currentValue >= config.WarningThreshold) {
            await EvaluateThresholdAsync(config, AlertSeverity.Warning, currentValue,
                cancellationToken);
        }
        else {
            // Normal reading - may trigger auto-recovery
            await EvaluateNormalReadingAsync(config, cancellationToken);
        }
    }
    catch (Exception ex) {
        _logger.LogError(ex, "Error monitoring my_new_metric");
    }
}
```

4. **Register dependency injection** in `Program.cs` if needed
5. **Update UI** if new metric needs special handling (usually not needed)
6. **Test** with unit tests and manual testing

### Consuming Alerts from External Systems

Use the REST API endpoints to integrate alerts with external systems:

**Option 1: Polling**
```csharp
var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new("Bearer", token);

// Every 5 minutes
var response = await client.GetAsync("/api/alerts/summary");
var summary = await response.Content.ReadAsAsync<AlertSummaryDto>();

if (summary.TotalActive > 0) {
    // Send to Slack, PagerDuty, etc.
    await SendToExternalSystem(summary);
}
```

**Option 2: Event Streaming**
```csharp
// Connect to SignalR hub from external service
var connection = new HubConnectionBuilder()
    .WithUrl("https://botserver/hubs/dashboard")
    .Build();

connection.On<PerformanceIncidentDto>("OnAlertTriggered", async incident => {
    await SendToExternalSystem(incident);
});

await connection.StartAsync();
```

---

## Testing Strategy

The alerting system includes comprehensive test coverage for all components.

### Test Categories

| Component | Test Scenarios | Location |
|-----------|---|----------|
| `AlertMonitoringService` | Metric evaluation, consecutive counters, incident creation, auto-recovery, notification firing | `Tests/DiscordBot.Tests/Services/AlertMonitoringServiceTests.cs` |
| `PerformanceAlertService` | CRUD operations, filtering, pagination, acknowledgment | `Tests/DiscordBot.Tests/Services/PerformanceAlertServiceTests.cs` |
| `PerformanceAlertRepository` | Database queries, threshold comparisons, cleanup | `Tests/DiscordBot.Tests/Infrastructure/Repositories/PerformanceAlertRepositoryTests.cs` |
| `AlertsController` | API responses, validation, authorization | `Tests/DiscordBot.Tests/Controllers/AlertsControllerTests.cs` |

### Key Test Scenarios

**Consecutive Breaches:**
```csharp
[Fact]
public async Task MonitorMetrics_IncidentCreatedAfterConsecutiveBreaches()
{
    // Arrange: Set ConsecutiveBreachesRequired = 2
    var config = CreateAlertConfig(warningThreshold: 100);
    var firstMetric = 150;
    var secondMetric = 160;

    // Act: Simulate two consecutive metric checks exceeding threshold
    await service.MonitorMetricsAsync(CancellationToken.None);
    // ... mock first metric value
    await service.MonitorMetricsAsync(CancellationToken.None);
    // ... mock second metric value

    // Assert: Incident created after second breach
    var incidents = await repository.GetActiveIncidentsAsync();
    Assert.Single(incidents);
    Assert.Equal(AlertSeverity.Warning, incidents[0].Severity);
}
```

**Auto-Recovery:**
```csharp
[Fact]
public async Task MonitorMetrics_IncidentAutoResolves_AfterConsecutiveNormalReadings()
{
    // Arrange: Create active incident, set ConsecutiveNormalRequired = 3
    var incident = await repository.CreateIncidentAsync(...);

    // Act: Simulate three consecutive normal readings
    for (int i = 0; i < 3; i++) {
        await service.MonitorMetricsAsync(CancellationToken.None);
        // ... mock normal metric value
    }

    // Assert: Incident resolved
    var resolved = await repository.GetByIdAsync(incident.Id);
    Assert.Equal(IncidentStatus.Resolved, resolved.Status);
    Assert.NotNull(resolved.ResolvedAt);
}
```

**Threshold Validation:**
```csharp
[Fact]
public async Task UpdateConfiguration_RejectsCriticalLessThanWarning()
{
    // Arrange
    var config = CreateAlertConfig(
        warningThreshold: 500,
        criticalThreshold: 100);

    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(async () =>
        await service.UpdateConfigurationAsync(config));
}
```

### Edge Cases Covered

- **Simultaneous incidents**: Multiple metrics breaching simultaneously
- **Metric oscillation**: Metric bounces between normal and breach (tests counter behavior)
- **Disabled alerts**: Verify disabled metrics are skipped
- **Missing configurations**: Handle gracefully when config is deleted
- **Database errors**: Retry logic and error logging
- **Notification failures**: Incident persists even if notification fails

---

## Troubleshooting

### Issue: Alerts not triggering despite metric exceeding threshold

**Possible Causes:**
1. Alert configuration is disabled (`IsEnabled = false`)
2. Threshold values are misconfigured (critical < warning)
3. AlertMonitoringService background service not running
4. Metric name mismatch between monitor and config

**Solutions:**
- Check `/Admin/Performance/Alerts` → Configuration tab
- Verify thresholds are correct: Warning < Critical
- Review application logs for `AlertMonitoringService` startup
- Confirm metric name matches exactly (case-sensitive)

### Issue: False positive alerts (metric briefly spikes)

**Possible Causes:**
1. `ConsecutiveBreachesRequired` is too low
2. Threshold values are too strict

**Solutions:**
- Increase `ConsecutiveBreachesRequired` in appsettings (e.g., 2 → 3)
- Adjust threshold values upward via UI
- Monitor for 1-2 days to validate new settings

### Issue: Incidents stuck in "Active" status

**Possible Causes:**
1. Metric remains above threshold (legitimate issue)
2. `ConsecutiveNormalRequired` is too high
3. Auto-recovery logic disabled

**Solutions:**
- Check current metric value on dashboard
- Lower `ConsecutiveNormalRequired` in appsettings (e.g., 3 → 2)
- Manually acknowledge incident via UI if issue investigated

### Issue: No real-time updates on Alerts page

**Possible Causes:**
1. SignalR connection failed
2. User not logged in
3. User doesn't have Admin role

**Solutions:**
- Check browser console for SignalR errors
- Verify authentication via DevTools Network tab
- Check user has Admin or SuperAdmin role

### Issue: "Queue overflow" warning in logs

**Symptom:**
```
Failed to enqueue audit log entry. Queue may be closed.
```

**Cause**: Notification service queue is full (background processor can't keep up)

**Solutions:**
- Check database connectivity
- Review for slow queries blocking notification writes
- Increase notification queue capacity
- Reduce notification volume by adjusting severity filters

---

## Future Enhancements

Potential improvements to the alerting system:

### 1. **Cluster-Aware Metrics**

For clustered/scaled deployments, synchronize breach counters across instances via Redis or shared database table. Currently each instance maintains independent counters.

### 2. **Custom Metric Integration**

Allow third-party services to register custom metrics via plugin/extension system. Currently hardcoded metrics only.

### 3. **Alert Rules Engine**

Implement logic for composite alerts (e.g., "Critical only if both memory AND database latency high"). Currently alerts are independent per metric.

### 4. **Webhook Integration**

Send alert events to external systems (Slack, PagerDuty, Datadog, etc.) without polling.

### 5. **Machine Learning Anomaly Detection**

Use historical incident data to automatically adjust thresholds based on time-of-day, day-of-week, or detected patterns.

### 6. **Alert Suppression Windows**

Allow scheduling maintenance windows where alerts are suppressed (e.g., "7am-9am EST on Mondays for deploys").

### 7. **Escalation Policies**

Define escalation rules: if Critical alert unacknowledged for 5 minutes, page on-call engineer.

### 8. **Incident Correlation**

Group related incidents automatically (e.g., "high latency + high memory = resource contention").

### 9. **SLA Tracking**

Track MTTR (mean time to recovery) and MTTA (mean time to acknowledgment) for SLA reporting.

### 10. **Time-Series Storage**

Archive incident history to time-series database (InfluxDB, Prometheus) for long-term analysis.

---

## Related Documentation

- [Bot Performance Dashboard](bot-performance-dashboard.md) - Dashboard UI for performance monitoring
- [Notification System](notification-system.md) - In-app notification infrastructure
- [SignalR Real-Time Updates](signalr-realtime.md) - Real-time communication patterns
- [Audit Log System](audit-log-system.md) - Similar fluent builder and background queue patterns
- [Authorization Policies](authorization-policies.md) - Role-based access control for admin features
- [Database Schema](database-schema.md) - Complete database entity documentation
- [API Endpoints](api-endpoints.md) - Full REST API reference

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-01-28 | Initial documentation for v0.17.0-dev release |

---

**Document Owner:** System Architect
**Review Cycle:** Quarterly
**Next Review:** 2025-04-28
