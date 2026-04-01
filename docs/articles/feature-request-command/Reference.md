# Technical Reference: `/feature-request` Command

**Audience:** Developers, power users, and admins  
**Date:** 2026-04-01  
**Version:** 1.0

---

## Command Reference

### Slash Command

```
/feature-request description:<text>
```

| Parameter | Type | Required | Min | Max |
|---|---|---|---|---|
| `description` | string | Yes | 20 chars | 500 chars |

**Guild-only.** Cannot be invoked in DMs.

---

## Preconditions & Constraints

| Constraint | Detail |
|---|---|
| Guild must be active | `[RequireGuildActive]` — standard bot precondition |
| Rate limit | 3 submissions per user per hour per guild |
| Command module toggle | Can be disabled per-guild via Admin → Settings → Commands |
| No role restriction | Available to all guild members by default |

---

## Submission Flow

### Path A — Conversation (description < 100 characters)

1. Command responds ephemerally with two buttons: **Tell me more** and **Submit directly**
2. **Tell me more** → bot opens a DM and asks up to 3 structured questions:
   - Problem statement
   - Success criteria
   - Priority / context
3. Bot presents a confirmation summary → user confirms or cancels
4. On confirm → submission saved, doc gen enqueued

### Path B — Direct (description ≥ 100 characters)

1. Command responds ephemerally with **Submit directly** and **Tell me more** buttons
2. **Submit directly** → submission saved immediately, doc gen enqueued
3. **Tell me more** → same DM conversation as Path A

### Conversation Session

- State tracked in `IInteractionStateService` with key `feature-request:{userId}:{guildId}`
- Session TTL: 30 minutes from last activity
- On expiry: bot sends DM notification; no partial submission saved
- On cancel: state cleared, no submission saved

---

## Input Validation

Validation runs on the initial description and on each gathered answer.

| Check | Rule | Response on Failure |
|---|---|---|
| Minimum length | ≥ 20 chars (after trim) | Error embed, ephemeral |
| Maximum length | ≤ 500 chars (description), ≤ 1000 chars (answers) | Error embed, ephemeral |
| Non-empty | Rejects whitespace-only | Error embed, ephemeral |
| Control characters | Strips non-printable (preserves `\n`, `\t`) | Silently stripped |
| Unicode normalization | NFC normalization applied | Silently normalized |
| HTML content | Tags stripped | Silently stripped |

### Prompt Injection Filter

Runs before any text is stored or forwarded to the AI pipeline.

- Pattern-matched against a configurable list of known override phrases
- Entropy check for base64 or obfuscated payloads
- On detection: submission rejected, `FeatureRequestRejection` log entry written (raw payload **not** stored in the main table), user receives a generic error

---

## Doc Generation

### When It Runs

After a `FeatureRequest` record is saved with `Status = Submitted`, a background job is enqueued.

### What It Does

1. Creates a new git branch `feature-proposal/{slug}` from the configured base branch (`main` by default)
2. Generates four documentation files under `docs/feature-proposals/{slug}/`:
   - `BRD.md` — Business Requirements Document
   - `PRD.md` — Product Requirements Document
   - `UserStories.md` — User Stories
   - `Architecture.md` — High-Level Architecture Proposal
3. Commits and pushes the branch
4. Branch is **never auto-merged**; developer manually reviews and merges (or closes)

### Branch Naming

Derived from the submission title via `FeatureNameSlugifier`:
- Lowercased, spaces → hyphens, special chars stripped
- Max 50 characters
- Collision detection: appends `-2`, `-3`, etc.

Example: `"Poll command for channels"` → `feature-proposal/poll-command-for-channels`

### Subprocess Configuration

| Setting | Path | Default |
|---|---|---|
| Binary path | `FeatureRequests:DocGen:ClaudeCodeBinaryPath` | `claude` (on PATH) |
| Timeout | `FeatureRequests:DocGen:TimeoutMinutes` | `5` |
| Base branch | `FeatureRequests:DocGen:BaseBranch` | `main` |
| Branch prefix | `FeatureRequests:DocGen:BranchPrefix` | `feature-proposal/` |
| Docs base path | `FeatureRequests:DocGen:DocsBasePath` | `docs/feature-proposals/` |

### Failure Handling

| Outcome | Status set to | Admin action available |
|---|---|---|
| Success | `DocsGenerated` | Approve / Reject |
| Non-zero exit or timeout | `DocGenFailed` | Retry (re-enqueues job) |

Error output from the subprocess is stored in `FeatureRequest.DocGenError`.

---

## Database Entities

