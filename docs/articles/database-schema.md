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
| IconUrl | TEXT | Yes | NULL | MaxLength: 500 | URL to guild's Discord icon/image |
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
    IconUrl TEXT,
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
| AvatarUrl | TEXT | Yes | NULL | MaxLength: 500 | URL to user's Discord avatar |
| IsBot | INTEGER (bool) | No | false | - | Whether user is a bot account |
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
    AvatarUrl TEXT,
    IsBot INTEGER NOT NULL DEFAULT 0,
    FirstSeenAt TEXT NOT NULL,
    LastSeenAt TEXT NOT NULL
);

CREATE INDEX IX_Users_LastSeenAt ON Users (LastSeenAt);
```

**Notes:**
- `Discriminator` defaults to "0" for new Discord usernames (post-discriminator system)
- Legacy usernames have 4-digit discriminators (e.g., "1234")
- `AvatarUrl` is populated from Discord API and updated on user cache refresh
- `IsBot` identifies bot accounts vs. human users

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
| CorrelationId | TEXT | Yes | NULL | MaxLength: 100 | Distributed tracing correlation ID |

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
    CorrelationId TEXT,
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
CREATE INDEX IX_CommandLogs_CorrelationId ON CommandLogs (CorrelationId);
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

## User & Identity Tables

### ApplicationUser

ASP.NET Core Identity user extended with Discord OAuth fields for web portal authentication and account linking.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | TEXT | No | - | PRIMARY KEY | Unique identity user identifier (GUID as string) |
| UserName | TEXT | No | - | MaxLength: 256, UNIQUE | Username for login |
| Email | TEXT | Yes | NULL | MaxLength: 256 | Email address |
| NormalizedUserName | TEXT | No | - | MaxLength: 256, UNIQUE | Normalized username for queries |
| NormalizedEmail | TEXT | Yes | NULL | MaxLength: 256 | Normalized email for queries |
| PasswordHash | TEXT | Yes | NULL | - | Hashed password |
| SecurityStamp | TEXT | Yes | NULL | - | Security timestamp for logout validation |
| ConcurrencyStamp | TEXT | Yes | NULL | - | Row version for concurrency |
| PhoneNumber | TEXT | Yes | NULL | - | Phone number (optional) |
| PhoneNumberConfirmed | INTEGER (bool) | No | false | - | Whether phone is confirmed |
| EmailConfirmed | INTEGER (bool) | No | false | - | Whether email is confirmed |
| LockoutEnabled | INTEGER (bool) | No | true | - | Whether account can be locked |
| LockoutEnd | TEXT (DateTime) | Yes | NULL | - | Lockout expiration timestamp |
| AccessFailedCount | INTEGER | No | 0 | - | Failed login attempts |
| DiscordUserId | INTEGER (long) | Yes | NULL | - | Linked Discord user ID |
| DiscordAccessToken | TEXT | Yes | NULL | - | Discord OAuth access token |
| DiscordRefreshToken | TEXT | Yes | NULL | - | Discord OAuth refresh token |
| TwoFactorEnabled | INTEGER (bool) | No | false | - | Whether 2FA is enabled |

**Indexes:**
- `IX_AspNetUsers_NormalizedUserName` on `NormalizedUserName` - Username login lookup
- `IX_AspNetUsers_NormalizedEmail` on `NormalizedEmail` - Email recovery lookup
- `IX_AspNetUsers_DiscordUserId` on `DiscordUserId` - Link Discord users to accounts

---

### GuildMember

Tracks guild membership and member-specific settings per guild.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique membership record ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild this membership belongs to |
| UserId | INTEGER (long) | No | - | FOREIGN KEY → Users(Id) | User in the guild |
| Nickname | TEXT | Yes | NULL | MaxLength: 32 | Member's guild nickname |
| JoinedAt | TEXT (DateTime) | No | - | - | When user joined the guild |
| Roles | TEXT (JSON) | Yes | NULL | - | JSON array of Discord role IDs |

**Indexes:**
- `IX_GuildMember_GuildId_UserId` on `(GuildId, UserId)` (UNIQUE) - One record per member per guild
- `IX_GuildMember_UserId` on `UserId` - User's guild memberships

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`
- `UserId` → `Users(Id)` with `ON DELETE CASCADE`

---

### UserGuildAccess

Portal access levels and permissions for web UI by guild.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique access record ID |
| UserId | TEXT | No | - | FOREIGN KEY → AspNetUsers(Id) | Portal user |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Target guild |
| Role | INTEGER | No | - | Enum: AccessRole | Access level (SuperAdmin, Admin, Moderator, Viewer) |
| AssignedAt | TEXT (DateTime) | No | - | - | When role was assigned |
| AssignedBy | TEXT | Yes | NULL | - | Portal user ID who assigned role |

