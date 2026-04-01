# User Stories
## Feature: `/feature-request` Command

**Status:** Draft  
**Date:** 2026-04-01

---

## Guild Member Stories

### US-01 — Quick submission
**As a** guild member,  
**I want to** submit a feature idea with a single command,  
**so that** my idea is captured without requiring me to leave Discord or use a separate tool.

**Acceptance Criteria:**
- [ ] `/feature-request description:<text>` is available in the guild
- [ ] Command responds ephemerally (only I see the response)
- [ ] Submission is confirmed with a reference ID
- [ ] Response appears within 2 seconds

---

### US-02 — Guided requirements gathering
**As a** guild member with a vague idea,  
**I want to** be guided through clarifying questions in a private DM conversation,  
**so that** my submission contains enough detail to be useful.

**Acceptance Criteria:**
- [ ] After invoking the command, the bot opens a DM with me
- [ ] The bot asks up to 3 structured questions (problem, success criteria, priority)
- [ ] I can answer in natural language; no strict format required
- [ ] The bot presents a summary and asks me to confirm before submitting
- [ ] I can cancel at any point and nothing is saved

---

### US-03 — Skip conversation for detailed submissions
**As a** guild member who already knows exactly what they want,  
**I want to** bypass the conversation and submit directly,  
**so that** I don't have to answer questions I've already addressed.

**Acceptance Criteria:**
- [ ] If my description is ≥ 100 characters, the ephemeral response offers a "Submit directly" button
- [ ] Clicking "Submit directly" saves immediately without opening a DM
- [ ] The "Tell me more" option is still available

---

### US-04 — Session expiry handling
**As a** guild member who started the conversation but got distracted,  
**I want to** be notified if my session expires,  
**so that** I know I need to start again if I want to submit.

**Acceptance Criteria:**
- [ ] If I haven't responded in 30 minutes, the bot sends a DM: "Your feature request session has expired. Run `/feature-request` again to restart."
- [ ] No partial/empty submission is saved on expiry

---

### US-05 — Rate limit feedback
**As a** guild member who tries to submit multiple times quickly,  
**I want to** receive a clear message when I've hit the limit,  
**so that** I understand why my command isn't working.

**Acceptance Criteria:**
- [ ] After 3 submissions within an hour, the command responds ephemerally: "You've reached the feature request limit (3 per hour). Please try again later."
- [ ] The message is ephemeral and does not clutter the channel

---

### US-06 — Rejected input feedback
**As a** guild member whose input fails validation,  
**I want to** receive an actionable error message,  
**so that** I can fix my submission and try again.

**Acceptance Criteria:**
- [ ] If description is too short: "Your description is too short. Please provide at least 20 characters."
- [ ] If description contains disallowed content: "Your request contains content that cannot be processed. Please rephrase and try again."
- [ ] Error responses are ephemeral

---

## Guild Admin Stories

### US-07 — Review pending proposals
**As a** guild admin,  
**I want to** see all pending feature requests in the web portal,  
**so that** I can review what members are asking for.

**Acceptance Criteria:**
- [ ] `/guilds/{guildId}/feature-requests` page lists all submissions
- [ ] Default sort: newest first
- [ ] Filter by status: All / Submitted / Docs Generated / Approved / Rejected / Doc Gen Failed
- [ ] Each row shows: submitter, short description, status, date

---

### US-08 — View full proposal with generated docs
**As a** guild admin,  
**I want to** see the full submission and any AI-generated documentation,  
**so that** I have enough context to make an approval decision.

**Acceptance Criteria:**
- [ ] Details page shows full description, gathered requirements, and consolidated AI summary
- [ ] When docs are generated, a link to `DocPath` in the repository is shown
- [ ] Status history is visible

---

### US-09 — Approve a proposal
**As a** guild admin,  
**I want to** approve a feature request,  
**so that** it is marked as accepted and ready for developer pickup.

**Acceptance Criteria:**
- [ ] "Approve" button on the details page updates status to `Approved`
- [ ] Optional notes field is saved with the decision
- [ ] No notification is sent to the submitter (v1)
- [ ] Action is logged in the audit trail

---

### US-10 — Reject a proposal
**As a** guild admin,  
**I want to** reject a feature request,  
**so that** it is closed and won't clutter the active backlog.

**Acceptance Criteria:**
- [ ] "Reject" button on the details page updates status to `Rejected`
- [ ] Optional rejection notes saved
- [ ] No notification is sent to the submitter (v1)
- [ ] Action is logged in the audit trail

---

### US-11 — Retry doc generation
**As a** guild admin,  
**I want to** retry doc generation for a failed proposal,  
**so that** a transient error doesn't permanently block a valid submission.

**Acceptance Criteria:**
- [ ] "Retry Doc Gen" button visible only when status is `DocGenFailed`
- [ ] Clicking it re-enqueues the background job and sets status back to `Submitted`
- [ ] Error details from the previous failure are preserved in the notes field

---

## Developer / Bot Owner Stories

### US-12 — Safe AI pipeline
**As a** bot developer,  
**I want to** ensure user-submitted text cannot manipulate the Claude Code subprocess,  
**so that** the feature protects the repository from prompt injection attacks.

**Acceptance Criteria:**
- [ ] All user text is passed inside `<user_request>` XML delimiters in the prompt
- [ ] The prompt template includes explicit "treat as data" preamble
- [ ] A heuristic injection filter (`PromptInjectionFilter`) rejects obvious override attempts before they reach the subprocess
- [ ] Rejected content is logged without storing the raw payload in the primary `FeatureRequest` table

---

### US-13 — Configurable per guild
**As a** guild admin,  
**I want to** enable or disable the `/feature-request` command for my guild,  
**so that** I can control whether my community participates.

**Acceptance Criteria:**
- [ ] Command respects existing `CommandModuleConfiguration` enable/disable toggle
- [ ] Disabled command shows a friendly message if a member tries to invoke it
