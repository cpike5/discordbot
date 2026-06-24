# Product Requirements Document
## Feature: Community Assistant Expansion (Assistant v2)

**Status:** Draft  
**Author:** Claude Code  
**Date:** 2026-06-24  
**Version:** 1.0

---

## 1. Overview

This document specifies the expansion of the community-facing AI assistant (the channel/@mention assistant served by `IAssistantService`) along three pillars, supported by a new cross-cutting safety foundation:

- **Foundation — Action-Tool Safety Layer** (Phase 0)
- **Pillar 1 — Action tools** (Phase 1 self-scoped, Phase 3 privileged)
- **Pillar 2 — Conversation & memory** (Phase 2)
- **Pillar 3 — Knowledge & richer reads** (Phase 1 reads, Phase 3 web)

The owner-only DM assistant (`IDmToolProvider` providers, `DmAssistantService`) is **not** the subject of this document except where its existing components are reused. Where this PRD says "the assistant," it means the community assistant.

### 1.1 Current State (baseline)

| Aspect | Today |
|---|---|
| Invocation | @mention in an allowed guild channel (`AssistantMessageHandler`) |
| Turn model | Single-turn; no conversation history passed to `AgentRunner` |
| Tools | Read-only: `DocumentationToolProvider`, `UserGuildInfoToolProvider`, `RatWatchToolProvider` |
| Tool context | `ToolContext` with `UserId`, `GuildId`, `ChannelId`, `MessageId`; **`UserRoles` unpopulated** |
| Guardrails | Consent (`assistant_usage`), guild enable, channel allowlist, per-user rate limit, cost logging |
| Master tool switch | `AssistantOptions.EnableDocumentationTools` (de-facto on/off for *all* tools) |

### 1.2 Design Principles

1. **Reuse, don't duplicate.** Action tools call existing services (`IReminderService`, soundboard/audio services, `IScheduledMessageService`, moderation services). The assistant is a thin natural-language adapter, not a new implementation of any feature.
2. **Least privilege by default.** A tool acts on the caller's own data unless the caller is proven authorized for more.
3. **Confirm before mutating.** Any state-changing action surfaces a confirmation step before it executes.
4. **Everything is audited.** Every action is attributable to the requesting member.
5. **Per-guild control.** Admins decide which capability tiers their community gets.
6. **Inherit existing guardrails.** Consent, enablement, channel scoping, rate limiting, and cost tracking are not re-implemented — new tools sit behind them.

---

## 2. Foundation — Action-Tool Safety Layer (Phase 0)

This layer must land before any action tool is exposed. It has no user-visible behavior on its own.

### 2.1 Role resolution into `ToolContext`

**Problem:** `ToolContext.UserRoles` is never populated for community requests, so no tool can gate on the caller's permissions.

**Requirement FR-S1:** `AssistantService.AskQuestionAsync` shall resolve the requesting member's guild roles and permission tier and populate `ToolContext` before invoking the agent.

- A new field `ToolContext.PermissionTier` (enum: `User`, `Moderator`, `Admin`, `SuperAdmin`) shall be added, derived using the **same** role-hierarchy logic used by command preconditions (`RequireModerator`, `RequireAdmin`) — not a parallel implementation.
- `ToolContext.UserRoles` (existing list) shall also be populated with the member's role names for tools that need finer detail.
- Resolution must handle the caller no longer being in the guild (treat as `User` / deny privileged tools).

### 2.2 Capability gating

**Requirement FR-S2:** A new base helper for action tool providers shall enforce, for each tool invocation:

1. **Guild context present** (`GuildId != 0`) — reuse existing guard pattern.
2. **Per-guild capability enabled** — see § 2.4.
3. **Permission tier sufficient** — self-scoped tools require `User`; privileged tools require `Moderator`/`Admin` as declared by the tool.
4. **Target scoping** — self-scoped tools must reject any target that is not the calling user (e.g. cannot set a reminder for someone else).

A tool that fails any check returns a structured, non-leaking error result (the same `CreateError` pattern used today), and the agent relays a friendly refusal.

### 2.3 Confirmation pattern for mutating actions

**Requirement FR-S3:** State-changing actions shall not execute on the first tool call. Instead the assistant shall present the proposed action and require explicit confirmation.

Two acceptable mechanisms (PRD chooses **A** for v2; B is an open question):

