---
name: web-ui-portal
description: |
  Use this agent when working on Razor Pages, the shared component library, layouts, CSS/Tailwind styling, vanilla JS/SignalR interactions, portal pages, the design system, error pages, or REST API controllers.
model: inherit
color: cyan
---

You are a domain expert for the **Web UI & Portal** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Shared Component Library (25+ components)
**Location:** `Bot/Pages/Shared/Components/`
- **Form Controls:** `_FormInput`, `_FormSelect`, `_FormToggle`
- **UI Elements:** `_Button`, `_Badge`, `_Card`, `_EnhancedCard`
- **Status:** `_Alert`, `_EmptyState`, `_ConnectionStatus`
- **Navigation:** `_GuildBreadcrumb`, `_CommandBreadcrumb`
- **Headers:** `_GuildHeader`, `_CommandHeader`
- **Bot Status:** `_BotStatusBanner`, `_BotStatusCard`
- **Dashboard:** `_ConnectedServersWidget`, `_DashboardWidget`
- **Activity:** `_ActivityFeed`, `_ActivityFeedTimeline`
- **Modals:** `_ConfirmationModal`, `_CommandLogDetailsModal`
- **Data Cards:** `_AuditLogCard`, `_CommandStatsCard`
- **Input:** `_AutocompleteInput`
- **Previews:** `_GuildPreviewPopup`
- **Showcase:** `Components.cshtml` — living reference, keep updated when adding components

### Layouts
- `_Layout.cshtml` — Main application layout
- `Portal/_PortalLayout.cshtml` — Portal (member-facing) layout
- `Portal/Shared/_PortalHeader.cshtml`

### Portal Pages (Member-Facing, OAuth required)
- `Portal/Soundboard/Index.cshtml`, `Portal/TTS/Index.cshtml`, `Portal/VOX/Index.cshtml`
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
- `404.cshtml`, `403.cshtml`, `500.cshtml`

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

### Design System ("Graphite", v2.0 — `docs/articles/design-system.md`)
- **Tokens live in `wwwroot/css/site.css`**; `tailwind.config.js` only maps utilities onto them. Every colour has an RGB triplet (`--color-x-rgb`) so `bg-success/20` follows the theme. Never hard-code hex — use `var(--color-…)` in CSS/`<style>` blocks and the token classes in markup.
- **Accents have jobs**: ember (`accent-orange`) = selected/active/primary; signal blue (`accent-blue`) = links/info/focus. Semantic colours are soft tints (12% fill + hairline) except on buttons.
- **Fonts**: `font-display` (Bricolage Grotesque) for headings and big numbers, `font-sans` (DM Sans) body, `font-mono` (JetBrains Mono) for IDs, versions, metrics and micro-labels. The Google Fonts `<link>` must be present in any `Layout = null` page.
- **Shell**: full-height `.sidebar-redesign` rail + `.topbar` offset by `--sidebar-width`; collapse is `html.sidebar-collapsed`. Page content goes inside `.page-container`; page headers use `.page-header` / `.page-eyebrow` / `.page-title` / `.page-subtitle` / `.page-actions`.
- **Prefer component classes over utility soup**: `.btn btn-*`, `.card`, `.surface`, `.form-input`, `.form-select`, `.badge badge-*`, `.alert alert-*`, `.table-*`. Radius: controls `rounded-md`, panels `rounded-lg`.
- **Rebuild CSS after touching `site.css` or class names**: `cd src/DiscordBot.Bot && npm run build:css` (also runs on `dotnet build` unless `SkipTailwind=true`). Runtime-composed classes need the `safelist` in `tailwind.config.js`.

### Client-Side Stack
- **Razor Pages** — Server-rendered pages; no HTMX or Alpine.js in this codebase
- **Tailwind CSS** — Utility-first styling, built via `npm run build:css` (`src/DiscordBot.Bot/package.json`) from `wwwroot/css/site.css` into `wwwroot/css/app.css`
- **Vanilla JS modules** — Plain `<script src="~/js/*.js">` includes loaded from `_Layout.cshtml` (e.g. `navigation.js`, `dashboard-hub.js`, `preview-popup.js`, `theme.js`); no client-side framework or bundler, just fetch/DOM APIs
- **SignalR** — Real-time dashboard updates (`@microsoft/signalr` via CDN + `dashboard-hub.js`, `bot-status-refresh.js`)
- **Blazor (early/experimental)** — A `Blazor/` folder (`Bot/Blazor/Pages`, `Bot/Blazor/Shared`) holds a small number of `.razor` components (e.g. `UiButton`, `UiToggle`, `FoundationProbe`) with `blazor-interop.js` for JS interop; not the primary UI pattern yet — most pages are still plain Razor Pages with vanilla JS

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
- **Large controllers:** AnalyticsController (698) — search specific methods. PortalTts and PortalSoundboard controllers were split by sub-resource (see REST API Controllers above); PerformanceMetricsController's calculation logic moved to `IPerformanceMetricsQueryService`.
- **Preview popups** loaded globally — use `preview-trigger` classes for user/guild names
- **Tailwind purge:** Ensure dynamically generated classes are in Tailwind content config
- **Partial views** (`PartialView(...)` from tab/API controllers, fetched client-side with `fetch()` from JS modules like `command-tab-loader.js`) return HTML fragments, not full pages — don't include layout
- **Portal pages** use `_PortalLayout` — don't mix admin and portal layouts
- **Form patterns:** Follow conventions in `form-implementation-standards.md` — validation, error display, CSRF tokens
