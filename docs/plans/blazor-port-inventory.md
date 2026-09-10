# Blazor Port — UI Inventory (appendix)

> **Status:** Survey, 2026-09-10
> **Companion:** [`blazor-port-plan.md`](blazor-port-plan.md)

This is the detailed survey behind the plan: every Razor Page, shared component, layout, JavaScript module, controller and hosting concern, with a proposed Blazor mapping for each. It was produced by reading the code on `main` at `2a43628` and is a point-in-time snapshot; `docs/architecture/ui-inventory.md` remains the maintained reference and will be rewritten when the port completes.

Counts re-verified against the tree: 69 routable pages (`@page`), 92 partials without `@page` (56 in `Pages/Shared/Components/`), 3 full layouts plus the nested `_GuildLayout`, 71 production JavaScript files (29,656 lines) plus one test file, 36 concrete controllers (39 files, 3 abstract bases), 57 component view models, 150 PageModel handler tests.

Complexity key used throughout: **S** static or form-only, **M** some JavaScript or AJAX partials, **L** charts, SignalR, audio, or heavy JavaScript.

Sections:

1. Pages — non-guild areas (Account, Admin, Commands, Portal, misc)
2. Pages — guild areas and the guild shell
3. Design system: tokens, theming, the 56 shared components, layouts, tag helpers
4. JavaScript: every file, classification A/B/C/D, SignalR surface, charts, audio
5. Hosting, identity, authorization, SignalR, controllers, tests, Docker, and the constraints a Blazor design must satisfy
6. Gotchas recorded by the earlier, unmerged migration branch

---

# Part 1 — Pages: non-guild areas


Scope: `Pages/Account/**`, `Pages/Admin/**`, `Pages/CommandLogs/**`, `Pages/Commands/**`,
`Pages/Components.cshtml`, `Pages/Error/**`, `Pages/Index`, `Pages/Landing`, `Pages/Search`,
`Pages/Portal/**`, root `Pages/_ViewImports.cshtml` / `Pages/_ViewStart.cshtml`.
Excludes `Pages/Guilds/**` and `Pages/Shared/**` (covered by another agent), though components
and partials that live under `Shared/Components` are referenced here because these pages consume them.

All paths below are relative to `/home/user/discordbot/src/DiscordBot.Bot/Pages/` unless given in full.

Legend for complexity: **S** = static/form-only (safe 1:1 port to a Razor component),
**M** = some client JS or AJAX partials, **L** = heavy JS, real-time (SignalR), audio, or charts.

---

## 1. Account (`Pages/Account/`)

Layout: all account pages use the standalone `_LayoutLanding`-style `<html>` markup with
`Layout = null` **except** `LinkDiscord.cshtml` and `Profile.cshtml`, which inherit the default
`_Layout` (root `_ViewStart.cshtml` sets `Layout = "_Layout"`, and neither page overrides it).
No `[Authorize]` on the standalone auth pages (they must be reachable pre-login); `LinkDiscord`
and `Profile` are `[Authorize]` (any signed-in user, no policy).

| Page | Route | Model base | Auth | Handlers | Return kind | Services injected |
|---|---|---|---|---|---|---|
| `AccessDenied.cshtml` | `/Account/AccessDenied` | `PageModel` | none | `OnGet(string? returnUrl)` | void → `Page()` | none |
| `ExternalLogin.cshtml` | `/Account/ExternalLogin` | `PageModel` | none | `OnGet()` → `RedirectToPage("./Login")`; `OnGetCallbackAsync(string? returnUrl, string? remoteError)` | `IActionResult`, mostly `RedirectToPage`/`LocalRedirect` after OAuth token exchange | `IDiscordTokenService`, `IUserDiscordGuildService`, `IHttpClientFactory`, `IAuditLogService` |
| `LinkDiscord.cshtml` | `/Account/LinkDiscord` | `PageModel` | `[Authorize]` | `OnGetAsync`, `OnPostLinkAsync` (challenge → Discord OAuth), `OnPostUnlinkAsync`, `OnPostInitiateBotVerificationAsync`, `OnPostVerifyCodeAsync`, `OnPostCancelVerificationAsync`, `OnPostRefreshDiscordDataAsync` | classic POST + redirect back to same page (PRG), `[TempData] StatusMessage/IsSuccess` flash | `IDiscordTokenService`, `IDiscordUserInfoService`, `IGuildMembershipService`, `IUserDiscordGuildService`, `IVerificationService` |
| `Lockout.cshtml` | `/Account/Lockout` | `PageModel` | none | `OnGet()` | void | none |
| `Login.cshtml` | `/Account/Login` | `PageModel` | none | `OnGet(string? returnUrl)`, `OnPostAsync(string? returnUrl)` (email/password sign-in), `OnPostDiscordLogin(string? returnUrl)` (OAuth `Challenge`) | mix — `Page()` on validation failure, `LocalRedirect`/`RedirectToPage` on success, `ChallengeResult` for Discord | `IAuditLogService`; `@inject IVersionService` in the view |
| `Logout.cshtml` | `/Account/Logout` | `PageModel` | none | `OnPostAsync(string? returnUrl)` only (no GET UI beyond an `<h1>`) | `SignOutResult`/redirect | `IAuditLogService` |
| `Privacy.cshtml` | `/Account/Privacy` | `PageModel` | `[Authorize]` | `OnGetAsync`, `OnPostToggleConsentAsync(int type, bool grant)`, `OnPostExportDataAsync`, `OnPostDeleteDataAsync` | mixed: consent toggle is classic form POST, **but delete-account is a `fetch('?handler=DeleteData', …)` call from inline JS** that manually reads `input[name="__RequestVerificationToken"]` and sends it as a `RequestVerificationToken` header | `IConsentService`, `IUserDataExportService`, `IUserPurgeService` |
| `Profile.cshtml` | `/Account/Profile` | `PageModel` | `[Authorize]` | `OnGetAsync`, `OnPostAsync` (theme selection, `[BindProperty] SelectedThemeId`) | classic POST + `[TempData]` flash | `IThemeService` |

**Interactivity:** Login has a bespoke `~/js/login.js` (password show/hide toggle, client-side
email/password validation before submit) plus an inline blocking `<script>` in `<head>` that
reads a `theme-preference` cookie/`localStorage` to set `data-theme` before first paint (FOUC
guard — must be reproduced as early static markup/inline script in a Blazor port, not a
component, since it runs before any framework boots). LinkDiscord has a live countdown
(`data-utc="@Model.PendingVerification!.ExpiresAtUtcIso" data-format="time"`, formatted by a
shared timezone/format JS util) and a `_ConfirmationModal` partial for unlink. Privacy has a
custom `window.confirmConsentToggle(checkbox)` global wired to `onchange` on each toggle input,
and the delete-account flow shows a countdown/redirect (`setTimeout(() => window.location.href
= data.redirectUrl …, 2000)`).

**Complexity:** AccessDenied/Lockout = **S**. Logout = **S**. Profile = **S**. Login = **M**
(custom JS, theme-flash script, Discord OAuth challenge button). ExternalLogin = **M** (trivial
UI but the callback handler embeds real OAuth token exchange + guild sync logic that a Blazor
port must relocate server-side; the page itself is just a spinner/error state).
LinkDiscord = **M** (6 POST handlers, PRG + TempData, confirmation modal, live countdown).
Privacy = **M** (manual antiforgery header fetch call, consent AJAX toggle, timed redirect).

---

## 2. Admin (`Pages/Admin/`)

All Admin pages use the default `_Layout` (root `_ViewStart.cshtml`). Every Admin page model
carries an `[Authorize(Policy = "...")]` attribute — policies seen: `RequireAdmin`,
`RequireSuperAdmin`, `RequireViewer` (viewer is the lowest of the three; used on read-only
dashboards). `PaginatedPageModel` (not `PaginatedGuildPageModel` — these are non-guild lists)
is the base for the list pages that page server-side.

### 2a. Users, Purge, Audit/Message Logs, Notifications

