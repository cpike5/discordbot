# Architecture
## Feature: Community Assistant Expansion (Assistant v2)

**Status:** Draft  
**Author:** Claude Code  
**Date:** 2026-06-24  
**Version:** 1.0

---

## 1. Component Overview

The community assistant flows through these existing components (unchanged unless noted):

```
Discord @mention
   │
   ▼
AssistantMessageHandler                 (Bot/Handlers/AssistantMessageHandler.cs)
   │  guards: global enable, guild enable, channel allowlist,
   │          consent (assistant_usage), per-user rate limit
   ▼
IAssistantService.AskQuestionAsync      (Infrastructure/Services/AssistantService.cs)
   │  builds AgentContext + ToolContext  ◄── (CHANGED) role resolution, history
   ▼
IAgentRunner.RunAsync                   (Infrastructure/Services/LLM/AgentRunner.cs)
   │  agentic loop, executes tools via registry
   ▼
IToolRegistry  ──► IToolProvider(s)     (community = IToolProvider; DM = IDmToolProvider)
   │
   ├─ existing read providers: Documentation, UserGuildInfo, RatWatch
   └─ NEW providers: SelfAction, PrivilegedAction, RicherReads, WebKnowledge
                          │
                          ▼
              ActionToolBase (NEW)        — gating, scoping, confirmation, audit
                          │
                          ▼
   existing services: IReminderService, Soundboard/Audio, IScheduledMessageService,
                      Moderation services, WebFetchTools (promoted)
```

### Scoping mechanism (unchanged, reused)

A tool provider is **community-facing** if it implements `IToolProvider`, and **owner-DM-only** if it implements the marker `IDmToolProvider : IToolProvider`. The community `ToolRegistry` is injected with `IEnumerable<IToolProvider>` and therefore never sees DM-only providers. New community providers are registered as `IToolProvider` in `Bot/Extensions/AssistantServiceExtensions.cs`.

> **Master switch note:** Today `AssistantOptions.EnableDocumentationTools` is the de-facto on/off for *all* community tools (`AssistantService` passes `ToolRegistry = null` when false). v2 keeps this as the documentation switch but adds capability-specific gates (§ 4) so action tools have independent control.

---

## 2. The Action-Tool Safety Layer (Phase 0)

This is the foundation everything else depends on. Four parts:

### 2.1 Role/tier resolution into `ToolContext`

**Change:** `AssistantService.AskQuestionAsync` currently builds `ToolContext` with only `UserId/GuildId/ChannelId/MessageId` and leaves `UserRoles` empty. We add resolution of the caller's guild roles and permission tier.

- Add `ToolContext.PermissionTier` (enum `User|Moderator|Admin|SuperAdmin`).
- Derive the tier using the **same** logic backing `RequireModerator` / `RequireAdmin` preconditions — extract that into a shared resolver (`IPermissionTierResolver`) so command preconditions and the assistant agree by construction. Do **not** re-implement role hierarchy.
- Populate `ToolContext.UserRoles` with role names too.
- The handler has the `SocketGuildUser`; pass roles down to the service (or resolve in the service from the guild cache). Caller-left-guild → `User` tier.

### 2.2 `ActionToolBase` — gating + scoping helper

A base class for action tool providers that wraps every tool execution with:

1. `GuildId != 0` guard (existing pattern).
2. Per-guild capability check (§ 4) for the tool's declared capability.
3. Permission-tier check against the tool's declared minimum tier.
4. Target scoping: self-scoped tools assert `targetUserId == context.UserId`.
5. Per-user action-budget check (new counter, separate from question rate limit).
6. Per-tool timeout wrapper (the current `AgentRunner` has no per-tool timeout — `ActionToolBase` adds one using `ToolExecutionTimeoutMs`).
7. Audit emission on completion (§ 2.4).

Failures return the existing `CreateError(...)` structured result, so the agent relays a clean refusal and the loop continues.

### 2.3 Confirmation pipeline

Two paths (see PRD § 2.3 decision):

