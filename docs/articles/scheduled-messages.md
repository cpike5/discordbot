# Scheduled Messages

**Last Updated:** 2025-12-30
**Feature Status:** Active
**Related Issues:** #312 (Documentation), Epic #303 (Documentation Overhaul)

---

## Overview

The Scheduled Messages feature enables administrators to create, manage, and automatically send recurring or one-time messages to Discord channels. This feature is useful for announcements, reminders, recurring events, and automated community engagement.

### Key Features

- **Multiple Schedule Frequencies**: One-time, hourly, daily, weekly, monthly, or custom cron expressions
- **Flexible Management**: Create, edit, enable/disable, and delete scheduled messages via both Discord commands and Admin UI
- **Timezone Support**: All execution times are stored in UTC with local timezone display in the Admin UI
- **Background Execution**: Automated message delivery via background service with configurable intervals
- **Concurrent Processing**: Multiple messages can be executed simultaneously with configurable limits
- **Timeout Protection**: Individual message executions have timeout safeguards to prevent resource exhaustion
- **Manual Execution**: Test or immediately send any scheduled message on-demand via slash command

### Use Cases

- **Server Announcements**: Daily welcome messages, event reminders, rule refreshers
- **Community Engagement**: Weekly discussion prompts, monthly feedback requests
- **Event Notifications**: Recurring event start times, registration deadlines
- **Moderation Reminders**: Periodic rule reminders, channel guidelines
- **Custom Schedules**: Complex timing via cron expressions (e.g., first Monday of each month)

---

## Slash Commands

All scheduled message commands require the `RequireAdmin` precondition and can only be used in guilds with active status.

### `/schedule-list`

Lists all scheduled messages for the current guild with pagination and interactive selection.

**Usage:**
```
/schedule-list
```

**Response:**
- Embed showing up to 10 scheduled messages with status icons, frequency, and next execution time
- Select menu to view detailed information for any message
- Message details include full content, channel, schedule configuration, creation info

**Example Output:**
```
📅 Scheduled Messages (Page 1/1)

✅ Daily Welcome Message
Frequency: Daily
Next: in 3 hours
ID: 550e8400-e29b-41d4-a716-446655440000

❌ Weekly Event Reminder
Frequency: Weekly
Next: Not scheduled
ID: 6ba7b810-9dad-11d1-80b4-00c04fd430c8
```

### `/schedule-create`

