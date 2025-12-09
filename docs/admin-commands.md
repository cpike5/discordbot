# Admin Commands

This document provides a comprehensive reference for administrative slash commands implemented in the Discord bot. Admin commands provide essential functionality for bot management, monitoring, and control.

## Overview

All admin commands require specific permissions to execute:
- **Admin Commands** (`/status`, `/guilds`) - Require guild Administrator permission
- **Owner Commands** (`/shutdown`) - Require bot owner status

Commands are automatically logged to the database with correlation IDs for audit tracking and troubleshooting.

---

## Command Reference

### /status

**Description:** Display bot status and health information

**Permission:** Requires Administrator permission in the guild

**Response Type:** Embed message

**Fields:**
- **Bot Username** - The current username of the bot
- **Connection State** - Discord connection status (Connected, Connecting, Disconnected, etc.)
- **Uptime** - Formatted time since bot started (days, hours, minutes, seconds)
- **Guild Count** - Total number of guilds the bot is connected to
- **Latency** - WebSocket latency to Discord in milliseconds
- **Started At** - Timestamp when the bot process started

**Response Formatting:**
- Green embed color when connected
- Orange embed color for other connection states
- Timestamps displayed in Discord's relative time format
- Current timestamp in footer

**Usage Example:**
```
/status
```

**Sample Output:**
```
🤖 Bot Status
Bot Username: MyBot
Connection State: Connected
Uptime: 2d 5h 30m
Guild Count: 15
Latency: 42ms
Started At: December 7, 2025 10:30 AM
```

**Error Conditions:**
- Returns permission denied if user lacks Administrator permission
- Gracefully handles missing client information

---

### /guilds

**Description:** List all guilds the bot is connected to with interactive pagination

**Permission:** Requires Administrator permission in the guild

**Response Type:** Ephemeral embed message with pagination buttons

**Fields:**
For each guild (up to 5 per page):
- **Guild Name** - Display name of the guild
- **Guild ID** - Unique Discord snowflake ID

**Sorting:** Guilds are sorted by member count in descending order

**Pagination:**
- **Page Size:** 5 guilds per page
- **Navigation:** Previous and Next buttons for navigating pages
- **Page Indicator:** Shows current page and total pages in title
- **Total Count:** Shows total guild count in footer
- **Button Behavior:**
  - Previous button only shown when not on first page
  - Next button only shown when not on last page
  - Buttons automatically update page content when clicked

**State Management:**
- Interaction state stores guild list and pagination context
- State expires after 15 minutes
- Clicking pagination buttons updates the message in-place

**Usage Example:**
```
/guilds
```

**Sample Output (Page 1):**
```
📋 Connected Guilds (Page 1/3)

**Gaming Community** (ID: 123456789012345678)
**Developer Hub** (ID: 234567890123456789)
**Tech Talk** (ID: 345678901234567890)
**Anime Fans** (ID: 456789012345678901)
**Music Lovers** (ID: 567890123456789012)

Total: 15 guilds

[Next]
```

**Sample Output (Page 2):**
```
📋 Connected Guilds (Page 2/3)

**Book Club** (ID: 678901234567890123)
**Fitness Group** (ID: 789012345678901234)
**Art Community** (ID: 890123456789012345)
**Cooking Enthusiasts** (ID: 901234567890123456)
**Travel Blog** (ID: 012345678901234567)

Total: 15 guilds

[Previous] [Next]
```

**Interactive Workflow:**
1. User runs `/guilds` command
2. Bot responds with first page (5 guilds) and Next button
3. User clicks "Next" to view next page
4. Bot updates message with new page content and Previous/Next buttons
5. User can navigate through all pages
6. Interaction state expires after 15 minutes

**Component Security:**
- Only the user who invoked the command can click the pagination buttons
- If another user tries to click, they receive "You cannot interact with this component"
- If state expires, user receives "This interaction has expired" and must re-run the command

**Error Conditions:**
- Returns permission denied if user lacks Administrator permission
- Shows "The bot is not connected to any guilds" if guild count is 0
- Returns "You cannot interact with this component" if another user tries to click buttons
- Returns "This interaction has expired" if buttons are clicked after 15-minute timeout

---

### /shutdown

**Description:** Gracefully shut down the bot with interactive confirmation

**Permission:** Requires bot owner status (not just Administrator)

**Response Type:** Ephemeral embed message with confirmation buttons

