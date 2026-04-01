# Audit Log System Documentation

**Version:** 1.0
**Last Updated:** 2025-12-30
**Related Issue:** [#311](https://github.com/cpike5/discordbot/issues/311) (Epic [#303](https://github.com/cpike5/discordbot/issues/303))

---

## Overview

The audit log system provides comprehensive tracking and monitoring of all significant actions performed within the Discord Bot Management System. It records user activities, bot operations, guild changes, configuration updates, and security events for compliance, debugging, and security analysis.

### Key Features

- **Comprehensive Event Tracking**: Logs user actions, bot operations, guild management, and system events
- **Fluent Builder API**: Type-safe, chainable interface for creating audit entries
- **High-Performance Queue**: Background processing using System.Threading.Channels for non-blocking logging
- **Correlation Tracking**: Groups related events using correlation IDs for operation tracing
- **Actor Attribution**: Tracks whether actions were performed by users, the system, or the bot
- **Automatic Cleanup**: Configurable retention policies with background cleanup service
- **Rich Filtering**: Search and filter by category, action, actor, guild, date range, and more
- **Admin UI**: Web-based viewer with filtering, pagination, details view, and CSV export
- **REST API**: Full programmatic access for integrations and analytics

---

## Audit Log Categories

Audit events are organized into seven distinct categories defined in the `AuditLogCategory` enum:

| Category | Value | Description | Example Events |
|----------|-------|-------------|----------------|
| `User` | 1 | User-related actions | Login, logout, profile updates, ban, kick |
| `Guild` | 2 | Guild-related actions | Guild settings changes, channel management, role changes |
| `Configuration` | 3 | Configuration-related actions | Bot settings, feature toggles, retention policies |
| `Security` | 4 | Security-related actions | Permission changes, role modifications, authentication |
| `Command` | 5 | Command execution actions | Slash command usage, command success/failure |
| `Message` | 6 | Message-related actions | Message deletion, editing, moderation |
| `System` | 7 | System-level actions | Bot startup, shutdown, errors, background tasks |

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Enums\AuditLogCategory.cs`

---

## Audit Log Actions

The `AuditLogAction` enum defines specific actions that can be performed within each category:

| Action | Value | Description | Common Categories |
|--------|-------|-------------|-------------------|
| `Created` | 1 | A new entity was created | User, Guild, Configuration |
| `Updated` | 2 | An existing entity was updated | User, Guild, Configuration |
| `Deleted` | 3 | An entity was deleted | User, Guild, Message |
| `Login` | 4 | A user logged in to the system | Security |
| `Logout` | 5 | A user logged out of the system | Security |
| `PermissionChanged` | 6 | Permissions were changed for a user or role | Security |
| `SettingChanged` | 7 | A configuration setting was changed | Configuration |
| `CommandExecuted` | 8 | A command was executed | Command |
| `MessageDeleted` | 9 | A message was deleted | Message |
| `MessageEdited` | 10 | A message was edited | Message |
| `UserBanned` | 11 | A user was banned from a guild | User, Security |
| `UserUnbanned` | 12 | A user was unbanned from a guild | User, Security |
| `UserKicked` | 13 | A user was kicked from a guild | User, Security |
| `RoleAssigned` | 14 | A role was assigned to a user | Security |
| `RoleRemoved` | 15 | A role was removed from a user | Security |
| `BotStarted` | 16 | The Discord bot has started | System |
| `BotStopped` | 17 | The Discord bot has stopped | System |
| `BotConnected` | 18 | The bot connected to Discord gateway | System |
| `BotDisconnected` | 19 | The bot disconnected from Discord gateway | System |

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Enums\AuditLogAction.cs`

### When Actions Are Triggered

| Action | Triggered By | Details Captured |
|--------|--------------|------------------|
| `Login` | User authentication (password or OAuth) | Email, method, IP address |
| `Logout` | User sign-out | N/A |
| `Created` | User creation, scheduled message creation, etc. | Entity-specific details (email, title, etc.) |
| `Updated` | Guild settings update, user profile update, etc. | Changed fields and values |
| `Deleted` | User deletion, message deletion, etc. | Entity identifier and type |
| `SettingChanged` | Settings page save | Setting key, old value, new value |
| `CommandExecuted` | Discord slash command execution | Command name, parameters, success/failure |
| `PermissionChanged` | Role assignment/removal | Role name, user affected |
| `BotStarted` | Bot hosted service startup | Bot version |
| `BotStopped` | Bot graceful shutdown | Reason |

---

## Actor Types

The `AuditLogActorType` enum identifies who or what performed an audited action:

| Actor Type | Value | Description | Actor ID |
|------------|-------|-------------|----------|
| `User` | 1 | Action performed by an authenticated user | ASP.NET Identity User ID (GUID) |
| `System` | 2 | Action performed by the system | `"System"` |
| `Bot` | 3 | Action performed by the Discord bot | `"Bot"` |

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Enums\AuditLogActorType.cs`

---

## Fluent Builder API

The audit log system provides a fluent builder pattern for creating audit entries with a readable, chainable API.

### Basic Usage

```csharp
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

// Inject IAuditLogService via dependency injection
private readonly IAuditLogService _auditLogService;

// Create an audit log entry
await _auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Security)
    .WithAction(AuditLogAction.Login)
    .ByUser(userId)
    .OnTarget("User", userId)
    .FromIpAddress(ipAddress)
    .WithDetails(new { email = user.Email, method = "Password" })
    .LogAsync();
