# Assistant Tool Catalog

## Overview

This catalog documents all tools available to the AI assistant for answering user questions, providing support, and retrieving bot state information. Tools are organized by category and prioritized for implementation.

The assistant uses these tools to provide contextual help, troubleshooting guidance, and information retrieval without requiring direct API access. All tools include security validations to prevent unauthorized access to sensitive data.

---

## Implementation Priority Matrix

| Priority | Category | Tool Count | Status | Phase |
|----------|----------|-----------|--------|-------|
| MVP | Documentation | 4 tools | Planned | Phase 1 |
| MVP | User & Guild Information | 2 tools | Planned | Phase 1 |
| Phase 2 | Guild Members | 1 tool | Planned | Phase 2 |
| Phase 2 | Permissions & Access | 2 tools | Planned | Phase 2 |
| Phase 3 | Bot Configuration | 3 tools | Planned | Phase 3 |
| Phase 3 | Moderation & Logging | 3 tools | Planned | Phase 3 |
| Phase 3 | Bot Diagnostics | 2 tools | Planned | Phase 3 |

**Total Tools Planned**: 17 across all phases

---

## MVP Tools - Phase 1

### Documentation Tools

**Provider**: `DocumentationToolProvider`

Documentation tools provide feature information, command discovery, and help content without requiring database access.

#### get_feature_documentation

Retrieves comprehensive markdown documentation for a specific bot feature.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| feature_name | string | Yes | Feature name (e.g., "soundboard", "rat-watch", "tts-support", "reminder-system") |

**Returns**:

- Feature markdown content
- Sections: overview, configuration, usage, examples
- Status and availability notes

**Example Request**:
```json
{
  "feature_name": "soundboard"
}
```

**Example Response**:
```json
{
  "feature": "soundboard",
  "content": "# Soundboard Feature\n\n## Overview\nThe soundboard...",
  "available": true,
  "last_updated": "2026-01-15T00:00:00Z"
}
```

**Use Cases**:
- User asks about specific features: "How does the soundboard work?"
- Feature overview requests: "Tell me about Rat Watch"
- Configuration help: "How do I set up text-to-speech?"

**Security**: Returns public documentation only. No authentication required.

---

#### search_commands

Searches available slash commands by keyword and returns matching results with descriptions and usage examples.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| query | string | Yes | Search keyword (e.g., "moderation", "sound", "remind") |
| limit | integer | No | Maximum results to return (default: 10, max: 50) |
| module | string | No | Filter by command module (e.g., "ModerationModule", "ReminderModule") |

**Returns**:

- List of matching commands (limited by `limit` parameter)
- Command name, description, module
- Parameter count and examples
- Preconditions (e.g., "Requires Moderator role")

**Example Request**:
```json
{
  "query": "moderation",
  "limit": 10
}
```

**Example Response**:
```json
{
  "results": [
    {
      "name": "warn",
      "description": "Issue a warning to a user",
      "module": "ModerationActionModule",
      "parameters": 2,
      "preconditions": ["RequireModerator"]
    },
    {
      "name": "ban",
      "description": "Ban a user from the guild",
      "module": "ModerationActionModule",
      "parameters": 2,
      "preconditions": ["RequireAdminAttribute", "RequireGuildActive"]
    }
  ],
  "total_matches": 15,
  "limited_to": 10
}
```

**Use Cases**:
- User asks "What moderation commands are available?"
- Feature discovery: "Show me reminder commands"
- Help queries: "What can I do with soundboard?"

**Security**: Returns public command metadata only.

---

#### get_command_details

Retrieves detailed information about a specific command including parameters, options, examples, and preconditions.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| command_name | string | Yes | Command name without slash (e.g., "remind", "play", "ban") |

**Returns**:

- Command name, description, module
- All parameters with types and descriptions
- Optional parameters and defaults
- Preconditions and permission requirements
- Usage examples
- Related commands

**Example Request**:
```json
{
  "command_name": "remind"
}
```