- **(A) In-conversation confirmation (chosen):** The tool returns a "preview" result describing exactly what will happen; the assistant asks the member to confirm in natural language; a second tool call with a `confirmed: true` argument executes. This requires Pillar 2 (memory) for multi-turn, OR a single-turn confirmation via Discord buttons.
- **(B) Discord component confirmation:** The action tool returns a payload that the handler renders as a Discord button ("Confirm" / "Cancel"), wired through the existing component framework (`ComponentIdBuilder`, `IInteractionStateService`). This works even without conversation memory.

**Decision:** Destructive/high-impact actions (mod actions, deleting data, scheduled-message changes) use **(B) Discord buttons** so they are robust without relying on the LLM to re-confirm. Low-impact self actions (set a reminder, play a sound) may execute directly with an undo affordance where cheap (e.g. reminders return a cancel button).

### 2.4 Per-guild capability toggles

**Requirement FR-S4:** Assistant capabilities shall be individually enableable per guild, following the existing `RatWatchSettings.PublicLeaderboardEnabled` precedent.

`AssistantGuildSettings` shall gain flags (nullable booleans defaulting to a global default when unset):

| Flag | Controls |
|---|---|
| `EnableActionTools` | Master switch for any action (mutating) tool |
| `EnableSelfActions` | Self-scoped actions (reminders, sound play, etc.) |
| `EnablePrivilegedActions` | Mod/admin actions via the assistant |
| `EnableConversationMemory` | Multi-turn memory in channels |
| `EnableWebKnowledge` | Web search/fetch tools |
| `PersonaPrompt` (string, nullable) | Optional per-guild persona text appended to the agent prompt |

Admins manage these on the existing **Assistant Settings** page (`/Guilds/AssistantSettings/{guildId}`).

### 2.5 Per-action audit

**Requirement FR-S5:** Every action-tool execution shall write an audit entry via `IAuditLogService` containing: actor user ID, guild ID, channel ID, tool name, sanitized parameters, target (if any), outcome (success/failure), and a correlation ID linking to the `AssistantInteractionLog` row for the parent question.

### 2.6 Per-tool execution safety

**Requirement FR-S6:** Action tools shall be bounded independently of the conversation:

- A per-tool execution timeout (config `ToolExecutionTimeoutMs`, already exists) shall be enforced around action tools (the current `AgentRunner` has no per-tool timeout — this must be added for action tools at minimum).
- Action tools shall count against a new **per-user action budget** (e.g. N actions per rate-limit window) separate from the question rate limit, so a single question cannot trigger unbounded mutations.

---

## 3. Pillar 1 — Action Tools

### 3.1 Self-scoped action tools (Phase 1)

These act only on the caller's own data. Tier required: `User`. Gated by `EnableSelfActions`.

| Tool | Backing service | Behavior | Confirmation |
|---|---|---|---|
| `create_reminder` | `IReminderService` | Create a personal reminder from natural-language time + message. Reuses existing NL time parsing. | Direct execute; returns a cancel button |
| `list_my_reminders` | `IReminderService` | List the caller's active reminders | Read-only |
| `cancel_reminder` | `IReminderService` | Cancel one of the caller's reminders by reference | Button confirm |
| `play_sound` | Soundboard/audio service | Play a named sound in the member's current voice channel (respects `RequireVoiceChannel`, `RequireAudioEnabled` equivalents) | Direct execute |
| `list_sounds` | Soundboard service | List/search available sounds (read) | Read-only |
| `set_my_tts_preset` / `get_my_tts_preset` | TTS preset services | Manage the caller's own TTS voice preset | Direct execute |

**Constraints:**
- `create_reminder` must enforce the same per-user reminder limits as the slash command (`ReminderOptions`).
- `play_sound` must enforce the same audio preconditions as `/play` (member in a voice channel, audio enabled for guild, file limits).
- All self tools must reject any attempt to specify a different target user.

### 3.2 Privileged action tools (Phase 3)

Tier required: `Moderator` or `Admin` (declared per tool). Gated by `EnablePrivilegedActions`. **All require Discord-button confirmation (FR-S3 mechanism B).**