```

### Builder Methods

The `IAuditLogBuilder` interface provides the following methods:

| Method | Parameters | Description | Returns |
|--------|------------|-------------|---------|
| `ForCategory()` | `AuditLogCategory category` | Sets the category of the audit log entry | `IAuditLogBuilder` |
| `WithAction()` | `AuditLogAction action` | Sets the action that was performed | `IAuditLogBuilder` |
| `ByUser()` | `string userId` | Sets the actor as a user with the specified ID | `IAuditLogBuilder` |
| `BySystem()` | None | Sets the actor as the system (automated process) | `IAuditLogBuilder` |
| `ByBot()` | None | Sets the actor as the Discord bot | `IAuditLogBuilder` |
| `OnTarget()` | `string targetType, string targetId` | Sets the target entity affected by the action | `IAuditLogBuilder` |
| `InGuild()` | `ulong guildId` | Sets the Discord guild associated with the action | `IAuditLogBuilder` |
| `WithDetails()` | `object details` | Sets additional contextual information (serialized to JSON) | `IAuditLogBuilder` |
| `FromIpAddress()` | `string ipAddress` | Sets the IP address from which the action was performed | `IAuditLogBuilder` |
| `WithCorrelationId()` | `string correlationId` | Sets a correlation ID to group related audit entries | `IAuditLogBuilder` |
| `LogAsync()` | `CancellationToken cancellationToken = default` | Logs the audit entry asynchronously and waits for confirmation | `Task` |
| `Enqueue()` | None | Enqueues the audit entry for background processing (fire-and-forget) | `void` |

**Interface Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IAuditLogBuilder.cs`

**Implementation Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\AuditLogBuilder.cs`

### Usage Examples

#### User Login

```csharp
var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Security)
    .WithAction(AuditLogAction.Login)
    .ByUser(user.Id)
    .OnTarget("User", user.Id)
    .FromIpAddress(ipAddress ?? "Unknown")
    .WithDetails(new { email = user.Email, method = "Password" })
    .Enqueue();
```

#### Guild Settings Update

```csharp
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Guild)
    .WithAction(AuditLogAction.Updated)
    .ByUser(userId)
    .OnTarget("Guild", guildId.ToString())
    .InGuild(guildId)
    .WithDetails(new
    {
        changedFields = new[] { "WelcomeChannelId", "WelcomeMessage" },
        oldValues = new { welcomeChannelId = oldChannelId, welcomeMessage = oldMessage },
        newValues = new { welcomeChannelId = newChannelId, welcomeMessage = newMessage }
    })
    .Enqueue();
```

#### Scheduled Message Creation

```csharp
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Configuration)
    .WithAction(AuditLogAction.Created)
    .ByUser(userId)
    .OnTarget("ScheduledMessage", messageId.ToString())
    .InGuild(guildId)
    .WithDetails(new
    {
        title = message.Title,
        channelId = message.ChannelId,
        cronExpression = message.CronExpression,
        timezone = message.Timezone
    })
    .Enqueue();
