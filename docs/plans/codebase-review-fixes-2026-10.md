# Codebase Review Fixes (October 2026)

**Status:** Waves 1–3 done (2026-10-09). Debug and Release builds have 0 errors; tests green on PostgreSQL. Deferred items and owner decisions remain; see below.
**Date:** 2026-10-08
**Source:** [`reports/codebase-review-2026-10-08.md`](../../reports/codebase-review-2026-10-08.md). Finding IDs (H1–H11) refer to that report.
**Branch:** `claude/nice-bohr-il9uyg`

This plan turns the review findings into fixes. It groups them into waves of disjoint file sets, so each
wave can be built in parallel and tested once on the branch. Every fix lands with a test that fails before
the change and passes after it. The review found that none of the high-severity defects had such a test.

Paths are relative to `src/DiscordBot.Bot/` unless they start with another project or `docs/`.

## Decisions

Taken for this pass. Rows marked **awaiting decision** use a safe default now and need the owner's call later.

| # | Topic | Decision |
|---|---|---|
| D1 | Scheduled-message failure counter | No schema change in this pass. A missing channel is unrecoverable, so the message is **disabled at once** and the reason logged. Other failures retry as now. A per-row attempt counter needs a column and both migrations; it is listed under Deferred. |
| D2 | Send vs. save order | Advance and save the schedule **before** sending. A failed save then skips one send instead of sending twice. A duplicate in a channel is worse than a missed post. |
| D3 | Wallet floor | The lock-side balance check takes a per-call minimum from the caller. Spend and transfer use 0. Fines keep the currency's debt floor. It is not a blanket `>= 0`. |
| D4 | `execute_python` | Clear the child environment now and pass back only `PATH` and `HOME`. Change the catalogue wording from "sandbox". A real sandbox (separate user, no network) is **awaiting decision**. |
| D5 | Purge and export (H10) | Add the clearly personal tables: preferences, sound favourites, TTS presets, TTS and VOX history, DM conversation messages, notifications, activity events and logs, audio playback logs. **Retain** moderation cases, flagged events, mod-note targets and the wallet ledger, because they are guild records and the ledger is append-only. The retained rows are **awaiting decision**. A written inventory and a reflection test keep the list honest. |
| D6 | Default database in deployment | Switching `appsettings.json` and the compose files to PostgreSQL changes how existing installs start. It goes in its own PR, outside this plan. |
| D7 | Time zones for reminders and schedules | Needs a column, both migrations and a UX decision. Deferred. This pass fixes the docs so they no longer promise the user's time zone. |
| D8 | Mentions | Bot messages built from templates or user content send with `AllowedMentions` limited to users. Mass mentions (`@everyone`, `@here`) and role pings are off. |

## Waves

Each wave is built in parallel, merged onto the branch, then built and tested once in full before the next
wave starts. Shared docs (`service-catalog.md`, `feature-map.md`, `configuration-guide.md`, `data-model.md`,
`.claude/agents/*.md`) and the status notes in this plan are updated by the coordinating session, not by the
wave agents, so the agents do not conflict.

### Wave 1: high-severity defects

| Item | Finding | Fix | Test | Owner area |
|---|---|---|---|---|
| 1a | H2 | `ScheduledMessageExecutionService` creates a DI scope per message. The message is re-read by id inside that scope. | Two due messages in one cycle both send once and both save. | scheduling |
| 1b | H3, D1, D2 | Missing channel disables the message. Save the next run before sending. | Deleted channel: message disabled after one cycle, no further attempts. | scheduling |
| 1c | H11 | `ReminderExecutionService` falls back to a REST user lookup before failing. | Cache miss with a REST hit delivers the reminder. | scheduling |
| 1d | report §4 | The reminder retry path stops overwriting a reminder cancelled during delivery. | Cancel during delivery stays cancelled. | scheduling |
| 1e | report §4 | `/schedule-create` supports one-time schedules. Monthly schedules keep their original day of month. | Jan 31 monthly runs Feb 28, then Mar 31. | scheduling |
| 1f | H1 | New `TemporaryBanExpiryService` (a `MonitoredBackgroundService`) lifts expired bans and closes the case, so the case is not picked up again. A ban already lifted by hand (404) still closes the case. | Expired ban is lifted once; already-unbanned case is closed without error. | moderation |
| 1g | report §4 | `/warn` checks role hierarchy, like kick, ban and mute. | Warn against a higher role is refused. | moderation |
| 1h | H4, D3 | `LedgerRepository` re-checks the balance inside the wallet lock against a caller-supplied minimum. | Concurrent spends of the full balance: exactly one succeeds. | currency |
| 1i | H5 | `fetch_url` turns off auto-redirect and follows up to 5 hops by hand, checking each hop for private addresses. | Public URL redirecting to `127.0.0.1` is refused. | assistant |
| 1j | H6, D4 | `execute_python` clears the environment. | The child process cannot read a planted variable. | assistant |
| 1k | report §4 | Assistant rate limiter reserves a slot atomically at check time. | Parallel requests over the limit: the extra ones are refused. | assistant |
| 1l | H7 | Guild-keyed API controllers carry the `GuildAccess` policy. | Reflection test: every controller route with `{guildId}` carries `GuildAccess` or `PortalGuildMember`, or is listed as an intended exception. | web |
| 1m | H8 | Fix the `ModeratorAccess` policy name. | Every `[Authorize(Policy = …)]` name in the assembly resolves on a booted host. | web |
| 1n | report §4 | Portal sound upload enforces the per-file size limit on the API path. Upload and play get rate-limit policies. The TTS voice name is escaped in SSML. | Oversized upload is refused; voice name with a quote cannot break the SSML. | web / audio |
| 1o | H9 | One retention service covers command logs, user activity events, connection events, TTS messages, assistant usage metrics and audio playback logs, deleting in batches. Binds `UserActivityEventRetentionOptions`. | Rows older than the cutoff are deleted; newer rows stay. | data |

