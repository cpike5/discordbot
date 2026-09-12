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

- **Prompt Surface:** What every question on this server pays for before it is read. The tool array
  serializes at the top of each request, so a tool costs its schema whether or not the question
  needs it. The panel shows the tools sent, the schema size in characters, an estimated token count
  (characters ÷ 4), the characters held back, and then a per-tool table with each tool's share of
  the prefix — marking the tools this server has turned off and the ones a skill is holding back,
  since neither is in the request.

  It measures what the surface *advertises*, not everything registered: a tool behind an unloaded
  skill is callable once the skill loads and is not billed for until then. The same numbers are
  logged once per surface at startup, so a change in the tool array is visible at deploy time rather
  than on the bill.

  Read it against the Tool Usage table above it. A tool that is 15% of every request and was called
  twice in a month belongs behind a skill or turned off — and that is a decision with a number
  attached.

**Use Cases:**
- Monitor usage trends
- Decide whether a tool earns its place in every request, by comparing its share of the prefix with
  how often it is actually called
- Retire or re-enable tools based on what the assistant actually reaches for
- Track API costs against budget
- Identify peak usage periods
- Optimize caching strategy based on cache hit rates

### LLM Usage and Cost Ledger

Every user message sent through the guild assistant, the DM (owner) assistant, or a feature-request
conversation writes one row to the `LlmUsageRecord` ledger — tokens, cost, latency, success, and
which model actually answered. No message text is stored there; the existing per-mode interaction
logs keep that.

