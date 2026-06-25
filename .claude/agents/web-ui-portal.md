---
name: web-ui-portal
description: |
  Use this agent when working on Razor Pages, the shared component library, layouts, CSS/Tailwind styling, HTMX/Alpine.js interactions, portal pages, the design system, error pages, or REST API controllers.
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

### Blazor Server Islands (`Bot/Blazor/`)
Islands-first modernization (see `docs/architecture/blazor-modernization-selective-plan.md`):
interactive Blazor Server components embedded into existing Razor Pages via the
`<component render-mode="ServerPrerendered">` tag helper. Routing/auth/layout stay with the
host Razor Page; the island inherits them. **Do not** convert whole pages or add an
`App.razor` — these are embedded regions only.
- **Shared kit** (`Blazor/Shared/`): `UiButton`, `UiToggle` (design-system twins of the
  partials), `TabbedFormShell` (tab strip + centralized dirty flag + unsaved-changes guard),
  `ConfirmModal` (awaitable `ShowAsync` → `Task<bool>`, mirrors `_ConfirmationModal`),
  `SaveButton` (3-state Idle→Saving→Saved), `TabDefinition`.
- **Interop** (`Blazor/Interop/`): `ToastInterop`/`ThemeInterop` bridge the existing
  `toast.js`/`theme.js` via the window shim `wwwroot/js/blazor-interop.js` (must load **after**
  toast.js/theme.js).
- **Event bus** (`Blazor/Services/`): `IDashboardEventBus` (singleton, Slice 2) is in-process
  pub/sub for real-time islands. Existing notifiers **dual-publish** to it after their SignalR
  broadcast (`NotificationBroadcaster` → notification events; `DashboardUpdateService` →
  `BotStatusChanged`) — additive, JS path untouched. Islands subscribe in
  `OnAfterRenderAsync(firstRender)`, marshal with `InvokeAsync(StateHasChanged)`, unsubscribe in
  `Dispose`; notification events carry `userId` so each circuit filters to its own user.
- **Islands** (`Blazor/Pages/`): `ModerationSettingsIsland` (Slice 1, `/Guilds/{id}/ModerationSettings`
  body), `NotificationBellIsland` (Slice 2, global navbar bell — replaces `notification-bell.js`),
  `BotStatusCardIsland` (Slice 2, dashboard banner — replaces `bot-status-refresh.js` 30s polling),
  `AdminSettingsIsland` (Slice 3, `/Admin/Settings` body — replaces `settings.js`; 7 tabs, per-category
  & global save/reset, command modules, appearance (SuperAdmin), Bot Control with event-bus live status
  + restart/typed-confirm shutdown; audit-log enqueues mirror the page handlers),
  `CommandsIsland` (Slice 4, `/Commands` body — replaces the AJAX tab-loader stack for the Command
  List + Execution Logs tabs: native accordion, debounced filter panel, results table/cards, native
  log-details modal, admin clear/re-register via `ConfirmModal`; the Analytics tab stays on Chart.js,
  delegated to `/api/commands/analytics` via `commands-island-interop.js`),
  `FoundationProbe` (Phase 0 PoC on `/Components`).
- **Shared kit additions:** `TypedConfirmModal` (Slice 3) — awaitable type-to-confirm dialog mirroring
  `_TypedConfirmationModal.cshtml`, used for the bot shutdown flow. `Pagination` (Slice 4) — reusable
  numbered-window pager raising `OnPageChange`; Member Directory (Slice 5) reuses it. (A generic
  `FilterableTable` is intentionally still deferred — to be co-designed in Slice 5 with a second consumer.)
- **Blazor bootstrap is global** (Slice 2): the bell lives in `_Navbar`, so `_Layout` starts the
  circuit for every layout page (`blazor.server.js` autostart=false + `blazor-interop.js`, after
  toast/theme). Host pages no longer add their own `blazor.server.js`.
- **Patterns:** pass snowflake IDs as **strings** (`param-GuildId="@Model.GuildId.ToString()"`);
  data access = `IServiceScopeFactory.CreateScope()` per op resolving the existing services
  (no circuit-scoped `DbContext`); the circuit starts with an explicit absolute `/_blazor` hub URL
  (nested routes would otherwise resolve negotiate relative to the page path); auth/userId inside
  an island via injected `AuthenticationStateProvider` (NameIdentifier claim). Parity-gate page
  conversions behind a `?legacy=true` query until the island matches, then remove the legacy branch.

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

### Client-Side Stack
- **Tailwind CSS** — Utility-first styling
- **HTMX** — Server-driven interactivity (partial page updates, lazy loading)
- **Alpine.js** — Lightweight client-side reactivity
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
- **Large controllers:** PerformanceMetricsController (1,173), PortalTtsController (1,089), AnalyticsController (698) — search specific methods
- **Preview popups** loaded globally — use `preview-trigger` classes for user/guild names
- **Tailwind purge:** Ensure dynamically generated classes are in Tailwind content config
- **HTMX partial views** return HTML fragments, not full pages — don't include layout
- **Portal pages** use `_PortalLayout` — don't mix admin and portal layouts
- **Form patterns:** Follow conventions in `form-implementation-standards.md` — validation, error display, CSRF tokens
