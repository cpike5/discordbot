# Documentation Update Plan

**Date:** 2026-06-24
**Companion to:** [`DOCUMENTATION-AUDIT.md`](DOCUMENTATION-AUDIT.md) (the detailed findings with `doc → code` evidence)

## Guiding principle

**Documentation must describe what the code actually does today — not what it was intended to do.** Every change below brings a doc into line with the current implementation. Where the implementation itself looks wrong (i.e. the doc describes the more sensible behavior), that is captured separately in [§ Suspected code issues](#suspected-code-issues-doc-may-be-right-code-may-be-wrong) and is **not** silently "fixed" by editing the doc to match a bug — those need a product decision first.

## Approach decisions (make these once, up front)

1. **Spec vs. reference.** Several `docs/articles/*-spec.md` files are design specs, not behavior docs. Decide per file: (a) shipped → rewrite as reference matching code, or (b) unbuilt → add a top banner `> **Status: Proposed — not yet implemented.**` Don't leave specs that read as shipped features.
2. **Generate config docs, don't hand-write them.** The worst inaccuracies (`environment-configuration.md`, `background-services.md` config tables) come from prose written from memory. Prefer a small generator/reflection pass over the `*Options` classes (there are ~37 in `src/DiscordBot.Core/Configuration` plus a few in Bot/Infrastructure) to produce the key/default tables. `configuration-guide.md` and `CLAUDE-REFERENCE.md` track real classes and were accurate — use them as the model.
3. **Prefer symbol/route references over `file:line`.** A large fraction of the docs' line-number citations have already rotted. When editing, replace `file.cs:123` with the class/method/route name where practical.
4. **Single source of truth for the command list.** The command-name drift exists because the list is duplicated across README, `command-configuration.md`, and `commands-page`. Consider generating the command catalog from the modules (the project already has `DiscordBot.DocGen`).

---

## Phased work

Phases are ordered by user impact. Each item links to the audit section with the evidence.

### Phase 1 — Critical user-facing breakages (do first; small, high-value edits)

These make a user/operator fail immediately if followed.

| # | Doc | Change | Audit § |
|---|-----|--------|---------|
| 1.1 | `README.md`, `command-configuration.md` | Fix moderation command names: `/mod-history`→`/modlog`, `/mod-stats`→`/modstats`, `/mod-notes`→`/modnote` (delete→`remove`), `/mod-tag`→`/modtag`, `/verify`→`/verify-account`. Fix `/remind delete`→`cancel`, `/welcome setup`→`show/enable/disable/channel/message/test`, `/schedule-message …`→the five `schedule-*` commands. | §1 |
| 1.2 | `ai-assistant.md` | `Claude:ApiKey` → `Anthropic:ApiKey`; update model IDs `claude-3-*` → `claude-sonnet-4-20250514` (+ opus-4/haiku-4). | §2 |
| 1.3 | `scheduled-messages.md` | Rewrite every cron example to 6-field seconds-first form (parser uses `CronFormat.IncludeSeconds`); state the format explicitly; drop crontab.guru references that generate 5-field. **See code-issue C5 first** — confirm whether 6-field is the intended design before mass-editing. | §4 |
| 1.4 | `api-endpoints.md` | Remove the "Authentication: None (MVP)" overview line; state the real per-policy auth model. Fix health path `/api/health` → `/health`. Fix Commands API policy `RequireModerator` → `RequireViewer`. | §7 |
| 1.5 | `ssml-support.md` | Fix all `/api/portal-tts/…` routes → `/api/portal/tts/…`; remove the phantom `GET …/voices`; fix inverted preset IDs (`cheerful-jenny`→`jenny-cheerful`, etc.); fix policy `ModeratorAccess`→`PortalGuildMember`. | §3 |
| 1.6 | `bot-verification.md` | Rewrite the flow in the correct direction (run `/verify-account` in Discord → code → enter on profile page); fix config section/keys (`Verification`, not `Discord:AccountVerification`), entity name (`VerificationCode`), service name (`VerificationCleanupService`). | §5 |
| 1.7 | `consent-privacy.md` | Remove "Data Export: not implemented"; document `/privacy export-data` + web export (`UserDataExportService`, ZIP w/ 7-day link). | §5 |

### Phase 2 — Configuration surface (largest single source of error)

| # | Doc | Change | Audit § |
|---|-----|--------|---------|
| 2.1 | `environment-configuration.md` | **Regenerate** the Quick Reference table + "Additional Configuration Sections" from the actual `*Options` classes. ~15 classes currently have fabricated property names/defaults. Fix section keys (`Reminder`, `Identity`, `Notification`, `DatabaseSettings`). Add missing sections (`Mogwai`, `DmAssistant`, `FeatureRequests`, `NotX`, `LogSanitization`, `Vox`, `AzureSpeech:Ssml`, `OpenTelemetry`). | §8 |
| 2.2 | `background-services.md` | Fix config keys (`ExecutionIntervalSeconds`→`CheckIntervalSeconds`), defaults (MessageLog/SoundPlayLog retention 30→90 days; RatWatch 5min→30s), and retention property names. Add the ~10 missing hosted services. Note the `MonitoredBackgroundService` base class (it wires the health registry). | §6 |
| 2.3 | `README.md` (Dependencies) | `Anthropic.SDK 5.8.0`→`Anthropic 12.2.0`; Discord.Net `3.19.0-beta.1`→forked `Discord.Net.* 3.19.0-fork`; OpusDotNet→`OpusDotNet.opus.win-x64`; version badge `v1.0.0`→`1.5.1-dev`; refresh stale appsettings line citations. | §8 |
| 2.4 | `docker-deployment.md` | Seq port 5341→7301; sounds mount `:ro`→`:rw` (matches `docker-compose.yml`); health check `/api/health/live`→`/health`. | §8 |
| 2.5 | `README.md`, `configuration-guide.md` | Resolve `Elastic:ApiKey` — confirm whether it's `ElasticSearch:ApiKey` (the real Serilog sink key) and correct the secret-setup instructions. | §8/§9 |

### Phase 3 — Behavior corrections (feature docs that mislead)

| # | Doc | Change | Audit § |
|---|-----|--------|---------|
| 3.1 | `log-aggregation.md` | Replace the fictional `ElasticOptions`/`Elastic:*`/`Serilog:WriteTo[n]` model with the real wiring: `ElasticSearch:Url`, `ElasticSearch:ApiKey`, `Observability:SeqUrl`, data stream `logs-discordbot-{env}`. | §9 |
| 3.2 | `alerting-system.md` | Correct every default threshold to the seeded values (`AddPerformanceAlerts.cs`); fix `api_rate_limit_usage` unit (%, not count); remove `DuplicateSuppressionMinutes`. **See code-issue C3.** | §9 |
| 3.3 | `metrics.md` | Fix `feature.usage` unit `{usages}`→`{events}`; reconcile `users.unique` description; document the `DiscordBot.Vox` meter; remove/flag the non-functional `OpenTelemetry:Metrics` toggles (**code-issue C4**). | §9 |
| 3.4 | `tracing.md` | Remove the "user error → Ok span status" claim (any exception sets `Error`). | §9 |
| 3.5 | `reminder-system.md` | State that reminder times are parsed in **UTC** (not guild timezone); fix the admin route `/Guilds/Reminders/{guildId}`; correct the parsing description (regex-based, not `DateTime.TryParse`; numeric-month dates unsupported); add the missing keywords. **See code-issue C1.** | §4 |
| 3.6 | `scheduled-messages.md`, `timezone-handling.md` | Document the real `ScheduledMessagesController` REST API; remove `ReminderOptions.ExecutionTimeoutSeconds`; fix the entity-field example (`NextExecutionAt`/`IsEnabled`). | §4 |
| 3.7 | `identity-configuration.md`, `authorization-policies.md` | Fix cookie settings (`SameAsRequest`/`Lax`/7-day — note Lax is required for OAuth); `RequiredUniqueChars` 4→1; remove default-admin fallback creds; fix seeder path/method; OAuth scopes (`identify`,`email`,`guilds`); rewrite guild-auth to match `GuildAccessHandler` (live Discord membership, not `UserGuildAccess`). **See code-issue C2.** | §5 |
| 3.8 | `user-management.md` | Password min length 6→8. | §5 |
| 3.9 | `soundboard.md`, `unified-now-playing.md` | Fix per-guild defaults (5MB/50/100MB/5min); correct portal response shapes & status codes; fix the now-playing usage matrix (Soundboard is compact, no progress bar); remove non-existent service calls. | §3 |
| 3.10 | `service-architecture.md` | Replace the fictional `IAuditLogBuilder` API with the real `.ForCategory()/.WithAction()/…/.LogAsync()`/`Enqueue()` (already correct in `audit-log-system.md`). | §6 |
| 3.11 | `search.md` | Rewrite for the provider architecture (206-line orchestrator + 9 `ISearchProvider`); fix `SearchCategory` enum order. | §6 |
| 3.12 | `member-directory.md` | Fix CSV columns (add `Discriminator`, correct order, two `;`-delimited role columns); cache key/default (`GuildMemberListDurationMinutes`=5); remove the page-level Export route; minor JS/page-size fixes. | §10 |
| 3.13 | `welcome-system.md`, `README.md` | Remove the phantom `/welcome test [user]` parameter. | §10 |

### Phase 4 — Completeness (fill documented gaps)

| # | Doc | Change | Audit § |
|---|-----|--------|---------|
| 4.1 | `README.md`, command docs | Document `/notx` group, `/unban`, `/feature-request`, `/tts-styled`, `/case`/`/reason`/`/modexport`, `/privacy export-data`, `/modtag create|delete`, and `/ban` temp-ban params. | §1 |
| 4.2 | `api-endpoints.md`, `signalr-realtime.md` | Add the ~10 undocumented controllers (Analytics, Audio, Notifications, PerformanceTabs, Preview, PortalSoundboard, PortalTts, UserPreferences, BulkPurge, Sounds) and the undocumented hub methods/events; remove phantom `GuildUpdated`. | §7 |
| 4.3 | `database-schema.md` | Add the 16 missing entities/tables + `MessageLogs.ChannelName`; correct `MessageLog` "not stored" note. | §6 |
| 4.4 | `ai-assistant.md`, `assistant-tool-catalog.md` | Document the real tool providers/tools (RatWatch on the guild assistant, the DM provider set); remove phantom providers/tools; fix `get_user_profile`/`get_user_roles` params and the `ToolContext` type; document the `DmAssistant` options + `IDmToolProvider`. | §2 |
| 4.5 | `audit-log-system.md` | Add `AuditLogAction` 20-22; fix the `Services/Audit/` paths. | §6 |

### Phase 5 — Hygiene

- **5.1** Apply the spec/reference decision (§ Approach #1) to `voice-favorites-spec.md` (unbuilt → banner), `voice-capability-system.md` (shipped → reframe/archive), and the `vox-*-spec.md` "Future Expansion" sections that already ship.
- **5.2** Refresh the stale `.claude/agents/data-infrastructure` definition ("72 DbSets"→61, "919-line SearchService"→provider pattern, wrong `IAuditLogBuilder` API) per the project's agent-maintenance rule.
- **5.3** Sweep remaining `file:line` citations during the above edits; replace with symbol/route names.

---

## Suspected code issues (doc may be right, code may be wrong)

These are cases where the **documentation describes the more sensible/intended behavior and the implementation looks like the actual defect.** Per the guiding principle, the docs should still be updated to describe current behavior — but each of these warrants a code decision first, and possibly a bug ticket. **Do not bury these by quietly editing the doc to match the bug.** All verified against code.

| ID | Severity | What the doc says | What the code does | Why it smells like a code bug |
|----|----------|-------------------|--------------------|-------------------------------|
| **C1** | High (functional) | Reminders are parsed in the guild's configured timezone. | `ReminderModule.cs:83` hardcodes `const string timezone = "UTC"` (with a `// future enhancement` comment), even though `TimeParsingService.Parse` already accepts a timezone. | The plumbing exists and is bypassed. Users in non-UTC guilds get reminders at the wrong wall-clock time. The doc describes what users almost certainly expect. **Recommend:** wire the guild timezone through; until then, doc must say UTC. |
| **C2** | High (cost/safety) | Mogwai enforces a per-invocation spend cap via `--max-budget-usd` (`MogwaiOptions.MaxBudgetUsd`, default \$5, set in appsettings). | `ClaudeCodeToolProvider` never passes `--max-budget-usd` to the CLI (confirmed: no occurrence in `src/` except the option's own XML comment). | A configured, defaulted spend cap that silently does nothing is a real safety gap for a feature that shells out to a paid CLI. **Recommend:** either pass the flag or remove the option; flag for a fix, not just a doc edit. |
| **C3** | Medium (broken alert) | `api_rate_limit_usage` alerts at 85%/95% (threshold unit `%`). | `MetricValueCollector.GetApiRateLimitUsage()` returns a **raw count** of rate-limit events in the last hour (comment: "as a count of rate limit hits"), but the seeded threshold is a **percentage** (85/95). | Comparing an event count against an 85% threshold means the alert fires/doesn't-fire on meaningless math. Internal code inconsistency. **Recommend:** decide the intended semantic (count vs %) and align collector + seed; doc should match whatever is chosen. |
| **C4** | Medium (config no-op) | `OpenTelemetry:Metrics.Enabled` / `IncludeRuntimeMetrics` / `IncludeHttpMetrics` control metric collection. | `OpenTelemetryExtensions` adds all instrumentation unconditionally and never reads these keys (only `ServiceName`). | Config toggles that don't toggle anything. Either honor them or remove them. **Recommend:** remove the keys (and doc) or wire them up. |
| **C5** | Medium (UX/strictness) | Scheduled-message cron uses standard 5-field expressions; in-product `/schedule-create` examples are 5-field. | Parser uses `CronFormat.IncludeSeconds` → **requires 6 fields**; all 5-field inputs throw. | Requiring seconds precision is unusual and undocumented in the command help. This may be an oversight (most schedulers default to 5-field). **Recommend:** confirm intent — if 5-field was intended, this is a code fix, not a doc fix. Either way the `validate-cron` endpoint and command help should agree with the doc. |
| **C6** | Low (dormant feature) | Guild authorization uses a `UserGuildAccess` table with `GuildAccessLevel` comparison. | The registered `GuildAccessHandler` checks live Discord membership and ignores `UserGuildAccess` entirely; the entity/enum exist but no handler consumes them. | Looks like an abandoned or half-migrated design (dead entity table). **Recommend:** confirm whether `UserGuildAccess` should be removed or wired in; doc currently describes the dormant design. |
| **C7** | Low (security posture) | Verification codes are stored hashed (PBKDF2) with constant-time comparison. | `VerificationCode.Code` is stored as **plaintext**; validation compares the raw code. | Codes are short-lived one-time tokens (24h cleanup, rate-limited), so impact is limited — but the doc describes a stronger design than exists. **Recommend:** note as a minor hardening opportunity; doc should say plaintext for now. |
| **C8** | Low (dead config) | OAuth scopes are configurable via `DiscordOAuthOptions.Scopes`. | `AddDiscordOAuth` hardcodes `identify`,`email`,`guilds`; the `Scopes` property is never read. | A config property with no effect. **Recommend:** remove the property or honor it; don't document it as configurable. |
| **C9** | Note (code is *safer* than doc) | A default admin (`admin@example.com` / `Admin@123456`) is seeded if not configured. | No fallback exists; without `Identity:DefaultAdmin`, no admin is seeded. | Here the **code is correct and the doc is dangerous** — documenting default credentials would invite a real vulnerability. Just fix the doc (already in 3.7); flagging so the "fix" isn't reversed. |

### Suggested handling

- For **C1–C5**, open issues and link them from the relevant doc with a short "Known limitation" note, so the doc is accurate *and* the gap is tracked rather than blessed.
- For **C6–C8**, low-priority cleanup — bundle into a "dead config/feature" sweep.
- **C9** needs no code work — just ensure the doc correction lands.

---

## Effort & sequencing notes

- **Phase 1** is ~7 small, surgical edits — highest ROI, do in one pass.
- **Phase 2** is the biggest writing effort; invest in the config generator (Approach #2) so it stays correct.
- **Phases 3–4** can be parallelized by domain (the same split used for the audit: assistant / audio / scheduling / identity / data / web / observability / moderation).
- **Phase 5** is cleanup, low urgency.
- Recommend doing each phase as its own PR (or at least its own commit series) so reviewers can verify doc-vs-code per domain.