```

#### System Event (Bot Startup)

```csharp
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.System)
    .WithAction(AuditLogAction.BotStarted)
    .BySystem()
    .WithDetails(new { version = botVersion, environment = environmentName })
    .Enqueue();
```

#### Correlation Tracking (Multi-Step Operation)

```csharp
var correlationId = Guid.NewGuid().ToString();

// Step 1: Create user
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.User)
    .WithAction(AuditLogAction.Created)
    .ByUser(adminUserId)
    .OnTarget("User", newUserId)
    .WithCorrelationId(correlationId)
    .Enqueue();

// Step 2: Assign roles
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Security)
    .WithAction(AuditLogAction.RoleAssigned)
    .ByUser(adminUserId)
    .OnTarget("User", newUserId)
    .WithDetails(new { roles = new[] { "Viewer", "Moderator" } })
    .WithCorrelationId(correlationId)
    .Enqueue();
```

### LogAsync() vs Enqueue()

The builder provides two methods for submitting audit entries:

| Method | Behavior | Performance | Use Case |
|--------|----------|-------------|----------|
| `LogAsync()` | Enqueues to background queue and waits | Slightly slower (async overhead) | When you need to ensure log is queued before proceeding |
| `Enqueue()` | Fire-and-forget, returns immediately | Fastest (synchronous enqueue) | Most common case for non-critical logging |

**Recommendation:** Use `Enqueue()` for most audit logging scenarios. Use `LogAsync()` only when you need to verify the log was successfully queued before continuing execution.

---

## Background Queue Architecture

The audit log system uses a high-performance background queue to ensure logging operations never block request handling.

### Queue Implementation

- **Technology:** `System.Threading.Channels` with bounded channel
- **Capacity:** 10,000 entries
- **Backpressure Strategy:** `BoundedChannelFullMode.DropOldest` (drops oldest entries when full)
- **Concurrency:** Single reader (background processor), multiple writers (thread-safe)

**Queue Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\AuditLogQueue.cs`

### Queue Processor

The `AuditLogQueueProcessor` is a background service that continuously reads from the queue and writes to the database:

- **Processing:** Dequeues entries and persists to database via repository
- **Error Handling:** Retries failed writes, logs errors without crashing
- **Shutdown:** Gracefully processes remaining entries on application shutdown

**Processor Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\AuditLogQueueProcessor.cs`

### Performance Benefits

1. **Non-Blocking:** User requests complete immediately without waiting for database writes
2. **Batching Potential:** Queue processor can be extended to batch writes for higher throughput
3. **Fault Isolation:** Database errors during audit logging don't crash user requests
4. **Scalability:** Handles high-volume logging scenarios (10,000+ entries/minute)

---

## Configuration

Audit log retention and cleanup behavior is controlled by the `AuditLogRetentionOptions` class.

### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable or disable automatic cleanup |
| `RetentionDays` | `int` | `90` | Number of days to retain audit logs before cleanup |
| `CleanupBatchSize` | `int` | `1000` | Maximum number of records to delete in a single cleanup operation |
| `CleanupIntervalHours` | `int` | `24` | Interval (in hours) between automatic cleanup operations |

**Options Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Configuration\AuditLogRetentionOptions.cs`

### appsettings.json Configuration

```json
{
  "AuditLogRetention": {
    "Enabled": true,
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24
  },
  "BackgroundServices": {
    "AuditLogCleanupInitialDelayMinutes": 5
  }
}
```

### Runtime Configuration

The retention days setting can be overridden at runtime via the Settings page:

- **Setting Key:** `Advanced:AuditLogRetentionDays`
- **Page:** `/Admin/Settings` → Advanced tab
- **Type:** Integer (number input)
- **Validation:** Must be > 0
- **Restart Required:** No

When configured via the Settings UI, the database value takes precedence over `appsettings.json`.

### Retention Service

The `AuditLogRetentionService` is a background service that automatically cleans up old audit logs:

- **Startup Delay:** Waits 5 minutes after application start (configurable)
- **Cleanup Schedule:** Runs every 24 hours by default
- **Deletion Strategy:** Deletes logs older than `RetentionDays` in batches of `CleanupBatchSize`
- **Graceful Shutdown:** Stops cleanly when application shuts down

