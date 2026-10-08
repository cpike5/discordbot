# DiscordBot codebase review: defects, gaps and recommendations

**Date:** 8 October 2026 · **Commit:** `79f291a` · **Prepared by:** Claude Code, from 11 parallel read-only exploration passes, plus a build, the full test run and package audits

**Status:** Findings and recommendations. Read-only review; no application code was changed. This is not an implementation plan.

---

## The short version

- **The build and the tests are green.** Debug build: 0 errors, 138 warnings. Tests: 5,651 passed, 0 failed, 21 skipped.
- **Most of the September refactoring list is done.** Ten of the twelve recommendations from [`architecture-review-2026-09-04.html`](architecture-review-2026-09-04.html) shipped in full or in large part; the other two are partly done. Section 2 has the scorecard.
- **The real risk now is behaviour, not structure.** This pass found 11 high-severity defects. Each one was confirmed by reading the cited lines. The worst ones break user-visible promises: temporary bans never lift, scheduled messages can fail and resend, and wallets can overdraw.
- **Two assistant tools are too trusting.** `fetch_url` follows redirects to internal addresses. `execute_python` gives its child process the bot's environment variables, and in Docker those hold the secrets.
- **Six high-volume tables grow forever.** Their retention code is missing or never called.
- **Nothing in the bot sets `AllowedMentions`.** Scheduled messages and plain-text welcome messages can ping `@everyone`.
- **Command modules are still almost untested.** One of 34 has a direct test.

| | |
| --- | --- |
| High-severity findings, verified | **11** |
| Medium-severity findings | **22** |
| September recommendations done or mostly done | **10 of 12** |
| Tests passing / skipped | **5,651 / 21** |
| Command modules with a direct test | **1 of 34** |
| Build warnings | **138** |

---

## 1. Method and how to read this

- Eleven Haiku sub-agents each audited one area with a fixed checklist. The areas were the assistant, moderation, audio, scheduling, identity, web/API, data, guild/community, observability, tests and docs. Each agent reported `path:line` evidence.
- **I opened every high-severity claim at the cited lines before including it.** Five agent claims failed that check and are left out or downgraded (section 7).
- Medium and low findings come from the agents. Each one quotes the code it saw. Where I did not re-check a medium finding, it carries the mark *(agent)*.
- Severity is about the impact on users or operators, not code quality. Effort: **S** is under half a day, **M** is one to three days, **L** is longer.
- I ran the build with `AllowMissingTailwindCss=true` and the test suite against the local PostgreSQL 16. SQLite was not exercised (see section 6).

---

## 2. Status of the September 2026 recommendations

| # | Recommendation (4 Sept) | Status now | Evidence |
| --- | --- | --- | --- |
| 01 | Extract a moderation action pipeline | **Mostly done** | `Services/Moderation/ModerationActionRunner.cs` exists. `ModerationActionModule.cs` fell from 1,002 to 433 lines. `/purge` and the message-context "Warn User" modal still run inline (`ModerationActionModule.cs:195`, `:345-433`). |
| 02 | Merge the guild and DM assistant handlers | **Mostly done** | `AssistantMessagePipeline` is shared by both services. The two handlers still duplicate typing, error replies and chunking. |
| 03 | Paged and no-tracking helpers on `Repository<T>` | **Partly done** | `GetPagedAsync` exists but is `protected` and does not call `AsNoTracking` (`Repository.cs:496`). |
| 04 | Register repositories by convention; move the provider contexts | **Mostly done** | Infrastructure scans its own assembly (`ServiceCollectionExtensions.cs:128-146`). About 18 hand-typed repository lines remain in `AssistantServiceExtensions.cs:62-110`. The contexts have their own files. |
| 05 | Fix the duplicate `SsmlBuildRequest`; fix the stale UI agent doc | **Done** | One `SsmlBuildRequest` remains. `.claude/agents/web-ui-portal.md` no longer names HTMX. Three more duplicate DTOs remain, unused (section 5). |
| 06 | Split the portal controllers | **Done for TTS and Soundboard** | `PortalTtsController` is now five controllers (largest 569 lines). Soundboard is four. `PerformanceMetricsController` is still 1,009 lines. |
| 07 | Break up `DashboardHub` | **Done** | 680 lines, 5 dependencies (was 1,195 and 15). |
| 08 | Split `InteractionHandler` and `BotHostedService`; document hosted-service order | **Partly done** | The order is documented in `docs/articles/background-services.md`. The handler is 650 lines, the hosted service 681. |
| 09 | Split the DTO bundles and the oversized interfaces | **DTOs done** | `ModerationDtos.cs` and `PerformanceMetricsDtos.cs` no longer exist. The interface splits were not checked. |
| 10 | Move aggregation out of the heavy page models | **Done for Settings** | `Admin/Settings.cshtml.cs` is 381 lines (was 1,000). |
| 11 | Shared JavaScript API client | **Done** | `wwwroot/js/api-client.js` loads in the layout and sends the anti-forgery header. |
| 12 | Externalise the VOX page scripts; decide on `Blazor/` | **Done** | `Portal/VOX/Index.cshtml` is 1,664 lines with one inline script. Commit `dbd59ce` removed `Blazor/`. |

