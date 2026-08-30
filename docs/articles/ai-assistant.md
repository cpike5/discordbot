---
uid: ai-assistant
title: AI Assistant
description: LLM-powered conversational assistant that responds to mentions with helpful information about bot features
---

# AI Assistant

This document describes the AI Assistant feature, which provides LLM-powered conversational responses to user questions about bot features, commands, and usage. Users can mention the bot in Discord with a question and receive helpful answers directly in the channel. Models are reached through [OpenRouter](https://openrouter.ai), which exposes an OpenAI-compatible chat-completions API in front of many providers; the default model is `anthropic/claude-sonnet-4`.

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

**Use Cases:**
- Monitor usage trends
- Track API costs against budget
- Identify peak usage periods
- Optimize caching strategy based on cache hit rates

---

## Configuration

### ApplicationOptions (Global Settings)

Configuration is managed via `appsettings.json` and User Secrets. The feature is **disabled by default**.

#### Feature Flags

| Setting | Default | Description |
|---------|---------|-------------|
| `GloballyEnabled` | `false` | Master switch to enable/disable assistant globally |
| `EnabledByDefaultForNewGuilds` | `false` | Whether newly joined guilds have assistant enabled by default |
| `ShowTypingIndicator` | `true` | Show typing indicator while the model responds |
| `IncludeGuildContext` | `true` | Include guild ID for URL generation (keep true) |

#### Rate Limiting

| Setting | Default | Description |
|---------|---------|-------------|
| `DefaultRateLimit` | `5` | Max questions per user per time window |
| `RateLimitWindowMinutes` | `5` | Time window for rate limit (minutes) |
| `RateLimitBypassRole` | `"Admin"` | Role required to bypass limits (Admin+ or "SuperAdmin", "Moderator", null) |

#### Message Constraints

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxQuestionLength` | `500` | Max question length in characters |
| `MaxResponseLength` | `1800` | Max response length (Discord limit is 2000) |
| `TruncationSuffix` | `"\n\n... *(response truncated)*"` | Suffix appended to truncated responses |

#### Model Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Model` | `"anthropic/claude-sonnet-4"` | OpenRouter model slug (blank falls back to `OpenRouter:DefaultModel`) |
| `ApiTimeoutMs` | `30000` | API call timeout in milliseconds |
| `MaxTokens` | `512` | Maximum tokens in the model's response (~375 words) |
| `Temperature` | `0.7` | Response creativity (0.0=deterministic, 1.0=random) |

**Model slugs** are OpenRouter identifiers, not vendor model IDs — `anthropic/claude-sonnet-4`, not `claude-sonnet-4-20250514`. Any slug from https://openrouter.ai/models works. Common choices:
- `anthropic/claude-sonnet-4` - **Recommended** - Best balance of speed, quality, and cost
- `anthropic/claude-opus-4` - Highest quality, slower and more expensive
- `anthropic/claude-haiku-4.5` - Fastest and cheapest, lower quality
- `openai/gpt-4o` - Non-Claude alternative (no prompt caching; see below)

#### Tool Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableDocumentationTools` | `true` | Whether the model can call documentation tools |
| `MaxToolCallsPerQuestion` | `5` | Max tool calls per question (prevents loops) |
| `ToolExecutionTimeoutMs` | `5000` | Tool execution timeout in milliseconds |

#### Error Handling

| Setting | Default | Description |
|---------|---------|-------------|
| `ErrorMessage` | `"Oops, I'm having trouble thinking right now..."` | Friendly error message shown to users |
| `MaxRetryAttempts` | `2` | API retry attempts on failure |
| `RetryDelayMs` | `1000` | Delay between retries in milliseconds |

#### Cost Monitoring

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableCostTracking` | `true` | Track token usage and record costs |
| `DailyCostThresholdUsd` | `5.00` | Daily cost alert threshold (creates alert incident if exceeded) |
| `CostPerMillionInputTokens` | `3.00` | Fallback cost per million input tokens |
| `CostPerMillionOutputTokens` | `15.00` | Fallback cost per million output tokens |

**OpenRouter reports the real billed cost** (`usage.cost`) on every response, and that figure is what gets recorded. The `CostPerMillion*` rates are a *fallback only*, used when a response carries no cost — and because they are fixed, they are wrong for any model whose pricing differs from Claude Sonnet. Change the model, and the recorded cost stays accurate; only the fallback estimate drifts.

#### Prompt Caching (Cost Optimization)

Prompt caching reduces API costs by ~50% by caching the agent prompt and common documentation files for 5 minutes. OpenRouter passes cache breakpoints through to **Claude-family models only** — other models ignore them and report zero cached tokens, so caching is safe to leave on for any slug but only pays off on Claude.

| Setting | Default | Description |
|---------|---------|-------------|
| `EnablePromptCaching` | `true` | Enable prompt caching (honoured by Claude-family models) |
| `CacheCommonDocumentation` | `true` | Include common docs in cached system prompt |
| `CachedDocumentationFiles` | `["commands-page.md", "soundboard.md", "rat-watch.md", "tts-support.md"]` | Docs to include in cache |
| `CostPerMillionCachedTokens` | `0.30` | Cost for cached tokens (90% discount) |
| `CostPerMillionCacheWriteTokens` | `3.75` | Cost for cache writes (on cache miss) |

**Cost Comparison (Single Question):**
- Without caching: ~$0.0078/question
- With caching: ~$0.00375/question (50% reduction)

At 100 questions/day:
- Without caching: ~$0.78/day ($23.40/month)
- With caching: ~$0.38/day ($11.40/month)

#### Privacy and Audit

| Setting | Default | Description |
|---------|---------|-------------|
| `RequireExplicitConsent` | `true` | Require `/consent` opt-in before using assistant |
| `LogInteractions` | `true` | Log questions and responses to audit log |
| `InteractionLogRetentionDays` | `90` | Days to retain interaction logs before cleanup |

#### Paths

| Setting | Default | Description |
|---------|---------|-------------|
| `AgentPromptPath` | `"docs/agents/assistant-agent.md"` | Path to agent behavior/security prompt |
| `DocumentationBasePath` | `"docs/articles"` | Base directory for feature documentation |
| `ReadmePath` | `"README.md"` | Path to README for command lists |
| `BaseUrl` | `null` (uses Application.BaseUrl) | Base URL for link generation in responses |

### Example appsettings.json

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
    "Model": "anthropic/claude-sonnet-4",
    "ApiTimeoutMs": 30000,
    "MaxTokens": 512,
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
    "CachedDocumentationFiles": [
      "commands-page.md",
      "soundboard.md",
      "rat-watch.md",
      "tts-support.md"
    ],
    "CostPerMillionCachedTokens": 0.30,
    "CostPerMillionCacheWriteTokens": 3.75,
    "RequireExplicitConsent": true,
    "LogInteractions": true,
    "InteractionLogRetentionDays": 90,
    "BaseUrl": null,
    "ShowTypingIndicator": true,
    "IncludeGuildContext": true
  }
}
```

### OpenRouter Options

The transport-level settings live in their own `OpenRouter` section, separate from the `Assistant` feature settings above.

| Setting | Default | Description |
|---------|---------|-------------|
| `ApiKey` | *(none)* | OpenRouter API key (**secret** - user secrets or environment only) |
| `BaseUrl` | `"https://openrouter.ai/api/v1/"` | API base address (trailing slash matters - request paths are relative to it) |
| `DefaultModel` | `"anthropic/claude-sonnet-4"` | Model slug used when a request does not name one |
| `MaxRetries` | `3` | Retry attempts for transient failures (HTTP 408/429/5xx, timeouts, network errors) |
| `TimeoutSeconds` | `300` | Per-attempt request timeout |
| `RetryBaseDelayMs` | `1000` | Base delay for exponential backoff (`baseDelay * 2^attempt`) |
| `EnablePromptCachingByDefault` | `true` | Add a cache breakpoint to the system prompt unless a request overrides it |
| `AppUrl` | *(none)* | Site URL sent as `HTTP-Referer` (attribution on openrouter.ai rankings) |
| `AppTitle` | `"DiscordBot"` | Application name sent as `X-Title` |

```json
{
  "OpenRouter": {
    "BaseUrl": "https://openrouter.ai/api/v1/",
    "DefaultModel": "anthropic/claude-sonnet-4",
    "MaxRetries": 3,
    "TimeoutSeconds": 300,
    "RetryBaseDelayMs": 1000,
    "EnablePromptCachingByDefault": true,
    "AppTitle": "DiscordBot"
  }
}
```

### User Secrets Configuration

The OpenRouter API key must be configured via User Secrets (never commit to version control):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "OpenRouter:ApiKey" "sk-or-v1-your-api-key-here"
```

To obtain an API key:
1. Go to https://openrouter.ai/keys
2. Sign in and create a new key
3. Copy the key to user secrets as shown above

In containers and other hosted environments, use the environment-variable form `OpenRouter__ApiKey`.

---

## Architecture

### LLM Abstraction Layer

The assistant uses a provider-agnostic LLM abstraction layer that supports multiple AI providers:

**Interfaces:**
- `ILlmClient` - Core LLM provider interface (message completion, tool use, prompt caching support)
- `IAgentRunner` - Orchestrates agentic loop (tool use cycles, conversation management)
- `IToolProvider` - Provides related tools (definitions and execution)
- `IToolRegistry` - Manages tool providers with enable/disable capability
- `IPromptTemplate` - Loads and renders prompt templates with variable substitution

**Current Provider:**
- `OpenRouterLlmClient` (`DiscordBot.Infrastructure.Services.LLM.OpenRouter`) - OpenAI-compatible chat completions against OpenRouter, via an **owned typed `HttpClient`** and owned wire records (`ChatCompletionRequest.cs`, `ChatCompletionResponse.cs`). There is no LLM SDK dependency.
- `OpenRouterMessageMapper` - translates between the neutral `LlmRequest`/`LlmResponse` DTOs and the wire records.

Because OpenRouter fronts many providers, switching models is a configuration change (a different slug), not a code change.

**Wire-shape notes** (these differ from a native Anthropic Messages API and are handled inside the mapper):
- The system prompt is the **first message** in `messages`, not a top-level parameter.
- Tool results are `role: "tool"` messages carrying a `tool_call_id`, not content blocks appended to a user turn.
- Tool arguments arrive as a **JSON string**, which the mapper parses.
- `finish_reason` replaces `stop_reason`.
- Usage is reported as `prompt_tokens` / `completion_tokens`, with cached reads under `prompt_tokens_details.cached_tokens`, plus a real billed `cost` field.
- Every request that carries tools also sends `provider.require_parameters: true`, so routing cannot select a provider that lacks function-calling support.

**Future Providers:**
- Local models via Ollama
- Other providers following the `ILlmClient` interface

### Tool System

**Tool Providers:**
- `DocumentationToolProvider` - Access to feature docs, command info, and guild context
  - `get_feature_documentation` - Fetch markdown docs for a feature
  - `search_commands` - Search available commands by keyword
  - `get_command_details` - Get detailed info about a specific command
  - `list_features` - List all available bot features

- `UserGuildInfoToolProvider` - User and guild information
  - `get_user_profile` - User info (username, avatar, roles)
  - `get_guild_info` - Guild info (name, member count, icon)
  - `get_user_roles` - List user's roles in guild

**Tool Registry:**
- Centralized management of all providers
- Enable/disable providers at runtime
- Routes tool calls to appropriate provider

### Agentic Loop

The `AgentRunner` orchestrates the model's responses with tool use:

1. Send user question to the model with available tools
2. The model responds with either:
   - Text answer (`finish_reason: stop`) → Return response
   - Tool calls (`finish_reason: tool_calls`) → Execute tools and send results back as `role: "tool"` messages
   - Loop back to step 1 if more tool calls needed (max 5 calls)
3. Extract final text answer and return

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

**Agent System Prompt:** ~1500 tokens (cached if enabled)
**User Question:** ~50-100 tokens (varies by question length)
**Tool Responses:** ~100-500 tokens (depends on docs fetched)
**Model Response:** ~100-200 tokens (depends on answer length)

**Total per interaction:** ~250-1000 tokens

### Pricing (anthropic/claude-sonnet-4)

OpenRouter bills per model at that model's published rate and reports the billed `cost` on each response, so these figures are the shape of the default model's pricing rather than a fixed constant. Check https://openrouter.ai/models for current per-model rates.

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
1. Ensure prompt caching is enabled (`EnablePromptCaching: true`)
2. Review rate limit settings (lower to reduce volume)
3. Check cache hit rate - if low, verify `CachedDocumentationFiles` are being accessed
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
- `AgentRunner.RunAsync`
- `OpenRouterLlmClient.CompleteAsync`
- Tool execution logs

---

## Security Considerations

### Prompt Injection Defense

The agent prompt includes extensive guidelines to prevent:
- Revealing internal bot implementation
- Exposing API keys or credentials
- Accessing other users' private data
- Executing commands on behalf of users
- Jailbreak attempts

Users cannot break out of the assistant context - the model is constrained to answering questions about bot features.

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
- **Feature Specs:** [Assistant Requirements](../requirements/assistant-requirements.md)
- **Implementation Plan:** [Assistant Implementation Plan](../requirements/assistant-implementation-plan.md)
- **Agent Prompt:** `docs/agents/assistant-agent.md`
- **OpenRouter Documentation:** https://openrouter.ai/docs
- **OpenRouter Model List:** https://openrouter.ai/models
- **OpenRouter Privacy:** https://openrouter.ai/privacy
