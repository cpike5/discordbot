# API Endpoints Reference

This document provides comprehensive reference documentation for the Discord Bot Management System REST API.

## Overview

The REST API provides programmatic access to bot status, guild management, and command log analytics. All endpoints return JSON responses and use standard HTTP status codes.

**Base URL:** `http://localhost:5000/api` (development)

**API Version:** 1.0

**Authentication:** None (MVP - authentication to be added in future releases)

---

## Quick Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check with database connectivity |
| `/metrics` | GET | OpenTelemetry metrics (Prometheus format) |
| `/api/bot/status` | GET | Bot status (uptime, latency, guilds) |
| `/api/bot/guilds` | GET | Connected guilds from Discord |
| `/api/bot/restart` | POST | Restart bot (not supported) |
| `/api/bot/shutdown` | POST | Graceful shutdown |
| `/api/guilds` | GET | All guilds (DB + Discord merged) |
| `/api/guilds/{id}` | GET | Specific guild by ID |
| `/api/guilds/{id}` | PUT | Update guild settings |
| `/api/guilds/{id}/sync` | POST | Sync guild from Discord to DB |
| `/api/commandlogs` | GET | Query command logs (filtered, paginated) |
| `/api/commandlogs/stats` | GET | Command usage statistics |
| `/api/messages` | GET | Query message logs (filtered, paginated) |
| `/api/messages/{id}` | GET | Get specific message log by ID |
| `/api/messages/stats` | GET | Message statistics |
| `/api/messages/user/{userId}` | DELETE | Delete all messages for a user (GDPR) |
| `/api/messages/cleanup` | POST | Manually trigger message cleanup |
| `/api/messages/export` | GET | Export messages to CSV |

---

## Health Endpoints

### GET /api/health

Returns the health status of the application including database connectivity.

**Response: 200 OK**

```json
{
  "status": "Healthy",
  "timestamp": "2024-12-08T15:30:00Z",
  "version": "1.0.0.0",
  "checks": {
    "Database": "Healthy"
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | Overall status: "Healthy" or "Degraded" |
| `timestamp` | datetime | UTC timestamp of health check |
| `version` | string | Application version |
| `checks` | object | Individual health check results |

**Status Values:**
- `Healthy`: All checks passed
- `Degraded`: One or more checks failed (still operational)

---

## Metrics Endpoints

### GET /metrics

Returns OpenTelemetry metrics in Prometheus text exposition format for monitoring and observability.

**Response: 200 OK (text/plain)**

**Content-Type:** `text/plain; version=0.0.4`

**Response Format:** Prometheus text format

```
# HELP discordbot_command_count Total number of Discord commands executed
# TYPE discordbot_command_count counter
discordbot_command_count{command="ping",status="success"} 1250
discordbot_command_count{command="status",status="success"} 85
discordbot_command_count{command="verify",status="failure"} 12

# HELP discordbot_command_duration Duration of command execution in milliseconds
# TYPE discordbot_command_duration histogram
discordbot_command_duration_bucket{command="ping",status="success",le="5"} 0
discordbot_command_duration_bucket{command="ping",status="success",le="10"} 250
discordbot_command_duration_bucket{command="ping",status="success",le="25"} 1200
discordbot_command_duration_bucket{command="ping",status="success",le="+Inf"} 1250
discordbot_command_duration_sum{command="ping",status="success"} 15234.5
discordbot_command_duration_count{command="ping",status="success"} 1250

# HELP discordbot_guilds_active Number of guilds the bot is connected to
# TYPE discordbot_guilds_active gauge
discordbot_guilds_active 5

# HELP process_runtime_dotnet_gc_collections_count Number of garbage collections
# TYPE process_runtime_dotnet_gc_collections_count counter
process_runtime_dotnet_gc_collections_count{generation="gen0"} 42
process_runtime_dotnet_gc_collections_count{generation="gen1"} 18
process_runtime_dotnet_gc_collections_count{generation="gen2"} 3
```

**Metric Categories:**

| Category | Prefix | Description |
|----------|--------|-------------|
| Bot Commands | `discordbot.command.*` | Command execution metrics |
| Components | `discordbot.component.*` | Interactive component metrics |
| API Requests | `discordbot.api.*` | HTTP request metrics |
| Rate Limits | `discordbot.ratelimit.*` | Rate limit violation tracking |
| Bot Status | `discordbot.guilds.*`, `discordbot.users.*` | Guild and user counts |
| ASP.NET Core | `http.server.*` | Built-in HTTP server metrics |
| Runtime | `process.runtime.dotnet.*` | .NET runtime metrics (GC, threads, etc.) |

**Example Usage:**

```bash
# Fetch metrics directly
curl http://localhost:5000/metrics