Creates a new scheduled message with specified frequency and content.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `title` | String | Yes | Descriptive title for the scheduled message (max 200 characters) |
| `channel` | Text Channel | Yes | Discord channel where the message will be sent |
| `message` | String | Yes | Content of the message to send (max 2000 characters - Discord limit) |
| `frequency` | ScheduleFrequency | Yes | How often the message should be sent (see [Schedule Frequencies](#schedule-frequencies)) |
| `cron` | String | Conditional | Cron expression (required only for `Custom` frequency, max 100 characters) |

**Usage Examples:**

```
# Daily message
/schedule-create title:"Daily Welcome" channel:#general message:"Welcome to our server!" frequency:Daily

# Weekly reminder
/schedule-create title:"Event Reminder" channel:#announcements message:"Weekly event starts in 1 hour!" frequency:Weekly

# Custom schedule (every Monday and Friday at 9 AM)
/schedule-create title:"Workweek Reminder" channel:#general message:"Good morning!" frequency:Custom cron:"0 9 * * 1,5"
```

**Response:**
- Success embed with message details, next execution timestamp, and message ID
- Preview of message content (first 500 characters)
- Error embed if validation fails (invalid cron, missing parameters, etc.)

**Validation:**
- Title and message content length limits enforced
- Channel must be a valid text-capable channel in the guild
- Cron expression validated if frequency is `Custom`
- Next execution time calculated and validated before creation

### `/schedule-delete`

Deletes a scheduled message with confirmation prompt.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | String | Yes | The GUID identifier of the scheduled message to delete |

**Usage:**
```
/schedule-delete id:550e8400-e29b-41d4-a716-446655440000
```

**Response:**
- Confirmation prompt with message details and confirmation/cancel buttons
- Success message after confirmation
- Error if message not found or belongs to different guild

**Security:**
- Verifies the message belongs to the current guild
- Requires button confirmation before deletion
- Interactive components expire after 15 minutes (default)

### `/schedule-toggle`

Enables or disables a scheduled message without deleting it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | String | Yes | The GUID identifier of the scheduled message to enable/disable |

**Usage:**
```
/schedule-toggle id:550e8400-e29b-41d4-a716-446655440000
```

**Response:**
- Success embed showing the new enabled/disabled state
- Status icon (✅ Enabled / ❌ Disabled)
- Error if message not found or belongs to different guild

**Behavior:**
- Toggles the `IsEnabled` flag
- Disabled messages are skipped during background execution checks
- Does not affect the `NextExecutionAt` timestamp
- Useful for temporarily pausing messages without losing schedule configuration

### `/schedule-run`

Executes a scheduled message immediately (manual trigger).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | String | Yes | The GUID identifier of the scheduled message to execute |

**Usage:**
```
/schedule-run id:550e8400-e29b-41d4-a716-446655440000
```

**Response:**
- Success embed confirming message was sent to the target channel
- Error if message not found, channel inaccessible, or execution fails

**Behavior:**
- Sends message to Discord immediately
- Updates `LastExecutedAt` timestamp
- Calculates and updates `NextExecutionAt` based on frequency
- For `Once` frequency, disables the message after execution
- Does NOT wait for scheduled time - executes immediately

**Use Cases:**
- Testing new scheduled messages
- Sending unscheduled announcements using existing templates
- Recovering from missed executions

---

## Schedule Frequencies

The `ScheduleFrequency` enum defines how often a scheduled message is sent. All timestamps are stored and calculated in UTC.

### Frequency Types

| Frequency | Enum Value | Behavior | Next Execution Calculation |
|-----------|------------|----------|---------------------------|
| **Once** | 1 | Executes a single time at the specified timestamp, then disables | `null` (message disabled after execution) |
| **Hourly** | 2 | Executes every hour | Current time + 1 hour |
| **Daily** | 3 | Executes once per day | Current time + 1 day |
| **Weekly** | 4 | Executes once per week | Current time + 7 days |
| **Monthly** | 5 | Executes once per month | Current time + 1 month |
| **Custom** | 6 | Executes according to a cron expression | Calculated from cron expression using Cronos library |

### Frequency Details

#### Once
- **Use Case**: One-time announcements, event start notifications
- **Behavior**: Message is automatically disabled after execution
- **Example**: "Server maintenance in 1 hour" scheduled for specific timestamp

#### Hourly
- **Use Case**: Frequent reminders, status updates
- **Calculation**: Simple +1 hour from last execution
- **Example**: "Remember to check your DMs!" every hour

#### Daily
- **Use Case**: Daily digests, routine reminders
- **Calculation**: Simple +24 hours from last execution
- **Example**: "Daily server rules reminder" at the same time each day

#### Weekly
- **Use Case**: Weekly events, recurring announcements
- **Calculation**: Simple +7 days from last execution
- **Example**: "Weekly game night tonight!" every Saturday

#### Monthly
- **Use Case**: Monthly events, recurring administrative tasks
- **Calculation**: +1 month from last execution (handles month-end edge cases)
- **Example**: "Monthly community feedback survey" on the 1st of each month

#### Custom (Cron)
- **Use Case**: Complex schedules, specific day/time combinations
- **Library**: Uses [Cronos](https://github.com/HangfireIO/Cronos) for cron parsing and calculation
- **Format**: Standard cron expression (minute, hour, day-of-month, month, day-of-week)
- **Validation**: Cron expression validated before message creation/update
- **Examples**:
  - `0 9 * * 1-5` - Every weekday at 9:00 AM
  - `0 12 1 * *` - First day of every month at noon
  - `30 14 * * 0` - Every Sunday at 2:30 PM
  - `0 */3 * * *` - Every 3 hours

**Cron Expression Resources:**
- [Crontab.guru](https://crontab.guru/) - Cron expression validator and examples
- [Cronos Documentation](https://github.com/HangfireIO/Cronos) - Library documentation

---

## Timezone Handling

All scheduled message timestamps are stored in UTC to ensure consistency across servers and timezones.

### Storage and Calculation

- **Database Storage**: All `NextExecutionAt` and `LastExecutedAt` timestamps stored as UTC `DateTime`
- **Background Service**: Executes checks and calculations in UTC
- **Frequency Calculations**: All simple frequency calculations (hourly, daily, etc.) use UTC timestamps
- **Cron Evaluation**: Cron expressions evaluated against UTC time using Cronos library

### Admin UI Display

The Admin UI converts UTC timestamps to the user's local timezone for display:

- **JavaScript Conversion**: `timezone.js` utility converts ISO 8601 UTC timestamps to local time
- **datetime-local Inputs**: Form inputs for next execution time use browser's local timezone
- **Display Format**: Timestamps shown in user's local timezone with clear formatting
- **Submission Conversion**: User's local time converted back to UTC via `TimezoneHelper.ConvertToUtc()`

### Example Flow

1. **User creates message** via Admin UI:
   - User selects "Next Execution: 2024-01-15 9:00 AM" in their local timezone (PST = UTC-8)
   - Browser sends `UserTimezone` offset with form submission
   - `TimezoneHelper.ConvertToUtc()` converts to UTC: "2024-01-15 17:00:00Z"
   - UTC timestamp stored in database

2. **Background service checks**:
   - Service queries messages where `NextExecutionAt <= DateTime.UtcNow`
   - Executes message at UTC timestamp
   - Calculates next execution in UTC based on frequency

3. **User views message** via Admin UI:
   - Database returns UTC timestamp: "2024-01-22 17:00:00Z"
   - JavaScript converts to user's timezone: "2024-01-22 9:00 AM PST"
   - Displayed in local timezone

### Best Practices

- Always review execution times in the Admin UI after creation to ensure correct timezone conversion
- Use `/schedule-run` command to test messages before scheduled execution
- For cron expressions, consider UTC time when defining schedules (or calculate local offset)
- Document expected execution times in message titles for clarity (e.g., "Daily 9AM PST Reminder")

---

## Admin UI Pages

The scheduled messages feature provides three Razor Pages for web-based management with full CRUD operations.

### Scheduled Messages List (`/Guilds/ScheduledMessages/{guildId}`)

**Route:** `/Guilds/ScheduledMessages/{guildId:long}`
**Authorization:** `RequireAdmin` policy
**Page Model:** `DiscordBot.Bot.Pages.Guilds.ScheduledMessages.IndexModel`

#### Features

- **Paginated List**: Display scheduled messages with customizable page size (default: 20, max: 100)
- **Status Indicators**: Visual icons for enabled/disabled state
- **Quick Actions**: Toggle enable/disable, delete with inline confirmation
- **Channel Resolution**: Displays channel names resolved from Discord client
- **Guild Context**: Guild icon and name displayed in page header
- **Success/Error Messages**: TempData-based feedback for actions

#### Display Columns

| Column | Description |
|--------|-------------|
| **Title** | Message title with status icon (✅/❌) |
| **Channel** | Target Discord channel name (e.g., "#general") |
| **Frequency** | Schedule frequency (e.g., "Daily", "Custom (0 9 * * 1,5)") |
| **Next Execution** | Local timestamp of next scheduled execution |
| **Status** | Enabled/Disabled badge |
| **Actions** | Edit, Toggle, Delete buttons |

#### Actions

- **Edit**: Navigate to edit page
- **Toggle**: POST to `OnPostToggleAsync` to enable/disable
- **Delete**: POST to `OnPostDeleteAsync` with confirmation prompt

#### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | int | 1 | Page number (1-based) |
| `pageSize` | int | 20 | Number of items per page (1-100) |

#### Example URL
```
/Guilds/ScheduledMessages/123456789012345678?page=2&pageSize=50
```

### Create Scheduled Message (`/Guilds/ScheduledMessages/Create/{guildId}`)

**Route:** `/Guilds/ScheduledMessages/Create/{guildId:long}`
**Authorization:** `RequireAdmin` policy
**Page Model:** `DiscordBot.Bot.Pages.Guilds.ScheduledMessages.CreateModel`

#### Form Fields

| Field | Type | Validation | Description |
|-------|------|------------|-------------|
| **Title** | String | Required, max 200 chars | Descriptive title for the message |
| **Message Content** | Textarea | Required, max 2000 chars | Discord message content (supports Discord markdown) |
| **Target Channel** | Select | Required | Channel dropdown populated from guild's text-capable channels |
| **Frequency** | Select | Required | Schedule frequency (Once, Hourly, Daily, Weekly, Monthly, Custom) |
| **Cron Expression** | String | Conditional, max 100 chars | Required when frequency is "Custom" |
| **Next Execution Time** | datetime-local | Required | When the message should first execute (local timezone) |
| **Active** | Checkbox | Optional, default: true | Whether the message is enabled immediately |

#### Validation

- **Server-Side Validation**:
  - All required fields enforced
  - Length limits checked
  - Cron expression validated for Custom frequency using `IScheduledMessageService.ValidateCronExpressionAsync()`
  - Channel ID must be valid and belong to the guild

- **Client-Side Validation**:
  - HTML5 form validation for required fields
  - Character count indicators for text fields
  - Cron expression help text and examples
  - Frequency change shows/hides cron input field

#### Channel Selection

The create page fetches available channels from the Discord client:

- **Text Channels**: Regular guild text channels
- **Announcement Channels**: News/announcement channels
- **Voice Channels**: Voice channels (which support text chat)
- **Stage Channels**: Stage channels (which support text chat)

Channels are displayed with type icons and sorted by position.

#### Timezone Handling

- **Input**: User enters time in their local timezone using `datetime-local` input
- **Hidden Field**: `UserTimezone` captures browser's timezone offset
- **Conversion**: Server converts local time to UTC using `TimezoneHelper.ConvertToUtc()`
- **Storage**: UTC timestamp stored in database

#### Success Flow

1. User fills out form and submits
2. Server validates all inputs
3. Cron expression validated if Custom frequency
4. Local time converted to UTC
5. Message created via `IScheduledMessageService.CreateAsync()`
6. Redirect to list page with success message

### Edit Scheduled Message (`/Guilds/ScheduledMessages/Edit/{guildId}/{id}`)

**Route:** `/Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}`
**Authorization:** `RequireAdmin` policy
**Page Model:** `DiscordBot.Bot.Pages.Guilds.ScheduledMessages.EditModel`

#### Features

- **Pre-Populated Form**: All fields loaded from existing message
- **Same Validation**: Identical validation rules as create page
- **Metadata Display**: Shows created date, last updated, last executed timestamps
- **Timezone Conversion**: Displays and accepts times in user's local timezone

#### Form Fields

All fields from create page, plus:

- **Created At**: Display-only, shows when message was created (local timezone)
- **Last Updated At**: Display-only, shows last modification time (local timezone)
- **Last Executed At**: Display-only, shows last execution time if message has run (local timezone)

#### Update Behavior

- Updates only modified fields via `ScheduledMessageUpdateDto`
- Recalculates `NextExecutionAt` if frequency or cron expression changes
- Updates `UpdatedAt` timestamp automatically
- Logs audit entry for update action

#### Example URL
```
/Guilds/ScheduledMessages/Edit/123456789012345678/550e8400-e29b-41d4-a716-446655440000
```

---

## Configuration Options

Scheduled messages behavior is configured via `ScheduledMessagesOptions` in `appsettings.json`.

### Configuration Section

**Section Name:** `ScheduledMessages`
**Options Class:** `DiscordBot.Core.Configuration.ScheduledMessagesOptions`
**Location:** `src/DiscordBot.Core/Configuration/ScheduledMessagesOptions.cs`

### Available Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `CheckIntervalSeconds` | int | 60 | Interval (in seconds) between scheduled message checks by background service |
| `MaxConcurrentExecutions` | int | 5 | Maximum number of messages to execute concurrently (prevents resource exhaustion) |
| `ExecutionTimeoutSeconds` | int | 30 | Timeout (in seconds) for individual message execution (prevents hanging) |

### Configuration Example

```json
{
  "ScheduledMessages": {
    "CheckIntervalSeconds": 60,
    "MaxConcurrentExecutions": 5,
    "ExecutionTimeoutSeconds": 30
  }
}
```

### Configuration Guidelines

#### CheckIntervalSeconds

- **Purpose**: How often the background service checks for due messages
- **Impact**: Lower values = more responsive but higher CPU/DB load
- **Recommendations**:
  - 60 seconds (default) - Good balance for most use cases
  - 30 seconds - High-frequency messages (hourly or more frequent)
  - 300 seconds (5 min) - Low-frequency messages (daily or less frequent)
- **Minimum**: 10 seconds (lower values may cause performance issues)

#### MaxConcurrentExecutions

- **Purpose**: Limit concurrent message sending to Discord
- **Impact**: Prevents rate limiting and resource exhaustion when many messages are due simultaneously
- **Recommendations**:
  - 5 (default) - Balanced for most servers
  - 10 - High-volume servers with many scheduled messages
  - 1 - Conservative setting to avoid any rate limit risk
- **Note**: Discord's rate limits are per-channel; multiple messages to different channels can execute in parallel safely

#### ExecutionTimeoutSeconds

- **Purpose**: Maximum time allowed for a single message execution
- **Impact**: Prevents individual message failures from blocking other executions
- **Recommendations**:
  - 30 seconds (default) - Sufficient for standard messages
  - 60 seconds - Large messages or slower network conditions
  - 10 seconds - Fast-fail for simple messages
- **Note**: Timeout includes channel lookup, message sending, and database updates

### Environment-Specific Configuration

Override settings in environment-specific configuration files:

```json
// appsettings.Production.json
{
  "ScheduledMessages": {
    "CheckIntervalSeconds": 30,     // More responsive in production
    "MaxConcurrentExecutions": 10,  // Higher capacity
    "ExecutionTimeoutSeconds": 60   // More generous timeout
  }
}
```

---

## Background Execution

Scheduled messages are automatically executed by the `ScheduledMessageExecutionService` background service.

### Service Overview

**Service Class:** `DiscordBot.Bot.Services.ScheduledMessageExecutionService`
**Base Class:** `BackgroundService` (Microsoft.Extensions.Hosting)
**Lifecycle:** Singleton, runs for application lifetime
**Dependencies:** `IServiceScopeFactory`, `IOptions<ScheduledMessagesOptions>`, `ILogger`

### Execution Flow

1. **Service Starts**:
   - Logs startup message with configuration details
   - Waits 10 seconds for Discord client to connect
   - Enters execution loop

2. **Check Cycle** (every `CheckIntervalSeconds`):
   - Creates scoped service provider for database access
   - Queries `IScheduledMessageRepository.GetDueMessagesAsync()` for messages where:
     - `NextExecutionAt <= DateTime.UtcNow`
     - `IsEnabled == true`
   - Logs count of due messages (or trace-level log if none)

3. **Concurrent Execution**:
   - Creates semaphore with `MaxConcurrentExecutions` limit
   - Spawns parallel tasks for each due message
   - Each task:
     - Acquires semaphore slot
     - Creates linked cancellation token with `ExecutionTimeoutSeconds`
     - Calls `IScheduledMessageService.ExecuteScheduledMessageAsync()`
     - Logs success or failure
     - Releases semaphore slot
   - Waits for all tasks to complete

4. **Individual Message Execution** (in `ScheduledMessageService`):
   - Fetches message from repository
   - Resolves Discord channel via `DiscordSocketClient.GetChannel()`
   - Sends message via `IMessageChannel.SendMessageAsync()`
   - Updates message timestamps:
     - `LastExecutedAt` = current UTC time
     - `NextExecutionAt` = calculated based on frequency
   - Disables message if frequency is `Once`
   - Logs audit entry for execution
   - Returns success/failure status

5. **Error Handling**:
   - Individual message failures logged but don't stop other executions
   - Timeout exceptions caught and logged separately
   - Database errors logged and retried on next cycle
   - Service shutdown handled gracefully via `CancellationToken`

### Execution Guarantees

**What is guaranteed:**
- Messages will execute at or after their scheduled time (within `CheckIntervalSeconds` precision)
- Failed executions are logged but don't affect other messages
- Timeouts prevent individual messages from blocking the service
- Graceful shutdown ensures in-progress executions complete

**What is NOT guaranteed:**
- Exact execution time (precision limited by `CheckIntervalSeconds`)
- Retry on failure (failed messages require manual intervention)
- Execution during application downtime (messages are not queued or backfilled)
- Execution if Discord channel is deleted or bot loses access

### Monitoring and Observability

The service emits structured logs at multiple levels:

- **Information**: Service start/stop, message execution results, processing counts
- **Debug**: Individual message execution attempts, configuration details
- **Warning**: Execution failures, message not found, channel inaccessible, timeouts
- **Error**: Unexpected exceptions, database errors, critical failures
- **Trace**: No due messages (only when none are due)

**Log Correlation**: All logs include `MessageId` for correlation with audit logs and UI

### Performance Considerations

- **Database Load**: Each check cycle queries for due messages (1 query per cycle)
- **Discord API**: Each message sends 1 API request (respects Discord rate limits via semaphore)
- **Memory**: Scoped service providers created per cycle (disposed after execution)
- **CPU**: Minimal CPU usage during waits, spikes during concurrent executions

**Optimization Tips**:
- Increase `CheckIntervalSeconds` if you have few scheduled messages
- Decrease `MaxConcurrentExecutions` if experiencing rate limit issues
- Monitor logs for timeout warnings and adjust `ExecutionTimeoutSeconds`

---

## Data Model

### Entity: ScheduledMessage

**Location:** `src/DiscordBot.Core/Entities/ScheduledMessage.cs`
**Table Name:** `ScheduledMessages` (EF Core default pluralization)

#### Properties

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key, unique identifier |
| `GuildId` | `ulong` | No | Discord guild snowflake ID |
| `ChannelId` | `ulong` | No | Discord channel snowflake ID where message will be sent |
| `Title` | `string` | No | Descriptive title (max 200 characters) |
| `Content` | `string` | No | Message content to send (max 2000 characters - Discord limit) |
| `CronExpression` | `string?` | Yes | Cron expression for custom schedules (null for non-custom frequencies) |
| `Frequency` | `ScheduleFrequency` | No | Schedule frequency enum value |
| `IsEnabled` | `bool` | No | Whether message is active (default: true) |
| `LastExecutedAt` | `DateTime?` | Yes | UTC timestamp of last execution (null if never executed) |
| `NextExecutionAt` | `DateTime?` | Yes | UTC timestamp of next execution (null for disabled/once messages) |
| `CreatedAt` | `DateTime` | No | UTC timestamp when message was created |
| `CreatedBy` | `string` | No | User identifier who created the message |
| `UpdatedAt` | `DateTime` | No | UTC timestamp of last update |
| `Guild` | `Guild?` | Yes | Navigation property to guild entity |

#### Indexes

- **Primary Key**: `Id` (Guid)
- **Foreign Key**: `GuildId` → `Guilds.GuildId`
- **Suggested Index**: `(GuildId, IsEnabled, NextExecutionAt)` for efficient due message queries

#### Constraints

- **GuildId**: Must reference valid guild in `Guilds` table
- **ChannelId**: No FK constraint (channel may be deleted in Discord)
- **Content**: Max length 2000 (Discord message limit)
- **Title**: Max length 200

### DTOs

#### ScheduledMessageDto

**Location:** `src/DiscordBot.Core/DTOs/ScheduledMessageDto.cs`
**Purpose:** Read model for scheduled messages

All properties from entity, mapped 1:1.

#### ScheduledMessageCreateDto

**Location:** `src/DiscordBot.Core/DTOs/ScheduledMessageCreateDto.cs`
**Purpose:** Input model for creating scheduled messages

**Properties:**

```csharp
public class ScheduledMessageCreateDto
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ScheduleFrequency Frequency { get; set; }
    public string? CronExpression { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime NextExecutionAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
```

#### ScheduledMessageUpdateDto

**Location:** `src/DiscordBot.Core/DTOs/ScheduledMessageUpdateDto.cs`
**Purpose:** Partial update model for scheduled messages

**Properties:** All nullable/optional to support partial updates

```csharp
public class ScheduledMessageUpdateDto
{
    public ulong? ChannelId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public ScheduleFrequency? Frequency { get; set; }
    public string? CronExpression { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? NextExecutionAt { get; set; }
}
```

---

## Service Interface

### IScheduledMessageService

**Location:** `src/DiscordBot.Core/Interfaces/IScheduledMessageService.cs`
**Implementation:** `DiscordBot.Bot.Services.ScheduledMessageService`
**Registration:** Scoped service

#### Methods

##### GetByIdAsync

```csharp
Task<ScheduledMessageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
```

Retrieves a single scheduled message by ID with guild navigation property.

**Parameters:**
- `id`: Message GUID
- `cancellationToken`: Optional cancellation token

**Returns:** DTO or null if not found

##### GetByGuildIdAsync

```csharp
Task<(IEnumerable<ScheduledMessageDto> Items, int TotalCount)> GetByGuildIdAsync(
    ulong guildId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default);
```

Retrieves paginated scheduled messages for a guild.

**Parameters:**
- `guildId`: Guild snowflake ID
- `page`: Page number (1-based)
- `pageSize`: Number of items per page
- `cancellationToken`: Optional cancellation token

**Returns:** Tuple of (paginated items, total count)

##### CreateAsync

```csharp
Task<ScheduledMessageDto> CreateAsync(
    ScheduledMessageCreateDto dto,
    CancellationToken cancellationToken = default);
```

Creates a new scheduled message with validation and audit logging.

**Parameters:**
- `dto`: Creation request DTO
- `cancellationToken`: Optional cancellation token

**Returns:** Created message DTO

**Throws:**
- `ArgumentException`: If cron expression invalid or required parameters missing

##### UpdateAsync

```csharp
Task<ScheduledMessageDto?> UpdateAsync(
    Guid id,
    ScheduledMessageUpdateDto dto,
    CancellationToken cancellationToken = default);
```

Updates an existing scheduled message (partial update).

**Parameters:**
- `id`: Message GUID
- `dto`: Update request DTO (only non-null fields updated)
- `cancellationToken`: Optional cancellation token

**Returns:** Updated DTO or null if not found

**Throws:**
- `ArgumentException`: If cron expression invalid

##### DeleteAsync

```csharp
Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
```

Deletes a scheduled message.

**Parameters:**
- `id`: Message GUID
- `cancellationToken`: Optional cancellation token

**Returns:** True if deleted, false if not found

##### CalculateNextExecutionAsync

```csharp
Task<DateTime?> CalculateNextExecutionAsync(
    ScheduleFrequency frequency,
    string? cronExpression,
    DateTime? baseTime = null);
```

Calculates the next execution time based on frequency.

**Parameters:**
- `frequency`: Schedule frequency
- `cronExpression`: Cron expression (required for Custom frequency)
- `baseTime`: Optional base time (defaults to current UTC)

**Returns:** Next execution timestamp or null if frequency is Once or calculation fails

##### ExecuteScheduledMessageAsync

```csharp
Task<bool> ExecuteScheduledMessageAsync(Guid id, CancellationToken cancellationToken = default);
```

Executes a scheduled message immediately by sending to Discord and updating state.

**Parameters:**
- `id`: Message GUID
- `cancellationToken`: Optional cancellation token

**Returns:** True if execution successful, false if message not found or send fails

**Side Effects:**
- Sends message to Discord channel
- Updates `LastExecutedAt` timestamp
- Calculates and updates `NextExecutionAt`
- Disables message if frequency is Once
- Logs audit entry

##### ValidateCronExpressionAsync

```csharp
Task<(bool IsValid, string? ErrorMessage)> ValidateCronExpressionAsync(string cronExpression);
```

Validates a cron expression for correctness using Cronos library.

**Parameters:**
- `cronExpression`: Cron expression string

**Returns:** Tuple of (validity, error message if invalid)

---

## API Endpoints

Currently, there are no dedicated REST API endpoints for scheduled messages. All operations are performed via:

- **Discord Slash Commands**: For in-Discord management
- **Admin UI Razor Pages**: For web-based management

**Future Enhancement (Tracked in Epic #303):**

Potential future API endpoints:

```
GET    /api/guilds/{guildId}/scheduledmessages       # List scheduled messages
POST   /api/guilds/{guildId}/scheduledmessages       # Create scheduled message
GET    /api/scheduledmessages/{id}                   # Get single message
PUT    /api/scheduledmessages/{id}                   # Update message
DELETE /api/scheduledmessages/{id}                   # Delete message
POST   /api/scheduledmessages/{id}/execute           # Execute immediately
POST   /api/scheduledmessages/{id}/toggle            # Enable/disable
```

See [api-endpoints.md](api-endpoints.md) for current REST API documentation.

---

## Error Handling and Edge Cases

### Common Error Scenarios

#### Channel Deleted or Inaccessible

**Scenario:** Target channel deleted from Discord or bot loses access permissions

**Behavior:**
- Background service logs error: "Channel {ChannelId} not found for scheduled message {MessageId}"
- Message execution marked as failed
- `NextExecutionAt` NOT updated (will retry on next cycle)
- Message remains enabled

**Resolution:**
- Admin edits message to select valid channel
- Admin disables or deletes message if channel permanently unavailable

#### Bot Not in Guild

**Scenario:** Bot removed from guild while scheduled messages exist

**Behavior:**
- Background service cannot resolve channel
- Message execution fails silently
- Database retains messages (for potential bot re-invite)

**Resolution:**
- Messages automatically work again if bot re-invited to guild
- Admin can manually clean up messages via database query

#### Invalid Cron Expression

**Scenario:** Cron expression becomes invalid due to library update or edge case

**Behavior:**
- Validation fails during creation/update
- User receives error embed with validation message
- Message not created/updated

**Resolution:**
- User corrects cron expression
- System provides helpful error message from Cronos library

#### Concurrent Execution Overflow

**Scenario:** More messages due than `MaxConcurrentExecutions` limit

**Behavior:**
- Semaphore limits concurrent executions
- Excess messages wait for semaphore slot
- All messages eventually execute (within timeout limit)

**Resolution:**
- Increase `MaxConcurrentExecutions` if this occurs frequently
- Stagger message schedules to avoid simultaneous execution

#### Execution Timeout

**Scenario:** Message execution takes longer than `ExecutionTimeoutSeconds`

**Behavior:**
- Execution cancelled via linked `CancellationToken`
- Warning logged: "Scheduled message execution timed out after {Timeout}s: {MessageId}"
- `NextExecutionAt` NOT updated (will retry on next cycle)

**Resolution:**
- Increase `ExecutionTimeoutSeconds` if legitimate
- Investigate slow network or Discord API issues
- Simplify message content if too large

#### Frequency Change After Creation

**Scenario:** User changes frequency from Daily to Custom without providing cron

**Behavior:**
- Validation fails during update
- Error returned: "Cron expression is required when frequency is Custom"
- Update rejected

**Resolution:**
- User provides valid cron expression
- Or changes frequency to non-Custom value

### Data Integrity

#### Orphaned Messages

**Scenario:** Guild deleted from database but messages remain

**Prevention:**
- Foreign key constraint on `GuildId`
- Cascade delete configured in EF Core

**Behavior:** Messages automatically deleted when guild deleted

#### Duplicate Execution Prevention

**Scenario:** Multiple background service instances running (distributed deployment)

**Current Behavior:**
- No built-in distributed lock mechanism
- Multiple instances may execute same message

**Future Enhancement:**
- Implement distributed lock (Redis, SQL Server App Lock, etc.)
- Add `LastExecutionBy` field to track executing instance

---

## Testing Scheduled Messages

### Manual Testing via Slash Commands

1. **Create a test message** with Once frequency and near-future execution:
   ```
   /schedule-create title:"Test" channel:#test-channel message:"Test message" frequency:Once
   ```

2. **Verify creation** via list command:
   ```
   /schedule-list
   ```

3. **Test immediate execution** without waiting:
   ```
   /schedule-run id:550e8400-e29b-41d4-a716-446655440000
   ```

4. **Verify message appears** in target channel

5. **Clean up** test message:
   ```
   /schedule-delete id:550e8400-e29b-41d4-a716-446655440000
   ```

### Testing Custom Cron Expressions

1. **Use a near-future cron** for immediate testing:
   - Example: Current time is 14:45 UTC
   - Cron: `50 14 * * *` (executes at 14:50 UTC, 5 minutes from now)

2. **Verify calculation** in Admin UI:
   - Create message with custom cron
   - Check "Next Execution" timestamp matches expectation

3. **Test validation** with invalid cron:
   ```
   /schedule-create title:"Invalid" channel:#test message:"Test" frequency:Custom cron:"invalid cron"
   ```
   - Should receive error embed with validation message

### Testing Frequency Calculations

1. **Hourly**: Create message, note next execution, verify it increments by 1 hour after execution
2. **Daily**: Verify 24-hour increment
3. **Weekly**: Verify 7-day increment
4. **Monthly**: Test month-end edge cases (e.g., Jan 31 → Feb 28)

### Admin UI Testing

1. **Pagination**: Create 25+ messages, verify pagination controls work
2. **Toggle**: Disable message, verify status icon changes and background service skips it
3. **Edit**: Modify message, verify changes persist and next execution recalculated
4. **Timezone**: Create message in different timezones, verify display matches local time

### Load Testing

1. **Create multiple messages** with same execution time
2. **Monitor logs** during execution
3. **Verify concurrency** respects `MaxConcurrentExecutions`
4. **Check for timeout warnings**

---

## Security Considerations

### Authorization

- **Slash Commands**: Require `RequireAdmin` precondition (Admin role or higher)
- **Admin UI Pages**: Require `RequireAdmin` authorization policy
- **Guild Isolation**: All operations verify message belongs to current guild
- **Component Interactions**: Verify user ID matches component creator

### Input Validation

- **Length Limits**: Title (200), Content (2000), Cron (100)
- **Cron Injection**: Validated via Cronos library, cannot execute arbitrary code
- **XSS Prevention**: Razor Pages automatically encode output
- **SQL Injection**: EF Core parameterized queries

### Rate Limiting

- **Discord API**: Semaphore limits concurrent requests to prevent rate limiting
- **User Commands**: Discord's built-in command rate limiting applies
- **Background Service**: Configurable execution limits prevent abuse

### Sensitive Data

- **No PII**: Scheduled messages do not store user PII
- **Guild Isolation**: Messages scoped to guilds, not visible cross-guild
- **Audit Logging**: All create/update/delete operations logged

---

## Troubleshooting

### Messages Not Executing

**Check:**
1. Background service running: Check application logs for "Scheduled message execution service starting"
2. Message enabled: Verify `IsEnabled = true` in database or Admin UI
3. Next execution time: Verify `NextExecutionAt` is in the past (UTC)
4. Channel accessible: Verify bot has permission to send messages in channel
5. Configuration: Check `CheckIntervalSeconds` not set too high

**Logs to Review:**
```
# Service startup
Scheduled message execution service enabled. Check interval: 60s, Max concurrent: 5, Timeout: 30s

# Due message detection
Found 3 scheduled messages due for execution

# Individual execution
Executing scheduled message {MessageId}: {Title}
Successfully executed scheduled message {MessageId}: {Title}
```

### Cron Expression Not Working

**Check:**
1. Frequency set to Custom
2. Cron expression valid: Test at [crontab.guru](https://crontab.guru/)
3. UTC time zone: Cron evaluated against UTC, not local time
4. Next execution calculated: Check `NextExecutionAt` after creation

**Example Debugging:**
```
# Create message with verbose logging
/schedule-create ... frequency:Custom cron:"0 9 * * 1-5"

# Check logs
Calculated next execution for Custom: 2024-01-15 09:00:00Z
```

### Messages Executing Multiple Times

**Check:**
1. Single background service instance: Verify not running multiple app instances
2. Check interval: Verify reasonable `CheckIntervalSeconds` value
3. Logs: Look for duplicate execution logs with same `MessageId`

### Timezone Display Issues

**Check:**
1. Browser timezone: Ensure browser reports correct timezone
2. JavaScript enabled: `timezone.js` requires JavaScript
3. ISO format: Verify UTC timestamps in ISO 8601 format
4. Hidden field: Check `UserTimezone` field populated on form submit

**Debugging:**
```javascript
// In browser console
console.log(new Date().toString());  // Shows browser timezone
console.log(document.querySelector('[name="Input.UserTimezone"]').value);  // Shows submitted offset
```

### Performance Issues

**Symptoms:**
- Slow Admin UI list page
- High database load
- Execution delays

**Solutions:**
1. Increase `CheckIntervalSeconds` to reduce query frequency
2. Add database index on `(GuildId, IsEnabled, NextExecutionAt)`
3. Reduce `MaxConcurrentExecutions` if experiencing rate limits
4. Paginate Admin UI results (reduce `pageSize`)

---

## Best Practices

### Message Content

- **Keep it concise**: Shorter messages load faster and are easier to read
- **Use Discord markdown**: Bold, italics, code blocks for formatting
- **Test first**: Use `/schedule-run` to preview before scheduling
- **Mention sparingly**: Avoid @everyone/@here in frequent messages
- **Include context**: Message should make sense without prior context

### Scheduling

- **Stagger executions**: Avoid scheduling many messages at the same time
- **Use appropriate frequency**: Don't use Hourly for daily announcements
- **Test cron expressions**: Verify next execution time after creation
- **Document complex crons**: Include explanation in message title
- **Consider timezones**: Remember all times stored in UTC

### Management

- **Descriptive titles**: Use clear, searchable titles (e.g., "Daily Welcome - 9AM PST")
- **Disable, don't delete**: Disable messages for temporary pauses
- **Regular audits**: Review scheduled messages periodically
- **Clean up old messages**: Delete one-time messages after execution
- **Monitor logs**: Watch for execution failures

### Performance

- **Batch creates**: Use Admin UI for bulk message creation
- **Reasonable limits**: Don't create hundreds of messages per guild
- **Monitor execution**: Check logs for timeout warnings
- **Adjust configuration**: Tune `CheckIntervalSeconds` and `MaxConcurrentExecutions` based on load

---

## Related Documentation

- **[Commands Page](commands-page.md)**: Admin UI for viewing registered slash commands
- **[Settings Page](settings-page.md)**: Global application settings
- **[API Endpoints](api-endpoints.md)**: REST API documentation
- **[Interactive Components](interactive-components.md)**: Discord button/component patterns used in schedule commands
- **[Authorization Policies](authorization-policies.md)**: Role hierarchy and guild access
- **[Form Implementation Standards](form-implementation-standards.md)**: Razor Pages form patterns used in Admin UI

---

## Future Enhancements

### Planned Features (Not Yet Implemented)

1. **Message Templates**: Reusable message content with variable substitution
2. **Attachment Support**: Schedule messages with images, files, embeds
3. **Conditional Execution**: Execute only if certain conditions met
4. **Execution History**: View past executions with timestamps and status
5. **Retry Logic**: Automatic retry on transient failures
6. **Distributed Lock**: Prevent duplicate execution in multi-instance deployments
7. **REST API Endpoints**: Programmatic access for external integrations
8. **Webhook Support**: Execute webhooks instead of bot messages
9. **Rich Embeds**: Visual scheduling with Discord embed builder
10. **Execution Notifications**: Alert admins on execution failures

### Enhancement Tracking

See Epic #303 (Documentation Overhaul for v0.4.0) and related feature issues in GitHub Issues.

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-30 | Initial documentation for scheduled messages feature |

---

**Document Status:** Complete
**Next Review:** 2025-03-30 (or when feature updated)
