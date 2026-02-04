# DM Chat Assistant Requirements

> **Status:** Draft
> **Created:** 2026-02-03
> **Target Version:** v0.20.0

## Executive Summary

A Discord DM-based AI assistant that provides general-purpose conversational support via direct messages. Initially restricted to the bot owner with an unrestricted system prompt, with infrastructure to expand to other users later. Foundation for future MCP/Claude Code tooling integration.

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
- Token usage and cost estimates are tracked
- Latency is measured and recorded
- Daily aggregated metrics are available
- Logs are retained per existing retention policies

### 6. Configuration
Enable/disable toggle and prompt path configuration.

**Acceptance Criteria:**
- Feature can be enabled/disabled via configuration
- System prompt paths are configurable
- Configuration follows existing `IOptions<T>` pattern

---

## Future Features

| Feature | Description | Priority |
|---------|-------------|----------|
| Non-owner access | Restricted prompts for non-owner users | Medium |
| Multi-turn conversations | Session-based or persistent conversation history | Medium |
| MCP/Claude Code tooling | Dev tooling integration via MCP | High |
| Rate limiting | Per-user rate limits for non-owners | Low |
| Per-user prompts | Customizable prompts per user | Low |

---

## Out of Scope

- **Rate limiting** — Deferred until non-owner access is implemented
- **Production-to-dev communication** — Separate tooling phase
- **Multi-turn conversations** — Single-turn for MVP
- **Tool use** — Start with pure conversation, add tools later

---

## Technical Approach

### Architecture

| Component | Approach |
|-----------|----------|
| Service | New `IDmAssistantService` (separate from guild assistant) |
| Handler | DM message handler in bot event handlers |
| LLM | Reuse existing `ILlmClient` / `AnthropicLlmClient` |
| Prompts | New prompt file(s) in `docs/agents/` |
| Config | New `DmAssistant` section in appsettings |
| Storage | New entities for DM interaction logs/metrics |

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

### Configuration

```csharp
public class DmAssistantOptions
{
    public bool Enabled { get; set; } = false;
    public string OwnerSystemPromptPath { get; set; } = "docs/agents/dm-owner-agent.md";
    public string DefaultSystemPromptPath { get; set; } = "docs/agents/dm-assistant-agent.md";
    public string PlaceholderMessage { get; set; } = "DM assistant support is coming soon! Stay tuned.";
}
```

**appsettings.json:**
```json
{
  "DmAssistant": {
    "Enabled": true,
    "OwnerSystemPromptPath": "docs/agents/dm-owner-agent.md",
    "DefaultSystemPromptPath": "docs/agents/dm-assistant-agent.md",
    "PlaceholderMessage": "DM assistant support is coming soon! Stay tuned."
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
| EstimatedCostUsd | decimal | Cost estimate |

#### DmAssistantUsageMetrics

Daily aggregated metrics, similar structure to `AssistantUsageMetrics` but for DM interactions.

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Separate service | Different concerns from guild assistant (permissions, context, prompts) |
| Owner via Discord API | Built-in, reliable, no extra config needed |
| Skip rate limiting | Owner-only initially, add later when opening to others |
| Single-turn first | Simpler foundation, multi-turn can be added later |
| Same metrics detail | Consistency, cost tracking still valuable |
| Placeholder for non-owners | Friendly UX, signals feature is planned |

---

## Dependencies

- Existing `ILlmClient` / `AnthropicLlmClient` infrastructure
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
