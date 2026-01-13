# Requirements: AI Assistant Agent System

## Executive Summary

Add an AI-powered assistant feature to the Discord bot that responds to user mentions with helpful information about bot features, commands, and usage. Users can ask questions naturally (e.g., `@DiskordBott what's the URL for the soundboard?`) and receive conversational responses powered by Claude API.

## Problem Statement

Discord server members need an easy way to learn about the bot's features and get help with commands without leaving Discord or reading through documentation. Currently, users must search external docs or ask server admins for help.

## Primary Purpose

Provide Discord users with instant, conversational help about bot features, commands, and usage through natural language questions asked by mentioning the bot.

## Target Users

- **Discord Server Members**: Regular users in guilds where the bot is installed who need help understanding bot features
- **Guild Administrators**: Server admins who want to enable/disable the assistant and configure rate limits

## Core Features (MVP)

### 1. Message Detection & Response
- Listen for messages where the bot is mentioned (`@DiskordBott`)
- Extract user question from the message
- Send question to Claude API with assistant agent prompt + tool definitions
- **Tool-based documentation access**: Claude can call tools to fetch documentation dynamically
  - Reduces prompt size (don't embed all docs)
  - Always provides fresh documentation
  - More cost-effective (only sends relevant docs)
- Post Claude's response as a reply in the same channel
- **Stateless**: Each mention is independent, no conversation history

### 1a. Documentation Tools
Define tools that Claude can invoke to retrieve information:
- `get_feature_documentation(feature_name)` - Returns markdown documentation for a specific feature
- `search_commands(query)` - Searches available slash commands by keyword
- `get_command_details(command_name)` - Returns detailed info about a specific command
- `list_features()` - Returns list of all available bot features

Tool implementations will:
- Read from `docs/articles/` markdown files
- Parse README.md for command lists
- Return structured data that Claude can use to formulate answers

### 2. Channel Configuration
- **Default**: Responds in any channel where mentioned
- **Optional**: Guild admins can configure specific channels where assistant is active (restrict to #bot-help, etc.)

### 3. Rate Limiting
- **Per-user limits**: Prevent spam and control costs
- **Global default**: System-wide rate limit (e.g., 5 questions per user per hour)
- **Guild override**: Admins can customize rate limit for their guild
- **User feedback**: When rate limit hit, send ephemeral message: "You've reached your limit, try again in X minutes"

### 4. Enable/Disable Per Guild
- Guild administrators can enable or disable the assistant feature for their guild
- Disabled by default or enabled by default (to be decided during implementation)

### 5. Usage Metrics (Basic)
- Track aggregated stats per guild:
  - Total questions asked
  - Average tokens per response
- Track aggregated stats per user:
  - Questions asked (for rate limiting enforcement)
  - Timestamp of last question

### 6. Error Handling
- API failures/timeouts: Send friendly error to user ("Oops, I'm having a brain fart, try again later")
- Always log errors to existing logging system (Serilog)
- Log all API interactions for debugging and cost monitoring

### 7. Configuration Storage
- Store assistant settings in database using existing patterns:
  - Guild-level: Enabled/disabled, allowed channels, rate limit override
  - Global: Default rate limit, Claude API key
- Configuration via admin UI (add to existing guild settings page)

## Future Features (Out of Scope for MVP)

- Conversation history/context (multi-turn conversations)
- Dedicated analytics dashboard showing usage trends, costs, popular questions
- Per-guild custom prompts or personality customization
- Slash command interface (e.g., `/ask <question>`)
- Thread-based conversations
- Response caching for common questions
- Custom responses or FAQ overrides per guild

## Out of Scope

- Answering questions unrelated to the bot (no general chatbot functionality)
- Executing commands on behalf of users
- Accessing or revealing private user data
- Multi-language support (English only for MVP)

## Tech Stack

### LLM Provider
- **Claude API** via official Anthropic .NET SDK (`Anthropic.SDK` NuGet package)
- Model: Claude 3.5 Sonnet (or latest recommended model)
- Agent prompt: Load from `docs/agents/assistant-agent.md` with placeholder substitution
- **Tool Use/Function Calling**: Claude invokes defined tools to fetch documentation on-demand

### Backend
- **.NET 8** (existing bot infrastructure)
- **Discord.NET** for message event handling
- **Anthropic.SDK** for Claude API calls
- **Entity Framework Core** for database storage (existing pattern)
- **Serilog** for logging (existing system)

### Database
- New tables:
  - `AssistantGuildSettings` (guild configuration)
  - `AssistantUsageMetrics` (aggregated stats per guild)
  - `AssistantUserRateLimits` (rate limit tracking per user)

### Configuration
- User Secrets for Claude API key
- Options pattern: `AssistantOptions` in `appsettings.json`

## Design Preferences

### User Experience
- Responses should feel natural and conversational
- Keep responses concise (Discord users prefer brief answers)
- Use Discord markdown for formatting (*italic*, **bold**, `code`)
- Minimal emoji usage (only for clarity)
- Friendly error messages, not technical jargon

### Code Architecture
- Follow existing three-layer architecture (Core → Infrastructure → Bot)
- Service interface: `IAssistantService` in Core
- Implementation: `AssistantService` in Infrastructure
- Message handler in Bot project (Discord.NET event handler)
- Repository pattern for data access

### Tool Architecture
- **Tool Service**: `IDocumentationToolService` provides tool implementations
- **Tool Handlers**: Each tool is a method that returns documentation data
- **Tool Execution Flow**:
  1. User mentions bot with question
  2. Claude receives question + tool definitions
  3. Claude decides which tool(s) to call
  4. Bot executes tool, returns data to Claude
  5. Claude formulates answer using tool data
  6. Bot posts final response to Discord
- **Tool Data Sources**:
  - `docs/articles/*.md` files for feature documentation
  - `README.md` for command lists and overview
  - In-memory command metadata from Discord.NET registration
  - Optionally: Live guild settings from database

### Security
- Agent prompt includes extensive security guidelines (prompt injection defense, jailbreak prevention, etc.)
- Never expose API keys, internal implementation, or sensitive data
- Log all interactions for security audit trail
- Rate limiting to prevent abuse

## Constraints

### Performance
- Response time: Target < 10 seconds (depends on Claude API)
- Show typing indicator while waiting for response
- No impact on other bot features during API calls (async/await)

### Cost
- Claude API costs per token (input + output)
- Rate limiting is critical for cost control
- Basic metrics to monitor spending

### Security
- API key must be stored in User Secrets (never committed)
- Agent prompt must enforce security boundaries
- All errors logged but not exposed to users

### Scale
- Expected usage: Low to moderate (personal project, limited guilds)
- Rate limiting prevents runaway costs
- No special infrastructure needed (existing bot handles it)

## Configuration Options

### Guild Settings (Database)
```csharp
public class AssistantGuildSettings
{
    public ulong GuildId { get; set; }
    public bool IsEnabled { get; set; }
    public List<ulong> AllowedChannelIds { get; set; } // Empty = all channels
    public int? RateLimitOverride { get; set; } // Null = use global default
}
```

### Global Settings (appsettings.json + User Secrets)

See `src/DiscordBot.Core/Configuration/AssistantOptions.cs` for the complete configuration schema.

```json
{
  "Assistant": {
    "GloballyEnabled": false,
    "EnabledByDefaultForNewGuilds": false,
    "DefaultRateLimit": 5,
    "RateLimitWindowMinutes": 5,
    "RateLimitBypassRole": "Admin",
    "MaxQuestionLength": 500,
    "MaxResponseLength": 1800,
    "TruncationSuffix": "\n\n... *(response truncated)*",
    "Model": "claude-3-5-sonnet-20241022",
    "ApiTimeoutMs": 30000,
    "MaxTokens": 1024,
    "Temperature": 0.7,
    "AgentPromptPath": "docs/agents/assistant-agent.md",
    "DocumentationBasePath": "docs/articles",
    "ReadmePath": "README.md",
    "EnableDocumentationTools": true,
    "MaxToolCallsPerQuestion": 5,
    "ToolExecutionTimeoutMs": 5000,
    "ErrorMessage": "Oops, I'm having trouble thinking right now. Please try again in a moment.",
    "MaxRetryAttempts": 2,
    "RetryDelayMs": 1000,
    "EnableCostTracking": true,
    "DailyCostThresholdUsd": 5.00,
    "CostPerMillionInputTokens": 3.00,
    "CostPerMillionOutputTokens": 15.00,
    "EnablePromptCaching": true,
    "CacheCommonDocumentation": true,
    "CachedDocumentationFiles": ["commands-page.md", "soundboard.md", "rat-watch.md", "tts-support.md"],
    "CostPerMillionCachedTokens": 0.30,
    "CostPerMillionCacheWriteTokens": 3.75,
    "RequireExplicitConsent": true,
    "LogInteractions": true,
    "InteractionLogRetentionDays": 90,
    "BaseUrl": null,
    "ShowTypingIndicator": true,
    "IncludeGuildContext": true
  },
  "Claude": {
    "ApiKey": "sk-ant-..." // User Secrets only
  }
}
```

### Configuration Setting Details

| Category | Setting | Purpose | Default Value |
|----------|---------|---------|---------------|
| **Feature Flags** | `GloballyEnabled` | Master switch for assistant feature | `false` |
| | `EnabledByDefaultForNewGuilds` | Auto-enable for new guilds | `false` |
| | `ShowTypingIndicator` | Show typing while waiting | `true` |
| | `IncludeGuildContext` | Pass guild ID to Claude | `true` |
| **Rate Limiting** | `DefaultRateLimit` | Max questions per user per window | `5` |
| | `RateLimitWindowMinutes` | Rate limit time window | `5` minutes |
| | `RateLimitBypassRole` | Minimum role to bypass limits | `"Admin"` (Admin and SuperAdmin bypass) |
| **Message Constraints** | `MaxQuestionLength` | Max chars in user question | `500` |
| | `MaxResponseLength` | Max chars in Claude response | `1800` |
| | `TruncationSuffix` | Text appended when truncated | `"\n\n... *(response truncated)*"` |
| **Claude API** | `Model` | Claude model identifier | `"claude-3-5-sonnet-20241022"` |
| | `ApiTimeoutMs` | API call timeout | `30000` (30s) |
| | `MaxTokens` | Max response tokens | `1024` |
| | `Temperature` | Response creativity (0.0-1.0) | `0.7` |
| **Paths** | `AgentPromptPath` | Agent behavior prompt file | `"docs/agents/assistant-agent.md"` |
| | `DocumentationBasePath` | Root dir for feature docs | `"docs/articles"` |
| | `ReadmePath` | Path to README for commands | `"README.md"` |
| | `BaseUrl` | Base URL for link generation | `null` (uses `Application.BaseUrl`) |
| **Tools** | `EnableDocumentationTools` | Enable Claude tool access | `true` |
| | `MaxToolCallsPerQuestion` | Max tool calls per question | `5` |
| | `ToolExecutionTimeoutMs` | Tool execution timeout | `5000` (5s) |
| **Error Handling** | `ErrorMessage` | User-facing error message | `"Oops, I'm having trouble..."` |
| | `MaxRetryAttempts` | API retry attempts | `2` |
| | `RetryDelayMs` | Delay between retries | `1000` (1s) |
| **Cost Monitoring** | `EnableCostTracking` | Track token usage | `true` |
| | `DailyCostThresholdUsd` | Alert threshold (USD/day) | `5.00` |
| | `CostPerMillionInputTokens` | Input token cost (USD) | `3.00` |
| | `CostPerMillionOutputTokens` | Output token cost (USD) | `15.00` |
| **Prompt Caching** | `EnablePromptCaching` | Use Claude prompt caching | `true` |
| | `CacheCommonDocumentation` | Pre-cache common docs | `true` |
| | `CachedDocumentationFiles` | Docs to include in cache | `["commands-page.md", "soundboard.md", ...]` |
| | `CostPerMillionCachedTokens` | Cached token cost (USD) | `0.30` (90% discount) |
| | `CostPerMillionCacheWriteTokens` | Cache write cost (USD) | `3.75` (on cache miss) |
| **Privacy/Audit** | `RequireExplicitConsent` | Require /consent opt-in | `true` |
| | `LogInteractions` | Log questions/responses | `true` |
| | `InteractionLogRetentionDays` | Days to retain logs | `90` |

### Prompt Caching (Cost Optimization)

**Overview:**
Claude's Prompt Caching feature caches static content (agent prompt + common docs) for 5 minutes, reducing costs by ~50% and improving latency.

**How it works:**
- Agent prompt and common docs are marked with `cache_control: ephemeral`
- First request (cache miss): Pay cache write cost ($3.75/M tokens)
- Subsequent requests within 5 minutes (cache hit): Pay cached token cost ($0.30/M instead of $3.00/M)
- Cache is shared globally across all users and guilds

**Cost comparison (per question):**

| Scenario | Agent Prompt | User Question | Output | Total |
|----------|--------------|---------------|--------|-------|
| Without caching | ~1500 tokens × $3/M = $0.0045 | ~100 tokens × $3/M = $0.0003 | ~200 tokens × $15/M = $0.003 | **$0.0078** |
| With caching (hit) | ~1500 tokens × $0.30/M = $0.00045 | ~100 tokens × $3/M = $0.0003 | ~200 tokens × $15/M = $0.003 | **$0.00375** |
| **Savings** | | | | **~52% cost reduction** |

**Volume savings:**
- 100 questions/day without caching: $0.78/day (~$23.40/month)
- 100 questions/day with caching: $0.38/day (~$11.40/month)
- **Savings: ~$12/month**

**Cached content:**
1. **Agent system prompt** (~1500 tokens) - behavior, security, response format
2. **Common documentation** (configurable via `CachedDocumentationFiles`):
   - `commands-page.md` - Command overview
   - `soundboard.md` - Soundboard feature (most requested)
   - `rat-watch.md` - Rat Watch feature
   - `tts-support.md` - Text-to-Speech feature

**Guild-specific context:**
- `{BASE_URL}` and `{GUILD_ID}` are NOT cached in system prompt (would break cache sharing)
- Instead, passed to tools via tool context at runtime
- Tools generate URLs using guild ID from context: `{BASE_URL}/Portal/Soundboard/{GUILD_ID}`

**Configuration:**
```json
{
  "EnablePromptCaching": true,              // Master switch
  "CacheCommonDocumentation": true,         // Include common docs in cache
  "CachedDocumentationFiles": [...]         // Which docs to cache
}
```

**Metrics tracked:**
- Cache hit rate
- Cached tokens vs. regular tokens
- Cache write costs vs. savings

### URL Generation

The `BaseUrl` setting (or `Application.BaseUrl` if not set) is used to generate guild-specific URLs in responses:
- Soundboard: `{BASE_URL}/Portal/Soundboard/{GUILD_ID}`
- TTS Portal: `{BASE_URL}/Portal/TTS/{GUILD_ID}`
- Public Leaderboard: `{BASE_URL}/Guilds/{GUILD_ID}/Leaderboard`

Base URL and guild ID are passed to tools at runtime (not embedded in cached system prompt to maintain cache sharing across guilds).

### Documentation Tool Paths

The documentation tools use `DocumentationBasePath` to locate feature files:
- `get_feature_documentation("soundboard")` → reads `docs/articles/soundboard.md`
- `get_feature_documentation("rat-watch")` → reads `docs/articles/rat-watch.md`
- `search_commands(query)` → searches `README.md` and in-memory command metadata

## Admin UI (Minimal MVP)

### Guild Settings Page Enhancement
Add section to existing guild settings page:
- **Enable/Disable** assistant (toggle)
- **Allowed Channels** (multi-select dropdown, empty = all channels)
- **Rate Limit Override** (optional number input, blank = use global default)

### No Dedicated Pages for MVP
- No analytics dashboard
- No usage reports
- View metrics through existing logging tools (Seq, Kibana)

## Open Questions

**Resolved:**
- ✅ LLM Provider: Claude API via Anthropic.SDK
- ✅ Interaction model: Mentions only (no slash command for MVP)
- ✅ Rate limiting: Per-user, with global default and guild override
- ✅ Error handling: Friendly messages to user, always log
- ✅ Conversation history: No (stateless for MVP)
- ✅ Channel filtering: Optional per-guild configuration
- ✅ Admin UI: Minimal (add to existing guild settings)

**All Resolved (via Configuration):**
- ✅ Default enabled or disabled: Disabled by default (`GloballyEnabled: false`, `EnabledByDefaultForNewGuilds: false`)
- ✅ Rate limit values: 5 questions per 5 minutes (`DefaultRateLimit: 5`, `RateLimitWindowMinutes: 5`)
- ✅ Admin bypass: Admins and SuperAdmins bypass limits (`RateLimitBypassRole: "Admin"`)
- ✅ Consent/privacy: Explicit opt-in required (`RequireExplicitConsent: true`), users must enable via `/consent`
- ✅ Guild ID in prompt: Passed to Claude for URL generation (`IncludeGuildContext: true`); user ID never passed
- ✅ Base URL for links: Configurable via `BaseUrl` (defaults to `Application.BaseUrl` if not set)

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Use Anthropic.SDK | Easiest to implement, official SDK, type-safe |
| Tool-based documentation access | Smaller prompts, fresh data, cost-effective |
| Enable prompt caching | 50%+ cost reduction, faster responses, 5-min cache lifetime perfect for chatty users |
| Cache agent prompt + common docs | Best of both worlds: static content cached, dynamic lookups via tools |
| Don't cache guild-specific context | Guild ID passed to tools at runtime to maintain cache sharing across guilds |
| Stateless (no history) | Simplifies MVP, reduces cost and complexity |
| Per-user rate limits (5 per 5 min) | Tighter cost control, prevents rapid spam, more reasonable than hourly |
| Admin+ role bypass rate limits | Admins need freedom to test/debug, SuperAdmins too |
| Ephemeral rate limit messages | Private feedback, doesn't clutter channels |
| Explicit consent required | Better privacy compliance, GDPR-friendly, user control |
| Pass guild ID only (not user ID) | Needed for URL generation, user ID unnecessary and privacy risk |
| Friendly error messages | Better UX, consistent with bot personality |
| Minimal admin UI for MVP | Faster to ship, iterate based on usage |
| Store in existing database | Consistent with other features |
| Disabled by default | Safer rollout, opt-in per guild, explicit enablement |

## Example Tool Flow

### User Question: "@DiskordBott what's the soundboard URL?"

1. **Message Received**: Bot detects mention in Discord
2. **Check Rate Limit**: User hasn't exceeded limit
3. **Claude API Call**: Send question with tool definitions:
   ```json
   {
     "model": "claude-3-5-sonnet-20241022",
     "messages": [{"role": "user", "content": "what's the soundboard URL?"}],
     "tools": [
       {
         "name": "get_feature_documentation",
         "description": "Returns documentation for a specific bot feature",
         "input_schema": {
           "type": "object",
           "properties": {
             "feature_name": {"type": "string"}
           }
         }
       }
     ]
   }
   ```
4. **Claude Decides**: "I need soundboard documentation"
5. **Tool Call**: Claude requests `get_feature_documentation(feature_name="soundboard")`
6. **Bot Executes**: Reads `docs/articles/soundboard.md`, returns content
7. **Claude Response**: Formats answer using soundboard docs
8. **Bot Posts**: "The soundboard URL is https://discordbot.cpike.ca/Portal/Soundboard/{GUILD_ID}"
9. **Metrics Updated**: Log tokens used, question count

## Privacy & Compliance

### Data Collection
- Questions asked and responses given are logged for debugging and cost monitoring
- User IDs and Guild IDs are stored for rate limiting and metrics
- No message content is stored permanently (only transient for API call)

### User Consent
- Consider adding to existing `/consent` and `/privacy` commands
- Disclose that questions are sent to third-party AI service (Claude)
- Allow users to opt-out of assistant (to be decided)

### GDPR Compliance
- Users can request deletion of assistant interaction logs
- Retention policy for usage metrics (align with existing message log retention)

## Testing Strategy

### Unit Tests
- AssistantService logic (rate limiting, channel filtering)
- Prompt template substitution
- Error handling paths

### Integration Tests
- Mock Claude API responses
- Database interactions (settings, metrics, rate limits)
- Rate limiting enforcement across multiple requests

### Manual Testing
- Real Discord interactions with live bot
- Test rate limit messages (ephemeral)
- Test error scenarios (API down, timeout)
- Verify security (prompt injection attempts)

## Deployment Considerations

### Configuration
- Add Claude API key to production User Secrets
- Set default rate limit values in appsettings.json
- Database migration for new tables

### Monitoring
- Log all API calls (success, failure, latency, token usage)
- Alert on high error rates or excessive API costs
- Track rate limit violations (potential abuse)

### Rollout
- Deploy with feature disabled by default
- Enable for a single test guild first
- Monitor costs and usage for a week
- Gradually enable for other guilds

## Success Metrics

### MVP Launch
- Feature is functional and responds to mentions
- Rate limiting prevents abuse
- Error rate < 5%
- Average response time < 10 seconds

### Post-Launch (30 days)
- Number of questions asked per week
- Most common questions (identify gaps in documentation)
- API costs vs. budget
- User feedback (qualitative)

## Additional Design Details

### Discord Message Constraints

| Constraint | Value | Handling |
|------------|-------|----------|
| Max message length | 2000 chars | Truncate at `MaxResponseLength` (1800) with `TruncationSuffix` |
| Markdown support | Discord markdown | Claude generates Discord-compatible markdown (\*, \*\*, \`, \`\`\`) |
| Embed usage | Not for MVP | Plain text responses only |
| Code blocks | Supported | Claude can use \`\`\`language\n...\n\`\`\` syntax |

### Tool Response Format

Tools return structured data to Claude:

```json
{
  "success": true,
  "data": "... markdown content or structured data ...",
  "error": null
}
```

Or on error:

```json
{
  "success": false,
  "data": null,
  "error": "Documentation file not found: soundboard.md"
}
```

Claude receives the response and formulates a user-friendly answer even when tools fail.

### Logging Schema

Each assistant interaction is logged with the following fields:

| Field | Type | Purpose |
|-------|------|---------|
| `Timestamp` | `DateTime` | When question was asked |
| `UserId` | `ulong` | Discord user ID |
| `GuildId` | `ulong` | Discord guild ID |
| `ChannelId` | `ulong` | Discord channel ID |
| `Question` | `string` | User's original question |
| `Response` | `string` | Claude's response |
| `InputTokens` | `int` | Tokens in request (non-cached) |
| `OutputTokens` | `int` | Tokens in response |
| `CachedTokens` | `int` | Tokens served from cache |
| `CacheCreationTokens` | `int` | Tokens written to cache (on cache miss) |
| `CacheHit` | `bool` | Whether cache was hit |
| `ToolCalls` | `int` | Number of tools invoked |
| `LatencyMs` | `int` | Total response time |
| `Success` | `bool` | Whether request succeeded |
| `ErrorMessage` | `string?` | Error details if failed |
| `EstimatedCostUsd` | `decimal` | Estimated API cost (includes cache savings) |

Logs are stored in the audit log system with action type `AssistantQuestion`.

Retention: `InteractionLogRetentionDays` (default 90 days), aligns with `MessageLogRetention`.

### Fallback Behavior

| Failure Scenario | Fallback Action |
|------------------|-----------------|
| All documentation tools fail | Claude responds with: "I don't have information about that feature yet. Please check the documentation or ask a server admin." |
| Agent prompt file missing | Use hardcoded minimal prompt: "You are a helpful Discord bot assistant. Answer questions about bot features concisely." |
| API timeout or error | Show `ErrorMessage` to user, log error, retry up to `MaxRetryAttempts` |
| Rate limit exceeded | Ephemeral message: "You've asked too many questions. Try again in X minutes." |
| Claude returns empty response | Show `ErrorMessage` to user, log as error |

### Privacy and Consent

**Selected Approach: Explicit Opt-in (via `RequireExplicitConsent: true`)**

Users must explicitly consent before using the assistant feature:

1. **First-time use flow:**
   - User mentions bot with question
   - Bot checks consent status via existing consent system
   - If not consented: Send ephemeral message: "You must opt-in to use the assistant. Questions are processed by Anthropic's Claude API. Run `/consent` to enable `assistant_usage`."
   - If consented: Process question normally

2. **Consent management:**
   - Extend existing `/consent` command with new `assistant_usage` consent type
   - Store consent preference in existing `UserConsent` table (new column: `AssistantUsageConsent`)
   - Users can revoke consent at any time via `/consent`

3. **Privacy disclosure:**
   - Update `/privacy` command to describe assistant feature
   - Explain that questions are sent to Claude API (third-party service)
   - Describe what data is logged and retention policy (90 days default)
   - Link to Anthropic's privacy policy

4. **GDPR compliance:**
   - Users can request deletion of assistant interaction logs via existing data deletion process
   - Logs include: timestamp, user ID, guild ID, question, response, tokens, cost
   - Retention controlled by `InteractionLogRetentionDays` (default 90 days)

**Fallback option:**
- If `RequireExplicitConsent: false`, mentioning bot implies consent (simpler UX)
- Default is `true` for better privacy compliance

## Next Steps After Requirements

1. **Create GitHub issue** via `/create-issue` command
2. **Generate implementation plan** via systems-architect agent
3. **Database migration design** (new tables for settings and metrics)
4. **Agent prompt finalization** (finalize placeholders and security guidelines)
5. **User Secrets configuration** (add Claude API key documentation to CLAUDE.md)
6. **Begin implementation** (start with core service and API integration)

---

**Document Version**: 1.1
**Last Updated**: 2026-01-12
**Status**: Ready for implementation planning
