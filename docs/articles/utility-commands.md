---
uid: utility-commands
title: Utility Commands
description: Information commands for users, servers, and roles
---

# Utility Commands

This document provides a comprehensive reference for utility slash commands that allow users to query information about users, servers, and roles. Utility commands are informational and do not modify any data.

## Overview

Utility commands provide quick access to Discord entity information through formatted embed responses. All commands:

- Are available to all guild members (no special permissions required)
- Return ephemeral responses (visible only to the command user)
- Use embed formatting for structured data display
- Require the guild to be active in the bot's system

Commands are automatically logged to the database with correlation IDs for audit tracking and troubleshooting.

---

## Command Reference

### /userinfo [user]

**Description:** Display detailed information about a Discord user

**Parameters:**
- `user` (optional) - The user to query information about. If omitted, displays information about yourself.

**Permission:** None (available to all guild members)

**Preconditions:** `RequireGuildActive`

**Response Type:** Ephemeral embed message

**Fields:**
- **Username** - The user's current Discord username
- **Display Name** - The user's display name in the guild (nickname or username)
- **User ID** - Discord snowflake ID
- **Account Created** - Timestamp when the Discord account was created (relative format)
- **Joined Server** - Timestamp when the user joined the guild (relative format)
- **Boost Status** - Whether the user is boosting the server and since when
- **Roles** - List of assigned roles (excluding @everyone)
- **Key Permissions** - Highlights dangerous or notable permissions (Administrator, Manage Guild, Manage Roles, etc.)

**Response Formatting:**
- Embed color matches user's highest role color
- Timestamps displayed in Discord's relative time format (`<t:timestamp:R>`)
- User avatar displayed as embed thumbnail
- Dangerous permissions highlighted in separate field

**Usage Example:**
```
/userinfo
/userinfo user:@JohnDoe
```

**Sample Output:**
```
👤 User Information

Username: JohnDoe#1234
Display Name: John
User ID: 123456789012345678
Account Created: 2 years ago
Joined Server: 6 months ago
Boost Status: Boosting since 3 months ago

Roles:
Admin, Moderator, Member

Key Permissions:
• Administrator
• Manage Channels
```

**Edge Cases:**
- If user is not in the guild, command fails with "User not found in this guild"
- If user has no roles (besides @everyone), displays "No roles"
- If user has no notable permissions, "Key Permissions" field is omitted
- Boost status only shown if user is actively boosting

---

### /serverinfo

**Description:** Display detailed information about the current Discord server (guild)

**Parameters:** None

**Permission:** None (available to all guild members)

**Preconditions:** `RequireGuildActive`

**Response Type:** Ephemeral embed message

**Fields:**
- **Owner** - The guild owner's mention and ID
- **Server ID** - Discord snowflake ID
- **Created** - Timestamp when the guild was created (relative format)
- **Members** - Total member count
- **Text Channels** - Number of text channels
- **Voice Channels** - Number of voice channels
- **Roles** - Total role count
- **Boost Status** - Current boost level and number of boosts
- **Vanity URL** - Custom invite URL (if configured)
- **Features** - Notable guild features (verified, partnered, community, etc.)

**Response Formatting:**
- Embed color varies by boost level:
  - No boost: Gray
  - Level 1: Pink
  - Level 2: Lighter pink
  - Level 3: White/highest tier
- Guild icon displayed as embed thumbnail
- Server banner as embed image (if available)
- Current timestamp in footer

**Usage Example:**
```
/serverinfo
```

**Sample Output:**
```
🏰 Server Information

Owner: @ServerOwner (ID: 123456789012345678)
Server ID: 987654321098765432
Created: 3 years ago

Members: 1,250
Text Channels: 25
Voice Channels: 8
Roles: 15

Boost Status: Level 2 (15 boosts)
Vanity URL: discord.gg/mycommunity

Features:
• Community
• News Channels
• Invite Splash
```

**Edge Cases:**
- If guild has no vanity URL, field is omitted
- If guild has no special features, "Features" field shows "None"
- Channel counts include categories
- Member count may include bots

---

### /roleinfo <role>

**Description:** Display detailed information about a guild role

