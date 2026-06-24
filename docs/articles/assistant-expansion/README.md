# Community Assistant Expansion

Documentation for **Assistant v2** — an evolution of the community-facing (channel/@mention) AI assistant from a read-only FAQ bot into a conversational agent that members can actually *talk to* and *get things done with*.

Today the community assistant is single-turn, has no memory, and every one of its tools is read-only: it can *tell* you how to use `/remind`, but it cannot *set* the reminder. This feature closes that gap across three pillars — **action tools**, **conversation & memory**, and **knowledge & richer reads** — built on a new **Action-Tool Safety Layer** that the current assistant lacks.

---

## Documents

| Document | Audience | Description |
|---|---|---|
| [BRD.md](BRD.md) | Product / Stakeholders | Business objective, context, stakeholders, requirements, out-of-scope, success criteria |
| [PRD.md](PRD.md) | Product / Engineering | Full product requirements — the three pillars, the safety layer, tool catalog, conversation model, knowledge tools, configuration, phasing, open questions |
| [UserStories.md](UserStories.md) | Product / QA | Acceptance-criteria-driven stories for members, moderators, admins, and the bot owner |
| [Architecture.md](Architecture.md) | Engineering | Component map, data flow, the safety layer design, tool-context role plumbing, conversation/memory model, new file inventory, risk register |
| [Reference.md](Reference.md) | Developers / Power Users | Tool catalog reference, configuration keys, model/pricing, per-guild settings, permission tiers, audit/observability, security notes |
| [ImplementationPlan.md](ImplementationPlan.md) | Engineering | Concrete phased build plan — prerequisite model fix, then Phases 0–3 with files, migrations, tests, and exit criteria |

---

## Feature Summary

- **Audience:** All guild members (channel/@mention), subject to existing consent + per-guild enablement
- **Pillar 1 — Action tools:** Natural-language front-end to existing bot services. "remind me in 2 hours to check the oven," "what sounds do you have? play airhorn," "schedule the standup message for weekdays at 9am." Two trust tiers: **self-scoped** (act only on the caller's own data) and **privileged** (mod/admin actions, behind role checks + confirmation).
- **Pillar 2 — Conversation & memory:** Opt-in multi-turn, thread-aware conversations in channels, reusing the DM assistant's memory machinery. Optional per-guild persona.
- **Pillar 3 — Knowledge & richer reads:** Expanded read tools (my reminders, soundboard inventory, server stats, my mod status) plus optional, rate-limited web search/fetch.
- **Foundation — Action-Tool Safety Layer:** Role resolution into `ToolContext`, per-action audit logging, a confirmation pattern for state-changing/destructive actions, and per-guild per-capability opt-in (following the existing `PublicLeaderboardEnabled` precedent).
- **Inherited guardrails:** Consent (`assistant_usage`), guild enable, channel allowlist, per-user rate limit, cost tracking — all already enforced upstream and reused unchanged.

---

## Status

| Document | Status |
|---|---|
| BRD | Draft |
| PRD | Draft — all open questions resolved (see Decision Log) |
| User Stories | Draft |
| Architecture | Draft |
| Reference | Draft |
| Implementation Plan | Draft |
| Implementation | Not started |

---

## Phasing at a glance

| Phase | Scope | Risk |
|---|---|---|
| **0 — Safety Layer** | Role plumbing into `ToolContext`, per-action audit, confirmation pattern, per-guild capability toggles | Low (no user-visible behavior yet) |
| **1 — Self-scoped actions + richer reads** | Set/list/cancel my reminders, play sounds, my stats, soundboard inventory, server stats | Medium |
| **2 — Conversation & memory** | Thread-aware multi-turn memory in channels, per-guild persona | Medium |
| **3 — Privileged actions + web knowledge** | Mod/admin actions via NL (with confirmation), scheduled messages, web search/fetch | Higher |

---

## Decisions

All seven original open questions are resolved — see [PRD.md § Decision Log](PRD.md#9-decision-log). Highlights: tiered confirmation keyed on reversibility (D-01), `ban`/`kick` excluded from v2 (D-02), native Claude web tools (D-04), default model `claude-sonnet-4-6` (D-06), privileged actions require both bot tier and native Discord permission (D-07).
</content>
</invoke>