**Indexes:**
- `IX_UserGuildAccess_UserId_GuildId` on `(UserId, GuildId)` (UNIQUE) - One role per user per guild
- `IX_UserGuildAccess_GuildId` on `GuildId` - All users with access to guild

**Foreign Keys:**
- `UserId` → `AspNetUsers(Id)` with `ON DELETE CASCADE`
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

**Enum Values (AccessRole):**
- 0 = SuperAdmin (Full system access)
- 1 = Admin (Guild administration)
- 2 = Moderator (Moderation and logging)
- 3 = Viewer (Read-only access)

---

### UserDiscordGuild

User-guild relationships for portal membership verification and Discord permissions tracking.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique relationship record ID |
| UserId | INTEGER (long) | No | - | FOREIGN KEY → Users(Id) | Discord user |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Discord guild |
| IsOwner | INTEGER (bool) | No | false | - | Whether user owns the guild |
| Permissions | TEXT (JSON) | Yes | NULL | - | JSON object of Discord permissions |
| RefreshableAt | TEXT (DateTime) | No | - | - | When permissions data was last updated |

**Indexes:**
- `IX_UserDiscordGuild_UserId_GuildId` on `(UserId, GuildId)` (UNIQUE) - One record per user per guild
- `IX_UserDiscordGuild_GuildId` on `GuildId` - All users in a guild

**Foreign Keys:**
- `UserId` → `Users(Id)` with `ON DELETE CASCADE`
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

## Moderation Tables

### ModerationCase

Records of moderation actions taken against users (bans, mutes, warns, etc.).

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique case identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild where action occurred |
| UserId | INTEGER (long) | No | - | - | User being moderated |
| ModeratorId | INTEGER (long) | No | - | - | Moderator who took action |
| CaseType | INTEGER | No | - | Enum: ModerationCaseType | Type of action (Warn, Mute, Ban, Kick, etc.) |
| Reason | TEXT | Yes | NULL | MaxLength: 500 | Reason for moderation action |
| Duration | INTEGER | Yes | NULL | - | Duration in minutes (NULL = permanent) |
| CreatedAt | TEXT (DateTime) | No | - | - | When action was taken |
| ExpiresAt | TEXT (DateTime) | Yes | NULL | - | When action expires (if temporary) |
| IsActive | INTEGER (bool) | No | true | - | Whether action is currently active |

**Indexes:**
- `IX_ModerationCase_GuildId_UserId` on `(GuildId, UserId)` - User's moderation history in guild
- `IX_ModerationCase_CreatedAt` on `CreatedAt` - Recent cases query
- `IX_ModerationCase_ExpiresAt_IsActive` on `(ExpiresAt, IsActive)` - Expiration checks

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

**Enum Values (ModerationCaseType):**
- 0 = Warn
- 1 = Mute
- 2 = Ban
- 3 = Kick
- 4 = Softban
- 5 = TempBan
- 6 = TempMute

---

### ModNote

Moderator notes attached to users for communication and documentation.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique note identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| UserId | INTEGER (long) | No | - | - | User the note is about |
| ModeratorId | INTEGER (long) | No | - | - | Moderator who created note |
| Content | TEXT | No | - | MaxLength: 2000 | Note content |
| CreatedAt | TEXT (DateTime) | No | - | - | When note was created |
| UpdatedAt | TEXT (DateTime) | No | - | - | When note was last updated |
| IsPrivate | INTEGER (bool) | No | false | - | Whether note is private (not visible to user) |

**Indexes:**
- `IX_ModNote_GuildId_UserId` on `(GuildId, UserId)` - User's notes in guild
- `IX_ModNote_CreatedAt` on `CreatedAt` - Recent notes query

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### ModTag

