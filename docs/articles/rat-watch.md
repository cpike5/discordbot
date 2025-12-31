# Rat Watch Feature

**Last Updated:** 2025-12-30
**Feature Reference:** Issue #404 (Epic)
**Status:** Implemented

---

## Overview

Rat Watch is an accountability system that allows Discord users to hold each other accountable for commitments. When someone makes a promise (like "I'll go to the gym" or "I'll finish this task"), other users can set a "Rat Watch" on them to check if they follow through.

### Key Features

- **Context Menu Integration**: Right-click any message to start a Rat Watch
- **Flexible Scheduling**: Set check-in times using natural language (10m, 2h, 10pm)
- **Early Check-in**: Accused users can clear themselves before the deadline
- **Community Voting**: If the user doesn't check in, the community votes on guilt
- **Leaderboard**: Track the biggest "rats" in your server
- **Guild Settings**: Configurable timezone, voting duration, and advance scheduling limits

### How It Works

1. **Create Watch**: User A right-clicks User B's message and selects "Rat Watch"
2. **Schedule**: User A enters when to check (e.g., "2h" for 2 hours)
3. **Wait**: User B can click "I'm Here!" anytime before the deadline
4. **Check-in Time**: If User B doesn't check in, voting begins
5. **Vote**: Community votes "Rat" or "Not Rat" for 5 minutes
6. **Verdict**: Majority wins; guilty verdicts are recorded permanently

---

## Discord Commands

### Context Menu: Rat Watch

**Type:** Message Command
**Preconditions:** RequireGuildActive, RequireRatWatchEnabled

Right-click any message and select "Rat Watch" from the Apps menu to create a watch on that message's author.

**Modal Fields:**
- **When to check in** (required): Time expression like `10m`, `2h`, `1h30m`, `10pm`, or `22:00`
- **Custom message** (optional): Description of what the user committed to (max 200 chars)

**Time Parsing:**
| Format | Example | Meaning |
|--------|---------|---------|
| `Nm` | `10m`, `30m` | N minutes from now |
| `Nh` | `2h`, `4h` | N hours from now |
| `NhMm` | `1h30m` | N hours and M minutes from now |
| `Npm` | `10pm` | Next occurrence of 10 PM (guild timezone) |
| `Nam` | `9am` | Next occurrence of 9 AM (guild timezone) |
| `HH:mm` | `22:00` | Next occurrence of that time (24-hour, guild timezone) |

**Restrictions:**
- Cannot target bots
- Cannot schedule more than MaxAdvanceHours (default 24) in the future
- Must be in the future

### /rat-clear

**Description:** Clear yourself from all active Rat Watches in this server
**Preconditions:** RequireGuildActive, RequireRatWatchEnabled

Allows the accused user to check in and clear all their pending watches at once.

### /rat-stats [user]

**Description:** View a user's rat record
**Parameters:**
- `user` (optional): The user to check (defaults to yourself)
**Preconditions:** RequireGuildActive, RequireRatWatchEnabled

Shows:
- Total guilty verdict count
- Recent guilty records with dates and vote tallies
- Links to original messages

### /rat-leaderboard

**Description:** View the top rats in this server
**Preconditions:** RequireGuildActive, RequireRatWatchEnabled

Displays the top 10 users by guilty verdict count with medals for top 3.

### /rat-settings [timezone]

**Description:** View or configure Rat Watch settings
**Parameters:**
- `timezone` (optional): Set the timezone for parsing times like "10pm"
**Required Permissions:** Administrator
**Preconditions:** RequireGuildActive, RequireRatWatchEnabled

Available timezones:
- Eastern Time
- Central Time
- Mountain Time
- Pacific Time
- UTC

---

## Interactive Components

### Check-in Button

**Button:** "I'm Here! ✓" (Success style)
**Component ID:** `ratwatch:checkin:{accusedUserId}:{watchId}:` (built via `ComponentIdBuilder`)
**Handler:** `RatWatchComponentModule` with pattern `ratwatch:checkin:*:*:`

Only the accused user can click this button. Clears them from the watch early.

### Voting Buttons

**Buttons:** "🐀 Rat" (Danger style), "Not Rat" (Success style)
**Component ID Format:** `ratwatch:vote:{accusedUserId}:{watchId}:{guilty|notguilty}`
**Handler:** `RatWatchComponentModule` with pattern `ratwatch:vote:*:*:*`

Anyone except the accused can vote. Users can change their vote while voting is active.

---

## Admin UI

### Rat Watch Management Page

**URL:** `/Guilds/RatWatch/{guildId}`
**Authorization:** RequireAdmin policy

**Features:**
- View and edit guild settings (timezone, voting duration, max advance hours, enable/disable, public leaderboard)
- Summary statistics (total, pending, voting, completed watches)
- Hall of Shame leaderboard
- Paginated watch list with status, participants, scheduled times
- Cancel pending/voting watches

### Rat Watch Analytics Page

**URL:** `/Guilds/RatWatch/{guildId}/Analytics`
**Authorization:** RequireAdmin policy

