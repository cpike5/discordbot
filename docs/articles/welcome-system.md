# Welcome System Documentation

**Version:** 1.0
**Last Updated:** 2025-12-30
**Feature Area:** Guild Configuration
**Related Issues:** #313 (Documentation), Epic #303 (Documentation Overhaul)

---

## Overview

The Welcome System provides automated greeting messages for new members joining Discord servers (guilds). When a user joins a configured guild, the bot automatically sends a customizable welcome message to a designated channel. This feature enhances the onboarding experience and helps new members feel welcomed to the community.

### Key Features

- **Automated welcome messages**: Trigger on `GuildMemberJoined` Discord event
- **Customizable message templates**: Support for dynamic placeholders (user, server name, member count)
- **Rich embed support**: Send messages as Discord embeds with custom colors and avatars
- **Channel selection**: Choose any text-capable channel for welcome messages
- **Global and per-guild toggles**: Feature can be disabled globally or per-guild
- **Live preview**: Admin UI shows real-time preview of message appearance
- **Template variables**: Insert dynamic content using placeholder tokens

---

## Architecture

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| **WelcomeConfiguration** | `DiscordBot.Core.Entities` | Database entity storing per-guild settings |
| **IWelcomeService** | `DiscordBot.Core.Interfaces` | Service interface for configuration and message sending |
| **WelcomeService** | `DiscordBot.Bot.Services` | Service implementation with template processing |
| **WelcomeHandler** | `DiscordBot.Bot.Handlers` | Discord event handler for `UserJoined` event |
| **WelcomeModule** | `DiscordBot.Bot.Commands` | Slash command module for Discord configuration |
| **WelcomeController** | `DiscordBot.Bot.Controllers` | REST API endpoints for configuration |
| **WelcomeModel** | `DiscordBot.Bot.Pages.Guilds` | Razor Pages admin UI for configuration |

### Data Flow

```
Discord Event: UserJoined
         ↓
WelcomeHandler (event subscriber)
         ↓
Check global setting: Features:WelcomeMessagesEnabled
         ↓
WelcomeService.SendWelcomeMessageAsync()
         ↓
Load WelcomeConfiguration from database
         ↓
Check IsEnabled for guild
         ↓
Replace template variables
         ↓
Send message to configured channel (embed or plain text)
```

---

## Welcome Configuration

### Database Entity

The `WelcomeConfiguration` entity stores per-guild welcome settings:

```csharp
public class WelcomeConfiguration
{
    public ulong GuildId { get; set; }              // Primary key (Discord snowflake ID)
    public bool IsEnabled { get; set; }             // Enable/disable for this guild
    public ulong? WelcomeChannelId { get; set; }    // Target channel for messages
    public string WelcomeMessage { get; set; }      // Message template with placeholders
    public bool IncludeAvatar { get; set; }         // Show user avatar in embed
    public bool UseEmbed { get; set; }              // Send as embed vs plain text
    public string? EmbedColor { get; set; }         // Hex color for embed (e.g., "#5865F2")
    public DateTime CreatedAt { get; set; }         // Configuration creation timestamp
    public DateTime UpdatedAt { get; set; }         // Last modification timestamp
}
```

### Default Values

| Field | Default | Description |
|-------|---------|-------------|
| `IsEnabled` | `true` | Welcome messages enabled on creation |
| `WelcomeChannelId` | `null` | No channel configured (must be set) |
| `WelcomeMessage` | `""` | Empty message (must be set) |
| `IncludeAvatar` | `true` | Show user avatar in embed |
| `UseEmbed` | `true` | Send as rich embed by default |
| `EmbedColor` | `null` | Discord default embed color |

### Global Feature Toggle

The welcome system has a global feature toggle in application settings:

**Setting Key:** `Features:WelcomeMessagesEnabled`
**Type:** Boolean
**Default:** `true`

When set to `false`, welcome messages are disabled across all guilds regardless of per-guild configuration. This is checked by `WelcomeHandler` before processing any join events.

---

## Message Templates

### Template Placeholders

Welcome messages support dynamic placeholders that are replaced with actual values when the message is sent:

| Placeholder | Description | Example Output |
|------------|-------------|----------------|
| `{user}` | Mentions the new member (Discord mention) | `@NewMember` |
| `{username}` | Member's display name (plain text) | `NewMember` |
| `{server}` | Guild/server name | `My Awesome Server` |
| `{membercount}` | Current member count in guild | `1,234` |

**Case Insensitive:** Placeholders are matched case-insensitively (e.g., `{User}`, `{USER}`, `{user}` all work).

### Template Processing

Template variables are replaced in `WelcomeService.ReplaceTemplateVariables()`:

```csharp
private static string ReplaceTemplateVariables(string template, SocketGuild guild, SocketGuildUser user)
{
    return template
        .Replace("{user}", user.Mention, StringComparison.OrdinalIgnoreCase)
        .Replace("{username}", user.DisplayName, StringComparison.OrdinalIgnoreCase)
        .Replace("{server}", guild.Name, StringComparison.OrdinalIgnoreCase)
        .Replace("{membercount}", guild.MemberCount.ToString(), StringComparison.OrdinalIgnoreCase);
}
```

### Example Templates

**Basic Welcome:**
```
Welcome to {server}, {user}! You are member #{membercount}.
```

**Friendly Greeting:**
```
Hey {username}! 👋 Welcome to {server}!

We're excited to have you as our {membercount}th member. Check out the rules and introduce yourself!
```

**Formal Welcome:**
```
Welcome {user}!

You have joined {server}. Please review our community guidelines and feel free to ask questions.

Current members: {membercount}
```

**Fun Welcome:**
```
🎉 {username} just joined the party! 🎉

{server} now has {membercount} amazing members!
```

---

## Message Appearance

### Embed vs Plain Text

**Embed Messages (UseEmbed = true):**
- Rich Discord embed with border color
- Supports avatar thumbnail
- Timestamp automatically added
- Professional appearance

**Plain Text Messages (UseEmbed = false):**
- Simple text message
- No special formatting
- No avatar or color customization
- Lightweight

### Embed Color

When `UseEmbed` is `true`, you can customize the embed border color:

- **Format:** Hex color code (e.g., `#5865F2`)
- **Validation:** Must match pattern `^#[0-9A-Fa-f]{6}$`
- **Default:** Discord's default embed color (if null)

The admin UI provides a color picker for easy selection.

### Avatar Display

When `IncludeAvatar` is `true` and `UseEmbed` is `true`:
- User's Discord avatar displayed as embed thumbnail
- Size: 256x256 pixels
- Automatically fetches user's current avatar

---

## Slash Commands

The welcome system provides Discord slash commands for in-app configuration. All commands require the `[RequireAdmin]` precondition.

**Command Group:** `/welcome`
**Authorization:** Requires `Admin` role (guild-level permission)
**Rate Limit:** 5 commands per 60 seconds

### Available Commands

#### `/welcome show`

Displays the current welcome message configuration for the guild.

**Response:** Ephemeral embed showing:
- Status (Enabled/Disabled)
- Welcome channel
- Use embed setting
- Include avatar setting
- Embed color
- Message template
- Available template variables

**Example Response:**
```
📋 Welcome Message Configuration

Status: Enabled
Channel: #welcome
Use Embed: Yes
Include Avatar: Yes
Embed Color: #5865F2

Message Template:
```
Welcome to {server}, {user}!
```

Template Variables:
{user} - mentions the user
{username} - user's display name
{server} - guild name
{membercount} - current member count
```

#### `/welcome enable`

Enables welcome messages for the guild.

**Effect:** Sets `IsEnabled` to `true` in the database.

**Response:** Ephemeral embed confirming activation and showing current channel and message settings.

**Note:** If channel or message not configured, the response includes instructions to set them.

#### `/welcome disable`

Disables welcome messages for the guild.

**Effect:** Sets `IsEnabled` to `false` in the database (configuration is preserved for re-enabling).

**Response:** Ephemeral embed confirming deactivation.

#### `/welcome channel <channel>`

Sets the channel where welcome messages will be sent.