**Example Response**:
```json
{
  "name": "remind",
  "description": "Set a personal reminder",
  "module": "ReminderModule",
  "parameters": [
    {
      "name": "time",
      "type": "string",
      "required": true,
      "description": "When to remind (natural language: '5 minutes', 'tomorrow at 3pm', 'in 2 days')"
    },
    {
      "name": "message",
      "type": "string",
      "required": true,
      "description": "Reminder message"
    }
  ],
  "preconditions": [],
  "examples": [
    "/remind time:5 minutes message:Check on task",
    "/remind time:tomorrow at 3pm message:Team meeting"
  ],
  "related_commands": ["remind list", "remind delete"]
}
```

**Use Cases**:
- User asks "How do I use the /remind command?"
- Parameter help: "What options does /play have?"
- Syntax clarification: "What's the format for the ban command?"

**Security**: Returns public command metadata. No user-specific data returned.

---

#### list_features

Lists all available bot features with brief descriptions. No parameters required.

**Returns**:

- Feature name and category
- Brief description (1-2 sentences)
- Enabled status
- Availability notes (if any)
- Documentation link

**Example Response**:
```json
{
  "features": [
    {
      "name": "Rat Watch",
      "category": "Accountability",
      "description": "Community accountability system with voting and leaderboards.",
      "enabled": true,
      "availability": "All guilds"
    },
    {
      "name": "Soundboard",
      "category": "Audio",
      "description": "Upload and play audio clips in voice channels.",
      "enabled": true,
      "availability": "Requires audio configuration"
    },
    {
      "name": "Text-to-Speech",
      "category": "Audio",
      "description": "Send text-to-speech messages to voice channels.",
      "enabled": true,
      "availability": "Requires Azure Cognitive Services setup"
    },
    {
      "name": "Reminders",
      "category": "Productivity",
      "description": "Personal reminders with natural language time parsing.",
      "enabled": true,
      "availability": "All users"
    }
  ],
  "total_count": 15
}
```

**Use Cases**:
- User asks "What can this bot do?"
- Feature overview: "What features are available?"
- Discovery: "Show me all productivity features"

**Security**: Returns feature metadata only.

---

### User & Guild Information Tools

**Provider**: `UserGuildInfoToolProvider`

These tools retrieve public user and guild information with permission-based access control.

#### get_user_profile

Retrieves basic user information and optionally includes roles within a specific guild context.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| user_id | string | Yes | Discord user ID (snowflake) |
| guild_id | string | No | Guild context for role lookup |
| include_roles | boolean | No | Include roles in guild (default: false) |

**Returns**:

- Username and discriminator (if available)
- Avatar URL
- Account creation date
- Roles in guild (if `guild_id` and `include_roles` provided)
- Last activity timestamp (if available)
- Bot status (if user is a bot)

**Example Request**:
```json
{
  "user_id": "123456789012345678",
  "guild_id": "987654321098765432",
  "include_roles": true
}
```

**Example Response**:
```json
{
  "user_id": "123456789012345678",
  "username": "ExampleUser",
  "discriminator": "0",
  "avatar_url": "https://cdn.discordapp.com/avatars/...",
  "created_at": "2021-03-15T10:30:00Z",
  "last_activity": "2026-01-15T14:22:00Z",
  "is_bot": false,
  "roles": [
    {
      "name": "Member",
      "id": "111111111111111111",
      "position": 1
    }
  ]
}
```

**Use Cases**:
- Permission troubleshooting: "Why can't user X access feature Y?"
- User identification: "Who is this user?"
- Role verification: "What roles does this user have?"

**Security**:
- Regular users can only view public information
- Users can view their own profile without restriction
- Moderators and Admins can view any user's profile
- Sensitive data (email, phone, IP) never returned

**Permission Levels**:
- Regular User: Own profile only
- Moderator: Any user in guild where moderator
- Admin: Any user in bot's presence
- SuperAdmin: Unrestricted

---

#### get_guild_info

Retrieves basic guild/server information.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID (snowflake) |

**Returns**:

- Guild name and icon URL
- Creation date
- Member count (current)
- Owner information (name and ID)
- Bot status in guild (is connected, join date)

**Example Request**:
```json
{
  "guild_id": "987654321098765432"
}
```

**Example Response**:
```json
{
  "guild_id": "987654321098765432",
  "name": "Example Server",
  "icon_url": "https://cdn.discordapp.com/icons/...",
  "created_at": "2020-01-10T08:15:00Z",
  "member_count": 2847,
  "owner": {
    "id": "111111111111111111",
    "username": "ServerOwner"
  },
  "bot_connected": true,
  "bot_joined_at": "2023-06-20T12:45:00Z"
}
```

**Use Cases**:
- Guild overview: "Tell me about this server"
- Status checks: "Is the bot in this guild?"
- Guild information: "When was this server created?"

**Security**:
- Returns only public guild information
- Requires bot to be in guild (or user has guild access)
- No sensitive configuration returned at this level

---

## Phase 2 Tools

### Guild Member Information

**Provider**: `UserGuildInfoToolProvider`

#### get_user_roles

Gets all roles for a specific user within a guild, including role hierarchy and special properties.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| user_id | string | Yes | Discord user ID |
| guild_id | string | Yes | Discord guild ID |

**Returns**:

- List of role objects
- Role name, ID, color, hierarchy position
- Special role flags (is managed, is bot role, is mentionable)
- Permissions summary

**Example Request**:
```json
{
  "user_id": "123456789012345678",
  "guild_id": "987654321098765432"
}
```

**Example Response**:
```json
{
  "user_id": "123456789012345678",
  "guild_id": "987654321098765432",
  "roles": [
    {
      "name": "@everyone",
      "id": "987654321098765432",
      "position": 0,
      "color": "#000000",
      "is_managed": true,
      "is_mentionable": false
    },
    {
      "name": "Moderator",
      "id": "111111111111111111",
      "position": 5,
      "color": "#FF6B6B",
      "is_managed": false,
      "is_mentionable": true
    }
  ],
  "highest_role_position": 5
}
```

**Use Cases**:
- Permission debugging: "What roles does this user have?"
- Access level verification: "Is this user a moderator?"
- Role inquiry: "Can I assign this role?"

**Security**:
- Requires guild membership to query
- Regular users can only view their own roles
- Moderators can view guild members they have access to
- Admins can view any member's roles

**Permission Levels**:
- Regular User: Own roles only
- Moderator: Guild members
- Admin: Any member in guild where admin

---

### List Guild Members

**Provider**: `UserGuildInfoToolProvider`

#### list_guild_members

Lists guild members with filtering, searching, and pagination capabilities.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| filter | string | No | Filter type: "all", "admins", "mods", "bots", "humans" (default: "all") |
| search | string | No | Search by username or username fragment |
| limit | integer | No | Number of results (default: 10, max: 100) |
| offset | integer | No | Pagination offset (default: 0) |

**Returns**:

- Paginated list of members
- Username, user ID, join date
- Roles in guild
- Bot status
- Total member count

**Example Request**:
```json
{
  "guild_id": "987654321098765432",
  "filter": "mods",
  "limit": 20
}
```

**Example Response**:
```json
{
  "guild_id": "987654321098765432",
  "members": [
    {
      "user_id": "111111111111111111",
      "username": "ModUser1",
      "joined_at": "2023-01-15T10:30:00Z",
      "is_bot": false,
      "roles": ["Moderator", "Member"]
    }
  ],
  "total_members": 2847,
  "filtered_count": 12,
  "returned_count": 20,
  "offset": 0
}
```

**Use Cases**:
- Member lookup: "Find user X in this guild"
- Moderation queries: "List all moderators"
- Member discovery: "Show me all bots in the guild"

**Security**:
- Requires guild membership
- Filters output based on user permissions
- Never returns banned members unless querying mod logs
- Regular users can only search without admin/mod filters

---

## Phase 2 Tools (continued)

### Permissions & Access Control

**Provider**: `PermissionToolProvider`

#### check_user_permissions

