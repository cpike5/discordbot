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

### Error Pages
- `404.cshtml`, `403.cshtml`, `500.cshtml`

### REST API Controllers (30)
**Location:** `Bot/Controllers/` — JSON API endpoints for Razor Pages frontend and external consumers.

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
- **Large controllers:** PerformanceMetricsController (1,173), PortalTtsController (1,089), AnalyticsController (698) — search specific methods
- **Preview popups** loaded globally — use `preview-trigger` classes for user/guild names
- **Tailwind purge:** Ensure dynamically generated classes are in Tailwind content config
- **Partial views** (`PartialView(...)` from tab/API controllers, fetched client-side with `fetch()` from JS modules like `command-tab-loader.js`) return HTML fragments, not full pages — don't include layout
- **Portal pages** use `_PortalLayout` — don't mix admin and portal layouts
- **Form patterns:** Follow conventions in `form-implementation-standards.md` — validation, error display, CSRF tokens
