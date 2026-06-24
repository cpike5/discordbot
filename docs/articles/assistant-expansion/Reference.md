# Reference
## Feature: Community Assistant Expansion (Assistant v2)

**Status:** Draft  
**Date:** 2026-06-24

Technical reference for the expanded community assistant — tool catalog, permission tiers, configuration keys, per-guild settings, audit/observability, and security notes. For design rationale see [Architecture.md](Architecture.md); for requirements see [PRD.md](PRD.md).

---

## 1. Permission Tiers

Resolved server-side from the caller's Discord/guild roles via `IPermissionTierResolver` (shared with command preconditions) and carried on `ToolContext.PermissionTier`.

| Tier | Source | Can use |
|---|---|---|
| `User` | Any guild member | Read tools, self-scoped action tools |
| `Moderator` | Moderator role-tier | + moderation action tools (warn, timeout, purge) |
| `Admin` | Admin role-tier | + admin action tools (scheduled messages) |
| `SuperAdmin` | SuperAdmin | All; bypasses per-guild capability gates only where the operator allows |

Tier is **never** derived from the message content or tool arguments. Prompt injection cannot alter it.

---

## 2. Tool Catalog

Legend — **Mut**: mutating; **Conf**: confirmation required; **Cap**: governing per-guild capability; **Tier**: minimum tier.

### 2.1 Read tools (existing + new)

| Tool | Mut | Tier | Cap | Description |
|---|---|---|---|---|
| `get_feature_documentation` | – | User | (docs) | Existing — feature docs |
| `search_commands` / `get_command_details` / `list_features` | – | User | (docs) | Existing — command discovery |
| `get_user_profile` / `get_guild_info` / `get_user_roles` | – | User | (docs) | Existing — user/guild info |
| `get_rat_watch_leaderboard` / `_user_stats` / `_summary` | – | User | (docs) | Existing — Rat Watch reads |
| `get_my_reminders` | – | User | reads | New — caller's upcoming reminders |
| `get_server_stats` | – | User | reads | New — public guild stats from analytics snapshots |
| `get_soundboard_inventory` | – | User | reads | New — available sounds + metadata |
| `get_my_mod_status` | – | User | reads | New — caller's own warnings/cases (self only) |
| `get_leaderboard` | – | User | reads | New — generalized leaderboard read |

### 2.2 Self-scoped action tools (Phase 1)

All assert `target == caller`. Governing capability: `EnableSelfActions` (under `EnableActionTools`).

| Tool | Mut | Conf | Tier | Backing service | Notes |
|---|---|---|---|---|---|
| `create_reminder` | ✓ | direct + cancel button | User | `IReminderService` | NL time parse; enforces `ReminderOptions` per-user limits |
| `cancel_reminder` | ✓ | button | User | `IReminderService` | Only caller's reminders |
| `play_sound` | ✓ | direct | User | Soundboard/Audio service | Requires caller in voice channel; audio enabled |
| `list_sounds` | – | – | User | Soundboard service | Read |
| `set_my_tts_preset` | ✓ | direct | User | TTS preset service | Caller's own preset |
| `get_my_tts_preset` | – | – | User | TTS preset service | Read |

### 2.3 Privileged action tools (Phase 3)

Governing capability: `EnablePrivilegedActions`. **All require Discord-button confirmation; tier re-validated at execution.**

| Tool | Mut | Conf | Tier | Backing service | Notes |
|---|---|---|---|---|---|
| `warn_user` | ✓ | button | Moderator | Moderation action service | Creates a mod case (mirrors `/warn`) |
| `timeout_user` | ✓ | button | Moderator | Moderation action service | Duration via NL (mirrors `/mute`) |
| `purge_messages` | ✓ | button | Moderator | Purge service | Bounded by existing purge limits; explicit count |
| `create_scheduled_message` | ✓ | button | Admin | `IScheduledMessageService` | Mirrors `/schedule-create` |
| `toggle_scheduled_message` | ✓ | button | Admin | `IScheduledMessageService` | Enable/disable a schedule |

> `ban` / `kick` are intentionally **not** exposed via the assistant in v2 (OQ-02).

### 2.4 Web knowledge tools (Phase 3)

Governing capability: `EnableWebKnowledge` (off by default).

| Tool | Mut | Tier | Notes |
|---|---|---|---|
| `web_search` | – | User | Summarized results with cited URLs |
| `web_fetch` | – | User | Reuses hardened `WebFetchTools` (10s timeout, 512KB cap, host restrictions); counts against action budget |

---

## 3. Configuration Keys

Under the `Assistant` section (`AssistantOptions`). Global `false` always wins over per-guild.

| Key | Type | Default | Description |
|---|---|---|---|
| `EnableActionTools` | bool | `false` | Master switch for all mutating tools |
| `EnableSelfActionsByDefault` | bool | `false` | Default for guilds with `EnableSelfActions` unset |
| `EnablePrivilegedActionsByDefault` | bool | `false` | Default for `EnablePrivilegedActions` unset |
| `EnableConversationMemoryByDefault` | bool | `false` | Default for `EnableConversationMemory` unset |
| `EnableWebKnowledgeByDefault` | bool | `false` | Default for `EnableWebKnowledge` unset |
| `ConversationIdleWindowMinutes` | int | `10` | Idle timeout ending a channel conversation |
| `MaxConversationMessages` | int | `10` | Sliding-window size for channel memory |
| `ActionsPerWindow` | int | `10` | Per-user action budget per rate-limit window |
| `MaxPersonaPromptLength` | int | `1000` | Max chars for per-guild persona |
| `ToolExecutionTimeoutMs` | int | `5000` | Existing — now also enforced per action tool |