**Parameters:**
- `channel` (ITextChannel, required): The text channel to send welcome messages to

**Supported Channel Types:**
- Text channels
- Voice channels (with text chat)
- Announcement/News channels
- Stage channels (with text chat)

**Effect:** Updates `WelcomeChannelId` in the database.

**Response:** Ephemeral embed confirming the channel update and showing enabled status.

#### `/welcome message <message>`

Sets the welcome message template.

**Parameters:**
- `message` (string, required, max 2000 characters): The welcome message template

**Effect:** Updates `WelcomeMessage` in the database.

**Response:** Ephemeral embed showing:
- New message template
- Available template variables
- Enabled status

**Validation:** Message limited to 2000 characters (Discord message limit).

#### `/welcome test`

Sends a test welcome message to the configured channel using the current user.

**Effect:** Calls `WelcomeService.SendWelcomeMessageAsync()` with the executing user's ID.

**Requirements:**
- Welcome configuration must exist
- Welcome channel must be set
- Welcome message must be set

**Response:**
- **Success:** Ephemeral message confirming test sent
- **Incomplete Config:** Ephemeral message explaining what's missing
- **Failure:** Ephemeral message with troubleshooting tips (permissions, channel exists, etc.)

**Use Case:** Test message appearance before enabling or after making changes.

---

## Admin UI Configuration

### Page Location

**Route:** `/Guilds/Welcome/{id:long}`
**Page Model:** `WelcomeModel` (`Pages/Guilds/Welcome.cshtml.cs`)
**Authorization:** Requires `Admin` policy

### Features

#### Two-Column Layout

**Left Column: Configuration Form**
- Enable/disable toggle
- Channel selector dropdown
- Message template textarea with token toolbar
- Avatar and embed toggles
- Embed color picker
- Token help panel (collapsible)
- Save/Cancel buttons

**Right Column: Live Preview**
- Discord-styled message preview
- Real-time updates as you type
- Sample data reference
- Shows embed color and avatar

#### Token Insertion Toolbar

Clickable buttons above the message textarea:
- `{user}` button
- `{username}` button
- `{server}` button
- `{membercount}` button

Clicking a button inserts the token at the cursor position in the textarea.

#### Channel Selector

Dropdown showing all text-capable channels with type indicators:
- `#` for text channels
- `🔊` for voice channels
- `📢` for announcement channels
- `🎭` for stage channels

Channels sorted by position (as they appear in Discord).

#### Live Preview

Real-time Discord-styled message preview showing:
- Bot avatar and "BOT" badge
- Current timestamp
- Message with replaced tokens (sample data)
- Embed border color (if enabled)
- User avatar thumbnail (if enabled)

**Sample Data for Preview:**
- `{user}` → `@NewMember`
- `{username}` → `NewMember`
- `{server}` → Actual guild name
- `{membercount}` → `1,234`

#### Form Validation

**Client-side:**
- Channel required when enabled
- Message max length 2000 characters
- Embed color format validation (`^#[0-9A-Fa-f]{6}$`)

**Server-side:**
- ModelState validation
- Channel required when `IsEnabled = true`
- Embed color regex validation

**Validation Messages:**
- Displayed inline below fields
- Red error text
- Prevents form submission until resolved

#### Success/Error Feedback

- Success message on save (TempData, dismissible alert)
- Error message for failures (dismissible alert)
- Redirect to same page after successful save (PRG pattern)

---

## API Endpoints

The welcome system exposes REST API endpoints for programmatic configuration.

**Base Path:** `/api/guilds/{guildId}/welcome`
**Authorization:** Requires `Admin` policy
**Content-Type:** `application/json`

See [API Endpoints Reference](api-endpoints.md) for general API documentation.

### GET /api/guilds/{guildId}/welcome

Retrieves the welcome configuration for a specific guild.

**Path Parameters:**
- `guildId` (ulong): Discord guild snowflake ID

**Response: 200 OK**
```json
{
  "guildId": 123456789012345678,
  "isEnabled": true,
  "welcomeChannelId": 987654321098765432,
  "welcomeMessage": "Welcome to {server}, {user}!",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#5865F2",
  "createdAt": "2025-12-26T10:30:00Z",
  "updatedAt": "2025-12-30T14:20:00Z"
}
```