---

## 3. High-severity findings (verified)

### H1. Temporary bans never lift
`Services/Moderation/ModerationActionRunner.cs:276-283` stores a ban with a `Duration`, and the DM tells the user "You have been temporarily banned". **Nothing ever unbans them.** `ModerationService.GetExpiredTemporaryActionsAsync` (`ModerationService.cs:341`) has no caller anywhere in `src/` or `tests/`. The only unban is the manual `/unban` command.
**Fix:** a `MonitoredBackgroundService` that polls expired bans, calls `RemoveBanAsync` and closes the case. **Effort M.**

### H2. Scheduled messages share one DbContext across parallel sends
`ScheduledMessageExecutionService.cs:94-96` resolves one scoped `IScheduledMessageService` for the cycle. It then runs up to `MaxConcurrentExecutions` (default **5**) sends on it at once (`:120-128`). When two messages are due together, their `SaveChanges` calls collide on one `DbContext`. The catch at `ScheduledMessageService.cs:519` returns `false`, but the Discord message has already gone out. **The next cycle sends it again.**
**Fix:** create a scope per message, as `ReminderExecutionService` already does. **Effort S.**

### H3. A broken scheduled message fails every minute, forever
`ScheduledMessageService.cs:427-433` returns `false` when the channel is gone. The catch at `:519` does the same. Neither path changes the row, so `GetDueMessagesAsync` selects it again every cycle (default 60 s). **No attempt counter and no auto-disable exist.** The same file sends before it saves (`:440` then `:515`), so a save failure or timeout also resends.
**Fix:** count failures and disable the row after N failures. Advance `NextExecutionAt` before sending, or record the send first. **Effort M.**

### H4. Wallets can overdraw under concurrent spends
`WalletService.cs:148-160` (spend) and `:229-239` (transfer) check `CachedBalance` **before** taking the row lock. `LedgerRepository.AppendCoreAsync` (`:220-231`) takes `FOR UPDATE`, then writes `wallet.CachedBalance + row.Amount` **without re-checking the balance**. Two spends that arrive together both pass the pre-check, and the wallet goes negative. Each `/pay` mints a fresh GUID idempotency key (`WalletModule.cs:240`), so a quick second command is enough. `Wallet` has no concurrency token. The fine path has the same shape (agent: `:322-324`).
**Fix:** re-check the balance inside the lock in `AppendCoreAsync`, using a per-row "may not go below X" rule. **Effort M.**

### H5. `fetch_url` follows redirects to internal addresses
`WebFetchToolProvider.cs:109` resolves DNS and blocks private ranges, then `:122` calls `GetAsync`. The named client (`DmAssistantServiceExtensions.cs:68-73`) keeps the default `AllowAutoRedirect = true`. **A public URL that redirects to `169.254.169.254` or `localhost` passes the check.** A second DNS lookup at connect time also allows DNS rebinding.
**Fix:** disable auto-redirect and follow redirects by hand, re-checking each hop. Better, pin the connection to the checked IP with a `SocketsHttpHandler.ConnectCallback`. **Effort S.**