### Wave 2: playback, observability, mentions, privacy

| Item | Finding | Fix | Test | Owner area |
|---|---|---|---|---|
| 2a | report §4 | `PlaybackService` loop exit clears state only if it still owns it. A new loop cannot be disposed by an old one. | Play during loop exit: the new sound plays and Skip works. | audio |
| 2b | report §4 | Queue positions: the broadcast and `RemoveFromQueueAsync` agree on numbering. | Removing the first waiting item leaves the current sound playing. | audio |
| 2c | report §4 | `TtsSendPipeline` disposes only the token it registered. SSML play-live registers a token so Stop works. | Superseded request does not break Stop on the newer one. | audio |
| 2d | report §4 | `AudioStreamer` drains or kills FFmpeg before waiting for stderr on cancel. | Cancel completes within a bound. | audio |
| 2e | D8 | `AllowedMentions` on template and user-content sends (scheduled messages, welcome, others found by grep). | Sent message carries the restricted mentions. | guild / scheduling |
| 2f | report §4 | `Random.Shared` in the samplers; fix the sampling-ratio config key; set `BackgroundServiceExceptionBehavior` deliberately. | Sampler config binds from the production key. | observability |
| 2g | H10, D5 | Purge and export cover the personal tables. Privacy docs carry an inventory of every user-keyed entity. | Reflection test: every entity with a user id column is in the inventory. | identity |
| 2h | report §4 | `/join-channel` requires a precondition. | — (attribute) | audio |

### Wave 3: housekeeping

| Item | Fix |
|---|---|
| 3a | Delete the unregistered `GuildAccessAuthorizationHandler` and its nine skipped tests. Delete the unused root copies of the SSML DTOs. |
| 3b | Upgrade OpenTelemetry past 1.14.0. Add `dependabot.yml`. |
| 3c | Replace the `BeCloseTo(DateTime.UtcNow, …)` sites with `DbTimestamp.LowerBound()`. |
| 3d | Docs: `api-endpoints.md`, `service-catalog.md`, `.claude/agents/*.md`, spec status lines, `TEST_COVERAGE_GAPS.md`, `architecture-diagrams.md`, `configuration-guide.md`, reminder time-zone wording (D7). |

## Deferred, outside this plan

- Per-row failure counter for scheduled messages (needs a migration) — D1.
- Time zones for reminders and recurring schedules — D7.
- Default deployment on PostgreSQL — D6.
- Real sandbox for `execute_python` — D4.
- Retention decision for moderation cases, flagged events and the ledger — D5.
- Connection pinning for `fetch_url` (closes DNS rebinding; the redirect fix closes the main hole).
- Anti-forgery validation on cookie-authenticated API controllers. It needs every JS caller checked first.
- Trigram indexes for log search.
- Data Protection key ring at rest (`IdentityServiceExtensions.cs:45-46`, report §4). Keys are written to disk unencrypted, so anyone who can read that directory can decrypt the stored Discord OAuth tokens. Options: `ProtectKeysWithCertificate` with a certificate from configuration, or tight filesystem permissions plus documentation. This is a deployment decision.
- Command-module test coverage beyond the tests added here.

## Status notes

Updated as each wave lands.

### Wave 1: done (2026-10-08)

All 15 items landed. Full suite on the merged branch: 5,730 passed, 0 failed, 21 skipped (PostgreSQL). SQLite was not exercised.

