# Implementation Plan
## Feature: Community Assistant Expansion (Assistant v2)

**Status:** Draft  
**Author:** Claude Code  
**Date:** 2026-06-24  
**Version:** 1.0

Concrete build plan for the spec in this folder. Each phase is independently shippable, independently reversible by config (every capability ships behind a global `false`), and gated by tests before the next begins. File references use the seams identified in [Architecture.md](Architecture.md).

---

## Prerequisite — Fix the retired model (~½ day, ships alone)

Unrelated to the new features but **blocking and trivial**: the configured `claude-3-5-sonnet-20241022` was retired 2025-10-28 and is no longer a valid model ID.

1. `AssistantOptions`: set `Model` → `claude-sonnet-4-6`; switch any `budget_tokens` thinking config to `thinking: {type: "adaptive"}`.
2. Leave cost constants ($3/$15) unchanged — Sonnet 4.6 pricing is identical, so the metrics dashboard stays accurate.
3. Confirm prompt caching still engages (`usage.cache_read_input_tokens > 0`): keep caching agent prompt **+** common docs together so the prefix clears Sonnet's 2048-token minimum.
4. Verify the existing assistant still answers a mention, then proceed to Phase 0.

**Exit:** existing read-only assistant works on the current model; no feature change.

---

## Phase 0 — Action-Tool Safety Layer

No user-visible behavior. This is the scaffolding every later phase depends on. Build in order:

### 0.1 Permission tier resolution
- Extract the role-hierarchy logic behind `RequireModerator` / `RequireAdmin` into a shared `IPermissionTierResolver` (`Infrastructure/Services/Permissions/PermissionTierResolver.cs`). **Do not** re-implement the hierarchy — command preconditions and the assistant must agree by construction.
- Add `PermissionTier` (enum `User|Moderator|Admin|SuperAdmin`) to `ToolContext` (`Core/DTOs/LLM/ToolContext.cs`); populate it **and** `UserRoles` in `AssistantService.AskQuestionAsync` where `ToolContext` is built (currently both are left empty for community requests). The handler holds the `SocketGuildUser`; pass roles down. Caller-left-guild → `User`.

### 0.2 `ActionToolBase`
`Infrastructure/Services/LLM/ActionToolBase.cs` — wraps every action-tool execution with, in order:
1. `GuildId != 0` guard (existing pattern).
2. Per-guild capability check (§ 0.3).
3. Permission-tier check against the tool's declared minimum tier.
4. Target scoping — self-scoped tools assert `targetUserId == context.UserId`.
5. Per-user **action budget** (`IMemoryCache`, mirrors the existing question rate-limit), key `(GuildId, UserId)`, `ActionsPerWindow`.
6. Per-tool **timeout** (`ToolExecutionTimeoutMs`) — `AgentRunner` has none today.
7. Audit emission via `IAuditLogService` (actor, guild, channel, tool, sanitized params, target, outcome, correlation ID = parent `AssistantInteractionLog.Id`).

Failures return the existing `CreateError(...)` shape so the agent relays a clean refusal and the loop continues.

### 0.3 Capability service
`IAssistantCapabilityService` (+ impl) centralizing `effective = globalDefault AND (perGuild ?? globalDefault)` — global `false` always wins. Follows the `RatWatchSettings.PublicLeaderboardEnabled` precedent.

### 0.4 Settings, config, migration
- Add nullable capability flags + `PersonaPrompt` to `AssistantGuildSettings` (`Core/Entities/`).
- One migration per provider: **SQLite** → `Migrations/Sqlite`, **PostgreSQL** → `Migrations/Postgresql` (`--context` required per CLAUDE.md).
- New keys on `AssistantOptions` (see Reference.md § 3).
- Capability toggles + persona field on the Assistant Settings page (`/Guilds/AssistantSettings/{guildId}`, RequireAdmin).

### 0.5 Confirmation infrastructure
- `AssistantConfirmComponentModule` (`Bot/Commands/`) — handles the "Confirm" / "Cancel" button via `ComponentIdBuilder` + `IInteractionStateService` (15-min expiry). The action descriptor is stashed on tool call and executed on click, with tier **re-validated at click time**.

**Exit criteria:** unit tests prove tier resolution matches the command preconditions; gating / scoping / budget / timeout / audit all fire; a synthetic prompt-injection attempt cannot flip `PermissionTier` or trigger an unconfirmed mutation. No end-user behavior change.

---

## Phase 1 — Self-scoped actions + richer reads

All tier `User`, gated by `EnableSelfActions` (under `EnableActionTools`). New providers register as `IToolProvider` in `Bot/Extensions/AssistantServiceExtensions.cs` (one DI line each).