**Response: 404 Not Found**
```json
{
  "message": "Welcome configuration not found",
  "detail": "No welcome configuration exists for guild 123456789012345678.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**DTO:** `WelcomeConfigurationDto`

### PUT /api/guilds/{guildId}/welcome

Updates the welcome configuration for a specific guild. Creates a new configuration if one doesn't exist.

**Path Parameters:**
- `guildId` (ulong): Discord guild snowflake ID

**Request Body (all fields optional):**
```json
{
  "isEnabled": true,
  "welcomeChannelId": 987654321098765432,
  "welcomeMessage": "Welcome to {server}, {user}!",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#5865F2"
}
```

**DTO:** `WelcomeConfigurationUpdateDto` (all properties nullable for partial updates)

**Response: 200 OK**

Returns the updated `WelcomeConfigurationDto` (same format as GET).

**Response: 404 Not Found**

Returned when the guild doesn't exist in the database.

```json
{
  "message": "Guild not found",
  "detail": "No guild with ID 123456789012345678 exists in the database.",
  "statusCode": 404,
  "traceId": "00-abc123-def456-00"
}
```

**Response: 400 Bad Request**

Returned when request body is null or invalid.

**Audit Logging:**

Updates are automatically logged to the audit log:
- **Category:** `Configuration`
- **Action:** `Created` or `Updated`
- **Target:** `WelcomeConfiguration`, guild ID
- **Details:** `isEnabled`, `welcomeChannelId`, `hasMessage`
- **Actor:** `System` (API calls don't include user context)

### POST /api/guilds/{guildId}/welcome/preview

Generates a preview of the welcome message for testing.

**Path Parameters:**
- `guildId` (ulong): Discord guild snowflake ID

**Request Body:**
```json
{
  "previewUserId": 123456789012345678
}
```

**DTO:** `WelcomePreviewRequestDto`

**Fields:**
- `previewUserId` (ulong, required): Discord user ID to use for template preview

**Response: 200 OK**
```json
{
  "message": "Welcome to My Server, @UserDisplayName! You are member #1,234."
}
```

**Response: 404 Not Found**

Returned when configuration doesn't exist, guild not found, or user not found in guild.

**Response: 400 Bad Request**

Returned when `previewUserId` is 0 or null.

**Use Case:** Test message template rendering before enabling or after changes.

---

## Event Handling

### UserJoined Event Flow

1. **Discord Event:** User joins a guild
2. **Event Subscriber:** `WelcomeHandler.HandleUserJoinedAsync()` receives `SocketGuildUser`
3. **Global Check:** Query `ISettingsService` for `Features:WelcomeMessagesEnabled`
4. **Service Call:** If enabled, call `IWelcomeService.SendWelcomeMessageAsync(guildId, userId)`
5. **Configuration Load:** Service loads `WelcomeConfiguration` from database
6. **Validation:** Check `IsEnabled`, `WelcomeChannelId`, `WelcomeMessage` all set
7. **Discord API:** Fetch guild, channel, and user objects from `DiscordSocketClient`
8. **Template Processing:** Replace placeholders with actual values
9. **Message Send:** Send embed or plain text message to configured channel
10. **Logging:** Log success or failure

### WelcomeHandler Implementation

**Service Scope:** `WelcomeHandler` is registered as singleton (subscribes to Discord events), but uses `IServiceScopeFactory` to access scoped services (`ISettingsService`, `IWelcomeService`).

**Error Handling:** Exceptions are logged but do not crash the bot. Join events continue processing even if welcome messages fail.

**Registered In:** `BotHostedService.cs`

```csharp
_client.UserJoined += async (user) =>
{
    var welcomeHandler = _serviceProvider.GetRequiredService<WelcomeHandler>();
    await welcomeHandler.HandleUserJoinedAsync(user);
};
```

---

## Service Layer

### IWelcomeService Interface

Located in `DiscordBot.Core.Interfaces.IWelcomeService`

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `GetConfigurationAsync(guildId)` | `Task<WelcomeConfigurationDto?>` | Get configuration for guild |
| `UpdateConfigurationAsync(guildId, updateDto)` | `Task<WelcomeConfigurationDto?>` | Update/create configuration |
| `SendWelcomeMessageAsync(guildId, userId)` | `Task<bool>` | Send welcome message for new member |
| `PreviewWelcomeMessageAsync(guildId, previewUserId)` | `Task<string?>` | Generate message preview |

### WelcomeService Implementation

Located in `DiscordBot.Bot.Services.WelcomeService`

**Dependencies:**
- `IWelcomeConfigurationRepository` - Database access
- `DiscordSocketClient` - Discord API access
- `ILogger<WelcomeService>` - Logging
- `IAuditLogService` - Audit trail

**Key Methods:**

#### SendWelcomeMessageAsync

**Validation Steps:**
1. Check configuration exists
2. Check `IsEnabled = true`
3. Check `WelcomeChannelId` is set
4. Verify guild exists in Discord client
5. Verify channel exists in guild
6. Verify user exists in guild

**Message Construction:**
- Replace template variables
- Build embed (if `UseEmbed = true`)
- Apply embed color (if valid hex)
- Add avatar thumbnail (if `IncludeAvatar = true`)
- Add timestamp

**Error Handling:**
- Logs warnings for missing data
- Returns `false` on failure (doesn't throw)
- Logs errors for send failures (permissions, etc.)

#### PreviewWelcomeMessageAsync

Similar validation to `SendWelcomeMessageAsync` but returns the rendered message string instead of sending it.

**Use Cases:**
- API preview endpoint
- Admin UI live preview (client-side, not used currently)
- Testing templates before enabling

---

## Configuration Examples

### Example 1: Basic Text Welcome

```json
{
  "isEnabled": true,
  "welcomeChannelId": 123456789012345678,
  "welcomeMessage": "Welcome to {server}, {user}!",
  "includeAvatar": false,
  "useEmbed": false,
  "embedColor": null
}
```

**Result:** Plain text message in #welcome channel:
```
Welcome to My Server, @NewMember!
```

### Example 2: Rich Embed with Avatar

```json
{
  "isEnabled": true,
  "welcomeChannelId": 123456789012345678,
  "welcomeMessage": "Welcome {user}!\n\nYou are member #{membercount} of {server}. Please read the rules and enjoy your stay!",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#5865F2"
}
```

**Result:** Embed message with blue border, user avatar, and timestamp:

```
[Embed with blue left border]
[User avatar thumbnail in top-right]