**Parameters:**
- `role` (required) - The role to query information about

**Permission:** None (available to all guild members)

**Preconditions:** `RequireGuildActive`

**Response Type:** Ephemeral embed message

**Fields:**
- **Role Name** - Display name of the role
- **Role ID** - Discord snowflake ID
- **Color** - Hex color code
- **Members** - Number of users with this role
- **Position** - Role hierarchy position
- **Created** - Timestamp when the role was created (relative format)
- **Mentionable** - Whether the role can be mentioned by non-admins
- **Hoisted** - Whether the role is displayed separately in the member list
- **Permissions** - Summary of granted permissions
- **Dangerous Permissions** - Highlighted warning if role has Administrator, Manage Roles, or other high-risk permissions

**Response Formatting:**
- Embed color matches role color
- Role mention displayed in title
- Dangerous permissions shown in red warning field
- Permissions displayed as bullet list

**Usage Example:**
```
/roleinfo role:@Moderator
```

**Sample Output:**
```
🎭 Role Information

@Moderator
Role ID: 456789012345678901
Color: #5865F2
Members: 12
Position: 5
Created: 8 months ago

Mentionable: Yes
Hoisted: Yes

Permissions:
• Kick Members
• Ban Members
• Manage Messages
• Mute Members

⚠️ Dangerous Permissions Detected:
• Manage Roles
• Manage Channels
```

**Edge Cases:**
- Cannot query @everyone role (filtered out in autocomplete)
- If role has no members, displays "Members: 0"
- If role has default color, displays "Color: Default"
- If role has no dangerous permissions, warning field is omitted
- Managed roles (bot roles, booster role) are marked with "(Managed)" tag

---

## Preconditions

### RequireGuildActive Attribute

All utility commands require the guild to be active in the bot's system.

**Behavior:**
1. Checks if the command is executed in a guild (not DM)
2. Queries the database for guild settings
3. Verifies guild is marked as active
4. Returns success or error

**Error Messages:**
- "This command can only be used in a guild (server)." - When executed in DMs
- "This server is not active in the bot system." - When guild is inactive or not found

**Implementation:**
```csharp
[RequireGuildActive]
public class UtilityModule : InteractionModuleBase<SocketInteractionContext>
{
    // All commands in this module require active guild
}
```

---

## Response Format

### Embed Design

All utility commands use Discord embed format for rich, structured responses:

**Common Elements:**
- **Title** - Icon + command type (e.g., "👤 User Information")
- **Description** - Primary entity name or summary
- **Fields** - Structured key-value data with inline formatting
- **Thumbnail** - Entity icon (user avatar, guild icon, role color)
- **Footer** - Correlation ID or timestamp for support
- **Color** - Context-appropriate color (role color, boost tier, etc.)

**Ephemeral Behavior:**

All responses are ephemeral (visible only to the command user). This ensures:
- Privacy for user queries
- Reduced channel clutter
- Focus on individual information needs

**Timestamp Formatting:**

Discord timestamp formats used:
- `<t:timestamp:F>` - Full date and time (e.g., "Monday, December 31, 2024 3:00 PM")
- `<t:timestamp:R>` - Relative time (e.g., "2 years ago", "6 months ago")
- `<t:timestamp:D>` - Date only (e.g., "December 31, 2024")

---

## Permission Highlighting

### Dangerous Permissions

The `/userinfo` and `/roleinfo` commands highlight dangerous permissions that grant significant control:

**Dangerous Permission List:**
- **Administrator** - Full control over guild (bypasses all permissions)
- **Manage Guild** - Change guild settings, integrations, webhooks
- **Manage Roles** - Create, edit, delete roles and assign to users
- **Manage Channels** - Create, edit, delete channels and categories
- **Manage Webhooks** - Create and modify webhooks
- **Ban Members** - Permanently remove users from guild
- **Kick Members** - Remove users from guild (can rejoin)
- **Manage Messages** - Delete messages from other users
- **Mention Everyone** - Ping @everyone and @here
- **Mute Members** - Server mute/deafen members in voice
- **Move Members** - Move members between voice channels

**Warning Display:**

