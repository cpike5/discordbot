# DM Chat Assistant Requirements

> **Status:** Implemented (MVP + Claude Code extension)
> **Created:** 2026-02-03
> **Updated:** 2026-03-23
> **Target Version:** v0.20.0

## Executive Summary

A Discord DM-based AI assistant that provides multi-turn conversational support via direct messages. Initially restricted to the bot owner with an unrestricted system prompt, with infrastructure to expand to other users later. Foundation for future MCP/Claude Code tooling integration.

---

## Problem Statement

The existing AI assistant is guild/channel-based and constrained to answering questions about bot features. The bot owner needs a personal, general-purpose assistant accessible via Discord DMs that can later be extended with dev tooling capabilities.

## Primary Purpose

Provide an unrestricted, general-purpose AI assistant via Discord DMs for the bot owner.

---

## Target Users

| User Type | Access | Experience |
|-----------|--------|------------|
| Bot Owner | Full | Unrestricted general-purpose assistant |
| Non-Owners | Placeholder | "DM support coming soon" message |

---

## Core Features (MVP)

### 1. DM Message Handler
Detect incoming DMs to the bot and route to DM assistant service.

**Acceptance Criteria:**
- Bot receives and processes DM messages
- DMs are distinguished from guild channel messages
- Handler integrates with existing bot event infrastructure

### 2. Owner Detection
Use Discord.NET `GetApplicationInfoAsync()` to identify application owner.

**Acceptance Criteria:**
- Owner is identified via Discord application info API
- No additional configuration required for owner identification
- Owner check is cached to avoid repeated API calls

### 3. Owner System Prompt
General-purpose, unrestricted prompt stored in `docs/agents/dm-owner-agent.md`.

**Acceptance Criteria:**
- Prompt is not constrained to bot features
- Prompt allows general-purpose conversation
- Prompt is loaded from configurable file path

### 4. Non-Owner Placeholder
Friendly response indicating DM support is coming soon.

**Acceptance Criteria:**
- Non-owner DMs receive a polite placeholder message
- Message indicates feature is coming soon
- No error or silent failure for non-owners

### 5. Logging & Metrics
Same detail level as existing assistant (tokens, cost, latency, interaction logs).

**Acceptance Criteria:**
- All DM interactions are logged with full detail
- Token usage and costs are tracked (OpenRouter reports the billed cost per call; configured rates are a fallback)
- Latency is measured and recorded
- Daily aggregated metrics are available
- Logs are retained per existing retention policies

### 6. Configuration
Enable/disable toggle and prompt path configuration.

**Acceptance Criteria:**
- Feature can be enabled/disabled via configuration
- System prompt paths are configurable
- Configuration follows existing `IOptions<T>` pattern

### 7. Conversation History
Maintain a sliding-window conversation history per user, stored in the database.

**Acceptance Criteria:**
- Each user has a single conversation thread (sliding window, not session-based)
- Maximum messages retained is configurable via `MaxConversationMessages` (default 20)
- History is loaded before each LLM call and included in the messages array
- Both user messages and assistant responses are stored
- Oldest messages are trimmed when the limit is exceeded
- History persists across bot restarts

---

## Future Features

| Feature | Description | Priority |
|---------|-------------|----------|
| Non-owner access | Restricted prompts for non-owner users | Medium |
| ~~MCP/Claude Code tooling~~ | **Implemented as Mogwai** — see [mogwai.md](../articles/mogwai.md) | Done |
| Rate limiting | Per-user rate limits for non-owners | Low |
| Per-user prompts | Customizable prompts per user | Low |

---

## Out of Scope (MVP)

- **Rate limiting** — Deferred until non-owner access is implemented
- **Production-to-dev communication** — Separate tooling phase
- **Tool use** — Deferred from MVP; implemented as part of Mogwai (see [mogwai.md](../articles/mogwai.md))

---

## Technical Approach

### Architecture

| Component | Approach |
|-----------|----------|
| Service | `IDmAssistantService` / `DmAssistantService` (separate from guild assistant) |
| Handler | `DmAssistantMessageHandler` — responds to Discord DM events; handles response chunking (split ≤2000-char chunks or `.md` file attachment for long responses) |
| LLM | Reuses existing `ILlmClient` / `OpenRouterLlmClient` (OpenAI-compatible chat completions via OpenRouter; no LLM SDK). `DmAssistant:Model` is an OpenRouter slug, default `anthropic/claude-sonnet-4`; the Mogwai compose file overrides it to `anthropic/claude-haiku-4.5` |
| Tool integration | `ClaudeCodeToolProvider` implements `IDmToolProvider`; registered as scoped DI — no changes to `AgentRunner` or `ToolRegistry` |
| Prompts | `docs/agents/dm-owner-agent.md` — includes guidance on when to use Claude Code vs answer directly |
| Config | `DmAssistant` section (base DM assistant) + `OpenRouter` section (API key, base URL, retries) + `Mogwai` section (Claude Code extension) in appsettings |
| Storage | `DmConversationMessage`, `DmAssistantInteractionLog`, `DmAssistantUsageMetrics` entities; Claude Code session IDs are in-memory only (no DB entities) |