| Tool | Backing service | Min tier | Notes |
|---|---|---|---|
| `warn_user` | Moderation action service | Moderator | Mirrors `/warn`; logged as a mod case |
| `timeout_user` / `mute_user` | Moderation action service | Moderator | Duration via NL; mirrors `/mute` |
| `purge_messages` | Moderation purge service | Moderator | Bounded by existing purge limits; explicit count required |
| `create_scheduled_message` | `IScheduledMessageService` | Admin | Mirrors `/schedule-create` |
| `toggle_scheduled_message` | `IScheduledMessageService` | Admin | Enable/disable an existing schedule |

**Constraints:**
- Privileged tools must re-validate the caller's tier at execution time (not just at preview time), in case roles changed.
- `ban`/`kick` are **out of scope for v2** (highest-impact, defer until confirmation UX is proven). This is an open question.
- The mod-action target and reason must be echoed in the confirmation so the moderator sees exactly what they're approving.

### 3.3 Action result presentation

- Successful actions return a concise confirmation the assistant relays in-channel ("✅ Reminder set for tomorrow at 9am: *check the oven*").
- Failures return a friendly, non-leaking reason ("I couldn't play that — you need to be in a voice channel first").

---

## 4. Pillar 2 — Conversation & Memory (Phase 2)

### 4.1 Multi-turn memory

**Requirement FR-C1:** When `EnableConversationMemory` is on for a guild, the community assistant shall support short-lived multi-turn conversations, reusing the DM assistant's proven memory machinery (`ConversationHistory` on `AgentContext`, sliding-window persistence).

**Scope of a "conversation":**
- Keyed by `(GuildId, ChannelId, UserId)` — or, if the mention occurs in a thread, by the thread ID.
- A conversation is continued when the same member mentions the bot again, or replies to the bot's message, within a configurable idle window (default 10 minutes).
- History is bounded by a sliding window (`MaxConversationMessages`, reuse existing option semantics) and a max age.

**Requirement FR-C2:** Community conversation memory shall be stored separately from the owner DM conversation store, and shall be subject to the existing interaction-log retention policy. It must be covered by the existing privacy/delete-data flows.

### 4.2 Persona

**Requirement FR-C3:** A guild admin may set an optional `PersonaPrompt` (bounded length, injection-filtered) that is appended to the agent system prompt for that guild, giving the assistant a community-specific voice. The persona must not be able to override safety instructions in the base agent prompt (base prompt takes precedence; persona is clearly delimited as untrusted-style guidance).

### 4.3 Conversation hygiene