**Features:**
- Summary metrics (total watches, guilty rate, early check-in rate, avg voting participation)
- Time series charts showing watch trends over time
- Activity heatmap by day/hour
- Top users leaderboards (most watched, top accusers, biggest rats)
- Fun stats (longest streaks, biggest landslides, closest calls)

### Rat Watch Incidents Page

**URL:** `/Guilds/RatWatch/{guildId}/Incidents`
**Authorization:** RequireAdmin policy

**Features:**
- Advanced filtering by status, date range, users, vote count, keyword
- Quick filters for today, last 7 days, last 30 days
- Sortable columns (scheduled time, created time, votes)
- Paginated incident browser
- Direct links to watch details

### Public Leaderboard Page

**URL:** `/Guilds/{guildId}/Leaderboard`
**Authorization:** None (public, requires `PublicLeaderboardEnabled` setting)

**Features:**
- Public-facing leaderboard for guilds that opt in
- Top rats with guilty counts
- Fun stats display
- No authentication required

### Global Rat Watch Analytics

**URL:** `/Admin/RatWatchAnalytics`
**Authorization:** RequireAdmin policy

**Features:**
- Cross-guild analytics for administrators
- Aggregate metrics across all guilds
- System-wide trends and patterns

---

## Database Schema

### RatWatches Table

| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| GuildId | INTEGER (ulong) | Discord guild snowflake ID |
| ChannelId | INTEGER (ulong) | Channel where watch was created |
| AccusedUserId | INTEGER (ulong) | User being watched |
| InitiatorUserId | INTEGER (ulong) | User who created the watch |
| OriginalMessageId | INTEGER (ulong) | Message that triggered the watch |
| CustomMessage | TEXT | Optional commitment description |
| ScheduledAt | DATETIME | When check-in is due (UTC) |
| CreatedAt | DATETIME | When watch was created (UTC) |
| Status | INTEGER | RatWatchStatus enum value |
| NotificationMessageId | INTEGER (ulong)? | Message with check-in button |
| VotingMessageId | INTEGER (ulong)? | Message with voting buttons |
| ClearedAt | DATETIME? | When accused cleared early |
| VotingStartedAt | DATETIME? | When voting began |
| VotingEndedAt | DATETIME? | When voting ended |

**Indexes:**
- IX_RatWatches_GuildId
- IX_RatWatches_GuildId_Status
- IX_RatWatches_AccusedUserId
- IX_RatWatches_ScheduledAt
- IX_RatWatches_Status

### RatVotes Table

| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| RatWatchId | GUID | Foreign key to RatWatches |
| VoterUserId | INTEGER (ulong) | User who cast the vote |
| IsGuiltyVote | BOOLEAN | True = Rat, False = Not Rat |
| VotedAt | DATETIME | When vote was cast (UTC) |

**Indexes:**
- IX_RatVotes_RatWatchId
- IX_RatVotes_RatWatchId_VoterUserId (unique)

### RatRecords Table

Permanent record of guilty verdicts.

| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| RatWatchId | GUID | Foreign key to RatWatches (unique) |
| GuildId | INTEGER (ulong) | Discord guild snowflake ID |
| UserId | INTEGER (ulong) | User who was found guilty |
| GuiltyVotes | INTEGER | Count of "Rat" votes |
| NotGuiltyVotes | INTEGER | Count of "Not Rat" votes |
| RecordedAt | DATETIME | When verdict was recorded (UTC) |
| OriginalMessageLink | TEXT? | Link to original commitment message |

**Indexes:**
- IX_RatRecords_GuildId
- IX_RatRecords_UserId
- IX_RatRecords_GuildId_UserId

### RatWatchStatus Enum

| Value | Name | Description |
|-------|------|-------------|
| 0 | Pending | Waiting for scheduled time |
| 1 | ClearedEarly | Accused checked in before deadline |
| 2 | Voting | Community vote in progress |
| 3 | Guilty | Guilty verdict (majority voted Rat) |
| 4 | NotGuilty | Not guilty verdict (majority voted Not Rat) |
| 5 | Expired | Bot was offline, watch expired |
| 6 | Cancelled | Admin cancelled the watch |

---

## Configuration

### Application Options

Application-level settings in `appsettings.json` under `RatWatch` section:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| CheckIntervalSeconds | int | 30 | Background service polling interval |
| MaxConcurrentExecutions | int | 5 | Max concurrent watch executions |
| ExecutionTimeoutSeconds | int | 30 | Timeout per watch execution |
| DefaultVotingDurationMinutes | int | 5 | Fallback voting duration for new guilds |
| DefaultMaxAdvanceHours | int | 24 | Fallback max advance hours for new guilds |

### Guild Settings Entity