- **Buttons (privileged/destructive):** The action tool returns a `pending_confirmation` result carrying a token; `AssistantMessageHandler` (or a small post-processor) renders a Discord button via `ComponentIdBuilder` and stashes the action descriptor in `IInteractionStateService` (15-min expiry). A new `AssistantConfirmComponentModule` handles the button click, re-validates the tier, executes the underlying service call, and writes the audit entry.
- **Direct + undo (low-impact self):** The tool executes immediately and returns an affordance (e.g. reminders return a cancel button) — no separate confirmation round trip.

This keeps destructive actions robust even though the channel assistant may be single-turn (Phase 1 ships before conversation memory in Phase 2).

### 2.4 Audit

`ActionToolBase` writes via `IAuditLogService` (the same fluent builder used elsewhere; `BotManagementToolProvider` already *reads* audit logs, so the dependency direction is established). Entry fields: actor, guild, channel, tool name, sanitized params, target, outcome, correlation ID = the `AssistantInteractionLog.Id` of the parent question.

---

## 3. Data & State Changes

### 3.1 `AssistantGuildSettings` (extend existing entity)

Add nullable capability flags + persona (nullable → fall back to global default):

```
EnableActionTools?         bool
EnableSelfActions?         bool
EnablePrivilegedActions?   bool
EnableConversationMemory?  bool
EnableWebKnowledge?        bool
PersonaPrompt?             string (<= MaxPersonaPromptLength)
```

Requires one migration per provider set (**SQLite** → `Migrations/Sqlite`, **PostgreSQL** → `Migrations/Postgresql`; `--context` required per CLAUDE.md).

### 3.2 Community conversation memory (Phase 2)

- New entity `AssistantConversationMessage` (parallels `DmConversationMessage`) keyed by `(GuildId, ChannelId or ThreadId, UserId)` with `Role`, `Content`, `Timestamp`.
- Sliding window (`MaxConversationMessages`) + idle expiry (`ConversationIdleWindowMinutes`) enforced on read; a cleanup background task prunes expired rows (reuse the retention/cleanup background-service pattern).
- Must be included in the existing privacy **delete-data** / **export-data** flows (`PrivacyModule`) — community memory is user data.

### 3.3 Action budget counter

In-memory `IMemoryCache` counter per `(GuildId, UserId)` over the rate-limit window, mirroring how the question rate limit is tracked today. No persistence required.

---

## 4. Capability Gating Resolution

For a given `(guild, capability)` the effective enabled state is:

```
effective = globalDefault AND (perGuild ?? globalDefault)
```

- `globalDefault` comes from `AssistantOptions` (e.g. `EnableActionTools`, `EnableSelfActionsByDefault`).
- A globally `false` capability can never be enabled per guild (operator override always wins).
- `EnableActionTools` is a master gate above `EnableSelfActions` / `EnablePrivilegedActions`.

A small `IAssistantCapabilityService` centralizes this so providers, the handler, and the settings UI ask the same question the same way.

---

## 5. AgentRunner Interaction

The agentic loop (`AgentRunner.RunAsync`) is reused as-is with two notes:

- It executes all tool calls in a request **sequentially in a foreach** with per-call try/catch (a failing tool returns `IsError`, not a crash). Action tools rely on this for graceful degradation.
- It has **no per-tool timeout** — `ActionToolBase` adds one around action tool bodies so a slow service call cannot stall the whole response. (A broader fix to `AgentRunner` is possible but out of scope; the base-class wrapper is sufficient for v2.)
- Community `MaxToolCallIterations` stays `AssistantOptions.MaxToolCallsPerQuestion`. Confirmation-via-button does **not** consume loop iterations because execution happens later in the component handler, not inside the loop.

---

## 6. New / Changed File Inventory