Checks what actions a user can perform in a specific context, with optional verification of specific actions.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| user_id | string | Yes | Discord user ID |
| guild_id | string | Yes | Discord guild ID |
| action | string | No | Specific action to check (e.g., "ban", "kick", "manage_roles", "use_soundboard", "use_tts") |

**Returns**:

- List of permissions user has
- If `action` specified: whether action is allowed and reason if denied
- Permission source (role, guild membership, admin status)
- Related permissions

**Example Request**:
```json
{
  "user_id": "123456789012345678",
  "guild_id": "987654321098765432",
  "action": "ban"
}
```

**Example Response**:
```json
{
  "user_id": "123456789012345678",
  "guild_id": "987654321098765432",
  "permissions": [
    "view_commands",
    "use_reminders",
    "use_soundboard",
    "view_member_directory"
  ],
  "action_check": {
    "action": "ban",
    "allowed": false,
    "reason": "Requires Moderator role",
    "required_roles": ["Moderator", "Admin"],
    "current_roles": ["Member"]
  }
}
```

**Use Cases**:
- Permission verification: "Can user X ban members?"
- Troubleshooting: "Why can't this user access the soundboard?"
- Access explanation: "What permissions does this user have?"

**Security**:
- Validates that requesting user has permission to check
- Never returns sensitive permission combinations
- Requires guild context
- Users can check their own permissions without restriction

---

#### check_oauth_status

Checks OAuth integration status and account linking status for a user.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| user_id | string | Yes | Discord user ID |
| guild_id | string | No | Guild context (optional, for role mapping info) |

**Returns**:

- OAuth linked status (yes/no)
- Account linking date
- Auto-role assignment status
- Roles applied via OAuth (if applicable)
- Last sync date
- Any pending OAuth actions

**Example Request**:
```json
{
  "user_id": "123456789012345678",
  "guild_id": "987654321098765432"
}
```

**Example Response**:
```json
{
  "user_id": "123456789012345678",
  "oauth_linked": true,
  "linked_at": "2025-06-10T14:22:00Z",
  "auto_role_assignment": true,
  "roles_from_oauth": [
    "Verified User",
    "Active Member"
  ],
  "last_sync": "2026-01-15T10:15:00Z",
  "sync_status": "success"
}
```

**Use Cases**:
- OAuth troubleshooting: "Is user X linked to OAuth?"
- Role assignment verification: "Why didn't auto-role assignment work?"
- Account linking help: "How do I link my account?"

**Security**:
- Returns only status information, no credentials
- Users can check their own status without restriction
- Admins can check any user's status
- Never returns OAuth tokens or sensitive secrets

---

## Phase 3 Tools

### Bot Configuration Tools

**Provider**: `BotConfigToolProvider`

**Status**: Deferred to Phase 3 - Post-MVP implementation

#### get_bot_config

Retrieves guild-specific bot configuration for all features or a specific section.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| section | string | No | Config section: "all", "moderation", "soundboard", "tts", "oauth", "reminders" (default: "all") |

**Returns**:

- Moderation settings (enabled, log channel, mute role)
- Soundboard configuration (enabled, file size limits, supported formats)
- Text-to-speech settings (enabled, language, voice options)
- OAuth settings (enabled, role mapping)
- Reminder settings (enabled, limits)

**Use Cases**: Config troubleshooting, feature status checks, settings inquiries

---

#### get_soundboard_sounds

Lists available soundboard sounds with filtering and search capabilities.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| search | string | No | Search by sound name |
| limit | integer | No | Max results (default: 20, max: 100) |

**Returns**:

- Sound name and ID
- Duration, file size
- Creator/uploader
- Upload date, play count
- Permission level (who can play)

**Use Cases**: Sound discovery, sound inventory checks, audio library exploration

---

#### get_text_to_speech_config

Retrieves TTS configuration and available voices/languages.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |

**Returns**:

- TTS enabled status
- Available voices and languages
- Default voice per user (if set)
- Channel-specific TTS settings
- Character/message limits

