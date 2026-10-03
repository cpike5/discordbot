---
name: web-ui-portal
description: |
  Use this agent when working on Razor Pages, the shared component library, layouts, CSS/Tailwind styling, page-level JavaScript interactions, portal pages, the design system, error pages, or REST API controllers.
model: inherit
color: cyan
---

You are a domain expert for the **Web UI & Portal** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Shared Component Library (25+ components)
**Location:** `Bot/Pages/Shared/Components/`
- **Form Controls:** `_FormInput`, `_FormTextarea`, `_FormSelect`, `_FormToggle` (`role=switch`, unchecked posts `false`), `_RadioCardGroup` / `_RadioCard`. Validation classes are `.input-validation-error|warning|success` (the names tag helpers emit)
- **UI Elements:** `_Button`, `_Badge`, `_Card`, `_EnhancedCard`
- **Status:** `_Alert`, `_EmptyState`, `_ConnectionStatus`
- **Navigation:** `_GuildBreadcrumb`, `_CommandBreadcrumb`
- **Headers:** `_GuildHeader`, `_CommandHeader`
- **Bot Status:** `_BotStatusBanner`, `_BotStatusCard`
- **Dashboard:** `_ConnectedServersWidget` (container-query row/card layout, rows redrawn by `dashboard-actions.js`), `_DashboardWidget`. `Pages/Index.cshtml` hides what the role cannot use and closes the grid (D8); hero numbers come from `IDashboardStatsProvider` and are pushed by `IDashboardStatsBroadcaster` as `StatsUpdated` (field names in `DashboardStatsDto` = `wwwroot/js/dashboard-stats.js`; a test guards it). `dashboard-realtime.js` draws the live feed and stats only; connection state is the layout banner's job
- **Activity:** `_ActivityFeed`, `_ActivityFeedTimeline`
- **Modals:** `_ConfirmationModal`, `_TypedConfirmationModal`, `_CommandLogDetailsModal`. One layer, `wwwroot/js/quick-actions.js`: `quickActions.openDialog/closeDialog` (motion via `.qa-open`, scroll lock, `inert` background, focus trap, stacking), `showConfirmationModal`, and the Promise API `confirm/alert/typedConfirm`. Confirmation forms post to their own `action` (the partial puts `handler=` in the URL), do not follow redirects (TempData must survive), and skip forms with `data-custom-submit`. Cancel/backdrop are `data-modal-dismiss`, never inline handlers
- **Loading/empty/unsaved:** `_Skeleton`, `_SkeletonCard`, `_SkeletonTable`, `_SkeletonLines` and `wwwroot/js/skeleton.js` (`Skeleton.show` waits 300ms); `_EmptyState` and its JS twin `wwwroot/js/empty-state.js` (`EmptyState.render/filtered/error`); `wwwroot/js/unsaved-changes.js` (`data-unsaved-changes` on a form, `UnsavedChanges.markClean(form)` after a fetch save). `_Pagination` state rules live on `PaginationViewModel` (unit tested)
- **Data Cards:** `_AuditLogCard`, `_CommandStatsCard`
- **Input:** `_AutocompleteInput`
- **Previews:** `_GuildPreviewPopup`
- **Currency:** `_CurrencyWalletPanel` — holder list + ledger + the mint/fine/adjust dialog for one currency (member chosen with the user picker), filled by `wwwroot/js/currency/currency-wallets.js`. `_CurrencyManageModals` (editor + mint authorities, shared by both currency pages), `_CurrencyCard` / `_CurrencyRow` (patched in place by `currency-manage.js`, never a reload; a create clones the page's `<template>`). `CanMint`/`CanFine`/`CanAdminister` decide which actions render at all; `window.CurrencyWallets.setCurrency(id, symbol)` repoints it (the bot-wide page starts with none selected).
- **Showcase:** `Components.cshtml` — living reference, keep updated when adding components

### Layouts
- `_Layout.cshtml` — Main application layout
- `Portal/_PortalLayout.cshtml` — Portal (member-facing) layout
- `Portal/Shared/_PortalHeader.cshtml`
- `Shared/_PwaHead.cshtml` — PWA head tags (manifest, icons, `pwa.js`); every layout and `Layout = null` page that should stay installable includes it. Service worker is `wwwroot/sw.js` (static assets only, never pages/API); see `docs/articles/pwa.md`

### Portal Pages (Member-Facing, OAuth required)
- `Portal/Soundboard/Index.cshtml`, `Portal/TTS/Index.cshtml`, `Portal/VOX/Index.cshtml`
- **Mobile first (UX polish Phase 9):** under 1024px `.sidebar` is `display: contents` so the voice panel
  (a sticky bar, `_VoiceChannelPanel` with `ApiBase` set) sits above the content and the upload and clip-count
  panels follow it; one-row header under 640px; 44px targets, `--safe-*` insets, `dvh`. A guild with
  `EnableMemberPortal` off gets `Portal/Shared/_PortalDisabled.cshtml`. Members have no Identity role, so
  portal pages omit the SignalR hub scripts (`PortalPageModelBase.CanUseDashboardHub`) and the panel polls
  `GET /api/portal/soundboard/{guildId}/status`. `_PortalLayout` carries `quick-actions.js`: use
  `quickActions.confirm` for deletes, never native `confirm`/`prompt`. Offline, the dev-only member
  (`portal-member@example.com`, see audio-voice.md) is the account to verify with.
- **Inline scripts externalized (2026-09):** these pages no longer carry large inline `<script>`
  blocks — logic moved to dedicated files in `wwwroot/js/`: `portal-vox.js` (VOX composer, clip
  browser, A-Z rail, history/favorites — was ~1,190 lines of inline script across two `<script>`
  blocks), `portal-tts-inline.js` (mobile voice-settings toggle, SignalR hub connect, keyboard
  shortcuts), `portal-soundboard-inline.js` (bootstrap + `UserPreferences.init`). Each page keeps
  one tiny inline `<script>` that only sets a `window.portal<Name>Config = {...}` object (Discord
  IDs as strings — see CLAUDE.md snowflake gotcha) before loading the external file with the same
  `asp-append-version="true"` convention as other scripts. `portal-tts.js` and
  `portal-soundboard.js` (the larger, pre-existing shared modules) were not touched.

### Error Pages
- One page for every status code: `Pages/Error/Index.cshtml` (`/Error/{statusCode}`, `ErrorPageModel`) on `Shared/_ErrorLayout.cshtml` (standalone, themed, `<main>`). Handles every verb and ignores antiforgery, because the status-code middleware re-executes the failed request with its own method. Shows the failed address; script requests get problem JSON. Copy lives in `ErrorPageModel.Describe`.

### Feedback (UX plan D1)
- **Toast** for action results: `toast.success/error/warning/info(msg, { action })` in JS (`wwwroot/js/toast.js`; old `ToastManager.show` / `quickActions.showToast` / `showToast` / `Toast.show` shapes alias to it), `TempData.SetSuccessToast(...)` etc. in page handlers (`Extensions/TempDataExtensions.cs`). `_ToastContainer` (in `_Layout` and `_PortalLayout`, never in pages) renders queued toasts as JSON.
- **`_Alert`** for persistent page state (load failure, degraded): a plain `ErrorMessage` property, never `[TempData]`. Dismiss is global (`data-alert-dismiss`), `role="status"`.
- **Requests from scripts** go through `ApiClient`: session-expiry toast with Sign in, plain-language errors (no 5xx `detail`), 30s timeout, never HTML as data. The server answers script requests (`/api`, `/hubs`, `X-Requested-With`) with 401/403 problem JSON instead of redirecting (`IdentityServiceExtensions`, `HttpRequestExtensions.IsScriptRequest`).
- **Server-posted forms** get `data-submit-guard` (`loading-manager.js`).

### Formatting and live status (UX plan D6, Phase 4)
- **One formatter.** `wwwroot/js/format.js` (`window.Format`: `formatDate`, `relativeTime`, `plural`, `number`, `duration`, `currency`; browser locale and 12/24h, a zone-less timestamp is UTC) and `Helpers/DisplayFormat.cs` (server twin: `Time(...)` renders a `<time>` with a UTC fallback, plus `Iso`, `Plural`, `Number`, `Duration`, `Currency`). Do not add another date or "time ago" function. `<time data-relative-time="…">` auto-refreshes and shows the absolute time on hover and focus. API: `docs/articles/component-api.md` § Formatting.
- **Dates from the server** go in `data-utc="…" data-format="…"` (`Iso(value)` from C#, never `ToString("o")` on an `Unspecified` value); `timezone.js` converts them, including content inserted later (MutationObserver; `timezoneUtils.scan(root)`).
- **Date presets** come from `DateRangeFilter.presetRange` (`date-range-filter.js`, local calendar). Pages with their own `toISOString()` presets are wrong in the local evening and should move over.
- **Live connection.** `DashboardHub` retries forever (fast, then every 25 to 35 s; `retryNow()`); states `connecting | connected | reconnecting | disconnected`; a failed first attempt is `reconnecting`, and recovery raises `connected` and `reconnected` (rejoin groups there). `_ConnectionBanner` (layout) and any `[data-stale-badge][hidden]` follow the hub; the sidebar footer follows the bot (`bot-status-refresh.js`, `BotStatus.apply`). They are different things: in offline mode the hub is up and the bot is offline.
- **Nav chrome.** `navigation.js` makes the closed mobile drawer `inert`, so it has no Tab stops; Escape acts only on an open menu or drawer; `<main id="main-content" tabindex="-1">` takes focus from the skip link; the user menu is a disclosure, not `role=menu`.

### REST API Controllers (37)
**Location:** `Bot/Controllers/` — JSON API endpoints for Razor Pages frontend and external consumers.

**Portal TTS** (was one 1,834-line `PortalTtsController`) is split by sub-resource, sharing `PortalTtsControllerBase` (playback-tracking state, `IsAudioGloballyEnabledAsync`, `SendTtsCoreAsync`, SSML/WAV synthesis helpers):
- `PortalTtsPlaybackController` — status, send, voice channels, join/leave, stop (`api/portal/tts/{guildId}`)
- `PortalTtsSynthesisController` — SSML validate/synthesize/build, voice capabilities
- `PortalTtsPresetsController` — built-in and custom style presets, preview
- `PortalTtsHistoryController` — message history, replay (via `SendTtsCoreAsync`), favorite, delete

**Portal Soundboard** (was one 1,172-line `PortalSoundboardController`) is split the same way, sharing `PortalSoundboardControllerBase` (`IsAudioGloballyEnabledAsync`):
- `PortalSoundboardSoundsController` — list/upload/download/delete sounds
- `PortalSoundboardPlaybackController` — play sound, voice channels, join/leave, stop, status
- `PortalSoundboardFavoritesController` — list/add/remove favorites
- `PortalSoundboardCategoriesController` — CRUD categories, assign sound to category

**PerformanceMetricsController** keeps its 9 endpoints as thin pass-throughs to existing metrics services; the historical/statistical calculation logic (time-range bucketing, database/memory history statistics, command error-rate aggregation, overall cache stats) moved to `IPerformanceMetricsQueryService` (`Core/Interfaces`) / `PerformanceMetricsQueryService` (`Bot/Services/Performance`, registered scoped in `PerformanceMetricsServiceExtensions`).

**Thin page models via aggregators/section services** (`Bot/Interfaces`, implementations under `Bot/Services/<Area>`, registered scoped in `ApplicationServiceExtensions`/`PerformanceMetricsServiceExtensions`): the three heaviest page models were split so the `.cshtml.cs` files stay request routing + view-model assembly only, with data aggregation and audit logging in services.
- `Pages/Guilds/Details.cshtml.cs` (14 deps → 4) delegates to `IGuildDetailsAggregator` (`Bot/Services/Guilds/GuildDetailsAggregator`), which returns one `GuildDetailsAggregateDto` covering the guild record plus every widget (welcome, scheduled messages, rat watch, reminders, members, audio, assistant). `IGuildService`/`IGuildMembershipService` stay on the page model for `OnPostSyncAsync` and the `CanEdit` check.
- `Pages/Admin/Settings.cshtml.cs` (7 deps → 4) delegates to `ISettingsSectionService` (General/Features/Advanced/Commands save+reset+audit log), `IAppearanceSettingsService` (Appearance tab: SuperAdmin check, theme list/save/reset, all under `Bot/Services/Settings`), and `IBotControlService` (Bot Control tab: status view model, restart/shutdown + audit log). All handler names (`asp-page-handler` values) and JSON response shapes are unchanged; save/reset operations share a `SettingsSectionResult { Success, Message, Errors, RestartRequired, StatusCode, ThemeName }` return type.
- `Pages/Admin/Performance/Index.cshtml.cs` (12 deps → 2) delegates every tab builder (`overview`/`health`/`commands`/`api`/`system`/`alerts`) to `IPerformanceDashboardAggregator` (`Bot/Services/Performance/PerformanceDashboardAggregator`), which reuses the same per-tab logic that already lived, independently, in the sibling `CommandsModel`/`HealthMetricsModel`/`ApiMetricsModel`/`SystemHealthModel`/`AlertsModel` page models (those were left as-is — still separately thin — this task only touched the shell page).

When adding a new section/tab to Settings or the Performance dashboard, or a new widget to Guild Details, add the data-fetch to the matching aggregator/section service (with a unit test covering happy path + one failure path) rather than back into the page model.

### Virtual Currency Portal (PR 5)
- **Pages:** `Pages/Guilds/Currency/{Index,Details,Prices}.cshtml` (routes `/Guilds/{guildId}/Currency`, `.../Currency/{currencyId}`, `.../Currency/Prices`) and `Pages/Admin/Currency/Index.cshtml` (`/Admin/Currency`, SuperAdmin). Guild pages inherit `GuildPageModelBase`; nav tab id `currency` (order 7) in `GuildNavigationConfig`.
- **Controllers:** `CurrenciesController`, `WalletsController`, `PricesController`, all on `CurrencyControllerBase` (feature-off 404, `CurrencyErrors` → HTTP status with `errorCode` on the body, and `ResolveCurrencyAsync`, which loads the currency and checks access in one step).
- **Feature switch:** the currency services are registered only when `Currency:Enabled` is true, so every currency page model and controller takes them as **optional constructor arguments defaulting to null** and answers 404 when they are absent. Do not "fix" that by making them required — the DI container has nothing to give.
- **Authorization:** `GuildAccess` only works where the route carries a `guildId`. Currency-keyed routes ask `ICurrencyAccessService` (`Bot/Authorization/CurrencyAccessService`) instead: None / Read / Moderate / Administer. A currency the caller cannot see answers 404, not 403. The detail page asks the same seam for its button flags, so the UI and the API cannot drift apart.
- **Feature keys:** the Prices page writes entries under `CurrencyFeatureKeys.Soundboard(soundId)`. The charge seam and the portal price badge look a sound up by that exact string, so the page renders the key onto the row and the script sends it back untouched — never rebuild a key in JavaScript.
- **Page scripts:** `wwwroot/js/currency/` — `currency-manage.js` (create/edit/deactivate/authorities, shared by both list pages), `currency-wallets.js`, `currency-prices.js`, `currency-reconcile.js`. All talk to the API through `window.ApiClient`.

### Commands, Search and tabs (UX plan Phase 6)
- **Commands page** (`Pages/Commands/*`, `docs/articles/unified-command-pages.md`): one controller, `wwwroot/js/commands-page.js`, owns the tab, filters, page, command-list search and open log, all in the query string under the page model's names (`tab`, `StartDate`, `EndDate`, `GuildId`, `CommandName`, `StatusFilter`, `SearchTerm`, `pageNumber`, `q`, `log`). One request per Apply; abort, 300 ms skeleton, Retry that keeps the filters. The old `command-tab-loader/filters/pagination` and `url-state` modules are gone. `/api/commands/*` refuses with problem JSON; the script reads it through `ApiClient.getHtml`.
- **`tab-panel.js`** ignores a hash (or stored id) that is not one of its container's own tab ids, so `#main-content` from the skip link no longer blanks the panels. `nav-tabs.js` already did.
- **Search** renders its validation message, counts every match (`SearchResultsViewModel.TotalMatches`), and links command logs to the Details page only for moderators (`CanOpenCommandLogDetails`); viewers get `/Commands?tab=execution-logs&log=<id>`.
- **CommandLogs/Details** takes `?returnUrl=` for Back (sanitised).

### Design System ("Graphite", v2.0 — `docs/articles/design-system.md`)
- **Tokens live in `wwwroot/css/site.css`**; `tailwind.config.js` only maps utilities onto them. Every colour has an RGB triplet (`--color-x-rgb`) so `bg-success/20` follows the theme. Never hard-code hex — use `var(--color-…)` in CSS/`<style>` blocks and the token classes in markup.
- **Accents have jobs**: ember (`accent-orange`) = selected/active/primary; signal blue (`accent-blue`) = links/info/focus. Semantic colours are soft tints (12% fill + hairline) except on buttons.
- **Inks and fills**: each accent/semantic colour has an ink (`--color-success`, text and lines) and a darker fill (`--color-success-fill`, `-fill-hover`, `-fill-active`) that white text reads on. Tailwind `bg-*` resolves to the fill, every other utility to the ink. Text on a warning fill is `text-on-warning`, never white. `DesignTokenContrastTests` checks every pairing at 4.5:1 in both themes, so a token change that breaks contrast fails the build.
- **Themes**: Graphite (dark, `:root`, key `discord-dark`) and Purple Dusk (light, key `purple-dusk`). Layouts and standalone pages need `theme-root` on `<html>` (`ThemeRootTagHelper`) and `<partial name="_ThemeHead" />`; with no saved choice the page follows `prefers-color-scheme`. `_ThemeToggle` + `theme.js` switch without reload and dispatch `themechange`. Charts take their chrome colours from `chart-theme.js`; never hard-code them.
- **Touch**: `viewport-fit=cover` with `--safe-*` inset tokens in the shell; under `pointer: coarse` inputs are 16px and shared controls 44px. Row buttons use `.row-actions` (visible on focus and on hover-less devices), not `opacity-0 group-hover:opacity-100`.
- **Fonts**: `font-display` (Bricolage Grotesque) for headings and big numbers, `font-sans` (DM Sans) body, `font-mono` (JetBrains Mono) for IDs, versions, metrics and micro-labels. The Google Fonts `<link>` must be present in any `Layout = null` page.
- **Shell**: full-height `.sidebar-redesign` rail + `.topbar` offset by `--sidebar-width`; collapse is `html.sidebar-collapsed`. Page content goes inside `.page-container`; page headers use `.page-header` / `.page-eyebrow` / `.page-title` / `.page-subtitle` / `.page-actions`.
- **Prefer component classes over utility soup**: `.btn btn-*`, `.card`, `.surface`, `.form-input`, `.form-select`, `.badge badge-*`, `.alert alert-*`, `.table-*`. Radius: controls `rounded-md`, panels `rounded-lg`.
- **Rebuild CSS after touching `site.css` or class names**: `cd src/DiscordBot.Bot && npm run build:css` (also runs on `dotnet build` unless `SkipTailwind=true`). Runtime-composed classes need the `safelist` in `tailwind.config.js`.

### Client-Side Stack
- **Tailwind CSS** — Utility-first styling
- **Vanilla JS modules** in `wwwroot/js/` — one file per page or widget; partial views are fetched with `fetch()` for tab loading and AJAX sorting
- **SignalR** — Real-time dashboard updates

### User/Guild Preview Popups
Loaded globally in `_Layout.cshtml`:
```razor
<span class="preview-trigger" data-preview-type="user"
      data-user-id="@item.UserId" data-context-guild-id="@Model.GuildId">@item.Username</span>
<span class="preview-trigger" data-preview-type="guild"
      data-guild-id="@item.GuildId">@item.GuildName</span>
```

## Gotchas

- **Discord Snowflake IDs in JavaScript:** Always treat as strings — `'@Model.GuildId'` not `@Model.GuildId`
- **User text in markup:** never inside an inline handler (`onclick="f('@name')"` is XSS: the browser decodes the entity first) — use `data-*` + `this.dataset`. JS-built markup uses `textContent` or `SafeHtml.escape` (`wwwroot/js/safe-html.js`). Full rules: `docs/architecture/patterns.md` § User Data in Markup and Scripts
- **Large controllers:** AnalyticsController (698) — search specific methods. PortalTts and PortalSoundboard controllers were split by sub-resource (see REST API Controllers above); PerformanceMetricsController's calculation logic moved to `IPerformanceMetricsQueryService`.
- **Preview popups** loaded globally — use `preview-trigger` classes for user/guild names
- **Tailwind purge:** Ensure dynamically generated classes are in Tailwind content config
- **Partial-view endpoints** (fetched by page JS) return HTML fragments, not full pages — don't include layout
- **Portal pages** use `_PortalLayout` — don't mix admin and portal layouts
- **Currency DTO snowflakes** (`WalletDto.UserId`, `PriceEntryDto.ExemptRoleIds`, `MintAuthorityDto.PrincipalId`, …) carry `[JsonNumberHandling(WriteAsString | AllowReadingFromString)]` so they cross into JavaScript as strings. Keep that attribute on any new ID field a page script touches.
- **Form patterns:** Follow conventions in `form-implementation-standards.md` — validation, error display, CSRF tokens
- **Pagination route values:** never `page` (`asp-route-page`, `RedirectToPage(new { page = … })`): Razor Pages reserves it for the page name, so the link loses the page number or the redirect throws. Bind and link `pageNumber`, as `PaginatedPageModel` does