### 1.1 `SelfActionToolProvider` (`Bot/Services/LLM/Providers/`)
| Tool | Backing service | Confirmation (D-01) |
|---|---|---|
| `create_reminder` | `IReminderService` (reuse NL time parse; enforce `ReminderOptions` per-user limits) | direct + cancel button |
| `cancel_reminder` | `IReminderService` | button |
| `play_sound` | soundboard/audio service (same preconditions as `/play`) | direct |
| `set_my_tts_preset` / `get_my_tts_preset` | TTS preset service | direct / read |

Every self tool hard-rejects a non-self target via `ActionToolBase`.

### 1.2 `RicherReadsToolProvider`
`get_my_reminders`, `get_server_stats` (existing analytics snapshots), `get_soundboard_inventory`, `get_my_mod_status` (self only), `get_leaderboard` (extends the existing RatWatch reads). Read-only, no confirmation.

**Exit criteria:** integration test mention → tool → service for `create_reminder` and `play_sound`; self-target rejection covered; dogfood in one guild before Phase 2.

---

## Phase 2 — Conversation & memory

### 2.1 Store
- `AssistantConversationMessage` entity (parallels `DmConversationMessage`), keyed `(guild, channel, user)` (Decision D-03); migration both providers.
- Reply-chains (user replies to the bot's message) also continue a conversation.

### 2.2 Wiring
- Pass `ConversationHistory` on `AgentContext` in `AssistantService` (the DM path already does this — reuse the machinery), with sliding window (`MaxConversationMessages`) + idle expiry (`ConversationIdleWindowMinutes`).
- Cleanup background task prunes expired rows (reuse the retention/cleanup background-service pattern).
- A "forget that" / "start over" intent clears the caller's conversation in that channel only.

### 2.3 Persona
- Apply `PersonaPrompt` appended to the agent system prompt, injection-filtered + length-bounded (`MaxPersonaPromptLength`); base safety prompt always takes precedence.

### 2.4 Privacy
- Wire the new store into `PrivacyModule` export-data / delete-data **from the start**.

> Thread auto-creation stays an opt-in per-guild setting, deferred unless prioritized.

**Exit criteria:** conversations expire cleanly with no orphaned state; delete-data removes memory and export-data includes it; persona cannot override safety rules.

---

## Phase 3 — Privileged actions + web knowledge

### 3.1 `PrivilegedActionToolProvider`
Gated by `EnablePrivilegedActions`; **all require Discord-button confirmation; both bot tier AND native Discord permission (Decision D-07)**, resolved through the same preconditions the slash commands use.

| Tool | Min tier | Native permission |
|---|---|---|
| `warn_user` | Moderator | (mod) |
| `timeout_user` / `mute_user` | Moderator | Moderate Members |
| `purge_messages` | Moderator | Manage Messages (bounded by existing purge limits) |
| `create_scheduled_message` / `toggle_scheduled_message` | Admin | (admin) |

`ban` / `kick` intentionally **excluded** in v2 (Decision D-02).

### 3.2 `CommunityWebKnowledgeToolProvider`
- Use Claude's **native server-side `web_search` / `web_fetch`** (Decision D-04) — no third-party backend.
- Off by default per guild (`EnableWebKnowledge`); counts against the action budget; per-guild monthly USD cap surfaced on the Assistant Metrics dashboard. Retain hardened `WebFetchTools` for user-pasted URLs.

**Exit criteria:** mod-action confirmation flow proven across a few guilds; web tools within cost/safety budget.

---

## Cross-cutting

- **Testing:** follow [testing-guide.md](../testing-guide.md) and the Not-X `test-constraints` style. Every new tool needs gating / scoping / confirmation / audit tests plus an adversarial prompt-injection test. Privacy tests for Phase 2.
- **Rollout:** all capabilities ship behind global `false`. An existing deployment behaves identically until an operator enables `EnableActionTools` and an admin opts a guild in. Each phase reversible by config.
- **Agent-definition maintenance (CLAUDE.md rule):** update `.claude/agents/ai-assistant.*` (new providers, safety layer, conversation memory) as part of the work; add the new tools to the assistant's own knowledge via the [assistant-feature-updates.md](../assistant-feature-updates.md) process so it can answer questions about itself.

---

## Sequencing summary

| Step | Ships | Reversible by |
|---|---|---|
| Prerequisite | Model fix | n/a (bugfix) |
| Phase 0 | Safety layer (invisible) | n/a (no surface) |
| Phase 1 | Self actions + reads | `EnableActionTools` / `EnableSelfActions` |
| Phase 2 | Conversation + persona | `EnableConversationMemory` |
| Phase 3 | Privileged actions + web | `EnablePrivilegedActions` / `EnableWebKnowledge` |