Guild-specific settings stored in `GuildRatWatchSettings`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| GuildId | ulong | - | Discord guild snowflake ID (primary key) |
| IsEnabled | bool | true | Whether Rat Watch is enabled for this guild |
| Timezone | string | "Eastern Standard Time" | Timezone for parsing time expressions |
| MaxAdvanceHours | int | 24 | Max hours in advance a watch can be scheduled |
| VotingDurationMinutes | int | 5 | How long voting lasts |
| PublicLeaderboardEnabled | bool | false | Whether the leaderboard is publicly accessible |

---

## Background Services

### RatWatchExecutionService

**Type:** BackgroundService
**Interval:** Every 30 seconds

**Responsibilities:**
1. **Start Voting**: Find Pending watches past their scheduled time, post voting messages
2. **End Voting**: Find Voting watches past their voting duration, tally votes, record verdicts
3. **Cleanup**: Mark expired watches that were never processed

**Resilience:**
- Handles bot restarts gracefully
- Skips individual watch errors to continue processing others
- Logs all state transitions

### RatWatchStatusService

**Type:** Singleton Service
**Interface:** `IRatWatchStatusService`

Manages the bot's Discord presence status during active Rat Watches.

**Behavior:**
- When any watch transitions to `Pending` or `Voting`, sets bot status to "Watching for rats..."
- When all watches are completed/cleared, restores normal status from `General:StatusMessage` setting
- Uses event-driven updates via `StatusUpdateRequested` event
- Thread-safe with internal locking

**Status Updates Triggered By:**
- New watch created (`RatWatchModule`)
- Watch cleared early via button or `/rat-clear` command
- Voting started by execution service
- Voting completed by execution service
- Bot startup (checks for active watches)

**Edge Cases:**
- **Multiple concurrent watches**: Status remains "Watching for rats..." until ALL watches are resolved
- **Bot restart during active watch**: Status is restored on startup if any watches are in `Pending` or `Voting` state
- **State unchanged**: Skips Discord API call if status hasn't actually changed

---

## Service Interface

### IRatWatchService

```csharp
public interface IRatWatchService
{
    // Watch CRUD
    Task<RatWatchDto> CreateWatchAsync(RatWatchCreateDto dto, CancellationToken ct = default);
    Task<RatWatchDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<RatWatchDto> Items, int TotalCount)> GetByGuildAsync(ulong guildId, int page, int pageSize, CancellationToken ct = default);
    Task<(IEnumerable<RatWatchDto> Items, int TotalCount)> GetFilteredByGuildAsync(ulong guildId, RatWatchIncidentFilterDto filter, CancellationToken ct = default);
    Task<bool> ClearWatchAsync(Guid watchId, ulong userId, CancellationToken ct = default);
    Task<bool> CancelWatchAsync(Guid id, string reason, CancellationToken ct = default);

    // Voting
    Task<bool> CastVoteAsync(Guid watchId, ulong voterId, bool isGuilty, CancellationToken ct = default);
    Task<(int Guilty, int NotGuilty)> GetVoteTallyAsync(Guid watchId, CancellationToken ct = default);

    // Execution (for background service)
    Task<IEnumerable<RatWatch>> GetDueWatchesAsync(CancellationToken ct = default);
    Task<IEnumerable<RatWatch>> GetExpiredVotingAsync(CancellationToken ct = default);
    Task<bool> StartVotingAsync(Guid watchId, ulong? votingMessageId = null, CancellationToken ct = default);
    Task<bool> FinalizeVotingAsync(Guid watchId, CancellationToken ct = default);
    Task<bool> HasActiveWatchesAsync(CancellationToken ct = default);

    // Stats & Leaderboard
    Task<RatStatsDto> GetUserStatsAsync(ulong guildId, ulong userId, CancellationToken ct = default);
    Task<IReadOnlyList<RatLeaderboardEntryDto>> GetLeaderboardAsync(ulong guildId, int limit = 10, CancellationToken ct = default);

    // Guild Settings
    Task<GuildRatWatchSettings> GetGuildSettingsAsync(ulong guildId, CancellationToken ct = default);
    Task<GuildRatWatchSettings> UpdateGuildSettingsAsync(ulong guildId, Action<GuildRatWatchSettings> update, CancellationToken ct = default);

    // Time Parsing
    DateTime? ParseScheduleTime(string input, string timezone);
}
```

---

## Preconditions

### RequireRatWatchEnabledAttribute

Located in `src/DiscordBot.Bot/Preconditions/RequireRatWatchEnabledAttribute.cs`

Checks that:
1. Command is executed in a guild
2. Rat Watch feature is enabled for that guild

Returns `PreconditionResult.FromError` with user-friendly message if disabled.

---

## Related Documentation

- [Interactive Components](interactive-components.md) - Button and component patterns
- [Timezone Handling](timezone-handling.md) - How time parsing works
- [Authorization Policies](authorization-policies.md) - Admin UI access control
- [Database Schema](database-schema.md) - Full schema documentation

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.2 | 2025-12-30 | Updated service interface, component IDs, configuration, and UI pages to match implementation |
| 1.1 | 2025-12-30 | Added bot status updates during active watches (Issue #412) |
| 1.0 | 2025-12-30 | Initial implementation (Issue #404) |