**Behavior:**
1. Validates that the user is the application owner
2. Sends confirmation dialog with two buttons: "Confirm Shutdown" (danger) and "Cancel" (secondary)
3. Creates interaction state with correlation ID for tracking
4. Waits for user to click a button (15-minute timeout)
5. On confirmation, triggers `IHostApplicationLifetime.StopApplication()`
6. On cancel, removes the buttons and displays cancellation message

**Usage Example:**
```
/shutdown
```

**Initial Response:**
```
⚠️ Shutdown Confirmation
Are you sure you want to shut down the bot? This will stop all services and disconnect from Discord.

[Confirm Shutdown] [Cancel]
```

**After Confirmation:**
```
Shutdown confirmed. The bot is shutting down...
```

**After Cancellation:**
```
Shutdown cancelled.
```

**Interactive Workflow:**
1. User runs `/shutdown` command
2. Bot responds with confirmation dialog containing two buttons
3. User clicks "Confirm Shutdown" to proceed or "Cancel" to abort
4. Bot updates the message to remove buttons and show result
5. If confirmed, bot initiates graceful shutdown
6. All shutdown attempts are logged at WARNING level with user details

**Component Security:**
- Only the user who invoked the command can click the buttons
- Permission is re-validated when the button is clicked (not just at command invocation)
- Interaction state expires after 15 minutes
- If state expires, user receives "This interaction has expired" and must re-run the command

**Security:**
- Only the Discord application owner can execute this command
- Additional owner IDs can be configured in `appsettings.json` (future enhancement)
- All shutdown confirmation and cancellation events are logged at INFO level
- All shutdown requests are logged at WARNING level with owner details

**Error Conditions:**
- Returns "This command can only be used by the bot owner" for non-owners
- Returns "You cannot interact with this component" if another user tries to click the buttons
- Returns "This interaction has expired" if buttons are clicked after 15-minute timeout
- Rate limiting does not apply to shutdown command by default

---

## Permission System

### RequireAdmin Attribute

The `RequireAdminAttribute` is a precondition that enforces guild Administrator permission.

**Behavior:**
1. Checks if the command is executed in a guild (not DM)
2. Casts the user to `IGuildUser` to access permissions
3. Verifies `GuildPermissions.Administrator` is set
4. Returns success or permission denied error

**Usage:**
```csharp
[RequireAdmin]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    // All commands in this module require admin permission
}
```

Or on individual commands:
```csharp
[SlashCommand("mycommand", "Description")]
[RequireAdmin]
public async Task MyCommandAsync()
{
    // Command logic
}
```

**Error Messages:**
- "This command can only be used in a guild (server)." - When executed in DMs
- "Unable to retrieve guild user information." - Internal error
- "You must have Administrator permission to use this command." - Permission denied

---

### RequireOwner Attribute

The `RequireOwnerAttribute` is a precondition that enforces bot owner status.

**Behavior:**
1. Retrieves the Discord application info via `GetApplicationInfoAsync()`
2. Compares the user ID with the application owner ID
3. Returns success or permission denied error

**Usage:**
```csharp
[SlashCommand("shutdown", "Gracefully shut down the bot")]
[RequireOwner]
public async Task ShutdownAsync()
{
    // Command logic
}
```

**Error Messages:**
- "Unable to access Discord client." - Internal error
- "This command can only be used by the bot owner." - Permission denied

**Performance Note:**
The attribute makes an async API call to retrieve application info. This is cached by Discord.NET but should be used sparingly on high-frequency commands.

---

## Rate Limiting

### Configuration

Rate limiting is configured in `appsettings.json`:

```json
{
  "Discord": {
    "DefaultRateLimitInvokes": 3,
    "DefaultRateLimitPeriodSeconds": 60.0
  }
}
```

**Configuration Options:**

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DefaultRateLimitInvokes` | int | 3 | Number of invocations allowed within the period |
| `DefaultRateLimitPeriodSeconds` | double | 60.0 | Time window in seconds for rate limiting |

---

### RateLimit Attribute

The `RateLimitAttribute` applies per-user, per-guild, or global rate limiting to commands.

**Constructor Parameters:**
- `times` (int) - Number of allowed invocations
- `periodSeconds` (double) - Time period in seconds
- `target` (RateLimitTarget) - Scope of the rate limit (User, Guild, Global)

**Rate Limit Targets:**

| Target | Scope | Use Case |
|--------|-------|----------|
| `User` | Per user across all guilds | Prevent individual user spam |
| `Guild` | Per guild (all users share limit) | Protect guild-wide resources |
| `Global` | Entire bot across all users/guilds | Protect global resources or APIs |

**Usage Examples:**

```csharp
// Limit to 3 uses per user per 60 seconds
[RateLimit(3, 60.0, RateLimitTarget.User)]
[SlashCommand("search", "Search for something")]
public async Task SearchAsync(string query)
{
    // Command logic
}