| Page | Route | Model base / Auth | Handlers | Notes |
|---|---|---|---|---|
| `Users/Index.cshtml` | `/Admin/Users` | `PaginatedPageModel`, `RequireAdmin` | `OnGetAsync` (search/role/active filters, `[TempData]` Success/Error), `OnPostToggleActiveAsync(string userId, bool isActive)` | Server-paged table; `_Pagination`, `_Badge`, `_Alert`, `_EmptyState` partials |
| `Users/Create.cshtml` | `/Admin/Users/Create` | `PageModel`, `RequireAdmin` | `OnGetAsync`, `OnPostAsync` (`InputModel` nested class) | Classic form, `_FormInput`/`_FormSelect` partials, `_ValidationScriptsPartial` |
| `Users/Edit.cshtml` | `/Admin/Users/Edit/{id}` | `PageModel`, `RequireAdmin` | `OnGetAsync(string id)`, `OnPostAsync`, `OnPostResetPasswordAsync(string userId)`, `OnPostUnlinkDiscordAsync(string userId)` | 2 `_ConfirmationModal`s (reset password / unlink) |
| `Users/Details.cshtml` | `/Admin/Users/{id}` | `PageModel`, `RequireAdmin` | `OnGetAsync(string id)` only | Read-only; `_StatusIndicator`, `_Badge`, `_EmptyState` |
| `AuditLogs/Index.cshtml` | `/Admin/AuditLogs` | `PaginatedPageModel`, `RequireAdmin` | `OnGetAsync` → **legacy stub: `RedirectToPage("/Admin/Logs", new { tab = "audit" })`**; `OnGetExportAsync` (CSV, still defined but export links in the UI now point at `/Admin/Logs`'s handler — likely dead code) | Body is just a "Redirecting…" message |
| `AuditLogs/Details.cshtml` | `/Admin/AuditLogs/{id:long}` | `PageModel`, `RequireAdmin` | `OnGetAsync(long id, string? returnUrl)` | Rich detail view: expand/collapse raw JSON, copy-to-clipboard entry ID, **client-side JSON file download** (builds a JS object, `Blob`, `URL.createObjectURL`, synthetic `<a download>` click) — all inline `<script>`, no server round-trip |
| `MessageLogs/Index.cshtml` | `/Admin/MessageLogs` | `PaginatedPageModel`, `RequireAdmin` | `OnGetAsync()` → same legacy redirect stub pattern to `/Admin/Logs?tab=messages` | |
| `MessageLogs/Details.cshtml` | `/Admin/MessageLogs/{id:long}` | `PageModel`, `RequireAdmin` | `OnGetAsync(long id)` | Read-only message content viewer |
| `Logs/Index.cshtml` | `/Admin/Logs` | `PageModel`, `RequireAdmin` | `OnGetAsync` (huge parameter surface — ~20 `[BindProperty(SupportsGet=true)]` props split `message*` vs `audit*` prefixes, separately paginated), `OnGetExportAsync` (CSV `File()` result) | **This is the real, current unified logs page** — see below |
| `Notifications/Index.cshtml` | `/Admin/Notifications` | `PaginatedPageModel`, `RequireViewer` | `OnGetAsync` only (`userId` from `ClaimTypes.NameIdentifier`; defaults to last-7-days if no filter set) | Mark-read/bulk actions are **not** page handlers — driven client-side by `~/js/notification-history.js` (checkbox selection, bulk action bar, row actions) presumably against a REST controller, not this PageModel |
| `BulkPurge.cshtml` | `/Admin/BulkPurge` | `PageModel`, `RequireSuperAdmin` | `OnGet`, `OnPostPreviewAsync`, `OnPostExecuteAsync` | Typed-confirmation modal (`quickActions.typedConfirm`, must type "CONFIRM"); has a `#progress-container` div labelled "for SignalR" but **no SignalR script is actually loaded on this page** — dead/aspirational markup, a porter should not assume live progress works today |
| `UserPurge.cshtml` | `/Admin/UserPurge` | `PageModel`, `RequireSuperAdmin` | `OnGetAsync`, `OnPostAsync` | Similar purge-confirmation pattern, `[TempData]` flash |

### 2b. Admin/Logs (unified tabbed logs page)

`Logs/Index.cshtml` (route `/Admin/Logs`) is a single page with two in-page tabs — `messages`
and `audit` — rendered via `_TabPanel` + two large partials (`Tabs/_MessagesTab.cshtml` 315
lines, `Tabs/_AuditTab.cshtml` 703 lines), both modeled as `Admin.Logs.IndexModel` directly
(no separate view models). Tab switching is **client-side hash routing**: inline script reads
`?tab=` query param or `window.location.hash`, sets it on load, and listens for `hashchange`
(`~/js/tab-panel.js`). Each tab has its own `_AutocompleteInput` filter fields (author/guild/
channel for messages; actor for audit) wired by `~/js/autocomplete.js`, its own server-paged
`_Pagination`, and its own CSV `Export` link (`asp-page-handler="Export"` with every filter
forwarded as `asp-route-*`). `~/js/message-logs.js` provides additional page glue. Both tabs
convert user-local date-range filters to UTC via `TimezoneHelper.ConvertToUtc(date,
UserTimezone)`, where `UserTimezone` is a bound query-string property — **client sends its IANA
timezone as a query param on every load**, a pattern a porter must preserve (there is a third,
unused `application` tab stub in the markup that just shows an `_EmptyState`).

### 2c. Admin/Performance (dashboard, charts, SignalR)

Six pages share one visual "shell" (breadcrumbs + `_TabPanel` nav across Overview/Health/
Commands/API/System/Alerts) but are implemented as **six separate routed pages**, not one page
with client tabs — clicking a tab nav item is a normal `<a href="/Admin/Performance/...">`
navigation (see `data-tab-link` anchors in `_OverviewTab.cshtml`). Each full page re-renders the
shell layout and its own tab partial server-side. **In addition**, `Performance/Index.cshtml.cs`
uniquely also exposes `OnGetPartialAsync(string tabId, int hours)` which returns a
`PartialViewResult` for one of the same six tab partials — this AJAX capability exists but is
not obviously wired up from the shipped Overview page markup (no fetch call to `?handler=Partial`
found in the extracted script); it reads as a partially-finished SPA-shell migration. A porter
should treat these as **six pages that happen to duplicate a shared tab partial**, not as one
page with real AJAX tab switching.

| Page | Route | Handlers | `[BindProperty(SupportsGet)]` |
|---|---|---|---|
| `Performance/Index.cshtml` | `/Admin/Performance` | `OnGetAsync`, `OnGetPartialAsync(tabId, hours)` (unused by shipped UI) | none |
| `Performance/HealthMetrics.cshtml` | `/Admin/Performance/HealthMetrics` | `OnGetAsync` | none |
| `Performance/Commands.cshtml` | `/Admin/Performance/Commands` | `OnGetAsync` | `Hours` |
| `Performance/ApiMetrics.cshtml` | `/Admin/Performance/ApiMetrics` | `OnGetAsync` | `Hours` |
| `Performance/SystemHealth.cshtml` | `/Admin/Performance/SystemHealth` | `OnGet` (sync, no data fetch — page pulls via JS/API instead) | none |
| `Performance/Alerts.cshtml` | `/Admin/Performance/Alerts` | `OnGetAsync` | none |

All six inject only `IPerformanceDashboardAggregator` + `ILogger`. Every tab partial is a chart
gallery (Chart.js 4.4.1 via `<canvas>` + `~/js/performance/...` per-tab JS modules) and several
pages connect live: **SignalR** (`microsoft-signalr` 8.0.0 UMD + `~/js/dashboard-hub.js` +
`~/js/realtime-ui.js` + a page-specific `*-realtime.js`) drives HealthMetrics, SystemHealth,
Commands, and Alerts. Alerts additionally has an `unsaved-changes` guard on `beforeunload`
(`window.alertsTabHasUnsavedChanges`) and cleans up on `pagehide`. HealthMetrics has the densest
inline JS: 5 Chart.js instances (gauge charts for latency/memory/CPU built with a custom
`createGaugeChart`/`updateGaugeChart` helper drawing semicircular gauges, a sparkline, and a
time-series latency history chart fed by `fetch('/api/metrics/health/latency?hours=')`).
`Chart.defaults.color`/`borderColor` are set inline per-page to match the dark theme — a porter
should centralize this once instead of repeating it on every chart page. `_AuditLogCard`/etc.
aside, tab partials read data purely from server-rendered view models except the API tab, which
re-fetches its latency chart client-side via `fetch()` against a JSON endpoint on `hours` change.

**RatWatchAnalytics.cshtml** (`/Admin/RatWatchAnalytics`, `RequireAdmin`) is a standalone (not
part of the Performance shell) analytics dashboard: `OnGetAsync` with 3
`[BindProperty(SupportsGet)]` filter props, injects `IRatWatchRepository`,
`IRatRecordRepository`, `IGuildRatWatchSettingsRepository`, `IGuildService`. Renders two
Chart.js charts (line "watches over time", doughnut "outcome distribution") whose data is
serialized server-side to a `<script type="application/json" id="ratWatchChartData">` block and
picked up by `~/js/rat-watch-analytics.js` — a cleaner data-hand-off pattern than the inline
`@Html.Raw(JsonSerializer.Serialize(...))` seen elsewhere, worth reusing in the Blazor port.

### 2d. Admin/Settings

`Settings.cshtml` (`/Admin/Settings`, `RequireAdmin`) is the single largest "form" page (861
lines of markup / 361 lines of code-behind). In-page client tabs (`General`/`Features`/
`Commands`/`Advanced`/`BotControl`/`Appearance`) are pure `onclick="window.settingsManager?.
switchTab(...)"` (no URL/hash state) driven by `~/js/settings.js`. Every save/reset action is an
**AJAX POST handler returning JSON**, not a redirect:
`OnPostSaveCategoryAsync/OnPostSaveAllAsync/OnPostResetCategoryAsync/OnPostResetAllAsync/
OnPostSaveCommandModulesAsync/OnPostRestartBotAsync/OnPostShutdownBotAsync/
OnPostSaveAppearanceAsync/OnPostResetAppearanceAsync`, all funnelled through a private
`ToJsonResult(result)` helper. Confirmation/typed-confirmation modals guard restart/shutdown
(`_ConfirmationModal` for restart, `_TypedConfirmationModal` requiring typed text for shutdown).
`@Html.AntiForgeryToken()` is rendered once and reused by `settings.js` for all the fetch calls
(the antiforgery token field name/header convention here should be confirmed against
`settings.js` before porting — it is not read via a `RequestVerificationToken` request header
the way `Privacy.cshtml` does it, so the two pages may use two different antiforgery-over-fetch
conventions in this codebase). Injects `ISettingsSectionService`, `IAppearanceSettingsService`,
`IBotControlService`. Individual setting rows are rendered through a shared `_SettingField`
partial (not in `Shared/Components`, lives at `Pages/Shared/_SettingField.cshtml` per the
ui-inventory doc — out of this agent's scope but consumed here).

**Complexity ratings — Admin:**
- **S**: `AuditLogs/Index` (redirect stub), `MessageLogs/Index` (redirect stub),
  `MessageLogs/Details`, `Users/Details`, `Users/Create`.
- **M**: `Users/Index`, `Users/Edit`, `AuditLogs/Details` (client JSON export/copy/expand),
  `Notifications/Index`, `BulkPurge`, `UserPurge`, `Logs/Index` (hash-tab routing + dual
  autocomplete filters + CSV export, but no real-time/audio).
- **L**: `Performance/Index`, `Performance/HealthMetrics`, `Performance/SystemHealth`,
  `Performance/Commands`, `Performance/ApiMetrics`, `Performance/Alerts` (Chart.js + SignalR +
  per-page realtime JS), `RatWatchAnalytics` (2 Chart.js charts + JSON data-island pattern),
  `Settings` (largest handler surface, 9 AJAX JSON POST handlers, multi-tab, 2 modal types).

---

## 3. Commands & CommandLogs

Both use default `_Layout`.

| Page | Route | Model base / Auth | Handlers |
|---|---|---|---|
| `Commands/Index.cshtml` | `/Commands` | `PageModel`, `RequireViewer` | `OnGetAsync` (7 `[BindProperty(SupportsGet)]` filter/tab props), `OnPostClearAndRegisterGloballyAsync` | 
| `CommandLogs/Details.cshtml` | `/CommandLogs/{id:guid}` | `PageModel`, `RequireModerator` | `OnGetAsync(Guid id)` only |

`Commands/Index.cshtml` is the most JS-heavy page in this whole scope outside Portal/Performance:
3 in-page tabs (`command-list` / `execution-logs` / `analytics`) via `_TabPanel`, each backed by
a full-page partial (`Tabs/_CommandListTab.cshtml`, `Tabs/_ExecutionLogsTab.cshtml` — 290 lines,
`Tabs/_AnalyticsTab.cshtml` — 339 lines, 4 Chart.js charts). It loads **10 separate JS modules**:
`tab-panel.js`, `command-tabs.js`, `command-tab-loader.js`, `command-filters.js`,
`command-pagination.js`, `url-state.js`, `date-range-filter.js`, `autocomplete.js`,
`command-log-modal.js`, plus Chart.js itself. State is synced to the URL via a custom
`window.UrlState` module (`getStateForUrl`/`updateUrl`/`restoreStateFromUrl`/`isRestoringFromUrl`)
so filters/pagination/active-tab survive reload and back/forward — this is the one page in scope
doing real URL-state sync beyond simple query-string GET binding. Execution-logs and analytics
tabs each have their own date-range filter panel with quick-presets (`today`/`7days`/`30days`)
toggled via `window.DateRangeFilter`, and tab content itself is loaded via AJAX
(`window.CommandTabLoader.reloadActiveTab(...)`) rather than full page reloads — this needs a
REST/partial endpoint on the server side (likely the Commands API controller, not a page handler,
since the only POST page handler is `ClearAndRegisterGlobally`). A `_CommandLogDetailsModal`
partial + `command-log-modal.js` lets a log row open in a modal without navigating away — its
content partial is `CommandLogs/_CommandLogDetailsContent.cshtml`, which itself has a
"copy correlation ID" clipboard button using `window.ToastManager`.

`CommandLogs/Details.cshtml` (full page, not modal) is comparatively simple — read-only detail
view with 2 `_Badge`s, an `_Alert`, `_ToastContainer`, and `~/js/toast.js` for a copy-confirmation
toast.

**Complexity:** `Commands/Index` = **L** (3 AJAX-loaded tabs, URL-state sync, 4 charts, 10 JS
modules, autocomplete, modal). `CommandLogs/Details` = **S**.

---

## 4. Portal (`Pages/Portal/`)

Portal pages are the **public, non-admin, guild-member-facing** audio surfaces. They use a
dedicated `_PortalLayout.cshtml` (set by `Portal/_ViewStart.cshtml`) rather than the admin
`_Layout`, and `Portal/_ViewImports.cshtml` only pulls in `@using DiscordBot.Bot` (none of the
root `_ViewImports.cshtml` identity/tag-helper imports are inherited automatically — Razor
`_ViewImports` files merge up the folder tree, so Portal actually gets both, but it's worth
double-checking `SignInManager`/`UserManager` injection still resolves for a Blazor route base
under `/Portal`). `_PortalLayout.cshtml` injects `IThemeService`, has its own FOUC-guard theme
script, and always loads `~/js/api-client.js`, `~/js/user-preferences.js`, `~/js/toast.js`,
`~/js/shared/keyboard-shortcuts.js` (i.e. **every Portal page ships a keyboard-shortcuts module
by default** — worth checking what shortcuts are bound; none of the individual Portal pages
override this).

All three feature pages share `PortalPageModelBase` (`Portal/PortalPageModelBase.cs`, abstract,
extends `PageModel` directly, not `GuildPageModelBase` — this is a guild-scoped-by-route-param
base class unrelated to the admin `GuildPageModelBase`). Key behavior:
- `[AllowAnonymous]` on every concrete page — auth is done **inside** `OnGetAsync` via
  `CheckPortalAuthorizationAsync(guildId, portalName, ct)`, which returns a `PortalAuthResult`
  enum (`GuildNotFound` → `NotFound()`, `NotGuildMember` → `Forbid()`, `ShowLandingPage` → render
  a landing/verify page, `Authorized` → render the full portal).
- This is the **"landing page instead of redirect-to-login" UX pattern**: unauthenticated or
  non-member users get a 200 page with a different partial (`_PortalLanding` /
  `_PortalUnauthorized`), not a 302. A Blazor port must replicate this per-request branching
  inside the component rather than relying on `<AuthorizeView>`/route-level `[Authorize]` alone.
- SuperAdmins/Admins bypass guild-membership checks; regular users are checked against the
  cached `SocketGuild.GetUser` first, then a Discord REST `GetGuildUserAsync` fallback.
- `BuildVoiceChannelList(socketGuild)` is shared logic for the `_VoiceChannelPanel` component.

| Page | Route | Handlers | Services (beyond base) |
|---|---|---|---|
| `Soundboard/Index.cshtml` | `/Portal/Soundboard/{guildId:long}` | `OnGetAsync(guildId, ...)` only | `ISoundService`, `IGuildAudioSettingsRepository`, `IAudioService`, `IPlaybackService`, `ISettingsService` |
| `TTS/Index.cshtml` | `/Portal/TTS/{guildId:long}` | `OnGetAsync(guildId, ...)` only | `IAudioService`, `ITtsService`, `ISettingsService`, `ITtsSettingsService`, `IPlaybackService` |
| `VOX/Index.cshtml` | `/Portal/VOX/{guildId:long}` (explicit `@page` path) | `OnGetAsync(guildId, ...)` only | `IVoxClipLibrary`, `IVoxService`, `IAudioService`, `IPlaybackService` |

All three are GET-only PageModels of ~1200–1600 markup lines each (`VOX/Index.cshtml` is the
single largest file in this scope at 1566 lines) — every mutation (play a sound, save TTS
settings, upload a clip, join/leave voice) happens via `fetch()` from page-specific JS against
REST controllers, never a page POST handler. Each page passes a `window.portal*Config` object
(guildId **always as a quoted string** per the CLAUDE.md snowflake gotcha — confirmed correctly
followed in all three) to its inline-config JS module (`portal-tts-inline.js`,
`portal-soundboard-inline.js`, `portal-vox.js`), then loads a heavier feature JS
(`portal-tts.js`, `portal-soundboard.js`) plus `ssml-markers.js` for TTS. All three load SignalR
+ `~/js/dashboard-hub.js` + `~/js/voice-channel-panel.js` for **live voice-channel occupancy**
(who's in which channel, likely bot-connected-channel highlighting) — this is real-time
presence, not just data refresh. Soundboard additionally serializes its entire sound list to a
`<script id="soundboard-data" type="application/json">` data-island (same good pattern as
RatWatchAnalytics) rather than inline JS object literals. TTS has the richest form surface: a
`_ModeSwitcher` (simple/SSML), `_PresetBar`, `_VoiceSelector`, `_StyleSelector`,
`_EmphasisToolbar` + `_SsmlPreview` (Pro/SSML mode only), and a `_PauseModal` for inserting SSML
pause markers — essentially a small rich-text/markup editor. VOX uses `_NavTabs` +
`nav-tabs.css`/`nav-tabs.js` to group clips, plus its own unauthenticated/unauthorized landing
partials shown inline (`_PortalLanding`, `_PortalUnauthorized`) rather than only `_PortalLanding`
like TTS/Soundboard (VOX is the one Portal page that renders the "not a member" state, not just
"not logged in").

Portal shared partials (in scope per the task list): `Portal/Shared/_PortalHeader.cshtml`
(renders a `_TabPanel` for guild-scoped Portal nav), `_PortalLanding.cshtml`,
`_PortalUnauthorized.cshtml`, `_PortalAudioDisabledWarning.cshtml` (wraps `_Alert`).

**Complexity:** all three Portal feature pages = **L** (real-time SignalR presence, audio
playback control via REST+JS, large per-page JS bundles, JSON data islands, and — for TTS — an
inline SSML mini-editor). `_PortalLayout`/shared partials = **S** individually, but they are
load-bearing for every Portal page's theme/keyboard-shortcut/toast behavior.

---

## 5. Misc top-level pages

| Page | Route | Model base / Auth | Layout | Handlers | Notes |
|---|---|---|---|---|---|
| `Index.cshtml` | `/` (authenticated home) | `PageModel`, `RequireViewer` | `_Layout` | `OnGetAsync`, `OnPostRestartBotAsync` (manual admin check inside handler — `[Authorize]` can't target a handler method), `OnPostSyncAllGuildsAsync` | Dashboard: `_BotStatusBanner`, `_HeroMetricCard` list, `_ConnectedServersWidget`, `_ActivityFeedTimeline`, `_AuditLogCard`, `_QuickActionsCard`, restart confirmation modal, `_ToastContainer`. Loads Chart.js + SignalR (`dashboard-hub.js`, `dashboard-realtime.js`, `command-stats-chart.js`) — live dashboard tile updates. Injects `IBotService`, `IGuildService`, `ICommandLogService`, `IAuditLogService`, `IVersionService`, `IRatWatchService`, `IConnectionStateService`. |
| `Landing.cshtml` | `/landing` | `PageModel`, no auth | `_LayoutLanding` (explicit override) | `OnGet()` only | Marketing/unauthenticated splash page (624 markup lines). Scroll-driven nav-highlight JS (`updateActiveNav` on `scroll`, respects `prefers-reduced-motion`). Injects `IOptions<ApplicationOptions>` only — no data services. |
| `Search.cshtml` | `/Search` | `PageModel`, `RequireViewer` | `_Layout` | `OnGetAsync(CancellationToken)`, query bound via `[BindProperty(SupportsGet=true, Name="q")]` | Global search across guilds/command logs/users/pages/audit logs/message logs/reminders/scheduled messages, each result section behind its own permission check (uses `IAuthorizationService.AuthorizeAsync` per section) and `IUserDiscordGuildService` to scope guild results to what the user administers. Uses the `<highlight text="..." search-term="..." max-length="..." />` **custom tag helper** to bold matched substrings — a Blazor port needs an equivalent component, not a literal tag-helper port. Also renders `_GuildContextSelector` (a Shared component, out of this agent's direct scope but consumed here) when a page result needs a guild picker. No JS beyond what's inherited from `_Layout`; entirely server-rendered. |
| `Components.cshtml` | `/Components` | `PageModel`, `RequireAdmin` | `_Layout` | `OnGet()` (builds ~20 sample view-model collections), `OnPostShowcaseConfirm()` → `JsonResult` | **Not a real feature page — a live UI-component style guide/showcase** (622 markup / 614 code-behind lines) exercising every `Shared/Components` partial (buttons, badges, status indicators, spinners, form inputs/selects, alerts, 3 confirmation-modal variants, cards, empty states, 4 pagination styles, 4 `_NavTabs` variants incl. client-side tab panels). High value as a **reference for what the Blazor component library must cover**, but should probably not be ported 1:1 as a page — more useful mined for its enumeration of every component + variant. |
| `Error/403.cshtml` | `/Error/403` | `PageModel` (`ForbiddenModel`), `[AllowAnonymous]` implied (no explicit attribute, but no auth requirement blocks it) | `Layout = null` | `OnGet()` | Static |
| `Error/404.cshtml` | `/Error/404` | `PageModel` (`NotFoundModel`) | `Layout = null` | `OnGet()` | Static |
| `Error/500.cshtml` | `/Error/500` | `PageModel` (`ServerErrorModel`), `[AllowAnonymous]` | `Layout = null` | `OnGet(string? requestId)` | **Prerender-sensitive**: reads `HttpContext.Features.Get<IExceptionHandlerPathFeature>()` and `HttpContext.TraceIdentifier`, and only exposes `ExceptionMessage`/`StackTrace` when `IWebHostEnvironment.IsDevelopment()`. A Blazor equivalent needs an explicit error-boundary/exception-handler-middleware integration point, not just a component. |
| `_ViewImports.cshtml` (root) | — | — | — | — | `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` + `@addTagHelper *, DiscordBot.Bot` (registers the app's custom tag helpers, e.g. `<highlight>`), `@inject SignInManager<ApplicationUser>` / `@inject UserManager<ApplicationUser>` available to **every** page in scope by default (even ones that don't obviously need them) |
| `_ViewStart.cshtml` (root) | — | — | — | — | `Layout = "_Layout"` default for the whole tree; overridden per-page as noted above |

**Complexity:** `Index` = **L** (SignalR + Chart.js live dashboard). `Landing` = **M** (scroll JS,
otherwise static marketing content). `Search` = **S** (no client JS, but a custom tag helper to
replace). `Components` = **M** (not user-facing; JS is only what individual showcased components
need — tabs/modals — but its *purpose* for the port is as a component-inventory reference, not a
page to preserve as-is). Error pages = **S** each (500 needs a server-side exception-handler
equivalent, not a client concern).

---

## Cross-cutting observations

1. **Two antiforgery-over-fetch conventions coexist.** `Account/Privacy.cshtml` manually reads
   the `__RequestVerificationToken` hidden input and sends it as a `RequestVerificationToken`
   header on a raw `fetch()`. `Admin/Settings.cshtml` renders `@Html.AntiForgeryToken()` once and
   relies on `settings.js` to attach it (mechanism not visible from the page itself — must be
   checked in `wwwroot/js/settings.js` before porting). Standardize on one approach (e.g. a
   shared `apiFetch` wrapper) in the Blazor port rather than reproducing both.

2. **Discord snowflakes as strings is followed consistently** in every `window.portal*Config`
   / inline-JS object seen in this scope (`Portal/TTS`, `Portal/Soundboard`, `Portal/VOX`) — all
   wrap `guildId`/`currentUserId` in quotes. No violations found in the audited files. Keep this
   discipline in Blazor JS-interop calls and in any `long`/`ulong` serialized to a page for JS.

3. **TempData flash-message pattern** (`[TempData] SuccessMessage`/`ErrorMessage`, PRG
   redirect) is used throughout classic-form pages: `LinkDiscord`, `Profile` (partially),
   `Users/Index`, `Users/Edit`, `BulkPurge`, `UserPurge`, `Admin/Performance/Alerts` (reads
   `TempData["SuccessMessage"]`/`["ErrorMessage"]` directly rather than a typed property),
   `Admin/RatWatchAnalytics` (`TempData["ErrorMessage"]`). A Blazor port replaces this with
   either in-component state (no full navigation) or a toast/notification service — there is
   already a `window.ToastManager` used ad hoc on a few pages (`CommandLogs`,
   `_CommandLogDetailsContent`) that could become that service's Blazor equivalent.

4. **Two competing tab-navigation mechanisms.** (a) Full **separate routed pages** sharing one
   visual shell + a common tab partial, re-rendered server-side on each click (all 6
   `Admin/Performance/*` pages). (b) **In-page client tabs** switching visibility of already-
   rendered `hidden` panels with no navigation (`Admin/Logs` via hash routing,
   `Admin/Settings` via plain `onclick`, `Commands/Index` via `_TabPanel` + AJAX-loaded panel
   content + URL-state sync, `Components.cshtml`'s `_NavTabs` demos). A Blazor port should pick
   one pattern (component-local tab state, likely closer to (b)) rather than porting the
   page-per-tab duplication in (a) — that duplication exists only because Razor Pages made
   AJAX partial tabs harder to retrofit onto an already-shipped page-based design (see
   `Performance/Index.cshtml.cs`'s unused `OnGetPartialAsync`, which looks like an abandoned
   first attempt at doing this the (b) way).

5. **CSV export = `FileResult` from a GET handler**, always paired with a `UserTimezone`
   `[BindProperty(SupportsGet)]` populated client-side (not shown in these files, but must be by
   JS reading `Intl.DateTimeFormat().resolvedOptions().timeZone()`, per the
   `TimezoneHelper.ConvertToUtc(date, UserTimezone)` calls in `Admin/Logs`,
   `Admin/AuditLogs`) so date-range filters are interpreted in the browser's local time before
   being sent as UTC to the query. A Blazor port needs an equivalent "detect and forward browser
   timezone" step and a same-tab file-download flow (browser `download` triggered from a
   component method — standard Blazor `IJSRuntime` file-save, no special antiforgery wrinkle
   since it's a GET).

6. **SignalR is used far more widely than just the Guild pages this catalog excludes**: Admin
   Performance (Health/System/Commands/Alerts), the top-level `Index` dashboard, and **all three**
   Portal audio pages connect via `~/js/dashboard-hub.js`. Every one of these loads the
   `microsoft-signalr` UMD build from a CDN (`8.0.0`) plus Chart.js `4.4.1` from a **different**
   CDN (jsdelivr) — inconsistent version pinning style (`@4.4.1` vs `@@4.4.1` templated in
   `Admin/Performance/HealthMetrics.cshtml` — note the double `@@` there, a Razor-escaped `@`,
   worth grepping wwwroot to confirm the emitted URL is correct: `chart.js@4.4.1`). A Blazor
   port using a SignalR .NET client (`HubConnectionBuilder`) removes the JS SignalR dependency
   entirely for Server render mode; for WASM it still needs the same reconnect/dispose
   lifecycle currently handled ad hoc per-page (`beforeunload`/`pagehide` listeners, inconsistent
   across pages — some disconnect, some "keep connected, could optionally cleanup").

7. **Two legacy redirect-stub pages exist and should probably not be ported at all**:
   `Admin/AuditLogs/Index` and `Admin/MessageLogs/Index` both just `RedirectToPage("/Admin/Logs",
   new { tab = "..." })`. Their `Details` pages and (for AuditLogs) an apparently-orphaned
   `OnGetExportAsync` remain real and in use. Confirm with the team whether the redirect stubs'
   routes need to keep resolving (e.g. old bookmarks/links) — if so the Blazor router needs an
   equivalent redirect route, not a ported page.

8. **`Components.cshtml` is a component style guide, not a product page** — treat it as
   documentation input for building the Blazor component library (it enumerates every variant
   of every `Shared/Components` partial with sample view models) rather than porting it as a
   user-facing route. Same applies loosely to the unused `application` tab stub in `Admin/Logs`.

9. **Portal pages intentionally return 200 with a different body instead of redirecting**
   for unauthenticated/non-member users (`PortalPageModelBase.CheckPortalAuthorizationAsync` /
   `GetAuthResultAction`). This is a deliberate UX choice (avoid leaking guild existence via
   redirect-to-login vs 404 timing/behavior, and let the "verify membership" messaging live
   in-page) that a Blazor port must reproduce with explicit state branching inside the page
   component — plain `[Authorize]`/`<AuthorizeView>` redirect-on-fail semantics would regress
   this UX.

10. **Root `_ViewImports.cshtml` auto-injects `SignInManager`/`UserManager`** into every page
    in scope (via `@inject`), and registers the app's custom tag helpers
    (`@addTagHelper *, DiscordBot.Bot` — this is where `<highlight>` used in `Search.cshtml`
    comes from; the other custom tag helpers referenced in `patterns.md` such as `<authorize>`
    and `<filter-panel>` were not observed in use anywhere in this scope's files, only `<highlight>`
    was). Any Blazor port needs to inventory `DiscordBot.Bot`'s tag helper classes directly (not
    just what's referenced in this subset of pages) to know the full set to reimplement as
    components.

11. **Culture/timezone handling is manual and per-page**, not centralized: `Admin/Logs`,
    `Admin/AuditLogs` both do their own `UserTimezone` bind + `TimezoneHelper.ConvertToUtc` call;
    `data-utc="..." data-format="..."` attributes (seen in `LinkDiscord.cshtml`'s verification
    countdown) are picked up by an unnamed shared JS timezone-formatting utility
    (`window.timezoneUtils.convertDisplayTimes()`, referenced from `Commands/Index.cshtml`) that
    converts server-rendered UTC timestamps to the browser's local time after page load. A
    Blazor port should centralize this as one time-formatting component/service instead of the
    current "render UTC, patch with JS after load" pattern scattered across pages.

---

# Part 2 — Pages: guild areas


Scope: `src/DiscordBot.Bot/Pages/Guilds/**` (60 files: 30 `.cshtml` + 30 `.cshtml.cs`, one `.cshtml`-only partial pair counted in Members/Soundboard). Read in full via `cat`/`grep` on 2026-09-10.

Legend for **Handlers** column: `OnGet`/`OnPost[Name]Async` and what each returns. Legend for **Interactivity**: modals / tabs / pagination / live-update / audio / charts / drag-drop / upload / keyboard / URL-state.

---

## Guild shell

**Layout chain**: every guild page's `.cshtml` sets `Layout = "_GuildLayout"` explicitly in its `@{ }` block (no `_ViewStart.cshtml` in `Pages/Guilds/`, so it is not a folder convention — each file opts in). `_GuildLayout.cshtml` itself sets `Layout = "_Layout"`, so the chain is `page → _GuildLayout → _Layout` (the site-wide chrome: sidebar, navbar, SignalR bootstrap, toast container, theme cookie script, all the global `wwwroot/js/*.js` includes). The one exception is `PublicLeaderboard.cshtml`, which sets `Layout = null` and renders standalone (see below).

**`_GuildLayout.cshtml`** (`src/DiscordBot.Bot/Pages/Shared/_GuildLayout.cshtml`) renders, in order:
1. `Components/_GuildBreadcrumb` partial, bound to `Model.Breadcrumb` (`GuildBreadcrumbViewModel`).
2. `Components/_GuildHeader` partial, bound to `Model.Header` (`GuildHeaderViewModel` — icon, title, description, status badge, header actions).
3. Guild nav bar: desktop renders `Components/_TabPanel` fed by `GuildNavBarHelper.CreateGuildNavBar(guildId, activeTab, tabs)` (Pills style, `PageNavigation` mode, no persistence); mobile renders a dropdown built from the same tab list. Loads `~/js/tab-panel.js` and `~/js/guild-nav.js`.
4. `@RenderBody()` for page content.
5. `@RenderSectionAsync("Styles")` / `("Scripts")` pass-through so pages can still inject their own section content.

**Tabs** come from `GuildNavigationConfig.GetTabs()` (`src/DiscordBot.Bot/Configuration/GuildNavigationConfig.cs`) — a static, hard-coded list (not per-guild feature-flagged): Overview, Members, Moderation, Messages, Audio, Rat Watch, Reminders, Welcome, Assistant, Feature Requests (10 tabs, `Order` 1–10). Each tab has `Id`, `Label`, `PageName`, `UrlPattern` (with `{guildId}` token replaced via `GuildNavItem.GetUrl(guildId)`), and outline/solid SVG icon paths. **No tab is conditionally hidden based on per-guild settings** (e.g. Rat Watch tab always shows even if Rat Watch is disabled for that guild) — pages self-guard by loading data and showing "not enabled" banners instead.

**`GuildPageModelBase`** (`Pages/Guilds/GuildPageModelBase.cs`) is the base every guild page model extends (directly, or via `PaginatedGuildPageModel`). It does **not** enforce authorization itself — it only carries three settable view-model properties (`Breadcrumb`, `Header`, `Navigation`) plus `[TempData] SuccessMessage`/`ErrorMessage`, and helper methods (`BuildBasicBreadcrumb`, `BuildPageBreadcrumb`, `BuildHeader`, `BuildNavigation`, `PopulateGuildLayout`) that pages call from `OnGet` to fill those three properties. `PaginatedGuildPageModel : GuildPageModelBase` adds `[BindProperty(SupportsGet=true)]` pagination props: `SortBy` (default `"Name"`), `SortDescending`, `CurrentPage` (bound as query `pageNumber`), `PageSize` (default 10), plus settable `TotalPages`/`TotalCount`. A parallel non-guild `PaginatedPageModel` (`Pages/PaginatedPageModel.cs`) exists for the top-level `Guilds/Index` page (same shape, `PageModel` base instead of `GuildPageModelBase`).

**Authorization** is per-page, stacked `[Authorize]` attributes on the PageModel class — **not** a folder-level `AuthorizeFolder` convention (none found in `Program.cs`/`*ServiceExtensions.cs`):
- A role policy: `RequireViewer` / `RequireModerator` / `RequireAdmin` (hierarchical — `RequireAdmin` role-checks `SuperAdmin` or `Admin`; `RequireModerator` adds `Moderator`; `RequireViewer` adds `Viewer`). Defined in `IdentityServiceExtensions.AddAuthorizationPolicies`.
- Plus `[Authorize(Policy = "GuildAccess")]` on (almost) every page — evaluated by `GuildAccessHandler` (`Authorization/GuildAccessHandler.cs`) against `GuildAccessRequirement`. It reads `guildId` from route values or query string, resolves the ASP.NET Identity user, requires `DiscordUserId` to be linked, then calls `DiscordSocketClient.GetGuild(guildId).GetUser(...)` live against the in-memory Discord gateway cache: SuperAdmin bypasses; Admin role additionally requires `GuildPermissions.Administrator` in that live Discord guild; Moderator/Viewer only need to be a member. **This means guild-page authorization depends on the bot's live gateway state, not just the database** — a Blazor port needs an equivalent live-guild-membership check (not just a DB permissions table), most naturally as a custom `AuthorizeView`/cascading-auth-state provider that also calls into the Discord client.
- `Reminders/Index` is the one page with only `[Authorize(Policy = "GuildAccess")]` and no role policy (any authenticated guild member/viewer can see it).
- `Guilds/Index` (top-level list) has only `[Authorize(Policy = "RequireModerator")]` — no `GuildAccess` (there's no single guild in scope yet); it does its own row-level filtering by user role/guild membership inside `OnGetAsync`, and manually re-checks `RequireAdmin` via `IAuthorizationService.AuthorizeAsync` inside `OnPostSyncAllAsync` (handlers can't carry their own `[Authorize]` attribute in Razor Pages).
- `PublicLeaderboard` is `[AllowAnonymous]` and does its own three-state gate in code (unauthenticated → landing page; authenticated but not a guild member → `Forbid()`; authenticated + member → full data), plus a guild-level `PublicLeaderboardEnabled` settings flag.

**Per-page context loaded on every guild page** (via each page's own `OnGetAsync`, not a shared middleware/filter): guild existence check (404 if `IGuildService.GetGuildByIdAsync` returns null), guild name/icon for header, breadcrumb trail, and the active nav tab id. There is **no shared "guild context" service/filter** that centralizes this — each of the 27 page models independently calls `IGuildService.GetGuildByIdAsync`, checks null, and populates `Breadcrumb`/`Header`/`Navigation` (via `PopulateGuildLayout` or manually inline — both patterns are used, inconsistently, across pages). A Blazor port should almost certainly centralize this into a cascading `GuildContext` (guild DTO + permissions + active tab) resolved once per `/Guilds/{guildId}/...` route, replacing ~27 duplicated "load guild or 404" blocks.

**Bot presence / feature flags surfaced per page** (not centralized — each page queries its own service): global feature toggles come from `ISettingsService.GetSettingValueAsync<bool>("Features:AudioEnabled")` / `"Assistant:GloballyEnabled"` (runtime-configurable via the separate global Settings admin page, checked ad hoc by `AudioSettings`, `TextToSpeech`, `AssistantSettings`, `Soundboard`). Discord guild membership/roles/channels come from the injected `DiscordSocketClient` singleton (live gateway cache) or `IDiscordChannelResolver`, used directly inside many page models (`AudioSettings`, `Welcome`, `AssistantSettings`, `Members`, `ModerationSettings`, `AudioModerationLog`, `RatWatch`) rather than through a DTO service — a Blazor port needs a server-side boundary (e.g. a Blazor Server component or API call) since `DiscordSocketClient` cannot run client-side.

**SignalR**: `_Layout.cshtml` loads the SignalR JS client + `~/js/api-client.js` + `~/js/dashboard-hub.js` globally and auto-connects `DashboardHub` on every page load (`DOMContentLoaded`) — this is a global live-notification channel, not guild-scoped, used for toast/notification-bell delivery everywhere. Two guild pages additionally use it for feature-specific live updates: **Soundboard** (sound list / category changes) and **TextToSpeech** (`_VoiceChannelPanel` component, shared by Soundboard, TTS and VOX for showing bot voice-channel presence).

---

## Overview / core guild pages

| Page | Route | PageModel base | Auth | Handlers | Injected services | Scripts / partials | Interactivity | Complexity |
|---|---|---|---|---|---|---|---|---|
| `Guilds/Index` | `/Guilds` | `PaginatedPageModel` (not guild-scoped) | `RequireModerator` | `OnGetAsync` → `Page()`; `OnPostSyncGuildAsync(id)` → `JsonResult` (AJAX) or `RedirectToPage()`; `OnPostSyncAllAsync` → manual `RequireAdmin` check via `IAuthorizationService`, then `JsonResult`/redirect | `IGuildService`, `IAuthorizationService`, `UserManager<ApplicationUser>` | `~/js/toast.js`, `~/js/guild-sync.js`; role-filtered "Sync All" button (SuperAdmin/Admin only); `_StatusIndicator`, `_Pagination`, `_EmptyState`, `_Badge` partials | Desktop table + mobile card layout (two full render paths), classic GET-form search/filter/sort, per-row async sync spinner via `syncGuild()`/`syncAllGuilds()` JS, click-row-to-navigate with keyboard `Enter` support, toast on sync result | **M** — dual desktop/mobile markup, AJAX sync buttons with spinner state, role-gated bulk action |
| `Details` | `/Guilds/{guildId:long}` | `GuildPageModelBase` | `RequireModerator` + `GuildAccess` | `OnGetAsync(guildId)` → `Page()`/`NotFound()`; `OnPostSyncAsync(guildId)` → `JsonResult` (AJAX) or `RedirectToPage` | `IGuildDetailsAggregator` (single aggregate call across 6+ subsystems), `IGuildService`, `IGuildMembershipService` | `~/js/toast.js`, `~/js/guild-sync.js`; inline script for clipboard copy, "more actions" dropdown, Escape-to-close, relative-time formatting; 6× `_DashboardWidget` partials (Scheduled Messages, Rat Watch, Reminders, Audio, Assistant, Members) + one Activity widget | Dashboard-of-widgets overview page, dropdown menu, copy-to-clipboard, client-side relative/local time conversion, `CanEdit` computed from live guild-admin + app-role check | **M** — large aggregated read model (~25 bound properties) but no complex client behavior beyond a dropdown and clipboard |
| `Edit` | `/Guilds/{id:long}` | `GuildPageModelBase` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(id)`; `OnPostAsync()` — classic ModelState-validated form POST, redirects to `Details` on success | `IGuildService`, `IGuildAudioSettingsService` | none beyond shared `_ValidationScriptsPartial`-style layout scripts | Simple settings form (IsActive, audio toggle, auto-leave timeout, queue toggle), server-side validation re-render on error | **S** — plain `EditForm`-shaped page |
| `Welcome` | `/Guilds/{guildId:long}` (own model, not `GuildPageModelBase` — plain `PageModel` with hand-rolled Breadcrumb/Header/Navigation props) | `PageModel` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId)`; `OnPostAsync()` — classic form POST with 3 layers of validation (ModelState, "channel required if enabled", regex hex-color check), redirects to self on success | `IWelcomeService`, `IGuildService`, `IDiscordChannelResolver` | `_ValidationScriptsPartial` | Channel `<select>` populated from live Discord channels, embed-color hex validation, toggle-driven conditional required field | **S** — form with a few conditional-validation rules |
| `AssistantSettings` | `/Guilds/{guildId:long}` | plain `PageModel` (duplicates Breadcrumb/Header/Navigation props rather than extending `GuildPageModelBase`) | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId)`; `OnPostAsync()` — classic form POST, redirects to self | `IAssistantGuildSettingsService`, `IGuildService`, `IDiscordChannelResolver`, `IOptions<AssistantOptions>`, `ISettingsService` (global) | inline `<script>` (details not fully dumped, minor) | Multi-checkbox channel allow-list, rate-limit override vs. global default display, header action link to Metrics page, "globally disabled" banner read live from settings service | **S/M** — mostly a form, but reads two layered config sources (global + guild override) |
| `AssistantMetrics` | `/Guilds/{guildId:long}` | `GuildPageModelBase` | `RequireAdmin` + `GuildAccess` | `OnGetAsync()` only (guildId via `[BindProperty(SupportsGet)]`) | `IAssistantService`, `IGuildService` | none — **no chart library**; stat cards only (SVG icons, no canvas/Chart.js) | Pure server-rendered 30-day summary stats (cost, latency, cache hit rate, success rate) — no client interactivity | **S** — read-only stat dashboard, no charting |
| `PublicLeaderboard` | `/Guilds/{guildId:long}/Leaderboard` — **standalone**, `Layout = null` | plain `PageModel` | `[AllowAnonymous]` (custom in-code 3-state auth: anonymous / authenticated-non-member / authenticated-member) | `OnGetAsync(guildId)` only | `IRatRecordRepository`, `IRatWatchRepository`, `IGuildRatWatchSettingsRepository`, `IGuildService`, `DiscordSocketClient`, `UserManager<ApplicationUser>`, `IConfiguration` | not part of guild shell — no `_GuildLayout`, own page chrome | Public-facing landing/leaderboard hybrid page; parallelized (`Task.WhenAll`) username resolution for leaderboard + recent incidents; OAuth login redirect with `returnUrl` | **M** — sits outside the guild shell entirely; port needs its own public route/layout, not the guild dashboard shell |

---

## Analytics/*

Three dashboards sharing one pattern: `IServerAnalyticsService`/`IEngagementAnalyticsService`/`IModerationAnalyticsService` build a view model server-side, which is JSON-serialized into an inline `<script type="application/json">` block and consumed by hand-written Chart.js v4.4.1 (loaded from `cdn.jsdelivr.net`) initialization scripts. All three support `StartDate`/`EndDate` query-bound filters (`[BindProperty(SupportsGet = true)]`), default to a 7-day window, and use `~/js/shared/filter-panel.js`.

| Page | Route | Auth | Handlers | Services | Charts (Chart.js, canvas id) | Complexity |
|---|---|---|---|---|---|---|
| `Analytics/Index` (Server Analytics) | `{guildId:long}` | `RequireViewer` + `GuildAccess` | `OnGetAsync(guildId)` only, `PageModel` (not `GuildPageModelBase` — manual Breadcrumb/Header/Nav) | `IServerAnalyticsService`, `IGuildService`, `IMessageLogRepository`, `IDiscordChannelResolver` | Line (`activityOverTimeChart` — messages + active members, dual Y-axis), horizontal bar (`topChannelsChart`), plus a **custom hand-rolled activity heatmap** (`renderActivityHeatmap`, not Chart.js) | **L** — 2 Chart.js charts + custom heatmap renderer + date-range filter, all client-JS |
| `Analytics/Engagement` | `{guildId:long}` | `RequireViewer` + `GuildAccess` | `OnGetAsync(guildId)` only, `GuildPageModelBase` | `IEngagementAnalyticsService`, `IGuildService` | Line with dual Y-axes (`messageTrendsChart`), horizontal bar (`channelEngagementChart`) | **L** — 2 Chart.js charts, retention/engagement metric cards |
| `Analytics/Moderation` | `{guildId:long}` | `RequireModerator` + `GuildAccess` | `OnGetAsync(guildId)` only, `GuildPageModelBase` | `IModerationAnalyticsService`, `IGuildService`, `DiscordSocketClient` | Stacked area (`moderationTrendsChart`), doughnut (`caseDistributionChart`), horizontal bar (`moderatorWorkloadChart`) — **3 chart types** | **L** — heaviest chart page (3 distinct Chart.js configs) |

---

## AudioModerationLog/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `AudioModerationLog/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync()` only (all filters query-bound) | `IAudioPlaybackLogRepository`, `IGuildService`, `DiscordSocketClient` | `PaginatedGuildPageModel` (`SortBy="PlayedAt"` desc, `PageSize=25` by ctor override); GET-form filters (feature type, user id text, date range); server-side Discord user/channel name resolution (`ResolveUserName`/`ResolveChannelName` called from Razor, live gateway lookups — **N+1 risk** if called per row); `_TabPanel`, `_Pagination` partials | **S** — read-only filtered/paginated table, classic GET forms, no JS beyond layout |

---

## AudioSettings/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `AudioSettings/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync()`; 5 **named JSON handlers**, all `[FromBody]` DTOs → `JsonResult`: `OnPostSaveGeneralAsync`, `OnPostSaveLimitsAsync`, `OnPostSaveTtsSettingsAsync`, `OnPostUpdateCommandRolesAsync`, `OnPostResetToDefaultsAsync` | `IGuildAudioSettingsService`, `IGuildService`, `ISoundService`, `DiscordSocketClient`, `SoundboardOptions`, `ISettingsService`, `ITtsSettingsService` | **Fetch-driven, no classic form POST** — `window.audioSettings` JS object calls `?handler=SaveGeneral`/`SaveLimits`/`SaveTtsSettings`/`UpdateCommandRoles`/`ResetToDefaults` via `fetch`, manually reading the anti-forgery token off a hidden input; custom multi-select role-dropdown UI per soundboard command (`toggleRoleDropdown`/`toggleRole`/`removeRole`) that auto-saves on every change; live server-side validation returned as JSON error messages | **M** — 5 independent save actions, per-command role-restriction UI is the fiddliest part to port (would become component state + API calls) |

---

## FeatureRequests/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `FeatureRequests/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId)` only | `IFeatureRequestService`, `IGuildService` | Classic GET-bound status filter + page number (`new int Page`), server pagination, `_Alert` partials | **S** — plain filtered list |
| `FeatureRequests/Details` | `{guildId:long}/{id:guid}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId,id)`; `OnPostApproveAsync(guildId,id)`; `OnPostRejectAsync(guildId,id)` — classic POST/redirect | `IFeatureRequestService`, `IGuildService` | Approve/Reject action buttons, no JS | **S** — detail + 2 classic POST actions |

---

## FlaggedEvents/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `FlaggedEvents/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId, page, pageSize, ...)` with 5 filter params (`RuleType`, `Severity`, `FlaggedEventStatus`, date range) | `IFlaggedEventService`, `IGuildService` | **Fetch to REST controller**, not page handlers: dismiss/acknowledge call `/api/guilds/{guildId}/flagged-events/{eventId}/dismiss` and `/acknowledge`; `_SeverityBadge`, `_RuleTypeIcon`, `_StatusBadge` partials; per-row and bulk dismiss/acknowledge | **M** — filterable list + 2 inline REST-driven row actions |
| `FlaggedEvents/Details` | `{guildId:long}/{id:guid}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId,id)` only — all mutations are client-side `fetch` | `IFlaggedEventService`, `IGuildService` | Fetch to `/api/guilds/{guildId}/flagged-events/{eventId}/dismiss`, `/acknowledge`, and `/action` (×2, likely approve/escalate); related-events list | **M** — detail page entirely driven by REST-API fetch calls, no page handlers of its own |

---

## Members/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `Members/Index` | `/Guilds/{guildId:long}/Members` (explicit absolute route override) | `RequireModerator` + `GuildAccess` | `OnGetAsync()` only (`PaginatedGuildPageModel`, `SortBy="JoinedAt"` desc, `PageSize=25`) | `IGuildMemberService`, `IGuildService`, `DiscordSocketClient` | `~/js/member-directory.js` (496 lines): collapsible filter panel, custom role multi-select dropdown, bulk-selection checkboxes + toolbar, CSV export via `GET /api/guilds/{id}/members/export?...`, member-detail **modal** populated by `fetch('/api/guilds/{id}/members/{userId}')` (renders `_MemberDetailModal.cshtml` partial shell), focus-trap/keyboard modal handling | **L** — richest list page: filter panel + bulk actions + CSV export + async modal via REST API |
| `Members/Moderation` (per-user profile) | `/Guilds/{guildId:long}/Members/{userId:long}/Moderation` | `RequireAdmin` + `GuildAccess` | `OnGetAsync()` only — **all mutations client-side** | `IGuildService`, `IGuildMemberService`, `IModerationService`, `IModNoteService`, `IModTagService`, `IFlaggedEventService`, `DiscordSocketClient` | `~/js/user-moderation-profile.js`: fetch to `/api/guilds/{id}/users/{userId}/tags/{tag}` (add/remove tag), `/notes` (add), `/notes/{id}` (edit/delete) — tag/note CRUD entirely via REST API against a single aggregated read page | **L** — aggregates 5 services into one profile view, all writes via REST fetch (tags, notes) |
| `_MemberDetailModal` (partial) | n/a | n/a | n/a | n/a | Static modal shell (loading/error/content states) populated entirely by JS from Members/Index | **S** — becomes a Blazor component/dialog with the same 3 states |

---

## ModerationSettings/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `ModerationSettings/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync()`; **8 named JSON handlers**: `OnPostSaveOverviewAsync`, `OnPostSaveSpamAsync`, `OnPostSaveContentAsync`, `OnPostSaveRaidAsync`, `OnPostApplyPresetAsync`, `OnPostCreateTagAsync`, `OnPostDeleteTagAsync`, `OnPostImportTemplatesAsync` — all `[FromBody]`/`[FromQuery]` → `JsonResult` | `IGuildModerationConfigService`, `IModTagService`, `IGuildService`, `IFlaggedEventService`, `DiscordSocketClient` | `~/js/moderation-settings.js` (462 lines) using a shared `window.ApiClient.postRaw('?handler=X&guildId=Y', body)` wrapper (not raw `fetch`); multi-tab settings form (Overview/Spam/Content/Raid) each independently saved; preset application; mod-tag CRUD; template import (bulk) | **L** — the busiest settings page: 8 independent save actions across 4 sub-forms plus tag/preset/template management, all via a shared JS API-client wrapper |

---

## RatWatch/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `RatWatch/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId, page, pageSize)`; `OnPostCancelAsync`; `OnPostEndVoteAsync`; `OnPostUpdateSettingsAsync` — classic form POST/redirect | `IRatWatchService`, `IGuildService`, `IRatWatchRepository` | Two confirm **modals** (Cancel watch, End vote — show live vote counts passed via JS args), inline settings-display/settings-form toggle, Escape-to-close both modals | **M** — modal-driven admin actions over a live-voting feature, but classic POST (no fetch) |
| `RatWatch/Incidents` | `{guildId:long}` | `RequireModerator` + `GuildAccess` | `OnGetAsync(guildId)` with ~8 filter params (statuses list, date range, accused/initiator user, min votes, keyword, page/pageSize/sort); named handler `OnGetIncidentDetailAsync` fetched via `?handler=IncidentDetail&...` (returns partial HTML, loaded into a detail panel) | `IRatWatchService`, `IGuildService` | Rich filter bar (multi-value status, free-text, numeric, keyword), CSV export (`incidentsExportData` embedded JSON → client-side CSV build), async incident-detail fetch-into-panel pattern | **L** — heaviest filter surface in the app (8 independent filters) + CSV export + async detail fetch |
| `RatWatch/Analytics` | `{guildId:long}` | `RequireModerator` + `GuildAccess` | `OnGetAsync(guildId)` only | `IRatWatchRepository`, `IRatRecordRepository`, `IGuildService`, `DiscordSocketClient` | Chart.js: line (`watchesOverTimeChart`), doughnut (`outcomeDistributionChart`), horizontal bar (`topUsersChart`); uses a **dedicated external file** `~/js/rat-watch-analytics.js` (unlike the Analytics/* pages which inline their chart JS) | **L** — 3 chart types, same pattern as Analytics/Moderation |

---

## Reminders/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `Reminders/Index` | `{guildId:long}` | **`GuildAccess` only** (no role policy — any linked guild member can view) | (not fully dumped — `OnGetAsync`/list-style) | `IReminderRepository`, `IGuildService`, `DiscordSocketClient` | Standard list/table page | **S** — simplest auth surface in the whole catalog, worth flagging explicitly for the Blazor auth design |

---

## ScheduledMessages/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `ScheduledMessages/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId, ...)`; `OnPostDeleteAsync`; `OnPostToggleAsync` — classic POST/redirect | `IScheduledMessageService`, `IGuildService`, `IDiscordChannelResolver` | List with enable/disable toggle + delete, inline script (not fully dumped) | **S/M** |
| `ScheduledMessages/Create` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId)`; `OnPostAsync()` — classic ModelState-validated POST, redirect on success | `IScheduledMessageService`, `IGuildService`, `IDiscordChannelResolver` | Live char-count, Discord-message **preview** pane (newline→`<br>` mirror), radio-button schedule-type picker with conditional cron-expression field (`ScheduleFrequency` enum: Once/Hourly/Daily/Weekly/Monthly/Custom), client-side timezone capture (`Input.UserTimezone`) + `NextExecutionAtUtcIso` round-trip | **M** — richest single-entity form in the catalog: schedule-type UI + live preview + timezone handling |
| `ScheduledMessages/Edit` | `{guildId:long}/{id:guid}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(guildId,id)`; `OnPostAsync(guildId,id)`; `OnPostDeleteAsync(guildId,id)` | `IScheduledMessageService`, `IGuildService`, `IDiscordChannelResolver` | Same schedule-type/preview UI as Create, plus status badge logic (`GetStatusBadgeVariant`) and a delete action | **M** — same complexity as Create, plus delete + status display |