### H6. `execute_python` gives its child process the bot's secrets
`CodeExecutionToolProvider.cs:88-96` starts Python with a default `ProcessStartInfo`, so the child process inherits every environment variable. In the Docker deployment, secrets are environment variables: `deployment/discordbot.env.template` sets `Discord__Token` and `Discord__OAuth__ClientSecret`, and the OpenRouter key is passed the same way when configured. `ToolCatalog.cs` calls this tool "a sandbox". The DM assistant is owner-only. Even so, a page read by `fetch_url` can carry a prompt injection that makes the model run code that reads `os.environ` and sends it out.
**Fix:** call `psi.Environment.Clear()` and pass back only `PATH` and `HOME`. Run Python as a separate low-privilege user or in a container with no network. Otherwise, drop the word "sandbox". **Effort S** (environment), **M** (real sandbox).

### H7. `AudioController` controls voice in any guild
`Controllers/AudioController.cs:13-15` is `[Route("api/guilds/{guildId}/audio")]` with only `[Authorize(Policy = "RequireViewer")]`. Join, leave, stop and dequeue (`:53`, `:100`, `:145`, `:175`) take `guildId` with **no `GuildAccess` check**. Viewer is a global role that admins grant. So a Viewer for one guild can drive the bot's voice in every guild. Several Viewer-gated read controllers have the same shape (agent: Analytics, Sounds, Preview, Alerts, Autocomplete).
**Fix:** add `[Authorize(Policy = "GuildAccess")]` to guild-keyed controllers, as the guild Razor pages already do. **Effort S.**

### H8. `synthesize-ssml` always fails with a 500
`PortalTtsSynthesisController.cs:98` requires `[Authorize(Policy = "ModeratorAccess")]`. **No such policy is registered.** `IdentityServiceExtensions.cs:261-289` defines only `RequireSuperAdmin`, `RequireAdmin`, `RequireModerator`, `RequireViewer`, `GuildAccess` and `PortalGuildMember`. ASP.NET Core throws `InvalidOperationException` for an unknown policy. No page in `wwwroot/` or `Pages/` calls the endpoint today, so it is broken but unused.
**Fix:** use `RequireModerator`, or delete the endpoint. Add a test that resolves every `[Authorize(Policy=…)]` name in the assembly. **Effort S.**

### H9. Six high-volume tables have no retention
These tables have no cleanup job:

| Table | Evidence |
| --- | --- |
| `CommandLogs` | `CommandLogRepository` has no delete method. Only the manual bulk purge touches it. |
| `UserActivityEvents` | `DeleteOlderThanAsync` exists (`UserActivityEventRepository.cs:113`) but nothing calls it. `UserActivityEventRetentionOptions` is never bound. |
| `ConnectionEvents` | `CleanupOldEventsAsync` exists (`ConnectionEventRepository.cs:98`) but nothing calls it. It also loads every expired row into memory. |
| `TtsMessages` | `DeleteOlderThanAsync` has no caller (`TtsMessageRepository.cs:233`). |
| `AssistantUsageMetrics` | `DeleteOlderThanAsync` has no caller (`AssistantUsageMetricsRepository.cs:177`). |
| `AudioPlaybackLogs` | No retention service references it. |

**Fix:** one retention service in the style of `SoundPlayLogRetentionService` that covers all six, with options in `configuration-guide.md`. **Effort M.**

### H10. GDPR purge and export miss many user-keyed tables
`UserPurgeService.cs:96-240` purges about 20 tables. It does not reference `UserPreference`, `UserSoundFavorite`, `UserTtsPreset`, `TtsMessageHistory`, `VoxMessageHistory`, `AudioPlaybackLog`, `Wallet`, `DmConversationMessage`, `UserActivityEvent`, `UserActivityLog`, `MemberActivitySnapshot` or `UserNotification`. I confirmed by grep that the service does not name `UserPreference`, `Wallet` or `UserSoundFavorite`. I did not check whether any of these cascade from `ApplicationUser`. `UserDataExportService.cs:98-116` has the same gaps. Some records may need to stay for legal reasons: `ModerationCase`, `FlaggedEvent` and the wallet ledger. That needs a decision and a written rule.
**Fix:** one table in the privacy docs that lists every user-keyed entity as *purged*, *anonymised* or *retained (reason)*. Add a test that fails when a new entity with a user ID column is missing from that table. **Effort M.**

