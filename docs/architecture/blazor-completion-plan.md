# Blazor Completion Plan — Islands to Full Blazor UI

> **Purpose.** The islands-first modernization ([`blazor-modernization-selective-plan.md`](./blazor-modernization-selective-plan.md))
> is complete: Slices 1–6 shipped, Slice 7 shipped its event-bus foundation, and everything is
> merged to `main` (PR #1931). This document is the successor plan: it takes the app from
> "8 islands embedded in Razor Pages" to the **full Blazor UI** end-state described in
> [`blazor-migration-plan.md`](./blazor-migration-plan.md), updated for what actually shipped,
> what the plans got wrong, and what changed in the .NET ecosystem since the north-star plan
> was written.
>
> Every phase is independently shippable; the app is consistent after any phase. Phases A and B
> are prerequisites for everything else; Phases D–H parallelize heavily.

---

## 1. Current state (verified inventory, July 2026)

| Category | Count | Notes |
|---|---|---|
| Framework | .NET 8 (all 3 projects) | **.NET 8 LTS support ends November 2026** — the upgrade is no longer optional |
| Razor Pages (`.cshtml`, non-partial) | 70 | incl. pages added since the north-star plan: FeatureRequests (2), AudioModerationLog, Members/Moderation, Guilds admin Soundboard/TTS/VOX, Landing |
| Shared partials (`Pages/Shared/Components/`) | 56 | the current design system |
| JavaScript | 57 files under `wwwroot/js/` + 11 under `js/performance/` (~29,100 lines) | legacy paths still shipped behind `?legacy=true` gates |
| API controllers | 31 | most exist only to serve admin-page AJAX |
| Blazor islands (`Blazor/Pages/`) | 8 | ModerationSettings, AdminSettings, Commands, MemberDirectory, Soundboard grid, NotificationBell, BotStatusCard, FoundationProbe (PoC) |
| Blazor shared kit (`Blazor/Shared/`) | 9 | TabbedFormShell, ConfirmModal, TypedConfirmModal, SaveButton, FilterableTable, Pagination, UiButton, UiToggle, TabDefinition |
| Interop | ToastInterop, ThemeInterop (C#); `blazor-interop.js` shim (download/copyText/convertTimes/setIndeterminate/setUnsavedGuard) | |
| Event bus | `IDashboardEventBus` (singleton) | carries notifications, bot status, health/command/system metrics, alert events; 4 notifiers dual-publish |
| Hosting model | Classic Blazor Server (`AddServerSideBlazor` + `MapBlazorHub`), circuit bootstrapped globally in `_Layout` | `_PortalLayout` boots per-page |
| Component tests | none (no bUnit) | called for by the selective plan §9, never shipped |

**What the islands already proved** (don't re-litigate): circuit + prerender + auth inheritance
works; the event-bus dual-publish pattern works; `IServiceScopeFactory`-per-operation is a safe
data-access pattern in long-lived circuits; Tailwind classes carry over verbatim; Chart.js and
audio playback are fine staying in JS behind thin interop; snowflake IDs must cross the
prerender boundary as strings.

---

## 2. Key decisions (deltas from the north-star plan)

These supersede the corresponding sections of `blazor-migration-plan.md`:

1. **Target .NET 10 LTS, not .NET 9.** The north-star plan predates .NET 10. .NET 9 (STS) left
   support in May 2026; .NET 8 LTS ends November 2026. Going 8 → 10 directly is one migration
   instead of two and lands on an LTS supported to November 2028.
2. **Keep `IServiceScopeFactory` as the primary data-access pattern.** The north-star plan
   mandated `IDbContextFactory<BotDbContext>` everywhere. Six shipped islands use
   scope-factory-per-operation and it composes with *all* existing services/repositories with
   zero DI changes. Adopt `IDbContextFactory` only where a component genuinely needs a raw
   context (none identified so far). This removes the Postgres factory-adapter work item.
3. **Islands are the migration vehicle, not throwaway.** `Blazor/Pages/*Island.razor` components
   have no `@page` directive — when their host page converts to a routed Blazor page, the routed
   page hosts the island component directly. Nothing shipped gets rewritten.
4. **Routed Blazor arrives as a parallel router, page-by-page** (`MapRazorComponents<App>()`
   alongside `MapRazorPages()`), exactly as the north-star plan intended. A Razor Page is
   deleted in the same commit its Blazor replacement lands (route conflict rule).
5. **Chart.js stays permanently** (via a proper `ChartJsInterop`), as do `toast.js`, `theme.js`,
   `timezone.js`, browser audio playback (`soundboard-island.js` pattern), and JS file upload
   (`soundboard-upload.js` pattern — multi-MB bytes never cross the circuit).
6. **Auth pages stay Razor Pages permanently**: Login, Logout, ExternalLogin, LinkDiscord,
   Lockout (HTTP redirect/cookie semantics).
7. **The Slice 7 "hold" is resolved by this plan.** The objection to perf-page islands was
   "you'd have to rewrite the whole page to host the island." Under a full migration the whole
   page *is* being rewritten, so the live tiles convert naturally in Phase G using the
   already-shipped bus streams.

---

## 3. Phase A — Consolidate the islands (flip gates, delete dead JS)

*Goal: the islands become the only implementation; the legacy JS they replaced is deleted.
Zero new features — this is debt payoff and it de-risks everything after it.*

1. **Flip the six parity gates.** Remove `?legacy=true` handling and the legacy markup/handlers
   from: `Admin/Settings`, `Commands/Index`, `Guilds/ModerationSettings/Index`,
   `Guilds/Members/Index`, `Portal/Soundboard/Index` (grid), and the `Search` legacy flag if it
   guards island behavior. The islands have been the default since they shipped; the gates were
   transition insurance.
2. **Delete the superseded JS** (~5,300 lines):
   `settings.js`, `moderation-settings.js`, `member-directory.js`, `notification-bell.js`,
   `bot-status-refresh.js`, `command-tabs.js`, `command-tab-loader.js`, `command-filters.js`,
   `command-pagination.js`, `command-log-modal.js`, `command-loading-states.js`,
   `date-range-filter.js`, `url-state.js` (verify no other page references first — `url-state`
   and `date-range-filter` are shared-looking), and the grid half of `portal-soundboard.js`.
3. **Delete `Blazor/Pages/FoundationProbe.razor`** and its host block in `Pages/Components.cshtml`.
4. **Remove now-dead page handlers / API endpoints** the legacy paths called (e.g.
   `/api/commands/log-details` replacement was already noted in Slice 4).
5. **Add bUnit.** New `tests/DiscordBot.ComponentTests` project; baseline tests for
   `TabbedFormShell` (dirty-flag, unsaved guard), `ConfirmModal`/`TypedConfirmModal`,
   `FilterableTable` (paging, empty state), `Pagination`, and one island smoke test
   (render + event-bus update via a fake `IDashboardEventBus`). This is the harness every later
   phase gates on.

**Gate:** solution builds 0 errors; all tests green; manual pass over the six pages; no console
requests for deleted JS.
**Effort:** 2–3 days.

---

## 4. Phase B — .NET 10 LTS upgrade

*Goal: all three projects on net10.0 with zero functional change. Prerequisite for
`MapRazorComponents` work in Phase C and removes the November 2026 support cliff.*

- TFM `net8.0` → `net10.0` in Core, Infrastructure, Bot, Tests; add `global.json` pinning the SDK.
- `Dockerfile` base images `sdk:8.0`/`aspnet:8.0` → `10.0`.
- Package sweep to matching majors: EF Core + Sqlite + Npgsql provider, Identity,
  `AspNet.Security.OAuth.Discord`, `Microsoft.Extensions.Hosting.Systemd`, Serilog.AspNetCore,
  Swashbuckle, OpenTelemetry, Elastic APM.
- **Risk audit (same three as the old plan, still real):**
  - Elastic APM compatibility on .NET 10 — test first; fall back to disabling APM behind config
    if the agent lags.
  - `Npgsql.EnableLegacyTimestampBehavior` still honored (CLAUDE.md hard rule — verify with the
    Postgres context before merging).
  - Run both migration contexts (`SqliteBotDbContext`, `PostgresBotDbContext`) end-to-end;
    regenerate no migrations.
  - Discord.Net compatibility on net10.0.
- `.claude/hooks/session-start.sh`: switch to the .NET 10 SDK package (Ubuntu archive — the
  Microsoft CDN is blocked by egress policy).

**Gate:** build/test green on both DB providers; Docker image builds; login → dashboard →
soundboard playback manual pass; bot connects to Discord.
**Effort:** 2–4 days (APM is the wildcard).

---

## 5. Phase C — Routed Blazor foundation

*Goal: `MapRazorComponents<App>()` runs alongside `MapRazorPages()`; one proof page migrates;
the auth/lifetime plumbing that every routed page needs exists.*

1. **Root files** under `Blazor/` (keep the established top-level folder; do **not** introduce a
   second `Components/` tree — the north-star plan's naming predates the islands):
   `Blazor/App.razor`, `Blazor/Routes.razor` (Router + `AuthorizeRouteView` +
   `RedirectToLogin`), and layouts `Blazor/Layout/MainLayout.razor`,
   `PortalLayout.razor`, `GuildLayout.razor` mirroring `_Layout.cshtml` / `_PortalLayout.cshtml`
   / the guild sidebar+breadcrumb chrome. MainLayout hosts the already-built
   `NotificationBellIsland` + nav; theme FOUC script and script ordering copied from `_Layout`
   (toast.js/theme.js before `blazor-interop.js` — gotcha §6.1 of the handoff).
2. **Service/middleware wiring:** `AddRazorComponents().AddInteractiveServerComponents()` next to
   the existing `AddServerSideBlazor()` (both can coexist during transition; retire
   `AddServerSideBlazor`/`MapBlazorHub` in Phase J when the last `<component>` host page dies),
   `app.UseAntiforgery()` after auth, `MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
3. **Resource-based guild authorization.** Refactor `GuildAccessHandler` /
   `PortalGuildMemberAuthorizationHandler` to check `context.Resource is ulong guildId` first,
   falling back to the existing `HttpContext.RouteValues` extraction. Routed Blazor pages call
   `IAuthorizationService.AuthorizeAsync(user, guildId, "GuildAccess")`.
4. **`RevalidatingServerAuthenticationStateProvider`** (30-minute revalidation: user exists, not
   locked out) — circuits now outlive cookie lifetimes routinely.
5. **Circuit client-info service** capturing the IP from the initial request so audit-log
   enqueues from routed pages keep recording an address.
6. **`ChartJsInterop` + `blazor-chart-interop.js`** (`create`/`update`/`destroy` by canvas id) —
   needed by half the remaining pages; build it here, prove it on the proof page or a test page.
7. **Scoped-CSS decision:** link `DiscordBot.Bot.styles.css` in both layout stacks and stop
   inlining `<style>` in components (gotcha §6.6 becomes moot).
8. **Proof migration:** `Pages/Error/403|404|500.cshtml` → routed Blazor pages
   (`[AllowAnonymous]`, verify `UseStatusCodePagesWithReExecute` re-execution hits the Blazor
   router), plus `Account/AccessDenied` and `Account/Privacy`. Trivial pages, exercises routing,
   layouts, anonymous auth, and the delete-page-same-commit rule.

**Gate:** error routes + the two account pages serve from Blazor; every existing Razor Page and
island still works; login/logout unchanged; `DashboardHub` JS clients unaffected.
**Effort:** 4–6 days.

---

## 6. Phase D — Component library completion

*Goal: every shared partial a remaining page needs has a `.razor` twin, so page phases never
block on components. Port on demand per consuming page where possible; this phase builds the
core set that many pages share.*

Current kit covers 9 of ~56 partials. Port in tiers (Tailwind markup copied verbatim,
ViewModel → `[Parameter]`s):

- **Tier 1 — pure render (~24):** Badge, Alert, Card, EnhancedCard, EmptyState, LoadingSpinner,
  Skeleton(+Card), StatusIndicator/StatusBadge/SeverityBadge, RuleTypeIcon, FormInput,
  FormSelect, GuildBreadcrumb, CommandBreadcrumb, GuildHeader, CommandHeader, HeroMetricCard,
  DashboardWidget, RestartBanner, PageLoadingOverlay, EmphasisToolbar-adjacent statics.
- **Tier 2 — interactive (~8):** TabPanel/NavTabs (native tab state), AutocompleteInput
  (debounced via `Debouncer`), SortDropdown, PresetBar, PauseModal, ToastContainer stays JS.
- **Tier 3 — stateful/real-time (~20, built with their consuming page in Phases F–H):**
  ActivityFeed(+Timeline), ConnectionStatus, ConnectedServersWidget, GuildStatsCard,
  CommandStatsCard, QuickActionsCard, RecentActivityCard, AuditLogCard,
  CommandLogDetailsModal (exists inside CommandsIsland — extract to shared),
  VoiceChannelPanel (event-bus driven; retires `voice-channel-panel.js`, 656 lines),
  VoiceSelector, ModeSwitcher, StyleSelector, SsmlPreview, UserPreviewPopup/GuildPreviewPopup
  (+loading/error states; retires `preview-popup.js`).
- **Interop additions:** `TimezoneInterop`, `ClipboardInterop` (fold into the existing
  `blazor-interop.js` shim namespace), `NavigationInterop` (sidebar/scroll) for MainLayout.

Add bUnit render tests per Tier 1/2 component as they land (cheap, mechanical).

**Gate:** side-by-side visual parity per component (partial vs Blazor twin on a scratch page).
**Effort:** 6–9 days, highly parallelizable.

---

## 7. Phase E — Simple pages (read-only + account)

*Goal: burn down the low-risk long tail; every one deletes its `.cshtml` on landing.*

- `Admin/AuditLogs/Details`, `Admin/MessageLogs/Details`, `Admin/Users/Details`,
  `CommandLogs/Details`, `Guilds/Details`, `Guilds/FlaggedEvents/Details`,
  `Guilds/FeatureRequests/Details`.
- `Account/Profile` — evaluate: if its POSTs are plain service calls, migrate; if it leans on
  Identity page plumbing, it joins the permanent Razor Pages set.
- `Landing` and `Guilds/PublicLeaderboard` — **render statically** (static SSR via the Blazor
  router or stay Razor Pages): hot anonymous pages must not open circuits (selective plan §6
  guardrail). Recommend: leave both as Razor Pages until Phase J, then decide.

**Gate:** each page's data, auth policy, breadcrumb, and timezone rendering match the old page.
**Effort:** 3–4 days, parallelizable.

---

## 8. Phase F — Form & list pages

*Goal: all CRUD/settings/list pages are routed Blazor using `EditForm` + the kit
(`TabbedFormShell`, `FilterableTable`, `Pagination`, `ConfirmModal`). API controllers are
bypassed (components call services in DI scopes), not yet deleted.*

- **Batch F1 — simple CRUD:** `Admin/Users/Create|Edit`, `Guilds/Edit`,
  `Guilds/ScheduledMessages/Create|Edit`, `Guilds/Welcome`.
- **Batch F2 — settings pages:** `Guilds/AudioSettings`, `Guilds/AssistantSettings`
  (Admin/Settings and ModerationSettings are already islands — their host pages convert to thin
  routed Blazor pages hosting the same island components, deleting the `.cshtml` hosts).
- **Batch F3 — filtered lists:** `Guilds/Index`, `Admin/Users/Index`, `Admin/Notifications`
  (retires `notification-history.js`), `Admin/Logs`, `Admin/AuditLogs/Index`,
  `Admin/MessageLogs/Index` (retires `message-logs.js`), `Guilds/ScheduledMessages/Index`,
  `Guilds/Reminders/Index`, `Guilds/FlaggedEvents/Index`, `Guilds/FeatureRequests/Index`,
  `Guilds/AudioModerationLog/Index`, `Guilds/Members/Moderation` (retires
  `user-moderation-profile.js`), `Admin/BulkPurge` + `Admin/UserPurge` (progress via
  event bus — add BulkPurge notifier dual-publish, the last notifier not yet on the bus),
  `Search` (retires `search.js`, `ajax-sort.js`; autocomplete uses the Tier 2 component).
- Every list page: debounced filters (§6 rules — 250–300ms, cancel in-flight with CTS),
  server-side paging through `FilterableTable`.
- Commands and Members index pages convert like F2 (already islands → thin routed hosts).

**Gate per batch:** validation parity, toast on save, filter/sort/page without reload,
audit-log entries still written, `dotnet format` clean.
**Effort:** 8–12 days; batches parallelizable.

---

## 9. Phase G — Real-time pages (dashboard + performance)

*Goal: every SignalR-JS page is push-driven Blazor over `IDashboardEventBus`; the browser
`DashboardHub` client path goes quiet on admin pages. The bus already carries every stream this
phase needs (Slice 2 + Slice 7 foundation) — this phase is UI, not plumbing.*

- **Dashboard (`/`):** routed Blazor page composing BotStatusCardIsland (exists),
  ConnectionStatus, GuildStatsCard, CommandStatsCard, ActivityFeed, ConnectedServersWidget.
  Retires `dashboard-realtime.js`, `dashboard-hub.js` auto-connect, `command-stats-chart.js`
  (chart via `ChartJsInterop`), `realtime-ui.js`.
- **Performance suite (6 pages):** `Admin/Performance/Index|HealthMetrics|Commands|SystemHealth|
  ApiMetrics|Alerts`. Rebuild each as a routed page whose live regions subscribe to the bus
  (coalesce re-render ≤1 Hz per widget, keep the existing sliding windows) and whose charts stay
  Chart.js fed through `ChartJsInterop`. The overview page's AJAX tab-partial loader
  (`performance-shell.js`, `performance-tabs.js`, `js/performance/tabs/*`) is replaced by native
  Blazor tab composition — the shared-tab-partial blocker that deferred Slice 7 disappears.
  Retires all of `js/performance/` (11 files) plus `api-metrics-chart.js`, `time-range.js`.
  The subscriber-count gates already fire on bus-only subscribers (Slice 7 foundation), so
  metric collectors keep pausing when nobody is watching.
- **VoiceChannelPanel** component (Tier 3, built here): event-bus audio status
  (`AudioNotifier` dual-publish — add it, same pattern as the other notifiers), retiring
  `voice-channel-panel.js` for Phase H/I consumers.

**Gate:** live updates arrive without page refresh on all 7 pages; re-render rate capped;
un-migrated pages (if any remain) still receive `DashboardHub` broadcasts; per-circuit memory
sane with several concurrent admin tabs.
**Effort:** 8–10 days.

---

## 10. Phase H — Complex feature pages

*Goal: the chart-heavy and audio admin pages.*

- **Guild analytics (3):** `Guilds/Analytics/Index|Engagement|Moderation` — `ChartJsInterop` +
  native filters; retires `server-analytics.js`, `engagement-analytics.js`,
  `moderation-analytics.js`.
- **Admin audio (3):** `Guilds/Soundboard` (reuse SoundboardIsland patterns + the JS
  upload/playback modules — bytes stay off the circuit), `Guilds/TextToSpeech`
  (VoiceSelector/ModeSwitcher/StyleSelector/SsmlPreview components; **keep** preview scrubbing +
  sliders in JS per the selective plan's latency rule; retires `tts-page.js` state management,
  keeps a small audio shim + `ssml-markers.js`), `Guilds/VOX`.
- **RatWatch (4):** `Guilds/RatWatch/Index|Incidents|Analytics`, `Admin/RatWatchAnalytics`
  (retires `rat-watch-analytics.js`).
- **`Guilds/AssistantMetrics`** (charts).
- Preview popups (User/Guild) convert here with their consumers (retires `preview-popup.js`).

**Gate:** charts render/update; uploads work end-to-end; voice panel state correct across
play/stop from another tab (bus echo dedupe, per SoundboardIsland).
**Effort:** 10–14 days (audio pages dominate).

---

## 11. Phase I — Portal completion

*Goal: the three member-facing portal pages fully Blazor on `PortalLayout`, keeping the
latency-critical JS.*

- `Portal/Soundboard` — routed page hosting the existing SoundboardIsland + VoiceChannelPanel
  component; `soundboard-island.js` (playback/SignalR bridge → replace hub bridge with bus) and
  `soundboard-upload.js` stay.
- `Portal/TTS` — the selective plan excluded it for latency; under full migration, state/forms
  move to Blazor and preview playback + speed/pitch scrubbing stay JS (`portal-tts.js` shrinks
  to an audio shim). Real-time queue/playback status via the bus.
- `Portal/VOX` — trivial tabs+play; straightforward conversion.
- Anonymous visitors get the static landing treatment (no circuit until authenticated —
  `PortalGuildMember` policy unchanged).

**Gate:** OAuth-only member access works; playback E2E on mobile viewport; anonymous landing
renders circuit-free.
**Effort:** 5–8 days.

---

## 12. Phase J — Cleanup & decommission

*Goal: one UI stack.*

1. **Delete `Pages/`** except the permanent set: Account/{Login, Logout, ExternalLogin,
   LinkDiscord, Lockout} (+ Profile if Phase E kept it), `_ViewImports` for them, and whatever
   Phase E decided for Landing/PublicLeaderboard.
2. **Delete JS:** everything except `toast.js`, `theme.js`, `timezone.js`, `blazor-interop.js`,
   `blazor-chart-interop.js`, `login.js`, the audio shims (`soundboard-island.js`,
   `soundboard-upload.js`, TTS audio shim, `ssml-markers.js`). Net: ~50 files / ~24k lines gone.
3. **Delete redundant API controllers** (~25 of 31): keep Theme, the Portal controllers only if
   the audio shims still POST to them (they do — playback POSTs stay HTTP), and any consumed by
   external clients. Audit each against the shims before deletion.
4. **Retire the JS SignalR client path:** remove `signalr.min.js` + `dashboard-hub.js`
   auto-connect from layouts. **Keep `DashboardHub` itself** (external clients, and the
   dual-publish notifiers are harmless) — or, if zero subscribers remain, demote dual-publish to
   bus-only in a follow-up.
5. **Retire classic island hosting:** remove `AddServerSideBlazor`/`MapBlazorHub` and the
   `<component>` tag-helper hosts once no `.cshtml` hosts remain.
6. **Delete tag helpers** (Authorize/FilterPanel/Highlight), `ViewModels/Components/`, unused
   partials in `Pages/Shared/`.
7. **Docs & agents:** update `CLAUDE.md` (Blazor patterns, drop stale references),
   `docs/articles/component-api.md`, `design-system.md`, `form-implementation-standards.md`,
   `.claude/agents/web-ui-portal.md` (+ any stream agents whose surface moved), and mark both
   predecessor plan docs superseded by this one.

**Gate:** full route regression (crawl every route in §1's inventory), no console errors, no
404s on static assets, Docker build + deploy, performance baseline vs pre-Phase-A numbers.
**Effort:** 3–5 days. High regression risk — do not compress the verification.

---

## 13. Sequencing & effort summary

```
A  Consolidate islands (gates, dead JS, bUnit)      2–3 d   ── prerequisite, do first
B  .NET 10 LTS upgrade                              2–4 d   ── prerequisite (support cliff Nov 2026)
C  Routed foundation + proof pages                  4–6 d   ── unlocks all page phases
D  Component library                                6–9 d   ┐
E  Simple pages                                     3–4 d   ├─ D/E/F parallelize after C
F  Form & list pages                                8–12 d  ┘
G  Real-time pages (dashboard + performance)        8–10 d  ── after D Tier 3 + F patterns
H  Complex feature pages                            10–14 d ── after G (voice panel, charts interop)
I  Portal completion                                5–8 d   ── after H (voice components)
J  Cleanup & decommission                           3–5 d   ── last
                                            total   51–75 d (~10–15 weeks, 1 dev; ~7–10 weeks, 2 devs)
```

The total matches the north-star estimate because the islands work that's already banked
(~2 weeks of the hardest interactive pages) is offset by scope the old plan missed (7 newer
pages, bUnit harness, the 8→10 jump) — but Phases A+B deliver standalone value even if the
effort stops there, and after any phase the app is consistent and shippable.

## 14. Risk register (delta from north-star)

| Risk | Sev | Mitigation |
|---|---|---|
| .NET 8 EOL (Nov 2026) arrives mid-migration | High | Phase B is second, not late; it does not depend on any UI work |
| Elastic APM lag on .NET 10 | High | Test first in Phase B; config kill-switch fallback |
| Npgsql legacy-timestamp behavior on EF 10 | High | Verify against Postgres context before Phase B merges (CLAUDE.md hard rule) |
| Route conflicts Razor↔Blazor | Med | Delete `.cshtml` in the same commit its Blazor page lands |
| SQLite single-writer under many circuits | Med | Document PostgreSQL as recommended for multi-user; scope-factory keeps context lifetimes short |
| Controller deletion breaks the audio shims | Med | Phase J audits every controller against `soundboard-*.js`/TTS shim call sites before deleting |
| Claims staleness across long circuits | Low | RevalidatingAuthStateProvider (Phase C); document link/unlink requires refresh |
| Hot anonymous pages opening circuits | Low | Landing/PublicLeaderboard stay static (Phase E decision) |

## 15. Standing rules (apply to every phase)

- Snowflake IDs cross prerender/JS boundaries as **strings**, always.
- Data access: `IServiceScopeFactory`, scope per operation, existing services only.
- Debounce every text/filter input 250–300ms; coalesce inbound streams to ≤1 Hz per widget;
  optimistic updates for mark-read/dismiss-style actions.
- File bytes and low-latency audio never cross the circuit.
- Every destructive action goes through `ConfirmModal`/`TypedConfirmModal` and re-checks
  authorization server-side inside the circuit.
- Commit per page/batch with the established trailers; each commit leaves the app consistent.
- Update the relevant `.claude/agents/*` definition when a phase moves a stream's surface.
