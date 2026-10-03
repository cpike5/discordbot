# UI Inventory

**Version:** 1.1
**Last Updated:** 2026-10-03
**Target Framework:** .NET 8 Razor Pages with Tailwind CSS

---

## Overview

This document provides a comprehensive inventory of all UI components, pages, and layouts in the Discord bot project. Use this as a quick reference to understand what UI building blocks exist without diving into the codebase.

For detailed component documentation, see [Component API Usage Guide](../articles/component-api.md).

---

## Razor Page Routes

### Public/Landing Pages

| Route | File | Purpose |
|-------|------|---------|
| `/landing` | `Pages/Landing.cshtml` | Unauthenticated landing page (an anonymous visit to `/` is redirected here): themed (follows the OS), `<main>`, nav with theme toggle and a Sign in link at every width, nine current feature cards, real repository links |
| `/` | `Pages/Index.cshtml` | Authenticated home/dashboard. Hero cards (`data-stat-*`) follow the `StatsUpdated` hub event and `?handler=Stats`; Connected Servers is Moderator+, Audit Log and Quick Actions are Admin (a Viewer's grid closes up: the timeline takes the full row and an empty Quick Actions card is not rendered); Restart and Sync All ask first (`restartBotModal`, `syncGuildsModal`) and `dashboard-actions.js` follows up (restart: banner reads "Restarting" until the bot is back; sync: servers and numbers refresh). Handlers: `Stats` (GET JSON), `ConnectedServers` (GET JSON, Moderator+), `RestartBot` and `SyncAllGuilds` (POST, Admin) |
| `/Components` | `Pages/Components.cshtml` | Living component showcase (`components-showcase.js`): every shared primitive in every state. Keep it updated when a component changes |

### Account Pages

| Route | File | Purpose |
|-------|------|---------|
| `/Account/Login` | `Pages/Account/Login.cshtml` | Sign in with Discord or email: one h1, `autocomplete="username"`, pending state (`data-submit-guard`), focus on the first error (`form-focus.js`), form-level errors in one alert |
| `/Account/ExternalLogin` | `Pages/Account/ExternalLogin.cshtml` | External OAuth flow handler |
| `/Account/LinkDiscord` | `Pages/Account/LinkDiscord.cshtml` | Link Discord account to profile: pending states on every form, code length from `Verification:CodeLength`, live expiry countdown (`link-discord.js`), IDs offered as copy buttons instead of text |
| `/Account/Logout` | `Pages/Account/Logout.cshtml` | Sign out: POST signs out; GET auto-submits a sign-out POST when signed in, else confirms "signed out" (standalone page) |
| `/Account/AccessDenied` | `Pages/Account/AccessDenied.cshtml` | Authorization failure page in plain language, with Sign out (POST) |
| `/Account/Lockout` | `Pages/Account/Lockout.cshtml` | Account lockout notification; the duration comes from the Identity lockout configuration |
| `/Account/Privacy` | `Pages/Account/Privacy.cshtml` | Consent switches (`_FormToggle`, confirmed, redirect back to the row), export with a real download button (served by the authenticated `?handler=DownloadExport&id=` handler, owner only), typed-confirm delete over `ApiClient` (`privacy.js`) |
| `/Account/Profile` | `Pages/Account/Profile.cshtml` | User profile: local dates, theme radio cards with "Match my system" (clears the saved choice) |

### Admin Pages

| Route | File | Purpose |
|-------|------|---------|
| `/Admin/Settings` | `Pages/Admin/Settings.cshtml` | Tabbed application settings on the shared tab panel (`settingsTabs`): General, Features, Commands, Advanced, Bot Control, AI Models, and Appearance for SuperAdmins. The active tab is `?category=` (an unknown or not-allowed value opens General; `SettingsModel.ResolveTab`), kept current by `settings.js`, so a reload or shared link keeps the tab. Every tab that saves is its own `<form data-settings-form="{tab}" data-settings-handler data-unsaved-changes>`; **Save** posts only that tab (`SettingsSectionService.SaveCategoryAsync` also drops keys that belong to another category) and **Save all changes** saves exactly the tabs `unsaved-changes.js` reports dirty, one request each (Commands via `SaveCommandModules`, Appearance via `SaveAppearance`). Save answers with `changeCount`, so an unchanged save reads "Nothing changed" (info), not a success; a failed save shows an `_Alert` in the tab. A tab with unsaved edits gets a dot. Reset buttons open static confirm modals (`reset-{tab}`, `reset-all`) that post to the page, which redirects with a toast (the page reloads once). When a save needs a restart, the always-rendered `_RestartBanner` is revealed in place. **Bot Control** polls `/api/bot/status` through `ApiClient` while the tab is visible (a failure shows an `_Alert` with Try now and marks Last updated); Restart follows the dashboard flow (`quickactions:confirmed` then `BotStatus.watchRestart()`); Shutdown stops polling and shows a notice. The **AI Models** tab (`wwwroot/js/llm-models.js`) has two parts: an **editable per-mode defaults panel** (guild assistant, DM assistant, feature requests: one `<select>` per mode, options limited to enabled catalog models, source and status badges, price/context detail) saved like any other tab (`?handler=SaveCategory&category=AiModels`; `SettingCategory.AiModels`, so audit logging and reset are the normal ones), and the **model catalog table** (filters, sortable columns, a per-model `_FormToggle`-style switch that saves at once, "Refresh from OpenRouter"), which is not a `SettingCategory` and talks to `LlmModelsController` (`api/admin/llm-models`) directly. Saving a default takes effect on the next message with no restart (`ILlmModelResolver` cache invalidated by `ISettingsService.SettingsChanged`). |
| `/Admin/Users` | `Pages/Admin/Users/Index.cshtml` | User management list. Labelled filters (search, role, status, Discord), a filter-aware empty state with "Clear filters", a "Locked out" badge, and a Disable / Enable row action (confirm before disabling; not offered on your own row; returns to the same filtered page). The service has no delete or unlock operation, so the page offers none. |
| `/Admin/Users/Create` | `Pages/Admin/Users/Create.cshtml` | Create new user. `new-password` on both password fields, the chosen role and the welcome-email choice survive a failed submit, service failures land on their field (`CreateModel.FieldForError`), focus moves to the first error (`form-focus.js`), unsaved input warns. |
| `/Admin/Users/Edit?id={id}` | `Pages/Admin/Users/Edit.cshtml` | Edit user details. Breadcrumb Users > email (Details) > Edit; a reset shows the one-time temporary password with Copy password and Done (`user-edit.js`). |
| `/Admin/Users/Details?id={id}` | `Pages/Admin/Users/Details.cshtml` | User details view |
| `/Admin/AuditLogs` | `Pages/Admin/AuditLogs/Index.cshtml` | Old address: redirects to the Audit tab of `/admin/logs`, and `?handler=Export` to its export, keeping the filters |
| `/Admin/AuditLogs/Details/{id}` | `Pages/Admin/AuditLogs/Details.cshtml` | Audit log entry details. Back and the breadcrumb follow `returnUrl` (the Logs tab with its filters and page) |
| `/Admin/MessageLogs` | `Pages/Admin/MessageLogs/Index.cshtml` | Message log viewer |
| `/Admin/MessageLogs/Details/{id}` | `Pages/Admin/MessageLogs/Details.cshtml` | Message details. Back follows `returnUrl` (the Messages tab with its filters and page); long content wraps |
| `/Admin/Performance` | `Pages/Admin/Performance/Index.cshtml` | Performance dashboard shell. Tabs overview, health, commands, api, system, alerts; `?tab=` and `?hours=` in the query string; "Live" only on tabs subscribed to a hub group (overview and health: `performance`, system: `system-health`, alerts: `alerts`), "Updated X ago" elsewhere |
| `/admin/performance/{SystemHealth,ApiMetrics,HealthMetrics,Alerts,Commands}` | `Pages/Admin/Performance/*.cshtml` | Retired standalone pages: temporary (302) redirects to `/Admin/Performance?tab=…` so old links, bookmarks and stored notification links keep working |
| `/Admin/Logs` | `Pages/Admin/Logs/Index.cshtml` | Message and audit logs. Tabs are page navigation (`?tab=messages|audit`): only the active tab's data is read, and a failed read shows an error state with Retry instead of an empty table. Audit export (`?handler=Export`) streams CSV in pages of 100, capped at 10,000 rows (the file name says `-first-10000` when cut), with UTC column headers. `wwwroot/js/logs-page.js` drives the filter toggle, row expanders and the message "Show more". The disabled Application tab is gone (decision D11). |
| `/Admin/Notifications` | `Pages/Admin/Notifications/Index.cshtml` | Notification center. A list with no filters shows the last 7 days as a clearable chip (`?AllTime=true` clears it). Mark read, delete and bulk actions update the list in place (`wwwroot/js/notification-history.js`, no reload); Delete all sends the list's own filters and asks for typed confirmation; Mark all read asks nothing. |
| `/Admin/BulkPurge` | `Pages/Admin/BulkPurge.cshtml` | Bulk data purge (SuperAdmin). Criteria are a GET (`?Preview=true&EntityType=...`), so refresh and Back only preview again; the purge is a POST that always redirects (post/redirect/get) and the outcome shows once from TempData. Record types are `_RadioCardGroup` radios with plain names (`[Display]` on `BulkPurgeEntityType`, read by `DisplayName()`; counts use `DisplayFormat.Plural`, and a server filter shows its name); the purge button is under `data-submit-guard`, asks for CONFIRM through `quickActions.typedConfirm` (`purge.js`, then `requestSubmit`) and a progress panel follows the `BulkPurgeProgress` hub event. |
| `/Admin/UserPurge` | `Pages/Admin/UserPurge.cshtml` | User data purge (SuperAdmin). Preview is a GET; the purge redirects to the bare page and shows its result once. The Discord username (from `IUserRepository`) is in the preview and the typed confirm (which still requires the user ID), data names are plain (`PurgeDisplay.CountLabel`). |
| `/Admin/RatWatchAnalytics` | `Pages/Admin/RatWatchAnalytics.cshtml` | RatWatch analytics dashboard |
| `/Admin/Currency` | `Pages/Admin/Currency/Index.cshtml` | Bot-wide currencies, including the credit that backs paid features: create, edit, deactivate, mint authorities, and the shared wallet/ledger panel across every guild. Rows are `_CurrencyRow`, patched in place like the guild cards; Mint stays disabled until a currency is chosen. SuperAdmin only; sidebar entry "Currency" in the Administration group. |
| `/Admin/LlmUsage` | `Pages/Admin/LlmUsage.cshtml` | Portal-wide LLM token/cost usage dashboard — date-range/guild/mode filters, hero totals, breakdowns by user/model/mode/day (rendered server-side from `ILlmUsageRepository`), and a per-user drill-down of raw ledger rows fetched client-side (`wwwroot/js/llm-usage.js`) from `LlmUsageController` (`api/admin/llm-usage/records`). Each user is a real button; the drill-down has loading, empty and error states, aborts a request a newer one replaces, and shows times in the viewer's zone. The date range is the viewer's own calendar days (sent as `UserTimezone`); the daily breakdown groups by UTC day and says so. Sidebar entry "LLM Usage" in the Administration group. |

### Guild Pages (Per-Server Management)

| Route | File | Purpose |
|-------|------|---------|
| `/Guilds` | `Pages/Guilds/Index.cshtml` | Servers list: search, filters, `_Pagination` (`pageNumber`, one count), table from `md` and cards below, per-row Sync in place through `guild-sync.js` (`ApiClient`, no reload), confirmed Sync all form (PRG, TempData toast), error and filtered-empty states. Overview widgets are on `Guilds/Details` |
| `/Guilds/Details/{guildId}` | `Pages/Guilds/Details.cshtml` | Server overview: one `GuildDetailsAggregator` call fills `_DashboardWidget` cards (welcome, scheduled messages, Rat Watch, reminders, members, audio, assistant); a section that fails to load shows a retry widget (Retry reloads the page) while the rest render. `OnPostSyncAsync` syncs the one guild |
| `/Guilds/AudioModerationLog/{guildId}` | `Pages/Guilds/AudioModerationLog/Index.cshtml` | Audio moderation log: filters (user filter accepts a pasted `<@id>`; an unreadable user or reversed dates are refused, not ignored), `_Pagination` |
| `/Guilds/Welcome/{guildId}` | `Pages/Guilds/Welcome.cshtml` | Welcome settings: `_FormToggle`/`_FormSelect`/`_FormTextarea`, `data-section-gate` (inert while off), saved channel the bot cannot see stays selected with a warning, preview through `discord-markdown.js`, `data-unsaved-changes`, a failed save keeps the chrome and the input |
| `/Guilds/Edit/{guildId}` | `Pages/Guilds/Edit.cshtml` | Server settings: audio fields are saved only when the form was drawn with the real audio settings (`Input.AudioSettingsLoaded`), a warning with reload otherwise; `data-submit-guard`, `data-unsaved-changes` |
| `/Guilds/{guildId}/Members` | `Pages/Guilds/Members/Index.cshtml` | Member directory: filters, `bulk-selection.js` selection, `?handler=Export` CSV (current filters, or `UserIds` for a selection), card layout under `md` |
| `/Guilds/{guildId}/Members/{userId}/Moderation` | `Pages/Guilds/Members/Moderation.cshtml` | Moderation profile: cases, notes, flags on `_TabPanel` (hash), tag chips with real remove buttons, `ApiClient` notes and tags without reloads |
| `/Guilds/ModerationSettings/{guildId}` | `Pages/Guilds/ModerationSettings/Index.cshtml` | Moderation rules: `_TabPanel` (hash), one `data-unsaved-changes` form per tab, changed-fields-only saves, confirmed presets, template import dialog |
| `/Guilds/Reminders/{guildId}` | `Pages/Guilds/Reminders/Index.cshtml` | Reminders: stats, status filter with Apply and Clear filters, table from `md` and cards below, cancel as a confirmed form (`confirm-forms.js`), names from `IDiscordUserResolver` (one lookup per page), `_Pagination` |
| `/Guilds/ScheduledMessages/{guildId}` | `Pages/Guilds/ScheduledMessages/Index.cshtml` | Scheduled messages: table from `md`, cards below, `.row-actions` and `.row-action-btn` (44px on touch), pause/resume and confirmed delete as forms, `_Pagination` (`pageNumber`) |
| `/Guilds/ScheduledMessages/Create/{guildId}` | `Pages/Guilds/ScheduledMessages/Create.cshtml` | Create scheduled message: shared form `_MessageEditor.cshtml` (`_RadioCardGroup` schedule type, preview through `discord-markdown.js`, `scheduled-message-editor.js`) |
| `/Guilds/ScheduledMessages/Edit/{guildId}/{id}` | `Pages/Guilds/ScheduledMessages/Edit.cshtml` | Edit scheduled message: same `_MessageEditor.cshtml`; a failed save shows the next-run time as typed (never converted again), Delete is a confirmed form beside Save |
| `/Guilds/Analytics/{guildId}` | `Pages/Guilds/Analytics/Index.cshtml` | Analytics overview: `_DateRangeFilter`, charts on `ChartTheme` (`server-analytics.js`), a data table behind every chart, load failure as an error state with Try again |
| `/Guilds/Analytics/Engagement/{guildId}` | `Pages/Guilds/Analytics/Engagement.cshtml` | Engagement metrics (`engagement-analytics.js`); the retention funnel is text over proportional bars and works at 320; no placeholder cards |
| `/Guilds/Analytics/Moderation/{guildId}` | `Pages/Guilds/Analytics/Moderation.cshtml` | Moderation analytics (`moderation-analytics.js`); escalation steps carry the action name, not just a letter |
| `/Guilds/FlaggedEvents/{guildId}` | `Pages/Guilds/FlaggedEvents/Index.cshtml` | Flagged events: filters (no forced date window), `_Pagination` (`pageNumber`), bulk and row review as confirmed form posts with TempData toasts, cards under `md` |
| `/Guilds/FlaggedEvents/Details/{guildId}/{id}` | `Pages/Guilds/FlaggedEvents/Details.cshtml` | Flagged event details: acknowledge, dismiss and record outcome (also for acknowledged events), user-history links |
| `/Guilds/RatWatch/{guildId}` | `Pages/Guilds/RatWatch/Index.cshtml` | RatWatch settings tab: `_FormSelect`/`_FormInput`/`_FormToggle` settings form (a stored time zone the list does not know stays selectable), row actions through `quickActions.confirm` (`rat-watch-manage.js`), `_Pagination` on `pageNumber`, cards under `md` |
| `/Guilds/RatWatch/Analytics/{guildId}` | `Pages/Guilds/RatWatch/Analytics.cshtml` | RatWatch analytics tab (`rat-watch-analytics.js`), leaderboards in `_TabPanel` |
| `/Guilds/RatWatch/Incidents/{guildId}` | `Pages/Guilds/RatWatch/Incidents.cshtml` | Incident browser: filters, `_Pagination` on `pageNumber`, incident dialog on `quickActions.openDialog` (`rat-watch-incidents.js`), `?handler=ExportCsv` exports every row the filters select (UTC, formula-guarded, capped at 10,000), cards under `md` |
| `/Guilds/AssistantSettings/{guildId}` | `Pages/Guilds/AssistantSettings.cshtml` | AI assistant configuration: enable toggle, channel allow-list, per-guild tool checklist (grouped by `ToolCatalog` category; an empty selection means the house default set and the page says so), and rate-limit override. Switch-gated section is `inert` with the reason beside it, saved channels the bot cannot see stay ticked, a failed save keeps the ticks and the chrome, `data-unsaved-changes` |
| `/Guilds/AssistantMetrics/{guildId}` | `Pages/Guilds/AssistantMetrics.cshtml` | Assistant usage metrics (daily `AssistantUsageMetrics` aggregates) plus a **Cost by User** table sourced from the `LlmUsageRecord` ledger via `ILlmUsageRepository` (injected directly into `AssistantMetricsModel`, top 20 spenders over the same 30-day window, filtered by guild) and a **Tool Usage** table counted from `AssistantInteractionLog.ToolNames` via `IAssistantInteractionLogRepository.GetToolUsageAsync`, paired with `ToolCatalog` so tools that were never called still get a row, and a **Prompt Surface** panel from `IPromptSurfaceReporter` showing what the guild's advertised tool array costs per request (per-tool schema size and share of the prefix, with the tools the guild has turned off and the ones held back by a skill marked as not sent). The page lists no recent-interaction rows, so there is no `Model` column to add here.. Reads `IAssistantTelemetryReader` (registered without an API key), so the page opens without one: an "Assistant not configured" alert, "no data yet" figures (no red zeros), and a retry state when the load fails |
| `/Guilds/Soundboard/{guildId}` | `Pages/Guilds/Soundboard/Index.cshtml`, `_SoundsList.cshtml` | Soundboard management. Sort re-renders the list in place (`ajax-sort.js`; keeps the old list and offers Retry on failure; Back and Forward restore the sort). Rows are cards under `sm`; category selects are rendered on the server. Category create/rename/delete/assign are admin page handlers (`?handler=CreateCategory` etc.), not the member portal endpoints, so they work while the portal is off. Upload takes several files at once, checks type, size and free slots in the browser, and shows progress and a result per file; delete uses `quickActions.confirm` and removes the row in place. The voice panel shows now-playing and Stop. Script: `wwwroot/js/soundboard-admin.js`. |
| `/Guilds/AudioSettings/{guildId}` | `Pages/Guilds/AudioSettings/Index.cshtml` | Audio feature settings: one form, one sticky Save (`?handler=SaveAll`, field-keyed errors, `data-unsaved-changes`), whole-number validation in the browser and on the server, command permissions as a native `<details>` checkbox list (keyboard operable), Reset repopulates in place. Script: `wwwroot/js/audio-settings.js`. |
| `/Guilds/TextToSpeech/{guildId}` | `Pages/Guilds/TextToSpeech/Index.cshtml` | TTS: Send and Preview (in the browser, `?handler=Preview`) use the voice, speed, pitch, volume and style on screen and, in Pro mode, the SSML built from the message; "Save as server defaults" is the only thing that changes what `/tts` uses. History delete uses `quickActions.confirm`. Script: `wwwroot/js/tts-page.js`. |
| `/Guilds/VOX/{guildId}` | `Pages/Guilds/VOX/Index.cshtml` | VOX clip library: read-only settings, group tabs and search in the query string, `_Pagination`, Rescan with a submit guard. No Play button (there is no endpoint that serves a clip to the browser). |
| `/Guilds/PublicLeaderboard/{guildId}/Leaderboard` | `Pages/Guilds/PublicLeaderboard.cshtml` | Public member leaderboard (standalone, `Layout = null`): in-page states for not found (404), members only (403), bot offline and load failure (503) with Try again, never an empty board; relative times through `format.js`/`timezone.js`; the rat animation only without reduced motion |
| `/Guilds/FeatureRequests/{guildId}` | `Pages/Guilds/FeatureRequests/Index.cshtml` | Feature requests: status filter, table from `md` and cards below, `_Pagination` (`pageNumber`), submitters by name |
| `/Guilds/FeatureRequests/Details/{guildId}/{id}` | `Pages/Guilds/FeatureRequests/Details.cshtml` | Feature request: Approve with optional notes, Reject through a dialog that requires a reason, "Queue documentation again" for a failed documentation run (`RequeueDocGenAsync`), no raw error text |
| `/Guilds/{guildId}/Currency` | `Pages/Guilds/Currency/Index.cshtml` | Guild currencies: create, edit rules, deactivate, manage mint authorities. Server-rendered cards (`_CurrencyCard`) with holder/circulation/debtor totals; every write goes out through `CurrenciesController` from `wwwroot/js/currency/currency-manage.js`, which patches the card in place (a new one is cloned from the page's `<template id="currency-item-template">`) and never reloads. Admin + `GuildAccess`. |
| `/Guilds/{guildId}/Currency/{currencyId}` | `Pages/Guilds/Currency/Details.cshtml` | One currency's wallets and ledger, with mint / fine / adjust and the reconcile check. Renders the shared `_CurrencyWalletPanel`, driven by `currency-wallets.js` (+ `currency-reconcile.js` for administrators). Moderator + `GuildAccess`; what the viewer may actually do comes from `ICurrencyAccessService`. |
| `/Guilds/{guildId}/Currency/Prices` | `Pages/Guilds/Currency/Prices.cshtml` | Soundboard prices: per-sound price, currency picker, exempt-role picker, plus a read-only list of prices that are not this guild's sounds. Rows are keyed by `CurrencyFeatureKeys.Soundboard(soundId)` and `currency-prices.js` sends that key back untouched. Admin + `GuildAccess`. |

### Commands Pages

| Route | File | Purpose |
|-------|------|---------|
| `/Commands` | `Pages/Commands/Index.cshtml` | Command List (debounced client-side search, `?q=`), Execution Logs and Analytics tabs. Tab, filters, page and open log are all in the query string; `commands-page.js` is the one controller. See `docs/articles/unified-command-pages.md`. |
| `/CommandLogs/Details/{id}` | `Pages/CommandLogs/Details.cshtml` | One command log (moderator). `?returnUrl=` sets where Back goes. |
| `/Search` | `Pages/Search.cshtml` | Unified search. Shows its validation message and the true total; command log results open the Details page for moderators and the Commands log dialog for everyone else. |

### Portal Pages (User Self-Service)

| Route | File | Purpose |
|-------|------|---------|
| `/Portal/Soundboard/{guildId}` | `Pages/Portal/Soundboard/Index.cshtml` | Member soundboard: search, sort, favourites, preview, upload, delete own sounds, play (with pending and queued feedback) |
| `/Portal/TTS/{guildId}` | `Pages/Portal/TTS/Index.cshtml` | Member TTS: message (limit from `AzureSpeech:MaxTextLength`), presets with a name dialog, preview, history |
| `/Portal/VOX/{guildId}` | `Pages/Portal/VOX/Index.cshtml` | Member VOX composer and clip browser, keyboard-reachable history |

The three share `Pages/Portal/_PortalLayout.cshtml` and mobile-first behaviour (plan decision D13): a one-row header on phones, the voice panel as a sticky bar above the content (`display: contents` on `.sidebar` under 1024px lets the sidebar's panels reorder), 44px touch targets, safe-area insets. When the guild's `EnableMemberPortal` is off each renders `Portal/Shared/_PortalDisabled.cshtml` ("The member portal is off") instead. With `AudioEnabled` off the page opens with `_PortalAudioDisabledWarning`.

### Error Pages

| Route | File | Purpose |
|-------|------|---------|
| `/Error/{statusCode}` | `Pages/Error/Index.cshtml` | One page for every status code (400 expired page, 403, 404, 405, 429, 500, 503, …). Reached through `UseStatusCodePagesWithReExecute` and `UseExceptionHandler`; shows the address that failed, a reference for 5xx, and Sign in / Reload / Go back actions. Uses `_ErrorLayout`. A failed request from a script (`/api`, `/hubs`, `X-Requested-With`) gets problem JSON instead. |

---

## Layouts

All layouts are located in `Pages/Shared/`.

| Layout | File | Purpose | Used By |
|--------|------|---------|---------|
| **Main Layout** | `_Layout.cshtml` | Default authenticated layout with navbar, sidebar, footer | Most admin/guild pages |
| **Landing Layout** | `_LayoutLanding.cshtml` | Unauthenticated layout for public pages (themed, `<main>`, skip link) | Landing, Login pages |
| **Guild Layout** | `_GuildLayout.cshtml` | Guild-specific layout with guild header/context; a failed POST rebuilds it through `GuildPageModelBase.PopulateGuildLayout` | Guild pages under `/Guilds/*/{guildId}` |
| **Portal Layout** | `Portal/_PortalLayout.cshtml` | Member-facing layout: one-row header, sticky voice bar, `quick-actions.js`, safe-area insets; no SignalR hub scripts for members | `/Portal/*` |
| **Error Layout** | `_ErrorLayout.cshtml` | Standalone, themed, `<main>` landmark; renders even when the theme lookup fails | `Pages/Error/Index` |

Every layout and standalone page (`_Layout`, `Portal/_PortalLayout`, `_ErrorLayout`, the `Layout = null` Account pages and `PublicLeaderboard`) puts `theme-root` on `<html>` (`TagHelpers/ThemeRootTagHelper`) and `<partial name="_ThemeHead" />` in `<head>`: together they render the saved theme, or follow `prefers-color-scheme` when none is saved, before first paint. `_LayoutLanding` does too, and loads `theme.js` so Landing follows the OS while open. See *Theme System* in [Design System](../articles/design-system.md).

`_PwaHead.cshtml` is a head partial (manifest link, install icons, service worker registration) included by `_Layout`, `Portal/_PortalLayout`, `_LayoutLanding`, and `Account/Login`. See [Progressive Web App](../articles/pwa.md).

### Layout Components

| Component | File | Purpose |
|-----------|------|---------|
| Navbar | `_Navbar.cshtml` | Top navigation bar; the user menu is a disclosure (button with `aria-expanded`, plain links, no `role=menu`) |
| Sidebar | `_Sidebar.cshtml` | Left sidebar with navigation (admin/authenticated). `inert` while the mobile drawer is closed (`navigation.js`). The footer shows the bot's real state (rendered from `IBotService`, kept current by `bot-status-refresh.js`) and a `data-stale-badge` |
| Connection Banner | `_ConnectionBanner.cshtml` | Floating "Reconnecting…" banner and its announcer, hidden while the SignalR hub is up (`connection-banner.js`). Rendered by `_Layout` only |
| Toast Container | `_ToastContainer.cshtml` | Toast container and live regions, plus toasts queued through `TempData.Set*Toast` (as JSON for `toast.js`). Rendered by `_Layout` and `_PortalLayout`; do not include it in pages |
| Mobile Search | `_MobileSearchOverlay.cshtml` | Mobile-friendly search overlay |
| Theme Head | `_ThemeHead.cshtml` | `theme-color` meta and the blocking first-paint theme script; needs `theme-root` on `<html>` |
| Theme Toggle | `_ThemeToggle.cshtml` | Header button switching dark/light (`theme.js`); model = extra button classes. In `_Navbar` and the portal header |
| Validation Scripts | `_ValidationScriptsPartial.cshtml` | Client-side validation script inclusion |
| Breadcrumb | `_Breadcrumb.cshtml` | Navigation breadcrumb trail |
| Sort Dropdown | `_SortDropdown.cshtml` | Sort control for tables/lists |
| Setting Field | `_SettingField.cshtml` | Form field wrapper for settings pages |
| ViewStart | `_ViewStart.cshtml` | Shared view initialization |
| ViewImports | `_ViewImports.cshtml` | Shared imports (models, directives) |
| ViewStart (Portal) | `Portal/_ViewStart.cshtml` | Portal-specific view initialization |
| ViewImports (Portal) | `Portal/_ViewImports.cshtml` | Portal-specific imports |

---

## Reusable Components

All components are located in `Pages/Shared/Components/` unless noted otherwise.

### Form Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Form Input** | `_FormInput.cshtml` | Text input fields with autocomplete, inputmode, min/max/step, aria-describedby | `FormInputViewModel` |
| **Form Select** | `_FormSelect.cshtml` | Dropdown selection with option groups | `FormSelectViewModel` |
| **Form Toggle** | `_FormToggle.cshtml` | Switch (`role="switch"`); an unchecked switch posts `false` | `FormToggleViewModel` |
| **Form Textarea** | `_FormTextarea.cshtml` | Multi-line text with label, help, validation | `FormTextareaViewModel` |
| **Radio Card Group** | `_RadioCardGroup.cshtml` | Fieldset of radio cards (hidden but focusable radios) | `RadioCardGroupViewModel` |
| **Radio Card** | `_RadioCard.cshtml` | One radio card, for custom layouts | `RadioCardViewModel` |
| **Autocomplete Input** | `_AutocompleteInput.cshtml` | Text input with autocomplete suggestions | `AutocompleteInputViewModel` |
| **Date Range Filter** | `_DateRangeFilter.cshtml` | The one date-range filter: preset buttons (local calendar days via `DateRangeFilter.presetRange`) plus start and end dates, inside a page's GET form. Used by Analytics, Rat Watch and Admin RatWatchAnalytics | `DateRangeFilterViewModel` |
| **Voice Selector** | `_VoiceSelector.cshtml` | Two-tier TTS voice dropdown grouped by language (`voice-selector.js`) | `VoiceSelectorViewModel` |
| **Guild Context Selector** | `_GuildContextSelector.cshtml` | Guild picker used by Search | `GuildContextSelectorViewModel` |

### Layout & Container Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Card** | `_Card.cshtml` | Flexible container with header/body/footer. Rendered only on `/Components` today (pages use the `.card` class directly); kept as the documented primitive | `CardViewModel` |
| **Hero Metric Card** | `_HeroMetricCard.cshtml` | Large metric/stat card for dashboards | `HeroMetricCardViewModel` |

### Navigation & Tabs

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **NavTabs** | `_NavTabs.cshtml` | Multi-tab navigation (page/in-page/AJAX modes, `nav-tabs.js`) | `NavTabsViewModel` |
| **Tab Panel** | `_TabPanel.cshtml` | Tablist plus panels driven by `tab-panel.js` (persistence mode: URL hash, local storage or none; arrow keys; it ignores a hash that is not its own tab id). The standard tab control for in-page tabs | `TabPanelViewModel` |
| **Guild Breadcrumb** | `_GuildBreadcrumb.cshtml` | Guild context breadcrumb | `GuildBreadcrumbViewModel` |
| **Command Breadcrumb** | `_CommandBreadcrumb.cshtml` | Command context breadcrumb | `CommandBreadcrumbViewModel` |

### Status & Indicators

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Status Indicator** | `_StatusIndicator.cshtml` | Online/offline/idle/busy status dot | `StatusIndicatorViewModel` |
| **Status Badge** | `_StatusBadge.cshtml` | Flagged-event status as a `.status-badge` pill (styles in `site.css`) | `FlaggedEventStatus` |
| **Rat Watch Status Badge** | `_RatWatchStatusBadge.cshtml` | Rat Watch status as a dot plus words (`RatWatchStatusDisplay.DisplayName()`), so colour is never the only signal | `RatWatchStatus` |
| **Severity Badge** | `_SeverityBadge.cshtml` | Severity as a `.severity-badge` pill, with a pulse dot when critical (styles in `site.css`) | `Severity` |
| **Bot Status Banner** | `_BotStatusBanner.cshtml` | Bot status banner for page top | `BotStatusBannerViewModel` |
| **Connection Status** | `_ConnectionStatus.cshtml` | SignalR connection pill (`Id` and `Live` parameters; used inside `_ConnectionBanner`) | `ConnectionStatusViewModel` |
| **Restart Banner** | `_RestartBanner.cshtml` | "Restart needed" notice on Settings. Model: `bool` (pending when the page is drawn); always rendered, `hidden` unless pending, so `settings.js` can reveal it after a save. Links to `?category=BotControl` | `bool` |

### Data Display Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Badge** | `_Badge.cshtml` | Small labeled tag/status indicator; `IsPill = true` for a fully rounded pill | `BadgeViewModel` |
| **Rule Type Icon** | `_RuleTypeIcon.cshtml` | Rule type visual indicator | `RuleTypeIconViewModel` |
| **Pagination** | `_Pagination.cshtml` | Page navigation; disabled ends are spans, empty and single-page states handled | `PaginationViewModel` |
| **Activity Feed Timeline** | `_ActivityFeedTimeline.cshtml` | Vertical timeline of activities; each time is a refreshing `<time data-relative-time>`; `dashboard-realtime.js` prepends live events from the `#activity-item-template` | `ActivityFeedTimelineViewModel` |
| **Audit Log Card** | `_AuditLogCard.cshtml` | Audit log entry display card | `AuditLogCardViewModel` |
| **Chart Data Table** | `_ChartDataTable.cshtml` | The figures behind a chart as a visually hidden table (wrapper `sr-only`), linked from the canvas by `aria-describedby`; every chart and heatmap has one | `ChartDataTableViewModel` |
| **Connected Servers Widget** | `_ConnectedServersWidget.cshtml` | Rows of servers: a header-and-columns grid when the card is 40rem wide or more, a stacked card per server below that (container query). No role check inside; the page decides who sees it (Moderator and up). Rows are redrawn from `?handler=ConnectedServers` JSON by `dashboard-actions.js`, cloning `#connected-server-template`: change the two together. The copy-ID button is a `.row-actions` button | `ConnectedServersWidgetViewModel` |

### Feedback Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Alert** | `_Alert.cshtml` | Info/success/warning/error message banner | `AlertViewModel` |
| **Button** | `_Button.cshtml` | Interactive button (primary/secondary/danger/ghost) | `ButtonViewModel` |
| **Loading Spinner** | `_LoadingSpinner.cshtml` | Loading indicator (simple/dots/pulse) | `LoadingSpinnerViewModel` |
| **Skeleton** | `_Skeleton.cshtml` | Content placeholder during loading | `SkeletonViewModel` |
| **Skeleton Card** | `_SkeletonCard.cshtml` | Card-shaped skeleton loader. Rendered only on `/Components` today; kept with the other skeleton primitives | `SkeletonCardViewModel` |
| **Skeleton Table** | `_SkeletonTable.cshtml` | Placeholder rows for a table or list | `SkeletonTableViewModel` |
| **Skeleton Lines** | `_SkeletonLines.cshtml` | Placeholder text lines | `SkeletonLinesViewModel` |
| **Page Loading Overlay** | `_PageLoadingOverlay.cshtml` | Full-page loading overlay with backdrop | `PageLoadingOverlayViewModel` |
| **Empty State** | `_EmptyState.cshtml` | No data/no results/error state; configurable icon and action; script twin `wwwroot/js/empty-state.js` | `EmptyStateViewModel` |
| **Confirmation Modal** | `_ConfirmationModal.cshtml` | Confirmation dialog; posts over fetch to its own action, handles JSON, redirect and failure | `ConfirmationModalViewModel` |
| **Typed Confirmation Modal** | `_TypedConfirmationModal.cshtml` | Enhanced confirmation requiring text input | `TypedConfirmationModalViewModel` |
| **Pause Modal** | `_PauseModal.cshtml` | Pause/resume action dialog | `PauseModalViewModel` |

### Specialized Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Command Header** | `_CommandHeader.cshtml` | Command title/description header | `CommandHeaderViewModel` |
| **Command Log Details Modal** | `_CommandLogDetailsModal.cshtml` | Modal for command log entry details | `CommandLogDetailsModalViewModel` |
| **Dashboard Widget** | `_DashboardWidget.cshtml` | Dashboard widget shell: header, then a retry state (`LoadFailed`), an empty state, or a body partial (`BodyPartial` + `BodyModel`, no HTML built in C#) | `DashboardWidgetViewModel` |
| **Widget Stat** | `Pages/Guilds/Widgets/_WidgetStat.cshtml` | One labelled figure inside a widget body | `WidgetStatViewModel` |
| **Quick Actions Card** | `_QuickActionsCard.cshtml` | Card with action buttons/links | `QuickActionsCardViewModel` |
| **Guild Header** | `_GuildHeader.cshtml` | Guild name/icon header | `GuildHeaderViewModel` |
| **Voice Channel Panel** | `_VoiceChannelPanel.cshtml` | Voice channel list/control panel | `VoiceChannelPanelViewModel` |
| **Currency Wallet Panel** | `_CurrencyWalletPanel.cshtml` | Holder list, ledger with paging, and the mint / fine / adjust dialog for one currency (a `quickActions` dialog; the member is chosen with the user picker, not a typed ID). Static markup filled by `currency-wallets.js`; the `CanMint` / `CanFine` / `CanAdminister` flags decide which actions are rendered at all, and Mint is disabled until a currency is chosen. Shared by the guild currency detail page and `/Admin/Currency`. | `CurrencyWalletPanelViewModel` |
| **Currency Manage Modals** | `_CurrencyManageModals.cshtml` | The currency editor and mint authority dialogs shared by `/Guilds/{guildId}/Currency` and `/Admin/Currency` (`quickActions` dialogs; user picker, role select on the guild page, revoke asks for confirmation). | `CurrencyManageModalsViewModel` |
| **Currency Card / Row** | `_CurrencyCard.cshtml`, `_CurrencyRow.cshtml` | One currency as a guild card or a bot-wide table row. Elements a script changes carry `data-field`; the same markup is rendered blank inside the page's `<template>` for a create. | `CurrencyPortalItemViewModel` |

### Page Partials (not shared components)

Partials that belong to one page. They are fetched as HTML fragments (`ApiClient.getHtml`) or included once, so they never include the layout.

| Area | Partials | Notes |
|------|----------|-------|
| Performance tabs | `Admin/Performance/Tabs/_OverviewTab`, `_HealthTab`, `_CommandsTab`, `_ApiTab`, `_SystemTab`, `_AlertsTab`, `_TabUnavailable` | Built only by `IPerformanceDashboardAggregator`; loaded with `?handler=Partial&tabId=&hours=` by `performance/dashboard.js`; each tab's script is `performance/tabs/*.js` |
| Logs tabs | `Admin/Logs/Tabs/_MessagesTab`, `_AuditTab` | Tabs are page navigation (`?tab=`); only the active tab reads its data |
| Commands tabs | `Commands/Tabs/_CommandListTab`, `_ExecutionLogsTab`, `_AnalyticsTab`; `CommandLogs/_CommandLogDetailsContent` | Loaded by `commands-page.js`; the log content fills the log dialog and the Details page |
| Settings | `Admin/_SettingsTabFooter` | Per-tab Save, error alert and status line, driven by `settings.js` |
| Guild Details widgets | `Guilds/Widgets/_ActivityBody`, `_AssistantBody`, `_AudioBody`, `_MembersBody`, `_RatWatchBody`, `_RemindersBody`, `_ScheduledMessagesBody`, `_WidgetStat` | Bodies rendered inside `_DashboardWidget` (`BodyPartial` + `BodyModel`) |
| Members | `Guilds/Members/_MemberDetailModal` | Member detail dialog (`quickActions.openDialog`), filled from the members API; not a route |
| Member portal | `Portal/Shared/_PortalHeader`, `_PortalDisabled`, `_PortalAudioDisabledWarning`, `_PortalLanding`, `_PortalUnauthorized` | `_PortalDisabled` is what every portal page renders when the guild's `EnableMemberPortal` is off |

### TTS & Audio Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **SSML Preview** | `_SsmlPreview.cshtml` | SSML text preview and editor | `SsmlPreviewViewModel` |
| **Mode Switcher** | `_ModeSwitcher.cshtml` | Switch between text/SSML modes | `ModeSwitcherViewModel` |
| **Style Selector** | `_StyleSelector.cshtml` | TTS voice style selector | `StyleSelectorViewModel` |
| **Preset Bar** | `_PresetBar.cshtml` | Preset voice/style quick selector; custom presets go to `CustomPresetsUrl` (portal endpoint by default, `/api/guilds/{guildId}/tts/presets/custom` on the admin TTS page) | `PresetBarViewModel` |
| **Emphasis Toolbar** | `_EmphasisToolbar.cshtml` | SSML emphasis/prosody editor toolbar | `EmphasisToolbarViewModel` |

### User/Guild Preview Popups

There are no preview partials. `wwwroot/js/preview-popup.js` (loaded by `_Layout`) builds the popup in script from the preview API's JSON, with loading and error states, on any element carrying `class="preview-trigger"` and `data-preview-type="user|guild"` plus `data-user-id` / `data-guild-id` (and `data-context-guild-id`). The old `_UserPreviewPopup`, `_GuildPreviewPopup`, `_PreviewPopupLoading` and `_PreviewPopupError` partials were deleted in Phase 16 because nothing rendered them.

---

## Component Groups by Feature

### Admin Dashboard

**Pages:**
- `/` - Dashboard (hero stats pushed over SignalR, connected servers, activity timeline)
- `/Admin/Performance` - One shell with tabs overview, health, commands, api, system, alerts (`?tab=`, `?hours=`)

**Components:**
- `_TabPanel` (tablist), the `Admin/Performance/Tabs/*` partials
- `_HeroMetricCard`, `_DashboardWidget`, `_ConnectedServersWidget`, `_BotStatusBanner`
- `_ChartDataTable` beside every chart

**Purpose:** System health, performance metrics, command stats, API usage monitoring

### User Management

**Pages:**
- `/Admin/Users` - User list with pagination
- `/Admin/Users/Create` - Create user form
- `/Admin/Users/Edit?id={id}` - Edit user form
- `/Admin/Users/Details?id={id}` - User details view

**Components:**
- FormInput, FormSelect, FormToggle
- Button, Alert
- Badge (for status/roles)
- Pagination
- Card

**Purpose:** CRUD operations for system users

### Guild Management

**Pages:**
- `/Guilds/Details/{guildId}` - Server overview (widgets)
- `/Guilds/{guildId}/Members` - Member directory with filters
- `/Guilds/Edit/{guildId}` - Server settings
- `/Guilds/ModerationSettings/{guildId}` - Moderation configuration
- `/Guilds/Analytics/{guildId}` (+ `/Engagement/`, `/Moderation/`) - Guild analytics with multiple views

**Components:**
- Guild layout with context
- `_TabPanel` for multi-section pages
- FormInput, FormSelect, FormTextarea, FormToggle, RadioCardGroup (settings forms)
- Card, DashboardWidget
- StatusIndicator, StatusBadge, SeverityBadge
- EmptyState, Skeleton*, `_Pagination` (member lists)
- ActivityFeedTimeline, AuditLogCard

**Purpose:** Per-server configuration and analytics

### Audio/Soundboard

**Pages:**
- `/Guilds/Soundboard/{guildId}` - Soundboard manager
- `/Guilds/TextToSpeech/{guildId}` - TTS settings
- `/Guilds/VOX/{guildId}` - VOX clip library
- `/Guilds/AudioSettings/{guildId}`, `/Guilds/AudioModerationLog/{guildId}`
- `/Portal/Soundboard/{guildId}`, `/Portal/TTS/{guildId}`, `/Portal/VOX/{guildId}` - Member portal (OAuth, mobile first)

**Components:**
- FormInput, FormSelect, FormToggle
- Button (play/record/delete actions)
- Card (sound item display)
- SSML-related components (StyleSelector, ModeSwitcher, EmphasisToolbar, SsmlPreview)
- PresetBar, ModeSwitch
- StatusIndicator (playback state)

**Purpose:** Audio playback, TTS configuration, VOX management

### Logging & Analytics

**Pages:**
- `/Admin/Logs` - Message and audit logs (the old `/Admin/AuditLogs` redirects to its Audit tab)
- `/Admin/AuditLogs/Details/{id}`, `/Admin/MessageLogs/Details/{id}` - Entry details
- `/Commands` - Command list, execution logs and analytics tabs
- `/CommandLogs/Details/{id}` - One command log
- `/Guilds/Analytics/Engagement/{guildId}`, `/Guilds/Analytics/Moderation/{guildId}` - Engagement and moderation analytics
- `/Guilds/RatWatch/{guildId}` (+ `/Analytics/`, `/Incidents/`) - RatWatch
- `/Admin/RatWatchAnalytics` - RatWatch analytics

**Components:**
- `_TabPanel` (multi-view analytics)
- Card, HeroMetricCard
- AuditLogCard
- Pagination
- EmptyState, Skeleton*
- StatusBadge, SeverityBadge, RatWatchStatusBadge, Badge
- ActivityFeedTimeline
- `_DateRangeFilter` (the one date filter), `_ChartDataTable` (text alternative for every chart)

**Purpose:** Activity tracking, metrics visualization, moderation logs

---

## Page Hierarchy Diagram

```
/                              Dashboard (authenticated); anonymous visitors go to /landing
├── landing                    Landing (unauthenticated)
├── Account/                   Login, ExternalLogin, LinkDiscord, Logout, Profile, Privacy, AccessDenied, Lockout
├── Search
├── Commands                   one page, tabs: list, execution logs, analytics
├── CommandLogs/Details/{id}
├── Components                 component showcase
├── Error/{statusCode}         one page for every status code
│
├── Admin/                     (Admin; the purge pages and Currency need SuperAdmin)
│   ├── Users/                 Index, Create, Edit?id=, Details?id=
│   ├── Logs                   tabs: messages, audit
│   ├── AuditLogs/             Index (redirects to Logs), Details/{id}
│   ├── MessageLogs/           Index, Details/{id}
│   ├── Performance            one shell, tabs: overview, health, commands, api, system, alerts
│   │   └── Alerts, ApiMetrics, Commands, HealthMetrics, SystemHealth   (redirects to ?tab=)
│   ├── Settings               tabs by ?category=
│   ├── Notifications, LlmUsage, Currency
│   └── BulkPurge, UserPurge, RatWatchAnalytics
│
├── Guilds/                    (per-server pages; the guild id is the last route segment, except Members and Currency)
│   ├── Index                  server list
│   ├── Details/{guildId}      overview widgets
│   ├── Edit, Welcome, ModerationSettings, Reminders
│   ├── {guildId}/Members/     Index, {userId}/Moderation
│   ├── ScheduledMessages/     Index, Create, Edit/{guildId}/{id}
│   ├── Analytics/             Index, Engagement, Moderation
│   ├── FlaggedEvents/         Index, Details/{guildId}/{id}
│   ├── RatWatch/              Index, Analytics, Incidents
│   ├── AssistantSettings, AssistantMetrics, FeatureRequests/
│   ├── Soundboard, TextToSpeech, VOX, AudioSettings, AudioModerationLog
│   ├── {guildId}/Currency/    Index, {currencyId}, Prices
│   └── PublicLeaderboard/{guildId}/Leaderboard   (standalone, no login)
│
└── Portal/                    (member portal, OAuth)
    ├── Soundboard/{guildId}
    ├── TTS/{guildId}
    └── VOX/{guildId}
```

---

## Layout Usage Map

| Layout | Routes | Key Feature |
|--------|--------|-------------|
| **_LayoutLanding** | `/landing` | Public page with minimal chrome |
| **_Layout** | Admin, Commands, Search and home pages | Full nav + sidebar authenticated layout |
| **_GuildLayout** | Guild pages under `/Guilds/*` | Guild context header + nav |
| **_PortalLayout** | `/Portal/*` | Member layout: one-row header, sticky voice bar |
| **_ErrorLayout** | `/Error/{statusCode}` | Standalone, themed |
| **None (`Layout = null`)** | Account pages, `PublicLeaderboard` | Standalone pages; each includes `_ThemeHead`, `_PwaHead` where installable, and the Google Fonts link |

---

## Key Component Relationships

### Form Validation Flow

1. **FormInput/FormSelect/FormTextarea/FormToggle/RadioCardGroup** capture user input and render their own field error (`aria-invalid`, `aria-describedby`)
2. The `<form>` carries `data-submit-guard` (pending state, no double submit) and `data-unsaved-changes`
3. The server re-renders with errors on the fields; `data-focus-first-error` (`form-focus.js`) moves focus to the first one
4. The result of a successful save is a TempData toast; a page-level failure is an **Alert**
5. **ValidationScriptsPartial** enables client-side validation

### Data Display Flow

1. **Pagination** divides data into pages
2. **NavTabs** or filter controls change data view
3. **Card** containers display individual items
4. **Badge/StatusIndicator/StatusBadge** annotate items with metadata
5. **EmptyState** shows when there is no data (and a filtered variant with "Clear filters"); **Skeleton*** while a region loads
6. A failed load is an error state with Retry, never zeros

### Navigation Flow

1. **Navbar** provides top-level navigation
2. **Sidebar** provides admin/authenticated navigation
3. **TabPanel** (in-page) and **NavTabs** (page navigation) handle section navigation within pages; filters, tab and page live in the query string
4. **Breadcrumb** shows navigation context
5. Links are built with `asp-page` / `Url.Page`, never string-built routes

---

## Shared Utilities

### JavaScript Modules

All under `src/DiscordBot.Bot/wwwroot/js/` unless noted. One file per page or widget; the primitives below are the shared layer.

| Module | File | Purpose |
|--------|------|---------|
| Modals | `quick-actions.js` | The only modal layer: `quickActions.openDialog/closeDialog`, `confirm/alert/typedConfirm` (Promises), `showConfirmationModal`. Motion, scroll lock, `inert` background, focus trap, stacking |
| Toasts | `toast.js` | `toast.success/error/warning/info(msg, { action })`, legacy aliases, server-queued toasts, `_Alert` dismiss |
| API client | `api-client.js` | `ApiClient` (`get/post/put/del/getHtml/requestRaw`): anti-forgery, session-expiry toast, plain-language errors, 30s timeout, never HTML as data |
| Loading | `loading-manager.js` | `LoadingManager` button pending states, overlays, `data-submit-guard` |
| Skeleton / empty | `skeleton.js`, `empty-state.js` | `Skeleton.show` (300ms delay), `EmptyState.render/filtered/error` |
| Unsaved changes | `unsaved-changes.js` | `data-unsaved-changes` dirty tracking and `beforeunload`; `UnsavedChanges.markClean(form)` |
| Formatting | `format.js`, `timezone.js`, `date-range-filter.js` | `Format.*`, `data-utc` conversion (also for inserted content), `DateRangeFilter.presetRange` (local days) |
| Tabs | `tab-panel.js`, `nav-tabs.js` | `_TabPanel` and `_NavTabs` behaviour |
| Confirm forms | `confirm-forms.js` | `data-confirm-*` on a form asks first, then submits normally |
| Bulk selection | `bulk-selection.js` | `BulkSelection` keyed by row id, mirrors table and card checkboxes |
| Focus first error | `form-focus.js` | `FormFocus`, `data-focus-first-error` |
| Section gate | `section-gate.js` | A switched-off section is `inert` with its reason beside it |
| Escaping | `safe-html.js`, `discord-markdown.js` | `SafeHtml.escape`; escape-first Discord markdown for previews |
| Charts | `chart-theme.js`, `analytics-charts.js`, `performance/components/chart-utils.js` | Token-driven Chart.js theming and the chart helpers built on it |
| Live status | `dashboard-hub.js`, `connection-banner.js`, `bot-status-refresh.js`, `dashboard-stats.js`, `dashboard-realtime.js`, `dashboard-actions.js` | Hub connection and states, banner, bot status, dashboard stats/feed/actions |
| Theme / PWA | `theme.js`, `pwa.js` (+ `sw.js` at the root) | Theme switching without reload; service worker registration |
| Navigation | `navigation.js`, `guild-nav.js`, `notification-bell.js`, `search.js`, `preview-popup.js` | Shell chrome, bell disclosure, search, user/guild preview popups |
| Filter panel | `shared/filter-panel.js` | `toggleFilterPanel()`, and the `data-date-preset` buttons of `_DateRangeFilter` |
| AJAX Sort | `ajax-sort.js` | Re-renders a list from a `?handler=Partial` URL when `_SortDropdown` changes |
| Pages | `commands-page.js`, `settings.js`, `performance/*`, `soundboard-admin.js`, `tts-page.js`, `audio-settings.js`, `portal-*.js`, `currency/*`, ... | One controller per page or widget; see the page's row above |

### CSS Framework

- **Tailwind CSS** for all styling
- **Design System tokens** in `design-system.md`
- **Color classes:** `bg-accent-orange`, `bg-bg-primary`, `text-text-secondary`, etc.
- **Spacing:** Tailwind standard scale (1 = 4px)

---

## Component Statistics

| Category | Count |
|----------|-------|
| Razor Pages (with a route) | 72 |
| Layouts | 5 (`_Layout`, `_GuildLayout`, `_LayoutLanding`, `_ErrorLayout`, `Portal/_PortalLayout`) |
| Shared components (`Pages/Shared/Components`) | 57 |
| Shared layout partials (`Pages/Shared`) | 16 |
| Page script modules (`wwwroot/js/*.js`) | 80 |

---

## Navigation Reference

### For AI Agents (Claude)

When working on features, use these pages as entry points:

- **Need to add a form?** Look at `/Admin/Users/Create` or `/Guilds/Welcome/{guildId}`
- **Need to display a list?** Look at `/Guilds/Reminders/{guildId}` (table from `md`, cards below) or `/Admin/Users`
- **Need tabbed navigation?** See `/Admin/Settings` (`_TabPanel`) or `/Admin/Logs` (page navigation)
- **Need modals/popups?** See `_ConfirmationModal.cshtml` and `quickActions.openDialog` (`_MemberDetailModal.cshtml`)
- **Need real-time status?** See `_BotStatusBanner`, `_StatusIndicator` and `_ConnectionStatus`
- **Need to show activity?** See `_ActivityFeedTimeline` and `_AuditLogCard`

---

## See Also

- **[Component API Usage Guide](../articles/component-api.md)** - Detailed component documentation with examples
- **[Design System](../articles/design-system.md)** - Color palette, typography, tokens
- **[Form Implementation Standards](../articles/form-implementation-standards.md)** - Form patterns and validation
- **[Authorization Policies](../articles/authorization-policies.md)** - Role-based access control
- **[Interactive Components](../articles/interactive-components.md)** - Discord button interactions

---

**Maintained by:** UI Development Team
**Last Updated:** 2026-10-03
**Status:** Complete