Existing keys retained: `EnableDocumentationTools`, `MaxToolCallsPerQuestion`, `DefaultRateLimit`, `RateLimitWindowMinutes`, `RequireExplicitConsent`, cost-tracking keys, prompt-caching keys.

### 3.1 Model & Pricing (resolved — PRD Decision D-06)

| Key | Value | Notes |
|---|---|---|
| `Model` | `claude-sonnet-4-6` | Default. Replaces the **retired** `claude-3-5-sonnet-20241022` (retired 2025-10-28; no longer a valid ID). |
| Economy option | `claude-haiku-4-5` | Optional per-guild model for very high-volume guilds. |
| Thinking | `adaptive` | Use `thinking: {type: "adaptive"}`. `budget_tokens` is rejected on current models. |
| `CostPerMillionInputTokens` | `3.00` | Unchanged — Sonnet 4.6 pricing matches the retired 3.5-Sonnet, so cost tracking stays accurate. |
| `CostPerMillionOutputTokens` | `15.00` | Unchanged. |

**Prompt caching caveat:** Sonnet 4.6 has a **2048-token minimum cacheable prefix**. The agent prompt alone (~1500 tokens) may silently not cache; cache the agent prompt **plus** the common documentation files together (as the current implementation does) so the prefix clears the threshold. Verify with `usage.cache_read_input_tokens > 0`.

**Web knowledge (Decision D-04):** uses Claude's native server-side `web_search` / `web_fetch` tools — no third-party search backend or API key. The hardened `WebFetchTools` is retained for user-pasted URLs.

---

## 4. Per-Guild Settings (`AssistantGuildSettings`)

| Field | Type | Null behavior |
|---|---|---|
| `IsEnabled` | bool | existing |
| `AllowedChannelIds` | json | existing |
| `RateLimitOverride` | int? | existing |
| `EnableActionTools` | bool? | falls back to global `EnableActionTools` |
| `EnableSelfActions` | bool? | falls back to `EnableSelfActionsByDefault` |
| `EnablePrivilegedActions` | bool? | falls back to `EnablePrivilegedActionsByDefault` |
| `EnableConversationMemory` | bool? | falls back to `EnableConversationMemoryByDefault` |
| `EnableWebKnowledge` | bool? | falls back to `EnableWebKnowledgeByDefault` |
| `PersonaPrompt` | string? | none (no persona) |

Managed at `/Guilds/AssistantSettings/{guildId}` (RequireAdmin).

Effective capability: `effective = global AND (perGuild ?? globalDefault)` via `IAssistantCapabilityService`.

---

## 5. Inherited Guardrails (unchanged)

Every new capability sits behind the existing pre-flight checks in `AssistantMessageHandler` / `AssistantService`:

1. Global assistant enabled
2. Guild assistant enabled (`AssistantGuildSettings.IsEnabled`)
3. Channel allowlist
4. Consent `ConsentType.AssistantUsage` (when `RequireExplicitConsent`)
5. Per-user question rate limit (`DefaultRateLimit` / `RateLimitWindowMinutes`)
6. Question length cap + response truncation
7. Cost tracking (`AssistantUsageMetrics`, `AssistantInteractionLog`)

New, additive:

8. Per-guild capability gate
9. Permission-tier gate (privileged tools)
10. Target-scope assertion (self tools)
11. Per-user action budget (`ActionsPerWindow`)
12. Confirmation (mutating tools)
13. Per-tool execution timeout

---

## 6. Audit & Observability

- **Audit:** every action-tool execution → `IAuditLogService` entry: actor, guild, channel, tool, sanitized params, target, outcome, correlation ID (`AssistantInteractionLog.Id`).
- **Metrics:** `AssistantUsageMetrics` extended (or a companion table) to count actions per guild/day alongside questions, tokens, and cost; surfaced on the Assistant Metrics page.
- **Interaction log:** existing `AssistantInteractionLog` continues to record the parent question; tool calls already counted via `ToolCalls`.
- **Tracing/logging:** action tools log under `DiscordBot.*.LLM` per the existing debug-logging guidance.

---

## 7. Security Notes

- Permission tier is resolved from Discord roles **server-side**; tool arguments and message text cannot influence it.
- Self-scoped tools hard-reject any non-self target.
- Mutating privileged actions execute only after explicit Discord-button confirmation, with tier re-validated at click time.
- Per-guild persona is injection-filtered and length-bounded; the base agent safety prompt always takes precedence.
- Web tools reuse the existing hardened fetch path; off by default.
- Existing data-sanitization rules apply: no secrets, no API keys, no internal identifiers, masked emails.
- Community conversation memory is user data: covered by `/privacy export-data` and `/privacy delete-data`.

---

## 8. Privacy & Retention

| Data | Store | Retention |
|---|---|---|
| Interaction logs | `AssistantInteractionLog` | `InteractionLogRetentionDays` (existing, default 90) |
| Action audit entries | Audit log | Audit log retention policy (existing) |
| Channel conversation memory | `AssistantConversationMessage` | Sliding window + idle expiry + cleanup task; included in delete/export |

---

## 9. Backward Compatibility

- Defaults keep every new capability **off**; an existing deployment behaves identically until an operator enables `EnableActionTools` globally and an admin opts a guild in.
- No existing tool changes behavior. `EnableDocumentationTools` remains the documentation/read switch.
- Migrations are additive (new nullable columns + one new table); both SQLite and PostgreSQL sets required (`--context` per CLAUDE.md).
</content>