Welcome @NewMember!

You are member #1,234 of My Server. Please read the rules and enjoy your stay!

[Timestamp: Today at 2:30 PM]
```

### Example 3: Disabled Configuration

```json
{
  "isEnabled": false,
  "welcomeChannelId": 123456789012345678,
  "welcomeMessage": "Welcome to {server}, {user}!",
  "includeAvatar": true,
  "useEmbed": true,
  "embedColor": "#FF5733"
}
```

**Result:** No message sent when users join (configuration preserved for re-enabling).

---

## Troubleshooting

### Welcome Messages Not Sending

**Check:**
1. **Global feature enabled:** `Features:WelcomeMessagesEnabled = true` in settings
2. **Guild configuration enabled:** `IsEnabled = true` for the guild
3. **Channel configured:** `WelcomeChannelId` is set and channel exists
4. **Message configured:** `WelcomeMessage` is not empty
5. **Bot permissions:** Bot has `SendMessages` permission in welcome channel
6. **Bot online:** Discord bot is connected and running
7. **Logs:** Check `logs/discordbot-*.log` for errors or warnings

### Embed Not Displaying Correctly

**Check:**
1. **UseEmbed enabled:** `UseEmbed = true` in configuration
2. **Embed permissions:** Bot has `EmbedLinks` permission in channel
3. **Color format:** `EmbedColor` matches `^#[0-9A-Fa-f]{6}$` (e.g., `#5865F2`, not `5865F2`)

### Avatar Not Showing