### New
| Path | Purpose |
|---|---|
| `Core/Interfaces/LLM/IPermissionTierResolver.cs` | Shared role-tier resolution contract |
| `Infrastructure/Services/Permissions/PermissionTierResolver.cs` | Implementation (shared with command preconditions) |
| `Infrastructure/Services/LLM/ActionToolBase.cs` | Gating, scoping, budget, timeout, audit wrapper |
| `Core/Interfaces/LLM/IAssistantCapabilityService.cs` + impl | Effective capability resolution |
| `Bot/Services/LLM/Providers/SelfActionToolProvider.cs` | Reminders, sound play, TTS preset (Phase 1) |
| `Bot/Services/LLM/Providers/RicherReadsToolProvider.cs` | My reminders, server stats, soundboard inventory, my mod status (Phase 1) |
| `Bot/Services/LLM/Providers/PrivilegedActionToolProvider.cs` | Warn/timeout/purge/schedule (Phase 3) |
| `Bot/Services/LLM/Providers/CommunityWebKnowledgeToolProvider.cs` | Promotes hardened `WebFetchTools` to community (Phase 3) |
| `Bot/Commands/AssistantConfirmComponentModule.cs` | Handles confirmation button clicks |
| `Core/Entities/AssistantConversationMessage.cs` | Channel conversation memory (Phase 2) |
| Migrations (Sqlite + Postgresql) | Settings flags + conversation table |

### Changed
| Path | Change |
|---|---|
| `Core/DTOs/LLM/ToolContext.cs` | Add `PermissionTier`; populate `UserRoles` |
| `Infrastructure/Services/AssistantService.cs` | Resolve tier/roles; pass conversation history (Phase 2); correlation ID |
| `Bot/Handlers/AssistantMessageHandler.cs` | Pass guild-user roles; render confirmation buttons; continue conversations (Phase 2) |
| `Core/Entities/AssistantGuildSettings.cs` | Capability flags + persona |
| `Bot/Extensions/AssistantServiceExtensions.cs` | Register new `IToolProvider`s + new services |
| `AssistantOptions` (Infrastructure/Configuration) | New config keys |
| Assistant Settings page (`Pages/Guilds/AssistantSettings`) | Capability toggles + persona field |
| Assistant Metrics page | Surface action counts |
| `PrivacyModule` / data-export & delete | Include community conversation memory |

---

## 7. Security Design

| Threat | Mitigation |
|---|---|
| Prompt injection escalates privilege | Tier is resolved from Discord roles server-side and enforced in `ActionToolBase` — the LLM cannot set its own tier; tool args cannot change `context.PermissionTier` |
| Self tool acts on another user | Target-scoping assertion (`targetUserId == context.UserId`) in `ActionToolBase` |
| Confirmation bypass | Privileged/destructive actions execute only in the component handler after a button click, with tier re-validated at execution time |
| Persona overrides safety rules | Base agent prompt takes precedence; persona is length-bounded, injection-filtered, and delimited as untrusted guidance |
| Web SSRF / data exfil | Reuse hardened `WebFetchTools` (timeout, size cap, host restrictions); off by default |
| Cost/abuse blowup | Per-user action budget + existing question rate limit + per-guild cost threshold + capability kill switches |
| Sensitive data leakage | Existing sanitization rules (no secrets, no internal IDs, mask emails) apply unchanged |

---

## 8. Risk Register

| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-01 | Tier resolver drifts from command preconditions | Med | High | Single shared `IPermissionTierResolver` used by both |
| R-02 | LLM hallucinates an action the user didn't ask for | Med | High | Confirmation for mutations; per-action audit; action budget |
| R-03 | Conversation memory leaks across users/channels | Low | High | Strict `(guild, channel/thread, user)` key; covered by tests |
| R-04 | Web knowledge cost spikes | Med | Med | Off by default; per-guild cap; action budget |
| R-05 | Slow service call stalls responses | Med | Med | Per-tool timeout in `ActionToolBase` |
| R-06 | Memory store grows unbounded | Low | Med | Sliding window + idle expiry + cleanup background task |
| R-07 | Privacy non-compliance (memory not deletable) | Low | High | Wire into existing delete-data/export-data flows from the start |

---

## 9. Testing Strategy

- **Unit:** tier resolution; `ActionToolBase` gating/scoping/budget/timeout/audit; each tool's happy path + refusal path; capability resolution (`global AND perGuild`).
- **Injection:** adversarial questions attempting privilege escalation and unconfirmed mutation must produce refusals, not actions.
- **Integration:** end-to-end mention → tool → service for `create_reminder` and `play_sound`; confirmation button flow for a privileged action.
- **Privacy:** delete-data removes community conversation memory; export-data includes it.
- Follow the patterns in [testing-guide.md](../testing-guide.md) and the test-constraints style used by the Not-X feature.
</content>
