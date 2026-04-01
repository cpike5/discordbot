# Message Logging

**Last Updated:** 2025-12-30
**Implemented In:** v0.3.0
**Related Issues:** [#315](https://github.com/cpike5/discordbot/issues/315)

## Overview

The message logging feature provides comprehensive tracking and analytics for Discord messages sent in both server channels and direct messages. This system captures message metadata and content for moderation, analytics, and auditing purposes while respecting user privacy through consent-based collection.

### Purpose

Message logging serves several key objectives:

- **Moderation Support**: Provides message history for investigating user behavior, disputes, and policy violations
- **Analytics and Insights**: Enables analysis of message patterns, activity trends, and user engagement
- **Audit Trail**: Creates a historical record of communications for compliance and accountability
- **GDPR Compliance**: Supports user data deletion requests and privacy rights through consent tracking

### What Messages are Captured

The system logs messages from the following sources:

- **Server Channel Messages**: Messages sent in public and private channels within Discord servers (guilds)
- **Direct Messages**: Private messages sent to the bot or users who have granted consent

### What is NOT Logged

To protect privacy and reduce storage overhead, the following are excluded from logging:

- **Bot Messages**: Messages sent by bots (including this bot) are not logged
- **System Messages**: Discord system messages (user joins, pins, boosts, etc.) are not logged
- **Attachment Content**: Only metadata flags (`HasAttachments`, `HasEmbeds`) are stored; actual files, images, and embed content are not saved
- **Messages from Non-Consenting Users**: Users who have not granted `MessageLogging` consent are excluded from logging

### Privacy Considerations

Message logging is privacy-conscious by design:

1. **User Consent Required**: Messages are only logged for users who have explicitly granted `MessageLogging` consent via the `/consent` Discord command or the web UI privacy settings
2. **Feature Toggle**: Message logging can be globally disabled via the `Features:MessageLoggingEnabled` setting (see [Settings Page](settings-page.md))
3. **Automatic Retention Policy**: Old messages are automatically cleaned up based on configurable retention periods (default: 90 days)
4. **GDPR Deletion**: Users can request deletion of all their messages via the API (SuperAdmin only endpoint)
5. **No Content Analysis**: Message content is stored as-is without processing, sentiment analysis, or automated scanning

---

## Message Log Entity

The `MessageLog` entity (defined in `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Entities\MessageLog.cs`) represents a single logged Discord message.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `long` | Unique identifier for this log entry (database primary key) |
| `DiscordMessageId` | `ulong` | Discord's unique message ID (snowflake) |
| `AuthorId` | `ulong` | Discord user ID of the message author |
| `ChannelId` | `ulong` | Discord channel ID where the message was sent |
| `GuildId` | `ulong?` | Discord guild (server) ID; null for direct messages |
| `Source` | `MessageSource` | Enum indicating message source (DirectMessage or ServerChannel) |
| `Content` | `string` | Text content of the message (empty string if no text) |
| `Timestamp` | `DateTime` | Original timestamp when message was sent on Discord (UTC) |
| `LoggedAt` | `DateTime` | Timestamp when message was logged to the database (UTC) |
| `HasAttachments` | `bool` | Whether the message included file attachments (images, videos, files) |
| `HasEmbeds` | `bool` | Whether the message included rich embeds |
| `ReplyToMessageId` | `ulong?` | Discord message ID this message is replying to; null if not a reply |

### Navigation Properties

| Property | Type | Description |
|----------|------|-------------|
| `Guild` | `Guild?` | Navigation property to the guild entity; nullable for DMs |
| `User` | `User?` | Navigation property to the user who authored the message |

### MessageSource Enum

The `MessageSource` enum (defined in `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Enums\MessageSource.cs`) categorizes where a message originated:

```csharp
public enum MessageSource
{
    DirectMessage = 1,    // Message sent in a direct message (DM) channel
    ServerChannel = 2     // Message sent in a server (guild) channel
}
```

### Database Schema

```sql
CREATE TABLE MessageLogs (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    DiscordMessageId INTEGER NOT NULL,
    AuthorId INTEGER NOT NULL,
    ChannelId INTEGER NOT NULL,
    GuildId INTEGER NULL,
    Source INTEGER NOT NULL,
    Content TEXT NOT NULL,
    Timestamp TEXT NOT NULL,
    LoggedAt TEXT NOT NULL,
    HasAttachments INTEGER NOT NULL DEFAULT 0,
    HasEmbeds INTEGER NOT NULL DEFAULT 0,
    ReplyToMessageId INTEGER NULL,
    FOREIGN KEY (AuthorId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id) ON DELETE CASCADE
);

CREATE INDEX IX_MessageLogs_AuthorId ON MessageLogs (AuthorId);
CREATE INDEX IX_MessageLogs_GuildId ON MessageLogs (GuildId);
CREATE INDEX IX_MessageLogs_Timestamp ON MessageLogs (Timestamp);
```

**Indexes:**
- `IX_MessageLogs_AuthorId` - Optimizes queries filtering by user
- `IX_MessageLogs_GuildId` - Optimizes queries filtering by guild
- `IX_MessageLogs_Timestamp` - Optimizes date range queries and retention cleanup

---

## Configuration

Message logging behavior is controlled through several configuration options.

### MessageLogRetentionOptions

The `MessageLogRetentionOptions` class (defined in `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Configuration\MessageLogRetentionOptions.cs`) controls automatic cleanup and retention policies.

**Configuration Section:** `MessageLogRetention` in `appsettings.json`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RetentionDays` | `int` | `90` | Number of days to retain message logs before automatic cleanup |
| `CleanupBatchSize` | `int` | `1000` | Maximum number of records to delete in a single cleanup batch (prevents long transactions) |
| `CleanupIntervalHours` | `int` | `24` | Interval (in hours) between automatic cleanup operations |
| `Enabled` | `bool` | `true` | Whether automatic cleanup is enabled |

**Example Configuration:**

```json
{
  "MessageLogRetention": {
    "RetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

**Dynamic Retention Override:**

The retention period can also be configured dynamically via the Settings page (`Advanced:MessageLogRetentionDays` setting). If set, this overrides the `appsettings.json` value.

### Feature Toggle

Message logging can be globally enabled or disabled via the Settings page:

**Setting Key:** `Features:MessageLoggingEnabled` (boolean)

- **Default:** `true`
- **Effect:** When disabled, no messages are logged regardless of user consent

### Background Services Configuration

The message log cleanup service respects the `BackgroundServices:MessageLogCleanupInitialDelayMinutes` setting for its initial delay before first execution.

**Configuration Section:** `BackgroundServices` in `appsettings.json`

```json
{
  "BackgroundServices": {
    "MessageLogCleanupInitialDelayMinutes": 5
  }
}
```

---

## Message Capture and Handling

### MessageLoggingHandler

The `MessageLoggingHandler` service (located at `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\MessageLoggingHandler.cs`) is responsible for capturing Discord messages in real-time and persisting them to the database.

#### Event Handling

The handler subscribes to the `DiscordSocketClient.MessageReceived` event and processes incoming messages asynchronously.

**Event Flow:**

1. **Message Received**: Discord.NET fires `MessageReceived` event
2. **Bot Message Filter**: Exclude messages sent by bots
3. **System Message Filter**: Exclude non-user messages (joins, pins, etc.)
4. **Feature Toggle Check**: Verify `Features:MessageLoggingEnabled` setting
5. **Consent Check**: Verify user has granted `MessageLogging` consent via `IConsentService`
6. **Message Capture**: Create `MessageLog` entity with message data
7. **Database Insert**: Persist message to database via `IMessageLogRepository`

#### Filtering Logic

The handler applies several filters to determine which messages to log:

```csharp
// Filter 1: Skip bot messages
if (message.IsAuthorBot)
    return;

// Filter 2: Skip system messages (only process user messages)
if (!message.IsUserMessage)
    return;

// Filter 3: Check global feature toggle
var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:MessageLoggingEnabled");
if (!isEnabled)
    return;

// Filter 4: Check user consent
var hasConsent = await consentService.HasConsentAsync(
    message.AuthorId,
    ConsentType.MessageLogging);
if (!hasConsent)
    return;
```

#### Consent Integration

Message logging requires explicit user consent for privacy compliance. The system uses the `IConsentService` to check whether a user has granted `ConsentType.MessageLogging` consent.

**Consent Check:**
- **Service:** `IConsentService.HasConsentAsync(ulong userId, ConsentType type)`
- **Consent Type:** `ConsentType.MessageLogging`
- **Behavior:** If user has not granted consent, their messages are silently skipped

Users can manage consent via:
- **Discord Command:** `/consent` (see consent module documentation)
- **Web UI:** Account > Privacy page (`/Account/Privacy`)

For more details on the consent system, see the user consent documentation.

#### Error Handling

The handler includes comprehensive error handling to ensure message logging failures don't crash the bot:

```csharp
try
{
    // ... message logging logic ...
}
catch (Exception ex)
{
    _logger.LogError(ex,
        "Failed to log message {MessageId} from user {AuthorId} in channel {ChannelId}",
        message.Id, message.AuthorId, message.ChannelId);

    // Error is logged but not propagated - bot continues operating
}
```

### MessageLogCleanupService

The `MessageLogCleanupService` background service (located at `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\MessageLogCleanupService.cs`) automatically deletes old message logs according to the retention policy.

#### Cleanup Process

1. **Initial Delay**: Waits for configured initial delay (default: 5 minutes) to allow app to fully start
2. **Check if Enabled**: Verifies `MessageLogRetentionOptions.Enabled` is `true`
3. **Get Retention Period**: Retrieves retention days from `Advanced:MessageLogRetentionDays` setting or falls back to `MessageLogRetentionOptions.RetentionDays`
4. **Calculate Cutoff Date**: `DateTime.UtcNow.AddDays(-retentionDays)`
5. **Batch Deletion**: Deletes messages older than cutoff in batches of `CleanupBatchSize` (default: 1000)
6. **Wait for Next Interval**: Sleeps for `CleanupIntervalHours` (default: 24 hours) before next cleanup

#### Batch Processing

To prevent long-running database transactions, the cleanup service deletes messages in batches:

```csharp
while (!cancellationToken.IsCancellationRequested)
{
    var batchDeleted = await repository.DeleteBatchOlderThanAsync(
        cutoffDate,
        batchSize,
        cancellationToken);

    if (batchDeleted == 0 || batchDeleted < batchSize)
        break; // No more records to delete

    totalDeleted += batchDeleted;
}
```

---

## Admin UI Pages

The admin UI provides two main pages for browsing and analyzing message logs.

### Message Logs List Page

**Route:** `/Admin/MessageLogs` (requires Admin role)

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Admin\MessageLogs\Index.cshtml`

The message logs list page provides powerful filtering and search capabilities for browsing logged messages.

#### Features

**Filtering Options:**

| Filter | Type | Description |
|--------|------|-------------|
| User ID | `ulong?` | Filter by Discord user ID |
| Guild ID | `ulong?` | Filter by Discord guild ID |
| Channel ID | `ulong?` | Filter by Discord channel ID |
| Source Type | `MessageSource?` | Filter by DirectMessage, ServerChannel, or All |
| From Date | `DateTime?` | Messages sent on or after this date |
| To Date | `DateTime?` | Messages sent on or before this date |
| Content Search | `string?` | Search message content (case-insensitive substring match) |

**Default Date Range:** If no dates are specified, the page defaults to showing messages from the last 7 days.

**Pagination:**

- **Page Size Options:** 25, 50, 100 messages per page
- **Default Page Size:** 25
- **Navigation:** First, Previous, Page Numbers, Next, Last

**Table Columns:**

| Column | Description |
|--------|-------------|
| Timestamp | When the message was sent (displayed in user's local timezone) |
| Author | User avatar placeholder, username, and Discord user ID |
| Guild | Guild name or "(DM)" for direct messages |
| Channel | Channel name or ID |
| Source | Badge indicating "DM" or "Server" |
| Content | Truncated message content (max ~200 chars) |
| Actions | View details button |

**Export to CSV:**

The page includes an "Export CSV" button that downloads filtered messages as a CSV file (see API endpoints below).

#### Query Parameters

All filters are passed as query parameters in the URL:

```
/Admin/MessageLogs?AuthorId=123456789&Source=ServerChannel&StartDate=2025-12-01&pageNumber=2
```

### Message Log Details Page

**Route:** `/Admin/MessageLogs/Details/{id}` (requires Admin role)

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Admin\MessageLogs\Details.cshtml`

The message log details page displays comprehensive information about a single logged message.

#### Details Displayed

- **Message ID**: Database log ID and Discord message ID (snowflake)
- **Author Information**: Username, Discord user ID
- **Timestamp Information**: When message was sent (Discord timestamp) and when it was logged to the database
- **Location**: Guild name and ID, channel name and ID (or "Direct Message" if DM)
- **Source**: DirectMessage or ServerChannel
- **Content**: Full message content (no truncation)
- **Metadata**: HasAttachments flag, HasEmbeds flag, ReplyToMessageId if applicable
- **Navigation Properties**: Links to user profile, guild details (if applicable)

#### Error Handling

If the message log ID doesn't exist, the page returns a `404 Not Found` response.

---

## API Endpoints

The `MessagesController` (located at `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\MessagesController.cs`) exposes RESTful API endpoints for message log access and management.

**Base Route:** `/api/messages`

**Authorization:** All endpoints require `RequireAdmin` policy (Admin role or higher) unless otherwise noted.

### GET /api/messages

Retrieves paginated message logs with optional filters.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `AuthorId` | `ulong?` | No | null | Filter by Discord user ID |
| `GuildId` | `ulong?` | No | null | Filter by Discord guild ID |
| `ChannelId` | `ulong?` | No | null | Filter by Discord channel ID |
| `Source` | `MessageSource?` | No | null | Filter by DirectMessage (1) or ServerChannel (2) |
| `StartDate` | `DateTime?` | No | null | Messages sent on or after this date |
| `EndDate` | `DateTime?` | No | null | Messages sent on or before this date |
| `SearchTerm` | `string?` | No | null | Search message content (case-insensitive) |
| `Page` | `int` | No | 1 | Page number (1-based) |
| `PageSize` | `int` | No | 25 | Number of results per page (max: 100) |

**Validation:**

- If `StartDate` > `EndDate`, returns `400 Bad Request`
- `Page` defaults to 1 if < 1
- `PageSize` defaults to 25 if < 1 or > 100

**Response: 200 OK**

```json
{
  "items": [
    {
      "id": 12345,
      "discordMessageId": 1234567890123456789,
      "authorId": 987654321098765432,
      "authorUsername": "JohnDoe",
      "channelId": 1111111111111111111,
      "channelName": null,
      "guildId": 2222222222222222222,
      "guildName": "My Discord Server",
      "source": 2,
      "content": "Hello, world!",
      "timestamp": "2025-12-30T10:30:00Z",
      "timestampUtcIso": "2025-12-30T10:30:00.0000000Z",
      "loggedAt": "2025-12-30T10:30:01Z",
      "loggedAtUtcIso": "2025-12-30T10:30:01.0000000Z",
      "hasAttachments": false,
      "hasEmbeds": false,
      "replyToMessageId": null
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 150,
  "totalPages": 6,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

**Response: 400 Bad Request**

```json
{
  "message": "Invalid date range",
  "detail": "Start date cannot be after end date.",
  "statusCode": 400,
  "traceId": "00-abc123..."
}
```

**Example Request:**

```bash
GET /api/messages?GuildId=2222222222222222222&Source=2&Page=1&PageSize=50
```

### GET /api/messages/{id}

Retrieves a single message log entry by its database ID.

**Route Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | `long` | Yes | Database message log ID |

**Response: 200 OK**

```json
{
  "id": 12345,
  "discordMessageId": 1234567890123456789,
  "authorId": 987654321098765432,
  "authorUsername": "JohnDoe",
  "channelId": 1111111111111111111,
  "channelName": null,
  "guildId": 2222222222222222222,
  "guildName": "My Discord Server",
  "source": 2,
  "content": "Hello, world!",
  "timestamp": "2025-12-30T10:30:00Z",
  "timestampUtcIso": "2025-12-30T10:30:00.0000000Z",
  "loggedAt": "2025-12-30T10:30:01Z",
  "loggedAtUtcIso": "2025-12-30T10:30:01.0000000Z",
  "hasAttachments": false,
  "hasEmbeds": false,
  "replyToMessageId": null
}
```

**Response: 404 Not Found**

```json
{
  "message": "Message not found",
  "detail": "No message log with ID 12345 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123..."
}
```

### GET /api/messages/stats

Retrieves comprehensive message statistics including counts, source breakdowns, and daily trends.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `guildId` | `ulong?` | No | null | Filter statistics by guild; null returns global stats |

**Response: 200 OK**

```json
{
  "totalMessages": 15000,
  "dmMessages": 2500,
  "serverMessages": 12500,
  "uniqueAuthors": 250,
  "messagesByDay": [
    {
      "date": "2025-12-30",
      "count": 450
    },
    {
      "date": "2025-12-29",
      "count": 520
    },
    {
      "date": "2025-12-28",
      "count": 380
    }
  ],
  "oldestMessage": "2025-10-01T12:00:00Z",
  "newestMessage": "2025-12-30T14:30:00Z"
}
```

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `totalMessages` | `long` | Total number of logged messages |
| `dmMessages` | `long` | Number of direct messages |
| `serverMessages` | `long` | Number of server channel messages |
| `uniqueAuthors` | `int` | Number of distinct users who have sent logged messages |
| `messagesByDay` | `array` | Daily message counts for last 7 days |
| `oldestMessage` | `DateTime?` | Timestamp of oldest message in database |
| `newestMessage` | `DateTime?` | Timestamp of newest message in database |

### DELETE /api/messages/user/{userId}

Deletes all message logs for a specific user. This endpoint supports GDPR "right to erasure" data deletion requests.

**Authorization:** Requires `SuperAdmin` role

**Route Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | `ulong` | Yes | Discord user ID whose messages should be deleted |

**Response: 200 OK**

```json
{
  "deletedCount": 1250
}
```

**Example Request:**

```bash
DELETE /api/messages/user/987654321098765432
Authorization: Bearer <token>
```

**Logging:**

This operation is logged with `LogInformation` level for audit trail purposes:

```
GDPR deletion requested for user 987654321098765432
Deleted 1250 message logs for user 987654321098765432
```

### POST /api/messages/cleanup

Manually triggers cleanup of old message logs according to the configured retention policy. Useful for testing or forcing cleanup outside the regular schedule.

**Authorization:** Requires `SuperAdmin` role

**Response: 200 OK**

```json
{
  "deletedCount": 5000
}
```

**Behavior:**

- Deletes messages older than the retention period (from settings or configuration)
- Uses batch deletion with `CleanupBatchSize` batches (default: 1000)
- Respects `MessageLogRetentionOptions.Enabled` setting

**Example Request:**

```bash
POST /api/messages/cleanup
Authorization: Bearer <token>
```

### GET /api/messages/export

Exports message logs matching the query criteria to a CSV file.

**Query Parameters:** Same as `GET /api/messages` (AuthorId, GuildId, ChannelId, Source, StartDate, EndDate, SearchTerm)

**Response: 200 OK (text/csv)**

```csv
Id,DiscordMessageId,AuthorId,ChannelId,GuildId,Source,Content,Timestamp,LoggedAt,HasAttachments,HasEmbeds,ReplyToMessageId
12345,1234567890123456789,987654321098765432,1111111111111111111,2222222222222222222,ServerChannel,"Hello, world!",2025-12-30T10:30:00.0000000Z,2025-12-30T10:30:01.0000000Z,False,False,
12346,1234567890123456790,987654321098765432,1111111111111111111,2222222222222222222,ServerChannel,"How are you?",2025-12-30T10:31:00.0000000Z,2025-12-30T10:31:01.0000000Z,False,False,1234567890123456789
```

**Response Headers:**

```
Content-Type: text/csv
Content-Disposition: attachment; filename="message-logs-20251230143000.csv"
```

**CSV Fields:**

All fields from `MessageLog` entity are included. Message content is properly escaped for CSV format (quotes, commas, newlines).

**Export Limitations:**

- Maximum 10,000 records per export
- If query matches more than 10,000 records, only the first 10,000 are exported (warning logged)
- Consider adding filters to reduce result set if needed

**Example Request:**

```bash
GET /api/messages/export?GuildId=2222222222222222222&StartDate=2025-12-01&EndDate=2025-12-31
```

---

## MessageLogService

The `MessageLogService` (located at `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\MessageLogService.cs`) implements the `IMessageLogService` interface and provides business logic for message log operations.

### Service Interface (IMessageLogService)

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IMessageLogService.cs`

#### Methods

| Method | Description |
|--------|-------------|
| `GetLogsAsync(MessageLogQueryDto, CancellationToken)` | Retrieves paginated message logs with optional filtering |
| `GetByIdAsync(long, CancellationToken)` | Gets a single message log entry by database ID |
| `GetStatsAsync(ulong?, CancellationToken)` | Gets comprehensive statistics (counts, breakdowns, trends) |
| `DeleteUserMessagesAsync(ulong, CancellationToken)` | Deletes all messages for a user (GDPR compliance) |
| `CleanupOldMessagesAsync(CancellationToken)` | Cleans up old messages according to retention policy |
| `ExportToCsvAsync(MessageLogQueryDto, CancellationToken)` | Exports filtered messages to CSV format |

### Key Features

#### Pagination and Filtering

The service validates and normalizes pagination parameters:

```csharp
// Validate pagination
if (query.Page < 1)
    query.Page = 1;

if (query.PageSize < 1 || query.PageSize > 100)
    query.PageSize = 25;
```

#### Entity to DTO Mapping

The service converts `MessageLog` entities to `MessageLogDto` for API responses:

```csharp
private static MessageLogDto MapToDto(MessageLog entity)
{
    return new MessageLogDto
    {
        Id = entity.Id,
        DiscordMessageId = entity.DiscordMessageId,
        AuthorId = entity.AuthorId,
        AuthorUsername = entity.User?.Username,
        ChannelId = entity.ChannelId,
        ChannelName = null, // Not stored in database
        GuildId = entity.GuildId,
        GuildName = entity.Guild?.Name,
        Source = entity.Source,
        Content = entity.Content,
        Timestamp = entity.Timestamp,
        LoggedAt = entity.LoggedAt,
        HasAttachments = entity.HasAttachments,
        HasEmbeds = entity.HasEmbeds,
        ReplyToMessageId = entity.ReplyToMessageId
    };
}
```

#### CSV Export

The `ExportToCsvAsync` method generates CSV output with proper escaping for special characters:

```csharp
private static string EscapeCsvField(string field)
{
    if (string.IsNullOrEmpty(field))
        return "";

    // If field contains quotes, commas, or newlines, wrap in quotes and escape internal quotes
    if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
    {
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    return field;
}
```

#### Retention Cleanup

The cleanup method respects configuration and deletes in batches:

```csharp
var options = _retentionOptions.Value;

if (!options.Enabled)
    return 0;

var cutoffDate = DateTime.UtcNow.AddDays(-options.RetentionDays);

// Delete in batches to prevent long-running transactions
while (!cancellationToken.IsCancellationRequested)
{
    var batchDeleted = await _repository.DeleteBatchOlderThanAsync(
        cutoffDate,
        options.CleanupBatchSize,
        cancellationToken);

    if (batchDeleted == 0 || batchDeleted < options.CleanupBatchSize)
        break;

    totalDeleted += batchDeleted;
}
```

---

## DTOs (Data Transfer Objects)

### MessageLogDto

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\MessageLogDto.cs`

Represents message log data for API responses and UI views.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `long` | Database log entry ID |
| `DiscordMessageId` | `ulong` | Discord message snowflake ID |
| `AuthorId` | `ulong` | Discord user ID of author |
| `AuthorUsername` | `string?` | Display username (nullable, loaded from navigation property) |
| `ChannelId` | `ulong` | Discord channel ID |
| `ChannelName` | `string?` | Channel name (nullable, not stored in database) |
| `GuildId` | `ulong?` | Discord guild ID (null for DMs) |
| `GuildName` | `string?` | Guild name (nullable, loaded from navigation property) |
| `Source` | `MessageSource` | DirectMessage (1) or ServerChannel (2) |
| `Content` | `string` | Message text content |
| `Timestamp` | `DateTime` | When message was sent on Discord (UTC) |
| `TimestampUtcIso` | `string` | ISO 8601 formatted timestamp for client-side timezone conversion |
| `LoggedAt` | `DateTime` | When message was logged to database (UTC) |
| `LoggedAtUtcIso` | `string` | ISO 8601 formatted logged timestamp |
| `HasAttachments` | `bool` | Whether message included attachments |
| `HasEmbeds` | `bool` | Whether message included embeds |
| `ReplyToMessageId` | `ulong?` | Discord message ID this is replying to (nullable) |

### MessageLogQueryDto

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\MessageLogQueryDto.cs`

Represents query parameters for filtering and paginating message logs.

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AuthorId` | `ulong?` | null | Filter by Discord user ID |
| `GuildId` | `ulong?` | null | Filter by Discord guild ID |
| `ChannelId` | `ulong?` | null | Filter by Discord channel ID |
| `Source` | `MessageSource?` | null | Filter by DirectMessage or ServerChannel |
| `StartDate` | `DateTime?` | null | Messages on or after this date |
| `EndDate` | `DateTime?` | null | Messages on or before this date |
| `SearchTerm` | `string?` | null | Search message content (case-insensitive) |
| `Page` | `int` | 1 | Page number (1-based) |
| `PageSize` | `int` | 25 | Results per page (max: 100) |

### MessageLogStatsDto

Represents aggregate statistics about message logs.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `TotalMessages` | `long` | Total number of logged messages |
| `DmMessages` | `long` | Count of direct messages |
| `ServerMessages` | `long` | Count of server channel messages |
| `UniqueAuthors` | `int` | Number of distinct users who sent messages |
| `MessagesByDay` | `List<DailyMessageCount>` | Daily message counts for last 7 days |
| `OldestMessage` | `DateTime?` | Timestamp of oldest message in database |
| `NewestMessage` | `DateTime?` | Timestamp of newest message in database |

---

## Privacy and GDPR Compliance

### User Consent

Message logging is consent-based. Users must explicitly grant `ConsentType.MessageLogging` consent for their messages to be logged.

**Consent Granting Methods:**

1. **Discord Command:** `/consent` - Interactive slash command with component buttons
2. **Web UI:** `/Account/Privacy` - Privacy settings page (requires Discord OAuth login)

**Consent Service Integration:**

The `MessageLoggingHandler` checks consent before logging each message:

```csharp
var hasConsent = await consentService.HasConsentAsync(
    message.AuthorId,
    ConsentType.MessageLogging);

if (!hasConsent)
{
    _logger.LogDebug("User {AuthorId} has not granted message logging consent, skipping message {MessageId}",
        message.AuthorId, message.Id);
    return;
}
```

### Data Retention and Deletion

**Automatic Retention Policy:**

- Default retention: 90 days
- Configurable via `MessageLogRetention:RetentionDays` in `appsettings.json`
- Can be overridden via `Advanced:MessageLogRetentionDays` setting (dynamic)
- Automatic cleanup runs every 24 hours (configurable)

**Manual Deletion:**

SuperAdmins can manually delete all messages for a specific user via:

- **API Endpoint:** `DELETE /api/messages/user/{userId}`
- **Purpose:** GDPR "right to erasure" compliance

**Consent Revocation:**

When a user revokes `MessageLogging` consent:

- **Existing Messages:** Not automatically deleted (historical data)
- **Future Messages:** Will no longer be logged
- **User Action Required:** User must request manual deletion of existing messages via SuperAdmin

**Recommendations for GDPR Compliance:**

1. Implement a user-facing "Delete My Data" request form in the web UI
2. Create an admin workflow for processing deletion requests
3. Document retention policies in privacy policy and terms of service
4. Audit log all deletion operations for compliance tracking
5. Consider automatically deleting messages when consent is revoked (policy decision)

### What Data is NOT Collected

To minimize privacy impact:

- **Attachment Content**: Images, files, and other attachments are not stored (only metadata flag)
- **Embed Content**: Rich embed data is not stored (only metadata flag)
- **Edit History**: Message edits are not tracked (only original message)
- **Deleted Messages**: If a message is deleted on Discord, it remains in the log (historical record)
- **User IP Addresses**: No IP tracking for message authors
- **Metadata**: No geolocation, device info, or user agent data

---

## Testing and Development

### Repository Tests

The `MessageLogRepository` is tested via `MessageLogRepositoryTests` with in-memory SQLite:

- Pagination and filtering logic
- Date range queries
- Content search (case-insensitive)
- Batch deletion for retention cleanup
- Statistics aggregation

### Service Tests

The `MessageLogService` is tested via `MessageLogServiceTests`:

- Query parameter validation and normalization
- DTO mapping
- CSV export formatting and escaping
- Retention cleanup batch processing
- GDPR deletion operations

### Handler Tests

The `MessageLoggingHandler` is tested via `MessageLoggingHandlerTests`:

- Bot message filtering
- System message filtering
- Feature toggle behavior
- Consent checking
- Error handling and resilience

---

## Related Documentation

- **[API Endpoints Reference](api-endpoints.md)** - Full REST API documentation
- **[Settings Page](settings-page.md)** - Configuration management including message logging toggle
- **[Authorization Policies](authorization-policies.md)** - Role requirements for admin pages
- **[Database Schema](database-schema.md)** - Complete database schema documentation
- **User Consent Documentation** - (To be created) Consent management system

---

## Troubleshooting

### Messages Not Being Logged

**Possible Causes:**

1. **User Consent Not Granted**: Check if user has granted `MessageLogging` consent via `/consent` command
2. **Feature Disabled**: Verify `Features:MessageLoggingEnabled` setting is `true` in Settings page
3. **Bot Message**: Bot messages are intentionally excluded from logging
4. **System Message**: System messages (joins, pins, etc.) are excluded
5. **Database Error**: Check application logs for exceptions in `MessageLoggingHandler`

**Diagnostic Steps:**

```bash
# Check application logs
tail -f logs/discordbot-*.log | grep "MessageLoggingHandler"

# Check if feature is enabled (requires SQL access)
sqlite3 discordbot.db "SELECT Value FROM Settings WHERE Key = 'Features:MessageLoggingEnabled';"

# Check user consent (requires SQL access)
sqlite3 discordbot.db "SELECT * FROM UserConsents WHERE DiscordUserId = 123456789 AND ConsentType = 1;"
```

### Cleanup Not Running

**Possible Causes:**

1. **Cleanup Disabled**: `MessageLogRetention:Enabled` is set to `false`
2. **No Old Messages**: All messages are within retention period
3. **Service Not Started**: Background service failed to start

**Diagnostic Steps:**

```bash
# Check if cleanup service is running
# Look for log entries like "Message log cleanup service starting"
grep "MessageLogCleanupService" logs/discordbot-*.log

# Manually trigger cleanup via API
curl -X POST http://localhost:5000/api/messages/cleanup \
  -H "Authorization: Bearer <token>"
```

### Export Fails or Times Out

**Possible Causes:**

1. **Too Many Records**: Query matches more than 10,000 records
2. **Large Content**: Messages with very large content cause CSV generation to be slow
3. **Database Lock**: Database is locked by another operation

**Solutions:**

- Add more filters (date range, guild, user) to reduce result set
- Export in smaller batches (e.g., one month at a time)
- Run export during low-traffic periods

---

## Future Enhancements

Potential improvements for the message logging system:

1. **Message Edit Tracking**: Log message edits as separate entries with references to original
2. **Message Deletion Tracking**: Track when messages are deleted on Discord
3. **Attachment Metadata**: Store attachment filenames, sizes, and types (without content)
4. **Embed Metadata**: Store embed titles and types for better search
5. **Full-Text Search**: Implement Elasticsearch or SQLite FTS5 for advanced search
6. **Analytics Dashboard**: Visualize message trends, top authors, peak times
7. **Export Formats**: Support JSON, Excel, and Parquet export formats
8. **Automated Deletion on Consent Revocation**: Policy option to auto-delete when user revokes consent
9. **Message Reactions**: Track reactions added to messages
10. **Thread Support**: Enhanced tracking for threaded conversations

---

**See Also:**

- [API Endpoints Reference](api-endpoints.md)
- [Settings Page Documentation](settings-page.md)
- [Database Schema](database-schema.md)
- [Authorization Policies](authorization-policies.md)