### H11. Reminders fail when the user is not in the socket cache
`ReminderExecutionService.cs:206-213` calls `_client.GetUser(reminder.UserId)`. That method reads only the gateway cache. `DiscordServiceExtensions.cs` requests the `GuildMembers` intent but sets `AlwaysDownloadUsers = false`, so the member lists are never downloaded. After a restart, a user who has not appeared in a gateway event is not in the cache. **The reminder is marked `Failed` on the first try, with no retry.**
**Fix:** fall back to `_client.Rest.GetUserAsync` (or `GetUserAsync` with REST) before failing. **Effort S.**

---

## 4. Medium-severity findings

### Safety and security
| Finding | Where | Effort |
| --- | --- | --- |
| **No `AllowedMentions` anywhere in `src/`** (grep: 0 hits). Scheduled messages (`ScheduledMessageService.cs:440`) and plain-text welcome messages (`WelcomeService.cs:276`) can ping `@everyone` and roles. The authors are admins, but a recurring mass ping is one typo away. | bot-wide | S |
| API controllers do not validate anti-forgery tokens. Only `GuildTtsPresetsController` and `LlmModelsController` use `[ValidateAntiForgeryToken]`, and there is no global filter. `SameSite=Lax` cookies limit this to same-site attackers. `api-client.js` already sends the header, so a global `AutoValidateAntiforgeryToken` filter for cookie-auth APIs is cheap. | `Controllers/` | M |
| The portal sound upload has no per-file size limit on the API path. The 5 MB `MaxFileSizeBytes` is checked only on the Razor page (`Guilds/Soundboard/Index.cshtml.cs:319`). The API uses only an extension whitelist, and the framework default request limit applies. *(agent)* | `PortalSoundboardSoundsController.cs:224-266` | S |
| The upload and play endpoints have no rate limit. Both carry `// TODO: Add rate limiting`. | `PortalSoundboardSoundsController.cs:221`, `PortalSoundboardPlaybackController.cs:47` | S |
| The voice name goes into SSML without escaping: `$"<voice name=\"{options.Voice}\">"`. `SsmlBuilder` escapes; this path does not. The agent found no voice allowlist upstream. | `AzureTtsService.cs:544` | S |
| `/warn` has no role-hierarchy check. Kick, ban and mute have one. The hierarchy check also fails open when the moderator is not a `SocketGuildUser` (`InteractionModerationCommandContext.cs:29`). *(agent)* | `ModerationActionRunner.cs:77`, `:169` | S |
| `/join-channel` has no precondition, so any member can move the bot to another voice channel. *(agent)* | `VoiceModule.cs:127` | S |
| The Data Protection key ring is stored on disk with no `ProtectKeysWith*`. Anyone who can read that directory can decrypt every stored Discord OAuth token. | `IdentityServiceExtensions.cs:45-46` | S |
| The assistant rate limiter checks before the run and records after it succeeds, and `entry.Count++` has no lock. Parallel requests from one user all pass. *(agent)* | `AssistantService.cs:119`, `:155`; `AssistantRateLimiter.cs:59-70` | S |
| `OpenTelemetry.Api` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.14.0 have moderate advisories (`GHSA-g94r-2vxg-569j`, `GHSA-4625-4j76-fww9`). | `DiscordBot.Bot.csproj` | S |