// Limit to 10 uses per guild per 300 seconds (5 minutes)
[RateLimit(10, 300.0, RateLimitTarget.Guild)]
[SlashCommand("announcement", "Make an announcement")]
public async Task AnnouncementAsync(string message)
{
    // Command logic
}

// Global limit of 100 uses per hour across all users/guilds
[RateLimit(100, 3600.0, RateLimitTarget.Global)]
[SlashCommand("generate", "Generate AI content")]
public async Task GenerateAsync(string prompt)
{
    // Command logic
}
```

**Rate Limit Behavior:**
- Tracks invocations in a concurrent dictionary (`ConcurrentDictionary<string, List<DateTime>>`)
- Automatically removes expired invocations outside the time window
- Returns time until reset when rate limit is exceeded

**Error Message:**
```
Rate limit exceeded. Please wait 42.3 seconds before using this command again.
```

**Performance Considerations:**
- In-memory storage (does not persist across restarts)
- Automatic cleanup of expired entries
- Thread-safe implementation with locks per key
- Consider external caching (Redis) for distributed deployments

---

## Command Logging

### Automatic Logging

All command executions are automatically logged to the database with the following information:

| Field | Type | Description |
|-------|------|-------------|
| `Id` | Guid | Unique identifier for the log entry |
| `GuildId` | ulong? | Guild where command was executed (null for DMs) |
| `UserId` | ulong | User who executed the command |
| `CommandName` | string | Name of the executed command |
| `Parameters` | string? | JSON-serialized command parameters (reserved for future use) |
| `ExecutedAt` | DateTime | UTC timestamp of execution |
| `ResponseTimeMs` | int | Execution time in milliseconds |
| `Success` | bool | Whether the command completed successfully |
| `ErrorMessage` | string? | Error message if command failed |
| `CorrelationId` | string? | Correlation ID for request tracing |

### Correlation IDs

Every command execution generates a unique 16-character correlation ID for tracking requests across logs.

**Format:** First 16 characters of a GUID (lowercase, no hyphens)

**Example:** `7a3f9b2c8d1e4f5a`

**Usage:**
- Appears in structured logs
- Included in error embed footers shown to users
- Stored in `CommandLog` database table
- Used for troubleshooting and support

**Log Scope Example:**
```
[12:34:56 INF] AdminModule: Slash command 'status' executed successfully by JohnDoe in guild MyGuild (ID: 123456789), ExecutionTime: 42ms, CorrelationId: 7a3f9b2c8d1e4f5a
```

### Querying Command Logs

Command logs can be queried using the `ICommandLogRepository`:

```csharp
// Get recent commands for a user
var userLogs = await commandLogRepository.GetByUserIdAsync(userId, limit: 50);

// Get recent commands in a guild
var guildLogs = await commandLogRepository.GetByGuildIdAsync(guildId, limit: 100);

// Get failed commands for debugging
var failedLogs = await commandLogRepository.GetFailedCommandsAsync(limit: 100);

// Get commands by correlation ID
var correlatedLog = await commandLogRepository.GetByCorrelationIdAsync(correlationId);
```

---

## Error Handling

### Permission Denied Errors

When a user lacks the required permission, they receive an ephemeral (private) embed message:

```
Permission Denied
You must have Administrator permission to use this command.

Correlation ID: 7a3f9b2c8d1e4f5a
```

**Properties:**
- Red color
- Ephemeral (only visible to the user who invoked the command)
- Includes correlation ID for support
- Automatically logged to database as failed command

### Rate Limit Errors

When a user exceeds the rate limit, they receive an ephemeral message with the cooldown time:

```
Permission Denied
Rate limit exceeded. Please wait 42.3 seconds before using this command again.

Correlation ID: 7a3f9b2c8d1e4f5a
```

### Internal Errors

When an unexpected error occurs during command execution:

```
Error
An error occurred while executing this command.