**Billed vs. estimated.** `CostSource` on each row is `Billed` when OpenRouter reported a real cost
(`usage.cost`) for that call, or `Estimated` when the fallback per-million rates were used instead
(see [Cost Monitoring](#cost-monitoring) above). The usage dashboard's "Billed Share" figure is the
fraction of total cost that came from billed rows — a low share means most of what you're seeing is
an estimate, not OpenRouter's own number.

**Where to look:**

- **`/Admin/LlmUsage`** (`RequireAdmin`) — the portal-wide dashboard: a date-range filter (default
  last 30 days, max 366) with optional guild and mode filters, hero totals (messages, tokens, cost,
  billed share, failed count, average latency), and tables broken down by user, model, mode, and
  day. Clicking a row in the "Cost by User" table opens a drill-down panel of that user's individual
  messages (model, mode, tokens, cost with its source badge, latency, success), paged via
  `GET api/admin/llm-usage/records`.
- **`/guild/{guildId}/assistant-metrics`** — the same ledger, filtered to one guild, as the "Cost by
  User" table described above.
- **`GET api/admin/llm-usage/summary`** / **`GET api/admin/llm-usage/records`** (`LlmUsageController`,
  `RequireAdmin`) — the API behind both pages; see `docs/articles/api-endpoints.md` for the full
  request/response shape.

**Retention and GDPR.** `AssistantInteractionLogRetentionService` sweeps daily
(`Llm:RetentionSweepIntervalHours`, default 24h) and deletes ledger rows on the same
`Assistant:Privacy:InteractionLogRetentionDays` window (default 90 days) as the guild interaction
logs — no separate retention setting for the ledger. DM interaction logs are swept too, on
`DmAssistant:InteractionLogRetentionDays`. Before this service existed, none of these tables had
any cleanup at all. A user purge or data export includes their `LlmUsageRecords`, guild
`AssistantInteractionLogs`, and DM `DmAssistantInteractionLogs` alongside the rest of their data.

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
| `Model` | `"openrouter/auto"` | OpenRouter model slug (blank falls back to `OpenRouter:DefaultModel`) |
| `ApiTimeoutMs` | `30000` | API call timeout in milliseconds |
| `MaxTokens` | `512` | Maximum tokens in the model's response (~375 words) |
| `Temperature` | `0.3` | Response creativity (0.0=deterministic, 1.0=random). Low by default because the assistant answers factual command questions |

**Model slugs** are OpenRouter identifiers, not vendor model IDs — `anthropic/claude-sonnet-4`, not `claude-sonnet-4-20250514`. Any slug from https://openrouter.ai/models works. Common choices:
- `openrouter/auto` - **Default** - OpenRouter picks a model per request; no prompt caching
- `anthropic/claude-sonnet-4.5` - Pinned Claude model with prompt caching; predictable cost and behaviour
- `anthropic/claude-haiku-4.5` - Fastest and cheapest Claude option
- `openai/gpt-4o` - Non-Claude alternative (no prompt caching; see below)

**Changing the model without a redeploy.** `Assistant:Sampling:Model` is the config-file default;
an admin can override it per mode — guild assistant, DM assistant, and feature requests each have
their own model — from **Admin → Settings → AI Models → Per-Mode Defaults**, without editing
configuration or restarting the bot. The picker only offers models enabled on that tab's catalog
table (nothing is enabled by default — pull the OpenRouter catalog with "Refresh from OpenRouter",
then enable the ones you want to allow), and only tool-capable ones, since every mode sends tools.
`ILlmModelResolver` resolves DB override → bound configuration value → `OpenRouter:DefaultModel` as
a last resort, and the change takes effect on the *next* message — no restart, because the
resolver's per-mode cache is invalidated on save via `ISettingsService.SettingsChanged`. Picking a
non-Claude model does not break anything, but prompt caching (below) only pays off on Claude-family
slugs. See `docs/articles/settings-page.md` ("AI Models Tab") for the UI details.

#### Tool Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableDocumentationTools` | `true` | Whether the model can call documentation tools |
| `MaxToolRounds` | `8` | Max tool-use rounds per question (prevents loops). A round is one completion that asks for tools, not one tool call |
| `MaxToolCallsPerQuestion` | — | Deprecated name for `MaxToolRounds`. Still binds, and still wins when both are set |
| `ToolExecutionTimeoutMs` | `10000` | Per-tool deadline in milliseconds; an overrun tool is abandoned and the model is told so, and the loop continues. `0` disables it |
| `MaxToolResultChars` | `8000` | Ceiling on one tool result entering conversation history (~2,000 tokens). A longer result is replaced by a truncation envelope telling the model it is reading a fragment. `0` disables the cap |
| `DuplicateToolCallLimit` | `3` | How many times one tool may be called with identical arguments in a run before further identical calls are refused without executing the tool. `0` disables the guard |

When the round budget runs out the run no longer fails. The model is asked once more for an
answer with `tool_choice: none` — it keeps every tool result it already gathered, it just cannot
fetch more — and the reply is prefixed with *"Heads up — I ran out of steps on this one, so this
may be incomplete."* so the user is told it may be partial whatever the model wrote. Only if that
call fails or comes back empty does the old error surface. The same follow-up covers a model that
ends its turn without writing anything: one recovery call, never two, which previously reached the
user as a blank reply.

A tool result is not paid for once: it is appended to the conversation and re-sent on every later
iteration of the loop, so one oversized read costs its tokens again on each following turn. That is
what `MaxToolResultChars` exists to bound — individual tools should still return aggregates rather
than dumps; the cap is the backstop that makes the next careless tool safe. The truncation envelope
carries an explicit instruction not to re-call the tool, and `DuplicateToolCallLimit` refuses the
repeat if the model tries anyway.

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

Prompt caching reduces API costs by ~50% by caching the agent prompt and common documentation files. OpenRouter passes cache breakpoints through to **Claude-family models only** — other models ignore them and report zero cached tokens, so caching is safe to leave on for any slug but only pays off on Claude.

The breakpoint sits on the system prompt, and its lifetime comes from `OpenRouter:PromptCacheTtl` (default `"1h"`; clear it to fall back to the provider's 5 minutes). The system prompt and the tool schemas behind it are the layer shared across every user and every question in a guild, and a guild's questions are frequently more than five minutes apart, so the longer TTL's write premium pays for itself.

Two things sit in front of that breakpoint and must not move: the tool schemas serialize at position 0 of the request, ahead of the system message, so any change in their order invalidates the cache behind them. `ToolRegistry.GetEnabledTools()` therefore returns them sorted by name (ordinal) rather than in DI registration order — the symptom of getting this wrong is correct answers at roughly ten times the price, which no test catches unless one asserts the order.

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
| `Tools:SkillsPath` | `"docs/agents/skills/guild"` | Directory of skill files for the guild assistant; blank disables skills here. Nested key only — unlike the paths above it has no flat `Assistant:SkillsPath` forwarder. Ships empty, see [Skills](#skills) |
| `DocumentationBasePath` | `"docs/articles"` | Base directory for feature documentation, and the only directory `get_feature_documentation` may read — a feature name that resolves outside it is refused |
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
    "Model": "openrouter/auto",
    "ApiTimeoutMs": 30000,
    "MaxTokens": 512,
    "Temperature": 0.3,
    "AgentPromptPath": "docs/agents/assistant-agent.md",
    "DocumentationBasePath": "docs/articles",
    "ReadmePath": "README.md",
    "EnableDocumentationTools": true,
    "MaxToolRounds": 8,
    "ToolExecutionTimeoutMs": 10000,
    "MaxToolResultChars": 8000,
    "DuplicateToolCallLimit": 3,
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
| `DefaultModel` | `"openrouter/auto"` | Model slug used when a request does not name one |
| `MaxRetries` | `3` | Retry attempts for transient failures (HTTP 408/429/5xx, timeouts, network errors) |
| `TimeoutSeconds` | `300` | Per-attempt request timeout |
| `RetryBaseDelayMs` | `1000` | Base delay for exponential backoff (`baseDelay * 2^attempt`) |
| `EnablePromptCachingByDefault` | `true` | Add a cache breakpoint to the system prompt unless a request overrides it |
| `PromptCacheTtl` | `"1h"` | Lifetime of that breakpoint, as Anthropic spells it (`"5m"` or `"1h"`). Empty falls back to the provider default of 5 minutes |
| `AppUrl` | *(none)* | Site URL sent as `HTTP-Referer` (attribution on openrouter.ai rankings) |
| `AppTitle` | `"DiscordBot"` | Application name sent as `X-Title` |

```json
{
  "OpenRouter": {
    "BaseUrl": "https://openrouter.ai/api/v1/",
    "DefaultModel": "openrouter/auto",
    "MaxRetries": 3,
    "TimeoutSeconds": 300,
    "RetryBaseDelayMs": 1000,
    "EnablePromptCachingByDefault": true,
    "PromptCacheTtl": "1h",
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
- `IAgentTool` - One tool: a definition and an `InvokeAsync`. The unit a new tool is written as
- `IToolProvider` - A group of tools (definitions and execution); what the registry speaks
- `IToolRegistry` - Holds the registered providers and routes a call to the one that owns the tool
- `IPromptTemplate` - Loads and renders prompt templates with variable substitution

**Current Provider:**
- `OpenRouterLlmClient` (`DiscordBot.Agents.OpenRouter`) - OpenAI-compatible chat completions against OpenRouter, via an **owned typed `HttpClient`** and owned wire records (`ChatCompletionRequest.cs`, `ChatCompletionResponse.cs`). There is no LLM SDK dependency.
- `OpenRouterMessageMapper` - translates between the neutral `LlmRequest`/`LlmResponse` DTOs and the wire records.

Because OpenRouter fronts many providers, switching models is a configuration change (a different slug), not a code change.

**Wire-shape notes** (these differ from a native Anthropic Messages API and are handled inside the mapper):
- The system prompt is the **first message** in `messages`, not a top-level parameter.
- Tool results are `role: "tool"` messages carrying a `tool_call_id`, not content blocks appended to a user turn.
- Tool arguments arrive as a **JSON string**, which the mapper parses.
- `finish_reason` replaces `stop_reason`.
- Usage is reported as `prompt_tokens` / `completion_tokens`, with cached reads under `prompt_tokens_details.cached_tokens`, plus a real billed `cost` field.
- Every request that carries tools also sends `provider.require_parameters: true`, so routing cannot select a provider that lacks function-calling support.
- The flip side of `require_parameters`: a parameter the model itself does not support is refused (404, "No endpoints found that can handle the requested parameters") rather than dropped. Reasoning models such as the GPT-5 family do not accept `temperature`, so the client resends once without it and remembers the slug (`OpenRouterParameterSupportCache`, process lifetime) so later requests omit it up front. One failed round trip per such model per process.

**Future Providers:**
- Local models via Ollama
- Other providers following the `ILlmClient` interface

### Tool System

**Writing a tool** is one file implementing `IAgentTool` plus a `ToolCatalog` entry — no provider
class and no DI line; the assembly scan finds it, and the catalogue decides which assistant
advertises it. The helpers (`ToolInput`, `ToolResults`, `ToolJson`) are what make the tool's
argument reading and failure reporting match house convention by default. Full pattern, including
when a provider is still the right shape, in
[patterns.md § Agent Tool Authoring](../architecture/patterns.md#agent-tool-authoring).

**Tool Providers:**
- `GuildAgentToolProvider` / `DmAgentToolProvider` - The two adapters over individually authored
  `IAgentTool`s. Each keeps the scanned tools whose `ToolCatalog` scope matches its surface, and
  refuses a write the caller may not make before entering the tool
  - `save_note`, `search_notes`, `get_note`, `list_notes`, `delete_note` - The DM assistant's memory
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
- Holds every registered provider and routes a tool call to the one that owns it
- Advertises tools sorted by name, because their order is part of the prompt-cache prefix

**Tool Access:**
- `FilteredToolRegistry` narrows a registry to a guild's allow-list, resolved by
  `IToolAccessResolver` from `AssistantGuildSettings.EnabledTools` (empty = the house default set)
- It refuses a call to a tool outside the set as well as hiding it, because a model that saw the
  tool in an earlier cached prefix will sometimes call it anyway
- Per-caller permission is separate: `ToolContext.CanMutate` gates a tool that writes, rather than
  filtering the advertised list, which would give every permission level its own prompt-cache
  prefix. In a guild it is set from the caller's Discord permissions (Manage Server or
  Administrator); in DMs, which are owner-only, it is always true. A tool authored as `IAgentTool`
  declares `Mutation` and `AgentToolProvider` applies the check before entering it; the remaining
  hand-written providers check it themselves.

**Tool Specifications:**
- One page per tool in [`docs/tools/`](../tools/README.md): purpose, dependencies, the exact
  model-facing description, an input table, and every result shape — success and each expected
  failure. Written when a tool is touched rather than all at once, so the pages that exist are the
  ones that are true
- `ToolContractTests` asserts the house rules over every registered tool: name shape and uniqueness,
  a description between 40 and 600 characters, a well-formed object schema with every property
  described, a `ToolCatalog` entry, a mutation refused when the caller may not write, and a missing
  argument reported through `ToolResults` so `ToolOutcomes.Classify` counts it

**Prompt Surface:**
- `PromptSurface.Measure` puts a character and token count on what a surface advertises, measured
  through the real wire serialization. One Information line per surface at startup, and a per-tool
  panel on each guild's Assistant Metrics page
- It counts the skill-aware advertised set, not everything the registry holds: a tool behind an
  unloaded skill is not in the request and is not billed for

**Tool Telemetry:**
- Every tool call emits an `agent.tool {name}` span on the `DiscordBot.Agents` source, tagged with
  the tool name, call id, owning provider, result size and an outcome — `ok`, `failed_result`,
  `timeout`, `repeated_call`, `error` or `unknown_tool`
- The tools a question used are also stored on its interaction log row, which is what the metrics
  page's Tool Usage table counts, so it works whether or not a trace backend is deployed

### Skills

A tool is not free. Its schema is serialized into every request whether or not the question needs
it, and the prose explaining when to use it costs its tokens in the system prompt on the same terms.
A **skill** is how that stops being true for a tool the assistant rarely needs: one markdown file
naming some tools and carrying the instructions for using them, of which the model sees only a
one-line summary until it decides a request is actually about it and calls `load_skill`. The tools
a skill names are held back from the advertised tool array until then.

`load_skill` is an ordinary tool with an ordinary `ToolCatalog` entry — category **Skills**, both
surfaces, on by default — so it appears in a server's Tool Access checklist like any other, and an
admin who unticks it has turned skills off for that server's assistant. It disappears on its own
when a surface has no skill files, rather than costing its schema on every request for a mechanism
with nothing to load.

Skill files live in `docs/agents/skills/<surface>/`, one directory per assistant, pointed at by
`DmAssistant:SkillsPath` and `Assistant:Tools:SkillsPath` (blank on either disables skills there).
Two skills ship, both on the DM side:

| Skill | Loads |
|-------|-------|
| `moderation` | `get_moderation_cases`, `get_user_mod_history`, `search_audit_logs` |
| `analytics` | `get_server_activity_summary`, `get_command_analytics` |

Those five tools are no longer advertised to the DM assistant on every message; they arrive when the
owner asks something moderation- or analytics-shaped and the model loads the skill that owns them.
Everything else the surface offers — the documentation tools, the memory tools, the rest — is
advertised exactly as before. `docs/agents/skills/guild/` ships empty, and deliberately: the DM
assistant is multi-turn, so an activation is replayed on later turns and a skill is paid for once,
while the guild assistant is single-turn and would pay the loading round every time.

Adding a skill is one markdown file in the right directory. There is no catalogue entry, no DI
registration, and no code; the file is picked up without a restart, on the same cache terms as a
prompt file. The tools it names have to exist and be advertised on that surface already — a skill
can only ever un-hide something the assistant was allowed to use, never widen its reach, so a name
a guild has turned off is dropped and the model is never told it existed.

The file format and the authoring advice are in
[`docs/agents/skills/README.md`](../agents/skills/README.md); the mechanism — how the tool array is
re-composed, and what it costs the prompt cache — is
[patterns.md § Agent Skills](../architecture/patterns.md#agent-skills).

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
- **Feature Specs:** [Assistant Requirements](../requirements/assistant-requirements.md)
- **Implementation Plan:** [Assistant Implementation Plan](../requirements/assistant-implementation-plan.md)
- **Agent Prompt:** `docs/agents/assistant-agent.md`
- **OpenRouter Documentation:** https://openrouter.ai/docs
- **OpenRouter Model List:** https://openrouter.ai/models
- **OpenRouter Privacy:** https://openrouter.ai/privacy