- **1a–1e (scheduling).** A scope per message; a missing channel disables the message, but only while the client is Connected; the next run is saved before sending. Trade-off: `LastExecutedAt` now means "attempted", and a one-time message whose send fails is not retried. Reminders fall back to a REST lookup, and a cancel during delivery sticks. `/schedule-create` takes an optional UTC `start`, required for one-time schedules. Monthly schedules keep their day of month. **Open:** the anchor day is inferred, not stored. A schedule created on the 15th for the 31st settles on the 28th after February. Storing it needs a column.
- **1f–1g (moderation).** `TemporaryBanExpiryService` lifts expired bans and records an Unban case. No schema change: a later Ban or Unban case closes a ban. `/warn` checks role hierarchy. **Open:** a guild the bot has left is retried every cycle (logged at Debug). The message-context Warn modal still has no hierarchy check.
- **1h (currency).** The ledger re-checks the balance after the wallet lock against a caller floor (`LedgerFloorException`). It also fixed a lost update: the tracked wallet copy could be stale, so the wallet is reloaded under the lock.
- **1i–1k (assistant).** `fetch_url` follows up to 5 redirects by hand and checks each hop. `execute_python` gets a cleared environment and no longer claims to be a sandbox. The rate limiter reserves at check time and releases on failure.
- **1l–1n (web).** Twelve controllers got `GuildAccess` at class level; Preview got it on its two guild actions, and Autocomplete checks access by hand. **Behaviour change:** on admin API controllers, an Identity Admin now needs Discord Administrator in that guild, as the guild pages already require. `ModeratorAccess` is now `RequireModerator`. Portal upload enforces the per-file size limit. Upload and play have per-user rate limits (`PortalRateLimit`). The voice name is escaped. **Open:** Admin Logs' channel picker returns 403 for guilds the admin does not administer. Preview popups show their error state for guilds the viewer is not in.
- **1o (data).** `DataRetentionService` covers the six tables. `PerformanceMetrics:ConnectionEventRetentionDays` was never read before; it is now applied, and its default went from 7 to 30 to match the 30-day uptime view. **Note:** command logs older than 90 days drop out of all-time command counts.

### Waves 2 and 3: done (2026-10-09)

Full suite on the merged branch: 5,768 passed, 0 failed, 10 skipped (was 21 skipped). PostgreSQL only.

- **2a–2d, 2h (audio).** `PlaybackService` starts a loop only when `LoopRunning` is false, set under the guild lock. A second race also existed: two queued `PlayAsync` calls both started loops. Queue positions are 1-based end to end, and 0 skips the playing sound. The old code removed the item after the one clicked. TTS stop tokens are per request, and SSML play-live registers one. On cancel, FFmpeg is killed instead of waited on. `/join-channel` is moderator-only. **Open:** `/leave` has no precondition.
- **2e (mentions).** `SafeMentions.UsersOnly`/`ReplyOnly` on scheduled messages, plain-text welcome, the Rat Watch vote message, and assistant replies. **Behaviour change:** existing scheduled templates that use `@everyone`, `@here` or role mentions no longer ping.
- **2f (observability).** Samplers use `Random.Shared` through a seam, and two skipped tests are now deterministic. The sampling key in the production and development settings is fixed. `BackgroundServiceExceptionBehavior.Ignore` is set, and a faulted `MonitoredBackgroundService` now stays registered as Error; before, it unregistered and vanished. Hub spans carry the Discord id. **Open:** four hosted services still bypass the base class.
- **2g (privacy).** Purge and export cover the personal tables, plus `DmAssistantNote`, `FeatureRequestRejection` and `VerificationCode`. `FeatureRequest` and `UserActivityLog` targets are anonymised. Moderation cases, flagged events, mod-note targets and wallets are retained (D5, **awaiting decision**). `docs/articles/user-data-inventory.md` plus `UserDataInventoryTests` keep the list honest. **Bug fixed:** purging a former admin with portal activity always rolled back on a Restrict FK.
- **3a.** The unregistered `GuildAccessAuthorizationHandler` and its tests are gone. The live `GuildAccessHandler` got 9 tests (it had none). `UserGuildAccess` rows never granted access through the live handler, and the docs now say so. The duplicate SSML DTOs are gone. **Kept:** `Serilog.Sinks.Grafana.Loki`. The review called it unused, but the deployment env template loads it by name.
- **3b.** OpenTelemetry 1.14.0 → 1.15.3 (advisories cleared). NU1603 pins added. `dependabot.yml` added. **Still reported by the vulnerability scan:** SharpCompress and Snappier (via MongoDB.Driver.Core) and System.Text.Json 8.0.0.
- **3c.** All 21 `BeCloseTo(DateTime.UtcNow, …)` sites now use `DbTimestamp.LowerBound()`.
- **3d.** Docs pass done: API endpoints, service catalog, agent definitions, spec status lines, reminder time zones, authorization policies, patterns (mentions, background-service faults), configuration guide.