### Correctness
| Finding | Where | Effort |
| --- | --- | --- |
| **The soundboard playback loop can exit and restart in a race.** The empty-queue branch releases the guild lock and returns. The `finally` block then takes the lock again and sets `IsPlaying = false` and disposes the CTS. A `PlayAsync` that runs in that gap starts a new loop, and the old `finally` disposes that loop's CTS. Two loops can then play at once, or **Skip** stops working. | `PlaybackService.cs:385-395`, `:449-455` | S |
| **The queue numbers are off by one.** `BroadcastQueueUpdate` numbers the waiting items from 0. `RemoveFromQueueAsync` treats position 0 as "skip the current sound". Removing the first waiting item from the panel skips the sound that is playing. | `PlaybackService.cs:279`, `:553-555` | S |
| The TTS pipeline `finally` removes and disposes whatever CTS is registered for the guild. A superseded request can dispose the newer request's CTS, which breaks **Stop**. *(agent)* | `Tts/TtsSendPipeline.cs:371-374` | S |
| SSML "play live" never registers a CTS, so **Stop** cannot stop it. *(agent)* | `PortalTtsSynthesisController.cs:294-329` | M |
| On cancel, the audio streamer waits for stderr EOF without draining stdout. FFmpeg can block on a full pipe, which freezes the guild queue. *(agent)* | `Audio/AudioStreamer.cs:255-303` | S |
| A reminder cancelled during delivery can come back. The retry path writes a stale `AsNoTracking` snapshot with a full-row `Update`, which resets `Status` to `Pending`. *(agent)* | `ReminderExecutionService.cs:266-292` | S |
| `/schedule-create` cannot create a one-time schedule. `Once => null` (`ScheduledMessageService.cs:358`), and the command treats `null` as "Failed to calculate next execution time". | `ScheduleModule.cs:176-181` | S |
| Monthly schedules drift. A schedule on the 31st moves to the 28th in February and stays there. | `ScheduledMessageService.cs:362` | S |
| **Time zones:** `/remind` hard-codes `"UTC"` (`ReminderModule.cs:82-83`), although the docs say "in user's timezone". Recurring schedules carry no time zone, so a daily local-time schedule shifts by an hour across DST. | `ReminderModule.cs`, `ScheduledMessage` entity | M–L |
| `MonitoredBackgroundService` logs and rethrows. No `BackgroundServiceExceptionBehavior` is set, so .NET 8's default `StopHost` applies: **one escaped exception stops the whole bot.** The loops checked have their own try/catch, so this is a backstop, but the choice should be deliberate. | `MonitoredBackgroundService.cs:63-68` | S |
| `PrioritySampler` and `ElasticApmTransactionFilter` share a `new Random()` across threads. A `Random` used from many threads can get stuck returning 0. Use `Random.Shared`. | `Tracing/PrioritySampler.cs:16`, `ElasticApmTransactionFilter.cs:19` | S |
| `appsettings.Production.json:53` sets `OpenTelemetry:Tracing:SamplingRatio`. The code binds `OpenTelemetry:Tracing:Sampling` (`OpenTelemetryExtensions.cs:172`), so the setting has no effect. | config | S |

---

## 5. Lower-severity findings

**Data and performance**
- Message log, command log and audit log searches use `ToLower().Contains(...)` on the largest tables, with no trigram index (`MessageLogRepository.cs:221`, `CommandLogRepository.cs:351`, `AuditLogRepository.cs:90`). Add `pg_trgm` GIN indexes, or full-text search, once the tables grow.
- `Admin/RatWatchAnalytics.cshtml.cs:145-147` reads `.Result` after `Task.WhenAll`. It is safe, but it hides intent. `VoxClipLibrary.cs:304-313` blocks with `GetAwaiter().GetResult()` on pool threads, which is a real starvation risk.
- Rat Watch finalisation has no compare-and-set. Two callers can race. A unique FK probably turns the second one into an exception, not a duplicate.
- Snowflake `ulong` fields in about 65 Core DTOs serialise as JSON numbers. No converter writes them as strings. Razor pages quote their IDs correctly, but any JavaScript that reads an ID from an API response loses precision. Trace the consumers, or add a global `ulong`-as-string converter.

**Hosting and CI**
- No `dependabot.yml` and no CodeQL workflow. Actions are pinned to tags, not SHAs.
- `docker-compose*.yml` default the Postgres password to a weak literal and publish port 5432 on the host.
- `Dockerfile` downloads libdave with no checksum and pipes the NodeSource script to `bash`.
- `/health` maps `Degraded` to 200, and a dropped gateway is `Degraded`. The container never goes unhealthy when Discord disconnects.
- Seven hosted services bypass `MonitoredBackgroundService`, and no health check reads `BackgroundServiceHealthRegistry`.
- `npm audit` lists 11 advisories (9 high). They are all in the Tailwind build chain (`postcss`, `braces`, `nanoid`…), so none reach the runtime. `package.json` also lists about 50 transitive packages as direct `dependencies`, which looks like an accident.
- Build warnings: 48 × `CS8625` and 32 × `CS8602` (nullability), 38 × `CS0618` (26 of them `Role.Color`, which Discord has deprecated; it still returns the primary colour).

