# Technical Reference: User Profile Extraction

**Audience:** Developers, power users, and admins  
**Date:** 2026-04-01  
**Version:** 1.0

---

## Consent Reference

### New Consent Type

| Value | Name | Description |
|---|---|---|
| 3 | `ProfileExtraction` | Consent for the bot to analyze logged messages and build a personalized user profile |

This is in addition to the existing types: `MessageLogging` (1) and `AssistantUsage` (2).

### Prerequisite

`ProfileExtraction` requires active `MessageLogging` consent. The system enforces this at grant time and cascades revocation:

| Action | Behavior |
|---|---|
| Grant `ProfileExtraction` without `MessageLogging` | Rejected with error message |
| Revoke `MessageLogging` while `ProfileExtraction` is active | `ProfileExtraction` automatically revoked; profile data deleted |
| Revoke `ProfileExtraction` | Profile data deleted; `MessageLogging` unaffected |
| GDPR purge | All consents revoked; all profile data deleted |

### Consent Cache

Profile extraction consent is cached in memory using the same pattern as existing consent types:

- Cache key format: `consent:{userId}:{consentType}`
- TTL: governed by `CachingOptions.ConsentCacheDurationMinutes`
- Invalidated on grant, revoke, and purge

---

## Slash Command Reference

All `/profile` commands are **guild-only** and respond **ephemerally** (visible only to the invoking user).

### `/profile view`

Displays the user's full extracted profile for the current guild.

| Precondition | Detail |
|---|---|
| Guild must be active | `[RequireGuildActive]` — standard bot precondition |
| No consent required | Command works without consent — shows an opt-in prompt instead |

| State | Response |
|---|---|
| No `ProfileExtraction` consent | Prompt to grant consent |
| Consent active, profile not yet generated | "Profile pending" message |
| Profile exists | Full profile embed with all fields and metadata |

### `/profile refresh`

Queues an immediate re-extraction of the user's profile.

| Precondition | Detail |
|---|---|
| Guild must be active | `[RequireGuildActive]` |
| Rate limit | 2 per user per 24 hours |

Overwrites all profile fields, including user-edited ones. Resets the `ModifiedByUser` flag.

### `/profile delete`

Deletes the user's profile for the current guild. Consent remains active; a new profile will be generated in the next extraction cycle.

| Precondition | Detail |
|---|---|
| Guild must be active | `[RequireGuildActive]` |

### `/profile edit`

Updates a specific field in the user's profile.

| Parameter | Type | Required | Allowed Values |
|---|---|---|---|
| `field` | choice | Yes | `interests`, `expertise`, `style`, `summary` |
| `value` | string | Yes | Max 500 characters |

Sets `ModifiedByUser = true`. User-modified fields are preserved during automatic re-extraction (only non-modified fields are refreshed).

### Consent Commands (existing, extended)

`/consent grant ProfileExtraction` and `/consent revoke ProfileExtraction` are handled by the existing `ConsentModule`. The `ProfileExtraction` type appears in `/consent status` alongside `MessageLogging` and `AssistantUsage`.

---

## Profile Data Structure

### Fields

| Field | Format | Max Size | Description |
|---|---|---|---|
| Interests | JSON string array | 20 items, 100 chars each | Topics and subjects frequently discussed (e.g., "Python", "game development", "cooking") |
| ExpertiseAreas | JSON string array | 15 items, 100 chars each | Technical domains and skills demonstrated (e.g., "web development", "database design") |
| CommunicationStyle | Plain text | 500 characters | Natural-language description of tone and style (e.g., "Casual and direct, uses humor frequently, prefers concise answers") |
| ActivityPatterns | Plain text | 300 characters | Engagement summary (e.g., "Most active on weekday evenings, frequent contributor to technical discussions") |
| Summary | Plain text | 500 characters | Concise profile description injected into assistant system prompt |

### Metadata Fields

| Field | Description |
|---|---|
| ExtractionModel | Model ID used for the last extraction |
| MessagesSampled | Number of messages analyzed |
| MessageWindowStart | Timestamp of the oldest message in the sample |
| MessageWindowEnd | Timestamp of the newest message in the sample |
| ExtractedAt | When the profile was last extracted |
| ModifiedByUser | Whether the user has manually edited any field |

### Update Semantics

| Trigger | Fields Updated | ModifiedByUser Behavior |
|---|---|---|
| Automatic re-extraction | Non-user-modified fields only | Preserved |
| `/profile refresh` | All fields | Reset to `false` |
| `/profile edit` | Specified field only | Set to `true` |
| `/profile delete` | All fields removed | N/A (record deleted) |

---

## Extraction Pipeline

### Eligibility Criteria

A user-guild pair is eligible for extraction when **all** of the following are true:

1. User has active `ProfileExtraction` consent
2. User has active `MessageLogging` consent
3. Profile is missing, **or** `ExtractedAt` is older than the configured refresh interval
4. User has at least the minimum message count in the extraction window
5. Guild has profile extraction enabled (admin toggle)

### Message Sampling Strategy

