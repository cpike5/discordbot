# Reminder System - Entity and Repository Design

**Version:** 1.0
**Created:** 2025-12-31
**Status:** Draft
**Feature:** Reminder System

---

## Overview

This document specifies the entity model, enumerations, and repository interfaces for the Reminder system. The design enables users to set personal reminders via Discord slash commands that are delivered via DM at the scheduled time.

## Design Goals

1. **User-centric reminders**: Each reminder belongs to a single user and is delivered privately via DM
2. **Reliable delivery**: Track delivery status, attempts, and errors for debugging and retry logic
3. **Guild context**: Store guild/channel context for audit trail and potential fallback messaging
4. **Status tracking**: Clear lifecycle from pending → delivered/failed/cancelled
5. **Query efficiency**: Repository methods optimized for common queries (due reminders, user reminders, guild history)

---

## Entity Design

### Reminder Entity

**Location:** `src/DiscordBot.Core/Entities/Reminder.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user-scheduled reminder that will be delivered via Discord DM at a specified time.
/// </summary>
public class Reminder
{
    /// <summary>
    /// Unique identifier for this reminder.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where the reminder was created.
    /// Used for context and audit trail.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord channel snowflake ID where the reminder command was issued.
    /// Used for context and potential fallback messaging.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Discord user snowflake ID who set the reminder.
    /// The reminder will be delivered to this user via DM.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Reminder message content (max 500 characters).
    /// This is the message the user will receive when the reminder fires.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the reminder should fire and be delivered.
    /// </summary>
    public DateTime TriggerAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was successfully delivered to the user.
    /// Null if the reminder is still pending or failed to deliver.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Current status of the reminder.
    /// </summary>
    public ReminderStatus Status { get; set; }

    /// <summary>
    /// Number of times delivery has been attempted.
    /// Used to prevent infinite retry loops.
    /// </summary>
    public int DeliveryAttempts { get; set; }

    /// <summary>
    /// Error message from the most recent failed delivery attempt.
    /// Null if no errors have occurred.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Navigation property for the guild where this reminder was created.
    /// </summary>
    public Guild? Guild { get; set; }
}
```

### Database Schema

**Table:** `Reminders`

| Column | Type | Constraints | Index |
|--------|------|-------------|-------|
| Id | GUID | PK | Clustered |
| GuildId | BIGINT | NOT NULL | Yes (guild queries) |
| ChannelId | BIGINT | NOT NULL | - |
| UserId | BIGINT | NOT NULL | Yes (user queries) |
| Message | NVARCHAR(500) | NOT NULL | - |
| TriggerAt | DATETIME2 | NOT NULL | Yes (due reminder queries) |
| CreatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | - |
| DeliveredAt | DATETIME2 | NULL | - |
| Status | INT | NOT NULL, DEFAULT 0 | Yes (status filtering) |
| DeliveryAttempts | INT | NOT NULL, DEFAULT 0 | - |
| LastError | NVARCHAR(MAX) | NULL | - |

**Foreign Keys:**
- GuildId → Guilds.Id (optional cascade delete)

**Composite Indexes:**
- `IX_Reminders_Status_TriggerAt` (Status, TriggerAt) - Critical for background service polling
- `IX_Reminders_UserId_Status_TriggerAt` (UserId, Status, TriggerAt) - User reminder queries
- `IX_Reminders_GuildId_CreatedAt` (GuildId, CreatedAt DESC) - Guild audit queries

---

## Enumerations

### ReminderStatus

**Location:** `src/DiscordBot.Core/Enums/ReminderStatus.cs`

```csharp
namespace DiscordBot.Core.Enums;

/// <summary>
/// Status of a reminder throughout its lifecycle.
/// </summary>
public enum ReminderStatus
{
    /// <summary>
    /// Reminder is scheduled and awaiting delivery.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Reminder was successfully delivered to the user via DM.
    /// </summary>
    Delivered = 1,

    /// <summary>
    /// Reminder delivery failed after maximum retry attempts.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Reminder was cancelled by the user before delivery.
    /// </summary>
    Cancelled = 3
}
```

**Status Transitions:**

```
Pending → Delivered (successful DM delivery)
Pending → Failed (max delivery attempts reached)
Pending → Cancelled (user cancellation)
```

**Terminal States:** Delivered, Failed, Cancelled (no further state changes)

---

## Repository Interface

### IReminderRepository

**Location:** `src/DiscordBot.Core/Interfaces/IReminderRepository.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for Reminder entity operations.
/// Extends base repository with reminder-specific query methods.
/// </summary>
public interface IReminderRepository : IRepository<Reminder>
{
    /// <summary>
    /// Gets all pending reminders that are due for delivery (TriggerAt <= UtcNow).
    /// Orders by TriggerAt ascending (oldest first).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of due reminders.</returns>
    Task<IEnumerable<Reminder>> GetDueRemindersAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets paginated reminders for a specific user, optionally filtered to pending only.
    /// Orders by TriggerAt descending (newest first).
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="pendingOnly">If true, only return pending reminders. If false, return all statuses.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (reminders for page, total count across all pages).</returns>
    Task<(IEnumerable<Reminder> Items, int TotalCount)> GetByUserAsync(
        ulong userId,
        int page,
        int pageSize,
        bool pendingOnly = true,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of pending reminders for a specific user.
    /// Used to enforce per-user limits.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Count of pending reminders.</returns>
    Task<int> GetPendingCountByUserAsync(ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Gets paginated reminders for a specific guild, optionally filtered by status.
    /// Orders by CreatedAt descending (newest first).
    /// Used for guild moderation/audit purposes.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="status">Optional status filter. If null, returns all statuses.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (reminders for page, total count across all pages).</returns>
    Task<(IEnumerable<Reminder> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        ReminderStatus? status = null,
        CancellationToken ct = default);
}
```