**Service Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\AuditLogRetentionService.cs`

---

## Database Schema

Audit logs are stored in the `AuditLogs` table with the following structure:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `bigint` (Int64) | No | Primary key, auto-increment |
| `Timestamp` | `datetime` | No | UTC timestamp when the action occurred |
| `Category` | `int` | No | `AuditLogCategory` enum value |
| `Action` | `int` | No | `AuditLogAction` enum value |
| `ActorId` | `nvarchar(450)` | Yes | Identifier of the actor (User ID, "System", "Bot") |
| `ActorType` | `int` | No | `AuditLogActorType` enum value |
| `TargetType` | `nvarchar(100)` | Yes | Type name of the affected entity (e.g., "User", "Guild") |
| `TargetId` | `nvarchar(450)` | Yes | Identifier of the affected entity |
| `GuildId` | `bigint` (ulong) | Yes | Discord guild ID associated with the action |
| `Details` | `nvarchar(max)` | Yes | JSON string containing action-specific details |
| `IpAddress` | `nvarchar(45)` | Yes | IP address from which the action was performed |
| `CorrelationId` | `nvarchar(100)` | Yes | Correlation ID to group related entries |

**Entity Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Entities\AuditLog.cs`

**Configuration Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Infrastructure\Data\Configurations\AuditLogConfiguration.cs`

### Indexes

The following indexes are recommended for optimal query performance:

- `IX_AuditLogs_Timestamp` - For date range queries
- `IX_AuditLogs_ActorId` - For filtering by actor
- `IX_AuditLogs_GuildId` - For guild-specific queries
- `IX_AuditLogs_Category_Action` - For category/action filtering
- `IX_AuditLogs_CorrelationId` - For correlation tracking

---

## Admin UI

The audit log viewer provides a web-based interface for browsing, searching, and exporting audit logs.

### Audit Logs Index Page

**Route:** `/Admin/AuditLogs`
**Authorization:** Requires `Admin` or `SuperAdmin` role
**Page Model:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Admin\AuditLogs\Index.cshtml.cs`

#### Features

- **Default Time Range:** Shows last 24 hours when no filters applied
- **Filtering:** Category, action, actor ID, target type, guild, date range, search term
- **Pagination:** 25 entries per page (configurable)
- **Sorting:** By timestamp (newest first by default)
- **CSV Export:** Download filtered results to CSV file
- **Timezone Support:** Converts timestamps to user's local timezone
- **Guild Dropdown:** Select from connected guilds for filtering

#### Filter Options

| Filter | Type | Description |
|--------|------|-------------|
| Category | Dropdown | Filter by audit log category (User, Guild, Security, etc.) |
| Action | Dropdown | Filter by specific action (Created, Updated, Login, etc.) |
| Actor ID | Text | Filter by actor identifier (user ID, "System", "Bot") |
| Target Type | Text | Filter by target entity type (e.g., "User", "ScheduledMessage") |
| Guild | Dropdown | Filter by Discord guild |
| Start Date | Date Picker | Filter logs from this date forward |
| End Date | Date Picker | Filter logs up to this date |
| Search Term | Text | Search in details JSON (case-insensitive) |

#### CSV Export

Click the "Export to CSV" button to download all matching audit logs:

- **File Name Format:** `audit-logs-yyyyMMdd-HHmmss.csv`
- **Columns:** Timestamp, Category, Action, Actor, Target Type, Target ID, Guild, Details, IP Address, Correlation ID
- **Row Limit:** No limit (exports all matching records)
- **CSV Escaping:** Properly escapes quotes and special characters

### Audit Log Details Page

**Route:** `/Admin/AuditLogs/Details/{id}`
**Authorization:** Requires `Admin` or `SuperAdmin` role
**Page Model:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Admin\AuditLogs\Details.cshtml.cs`

#### Features

- **Full Entry Details:** All fields including actor, target, timestamp, IP address
- **Formatted JSON Viewer:** Pretty-printed details JSON with syntax highlighting
- **Related Entries:** Shows all audit logs with the same correlation ID
- **Back Navigation:** Returns to index page with filters preserved

#### Related Entries

If the audit log has a `CorrelationId`, the details page displays all related entries:

- **Ordering:** By timestamp (chronological order)
- **Display:** Entry preview with category, action, actor, and timestamp
- **Navigation:** Click to view details of related entry

### Dashboard Widget

The dashboard (`/`) displays recent audit logs for Admin and SuperAdmin users:

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Index.cshtml.cs` (lines 88-101)

