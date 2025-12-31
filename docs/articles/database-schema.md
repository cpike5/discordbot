# Database Schema Documentation

## Overview

The Discord bot uses Entity Framework Core with SQLite for local development and supports MSSQL, MySQL, and PostgreSQL for production deployments. The schema has evolved from the initial three core tables (`Guilds`, `Users`, `CommandLogs`) to include additional feature tables for audit logging, scheduled messages, welcome configurations, message logging, user consent tracking, and the Rat Watch accountability system.

## Data Type Considerations

### Discord Snowflake IDs

Discord uses 64-bit unsigned integers (ulong in C#) for snowflake IDs. Since most databases don't natively support ulong, these are converted to signed long (Int64) during storage using EF Core value conversions.

**Important:** The application layer always works with ulong, but the database stores these as long. This conversion is handled transparently by Entity Framework Core.

```csharp
// Example from GuildConfiguration.cs
builder.Property(g => g.Id)
    .HasConversion<long>()
    .ValueGeneratedNever();
```

## Tables

### Guilds

Stores Discord server (guild) metadata and bot-specific configuration.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER (long) | No | - | PRIMARY KEY | Discord guild snowflake ID |
| Name | TEXT | No | - | MaxLength: 100 | Display name of the guild |
| JoinedAt | TEXT (DateTime) | No | - | - | Timestamp when bot joined the guild |
| IsActive | INTEGER (bool) | No | true | - | Whether bot is currently active in guild |
| Prefix | TEXT | Yes | NULL | MaxLength: 10 | Custom command prefix (optional) |
| Settings | TEXT (JSON) | Yes | NULL | - | JSON-serialized guild-specific settings |

**Indexes:**
- `IX_Guilds_IsActive` on `IsActive` - Optimizes queries for active guilds

**SQL Schema:**
```sql
CREATE TABLE Guilds (
    Id INTEGER NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL,
    JoinedAt TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    Prefix TEXT,
    Settings TEXT
);

CREATE INDEX IX_Guilds_IsActive ON Guilds (IsActive);
```

---

### Users

Stores Discord user information and tracking metadata.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER (long) | No | - | PRIMARY KEY | Discord user snowflake ID |
| Username | TEXT | No | - | MaxLength: 32 | Discord username |
| Discriminator | TEXT | No | "0" | MaxLength: 4 | Discord discriminator (legacy) |
| FirstSeenAt | TEXT (DateTime) | No | - | - | Timestamp when user first interacted |
| LastSeenAt | TEXT (DateTime) | No | - | - | Timestamp of most recent interaction |

**Indexes:**
- `IX_Users_LastSeenAt` on `LastSeenAt` - Optimizes queries for recently active users

**SQL Schema:**
```sql
CREATE TABLE Users (
    Id INTEGER NOT NULL PRIMARY KEY,
    Username TEXT NOT NULL,
    Discriminator TEXT NOT NULL DEFAULT '0',
    FirstSeenAt TEXT NOT NULL,
    LastSeenAt TEXT NOT NULL
);

CREATE INDEX IX_Users_LastSeenAt ON Users (LastSeenAt);
```

**Notes:**
- `Discriminator` defaults to "0" for new Discord usernames (post-discriminator system)
- Legacy usernames have 4-digit discriminators (e.g., "1234")

---

### CommandLogs

Audit log for command executions with performance metrics and error tracking.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique log entry identifier |
| GuildId | INTEGER (long) | Yes | NULL | FOREIGN KEY → Guilds(Id) | Guild where command executed (NULL for DMs) |
| UserId | INTEGER (long) | No | - | FOREIGN KEY → Users(Id) | User who executed the command |
| CommandName | TEXT | No | - | MaxLength: 50 | Name of the executed command |
| Parameters | TEXT (JSON) | Yes | NULL | - | JSON-serialized command parameters |
| ExecutedAt | TEXT (DateTime) | No | - | - | Command execution timestamp |
| ResponseTimeMs | INTEGER | No | - | - | Execution duration in milliseconds |
| Success | INTEGER (bool) | No | - | - | Whether command completed successfully |
| ErrorMessage | TEXT | Yes | NULL | MaxLength: 2000 | Error message if command failed |

**Indexes:**
- `IX_CommandLogs_GuildId` on `GuildId` - Query logs by guild
- `IX_CommandLogs_UserId` on `UserId` - Query logs by user
- `IX_CommandLogs_CommandName` on `CommandName` - Query logs by command
- `IX_CommandLogs_ExecutedAt` on `ExecutedAt` - Query logs by date/time
- `IX_CommandLogs_Success` on `Success` - Query failed commands

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE SET NULL` - Preserve logs if guild deleted
- `UserId` → `Users(Id)` with `ON DELETE CASCADE` - Remove logs when user deleted

**SQL Schema:**
```sql
CREATE TABLE CommandLogs (
    Id BLOB NOT NULL PRIMARY KEY,
    GuildId INTEGER,
    UserId INTEGER NOT NULL,
    CommandName TEXT NOT NULL,
    Parameters TEXT,
    ExecutedAt TEXT NOT NULL,
    ResponseTimeMs INTEGER NOT NULL,
    Success INTEGER NOT NULL,
    ErrorMessage TEXT,
    CONSTRAINT FK_CommandLogs_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE SET NULL,
    CONSTRAINT FK_CommandLogs_Users_UserId FOREIGN KEY (UserId)
        REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE INDEX IX_CommandLogs_GuildId ON CommandLogs (GuildId);
CREATE INDEX IX_CommandLogs_UserId ON CommandLogs (UserId);
CREATE INDEX IX_CommandLogs_CommandName ON CommandLogs (CommandName);
CREATE INDEX IX_CommandLogs_ExecutedAt ON CommandLogs (ExecutedAt);
CREATE INDEX IX_CommandLogs_Success ON CommandLogs (Success);
```

---

### AuditLogs

Comprehensive audit trail for system actions, user activities, and security events.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER (long) | No | Auto-increment | PRIMARY KEY | Unique audit log entry identifier |
| Timestamp | TEXT (DateTime) | No | - | - | Timestamp when action occurred (UTC) |
| Category | INTEGER | No | - | Enum: AuditLogCategory | Category of the action (User, Guild, Configuration, Security, Command, Message, System) |
| Action | INTEGER | No | - | Enum: AuditLogAction | Specific action performed (Created, Updated, Deleted, Login, etc.) |
| ActorType | INTEGER | No | - | Enum: AuditLogActorType | Type of actor (User, System, Bot) |
| ActorId | TEXT | Yes | NULL | MaxLength: 450 | Identifier of who performed the action |
| TargetType | TEXT | Yes | NULL | MaxLength: 200 | Type name of affected entity |
| TargetId | TEXT | Yes | NULL | MaxLength: 450 | Identifier of affected entity |
| GuildId | INTEGER (long) | Yes | NULL | - | Guild ID for guild-specific actions |
| Details | TEXT (JSON) | Yes | NULL | - | JSON-serialized action details and context |
| IpAddress | TEXT | Yes | NULL | MaxLength: 45 | IP address for user actions (IPv4/IPv6) |
| CorrelationId | TEXT | Yes | NULL | MaxLength: 100 | Groups related audit entries |

**Indexes:**
- `IX_AuditLogs_Timestamp` on `Timestamp` - Time-range queries and cleanup
- `IX_AuditLogs_Category` on `Category` - Filter by category
- `IX_AuditLogs_ActorId_Timestamp` on `(ActorId, Timestamp)` - User activity queries
- `IX_AuditLogs_GuildId_Timestamp` on `(GuildId, Timestamp)` - Guild-specific audit logs
- `IX_AuditLogs_CorrelationId` on `CorrelationId` - Trace related events
- `IX_AuditLogs_Category_Action_Timestamp` on `(Category, Action, Timestamp)` - Common filtering patterns
- `IX_AuditLogs_TargetType_TargetId_Timestamp` on `(TargetType, TargetId, Timestamp)` - Entity-specific audit trails

**SQL Schema:**
```sql
CREATE TABLE AuditLogs (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    Category INTEGER NOT NULL,
    Action INTEGER NOT NULL,
    ActorType INTEGER NOT NULL,
    ActorId TEXT,
    TargetType TEXT,
    TargetId TEXT,
    GuildId INTEGER,
    Details TEXT,
    IpAddress TEXT,
    CorrelationId TEXT
);

CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs (Timestamp);
CREATE INDEX IX_AuditLogs_Category ON AuditLogs (Category);
CREATE INDEX IX_AuditLogs_ActorId_Timestamp ON AuditLogs (ActorId, Timestamp);
CREATE INDEX IX_AuditLogs_GuildId_Timestamp ON AuditLogs (GuildId, Timestamp);
CREATE INDEX IX_AuditLogs_CorrelationId ON AuditLogs (CorrelationId);
CREATE INDEX IX_AuditLogs_Category_Action_Timestamp ON AuditLogs (Category, Action, Timestamp);
CREATE INDEX IX_AuditLogs_TargetType_TargetId_Timestamp ON AuditLogs (TargetType, TargetId, Timestamp);
```

---

### ScheduledMessages

Recurring or one-time messages sent to Discord channels on a schedule.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique scheduled message identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild where message will be sent |
| ChannelId | INTEGER (long) | No | - | - | Discord channel snowflake ID |
| Title | TEXT | No | - | MaxLength: 200 | Message title |
| Content | TEXT | No | - | MaxLength: 2000 | Message content (Discord limit) |
| Frequency | INTEGER | No | - | Enum: ScheduleFrequency | How often to send (Once, Hourly, Daily, Weekly, Monthly, Custom) |
| CronExpression | TEXT | Yes | NULL | MaxLength: 100 | Cron expression for custom schedules |
| IsEnabled | INTEGER (bool) | No | true | - | Whether message is active |
| LastExecutedAt | TEXT (DateTime) | Yes | NULL | - | Last execution timestamp (UTC) |
| NextExecutionAt | TEXT (DateTime) | Yes | NULL | - | Next scheduled execution (UTC) |
| CreatedAt | TEXT (DateTime) | No | - | - | Creation timestamp (UTC) |
| CreatedBy | TEXT | No | - | MaxLength: 450 | User ID who created the message |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp (UTC) |

**Indexes:**
- `IX_ScheduledMessages_GuildId_IsEnabled` on `(GuildId, IsEnabled)` - Guild listing queries
- `IX_ScheduledMessages_NextExecutionAt_IsEnabled` on `(NextExecutionAt, IsEnabled)` - Background service polling
- `IX_ScheduledMessages_ChannelId` on `ChannelId` - Channel lookup

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE` - Remove messages when guild deleted

**SQL Schema:**
```sql
CREATE TABLE ScheduledMessages (
    Id BLOB NOT NULL PRIMARY KEY,
    GuildId INTEGER NOT NULL,
    ChannelId INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Content TEXT NOT NULL,
    Frequency INTEGER NOT NULL,
    CronExpression TEXT,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    LastExecutedAt TEXT,
    NextExecutionAt TEXT,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CONSTRAINT FK_ScheduledMessages_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE CASCADE
);

CREATE INDEX IX_ScheduledMessages_GuildId_IsEnabled ON ScheduledMessages (GuildId, IsEnabled);
CREATE INDEX IX_ScheduledMessages_NextExecutionAt_IsEnabled ON ScheduledMessages (NextExecutionAt, IsEnabled);
CREATE INDEX IX_ScheduledMessages_ChannelId ON ScheduledMessages (ChannelId);
```

---

### WelcomeConfigurations

Per-guild settings for automated welcome messages sent when users join.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| GuildId | INTEGER (long) | No | - | PRIMARY KEY | Discord guild snowflake ID |
| IsEnabled | INTEGER (bool) | No | true | - | Whether welcome messages are enabled |
| WelcomeChannelId | INTEGER (long) | Yes | NULL | - | Channel ID for welcome messages |
| WelcomeMessage | TEXT | No | "" | MaxLength: 2000 | Welcome message template with placeholders |
| IncludeAvatar | INTEGER (bool) | No | true | - | Whether to include user avatar |
| UseEmbed | INTEGER (bool) | No | true | - | Send as rich embed or plain text |
| EmbedColor | TEXT | Yes | NULL | MaxLength: 7 | Hex color code (#RRGGBB) for embed |
| CreatedAt | TEXT (DateTime) | No | - | - | Configuration creation timestamp (UTC) |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp (UTC) |

**Indexes:**
- `IX_WelcomeConfigurations_IsEnabled` on `IsEnabled` - Find enabled guilds

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE` - Remove config when guild deleted

**SQL Schema:**
```sql
CREATE TABLE WelcomeConfigurations (
    GuildId INTEGER NOT NULL PRIMARY KEY,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    WelcomeChannelId INTEGER,
    WelcomeMessage TEXT NOT NULL DEFAULT '',
    IncludeAvatar INTEGER NOT NULL DEFAULT 1,
    UseEmbed INTEGER NOT NULL DEFAULT 1,
    EmbedColor TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CONSTRAINT FK_WelcomeConfigurations_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE CASCADE
);

CREATE INDEX IX_WelcomeConfigurations_IsEnabled ON WelcomeConfigurations (IsEnabled);
```

**Notes:**
- Welcome message supports placeholders: `{user}`, `{guild}`, `{memberCount}`, etc.
- EmbedColor must be in hex format with leading # (e.g., "#5865F2")

---

### MessageLogs

Log of Discord messages for analytics, auditing, and moderation purposes.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER (long) | No | Auto-increment | PRIMARY KEY | Unique log entry identifier |
| DiscordMessageId | INTEGER (long) | No | - | UNIQUE | Discord message snowflake ID |
| AuthorId | INTEGER (long) | No | - | FOREIGN KEY → Users(Id) | User who authored the message |
| ChannelId | INTEGER (long) | No | - | - | Channel where message was sent |
| GuildId | INTEGER (long) | Yes | NULL | FOREIGN KEY → Guilds(Id) | Guild ID (null for DMs) |
| Source | INTEGER | No | - | Enum: MessageSource | DirectMessage or ServerChannel |
| Content | TEXT | No | - | - | Message text content |
| Timestamp | TEXT (DateTime) | No | - | - | When message was sent (UTC) |
| LoggedAt | TEXT (DateTime) | No | - | - | When message was logged (UTC) |
| HasAttachments | INTEGER (bool) | No | false | - | Whether message has attachments |
| HasEmbeds | INTEGER (bool) | No | false | - | Whether message has embeds |
| ReplyToMessageId | INTEGER (long) | Yes | NULL | - | ID of replied-to message |

**Indexes:**
- `IX_MessageLogs_AuthorId_Timestamp` on `(AuthorId, Timestamp)` - User history queries
- `IX_MessageLogs_ChannelId_Timestamp` on `(ChannelId, Timestamp)` - Channel history queries
- `IX_MessageLogs_GuildId_Timestamp` on `(GuildId, Timestamp)` - Guild analytics queries
- `IX_MessageLogs_LoggedAt` on `LoggedAt` - Retention cleanup queries
- `IX_MessageLogs_DiscordMessageId_Unique` on `DiscordMessageId` (UNIQUE) - Prevent duplicate logging

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE SET NULL` - Preserve logs if guild deleted
- `AuthorId` → `Users(Id)` with `ON DELETE CASCADE` - Remove logs when user deleted

**SQL Schema:**
```sql
CREATE TABLE MessageLogs (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    DiscordMessageId INTEGER NOT NULL,
    AuthorId INTEGER NOT NULL,
    ChannelId INTEGER NOT NULL,
    GuildId INTEGER,
    Source INTEGER NOT NULL,
    Content TEXT NOT NULL,
    Timestamp TEXT NOT NULL,
    LoggedAt TEXT NOT NULL,
    HasAttachments INTEGER NOT NULL DEFAULT 0,
    HasEmbeds INTEGER NOT NULL DEFAULT 0,
    ReplyToMessageId INTEGER,
    CONSTRAINT FK_MessageLogs_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE SET NULL,
    CONSTRAINT FK_MessageLogs_Users_AuthorId FOREIGN KEY (AuthorId)
        REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE INDEX IX_MessageLogs_AuthorId_Timestamp ON MessageLogs (AuthorId, Timestamp);
CREATE INDEX IX_MessageLogs_ChannelId_Timestamp ON MessageLogs (ChannelId, Timestamp);
CREATE INDEX IX_MessageLogs_GuildId_Timestamp ON MessageLogs (GuildId, Timestamp);
CREATE INDEX IX_MessageLogs_LoggedAt ON MessageLogs (LoggedAt);
CREATE UNIQUE INDEX IX_MessageLogs_DiscordMessageId_Unique ON MessageLogs (DiscordMessageId);
```

---

### UserConsents

Tracks user consent for data processing activities (GDPR compliance).

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique consent record identifier |
| DiscordUserId | INTEGER (long) | No | - | FOREIGN KEY → Users(Id) | Discord user who granted/revoked consent |
| ConsentType | INTEGER | No | - | Enum: ConsentType | Type of consent (MessageLogging, etc.) |
| GrantedAt | TEXT (DateTime) | No | CURRENT_TIMESTAMP | - | When consent was granted (UTC) |
| RevokedAt | TEXT (DateTime) | Yes | NULL | - | When consent was revoked (UTC), null if active |
| GrantedVia | TEXT | Yes | NULL | MaxLength: 50 | How consent was granted (SlashCommand, WebUI) |
| RevokedVia | TEXT | Yes | NULL | MaxLength: 50 | How consent was revoked |

**Indexes:**
- `IX_UserConsents_DiscordUserId_ConsentType` on `(DiscordUserId, ConsentType)` - Active consent lookups
- `IX_UserConsents_RevokedAt` on `RevokedAt` - Filter active consents
- `IX_UserConsents_GrantedAt` on `GrantedAt` - Temporal queries

**Foreign Keys:**
- `DiscordUserId` → `Users(Id)` with `ON DELETE CASCADE` - Remove consents when user deleted

**SQL Schema:**
```sql
CREATE TABLE UserConsents (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    DiscordUserId INTEGER NOT NULL,
    ConsentType INTEGER NOT NULL,
    GrantedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    RevokedAt TEXT,
    GrantedVia TEXT,
    RevokedVia TEXT,
    CONSTRAINT FK_UserConsents_Users_DiscordUserId FOREIGN KEY (DiscordUserId)
        REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserConsents_DiscordUserId_ConsentType ON UserConsents (DiscordUserId, ConsentType);
CREATE INDEX IX_UserConsents_RevokedAt ON UserConsents (RevokedAt);
CREATE INDEX IX_UserConsents_GrantedAt ON UserConsents (GrantedAt);
```

---

### RatWatches

Rat Watch accountability trackers for monitoring user commitments.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique Rat Watch identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild where watch was created |
| ChannelId | INTEGER (long) | No | - | - | Channel where watch was created |
| AccusedUserId | INTEGER (long) | No | - | - | User being watched (the accused) |
| InitiatorUserId | INTEGER (long) | No | - | - | User who initiated the watch |
| OriginalMessageId | INTEGER (long) | No | - | - | Discord message ID that triggered watch |
| CustomMessage | TEXT | Yes | NULL | MaxLength: 200 | Optional custom commitment description |
| ScheduledAt | TEXT (DateTime) | No | - | - | When rat check should occur (UTC) |
| CreatedAt | TEXT (DateTime) | No | - | - | Watch creation timestamp (UTC) |
| Status | INTEGER | No | - | Enum: RatWatchStatus | Current status (Pending, ClearedEarly, Voting, Guilty, NotGuilty, Expired, Cancelled) |
| NotificationMessageId | INTEGER (long) | Yes | NULL | - | "I'm Here!" button message ID |
| VotingMessageId | INTEGER (long) | Yes | NULL | - | Voting message ID with buttons |
| ClearedAt | TEXT (DateTime) | Yes | NULL | - | When accused cleared themselves early (UTC) |
| VotingStartedAt | TEXT (DateTime) | Yes | NULL | - | Voting start timestamp (UTC) |
| VotingEndedAt | TEXT (DateTime) | Yes | NULL | - | Voting end timestamp (UTC) |

**Indexes:**
- `IX_RatWatches_GuildId_ScheduledAt_Status` on `(GuildId, ScheduledAt, Status)` - Background service polling
- `IX_RatWatches_GuildId_AccusedUserId` on `(GuildId, AccusedUserId)` - User stats queries
- `IX_RatWatches_ChannelId` on `ChannelId` - Channel lookup

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE` - Remove watches when guild deleted

**Relationships:**
- One-to-Many with `RatVotes` (votes on this watch)
- One-to-One with `RatRecord` (guilty verdict record)

**SQL Schema:**
```sql
CREATE TABLE RatWatches (
    Id BLOB NOT NULL PRIMARY KEY,
    GuildId INTEGER NOT NULL,
    ChannelId INTEGER NOT NULL,
    AccusedUserId INTEGER NOT NULL,
    InitiatorUserId INTEGER NOT NULL,
    OriginalMessageId INTEGER NOT NULL,
    CustomMessage TEXT,
    ScheduledAt TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    Status INTEGER NOT NULL,
    NotificationMessageId INTEGER,
    VotingMessageId INTEGER,
    ClearedAt TEXT,
    VotingStartedAt TEXT,
    VotingEndedAt TEXT,
    CONSTRAINT FK_RatWatches_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE CASCADE
);

CREATE INDEX IX_RatWatches_GuildId_ScheduledAt_Status ON RatWatches (GuildId, ScheduledAt, Status);
CREATE INDEX IX_RatWatches_GuildId_AccusedUserId ON RatWatches (GuildId, AccusedUserId);
CREATE INDEX IX_RatWatches_ChannelId ON RatWatches (ChannelId);
```

---

### RatVotes

Individual votes cast during Rat Watch voting periods.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique vote identifier |
| RatWatchId | BLOB (Guid) | No | - | FOREIGN KEY → RatWatches(Id) | Rat Watch this vote belongs to |
| VoterUserId | INTEGER (long) | No | - | - | User who cast the vote |
| IsGuiltyVote | INTEGER (bool) | No | - | - | True for "Rat" (guilty), false for "Not Rat" |
| VotedAt | TEXT (DateTime) | No | - | - | Vote timestamp (UTC) |

**Indexes:**
- `IX_RatVotes_RatWatchId_VoterUserId_Unique` on `(RatWatchId, VoterUserId)` (UNIQUE) - One vote per user per watch
- `IX_RatVotes_RatWatchId` on `RatWatchId` - Efficient watch lookup

**Foreign Keys:**
- `RatWatchId` → `RatWatches(Id)` with `ON DELETE CASCADE` - Remove votes when watch deleted

**SQL Schema:**
```sql
CREATE TABLE RatVotes (
    Id BLOB NOT NULL PRIMARY KEY,
    RatWatchId BLOB NOT NULL,
    VoterUserId INTEGER NOT NULL,
    IsGuiltyVote INTEGER NOT NULL,
    VotedAt TEXT NOT NULL,
    CONSTRAINT FK_RatVotes_RatWatches_RatWatchId FOREIGN KEY (RatWatchId)
        REFERENCES RatWatches (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_RatVotes_RatWatchId_VoterUserId_Unique ON RatVotes (RatWatchId, VoterUserId);
CREATE INDEX IX_RatVotes_RatWatchId ON RatVotes (RatWatchId);
```

---

### RatRecords

Permanent records of guilty verdicts from Rat Watch voting.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique record identifier |
| RatWatchId | BLOB (Guid) | No | - | FOREIGN KEY → RatWatches(Id) | Rat Watch that created this record |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild where verdict occurred |
| UserId | INTEGER (long) | No | - | - | User who received guilty verdict |
| GuiltyVotes | INTEGER | No | - | - | Number of "Rat" votes |
| NotGuiltyVotes | INTEGER | No | - | - | Number of "Not Rat" votes |
| RecordedAt | TEXT (DateTime) | No | - | - | Record creation timestamp (UTC) |
| OriginalMessageLink | TEXT | Yes | NULL | MaxLength: 500 | Link to original Discord message |

**Indexes:**
- `IX_RatRecords_GuildId_UserId` on `(GuildId, UserId)` - Leaderboard queries
- `IX_RatRecords_RecordedAt` on `RecordedAt` - Recent records query

**Foreign Keys:**
- `RatWatchId` → `RatWatches(Id)` with `ON DELETE CASCADE` - Remove record when watch deleted
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE` - Remove records when guild deleted

**SQL Schema:**
```sql
CREATE TABLE RatRecords (
    Id BLOB NOT NULL PRIMARY KEY,
    RatWatchId BLOB NOT NULL,
    GuildId INTEGER NOT NULL,
    UserId INTEGER NOT NULL,
    GuiltyVotes INTEGER NOT NULL,
    NotGuiltyVotes INTEGER NOT NULL,
    RecordedAt TEXT NOT NULL,
    OriginalMessageLink TEXT,
    CONSTRAINT FK_RatRecords_RatWatches_RatWatchId FOREIGN KEY (RatWatchId)
        REFERENCES RatWatches (Id) ON DELETE CASCADE,
    CONSTRAINT FK_RatRecords_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE CASCADE
);

CREATE INDEX IX_RatRecords_GuildId_UserId ON RatRecords (GuildId, UserId);
CREATE INDEX IX_RatRecords_RecordedAt ON RatRecords (RecordedAt);
```

---

### GuildRatWatchSettings

Per-guild configuration settings for the Rat Watch feature.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| GuildId | INTEGER (long) | No | - | PRIMARY KEY | Discord guild snowflake ID |
| IsEnabled | INTEGER (bool) | No | true | - | Whether Rat Watch is enabled |
| Timezone | TEXT | No | "UTC" | MaxLength: 100 | IANA timezone identifier (e.g., "America/New_York") |
| MaxAdvanceHours | INTEGER | No | 24 | - | Max hours in advance a watch can be scheduled |
| VotingDurationMinutes | INTEGER | No | 5 | - | Voting window duration in minutes |
| PublicLeaderboardEnabled | INTEGER (bool) | No | false | - | Whether leaderboard is publicly accessible |
| CreatedAt | TEXT (DateTime) | No | - | - | Settings creation timestamp (UTC) |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp (UTC) |

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE` - Remove settings when guild deleted

**SQL Schema:**
```sql
CREATE TABLE GuildRatWatchSettings (
    GuildId INTEGER NOT NULL PRIMARY KEY,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    Timezone TEXT NOT NULL DEFAULT 'UTC',
    MaxAdvanceHours INTEGER NOT NULL DEFAULT 24,
    VotingDurationMinutes INTEGER NOT NULL DEFAULT 5,
    PublicLeaderboardEnabled INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CONSTRAINT FK_GuildRatWatchSettings_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE CASCADE
);
```

**Notes:**
- Timezone must be a valid IANA timezone identifier
- Default timezone is "Eastern Standard Time" for compatibility

---

## Entity Relationships

### Core Entities Diagram

```
                         ┌─────────────────┐
                         │      Guilds     │
                         ├─────────────────┤
                         │ Id (PK)         │
                         │ Name            │
                         │ JoinedAt        │
                         │ IsActive        │
                         │ Prefix          │
                         │ Settings        │
                         └────────┬────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │                         │                         │
        │ 1                       │ 1                       │ 1
        │                         │                         │
┌───────▼────────┐    ┌──────────▼──────────┐    ┌────────▼────────────┐
│ CommandLogs    │    │   MessageLogs       │    │ ScheduledMessages   │
├────────────────┤    ├─────────────────────┤    ├─────────────────────┤
│ Id (PK)        │    │ Id (PK)             │    │ Id (PK)             │
│ GuildId (FK)   │    │ DiscordMessageId    │    │ GuildId (FK)        │
│ UserId (FK)    │    │ AuthorId (FK)       │    │ ChannelId           │
│ CommandName    │    │ ChannelId           │    │ Title               │
│ ExecutedAt     │    │ GuildId (FK)        │    │ Content             │
│ Success        │    │ Content             │    │ Frequency           │
└────────┬───────┘    │ Timestamp           │    │ NextExecutionAt     │
         │            └──────────┬──────────┘    └─────────────────────┘
         │                       │
         │                       │
         │ 1..*              1   │
         │         ┌─────────────▼────────┐
         └─────────┤       Users          │
                   ├──────────────────────┤
                   │ Id (PK)              │
                   │ Username             │
                   │ Discriminator        │
                   │ FirstSeenAt          │
                   │ LastSeenAt           │
                   └──────────┬───────────┘
                              │
                              │ 1
                              │
                   ┌──────────▼───────────┐
                   │   UserConsents       │
                   ├──────────────────────┤
                   │ Id (PK)              │
                   │ DiscordUserId (FK)   │
                   │ ConsentType          │
                   │ GrantedAt            │
                   │ RevokedAt            │
                   └──────────────────────┘
```

### Rat Watch System Diagram

```
                         ┌─────────────────┐
                         │      Guilds     │
                         └────────┬────────┘
                                  │
        ┌─────────────────────────┼──────────────────────────┐
        │ 1                       │ 1                        │ 1
        │                         │                          │
┌───────▼────────────┐  ┌─────────▼──────────┐  ┌──────────▼─────────────┐
│  RatWatches        │  │ RatRecords         │  │ GuildRatWatchSettings  │
├────────────────────┤  ├────────────────────┤  ├────────────────────────┤
│ Id (PK)            │  │ Id (PK)            │  │ GuildId (PK, FK)       │
│ GuildId (FK)       │  │ RatWatchId (FK)    │  │ IsEnabled              │
│ AccusedUserId      │  │ GuildId (FK)       │  │ Timezone               │
│ ScheduledAt        │  │ UserId             │  │ MaxAdvanceHours        │
│ Status             │  │ GuiltyVotes        │  │ VotingDurationMinutes  │
└──────┬─────────────┘  │ NotGuiltyVotes     │  └────────────────────────┘
       │                │ RecordedAt         │
       │ 1              └────────────────────┘
       │                         ▲
       │                         │
       │                         │ 1
       │                         │
       │ 0..*         ┌──────────┴────────┐
       └──────────────┤    RatVotes       │
                      ├───────────────────┤
                      │ Id (PK)           │
                      │ RatWatchId (FK)   │
                      │ VoterUserId       │
                      │ IsGuiltyVote      │
                      │ VotedAt           │
                      └───────────────────┘
```

### Audit and Configuration Entities

```
┌─────────────────┐       ┌──────────────────────────┐
│   AuditLogs     │       │  WelcomeConfigurations   │
├─────────────────┤       ├──────────────────────────┤
│ Id (PK)         │       │ GuildId (PK, FK)         │
│ Timestamp       │       │ IsEnabled                │
│ Category        │       │ WelcomeChannelId         │
│ Action          │       │ WelcomeMessage           │
│ ActorType       │       │ UseEmbed                 │
│ ActorId         │       │ EmbedColor               │
│ TargetType      │       └────────┬─────────────────┘
│ TargetId        │                │
│ GuildId         │                │ 1
│ Details         │                │
│ CorrelationId   │       ┌────────▼─────────────────┐
└─────────────────┘       │       Guilds             │
                          └──────────────────────────┘
```

### Relationship Summary

| Parent Entity | Child Entity | Relationship Type | Cardinality | Delete Behavior | Notes |
|--------------|--------------|-------------------|-------------|-----------------|-------|
| Guilds | CommandLogs | One-to-Many | 1:0..* | SET NULL | Preserve logs when guild deleted |
| Guilds | MessageLogs | One-to-Many | 1:0..* | SET NULL | Preserve logs when guild deleted |
| Guilds | ScheduledMessages | One-to-Many | 1:0..* | CASCADE | Remove messages when guild deleted |
| Guilds | WelcomeConfigurations | One-to-One | 1:0..1 | CASCADE | One config per guild |
| Guilds | RatWatches | One-to-Many | 1:0..* | CASCADE | Remove watches when guild deleted |
| Guilds | RatRecords | One-to-Many | 1:0..* | CASCADE | Remove records when guild deleted |
| Guilds | GuildRatWatchSettings | One-to-One | 1:0..1 | CASCADE | One settings per guild |
| Users | CommandLogs | One-to-Many | 1:1..* | CASCADE | Remove logs when user deleted |
| Users | MessageLogs | One-to-Many | 1:0..* | CASCADE | Remove logs when user deleted |
| Users | UserConsents | One-to-Many | 1:0..* | CASCADE | Remove consents when user deleted |
| RatWatches | RatVotes | One-to-Many | 1:0..* | CASCADE | Remove votes when watch deleted |
| RatWatches | RatRecords | One-to-One | 1:0..1 | CASCADE | One record per guilty watch |

---

## Configuration Classes

Entity configurations are defined using EF Core's Fluent API in separate configuration classes:

| Entity | Configuration Class | Location |
|--------|---------------------|----------|
| Guild | `GuildConfiguration` | `Infrastructure/Data/Configurations/GuildConfiguration.cs` |
| User | `UserConfiguration` | `Infrastructure/Data/Configurations/UserConfiguration.cs` |
| CommandLog | `CommandLogConfiguration` | `Infrastructure/Data/Configurations/CommandLogConfiguration.cs` |
| AuditLog | `AuditLogConfiguration` | `Infrastructure/Data/Configurations/AuditLogConfiguration.cs` |
| ScheduledMessage | `ScheduledMessageConfiguration` | `Infrastructure/Data/Configurations/ScheduledMessageConfiguration.cs` |
| WelcomeConfiguration | `WelcomeConfigurationConfiguration` | `Infrastructure/Data/Configurations/WelcomeConfigurationConfiguration.cs` |
| MessageLog | `MessageLogConfiguration` | `Infrastructure/Data/Configurations/MessageLogConfiguration.cs` |
| UserConsent | `UserConsentConfiguration` | `Infrastructure/Data/Configurations/UserConsentConfiguration.cs` |
| RatWatch | `RatWatchConfiguration` | `Infrastructure/Data/Configurations/RatWatchConfiguration.cs` |
| RatVote | `RatVoteConfiguration` | `Infrastructure/Data/Configurations/RatVoteConfiguration.cs` |
| RatRecord | `RatRecordConfiguration` | `Infrastructure/Data/Configurations/RatRecordConfiguration.cs` |
| GuildRatWatchSettings | `GuildRatWatchSettingsConfiguration` | `Infrastructure/Data/Configurations/GuildRatWatchSettingsConfiguration.cs` |

**Example:**
```csharp
public class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.ToTable("Guilds");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        // ... additional configuration
    }
}
```

---

## Enumerations

The database uses several enumerations stored as integers. These enums are defined in `src/DiscordBot.Core/Enums/`.

### AuditLogCategory

Categories for grouping audit log entries.

| Value | Name | Description |
|-------|------|-------------|
| 1 | User | User-related actions (login, profile updates, ban, kick) |
| 2 | Guild | Guild-related actions (settings, channel management) |
| 3 | Configuration | Configuration-related actions (bot settings, feature toggles) |
| 4 | Security | Security-related actions (permission changes, role modifications) |
| 5 | Command | Command execution actions (slash command usage) |
| 6 | Message | Message-related actions (deletion, editing) |
| 7 | System | System-level actions (bot startup, shutdown, errors) |

### AuditLogAction

Specific actions performed in audit log entries.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Created | New entity created |
| 2 | Updated | Existing entity updated |
| 3 | Deleted | Entity deleted |
| 4 | Login | User logged in |
| 5 | Logout | User logged out |
| 6 | PermissionChanged | Permissions changed for user or role |
| 7 | SettingChanged | Configuration setting changed |
| 8 | CommandExecuted | Command executed |
| 9 | MessageDeleted | Message deleted |
| 10 | MessageEdited | Message edited |
| 11 | UserBanned | User banned from guild |
| 12 | UserUnbanned | User unbanned from guild |
| 13 | UserKicked | User kicked from guild |
| 14 | RoleAssigned | Role assigned to user |
| 15 | RoleRemoved | Role removed from user |
| 16 | BotStarted | Discord bot started |
| 17 | BotStopped | Discord bot stopped |
| 18 | BotConnected | Bot connected to Discord gateway |
| 19 | BotDisconnected | Bot disconnected from Discord gateway |

### AuditLogActorType

Type of actor that performed an audited action.

| Value | Name | Description |
|-------|------|-------------|
| 1 | User | Action performed by authenticated user |
| 2 | System | Action performed by system (automated process, scheduled task) |
| 3 | Bot | Action performed by Discord bot itself |

### MessageSource

Source/origin of a Discord message.

| Value | Name | Description |
|-------|------|-------------|
| 1 | DirectMessage | Message sent in direct message (DM) channel |
| 2 | ServerChannel | Message sent in server (guild) channel |

### ConsentType

Types of user consent for data processing.

| Value | Name | Description |
|-------|------|-------------|
| 1 | MessageLogging | Consent for logging user messages and interactions |

**Note:** Future consent types (Analytics, LLMInteraction, etc.) can be added as needed.

### ScheduleFrequency

Frequency at which scheduled messages should be sent.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Once | Message sent only once at specified time |
| 2 | Hourly | Message sent every hour |
| 3 | Daily | Message sent once per day |
| 4 | Weekly | Message sent once per week |
| 5 | Monthly | Message sent once per month |
| 6 | Custom | Message sent according to custom cron expression |

### RatWatchStatus

Status states for Rat Watch accountability trackers.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Pending | Waiting for scheduled time |
| 1 | ClearedEarly | Accused checked in before scheduled time |
| 2 | Voting | Voting in progress |
| 3 | Guilty | Voting complete - guilty verdict |
| 4 | NotGuilty | Voting complete - not guilty verdict |
| 5 | Expired | Bot was offline, watch expired without voting |
| 6 | Cancelled | Admin cancelled the watch |

---

## Migrations

### Initial Migration

**Name:** `InitialCreate`
**Created:** 2025-12-09 01:19:28 UTC

Creates all three tables with indexes and foreign key constraints.

**Apply Migration:**
```bash
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

**Create New Migration:**
```bash
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

---

## Database Providers

### SQLite (Development/Testing)

**Connection String Format:**
```
Data Source=discordbot.db
```

**Characteristics:**
- File-based, zero configuration
- Excellent for local development
- Limited concurrency support
- Some SQL features unavailable

### Production Databases

The schema supports MySQL, PostgreSQL, and SQL Server with minimal changes. Provider-specific configurations (if needed) are handled in `BotDbContext`.

**Example for SQL Server:**
```csharp
services.AddDbContext<BotDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
```

---

## Common Queries

### Get All Active Guilds
```csharp
var activeGuilds = await context.Guilds
    .Where(g => g.IsActive)
    .ToListAsync();
```

### Get Recent Command Logs for a Guild
```csharp
var logs = await context.CommandLogs
    .Where(c => c.GuildId == guildId)
    .OrderByDescending(c => c.ExecutedAt)
    .Take(100)
    .Include(c => c.User)
    .ToListAsync();
```

### Get Failed Commands
```csharp
var failedCommands = await context.CommandLogs
    .Where(c => !c.Success)
    .OrderByDescending(c => c.ExecutedAt)
    .Take(50)
    .ToListAsync();
```

### Get Recently Active Users
```csharp
var cutoff = DateTime.UtcNow.AddDays(-7);
var activeUsers = await context.Users
    .Where(u => u.LastSeenAt >= cutoff)
    .OrderByDescending(u => u.LastSeenAt)
    .ToListAsync();
```

---

## Performance Considerations

### Indexes

All indexes are created to optimize common query patterns:
- Guild active status filtering
- User activity tracking
- Command log filtering by guild, user, command name, and date
- Failed command queries

### Query Tips

1. **Use AsNoTracking for Read-Only Queries:**
   ```csharp
   var guilds = await context.Guilds
       .AsNoTracking()
       .Where(g => g.IsActive)
       .ToListAsync();
   ```

2. **Include Related Data Only When Needed:**
   ```csharp
   // Only include if you need command logs
   var guild = await context.Guilds
       .Include(g => g.CommandLogs)
       .FirstOrDefaultAsync(g => g.Id == guildId);
   ```

3. **Use Pagination for Large Result Sets:**
   ```csharp
   var logs = await context.CommandLogs
       .OrderByDescending(c => c.ExecutedAt)
       .Skip(page * pageSize)
       .Take(pageSize)
       .ToListAsync();
   ```

---

## Backup and Maintenance

### SQLite Backup
```bash
# Simple file copy (ensure bot is stopped)
copy discordbot.db discordbot.backup.db

# Or use SQLite command line tool
sqlite3 discordbot.db ".backup 'discordbot.backup.db'"
```

### Database Cleanup

Consider implementing periodic cleanup for old command logs:
```csharp
// Delete logs older than 90 days
var cutoff = DateTime.UtcNow.AddDays(-90);
await context.CommandLogs
    .Where(c => c.ExecutedAt < cutoff)
    .ExecuteDeleteAsync();
```

---

## Troubleshooting

### Migration Issues

**Problem:** Migration fails with "table already exists"
**Solution:** Drop database and recreate, or manually align schema

```bash
# Reset database (development only)
dotnet ef database drop --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

### ulong Conversion Issues

**Problem:** Snowflake IDs appear as negative numbers in database
**Solution:** This is expected. ulong values > Int64.MaxValue are stored as negative signed longs. The conversion back to ulong is handled by EF Core.

**Example:**
- Application: `987654321098765432` (ulong)
- Database: `-8792092752610786184` (long)
- Retrieved: `987654321098765432` (ulong) ✓

### Connection String Not Found

**Problem:** `GetConnectionString("DefaultConnection")` returns null
**Solution:** Add connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=discordbot.db"
  }
}
```

---

## Summary

The database schema has evolved significantly from the initial three tables to support comprehensive features:

**Core Functionality:**
- Guilds, Users, CommandLogs - Original tracking and logging
- AuditLogs - Comprehensive system audit trail
- MessageLogs - Discord message history and analytics

**Feature-Specific Tables:**
- ScheduledMessages - Recurring and one-time message automation
- WelcomeConfigurations - Per-guild welcome message settings
- UserConsents - GDPR compliance and privacy tracking

**Rat Watch System:**
- RatWatches - Accountability tracker instances
- RatVotes - Voting system for verdicts
- RatRecords - Permanent guilty verdict records
- GuildRatWatchSettings - Per-guild Rat Watch configuration

All entities follow consistent patterns:
- Discord snowflake IDs stored as `long` with conversion to/from `ulong`
- UTC timestamps for all datetime fields
- Comprehensive indexing for common query patterns
- Appropriate cascading delete behaviors
- Enums stored as integers with clear documentation

---

*Document Version: 2.0*
*Created: December 2024*
*Last Updated: December 30, 2024 - Added v0.3.0 entities (AuditLogs, ScheduledMessages, WelcomeConfigurations, MessageLogs, UserConsents, Rat Watch system)*
