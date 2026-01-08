# Consent and Privacy Management

This guide provides comprehensive documentation for the Consent and Privacy features, which enable GDPR-compliant data processing controls for Discord bot users. Users can manage their consent preferences for various data collection activities through both Discord slash commands and the web admin interface.

**Feature Status:** Implemented (Issue #314)

**Access Level:**
- Slash commands: All Discord users
- Web UI Privacy page: Authenticated users with linked Discord accounts

**Last Updated:** 2026-01-08

**Related Documentation:**
- [Authorization Policies](authorization-policies.md)
- [Identity Configuration](identity-configuration.md)
- [API Endpoints](api-endpoints.md)

---

## Table of Contents

- [Overview](#overview)
- [Consent Scope](#consent-scope)
  - [Data Collected WITHOUT Consent](#data-collected-without-consent-operational-data)
  - [Data Collected WITH Consent](#data-collected-with-consent-messagelogging)
  - [Analytics and Consent](#analytics-and-consent)
- [Consent Types](#consent-types)
- [Slash Commands](#slash-commands)
  - [/consent grant](#consent-grant)
  - [/consent revoke](#consent-revoke)
  - [/consent status](#consent-status)
- [Web UI Privacy Page](#web-ui-privacy-page)
- [User Data Handling](#user-data-handling)
  - [Data Collection](#data-collection)
  - [Data Storage](#data-storage)
  - [Data Retention](#data-retention)
  - [Data Export](#data-export)
  - [Data Deletion](#data-deletion)
- [ConsentService Architecture](#consentservice-architecture)
  - [Service Interface](#service-interface)
  - [Caching Strategy](#caching-strategy)
  - [Batch Operations](#batch-operations)
- [How Consent Affects Bot Behavior](#how-consent-affects-bot-behavior)
- [Admin Considerations](#admin-considerations)
- [Implementation Details](#implementation-details)
- [Database Schema](#database-schema)
- [Security and Compliance](#security-and-compliance)

---

## Overview

The Consent and Privacy Management system provides GDPR-compliant mechanisms for Discord bot users to control how their data is processed. The system implements an opt-in consent model where users must explicitly grant permission before their data is collected or processed.

**Key Principles:**

- **Opt-in by Default:** Users must explicitly grant consent before message content is collected
- **Granular Control:** Different consent types for different data processing activities
- **Transparent Processing:** Users can view their current consent status and history
- **Easy Revocation:** Users can revoke consent at any time through simple commands
- **Audit Trail:** All consent grants and revocations are logged with timestamps and source

> **Important Distinction:** Consent applies specifically to **message content logging**. Basic user metadata (username, avatar, guild membership) is collected for all guild members as essential operational data for bot functionality. See [Consent Scope](#consent-scope) for details.

**Architecture:**

- **Entity:** `UserConsent` (Core/Entities)
- **Enum:** `ConsentType` (Core/Enums)
- **Service:** `IConsentService` / `ConsentService` (Core/Interfaces, Bot/Services)
- **Repository:** `IUserConsentRepository` / `UserConsentRepository` (Core/Interfaces, Infrastructure/Data/Repositories)
- **Commands:** `ConsentModule` (Bot/Commands)
- **Web UI:** `Privacy.cshtml` (Bot/Pages/Account)
- **DTOs:** `ConsentStatusDto`, `ConsentHistoryEntryDto`, `ConsentUpdateResult` (Core/DTOs)

---

## Consent Scope

This section clarifies exactly what data requires consent and what data is collected without consent for operational purposes.

### Data Collected WITHOUT Consent (Operational Data)

The following data is collected for all guild members regardless of consent status. This is essential operational data required for core bot functionality:

**User Metadata (`User` entity):**
- Discord user ID (snowflake)
- Username and discriminator
- Avatar hash (for display purposes)
- Global display name
- Account creation date (derived from snowflake)
- First/last seen timestamps

**Guild Membership (`GuildMember` entity):**
- Guild-to-user relationship
- Join date
- Nickname (if set)
- Role assignments (cached for display)
- Active/inactive status

**Purpose:** This data is synchronized via the `MemberSyncService` to support:
- Member directory and search
- Moderation features (viewing user info, role assignments)
- Welcome messages (greeting new members by name)
- Authorization checks (verifying guild membership)

**Legal Basis (GDPR):** Legitimate interest - essential for bot operation

### Data Collected WITH Consent (MessageLogging)

The following data is ONLY collected when the user has granted `MessageLogging` consent:

**Message Content (`MessageLog` entity):**
- Full message text/content
- Discord message ID
- Channel ID and name
- Attachment/embed presence flags
- Reply-to message ID
- Message timestamp

**Purpose:** Message logging supports:
- Moderation review (investigating incidents)
- Message search and history
- Analytics derived from message activity

**Legal Basis (GDPR):** Explicit consent (opt-in)

### Analytics and Consent

**Current Implementation:** Analytics aggregation services (`MemberActivityAggregationService`, `ChannelActivityAggregationService`, `GuildMetricsAggregationService`) derive their data from `MessageLog` records. This means:

- ⚠️ **Users who have not granted `MessageLogging` consent do NOT appear in member activity analytics**
- ⚠️ **Channel and guild message counts only reflect messages from consenting users**
- ⚠️ **Analytics are incomplete if many users have not consented**

**Planned Improvement:** Future releases may separate analytics from message content logging:
- Anonymous event counting (message sent, reaction added) without storing content
- Separate `Analytics` consent type for aggregated statistics
- Allow analytics collection without storing message bodies

### Summary Table

| Data Type | Requires Consent? | Storage Location | Retention |
|-----------|-------------------|------------------|-----------|
| User ID, username, avatar | No | `User` table | Indefinite |
| Guild membership, roles | No | `GuildMember` table | Until member leaves |
| Message content | **Yes** (`MessageLogging`) | `MessageLog` table | 90 days (configurable) |
| Command execution logs | No | `CommandLog` table | 30 days (configurable) |
| Moderation cases | No | `ModerationCase` table | Indefinite |
| Consent records | No (meta) | `UserConsent` table | Indefinite (audit trail) |

---

## Consent Types

The system supports multiple consent types defined in the `ConsentType` enum. Each consent type controls a specific category of data processing.

### MessageLogging (Type ID: 1)

**Display Name:** Message Logging

**Description:** Allow the bot to log your messages and interactions for moderation, analytics, and troubleshooting purposes. This includes message content, timestamps, and metadata.

**What Data is Collected:**
- Message content (text)
- Discord message ID (snowflake)
- Author ID (Discord user ID)
- Channel ID
- Guild ID (server ID, if in a server)
- Message timestamp
- Log timestamp
- Attachment presence (boolean flag, not content)
- Embed presence (boolean flag, not content)
- Reply-to message ID (if message is a reply)

**When Data is Collected:**
- When you send messages in Discord servers where the bot is present
- When you send direct messages to the bot
- Only if you have active consent for MessageLogging

**Default State:** Not granted (opt-in required)

**Impact When Revoked:**
- No new messages will be logged
- Previously logged messages remain in the database per retention policy
- Message logging feature will skip your messages

### Future Consent Types

The system is designed to support additional consent types. Planned types include:

- **Analytics:** User interaction analytics and usage patterns
- **LLMInteraction:** Consent for AI/LLM-powered features that may process message content
- **PersonalizedFeatures:** Consent for features that use historical data for personalization

New consent types can be added by extending the `ConsentType` enum and updating the display name and description mapping in `ConsentService`.

---

## Slash Commands

The `/consent` command group provides Discord users with direct control over their consent preferences. All consent commands are:

- **Ephemeral:** Responses are only visible to the user who executed the command
- **Rate Limited:** 5 commands per 60 seconds per user
- **Guild-Gated:** Requires the guild to be active in the bot's database

### /consent grant

Grants consent for a specific data collection type.

**Syntax:**
```
/consent grant [type:ConsentType]
```

**Parameters:**
- `type` (optional): The type of consent to grant. Defaults to `MessageLogging` if not specified.

**Behavior:**
1. Checks if user already has active consent for the specified type
2. If consent already exists, returns an informational message with the original grant date
3. If no active consent exists, creates a new `UserConsent` record with:
   - `GrantedAt`: Current UTC timestamp
   - `GrantedVia`: "SlashCommand"
   - `RevokedAt`: null (active consent)
4. Returns success confirmation with revocation instructions

**Example Response (Success):**
```
✅ Consent Granted

You have opted in to message logging. Your messages in DMs with this bot and in mutual servers may now be logged.

You can revoke consent at any time with /consent revoke.
```

**Example Response (Already Granted):**
```
ℹ️ Consent Already Active

You already have active consent for Message Logging.

Originally granted: Wednesday, December 25, 2024 3:45 PM
```

**Error Handling:**
- Database errors return a generic error message and log detailed errors
- Invalid consent types are prevented by Discord.NET's enum validation

**Source Code:** `ConsentModule.GrantAsync()` in `src/DiscordBot.Bot/Commands/ConsentModule.cs`

---

### /consent revoke

Revokes previously granted consent for a specific data collection type.

**Syntax:**
```
/consent revoke [type:ConsentType]
```

**Parameters:**
- `type` (optional): The type of consent to revoke. Defaults to `MessageLogging` if not specified.

**Behavior:**
1. Searches for active consent record for the user and specified type
2. If no active consent exists, returns an informational message
3. If active consent exists, updates the record with:
   - `RevokedAt`: Current UTC timestamp
   - `RevokedVia`: "SlashCommand"
4. Returns success confirmation with data retention notice

**Example Response (Success):**
```
✅ Consent Revoked

You have opted out of message logging. Your messages will no longer be logged.

Note: Previously logged messages are retained per our data retention policy.
Use /consent delete-data to request deletion of your data.
```

**Example Response (No Active Consent):**
```
ℹ️ No Active Consent

You don't have active consent for Message Logging to revoke.

Use /consent grant to grant consent.
```

**Important Notes:**
- Revoking consent does NOT automatically delete previously collected data
- Data retention policies continue to apply to historical data
- The `/consent delete-data` command mentioned in the response is a placeholder for future implementation

**Error Handling:**
- Database errors return a generic error message and log detailed errors
- Invalid consent types are prevented by Discord.NET's enum validation

**Source Code:** `ConsentModule.RevokeAsync()` in `src/DiscordBot.Bot/Commands/ConsentModule.cs`

---

### /consent status

Displays the user's current consent status for all consent types.

**Syntax:**
```
/consent status
```

**Parameters:** None

**Behavior:**
1. Retrieves all consent records for the user from the database
2. Iterates through all defined consent types in the `ConsentType` enum
3. For each consent type, determines if an active (non-revoked) consent exists
4. Builds an embed showing the status of each consent type

**Example Response:**
```
📋 Your Consent Status

Use /consent grant or /consent revoke to manage your preferences.

Message Logging
✅ Granted (since Dec 25, 2024)

Analytics
❌ Not granted

LLM Interaction
❌ Not granted
```

**Example Response (No Consents):**
```
📋 Your Consent Status

You have not granted consent for any data processing activities.

Use /consent grant to opt in to message logging.

Message Logging
❌ Not granted
```

**Status Indicators:**
- ✅ with grant date: Active consent
- ❌: No active consent (either never granted or revoked)

**Error Handling:**
- Database errors return a generic error message and log detailed errors

**Source Code:** `ConsentModule.StatusAsync()` in `src/DiscordBot.Bot/Commands/ConsentModule.cs`

---

## Web UI Privacy Page

Authenticated users can manage their consent preferences through the web admin interface at `/Account/Privacy`. This page requires:

1. User must be authenticated (logged into the web admin)
2. User must have a Discord account linked to their admin account

**Page URL:** `https://yourdomain.com/Account/Privacy`

**Features:**

### Consent Status Display

Shows a comprehensive view of all consent types with:
- Consent type display name (e.g., "Message Logging")
- Detailed description of what the consent controls
- Current status (Granted or Not Granted)
- Grant date and source (if granted)
- Toggle controls for granting/revoking consent

### Consent History

Displays a chronological history of all consent actions:
- Action type (Granted or Revoked)
- Consent type name
- Timestamp (displayed in user's local timezone)
- Source (SlashCommand, WebUI, etc.)
- Sorted by most recent first

### Toggle Consent Actions

Users can toggle consent status with a single click:
- **Grant Action:** Posts to `OnPostToggleConsentAsync` with `grant=true`
- **Revoke Action:** Posts to `OnPostToggleConsentAsync` with `grant=false`
- Both actions use the `IConsentService` with "WebUI" as the source

**User Flow:**

1. User navigates to `/Account/Privacy`
2. Page checks if Discord account is linked
3. If linked, fetches consent statuses and history from `IConsentService`
4. User clicks toggle button to grant or revoke consent
5. POST request updates consent in database
6. Page redirects with success/error status message
7. Updated consent status is displayed

**Authorization:**
- Requires `[Authorize]` attribute (authenticated users only)
- No specific role requirement (all authenticated users can manage their own consent)

**Discord Account Linking:**
- If Discord is not linked, consent controls are disabled
- Page displays message: "You must link your Discord account before managing consent preferences"
- Link to Discord OAuth flow provided

**Source Code:** `Privacy.cshtml` and `PrivacyModel` in `src/DiscordBot.Bot/Pages/Account/`

---

## User Data Handling

### Data Collection

The bot collects data only when users have granted explicit consent for the specific data processing activity. All data collection is governed by the consent framework.

**What Triggers Data Collection:**

1. **Message Logging:** User sends a message in a server or DM where the bot is present
   - **Consent Required:** `ConsentType.MessageLogging`
   - **Handler:** `MessageLoggingHandler.HandleMessageReceivedAsync()`
   - **Pre-Collection Checks:**
     - User is not a bot
     - Message is a user message (not a system message)
     - Message logging feature is globally enabled (`Features:MessageLoggingEnabled` setting)
     - User has active consent for `MessageLogging`

2. **Future Data Collection Types:**
   - Analytics events (planned, requires Analytics consent)
   - AI/LLM interactions (planned, requires LLMInteraction consent)

**Data Collection Flow (Message Logging):**

```
Discord Message Event
    ↓
Is author a bot? → Yes → Skip
    ↓ No
Is user message? → No → Skip
    ↓ Yes
Is message logging enabled globally? → No → Skip
    ↓ Yes
Does user have MessageLogging consent? → No → Skip
    ↓ Yes
Create MessageLog entity
    ↓
Save to database
```

**Implementation:** `MessageLoggingHandler` in `src/DiscordBot.Bot/Services/MessageLoggingHandler.cs`

---

### Data Storage

All consent-related data and user data is stored in a SQLite database (development) or SQL Server/MySQL/PostgreSQL (production) using Entity Framework Core.

**Consent Data Tables:**

**UserConsent Table:**
- `Id` (int, primary key): Unique consent record identifier
- `DiscordUserId` (ulong): Discord user snowflake ID
- `ConsentType` (int, enum): Type of consent (1 = MessageLogging, etc.)
- `GrantedAt` (datetime): UTC timestamp when consent was granted
- `RevokedAt` (datetime?, nullable): UTC timestamp when consent was revoked (null if active)
- `GrantedVia` (string?, nullable): Source of grant action ("SlashCommand", "WebUI", etc.)
- `RevokedVia` (string?, nullable): Source of revoke action
- **Navigation:** `User` (ApplicationUser)

**Indexes:**
- Composite index on `(DiscordUserId, ConsentType, RevokedAt)` for fast consent lookups
- See `UserConsentConfiguration` in `src/DiscordBot.Infrastructure/Data/Configurations/UserConsentConfiguration.cs`

**User Data Tables:**

**MessageLog Table:**
- Stores message content and metadata for users with MessageLogging consent
- See [Database Schema](database-schema.md) for full schema
- Includes automatic cleanup based on retention policies

**Data Protection:**
- Database connection strings stored in user secrets (development) or environment variables (production)
- No plaintext sensitive data in source control
- Entity Framework Core parameterized queries prevent SQL injection

---

### Data Retention

The bot implements automatic data retention policies to minimize data storage and comply with privacy best practices.

**Message Log Retention Policy:**

**Configuration Options:** `MessageLogRetentionOptions` in `appsettings.json`

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

**Configuration Keys:**
- `RetentionDays` (default: 90): Number of days to keep message logs before deletion
- `CleanupBatchSize` (default: 1000): Maximum records to delete per cleanup operation
- `CleanupIntervalHours` (default: 24): Hours between automatic cleanup runs
- `Enabled` (default: true): Whether automatic cleanup is enabled

**Cleanup Process:**

1. Background service `MessageLogCleanupService` runs every 24 hours (configurable)
2. Deletes message logs older than `RetentionDays` (default: 90 days)
3. Processes deletions in batches of `CleanupBatchSize` to avoid long transactions
4. Logs cleanup statistics (records deleted, errors encountered)

**Implementation:** `MessageLogCleanupService` in `src/DiscordBot.Bot/Services/MessageLogCleanupService.cs`

**Consent Records Retention:**

- Consent records (UserConsent table) are **not** automatically deleted
- Historical consent records provide an audit trail for compliance
- Both active and revoked consents are retained indefinitely
- Users who want their consent history deleted must request manual data deletion (see Data Deletion)

**Audit Log Retention:**

- Audit logs follow a similar retention policy
- See `AuditLogRetentionOptions` and `AuditLogRetentionService`
- Default retention: 365 days

---

### Data Export

**Current Status:** Not implemented (planned for future release)

**Planned Implementation:**

Users will be able to request a data export containing:
- All message logs (MessageLog records)
- All consent records (UserConsent records)
- Audit log entries related to the user
- Account information

**Planned Access Methods:**
- Slash command: `/privacy export-data`
- Web UI: "Export My Data" button on Privacy page
- Admin interface: Admins can export data on behalf of users (for compliance requests)

**Planned Export Format:**
- JSON file containing structured data
- CSV files for tabular data (message logs, consent history)
- Downloadable as ZIP archive
- Automatic deletion of export file after 7 days

**Implementation Tracking:** See GitHub issue backlog for data export feature

---

### Data Deletion

**Current Status:** Manual process (automated deletion planned for future release)

**Current Process:**

Users requesting data deletion should:

1. Revoke all active consents using `/consent revoke` or the Privacy page
2. Contact the bot administrator/owner
3. Administrator manually deletes data using database queries or admin tools

**Data Deletion Scope:**

When processing a deletion request, the following data should be removed:

- **MessageLog records** for the user (filter by `AuthorId = DiscordUserId`)
- **UserConsent records** for the user (filter by `DiscordUserId`)
- **AuditLog entries** created by the user (filter by `PerformedByUserId`)
- **User account** in the admin system (if applicable, filter by `DiscordUserId`)

**Data Deletion Limitations:**

Some data may be retained for legal or operational reasons:

- Audit logs of admin actions performed by the user (if user was an admin)
- Guild configuration changes attributed to the user
- Aggregate statistics that cannot be disaggregated

**Planned Implementation:**

- Slash command: `/privacy delete-data` (requires confirmation)
- Web UI: "Delete My Data" button with confirmation dialog
- Automatic processing of deletion requests
- Email/Discord notification when deletion is complete

**GDPR Compliance Notes:**

- Data deletion requests should be processed within 30 days (GDPR requirement)
- Users should receive confirmation when deletion is complete
- Deletion should be permanent and irreversible
- Logs of the deletion request itself should be retained for compliance auditing

**Implementation Tracking:** See GitHub issue backlog for automated data deletion feature

---

## ConsentService Architecture

The `ConsentService` provides a centralized service layer for managing user consent operations. It implements caching, batch operations, and error handling to ensure performant and reliable consent checks.

### Service Interface

**Interface:** `IConsentService` in `src/DiscordBot.Core/Interfaces/IConsentService.cs`

**Primary Methods:**

#### GetConsentStatusAsync

Retrieves current consent status for all consent types for a user.

```csharp
Task<IEnumerable<ConsentStatusDto>> GetConsentStatusAsync(
    ulong discordUserId,
    CancellationToken cancellationToken = default)
```

**Returns:** List of `ConsentStatusDto` objects containing:
- Type ID and display name
- Description of what the consent controls
- Whether consent is currently granted
- Grant timestamp and source (if granted)

**Used By:** Privacy page, admin tools

---

#### GetConsentHistoryAsync

Retrieves historical consent changes for a user, ordered by most recent first.

```csharp
Task<IEnumerable<ConsentHistoryEntryDto>> GetConsentHistoryAsync(
    ulong discordUserId,
    CancellationToken cancellationToken = default)
```

**Returns:** List of `ConsentHistoryEntryDto` objects containing:
- Action ("Granted" or "Revoked")
- Consent type display name
- Timestamp of action
- Source of action (SlashCommand, WebUI, etc.)

**Used By:** Privacy page consent history display

---

#### GrantConsentAsync

Grants consent for a specific consent type via Web UI.

```csharp
Task<ConsentUpdateResult> GrantConsentAsync(
    ulong discordUserId,
    ConsentType type,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `discordUserId`: Discord user snowflake ID
- `type`: Consent type to grant
- `cancellationToken`: Optional cancellation token

**Returns:** `ConsentUpdateResult` with:
- `Succeeded` (bool): Whether the operation succeeded
- `ErrorCode` (string?): Error code if failed (e.g., "ALREADY_GRANTED", "DATABASE_ERROR")
- `ErrorMessage` (string?): User-friendly error message

**Behavior:**
1. Validates consent type is defined in enum
2. Checks if user already has active consent for the type
3. If active consent exists, returns failure with `ALREADY_GRANTED` error
4. Creates new `UserConsent` record with `GrantedVia = "WebUI"`
5. Invalidates cache for the user and consent type
6. Returns success result

**Internal Overload:**
- Private `GrantConsentAsync` method accepts a `grantedVia` string parameter
- Used by slash commands to set source as "SlashCommand"

---

#### RevokeConsentAsync

Revokes consent for a specific consent type via Web UI.

```csharp
Task<ConsentUpdateResult> RevokeConsentAsync(
    ulong discordUserId,
    ConsentType type,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `discordUserId`: Discord user snowflake ID
- `type`: Consent type to revoke
- `cancellationToken`: Optional cancellation token

**Returns:** `ConsentUpdateResult` (same structure as GrantConsentAsync)

**Behavior:**
1. Validates consent type is defined in enum
2. Retrieves active consent record for the user and type
3. If no active consent exists, returns failure with `NOT_GRANTED` error
4. Updates consent record with `RevokedAt = DateTime.UtcNow` and `RevokedVia = "WebUI"`
5. Invalidates cache for the user and consent type
6. Returns success result

**Internal Overload:**
- Private `RevokeConsentAsync` method accepts a `revokedVia` string parameter
- Used by slash commands to set source as "SlashCommand"

---

#### HasConsentAsync

Checks if a user has active consent for a specific type. Uses caching for performance.

```csharp
Task<bool> HasConsentAsync(
    ulong discordUserId,
    ConsentType consentType,
    CancellationToken cancellationToken = default)
```

**Returns:** `true` if user has active consent, `false` otherwise

**Behavior:**
1. Checks memory cache with key `consent:{discordUserId}:{consentType}`
2. If cached, returns cached value immediately
3. If not cached, queries repository
4. Caches result for `ConsentCacheDurationMinutes` (default: 15 minutes, configurable in `CachingOptions`)
5. Returns result

**Used By:** `MessageLoggingHandler`, analytics services, feature flags

**Performance:** O(1) for cached lookups, O(log n) for database queries (indexed)

---

#### GetActiveConsentsAsync

Retrieves all active consent types for a user.

```csharp
Task<IEnumerable<ConsentType>> GetActiveConsentsAsync(
    ulong discordUserId,
    CancellationToken cancellationToken = default)
```

**Returns:** Collection of `ConsentType` enums for which the user has active consent

**Used By:** User profile displays, admin tools

---

#### HasConsentBatchAsync

Batch checks consent for multiple users (for efficiency).

```csharp
Task<IDictionary<ulong, bool>> HasConsentBatchAsync(
    IEnumerable<ulong> discordUserIds,
    ConsentType consentType,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `discordUserIds`: Collection of Discord user IDs to check
- `consentType`: Consent type to check for all users

**Returns:** Dictionary mapping user IDs to their consent status (true/false)

**Behavior:**
1. Checks cache for each user ID
2. Collects uncached user IDs
3. Performs single database query for all uncached users
4. Caches results for each user
5. Returns combined results (cached + queried)

**Performance Benefits:**
- Single database query instead of N queries
- Reduces round trips to database
- Fully utilizes cache for already-checked users

**Used By:** Bulk message logging operations, analytics batch processing

---

### Caching Strategy

The `ConsentService` uses `IMemoryCache` for performance optimization, reducing database queries for frequently checked consents.

**Cache Key Format:**
```
consent:{discordUserId}:{consentType}
```

**Example:**
```
consent:123456789012345678:MessageLogging
```

**Cache Configuration:**

Configured via `CachingOptions` in `appsettings.json`:

```json
{
  "Caching": {
    "ConsentCacheDurationMinutes": 15
  }
}
```

**Default:** 15 minutes

**Cache Invalidation:**

Cache entries are invalidated when:
- User grants consent (new consent record created)
- User revokes consent (consent record updated)
- Cache entry expires after `ConsentCacheDurationMinutes`

**Invalidation Method:** `InvalidateConsentCache()` removes the specific cache key

**Why Caching is Safe:**

- Consent changes are infrequent (user-initiated only)
- 15-minute stale data is acceptable for consent checks
- Cache invalidation ensures immediate consistency for the affected user
- Worst case: User revokes consent via slash command, web UI cache shows stale data for up to 15 minutes

**Performance Impact:**

- Uncached consent check: ~5-10ms (database query)
- Cached consent check: ~0.1ms (memory lookup)
- 50-100x performance improvement for cached checks

---

### Batch Operations

Batch operations optimize consent checks when processing multiple users simultaneously.

**Use Cases:**

1. **Message Logging Batch Processing:** When processing a batch of messages from different users
2. **Analytics Processing:** When analyzing activity across multiple users
3. **Admin Tools:** When displaying consent status for multiple users in a list

**Implementation: HasConsentBatchAsync**

```csharp
var userIds = new[] { 111111111111111111, 222222222222222222, 333333333333333333 };
var consentMap = await consentService.HasConsentBatchAsync(userIds, ConsentType.MessageLogging);

// consentMap contains:
// { 111111111111111111: true, 222222222222222222: false, 333333333333333333: true }
```

**Efficiency Comparison:**

**Without Batch (3 users, uncached):**
```
3 database queries
~15-30ms total
```

**With Batch (3 users, uncached):**
```
1 database query
~5-10ms total
```

**With Batch (3 users, all cached):**
```
0 database queries
~0.3ms total
```

**Implementation Details:**

1. Creates list of user IDs to check
2. Checks cache for each user ID
3. Collects IDs that are not in cache
4. Performs single query: `SELECT DiscordUserId FROM UserConsent WHERE DiscordUserId IN (...) AND ConsentType = ... AND RevokedAt IS NULL`
5. Builds HashSet of users with consent
6. Caches results for each uncached user
7. Returns dictionary with all results

**Database Optimization:**

The composite index on `(DiscordUserId, ConsentType, RevokedAt)` ensures the batch query is highly performant even with thousands of users.

---

## How Consent Affects Bot Behavior

Consent is checked before performing certain data processing operations. The bot respects user consent preferences for message content storage.

> **Note:** Consent only affects message content logging. Users are cached in the database regardless of consent status (see [Consent Scope](#consent-scope)).

### Features That Work WITHOUT Consent

The following bot features work for all users regardless of consent status:

- **Member Directory:** Users appear in the member directory with their username, avatar, and roles
- **Welcome Messages:** New members receive welcome messages (greeting by username)
- **Moderation:** Moderators can view user info, issue warnings, kicks, bans, etc.
- **Command Execution:** Users can run slash commands (command logs are stored for audit purposes)
- **Role Management:** Role assignments and permissions work normally

### Features That REQUIRE Consent

The following features require `MessageLogging` consent:

- **Message Logging:** Message content is stored for moderation review
- **Message Search:** Only consenting users' messages appear in search results
- **Analytics (current limitation):** Member activity analytics only reflect consenting users

### Message Logging

**Handler:** `MessageLoggingHandler.HandleMessageReceivedAsync()`

**Consent Check Flow:**

```csharp
// Inside MessageLoggingHandler
var hasConsent = await consentService.HasConsentAsync(
    message.AuthorId,
    ConsentType.MessageLogging);

if (!hasConsent)
{
    _logger.LogDebug("User {AuthorId} has not granted message logging consent, skipping message {MessageId}",
        message.AuthorId, message.Id);
    return; // Skip logging
}

// Proceed with logging
```

**Complete Message Logging Flow:**

```
1. Discord message event received
2. Filter: Is author a bot? → Skip if yes
3. Filter: Is user message? → Skip if no
4. Setting Check: Is message logging enabled globally? → Skip if disabled
5. Consent Check: Does user have MessageLogging consent? → Skip if no
6. Create MessageLog entity
7. Save to database
```

**Impact of Missing Consent:**

- User's message is not logged to the database
- No MessageLog record is created
- Logged at DEBUG level: "User X has not granted message logging consent"
- No error or warning shown to the user
- Bot continues normal operation

**Performance:**

- Consent check uses caching (see [Caching Strategy](#caching-strategy))
- Typical cached check: ~0.1ms
- No noticeable impact on message processing latency

---

### Future Feature Integrations

Consent checks will be integrated into future features:

**Analytics (Planned):**

```csharp
var hasConsent = await consentService.HasConsentAsync(
    userId,
    ConsentType.Analytics);

if (hasConsent)
{
    // Track analytics event
}
```

**AI/LLM Features (Planned):**

```csharp
var hasConsent = await consentService.HasConsentAsync(
    userId,
    ConsentType.LLMInteraction);

if (!hasConsent)
{
    await ReplyAsync("You must grant LLM consent to use this feature. Use /consent grant type:LLMInteraction");
    return;
}

// Proceed with AI processing
```

---

## Admin Considerations

Administrators have limited visibility into user consent for compliance and troubleshooting purposes.

### Viewing User Consent Status

**Current Implementation:**

Admins can view user consent status through:

1. **Database Queries:** Direct database access via SQL management tools
2. **Web UI (Planned):** User details page in `/Admin/Users/Details?id={userId}` will show consent status
3. **API (Planned):** API endpoint for querying user consent status

**Planned Admin Features:**

- View consent status for any user
- View consent history for any user
- Export consent reports for compliance audits
- View aggregate statistics (e.g., "80% of users have granted MessageLogging consent")

### Compliance Reporting

**Planned Reports:**

1. **Consent Adoption Report:**
   - Percentage of users who have granted each consent type
   - Trend over time (new consents per week/month)
   - Breakdown by guild or user segment

2. **Consent Audit Log:**
   - All consent grants and revocations
   - Filterable by date range, user, consent type
   - Exportable as CSV for compliance documentation

3. **Data Processing Report:**
   - Number of records processed per consent type
   - Data retention statistics (records deleted, records retained)
   - Compliance with retention policies

**GDPR Compliance:**

The consent system helps administrators comply with GDPR Article 7 (Conditions for consent):

- ✅ Consent is freely given (users can opt in/out anytime)
- ✅ Consent is specific (separate consent types for different processing activities)
- ✅ Consent is informed (descriptions explain what each consent allows)
- ✅ Consent is unambiguous (explicit opt-in required)
- ✅ Consent can be withdrawn (revoke commands available)
- ✅ Burden of proof (consent records stored with timestamps and audit trail)

---

## Implementation Details

### Service Registration

Consent services are registered in `Program.cs`:

```csharp
// Repository
builder.Services.AddScoped<IUserConsentRepository, UserConsentRepository>();

// Service
builder.Services.AddScoped<IConsentService, ConsentService>();
```

**Lifetimes:**
- `IUserConsentRepository`: Scoped (per HTTP request or command execution)
- `IConsentService`: Scoped (per HTTP request or command execution)
- `IMemoryCache`: Singleton (shared across all requests)

---

### Display Name and Description Mapping

The `ConsentService` provides user-friendly display names and descriptions for consent types:

**Display Names:**

```csharp
private static string GetConsentTypeDisplayName(ConsentType type)
{
    return type switch
    {
        ConsentType.MessageLogging => "Message Logging",
        _ => type.ToString()
    };
}
```

**Descriptions:**

```csharp
private static string GetConsentTypeDescription(ConsentType type)
{
    return type switch
    {
        ConsentType.MessageLogging => "Allow the bot to log your messages and interactions for moderation, analytics, and troubleshooting purposes. This includes message content, timestamps, and metadata.",
        _ => "No description available."
    };
}
```

**Adding New Consent Types:**

When adding a new consent type:

1. Add to `ConsentType` enum
2. Update display name mapping in both `ConsentService` and `ConsentModule`
3. Update description mapping in `ConsentService`
4. Add consent check to the feature that requires it
5. Document in this file

---

### Error Handling

Consent operations use structured error codes for consistent error handling:

**Error Codes (ConsentUpdateResult):**

| Error Code | Description | User Action |
|------------|-------------|-------------|
| `USER_NOT_FOUND` | Discord user not found in database | Contact admin |
| `INVALID_CONSENT_TYPE` | Consent type is not defined in enum | Report bug (should not occur) |
| `ALREADY_GRANTED` | Consent is already active | No action needed |
| `NOT_GRANTED` | No active consent to revoke | Grant consent first |
| `DATABASE_ERROR` | Database operation failed | Retry or contact admin |

**Error Handling Patterns:**

**Slash Commands:**
- Catch exceptions and return user-friendly error embeds
- Log detailed errors with stack traces
- Never expose internal error details to users

**Web UI:**
- Map error codes to user-friendly messages
- Display errors in status message (TempData)
- Redirect to same page to show error

**Background Services:**
- Log errors but don't crash the service
- Continue processing other records if batch operation fails
- Emit metrics for error rates (planned)

---

## Database Schema

### UserConsent Table

**Table Name:** `UserConsent`

**Columns:**

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | int | No | IDENTITY | Primary key |
| DiscordUserId | bigint (ulong) | No | - | Discord user snowflake ID |
| ConsentType | int | No | - | Consent type enum value (1 = MessageLogging) |
| GrantedAt | datetime2 | No | - | UTC timestamp when consent was granted |
| RevokedAt | datetime2 | Yes | NULL | UTC timestamp when consent was revoked (NULL = active) |
| GrantedVia | nvarchar(100) | Yes | NULL | Source of grant action (SlashCommand, WebUI, etc.) |
| RevokedVia | nvarchar(100) | Yes | NULL | Source of revoke action (SlashCommand, WebUI, etc.) |

**Indexes:**

1. **Primary Key:** Clustered index on `Id`
2. **Consent Lookup Index:** Non-clustered composite index on `(DiscordUserId, ConsentType, RevokedAt)`
   - Enables fast lookups for active consents
   - Supports queries filtering by user, type, and active status

**Relationships:**

- **Foreign Key:** `DiscordUserId` references `User.DiscordUserId` (optional navigation property)
- **Cascade Behavior:** No cascade delete (consents retained for audit trail)

**Migration:**

Initial migration: `20251224182211_AddUserConsent`

**Entity Configuration:**

```csharp
// UserConsentConfiguration.cs
builder.HasKey(uc => uc.Id);

builder.Property(uc => uc.DiscordUserId)
    .IsRequired();

builder.Property(uc => uc.ConsentType)
    .IsRequired();

builder.Property(uc => uc.GrantedAt)
    .IsRequired();

builder.Property(uc => uc.GrantedVia)
    .HasMaxLength(100);

builder.Property(uc => uc.RevokedVia)
    .HasMaxLength(100);

// Composite index for efficient consent lookups
builder.HasIndex(uc => new { uc.DiscordUserId, uc.ConsentType, uc.RevokedAt })
    .HasDatabaseName("IX_UserConsent_Lookup");

// Navigation property
builder.HasOne(uc => uc.User)
    .WithMany()
    .HasForeignKey(uc => uc.DiscordUserId)
    .HasPrincipalKey(u => u.DiscordUserId)
    .OnDelete(DeleteBehavior.NoAction);
```

**Example Records:**

```sql
-- Active consent (RevokedAt = NULL)
Id | DiscordUserId         | ConsentType | GrantedAt           | RevokedAt | GrantedVia   | RevokedVia
---|-----------------------|-------------|---------------------|-----------|--------------|------------
1  | 123456789012345678    | 1           | 2024-12-25 15:30:00 | NULL      | SlashCommand | NULL

-- Revoked consent
Id | DiscordUserId         | ConsentType | GrantedAt           | RevokedAt           | GrantedVia   | RevokedVia
---|-----------------------|-------------|---------------------|---------------------|--------------|---------------
2  | 123456789012345678    | 1           | 2024-12-20 10:00:00 | 2024-12-22 14:00:00 | SlashCommand | SlashCommand

-- Web UI grant
Id | DiscordUserId         | ConsentType | GrantedAt           | RevokedAt | GrantedVia | RevokedVia
---|-----------------------|-------------|---------------------|-----------|------------|------------
3  | 987654321098765432    | 1           | 2024-12-25 16:00:00 | NULL      | WebUI      | NULL
```

**Querying Active Consents:**

```sql
-- Check if user has active MessageLogging consent
SELECT COUNT(*)
FROM UserConsent
WHERE DiscordUserId = 123456789012345678
  AND ConsentType = 1
  AND RevokedAt IS NULL;

-- Get all users with active MessageLogging consent
SELECT DISTINCT DiscordUserId
FROM UserConsent
WHERE ConsentType = 1
  AND RevokedAt IS NULL;
```

---

## Security and Compliance

### GDPR Compliance

The consent system implements GDPR (General Data Protection Regulation) requirements:

**Article 7 (Conditions for consent):**
- ✅ Consent is freely given (no penalties for not consenting)
- ✅ Consent is specific (separate consent types for different processing)
- ✅ Consent is informed (clear descriptions of what is collected)
- ✅ Consent is unambiguous (explicit opt-in required, no pre-checked boxes)

**Article 7(3) (Right to withdraw consent):**
- ✅ Consent can be withdrawn at any time
- ✅ Withdrawing consent is as easy as granting it (same commands/UI)

**Article 30 (Records of processing activities):**
- ✅ Consent records maintained with timestamps
- ✅ Audit trail of all consent changes
- ✅ Source of consent tracked (SlashCommand, WebUI)

**Article 15 (Right of access):**
- 🟡 Partially implemented (user can view consent status and history)
- ⏳ Planned: Full data export feature

**Article 17 (Right to erasure):**
- 🟡 Manual process (contact administrator)
- ⏳ Planned: Automated data deletion feature

**Article 25 (Data protection by design and by default):**
- ✅ Opt-in by default (no consent = no data collection)
- ✅ Consent checked before every data processing operation
- ✅ Data retention policies minimize storage

**Legend:**
- ✅ Fully implemented
- 🟡 Partially implemented
- ⏳ Planned for future release

---

### Privacy Best Practices

**Principle of Least Privilege:**
- Only collect data that users have explicitly consented to
- Only admins can view other users' consent status (planned)
- Users can only manage their own consent

**Data Minimization:**
- Message logging captures only essential fields
- Attachment and embed content not stored (only boolean flags)
- Automatic deletion after retention period

**Transparency:**
- Clear descriptions of what each consent type allows
- Consent history shows all changes
- Privacy page accessible to all authenticated users

**User Control:**
- Users can grant/revoke consent at any time
- Multiple access methods (slash commands, web UI)
- No friction in consent management

**Security:**
- Consent records stored in secured database
- Web UI requires authentication
- Slash commands are ephemeral (only visible to user)

---

### Audit Trail

All consent changes are recorded for compliance and troubleshooting:

**Recorded Information:**
- Who: `DiscordUserId`
- What: `ConsentType` and action (grant or revoke)
- When: `GrantedAt` or `RevokedAt` timestamps
- How: `GrantedVia` or `RevokedVia` source

**Audit Trail Uses:**
- Prove consent was obtained (GDPR compliance)
- Troubleshoot issues ("Why isn't my data being logged?")
- Detect unusual patterns (mass consent revocations)
- Generate compliance reports

**Retention:**
- Consent records are retained indefinitely
- Even revoked consents are kept in the database (with `RevokedAt` set)
- Users can request deletion of their consent history (manual process)

---

## Example Scenarios

### Scenario 1: New User Grants Consent

**User Flow:**

1. User joins a Discord server where the bot is present
2. User sends a message: "Hello!"
3. Bot does NOT log the message (no consent yet)
4. User runs `/consent grant`
5. Bot responds: "✅ Consent Granted - Your messages will now be logged"
6. User sends another message: "Testing message logging"
7. Bot logs the message to the database

**Database Changes:**

```sql
-- After step 4
INSERT INTO UserConsent (DiscordUserId, ConsentType, GrantedAt, GrantedVia)
VALUES (123456789012345678, 1, '2024-12-25 15:30:00', 'SlashCommand');

-- After step 6
INSERT INTO MessageLog (DiscordMessageId, AuthorId, ChannelId, GuildId, Content, Timestamp, LoggedAt, ...)
VALUES (987654321098765432, 123456789012345678, 111111111111111111, 222222222222222222, 'Testing message logging', '2024-12-25 15:31:00', '2024-12-25 15:31:00', ...);
```

---

### Scenario 2: User Revokes Consent

**User Flow:**

1. User has active MessageLogging consent
2. User decides they don't want messages logged anymore
3. User runs `/consent revoke`
4. Bot responds: "✅ Consent Revoked - Your messages will no longer be logged. Previously logged messages are retained per our data retention policy."
5. User sends a message: "This should not be logged"
6. Bot does NOT log the message (consent revoked)
7. Previously logged messages remain in database until retention policy expires

**Database Changes:**

```sql
-- After step 3
UPDATE UserConsent
SET RevokedAt = '2024-12-25 16:00:00',
    RevokedVia = 'SlashCommand'
WHERE DiscordUserId = 123456789012345678
  AND ConsentType = 1
  AND RevokedAt IS NULL;

-- After step 5: No INSERT into MessageLog (consent revoked)
```

---

### Scenario 3: User Checks Status

**User Flow:**

1. User is unsure if they've granted consent
2. User runs `/consent status`
3. Bot displays embed showing:
   - Message Logging: ✅ Granted (since Dec 25, 2024)
   - Analytics: ❌ Not granted
   - LLM Interaction: ❌ Not granted

---

### Scenario 4: User Manages Consent via Web UI

**User Flow:**

1. User logs into web admin at `https://bot.example.com`
2. User links their Discord account via OAuth
3. User navigates to `/Account/Privacy`
4. Page displays consent statuses and history
5. User clicks "Revoke" button next to "Message Logging"
6. POST request to `OnPostToggleConsentAsync` with `grant=false`
7. Page redirects with success message: "Your consent preferences have been updated successfully."
8. Updated status shows: Message Logging ❌ Not granted

**Database Changes:**

```sql
UPDATE UserConsent
SET RevokedAt = '2024-12-25 17:00:00',
    RevokedVia = 'WebUI'
WHERE DiscordUserId = 123456789012345678
  AND ConsentType = 1
  AND RevokedAt IS NULL;
```

---

## Troubleshooting

### "My messages aren't being logged"

**Possible Causes:**

1. **No Consent:** You haven't granted MessageLogging consent
   - **Solution:** Run `/consent grant` or grant consent via the Privacy page

2. **Consent Revoked:** You previously revoked consent
   - **Solution:** Run `/consent status` to check, then `/consent grant` if needed

3. **Message Logging Disabled Globally:** Admin has disabled message logging
   - **Solution:** Contact bot administrator to enable `Features:MessageLoggingEnabled` setting

4. **Bot Not in Server:** The bot isn't present in the server where you sent the message
   - **Solution:** Ensure bot is invited to the server

5. **Bot Permissions:** Bot lacks permission to read messages in the channel
   - **Solution:** Contact server administrator to grant bot READ_MESSAGE_HISTORY permission

**Verification Steps:**

```
1. Run /consent status
2. Check if "Message Logging" shows ✅ Granted
3. If not granted, run /consent grant
4. Send a test message
5. Contact admin if still not logging
```

---

### "I granted consent but the bot says I already have it"

**Explanation:**

You previously granted consent, and it's still active. The bot prevents duplicate consent records.

**Solution:**

No action needed. Your consent is already active. If you want to revoke and re-grant consent:

```
1. Run /consent revoke
2. Wait a few seconds
3. Run /consent grant
```

---

### "I revoked consent but I still see my old messages in the logs"

**Explanation:**

Revoking consent stops **future** message logging. Previously logged messages are retained according to the data retention policy (default: 90 days).

**Solution:**

- Wait for messages to expire per retention policy (90 days by default)
- Request manual data deletion by contacting the bot administrator
- Future implementation: Use `/privacy delete-data` command (planned)

---

### "Consent status shows different values in Discord vs Web UI"

**Possible Cause:**

Cache staleness. The web UI uses a 15-minute cache for consent checks.

**Solution:**

- Wait up to 15 minutes for cache to expire
- Log out and log back into the web UI to force a fresh check
- This is normal and expected behavior; caching improves performance

---

### "I can't access the Privacy page"

**Possible Causes:**

1. **Not Authenticated:** You're not logged into the web admin
   - **Solution:** Log in at `/Account/Login`

2. **Discord Not Linked:** Your admin account doesn't have a Discord account linked
   - **Solution:** Link your Discord account at `/Account/LinkDiscord`

3. **Authorization Issue:** Your account lacks necessary permissions
   - **Solution:** Contact administrator to verify account status

---

### Admin: "How do I view a user's consent status?"

**Current Process:**

1. **Database Query:**
   ```sql
   SELECT *
   FROM UserConsent
   WHERE DiscordUserId = 123456789012345678
   ORDER BY GrantedAt DESC;
   ```

2. **Check Active Consents:**
   ```sql
   SELECT ConsentType, GrantedAt, GrantedVia
   FROM UserConsent
   WHERE DiscordUserId = 123456789012345678
     AND RevokedAt IS NULL;
   ```

**Planned Admin Tools:**

- User details page will show consent status
- Admin API endpoint for querying consent status
- Consent reports and analytics dashboard

---

## Related Files

**Core Layer:**
- `src/DiscordBot.Core/Entities/UserConsent.cs` - Entity model
- `src/DiscordBot.Core/Enums/ConsentType.cs` - Consent type enum
- `src/DiscordBot.Core/Interfaces/IConsentService.cs` - Service interface
- `src/DiscordBot.Core/Interfaces/IUserConsentRepository.cs` - Repository interface
- `src/DiscordBot.Core/DTOs/ConsentDtos.cs` - DTOs for consent data
- `src/DiscordBot.Core/Configuration/MessageLogRetentionOptions.cs` - Retention policy config

**Infrastructure Layer:**
- `src/DiscordBot.Infrastructure/Data/Repositories/UserConsentRepository.cs` - Repository implementation
- `src/DiscordBot.Infrastructure/Data/Configurations/UserConsentConfiguration.cs` - EF Core configuration
- `src/DiscordBot.Infrastructure/Migrations/20251224182211_AddUserConsent.cs` - Database migration

**Application Layer:**
- `src/DiscordBot.Bot/Services/ConsentService.cs` - Service implementation
- `src/DiscordBot.Bot/Commands/ConsentModule.cs` - Slash commands
- `src/DiscordBot.Bot/Pages/Account/Privacy.cshtml` - Privacy page UI
- `src/DiscordBot.Bot/Pages/Account/Privacy.cshtml.cs` - Privacy page model
- `src/DiscordBot.Bot/Services/MessageLoggingHandler.cs` - Consent enforcement
- `src/DiscordBot.Bot/Services/MessageLogCleanupService.cs` - Retention policy enforcement

**Tests:**
- `tests/DiscordBot.Tests/Commands/ConsentModuleTests.cs`
- `tests/DiscordBot.Tests/Services/ConsentServiceTests.cs`
- `tests/DiscordBot.Tests/Data/Repositories/UserConsentRepositoryTests.cs`

---

## Future Enhancements

**Planned Features:**

1. **Automated Data Export** (GitHub issue pending)
   - `/privacy export-data` command
   - Web UI "Export My Data" button
   - JSON/CSV export formats

2. **Automated Data Deletion** (GitHub issue pending)
   - `/privacy delete-data` command with confirmation
   - Web UI "Delete My Data" button
   - Automatic processing within 30 days (GDPR compliance)

3. **Admin Consent Dashboard** (GitHub issue pending)
   - View all users' consent status
   - Consent adoption statistics
   - Compliance reports

4. **New Consent Types**
   - Analytics consent for usage tracking
   - LLM consent for AI-powered features
   - Personalization consent for customized experiences

5. **Enhanced Privacy Controls**
   - Per-guild consent (consent for logging in specific servers only)
   - Per-feature consent (more granular than consent types)
   - Consent expiration dates (require re-consent after X months)

6. **Notifications**
   - Discord DM when data export is ready
   - Discord DM when data deletion is complete
   - Email notifications for consent changes

---

**End of Documentation**

For questions or issues, please refer to the GitHub issue tracker or contact the bot administrator.
