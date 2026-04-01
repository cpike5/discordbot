# User Stories
## Feature: User Profile Extraction

**Status:** Draft  
**Date:** 2026-04-01

---

## Guild Member Stories

### US-01 — Opt into profile extraction
**As a** guild member,  
**I want to** grant consent for the bot to build a profile from my logged messages,  
**so that** the AI assistant can give me more personalized and relevant responses.

**Acceptance Criteria:**
- [ ] `/consent grant ProfileExtraction` is available in the guild
- [ ] Command responds ephemerally with a clear explanation of what profile extraction does
- [ ] Consent is recorded with source `"SlashCommand"` and timestamp
- [ ] If I have not granted `MessageLogging` consent, I receive an error directing me to grant it first
- [ ] My consent status appears in `/consent status` alongside other consent types

---

### US-02 — View my profile
**As a** guild member who has opted in,  
**I want to** see the profile the bot has built about me,  
**so that** I know exactly what data the bot holds and can verify its accuracy.

**Acceptance Criteria:**
- [ ] `/profile view` shows my full profile in an ephemeral embed
- [ ] The embed includes: Interests, Expertise Areas, Communication Style, Activity Patterns, and Summary
- [ ] The embed shows when the profile was last extracted and how many messages were analyzed
- [ ] If my profile hasn't been generated yet, I see a message indicating it's pending
- [ ] Response appears within 2 seconds

---

### US-03 — Edit my profile
**As a** guild member,  
**I want to** correct or update fields in my extracted profile,  
**so that** the assistant uses accurate information about me.

**Acceptance Criteria:**
- [ ] `/profile edit field:<choice> value:<text>` updates the specified field
- [ ] Available fields: `interests`, `expertise`, `style`, `summary`
- [ ] Edited fields are marked as user-modified and are not overwritten by automatic re-extraction
- [ ] I receive ephemeral confirmation of the update
- [ ] Invalid or too-long values are rejected with a clear error

---

### US-04 — Delete my profile
**As a** guild member,  
**I want to** delete my profile without revoking consent,  
**so that** I can start fresh while remaining opted in.

**Acceptance Criteria:**
- [ ] `/profile delete` removes my profile from the database
- [ ] My `ProfileExtraction` consent remains active
- [ ] I receive ephemeral confirmation that my profile was deleted
- [ ] A new profile will be generated in the next extraction cycle
- [ ] If I have no profile, I see a message saying so

---

### US-05 — Trigger profile refresh
**As a** guild member,  
**I want to** manually request a profile re-extraction,  
**so that** my profile reflects my most recent conversations.

**Acceptance Criteria:**
- [ ] `/profile refresh` queues an immediate re-extraction
- [ ] I receive ephemeral confirmation that the refresh is queued
- [ ] Refresh overwrites all fields, including user-edited ones (resets `ModifiedByUser`)
- [ ] Rate limited to 2 refreshes per day per user
- [ ] If I hit the rate limit, I see a clear error with the cooldown period

---

### US-06 — Experience personalized assistant responses
**As a** guild member with an active profile,  
**I want to** receive assistant responses tailored to my interests and expertise level,  
**so that** the assistant is more useful and relevant to me.

**Acceptance Criteria:**
- [ ] When I mention the bot, the assistant's response reflects awareness of my profile (e.g., appropriate technical depth, relevant examples)
- [ ] The assistant does not explicitly reference my profile unless I ask about it
- [ ] If I ask "what do you know about me?", the assistant can summarize my profile
- [ ] The personalization is noticeably different from responses to users without profiles

---

### US-07 — Revoke consent and have data deleted
**As a** guild member,  
**I want to** revoke my profile extraction consent and have all profile data deleted immediately,  
**so that** I can fully withdraw from the feature with confidence.

**Acceptance Criteria:**
- [ ] `/consent revoke ProfileExtraction` revokes consent and deletes my profile in the same operation
- [ ] I receive ephemeral confirmation that consent was revoked and data was deleted
- [ ] The assistant no longer uses any profile context for my interactions
- [ ] The deletion is immediate — not deferred to a cleanup job
- [ ] My consent history shows the revocation with timestamp and source

---

### US-08 — Understand what data is collected
**As a** guild member considering opting in,  
**I want to** understand exactly what data will be extracted and how it will be used,  
**so that** I can make an informed consent decision.

**Acceptance Criteria:**
- [ ] `/consent status` describes `ProfileExtraction` with a clear, non-technical explanation
- [ ] The consent grant confirmation message explains: what is extracted (interests, expertise, style), how it's used (assistant personalization), and how to control it (view, edit, delete commands)
- [ ] No jargon or ambiguous language in consent-related messages

---

### US-09 — Export my profile data
**As a** guild member,  
**I want to** receive my profile data as part of a GDPR data export,  
**so that** I can exercise my right of access.

**Acceptance Criteria:**
- [ ] Data export includes a `user-profiles.json` file
- [ ] The file contains all profile fields for all guilds where I have a profile
- [ ] Metadata (extraction date, messages sampled, user-modified flag) is included
- [ ] Export format matches existing export conventions (JSON, included in ZIP)