### `FeatureRequest`

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `GuildId` | `ulong` | Guild snowflake |
| `SubmittedByUserId` | `ulong` | User snowflake |
| `Title` | `string` | Derived slug-friendly title |
| `Description` | `string` | Sanitized initial description |
| `GatheredRequirements` | `string?` | JSON — answers from DM conversation |
| `ConsolidatedSummary` | `string?` | AI-consolidated summary used for doc gen prompt |
| `Status` | `FeatureRequestStatus` | See status table below |
| `ReviewedByUserId` | `ulong?` | Admin who reviewed |
| `ReviewedAt` | `DateTime?` | UTC |
| `ReviewNotes` | `string?` | Admin notes |
| `DocBranchName` | `string?` | e.g. `feature-proposal/poll-command` |
| `DocPath` | `string?` | e.g. `docs/feature-proposals/poll-command/` |
| `DocGenError` | `string?` | Subprocess stderr on failure |
| `CreatedAt` | `DateTime` | UTC |
| `UpdatedAt` | `DateTime` | UTC |

### `FeatureRequestRejection` (abuse log)

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `GuildId` | `ulong` | Guild snowflake |
| `UserId` | `ulong` | Submitter snowflake |
| `RejectionReason` | `string` | e.g. `PromptInjection`, `TooShort` |
| `CreatedAt` | `DateTime` | UTC |

Raw input is intentionally **not stored** in this table.

### `FeatureRequestStatus` Enum

| Value | Meaning |
|---|---|
| `Submitted` | Saved, doc gen queued |
| `GeneratingDocs` | Claude Code subprocess running |
| `DocsGenerated` | Docs committed to branch, awaiting admin review |
| `DocGenFailed` | Subprocess exited non-zero or timed out |
| `Approved` | Admin approved via web UI |
| `Rejected` | Admin rejected via web UI |

---

## Admin Web UI

**URL pattern:** `/guilds/{guildId}/feature-requests`  
**Authorization:** `RequireAdmin` + `GuildAccess` policies

### List Page (`/Index`)

- Paginated list, newest first by default
- Filter by `FeatureRequestStatus`
- Columns: submitter, description (truncated), status badge, created date

### Details Page (`/Details/{id}`)

- Full description and gathered requirements
- Link to `DocBranchName` on GitHub (compare view) and `DocPath`
- Actions:
  - **Approve** — sets `Status = Approved`, records reviewer + timestamp
  - **Reject** — sets `Status = Rejected`, records reviewer + timestamp
  - **Retry Doc Gen** — visible only when `Status = DocGenFailed`; re-enqueues job
- Optional notes field on all actions
- No notification sent to submitter (v1 — silent)

---

## Configuration Reference

All settings under `FeatureRequests:` in `appsettings.json` / environment variables.

| Key | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `true` | Master toggle for the feature |
| `MinDescriptionLength` | int | `20` | Minimum characters for initial description |
| `MaxDescriptionLength` | int | `500` | Maximum characters for initial description |
| `DirectSubmitThreshold` | int | `100` | Chars at or above which "Submit directly" is offered |
| `ConversationTimeoutMinutes` | int | `30` | DM conversation idle timeout |
| `RateLimitCount` | int | `3` | Max submissions per window |
| `RateLimitWindowSeconds` | int | `3600` | Rate limit window (1 hour) |
| `DocGen:Enabled` | bool | `true` | Toggle doc gen without disabling the command |
| `DocGen:ClaudeCodeBinaryPath` | string | `claude` | Path to `claude` CLI binary |
| `DocGen:TimeoutMinutes` | int | `5` | Subprocess execution timeout |
| `DocGen:BaseBranch` | string | `main` | Branch to create feature branches from |
| `DocGen:BranchPrefix` | string | `feature-proposal/` | Prefix for generated branches |
| `DocGen:DocsBasePath` | string | `docs/feature-proposals/` | Repo-relative path for generated docs |
| `InjectionPatterns` | string[] | (see below) | Phrases triggering injection filter |

Default `InjectionPatterns`:
```
ignore previous instructions
you are now
system:
[INST]
new instructions:
```

---

## Observability

| Event | Log Level | Key Fields |
|---|---|---|
| Command invoked | `Information` | UserId, GuildId, DescriptionLength |
| Validation rejected | `Warning` | UserId, GuildId, Reason |
| Injection detected | `Warning` | UserId, GuildId, MatchedPattern |
| Submission saved | `Information` | RequestId, UserId, GuildId |
| Conversation expired | `Information` | UserId, GuildId, Stage |
| Doc gen started | `Information` | RequestId, BranchName |
| Doc gen succeeded | `Information` | RequestId, BranchName, DocPath |
| Doc gen failed | `Error` | RequestId, ExitCode, Error |
| Admin review | `Information` | RequestId, ReviewerId, Decision |

All state transitions are included in the existing audit log.

---

## Security Considerations

- User-submitted text is wrapped in `<user_request>` XML delimiters before reaching the Claude Code subprocess — never interpolated into the system prompt or task instructions
- The subprocess prompt restricts Claude Code to creating files under `docs/feature-proposals/` only; source code modification, secret access, and test execution are explicitly prohibited
- `FeatureRequestRejection` records abuse patterns for review without storing the rejected payload
- The `claude` subprocess should run under a service account with read access to the full repo and write access only to `docs/` and git operations