- **Display:** Last 5 audit log entries
- **Access:** Only visible to users with Admin or SuperAdmin role
- **Card Component:** `AuditLogCardViewModel`
- **Link:** "View All" button navigates to `/Admin/AuditLogs`

---

## API Endpoints

The audit log system exposes REST API endpoints for programmatic access.

### GET /api/auditlogs

Retrieves audit logs with optional filtering and pagination.

**Authorization:** Requires `SuperAdmin` role

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Category` | `AuditLogCategory?` | No | Filter by category |
| `Action` | `AuditLogAction?` | No | Filter by action |
| `ActorId` | `string?` | No | Filter by actor ID |
| `TargetType` | `string?` | No | Filter by target type |
| `GuildId` | `ulong?` | No | Filter by guild ID |
| `StartDate` | `DateTime?` | No | Filter logs from this date (UTC) |
| `EndDate` | `DateTime?` | No | Filter logs up to this date (UTC) |
| `SearchTerm` | `string?` | No | Search in details JSON |
| `Page` | `int` | No | Page number (default: 1) |
| `PageSize` | `int` | No | Items per page (default: 20, max: 100) |

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": 12345,
      "timestamp": "2025-12-30T14:23:45Z",
      "category": 4,
      "categoryName": "Security",
      "action": 4,
      "actionName": "Login",
      "actorId": "abc123-def456-...",
      "actorType": 1,
      "actorTypeName": "User",
      "actorDisplayName": "john.doe@example.com",
      "targetType": "User",
      "targetId": "abc123-def456-...",
      "guildId": null,
      "guildName": null,
      "details": "{\"email\":\"john.doe@example.com\",\"method\":\"Password\"}",
      "ipAddress": "192.168.1.100",
      "correlationId": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1523,
  "totalPages": 77,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

**Response: 400 Bad Request** (invalid date range)

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123..."
}
```

### GET /api/auditlogs/{id}

Retrieves a single audit log entry by its identifier.

**Authorization:** Requires `SuperAdmin` role

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `long` | The audit log entry ID |

**Response: 200 OK**

```json
{
  "id": 12345,
  "timestamp": "2025-12-30T14:23:45Z",
  "category": 4,
  "categoryName": "Security",
  "action": 4,
  "actionName": "Login",
  "actorId": "abc123-def456-...",
  "actorType": 1,
  "actorTypeName": "User",
  "actorDisplayName": "john.doe@example.com",
  "targetType": "User",
  "targetId": "abc123-def456-...",
  "guildId": null,
  "guildName": null,
  "details": "{\"email\":\"john.doe@example.com\",\"method\":\"Password\"}",
  "ipAddress": "192.168.1.100",
  "correlationId": null
}
```

**Response: 404 Not Found**

```json
{
  "message": "Audit log not found",
  "detail": "No audit log entry with ID 12345 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123..."
}
```

### GET /api/auditlogs/stats

Retrieves comprehensive audit log statistics.

**Authorization:** Requires `SuperAdmin` role

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `guildId` | `ulong?` | No | Filter statistics by guild ID (omit for global stats) |

**Response: 200 OK**

```json
{
  "totalEntries": 15234,
  "last24Hours": 145,
  "last7Days": 892,
  "last30Days": 3421,
  "categoryCounts": {
    "User": 3420,
    "Guild": 2145,
    "Configuration": 823,
    "Security": 4521,
    "Command": 3892,
    "Message": 423,
    "System": 10
  },
  "actionCounts": {
    "Created": 1234,
    "Updated": 2345,
    "Deleted": 234,
    "Login": 3456,
    "Logout": 3210,
    "CommandExecuted": 3892,
    "SettingChanged": 823
  },
  "topActors": [
    {
      "actorId": "abc123-def456-...",
      "actorDisplayName": "admin@example.com",
      "count": 523
    },
    {
      "actorId": "System",
      "actorDisplayName": "System",
      "count": 412
    }
  ]
}
```

