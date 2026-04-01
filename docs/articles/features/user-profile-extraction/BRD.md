# Business Requirements Document
## Feature: User Profile Extraction

**Status:** Draft  
**Author:** Claude Code  
**Date:** 2026-04-01  
**Version:** 1.0

---

## 1. Business Objective

Enable the AI assistant to deliver personalized, contextually relevant responses by building opt-in user profiles derived from logged chat messages. This transforms the assistant from a stateless Q&A tool into one that understands each user's interests, expertise, and communication preferences — while maintaining strict user control and privacy compliance.

## 2. Business Context

The bot's AI assistant currently treats every user interaction as a blank slate. It has no awareness of a user's interests, expertise level, communication style, or history of engagement. This means:

- A user who frequently discusses Python development receives the same generic tone and detail level as a first-time user
- The assistant cannot tailor explanations to a user's known skill level
- Repeated interactions do not build rapport or improve response quality over time
- Users in different guilds with different contexts receive identical treatment

The bot already has the infrastructure to support this:

- **Message logging** captures chat content with explicit user consent (`ConsentType.MessageLogging`)
- **AI assistant** uses a system prompt and tool-based context injection via `ToolRegistry` and `AgentRunner`
- **Consent framework** supports multiple consent types with grant/revoke/audit capabilities
- **GDPR pipeline** handles data export and purge for all user-linked entities

User profile extraction builds on these existing systems to close the personalization gap.

## 3. Stakeholders

| Role | Interest |
|---|---|
| Guild Members | Receive personalized assistant responses; maintain control over their data |
| Guild Admins | Enable or disable the feature per guild; monitor costs and adoption |
| Bot Developer(s) | Implement and maintain extraction pipeline; manage LLM costs |
| Privacy / Compliance | Ensure GDPR compliance, consent integrity, and data minimization |

## 4. Business Requirements

| ID | Requirement |
|---|---|
| BR-01 | Users shall explicitly opt in to profile extraction via a dedicated consent type (`ProfileExtraction`), separate from message logging consent |
| BR-02 | Profile extraction consent shall require active message logging consent as a prerequisite |
| BR-03 | User profiles shall be derived exclusively from the user's own logged messages — never from other users' messages or external data |
| BR-04 | Profiles shall be scoped per guild to reflect context-appropriate user behavior |
| BR-05 | Users shall be able to view their full extracted profile at any time |
| BR-06 | Users shall be able to edit or correct any field in their profile |
| BR-07 | Users shall be able to delete their profile without revoking consent |
| BR-08 | Revoking `ProfileExtraction` consent shall immediately and permanently delete the associated profile data |
| BR-09 | Extracted profiles shall enhance AI assistant responses with personalized context when the profiled user interacts with the assistant |
| BR-10 | The system shall not extract or store sensitive personal data categories (race, ethnicity, religion, health, sexual orientation, political affiliation) |
| BR-11 | LLM extraction costs shall be managed through message sampling, configurable intervals, and per-guild toggles |
| BR-12 | Profile data shall be included in GDPR data export and deleted during GDPR data purge |
| BR-13 | Guild administrators shall be able to enable or disable profile extraction for their guild independently of other features |
| BR-14 | Profile freshness shall be maintained through periodic re-extraction on a configurable schedule |

## 5. Out of Scope (Initial Release)

- Cross-guild or global user profiles (profiles are strictly per-guild in v1)
- DM assistant personalization (only the guild assistant uses profiles in v1)
- Profile sharing or visibility between users (a user's profile is private to them)
- Sentiment analysis or emotional state tracking
- Behavioral prediction or recommendation engines
- Profile-based moderation decisions (profiles are for personalization only, never for enforcement)
- Real-time extraction on each message (extraction is batch/periodic only)
- User-facing profile comparison or social features

## 6. Success Criteria

- Users with active `ProfileExtraction` consent receive demonstrably more relevant assistant responses compared to users without profiles (qualitative review)
- 100% of profile data is included in GDPR exports and deleted during GDPR purges
- Consent revocation results in profile deletion within the same request (no delayed cleanup)
- Extraction pipeline processes all eligible users within the configured interval without exceeding cost budgets
- Zero instances of profile data leaking to non-profiled users' assistant interactions
- Users can view, edit, and delete their profiles via slash commands with sub-2-second response times