**Use Cases**: TTS feature inquiry, voice selection help, troubleshooting audio issues

---

### Moderation & Logging Tools

**Provider**: `ModerationToolProvider`

**Status**: Deferred to Phase 3

**Security Note**: All moderation tools require Moderator+ role

#### get_moderation_log

Retrieves moderation actions with filtering and pagination.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| user_id | string | No | Filter by target user |
| action_type | string | No | Filter by action (warn, mute, kick, ban, etc.) |
| limit | integer | No | Results per page (default: 10, max: 50) |
| days_back | integer | No | Search last N days (default: 30) |

**Returns**:

- Timestamp, action type
- Target user and moderator
- Reason, duration (if applicable)
- Appeal status (if applicable)

**Use Cases**: User history review, moderation accountability, appeal context

**Security**: Requires Moderator role. Users can view own moderation history.

---

#### get_muted_users

Lists currently muted users with remaining duration.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| include_duration | boolean | No | Show remaining mute time (default: true) |

**Returns**:

- Muted users list
- Mute start time, remaining duration
- Reason for mute
- Moderator who issued mute

**Use Cases**: Active mute overview, mute status queries

**Security**: Requires Moderator role.

---

#### get_audit_log

Retrieves bot audit trail with event filtering.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| event_type | string | No | Filter by type (config_change, soundboard_upload, user_action, etc.) |
| limit | integer | No | Results (default: 20, max: 100) |
| days_back | integer | No | Search last N days (default: 7) |

**Returns**:

- Timestamp, event type
- Actor (who performed action)
- Action details
- Change summary (for config changes)

**Use Cases**: Debugging config issues, tracking changes, audit trail review

**Security**: Requires Admin role. SuperAdmin has unrestricted access.

---

### Bot Status & Diagnostics Tools

**Provider**: `DiagnosticsToolProvider`

**Status**: Deferred to Phase 3

#### get_bot_health

Checks bot operational status and health metrics.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |

**Returns**:

- Bot connection status
- Response time / latency
- Database connectivity
- Feature availability (moderation, soundboard, TTS, OAuth, etc.)
- Error count (last hour)
- Last successful health check

**Use Cases**: Troubleshooting bot issues, diagnosing connectivity problems

---

#### get_feature_status

Checks status of specific bot features.

**Parameters**:

| Name | Type | Required | Description |
|------|------|----------|-------------|
| guild_id | string | Yes | Discord guild ID |
| feature | string | No | Specific feature (moderation, soundboard, tts, oauth) - all if not specified |

**Returns**:

- Feature enabled/disabled status
- Last error (if any)
- Performance metrics
- Configuration status
- Known issues (if applicable)

**Use Cases**: Feature troubleshooting, status inquiries, diagnostic queries

---

## Tool Provider Implementation Map

| Provider Class | Tools | MVP Phase | Total Tools |
|----------------|-------|-----------|-------------|
| `DocumentationToolProvider` | get_feature_documentation, search_commands, get_command_details, list_features | Phase 1 | 4 |
| `UserGuildInfoToolProvider` | get_user_profile, get_guild_info, get_user_roles, list_guild_members | Phase 1-2 | 4 |
| `PermissionToolProvider` | check_user_permissions, check_oauth_status | Phase 2 | 2 |
| `BotConfigToolProvider` | get_bot_config, get_soundboard_sounds, get_text_to_speech_config | Phase 3 | 3 |
| `ModerationToolProvider` | get_moderation_log, get_muted_users, get_audit_log | Phase 3 | 3 |
| `DiagnosticsToolProvider` | get_bot_health, get_feature_status | Phase 3 | 2 |

---

## Security & Data Handling

### Permission Validation Strategy

All tools validate permissions based on user role hierarchy:

| Role | Access Level | Tools Available |
|------|--------------|-----------------|
| Regular User | Basic | Documentation, own profile, guild info, feature status (basic) |
| Moderator | Enhanced | All basic + moderation logs, muted users, member directory |
| Admin | Full | All + configuration, audit logs, diagnostics |
| SuperAdmin | Unrestricted | All tools, all data |