### GET /api/auditlogs/by-correlation/{correlationId}

Retrieves all audit log entries related by correlation ID.

**Authorization:** Requires `SuperAdmin` role

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `correlationId` | `string` | The correlation ID to search for |

**Response: 200 OK**

```json
[
  {
    "id": 12345,
    "timestamp": "2025-12-30T14:23:45Z",
    "category": 1,
    "categoryName": "User",
    "action": 1,
    "actionName": "Created",
    "correlationId": "abc123-xyz789"
  },
  {
    "id": 12346,
    "timestamp": "2025-12-30T14:23:46Z",
    "category": 4,
    "categoryName": "Security",
    "action": 14,
    "actionName": "RoleAssigned",
    "correlationId": "abc123-xyz789"
  }
]
```

**Controller Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\AuditLogsController.cs`

---

## Service Interface

The `IAuditLogService` interface provides programmatic access to audit log operations.

### Service Methods

```csharp
public interface IAuditLogService
{
    // Query audit logs with filtering and pagination
    Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> GetLogsAsync(
        AuditLogQueryDto query,
        CancellationToken cancellationToken = default);

    // Retrieve a single audit log entry by ID
    Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    // Retrieve all audit logs with a specific correlation ID
    Task<IReadOnlyList<AuditLogDto>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    // Retrieve statistical information about audit logs
    Task<AuditLogStatsDto> GetStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    // Log an audit entry asynchronously (enqueues to background queue)
    Task LogAsync(AuditLogCreateDto dto, CancellationToken cancellationToken = default);

    // Create a fluent builder for constructing audit entries
    IAuditLogBuilder CreateBuilder();
}
```

**Service Interface:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IAuditLogService.cs`