**Check:**
1. **IncludeAvatar enabled:** `IncludeAvatar = true`
2. **UseEmbed enabled:** Avatar only shows in embeds
3. **User has avatar:** User must have a Discord avatar set (default avatars work)

### Placeholders Not Replacing

**Check:**
1. **Correct syntax:** Use `{user}`, not `{{user}}` or `<user>`
2. **Spelling:** Supported placeholders are `{user}`, `{username}`, `{server}`, `{membercount}` only
3. **Case insensitive:** `{User}` and `{user}` both work

### Slash Commands Not Working

**Check:**
1. **Bot registered:** Slash commands registered with Discord (can take up to 1 hour globally, instant with `TestGuildId`)
2. **Permissions:** User has `Admin` role in guild
3. **Rate limit:** Maximum 5 commands per 60 seconds

### API Errors

**400 Bad Request:**
- Request body is null
- `PreviewUserId` is 0 or missing

**404 Not Found:**
- Guild doesn't exist in database (run `/sync` command or visit guild details page)
- Configuration doesn't exist (create via API or admin UI)

**401 Unauthorized:**
- Missing authentication (API requires `Admin` policy)

---

## Database Schema

### WelcomeConfiguration Table

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `GuildId` | `BIGINT` | No | - | Primary key, Discord snowflake ID |
| `IsEnabled` | `BIT` | No | `1` | Enable/disable toggle |
| `WelcomeChannelId` | `BIGINT` | Yes | `NULL` | Discord channel snowflake ID |
| `WelcomeMessage` | `NVARCHAR(2000)` | No | `''` | Message template |
| `IncludeAvatar` | `BIT` | No | `1` | Show user avatar in embed |
| `UseEmbed` | `BIT` | No | `1` | Send as embed vs plain text |
| `EmbedColor` | `NVARCHAR(7)` | Yes | `NULL` | Hex color code |
| `CreatedAt` | `DATETIME2` | No | `GETUTCDATE()` | Creation timestamp |
| `UpdatedAt` | `DATETIME2` | No | `GETUTCDATE()` | Last update timestamp |

**Foreign Keys:**
- `GuildId` → `Guilds.Id` (CASCADE on delete)

**Indexes:**
- Primary key on `GuildId`
- No additional indexes (small table, primary key lookups only)

**Migration:** `20251226004701_AddWelcomeConfiguration.cs`

---

## Security Considerations

### Authorization

**Slash Commands:**
- Require `[RequireAdmin]` precondition
- Enforced at Discord command level
- Users need guild `Administrator` permission or `Admin` role in bot database

**API Endpoints:**
- Require `[Authorize(Policy = "RequireAdmin")]` attribute
- Enforced at ASP.NET Core middleware level
- Users need `Admin` or `SuperAdmin` role in ASP.NET Identity

**Admin UI:**
- Require `[Authorize(Policy = "RequireAdmin")]` page attribute
- Guild-specific access checked via `GuildAccessRequirement`

### Input Validation