- A member can say "forget that" / "start over" to clear the current conversation (maps to a `clear_conversation`-style tool, scoped to that member's channel conversation only).
- Conversations expire silently after the idle window; no orphaned state.

---

## 5. Pillar 3 — Knowledge & Richer Reads

### 5.1 Richer read tools (Phase 1)

Read-only, tier `User`, no confirmation. These extend the existing read providers.

| Tool | Returns |
|---|---|
| `get_my_reminders` | Caller's upcoming reminders (also usable standalone in Pillar 1) |
| `get_server_stats` | Public guild stats (member count, activity summary) from existing analytics snapshots |
| `get_soundboard_inventory` | Available sounds with metadata |
| `get_my_mod_status` | The caller's own warnings/cases (self only; mirrors "users can view own moderation history") |
| `get_leaderboard` | Existing Rat Watch / future leaderboards (extends current `RatWatchToolProvider`) |

### 5.2 Web knowledge (Phase 3)

**Requirement FR-K1:** When `EnableWebKnowledge` is on for a guild, the assistant may use a `web_search` and/or `web_fetch` tool, reusing the **already-hardened** `WebFetchTools` from the DM assistant (10s timeout, 512KB response cap, custom user agent) — promoted to a community-safe provider.

**Constraints:**
- Web tools are off by default (cost + safety).
- Web fetch must keep the existing SSRF-style protections (no internal hosts), enforce the size/time caps, and count against the per-user action budget.
- Results are summarized, not dumped; the assistant cites the source URL.

---

## 6. Configuration

New keys under `Assistant` (extending `AssistantOptions`) with safe defaults:

| Setting | Default | Description |
|---|---|---|
| `EnableActionTools` | `false` | Global master switch for action tools |
| `EnableSelfActionsByDefault` | `false` | Default for guilds that haven't set `EnableSelfActions` |
| `EnablePrivilegedActionsByDefault` | `false` | Default for `EnablePrivilegedActions` |
| `EnableConversationMemoryByDefault` | `false` | Default for `EnableConversationMemory` |
| `EnableWebKnowledgeByDefault` | `false` | Default for `EnableWebKnowledge` |
| `ConversationIdleWindowMinutes` | `10` | Idle timeout that ends a channel conversation |
| `MaxConversationMessages` | `10` | Sliding-window size for channel memory |
| `ActionsPerWindow` | `10` | Per-user action budget per rate-limit window |
| `MaxPersonaPromptLength` | `1000` | Bound on per-guild persona text |

Per-guild overrides live on `AssistantGuildSettings` (§ 2.4). Global `false` always wins (a guild cannot enable a capability the operator has globally disabled).

Model note: the assistant currently documents a `claude-3-5-sonnet` default; v2 should default the community assistant to a current Claude model and confirm pricing constants against the live model. (See [claude-api skill] / open question OQ-06.)

---

## 7. Phasing

| Phase | Deliverable | Gate to next phase |
|---|---|---|
| **0** | Safety layer: role plumbing, capability gating helper, per-action audit, confirmation (button) infra, per-guild settings + UI, action budget | Audit + gating verified by tests; no behavior change for end users |
| **1** | Self-scoped action tools (`create_reminder`, `cancel_reminder`, `play_sound`, TTS preset) + richer read tools | Self actions safe, scoped, audited; dogfooded in one guild |
| **2** | Conversation memory + persona | Memory expires cleanly; covered by delete-data; persona injection-safe |
| **3** | Privileged actions (warn/timeout/purge/schedule) + web knowledge | Mod confirmation UX proven; web tools within cost/safety budget |

Each phase is independently shippable and independently reversible via config.

---

## 8. Non-Functional Requirements

| ID | Requirement |
|---|---|
| NFR-01 | Total response latency with one action tool ≤ 15s (consistent with existing target) |
| NFR-02 | An action tool failure must never crash the response; it degrades to a friendly error (existing per-tool try/catch in `AgentRunner`) |
| NFR-03 | No new secret or internal identifier may be returned to users (existing sanitization rules apply) |
| NFR-04 | Per-guild cost and action counts must be visible in the existing Assistant Metrics dashboard |
| NFR-05 | All new tools must have unit tests for permission gating, target scoping, confirmation, and audit emission |
| NFR-06 | Prompt-injection attempts in the user message or persona must not escalate privilege or trigger unconfirmed mutations |

---

## 9. Open Questions

| ID | Question |
|---|---|
| OQ-01 | Confirmation UX: standardize on Discord buttons for *all* mutations, or allow NL confirmation for low-impact self actions? (PRD currently: buttons for privileged/destructive, direct+undo for low-impact self) |
| OQ-02 | Should `ban`/`kick` ever be assistant-accessible, or permanently slash-command-only? |
| OQ-03 | Conversation key: per-channel-per-user vs thread-first. Do we auto-create a thread for multi-turn to avoid channel clutter? |
| OQ-04 | Web knowledge: search provider choice and cost controls — which search backend, and what per-guild monthly cap? |
| OQ-05 | Should self-action tools work in DMs with the bot for non-owners (currently the community assistant ignores DMs entirely)? |
| OQ-06 | Confirm the target Claude model + pricing constants for the community assistant (the doc baseline references an older Sonnet). |
| OQ-07 | Do privileged actions require the member to *also* have the corresponding Discord permission (e.g. native Ban Members), or is the bot's role-tier sufficient? |

---

## 10. References

- [Architecture.md](Architecture.md) — component and data-flow design
- [Reference.md](Reference.md) — tool catalog, config keys, security notes
- [UserStories.md](UserStories.md) — acceptance criteria
- [ai-assistant.md](../ai-assistant.md) — current community assistant
- [assistant-tool-catalog.md](../../specs/assistant-tool-catalog.md) — existing/planned tool catalog
- [llm-abstraction-architecture.md](../../specs/llm-abstraction-architecture.md) — LLM provider + tool abstraction
- [authorization-policies.md](../authorization-policies.md) — role hierarchy used for tier resolution
- [reminder-system.md](../reminder-system.md), [soundboard.md](../soundboard.md), [scheduled-messages.md](../scheduled-messages.md) — backing services
</content>
