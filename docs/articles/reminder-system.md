---
uid: reminder-system
title: Reminder System
description: Personal reminders delivered via Discord DM with natural language time parsing
---

# Reminder System

This document provides comprehensive documentation for the Discord bot's reminder system. Users can set personal reminders that are delivered via DM at a scheduled time using natural language time inputs.

## Overview

The reminder system allows Discord users to schedule personal reminders that are delivered via direct message at a specified time. The system features:

- **Natural language time parsing** - Set reminders using friendly formats like "10m", "tomorrow 3pm", "Dec 31 noon"
- **DM delivery** - Reminders sent privately via Discord direct message
- **Reliable execution** - Background service with retry logic and timeout protection
- **User limits** - Per-user reminder quotas to prevent abuse
- **Admin management** - Guild administrators can view and manage reminders via admin UI

Reminders are personal and private - only the user who set the reminder receives the DM notification.

---

## Commands

### /remind set <time> <message>

**Description:** Set a personal reminder to be delivered via DM at the specified time

**Parameters:**
- `time` (required) - When the reminder should trigger. Supports natural language formats (see [Time Format Reference](#time-format-reference))
- `message` (required) - Reminder message content (max 500 characters)

**Permission:** None (available to all guild members)

**Response Type:** Ephemeral embed message with confirmation

**Behavior:**
1. Parses the time input using natural language processing
2. Validates time is within allowed range (1 minute to 1 year in future)
3. Checks user has not exceeded reminder limit (25 pending by default)
4. Creates reminder in database with status `Pending`
5. Returns confirmation with trigger timestamp

**Usage Examples:**
```
/remind set time:10m message:Check the oven
/remind set time:tomorrow 3pm message:Meeting with team
/remind set time:Dec 31 message:New Year's Eve party
/remind set time:1h30m message:Take a break
```

**Success Response:**
```
✅ Reminder Set

Your reminder has been scheduled.

Trigger Time: December 31, 2024 3:00 PM PST (in 2 hours)
Message: Check the oven

Reminder ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Response Fields:**
- **Trigger Time** - Parsed timestamp in user's timezone with relative time
- **Message** - Echo of the reminder message
- **Reminder ID** - Unique identifier for cancellation

**Error Conditions:**
- "Time input cannot be empty" - Missing time parameter
- "Could not understand time format..." - Invalid time format
- "Time must be at least 1 minute(s) in the future" - Time too soon
- "Time cannot be more than 365 day(s) in the future" - Time too far
- "Reminder message cannot exceed 500 characters" - Message too long
- "You have reached the maximum of 25 pending reminders" - User limit exceeded

---

### /remind list [page]

**Description:** View your pending reminders with pagination

**Parameters:**
- `page` (optional) - Page number to view (default: 1)

**Permission:** None (available to all guild members)

**Response Type:** Ephemeral embed message with pagination

**Behavior:**
1. Queries user's pending reminders from database
2. Sorts by trigger time (soonest first)
3. Displays up to 5 reminders per page
4. Shows page navigation if more than 5 reminders exist

**Usage Examples:**
```
/remind list
/remind list page:2
```

**Sample Output (Page 1/2):**
```
📋 Your Pending Reminders (Page 1/2)

1. ⏰ In 10 minutes
   "Check the oven"
   ID: a1b2c3d4

2. ⏰ Tomorrow at 3:00 PM
   "Meeting with team"
   ID: b2c3d4e5

3. ⏰ Dec 31 at 12:00 PM
   "New Year's Eve party"
   ID: c3d4e5f6

4. ⏰ Jan 15 at 9:00 AM
   "Doctor appointment"
   ID: d4e5f6g7

5. ⏰ Feb 1 at 8:00 AM
   "Start new project"
   ID: e5f6g7h8

Total: 8 pending reminders

[Next Page]
```

**Response Fields:**
- **Trigger Time** - Relative or absolute time display
- **Message** - Reminder message (truncated to 100 chars if needed)
- **ID** - Short ID (first 8 characters) for cancellation
- **Total** - Total pending reminder count in footer
- **Pagination Buttons** - Previous/Next buttons for navigation

**Edge Cases:**
- If user has no pending reminders, displays "You have no pending reminders"
- If page number exceeds total pages, shows last page
- Pagination buttons disabled when on first/last page

---

### /remind cancel <id>

**Description:** Cancel a pending reminder

**Parameters:**
- `id` (required) - Reminder ID to cancel (supports autocomplete)

**Permission:** None (users can only cancel their own reminders)

**Response Type:** Ephemeral embed message with confirmation

**Autocomplete Behavior:**

When typing the ID parameter, the command provides autocomplete suggestions showing:
- First 8 characters of reminder ID
- Trigger time (relative)
- Message preview (truncated to 50 chars)

This makes it easy to select the correct reminder without manually copying IDs.

**Usage Examples:**
```
/remind cancel id:a1b2c3d4-e5f6-7890-abcd-ef1234567890
/remind cancel id:a1b2c3d4
```

**Note:** Full ID or first 8 characters accepted

**Success Response:**
```
✅ Reminder Cancelled

Your reminder has been cancelled.

Trigger Time: Tomorrow at 3:00 PM
Message: Meeting with team
```

**Error Conditions:**
- "Reminder not found" - Invalid ID or reminder doesn't exist
- "You can only cancel your own reminders" - ID belongs to another user
- "Reminder has already been delivered" - Cannot cancel completed reminder
- "Reminder has already been cancelled" - Already cancelled

---

## Time Format Reference

The reminder system supports multiple natural language time formats for user convenience. Times are parsed relative to the guild's configured timezone (defaults to UTC if not set).

### Supported Formats

| Format Category | Examples | Meaning |
|----------------|----------|---------|
| **Minutes** | `10m`, `30min`, `5 minutes` | N minutes from now |
| **Hours** | `2h`, `3hours`, `1 hour` | N hours from now |
| **Days** | `1d`, `3days`, `7 day` | N days from now |
| **Weeks** | `1w`, `2weeks`, `1 week` | N weeks from now (7 days each) |
| **Combined Duration** | `1h30m`, `2d 4h`, `1w 2d 3h 30m` | Combined time units |
| **12-Hour Time** | `10pm`, `3:30am`, `12:00pm` | Today or tomorrow at specific time |
| **24-Hour Time** | `22:00`, `14:30`, `9:00` | Today or tomorrow at specific time |
| **Tomorrow** | `tomorrow`, `tomorrow 3pm`, `tomorrow 14:30` | Next day at specific time |
| **Day of Week** | `monday`, `friday 9am`, `next thursday 10:30am` | Next occurrence of weekday |
| **Date** | `Dec 31`, `Jan 1 9am`, `March 15 22:00` | Specific date at specific time |
| **Full Date/Time** | `2024-12-31 10:00`, `12/31/2024 3:00 PM` | ISO 8601 or common date formats |

### Format Details

#### Relative Time

Relative times are calculated from the current UTC time.

**Examples:**
- `10m` → 10 minutes from now
- `2h` → 2 hours from now
- `1d` → 1 day (24 hours) from now
- `1w` → 1 week (7 days) from now
- `1h30m` → 1 hour and 30 minutes from now
- `2d 4h` → 2 days and 4 hours from now (space optional)
- `in 10m` → 10 minutes from now (optional "in" prefix)

**Rules:**
- Must include at least one time unit
- Units can be combined in any order
- Spaces between units are optional
- Supports short forms: `m`, `h`, `d`, `w`
- Supports long forms: `min`, `minutes`, `hour`, `hours`, `day`, `days`, `week`, `weeks`

#### Absolute Time (12-Hour)

Times specified in 12-hour format are interpreted in the guild's timezone. If the time has already passed today, it is scheduled for tomorrow.

**Examples:**
- `10pm` → 10:00 PM today (or tomorrow if past)
- `10:30pm` → 10:30 PM today (or tomorrow if past)
- `3:45am` → 3:45 AM today (or tomorrow if past)
- `12am` → Midnight (00:00)
- `12pm` → Noon (12:00)

**Rules:**
- Must include `am` or `pm` (case-insensitive)
- Hour must be 1-12
- Minutes are optional (defaults to :00)
- Minutes must be zero-padded if specified (e.g., `10:05pm`, not `10:5pm`)

#### Absolute Time (24-Hour)

Times specified in 24-hour format are interpreted in the guild's timezone. If the time has already passed today, it is scheduled for tomorrow.

**Examples:**
- `22:00` → 10:00 PM today (or tomorrow if past)
- `14:30` → 2:30 PM today (or tomorrow if past)
- `00:00` → Midnight
- `9:00` → 9:00 AM today (or tomorrow if past)

**Rules:**
- Must use colon separator (e.g., `22:00`)
- Hour must be 0-23
- Minutes must be 0-59
- Minutes must be zero-padded (e.g., `09:00`, not `9:0`)

#### Named Day

Day names combined with times schedule the reminder for the next occurrence of that day.

**Examples:**
- `tomorrow 3pm` → 3:00 PM tomorrow
- `monday 9am` → 9:00 AM next Monday
- `friday 22:00` → 10:00 PM next Friday
- `next thursday 10:30am` → 10:30 AM next Thursday (prefix "next" is optional)

**Supported Day Names:**
- `tomorrow` - Next day
- `monday`, `tuesday`, `wednesday`, `thursday`, `friday`, `saturday`, `sunday` - Next occurrence of weekday

**Rules:**
- Day name must come first
- Time portion follows same rules as absolute time formats
- If today is the named day and time has passed, schedules for next week

#### Date with Time

Month and day combined with time for specific calendar dates.

**Examples:**
- `Dec 31 noon` → December 31 at 12:00 PM
- `Jan 1 9am` → January 1 at 9:00 AM
- `March 15 22:00` → March 15 at 10:00 PM
- `12/31 3pm` → December 31 at 3:00 PM (numeric month)

**Supported Month Names:**
- Full: `January`, `February`, `March`, `April`, `May`, `June`, `July`, `August`, `September`, `October`, `November`, `December`
- Abbreviated: `Jan`, `Feb`, `Mar`, `Apr`, `May`, `Jun`, `Jul`, `Aug`, `Sep`, `Oct`, `Nov`, `Dec`

**Rules:**
- Month name (or number) comes first
- Day must be 1-31 (validated against month)
- Time follows same rules as absolute time formats
- Year is inferred (current year, or next year if date has passed)

#### Full Date/Time

Full date and time specifications using ISO 8601 or common formats.

**Examples:**
- `2024-12-31 10:00` → December 31, 2024 at 10:00 AM
- `12/31/2024 10:00 AM` → December 31, 2024 at 10:00 AM
- `31/12/2024 22:00` → December 31, 2024 at 10:00 PM

**Rules:**
- Uses .NET's `DateTime.TryParse()` for flexible parsing
- Interpreted in guild's timezone
- Supports various date formats based on server culture
- Year is required

**Warning:** Ambiguous formats (e.g., `01/02/2024`) may parse differently based on server culture settings. Prefer explicit formats like ISO 8601.

---

## Delivery System

### DM Delivery

Reminders are delivered to users via Discord direct message (DM) when the scheduled time is reached.

**Delivery Message Format:**

```
╔══════════════════════════════════════════════════════╗
║ ⏰ Reminder                              [Blue Color]║
╟──────────────────────────────────────────────────────╢
║                                                      ║
║ {reminder.Message}                                   ║
║                                                      ║
║ Scheduled For          Created                       ║
║ Dec 31, 2024 3:00 PM   5 hours ago                   ║
║                                                      ║
╟──────────────────────────────────────────────────────╢
║ Reminder ID: {reminder.Id}              [Timestamp] ║
╚══════════════════════════════════════════════════════╝
```

**Embed Properties:**
- **Title:** "⏰ Reminder"
- **Description:** Reminder message content
- **Color:** Blue
- **Fields:**
  - "Scheduled For" - Original trigger time (full timestamp)
  - "Created" - When reminder was created (relative time)
- **Footer:** Reminder ID for reference
- **Timestamp:** Current delivery time

### Retry Behavior

If delivery fails (e.g., user has DMs disabled), the system attempts retry:

**Retry Strategy:**
1. First delivery attempt at scheduled time
2. If failed, retry on next polling cycle (30 seconds default)
3. Maximum 3 delivery attempts (configurable)
4. Retry interval equals polling interval (no exponential backoff)

**Retry Logging:**
- Each attempt is logged with attempt number
- Error message captured in database
- Status remains `Pending` until max attempts or success

**Max Attempts Reached:**

After 3 failed attempts, the reminder status is set to `Failed` and no further delivery is attempted.

### Failed Delivery Handling

**Permanent Failures (No Retry):**
- User not found (deleted account or no mutual servers)
- User ID invalid

**Transient Failures (Retry):**
- User has DMs disabled (`CannotSendMessageToUser` error)
- Discord API rate limit (automatically handled by Discord.NET)
- Network timeout or temporary API error

**User Notification:**

Failed reminders are NOT re-sent to the guild channel. Users will not receive notification of failed delivery unless they check the admin UI or contact administrators.

**Future Enhancement:** Consider optional channel fallback or notification of failed delivery.

---

## Admin UI

Guild administrators can view and manage reminders for their guild via the admin web UI.

**Page Location:** `/Guilds/{guildId:long}/Reminders`

**Features:**
- **View all guild reminders** - Paginated list of all reminders created in the guild
- **Filter by status** - Pending, Delivered, Failed, Cancelled
- **Sort by trigger time** - Newest or soonest first
- **User information** - See which user created each reminder
- **Cancel reminders** - Administrators can cancel pending reminders
- **Delivery status** - View delivery attempts and error messages

**Authorization:**
- Requires Administrator permission in guild
- Administrators can view all reminders but only cancel pending ones
- Users cannot view other users' reminders (privacy)

**Use Cases:**
- Monitor reminder system health
- Investigate delivery failures
- Moderate inappropriate reminder content (future: profanity filter)
- Analyze reminder usage statistics

---

## Configuration Options

The reminder system is configured via `appsettings.json` under the `Reminder` section.

### ReminderOptions

**Configuration Section:** `Reminder`

**Location:** `src/DiscordBot.Core/Configuration/ReminderOptions.cs`

```json
{
  "Reminder": {
    "CheckIntervalSeconds": 30,
    "MaxConcurrentDeliveries": 5,
    "ExecutionTimeoutSeconds": 30,
    "MaxDeliveryAttempts": 3,
    "RetryDelayMinutes": 5,
    "MaxRemindersPerUser": 25,
    "MaxAdvanceDays": 365,
    "MinAdvanceMinutes": 1
  }
}
```

### Configuration Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `CheckIntervalSeconds` | int | 30 | Interval (in seconds) between reminder checks. Background service polls for due reminders at this frequency. |
| `MaxConcurrentDeliveries` | int | 5 | Maximum number of reminders to deliver concurrently. Prevents resource exhaustion when many reminders are due simultaneously. |
| `ExecutionTimeoutSeconds` | int | 30 | Timeout (in seconds) for individual reminder delivery. If delivery takes longer, it will be cancelled. |
| `MaxDeliveryAttempts` | int | 3 | Maximum number of delivery attempts before marking as failed. Used when user has DMs disabled or transient failures occur. |
| `RetryDelayMinutes` | int | 5 | Delay (in minutes) between retry attempts. Currently not used - retries happen on next poll interval. |
| `MaxRemindersPerUser` | int | 25 | Maximum number of pending reminders a user can have. Prevents abuse and resource exhaustion. |
| `MaxAdvanceDays` | int | 365 | Maximum number of days in advance a reminder can be scheduled (1 year default). |
| `MinAdvanceMinutes` | int | 1 | Minimum number of minutes in advance a reminder can be scheduled. Prevents immediate execution. |

### Adjusting Configuration

**For High Volume:**
- Decrease `CheckIntervalSeconds` (more frequent polls)
- Increase `MaxConcurrentDeliveries` (more parallel deliveries)
- Increase `MaxRemindersPerUser` (allow power users)

**For Low Resources:**
- Increase `CheckIntervalSeconds` (less frequent polls)
- Decrease `MaxConcurrentDeliveries` (less memory/CPU)
- Decrease `MaxRemindersPerUser` (reduce database load)

**For Reliability:**
- Increase `MaxDeliveryAttempts` (more retries)
- Increase `ExecutionTimeoutSeconds` (allow slow API calls)

---

## Limits and Restrictions

### User Limits

- **Max Pending Reminders:** 25 per user (configurable via `MaxRemindersPerUser`)
- **Max Message Length:** 500 characters
- **Min Advance Time:** 1 minute in the future (configurable via `MinAdvanceMinutes`)
- **Max Advance Time:** 365 days in the future (configurable via `MaxAdvanceDays`)

### System Limits

- **Polling Interval:** 30 seconds default (reminders may fire up to 30s late)
- **Concurrent Deliveries:** 5 default (more may queue)
- **Delivery Timeout:** 30 seconds per reminder
- **Max Retry Attempts:** 3 attempts before permanent failure

### Rate Limiting

Reminder commands do NOT have rate limiting by default. Consider adding if abuse is detected:

```csharp
[RateLimit(5, 60.0, RateLimitTarget.User)] // 5 reminder sets per user per 60 seconds
[SlashCommand("set", "Set a personal reminder")]
public async Task SetReminderAsync(string time, string message)
{
    // Command logic
}
```

---

## Related Documentation

### Design Documents

Design documents are available in the `docs/designs/` directory:

- `reminder-entity-design.md` - Database schema, entity model, repository interface
- `reminder-execution-design.md` - Background service, delivery logic, retry strategy
- `time-parsing-service-design.md` - Natural language time parsing specification

### Related Features

- [Admin Commands](admin-commands.md) - Administrative slash commands
- [Scheduled Messages](scheduled-messages.md) - Similar background service pattern
- [Database Schema](database-schema.md) - Database entity relationships

---

## Troubleshooting

### Reminder Not Delivered

**Symptom:** Reminder did not arrive via DM at scheduled time

**Possible Causes:**
1. User has DMs disabled for the server or globally
2. User deleted their Discord account
3. Bot is offline or restarting during trigger time
4. Delivery failed after max retry attempts

**Solutions:**
- Check DM settings: User Settings → Privacy & Safety → Allow direct messages from server members
- Check admin UI for reminder status and error message
- Verify bot is online and healthy
- Check bot logs for delivery errors

### Time Parsed Incorrectly

**Symptom:** Reminder scheduled for wrong time

**Possible Causes:**
1. Guild timezone not configured (defaults to UTC)
2. Ambiguous time format (e.g., "10:00" interpreted as AM instead of PM)
3. Time input uses unexpected format

**Solutions:**
- Configure guild timezone in guild settings
- Use explicit 12-hour format with AM/PM (e.g., "10pm" instead of "22:00")
- Use ISO 8601 format for unambiguous parsing (e.g., "2024-12-31 22:00")
- Check confirmation embed to verify parsed time before confirming

### Cannot Cancel Reminder

**Symptom:** `/remind cancel` command fails or reminder not found

**Possible Causes:**
1. Reminder ID is incorrect or truncated
2. Reminder already delivered or cancelled
3. Reminder belongs to different user

**Solutions:**
- Use `/remind list` to get correct reminder ID
- Use autocomplete to select reminder from list
- Check admin UI to verify reminder status
- Verify you are the owner of the reminder

### Reached Reminder Limit

**Symptom:** "You have reached the maximum of 25 pending reminders" error

**Possible Causes:**
1. User has 25 or more pending reminders
2. Old reminders never fired due to bot downtime

**Solutions:**
- Cancel old or unnecessary reminders using `/remind cancel`
- Wait for reminders to be delivered (reduces pending count)
- Contact administrator to manually cancel reminders
- Request administrator increase `MaxRemindersPerUser` limit

---

*Document Version: 1.0*
*Last Updated: January 2026*
*Status: v0.5.0 Implementation Complete*
