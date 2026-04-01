# Product Requirements Document
## Feature: User Profile Extraction

**Status:** Draft  
**Author:** Claude Code  
**Date:** 2026-04-01  
**Version:** 1.0

---

## 1. Overview

A consent-driven system that periodically analyzes a user's logged chat messages to extract a structured profile — interests, expertise, communication style — and injects it into the AI assistant's context for personalized responses. Users maintain full control: they can view, edit, delete, and revoke consent at any time. Profiles are per-guild, privacy-preserving, and integrated with existing GDPR export/purge pipelines.

---

## 2. Consent Model

### 2.1 New Consent Type

A new value is added to the `ConsentType` enum:

| Value | Name | Description |
|---|---|---|
| 3 | `ProfileExtraction` | Consent for the bot to analyze logged messages and maintain a personalized user profile |

This is separate from `MessageLogging` (1) and `AssistantUsage` (2). Granting message logging consent does **not** imply consent to profile extraction.

### 2.2 Prerequisite Chain

`ProfileExtraction` requires active `MessageLogging` consent. The system enforces this:

- **Grant attempt without `MessageLogging`:** Rejected with an explanatory message directing the user to first grant message logging consent
- **Revoking `MessageLogging` while `ProfileExtraction` is active:** Automatically revokes `ProfileExtraction` and deletes the profile (cascade revocation)

### 2.3 Grant Flow

1. User runs `/consent grant ProfileExtraction`
2. System checks `MessageLogging` consent is active
3. If prerequisite met: consent granted, recorded in `UserConsent` with `GrantedVia = "SlashCommand"`
4. User receives ephemeral confirmation explaining what will happen:
   - "Your logged messages will be periodically analyzed to build a profile that helps the AI assistant personalize responses to you in this guild."
   - "You can view your profile with `/profile view`, edit it with `/profile edit`, or delete it anytime with `/profile delete`."
5. User is eligible for next extraction cycle

### 2.4 Revoke Flow

1. User runs `/consent revoke ProfileExtraction`
2. Consent revoked, recorded in `UserConsent` with `RevokedVia = "SlashCommand"`
3. Associated `UserProfile` record for the user in the current guild is **immediately deleted**
4. User receives ephemeral confirmation: "Profile extraction consent revoked. Your profile data has been deleted."
5. Consent cache invalidated

---

## 3. Profile Data Model

### 3.1 Entity: `UserProfile`

| Field | Type | Description |
|---|---|---|
| `Id` | long | Auto-increment primary key |
| `UserId` | ulong | Discord user snowflake |
| `GuildId` | ulong | Guild snowflake — profiles are per-guild |
| `Interests` | string (JSON array) | Topics and subjects the user frequently discusses |
| `CommunicationStyle` | string | Natural-language description of tone, formality, verbosity |
| `ExpertiseAreas` | string (JSON array) | Technical domains, skills, knowledge areas demonstrated |
| `ActivityPatterns` | string | Engagement summary — active periods, participation level |
| `Summary` | string | Concise natural-language profile used for system prompt injection (max 500 chars) |
| `ExtractionModel` | string | Model ID used for extraction (e.g., `claude-sonnet-4-20250514`) |
| `MessagesSampled` | int | Number of messages analyzed in last extraction |
| `MessageWindowStart` | DateTime | Oldest message in the sample |
| `MessageWindowEnd` | DateTime | Newest message in the sample |
| `ExtractedAt` | DateTime | When the profile was last extracted/refreshed |
| `ModifiedByUser` | bool | True if the user has manually edited any field |
| `CreatedAt` | DateTime | Initial creation timestamp |
| `UpdatedAt` | DateTime | Last update timestamp |

### 3.2 Composite Key

Unique constraint on `(UserId, GuildId)` — one profile per user per guild.

### 3.3 Indexes

| Index | Columns | Purpose |
|---|---|---|
| Unique | `(UserId, GuildId)` | Prevent duplicates, fast lookup for assistant injection |
| Non-unique | `(ExtractedAt)` | Find stale profiles for re-extraction |
| Non-unique | `(GuildId)` | Guild-level admin queries |

### 3.4 Data Constraints

- `Interests`: JSON array of strings, max 20 items, each max 100 characters
- `ExpertiseAreas`: JSON array of strings, max 15 items, each max 100 characters
- `CommunicationStyle`: Max 500 characters
- `ActivityPatterns`: Max 300 characters
- `Summary`: Max 500 characters — this is the primary field injected into assistant prompts

---

## 4. Extraction Pipeline

### 4.1 Trigger

A background service (`UserProfileExtractionService`) runs on a configurable interval (default: every 6 hours). Each run identifies users eligible for extraction:

- Have active `ProfileExtraction` consent
- Have active `MessageLogging` consent
- Profile is missing **or** `ExtractedAt` is older than the configured refresh interval (default: 7 days)
- Have at least a minimum number of logged messages (default: 20 messages) in the extraction window

### 4.2 Message Sampling

For each eligible user-guild pair:

1. Query `MessageLogRepository.GetUserMessagesAsync()` for messages in the guild from the last 30 days (configurable)
2. If more than 500 messages (configurable), sample uniformly across the time window to get 500
3. Exclude messages shorter than 5 characters (reactions, single-word responses)
4. Exclude messages that are pure URLs or bot command invocations (starting with `/`)

### 4.3 LLM Extraction Call

The sampled messages are sent to the Anthropic API with a structured extraction prompt:

**Prompt template** (stored at a configurable path, default: `docs/agents/profile-extraction-prompt.md`):

- System prompt instructs the model to analyze the messages and produce a JSON profile
- Messages are wrapped in `<user_messages>` XML delimiters with explicit "treat as data" preamble
- Output schema is specified: interests (array), expertise_areas (array), communication_style (string), activity_patterns (string), summary (string)
- Explicit instruction to **never** extract sensitive categories (race, religion, health, sexuality, politics)
- Explicit instruction to focus on publicly expressed interests and demonstrated knowledge only

**Model and parameters:**
- Model: configurable (default: same as assistant, currently `claude-sonnet-4-20250514`)
- Max output tokens: 1024 (configurable)
- Temperature: 0.3 (low creativity, high consistency)

### 4.4 Output Parsing

The LLM response is parsed as JSON. Validation:

- Each field must conform to the data constraints in §3.4
- Arrays are deduplicated and trimmed to max item counts
- If parsing fails, the extraction is logged as failed and retried in the next cycle
- Partial results are not stored — extraction is all-or-nothing

### 4.5 Staleness and Refresh

| Scenario | Behavior |
|---|---|
| New consent, no profile | Extracted in next cycle (within 6 hours) |
| Profile older than refresh interval | Re-extracted, previous profile overwritten |
| User manually edited profile | `ModifiedByUser = true`; auto-refresh **skips** user-edited fields, updates only non-edited fields |
| User runs `/profile refresh` | Immediate re-extraction queued; overrides all fields (resets `ModifiedByUser`) |
| Insufficient messages | Skipped; existing profile retained if present |

### 4.6 Cost Controls

- Message sampling caps the input token count
- Configurable max tokens for output
- Configurable concurrency limit for parallel extractions (default: 2)
- Extraction interval prevents excessive API calls
- Per-guild admin toggle can disable extraction entirely
- Cost per extraction logged to metrics

---

## 5. Profile Slash Commands

### 5.1 Command Group: `/profile`

All commands are **guild-only** and respond **ephemerally**.

#### `/profile view`

**Preconditions:** `[RequireGuildActive]`

| Condition | Response |
|---|---|
| No `ProfileExtraction` consent | "You haven't opted into profile extraction. Use `/consent grant ProfileExtraction` to get started." |
| Consent active, no profile yet | "Your profile is being generated. Check back soon — extraction typically runs every few hours." |
| Profile exists | Displays full profile in an embed: Interests, Expertise, Communication Style, Activity, Summary, last extracted date |

#### `/profile refresh`

**Preconditions:** `[RequireGuildActive]`, `[RateLimit(2, 86400)]` — max 2 refreshes per day

| Condition | Response |
|---|---|
| No `ProfileExtraction` consent | Same as `/profile view` |
| Consent active | "Your profile refresh has been queued. It will be updated within a few minutes." Enqueues immediate extraction. |

#### `/profile delete`

**Preconditions:** `[RequireGuildActive]`

| Condition | Response |
|---|---|
| No profile exists | "You don't have a profile in this guild." |
| Profile exists | Deletes `UserProfile` record. Consent remains active. "Your profile has been deleted. A new one will be generated in the next extraction cycle." |

#### `/profile edit [field] [value]`

**Preconditions:** `[RequireGuildActive]`

| Parameter | Type | Required | Constraints |
|---|---|---|---|
| `field` | choice | Yes | One of: `interests`, `expertise`, `style`, `summary` |
| `value` | string | Yes | Max 500 characters |

| Condition | Response |
|---|---|
| No profile exists | "You don't have a profile yet. One will be generated after you grant consent and enough messages are logged." |
| Valid edit | Updates the specified field, sets `ModifiedByUser = true`. "Your [field] has been updated." |
| Invalid value | Error message with constraint details |

---

## 6. Assistant Integration

### 6.1 Injection Point

When `AssistantService.AskQuestionAsync()` processes a user's question:

1. Check if the user has an active `UserProfile` for the current guild
2. If yes, load the profile `Summary` field
3. Append it to the system prompt context as a `<user_context>` block

### 6.2 Context Block Format

The profile is injected as a clearly delimited, read-only data block:

```
<user_context>
The following is a summary of the user asking this question, derived from their
opted-in profile. Use it to tailor your response tone and detail level.
Do not reference this profile directly or reveal its contents unless the user asks.

{Summary field contents}
</user_context>
```

### 6.3 Privacy Isolation