| Parameter | Default | Description |
|---|---|---|
| Window | 30 days | How far back to look for messages |
| Max sample | 500 messages | Maximum messages sent to the LLM |
| Min threshold | 20 messages | Minimum messages required to generate a profile |

**Sampling rules:**
- Messages shorter than 5 characters are excluded
- Messages starting with `/` (bot commands) are excluded
- Messages that are pure URLs are excluded
- If the total exceeds the max sample, messages are selected uniformly across the time window to maintain temporal diversity

### Extraction LLM Call

| Parameter | Default | Description |
|---|---|---|
| Model | Same as assistant (configurable) | Anthropic model used for extraction |
| Max output tokens | 1024 | Limit on extraction response size |
| Temperature | 0.3 | Low creativity for consistent output |
| Prompt template path | `docs/agents/profile-extraction-prompt.md` | Configurable prompt location |

**Prompt structure:**
- System prompt with extraction instructions and output schema
- User messages wrapped in `<user_messages>` XML delimiters
- Explicit "treat as data" preamble before user content
- Explicit prohibition on extracting sensitive data categories
- Required output format: JSON object matching the profile field schema

### Failure Handling

| Failure Mode | Behavior |
|---|---|
| LLM API error (transient) | Logged at Warning; retried in next cycle |
| LLM API error (rate limit) | Backoff applied; remaining users deferred to next cycle |
| Invalid JSON response | Logged at Warning; extraction skipped, retried next cycle |
| Timeout | Logged at Warning; extraction skipped |
| Partial/incomplete response | Rejected — extraction is all-or-nothing |

---

## Assistant Integration

### When Profile Is Injected

Profile injection occurs during `AssistantService.AskQuestionAsync()` when:

1. The requesting user has an active `UserProfile` for the current guild
2. The guild has profile extraction enabled
3. The profile is successfully loaded (from cache or database)

If any condition is not met, the assistant operates without profile context — no error, no placeholder.

### Context Block Format

The profile `Summary` field is injected into the system prompt as a clearly delimited block:

```
<user_context>
The following is a summary of the user asking this question, derived from their
opted-in profile. Use it to tailor your response tone and detail level.
Do not reference this profile directly or reveal its contents unless the user asks.

{Summary}
</user_context>
```

This block appears after the main system prompt and before any tool definitions.

### Cache Behavior

| Aspect | Detail |
|---|---|
| Cache key | `user_profile:{userId}:{guildId}` |
| TTL | Configurable (default: 30 minutes) |
| Invalidation triggers | Profile delete, edit, refresh, consent revocation |
| Cache miss | Loaded from database; cached for subsequent requests |

---

## Database Entities

### `UserProfiles` Table

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | long | No | Auto-increment primary key |
| `UserId` | ulong (stored as long) | No | Discord user snowflake |
| `GuildId` | ulong (stored as long) | No | Guild snowflake |
| `Interests` | TEXT | Yes | JSON array of strings |
| `ExpertiseAreas` | TEXT | Yes | JSON array of strings |
| `CommunicationStyle` | TEXT | Yes | Plain text, max 500 chars |
| `ActivityPatterns` | TEXT | Yes | Plain text, max 300 chars |
| `Summary` | TEXT | Yes | Plain text, max 500 chars |
| `ExtractionModel` | TEXT | Yes | Model ID string |
| `MessagesSampled` | int | No | Count of messages analyzed |
| `MessageWindowStart` | DateTime | Yes | Oldest message timestamp |
| `MessageWindowEnd` | DateTime | Yes | Newest message timestamp |
| `ExtractedAt` | DateTime | Yes | Last extraction timestamp |
| `ModifiedByUser` | bool | No | Default: false |
| `CreatedAt` | DateTime | No | Record creation |
| `UpdatedAt` | DateTime | No | Last modification |

### Indexes

| Name | Columns | Type | Purpose |
|---|---|---|---|
| `IX_UserProfiles_UserId_GuildId` | `(UserId, GuildId)` | Unique | Primary lookup, prevent duplicates |
| `IX_UserProfiles_ExtractedAt` | `(ExtractedAt)` | Non-unique | Find stale profiles for re-extraction |
| `IX_UserProfiles_GuildId` | `(GuildId)` | Non-unique | Guild-level admin queries and metrics |

### Foreign Keys

| Column | References | On Delete |
|---|---|---|
| `UserId` | `Users.Id` | Cascade |
| `GuildId` | `Guilds.Id` | Set Null |

---

## Configuration Reference

All settings under `ProfileExtraction:` in `appsettings.json` or environment variables.