**Message Template:**
- Max length: 2000 characters (Discord limit)
- No HTML sanitization (Discord doesn't render HTML)
- No SQL injection risk (parameterized queries)

**Embed Color:**
- Regex validation: `^#[0-9A-Fa-f]{6}$`
- Prevents invalid color codes
- Defaults to Discord color if invalid

**Channel ID:**
- Type validation: `ulong` (Discord snowflake)
- Existence validation: Check channel exists in guild before saving
- Permission validation: Recommended to check bot has `SendMessages` permission

### Rate Limiting

**Slash Commands:**
- `[RateLimit(5, 60)]` precondition
- Max 5 commands per 60 seconds per user
- Prevents command spam

**API Endpoints:**
- No rate limiting currently implemented
- Recommended: Add rate limiting middleware for production

**Discord API:**
- Send message rate limit: 5 messages per 5 seconds per channel
- Bot generally won't hit this (one message per user join)

---

## Testing Strategy

### Unit Tests

**Service Tests:** `DiscordBot.Tests.Bot.Services.WelcomeServiceTests`
- Configuration retrieval
- Configuration creation and updates
- Template variable replacement
- Embed color parsing
- Error handling

**Repository Tests:** `DiscordBot.Tests.Infrastructure.Data.Repositories.WelcomeConfigurationRepositoryTests`
- CRUD operations
- Query by guild ID

**Controller Tests:** `DiscordBot.Tests.Controllers.WelcomeControllerTests`
- GET endpoint success/failure
- PUT endpoint validation
- Preview endpoint
- Error responses

**Handler Tests:** `DiscordBot.Tests.Handlers.WelcomeHandlerTests`
- Event processing
- Global setting check
- Service interaction

### Integration Tests

**Recommended Test Cases:**
1. Create configuration via API, verify in database
2. Update configuration via admin UI, verify in database
3. Enable/disable via slash command, verify state
4. Send test message via slash command, verify in Discord
5. Trigger actual join event, verify welcome sent

### Manual Testing Checklist

**Configuration:**
- [ ] Create new configuration via admin UI
- [ ] Update existing configuration via admin UI
- [ ] Enable/disable via toggle
- [ ] Select different channels
- [ ] Test all template variables
- [ ] Test embed color picker
- [ ] Test avatar toggle
- [ ] Test embed toggle

**Slash Commands:**
- [ ] `/welcome show` displays current config
- [ ] `/welcome enable` activates messages
- [ ] `/welcome disable` deactivates messages
- [ ] `/welcome channel` updates channel
- [ ] `/welcome message` updates template
- [ ] `/welcome test` sends test message

**Message Sending:**
- [ ] Join event triggers welcome message
- [ ] Message appears in correct channel
- [ ] Template variables replaced correctly
- [ ] Embed color applied correctly
- [ ] Avatar displayed correctly
- [ ] Plain text mode works
- [ ] Global disable prevents messages
- [ ] Per-guild disable prevents messages

---

## Performance Considerations

### Database Queries

**Per Join Event:**
- 1 query: Load `WelcomeConfiguration` by guild ID (indexed primary key)
- Low overhead: Single row lookup

**Optimization:**
- Consider caching configurations in memory (TTL: 5 minutes)
- Invalidate cache on updates
- Trade-off: Slightly stale data vs reduced DB load

### Discord API Calls

**Per Join Event:**
- 0 API calls: Guild, channel, user fetched from cache (`DiscordSocketClient`)
- 1 API call: Send message (`SendMessageAsync`)

**Rate Limits:**
- Message send: 5 per 5 seconds per channel
- Unlikely to hit limit (one message per join)
- Large guilds with many simultaneous joins may hit limit

### Event Processing

**Async Processing:**
- Join event handler is async, doesn't block Discord gateway
- Send failures logged but don't prevent other events

**Scalability:**
- Supports multiple guilds without performance degradation
- Each guild has independent configuration

---

## Future Enhancements

### Potential Features

**Advanced Templates:**
- Conditional logic (e.g., different message for first 10 members)
- More placeholders (account age, join date, role count)
- Multi-language support

**Message Scheduling:**
- Delay welcome message by X seconds/minutes
- Delete welcome message after X minutes (cleanup)

**Welcome Roles:**
- Automatically assign role(s) to new members
- Role configuration in welcome settings

**DM Welcome:**
- Option to send welcome via direct message instead of channel
- Privacy-friendly alternative

**Welcome Images:**
- Custom welcome banner/image generation
- User avatar composited into image

**Analytics:**
- Track welcome message delivery rate
- Monitor join/leave patterns
- Dashboard widget for join statistics

**A/B Testing:**
- Multiple welcome message templates
- Rotate or randomize per join

---

## Related Documentation

- [API Endpoints Reference](api-endpoints.md) - REST API documentation
- [Slash Commands](commands-page.md) - Command metadata and usage
- [Authorization Policies](authorization-policies.md) - Role hierarchy and policies
- [Form Implementation Standards](form-implementation-standards.md) - Razor Pages form patterns
- [Database Schema](database-schema.md) - Complete database documentation
- [Audit Logs](user-management.md#audit-logging) - Audit trail for configuration changes

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-30 | Initial documentation created for issue #313 |