**Service Implementation:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\AuditLogService.cs`

### Dependency Injection

The audit log service is registered in `Program.cs` as a scoped service:

```csharp
// Register audit log services
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddSingleton<IAuditLogQueue, AuditLogQueue>();
builder.Services.AddHostedService<AuditLogQueueProcessor>();
builder.Services.AddHostedService<AuditLogRetentionService>();
```

---

## Testing Strategy

The audit log system includes comprehensive unit tests covering all components.

### Test Coverage

| Component | Test File | Key Test Scenarios |
|-----------|-----------|-------------------|
| `AuditLogService` | `AuditLogServiceTests.cs` | Query filtering, pagination, stats, correlation tracking |
| `AuditLogBuilder` | `AuditLogBuilderTests.cs` | Fluent API, method chaining, DTO construction |
| `AuditLogQueue` | `AuditLogQueueTests.cs` | Enqueue/dequeue, capacity limits, thread safety |
| `AuditLogQueueProcessor` | `AuditLogQueueProcessorTests.cs` | Background processing, error handling, shutdown |
| `AuditLogRetentionService` | `AuditLogRetentionServiceTests.cs` | Cleanup scheduling, retention logic, settings integration |
| `AuditLogRepository` | `AuditLogRepositoryTests.cs` | Database queries, filtering, deletion |
| `AuditLogsController` | `AuditLogsControllerTests.cs` | API endpoints, validation, authorization |
| `AuditLogCardViewModel` | `AuditLogCardViewModelTests.cs` | Dashboard widget mapping |

**Test Location:** `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\`

### Edge Cases Tested

- **High-Volume Logging:** Queue handles 10,000+ entries without blocking
- **Queue Overflow:** Oldest entries dropped when queue reaches capacity
- **Database Failures:** Queue processor retries failed writes
- **Graceful Shutdown:** Remaining queue entries processed on app shutdown
- **Invalid Filters:** API validates date ranges and parameter limits
- **Missing Actors:** Actor display names gracefully handle deleted users
- **Correlation Tracking:** Related entries correctly grouped and ordered

---

## Security Considerations

### Data Sensitivity

Audit logs may contain sensitive information. Follow these guidelines:

1. **PII in Details:** Avoid logging passwords, tokens, or sensitive user data in the `Details` field
2. **IP Addresses:** IP addresses are logged for security events only (login, permission changes)
3. **Access Control:** Audit logs are restricted to Admin and SuperAdmin roles
4. **API Authorization:** API endpoints require SuperAdmin role

### Retention and Compliance

- **Default Retention:** 90 days
- **Compliance:** Adjust retention period based on regulatory requirements (GDPR, HIPAA, etc.)
- **Data Deletion:** Audit logs are permanently deleted after retention period
- **Export:** Use CSV export for long-term archival before cleanup

### Tampering Prevention

- **Append-Only:** Audit logs cannot be edited or deleted through the UI (only via retention cleanup)
- **Database Permissions:** Application user should have INSERT and SELECT only (no UPDATE or manual DELETE)
- **Correlation IDs:** Enable tracing of multi-step operations for forensic analysis

---

## Performance Optimization

### Query Performance

- **Indexes:** Ensure indexes exist on `Timestamp`, `ActorId`, `GuildId`, `Category`, `Action`, and `CorrelationId`
- **Pagination:** Always use pagination for large result sets (default: 20 per page, max: 100)
- **Date Ranges:** Limit queries to reasonable date ranges to avoid full table scans
- **Details Search:** Full-text search on `Details` JSON can be slow; consider limiting to recent entries

### Write Performance

- **Background Queue:** All writes go through a background queue for non-blocking performance
- **Batch Size:** Retention cleanup processes 1,000 records at a time to avoid long transactions
- **Fire-and-Forget:** Use `Enqueue()` instead of `LogAsync()` for best performance

### Database Maintenance

- **Archive Old Logs:** For long-term retention, export and archive logs before cleanup
- **Index Maintenance:** Rebuild fragmented indexes monthly
- **Statistics Updates:** Keep table statistics current for optimal query plans

---

## Troubleshooting

### Issue: Audit logs not appearing in the UI

**Possible Causes:**

1. Queue processor not running
2. Database connection failure
3. Entries filtered out by default time range

**Solutions:**

- Check that `AuditLogQueueProcessor` is registered as a hosted service
- Review application logs for database errors
- Expand date range filter or clear all filters

### Issue: Queue overflow warnings in logs

**Symptoms:**

```
Failed to enqueue audit log entry for Security.Login. Queue may be closed.
```

**Solutions:**

- Check queue processor is running and consuming entries
- Increase queue capacity in `AuditLogQueue` constructor (default: 10,000)
- Review database performance; slow writes can cause queue buildup

### Issue: Retention cleanup not running

**Possible Causes:**

1. `AuditLogRetention:Enabled` set to `false`
2. `AuditLogRetentionService` not registered as hosted service
3. Initial delay not yet elapsed

**Solutions:**

- Verify `Enabled: true` in `appsettings.json`
- Check that service is registered in `Program.cs`
- Wait 5 minutes after application start for initial cleanup

### Issue: Actor display names showing GUIDs

**Cause:** User records deleted or not found in database

**Solution:** This is expected behavior. The audit log preserves the actor ID even if the user is deleted, ensuring accountability.

---

## Future Enhancements

Potential improvements to the audit log system:

1. **Batch Writing:** Queue processor could batch multiple entries into a single database transaction
2. **Archival:** Export old logs to cold storage (Azure Blob, S3) before cleanup
3. **Real-Time Notifications:** WebSocket notifications for critical audit events (security changes, admin actions)
4. **Advanced Search:** Full-text search on `Details` JSON with dedicated search index
5. **Analytics Dashboard:** Visualizations for audit trends, top actors, category breakdowns
6. **Anomaly Detection:** Machine learning to detect unusual patterns in audit logs
7. **Compliance Reports:** Pre-built reports for GDPR, SOC 2, HIPAA compliance

---

## Related Documentation

- [API Endpoints Reference](api-endpoints.md) - Full REST API documentation
- [Authorization Policies](authorization-policies.md) - Role-based access control
- [Settings Page Documentation](settings-page.md) - Runtime configuration management
- [Identity Configuration](identity-configuration.md) - User authentication and management
- [Database Schema](database-schema.md) - Complete database structure
- [Issue Tracking Process](issue-tracking-process.md) - Development workflow

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-30 | Initial documentation for v0.4.0 release |

---

**Document Owner:** System Architect
**Review Cycle:** Quarterly
**Next Review:** 2026-03-30