### Repository Implementation Notes

**Implementation Location:** `src/DiscordBot.Infrastructure/Repositories/ReminderRepository.cs`

**Key Implementation Details:**

1. **GetDueRemindersAsync:**
   ```csharp
   return await _context.Reminders
       .Where(r => r.Status == ReminderStatus.Pending && r.TriggerAt <= DateTime.UtcNow)
       .OrderBy(r => r.TriggerAt)
       .ToListAsync(ct);
   ```
   - Only fetch Pending reminders to avoid reprocessing
   - Use indexed Status and TriggerAt columns for fast queries
   - Order by TriggerAt ensures oldest reminders fire first

2. **GetByUserAsync:**
   ```csharp
   var query = _context.Reminders.Where(r => r.UserId == userId);
   if (pendingOnly)
       query = query.Where(r => r.Status == ReminderStatus.Pending);

   var totalCount = await query.CountAsync(ct);
   var items = await query
       .OrderByDescending(r => r.TriggerAt)
       .Skip((page - 1) * pageSize)
       .Take(pageSize)
       .ToListAsync(ct);

   return (items, totalCount);
   ```

3. **GetPendingCountByUserAsync:**
   ```csharp
   return await _context.Reminders
       .CountAsync(r => r.UserId == userId && r.Status == ReminderStatus.Pending, ct);
   ```
   - Use this to enforce `MaxRemindersPerUser` limit before allowing creation

---

## Data Validation

### Entity-Level Constraints

**Reminder.cs validation attributes:**

```csharp
[Required]
[MaxLength(500, ErrorMessage = "Reminder message cannot exceed 500 characters")]
public string Message { get; set; } = string.Empty;

[Required]
public DateTime TriggerAt { get; set; }

[Range(0, int.MaxValue)]
public int DeliveryAttempts { get; set; }
```

### Business Rule Validation

Validation to be enforced in `IReminderService` implementation:

1. **Message length:** 1-500 characters (trim whitespace before validation)
2. **TriggerAt constraints:**
   - Must be in the future (TriggerAt > DateTime.UtcNow)
   - Cannot exceed `MaxAdvanceDays` from current date
   - Must be at least `MinAdvanceMinutes` in the future
3. **User limits:** User cannot have more than `MaxRemindersPerUser` pending reminders
4. **Status transitions:** Only allow valid state transitions (e.g., cannot cancel a delivered reminder)

---

## Error Handling Considerations

### Common Failure Scenarios

1. **User has DMs disabled:**
   - Increment `DeliveryAttempts`
   - Set `LastError` to exception message
   - Retry up to `MaxDeliveryAttempts`
   - Set status to Failed if max attempts exceeded

2. **User no longer exists / bot blocked:**
   - Immediately set status to Failed (no retry)
   - Log warning with user ID

3. **Database connection failure:**
   - Allow exception to bubble up to background service
   - Service will retry on next polling interval

4. **Discord API rate limiting:**
   - Respect rate limit headers
   - Delay retry attempt instead of incrementing counter immediately

### Logging Requirements

Log at these severity levels:

- **Information:** Successful delivery, reminder created
- **Warning:** Failed delivery attempt (with attempts remaining), user reached reminder limit
- **Error:** Delivery failed permanently, unexpected exceptions
- **Debug:** Due reminder check results, delivery attempt details

---

## Future Considerations

### Potential Enhancements

1. **Recurring reminders:** Add `RecurrencePattern` (daily, weekly, custom cron)
2. **Snooze functionality:** Allow users to delay reminder by N minutes
3. **Reminder templates:** Pre-defined reminder messages users can select
4. **Channel fallback:** If DM fails, optionally post reminder in original channel (opt-in)
5. **Timezone-aware display:** Show trigger time in user's preferred timezone in confirmations
6. **Attachment support:** Allow attaching images/links to reminder messages
7. **Reminder groups:** Tag reminders for organization (work, personal, etc.)
8. **Shared reminders:** Allow multiple users to subscribe to a single reminder
9. **Edit functionality:** Allow users to modify pending reminders
10. **Statistics:** Track reminder completion rate, average delay, etc.

### Migration Path

If adding recurring reminders in the future:

- Add nullable `RecurrencePattern` column to Reminders table
- Add `ParentReminderId` for tracking recurrence series
- Modify `GetDueRemindersAsync` to handle recurrence logic
- Update status transitions to allow Delivered → Pending for recurring items

---

## Related Documentation

- [Reminder Execution Service Design](reminder-execution-design.md) - Background service implementation
- [Time Parsing Service Design](time-parsing-service-design.md) - Time input parsing specification
- Entity Framework Core migration guide (TBD)
- API endpoint documentation (TBD)
- Discord slash command module (TBD)

---

## Dependencies

**NuGet Packages:**
- Microsoft.EntityFrameworkCore (existing)
- Discord.Net (existing)

**Existing Systems:**
- Guild entity (foreign key relationship)
- Background service infrastructure (pattern from ScheduledMessageExecutionService)
- Repository pattern (IRepository<T> base interface)

---

## Acceptance Criteria

- [ ] Reminder entity created with all specified properties
- [ ] ReminderStatus enum created with all four states
- [ ] IReminderRepository interface defined with all five methods
- [ ] ReminderRepository implementation completed
- [ ] EF Core migration created for Reminders table with all indexes
- [ ] Unit tests for repository methods (GetDueRemindersAsync, GetByUserAsync, etc.)
- [ ] Database seeding for test data (optional)
- [ ] API documentation updated with Reminder model
