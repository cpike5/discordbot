# Business Requirements Document
## Feature: Community Assistant Expansion (Assistant v2)

**Status:** Draft  
**Author:** Claude Code  
**Date:** 2026-06-24  
**Version:** 1.0

---

## 1. Business Objective

Transform the community-facing AI assistant from a read-only FAQ bot into a conversational agent that members can talk to naturally and use to accomplish real tasks. The assistant should become the **universal natural-language front-end** to the bot's existing feature set, lowering the barrier to every capability already shipped and making the bot feel like a helpful participant in the community rather than a command reference.

## 2. Business Context

The bot has a large, mature feature set — reminders, soundboard, TTS, scheduling, moderation, leaderboards, and more — but each requires members to know exact slash-command syntax. The community AI assistant could remove that barrier, yet today it is deliberately limited:

- **Single-turn, no memory** — every @mention is an isolated exchange; the assistant cannot hold a conversation.
- **Read-only tools only** — it can explain how to use `/remind`, but it cannot create the reminder. All capability to *act* lives behind the owner-only DM assistant.
- **No knowledge beyond bundled docs** — it cannot answer questions that require current information.

This creates a clear opportunity. The infrastructure to do more already exists: the DM assistant demonstrates multi-turn memory and write-capable tools, the LLM abstraction cleanly separates community (`IToolProvider`) from owner-only (`IDmToolProvider`) tools, and all the underlying services (reminders, soundboard, scheduling) are already built. What is missing is a safety layer that lets the *public* assistant take actions responsibly.

A more capable community assistant:

- Increases adoption of existing features by letting members invoke them conversationally
- Reduces support load — members ask the bot instead of asking moderators
- Improves engagement and retention through a more natural, memorable interaction model
- Reuses existing services and guardrails rather than building parallel systems

## 3. Stakeholders

| Role | Interest |
|---|---|
| Guild Members | Talk to the bot naturally; get things done without memorizing command syntax |
| Guild Moderators | Optionally drive moderation actions conversationally, with safety confirmations |
| Guild Admins | Control which assistant capabilities are enabled per guild; trust that actions are audited and scoped |
| Bot Owner / Developer(s) | Reuse existing services and guardrails; contain cost and abuse; keep the public surface safe |

## 4. Business Requirements

| ID | Requirement |
|---|---|
| BR-01 | The community assistant shall be able to take actions on behalf of members via natural language, not only answer questions |
| BR-02 | Action capability shall be tiered: **self-scoped** actions (affecting only the calling user's own data) and **privileged** actions (moderation/admin), with stricter controls on the latter |
| BR-03 | Privileged actions shall be gated by the caller's actual Discord/role-tier permissions, not assumed |
| BR-04 | State-changing and destructive actions shall require explicit user confirmation before execution |
| BR-05 | Every action taken by the assistant shall be recorded in the audit trail, attributable to the requesting user |
| BR-06 | Guild admins shall be able to enable or disable assistant capabilities per guild at a meaningful granularity |
| BR-07 | The assistant shall support opt-in multi-turn conversation with short-lived memory within a channel/thread context |
| BR-08 | The assistant shall be able to retrieve richer read-only information about the member and guild (e.g. the member's own reminders, soundboard inventory, server statistics) |
| BR-09 | The assistant shall optionally be able to access external knowledge (web search/fetch) under rate and safety controls, configurable per guild |
| BR-10 | All new capabilities shall continue to respect existing guardrails: consent, guild enablement, channel allowlist, per-user rate limiting, and cost tracking |
| BR-11 | New capabilities shall be safe against prompt injection — user-controlled text shall never be able to escalate privileges or trigger actions the user is not authorized to perform |
| BR-12 | Cost and abuse exposure shall remain bounded and observable, with per-guild visibility into assistant usage and actions |

## 5. Out of Scope (Initial Release)

- Replacing slash commands — the assistant augments, never removes, the existing command surface
- Voice-channel interaction (speaking to the assistant); text-only for v2
- Cross-guild memory or a member profile that persists across servers
- Autonomous/unsolicited actions — the assistant only acts in response to a member's request
- Fine-grained per-role tool permissions beyond the self-scoped / privileged tiers (deferred)
- Custom per-guild tool authoring by admins
- Image generation or file creation

## 6. Success Criteria

- A member can set a reminder, play a sound, and check their own stats entirely through natural language, with no slash-command syntax
- Every assistant-initiated action appears in the audit trail attributed to the requesting member within seconds
- No member can cause a privileged action they are not authorized to perform, including via prompt-injection attempts
- Multi-turn conversations retain context within a session and expire cleanly
- Guild admins can enable/disable each capability tier per guild from the existing settings UI
- Per-guild cost and action volume remain visible in the assistant metrics dashboard, and stay within configured thresholds
- Zero unhandled exceptions from malicious or malformed input
</content>