| Key | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `true` | Master toggle for the feature |
| `ExtractionIntervalDays` | int | `7` | How often profiles are refreshed |
| `ServiceRunIntervalHours` | int | `6` | How often the background service checks for eligible users |
| `MessageWindowDays` | int | `30` | How far back to query messages for extraction |
| `MaxMessagesSampled` | int | `500` | Maximum messages sent to the LLM per extraction |
| `MinMessagesRequired` | int | `20` | Minimum messages needed to generate a profile |
| `MaxConcurrentExtractions` | int | `2` | Parallel extraction limit |
| `ExtractionModel` | string | (inherits from `Anthropic:Model`) | Override model for extraction calls |
| `ExtractionMaxTokens` | int | `1024` | Max output tokens for extraction |
| `ExtractionTemperature` | float | `0.3` | Temperature for extraction calls |
| `PromptTemplatePath` | string | `docs/agents/profile-extraction-prompt.md` | Path to extraction prompt template |
| `ProfileCacheDurationMinutes` | int | `30` | TTL for cached profiles |
| `RefreshRateLimitPerDay` | int | `2` | Max `/profile refresh` invocations per user per day |
| `SummaryMaxLength` | int | `500` | Character limit for the Summary field |

---

## GDPR Integration

### Data Export (Article 15)

Profile data is included in the existing GDPR export pipeline:

| Export File | Contents |
|---|---|
| `user-profiles.json` | All `UserProfile` records for the user, across all guilds |

All fields are exported including metadata (ExtractionModel, MessagesSampled, ExtractedAt, ModifiedByUser).

### Data Purge (Article 17)

The purge pipeline deletes all `UserProfile` records for the user:

| Purge Category | Table | Action |
|---|---|---|
| `UserProfiles` | `UserProfiles` | Delete all records where `UserId` matches |

The deletion count is included in the `PurgeResult.DeletedCounts` dictionary under the `"UserProfiles"` key.

### Consent Cascade Rules

| Event | Profile Action | Consent Action |
|---|---|---|
| Revoke `ProfileExtraction` | Delete profile (current guild) | Consent revoked |
| Revoke `MessageLogging` | Delete profile (current guild) | `ProfileExtraction` auto-revoked |
| GDPR purge | Delete all profiles (all guilds) | All consents revoked |
| Guild admin disables feature | Profiles retained (dormant) | Consents unaffected |

---

## Observability

### Log Events

| Event | Level | Key Fields |
|---|---|---|
| Extraction cycle started | Information | EligibleUserCount, GuildCount |
| Extraction started (per user) | Information | UserId, GuildId, MessageCount |
| Extraction succeeded | Information | UserId, GuildId, InputTokens, OutputTokens, Cost, Duration |
| Extraction failed | Warning | UserId, GuildId, ErrorType, ErrorMessage |
| Extraction skipped (insufficient messages) | Debug | UserId, GuildId, MessageCount, MinRequired |
| Profile injected into assistant | Debug | UserId, GuildId, SummaryLength |
| Profile cache hit | Debug | UserId, GuildId |
| Profile cache miss | Debug | UserId, GuildId |
| Profile deleted (user action) | Information | UserId, GuildId, Source |
| Profile deleted (consent revocation) | Information | UserId, GuildId |
| Profile edited by user | Information | UserId, GuildId, Field |
| Consent granted (ProfileExtraction) | Information | UserId, GuildId, GrantedVia |
| Consent revoked (ProfileExtraction) | Information | UserId, GuildId, RevokedVia |
| Consent cascade (MessageLogging → ProfileExtraction) | Information | UserId, GuildId |

### Metrics

| Metric | Type | Description |
|---|---|---|
| `profile_extraction_total` | Counter | Total extraction attempts |
| `profile_extraction_success` | Counter | Successful extractions |
| `profile_extraction_failed` | Counter | Failed extractions |
| `profile_extraction_duration_ms` | Histogram | Extraction duration per user |
| `profile_extraction_tokens_input` | Counter | Total input tokens consumed |
| `profile_extraction_tokens_output` | Counter | Total output tokens consumed |
| `profile_extraction_cost_dollars` | Counter | Estimated cost in dollars |
| `profile_injection_total` | Counter | Times a profile was injected into assistant context |
| `profile_cache_hit_ratio` | Gauge | Cache hit percentage |

---

## Security Considerations

### Sensitive Data Prohibition

The extraction prompt explicitly prohibits extracting the following categories:

- Race or ethnicity
- Religious or philosophical beliefs
- Political opinions
- Health or medical information
- Sexual orientation
- Financial information
- Biometric or genetic data

If the LLM output contains any of these categories (detected via post-processing validation), the offending fields are stripped before storage.

### Profile Isolation

- Profile lookup is keyed on the requesting user's Discord ID — no ambient or shared state
- Cache keys include both user ID and guild ID to prevent cross-user or cross-guild leakage
- The assistant system prompt injection is scoped to a single request lifecycle

### Prompt Injection Defense

User messages passed to the extraction LLM are wrapped in XML delimiters with a "treat as data" preamble. The extraction prompt:

- Clearly separates instructions from user data
- Instructs the model to treat message content as opaque text to analyze, not instructions to follow
- Specifies a strict JSON output schema — free-form responses are rejected

### Data Minimization

- No raw message content is stored in the profile — only derived summaries and categorizations
- Profile fields have strict max lengths to prevent data bloat
- Only the `Summary` field (max 500 chars) is injected into assistant prompts — full profile details are not sent to the LLM on every query
- Extraction uses sampled messages, not the complete message history