**Dead or duplicate code**
- `Authorization/GuildAccessAuthorizationHandler.cs` is never registered: no file in `src/` references it except a doc comment (`GuildAccessHandler` is the live one). Nine skipped tests target it. Delete both.
- `SsmlSynthesisRequest`, `SsmlValidationRequest` and `SsmlBuildResponse` exist in both `Core/DTOs/` and `Core/DTOs/Tts/`. The root copies are unused.
- `Serilog.Sinks.Grafana.Loki` is referenced but never configured. `LogSanitizationOptions.Enabled` is never read.

**Features half-built against their plans**
- VOX history: replay ignores the stored clip group and word gap, and nothing prunes history to the planned 20 entries (`portal-vox.js:1047`). The favourite endpoint toggles instead of setting a value, so a double-click undoes it (`PortalVoxController.cs:617`).
- The TTS portal still polls `/status` every 5 s. The unified now-playing spec lists SignalR push as a Must-Have.
- Auto-mute is fixed at one hour (`AutoModerationHandler.cs:279`); the requirements say it is configurable.
- Currency reconciliation runs only on demand (`CurrenciesController.cs:523`); the spec promises a daily check. Direct spends ignore open holds.
- The assistant plan items that remain: a `section` argument for `get_feature_documentation`, and rolling cache markers.

---

## 6. Tests, docs and the SQLite phase-out

**Tests.** 340 test files, 5,672 tests.
- **Command modules:** one of 34 has a direct test (`ConsentModuleTests`). `TtsModule` (563 lines), `UtilityModule` (494) and `ModTagModule` (478) have none. None of the high-severity defects in section 3 has a test that would have caught it.
- **Flaky patterns that CLAUDE.md forbids:** 21 `BeCloseTo(DateTime.UtcNow, …)` sites, including database-backed ones in `UserPurgeServiceTests.cs:116`. There are also about 10 `Thread.Sleep` waits in plain tests (`ConnectionStateServiceTests`, `InteractionStateServiceTests`).
- **Skips:** 21 tests skip. Nine of them target the dead handler above. Two point at issue #636, and one at a hard-coded 10 s startup delay.
- `docs/TEST_COVERAGE_GAPS.md` still quotes 3,216 tests across 182 files. Its command-module section is still accurate.

**Docs drift.**
- `api-endpoints.md` omits `api/analytics`, `api/preview` and `api/portal/preferences/{guildId}`.
- `service-catalog.md` omits about 16 services, among them `ModerationActionRunner`, `AssistantMessagePipeline`, `TtsSendPipeline` and the `*ToolProvider` classes.
- `.claude/agents/audio-voice.md` names `PortalSoundboardController`, and `analytics-observability.md` names `HealthController`. Neither class exists.
- `docs/architecture-diagrams.md` still says the UI uses HTMX and Alpine.js.
- Several specs say *Draft* or *Proposed* but have shipped: `soundboard-export-feature`, `ssml-ui-spec`, `issue-319-timezone-fix` and `llm-model-management-plan`. `agent-tooling-improvements.md` says "Nothing in this document is implemented", and much of it is.
- `UserActivityEventRetentionOptions` is not in `configuration-guide.md`. It is also unused (H9).
- 22 of the 29 `ToolCatalog` entries have no `docs/tools/` page. These are the older provider-style tools, and `ToolContractTests` does not cover them.

**SQLite phase-out.** The migration sets are consistent: Postgres has a squashed baseline (`20260219132220_InitialPostgresql`), the 19 later migrations pair one-to-one, and both snapshots have 75 entities. But **the default deployment is still SQLite.** `appsettings.json:3` and both `docker-compose*.yml` default to `Data Source=data/discordbot.db`, so the GHCR image starts on the provider that is being retired. This review did not exercise SQLite.

---

## 7. Agent claims that failed verification

These came back from the exploration passes. They are recorded so nobody chases them.