# Use with Prometheus scrape configuration
# See docs/articles/metrics.md for full setup guide
```

**Notes:**
- Metrics are updated in real-time as bot operations occur
- Prometheus scraping is recommended with 15-second intervals
- See [Metrics Documentation](metrics.md) for complete metric definitions and Grafana dashboard setup
- Observable gauges (guild count, user count) are updated every 30 seconds

**Security Considerations:**
- In production, consider protecting this endpoint with IP whitelisting or authentication
- Metrics do not contain sensitive user data or message content
- Guild IDs and user IDs are not included in metrics to prevent cardinality explosion

**Related Documentation:**
- [Metrics Documentation](metrics.md) - Complete metrics reference and setup guide
- [APM Tracing Plan](apm-tracing-plan.md) - Distributed tracing (future implementation)

---

## Bot Management Endpoints

### GET /api/bot/status

Returns current bot status including uptime, latency, and connection information.

**Response: 200 OK**

```json
{
  "uptime": "2.15:30:45",
  "guildCount": 5,
  "latencyMs": 42,
  "startTime": "2024-12-06T00:00:00Z",
  "botUsername": "MyDiscordBot#1234",
  "connectionState": "Connected"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `uptime` | TimeSpan | Time since bot started (format: d.HH:mm:ss) |
| `guildCount` | integer | Number of connected guilds |
| `latencyMs` | integer | Gateway latency in milliseconds |
| `startTime` | datetime | UTC timestamp when bot started |
| `botUsername` | string | Bot's Discord username with discriminator |
| `connectionState` | string | Discord connection state |

**Connection States:**
- `Connected`: Bot is connected and operational
- `Connecting`: Bot is establishing connection
- `Disconnected`: Bot is offline
- `Disconnecting`: Bot is shutting down

---

### GET /api/bot/guilds

Returns list of guilds currently connected to the bot via Discord gateway.

**Response: 200 OK**

```json
[
  {
    "id": 123456789012345678,
    "name": "My Awesome Server",
    "memberCount": 1250,
    "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
  },
  {
    "id": 987654321098765432,
    "name": "Dev Testing Server",
    "memberCount": 5,
    "iconUrl": null
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Discord snowflake ID |
| `name` | string | Guild name |
| `memberCount` | integer | Number of members |
| `iconUrl` | string? | CDN URL for guild icon (null if none) |

---

### POST /api/bot/restart

Restarts the bot. **Note:** Currently not supported and will return 500 error.

**Response: 202 Accepted**

```json
(Empty response body on success)
```

**Response: 500 Internal Server Error**

```json
{
  "message": "Restart operation is not supported",
  "detail": "Bot restart is not implemented in the current version",
  "statusCode": 500,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/bot/shutdown

Initiates graceful shutdown of the bot.

**Response: 202 Accepted**

```json
{
  "message": "Shutdown initiated"
}
```

**Notes:**
- Shutdown is asynchronous; the API will remain available briefly
- All pending commands will be completed before shutdown
- Database connections are closed gracefully

---

## Guild Management Endpoints

### GET /api/guilds

Returns all guilds with merged data from database and live Discord information.

**Response: 200 OK**

```json
[
  {
    "id": 123456789012345678,
    "name": "My Awesome Server",
    "joinedAt": "2024-01-15T10:30:00Z",
    "isActive": true,
    "prefix": "!",
    "settings": "{\"welcomeChannel\":\"general\"}",
    "memberCount": 1250,
    "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
  }
]
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | ulong | Discord snowflake ID |
| `name` | string | Guild name (from Discord) |
| `joinedAt` | datetime | When bot joined the guild |
| `isActive` | boolean | Whether guild is active in database |
| `prefix` | string? | Custom command prefix (nullable) |
| `settings` | string? | JSON-encoded guild settings (nullable) |
| `memberCount` | integer? | Current member count from Discord (nullable if offline) |
| `iconUrl` | string? | Guild icon URL from Discord (nullable) |

---

### GET /api/guilds/{id}

Returns detailed information for a specific guild by ID.

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
{
  "id": 123456789012345678,
  "name": "My Awesome Server",
  "joinedAt": "2024-01-15T10:30:00Z",
  "isActive": true,
  "prefix": "!",
  "settings": "{\"welcomeChannel\":\"general\",\"modRole\":\"Moderator\"}",
  "memberCount": 1250,
  "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### PUT /api/guilds/{id}

Updates guild settings in the database.

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | ulong | Discord guild snowflake ID |

**Request Body:**

```json
{
  "prefix": "?",
  "settings": "{\"welcomeChannel\":\"lobby\",\"modRole\":\"Staff\"}",
  "isActive": true
}
```

**Request Fields:** (all optional)

| Field | Type | Description |
|-------|------|-------------|
| `prefix` | string? | Custom command prefix (null = no change) |
| `settings` | string? | JSON-encoded settings (null = no change) |
| `isActive` | boolean? | Active status (null = no change) |

**Response: 200 OK**

```json
{
  "id": 123456789012345678,
  "name": "My Awesome Server",
  "joinedAt": "2024-01-15T10:30:00Z",
  "isActive": true,
  "prefix": "?",
  "settings": "{\"welcomeChannel\":\"lobby\",\"modRole\":\"Staff\"}",
  "memberCount": 1250,
  "iconUrl": "https://cdn.discordapp.com/icons/123456789012345678/abc123.png"
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid request",
  "detail": "Request body cannot be null.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### POST /api/guilds/{id}/sync

Synchronizes guild data from Discord to the database. Creates or updates the guild record with current Discord information.

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | ulong | Discord guild snowflake ID |

**Response: 200 OK**

```json
{
  "message": "Guild synced successfully",
  "guildId": 123456789012345678
}
```

**Response: 404 Not Found**

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 is connected to the bot.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Notes:**
- Guild must be currently connected to the bot
- Creates new database record if guild doesn't exist
- Updates name and other Discord-sourced fields if record exists

---

## Command Log Endpoints

### GET /api/commandlogs

Retrieves command execution logs with optional filtering and pagination.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `guildId` | ulong? | null | Filter by guild ID |
| `userId` | ulong? | null | Filter by user ID |
| `commandName` | string? | null | Filter by command name (case-sensitive) |
| `startDate` | datetime? | null | Filter logs after this date (inclusive) |
| `endDate` | datetime? | null | Filter logs before this date (inclusive) |
| `successOnly` | boolean? | null | If true, only show successful commands |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 50 | Items per page (max: 100) |

**Example Request:**

```
GET /api/commandlogs?guildId=123456789012345678&successOnly=true&page=1&pageSize=20
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "userId": 987654321098765432,
      "username": "JohnDoe#1234",
      "commandName": "ping",
      "parameters": "{}",
      "executedAt": "2024-12-08T15:30:00Z",
      "responseTimeMs": 42,
      "success": true,
      "errorMessage": null
    },
    {
      "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "userId": 111222333444555666,
      "username": "JaneSmith#5678",
      "commandName": "status",
      "parameters": "{}",
      "executedAt": "2024-12-08T15:25:00Z",
      "responseTimeMs": 156,
      "success": true,
      "errorMessage": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of CommandLogDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**CommandLogDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid | Unique log entry identifier |
| `guildId` | ulong? | Guild ID where command was executed (null for DMs) |
| `guildName` | string? | Guild name (null for DMs) |
| `userId` | ulong | User who executed the command |
| `username` | string? | Username of executor |
| `commandName` | string | Name of the command |
| `parameters` | string? | JSON-encoded command parameters |
| `executedAt` | datetime | UTC timestamp of execution |
| `responseTimeMs` | integer | Command execution time in milliseconds |
| `success` | boolean | Whether command succeeded |
| `errorMessage` | string? | Error message if command failed (null if success) |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/commandlogs/stats

Returns command usage statistics, optionally filtered by date.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `since` | datetime? | null | Only count commands since this date (null = all time) |

**Example Request:**

```
GET /api/commandlogs/stats?since=2024-12-01T00:00:00Z
```

**Response: 200 OK**

```json
{
  "ping": 1250,
  "status": 85,
  "shutdown": 12,
  "guilds": 42
}
```

**Response Format:**

Dictionary mapping command names (string) to usage counts (integer).

**Notes:**
- Returns all commands that have been executed
- Counts include both successful and failed executions
- Empty object `{}` returned if no commands match filter

---

## Message Log Endpoints

### GET /api/messages

Retrieves message logs with optional filtering and pagination. Provides access to logged Discord messages for analytics and moderation.

**Authorization:** Admin+

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `authorId` | ulong? | null | Filter by message author ID |
| `guildId` | ulong? | null | Filter by guild ID |
| `channelId` | ulong? | null | Filter by channel ID |
| `source` | MessageSource? | null | Filter by source (DirectMessage or ServerChannel) |
| `startDate` | datetime? | null | Filter messages after this date (inclusive) |
| `endDate` | datetime? | null | Filter messages before this date (inclusive) |
| `searchTerm` | string? | null | Search message content (case-insensitive) |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 25 | Items per page (max: 100) |

**Example Request:**

```
GET /api/messages?guildId=123456789012345678&page=1&pageSize=50&startDate=2024-12-01T00:00:00Z
```

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": 1,
      "discordMessageId": 1234567890123456789,
      "authorId": 987654321098765432,
      "authorUsername": "JohnDoe#1234",
      "channelId": 111222333444555666,
      "channelName": "general",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "source": "ServerChannel",
      "content": "Hello, world!",
      "timestamp": "2024-12-08T15:30:00Z",
      "loggedAt": "2024-12-08T15:30:01Z",
      "hasAttachments": false,
      "hasEmbeds": false,
      "replyToMessageId": null
    },
    {
      "id": 2,
      "discordMessageId": 9876543210987654321,
      "authorId": 111222333444555666,
      "authorUsername": "JaneSmith#5678",
      "channelId": 111222333444555666,
      "channelName": "announcements",
      "guildId": 123456789012345678,
      "guildName": "My Awesome Server",
      "source": "ServerChannel",
      "content": "Check out this cool link!",
      "timestamp": "2024-12-08T15:25:00Z",
      "loggedAt": "2024-12-08T15:25:00Z",
      "hasAttachments": true,
      "hasEmbeds": true,
      "replyToMessageId": 1234567890123456789
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of MessageLogDto objects |
| `page` | integer | Current page number (1-based) |
| `pageSize` | integer | Items per page |
| `totalCount` | integer | Total number of items across all pages |
| `totalPages` | integer | Total number of pages |
| `hasNextPage` | boolean | Whether there are more pages |
| `hasPreviousPage` | boolean | Whether there are previous pages |

**MessageLogDto Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | long | Unique database identifier |
| `discordMessageId` | ulong | Discord snowflake message ID |
| `authorId` | ulong | User who authored the message |
| `authorUsername` | string? | Username for display (nullable) |
| `channelId` | ulong | Channel ID where message was sent |
| `channelName` | string? | Channel name for display (nullable) |
| `guildId` | ulong? | Guild ID (null for DMs) |
| `guildName` | string? | Guild name for display (nullable) |
| `source` | MessageSource | DirectMessage or ServerChannel |
| `content` | string | Message content/text |
| `timestamp` | datetime | When message was sent on Discord |
| `loggedAt` | datetime | When message was logged to database |
| `hasAttachments` | boolean | Whether message has attachments |
| `hasEmbeds` | boolean | Whether message has embeds |
| `replyToMessageId` | ulong? | ID of message this is replying to (nullable) |

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/messages/{id}

Returns detailed information for a specific message log by ID.

**Authorization:** Admin+

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Message log database ID |

**Response: 200 OK**

```json
{
  "id": 1,
  "discordMessageId": 1234567890123456789,
  "authorId": 987654321098765432,
  "authorUsername": "JohnDoe#1234",
  "channelId": 111222333444555666,
  "channelName": "general",
  "guildId": 123456789012345678,
  "guildName": "My Awesome Server",
  "source": "ServerChannel",
  "content": "Hello, world!",
  "timestamp": "2024-12-08T15:30:00Z",
  "loggedAt": "2024-12-08T15:30:01Z",
  "hasAttachments": false,
  "hasEmbeds": false,
  "replyToMessageId": null
}
```

**Response: 404 Not Found**

```json
{
  "message": "Message not found",
  "detail": "No message log with ID 1 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

---

### GET /api/messages/stats

Returns comprehensive message statistics including counts, breakdowns by source, and daily trends.

**Authorization:** Admin+

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `guildId` | ulong? | null | Filter stats by guild (null = global statistics) |

**Example Request:**

```
GET /api/messages/stats?guildId=123456789012345678
```

**Response: 200 OK**

```json
{
  "totalMessages": 15420,
  "dmMessages": 250,
  "serverMessages": 15170,
  "uniqueAuthors": 156,
  "messagesByDay": [
    {
      "date": "2024-12-08",
      "count": 450
    },
    {
      "date": "2024-12-07",
      "count": 380
    },
    {
      "date": "2024-12-06",
      "count": 420
    }
  ],
  "oldestMessage": "2024-11-01T08:15:00Z",
  "newestMessage": "2024-12-08T15:30:00Z"
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `totalMessages` | long | Total number of messages logged |
| `dmMessages` | long | Count of direct messages |
| `serverMessages` | long | Count of server channel messages |
| `uniqueAuthors` | long | Number of unique message authors |
| `messagesByDay` | array | Daily message counts for last 7 days |
| `oldestMessage` | datetime? | Timestamp of oldest message (null if none) |
| `newestMessage` | datetime? | Timestamp of newest message (null if none) |

**DailyMessageCount Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `date` | DateOnly | The date for this count |
| `count` | long | Number of messages on this date |

**Notes:**
- `messagesByDay` contains the last 7 days of data
- If `guildId` is specified, statistics are filtered to that guild only
- Null timestamps indicate no messages exist

---

### DELETE /api/messages/user/{userId}

Deletes all message logs for a specific user. Used for GDPR compliance and user data deletion requests.

**Authorization:** SuperAdmin only

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `userId` | ulong | Discord user ID whose messages should be deleted |

**Response: 200 OK**

```json
{
  "deletedCount": 42
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `deletedCount` | integer | Number of message logs deleted |

**Notes:**
- This operation is permanent and cannot be undone
- All messages authored by the specified user are deleted
- Used for GDPR "right to be forgotten" compliance
- Operation is logged for audit purposes

---

### POST /api/messages/cleanup

Manually triggers cleanup of old message logs according to the configured retention policy. Deletes messages older than the retention period in batches.

**Authorization:** SuperAdmin only

**Response: 200 OK**

```json
{
  "deletedCount": 1250
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `deletedCount` | integer | Total number of message logs deleted |

**Notes:**
- Cleanup is normally performed automatically by a background service
- This endpoint allows manual triggering for administrative purposes
- Messages are deleted in batches to avoid database overload
- Only messages older than the configured retention period are deleted
- Operation is logged for audit purposes

---

### GET /api/messages/export

Exports message logs matching the query criteria to a CSV file for external analysis or archival.

**Authorization:** Admin+

**Query Parameters:**

Uses the same query parameters as `GET /api/messages`:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `authorId` | ulong? | null | Filter by message author ID |
| `guildId` | ulong? | null | Filter by guild ID |
| `channelId` | ulong? | null | Filter by channel ID |
| `source` | MessageSource? | null | Filter by source (DirectMessage or ServerChannel) |
| `startDate` | datetime? | null | Filter messages after this date (inclusive) |
| `endDate` | datetime? | null | Filter messages before this date (inclusive) |
| `searchTerm` | string? | null | Search message content (case-insensitive) |
| `page` | integer | 1 | Page number (1-based) |
| `pageSize` | integer | 25 | Items per page (max: 100) |

**Example Request:**

```
GET /api/messages/export?guildId=123456789012345678&startDate=2024-12-01T00:00:00Z
```

**Response: 200 OK**

```csv
Id,DiscordMessageId,AuthorId,AuthorUsername,ChannelId,ChannelName,GuildId,GuildName,Source,Content,Timestamp,LoggedAt,HasAttachments,HasEmbeds,ReplyToMessageId
1,1234567890123456789,987654321098765432,JohnDoe#1234,111222333444555666,general,123456789012345678,My Awesome Server,ServerChannel,"Hello, world!",2024-12-08T15:30:00Z,2024-12-08T15:30:01Z,false,false,
2,9876543210987654321,111222333444555666,JaneSmith#5678,111222333444555666,announcements,123456789012345678,My Awesome Server,ServerChannel,Check out this cool link!,2024-12-08T15:25:00Z,2024-12-08T15:25:00Z,true,true,1234567890123456789
```

**Response Headers:**

| Header | Value |
|--------|-------|
| `Content-Type` | text/csv |
| `Content-Disposition` | attachment; filename="message-logs-yyyyMMddHHmmss.csv" |

**Notes:**
- Filename includes UTC timestamp for uniqueness
- CSV includes all fields from MessageLogDto
- Export respects the same filters as the GET endpoint
- Large exports may take time to generate
- Consider using pagination to limit export size

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123-def456-00"
}
```

---

## Error Response Format

All error responses follow a consistent format using `ApiErrorDto`.

**Structure:**

```json
{
  "message": "Brief error description",
  "detail": "Detailed explanation of what went wrong",
  "statusCode": 400,
  "traceId": "00-abc123def456-789012-00"
}
```

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `message` | string | Short, user-friendly error message |
| `detail` | string? | Detailed error explanation (optional) |
| `statusCode` | integer | HTTP status code |
| `traceId` | string? | Correlation ID for log tracing (optional) |

**Common Status Codes:**

| Code | Meaning | Common Causes |
|------|---------|---------------|
| 400 | Bad Request | Invalid input, validation failure, malformed JSON |
| 404 | Not Found | Guild not found, resource doesn't exist |
| 500 | Internal Server Error | Unexpected server error, database connection failure |
| 202 | Accepted | Async operation initiated (shutdown, restart) |

---

## Accessing Swagger UI

The API includes interactive Swagger/OpenAPI documentation for testing endpoints directly in your browser.

**URL:** `http://localhost:5000/swagger`

**Features:**
- Interactive endpoint testing with request/response examples
- Schema definitions for all DTOs
- Try-it-out functionality for immediate testing
- Automatic request validation

**Development vs Production:**
- Swagger is enabled by default in Development environment
- Consider disabling Swagger in Production for security

---

## Rate Limiting

**Current Status:** Not implemented in MVP

**Future Considerations:**
- Per-IP rate limiting for API endpoints
- Separate rate limits for read vs write operations
- Rate limit headers in responses (`X-RateLimit-Limit`, `X-RateLimit-Remaining`)

---

## CORS Configuration

**Current Status:** CORS is configured to allow all origins in Development

**Configuration Location:** `Program.cs`

**Future Considerations:**
- Restrict allowed origins in Production
- Configure allowed methods and headers
- Implement credential support for authenticated requests

---

## User Management Service Interface

The User Management Service provides programmatic access to user administration operations. This is a service-layer interface (not a REST API endpoint) used by the admin web UI.

**Interface:** `IUserManagementService`

**Location:** `DiscordBot.Core.Interfaces.IUserManagementService`

**Authorization:** All operations enforce role-based authorization and self-protection rules. See [User Management Guide](user-management.md) for details.

### Service Methods

#### Query Operations

##### GetUsersAsync

Retrieves a paginated list of users with search and filter capabilities.

```csharp
Task<PaginatedResponseDto<UserDto>> GetUsersAsync(
    UserSearchQueryDto query,
    CancellationToken cancellationToken = default);
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `query` | UserSearchQueryDto | Search, filter, and pagination parameters |
| `cancellationToken` | CancellationToken | Cancellation token (optional) |

**UserSearchQueryDto Fields:**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SearchTerm` | string? | null | Search by email, display name, or Discord username |
| `Role` | string? | null | Filter by role (SuperAdmin, Admin, Moderator, Viewer) |
| `IsActive` | bool? | null | Filter by active status |
| `IsDiscordLinked` | bool? | null | Filter by Discord link status |
| `Page` | int | 1 | Page number (1-based) |
| `PageSize` | int | 20 | Items per page |
| `SortBy` | string | "CreatedAt" | Sort field (Email, DisplayName, CreatedAt, LastLoginAt) |
| `SortDescending` | bool | true | Sort direction |

**Returns:** `PaginatedResponseDto<UserDto>` containing user list and pagination metadata.

##### GetUserByIdAsync

Retrieves detailed information for a single user.

```csharp
Task<UserDto?> GetUserByIdAsync(
    string userId,
    CancellationToken cancellationToken = default);
```

**Returns:** `UserDto` if found, `null` otherwise.

##### GetAvailableRolesAsync

Gets the list of roles that the current user is authorized to assign.

```csharp
Task<IReadOnlyList<string>> GetAvailableRolesAsync(
    string currentUserId,
    CancellationToken cancellationToken = default);
```

**Role Assignment Rules:**
- SuperAdmin can assign: SuperAdmin, Admin, Moderator, Viewer
- Admin can assign: Admin, Moderator, Viewer (not SuperAdmin)
- Other roles cannot assign roles

**Returns:** List of role names the user can assign.

#### Create Operations

##### CreateUserAsync

Creates a new user account with the specified email, password, and role.

```csharp
Task<UserManagementResult> CreateUserAsync(
    UserCreateDto request,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**UserCreateDto Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Email` | string | Yes | User email address (must be unique) |
| `DisplayName` | string? | No | Display name (optional) |
| `Password` | string | Yes | Initial password (must meet complexity requirements) |
| `ConfirmPassword` | string | Yes | Password confirmation (must match) |
| `Role` | string | Yes | Initial role (default: "Viewer") |
| `SendWelcomeEmail` | bool | No | Send welcome email (default: true) |

**Validation:**
- Email must be unique
- Password must meet ASP.NET Identity complexity requirements
- Actor must have permission to assign the specified role

**Activity Logged:** `UserCreated` with email and assigned role

**Returns:** `UserManagementResult` with created user data on success.

#### Update Operations

##### UpdateUserAsync

Updates user information including email, display name, active status, and role.

```csharp
Task<UserManagementResult> UpdateUserAsync(
    string userId,
    UserUpdateDto request,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**UserUpdateDto Fields:** (all optional)

| Field | Type | Description |
|-------|------|-------------|
| `DisplayName` | string? | New display name |
| `Email` | string? | New email address |
| `IsActive` | bool? | Active status |
| `Role` | string? | New role |

**Self-Protection:**
- Users cannot change their own active status
- Users cannot change their own role

**Activity Logged:** `UserUpdated` with changed fields

**Returns:** `UserManagementResult` with updated user data.

##### SetUserActiveStatusAsync

Enables or disables a user account.

```csharp
Task<UserManagementResult> SetUserActiveStatusAsync(
    string userId,
    bool isActive,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Self-Protection:** Users cannot disable their own account.

**Activity Logged:** `UserEnabled` or `UserDisabled`

**Returns:** `UserManagementResult` with updated user data.

##### AssignRoleAsync

Assigns a role to a user (removes existing roles).

```csharp
Task<UserManagementResult> AssignRoleAsync(
    string userId,
    string role,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Authorization:**
- Actor must have permission to assign the target role
- SuperAdmin required to assign SuperAdmin role
- Only SuperAdmin can manage other SuperAdmins

**Self-Protection:** Users cannot change their own role.

**Activity Logged:** `RoleAssigned` with old and new roles

**Returns:** `UserManagementResult` with updated user data.

##### RemoveRoleAsync

Removes a specific role from a user.

```csharp
Task<UserManagementResult> RemoveRoleAsync(
    string userId,
    string role,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Self-Protection:** Users cannot remove their own roles.

**Activity Logged:** `RoleRemoved` with removed role

**Returns:** `UserManagementResult` with updated user data.

#### Password Operations

##### ResetPasswordAsync

Performs an admin-initiated password reset, generating a secure temporary password.

```csharp
Task<UserManagementResult> ResetPasswordAsync(
    string userId,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Behavior:**
- Generates a secure 16-character temporary password
- Password includes uppercase, lowercase, digits, and special characters
- Temporary password is returned in the result (`GeneratedPassword` property)
- Old password is immediately invalidated

**Security:** The password is never logged or stored in plain text.

**Activity Logged:** `PasswordReset` (no password details included)

**Returns:** `UserManagementResult` with `GeneratedPassword` containing the temporary password.

#### Discord Linking Operations

##### UnlinkDiscordAccountAsync

Removes the Discord account association from a user.

```csharp
Task<UserManagementResult> UnlinkDiscordAccountAsync(
    string userId,
    string actorUserId,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
```

**Behavior:**
- Removes Discord user ID, username, and avatar URL
- User can still log in with email/password after unlinking
- User must re-link Discord account to access Discord-only features

**Activity Logged:** `DiscordUnlinked` with previous Discord username

**Returns:** `UserManagementResult` with updated user data.

#### Activity Log

##### GetActivityLogAsync

Retrieves the audit log of user management actions.

```csharp
Task<PaginatedResponseDto<UserActivityLogDto>> GetActivityLogAsync(
    string? userId,
    int page = 1,
    int pageSize = 50,
    CancellationToken cancellationToken = default);
```

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `userId` | string? | null | Filter by user (null = all users) |
| `page` | int | 1 | Page number |
| `pageSize` | int | 50 | Items per page |

**Returns:** Paginated list of `UserActivityLogDto` entries, ordered by timestamp (newest first).

#### Validation

##### CanManageUserAsync

Checks if an actor has permission to manage a target user.

```csharp
Task<bool> CanManageUserAsync(
    string actorUserId,
    string targetUserId,
    CancellationToken cancellationToken = default);
```

**Rules:**
- Users cannot manage themselves (returns `false`)
- Only SuperAdmins can manage other SuperAdmins
- Admins and SuperAdmins can manage users below SuperAdmin

**Returns:** `true` if actor can manage target user, `false` otherwise.

---

## User Management DTOs

### UserDto

```csharp
{
  "id": string,                    // User ID
  "email": string,                 // Email address
  "displayName": string?,          // Display name (nullable)
  "isActive": bool,                // Active status
  "emailConfirmed": bool,          // Email confirmation status
  "createdAt": DateTime,           // Account creation timestamp
  "lastLoginAt": DateTime?,        // Last login timestamp (nullable)
  "isLockedOut": bool,             // Lockout status (computed)
  "lockoutEnd": DateTimeOffset?,   // Lockout end time (nullable)
  "isDiscordLinked": bool,         // Discord link status
  "discordUserId": ulong?,         // Discord user ID (nullable)
  "discordUsername": string?,      // Discord username (nullable)
  "discordAvatarUrl": string?,     // Discord avatar URL (nullable)
  "roles": string[],               // Assigned roles
  "highestRole": string            // Highest priority role (computed)
}
```

### UserCreateDto

```csharp
{
  "email": string,                 // Email address (required)
  "displayName": string?,          // Display name (optional)
  "password": string,              // Password (required)
  "confirmPassword": string,       // Password confirmation (required)
  "role": string,                  // Initial role (default: "Viewer")
  "sendWelcomeEmail": bool         // Send welcome email (default: true)
}
```

### UserUpdateDto

```csharp
{
  "displayName": string?,          // New display name (optional)
  "email": string?,                // New email (optional)
  "isActive": bool?,               // New active status (optional)
  "role": string?                  // New role (optional)
}
```

### UserManagementResult

```csharp
{
  "succeeded": bool,               // Operation success flag
  "errorCode": string?,            // Error code (nullable)
  "errorMessage": string?,         // Error message (nullable)
  "user": UserDto?,                // Updated user data (nullable)
  "generatedPassword": string?     // Temporary password (password reset only)
}
```

**Common Error Codes:**
- `USER_NOT_FOUND` - User does not exist
- `SELF_MODIFICATION_DENIED` - Cannot modify own account
- `INSUFFICIENT_PERMISSIONS` - Insufficient role privileges
- `INVALID_ROLE` - Role does not exist
- `EMAIL_ALREADY_EXISTS` - Email already in use
- `PASSWORD_VALIDATION_FAILED` - Password does not meet requirements
- `DISCORD_NOT_LINKED` - No Discord account linked

### UserActivityLogDto

```csharp
{
  "id": Guid,                      // Log entry ID
  "actorUserId": string,           // User who performed action
  "actorEmail": string,            // Actor's email
  "targetUserId": string?,         // User affected (nullable)
  "targetEmail": string?,          // Target's email (nullable)
  "action": UserActivityAction,    // Action type (enum)
  "details": string?,              // JSON details (nullable)
  "timestamp": DateTime,           // Action timestamp
  "ipAddress": string?             // Actor's IP address (nullable)
}
```

**UserActivityAction Enum:**
- `UserCreated` - User account created
- `UserUpdated` - User information updated
- `UserDeleted` - User account deleted
- `UserEnabled` - User account enabled
- `UserDisabled` - User account disabled
- `RoleAssigned` - Role assigned to user
- `RoleRemoved` - Role removed from user
- `PasswordReset` - Password reset by admin
- `DiscordLinked` - Discord account linked
- `DiscordUnlinked` - Discord account unlinked
- `AccountLocked` - Account locked (failed login attempts)
- `AccountUnlocked` - Account unlocked
- `LoginSuccess` - Successful login
- `LoginFailed` - Failed login attempt

### UserSearchQueryDto

```csharp
{
  "searchTerm": string?,           // Search filter (optional)
  "role": string?,                 // Role filter (optional)
  "isActive": bool?,               // Active status filter (optional)
  "isDiscordLinked": bool?,        // Discord link filter (optional)
  "page": int,                     // Page number (default: 1)
  "pageSize": int,                 // Items per page (default: 20)
  "sortBy": string,                // Sort field (default: "CreatedAt")
  "sortDescending": bool           // Sort direction (default: true)
}
```

---

## Data Transfer Objects (DTOs)

### HealthResponseDto

```csharp
{
  "status": string,           // "Healthy" or "Degraded"
  "timestamp": DateTime,      // UTC timestamp
  "version": string,          // Application version
  "checks": Dictionary<string, string>  // Check name -> status
}
```

### BotStatusDto

```csharp
{
  "uptime": TimeSpan,         // Time since start
  "guildCount": int,          // Number of guilds
  "latencyMs": int,           // Gateway latency
  "startTime": DateTime,      // Start timestamp
  "botUsername": string,      // Bot username#discriminator
  "connectionState": string   // Discord connection state
}
```

### GuildInfoDto

```csharp
{
  "id": ulong,                // Discord snowflake ID
  "name": string,             // Guild name
  "memberCount": int,         // Member count
  "iconUrl": string?          // Icon URL (nullable)
}
```

### GuildDto

```csharp
{
  "id": ulong,                // Discord snowflake ID
  "name": string,             // Guild name
  "joinedAt": DateTime,       // Join timestamp
  "isActive": bool,           // Active status
  "prefix": string?,          // Custom prefix (nullable)
  "settings": string?,        // JSON settings (nullable)
  "memberCount": int?,        // Live member count (nullable)
  "iconUrl": string?          // Live icon URL (nullable)
}
```

### GuildUpdateRequestDto

```csharp
{
  "prefix": string?,          // New prefix (null = no change)
  "settings": string?,        // New settings JSON (null = no change)
  "isActive": bool?           // New active status (null = no change)
}
```

### CommandLogDto

```csharp
{
  "id": Guid,                 // Unique identifier
  "guildId": ulong?,          // Guild ID (nullable for DMs)
  "guildName": string?,       // Guild name (nullable)
  "userId": ulong,            // User ID
  "username": string?,        // Username (nullable)
  "commandName": string,      // Command name
  "parameters": string?,      // Parameters JSON (nullable)
  "executedAt": DateTime,     // Execution timestamp
  "responseTimeMs": int,      // Response time
  "success": bool,            // Success flag
  "errorMessage": string?     // Error message (nullable)
}
```

### CommandLogQueryDto

```csharp
{
  "guildId": ulong?,          // Filter by guild
  "userId": ulong?,           // Filter by user
  "commandName": string?,     // Filter by command
  "startDate": DateTime?,     // Filter by start date
  "endDate": DateTime?,       // Filter by end date
  "successOnly": bool?,       // Filter successes only
  "page": int,                // Page number (default: 1)
  "pageSize": int             // Page size (default: 50, max: 100)
}
```

### PaginatedResponseDto&lt;T&gt;

```csharp
{
  "items": IReadOnlyList<T>,  // Page items
  "page": int,                // Current page (1-based)
  "pageSize": int,            // Items per page
  "totalCount": int,          // Total items
  "totalPages": int,          // Total pages (calculated)
  "hasNextPage": bool,        // Has next page (calculated)
  "hasPreviousPage": bool     // Has previous page (calculated)
}
```

### MessageLogDto

```csharp
{
  "id": long,                       // Unique database identifier
  "discordMessageId": ulong,        // Discord snowflake message ID
  "authorId": ulong,                // User who authored the message
  "authorUsername": string?,        // Username for display (nullable)
  "channelId": ulong,               // Channel ID where message was sent
  "channelName": string?,           // Channel name for display (nullable)
  "guildId": ulong?,                // Guild ID (null for DMs)
  "guildName": string?,             // Guild name for display (nullable)
  "source": MessageSource,          // DirectMessage or ServerChannel
  "content": string,                // Message content/text
  "timestamp": DateTime,            // When message was sent on Discord
  "loggedAt": DateTime,             // When message was logged to database
  "hasAttachments": bool,           // Whether message has attachments
  "hasEmbeds": bool,                // Whether message has embeds
  "replyToMessageId": ulong?        // ID of message this is replying to (nullable)
}
```

**MessageSource Enum:**
- `DirectMessage` - Message sent in a DM
- `ServerChannel` - Message sent in a guild channel

### MessageLogQueryDto

```csharp
{
  "authorId": ulong?,          // Filter by author ID
  "guildId": ulong?,           // Filter by guild ID
  "channelId": ulong?,         // Filter by channel ID
  "source": MessageSource?,    // Filter by source
  "startDate": DateTime?,      // Filter by start date
  "endDate": DateTime?,        // Filter by end date
  "searchTerm": string?,       // Search message content
  "page": int,                 // Page number (default: 1)
  "pageSize": int              // Page size (default: 25, max: 100)
}
```

### MessageLogStatsDto

```csharp
{
  "totalMessages": long,                        // Total number of messages logged
  "dmMessages": long,                           // Count of direct messages
  "serverMessages": long,                       // Count of server channel messages
  "uniqueAuthors": long,                        // Number of unique message authors
  "messagesByDay": List<DailyMessageCount>,     // Daily message counts for last 7 days
  "oldestMessage": DateTime?,                   // Timestamp of oldest message (nullable)
  "newestMessage": DateTime?                    // Timestamp of newest message (nullable)
}
```

### DailyMessageCount

```csharp
{
  "date": DateOnly,            // The date for this count
  "count": long                // Number of messages on this date
}
```

### ApiErrorDto

```csharp
{
  "message": string,          // Error message
  "detail": string?,          // Error details (nullable)
  "statusCode": int,          // HTTP status code
  "traceId": string?          // Trace ID (nullable)
}
```

---

## Integration Examples

### Example: Get Bot Status

```bash
curl -X GET "http://localhost:5000/api/bot/status" -H "accept: application/json"
```

### Example: Update Guild Settings

```bash
curl -X PUT "http://localhost:5000/api/guilds/123456789012345678" \
  -H "Content-Type: application/json" \
  -d '{
    "prefix": "!",
    "isActive": true,
    "settings": "{\"welcomeChannel\":\"general\"}"
  }'
```

### Example: Query Command Logs

```bash
curl -X GET "http://localhost:5000/api/commandlogs?guildId=123456789012345678&page=1&pageSize=10&successOnly=true" \
  -H "accept: application/json"
```

### Example: Get Command Statistics

```bash
curl -X GET "http://localhost:5000/api/commandlogs/stats?since=2024-12-01T00:00:00Z" \
  -H "accept: application/json"
```

### Example: Sync Guild from Discord

```bash
curl -X POST "http://localhost:5000/api/guilds/123456789012345678/sync" \
  -H "accept: application/json"
```

### Example: Query Message Logs

```bash
curl -X GET "http://localhost:5000/api/messages?guildId=123456789012345678&page=1&pageSize=50&startDate=2024-12-01T00:00:00Z" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

### Example: Get Message Statistics

```bash
curl -X GET "http://localhost:5000/api/messages/stats?guildId=123456789012345678" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-token-here"
```

### Example: Export Messages to CSV

```bash
curl -X GET "http://localhost:5000/api/messages/export?guildId=123456789012345678&startDate=2024-12-01T00:00:00Z" \
  -H "accept: text/csv" \
  -H "Authorization: Bearer your-token-here" \
  --output messages.csv
```

### Example: Delete User Messages (GDPR)

```bash
curl -X DELETE "http://localhost:5000/api/messages/user/987654321098765432" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-superadmin-token-here"
```

### Example: Manually Trigger Cleanup

```bash
curl -X POST "http://localhost:5000/api/messages/cleanup" \
  -H "accept: application/json" \
  -H "Authorization: Bearer your-superadmin-token-here"
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.3 | 2025-12-24 | Added Message Log endpoints documentation (Issue #140) |
| 1.2 | 2025-12-24 | Added `/metrics` endpoint documentation (Issue #104) |
| 1.1 | 2024-12-09 | Added User Management Service interface documentation (Issue #66) |
| 1.0 | 2024-12-08 | Initial API implementation (Phase 4 MVP) |

---

## Related Documentation

- [Metrics Documentation](metrics.md) - OpenTelemetry metrics and Prometheus setup
- [User Management Guide](user-management.md) - Comprehensive user administration guide
- [MVP Implementation Plan](mvp-plan.md) - Full development roadmap
- [Database Schema](database-schema.md) - Entity definitions and relationships
- [Repository Pattern](repository-pattern.md) - Data access implementation
- [Admin Commands](admin-commands.md) - Discord slash command reference
- [Authorization Policies](authorization-policies.md) - Role-based authorization

---

*Last Updated: December 24, 2025*
