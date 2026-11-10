---
uid: ai-assistant
title: AI Assistant
description: LLM-powered conversational assistant that responds to mentions with helpful information about bot features
---

# AI Assistant

This document describes the AI Assistant feature, which provides LLM-powered conversational responses to user questions about bot features, commands, and usage. Users can mention the bot in Discord with a question and receive helpful answers directly in the channel. Models are reached through [OpenRouter](https://openrouter.ai), which exposes an OpenAI-compatible chat-completions API in front of many providers; the default model is `openrouter/auto`, which lets OpenRouter choose a model per request.

## Overview

The AI Assistant feature provides:
- **Natural language questions** - Users mention the bot with any question about features or commands
- **Intelligent responses** - The model processes questions with context about available features
- **Tool-based documentation access** - Dynamic access to feature docs, command information, and guild context
- **Cost-aware operation** - Prompt caching (50% cost reduction), rate limiting, and cost tracking
- **Privacy and consent** - Explicit user consent required, interaction logging with retention policies
- **Guild configuration** - Per-guild enable/disable, channel restrictions, and rate limit overrides
- **Admin dashboard** - Settings page and metrics dashboard for monitoring usage and costs

The assistant is **disabled by default** and requires explicit enablement per guild for safety and cost control.

---

## Prerequisites

The AI Assistant requires configuration of the OpenRouter API key:

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "OpenRouter:ApiKey" "sk-or-v1-your-api-key-here"
```

Without `OpenRouter:ApiKey` the assistant services are not registered at all (the rest of the bot, including migrations, still runs).

See [Environment Configuration](environment-configuration.md) section "OpenRouterOptions" for the full option list.

---

## Using the Assistant

### Mentioning the Bot

To ask a question, simply mention the bot in any Discord channel where the feature is enabled:

```
@DiskordBott How do I use the soundboard?
```

The bot will respond with a helpful answer in the same channel as a reply to your message.

### Requirements

Before you can use the assistant, you must:

1. **Grant Consent** - Run `/consent` and enable the "assistant_usage" option
2. **Check Guild Settings** - The guild administrator must have enabled the assistant feature
3. **Use Allowed Channels** - The channel must not be restricted (or must be in the allowed list)
4. **Respect Rate Limits** - You can ask up to 5 questions per 5 minutes (varies by guild)

### Consent and Privacy

The AI Assistant sends your questions to OpenRouter, which routes them to the configured model's provider for processing. Questions and responses are logged by the bot for debugging and cost monitoring (retained for 90 days by default).

**To opt-in to the assistant:**
```
/consent type:assistant_usage action:grant
```

**To opt-out:**
```
/consent type:assistant_usage action:revoke
```

See [Consent and Privacy](consent-privacy.md) for full privacy information and data deletion options.

### Rate Limiting

The assistant enforces per-user rate limits to prevent abuse and control API costs:

- **Default Limit:** 5 questions per 5 minutes
- **Guild Override:** Administrators can customize the limit per guild
- **Admin Bypass:** Users with Admin role or higher bypass rate limits (for testing)
- **Exceeded Limit:** When you hit the limit, you'll receive an ephemeral message: "You've asked too many questions. Try again in X minutes."

---

## Slash Commands

### /consent

Manage your data consent preferences, including assistant usage consent.

**Syntax:**
```
/consent type:assistant_usage action:grant
/consent type:assistant_usage action:revoke
```

**Options:**
- `type` (required) - Choose "assistant_usage" for AI assistant consent
- `action` (required) - "grant" to opt-in, "revoke" to opt-out

**Response:**
- Confirmation of consent granted/revoked
- List of your current consent preferences

---

## Supported Questions

The assistant has access to information about:

- **Commands** - All slash commands, parameters, preconditions, and usage examples
- **Features** - Soundboard, Rat Watch, Reminders, Text-to-Speech, Scheduled Messages, and more
- **Settings** - How to configure guild and bot settings
- **Guild Context** - Member information, guild statistics
- **Bot Documentation** - Comprehensive docs about all features and how to use them

**Example questions:**
- "How do I use the soundboard?"
- "What's the TTS voice syntax?"
- "How do reminders work?"
- "What commands are available?"
- "Where's the admin dashboard for settings?"
- "How do I give myself a role?"

### Questions About Private Data

The assistant will **not** answer questions that would reveal:
- Internal bot implementation details or API keys
- Private user information (passwords, tokens, etc.)
- Sensitive system configuration
- Other users' personal data

If you ask about private data, the assistant will politely decline and suggest checking documentation or contacting an administrator.

---

## Admin UI

### Assistant Settings Page

**URL:** `/Guilds/AssistantSettings/{guildId}`

**Authorization:** RequireAdmin policy

**Features:**
- **Enable/Disable** - Toggle the assistant feature for the guild
- **Allowed Channels** - Restrict the assistant to specific channels (empty list = all channels allowed)
- **Tool Access** - Tick which tools the assistant may use in this server, grouped by category.
  Fewer tools means cheaper, faster answers, and a tool the assistant cannot see is one it cannot
  call. **Selecting nothing means the default set**, not "no tools" — the page says so, and shows
  the default ticked. The choice applies to every caller in the server, which is what keeps it from
  fragmenting the prompt cache.
- **Rate Limit Override** - Set a custom questions-per-window limit for your guild (leave blank to use global default)
- **Save** - Apply changes and return to page

**Navigation:** From guild details page, click "Assistant Settings" link

### Assistant Metrics Page

**URL:** `/Guilds/AssistantMetrics/{guildId}`

**Authorization:** RequireAdmin policy

**Displays:**
- **Summary Cards:**
  - Total questions asked (last 30 days)
  - Total estimated cost in USD
  - Average response latency in milliseconds
  - Cache hit rate percentage

- **Daily Metrics Table:**
  - Date
  - Questions asked
  - Input tokens (non-cached)
  - Output tokens
  - Cached tokens served (from prompt cache)
  - Cache write tokens (on cache miss)
  - Estimated cost for the day
  - Success/failure rate

- **Chart (Optional):** Visualization of costs over time
- **Cost by User:** Top 20 spenders in this guild over the same 30-day window, sourced from the LLM
  usage ledger (see below) rather than the daily aggregates above, since those carry no per-user
  breakdown

- **Tool Usage:** Per-tool calls, questions, failure rate and last-used over the same 30-day
  window, counted from the interaction log's tool names. Every tool this server allows is listed
  **including the ones that were never called** — "which of my tools has never been used" is the
  first question worth answering, and only the zeroes answer it.

- **Prompt Surface:**
- `PromptSurfaceReporter`, built on the framework's prompt store and tools, puts a character and token count on what a surface advertises. One Information line per surface at startup, and a per-tool panel on each guild's Assistant Metrics page
- It counts the advertised set, not everything registered: a tool behind an unloaded skill is not in the request and is not billed for

**Tool Telemetry:**
- The framework emits spans on the `cpike.Agent` source with `cpike.tool.*` tags (tool name, outcome, failure code) and the metrics `cpike.agent.tool.calls` and `cpike.agent.tool.duration` (seconds), labelled `gen_ai.tool.name`, `cpike.tool.outcome` and `cpike.tool.failure_code`. `cpike.Agent.OpenRouter` adds the client's own
- The tools a question used are also stored on its interaction log row, which is what the metrics page's Tool Usage table counts, so it works whether or not a trace backend is deployed

### Skills

A tool is not free. Its schema is serialized into every request whether or not the question needs
it, and the prose explaining when to use it costs its tokens in the system prompt on the same terms.
A **skill** is how that stops being true for a tool the assistant rarely needs: one markdown file
naming some tools and carrying the instructions for using them, of which the model sees only a
one-line summary until it decides a request is actually about it and calls `load_skill`. The tools
a skill names are held back from the advertised tool array until then.

`load_skill` is a framework built-in with a `ToolCatalog` entry — category **Skills**, both
surfaces, on by default — so it appears in a server's Tool Access checklist like any other, and an
admin who unticks it has turned skills off for that server's assistant. It is added to a mode only
when the mode has a `skills:` line, rather than costing its schema on every request for a mechanism
with nothing to load.

Skill files live in `docs/agents/prompts/skills/<surface>/` (copied to `Prompts/skills`) and a mode
opts in with a `skills:` line, as `dm.md` does.
Two skills ship, both on the DM side:

| Skill | Loads |
|-------|-------|
| `moderation` | `get_moderation_cases`, `get_user_mod_history`, `search_audit_logs` |
| `analytics` | `get_server_activity_summary`, `get_command_analytics` |

Those five tools are no longer advertised to the DM assistant on every message; they arrive when the
owner asks something moderation- or analytics-shaped and the model loads the skill that owns them.
Everything else the surface offers — the documentation tools, the memory tools, the rest — is
advertised exactly as before. The guild mode has no skills, deliberately: the DM
assistant is multi-turn, so stored activations are preloaded on later turns and a skill is paid for once,
while the guild assistant is single-turn and would pay the loading round every time.

Adding a skill is one markdown file in the right directory (plus its key on the mode's `skills:` line). There is no catalogue entry, no DI
registration, and no code; the file is picked up without a restart (hot reload). The tools it names have to exist and be advertised on that surface already — a skill
can only ever un-hide something the assistant was allowed to use, never widen its reach, so a name
a guild has turned off is dropped and the model is never told it existed.

The file format and the authoring advice are in
[`docs/agents/README.md`](../agents/README.md); the mechanism — how the tool array is
exposed, and what it costs the prompt cache — is
[patterns.md § Agent Skills](../architecture/patterns.md#agent-skills).

### Agentic Loop

The framework's `AgentService` orchestrates the model's responses with tool use:

1. Send the user question to the model with the advertised tools
2. The model responds with either:
   - Text answer (`finish_reason: stop`) → Return response
   - Tool calls (`finish_reason: tool_calls`) → Execute tools and send results back as `role: "tool"` messages
   - Loop back to step 1 if more tool calls needed (up to `MaxToolRounds`, default 8)
3. Extract the final text answer and return

This allows the model to dynamically fetch documentation as needed rather than including everything in the prompt.

### Services

**IAssistantService** - Main service for question processing
- `AskQuestionAsync()` - Process a question through full pipeline
- `IsEnabledForGuildAsync()` - Check if enabled
- `IsAllowedInChannelAsync()` - Check channel restrictions
- `CheckRateLimitAsync()` - Check and enforce rate limits
- `GetUsageMetricsAsync()` - Retrieve usage statistics
- `GetRecentInteractionsAsync()` - Retrieve interaction logs

**IAssistantGuildSettingsService** - Guild configuration management
- `GetOrCreateSettingsAsync()` - Get or create guild settings
- `UpdateSettingsAsync()` - Save settings changes
- `EnableAsync()` / `DisableAsync()` - Toggle feature per guild

### Repositories

**IAssistantGuildSettingsRepository** - Persist guild configuration
**IAssistantUsageMetricsRepository** - Persist daily usage metrics
**IAssistantInteractionLogRepository** - Persist detailed interaction logs

---

## Data Storage

### Database Entities

#### AssistantGuildSettings
Per-guild configuration stored in database.

**Fields:**
- `GuildId` (PK) - Discord guild ID
- `IsEnabled` - Feature enabled for this guild
- `AllowedChannelIds` - JSON array of channel IDs (empty = all channels)
- `RateLimitOverride` - Custom rate limit (null = use global default)
- `CreatedAt` - When settings were created
- `UpdatedAt` - Last modification time

**Table:** `AssistantGuildSettings`

#### AssistantUsageMetrics
Daily aggregated usage metrics per guild.

**Fields:**
- `Id` (PK) - Auto-increment ID
- `GuildId` - Discord guild ID (FK)
- `Date` - Date for metrics (UTC, one record per guild per day)
- `TotalQuestions` - Questions asked on this date
- `TotalInputTokens` - Non-cached input tokens
- `TotalOutputTokens` - Output tokens
- `TotalCachedTokens` - Tokens served from cache
- `TotalCacheHits` / `TotalCacheMisses` - Cache statistics
- `TotalToolCalls` - Total tool calls executed
- `EstimatedCostUsd` - Estimated API cost for the day
- `FailedRequests` - Number of failed requests
- `AverageLatencyMs` - Average response time in milliseconds
- `UpdatedAt` - Last update time

**Indexes:**
- Composite unique: `(GuildId, Date)`
- Single: `Date` (for cleanup queries)

#### AssistantInteractionLog
Detailed logs of individual interactions for audit and debugging.

**Fields:**
- `Id` (PK) - Auto-increment ID
- `Timestamp` - When question was asked
- `UserId` - Discord user ID (FK)
- `GuildId` - Discord guild ID (FK)
- `ChannelId` - Discord channel ID
- `MessageId` - Original Discord message ID
- `Question` - User's question (max 500 chars)
- `Response` - The model's response (max 2000 chars)
- `InputTokens` / `OutputTokens` / `CachedTokens` / `CacheCreationTokens` - Token counts
- `CacheHit` - Whether cache was used
- `ToolCalls` - Number of tool calls executed
- `LatencyMs` - Total response time
- `Success` - Whether request succeeded
- `ErrorMessage` - Error details if failed
- `EstimatedCostUsd` - Cost of this interaction

**Indexes:**
- Composite: `(GuildId, Timestamp)` - Guild queries
- Composite: `(UserId, Timestamp)` - User queries
- Single: `Timestamp` - Cleanup queries

---

## Cost Estimation

### Token Usage

Each interaction uses tokens as follows:

**Agent System Prompt:** ~1000 tokens (cached if enabled)
**User Question:** ~50-100 tokens (varies by question length)
**Tool Responses:** ~100-500 tokens (depends on docs fetched)
**Model Response:** ~100-200 tokens (depends on answer length)

**Total per interaction:** ~250-1000 tokens

### Pricing

OpenRouter bills per model at that model's published rate and reports the billed `cost` on each response. With the default `openrouter/auto` slug the model, and therefore the rate, varies per request, so the figures below are illustrative (they are Claude Sonnet's rates, which the `CostPerMillion*` fallback settings also assume). Check https://openrouter.ai/models for current per-model rates.

**Without Caching:**
- Input: $3.00 per million tokens
- Output: $15.00 per million tokens

**With Caching (90% discount on cached tokens):**
- Regular input: $3.00/M
- Cached input: $0.30/M (first 5 minutes)
- Cache write: $3.75/M (on cache miss)

### Cost Examples

**100 questions/month without caching:**
- Total tokens: ~50,000
- Cost: ~$1.00/month

**100 questions/day without caching:**
- Total tokens: ~1.5M
- Cost: ~$23.40/month

**100 questions/day with caching (50% reduction):**
- Total tokens: ~1.5M (cached)
- Cost: ~$11.40/month

---

## Performance

### Response Time Targets

- **Initial response:** < 10 seconds (depends on OpenRouter/model latency)
- **Tool execution:** < 5 seconds per tool call
- **Total latency with tools:** < 15 seconds

Actual times depend on:
- OpenRouter/model response latency (usually 1-3 seconds)
- Number of tool calls (max 5)
- Network conditions

### Optimization

**Prompt Caching:**
- Reduces input token costs by 90% (cached portion)
- Reduces latency by reusing cached tokens
- 5-minute cache lifetime shared across all requests

**Rate Limiting:**
- Prevents abuse and controls costs
- Per-user limits prevent single user from consuming all budget
- Default 5 questions per 5 minutes is conservative

**Monitoring:**
- Track cache hit rate in metrics dashboard
- Monitor daily costs against threshold
- Use interaction logs to identify patterns

---

## Troubleshooting

### Common Issues

#### Bot doesn't respond to mentions

**Check:**
1. Is the assistant globally enabled? (`GloballyEnabled: true` in appsettings)
2. Is the assistant enabled for your guild? (Check via admin UI Settings page)
3. Is your message in an allowed channel? (Check channel restrictions on Settings page)
4. Have you granted consent? (Run `/consent` to check)
5. Check Serilog logs for errors (look for Assistant-related entries)

#### "You've already asked too many questions"

**Solution:**
- Wait for the rate limit window to expire (default 5 minutes)
- Admins can bypass rate limits by running the command with Admin role
- Guild admins can increase the rate limit on the Settings page

#### "You must opt-in to use the assistant"

**Solution:**
- Run `/consent type:assistant_usage action:grant` to opt-in
- See [Consent and Privacy](consent-privacy.md) for details

#### Slow responses or timeouts

**Check:**
1. Is OpenRouter responding? (Check https://status.openrouter.ai)
2. Are tool calls timing out? (Check Serilog logs for timeout errors)
3. Try asking a simpler question without requesting tool-heavy features
4. Check network connectivity

#### High costs in metrics

**Mitigation:**
1. Prompt caching is automatic (the framework marks the system prompt, history and tool results); the `Assistant:Cost` caching keys are not read
2. Review rate limit settings (lower to reduce volume)
3. Check the cache hit rate: the framework logs a warning when a run's second model call reads nothing from the cache (`Agent:WarnOnPromptCacheMiss`)
4. Disable for less-used guilds

### Debug Logging

Enable verbose logging to troubleshoot:

```csharp
// In appsettings.json, increase log level for Assistant
{
  "Logging": {
    "LogLevel": {
      "DiscordBot.Infrastructure.Services.AssistantService": "Debug",
      "DiscordBot.Infrastructure.Services.LLM": "Debug"
    }
  }
}
```

Look for logs containing:
- `AssistantService.AskQuestionAsync`
- `AssistantMessagePipeline`
- `cpike.Agent` and `cpike.Agent.OpenRouter` (set their levels in `Logging:LogLevel`)
- Tool execution logs

---

## Security Considerations

### Prompt Injection Defense

The agent prompt scopes the assistant to questions about bot features and tells the model to treat the user message as a question, never as instructions. It rules out sharing secrets, configuration, internals, the prompt itself, stored user data, and help with abusing the bot.

The prompt deliberately does not quote injection phrases ("ignore previous instructions", persona names, and so on) as examples. OpenRouter's guardrails scan the whole request including the system message, so a prompt that quotes attack strings trips them on every call. Current models do not need the examples; a short statement of scope is enough. Keep it that way when editing the prompt.

### Tool Arguments Are Untrusted Input

A tool argument reaches the tool from the model, and the model's input is a Discord message from any user in any guild where the assistant is enabled. A prompt instruction is not a control: it is advice to a component that can be talked to. Anything a tool argument selects — a file, a row, a guild — is validated in the tool.

`get_feature_documentation` is the worked example. Its `feature_name` becomes a file name, so it is allow-listed to `[a-z0-9-]` before it is used and the resolved path is checked against `DocumentationBasePath`; a name that escapes is refused with the same payload as a name that is simply absent, because a distinct "rejected" message would tell a prober that their probe was understood. The `Warning` log line is where that is visible instead. Full behaviour in [`docs/tools/get_feature_documentation.md`](../tools/get_feature_documentation.md).

### Data Privacy

- User IDs are never sent to the model (only guild ID for URL generation)
- Questions and responses are logged locally only
- OpenRouter and the routed model provider receive only questions/responses in API calls (see OpenRouter's privacy policy)
- Rate limit tracking is local in-memory cache

### Consent and Audit

- Explicit consent required via `/consent` command
- All interactions logged for audit trail
- Users can revoke consent or request data deletion
- Logs retained for 90 days then automatically deleted

---

## Adding New Features to Assistant Knowledge

When adding a new feature to the bot, the AI assistant needs to be updated so it can answer questions about the new feature. This involves updating the agent prompt, documentation tool mappings, and ensuring feature documentation exists.

See **[Adding Features to Assistant Knowledge](assistant-feature-updates.md)** for the complete guide.

---

## Future Enhancements

**Potential improvements** (not in current MVP):
- Conversation history (multi-turn conversations)
- Slash command interface (`/ask <question>`)
- Dedicated analytics dashboard with trends
- Per-guild custom prompts or personality customization
- Thread-based conversations
- Response caching for common questions
- Custom FAQ per guild
- Multiple LLM providers (OpenAI, local models)

---

## References

- **Configuration Guide:** See [Environment Configuration](environment-configuration.md) and [Configuration Guide](configuration-guide.md)
- **Privacy Policies:** [Consent and Privacy](consent-privacy.md)
- **Agent Prompts:** `docs/agents/prompts/guild.md`, `docs/agents/prompts/dm.md`
- **OpenRouter Documentation:** https://openrouter.ai/docs
- **OpenRouter Model List:** https://openrouter.ai/models
- **OpenRouter Privacy:** https://openrouter.ai/privacy