| Claim | Why it is not in the findings |
| --- | --- |
| A real admin password is committed in `deployment/discordbot.env.template` | The value is the placeholder `ChangeThisPassword123!`, under a comment that says to change it. |
| Stored XSS through `@Html.Raw(Model.ViewModel.Details)` in `Admin/AuditLogs/Details.cshtml:417` | `AuditLogBuilder` writes `Details` with the default `System.Text.Json` encoder. That encoder escapes `<` and `>`, so `</script>` cannot appear. Kept as hardening only: prefer `Json.Serialize` over `Html.Raw`. |
| `Guilds/Analytics/Index` has no guild access check | The page carries `[Authorize(Policy = "GuildAccess")]`. |
| `Role.Color` deprecation breaks role colours | Discord.Net still returns the primary colour. It is cleanup work, not a defect. |
| `DashboardHub`, the VOX page and the Blazor folder are still as the September review describes | All three changed after that review (see section 2). |

---

## 8. Recommendations

Ranked by impact against effort. Each group is one PR, so each PR has one concern.

### Do first: user-visible defects, small changes
1. **Scheduled-message execution:** a scope per message, a failure counter that auto-disables the row, and save-before-send (H2, H3). Add tests for two due messages in one cycle and for a deleted channel. *Under a day.*
2. **Temporary-ban expiry service** (H1), with a test that a ban with a 1-minute duration is lifted. *1–2 days.*
3. **Wallet balance re-check inside the ledger lock** (H4), with a concurrency test that uses `ConcurrencyTestHelper`. *1 day.*
4. **Reminder REST fallback** (H11), and fix the stale-snapshot `Update` that revives cancelled reminders. *Half a day.*
5. **`AllowedMentions` default:** set `AllowedMentions.None` (or users only) in `DiscordSocketConfig.DefaultAllowedMentions`, plus an explicit opt-in for templates that should ping roles. *Under an hour.*

### Do next: assistant and web security
6. **`fetch_url`:** turn off auto-redirect, re-check each hop, and pin to the checked IP (H5).
7. **`execute_python`:** clear the environment now (H6). Then decide whether the tool needs a real sandbox or should go.
8. **`GuildAccess` on guild-keyed API controllers** (H7). Add a test that reflects over controllers and fails when a route has a `{guildId}` but no guild policy.
9. **Policy-name test and the `ModeratorAccess` fix** (H8). Then turn on a global anti-forgery filter for cookie-auth APIs.
10. **Upload limits and rate limits** on the portal soundboard endpoints. Escape the voice name in `AzureTtsService`.

### Then: data hygiene and privacy
11. **One retention service for the six unbounded tables** (H9).
12. **A user-data inventory and a test that enforces it** for purge and export (H10). This needs a decision from you on which records stay for legal reasons.
13. **Switch the default deployment to PostgreSQL:** `appsettings.json`, both compose files and the GHCR docs. This is the last visible step before SQLite can go.

### Then: audio reliability
14. **Fix the `PlaybackService` loop-exit race and the queue off-by-one.** Fix the TTS CTS ownership so each request disposes only its own token. Fix SSML stop and the FFmpeg drain on cancel. These share one theme, playback state ownership, and fit one PR with tests.

### Ongoing
15. **Command-module tests.** Start with the moderation, schedule and reminder modules, where most of the defects above live. Replace the 21 `BeCloseTo(DateTime.UtcNow)` sites with `DbTimestamp.LowerBound()`.
16. **Docs pass:** API endpoints, service catalog, agent definitions, spec status lines, `TEST_COVERAGE_GAPS.md`.
17. **Housekeeping:** upgrade OpenTelemetry past 1.14.0, add Dependabot and CodeQL, delete the dead authorization handler and the duplicate SSML DTOs, and use `Random.Shared` in the samplers.

---

## 9. Out of scope and disclosures

- **Not assessed:** runtime performance under load, the content of the migrations, the DocGen project, and anything that needs a live Discord connection or real Azure, ElevenLabs or OpenRouter keys. The eval project skipped, as designed, because no API key is present.
- **Verification depth:** every high-severity finding was opened at the cited lines. Medium findings marked *(agent)* rest on the agent's quoted evidence. Line numbers are from commit `79f291a`.
- **Agent coverage limits:** these areas were not reached: `MemberSyncService`, the NotX and feature-request services, `WatchlistService`, `InvestigationService`, `FlaggedEventService` internals and the settings pages.
- The 138-warning count is from a Debug build with `SkipTailwind=true`. CI builds in Release, which can differ.

*Read-only review. No application code was changed.*