### Data Sanitization Rules

**Never return**:
- Password hashes or authentication tokens
- Private user data (email addresses, phone numbers, IP addresses)
- API keys or webhook URLs
- Unencrypted OAuth credentials
- Internal system identifiers (not related to Discord)
- Raw database IDs (except Discord snowflakes)

**Always sanitize**:
- Replace email with masked version: `admin@ex****e.com`
- Truncate sensitive fields in error messages
- Log access to sensitive tool invocations
- Filter output based on requesting user's permissions

### Rate Limiting

Tool execution is rate-limited per user and guild:

| User Role | Calls Per Minute | Calls Per Hour |
|-----------|-----------------|-----------------|
| Regular User | 10 | 100 |
| Moderator | 20 | 200 |
| Admin | 50 | 500 |
| SuperAdmin | Unlimited | Unlimited |

**Configuration**: Via `AssistantOptions.ToolRateLimits`

### Audit Logging

All tool executions are logged:
- Tool name and execution timestamp
- User ID and guild ID
- Parameters (sanitized)
- Execution time (milliseconds)
- Success/failure status
- Error details (if failed)

Audit logs are retained for 90 days per GDPR requirements.

---

## Tool Execution Context

Tools receive execution context for validation and logging:

```csharp
public class ToolExecutionContext
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public UserRole UserRole { get; set; }  // Calculated from Discord roles
    public string BaseUrl { get; set; }     // For URL generation
    public DateTime ExecutedAt { get; set; }
}

public enum UserRole
{
    Unknown = 0,
    User = 1,
    Moderator = 2,
    Admin = 3,
    SuperAdmin = 4
}
```

---

## Adding New Tools

**Process for implementing new tools**:

1. Define tool in appropriate category's static `*Tools.cs` class
   - Document parameters, returns, use cases
   - Include security considerations
   - Add example request/response

2. Implement execution in the corresponding `*ToolProvider` class
   - Validate user permissions upfront
   - Sanitize all returned data
   - Handle errors gracefully
   - Log execution details

3. Register in dependency injection (Startup.cs)
   - Add tool provider to service collection
   - Wire up configuration options

4. Update this catalog with tool documentation
   - Add to appropriate section
   - Update priority matrix
   - Add security notes

5. Implement comprehensive unit tests
   - Test permission validation
   - Test error handling
   - Test data sanitization
   - Test rate limiting

6. Document in API specification if exposed via REST endpoints

---

## Tool Discovery & Help

### Discovering Available Tools

Users can request tool discovery:
- "What can you help me with?"
- "What information can you access?"
- "What tools do you have available?"

Assistant responds with relevant tools based on user's role and context.

### Tool Help & Examples

For each tool, the assistant provides:
- What the tool does
- When to use it
- Expected results
- Example invocations
- Limitations

Example:
> The `get_user_profile` tool retrieves basic user information. I can use this to answer questions about a user's account age, avatar, and roles in the guild. For example, I could answer "When did user X join Discord?" or "What roles does user Y have?"

---

## Future Considerations

### Planned Enhancements

- **Tool chaining**: Execute related tools in sequence to answer complex queries
- **Caching**: Cache frequently accessed data (guild config, feature status) for performance
- **Webhooks**: Real-time notifications for tool-relevant events
- **Batch operations**: Execute multiple tools in parallel
- **Custom filters**: User-defined tool behavior and output filtering

### Post-MVP Roadmap

- Integration with command analytics tools
- Trend analysis (activity patterns, feature usage)
- Predictive diagnostics (detect issues before they occur)
- Custom tool creation for guild-specific workflows
- Tool execution API for external integrations

---

## References

- [llm-abstraction-architecture.md](llm-abstraction-architecture.md) - LLM provider abstraction and tool system architecture
- [authorization-policies.md](../articles/authorization-policies.md) - Role hierarchy and permission system
- [api-endpoints.md](../articles/api-endpoints.md) - REST API for tool data sources