---

### US-10 — Revoke message logging without orphaned profile
**As a** guild member with both message logging and profile extraction consent,  
**I want to** revoke message logging consent and have profile extraction automatically revoked and data deleted,  
**so that** I don't end up with an orphaned profile based on deleted message data.

**Acceptance Criteria:**
- [ ] Revoking `MessageLogging` automatically revokes `ProfileExtraction` if active
- [ ] Profile data is deleted as part of the cascade
- [ ] I receive confirmation that both consents were revoked
- [ ] Consent history records both revocations with appropriate source/reason

---

## Guild Admin Stories

### US-11 — Enable or disable profile extraction for my guild
**As a** guild admin,  
**I want to** toggle profile extraction on or off for my guild,  
**so that** I can control whether this feature is available to my community.

**Acceptance Criteria:**
- [ ] Profile extraction toggle is available in Admin → Settings → Commands
- [ ] Disabling stops new extractions for the guild
- [ ] Disabling does not delete existing profiles (they become dormant)
- [ ] Re-enabling resumes extraction on the next scheduled cycle
- [ ] The assistant does not inject profiles when the feature is disabled for the guild

---

### US-12 — View extraction metrics
**As a** guild admin,  
**I want to** see profile extraction usage and cost metrics for my guild,  
**so that** I can make informed decisions about enabling the feature.

**Acceptance Criteria:**
- [ ] Admin portal shows: users opted in, profiles generated, last extraction run
- [ ] Cost metrics shown: average cost per extraction, total cost (rolling 30 days)
- [ ] Token usage shown: average input/output tokens per extraction
- [ ] Metrics page is accessible from the guild admin dashboard

---

### US-13 — See opt-in adoption
**As a** guild admin,  
**I want to** see how many users have opted into profile extraction (without seeing individual profiles),  
**so that** I can gauge adoption and decide whether the feature is worth enabling.

**Acceptance Criteria:**
- [ ] Admin dashboard shows count of users with active `ProfileExtraction` consent
- [ ] Individual user profiles are **not** visible to guild admins (privacy)
- [ ] Count updates in real time as users grant or revoke consent

---

### US-14 — Manage feature independently of assistant
**As a** guild admin,  
**I want to** disable profile extraction without disabling the AI assistant,  
**so that** the assistant remains available but without profile personalization.

**Acceptance Criteria:**
- [ ] Profile extraction toggle is separate from the assistant enable/disable toggle
- [ ] Disabling profile extraction does not affect the assistant's ability to answer questions
- [ ] The assistant gracefully falls back to non-personalized responses when profiles are unavailable

---

## Bot Developer / Owner Stories

### US-15 — Configure extraction schedule and limits
**As a** bot developer,  
**I want to** configure the extraction interval, message sample size, and concurrency limits,  
**so that** I can balance personalization quality against API costs.

**Acceptance Criteria:**
- [ ] `ProfileExtraction:ExtractionIntervalDays` controls how often profiles are refreshed
- [ ] `ProfileExtraction:MaxMessagesSampled` controls the sample size
- [ ] `ProfileExtraction:MessageWindowDays` controls how far back messages are queried
- [ ] `ProfileExtraction:MaxConcurrentExtractions` controls parallel API calls
- [ ] All settings have sensible defaults and can be changed without redeployment (config reload)

---

### US-16 — Monitor extraction costs
**As a** bot developer,  
**I want to** track token usage and dollar cost of profile extractions,  
**so that** I can ensure the feature stays within budget.

**Acceptance Criteria:**
- [ ] Each extraction logs input tokens, output tokens, cached tokens, and estimated cost
- [ ] Aggregate metrics available: total cost per day, per guild, per extraction run
- [ ] Alerts can be configured if costs exceed a threshold (via existing observability)
- [ ] Cost data is queryable in the admin portal

---

### US-17 — Ensure profile isolation
**As a** bot developer,  
**I want to** verify that one user's profile is never injected into another user's assistant context,  
**so that** profile data remains private.

**Acceptance Criteria:**
- [ ] Profile injection uses the requesting user's Discord ID to fetch the profile
- [ ] No ambient or shared profile state exists in the assistant service
- [ ] Unit tests verify that profile injection is scoped to the requesting user
- [ ] Profile cache keys include both user ID and guild ID

---

### US-18 — Verify GDPR compliance
**As a** bot developer,  
**I want to** confirm that profile data is fully covered by existing GDPR export and purge pipelines,  
**so that** the bot remains compliant with data protection regulations.

**Acceptance Criteria:**
- [ ] `UserDataExportService` includes `UserProfile` records in its export output
- [ ] `UserPurgeService` deletes all `UserProfile` records during purge
- [ ] Purge result includes a `UserProfiles` category with the deletion count
- [ ] Consent revocation triggers profile deletion within the same transaction
- [ ] No profile data survives a full user purge (verified by integration test)