Correlation ID: 7a3f9b2c8d1e4f5a
```

**Logging:**
- Error logged at ERROR level with full stack trace
- Correlation ID allows linking user reports to server logs
- Command execution logged to database with `Success = false`

---

## Best Practices

### Command Design

1. **Use Ephemeral Responses for Sensitive Data**
   - Admin status information should be ephemeral when appropriate
   - Error messages are always ephemeral

2. **Provide Feedback**
   - Always acknowledge command execution with a response
   - Use embed formatting for rich, structured information
   - Include timestamps for time-sensitive data

3. **Limit Data Exposure**
   - Paginate large datasets (guilds list limits to 25)
   - Don't expose internal IDs unnecessarily
   - Consider privacy when logging user actions

### Permission Strategy

1. **Principle of Least Privilege**
   - Grant minimum necessary permissions
   - Use `RequireAdmin` for server management commands
   - Reserve `RequireOwner` for bot lifecycle commands

2. **DM Handling**
   - Guild-specific commands should check for DM context
   - Provide clear error messages for DM restrictions

3. **Custom Preconditions**
   - Create custom attributes for complex permission logic
   - Combine multiple preconditions when needed

### Rate Limiting Strategy

1. **Choose Appropriate Targets**
   - `User` - Most common, prevents individual abuse
   - `Guild` - Protects shared resources in a community
   - `Global` - Protects external API quotas or bot-wide resources

2. **Set Reasonable Limits**
   - Consider typical usage patterns
   - Allow for legitimate rapid usage (e.g., pagination)
   - Start conservative and adjust based on monitoring

3. **Communicate Cooldowns**
   - Error messages include time until reset
   - Consider showing remaining uses in command responses

---

## Configuration Examples

### Production Configuration

```json
{
  "Discord": {
    "Token": "",
    "TestGuildId": null,
    "DefaultRateLimitInvokes": 5,
    "DefaultRateLimitPeriodSeconds": 120.0,
    "AdditionalOwnerIds": []
  }
}
```

### Development Configuration

```json
{
  "Discord": {
    "Token": "",
    "TestGuildId": 123456789012345678,
    "DefaultRateLimitInvokes": 100,
    "DefaultRateLimitPeriodSeconds": 10.0,
    "AdditionalOwnerIds": [234567890123456789]
  }
}
```

**Development Notes:**
- `TestGuildId` enables instant command registration for faster iteration
- Higher rate limits prevent interruptions during testing
- `AdditionalOwnerIds` allows team members to test owner commands

---

## Troubleshooting

### Command Not Appearing in Discord

**Symptom:** Slash commands don't show up in Discord's command picker

**Possible Causes:**
1. Bot not invited with `applications.commands` scope
2. Commands registered to test guild but testing in different guild
3. Global command registration hasn't propagated (takes up to 1 hour)

**Solutions:**
- Re-invite bot with proper scopes: `bot` + `applications.commands`
- Set `TestGuildId` in configuration for instant registration
- Check logs for command registration errors

### Permission Denied for Admin Commands

**Symptom:** Users with Administrator permission receive permission denied

**Possible Causes:**
1. User has Administrator permission from role but not directly
2. Bot checks for `GuildPermissions.Administrator` specifically
3. Command executed in DMs

**Solutions:**
- Verify user has Administrator permission in guild
- Check command is executed in guild, not DMs
- Review precondition logic for custom permission checks

### Rate Limit Not Resetting

**Symptom:** Rate limit persists beyond the configured period

**Possible Causes:**
1. Bot restarted (in-memory storage cleared)
2. Clock skew on server
3. Multiple bot instances with separate rate limit tracking

**Solutions:**
- Rate limits are in-memory and reset on bot restart
- Verify server time is correct
- Consider Redis for distributed rate limiting in multi-instance deployments

### Missing Correlation IDs in Logs

**Symptom:** Correlation IDs not appearing in log output

**Possible Causes:**
1. Log scope not preserved across async calls
2. Structured logging not configured properly
3. Serilog output template missing scope properties

**Solutions:**
- Verify Serilog configuration includes scope enrichment
- Check output template includes `{Properties}` or scope variables
- Ensure `AsyncLocal` execution context is maintained

---

## Related Documentation

- [Interactive Components](interactive-components.md) - Button interactions, state management, and component ID conventions
- [Permissions System](permissions.md) - Detailed precondition attribute documentation
- [Database Schema](database-schema.md) - CommandLog entity and relationships
- [Repository Pattern](repository-pattern.md) - ICommandLogRepository usage
- [Configuration Guide](../CLAUDE.md#configuration) - User secrets and appsettings

---

*Document Version: 1.0*
*Last Updated: December 2025*
*Status: Phase 3 Implementation Complete*