When dangerous permissions are detected, a separate field is added:

```
⚠️ Dangerous Permissions Detected:
• Administrator
• Manage Roles
• Ban Members
```

This helps users quickly identify roles or members with elevated privileges.

---

## Command Logging

### Automatic Logging

All command executions are automatically logged to the database with the following information:

| Field | Type | Description |
|-------|------|-------------|
| `Id` | Guid | Unique identifier for the log entry |
| `GuildId` | ulong? | Guild where command was executed |
| `UserId` | ulong | User who executed the command |
| `CommandName` | string | Name of the executed command |
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

---

## Error Handling

### Common Error Scenarios

**Permission Denied:**
```
Permission Denied
This server is not active in the bot system.

Correlation ID: 7a3f9b2c8d1e4f5a
```

**User Not Found:**
```
Error
User not found in this guild.

Correlation ID: 7a3f9b2c8d1e4f5a
```

**Role Not Found:**
```
Error
Role not found in this guild.

Correlation ID: 7a3f9b2c8d1e4f5a
```

**Internal Error:**
```
Error
An error occurred while executing this command.

Correlation ID: 7a3f9b2c8d1e4f5a
```

### Error Response Properties

All error responses are:
- **Ephemeral** - Only visible to the command user
- **Red color** - Indicates error state
- **Correlation ID** - Included in footer for support
- **Logged** - Recorded in database with `Success = false`

---

## Performance Considerations

### Caching

User, guild, and role information is cached by Discord.NET's `DiscordSocketClient`. Commands retrieve data from local cache, making them extremely fast (typically <50ms response time).

**Cache Behavior:**
- Guild member cache populated when bot joins guild
- User cache updated on presence/voice state changes
- Role cache updated on role create/update/delete events
- Cache is in-memory and lost on bot restart

### Rate Limiting

Utility commands do NOT have rate limiting by default since they are:
- Read-only operations
- Fast (cache-based)
- Ephemeral (low channel spam risk)

Rate limiting can be added via `[RateLimit]` attribute if needed:

```csharp
[RateLimit(5, 60.0, RateLimitTarget.User)] // 5 uses per user per 60 seconds
[SlashCommand("userinfo", "Display user information")]
public async Task UserInfoAsync(IUser? user = null)
{
    // Command logic
}
```

---

## Best Practices

### Using Utility Commands

**For Users:**
1. Use `/userinfo` to check your own join date, roles, and permissions
2. Use `/serverinfo` to see server statistics and boost status
3. Use `/roleinfo` to understand role permissions before requesting assignment
4. All responses are private - safe to check sensitive information

**For Moderators:**
1. Use `/userinfo @user` to verify member roles and permissions
2. Use `/roleinfo @role` to audit role permissions for security
3. Check "Dangerous Permissions" warnings when assigning roles
4. Use correlation IDs to report issues to bot administrators

### Command Design Patterns

When implementing similar commands:

1. **Ephemeral by Default** - Information queries should be private
2. **Embed Formatting** - Use structured embeds for rich data display
3. **Correlation IDs** - Always include for troubleshooting
4. **Dangerous Permission Warnings** - Highlight security-relevant data
5. **Graceful Degradation** - Omit missing fields rather than showing "None" or "N/A"

---

## Configuration

### Guild Activation

Guilds must be active in the bot system for utility commands to work. Guild activation happens automatically when:
- Bot joins the guild
- Administrator runs `/setup` or similar initialization command
- Guild is manually activated via admin UI

**Checking Guild Status:**

Administrators can verify guild activation status in the bot's admin UI under Guilds page.

**Reactivating Inactive Guilds:**

If a guild becomes inactive, contact bot administrators to reactivate through the admin UI.

---

## Related Documentation

- [Admin Commands](admin-commands.md) - Administrative slash commands
- [Interactive Components](interactive-components.md) - Button interactions and state management
- [Database Schema](database-schema.md) - CommandLog entity and relationships
- [Permissions](permissions.md) - Precondition attribute documentation

---

*Document Version: 1.0*
*Last Updated: January 2026*
*Status: v0.5.0 Implementation Complete*
