# User Profile Extraction

Documentation for the **User Profile Extraction** feature — an opt-in, consent-driven system that analyzes a user's logged chat messages to build a personalized profile, enabling the AI assistant to deliver contextually relevant, personalized responses.

---

## Documents

| Document | Audience | Description |
|---|---|---|
| [BRD.md](BRD.md) | Product / Stakeholders | Business requirements, objectives, success criteria, and out-of-scope items |
| [PRD.md](PRD.md) | Product / Engineering | Full product requirements — consent model, profile data model, extraction pipeline, slash commands, assistant integration, database schema, GDPR, and open questions |
| [UserStories.md](UserStories.md) | Product / QA | Acceptance-criteria-driven user stories for guild members, admins, and the bot developer |
| [Reference.md](Reference.md) | Developers / Power Users | Technical reference — consent mechanics, command syntax, profile structure, configuration keys, observability, and security notes |

---

## Feature Summary

- **Consent:** New `ProfileExtraction` consent type — explicit opt-in required, separate from message logging consent
- **Prerequisite:** User must already have `MessageLogging` consent granted before `ProfileExtraction` can be enabled
- **Scope:** Profiles are per-guild — a user's profile in one guild is independent of another
- **Extraction:** Background service periodically analyzes sampled logged messages via the Anthropic API to produce a structured profile
- **Commands:** `/profile view`, `/profile refresh`, `/profile delete`, `/profile edit` — all ephemeral, guild-only
- **Personalization:** Profile summary injected into the AI assistant's system prompt as a `<user_context>` block when the profiled user asks a question
- **Privacy:** No raw message content stored in profiles; only derived summaries. No sensitive data categories (race, religion, health, etc.) extracted. Full GDPR export and purge integration
- **Cost control:** Message sampling (max 500 messages, 30-day window), configurable extraction interval (default: 7 days), per-guild admin toggle

---

## Status

| Document | Status |
|---|---|
| BRD | Draft |
| PRD | Draft |
| User Stories | Draft |
| Reference | Draft |
| Implementation | Not started |

---

## Open Questions

See [PRD.md § 11 Open Questions](PRD.md#11-open-questions) for remaining decisions (OQ-01 through OQ-07).