Reusable tags for categorizing and marking users during moderation.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique tag identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild this tag belongs to |
| Name | TEXT | No | - | MaxLength: 50 | Tag name |
| Description | TEXT | Yes | NULL | MaxLength: 200 | Tag description |
| Color | TEXT | Yes | NULL | MaxLength: 7 | Hex color code (#RRGGBB) |
| Category | TEXT | Yes | NULL | MaxLength: 50 | Tag category for grouping |
| CreatedAt | TEXT (DateTime) | No | - | - | When tag was created |

**Indexes:**
- `IX_ModTag_GuildId` on `GuildId` - Guild's tags
- `IX_ModTag_GuildId_Name` on `(GuildId, Name)` (UNIQUE) - Tag name uniqueness per guild

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### UserModTag

Assignment of mod tags to users for tracking and categorization.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique assignment record ID |
| UserId | INTEGER (long) | No | - | - | User tagged |
| TagId | BLOB (Guid) | No | - | FOREIGN KEY → ModTag(Id) | The tag |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| AssignedBy | INTEGER (long) | No | - | - | Moderator who assigned tag |
| AssignedAt | TEXT (DateTime) | No | - | - | When tag was assigned |

**Indexes:**
- `IX_UserModTag_UserId_GuildId` on `(UserId, GuildId)` - User's tags in guild
- `IX_UserModTag_TagId` on `TagId` - Users with a specific tag

**Foreign Keys:**
- `TagId` → `ModTag(Id)` with `ON DELETE CASCADE`
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### GuildModerationConfig

Per-guild moderation system configuration and settings.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| GuildId | INTEGER (long) | No | - | PRIMARY KEY, FOREIGN KEY → Guilds(Id) | Guild ID |
| MuteRoleId | INTEGER (long) | Yes | NULL | - | Discord role ID for mutes |
| LogChannelId | INTEGER (long) | Yes | NULL | - | Discord channel ID for mod logs |
| AutoModEnabled | INTEGER (bool) | No | false | - | Whether auto-moderation is enabled |
| RaidProtectionEnabled | INTEGER (bool) | No | false | - | Whether raid detection is enabled |
| CreatedAt | TEXT (DateTime) | No | - | - | Configuration creation timestamp |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp |

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### FlaggedEvent

Flagging system for suspicious or notable activity requiring moderator attention.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique flag identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild where event occurred |
| UserId | INTEGER (long) | No | - | - | User involved in flagged event |
| EventType | TEXT | No | - | MaxLength: 50 | Type of event (Spam, RaidJoin, SuspiciousBehavior, etc.) |
| Description | TEXT | No | - | MaxLength: 500 | Event description |
| Status | INTEGER | No | 0 | Enum: FlaggedEventStatus | Current status (New, InProgress, Resolved, Dismissed) |
| FlaggedAt | TEXT (DateTime) | No | - | - | When event was flagged |
| ResolvedAt | TEXT (DateTime) | Yes | NULL | - | When issue was resolved |
| ResolvedBy | INTEGER (long) | Yes | NULL | - | Moderator who resolved |

**Indexes:**
- `IX_FlaggedEvent_GuildId_Status` on `(GuildId, Status)` - Open flags per guild
- `IX_FlaggedEvent_UserId` on `UserId` - User's flagged events

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### Watchlist

Watchlist entries for tracking problematic users.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique watchlist entry ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| UserId | INTEGER (long) | No | - | - | User on watchlist |
| Reason | TEXT | No | - | MaxLength: 500 | Reason for watchlist entry |
| AddedBy | INTEGER (long) | No | - | - | Moderator who added entry |
| AddedAt | TEXT (DateTime) | No | - | - | When entry was added |
| ExpiresAt | TEXT (DateTime) | Yes | NULL | - | When watchlist entry expires (NULL = permanent) |

**Indexes:**
- `IX_Watchlist_GuildId_ExpiresAt` on `(GuildId, ExpiresAt)` - Active entries per guild
- `IX_Watchlist_UserId` on `UserId` - User watchlist entries

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

## Audio & TTS Tables

### Sound

Soundboard entries - uploaded audio files for playback.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique sound identifier |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild this sound belongs to |
| Name | TEXT | No | - | MaxLength: 100 | Sound name/alias |
| FileName | TEXT | No | - | MaxLength: 255 | Original file name |
| FilePath | TEXT | No | - | MaxLength: 500 | Path to audio file |
| Duration | REAL | No | - | - | Duration in seconds |
| FileSize | INTEGER | No | - | - | File size in bytes |
| UploadedBy | INTEGER (long) | No | - | - | User who uploaded sound |
| UploadedAt | TEXT (DateTime) | No | - | - | Upload timestamp |
| PlayCount | INTEGER | No | 0 | - | Total times played |
| IsActive | INTEGER (bool) | No | true | - | Whether sound is available |

**Indexes:**
- `IX_Sound_GuildId_IsActive` on `(GuildId, IsActive)` - Guild's active sounds
- `IX_Sound_GuildId_Name` on `(GuildId, Name)` (UNIQUE) - Sound name lookup

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### SoundPlayLog

Log of all sound playbacks for analytics and auditing.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique play log entry ID |
| SoundId | BLOB (Guid) | No | - | FOREIGN KEY → Sound(Id) | Sound that was played |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| UserId | INTEGER (long) | No | - | - | User who triggered playback |
| ChannelId | INTEGER (long) | No | - | - | Voice channel where played |
| PlayedAt | TEXT (DateTime) | No | - | - | Play timestamp |

**Indexes:**
- `IX_SoundPlayLog_SoundId` on `SoundId` - Play history per sound
- `IX_SoundPlayLog_PlayedAt` on `PlayedAt` - Recent plays query

**Foreign Keys:**
- `SoundId` → `Sound(Id)` with `ON DELETE CASCADE`
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### GuildAudioSettings

Per-guild audio and soundboard configuration.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| GuildId | INTEGER (long) | No | - | PRIMARY KEY, FOREIGN KEY → Guilds(Id) | Guild ID |
| DefaultVolume | REAL | No | 0.5 | - | Default playback volume (0-1) |
| MaxQueueSize | INTEGER | No | 50 | - | Maximum playback queue size |
| AutoDisconnect | INTEGER | No | 300 | - | Auto-disconnect after N seconds (0 = disabled) |
| DjRoleId | INTEGER (long) | Yes | NULL | - | Discord role ID with DJ permissions |
| CreatedAt | TEXT (DateTime) | No | - | - | Configuration creation timestamp |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp |

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### TtsMessage

Text-to-speech message history for auditing and analytics.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique TTS message ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| UserId | INTEGER (long) | No | - | - | User who requested TTS |
| Text | TEXT | No | - | MaxLength: 1000 | Text that was synthesized |
| Voice | TEXT | No | - | MaxLength: 100 | Voice/speaker used |
| Duration | REAL | No | - | - | Audio duration in seconds |
| CreatedAt | TEXT (DateTime) | No | - | - | When message was synthesized |

**Indexes:**
- `IX_TtsMessage_GuildId_CreatedAt` on `(GuildId, CreatedAt)` - Guild TTS history
- `IX_TtsMessage_UserId` on `UserId` - User's TTS messages

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### GuildTtsSettings

Per-guild text-to-speech configuration.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| GuildId | INTEGER (long) | No | - | PRIMARY KEY, FOREIGN KEY → Guilds(Id) | Guild ID |
| Enabled | INTEGER (bool) | No | true | - | Whether TTS feature is enabled |
| DefaultVoice | TEXT | No | "en-US-JennyNeural" | MaxLength: 100 | Default voice name |
| MaxLength | INTEGER | No | 200 | - | Maximum characters per TTS request |
| RateLimitSeconds | INTEGER | No | 5 | - | Seconds between TTS requests per user |
| CreatedAt | TEXT (DateTime) | No | - | - | Configuration creation timestamp |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp |

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

## Command Configuration Tables

### CommandModuleConfiguration

Per-guild enable/disable settings for command modules.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique configuration ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild this config applies to |
| ModuleName | TEXT | No | - | MaxLength: 100 | Command module name |
| IsEnabled | INTEGER (bool) | No | true | - | Whether module is enabled |
| ConfiguredBy | TEXT | No | - | MaxLength: 450 | User ID who configured |
| ConfiguredAt | TEXT (DateTime) | No | - | - | Configuration timestamp |

**Indexes:**
- `IX_CommandModuleConfiguration_GuildId_ModuleName` on `(GuildId, ModuleName)` (UNIQUE) - One config per module per guild

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### CommandRoleRestriction

Role-based access control for individual commands.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique restriction ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild this applies to |
| CommandName | TEXT | No | - | MaxLength: 100 | Command name/path |
| RoleId | INTEGER (long) | No | - | - | Discord role ID |
| IsAllowed | INTEGER (bool) | No | true | - | Whether role is allowed/denied |
| ConfiguredAt | TEXT (DateTime) | No | - | - | Configuration timestamp |

**Indexes:**
- `IX_CommandRoleRestriction_GuildId_CommandName_RoleId` on `(GuildId, CommandName, RoleId)` (UNIQUE) - One restriction per role per command

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

## AI Assistant Tables

### AssistantGuildSettings

Per-guild AI assistant (LLM) configuration.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| GuildId | INTEGER (long) | No | - | PRIMARY KEY, FOREIGN KEY → Guilds(Id) | Guild ID |
| Enabled | INTEGER (bool) | No | true | - | Whether assistant is enabled |
| SystemPrompt | TEXT | Yes | NULL | MaxLength: 2000 | Custom system prompt |
| MaxTokens | INTEGER | No | 1024 | - | Maximum response tokens |
| Temperature | REAL | No | 0.7 | - | Response creativity (0-1) |
| AllowedChannels | TEXT (JSON) | Yes | NULL | - | JSON array of allowed channel IDs (null = all) |
| CreatedAt | TEXT (DateTime) | No | - | - | Configuration creation timestamp |
| UpdatedAt | TEXT (DateTime) | No | - | - | Last update timestamp |

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### AssistantUsageMetrics

Daily usage metrics for AI assistant requests per guild and user.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique metric record ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| UserId | INTEGER (long) | No | - | - | User making requests |
| Date | TEXT (Date) | No | - | - | Date of usage (YYYY-MM-DD) |
| TokensUsed | INTEGER | No | 0 | - | Total tokens used that day |
| RequestCount | INTEGER | No | 0 | - | Number of requests |
| Cost | REAL | No | 0.0 | - | Estimated API cost in dollars |

**Indexes:**
- `IX_AssistantUsageMetrics_GuildId_Date` on `(GuildId, Date)` - Guild usage by date
- `IX_AssistantUsageMetrics_UserId_Date` on `(UserId, Date)` - User usage by date

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### AssistantInteractionLog

Detailed log of all AI assistant interactions for auditing and analysis.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique interaction ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| UserId | INTEGER (long) | No | - | - | User who interacted |
| Prompt | TEXT | No | - | - | User's prompt/request |
| Response | TEXT | Yes | NULL | - | Assistant's response |
| TokensUsed | INTEGER | No | 0 | - | Tokens used for request |
| Duration | INTEGER | No | 0 | - | Response time in milliseconds |
| Timestamp | TEXT (DateTime) | No | - | - | Interaction timestamp |

**Indexes:**
- `IX_AssistantInteractionLog_GuildId_Timestamp` on `(GuildId, Timestamp)` - Guild interactions by time
- `IX_AssistantInteractionLog_UserId` on `UserId` - User interactions

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

## Analytics & Metrics Tables

### MemberActivitySnapshot

Aggregated member activity metrics (hourly, daily, weekly, monthly).

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique snapshot ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| Date | TEXT (DateTime) | No | - | - | Snapshot timestamp |
| Granularity | INTEGER | No | - | Enum: Granularity | Time granularity (Hourly, Daily, Weekly, Monthly) |
| ActiveMembers | INTEGER | No | 0 | - | Members with activity in period |
| NewMembers | INTEGER | No | 0 | - | Members who joined in period |
| LeftMembers | INTEGER | No | 0 | - | Members who left in period |
| MessageCount | INTEGER | No | 0 | - | Total messages in period |

**Indexes:**
- `IX_MemberActivitySnapshot_GuildId_Date_Granularity` on `(GuildId, Date, Granularity)` - Guild snapshots

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### ChannelActivitySnapshot

Per-channel activity aggregation.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique snapshot ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| ChannelId | INTEGER (long) | No | - | - | Discord channel ID |
| Date | TEXT (DateTime) | No | - | - | Snapshot timestamp |
| Granularity | INTEGER | No | - | Enum: Granularity | Time granularity |
| MessageCount | INTEGER | No | 0 | - | Messages in period |
| UniqueUsers | INTEGER | No | 0 | - | Unique users who posted |
| AverageLength | REAL | No | 0.0 | - | Average message length |

**Indexes:**
- `IX_ChannelActivitySnapshot_GuildId_ChannelId_Date` on `(GuildId, ChannelId, Date)` - Channel snapshots

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### GuildMetricsSnapshot

Guild-wide metrics aggregation.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique snapshot ID |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| Date | TEXT (DateTime) | No | - | - | Snapshot timestamp |
| Granularity | INTEGER | No | - | Enum: Granularity | Time granularity |
| TotalMembers | INTEGER | No | 0 | - | Total guild members |
| OnlineMembers | INTEGER | No | 0 | - | Currently online members |
| TotalChannels | INTEGER | No | 0 | - | Total guild channels |
| CommandsExecuted | INTEGER | No | 0 | - | Commands executed in period |

**Indexes:**
- `IX_GuildMetricsSnapshot_GuildId_Date_Granularity` on `(GuildId, Date, Granularity)` - Guild metrics

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

### MetricSnapshot

Generic metric snapshots for arbitrary metrics and tags.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique metric record ID |
| MetricName | TEXT | No | - | MaxLength: 100 | Metric name |
| Value | REAL | No | - | - | Metric value |
| Tags | TEXT (JSON) | Yes | NULL | - | JSON object of metric tags |
| Timestamp | TEXT (DateTime) | No | - | - | Metric timestamp |
| Granularity | INTEGER | No | - | Enum: Granularity | Time granularity |

**Indexes:**
- `IX_MetricSnapshot_MetricName_Timestamp` on `(MetricName, Timestamp)` - Metric queries by time

---

### PerformanceAlertConfig

Configuration for performance monitoring alerts and thresholds.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique alert config ID |
| MetricName | TEXT | No | - | MaxLength: 100 | Metric being monitored |
| Threshold | REAL | No | - | - | Alert threshold value |
| Operator | TEXT | No | - | MaxLength: 10 | Comparison operator (>, <, ==, etc.) |
| Severity | INTEGER | No | 1 | Enum: Severity | Alert severity (0=Info, 1=Warning, 2=Critical) |
| IsEnabled | INTEGER (bool) | No | true | - | Whether alert is active |
| NotifyChannel | INTEGER (long) | Yes | NULL | - | Discord channel for notifications |
| CreatedAt | TEXT (DateTime) | No | - | - | Configuration creation timestamp |

**Indexes:**
- `IX_PerformanceAlertConfig_IsEnabled` on `IsEnabled` - Active alerts lookup

---

### PerformanceIncident

Performance incidents triggered by alert thresholds.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique incident ID |
| AlertConfigId | BLOB (Guid) | No | - | FOREIGN KEY → PerformanceAlertConfig(Id) | Alert that triggered |
| Value | REAL | No | - | - | Measured metric value |
| Threshold | REAL | No | - | - | Threshold that was exceeded |
| Status | INTEGER | No | 0 | Enum: IncidentStatus | Current status (Active, Resolved) |
| StartedAt | TEXT (DateTime) | No | - | - | When incident started |
| ResolvedAt | TEXT (DateTime) | Yes | NULL | - | When incident resolved |
| Notes | TEXT | Yes | NULL | MaxLength: 500 | Incident notes |

**Indexes:**
- `IX_PerformanceIncident_Status_StartedAt` on `(Status, StartedAt)` - Active incidents

**Foreign Keys:**
- `AlertConfigId` → `PerformanceAlertConfig(Id)` with `ON DELETE CASCADE`

---

### UserActivityEvent

Detailed user activity event logging for analytics and insights.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique event ID |
| UserId | INTEGER (long) | No | - | - | User involved in event |
| GuildId | INTEGER (long) | No | - | FOREIGN KEY → Guilds(Id) | Guild context |
| EventType | TEXT | No | - | MaxLength: 100 | Type of event (MessageSent, CommandExecuted, VoiceJoin, etc.) |
| Details | TEXT (JSON) | Yes | NULL | - | JSON details about the event |
| Timestamp | TEXT (DateTime) | No | - | - | Event timestamp |

**Indexes:**
- `IX_UserActivityEvent_UserId_Timestamp` on `(UserId, Timestamp)` - User activity timeline
- `IX_UserActivityEvent_GuildId_Timestamp` on `(GuildId, Timestamp)` - Guild activity timeline

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE CASCADE`

---

## Other Tables

### Reminder

User reminders with temporal scheduling and recurrence support.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique reminder ID |
| UserId | INTEGER (long) | No | - | - | User who created reminder |
| GuildId | INTEGER (long) | Yes | NULL | FOREIGN KEY → Guilds(Id) | Guild context (NULL for DMs) |
| ChannelId | INTEGER (long) | Yes | NULL | - | Discord channel for reminder |
| Message | TEXT | No | - | MaxLength: 1000 | Reminder message |
| RemindAt | TEXT (DateTime) | No | - | - | When to send reminder |
| Status | INTEGER | No | 0 | Enum: ReminderStatus | Current status (Pending, Completed, Cancelled) |
| CreatedAt | TEXT (DateTime) | No | - | - | When reminder was created |
| Recurrence | TEXT | Yes | NULL | MaxLength: 100 | Recurrence pattern (cron or human-readable) |

**Indexes:**
- `IX_Reminder_UserId_Status` on `(UserId, Status)` - User's reminders
- `IX_Reminder_RemindAt_Status` on `(RemindAt, Status)` - Reminders to execute

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE SET NULL`

---

### VerificationCode

Email/phone verification codes for account security.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique code record ID |
| UserId | TEXT | No | - | FOREIGN KEY → AspNetUsers(Id) | User being verified |
| Code | TEXT | No | - | MaxLength: 10 | Verification code |
| Type | TEXT | No | - | MaxLength: 50 | Code type (Email, SMS, etc.) |
| ExpiresAt | TEXT (DateTime) | No | - | - | When code expires |
| IsUsed | INTEGER (bool) | No | false | - | Whether code has been used |
| CreatedAt | TEXT (DateTime) | No | - | - | Code creation timestamp |

**Indexes:**
- `IX_VerificationCode_UserId_Type` on `(UserId, Type)` - User's verification codes
- `IX_VerificationCode_ExpiresAt` on `ExpiresAt` - Expired code cleanup

**Foreign Keys:**
- `UserId` → `AspNetUsers(Id)` with `ON DELETE CASCADE`

---

### ApplicationSetting

Dynamic application-wide settings and configuration.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique setting ID |
| Key | TEXT | No | - | MaxLength: 100, UNIQUE | Setting key/name |
| Value | TEXT | No | - | - | Setting value |
| Category | TEXT | Yes | NULL | MaxLength: 50 | Setting category |
| DataType | TEXT | No | "string" | MaxLength: 50 | Value data type (string, int, bool, json, etc.) |
| Description | TEXT | Yes | NULL | MaxLength: 500 | Setting description |
| IsSecret | INTEGER (bool) | No | false | - | Whether value is sensitive |

**Indexes:**
- `IX_ApplicationSetting_Key` on `Key` - Setting lookup
- `IX_ApplicationSetting_Category` on `Category` - Category queries

---

### UserNotification

User notification queue for in-app and delivery notifications.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique notification ID |
| UserId | TEXT | No | - | FOREIGN KEY → AspNetUsers(Id) | Recipient user |
| Title | TEXT | No | - | MaxLength: 200 | Notification title |
| Message | TEXT | No | - | MaxLength: 500 | Notification message |
| Type | TEXT | No | "Info" | MaxLength: 50 | Notification type (Info, Warning, Error, Success) |
| IsRead | INTEGER (bool) | No | false | - | Whether user has read notification |
| CreatedAt | TEXT (DateTime) | No | - | - | Notification creation timestamp |
| ReadAt | TEXT (DateTime) | Yes | NULL | - | When notification was read |
| Link | TEXT | Yes | NULL | MaxLength: 500 | Optional link/action URL |

**Indexes:**
- `IX_UserNotification_UserId_IsRead` on `(UserId, IsRead)` - User's unread notifications
- `IX_UserNotification_CreatedAt` on `CreatedAt` - Recent notifications query

**Foreign Keys:**
- `UserId` → `AspNetUsers(Id)` with `ON DELETE CASCADE`

---

### Theme

UI theme customization and theming system.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique theme ID |
| Name | TEXT | No | - | MaxLength: 100, UNIQUE | Theme name |
| PrimaryColor | TEXT | No | - | MaxLength: 7 | Primary color hex code (#RRGGBB) |
| SecondaryColor | TEXT | No | - | MaxLength: 7 | Secondary color hex code |
| AccentColor | TEXT | No | - | MaxLength: 7 | Accent color hex code |
| IsDefault | INTEGER (bool) | No | false | - | Whether this is the default theme |
| CreatedBy | TEXT | Yes | NULL | MaxLength: 450 | User ID who created theme |
| CreatedAt | TEXT (DateTime) | No | - | - | Theme creation timestamp |

**Indexes:**
- `IX_Theme_IsDefault` on `IsDefault` - Default theme lookup
- `IX_Theme_Name` on `Name` - Theme lookup by name

---

### UserActivityLog

Audit log of user actions for security and compliance.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER | No | Auto-increment | PRIMARY KEY | Unique log entry ID |
| UserId | TEXT | No | - | FOREIGN KEY → AspNetUsers(Id) | User who performed action |
| Action | TEXT | No | - | MaxLength: 100 | Action performed |
| Details | TEXT (JSON) | Yes | NULL | - | JSON action details |
| Timestamp | TEXT (DateTime) | No | - | - | Action timestamp |
| IpAddress | TEXT | Yes | NULL | MaxLength: 45 | IP address (IPv4/IPv6) |

**Indexes:**
- `IX_UserActivityLog_UserId_Timestamp` on `(UserId, Timestamp)` - User activity history
- `IX_UserActivityLog_Timestamp` on `Timestamp` - Recent actions query

**Foreign Keys:**
- `UserId` → `AspNetUsers(Id)` with `ON DELETE CASCADE`

---

## Complete Entity Relationships

### Full Entity Relationship Diagram

```
Core Entities
├── Guild (1) ──────┬── CommandLogs (M)
│                   ├── MessageLogs (M)
│                   ├── AuditLogs (M)
│                   ├── ScheduledMessages (M)
│                   ├── WelcomeConfiguration (1)
│                   ├── UserDiscordGuild (M)
│                   ├── GuildMember (M)
│                   │
├── Moderation
│                   ├── ModerationCase (M)
│                   ├── ModNote (M)
│                   ├── ModTag (M)
│                   ├── GuildModerationConfig (1)
│                   ├── FlaggedEvent (M)
│                   ├── Watchlist (M)
│                   │
├── Audio/TTS
│                   ├── Sound (M)
│                   ├── GuildAudioSettings (1)
│                   ├── GuildTtsSettings (1)
│                   ├── TtsMessage (M)
│                   │
├── RatWatch System
│                   ├── RatWatches (M)
│                   ├── GuildRatWatchSettings (1)
│                   │
├── Configuration
│                   ├── CommandModuleConfiguration (M)
│                   ├── AssistantGuildSettings (1)
│                   │
├── Analytics
│                   ├── MemberActivitySnapshot (M)
│                   ├── ChannelActivitySnapshot (M)
│                   ├── GuildMetricsSnapshot (M)
│                   ├── UserActivityEvent (M)
│                   │
└── Performance
                    ├── PerformanceAlertConfig (M)
                    └── PerformanceIncident (M)

User (Discord)
    ├── GuildMember (M) - Guild memberships
    ├── CommandLog (M) - Commands executed
    ├── MessageLog (M) - Messages sent
    ├── Reminder (M) - User reminders
    ├── RatWatch (M) - As accused/initiator
    ├── RatVote (M) - Votes cast
    ├── UserModTag (M) - Applied tags
    ├── TtsMessage (M) - TTS history
    └── UserActivityEvent (M) - Activity log

ApplicationUser (Identity)
    ├── UserGuildAccess (M) - Portal roles per guild
    ├── UserNotification (M) - Notifications
    ├── VerificationCode (M) - Verification codes
    └── UserActivityLog (M) - Action audit trail

RatWatch System
├── RatWatches (1) ─── RatVotes (M)
└── RatWatches (1) ─── RatRecords (1)

Sound
    └── SoundPlayLog (M) - Play history

PerformanceAlertConfig (1)
    └── PerformanceIncident (M) - Triggered incidents
```

---

## Enum Reference

### Granularity

Time granularity for aggregated metrics.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Hourly | Hourly aggregation |
| 1 | Daily | Daily aggregation |
| 2 | Weekly | Weekly aggregation |
| 3 | Monthly | Monthly aggregation |

### ReminderStatus

Status values for reminders.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Pending | Reminder awaiting execution |
| 1 | Completed | Reminder has been sent |
| 2 | Cancelled | Reminder was cancelled by user |

### FlaggedEventStatus

Status values for flagged events.

| Value | Name | Description |
|-------|------|-------------|
| 0 | New | Event flagged, not yet reviewed |
| 1 | InProgress | Moderator is investigating |
| 2 | Resolved | Issue has been addressed |
| 3 | Dismissed | Flag was invalid |

### ModerationCaseType

Types of moderation actions.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Warn | User warning |
| 1 | Mute | Temporary or permanent mute |
| 2 | Ban | User ban from guild |
| 3 | Kick | User removal from guild |
| 4 | Softban | Ban and kick (removes messages) |
| 5 | TempBan | Temporary ban |
| 6 | TempMute | Temporary mute |

### AccessRole

Portal access roles for web UI.

| Value | Name | Description |
|-------|------|-------------|
| 0 | SuperAdmin | Full system access |
| 1 | Admin | Guild administration access |
| 2 | Moderator | Moderation and logging access |
| 3 | Viewer | Read-only access |

### Severity

Alert severity levels.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Info | Informational alert |
| 1 | Warning | Warning-level alert |
| 2 | Critical | Critical severity alert |

### IncidentStatus

Performance incident status.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Active | Incident currently occurring |
| 1 | Resolved | Incident has been resolved |

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

*Document Version: 3.0*
*Created: December 2024*
*Last Updated: January 25, 2026 - Added v0.12.1 entities: User & Identity (ApplicationUser, GuildMember, UserGuildAccess, UserDiscordGuild), Moderation (ModerationCase, ModNote, ModTag, UserModTag, GuildModerationConfig, FlaggedEvent, Watchlist), Audio & TTS (Sound, SoundPlayLog, GuildAudioSettings, TtsMessage, GuildTtsSettings), Command Configuration (CommandModuleConfiguration, CommandRoleRestriction), AI Assistant (AssistantGuildSettings, AssistantUsageMetrics, AssistantInteractionLog), Analytics (MemberActivitySnapshot, ChannelActivitySnapshot, GuildMetricsSnapshot, MetricSnapshot, UserActivityEvent), Performance (PerformanceAlertConfig, PerformanceIncident), Other (Reminder, VerificationCode, ApplicationSetting, UserNotification, Theme, UserActivityLog); Updated existing entities with missing fields (User.AvatarUrl, User.IsBot, Guild.IconUrl, CommandLog.CorrelationId)*