---

## Soundboard/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `Soundboard/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(...)`; `OnGetPartialAsync(guildId)` → **`PartialViewResult`** (`Partial("_SoundsList", this)`) for AJAX re-sort/reload; `OnPostDeleteAsync`; `OnPostUploadAsync`; `OnPostDiscoverAsync` (rescans sound folder on disk); `OnPostRenameAsync` | `ISoundService`, `ISoundFileService`, `ISoundboardOrchestrationService`, `IGuildAudioSettingsRepository`, `ISoundPlayLogRepository`, `IGuildService`, `DiscordSocketClient`, `IAudioService`, `ISettingsService` | **The most complex page in the catalog**: drag-and-drop file upload (`dragover`/`dragleave`/`drop` handlers into a hidden `<input type=file>`), `~/js/ajax-sort.js` reloading `_SoundsList` via `OnGetPartialAsync`, per-sound inline `<audio>` playback with play/pause icon state (`currentPlayingSoundId`, `resetPlayButton`), category CRUD via `fetch` to `/api/portal/soundboard/{guildId}/categories[/…]`, category drag-assignment per sound, right-click/kebab context menus (`menu-` prefixed, Escape-to-close-all), **SignalR** (`DashboardHub.connect()`) for live updates, `_VoiceChannelPanel` shared component (bot voice presence + join/leave), `_SortDropdown` component | **XL/L** — drag-drop upload + live audio player + SignalR + partial-view AJAX sort + REST category CRUD, by far the hardest page to port faithfully |
| `Soundboard/_SoundsList` (partial) | n/a | n/a | rendered by `OnGetAsync`/`OnGetPartialAsync`, model type `IndexModel` itself | — | Renders the sound grid/list only — becomes a Blazor component (`<SoundsList>`) receiving the sound collection + current sort as parameters, with the parent handling re-fetch instead of a partial-view round-trip | **S** (as a component, once the parent's complexity is absorbed) |

---

## TextToSpeech/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `TextToSpeech/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(...)`; `OnPostUpdateSettingsAsync`; `OnPostDeleteMessageAsync`; `OnPostSendMessageAsync` — all invoked via `fetch('?handler=...')` from `~/js/tts-page.js` (652 lines), not classic form POST | `ITtsHistoryService`, `ITtsSettingsService`, `ITtsService`, `IAudioService`, `ITtsPlaybackService`, `DiscordSocketClient`, `IGuildService`, `ISettingsService`, `IGuildAudioSettingsRepository`, `ISsmlBuilder` | `_ModeSwitcher` (simple text vs. SSML mode), `_PresetBar`, `_StyleSelector`, `_EmphasisToolbar`, `_SsmlPreview` (live-built SSML preview as you type/select), `_VoiceChannelPanel` (shared with Soundboard/VOX, SignalR-backed bot presence), history list with delete, "globally disabled" banner | **L** — SSML authoring toolchain (mode switch + emphasis + style + live preview) is bespoke UI logic, all fetch-driven |

---

## VOX/*

| Page | Route | Auth | Handlers | Services | Interactivity | Complexity |
|---|---|---|---|---|---|---|
| `VOX/Index` | `{guildId:long}` | `RequireAdmin` + `GuildAccess` | `OnGetAsync(long guildId)` — note `long` not `ulong` in the signature, cast internally; `OnPostRescanAsync` (rescans clip library on disk) | `IVoxClipLibrary`, `IGuildService`, `IGuildAudioSettingsRepository`, `VoxOptions` | Group filter + search + pagination (50/page) over a static clip library; **audio playback is a stub** — inline script instantiates `new Audio()` but the actual `audioPlayer.src = ...` line is commented out pending an API endpoint ("Note: This will require an API endpoint to serve clip audio") | **S/M** — mostly a browsable read-only catalog; note the playback TODO explicitly, since a Blazor port might be expected to finish it rather than carry the stub forward |

---

## Cross-cutting observations

1. **No shared "load guild + build layout" pipeline.** All 27 in-shell page models independently call `IGuildService.GetGuildByIdAsync`, `NotFound()` on null, and populate `Breadcrumb`/`Header`/`Navigation`. About half use `GuildPageModelBase.PopulateGuildLayout()`; the other half (`Welcome`, `AssistantSettings`, `AudioSettings`, `Analytics/Index`, `Analytics/Engagement`, `Analytics/Moderation`) hand-construct the same three view models inline despite not needing to — likely historical drift. A Blazor port should collapse this into one cascading guild-context resolver/route-level data loader.

2. **Two competing "save without navigating" patterns.** Newer/heavier settings pages (`AudioSettings`, `ModerationSettings`, `TextToSpeech`, `RatWatch/Incidents` detail) use named page handlers (`?handler=X`) called via `fetch`, returning `JsonResult`. Older/simpler forms (`Edit`, `Welcome`, `AssistantSettings`, `ScheduledMessages/Create|Edit`, `RatWatch/Index`) use classic `<form method="post">` + `ModelState` + `RedirectToPage`/`TempData` messages. `FlaggedEvents` and parts of `Members` bypass page handlers entirely and call REST API controllers (`/api/guilds/{guildId}/...`) directly. A Blazor port needs to pick one pattern (component method calling an API/service, most likely) and will effectively unify all three.

3. **`DiscordSocketClient` and `IDiscordChannelResolver` are injected directly into page models** (not wrapped behind a guild-scoped DTO service) on `Details`, `Edit`(no)/`Welcome`, `AssistantSettings`, `Members/Index`, `Members/Moderation`, `ModerationSettings`, `AudioSettings`, `AudioModerationLog`, `RatWatch/Analytics`, `Soundboard`, `TextToSpeech`, `PublicLeaderboard` — i.e. most of the catalog. This is live in-memory gateway state (roles, channels, live membership) with no caching/DTO boundary, and it cannot run in a Blazor WebAssembly client — any Blazor port keeping this data fresh needs it to stay server-side (Blazor Server component, or a new API layer wrapping the socket client) rather than being fetched client-side.

4. **Chart.js is the only charting library**, loaded from CDN (`chart.js@4.4.1` from `cdn.jsdelivr.net`) independently on each of the 4 chart-bearing pages (`Analytics/Index`, `Analytics/Engagement`, `Analytics/Moderation`, `RatWatch/Analytics`), each embedding its chart data as a `<script type="application/json">` block that a same-page `<script>` parses and feeds to `new Chart(...)`. `Analytics/Index` additionally hand-rolls a custom activity heatmap outside Chart.js. A Blazor port has real options here (keep Chart.js via JS interop, or move to a Blazor-native charting component) — this is the single biggest "pick an approach" decision for the port.

5. **SignalR (`DashboardHub`) is bootstrapped globally** in `_Layout.cshtml` (every page connects on load, for the notification bell / toasts) but is only used for feature-specific live data on **Soundboard** and via the shared `_VoiceChannelPanel` component on **Soundboard, TextToSpeech, and VOX** (bot voice-channel presence). No guild page uses SignalR for live-updating tables/lists/analytics beyond that.

6. **Two Discord-ID types drift in route/model code**: most routes/models use `ulong guildId`; a few use `long` (`PublicLeaderboard`'s `OnGetAsync(ulong guildId)` internally vs. route `{guildId:long}`; `VOX/Index.OnGetAsync(long guildId)` cast to `ulong`; `RatWatch/Incidents.OnGetAsync(long guildId)`). All route templates themselves use the `:long` constraint (Razor Pages has no unsigned route constraint), so this is consistent at the routing layer — but worth flagging per CLAUDE.md's "Discord IDs in JavaScript" gotcha: every page that echoes a guild/user/channel ID into an inline `<script>` block for use by fetch calls does so as a quoted string (`window.guildId = '@Model.GuildId'`), confirmed on `AudioSettings`, `Members/Index`. A Blazor port must preserve string-typed IDs across any JS interop boundary and in any client-rendered component state.

7. **Pagination is inconsistent**: `PaginatedGuildPageModel`/`PaginatedPageModel` (query-string `pageNumber`, `SortBy`, `SortDescending`, `PageSize`) is used by `Guilds/Index`, `AudioModerationLog/Index`, `Members/Index`. Several list pages instead roll their own `Page`/`PageSize` properties directly on the page model (`FeatureRequests/Index` uses `new int Page`, `RatWatch/Incidents` uses `Page`/`PageSize`/`SortBy`/`SortDesc` — note `SortDesc` not `SortDescending`). A Blazor port should standardize on one pagination component/state pattern rather than porting three slightly different shapes.

8. **Two pages intentionally sit outside the guild shell**: `PublicLeaderboard` (`Layout = null`, anonymous-accessible, its own three-state auth gate) is a genuinely separate public surface that should probably stay outside whatever Blazor "guild admin" layout the rest of this catalog becomes — it is closer to a marketing/community page than an admin tool.

9. **VOX audio playback is an acknowledged stub** in production code (commented-out `audioPlayer.src`) — flag this for the port as either "carry the stub forward" or "finally implement," a product decision, not just a mechanical port.

10. **Complexity distribution**: of 27 in-shell feature pages (excluding the two partials and `PublicLeaderboard`), roughly a third are simple CRUD/detail forms (S), a third are filtered lists or multi-tab settings pages with fetch-driven saves (M), and the remainder are chart-heavy dashboards or the two audio-authoring pages (L/XL) — see summary counts below.

---

# Part 3 — Design system and shared components


Source: `src/DiscordBot.Bot`. Compiled against the repo as of 2026-09-10. All paths are relative to `src/DiscordBot.Bot` unless stated otherwise.

---

## 1. Design Tokens

### 1.1 Token storage: CSS custom properties, not Tailwind literals

All actual color/shadow/radius/motion *values* live as CSS custom properties in `wwwroot/css/site.css`, section 1 ("DESIGN TOKENS"), lines 26–237. `tailwind.config.js` does **not** hard-code any color — it is a thin adapter that maps Tailwind utility names onto `var(--color-*)`:

```js
// tailwind.config.js
const rgb = (name) => `rgba(var(--color-${name}-rgb), <alpha-value>)`;
colors: { bg: { primary: rgb('bg-primary'), ... }, accent: { orange: { DEFAULT: rgb('accent-orange'), ... } }, ... }
fontFamily: { display: ['var(--font-display)'], sans: ['var(--font-body)'], mono: ['var(--font-mono)'] }
boxShadow: { sm: 'var(--shadow-sm)', md: 'var(--shadow-md)', ... }
borderRadius: { xs: 'var(--radius-xs)', sm: 'var(--radius-sm)', ... }
```

Every color is published **twice**: as a hex/`rgba()` value (`--color-success`) and as a bare RGB triplet (`--color-success-rgb`), so Tailwind's alpha modifiers (`bg-success/20`) resolve to `rgba(var(--color-success-rgb), 0.2)` and stay theme-correct. Hand-written CSS that needs alpha must use the `-rgb` triplet the same way.

Build pipeline: `site.css` (source, has `@tailwind base/components/utilities` + the token blocks + `@layer components`) is compiled by `npm run build:css` (Tailwind CLI, triggered from `dotnet build` via MSBuild, see `README.Tailwind.md`) into `wwwroot/css/app.css`, which is what `_Layout.cshtml` / `_LayoutLanding.cshtml` / `_PortalLayout.cshtml` actually `<link>`. **`site.css` itself is never served** — always edit the source, never `app.css`.

Token categories in `:root` (site.css:26-154):
- Typography: `--font-display` (Bricolage Grotesque), `--font-body` (DM Sans), `--font-mono` (JetBrains Mono)
- Layout: `--navbar-height` (56px), `--sidebar-width` (260px), `--sidebar-collapsed-width` (68px), `--content-max-width` (1440px)
- Radii: `--radius-xs/sm/md/lg/xl` (3/4/6/10/14px), `--radius-pill` (999px)
- Motion: `--ease-out-quart`, `--ease-out-expo`, `--ease-in-out`, `--transition-fast/normal/smooth/enter` (120/180/260/320ms)
- Z-index: `--z-fixed` (300), `--z-modal-backdrop` (1000), `--z-modal` (1001), `--z-popover` (1100), `--z-tooltip` (1200), `--z-toast`/`--z-notification` (1300)
- `--grain-opacity` (0.035) — a canvas grain texture toggle (0 disables); a `.no-grain` utility clears it
- Full graphite color ramp: bg-primary/secondary/tertiary/hover/inset, text-primary/secondary/tertiary/placeholder/inverse, accent-orange (+hover/active/muted), accent-blue (+hover/active/muted), accent-purple, semantic success/warning/error/info (+hover/active/bg/border), border-primary/secondary/strong/hover/focus, interaction washes (nav-hover/active, row-hover, overlay), Discord brand color, shadows sm/md/lg/xl, glass-effect tokens (guild header pill), status-active/inactive tokens

### 1.2 Theme switching mechanism

Two themes exist today: the default **"Graphite"** (dark, defined on bare `:root`) and **"Purple Dusk"** (a warm light theme, `[data-theme="purple-dusk"]` block, site.css:159-237, remapping the identical variable set — plum ink for text, plum for the "orange" accent role, rose for the "blue" accent role).

Mechanism, end to end:
1. **Attribute-based**: theme selection is expressed as `data-theme="<key>"` on `<html>`. No theme class, no per-component prop.
2. **Server-rendered default**: `_Layout.cshtml` / `_PortalLayout.cshtml` `@inject IThemeService ThemeService` and render `data-theme="@(await ThemeService.GetCurrentThemeKeyAsync())"` directly on `<html>` — so the correct theme paints on first byte for a signed-in user.
3. **Pre-paint blocking script**: immediately in `<head>`, before the CSS link, an inline `<script>` reads the `theme-preference` cookie (or `localStorage` fallback) and re-applies `data-theme` if different — a flash-of-wrong-theme guard for cached/edge-rendered HTML.
4. **Client-side manager**: `wwwroot/js/theme.js` (`ThemeManager` global) is the single source of truth for runtime changes: `applyTheme(key, persistToServer)` sets the DOM attribute, writes a `theme-preference` cookie (1yr, `SameSite=Lax`) *and* `localStorage['theme-preference']`, dispatches a `themechange` CustomEvent, and optionally PUTs `/api/theme/preference` to persist for a signed-in user. It also listens for the `storage` event so a theme change in one tab reflects in others.
5. **Server API**: `Controllers/ThemeController.cs` — `GET /api/theme/available`, `GET /api/theme/current` (resolves the effective theme through a precedence chain: user DB preference → cookie → admin-configured system default → hardcoded default), `POST /api/theme/user` (sets the user's DB preference **and** the cookie for next SSR), `POST /api/theme/default` (SuperAdmin-only, sets the system default). `Controllers/UserPreferencesController.cs` is unrelated to theme (generic key/value user prefs, no theme references).
6. Themes are **data-driven**, not hardcoded to two: `IThemeService` reads active themes from the database (`ThemeDto`), so adding a theme is "new CSS block + DB seed row", not a code change to the theme switcher itself (see `docs/articles/theme-creation-guide.md`).

Blazor-port implication: this whole mechanism (attribute-on-root, cookie+localStorage dual persistence, SSR-resolved default, DB-backed per-user override, live cross-tab sync) has no Blazor-native equivalent and must be reimplemented — likely as a small JS-interop service on top of the same CSS custom-property system (the CSS itself needs no change) plus a Blazor `CascadingValue`/DI theme service mirroring `IThemeService`.

### 1.3 Fonts

Loaded via Google Fonts `<link>` in every layout head (not self-hosted): `Bricolage+Grotesque:opsz,wght@12..96,500;12..96,600;12..96,700` (display/heading), `DM+Sans:wght@400;500;600;700` (body), `JetBrains+Mono:wght@400;500;600` (data/measured values — IDs, timestamps, counts, table headers use `.num`/`.tnum` classes with tabular numerals). `font-display: swap` is used implicitly via the Google Fonts URL.

### 1.4 Icons

**No icon library, no sprite sheet.** Every icon is an inline `<svg>` with a hand-copied Heroicons (https://heroicons.com, MIT) path, 24×24 outline style, `stroke-width="2"`, `fill="none"`, `aria-hidden="true"` when decorative. Icon paths are either inlined directly in `.cshtml` markup or held as `string` SVG-path properties on ViewModels (`ButtonViewModel.IconLeft/IconRight`, `BadgeViewModel.IconLeft`, `EmptyStateViewModel.IconSvgPath`, `HeroMetricCardViewModel.IconSvg` — the last takes a *raw inner SVG fragment*, not just a path, rendered via `@Html.Raw`). Size utilities (`icon-xs/sm/md/lg/xl`) and color utilities (`icon-primary/secondary/tertiary/orange/blue/success/warning/error`) are documented in `design-system.md` §6 but the *actual* components mostly use raw Tailwind (`w-4 h-4 text-accent-blue`) rather than those `icon-*` classes — an inconsistency (see §5 Gaps).

Blazor port: icons should become either (a) small dedicated icon components that accept a `Path` (or named icon enum) and render the wrapping `<svg>`, or (b) a shared `<Icon Name="..." />` component with a switch over cached path strings — either is pure markup, no interop.

### 1.5 Animation / motion utilities

No animation utility framework beyond Tailwind's built-ins (`animate-spin`, `animate-pulse`, `animate-bounce`, `animate-ping`) plus a handful of bespoke `@keyframes` scoped inside individual component `<style>` blocks (e.g. `_EmphasisToolbar.cshtml`'s `emphasisToolbar_slideUp`, `_PresetBar.cshtml`'s `preset-pulse`). Global custom-property-driven easing curves (`--ease-out-quart`, `--ease-out-expo`) are used in hand-written transitions throughout `site.css`. `prefers-reduced-motion` is explicitly honored in several places (mobile search, preview popups — see design-system.md §5 "Accessibility Features" → "Reduced Motion Support").

### 1.6 `@layer components` custom classes (site.css)

Two `@layer components` blocks (site.css:355-1465 and :1473-2207) define the hand-rolled component-class vocabulary Tailwind is told never to purge (`safelist` regex in `tailwind.config.js` covers `badge-*`, `btn-*`, `alert-*`, `status-*`, `card-*`, `page-*`, `form-*`, `toggle-*`, `topbar-*`, `sidebar-*`, `bot-status-*`, `hero-metric-*`, `table-*`, `kbd`, `section-*`). Key families:

| Family | Classes |
| --- | --- |
| Buttons | `.btn`, `.btn-primary`, `.btn-secondary`, `.btn-accent`, `.btn-danger`, `.btn-warning`, `.btn-ghost`, `.btn-sm`, `.btn-lg`, `.btn-block`, `.btn-icon`, `.btn-toggle` |
| Badges | `.badge`, `.badge-{orange,blue,success,warning,error,info,gray,purple}`, `.badge-{sm,lg}`, `.badge-{outline,subtle,solid,pill}`, `.badge-remove`, `.badge-case` |
| Cards | `.card`, `.card-header`, `.card-body`, `.card-footer`, `.card-title`, `.card-elevated`, `.card-interactive`, `.card-update-pulse`, `.card-enhanced` (the "Enhanced" variant, gradient top border) |
| Alerts | `.alert`, `.alert-{info,success,warning,error}`, `.alert-icon`, `.alert-title`, `.alert-message`, `.alert-close`, `.alert-content` |
| Status | `.status-indicator`, `.status-{online,idle,busy,offline}`, `.status-pulse`, `.status-dot`, `.severity-badge` + `.severity-{low,medium,high,critical}` (moderation.css), `.status-badge` + `.status-{pending,dismissed,acknowledged,actioned}` (moderation.css) |
| Forms | `.form-group`, `.form-label`, `.form-input`, `.form-select`, `.form-textarea`, `.form-help`, `.form-error`, `.form-success`, `.toggle`/`.toggle-input`/`.toggle-slider`/`.toggle-label` (a second toggle implementation — see Gaps) |
| Shell | `.sidebar-redesign`, `.sidebar-link-redesign`, `.sidebar-brand`, `.sidebar-group`, `.sidebar-section-header`, `.topbar`/`.navbar-redesign`, `.topbar-search`, `.topbar-user`, `.main-content-redesign`, `.page-container`, `.page-header`, `.page-title`, `.page-eyebrow` |
| Tables | `.table`, `.table-container`, `.table-header`, `.table-row`, `.table-cell`, `.table-cell-header` |
| Misc primitives | `.skeleton`/`.skeleton-static`, `.kbd`, `.avatar`, `.sparkline`/`.sparkline-bar`, `.trend-{up,down,neutral}`, `.hero-metric-card`, `.breadcrumb*`, `.dropdown-menu`, `.notification-dropdown`, `.user-menu-dropdown`, `.quick-action-card` |

Additional standalone stylesheets (not `@layer`, loaded per-page/per-feature so they can be split out cleanly in a Blazor port):
- `wwwroot/css/tab-panel.css` (13 top-level classes) — `_TabPanel` component, loaded via `@section Styles`
- `wwwroot/css/nav-tabs.css` (16 classes) — `_NavTabs` component
- `wwwroot/css/moderation.css` (57 classes) — severity/status badges, rule-type icon color classes, used by the moderation feature area
- `wwwroot/css/performance-pages.css` (136 classes) / `performance-shell.css` (22 classes) — the Bot Performance dashboard's own tab/shell chrome, largely independent of the generic `_TabPanel`/`_NavTabs`
- `wwwroot/css/portal.css` (53 classes) — shared chrome for the public Soundboard/TTS/VOX portal pages (`Pages/Portal/_PortalLayout.cshtml`), including a fixed `--portal-header-height` layout token

---

## 2. Component Catalog

All 56 files live in `Pages/Shared/Components/*.cshtml`. Every entry below lists: ViewModel class (from `ViewModels/Components/*.cs`, namespace `DiscordBot.Bot.ViewModels.Components` unless noted), key parameters/variants, the "slot" mechanism, JS coupling, and a proposed Blazor shape.

**Slot pattern used throughout**: there is **no `Func<object, IHtmlContent>` templating anywhere**. "Slots" (`HeaderContent`, `BodyContent`, `FooterContent`, `HeaderActions` on `CardViewModel`/`EnhancedCardViewModel`/`DashboardWidgetViewModel`) are plain `string?` properties containing pre-rendered HTML, injected with `@Html.Raw(...)`. Callers build that HTML by hand (string concatenation / other partial output) before constructing the ViewModel. This is the single biggest structural mismatch for Blazor: every one of these becomes a `RenderFragment` / `RenderFragment<T>` child-content parameter, which is strictly better (compile-checked, composable) but requires call-site rewrites everywhere a `BodyContent` string is currently built.

### 2.1 Primitives

| Component | ViewModel | Key params / variants | Slot mechanism | JS coupling | Blazor mapping |
| --- | --- | --- | --- | --- | --- |
| `_Button` | `ButtonViewModel` (record) | `Text`, `Variant` (Primary/Secondary/Accent/Danger/Ghost), `Size` (S/M/L), `Type`, `IconLeft/Right` (SVG path), `IsDisabled`, `IsLoading`, `LoadingText`, `IsIconOnly`, `AriaLabel`, `OnClick` (raw JS string!), `AdditionalAttributes: Dictionary<string,string>` | none (Text is a plain string, not child content) | none inherent; `OnClick` is a raw `onclick="..."` JS string emitted into the DOM | `Text`/icons as params, `OnClick` → `EventCallback`, `AdditionalAttributes` → `[Parameter(CaptureUnmatchedValues=true)]` dict. Pure markup, no interop. |
| `_Badge` | `BadgeViewModel` | `Text`, `Variant` (Default/Orange/Blue/Success/Warning/Error/Info), `Size` (S/M/L), `Style` (Filled/Outline/Subtle), `IconLeft`, `IsRemovable`, `OnRemove` (raw JS) | none | none | Direct parameter port; `OnRemove` → `EventCallback`. Pure markup. |
| `_Alert` | `AlertViewModel` | `Variant` (Info/Success/Warning/Error), `Title?`, `Message`, `IsDismissible`, `ShowIcon`, `DismissCallback` (raw JS fn name) | none | inline `onclick="@Model.DismissCallback"` | Pure markup + `EventCallback<>` for dismiss; can self-manage visibility with local `bool` state instead of calling out to JS. |
| `_Card` | `CardViewModel` | `Title/Subtitle`, `HeaderContent/HeaderActions/BodyContent/FooterContent` (raw HTML strings), `Variant` (Default/Flat/Elevated), `IsInteractive`, `IsCollapsible` + `IsExpanded`, `OnClick`, `CssClass` | **string HTML via `@Html.Raw`** | `onclick="this.nextElementSibling?.classList.toggle('hidden')"` for collapse toggle | `RenderFragment? HeaderContent/Body/Footer/HeaderActions`; collapsible → local `bool _expanded` state, no JS. Pure markup once slots are fragments. |
| `_EnhancedCard` | `EnhancedCardViewModel` | Same shape as Card plus `AccentColor` (CardAccent: None/Blue/Orange/Success/Info — gradient top border), `ShowGradientTop`, `HoverLift`, `CompactPadding`, `Id` | **string HTML via `@Html.Raw`** | same inline collapse toggle as Card | Same as Card; the two should probably merge into one Blazor `<Card>` with an `Accent` parameter (see Gaps — two card variants). |
| `_DashboardWidget` | `DashboardWidgetViewModel` | `Title/Subtitle`, `DetailUrl/DetailLinkText`, `IconSvgPath`, `IsEnabled?` (enabled/disabled badge), `BodyContent` (raw HTML) OR `EmptyState: EmptyStateViewModel?`, `ColSpan` (1|2), `HeaderActions: List<WidgetHeaderAction>` | **string HTML via `@Html.Raw`**, falls back to nested `_EmptyState` partial | none | `RenderFragment? Body`; `EmptyState` becomes a nested `<EmptyState>` component or fragment. Pure markup. |
| `_HeroMetricCard` | `HeroMetricCardViewModel` | `Title`, `Value`, `TrendValue/TrendDirection(Up/Down/Neutral)/TrendLabel`, `AccentColor`, `IconSvg` (raw inner SVG), `ShowSparkline` + `SparklineData: List<int>` (0-100 bar heights), `DataAttribute` (name of a `data-*` attr for SignalR live-patch targeting) | `IconSvg` raw HTML fragment | `DataAttribute` is consumed by realtime JS that patches the value in place (SignalR) | Pure markup + parameters; the `DataAttribute`/live-patch pattern should become a Blazor component that owns its own SignalR subscription instead of DOM patching. |
| `_EmptyState` | `EmptyStateViewModel` | `Type` (NoData/NoResults/FirstTime/Error/NoPermission/Offline — each has a canned icon), `Title`, `Description`, `IconSvgPath?` override, `PrimaryActionText/Url/OnClick`, `SecondaryActionText/Url`, `Size` (Compact/Default/Large) | none | `PrimaryActionOnClick` raw JS | Pure markup; action button becomes `EventCallback` or plain `<a>`. |
| `_Skeleton` | `SkeletonViewModel` | `Type` (Text/Title/Avatar/AvatarSmall/AvatarLarge/Button/Card/Rectangle — each has default w/h/radius), `Width/Height` (Tailwind class override), `Rounded`, `Animate`, `CssClass` | none | none | Pure markup, trivial component. |
| `_SkeletonCard` | `SkeletonCardViewModel` | `Type` (Stats/Server/Activity/Table — 4 canned composite layouts), `ShowHeader`, `CssClass` | none (layouts are hardcoded per type, not composable) | none | Pure markup; could become 4 small sub-components or one component with a `switch`. |
| `_LoadingSpinner` | `LoadingSpinnerViewModel` | `Variant` (Simple/Dots/Pulse), `Size` (S/M/L), `Message/SubMessage`, `Color` (Blue/Orange/White), `IsOverlay` | none | none | Pure markup. |
| `_StatusIndicator` | `StatusIndicatorViewModel` | `Status` (Online/Idle/Busy/Offline), `Text?` override, `DisplayStyle` (DotOnly/DotWithText/BadgeStyle), `IsPulsing`, `Size` (S/M/L) | none | none | Pure markup. |
| `_StatusBadge` | model = `DiscordBot.Core.Enums.FlaggedEventStatus` directly | enum only (Pending/Dismissed/Acknowledged/Actioned) — **no ViewModel wrapper**, styled via moderation.css `.status-badge` | none | none | Component takes the Core enum directly as its `Status` parameter; trivial markup. |
| `_SeverityBadge` | model = `DiscordBot.Core.Enums.Severity` directly | enum only (Low/Medium/High/Critical), Critical gets a pulsing dot | none | none | Same pattern — enum-typed parameter, pure markup. |
| `_RuleTypeIcon` | model = `DiscordBot.Core.Enums.RuleType` directly | enum only (Spam/Content/Raid/…) → icon+color+title | none | none | Pure markup, enum-typed parameter. |
| `_ConnectionStatus` | `ConnectionStatusViewModel` (positional record) | `State` (Connected/Connecting/Reconnecting/Disconnected), `CustomText?` | none | rendered with `id="connection-status"`; **JS-driven live updates expected** (dashboard-realtime.js sets `data-state` / text via id lookup rather than via SignalR-pushed re-render) | Should become a self-updating component (owns a SignalR/`HubConnection` subscription) rather than a static render target. |
| `_ConnectedServersWidget` | `ConnectedServersWidgetViewModel` | `Title`, `ViewAllUrl`, `Servers: List<ConnectedServerItemViewModel>` (Id, Name, IconUrl, Initials, AvatarGradient, MemberCount, `Status: ServerConnectionStatus`, CommandsToday, DetailUrl), `TotalServerCount` | none | inline `onclick` row navigation + `toggleServerActionMenu(this,event)` / `copyServerId(...)` (globals, defined elsewhere/inline, not found as a dedicated file — likely inline in the page) | Table → Blazor grid/table component; per-row action dropdown becomes local component state, clipboard copy needs `IJSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", ...)`. |

### 2.2 Form

| Component | ViewModel | Key params / variants | Slot mechanism | JS coupling | Blazor mapping |
| --- | --- | --- | --- | --- | --- |
| `_FormInput` | `FormInputViewModel` | `Id/Name/Label`, `Type` (text/email/password/search/url/tel/number...), `Placeholder/Value/HelpText`, `Size` (S/M/L), `ValidationState` (None/Success/Warning/Error) + `ValidationMessage`, `IsRequired/Disabled/ReadOnly`, `IconLeft/Right`, `MaxLength`, `ShowCharacterCount`, `AdditionalAttributes` | none | none inherent (relies on ambient page JS for char-count etc. via `MaxLength`/character count is actually computed server-side at render, no JS needed) | Maps almost 1:1 onto a Blazor `<InputText>`-wrapping component or a custom `<FormField>` bound with `@bind-Value`; `ValidationState`/Message → integrate with `EditContext`/`ValidationMessage`. Pure markup, no interop needed. |
| `_FormSelect` | `FormSelectViewModel` | `Id/Name/Label/Placeholder`, `SelectedValue`, `Options: List<SelectOption>` or `OptionGroups: List<SelectOptionGroup>` (`<optgroup>` support), `HelpText`, `Size`, `ValidationState/Message`, `IsRequired/Disabled`, `AllowMultiple` | none | none | Same shape as `<InputSelect>`; option groups need a small custom render loop (no native Blazor `InputSelect` optgroup support, but trivial to hand-roll). |
| `_FormToggle` | `FormToggleViewModel` | `Id/Name/Label/Description`, `IsChecked`, `IsDisabled` | none | `data-setting-toggle="true"` — consumed by `settings.js`/`moderation-settings.js` for dirty-state tracking and "restart required" banner logic on the Settings page | Straightforward `<InputCheckbox>`-style toggle; the "did anything change" dirty tracking currently done via `data-setting-toggle` DOM scanning should become explicit Blazor state (`EditContext.OnFieldChanged` or a bound model) rather than being reimplemented in JS. |
| `_SettingField` (Shared root, not Components/) | model = `DiscordBot.Core.DTOs.SettingDto` | Switches on `SettingDataType` (Boolean/Integer/Decimal/AllowedValues/String) to compose `_FormToggle`/`_FormSelect`/`_FormInput` internally; also renders a "Restart Required" badge per-field | composes 3 other components | none directly | A `<SettingField Setting="dto" />` wrapper component that internally dispatches to the typed input components — same composition, pure markup. |
| `_AutocompleteInput` | `AutocompleteInputViewModel` | `Id/Name/Label/Placeholder`, `Endpoint` (search API URL), `InitialValue/InitialDisplayText`, `GuildIdSourceElement` (id of another element to read guild-scoping from), `IsRequired`, `MinChars/DebounceMs/MaxResults`, `NoResultsMessage`, `HelpText` | none | **Heavy**: renders a bare text input + hidden input, then an inline `<script>` calls `AutocompleteManager.init({...})` (`wwwroot/js/autocomplete.js`, 639 lines) which owns fetch-debounce, dropdown construction, keyboard nav, and clear button — none of that DOM is server-rendered | **Needs JS interop** (or a full C# rewrite as a Blazor typeahead component with its own `HttpClient` search + keyboard handling). Good candidate to replace with a native Blazor component entirely (no interop) since the logic is self-contained fetch+DOM, not touching anything Blazor can't do natively — but is currently the most JS-heavy "simple" input. |
| `_VoiceSelector` | `VoiceSelectorViewModel` | `Voices: List<VoiceSelectorVoiceOption>` (Value/DisplayName/Gender/Locale/LocaleDisplayName), `SelectedVoice`, `ContainerId`, `OnVoiceChange` (raw JS fn name), `Placeholder/SearchPlaceholder` | none | **Heavy**: a fully custom 2-tier (locale-group → voice) combobox; all open/close/search/keyboard-nav logic lives in `wwwroot/js/voice-selector.js` (373 lines), driven by `VoiceSelector.toggle/search/toggleGroup/selectFromButton` globals and `data-selected-voice`/`data-callback-voice` | **Needs a full rewrite** as a native Blazor combobox component (grouped list + client-side filter over the `Voices` parameter) — the current design has no server round-trip, so this ports cleanly to pure C#/Razor with no interop once rebuilt. |
| `_StyleSelector` | `StyleSelectorViewModel` | `SelectedVoice/SelectedStyle`, `StyleIntensity: decimal` (0.5–2.0 slider), `AvailableStyles: List<StyleOption>` (Value/Label/Icon/Description/Example/IsDisabled), `ContainerId`, `OnStyleChange/OnIntensityChange` (raw JS fn names) | none | **Heavy**: inline `<script>` (~200 lines) handles select-change, slider-input, and an async `fetch('/api/portal/tts/voices/{voice}/capabilities')` call that disables unsupported styles and notifies `_EmphasisToolbar` of emphasis support via a global function call | Needs interop or, better, a full native rewrite: the capability fetch becomes a normal `HttpClient` call in `OnParametersSetAsync`, cross-component notification becomes a parent-owned state / event callback instead of `window.<callback>()`. |
| `_PresetBar` | `PresetBarViewModel` | `Presets: IReadOnlyList<PresetButtonViewModel>` (Id/Name/Icon/Description/VoiceName/Style/Speed/Pitch/IsActive), `ContainerId`, `OnPresetApply` (raw JS fn name) | none | **Heavy**: ~330-line inline script does keyboard nav, an active-badge DOM dance, and **owns its own persistence** — `fetch` POST/GET/DELETE to `/api/portal/tts/{guildId}/presets/custom` for user-saved custom presets, dynamically building extra buttons via `document.createElement` | Needs a genuine rewrite: custom-preset CRUD becomes an `HttpClient`-backed Blazor component with a `List<Preset>` state; no interop required once rebuilt, but this is one of the larger client-only mini-apps in the codebase. |
| `_ModeSwitcher` | `ModeSwitcherViewModel` (`TtsMode` enum: Simple/Standard/Pro) | `CurrentMode`, `ContainerId`, `OnModeChange` (raw JS fn name) | none | segmented control; inline script persists to `localStorage['tts_mode_preference']` and calls the named callback; also drives ARIA state (`aria-selected`, `tabindex`) purely client-side | Pure Blazor `<Toggle>`/tab-group component with local state + `EventCallback<TtsMode>`; `localStorage` persistence via a thin `IJSRuntime` call or a Blazor "local storage" service — light interop only for persistence, no DOM manipulation. |
| `_EmphasisToolbar` | `EmphasisToolbarViewModel` | `TargetTextareaId`, `ContainerId`, `OnFormatChange` (raw JS fn name, **validated as a JS-identifier regex in the ViewModel's `init` setter** — throws `ArgumentException` if invalid), `ShowKeyboardShortcuts` | none | **Heavy, textarea-selection-driven**: floating toolbar that tracks `textarea.selectionStart/End`, inserts marker syntax (`**bold**`, `[⏸ 500ms]`, etc.) directly into `textarea.value`, and shares marker-stripping logic with `ssml-markers.js` | **Needs interop**: this manipulates raw textarea selection state, which has no Blazor equivalent without `IJSRuntime` (selectionStart/End, cursor position, `getBoundingClientRect` positioning). A rewrite would keep a thin JS shim for the textarea mechanics and drive formatting decisions from C#. |
| `_SsmlPreview` | `SsmlPreviewViewModel` | `ContainerId`, `InitialSsml?`, `StartCollapsed`, `OnCopy` (raw JS fn name), `ShowCharacterCount` | none | **Heavy**: inline script does collapse/expand, clipboard copy (`navigator.clipboard.writeText`), and a hand-rolled XML syntax highlighter (`ssmlPreview_highlightSyntax`, regex-based tag/attribute/value coloring) — `window.ssmlPreview_update(containerId, ssml, plainLen)` is called externally by the TTS page whenever the message changes | Syntax highlighting and clipboard are the only genuinely JS-dependent bits (clipboard needs interop; highlighting is portable to C#). Collapse state → local Blazor state. Good candidate for a native rewrite with one small `IJSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", ...)` call. |

### 2.3 Navigation

| Component | ViewModel | Key params / variants | Slot mechanism | JS coupling | Blazor mapping |
| --- | --- | --- | --- | --- | --- |
| `_NavTabs` | `NavTabsViewModel` | `Tabs: IReadOnlyList<NavTabItem>` (Id/Label/ShortLabel/Href/IconPathOutline/Disabled), `ActiveTabId`, `StyleVariant` (Underline/Pills/Bordered), `NavigationMode` (InPage/PageNavigation/Ajax), `PersistenceMode` (None/Hash/LocalStorage), `AriaLabel`, `ContainerId` | none (content panels are separate elements matched by `data-nav-panel-for` + `data-tab-id`, not rendered by this component) | **Heavy**: `wwwroot/js/nav-tabs.js` (879 lines) — full keyboard nav (arrows/home/end), URL-hash and localStorage persistence, and an AJAX content-fetch mode that swaps `data-ajax-content-target` innerHTML from `fetch(tab.Href)` | This is explicitly documented (in its own XML doc comment) as the unifier of 4 legacy tab implementations. In Blazor: `InPage` mode → trivial (`@if (activeTab == "x")` conditional rendering, no JS); `PageNavigation` → `NavLink`; `Ajax` mode has no reason to exist in Blazor (fetch a partial's HTML) and should be replaced by dynamic component rendering. Hash/localStorage persistence needs light interop or `NavigationManager`. |
| `_TabPanel` | `TabPanelViewModel` | Nearly identical shape to NavTabs: `Tabs: IReadOnlyList<TabItemViewModel>` (adds `BadgeCount/BadgeVariant`, `Subtitle`), `ActiveTabId`, `NavigationMode` (InPage/PageNavigation/Ajax), `PersistenceMode` (None/UrlHash/LocalStorage), `StyleVariant` (Underline/Pills/Bordered/**Portal**), `Compact`, `AjaxUrlPattern/AjaxContentTarget`, `DisableAutoInit` | none, same external-panel convention as NavTabs | `wwwroot/js/tab-panel.js` (740 lines) — a close sibling implementation of nav-tabs.js | Same guidance as NavTabs — and per §5, **these two components are near-duplicates that should be merged into one Blazor `<TabGroup>`** rather than ported as two. |
| `_Breadcrumb` (Shared root) | none — reads `ViewData["Breadcrumbs"] as List<(string Text, string Url)>` | tuple list | none | none | `<Breadcrumb Items="List<(string,string)>" />` — trivial, but the `ViewData`-based wiring should become an explicit cascading parameter or page-level `[Parameter]`. |
| `_GuildBreadcrumb` | `GuildBreadcrumbViewModel` | `Items: List<BreadcrumbItem>` (Label/Url/IsCurrent) | none | none | Pure markup, strongly-typed version of the above. |
| `_CommandBreadcrumb` | `CommandBreadcrumbViewModel` | `ActiveTab` (string key: command-list/execution-logs/analytics) → `GetTabDisplayName()` | none | none | Pure markup; hardcoded 3-tab breadcrumb specific to the Commands page — could fold into `_GuildBreadcrumb`/generic `<Breadcrumb>` in the port. |
| `_CommandHeader` | `CommandHeaderViewModel` | `Title`, `Subtitle`, `ActiveTab` | none | none | Trivial page-header component. |
| `_GuildHeader` | `GuildHeaderViewModel` | `GuildId/Name/IconUrl`, `PageTitle/PageDescription`, `Actions: List<HeaderAction>` (Label/Url/Icon/Style: Primary\|Secondary\|Link/OpenInNewTab), `StatusBadge: BadgeViewModel?` | composes `_Badge` via nested partial | none | Pure markup; `Actions` → a `RenderFragment` or a typed list rendered via `foreach`, `StatusBadge` → nested `<Badge>` component. |
| `_GuildContextSelector` | `GuildContextSelectorViewModel` | `RouteTemplate` (has `{guildId}` placeholder), `Guilds: IReadOnlyList<GuildSelectorItem>` (GuildId/Name/IconUrl) | none | small inline script: click-outside-to-close for the dropdown, `Guid.NewGuid()`-based container id | Pure Blazor dropdown with local `bool _open` state — no interop needed once the click-outside behavior is reimplemented (`@onclick` + `@onfocusout`/JS click-outside helper — one of the few places light interop is still convenient). |
| `_Pagination` | `PaginationViewModel` | `CurrentPage/TotalPages/TotalItems/PageSize`, `PageSizeOptions: int[]`, `Style` (Full/Simple/Compact/Bordered), `ShowPageSizeSelector/ShowItemCount/ShowFirstLast`, `BaseUrl`, `PageParameterName/PageSizeParameterName` | none | none (pure `<a href>` links built from `BaseUrl` + query-string manipulation in C#) | Pure markup; page-size `<select onchange="window.location.href=...">` becomes `NavigationManager.NavigateTo(...)`. |
| `_SortDropdown` (Shared root) | `SortDropdownViewModel` | `Id`, `SortOptions: List<SortOption>` (Value/Label), `CurrentSort`, `ParameterName`, `UseAjax` + `TargetSelector/PartialUrl` | none | **Heavy inline `<script>`** (~200 lines, emitted per-instance): full dropdown open/close, keyboard nav (arrows/home/end/escape), and — in AJAX mode — dispatches a `sortchange` CustomEvent that page JS (`ajax-sort.js`) listens for to fetch `PartialUrl` and swap `TargetSelector` innerHTML | The AJAX partial-swap pattern has no reason to survive in Blazor (replace with component re-render). Dropdown UI → local Blazor state, no interop; keyboard nav reimplemented in C# event handlers. |

### 2.4 Overlays

| Component | ViewModel | Key params / variants | Slot mechanism | JS coupling | Blazor mapping |
| --- | --- | --- | --- | --- | --- |
| `_ConfirmationModal` | `ConfirmationModalViewModel` | `Id`, `Title/Message`, `ConfirmText/CancelText`, `Variant` (Info/Warning/Danger), `FormAction/FormHandler` (renders a real `<form method="post">` + `@Html.AntiForgeryToken()`), `CustomIconPath?` | none | Modal is server-rendered `hidden` and toggled via global `window.quickActions.showConfirmationModal(id)` / `hideConfirmationModal(id)` (`wwwroot/js/quick-actions.js`, 638 lines) — also does focus-trap and a `Promise`-based `confirm()/alert()/typedConfirm()` API layered on top (see `component-api.md` "quickActions JavaScript API") | Blazor modal component with local `bool _open`; the real `<form>` POST pattern maps to either a normal Blazor form-post (if staying MVC-style for that action) or an `EventCallback` invoking a service call. Focus trap needs a small interop helper (or a Blazor a11y modal library). |
| `_TypedConfirmationModal` | `TypedConfirmationModalViewModel` | Same as ConfirmationModal plus `RequiredText` (must be typed exactly to enable confirm), `InputLabel` | none | `oninput="window.settingsManager?.validateTypedInput(this)"` toggles the submit button's `disabled` by string-comparing to `data-required-text` | Trivial to port natively: bind the typed input to a Blazor `string`, compute `bool CanConfirm => Input == RequiredText`, no interop needed at all once decoupled from `settingsManager`. |
| `_PauseModal` | `PauseModalViewModel` | `Id`, `Title`, `MinDuration/MaxDuration/Step/DefaultDuration` (ms), `OnInsertCallback` (raw JS fn name, **required + validated as JS identifier**, throws on invalid), `InsertText/CancelText` | none | **Heavy**: range-slider with live gradient-fill update, 3 quick-preset buttons, a live text preview, and a hand-rolled focus trap — all in a ~150-line inline script exposing `window.pauseModal.{open,close,updateDuration,selectPreset,insert}` | Pure Blazor slider + preview binding (all local state, no server calls) — good native-rewrite candidate, zero interop needed except perhaps focus management. |
| `_CommandLogDetailsModal` | none (static shell) | fixed structural modal; body is filled by AJAX | none — content is injected by JS | **Heavy**: `wwwroot/js/command-log-modal.js` (261 lines) fetches log detail HTML/JSON and injects it into `#commandLogModalContent`; the outer shell here is just a spinner placeholder | In Blazor this becomes a real component that fetches its own detail via `HttpClient`/API and renders typed content — the "AJAX-injected HTML into a static shell" pattern goes away entirely. |
| `_GuildPreviewPopup` | `GuildPreviewViewModel` | `GuildId/Name/IconUrl`, `MemberCount/OnlineMemberCount?`, `OwnerUsername`, `BotJoinedAt`, `ActiveFeatures: IReadOnlyList<string>`, `IsActive`, `DetailsUrl/SettingsUrl` | none | Popup *content* is server-rendered but its **positioning, show/hide-on-hover/click/focus, and hover-intent debounce** are entirely owned by `wwwroot/js/preview-popup.js` (914 lines) reading `data-preview-type="guild" data-guild-id="..."` off arbitrary trigger elements anywhere on the page, then fetching this partial's HTML via AJAX | Needs interop or reimplementation as a Blazor popover/tooltip component (position calculation via `getBoundingClientRect` needs JS unless using CSS anchor positioning) — a fairly large behavior to port; the content itself is pure markup. |
| `_UserPreviewPopup` | `UserPreviewViewModel` | `UserId/Username/DisplayName?/AvatarUrl?`, `MemberSince?/Roles: IReadOnlyList<string>/LastActive?`, `IsVerified/HasActiveModeration`, `GuildId?`, `ProfileUrl/ModerationHistoryUrl?` | none | Same `preview-popup.js` engine as GuildPreviewPopup (`data-preview-type="user"`) | Same guidance as GuildPreviewPopup. |
| `_PreviewPopupLoading` / `_PreviewPopupError` | none (static markup) | loading skeleton / error state for the popup engine above | none | consumed by `preview-popup.js` while the AJAX fetch is in flight / on failure | Becomes the loading/error branch of whatever native Blazor popover component replaces the AJAX-popup engine — no longer separate partials. |
| `_PageLoadingOverlay` | `PageLoadingOverlayViewModel` | `Id`, `Variant/Size` (delegates to `_LoadingSpinner`, forced White color), `Message/SubMessage`, `ShowCancelButton/CancelText` | composes `_LoadingSpinner` | shown/hidden by `wwwroot/js/loading-manager.js` (referenced globally, id-targeted) | Blazor global-loading component driven by app state (e.g., a `LoadingStateService` the shell subscribes to) instead of `document.getElementById(id).classList`. |
| `_ToastContainer` (both `Pages/Shared/_ToastContainer.cshtml`, top-right, and `Pages/Shared/Components/_ToastContainer.cshtml`, bottom-right — **two different positioned variants**, see Gaps) | none — reads `TempData["Toast*"]` on the root-level one for server-flash toasts | container `<div id="toastContainer">` only; toasts themselves are 100% client-constructed | none | **All toast rendering is client-side**: `wwwroot/js/toast.js` (`ToastManager.show(type, message, opts)`, 279 lines) builds/animates/auto-dismisses toast DOM nodes; the root variant also bridges ASP.NET `TempData` flash messages into a `DOMContentLoaded` script that calls `ToastManager.show(...)` | Straightforward native rewrite: a Blazor `<ToastHost>` + `IToastService` (`Show(type, message)`) with an internal `List<Toast>` and `StateHasChanged()` — no interop needed; the `TempData` bridge becomes a server-side notification passed down after a form action (or simply not needed if actions are `EventCallback`s in the same circuit). |
| `_RestartBanner` | none (static, `<authorize policy="RequireAdmin">`-gated) | fixed copy: "Restart Required" | none | `onclick="window.settingsManager?.switchTab('BotControl'); return true;"` | Trivial static alert-style component; the settings-tab-switch handoff becomes a normal navigation/parameter. |

### 2.5 Dashboard / status widgets

| Component | ViewModel | Key params / variants | Slot mechanism | JS coupling | Blazor mapping |
| --- | --- | --- | --- | --- | --- |
| `_BotStatusBanner` | `BotStatusBannerViewModel` | `IsOnline`, `StatusText`, `ServerCount/TotalMembers`, `UptimeDisplay`, `Version`, `LatencyMs` | none | **Live-updated**: rendered with `data-bot-status-card`, `data-status-heading`, `data-uptime`, `data-version`, `data-latency`, `data-guild-count`, etc. — `wwwroot/js/bot-status-refresh.js` (281 lines) polls `GET /api/bot/status` every 30s (5s retry on failure) and patches these nodes directly; `dashboard-realtime.js` also touches `[data-bot-status-card]` via SignalR push | Should become a self-contained component that owns either a polling `Timer`/`PeriodicTimer` or a SignalR subscription and re-renders itself — eliminating all `data-*` DOM-patch targeting. |
| `_BotStatusCard` | model = `DiscordBot.Bot.ViewModels.Pages.BotStatusViewModel` (a **Pages-namespace** VM, not Components) | `StatusType`, `ConnectionState`, `LatencyMs`, `UptimeFormatted`, `GuildCount` — composes `_StatusIndicator` twice (badge style + dot-only style) | composes `_StatusIndicator` | same `data-bot-status-card`/`data-connection-state`/`data-latency`/`data-uptime`/`data-guild-count`/`data-last-updated` polling target as BotStatusBanner | Same guidance; note this card and the banner are two separate renderings of overlapping data (see Gaps). |
| `_ActivityFeed` | `ActivityFeedViewModel` (positional record) | `IsPaused`, `MaxItems`, `EmptyMessage` | none — **all items are rendered client-side from a `<template>`**, the C# side only renders the shell + empty state | **Heavy, SignalR-driven**: `dashboard-realtime.js` clones `#activity-item-template`, prepends new activity from the SignalR `DashboardHub` (`dashboard-hub.js`, 512 lines) connection, and the pause/resume button toggles a client-only `isPaused` flag that suppresses DOM insertion (items still arrive over the wire) | This is the clearest "needs interop or full C# component rewrite" case: a Blazor `<ActivityFeed>` would hold a `List<ActivityItem>` in component state and append via its own `HubConnection` (Microsoft.AspNetCore.SignalR.Client), no template-cloning needed at all — a strictly better native port. |
| `_ActivityFeedTimeline` | `ActivityFeedTimelineViewModel` | `Title`, `Items: List<ActivityFeedItemViewModel>` (Type: enum Success/Info/Warning/Error, Message, CommandText?, Source, Timestamp → computed `RelativeTime`/`TypeClassName`), `ShowRefreshButton`, `ViewAllUrl`, `MaxHeight` | none | Same `<template id="activity-item-template">` + realtime-append convention as `_ActivityFeed`, but this variant **also server-renders existing items** (a hybrid of SSR + live-append) | Same guidance — unify with `_ActivityFeed` in the Blazor port (see Gaps: near-duplicate activity feeds). |
| `_AuditLogCard` | `AuditLogCardViewModel` | `Logs: IReadOnlyList<AuditLogItem>` (Id/Timestamp/RelativeTime/Category/CategoryName/CategoryIcon/Action/ActionName/ActorDisplayName/TargetType/TargetId/GuildName/Description) — static factory `FromLogs(IEnumerable<AuditLogDto>)` does relative-time formatting + icon/description lookup in C# | none | none (fully server-rendered, `<authorize policy="RequireAdmin">`-gated footer link) | Pure markup + a C# formatting helper class ported as-is; no interop. |
| `_CommandStatsCard` | model = `DiscordBot.Bot.ViewModels.Pages.CommandStatsViewModel` (Pages namespace) | `TopCommands`, `TotalCommands`, `TimeRangeHours?` (24/168/720/null=all-time `<select>`) | none | **Chart.js-driven**: embeds `chartData` as a `<script type="application/json" data-command-stats-initial>` blob; `wwwroot/js/command-stats-chart.js` reads it and renders a horizontal bar chart on `<canvas id="commandUsageChart">`; the time-range `<select>` triggers a reload | Needs a Blazor-compatible charting approach — either JS-interop wrapping Chart.js (`IJSObjectReference`) or a native Blazor charting library; the JSON-blob-handoff pattern goes away in favor of passing data directly as a C# parameter into the interop call. |
| `_ConnectedServersWidget` | *(listed under Primitives above — also a dashboard widget; lives in both mental categories)* | | | | |
| `_GuildStatsCard` | model = `DiscordBot.Bot.ViewModels.Pages.GuildStatsViewModel` | `TotalGuilds/ActiveGuilds/InactiveGuilds` | none | none | Pure markup, trivial stat card. |
| `_QuickActionsCard` | `QuickActionsCardViewModel` | `Title`, `Actions: IReadOnlyList<QuickActionItemViewModel>` (Id/Label/IconPath/Color: 7-value enum/ActionType: Link\|PostAction/Href/Handler/RequiresConfirmation/ConfirmationModalId/IsAdminOnly), `UserIsAdmin` (filters admin-only actions) | none | `onclick="window.quickActions?.showConfirmationModal(...)"` or `.submitQuickAction(handler, this)` — the latter does an AJAX POST + button loading-state toggle, from `quick-actions.js` | `ActionType.Link` → `NavLink`; `ActionType.PostAction` → `EventCallback` invoking a service method directly (no AJAX-form-post needed in a Blazor Server/circuit model); confirmation → shared modal component. |
| `_RecentActivityCard` | model = `DiscordBot.Bot.ViewModels.Pages.RecentActivityViewModel` | `Activities` (CommandName, Success, ErrorMessage?, GuildName, RelativeTime) | none | Refresh button is present but **explicitly non-functional** (`title="Refresh (not yet functional)"`) | Pure markup port; an easy place to *add* real refresh behavior in the port that the current UI promises but doesn't deliver. |

### 2.6 Audio / TTS

| Component | ViewModel | Key params / variants | Slot mechanism | JS coupling | Blazor mapping |
| --- | --- | --- | --- | --- | --- |
| `_VoiceChannelPanel` | `VoiceChannelPanelViewModel` | `GuildId` (required), `IsCompact`, `ShowNowPlaying/ShowProgress`, `IsConnected`, `ConnectedChannelName/Id?`, `ChannelMemberCount?`, `AvailableChannels: IReadOnlyList<VoiceChannelInfo>` (Id/Name/MemberCount), `NowPlaying: NowPlayingInfo?` (Name/DurationSeconds/PositionSeconds → computed `ProgressPercent`), `Queue: IReadOnlyList<QueueItemInfo>` (Position/Name/DurationSeconds → computed `DurationFormatted`) | none | **Heavy, real-time**: `wwwroot/js/voice-channel-panel.js` drives channel-select → join, leave button, stop-playback, skip-queue-item, and a mobile collapse toggle (`toggleVoicePanel()` inline script in the partial itself) — almost certainly wired to SignalR for now-playing/queue live updates given the `data-connected`, `data-channel-id`, `data-queue-position` hooks | Prime candidate for a native SignalR-backed Blazor component (owns its own `HubConnection`, channel selection posts through a service call) — eliminates all the `data-*` targeting. |
| `_VoiceSelector`, `_StyleSelector`, `_PresetBar`, `_ModeSwitcher`, `_SsmlPreview`, `_EmphasisToolbar` | *(see Form section above — these are the TTS-specific controls; grouped there since they are literally form inputs, cross-referenced here since they only appear on TTS pages)* | | | | |

### 2.7 Two-file consistency note

56 files were catalogued; a handful of ViewModels are referenced by non-`Components/` partials or by other components as sub-types and are not separately listed above because they have no partial of their own: `SelectOption`/`SelectOptionGroup` (FormSelect), `HeaderAction`/`WidgetHeaderAction` (GuildHeader/DashboardWidget), `BreadcrumbItem`, `GuildSelectorItem`, `NavTabItem`/`TabItemViewModel`, `ConnectedServerItemViewModel`, `PresetButtonViewModel`, `QuickActionItemViewModel`, `StyleOption`, `VoiceSelectorVoiceOption`, `VoiceChannelInfo`/`NowPlayingInfo`/`QueueItemInfo`, `SortOption`, `AuditLogItem`. In a Blazor port these all become plain POCOs / component parameters, unchanged.

---

## 3. Layouts and Shell

### 3.1 `_Layout.cshtml` (main authenticated shell)
`@inject IThemeService`. Renders `<html data-theme="...">`, the pre-paint theme script, Google Fonts links, `~/css/app.css` + `~/css/tab-panel.css`, a sidebar-collapse FOUC-prevention script (reads `localStorage['sidebarCollapsed']`, adds `.sidebar-collapsed` to `<html>` before paint if `window.innerWidth >= 1024`). Body renders, in order: skip-link, `#mobileOverlay`, `_MobileSearchOverlay`, `_Sidebar`, `_Navbar`, `<main>` wrapping breadcrumb (`Html.PartialAsync("_Breadcrumb")` only if `ViewData["Breadcrumbs"]` is set) + `@RenderBody()`, `_PageLoadingOverlay` (with a default `PageLoadingOverlayViewModel`), `_ToastContainer` (root variant). Script bundle at the bottom (order matters — later scripts assume earlier globals exist): SignalR client (jsdelivr CDN, pinned `8.0.0`) → `api-client.js` (fetch wrapper with anti-forgery token + JSON/error handling) → `dashboard-hub.js` → `notification-bell.js` → `navigation.js` → `toast.js` → `quick-actions.js` → `loading-manager.js` → `timezone.js` → `bot-status-refresh.js` → `search.js` → `preview-popup.js` → `theme.js` → an inline `DashboardHub.connect()` call → `@RenderSectionAsync("Scripts")`. No CSP nonce or antiforgery meta tag is emitted in the layout itself (antiforgery tokens are per-form via `@Html.AntiForgeryToken()` inside individual modal/confirmation forms).

### 3.2 `_LayoutLanding.cshtml`
Minimal marketing/landing shell — fonts + `app.css` only, no theme injection (`data-theme` is **not** set — landing pages are unauthenticated and always render in the default Graphite palette), no sidebar/navbar/toast/loading chrome, just `@RenderBody()` + `@RenderSectionAsync("Scripts")`.

### 3.3 `_GuildLayout.cshtml` (nested layout: `Layout = "_Layout"`)
Not a full HTML shell — a nested content layout for guild-scoped pages. Expects `Model.Navigation` (cast to `GuildNavBarViewModel`), `Model.Breadcrumb` (cast to `GuildBreadcrumbViewModel`), `Model.Header` (cast to `GuildHeaderViewModel`) on the page model (dynamic-typed access via `Model.X`, not compile-checked — a `GuildPageModelBase` convention per `CLAUDE.md`). Renders `_GuildBreadcrumb`, `_GuildHeader`, then a **dual-rendering guild nav**: desktop uses `_TabPanel` (via `GuildNavBarHelper.CreateGuildNavBar(guildId, activeTab, tabs)`, which converts `GuildNavItem`→`TabItemViewModel` and forces `StyleVariant.Pills` + `NavigationMode.PageNavigation`), mobile uses a hand-rolled `<select>`-like dropdown (button + absolutely-positioned menu, `#guildNavDropdownToggle`/`#guildNavDropdownMenu`, driven by `guild-nav.js`). Adds `~/js/tab-panel.js` and `~/js/guild-nav.js` to the Scripts section.

### 3.4 `_Navbar.cshtml` (`Pages/Shared/_Navbar.cshtml`)
Top bar: mobile sidebar-toggle button, desktop sidebar-collapse toggle, mobile-only brand, centered search form (`asp-page="/Search"`, `Ctrl/⌘+K` hint with `#kbd-mac`/`#kbd-other` toggled by JS platform-sniffing), right side: mobile search icon button, notification bell (`#notificationBellButton` → `NotificationBell.toggle()`, dropdown populated by JS, unread badge `#notificationBadge`), then auth section — `@if (SignInManager.IsSignedIn(User))` renders a user-menu dropdown (avatar-or-initials, display name, Profile/Privacy links, a POST logout form with antiforgery), else a Sign-In button. **Directly injects `SignInManager`/`UserManager`** in the partial (implicit — no explicit `@inject`, relying on `_ViewImports.cshtml`, not read in this pass but implied by usage) — in Blazor this becomes an `AuthenticationStateProvider`-driven `<AuthorizeView>`.

### 3.5 `_Sidebar.cshtml`
`@inject IVersionService`, `@inject IOptions<ObservabilityOptions>`. Computes active-section booleans from `ViewContext.RouteData.Values["page"]` string-prefix matching (`IsPage`/`StartsWith` local functions) — this is how the nav decides which link gets `.active` + `aria-current="page"`. Structure: brand block, then three `<authorize policy="...">`-gated groups:
- **Overview** (all authenticated): Dashboard (no gate) / `RequireModerator`: Servers / `RequireViewer`: Commands, Bot Performance / `RequireAdmin`: Rat Watch Analytics, Settings
- **Administration** (`RequireAdmin`): Users, Logs, Notifications / `RequireSuperAdmin` nested: Bulk Purge, User Purge
- **Developer** (`RequireAdmin`): Components (the showcase page) + conditional external links to Kibana/Seq if `ObservabilityOptions` has URLs configured (opens in new tab)

Footer: bot status LED + version (`VersionService.GetVersion()`) — note this is a **static "Bot Online" label**, not live-bound (see Gaps — inconsistent with the genuinely-live `_BotStatusBanner`). Role gating is entirely through the `<authorize policy="...">` tag helper (§4); a Blazor port replaces every occurrence with `<AuthorizeView Policy="...">`.

### 3.6 `_Breadcrumb.cshtml`, `_MobileSearchOverlay.cshtml`, `_ToastContainer.cshtml` (root), `_ValidationScriptsPartial.cshtml`
- `_Breadcrumb`: reads `ViewData["Breadcrumbs"]` tuple list, renders an `<ol>` with a chevron separator between non-last items.
- `_MobileSearchOverlay`: full-screen search overlay shell (`#mobileSearchOverlay`), input `#mobile-search-input`, three content states (`#mobile-recent-searches`, `#mobile-search-results` hidden, `#mobile-search-empty`) all populated/toggled by `search.js` — no C# model.
- `_ToastContainer` (root): see §2.4 — bridges `TempData` flash messages into `ToastManager.show(...)` calls.
- `_ValidationScriptsPartial`: loads jQuery 3.7.1 + jquery-validate 1.21.0 + jquery-validate-unobtrusive 4.0.0 from cdnjs (SRI-pinned) — this is the **only jQuery usage found**; ASP.NET's unobtrusive client validation is not used by Blazor (`EditForm`/`DataAnnotationsValidator` replaces it entirely), so this whole partial has no Blazor equivalent and should simply be dropped.

### 3.7 `Pages/Portal/_PortalLayout.cshtml`
A separate full HTML shell for the **public, unauthenticated Discord-embed portal** (Soundboard/TTS/VOX pages reached via signed links, not the admin sidebar app). `@inject IThemeService` (still theme-aware — same `data-theme` + pre-paint script pattern as `_Layout`), adds `<html class="portal-page">`, loads `app.css` + `tab-panel.css` + `portal.css`. Script bundle is deliberately lighter than the admin shell: `api-client.js`, `user-preferences.js` (localStorage-first cache with debounced (500ms) background server sync, `pref:{guildId}:{key}` key format), `toast.js`, `shared/keyboard-shortcuts.js`. No sidebar, navbar, breadcrumb, or notification bell — portal pages own their own headers.

---

## 4. TagHelpers (`TagHelpers/*.cs`)

| TagHelper | Element | What it does | Blazor equivalent |
| --- | --- | --- | --- |
| `AuthorizeViewTagHelper` | `<authorize policy="...">` / `<authorize-view policy="...">` | Suppresses its own output entirely (`output.TagName = null`) unless the user is authenticated **and** (a) the given `policy` succeeds via `IAuthorizationService`, or (b) the user is in any of a comma-separated `roles` list, or (c) neither is given, in which case any authenticated user passes. Used pervasively in `_Sidebar` and inside several components (`_AuditLogCard`, `_ConnectedServersWidget`, `_CommandStatsCard`, `_RecentActivityCard`, `_RestartBanner`). | `<AuthorizeView Policy="..."> ... </AuthorizeView>` (and `<AuthorizeView Roles="...">` for the roles case) — a near-exact built-in Blazor equivalent, no custom component needed. |
| `RequireRoleTagHelper` | `<require-role roles="A,B">` | Same suppress-if-unauthorized pattern, roles-only (no policy option). | `<AuthorizeView Roles="A,B">` — built-in. |
| `FilterPanelTagHelper` | `<filter-panel title="..." is-collapsible="" default-expanded="" active-filter-count="" use-hidden-toggle="" toggle-id="" content-id="" chevron-id="" container-class="">` | Renders a collapsible filter-panel shell around arbitrary child content (a `<form>` typically), building the header button + chevron + active-filter badge + content wrapper as a hand-built HTML string (`StringBuilder`), with two toggle strategies: `UseHiddenToggle` (class-toggle, page-specific JS) or the default max-height-animation toggle driven by a global `toggleFilterPanel()` function. `Title` is HTML-encoded manually (`WebUtility.HtmlEncode`). | A `<FilterPanel Title="..." IsCollapsible="true">@ChildContent</FilterPanel>` Razor **component** (not a TagHelper — Blazor has no TagHelper concept) with local `bool _expanded` state replacing both toggle strategies; note there's already a parallel non-TagHelper `FilterPanelViewModel` (`ViewModels/Components/FilterPanelViewModel.cs`) that duplicates this exact same set of properties but **has no corresponding `.cshtml` partial** — dead/unused ViewModel (see Gaps). |
| `HighlightTagHelper` | `<highlight text="..." search-term="..." max-length="" show-context="">` | Wraps `TextHighlightHelper` (`Helpers/`, not read in this pass) to `<span>`-wrap matched search terms in the given `text`, optionally truncating around the match or showing surrounding context. | A `<Highlight Text="..." SearchTerm="..." />` component doing the same string-processing in C# (the underlying `TextHighlightHelper` logic ports untouched — it's plain string manipulation, no framework dependency) and emitting `MarkupString`/`RenderFragment` `<mark>` spans. |

---

## 5. Gaps, Risks, and Inconsistencies

### 5.1 Components whose behavior lives mostly in JavaScript (high port risk)
These render a thin server-side shell and depend on a substantial, stateful client-side script to actually function — each is a real design decision in Blazor (interop shim vs. full native rewrite), not a markup port:

- **`_ActivityFeed` / `_ActivityFeedTimeline`** — SignalR-fed, `<template>`-clone-driven feed (`dashboard-realtime.js`, `dashboard-hub.js`). Best fixed by a native Blazor `HubConnection`-backed component (see §2.5) — no interop needed once rewritten, but it's a genuine rewrite, not a template port.
- **`_AutocompleteInput`** — `autocomplete.js` (639 lines) owns all fetch/debounce/dropdown/keyboard logic; the server renders only two `<input>`s.
- **`_VoiceSelector`** — `voice-selector.js` (373 lines), a fully custom grouped combobox with zero server-rendered interaction affordances beyond option data.
- **`_StyleSelector`** — inline script + an async capability-fetch (`/api/portal/tts/voices/{voice}/capabilities`) that cross-talks to `_EmphasisToolbar` via a global function.
- **`_PresetBar`** — inline script that is itself a small CRUD app (fetch/POST/DELETE against `/api/portal/tts/{guildId}/presets/custom`, dynamic DOM node construction for custom presets).
- **`_EmphasisToolbar`** — raw `textarea.selectionStart/End` manipulation + `getBoundingClientRect` positioning; genuinely needs `IJSRuntime` interop even after a rewrite (no Blazor-native text-selection API).
- **`_SsmlPreview`** — clipboard API + hand-rolled regex syntax highlighter; highlighter ports to C# cleanly, clipboard needs interop.
- **`_NavTabs` / `_TabPanel`** — 879 + 740 lines of near-identical keyboard-nav/persistence/AJAX-swap JS (`nav-tabs.js`, `tab-panel.js`).
- **`_SortDropdown`** — ~200 lines of *inline, per-instance* `<script>` (not even an external file) duplicating dropdown+keyboard-nav logic already present in NavTabs/TabPanel.
- **`_GuildPreviewPopup` / `_UserPreviewPopup`** (+ their loading/error partials) — positioning, hover-intent, and AJAX-fetch owned by `preview-popup.js` (914 lines) operating on arbitrary `data-preview-type` trigger elements anywhere in the DOM.
- **`_VoiceChannelPanel`** — real-time channel/queue/now-playing state via `voice-channel-panel.js`, plus its own mobile-collapse script inline in the partial.
- **`_BotStatusBanner` / `_BotStatusCard`** — 30-second polling (`bot-status-refresh.js`) *and* SignalR push (`dashboard-realtime.js`) both patch the same `data-*` hooks — two live-update mechanisms targeting the same DOM, which a Blazor component should collapse into one subscription.
- **`_ConfirmationModal` / `_PauseModal` / `_TypedConfirmationModal` / `_CommandLogDetailsModal`** — all rely on `quick-actions.js` / `command-log-modal.js` globals (`window.quickActions`, `window.pauseModal`, `window.settingsManager`) for open/close/focus-trap; straightforward to rewrite natively (local component state) but every call site that currently does `onclick="window.quickActions?.showConfirmationModal('id')"` needs updating.
- **`_ToastContainer`** (both variants) — 100% client-rendered content (`toast.js`); server only emits the mount point (+ a `TempData`-to-JS bridge on the root variant).

### 5.2 Inconsistencies between components

- **Two card components.** `_Card` (`CardViewModel`) and `_EnhancedCard` (`EnhancedCardViewModel`) are near-identical (title/subtitle/header/body/footer slots, collapsible behavior, interactive hover) — the only real difference is `EnhancedCard`'s `AccentColor` gradient-top-border and `HoverLift`/`CompactPadding` options, plus a different CSS class (`.card` vs `.card-enhanced`). These should merge into one Blazor `<Card>` with an optional `Accent` parameter rather than porting as two components.
- **Two confirmation modals plus a typed variant living in three separate files** (`_ConfirmationModal`, `_TypedConfirmationModal`) with duplicated icon/color/button-class switch logic (`GetVariantColorClass`/`GetConfirmButtonClass`/`GetDefaultIconPath` — copy-pasted between the two `.cshtml` `@functions` blocks verbatim). A Blazor `<ConfirmationModal RequireTypedConfirmation="bool">` could unify both.
- **Two tab systems.** `_NavTabs` and `_TabPanel` have almost identical ViewModel shapes (`NavTabItem` vs `TabItemViewModel` differ only by `BadgeCount/BadgeVariant/Subtitle`) and near-duplicate JS engines (`nav-tabs.js` vs `tab-panel.js`). `NavTabsViewModel`'s own XML doc says it "consolidates the functionality of legacy tab implementations (PerformanceTabs, AudioTabs, GuildNavBar, TabPanel)" — but `_TabPanel` is still the one actually used by `_GuildLayout` (via `GuildNavBarHelper`) and still has its own separate CSS/JS/ViewModel, i.e. the stated consolidation is incomplete. A Blazor port should pick one tab component.
- **Two toggle-switch implementations.** `_FormToggle` (`.form-toggle*` classes, its own scoped `<style>` block inside the partial) and the `.toggle`/`.toggle-input`/`.toggle-slider` classes defined separately in `site.css` `@layer components` — it's unclear from static reading whether anything still consumes the latter, or whether it's a leftover from before `_FormToggle` existed; worth auditing call sites before porting either.
- **Two `_ToastContainer` partials with different positions** — `Pages/Shared/_ToastContainer.cshtml` (top-right, `top: 80px`, includes the `TempData` bridge, is the one actually wired into `_Layout.cshtml`) vs `Pages/Shared/Components/_ToastContainer.cshtml` (bottom-right, `bottom-4 right-4`, no `TempData` bridge, not referenced by any layout read in this pass). The `Components/` one appears to be dead code or a legacy leftover — confirm usage before porting either.
- **An orphaned `FilterPanelViewModel`.** `ViewModels/Components/FilterPanelViewModel.cs` exists (title/collapsible/active-count/expanded/badge-text/toggle-content-chevron ids/`UseHiddenToggle` — an exact property-for-property match of `FilterPanelTagHelper`'s attributes) but has **no corresponding `Components/_FilterPanel.cshtml` partial** — the actual filter-panel rendering path is the TagHelper (§4), which duplicates the same logic as a hand-built `StringBuilder` HTML string instead of a Razor partial. This ViewModel looks unused; a Blazor `<FilterPanel>` component should be built once, matching the TagHelper's actual behavior, and the orphan ViewModel dropped (or repurposed as the component's `[Parameter]`s, which is effectively what it already is).
- **Mixed enum-as-model vs. ViewModel-as-model.** Most components take a dedicated ViewModel, but three (`_StatusBadge`, `_SeverityBadge`, `_RuleTypeIcon`) take a bare `Core.Enums.*` type as `@model` directly. Functionally fine, but it means Core (framework-free, per `CLAUDE.md`) enums are reached into directly by presentation-layer components — in the Blazor port these become `[Parameter] public FlaggedEventStatus Status { get; set; }` etc., which is unremarkable, but it's a different pattern from every other component and worth flagging as intentional rather than accidental during the port.
- **Two `Pages.*`-namespaced ViewModels used by `Components/` partials.** `_BotStatusCard` (`ViewModels.Pages.BotStatusViewModel`), `_CommandStatsCard` (`ViewModels.Pages.CommandStatsViewModel`), `_GuildStatsCard` / `_RecentActivityCard` (`ViewModels.Pages.GuildStatsViewModel` / `RecentActivityViewModel`) all break the `Components/` namespace convention by consuming page-level ViewModels instead of dedicated `Components`-namespace ones. Not a functional bug, but means these four "shared" components are less reusable/self-contained than the rest, and a Blazor port should decide whether to give them their own component-scoped parameter types instead of reusing page DTOs.
- **`_Sidebar`'s bot-status footer is static** ("Bot Online" hardcoded text + version), while `_BotStatusBanner`/`_BotStatusCard` on the dashboard are genuinely live (polling + SignalR). A design inconsistency worth resolving rather than porting as-is (either make the sidebar LED live too, or stop implying it updates).
- **Icon color utility classes documented but seemingly unused.** `design-system.md` §6 documents `icon-primary/secondary/tertiary/orange/blue/success/warning/error` CSS classes; none of the 56 components or the layouts read in this pass actually use them (all icon coloring observed is raw Tailwind `text-accent-blue` etc.). Confirm before porting whether these classes exist anywhere in `site.css`/`moderation.css`/etc., or drop them from the design-system doc.
- **`onclick`/raw-JS-string ViewModel properties are pervasive** (`ButtonViewModel.OnClick`, `CardViewModel.OnClick`, `AlertViewModel.DismissCallback`, `BadgeViewModel.OnRemove`, `ModeSwitcherViewModel.OnModeChange`, `StyleSelectorViewModel.OnStyleChange/OnIntensityChange`, `PresetBarViewModel.OnPresetApply`, `SsmlPreviewViewModel.OnCopy`, `EmphasisToolbarViewModel.OnFormatChange`, `PauseModalViewModel.OnInsertCallback` — the last two even **validate the string as a JS identifier at construction time and throw `ArgumentException` if it isn't**). This "pass a global function name as a string" pattern is the single most pervasive porting hazard: every one of these needs to become an `EventCallback`/`EventCallback<T>` parameter, and the JS-identifier-validation code (in `EmphasisToolbarViewModel`/`PauseModalViewModel`) becomes entirely unnecessary.

### 5.3 Documentation vs. code mismatches

- `docs/articles/design-system.md` §"Theme System"/"Purple Dusk Theme" gives **stale hex values** that don't match `site.css`'s actual `[data-theme="purple-dusk"]` block: the doc lists `--color-bg-primary: #E8E3DF` / text-primary `#4F214A` / accent-orange(role) `#614978` etc. with slightly different values and even a different accent-blue-role hex (`#D5345B` doc vs `#c9305a` actual) than what's genuinely in `site.css:159-237` (`#ebe6e2`, `#3f1a3b`, `#614978`, `#c9305a` respectively — close but not identical, and the doc's own numbers are internally inconsistent about which theme version they describe). The doc also still calls the default theme **"Discord Dark"** in its "Theme Resolution Hierarchy" list and body copy, while `site.css`'s own header comment says the current (v2.0) default theme is named **"Graphite"**. Treat `site.css` as ground truth; the doc needs a refresh pass, not a re-read.
- `design-system.md`'s Icon Usage section's icon-color hex values (`.icon-orange { color: #cb4e1b; }` etc.) also don't match the current `--color-accent-orange: #e6602b` token — another sign this section predates the v2.0 "Graphite" rewrite and wasn't updated with it.
- The **component-level docs** (`component-api.md`, `razor-components.md`) match the actual `.cshtml`/ViewModel code closely for the primitives verified in this pass (Button, Badge, Card, StatusIndicator, FormInput/Select, Alert, ConfirmationModal, LoadingSpinner, EmptyState, Pagination) — these two docs appear to be kept in sync with code, unlike `design-system.md`'s theme section.
- `theme-creation-guide.md`'s "Theme Resolution Hierarchy" description (User Preference DB → Cookie → Admin Default → System Default) **is accurate** and matches `ThemeController`/`theme.js` behavior exactly — use that doc, not `design-system.md`, as the authoritative theme-mechanism reference during the port.

### 5.4 Summary counts

- 56 partials in `Pages/Shared/Components/`, grouped: 17 primitives-ish (incl. dashboard-adjacent ones counted once), 15 form/TTS controls, 10 navigation, 10 overlays, 9 dashboard/status widgets, 6 audio/TTS-specific (some overlap between groups by nature of the component, see per-section notes).
- 51 of 56 have a dedicated `Components`-namespace ViewModel; 4 use `Pages`-namespace ViewModels instead (`_BotStatusCard`, `_CommandStatsCard`, `_GuildStatsCard`, `_RecentActivityCard`); 3 take a bare `Core.Enums` type as model (`_StatusBadge`, `_SeverityBadge`, `_RuleTypeIcon`); 5 have no model at all (static/structural: `_CommandLogDetailsModal`, `_PreviewPopupError`, `_PreviewPopupLoading`, `_RestartBanner`, `_ToastContainer`).
- Roughly 17 of the 56 components carry meaningful client-side JavaScript logic beyond simple `onclick` wiring (listed in §5.1); the remainder are effectively pure markup + parameters and should port to Blazor with zero interop.

---

# Part 4 — JavaScript


Scope: `src/DiscordBot.Bot/wwwroot/js/**/*.js` — 72 files, 29,894 lines (per `wc -l`).
Read via full-text read for the smaller/complex files and via targeted grep extraction
(top comment, `fetch()`/URL literals, `querySelector`/`getElementById`/`dataset`/`data-*`,
`addEventListener`, `localStorage`/`sessionStorage`, `window.*` exports, SignalR
`connection.on`/`.invoke`, timers/observers/history/clipboard/audio) for the rest, per the
task's own guidance for the larger files. Page references found by grepping `Pages/**/*.cshtml`
for each filename; layout files checked directly (`_Layout.cshtml`, `_LayoutLanding.cshtml`,
`_GuildLayout.cshtml`, `Portal/_PortalLayout.cshtml`).

**Classification key**
- **A** — pure UI state / DOM toggling / fetch+render → replaced entirely by Blazor component state + injected services.
- **B** — mostly replaceable, thin JS interop shim survives (focus, scroll, clipboard, localStorage, keyboard shortcuts, media queries).
- **C** — must stay JS (Web Audio, Chart.js canvas rendering, drag-and-drop, file readers, XHR upload progress) → wrapped as an interop module.
- **D** — obsolete under Blazor (AJAX partial-HTML loading, url-state/history sync, loading-state managers, api-client wrappers, tab/script loaders, antiforgery-token plumbing) — deleted, not ported. Also used for **orphaned files that no page currently loads** (dead code found during this audit).

---

## 1. Master file table

### Global shared infrastructure (loaded from `_Layout.cshtml` / `_PortalLayout.cshtml` / `_GuildLayout.cshtml`)

| File | Lines | Purpose | Loaded by | DOM contract | API / endpoints | SignalR | Browser APIs | 3rd-party | Class |
|---|---:|---|---|---|---|---|---|---|---|
| `api-client.js` | 236 | Shared `fetch` wrapper: antiforgery token injection, JSON (de)serialize, `ApiClientError` w/ status+Retry-After, `get/post/put/del(+Raw)`, `showErrorToast`. Exposed as `window.ApiClient` and CommonJS (tested by `__tests__/api-client.test.js`). | `_Layout.cshtml`, `_PortalLayout.cshtml` (and used internally by ~15 other page/portal scripts) | reads `input[name="__RequestVerificationToken"]` | wraps arbitrary URLs passed by callers | — | `fetch` | — | **D** |
| `loading-manager.js` | 455 | `LoadingManager` singleton: full-page overlay w/ cancel + timeout, per-button spinner swap, per-container overlay, skeleton show/hide, generic form-submit loading wrapper, body-scroll lock. | `_Layout.cshtml` | `#pageLoadingOverlay`, `#pageLoadingOverlayCancelBtn`, `[data-skeleton]`/`[data-content]`, `.loading-container-overlay` | — | — | — | — | **A** |
| `url-state.js` | 367 | Two-way sync between URL query string and Commands-page filter/pagination state (`paramMapping`, `popstate` handling, `history.replaceState`/`pushState`). | `Commands/Index.cshtml` | reads `window.CommandFilters`/`window.CommandPagination`/`window.CommandTabLoader` globals | — | — | `history.replaceState/pushState`, `popstate` | — | **D** |
| `toast.js` | 279 | `ToastManager` singleton: creates `#toastContainer` if absent, queued toasts (max 5), auto-dismiss w/ hover-pause + progress bar, screen-reader live region, action button. | `_Layout.cshtml`, `_PortalLayout.cshtml`, several pages directly | `#toastContainer`, `#toastLiveRegion`, `.toast-*` | — | — | `setTimeout` | — | **A** |
| `realtime-ui.js` | 55 | Two tiny helpers: `animateValueChange` (flash-highlight a text node on change) and `setLiveIndicatorState`. | Admin/Performance `SystemHealth`, `Index`, `HealthMetrics`, `Commands` | any `#id` passed in | — | — | `setTimeout` | — | **A** |
| `dashboard-hub.js` | 512 | `DashboardHub` singleton: wraps one `signalR.HubConnectionBuilder` to `/hubs/dashboard` with exponential-backoff auto-reconnect, a generic pub/sub (`on/off/invoke`), and group join/leave + "get current X" helpers for guild, performance, alerts, system-health, and **guild-audio** groups. Every other real-time module in the app is built on top of this one connection. | `_Layout.cshtml`, `Index.cshtml`, Admin/Performance `SystemHealth/HealthMetrics/Alerts/Commands`, Portal `TTS/Soundboard/VOX` | — | `/hubs/dashboard` | see §2 | — | — | **D** — see §2 note |
| `dashboard-realtime.js` | 382 | Consumes `DashboardHub` events to update the home-dashboard connection badge, live activity feed (template clone + prepend, max 15 items, pause/resume), and stat tiles. | `Index.cshtml`, `_Navbar.cshtml` | `#connection-status`, `#activity-feed`, `#activity-item-template`, `#pause-feed-btn`, `[data-bot-status-card]`, `[data-connection-state]`, `[data-latency]`, `[data-uptime]`, `[data-guild-count]` | — | `BotStatusUpdated`, `CommandExecuted`, `GuildActivity`, `StatsUpdated` (+ hub `reconnecting/reconnected/disconnected/connectionFailed`) | — | — | **A** |
| `notification-bell.js` | 714 | Navbar bell dropdown: badge count, list w/ mark-read/dismiss/mark-all, keyboard nav (roving tabindex), live announcer. Fetches initial summary via hub `invoke`, updates from hub push events. | `_Layout.cshtml` | `#notificationBellButton`, `#notificationBadge`, `#notificationDropdown`, `#notificationList`, `#notificationAnnouncer`, `[data-notification-id]`, `[data-action]` | hub `invoke`: `GetNotificationSummary`, `GetNotifications`, `MarkNotificationRead`, `MarkAllNotificationsRead`, `DismissNotification` | `OnNotificationReceived`, `OnNotificationCountChanged`, `OnNotificationMarkedRead`, `OnAllNotificationsRead` | `setTimeout` | — | **A** |
| `search.js` | 463 | Global `Cmd/Ctrl+K` shortcut to focus navbar search, recent-searches list persisted in `localStorage`, mobile full-screen search overlay. | `_Layout.cshtml` | `#navbar-search`, `#recent-searches-dropdown`, `#mobileSearchOverlay`, `#mobile-search-input`, `#mobile-recent-searches` | — | — | `localStorage` (`recentSearches`), keydown shortcuts | — | **B** |
| `preview-popup.js` | 914 | Hover/click/focus-triggered user & guild "card" popups: viewport-aware positioning, in-memory response cache, idle-time prefetch via `[data-preview-type]:not([data-prefetched])`, `MutationObserver` for dynamically-added trigger elements, touch support. | `_Layout.cshtml` | `[data-preview-type]`, `[data-user-id]`, `[data-guild-id]`, `[data-context-guild-id]`, `.preview-trigger` | `GET` user/guild preview endpoints (`API.userPreview`, `API.guildPreview` — built from a base path) | — | `MutationObserver`, `setInterval`/`setTimeout`, scroll/resize repositioning | — | **B** |
| `quick-actions.js` | 638 | `window.quickActions`: confirmation modal (focus trap, focus-return), typed-confirmation modal, prompt modal, generic AJAX form submit w/ button spinner swap, toast reporting. | `_Layout.cshtml` | `[data-modal-backdrop]`, `[data-modal-cancel/confirm/ok/input]`, `[role="alertdialog"]` | submits arbitrary forms via `fetch` | — | — | — | **A** |
| `theme.js` | 207 | `ThemeManager`: reads/writes `data-theme` attribute, persists to both a `theme-preference` cookie (for SSR) and `localStorage`, syncs across tabs via `storage` event, POSTs the choice server-side. | `_Layout.cshtml`, `_PortalLayout.cshtml` (blocking inline script) | `data-theme` on `<html>` | `POST /api/theme/preference` | — | `localStorage`, cookies, `storage` event | — | **B** |
| `timezone.js` | 181 | `window.timezoneUtils`: detects IANA tz via `Intl.DateTimeFormat().resolvedOptions()`, auto-fills hidden `UserTimezone` inputs, converts `[data-utc]` elements to local time on load. | `_Layout.cshtml`, `Guilds/ScheduledMessages/Create.cshtml.cs` (server references the convention) | `input[name$="UserTimezone"]`, `[data-utc]`, `.timezone-indicator` | — | — | `Intl` | — | **B** |
| `bot-status-refresh.js` | 281 | Polls `/api/bot/status` every 30s (5s retry after failure) to refresh the bot-status card/banner (latency, uptime, guild count, online/offline styling). | `_Layout.cshtml` | `[data-bot-status-card]`, `[data-bot-status-banner]`, `[data-connection-state]`, `[data-latency]`, `[data-uptime]`, `[data-guild-count]`, `[data-status-badge/dot/heading/text]` | `GET /api/bot/status` | — | `setInterval`/`setTimeout` | — | **A** — *note: duplicates data `DashboardHub`'s `BotStatusUpdated` push already provides; candidate for deletion even pre-Blazor.* |
| `navigation.js` | 336 | Sidebar open/close + collapse (persisted to `localStorage`), mobile overlay, user-menu & per-server action dropdowns (click-outside + Escape to close), "copy" buttons, viewport breakpoint tracking. | `_Layout.cshtml` | `#sidebar`, `#mobileOverlay`, `#sidebarToggle`, `#sidebarCollapseIcon`, `#userMenu`, `#userMenuButton`, `.server-action-dropdown`, `.copy-text` | — | — | `localStorage` (`sidebarCollapsed`), `navigator.clipboard`, `resize`, `keydown` | — | **B** |
| `guild-nav.js` | 33 | Mobile dropdown toggle for the guild sub-nav (click outside / Escape to close). | `_GuildLayout.cshtml` | `#guildNavDropdownToggle`, `#guildNavDropdownMenu` | — | — | — | — | **A** |
| `tab-panel.js` | 740 | Generic accessible tab-panel component: 3 navigation modes (page-nav links, in-page show/hide, AJAX fetch), URL-hash + `localStorage` persistence options, roving-tabindex keyboard nav, auto-scroll-into-view, re-executes `<script>` tags in AJAX-loaded content. | `_GuildLayout.cshtml`, `Admin/Performance/Index.cshtml`, `Admin/Logs/Index.cshtml`, `Commands/Index.cshtml`, `_TabPanel.cshtml` | `[data-tab-panel]`, `data-panel-id`, `data-navigation-mode`, `data-persistence-mode`, `data-ajax-url-pattern`, `role="tab"/"tabpanel"` | AJAX mode fetches `data-ajax-url-pattern` | — | `history.replaceState`, `hashchange`, `scroll`/`resize`, `localStorage` | — | **B** — AJAX-fetch mode is **D**; keyboard/scroll/hash-persistence core is **B** |
| `keyboard-shortcuts.js` (`shared/`) | 259 | `KeyboardShortcuts` registry + a `?`-triggered help overlay listing all registered shortcuts by category. Ignores keys while focused in inputs/textareas. | `_PortalLayout.cshtml` | injects a `.kbd-help-overlay` w/ `#kbdShortcutList` | — | — | `keydown` | — | **B** |
| `user-preferences.js` | 217 | `UserPreferences`: localStorage-first cache (`pref:{guildId}:{key}`) with debounced (500 ms) background sync to the server; `get/set/delete/sync/init`. | `_PortalLayout.cshtml` | — | `GET/POST/DELETE /api/portal/preferences/{guildId}/...` | — | `localStorage` | — | **B** |
| `filter-panel.js` (`shared/`) | 79 | Generic expand/collapse for a filter panel (`toggleFilterPanel`) + a "clear date filters" helper. | `Guilds/Analytics/Index.cshtml`, `Guilds/RatWatch/Analytics.cshtml` | `#filterContent`, `#filterChevron`, `#filterToggle`, `#StartDate`/`#EndDate`, `#filterForm` | — | — | — | — | **A** |

### Commands page cluster (`Commands/Index.cshtml`)

| File | Lines | Purpose | Loaded by | DOM contract | API / endpoints | SignalR | Browser APIs | 3rd-party | Class |
|---|---:|---|---|---|---|---|---|---|---|
| `command-tabs.js` | 122 | Updates page subtitle/breadcrumb text on tab change (`tabchange` custom event). | `Commands/Index.cshtml` | `[data-panel-id="commandTabs"]`, `[data-command-subtitle]`, `[data-command-breadcrumb-active]` | — | — | — | — | **A** |
| `command-tab-loader.js` | 412 | Lazy-loads each Commands tab's HTML via AJAX (`/api/commands/{list|logs|analytics}`), caches loaded tabs, re-executes embedded `<script>` tags, coordinates with filters/pagination. | `Commands/Index.cshtml` | `[data-tab-content]`, `data-tab-id`, `dataset.loaded` | `GET /api/commands/list|logs|analytics` | — | — | — | **D** |
| `command-filters.js` | 472 | Debounced filter form (search input, date-preset buttons, selects) → triggers reload via tab loader; syncs with `UrlState`. | `Commands/Index.cshtml` | `[data-filter-form]`, `[data-action="clear-filters"]`, `[data-date-preset]` | — | — | — | `setTimeout` (debounce) | — | **A** |
| `command-pagination.js` | 355 | Pagination link/button clicks → AJAX page change, coordinates with tab loader & filters. | `Commands/Index.cshtml` | `.pagination [data-page]`, `[aria-current="page"]` | — | — | — | — | **D** |
| `date-range-filter.js` | 414 | Filter-panel expand/collapse (remembered in `localStorage`), quick date-range presets (`onclick="setPreset(...)"`), active-filter badge counts, syncs `#StartDate`/`#EndDate` inputs. | `Commands/Index.cshtml` | `[data-filter-panel]`, `[data-has-active-filters]`, `[name="StartDate"/"EndDate"]`, `[name="ActiveTab"]` | — | — | — | `localStorage` (`commandsPage-filterPanel-expanded`) | — | **B** |
| `command-log-modal.js` | 261 | Opens a modal and AJAX-loads command-log detail HTML into it; re-executes embedded `<script>` tags; Escape/focus handling. | `Commands/Index.cshtml`, `_CommandLogDetailsModal.cshtml` | `#commandLogDetailsModal`, `#commandLogModalContent` | fetches modal partial content | — | — | — | **A** (modal shell) / **D** (AJAX partial load) |
| `autocomplete.js` | 639 | Reusable `AutocompleteManager.init({inputId, hiddenInputId, endpoint, guildIdSource, minChars, debounceMs})` — debounced fetch, keyboard nav, ARIA live region, `mousedown`/`mouseover` selection. | `Admin/Logs/Index.cshtml`, `Commands/Index.cshtml`, `message-logs.js` (guild/channel pickers) | `.autocomplete-item`, `#autocomplete-live-region` | `GET /api/autocomplete/users` (endpoint is parameterized per usage) | — | — | `setTimeout` (debounce) | — | **B** |
| `command-stats-chart.js` | 321 | Chart.js horizontal-bar chart of top commands by usage on the home dashboard "Command Stats" card, with a time-range `<select>` that re-fetches. | `Index.cshtml` (via `_CommandStatsCard.cshtml`) | `#commandUsageChart`, `[data-command-stats-card]`, `[data-time-range-selector]`, `[data-total-count]` | `GET /api/commandlogs/stats` | — | — | — | **Chart.js** | **C** |
| `ajax-sort.js` | 165 | Generic "swap container innerHTML on sort-dropdown change" — fetches a raw HTML fragment and does `history.pushState`. | `Guilds/Soundboard/Index.cshtml` | listens for `sortchange` custom event | fetches page URL w/ sort query param, expects HTML back | — | `history.pushState`, `popstate` | — | **D** |

### Orphaned / dead code (no `<script src>` reference found anywhere in `Pages/`)

These five analytics-chart modules and three shell/loader modules are **not loaded by any page** — each has been superseded by an inline `<script>` block duplicating the same Chart.js logic directly inside the corresponding `.cshtml` (confirmed: `Guilds/Analytics/Index.cshtml`, `Engagement.cshtml`, `Moderation.cshtml`, and `Commands/Tabs/_AnalyticsTab.cshtml` each contain their own `new Chart(...)` calls), or by `performance/dashboard.js`. They should simply be **deleted**, not ported.

| File | Lines | Would-be purpose | Why orphaned | Class |
|---|---:|---|---|---|
| `server-analytics.js` | 423 | Chart.js: activity-over-time line + top-channels bar + heatmap, for guild Analytics. | Superseded by inline script in `Guilds/Analytics/Index.cshtml` | **D** |
| `engagement-analytics.js` | 430 | Chart.js: message-trends line + channel-engagement bar + retention funnel. | Superseded by inline script in `Guilds/Analytics/Engagement.cshtml` | **D** |
| `moderation-analytics.js` | 409 | Chart.js: moderation-trends area + case-distribution doughnut + moderator-workload bar. | Superseded by inline script in `Guilds/Analytics/Moderation.cshtml` | **D** |
| `command-analytics.js` | 512 | Chart.js: usage-over-time, top-commands, success-rate, response-time. | Superseded by inline script in `Commands/Tabs/_AnalyticsTab.cshtml` | **D** |
| `api-metrics-chart.js` | 208 | Chart.js API-latency chart + 30s poll for the old `ApiMetrics.cshtml`. | `ApiMetrics.cshtml` has its own inline chart+poll script instead | **D** |
| `command-error-handler.js` | 439 | `CommandErrorHandler` — richer AJAX error classification/retry for command pages. | Never wired in; `command-tab-loader.js`/`command-filters.js` handle errors inline | **D** |
| `command-loading-states.js` | 353 | `CommandLoadingStates` — `LoadingManager` wrapper w/ command-page defaults. | Never wired in | **D** |
| `performance-shell.js` | 450 | Tab switching + URL-hash routing + time-range persistence shell for Performance dashboard ("issue #721"). | Superseded by `performance/dashboard.js` + `tab-panel.js` | **D** |
| `performance-tabs.js` (top-level) | 1060 | Near-identical earlier version of `performance/dashboard.js` (same class body; different endpoint scheme: `/api/performance/tabs/*` vs `?handler=Partial&tabId=`). | Referenced only in **comments** inside `Index.cshtml`/`_ApiMetricsTab.cshtml`, never in a real `<script src>` | **D** |

### Performance dashboard (`Admin/Performance/*`)

The app has **two generations** of this dashboard: five legacy single-tab full pages (`SystemHealth.cshtml`, `HealthMetrics.cshtml`, `Alerts.cshtml`, `Commands.cshtml`, `ApiMetrics.cshtml`, each with its own inline Chart.js init + a dedicated `*-realtime.js`), and the newer unified `Index.cshtml` (tab shell + AJAX-loaded tab partials under `performance/tabs/*.js`).

| File | Lines | Purpose | Loaded by | DOM contract | API / endpoints | SignalR | Browser APIs | 3rd-party | Class |
|---|---:|---|---|---|---|---|---|---|---|
| `performance/dashboard.js` | 1062 | Unified tab shell for `Index.cshtml`: fetches tab HTML from `?handler=Partial&tabId=...`, caches per-tab, time-range button group, `history.pushState` per tab, destroys/recreates `canvas` charts on tab switch, re-executes embedded `<script>`s, converts `[data-utc-time]`. | `Index.cshtml` | `#performanceShellTabs`/`#tabContent`, `.time-range-btn[data-hours]`, `[data-tab-link]`, `[data-performance-tabs]` | `GET /Admin/Performance?handler=Partial&tabId={overview\|health\|commands\|api\|system\|alerts}` | — | `history.pushState/replaceState`, `popstate` | — | **D** (orchestration) — see per-tab modules below for the **C** chart pieces it hosts |
| `performance/time-range.js` | 88 | Centralized 24h/7d/30d time-range state, persisted to `localStorage`, dispatches `timeRangeChanged` custom event. | `Index.cshtml`, `Alerts.cshtml` | — | — | — | `localStorage` (`performance-dashboard-time-range`) | — | **B** |
| `performance/components/chart-utils.js` | 204 | Shared Chart.js dark-theme default options + helper builders (`window.Performance.ChartUtils`). | `Index.cshtml`, `Alerts.cshtml` | — | — | — | — | **Chart.js** | **C** |
| `performance/components/timestamp-utils.js` | 87 | UTC → local-time conversion for `[data-utc-time]` elements (`window.Performance.TimestampUtils`) — duplicates `timezone.js`'s conversion logic in a scoped form. | `Index.cshtml`, `Alerts.cshtml` | `[data-utc-time]`, `data-format` | — | — | `Intl`/`Date` | — | **B** |
| `performance/tabs/overview.js` | 147 | Overview tab: response-time line chart + throughput chart. | `Index.cshtml` (AJAX) | `#overviewResponseTimeChart`, `#overviewThroughputChart` | `GET /api/metrics/commands/performance?hours=`, `GET /api/metrics/commands/throughput?hours=&granularity=` | — | — | **Chart.js** | **C** |
| `performance/tabs/health.js` | 183 | Health tab: latency/memory/CPU gauges + latency-history line chart + sparkline. | `Index.cshtml` (AJAX), `_HealthTab.cshtml` | `#healthLatencyGauge`, `#healthMemoryGauge`, `#healthCpuGauge`, `#healthLatencyHistoryChart`, `#healthLatencySparkline` | `GET /api/metrics/health/latency?hours=` | — | — | **Chart.js** | **C** |
| `performance/tabs/commands.js` | 259 | Commands tab: response-time, throughput, error-rate charts. | `Index.cshtml` (AJAX) | `#commandsResponseTimeChart`, `#commandsThroughputChart`, `#commandsErrorRateChart` | `GET /api/metrics/commands/throughput?...`, `GET /api/metrics/commands/performance?hours=` | — | — | **Chart.js** | **C** |
| `performance/tabs/api.js` | 152 | API tab: Discord API-latency chart. | `Index.cshtml` (AJAX), `_ApiTab.cshtml` | `#apiLatencyChart`, `#apiLatencySubtitle` | `GET` API-latency endpoint (`fetch(url)`, URL built from constant) | — | — | **Chart.js** | **C** |
| `performance/tabs/system.js` | 246 | System tab: DB query-time + memory charts w/ retry buttons. | `Index.cshtml` (AJAX), `_SystemTab.cshtml` | `#systemQueryTimeChart`, `#systemMemoryChart`, `#systemQueryTimeRetry`, `#systemMemoryRetry` | `GET /api/metrics/system/history/database?hours=`, `GET /api/metrics/system/history/memory?hours=` | — | — | **Chart.js** | **C** |
| `performance/tabs/alerts.js` | 424 | Alerts tab: alert-frequency chart, per-metric threshold config form (editable table), acknowledge/acknowledge-all incident actions. | `Index.cshtml` (AJAX), `_AlertsTab.cshtml` | `#alertsFrequencyChart`, `.alerts-threshold-input`, `.alerts-enabled-toggle`, `[data-incident-id]` | `GET /api/alerts/config/{metric}`, `POST /api/alerts/incidents/acknowledge-all` (+ per-incident acknowledge) | — | — | **Chart.js** | **C** (chart) / **A** (config form + incident actions) |
| `performance/health-metrics-realtime.js` | 343 | Legacy `HealthMetrics.cshtml` real-time updates: latency/CPU/memory text + **pushes into a Chart.js sparkline** on every hub event; 30s poll fallback. | `HealthMetrics.cshtml` | `#currentLatency`, `#memoryUsage`, `#cpuUsage`, `#liveIndicator` | `GET /api/metrics/health` (fallback poll) | `HealthMetricsUpdate` | `setInterval`/`setTimeout` | **Chart.js** | **C** |
| `performance/commands-realtime.js` | 402 | Legacy `Commands.cshtml` real-time updates: **mutates 2 live Chart.js instances** (response-time, throughput) with a sliding 100-point window; 30s poll fallback. | `Commands.cshtml` | `#lastRefreshTime`, `#commandsLiveIndicator` | hub `invoke('GetCurrentCommandPerformance')` (fallback) | `CommandPerformanceUpdate` | `setInterval`/`setTimeout` | **Chart.js** | **C** |
| `performance/system-health-realtime.js` | 461 | Legacy `SystemHealth.cshtml`: service status dots/heartbeats, cache hit-rate bars, slow-query count — pure DOM text/class updates, no chart. | `SystemHealth.cshtml` | `[data-service-name]`, `[data-cache-name]`, `.service-status-dot`, `.cache-hit-rate`, `.progress-bar-fill` | `GET /api/metrics/system` (fallback poll) | `SystemMetricsUpdate` | `setInterval`/`setTimeout` | — | **A** |
| `performance/alerts-realtime.js` | 695 | Legacy `Alerts.cshtml`: live incident list (insert/update/remove rows), timeline entries, acknowledge/acknowledge-all, 30s poll fallback. | `Alerts.cshtml` | `[data-incident-id]`, `.severity-badge`, `.timeline`, `#alertsLiveIndicator`, `#acknowledgeAllBtn` | `GET /api/alerts/active` (fallback), `POST /api/alerts/incidents/acknowledge-all` | `OnAlertTriggered`, `OnAlertResolved`, `OnAlertAcknowledged`, `OnActiveAlertCountChanged` | `setInterval`/`setTimeout` | — | **A** |

### Portal (public/member-facing audio pages) & voice widgets

| File | Lines | Purpose | Loaded by | DOM contract | API / endpoints | SignalR | Browser APIs | 3rd-party | Class |
|---|---:|---|---|---|---|---|---|---|---|
| `portal-soundboard-inline.js` | 17 | Reads `window.portalSoundboardInlineConfig` (server-rendered) and re-exposes `guildId`/`currentUserId` as bare `window` globals for `portal-soundboard.js`. | `Portal/Soundboard/Index.cshtml` | — | — | — | — | — | **D** |
| `portal-soundboard.js` | 1374 | Full soundboard grid: search/sort/favorite, upload via drag-drop **and** `XMLHttpRequest` with progress bar, delete, browser preview playback (`new Audio(...)`), fullscreen mode w/ auto-hiding toolbar, `IntersectionObserver` for lazy reveal, keyboard shortcuts, live sync via `DashboardHub` guild-audio group. | `Portal/Soundboard/Index.cshtml` | `#dropzone`, `#fileInput`, `#uploadForm`, `#uploadProgress`/`#progressBar`, `.sound-card[data-sound-id]`, `#soundGrid`, `#searchInput`, `#sortSelect`, `#fullscreenToggle`, `#voice-channel-panel` | `GET /api/portal/soundboard/{g}/favorites`, `POST/DELETE .../favorites/{id}`, `POST .../play/{id}`, `DELETE .../sounds/{id}`, `POST .../sounds` (upload, XHR), `GET .../sounds/{id}/audio` (preview) | joins **guild-audio group**; listens `PlaybackStarted/Finished/Progress`, `AudioConnected/Disconnected`, `VoiceChannelMemberCountUpdated`, `SoundUploaded/Deleted`, `QueueUpdated`, `BotStatusUpdated` | `IntersectionObserver`, `new Audio`, drag/drop events, `XMLHttpRequest` (upload progress), `localStorage` (fullscreen + sort prefs), `setTimeout` | — | **C** (drag-drop upload + audio preview + fullscreen dominate; grid search/sort/favorite state is **A**, SignalR sync is **A**) |
| `portal-tts-inline.js` | 64 | Bootstraps `window.guildId`, mobile-collapsible voice-settings panel toggle. | `Portal/TTS/Index.cshtml` | `.voice-controls-collapsible-header`, `#voiceSettingsContent` | — | — | — | — | **D** (bootstrap) / **A** (toggle) |
| `portal-tts.js` | 1147 | TTS composer: character counter, speed/pitch sliders, **browser preview playback** (`new Audio`), draft auto-save, style/preset selection, `MutationObserver` watching the `voice-channel-panel`'s `data-connected` attribute to gate the Send button, history panel (replay/favorite/delete), SSML helpers via `SsmlMarkers`. | `Portal/TTS/Index.cshtml` | `#ttsMessage`, `#speedSlider`/`#pitchSlider`, `#sendBtn`/`#previewBtn`, `#voice-channel-panel[data-connected]`, `#ttsHistoryPanel` | `POST /api/portal/tts/{g}/send`, `POST .../preview`, `GET .../voices/{name}/capabilities`, `POST .../validate-ssml`, `POST .../build-ssml`, `GET/POST .../presets/custom`, `GET .../history`, `POST .../history/{id}/replay`, `POST .../history/{id}/favorite`, `DELETE .../history/{id}` | (relies on `voice-channel-panel.js` for connection state, not directly on the hub) | `new Audio`, `MutationObserver`, `localStorage` (`tts_mode_preference`) | — | **B** (audio preview is the one **C** sliver) |
| `portal-vox.js` | 1169 | VOX clip composer: autocomplete word-by-word clip insertion, A-Z jump rail w/ `IntersectionObserver` section tracking, mobile letter picker, word-gap slider, history/favorites. **No browser audio preview at all** — every "play" is a pure API call that has the bot play the clip live in the Discord voice channel. | `Portal/VOX/Index.cshtml` | `.vox-clip-tile`, `.vox-autocomplete-item`, `.vox-az-letter[data-letter]`, `.vox-section-header` | `GET /api/portal/vox/{g}/clips?group=`, `POST .../play`, `GET .../history?limit=`, `GET .../favorites`, `POST .../history/{id}/favorite`, `DELETE .../history/{id}` | — | `IntersectionObserver`, `matchMedia`, `setTimeout` | — | **B** |
| `voice-channel-panel.js` | 764 | Shared "now playing / connected channel / queue" widget embedded on all 3 portal pages (and the guild-operator Soundboard/TTS pages): connect/leave/stop controls, live queue list, playback progress bar — entirely SignalR-driven, no local audio. | `Portal/TTS/Soundboard/VOX/Index.cshtml`, `Guilds/Soundboard/Index.cshtml`, `Guilds/TextToSpeech/Index.cshtml` | `#voice-channel-panel[data-connected][data-channel-id]`, `#channel-selector`, `#now-playing-*`, `#queue-list` | (channel connect/leave likely via a Razor handler or `ApiClient`, not seen in grep — invoke calls not captured) | joins **guild-audio group**; listens `AudioConnected/Disconnected`, `PlaybackStarted/Progress/Finished`, `QueueUpdated` | — | — | **A** |
| `voice-selector.js` | 373 | Custom searchable voice-picker combobox (replaces a plain `<select>`): grouped by locale, keyboard nav, live filter. | `Portal/TTS/Index.cshtml` | `.voice-selector__trigger/__dropdown/__search/__option[data-voice-value]` | — | — | — | — | **B** |
| `ssml-markers.js` | 83 | Pure regex-based parser/serializer for the Pro-TTS visual markup (`**bold**`, `*emph*`, `[# n #]`, `[⏸️ Nms]`) shared between `portal-tts.js` and the `_EmphasisToolbar.cshtml` inline script. No DOM access. | `Portal/TTS/Index.cshtml`, `_EmphasisToolbar.cshtml` | — | — | — | — | — | **B** |
| `tts-page.js` | 652 | **Guild-operator** TTS page (distinct from the portal page): AJAX message send/settings-update/delete against **Razor Page handlers** (`?handler=`), not the REST API; stats tiles, recent-messages list, delete-confirm modal, style/preset controls. | `Guilds/TextToSpeech/Index.cshtml` | `#messageInput`, `#charCounter`, `#recentMessagesList`, `#delete-modal`, `#voiceSelect`, `#styleSelector-select` | `POST ?handler=SendMessage/UpdateSettings/DeleteMessage&guildId=` (Razor Page handlers, `X-Requested-With: XMLHttpRequest`) | — | `localStorage` (`tts_mode_preference`) | — | **A** |

### Other pages

| File | Lines | Purpose | Loaded by | DOM contract | API / endpoints | SignalR | Browser APIs | 3rd-party | Class |
|---|---:|---|---|---|---|---|---|---|---|
| `login.js` | 117 | Password-visibility toggle, inline blur/focus validation styling. | `Account/Login.cshtml` | `.password-toggle`, `#login-form`, `#email`/`#password` | — | — | — | — | **A** |
| `guild-sync.js` | 175 | "Sync from Discord" button(s): spinner swap, `POST ?handler=SyncGuild/SyncAll`. | `Guilds/Index.cshtml`, `Guilds/Details.cshtml` | `.sync-icon/.sync-spinner/.sync-text` | `POST ?handler=SyncGuild&id=`, `POST ?handler=SyncAll` | — | `setTimeout` | — | **A** |
| `member-directory.js` | 496 | Filter panel, bulk checkbox selection + toolbar, CSV export trigger, member-detail modal (AJAX fetch), copy-user-id button. | `Guilds/Members/Index.cshtml` | `.member-checkbox`, `#bulkActionsToolbar`, `#memberDetailModal`, `#roleMultiSelectDropdown` | `GET /api/guilds/{g}/members/{userId}` | — | `navigator.clipboard` | — | **B** |
| `message-logs.js` | 100 | Wires `AutocompleteManager` to the channel filter, clears channel when guild changes. | `Admin/Logs/Index.cshtml` | `#ChannelId-search`, `#GuildId-search` | (delegates to `autocomplete.js`) | — | — | — | **A** |
| `moderation-settings.js` | 462 | Simple/advanced mode toggle, tabbed settings sections, dirty-tracking with `beforeunload` guard, tag chip add/remove, preset radio buttons. | `Guilds/ModerationSettings/Index.cshtml` | `.settings-tab`, `.mode-toggle-btn`, `#tags-list`, `#moderationForm` | — | — | `history.replaceState`, `beforeunload` | — | **B** (dirty-guard needs `beforeunload` interop) |
| `settings.js` | 1036 | Site-wide admin settings: tabbed sections, per-field dirty tracking + `beforeunload`, bot-control panel that **polls `/api/bot/status` every 5s**, toggle switches, reset-category/reset-all confirmation modals, theme select. | `Admin/Settings.cshtml`, `Guilds/VOX/Index.cshtml`, `Guilds/ModerationSettings/Index.cshtml`, `Components.cshtml` | `.settings-tab`, `[data-bot-control-status]`, `#settingsForm`, `#resetCategoryModal`, `#resetAllModal`, `#SelectedThemeId` | `GET /api/bot/status` (5s poll) | — | `setInterval`, `beforeunload` | — | **B** (poll → Blazor push; `beforeunload` stays interop) |
| `notification-history.js` | 295 | Bulk select/mark-read/delete + individual row actions on the notification history page. | `Admin/Notifications/Index.cshtml` | `.notification-checkbox`, `#bulkActions`, `[data-action="toggle-read"/"delete"]` | `POST /api/notifications/mark-read`, `/delete`, `/mark-all-read`, `/delete-all` | — | — | — | **A** |
| `user-moderation-profile.js` | 340 | Tab switching, tag add/remove dropdown, note add/delete on a member's moderation profile. | `Guilds/Members/Moderation.cshtml` | `.settings-tab`, `#tagDropdown`, `#userTagsContainer`, `[data-note-id]` | `POST/DELETE /api/guilds/{g}/users/{u}/tags/{tag}` | — | — | — | **A** |
| `rat-watch-analytics.js` | 567 | Chart.js: watches-over-time line, outcome-distribution doughnut, top-users bar, **activity heatmap** (custom canvas/DOM grid). | `Guilds/RatWatch/Analytics.cshtml`, `Admin/RatWatchAnalytics.cshtml` | `#watchesOverTimeChart`, `#outcomeDistributionChart`, `#topUsersChart`, `#activityHeatmap`, JSON payload in `#ratWatchChartData` | — (data pre-embedded in page via JSON `<script>` tag) | — | — | **Chart.js** | **C** |

### Node test infrastructure

| File | Lines | Purpose |
|---|---:|---|
| `__tests__/api-client.test.js` | 238 | `node:test` + `node:assert/strict` unit tests for `api-client.js` (mocks `global.fetch`); covers `get/post/put/del`, antiforgery token header injection, error-body parsing, 429 `Retry-After` handling. Run via `npm test` → `"test": "node --test wwwroot/js/__tests__/*.test.js"` in `src/DiscordBot.Bot/package.json`. **This is the only JS test file in the repo** — none of the other 71 files (including the two 1000+ line portal modules) have unit tests; browser-DOM behavior is untested outside manual/E2E. |

---

## 2. SignalR

**Single hub**: `/hubs/dashboard` (`DashboardHub`), client library `@microsoft/signalr@8.0.0` from jsDelivr CDN (also cdnjs mirror on `Index.cshtml`). One connection is opened per page load by `dashboard-hub.js` and auto-started from `_Layout.cshtml`'s inline `DOMContentLoaded` handler (`DashboardHub.connect()` — idempotent, safe to call from every page). Reconnect: exponential backoff `1000 * 2^n` capped at 16s, stops after 5 attempts (`withAutomaticReconnect`).

**Groups** (join/leave via hub `invoke`, all guild-scoped except performance/alerts/system-health which are global):
- `JoinGuildGroup(guildId)` / `LeaveGuildGroup` — per-guild dashboard activity
- `JoinPerformanceGroup()` / `LeavePerformanceGroup()`
- `JoinAlertsGroup()` / `LeaveAlertsGroup()`
- `JoinSystemHealthGroup()` / `LeaveSystemHealthGroup()`
- `JoinGuildAudioGroup(guildId)` / `LeaveGuildAudioGroup(guildId)` — voice/soundboard/TTS/VOX playback state

**Server → client events consumed**, by module:

| Event | Consumer(s) |
|---|---|
| `BotStatusUpdated` | `dashboard-realtime.js`, `portal-soundboard.js` |
| `CommandExecuted`, `GuildActivity`, `StatsUpdated` | `dashboard-realtime.js` |
| `OnNotificationReceived`, `OnNotificationCountChanged`, `OnNotificationMarkedRead`, `OnAllNotificationsRead` | `notification-bell.js` |
| `OnAlertTriggered`, `OnAlertResolved`, `OnAlertAcknowledged`, `OnActiveAlertCountChanged` | `performance/alerts-realtime.js` |
| `CommandPerformanceUpdate` | `performance/commands-realtime.js` |
| `HealthMetricsUpdate` | `performance/health-metrics-realtime.js` |
| `SystemMetricsUpdate` | `performance/system-health-realtime.js` |
| `AudioConnected`, `AudioDisconnected`, `PlaybackStarted`, `PlaybackProgress`, `PlaybackFinished`, `QueueUpdated` | `voice-channel-panel.js`, `portal-soundboard.js` |
| `VoiceChannelMemberCountUpdated`, `SoundUploaded`, `SoundDeleted` | `portal-soundboard.js` only |
| (connection-state) `reconnecting`, `reconnected`, `disconnected`, `connectionFailed` | all of the above (local `DashboardHub` events, not server pushes) |

**Client → server `invoke` calls**: `GetCurrentStatus`, `GetCurrentPerformanceMetrics`, `GetActiveAlertCount`, `GetCurrentSystemHealth`, `GetCurrentAudioStatus(guildId)`, `GetCurrentCommandPerformance`, `GetNotificationSummary`, `GetNotifications(count)`, `MarkNotificationRead(id)`, `MarkAllNotificationsRead`, `DismissNotification(id)`.

Every legacy single-tab performance page (`HealthMetrics`, `Commands`, `SystemHealth`, `Alerts`) falls back to a **30s poll** of a REST endpoint if the hub push hasn't produced fresh data — the poll interval and endpoint are hard-coded per module (`/api/metrics/health`, `/api/metrics/system`, `/api/alerts/active`, hub `invoke('GetCurrentCommandPerformance')`).

**Blazor implication**: `dashboard-hub.js` (D) disappears entirely — a Blazor Server component already has a persistent circuit and can subscribe to the same server-side events directly in C# (either by having `DashboardHub`'s broadcast logic also push into a shared in-process pub/sub the Blazor components observe, or by having Razor components hold their own lightweight `HubConnection` via the .NET SignalR client if a decoupled hub is still wanted). Either way, none of the 15 SignalR event names above need to survive as JS — they become C# event handlers calling `StateHasChanged()`. The one thing that *would* need to stay JS-side is nothing here — this whole cluster is a clean **D**.

## 3. Charts

**Library**: Chart.js **4.4.1**, loaded from `https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js` — pinned per-page (not from a shared layout), duplicated across 13 `.cshtml` files: `Index.cshtml` (home dashboard), `Guilds/Analytics/Index.cshtml`, `Engagement.cshtml`, `Moderation.cshtml`, `Guilds/RatWatch/Analytics.cshtml`, `Admin/RatWatchAnalytics.cshtml`, `Admin/Performance/{Index,SystemHealth,HealthMetrics,Alerts,Commands,ApiMetrics}.cshtml`, `Commands/Index.cshtml`. No local `wwwroot/lib` copy exists (no `wwwroot/lib` directory at all — everything else is served from CDN or the two npm dev-dependencies for the Tailwind build).

**Charts by page** (canvas id → chart type, from `<canvas>` markup + init code):

| Page | Canvases | Datasets driving them |
|---|---|---|
| Home `Index.cshtml` | `#commandUsageChart` (h-bar) | `GET /api/commandlogs/stats` via `command-stats-chart.js` |
| `Guilds/Analytics/Index.cshtml` | `#activityOverTimeChart` (line), `#topChannelsChart` (h-bar) | server-embedded JSON (`#serverAnalyticsChartData`) + **inline script** (not `server-analytics.js`, which is dead) |
| `Guilds/Analytics/Engagement.cshtml` | `#messageTrendsChart` (line), `#channelEngagementChart` (h-bar) | inline script (dead `engagement-analytics.js` superseded) |
| `Guilds/Analytics/Moderation.cshtml` | `#moderationTrendsChart` (stacked area), `#caseDistributionChart` (doughnut), `#moderatorWorkloadChart` (h-bar) | inline script (dead `moderation-analytics.js` superseded) |
| `Guilds/RatWatch/Analytics.cshtml`, `Admin/RatWatchAnalytics.cshtml` | `#watchesOverTimeChart` (line), `#outcomeDistributionChart` (doughnut), `#topUsersChart` (h-bar), `#activityHeatmap` (custom grid) | `rat-watch-analytics.js` — **the only analytics-chart module that's actually live** |
| `Commands/Tabs/_AnalyticsTab.cshtml` | `#usageOverTimeChart` (line), `#topCommandsChart` (h-bar), `#successRateChart` (doughnut), `#responseTimeChart` (h-bar) | inline script (dead `command-analytics.js` superseded) |
| `Admin/Performance/Index.cshtml` (unified, per tab) | overview: `#overviewResponseTimeChart`, `#overviewThroughputChart`; health: `#healthLatencyHistoryChart` + 3 gauges + sparkline; commands: `#commandsResponseTimeChart/ThroughputChart/ErrorRateChart`; api: `#apiLatencyChart`; system: `#systemQueryTimeChart/MemoryChart`; alerts: `#alertsFrequencyChart` | `performance/tabs/*.js` + `chart-utils.js` shared theme |
| `Admin/Performance/{SystemHealth,HealthMetrics,Alerts,Commands,ApiMetrics}.cshtml` (legacy) | `#latencyHistoryChart`, `#apiLatencyChart`, `#alertsFrequencyChart`, response-time/throughput/error-rate charts | inline init + `*-realtime.js` mutating the live instance |

**Blazor chart-interop surface**: a single JS interop module wrapping Chart.js `new Chart(ctx, config)` / `.update()` / `.destroy()` is enough to cover every case above — pass a serialized config object + dataset arrays from C#, get back an opaque chart handle, call `updateData(handle, newDatasets)` on live-push. `performance/components/chart-utils.js`'s shared dark-theme defaults should become the interop module's baked-in defaults so every Blazor chart component looks consistent for free.

## 4. Audio — soundboard / TTS / VOX

Three distinct browser-audio patterns exist, and they are **not** the same:

- **Soundboard** (`portal-soundboard.js`): clicking a sound card's preview button plays a **local browser preview** via a lazily-created `new Audio(API.audio(guildId, soundId))` pointed at `GET /api/portal/soundboard/{g}/sounds/{id}/audio` (only one preview element is kept, reused/stopped on next click). Clicking "Play" (not preview) instead calls `POST /api/portal/soundboard/{g}/play/{id}` — a pure API call with **no local audio** — which tells the bot to play the sound in the connected Discord voice channel; the resulting playback state (`PlaybackStarted/Progress/Finished`) comes back over SignalR and is rendered by `voice-channel-panel.js`. Upload uses `XMLHttpRequest` directly (not `fetch`) specifically to get `upload.onprogress` for the progress bar, plus a client-side duration check by loading the file into a throwaway `new Audio()` and reading `.duration` before allowing upload. Drag-and-drop (`dragover`/`dragleave`/`drop`) is native, no library.
- **TTS** (`portal-tts.js`): the "Preview" button fetches synthesized audio (`POST /api/portal/tts/{g}/preview`, blob response) and plays it locally via `new Audio(url)` so the user can hear it before deciding to send. "Send" (`POST /api/portal/tts/{g}/send`) is the pure API call that makes the bot actually speak in the Discord channel — no local audio for that path. A `MutationObserver` on `#voice-channel-panel`'s `data-connected` attribute is used purely to gate the Send button's enabled state (a slightly indirect way to observe another module's SignalR-driven DOM update — under Blazor this becomes a direct component-parameter/cascading-value subscription, no `MutationObserver` needed).
- **VOX** (`portal-vox.js`): **no local audio playback at all.** Every clip "play" (`POST /api/portal/vox/{g}/play`) is a server call; there is nothing to preview client-side (VOX clips are short canned phrases, not synthesized text) — its complexity is entirely in the composer UI (autocomplete-driven clip insertion, A-Z rail navigation) and the SignalR-driven now-playing panel it shares with the other two pages.
- **`voice-channel-panel.js`** is the shared "connected channel / now playing / queue" widget embedded on all three portal pages plus the guild-operator Soundboard and TTS pages. It has zero direct audio handling — it only renders SignalR push data (`AudioConnected/Disconnected`, `PlaybackStarted/Progress/Finished`, `QueueUpdated`) and issues connect/leave/stop control actions.
- **`voice-selector.js`** and **`ssml-markers.js`** are pure UI/text-formatting helpers for the TTS composer (voice combobox; visual-markup parsing for Pro TTS mode) — no audio.
- **`tts-page.js`** (guild-operator page, separate from the portal) has no browser audio either — it posts to Razor Page handlers and the bot plays server-side.

**Blazor interop surface for audio**: one small JS module exposing `playPreview(url)` / `stopPreview()` (soundboard + TTS preview), `getAudioDuration(file)` (soundboard upload validation), and drag-and-drop event relay + `XMLHttpRequest`-style upload-progress reporting (soundboard upload) is the entire "must stay JS" audio surface. VOX needs none of it.

## 5. Totals

Line counts sum the 71 production `.js` files (excludes the 238-line test file, counted separately above) — total production JS: **29,656 lines**.

| Class | Files | Lines | % of lines |
|---|---:|---:|---:|
| **A** — pure UI state/DOM/fetch+render, fully replaced | 27 | ~9,050 | 31% |
| **B** — thin interop shim survives (focus/scroll/clipboard/localStorage/keyboard/media-query) | 20 | ~7,750 | 26% |
| **C** — must stay JS (Chart.js, Web Audio, drag-drop/XHR upload) | 14 | ~6,930 | 23% |
| **D** — obsolete (AJAX/url-state/loading-manager/api-client/tab-loader plumbing) incl. 8 orphaned dead files (~3,850 lines already excluded from "live" totals below where noted) | 10 live + 8 dead | ~5,926 | 20% |

*(A few files split across two classes in the table above — e.g. `command-log-modal.js` A/D, `tab-panel.js` B/D, `performance/dashboard.js` D-orchestration hosting C-tab-modules, `portal-soundboard.js` C-dominant with A/B parts — they're counted once, under their dominant/first-listed class, for this total.)*

**Dead code to delete outright** (not "port", just remove — 8 files, 3,851 lines): `server-analytics.js` (423), `engagement-analytics.js` (430), `moderation-analytics.js` (409), `command-analytics.js` (512), `api-metrics-chart.js` (208), `command-error-handler.js` (439), `command-loading-states.js` (353), `performance-shell.js` (450), `performance-tabs.js` top-level (1060) — **that's actually 9 files / 4,284 lines**; listed individually in §1's dead-code table.

**Estimated JS that must survive the port as interop modules (classes B + C combined)**: roughly **14,700 lines** of *source* JS map down to a much smaller set of actual interop modules once the fetch/DOM-update logic inside B-classified files is stripped out and only the browser-only capability remains:

- **Chart.js wrapper** (~300–400 lines): generic `createChart/updateChart/destroyChart` interop + the `chart-utils.js` theme defaults. Covers all 14 C-classified chart files' actual charting code (the fetch/polling/DOM logic around each chart does not survive).
- **Audio interop** (~150–250 lines): `playPreview/stopPreview`, `getAudioDuration`, drag-drop relay, upload-progress relay — covers `portal-soundboard.js`'s and `portal-tts.js`'s audio slivers.
- **Small focus/scroll/keyboard/clipboard/localStorage/media-query helpers** (~400–600 lines total): one shared interop module replacing the scattered `localStorage` reads (`theme.js`, `user-preferences.js`, `navigation.js`, `search.js`, `date-range-filter.js`, `performance/time-range.js`), `navigator.clipboard` (`navigation.js`, `member-directory.js`), `matchMedia` (`portal-vox.js`), `beforeunload` dirty-guards (`settings.js`, `moderation-settings.js`), and the `Intl` timezone detection (`timezone.js`).
- **`ssml-markers.js`** (83 lines) ports close to verbatim as a pure-function interop module (no DOM) if kept client-side for per-keystroke responsiveness in the Pro TTS composer, rather than round-tripping every keystroke to the Blazor Server circuit.
- **`keyboard-shortcuts.js`** (259 lines) survives close to as-is — it's already a clean registry+overlay pattern that just needs a thin C# `RegisterShortcut` interop call per component.

**Total realistic surviving interop code**: on the order of **1,200–1,800 lines** of new, purpose-built JS (versus ~14,700 lines of B/C-classified source today) — the rest of the B/C files' bulk is fetch/DOM-update/state-management logic that Blazor's component model replaces outright, even where a small interop call remains embedded in the same file today.

## 6. Other notes for the port

- **No `wwwroot/lib`** directory exists — all third-party JS is CDN-loaded per-page (`chart.js@4.4.1`, `@microsoft/signalr@8.0.0` from jsDelivr, with a cdnjs mirror of signalR on `Index.cshtml`). Nothing is bundled/vendored locally.
- **Two duplicate live pollers** for the same data: `bot-status-refresh.js` (30s poll of `/api/bot/status`) and `settings.js`'s bot-control panel (5s poll of the same endpoint) both run alongside `dashboard-hub.js`'s push-based `BotStatusUpdated` event — this redundancy predates Blazor and is worth collapsing to the push model even before the port.
- **Two full duplicate generations of the Performance dashboard** exist side by side (`Admin/Performance/Index.cshtml` unified AJAX-tab version vs. five legacy single-tab pages `SystemHealth/HealthMetrics/Alerts/Commands/ApiMetrics.cshtml`, each with its own `*-realtime.js`). Worth flagging to the team: does the port carry forward both, or retire the legacy pages first?
- Every `data-*`-driven module (there are dozens) maps naturally onto Blazor component parameters/`@bind`/`@onclick` — none of that DOM-contract surface needs to survive as literal `data-*` attributes once real components exist, it's listed here only so nothing gets missed during the rewrite.
- Discord snowflake IDs are consistently passed to JS as **strings** already (`window.guildId`, `dataset.soundId`, etc.) — the project's own `ulong`-in-JS gotcha (see CLAUDE.md) is already respected throughout; nothing to fix, just something Blazor component parameters must keep doing (bind guild/user/sound IDs as `string`, not `long`, in any interop payload).

---

# Part 5 — Hosting, auth, API, real-time, tests


Read-only survey of `/home/user/discordbot` web side, for planning a full Blazor port.

---

## Hosting pipeline

**`src/DiscordBot.Bot/Program.cs`** is a single top-level-statements file (no `Startup.cs`). Order matters:

Service registration (all via `*ServiceExtensions.cs` in `Extensions/`):
`AddDiscordBot` → `AddInfrastructure` → `AddObservability` → `Configure<ForwardedHeadersOptions>` → `AddIdentityServices` → `AddApplicationServices` → feature service groups (Verification, MessageLogging, AuditLogging, Notifications, ScheduledMessages, RatWatch, Reminders, VoiceSupport, Assistant, DmAssistant, NotX, Analytics, ModerationServices, FeatureRequests, PerformanceMetrics) → `AddHealthCheckServices` → `AddWebServices` (controllers + Razor Pages + Swagger explorer) → `AddSignalRServices` → `AddSwaggerDocumentation`.

`app.Build()` then **auto-applies EF Core migrations** (`db.Database.MigrateAsync()`) before the pipeline runs.

Middleware pipeline, in exact order:
1. `app.UseAllElasticApm(configuration)` — Elastic APM, must be first to capture full requests.
2. `app.UseForwardedHeaders()` — X-Forwarded-For/Proto, `KnownNetworks`/`KnownProxies` cleared (trusts any proxy — nginx/Cloudflare in front). Needed for SignalR WebSockets behind a reverse proxy.
3. `app.UseCorrelationId()` — custom middleware; reads/generates `X-Correlation-ID`, stores in `HttpContext.Items["CorrelationId"]`, pushes to Serilog `LogContext` and Elastic APM transaction label. `HttpContextExtensions.GetCorrelationId()` reads it back (falls back to `TraceIdentifier`).
4. `app.UseApiMetrics()` — custom middleware; records request count/duration via `ApiMetrics`, only for paths starting `/api/`, `/Account/`, `/Admin/`, or `/`/`/Index` (skips `/health`). Normalizes GUIDs/snowflakes/numeric IDs out of the path for cardinality control.
5. `app.UseSerilogRequestLogging(...)` — verbose level for `/health`, else Information.
6. Dev: `UseDeveloperExceptionPage()`; else `UseExceptionHandler("/Error/500")`.
7. `UseStatusCodePagesWithReExecute("/Error/{0}")` — re-executes `/Error/403`, `/Error/404`, etc. Razor Pages under `Pages/Error/`.
8. `UseStaticFiles()`.
9. Dev only: `UseSwagger()` + `UseSwaggerUI()` at `/swagger` — **Swagger is not exposed outside Development.**
10. `UseAuthentication()`.
11. **Inline anonymous middleware**: normalizes `"/"`/`"/Index"` and redirects unauthenticated users to `/landing` — runs *before* `UseAuthorization()` specifically so the authorization middleware never gets a chance to bounce them to `/Account/Login` for the root path.
12. `UseAuthorization()`.
13. `MapControllers()`, `MapDiscordBotHealthChecks()` (`/health`, anonymous, JSON body, custom `ResponseWriter`), `MapRazorPages()`, `MapHub<DashboardHub>("/hubs/dashboard")`, `UseOpenTelemetryPrometheusScrapingEndpoint()` (`/metrics`).

Startup tasks after `app.Build()`/before `RunAsync()`: `IdentitySeeder.SeedIdentityAsync` seeds the 4 roles (SuperAdmin/Admin/Moderator/Viewer) and an optional default admin from `Identity:DefaultAdmin` config (user secrets), inside a try/catch so seed failure doesn't crash startup.

**Razor Pages conventions.** No `AddRazorPagesOptions` / `.Conventions.AuthorizeFolder(...)` anywhere in the codebase — confirmed by grep. Every page's auth requirement is set **per PageModel** via `[Authorize(Policy = "...")]` (guild pages commonly stack two: `[Authorize(Policy = "RequireX")]` + `[Authorize(Policy = "GuildAccess")]`) or explicit `[AllowAnonymous]` (Login, ExternalLogin, Logout, AccessDenied, Lockout, Landing, Error/403/404/500, PublicLeaderboard, and the three Portal landing pages TTS/Soundboard/VOX which allow anonymous *viewing* but gate real functionality behind `PortalPageModelBase.CheckPortalAuthorizationAsync`). Everything else falls through to the global `FallbackPolicy = RequireAuthenticatedUser()` set in `AddAuthorizationPolicies()`. No route-template customization (`@page` uses default folder-based routing throughout).

**Endpoints mapped:** `MapControllers()`, `/health`, `MapRazorPages()`, `/hubs/dashboard` (SignalR), `/metrics` (Prometheus), plus `/swagger` in Development only.

---

## Identity & OAuth

**Cookie & Identity config** (`IdentityServiceExtensions.AddIdentityServices`, options bound from `Identity` section, `IdentityConfigOptions`):
- `AddIdentity<ApplicationUser, IdentityRole>` + `AddEntityFrameworkStores<BotDbContext>` + `AddDefaultTokenProviders()`.
- Password policy, lockout (`MaxFailedAccessAttempts` default 5, `LockoutTimeSpanMinutes` default 15, `LockoutAllowedForNewUsers` default true), `RequireUniqueEmail`, `SignIn.RequireConfirmedAccount`/`RequireConfirmedEmail` (both default **false**) — all config-driven via `IdentityConfigOptions` (`src/DiscordBot.Core/Configuration/IdentityConfigOptions.cs`).
- **No 2FA configuration exists** (`IdentityConfigOptions` has no 2FA knobs) and **there is no `LoginWith2fa` page** in `Pages/Account/`, even though `Login.cshtml.cs.OnPostAsync` has a live `if (result.RequiresTwoFactor) return RedirectToPage("./LoginWith2fa", ...)` branch — that branch is effectively dead/broken code today (would 404). `AddDefaultTokenProviders()` means 2FA *could* be turned on for a user via Identity APIs, but no UI supports it. Treat 2FA as out-of-scope unless the port is asked to actually build it.
- `ConfigureApplicationCookie`: `HttpOnly = true`, `SecurePolicy = SameAsRequest`, `SameSite = Lax`, `ExpireTimeSpan` from `CookieExpireDays` (default 7d), `SlidingExpiration` (default true), `LoginPath = /Account/Login`, `LogoutPath = /Account/Logout`, `AccessDeniedPath = /Account/AccessDenied`.
- Data Protection keys persisted to a configurable filesystem path (`DataProtection:KeyPath`, default `%LocalAppData%/DataProtection-Keys`) with `SetApplicationName("DiscordBot")` — required so antiforgery/cookies survive restarts under systemd `ProtectHome=true`. **This must keep working identically for a Blazor Server app** (circuits reuse the same auth cookie).

**Discord OAuth** (`AddDiscordOAuth`, package `AspNet.Security.OAuth.Discord`):
- `AddAuthentication().AddDiscord(_ => {})`, then options configured via `PostConfigure`-style `AddOptions<DiscordAuthenticationOptions>(...).Configure<IOptions<DiscordOAuthOptions>>(...)`: ClientId/Secret from `DiscordOAuthOptions` (validated with `ValidateDataAnnotations().ValidateOnStart()`), scopes `identify email guilds`, `SaveTokens = true`.
- `OnRemoteFailure`: classifies the exception (`ClassifyOAuthError` — HTTP 503/502/504 → `discord_unavailable`; message contains `invalid_grant`/`expired`/`code was already redeemed` → `discord_expired`; network exceptions → `discord_unavailable`; else `discord_error`) and redirects to `/Account/Login?authError={type}` — **this classification logic and redirect must survive the port** (it's what turns raw OAuth failures into user-facing messages on Login.cshtml).
- A singleton `DiscordOAuthSettings { IsConfigured = true }` is registered only when OAuth is wired up — pages check `_discordOAuthSettings.IsConfigured` to conditionally show the Discord login button (so the app works with password-only auth if Discord isn't configured).

**Login flow** (`Pages/Account/Login.cshtml.cs`, `[AllowAnonymous]`):
- `OnGet`: redirects already-authenticated users away (sanitized return URL via `ReturnUrlHelper.Sanitize`, prevents open-redirect and login-page self-loops); maps `?authError=` query param to a friendly title/message pair for display.
- `OnPostAsync`: password sign-in via `SignInManager.PasswordSignInAsync(lockoutOnFailure: true)`; rejects inactive users (`user.IsActive`); on success updates `LastLoginAt` and enqueues an audit log (`AuditLogCategory.Security` / `AuditLogAction.Login`); on 2FA-required redirects to the missing `LoginWith2fa` page (see above); on lockout redirects to `./Lockout`; on failure logs a failed-login audit entry with `BySystem()`.
- `OnPostDiscordLogin`: issues a `ChallengeResult("Discord", properties)` with `Url.Page("./ExternalLogin", pageHandler: "Callback", ...)` as the redirect target — **this Challenge/Callback round trip must stay a real HTTP GET/redirect flow; it cannot become a Blazor interactive-circuit interaction.**

**External login callback** (`Pages/Account/ExternalLogin.cshtml.cs`, `[AllowAnonymous]`, handler `OnGetCallbackAsync`):
- Reads OAuth tokens from the **external** auth scheme (`IdentityConstants.ExternalScheme`) *before* `SignInAsync`, because tokens live on the external cookie, not the app cookie.
- Tries `ExternalLoginSignInAsync` first (existing linked account); on success updates Discord profile fields, stores OAuth tokens (`IDiscordTokenService.StoreTokensAsync`) and guild memberships (`IUserDiscordGuildService.StoreGuildMembershipsAsync`, populated by calling `GET users/@me/guilds` on the `"Discord"` named `HttpClient` with a Bearer token), invalidates caches, audit-logs the login, then `LocalRedirect(returnUrl)`.
- If no existing link: `CreateUserFromExternalLoginAsync` — looks up by Discord ID first (handles email changes), then by email (links accounts), else creates a brand-new `ApplicationUser` (`EmailConfirmed = true` since Discord emails are pre-verified) and signs in.
- All of this is single-request, redirect-driven, no client JS involved — **must remain a plain endpoint** (Identity's own external-login machinery + `SignInManager` cookie writes require a normal HTTP request/response with `HttpContext`, not a persistent SignalR circuit).

**Account linking** (`Pages/Account/LinkDiscord.cshtml.cs`, `[Authorize]`, for already-logged-in password users to attach Discord): `OnPostLinkAsync` issues the same Challenge/Callback pattern back to `./LinkDiscord` as return URL; `OnPostUnlinkAsync` clears Discord fields, deletes tokens/guild cache, removes the external login; `OnPostInitiateBotVerificationAsync`/`OnPostVerifyCodeAsync`/`OnPostCancelVerificationAsync` drive a Discord-bot-side `/verify-account` slash-command verification code flow (`IVerificationService`); `OnPostRefreshDiscordDataAsync` re-pulls guild memberships.

**Roles** (`IdentitySeeder.Roles`): `SuperAdmin`, `Admin`, `Moderator`, `Viewer` — seeded on every startup if missing.

---

## Authorization policies & guild access resolution

Registered in `AddAuthorizationPolicies()` (`IdentityServiceExtensions.cs`):

| Policy | Requirement |
|---|---|
| `RequireSuperAdmin` | role `SuperAdmin` |
| `RequireAdmin` | role `SuperAdmin` or `Admin` |
| `RequireModerator` | role `SuperAdmin`, `Admin`, or `Moderator` |
| `RequireViewer` | role `SuperAdmin`, `Admin`, `Moderator`, or `Viewer` |
| `GuildAccess` | custom `GuildAccessRequirement` |
| `PortalGuildMember` | custom `PortalGuildMemberRequirement` |
| *(fallback)* | `RequireAuthenticatedUser()` for anything with no explicit policy |

**Notable discrepancy — two competing `GuildAccess` handlers exist in the codebase, only one is wired up:**
- `GuildAccessHandler` (`Authorization/GuildAccessHandler.cs`) is the one actually registered (`services.AddScoped<IAuthorizationHandler, GuildAccessHandler>()`). It checks `IsInRole(SuperAdmin)` → bypass; else resolves `guildId` from route/query, loads the `ApplicationUser`, requires `DiscordUserId` to be linked, looks up the guild live via `DiscordSocketClient.GetGuild(guildId)` and `guild.GetUser(...)`, then requires `GuildPermissions.Administrator` for the `Admin` role or plain membership for `Moderator`/`Viewer`.
- `GuildAccessAuthorizationHandler` (`Authorization/GuildAccessAuthorizationHandler.cs`) is a **separate, DB-backed** implementation — SuperAdmin/Admin bypass, then checks the `UserDiscordGuild` table (cached Discord membership) and falls back to explicit `UserGuildAccess` grants compared against `GuildAccessRequirement.MinimumLevel` (`GuildAccessLevel` enum in `Core/Entities/UserGuildAccess.cs`). **This class is unit-tested (`GuildAccessAuthorizationHandlerTests.cs`) and is what `docs/articles/authorization-policies.md` and `docs/architecture-diagrams.md` describe as *the* guild access handler — but it is never registered in DI**, so in the running app it is dead code today. Confirm with the team which behavior is authoritative before porting; a Blazor port is a natural point to consolidate to one (the DB-backed one scales better since it avoids a live Discord gateway lookup per request, and it's the one the docs and `GuildMembershipCacheOptions` assume).

`PortalGuildMemberAuthorizationHandler` (registered): lighter check for portal pages/APIs — SuperAdmin/Admin bypass; else requires authenticated user with Discord linked, requires the guild's `AudioSettings.AudioEnabled` flag true (a `TODO` in the code says this is a stand-in for a not-yet-built `EnableMemberPortal` flag — issue #947), then verifies guild membership (cache first, REST fallback via `DiscordSocketClient.Rest.GetGuildUserAsync`). Sets `HttpContext.Items["AuthorizationFailureReason"]` to `"Forbidden"` or `"NotFound"` for downstream handling (worth checking how/if that item is consumed to produce 403 vs 404 — search turned up only the handler setting it; if nothing reads it today, the port should decide the intended response mapping explicitly).

`DiscordClaimsTransformation` (`IClaimsTransformation`, scoped): on every authenticated request, if the user has `DiscordUserId` set, adds claims `discord:user_id`, `discord:linked=true`, `discord:username`, `discord:avatar_url`, `display_name`; otherwise `discord:linked=false`. Guarded against re-running via a `HasClaim` check. **This transformation runs per-request against the auth cookie's principal — in Blazor Server this still works because the circuit's `HttpContext`/principal is established once per circuit from the same cookie, but any place that reads these claims must not assume a fresh HTTP request each time.**

`ClaimsPrincipalExtensions.GetDiscordUserId()` reads the `DiscordId` claim (not `discord:user_id` — note the two different claim-name schemes in play: ASP.NET Identity's own `DiscordId` claim from the OAuth login vs. the transformation's `discord:*` claims).

Guild ID is read from **route values or query string** (`httpContext.Request.RouteValues["guildId"]` / `Request.Query["guildId"]`) in every one of these handlers — a Blazor router's route parameters populate `RouteValues` similarly for server-rendered/interactive pages, but this must be re-verified per render mode (WASM has no server `HttpContext` per navigation at all).

---

## Antiforgery & security headers

**No `services.AddAntiforgery(...)` call anywhere** — the app relies on the default antiforgery service auto-registered by `AddRazorPages()`/MVC, with default cookie/header names (`__RequestVerificationToken` field name is used explicitly in JS). There is **no global `[AutoValidateAntiforgeryToken]` filter and no `[ValidateAntiForgeryToken]` on any Razor Pages handler** (grep found zero results) — pages presumably rely on ASP.NET Core's built-in automatic antiforgery validation for Razor Pages `OnPost*` handlers (enabled by default via `AddRazorPages`), which validates the form-embedded token or matching header on unsafe verbs.

Token is rendered explicitly via `@Html.AntiForgeryToken()` **scattered on individual pages** that need to call JSON APIs from script (13 occurrences: `Guilds/AudioSettings/Index`, `Guilds/ModerationSettings/Index`, `Admin/Performance/Tabs/_AlertsTab`, `Admin/Settings`, `Shared/Components/_TypedConfirmationModal`, `Shared/Components/_ConfirmationModal`, `Account/Profile`) — **not** rendered globally in `_Layout.cshtml`, so any page whose JS calls a mutating API endpoint must remember to add this tag or the fetch will 400. `wwwroot/js/api-client.js` (the shared fetch wrapper used by 11 other JS files) has `getAntiForgeryToken()` which does `document.querySelector('input[name="__RequestVerificationToken"]')` and, when found, sets header `RequestVerificationToken` on every non-GET request by default (`options.token` defaults to `true`); if the hidden input is absent on a given page, the header is silently omitted (no client-side error) and the server-side request will fail antiforgery validation if it needs it.

The `[ApiController]` JSON endpoints (all 34 API controllers) get **no automatic antiforgery enforcement** from ASP.NET Core itself (that machinery is Razor Pages/MVC-view specific unless a filter is added) — in practice they're protected only by the **same-origin cookie + `SameSite=Lax`** posture, not by token validation, except where a page explicitly forwards the header and a handler explicitly checks it (none found). This is a soft spot worth flagging to the team, though out of scope to fix during a pure UI port.

**No Content-Security-Policy, X-Frame-Options, or any other custom security-header middleware exists anywhere in the app** (`Program.cs`, all `Extensions/`, all `Middleware/` — zero matches for `Content-Security-Policy` or `nonce`), and no CSP is set at the nginx layer either (no nginx config lives in this repo). This is a green field: a Blazor port introducing `blazor.web.js`/`blazor.server.js` (loaded via `<script>` with SRI by the Blazor SDK) has no existing CSP to reconcile with, but if the team wants one now, the interactive script tag will need either `'unsafe-inline'`-free nonce wiring or a hash allowlist, and the SignalR negotiate/WebSocket origin needs a `connect-src` entry.

---

## SignalR

Single hub: **`DashboardHub`** (`Hubs/DashboardHub.cs`), mapped at `/hubs/dashboard`, class-level `[Authorize(Policy = "RequireViewer")]` (so every SignalR connection requires at least Viewer role — no anonymous or portal-guest access to this hub). Registered via `AddSignalRServices` (`SignalRServiceExtensions.cs`): `KeepAliveInterval=15s`, `ClientTimeoutInterval=30s`, `HandshakeTimeout=15s`, `EnableDetailedErrors` only in Development.

The hub is a **thin transport shim** — connection/group lifecycle lives in the hub; all data comes from injected per-feature services (`IDashboardMetricsService`, `IDashboardAudioStatusService`, `IDashboardNotificationQueryService`, `IPerformanceSubscriptionTracker`).

**Groups** (constants on `DashboardHub`): `alerts`, `performance`, `system-health`, `bulk-purge`, guild-scoped `guild-{guildId}` and `guild-audio-{guildId}` (built from string-typed guild IDs to preserve JS `ulong` precision — `ulong.TryParse` guards every group join/leave). Corresponding `Join*/Leave*` hub methods for each.

**Client-callable methods** (invoked from JS): `JoinGuildGroup`/`LeaveGuildGroup`, `GetCurrentStatus`, `GetHealthStatus`, `JoinAlertsGroup`/`LeaveAlertsGroup`, `GetActiveAlertCount`, `JoinBulkPurgeGroup`/`LeaveBulkPurgeGroup`, `JoinPerformanceGroup`/`LeavePerformanceGroup` (also updates `IPerformanceSubscriptionTracker` for broadcast-optimization), `JoinSystemHealthGroup`/`LeaveSystemHealthGroup`, `GetCurrentPerformanceMetrics`, `GetCurrentSystemHealth`, `GetCurrentCommandPerformance(hours)`, `JoinGuildAudioGroup`/`LeaveGuildAudioGroup`, `GetCurrentAudioStatus(guildIdString)`, and a notification API (`GetNotificationSummary`, `GetNotifications(limit)`, `MarkNotificationRead(id)`, `MarkAllNotificationsRead`, `DismissNotification(id)` — these resolve the user via `Context.User.FindFirstValue(ClaimTypes.NameIdentifier)`, returning empty/no-op for unauthenticated contexts rather than throwing).

**Server→client events** (string event names, not strongly typed): `OnNotificationReceived`, `OnNotificationCountChanged`, `OnNotificationMarkedRead`, `OnAllNotificationsRead` (constants on `DashboardHub`), plus ad-hoc names used only at the call site — `"HealthMetricsUpdate"`, `"CommandPerformanceUpdate"`, `"SystemMetricsUpdate"`, `"OnAlertTriggered"`, `"OnAlertResolved"`, `"OnAlertAcknowledged"`, `"OnActiveAlertCountChanged"`, `"BulkPurgeProgress"` — a Blazor port using `HubConnection` directly (or a typed client wrapper) should centralize these as constants since several currently only exist as magic strings at each `SendAsync` call site.

**Broadcasters** (all inject `IHubContext<DashboardHub>`, found via grep across `src/`):
- `DashboardNotifier` — `Clients.All.SendAsync(...)` (bot status/health-ish, broad) and `Clients.Group(groupName).SendAsync(...)`.
- `Notifications/NotificationBroadcaster` — targets `Clients.User(userId)` implicitly through group/user patterns for the 4 notification events.
- `PerformanceMetricsBroadcastService` (a `MonitoredBackgroundService`) — periodically pushes `HealthMetricsUpdate`/`CommandPerformanceUpdate` to `performance` group, `SystemMetricsUpdate` to `system-health` group.
- `PerformanceNotifier` — alert lifecycle events to `alerts` group.
- `DashboardUpdateService` — mixes `Clients.All` and `Clients.Group(groupName)`.
- `AudioNotifier` — all guild-audio events to `guild-audio-{guildId}` groups (voice/soundboard/TTS state for the dashboard's live audio widget).
- `BulkPurgeService` — progress events to `bulk-purge` group.

For a Blazor port, `IHubContext<DashboardHub>` usage is **entirely server-side background/service code and is orthogonal to the UI framework** — it keeps working unchanged regardless of whether the client renders via Razor Pages+JS or Blazor components, as long as something still hosts the hub endpoint and something still connects a `HubConnection` (Blazor Server components could instead just use component state + `IHubContext` server-side... but since Blazor Server *is* already a SignalR circuit, there's a real design choice here: keep `DashboardHub` as a second, independent SignalR hub the Blazor page also subscribes to via JS interop/`HubConnection`, or fold its groups/events into direct server-side push to component state over the Blazor circuit itself. Either is workable; the existing hub's group model (per-guild, per-feature) transfers directly to option 1).

---

## Controllers

All 36 concrete controllers (39 files, 3 abstract bases) under `src/DiscordBot.Bot/Controllers/` inherit `ControllerBase` directly or `ApiControllerBase`/`PortalSoundboardControllerBase`/`PortalTtsControllerBase` (thin base classes adding error-response helpers — see `ApiControllerBase` below). All are `[ApiController]`; only **`ThemeController`** and the base classes lack a `[Route]` (Theme still has `[Route("api/[controller]")]` actually — checked). Auth policy is a class-level `[Authorize(Policy = "...")]` in every case except `AlertsController` and `BotController`, which have **no class-level `[Authorize]` at all** (effectively falling through to the global `FallbackPolicy = RequireAuthenticatedUser()` — i.e. any authenticated user, any role, can hit `/api/alerts/*` and `/api/bot/*`, including `POST /api/bot/shutdown`). **Flag this for the port**: shutdown/restart and alert-config endpoints having no role gate (relying only on "signed in") looks unintentional next to every sibling controller using `RequireAdmin`/`RequireSuperAdmin`.

Swagger is Development-only, and `docs/articles/api-endpoints.md` still states `"Authentication: None (MVP - authentication to be added in future releases)"` in its Overview — **stale**; every controller except `AlertsController`/`BotController` now enforces a policy. The doc covers roughly two-thirds of the controllers (health, metrics, alerts, bot, guilds, guild members, commands, command logs, welcome, scheduled messages, audit logs, message logs, theme, autocomplete, VOX portal) but has **no section at all** for: `NotificationsController`, `UserPreferencesController`, all 4 `PortalSoundboard*` controllers, all 5 `PortalTts*` controllers, `PreviewController`, `PerformanceTabsController`, `BulkPurgeController`, `ModTagsController`, `SoundsController`, `AudioController`, `AnalyticsController`, `WatchlistController`, `ModerationCasesController`, `ModerationConfigController`, `UserModerationController`, `FlaggedEventsController` (the last four are referenced only briefly, not with full endpoint tables). Doc coverage does **not** cleanly separate "public API" from "portal-internal" — VOX portal is documented despite being guild-member-gated portal JSON, while the structurally similar Soundboard/TTS portal controllers are not documented at all. Given there's no API-key/bearer auth path anywhere (everything rides the same ASP.NET Identity cookie the browser UI uses), **none of these controllers are genuinely "external" in the sense of being consumed by a different client today** — the X/B split below reflects intent/shape (resource CRUD vs widget-JSON) more than actual external consumption.

Legend: **P** = portal/UI-backing only (JSON for this app's own JS, HTML partials, autocomplete, widgets) · **X** = shaped/documented as a general REST resource · **B** = both (documented as a REST resource *and* is what the admin JS actually calls for its own pages).

| Controller | Route prefix | Auth policy | Endpoints (method + route — purpose) | Class |
|---|---|---|---|---|
| `AlertsController` | `api/alerts` | **none** (falls to authenticated-only) | GET `config`, GET `config/{metricName}`, PUT `config/{metricName}`, GET `active`, GET `incidents`, GET `incidents/{id}`, POST `incidents/{id}/acknowledge`, POST `incidents/acknowledge-all`, GET `summary`, GET `stats` — alert-rule config & incident management | B |
| `AnalyticsController` | `api/analytics` | RequireViewer | GET `{guildId}/summary`, `/activity`, `/channels`, `/growth`, `/server/summary`, `/server/activity`, `/server/heatmap`, `/server/contributors`, `/moderation/summary`, `/moderation/trends`, `/moderation/distribution`, `/moderation/offenders`, `/moderation/workload`, `/engagement/summary`, `/engagement/messages`, `/engagement/retention` — guild analytics dashboards | P |
| `ApiControllerBase` | — | — | abstract: `NotFoundError`/`BadRequestError`/`ValidationError` helpers returning `ApiErrorDto` | — |
| `AudioController` | `api/guilds/{guildId}/audio` | RequireViewer | POST `join/{channelId}`, POST `leave`, POST `stop`, DELETE `queue/{position}` — voice-channel join/leave/queue control | P |
| `AuditLogsController` | `api/auditlogs` | RequireSuperAdmin | GET (list, filtered/paginated), GET `{id}`, GET `stats`, GET `by-correlation/{correlationId}` — audit trail viewer | B |
| `AutocompleteController` | `api/autocomplete` | RequireViewer | GET `users`, `guilds`, `channels`, `commands` — search-box typeahead | B |
| `BotController` | `api/bot` | **none** (falls to authenticated-only) | GET `status`, GET `guilds`, GET `dashboard-stats`, POST `restart` (not supported), POST `shutdown` | B |
| `BulkPurgeController` | `api/bulkpurge` | RequireSuperAdmin | POST `preview`, POST `execute` — mass message deletion with SignalR progress | P |
| `CommandLogsController` | `api/commandlogs` | RequireModerator | GET (list), `stats`, `analytics`, `analytics/usage-over-time`, `analytics/success-rate`, `analytics/performance` | B |
| `CommandsApiController` | `api/commands` | RequireViewer | GET `list`, `logs`, `analytics`, `log-details/{id}` — same domain as CommandLogsController but returns **HTML partials** for tab content | P |
| `FlaggedEventsController` | `api/guilds/{guildId}/flagged-events` | RequireAdmin | GET (list), `filter`, `{id}`, POST `{id}/dismiss`, `{id}/acknowledge`, `{id}/action` — moderation flagged-content queue | B |
| `GuildMembersController` | `api/guilds/{guildId}/members` | RequireAdmin | GET (list), `{userId}`, `export` (CSV) — member directory | B |
| `GuildsController` | `api/guilds` | RequireAdmin | GET (list), `{id}`, PUT `{id}`, POST `{id}/sync` — guild CRUD/sync from Discord | X |
| `MessagesController` | `api/messages` | RequireAdmin | GET (list), `{id}`, `stats`, DELETE `user/{userId}` (GDPR), POST `cleanup`, GET `export` | X |
| `ModTagsController` | `api/[controller]` (implicit `api/modtags`) | RequireAdmin | GET, POST (create tag), DELETE `{tagName}`, POST (assign to member — 2nd POST overload), DELETE (unassign), POST `import-templates` — moderation tag taxonomy | P |
| `ModerationCasesController` | `api/guilds/{guildId}/cases` | RequireAdmin | GET (list), `{caseId}`, `number/{caseNumber}`, POST (create), PATCH `number/{caseNumber}/reason` | B |
| `ModerationConfigController` | `api/guilds/{guildId}/moderation-config` | RequireAdmin | GET, PUT, POST `preset` | B |
| `NotificationsController` | `api/notifications` | RequireViewer | GET (list), POST `{id}/read`, `{id}/unread`, `mark-read`, `mark-all-read`, `delete-all`, DELETE `{id}`, POST `delete` — in-app notification bell, mirrors SignalR `DashboardHub` notification methods over plain HTTP | P |
| `PerformanceMetricsController` | `api/metrics` | RequireViewer | 16 GET endpoints — health/latency/cpu/connections, command performance/slowest/throughput/errors, API usage/rate-limits/latency, system database/services/cache/history(+db/+memory) | B |
| `PerformanceTabsController` | `api/performance/tabs` | RequireViewer | GET `overview`, `health`, `commands`, `api`, `system`, `alerts` — **HTML partials** for the Performance dashboard's tabbed UI | P |
| `PortalSoundboardCategoriesController` | `api/portal/soundboard/{guildId}` | PortalGuildMember | GET/POST `categories`, PUT/DELETE `categories/{id}`, PUT `sounds/{soundId}/category` | P |
| `PortalSoundboardControllerBase` | — | — | shared base for the 4 soundboard-portal controllers | — |
| `PortalSoundboardFavoritesController` | `api/portal/soundboard/{guildId}` | PortalGuildMember | GET `favorites`, POST/DELETE `favorites/{soundId}` | P |
| `PortalSoundboardPlaybackController` | `api/portal/soundboard/{guildId}` | PortalGuildMember | POST `play/{soundId}`, GET `channels`, POST/DELETE `channel`, POST `stop`, GET `status` | P |
| `PortalSoundboardSoundsController` | `api/portal/soundboard/{guildId}` | PortalGuildMember | GET `sounds`, GET `sounds/{soundId}/audio` (stream), POST `sounds` (upload), DELETE `sounds/{soundId}` | P |
| `PortalTtsControllerBase` | — | — | shared base for the 5 TTS-portal controllers | — |
| `PortalTtsHistoryController` | `api/portal/tts/{guildId}` | PortalGuildMember | GET/POST `history`, POST `history/{id}/replay`, PUT `history/{id}/favorite`, DELETE `history/{id}` | P |
| `PortalTtsPlaybackController` | `api/portal/tts/{guildId}` | PortalGuildMember | GET `status`, POST `send`, GET `channels`, POST/DELETE `channel`, POST `stop` | P |
| `PortalTtsPresetsController` | `api/portal/tts/{guildId}` | PortalGuildMember | GET `/api/portal/tts/presets` (absolute — cross-guild), `presets/custom` GET/POST, DELETE `presets/custom/{id}`, POST `preview` | P |
| `PortalTtsSynthesisController` | `api/portal/tts/{guildId}` | PortalGuildMember | POST `/api/portal/tts/validate-ssml` (absolute), POST `synthesize-ssml`, POST `/api/portal/tts/build-ssml` (absolute), GET `/api/portal/tts/voices/{voiceName}/capabilities` (absolute) | P |
| `PortalVoxController` | `api/portal/vox/{guildId}` | PortalGuildMember | GET `clips`, `preview`, POST `play`, POST `stop`, GET `history`, `favorites`, POST `history/{id}/favorite`, DELETE `history/{id}` | B (documented as "VOX Portal API") |
| `PreviewController` | `api/preview` | RequireViewer | GET `users/{userId}`, `users/{userId}/guild/{guildId}`, `guilds/{guildId}` — hover-preview cards | P |
| `ScheduledMessagesController` | `api/guilds/{guildId}/scheduled-messages` | RequireAdmin | GET (list), `{id}`, POST (create), PUT `{id}`, DELETE `{id}`, POST `{id}/execute`, POST `validate-cron` | X |
| `SoundsController` | `api/guilds/{guildId}/sounds` | RequireViewer | GET `{soundId}/download`, GET `export` | P |
| `ThemeController` | `api/theme` | `[Authorize]` (any authenticated user, no specific policy) | GET `available`, `current`, POST `user`, POST `default` — light/dark/theme-variant preference | B |
| `UserModerationController` | `api/guilds/{guildId}/users/{userId}` | RequireAdmin | GET `cases`, `notes`, POST `notes`, GET `flags`, `tags`, POST `tags/{tagName}`, DELETE `tags/{tagName}`, DELETE `notes/{noteId}` — per-member moderation profile | B |
| `UserPreferencesController` | `api/portal/preferences/{guildId}` | PortalGuildMember | GET (all), GET `{key}`, PUT `{key}`, DELETE `{key}` — per-user portal preferences (extends `ApiControllerBase`) | P |
| `WatchlistController` | `api/guilds/{guildId}/watchlist` | RequireAdmin | GET, POST, DELETE `{userId}` | B |
| `WelcomeController` | `api/guilds/{guildId}/[controller]` | RequireAdmin | GET, PUT, POST `preview` | X |

**`ApiControllerBase`** (`Controllers/ApiControllerBase.cs`) — `[ApiController]` abstract base providing `NotFoundError`, `BadRequestError`, `ValidationError` (422), each returning a standardized `ApiErrorDto { Message, Detail, StatusCode, TraceId }` where `TraceId` comes from `HttpContext.GetCorrelationId()`. **Only a handful of controllers actually inherit it** (`UserPreferencesController` confirmed; `docs/architecture/patterns.md` claims "5 controllers use this pattern" and shows `OkPaginated`/`ServerError` helper methods that **do not exist in the current `ApiControllerBase.cs`** — the docs describe a richer base class than what's in the repo today; treat `patterns.md`'s API-controller example as aspirational/stale, not authoritative, when porting these response shapes).

---

## PageModel base classes

- **`GuildPageModelBase`** (`Pages/Guilds/GuildPageModelBase.cs`) — plain `PageModel` subclass (no constructor/DI of its own). Holds `Breadcrumb`/`Header`/`Navigation` view models plus `[TempData] SuccessMessage`/`ErrorMessage`, and protected builder methods (`BuildBasicBreadcrumb`, `BuildPageBreadcrumb`, `BuildNavigation`, `BuildHeader`) plus a one-call `PopulateGuildLayout(guildId, guildName, iconUrl, activeTab, pageTitle, pageDescription)` that fills all three. **Note**: `docs/architecture/patterns.md`'s "Guild Page Model Base" section describes a different, richer API — a constructor taking `(IGuildService, IDiscordChannelResolver, ILogger)` and an **async `PopulateGuildLayout(guildId)` returning `Task<bool>`** that does guild validation itself. The actual class has neither — it's synchronous, has no injected services, and does not validate the guild exists; each page still does its own guild lookup/`NotFound()` and then calls the synchronous layout builder. **Treat the docs example as describing an intended/older shape, not the current code — port from the real `GuildPageModelBase.cs`, not from `patterns.md`.** 24 guild pages inherit it directly (list captured via grep, e.g. `Guilds/Details`, `Guilds/AudioSettings/Index`, `Guilds/Reminders/Index`, `Guilds/FeatureRequests/*`, `Guilds/Analytics/*`, `Guilds/RatWatch/*`, `Guilds/Edit`, `Guilds/Soundboard/Index`, `Guilds/TextToSpeech/Index`, `Guilds/VOX/Index`, `Guilds/ModerationSettings/Index`, `Guilds/AssistantMetrics`, `Guilds/Members/Moderation`, `Guilds/FlaggedEvents/*`).
- **`PaginatedGuildPageModel`** (`Pages/Guilds/PaginatedGuildPageModel.cs`) extends `GuildPageModelBase`, adds `[BindProperty(SupportsGet=true)]` bound `SortBy`, `SortDescending`, `CurrentPage` (bound as query `pageNumber`), `PageSize`, plus output `TotalPages`/`TotalCount`.
- **`PortalPageModelBase`** (`Pages/Portal/PortalPageModelBase.cs`) — the base for the 3 public-facing Portal pages (TTS/Soundboard/VOX). Constructor DI's `IGuildService`, `DiscordSocketClient`, `UserManager<ApplicationUser>`, `ILogger`. Core method `CheckPortalAuthorizationAsync(guildId, portalName, ct)` returns a `(PortalAuthResult, PortalAuthContext?)` tuple where `PortalAuthResult` is one of `GuildNotFound` / `ShowLandingPage` (unauthenticated — page still renders, just shows a landing/login CTA instead of the full portal) / `NotGuildMember` / `Authorized`; `GetAuthResultAction(result)` maps that to `NotFound()`/`Forbid()`/`null` (continue rendering). This "landing page instead of redirect-to-login" UX pattern (unauthenticated users see *something*, not a 401/redirect) is a deliberate product decision — **a Blazor port must preserve the same three-state UX** (guest landing view vs. non-member forbidden vs. full portal), not collapse it to a simple `[Authorize]`/redirect.

---

## Tests & CI

Total: **3,353** `[Fact]`/`[Theory]` tests (`grep -c` sum across `tests/DiscordBot.Tests`) — higher than the ~3,600 the CLAUDE.md ballpark suggests is close enough (that figure is evidently a rounded approximation, or grew slightly since it was written).

Per-area rough counts (test-method count, not test classes):

| Area | Count | What it covers |
|---|---|---|
| `Bot/Services/` | 236 | Bot-layer services (largest single bucket) |
| `Bot/Pages/` | 150 | PageModel handler tests (`OnGet`/`OnPost*` unit tests with mocked services) — subfolders exist for `Account`, `Admin`, `Admin/Performance`, `CommandLogs`, `Guilds`, `Guilds/Analytics`, `Guilds/ModerationSettings`, `Guilds/RatWatch`, `Guilds/Reminders` |
| `Controllers/` (top-level) | 236 | Controller action tests — 13 files: `ApiControllerBaseTests`, `AuditLogsControllerTests`, `AutocompleteControllerTests`, `BotControllerTests`, `CommandLogsControllerTests`, `GuildsControllerTests`, `MessagesControllerTests`, `ModerationCasesControllerTests`, `PerformanceMetricsControllerTests`, `PortalTtsControllerTests`, `ScheduledMessagesControllerTests`, `ThemeControllerTests`, `WelcomeControllerTests` |
| `Bot/Controllers/` | 20 | `GuildMembersControllerTests` only |
| `ViewModels/` | 173 | View-model construction/mapping tests (`Components/AuditLogCardViewModelTests`, `Components/VoiceChannelPanelViewModelTests`, `CommandLogDetailViewModelTests`, `CommandLogListViewModelTests`, `CommandPerformanceViewModelTests`, `CommandStatsViewModelTests`, `GuildDetailViewModelTests`, `GuildEditViewModelTests`, `GuildSettingsViewModelTests`) |
| `Metrics/` | 86 | Includes `ApiMetricsMiddlewareTests` |
| `Bot/Collections/` | 72 | — |
| `Bot/Helpers/` | 46 | — |
| `Bot/Extensions/` | 26 | Includes extension-method tests |
| `Integration/` | 24 | `PortalTtsIntegrationTests.cs` — **not** a `WebApplicationFactory`/HTTP-pipeline test; it instantiates a controller directly against a real SQLite in-memory `BotDbContext`, mocked `DiscordSocketClient`/audio services. Confirmed via `grep -rln "WebApplicationFactory"` across the whole test project → **zero matches**. There is no full-stack HTTP-level integration test anywhere in this codebase today. |
| `Hubs/` | 23 | `DashboardHubTests.cs` — hub method unit tests, not live-connection tests |
| `Bot/Authorization/` | 23 | Includes `GuildAccessAuthorizationHandlerTests` (tests the *unregistered* handler — see Authorization section above) |
| `Components/` | 16 | `ComponentIdBuilderTests.cs` |
| `Middleware/` | 14 | `CorrelationIdMiddlewareTests.cs` |
| `Bot/TagHelpers/` | 16 | Custom Tag Helper tests |
| `Bot/Pages` sub-total is already inside `Bot/` above; `Bot/` total: 589 |

**What breaks / needs replacing under a Blazor port:**
- The 150 `Bot/Pages/` PageModel handler tests are Razor-Pages-specific (`OnGetAsync`/`OnPostAsync` unit tests against mocked services, asserting `IActionResult` types like `PageResult`/`RedirectToPageResult`/`NotFoundResult`). Every one of these needs a rewrite as a component test (bUnit or similar) once the corresponding page becomes a `.razor` component — there's no way to carry these forward as-is even if the underlying service logic is unchanged, because the assertions are against ASP.NET Core MVC/RazorPages result types that don't exist in component rendering.
- The 236 `Controllers/` + 20 `Bot/Controllers/` API-controller tests are **framework-agnostic relative to the UI** (they test JSON-returning `[ApiController]` actions) and can be kept unchanged if the port keeps the REST API layer as-is and only replaces the Razor Pages UI with Blazor components that call the same endpoints — this is true regardless of whether Blazor Server calls them via HttpClient/JS interop or a component calls a service directly.
- 173 `ViewModels/` tests are pure data/mapping tests, framework-agnostic, portable as-is (assuming the view-model types themselves are reused, which is a reasonable design goal for the port: keep DTOs/ViewModels, replace only the rendering layer).
- 23 `Hubs/` tests exercise `DashboardHub` methods directly (not over a live connection) — portable unchanged since the hub itself is untouched by a UI-only port.
- 14 `Middleware/` + 86 `Metrics/` (framework middleware, unrelated to rendering) — portable unchanged.
- No Playwright, no other e2e/browser-automation tooling, no `scripts/e2e`, nothing under `.github/workflows/` runs a browser test (confirmed via repo-wide grep for "playwright" and directory search for "e2e" — only hits are unrelated `scripts/kibana`). **A Blazor port has zero existing visual/behavioral regression safety net** beyond the unit/component-level tests above — worth calling out as a gap to fix (e.g. adding Playwright) alongside or before the port, not discovered by it.

**CI** (`.github/workflows/ci.yml`, runs on push/PR to `main`): checkout → setup .NET 8.0.x + Node 20.x (npm cached) → `dotnet restore` → `npm ci` in `src/DiscordBot.Bot` → `dotnet build -c Release` (embeds a PR-specific `InformationalVersion` on PR builds) → `dotnet test -c Release --no-build --logger trx --collect:"XPlat Code Coverage"` → uploads `.trx` and Cobertura coverage artifacts → writes a `$GITHUB_STEP_SUMMARY`. **No lint/format step, no Playwright step, no separate CSS-only build check** — the Tailwind build happens implicitly as part of `dotnet build` (via the Bot project's build-time npm/Tailwind MSBuild target referenced in CLAUDE.md), which CI satisfies by running `npm ci` first. `docker-publish.yml` and `release.yml` exist but weren't read in depth (out of scope for this pass; skimmed only ci.yml per the task list).

---

## Docker/build

`Dockerfile` (multi-stage):
- **Build stage**: `mcr.microsoft.com/dotnet/sdk:8.0-noble` (Ubuntu 24.04, not Debian/Alpine — required because the prebuilt `libdave.so`, Discord's DAVE E2EE voice library, needs glibc 2.38/glibcxx 3.4.32 that only Noble ships). Installs Node 20.x via NodeSource. Restores only the `.csproj` files first (layer caching), then `npm ci` in `src/DiscordBot.Bot` (also cached as its own layer via `package.json`/`package-lock.json` copy before full source copy), then downloads/unpacks `libdave` from a GitHub release zip, copies full `src/`, runs `npm run build:css` (**explicit Tailwind build step, separate from `dotnet publish`** — i.e. the Docker build does *not* rely on the MSBuild-integrated Tailwind target CLAUDE.md describes for local dev; it pre-builds CSS then presumably `dotnet publish` picks up the already-built `wwwroot/css` output, or the MSBuild target runs again and is idempotent — not fully disambiguated in this pass, worth a quick check if the port changes the Tailwind pipeline), then `dotnet publish -c Release --no-restore`.
- **Runtime stage**: `mcr.microsoft.com/dotnet/aspnet:8.0-noble`, installs `ffmpeg`, `python3`, `libsodium23`, `libopus0` + symlinks for the unversioned `.so` names, copies `libdave.so` from the build stage, copies published output plus `docs/agents/` and `docs/articles/` (the LLM assistant reads these as runtime prompt/reference material — **not just documentation, an actual runtime dependency** of the AI assistant feature), creates a non-root `appuser`, sets `ASPNETCORE_URLS=http://+:5000`, `HEALTHCHECK` against `/health`.

For the port: the Tailwind `content` globs in `tailwind.config.js` **already include `.razor`** — `"./Pages/**/*.{razor,cshtml}", "./Components/**/*.{razor,cshtml}"` — someone has already prepped the CSS pipeline for a Blazor migration; no Tailwind config change needed when `.razor` files start appearing under those same folders. `safelist` patterns for dynamically-composed classes (`badge-`, `btn-`, etc., built by C# switch expressions or JS templates) will need re-auditing once C# render logic moves into `.razor` markup/`@code` blocks, since the mechanism for how those classes get composed may change even though the safelist regex itself doesn't need to.

No `wwwroot/lib` (no vendored/local JS libraries — the only CDN script found is SignalR's client, `https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.0/dist/browser/signalr.min.js`, loaded in `Pages/Shared/_Layout.cshtml`). `wwwroot/exports` and `wwwroot/images` exist; `exports` is empty in source control (runtime-generated CSV exports write here — check whether this needs to become a `PhysicalFileProvider`-served folder or move to blob storage-style handling if the port changes hosting model). `wwwroot/images` has just `logo.svg`.

---

## Constraints for a Blazor port

Concrete things a Blazor Server/SSR (or hybrid) design must satisfy or work around, based on the above:

1. **OAuth Challenge/Callback must stay plain HTTP endpoints, never Blazor interactive components.** `SignInManager.ConfigureExternalAuthenticationProperties` + `ChallengeResult` + `SignInManager.ExternalLoginSignInAsync`/`SignInAsync` all require a normal request/response cycle to read/write the auth cookie and the transient external-auth cookie. Keep `Login`, `ExternalLogin` (`OnGetCallbackAsync`), `LinkDiscord`'s link/unlink POSTs as either classic Razor Pages (coexisting with Blazor is fully supported in .NET 8) or minimal-API endpoints — do not try to route Discord's OAuth redirect through a Blazor circuit.

2. **Identity's built-in Account UI conventions** (`/Account/Login`, `/Account/Logout`, `/Account/AccessDenied`, cookie paths from `IdentityConfigOptions`) are referenced by `ConfigureApplicationCookie` and Discord OAuth's `OnRemoteFailure` redirect target — if these move to Blazor "Identity UI" style components (as in .NET 8's Blazor + Identity template), the redirect targets and query-string contract (`?authError=discord_unavailable|discord_expired|discord_error`, `?returnUrl=`) must be preserved exactly, since `ExternalLoginModel`/`LoginModel` build these URLs today and any replacement must match.

3. **Cookie auth + Blazor circuits**: `ConfigureApplicationCookie` (`HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`) is shared infrastructure — Blazor Server's circuit is established over the same HTTP connection that carries the auth cookie, so no change needed there, but note that **a circuit's `AuthenticationStateProvider` snapshots the principal at circuit-start**; role/claim changes mid-session (e.g. `DiscordClaimsTransformation` adding claims, or an admin changing a user's role) won't reflect in an already-open circuit without an explicit `NotifyAuthenticationStateChanged` or a forced reconnect — today's per-request pipeline picks up claim changes on the very next page load, so this is a behavior change to design around, not just port.

4. **`HttpContext` availability differs by render mode.** All three custom `IAuthorizationHandler`s (`GuildAccessHandler`, `PortalGuildMemberAuthorizationHandler`) and the audit-logging `FromIpAddress(HttpContext.Connection.RemoteIpAddress)` calls scattered through PageModels read `HttpContext` directly (some via `IHttpContextAccessor`, some via the PageModel's own `HttpContext` property). In Blazor Server, `HttpContext` is only available during prerendering/first render of a page, not during subsequent circuit interactions — any of this logic invoked from component event handlers (button clicks, etc.) after initial render needs to either capture what it needs during `OnInitializedAsync`/prerender, or move to a real endpoint. In Blazor WebAssembly, there is no server `HttpContext` at all.

5. **Guild ID resolution from route values** (`RouteValues["guildId"]` / `Query["guildId"]`) inside both `GuildAccessHandler` and `PortalGuildMemberAuthorizationHandler` assumes a classic ASP.NET Core routing pipeline evaluating `[Authorize]` before the page runs. Blazor's router resolves route parameters differently (via `@page` route templates and `[Parameter]`-bound route values on the component), and `[Authorize]` on a Razor component only integrates with `AuthorizeRouteView`/cascading `AuthenticationState`, not the ASP.NET Core authorization middleware pipeline in the same way — **these two handlers cannot be reused unmodified for Blazor page-level authorization**; the guild-ID-from-route logic needs to move into a shared method callable from `OnInitializedAsync` (or into a custom `AuthorizationHandler` operating over a `RouteData`-derived resource, which is more setup than today's simple route/query read).

6. **Resolve the `GuildAccessHandler` vs. `GuildAccessAuthorizationHandler` split before or during the port**, not after — porting the wrong (or both, inconsistently) semantics forward would bake in a latent bug. The registered one hits the live Discord gateway per authorization check (`DiscordSocketClient.GetGuild/GetUser`); the documented/tested-but-unregistered one uses cached DB tables (`UserDiscordGuild`, `UserGuildAccess`) with `GuildMembershipCacheOptions`. The DB-backed approach is more scalable and is what the docs/diagrams describe as intended — recommend consolidating onto it during the port and deleting the other, updating `GuildAccessAuthorizationHandlerTests` accordingly (already exists and passes against the DB-backed one).

7. **`PortalPageModelBase`'s three-state UX (guest landing / not-a-member forbidden / authorized full portal) must be preserved**, not flattened to a binary `[Authorize]`/redirect — this is a deliberate product pattern for the 3 public Portal pages (TTS/Soundboard/VOX) and their PortalGuildMember-policy-gated API controllers. In Blazor terms this likely means each Portal page component does its own `CheckPortalAuthorizationAsync`-equivalent call during `OnInitializedAsync` and conditionally renders one of three child views, rather than relying purely on `<AuthorizeView>`/`[Authorize]`.

8. **`IHubContext<DashboardHub>` server-push code (7 services) is UI-framework-agnostic and needs no change**, but the *client* side (currently plain JS + `@microsoft/signalr` from CDN, calling `hub.invoke(...)`/`hub.on(...)`) needs a real design decision: keep a second `HubConnection` from Blazor components (works in both Server and WASM), or fold DashboardHub's responsibilities into direct server-to-component push within the same Blazor Server circuit (only viable for Server render mode, not WASM/Auto) and let background services notify components via a scoped pub/sub instead of a separate hub. Recommend keeping the existing hub structure (groups, method names, event names) since it's already guild/feature-partitioned and works uniformly across render modes; don't merge it into circuit state unless committing fully to Blazor Server-only.

9. **No CSP exists today** — clean slate, but a Blazor port is a natural point to add one if desired. If added: `blazor.web.js`/`blazor.server.js` (and the SignalR client, if kept as a separate CDN script) need `script-src` entries; the circuit's WebSocket needs a `connect-src` entry (`wss://` to the same origin, or the nginx-proxied origin — recall `UseForwardedHeaders` already clears `KnownNetworks`/`KnownProxies` to trust any proxy, consistent with a Cloudflare/nginx front for WebSocket traffic); inline `<style>`/`<script>` in existing `.cshtml` pages (several pages have inline `<script>` blocks per the grep hits above, e.g. `AudioSettings/Index.cshtml`) would need nonces or extraction to satisfy a strict CSP — since there's no nonce infrastructure today, adding one means either a middleware that stamps a per-request nonce into `HttpContext.Items` and a matching Razor `@Html.Raw($"nonce=\"{nonce}\"")` helper, or moving all remaining inline scripts to external files first.

10. **Antiforgery coverage is inconsistent today** (`@Html.AntiForgeryToken()` on 13 specific pages, not global; JS silently omits the header when the hidden input isn't present; `[ApiController]` JSON endpoints get no automatic antiforgery check at all). A Blazor port should not silently carry this forward — Blazor Server's own circuit has a different CSRF posture (the SignalR connection itself, established over the authenticated HTTP connection, is Blazor's primary CSRF mitigation for interactive calls), but any **remaining plain HTTP POST endpoints** (the OAuth pages, and the REST API controllers if they're kept as the backing API for Blazor components calling out over `HttpClient`) still need deliberate antiforgery/CSRF decisions — don't assume Blazor's circuit protection extends to a separate `fetch()`/`HttpClient` call to `/api/...` the way it does to circuit-native event handlers.

11. **Tailwind is already primed for `.razor`** (`tailwind.config.js` content globs), so no build-pipeline change needed there, but **`dotnet build`'s Tailwind step behavior vs. the Dockerfile's explicit `npm run build:css` step** should be reconciled/verified once `.razor` files start landing in `Pages/`/`Components/`, to make sure both paths still pick up classes used only in new Razor component markup (component libraries sometimes emit class names via `@code`-block C# string interpolation, which is the same "must survive purging" scenario the existing `safelist` regexes were written for).

12. **Elastic APM / OpenTelemetry instrumentation (`app.UseAllElasticApm`, `CorrelationIdMiddleware`, `ApiMetricsMiddleware`) is all classic ASP.NET Core middleware and traces HTTP requests** — it will correctly capture the initial page-load/prerender request and any REST API calls a Blazor component makes, but **will not automatically trace in-circuit interactions** (button clicks, component lifecycle events after the first render) since those never pass back through the HTTP middleware pipeline — they travel over the already-established SignalR circuit instead. If per-interaction tracing/metrics matter for the Blazor pages (e.g. the same request-duration dashboards `ApiMetricsMiddleware` currently powers), that needs separate circuit-level instrumentation (e.g. wrapping component event handlers, or a custom `CircuitHandler`), not a reuse of the existing HTTP middleware.

13. **No end-to-end/browser test safety net exists** (no Playwright, no WebApplicationFactory-based HTTP integration tests) to catch regressions during the port — recommend standing up at least a thin Playwright smoke suite (or bUnit component tests as a minimum) *before* migrating pages, given CLAUDE.md's own testing philosophy (specs before code, lessons-learned notes) and the scale of this UI (56+ pages with `[Authorize]`, 34 controllers, 1 SignalR hub) — the 150 existing `Bot/Pages/` PageModel tests are exactly the ones the port invalidates, so their behavioral coverage needs to be re-established some other way, not just carried forward as a checklist of "does this still work" line items with nothing runnable behind them mid-migration.

14. **Two independent Razor layouts** (`_Layout.cshtml` for the admin dashboard, `_PortalLayout.cshtml` for public guild portals, `_LayoutLanding.cshtml` for the anonymous landing page) reflect three distinct audiences/auth postures (authenticated staff / guild-member self-service / anonymous public) — a Blazor port should likely map these to three distinct root layout components (or three distinct render-mode/auth boundaries) rather than one universal shell, to keep the `PortalPageModelBase` three-state UX (constraint 7) and the FallbackPolicy-authenticated-by-default posture (constraint 2/inherent in Program.cs) cleanly separated per audience.

---

# Part 6 — Gotchas recorded by the earlier unmerged branch

The islands-first attempt on `claude/blazor-ui-remaining-work-rhesdp` never merged (only its Phase 0 foundation reached `main`, and that was removed in `dbd59ce`). Its plan is not reused. The following are the concrete, version-specific gotchas its commit history recorded; each should be re-verified against current code before being relied on.


1. **Pass Discord snowflake IDs across any prerender/JS boundary as strings, never `ulong`** — this
   was called out independently in three of the four docs and enforced in every commit; it's a
   `CLAUDE.md` rule for a reason (precision loss in JS).
2. **`toast.js`/`theme.js`-style modules that declare top-level `const`s are invisible to
   `IJSRuntime`'s dotted-path resolution** (it resolves against `window`/`globalThis`, and a
   classic-script `const` is lexically scoped, not a `window` property). Any future JS interop
   needs a window-attached shim script loaded after those modules — don't rediscover this the hard
   way.
3. **Blazor Server islands must not open a second WebSocket back to the app's own SignalR hub.**
   The in-process event-bus + dual-publish pattern (existing notifiers publish to both
   `IHubContext` and the bus) is the right shape and should be reused conceptually even if none of
   the DTOs survive.
4. **`.NET 10's `blazor.server.js` resolves `_blazor/initializers` relative to the page path**,
   which silently 404s (and kills the circuit) on nested routes unless an empty initializer set is
   supplied to `Blazor.start()`. This is a version-specific regression that cost real debugging
   time on the prior attempt (Phase C) — a static test worth adding to any new attempt so it fails
   loudly instead of silently.
5. **Circuits outlive both the auth cookie and often the originating `HttpContext`.** Any new plan
   needs a revalidating auth-state provider (security stamp + lockout re-check on an interval) and
   a circuit-scoped capture of IP/user-agent taken at circuit start, not resolved lazily later.
6. **Guild-scoped authorization handlers that read `HttpContext.Request.RouteValues` return null
   inside a circuit.** Refactor them to check `context.Resource` first with a fallback to the
   existing route-value extraction — needed once *any* island or routed page does resource-based
   `AuthorizeAsync` calls, not just at full-migration time.
7. **Debounce and coalesce, explicitly and by policy, not ad hoc.** The selective plan's numeric
   rules (250–300ms input debounce, ≤1Hz coalesced re-render per real-time widget, cancel
   in-flight work with a CTS before issuing the next) were followed consistently across every
   slice and are worth adopting verbatim as a standing rule, not rediscovering per-component.
8. **Keep large/low-latency/high-frequency concerns out of the circuit by design**: Chart.js,
   audio playback, and multi-MB file upload were kept in JS from the very first plan through the
   last commit, with no wavering. Don't relitigate this in the new plan — it was right the first
   time and every subsequent doc reaffirmed it.
9. *(removed: referred to island slices that never merged)*
10. **Route-conflict discipline works and should be kept**: delete the `.cshtml` in the same commit
   that lands its Blazor replacement. Every migrated-page commit on this branch followed this and
   it kept the app buildable/shippable after every single commit — never a multi-day broken window.
11. **A parity gate (`?legacy=true`) earns its cost during islands, but becomes debt once you're
    confident** — Phase A's whole job was flipping 5 gates and deleting ~5k lines of dead JS that
    the gates had been keeping alive "just in case." A new plan should set an explicit trigger for
    when a parity gate gets removed (e.g., "N days of default-on with no regression reports") so
    it doesn't linger indefinitely.
12. **Treat visual/design-system changes to the base Razor Pages app as a hard dependency of any
    in-flight Blazor migration, not an independent workstream.** The single biggest reason this
    branch can't just be resumed is that `main` restyled the entire UI (`9df4fef`, Graphite v2)
    while the Blazor branch was mid-flight, invalidating markup that had been carefully copied
    "verbatim." **Sequence a full redesign and a Blazor migration so they don't run concurrently**
    — either finish (or freeze) the design system before starting page-by-page conversion, or
    build the component kit against design tokens/CSS variables abstract enough to absorb a
    reskin without a rewrite (Tailwind + CSS-variable tokens, which this repo already has, makes
    the latter plausible — lean into token-level theming in shared components rather than
    hardcoded utility-class combinations, so a palette/typeface change is a token edit, not a
    per-component markup rewrite).
13. **Track "how far behind main" as an explicit go/no-go signal for a long-lived feature branch.**
    This branch went 21 commits deep over what the commit dates suggest was roughly two weeks,
    while main moved 36 commits including a full visual overhaul and two large concurrent refactor
    efforts. A multi-week UI migration branch needs either frequent rebasing against main or an
    explicit understanding that it will require a "catch-up" pass before landing — don't let this
    surface only at the end.
14. **Bake component tests in from the start of the *next* attempt, not as a "Phase A" catch-up.**
    bUnit was called for by the very first selective plan (§9) and didn't land until deep into the
    completion plan's Phase A — meaning six slices' worth of shared components were built with zero component-level tests. The new plan should add bUnit alongside the very
    first shared component, not defer it.