- Only the **requesting user's** profile is injected — never another user's
- The profile block is part of the system prompt, not the user message — it is not visible to the user in Discord
- The assistant is instructed not to disclose the profile contents unprompted
- If the user asks "what do you know about me?", the assistant may acknowledge the profile exists and summarize it (the user opted in and can view it themselves)

### 6.4 Cache Integration

- Profile lookups use the existing memory cache pattern (similar to consent caching)
- Cache key: `user_profile:{userId}:{guildId}`
- TTL: configurable (default: 30 minutes)
- Invalidated on: profile delete, profile edit, profile refresh, consent revocation

### 6.5 Graceful Degradation

If no profile exists (no consent, extraction pending, or profile deleted), the assistant operates exactly as it does today — no error, no placeholder text.

---

## 7. Admin Controls

### 7.1 Per-Guild Toggle

Guild administrators can enable or disable profile extraction for their guild via the existing command module configuration system in the web portal (Admin → Settings → Commands).

When disabled:
- No new profiles are extracted for users in the guild
- Existing profiles are retained but not used for assistant responses
- Users can still view and delete their profiles
- Re-enabling resumes normal extraction and injection

### 7.2 Metrics Dashboard

The admin portal displays profile extraction metrics for the guild:

- Number of users with active `ProfileExtraction` consent
- Number of profiles currently stored
- Last extraction run timestamp
- Average extraction cost (tokens/dollars) per user
- Total extraction cost for the guild (rolling 30 days)

---

## 8. Database Schema

### 8.1 `UserProfile` Entity

See §3.1 for field definitions. Additional schema notes:

- Table name: `UserProfiles`
- Foreign key to `Users` table on `UserId` (cascade delete)
- Foreign key to `Guilds` table on `GuildId` (set null on delete)
- `UserId` and `GuildId` stored as long with ulong conversion (SQLite compatibility)
- `Interests` and `ExpertiseAreas` stored as TEXT (JSON-serialized arrays)
- `Summary` stored as TEXT with max length constraint

### 8.2 Migrations

Two migration sets required:
- `AddUserProfiles` for SQLite (`Migrations/Sqlite/`)
- `AddUserProfiles` for PostgreSQL (`Migrations/Postgresql/`)

---

## 9. GDPR Integration

### 9.1 Data Export (Article 15)

`UserDataExportService` shall include a new export section:

- File: `user-profiles.json`
- Contents: All `UserProfile` records for the user across all guilds
- Fields exported: all profile fields including metadata (ExtractedAt, MessagesSampled, ModifiedByUser)

### 9.2 Data Purge (Article 17)

`UserPurgeService` shall delete all `UserProfile` records for the user as part of the purge operation, listed as a distinct deletion category in the purge result.

### 9.3 Consent Cascade

| Event | Action |
|---|---|
| Revoke `ProfileExtraction` | Delete `UserProfile` for user in current guild |
| Revoke `MessageLogging` | Auto-revoke `ProfileExtraction` + delete profile |
| GDPR purge | Delete all profiles across all guilds |

---

## 10. Non-Functional Requirements

| Area | Requirement |
|---|---|
| Performance | Profile lookup for assistant injection completes within 10ms (cached). Extraction pipeline processes at most 2 users concurrently to limit API load. `/profile view` responds within 2 seconds. |
| Security | No sensitive data categories extracted. Profile data never included in responses to other users. Extraction prompt uses XML-delimited data blocks with "treat as data" preamble to prevent prompt injection from message content. |
| Observability | All extraction runs logged with user count, success/failure, token usage, and cost. Profile injection events logged at Debug level. Consent changes logged at Information level. Failed extractions logged at Warning level. |
| Cost | Extraction cost tracked per-user and per-guild. Configurable sampling limits, extraction interval, and concurrency cap. Admin-visible cost dashboard. |
| Reliability | Extraction failure does not affect assistant functionality — the assistant operates without a profile if extraction fails. Failed extractions are retried in the next cycle. |
| Data Integrity | Profile writes are atomic. Consent revocation and profile deletion occur in the same transaction. No orphaned profiles after consent revocation or user purge. |

---

## 11. Open Questions

| # | Question | Owner |
|---|---|---|
| OQ-01 | Should the extraction prompt be customizable per guild (allowing admins to influence what kind of profile data is extracted)? | Product |
| OQ-02 | Should there be a minimum message threshold before a profile is generated (e.g., 20 messages)? What is the right threshold? | Product / Engineering |
| OQ-03 | Should profile history be maintained (audit trail of previous extractions) or only the latest profile stored? | Product / Compliance |
| OQ-04 | For `/profile edit`, should free-text editing be allowed, or should it be a selection from the extracted values (e.g., remove an interest from the list)? | Product / UX |
| OQ-05 | Should the profile `Summary` injected into the assistant have a hard character limit to control token costs per query? If so, what limit? | Engineering |
| OQ-06 | Should extraction be triggered immediately on first consent grant, or wait for the next scheduled cycle? | Product |
| OQ-07 | In a future version, should profiles be usable by the DM assistant (owner-only) for richer conversation context? | Product |