### Service Interface

```csharp
public interface IDmAssistantService
{
    Task<DmAssistantResponse> ProcessMessageAsync(
        ulong userId,
        string message,
        CancellationToken cancellationToken = default);

    Task<bool> IsOwnerAsync(ulong userId);
}
```

> **Conversation flow:** The service loads the user's recent conversation history (up to `MaxConversationMessages`) before each LLM call, builds the messages array with history + current message, calls the LLM, then saves both the user message and assistant response. Messages exceeding the limit are trimmed (oldest first).

### Configuration

```csharp
public class DmAssistantOptions
{
    public bool Enabled { get; set; } = false;
    public string OwnerSystemPromptPath { get; set; } = "docs/agents/dm-owner-agent.md";
    public string DefaultSystemPromptPath { get; set; } = "docs/agents/dm-assistant-agent.md";
    public string PlaceholderMessage { get; set; } = "DM assistant support is coming soon! Stay tuned.";
    public int MaxConversationMessages { get; set; } = 20;
}
```

**appsettings.json:**
```json
{
  "DmAssistant": {
    "Enabled": true,
    "OwnerSystemPromptPath": "docs/agents/dm-owner-agent.md",
    "DefaultSystemPromptPath": "docs/agents/dm-assistant-agent.md",
    "PlaceholderMessage": "DM assistant support is coming soon! Stay tuned.",
    "MaxConversationMessages": 20
  }
}
```

### Data Storage

#### DmAssistantInteractionLog

| Field | Type | Description |
|-------|------|-------------|
| Id | long | Primary key |
| Timestamp | DateTime | When message was received |
| UserId | ulong | Discord user ID |
| IsOwner | bool | Whether user is bot owner |
| Message | string | User's message (max 2000 chars) |
| Response | string | Assistant's response (max 2000 chars) |
| InputTokens | int | Input token count |
| OutputTokens | int | Output token count |
| CachedTokens | int | Cached token count |
| LatencyMs | int | Response latency |
| Success | bool | Whether request succeeded |
| ErrorMessage | string? | Error details if failed |
| EstimatedCostUsd | decimal | Cost. OpenRouter's billed `usage.cost` is recorded when reported; the configured per-million rates are a fallback |

#### DmAssistantUsageMetrics

Daily aggregated metrics, similar structure to `AssistantUsageMetrics` but for DM interactions.

#### DmConversationMessage

| Field | Type | Description |
|-------|------|-------------|
| Id | long | Primary key |
| UserId | ulong | Discord user ID |
| Role | string | "user" or "assistant" |
| Content | string | Message content |
| Timestamp | DateTime | When message was created |

**Index:** Composite index on `(UserId, Timestamp)` for efficient history retrieval.

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Separate service | Different concerns from guild assistant (permissions, context, prompts) |
| Owner via Discord API | Built-in, reliable, no extra config needed |
| Skip rate limiting | Owner-only initially, add later when opening to others |
| Sliding window history | Single conversation thread per user with configurable message limit; simpler than session-based and sufficient for owner-only use |
| Same metrics detail | Consistency, cost tracking still valuable |
| Placeholder for non-owners | Friendly UX, signals feature is planned |

---

## Dependencies

- Existing `ILlmClient` / `OpenRouterLlmClient` infrastructure
- Existing prompt loading utilities
- Discord.NET DM message handling
- EF Core for data storage

---

## Security Considerations

- **Owner verification** — Must use Discord API, not config-based user ID
- **Prompt injection** — Owner prompt should still include basic safety guidelines
- **Logging** — DM content is logged; consider privacy implications
- **API key exposure** — System prompt must not expose credentials

---

## Testing Strategy

- Unit tests for `IDmAssistantService`
- Unit tests for owner detection logic
- Integration tests for DM message handling
- Mock LLM responses for deterministic tests

---

## Related Documentation

- [AI Assistant](../articles/ai-assistant.md) — Existing guild-based assistant
- [Service Architecture](../articles/service-architecture.md) — Service patterns
- [Database Schema](../articles/database-schema.md) — Entity patterns

---

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2026-02-03 | 0.1 | Initial draft from requirements gathering |
| 2026-03-05 | 0.2 | Added conversation history (sliding window) to MVP scope |
| 2026-03-23 | 0.3 | Updated status to Implemented; reflected Mogwai Claude Code extension (ClaudeCodeToolProvider, response chunking, MogwaiOptions); marked MCP/Claude Code future feature as Done |
| 2026-08-30 | 0.4 | LLM integration migrated from the Anthropic SDK to OpenRouter (`OpenRouterLlmClient`, `OpenRouter` config section, OpenRouter model slugs). The `claude` CLI used by Mogwai is unaffected and still reads `ANTHROPIC_API_KEY`. |
