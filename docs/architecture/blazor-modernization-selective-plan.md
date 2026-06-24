# Selective Blazor Modernization Plan (Islands-First)

> **Scope note.** This is the *pragmatic, near-term* plan. It deliberately does **not**
> rewrite the whole portal. It adds Blazor Server **interactive islands** to the existing
> Razor Pages app to modernize the highest-ROI components and pages, while leaving most of
> the site — and the entire routing/auth model — exactly as it is today.
>
> A separate document, [`blazor-migration-plan.md`](./blazor-migration-plan.md), describes
> the *maximalist* end-state (full conversion, .NET 9, 51–75 days). Treat that as the
> long-term north star. **This document is what we actually do first**, and every step here
> is a valid prefix of that larger plan — nothing here has to be undone to continue.

---

## 1. Why selective, and how this differs from the full-rewrite doc

The portal is a mature ASP.NET Core 8 **Razor Pages + Web API + SignalR** app:

- ~28k lines of vanilla JS across ~55 modules (no HTMX, no Alpine — despite stale docs).
- 58 shared Razor *partials* + ViewModels acting as the design system.
- One `DashboardHub` (`/hubs/dashboard`) with rich groups and 7 server-side notifier services
  already pushing metrics, alerts, notifications, and audio status.
- Tailwind theme driven by CSS variables; `tailwind.config.js` **already globs `.razor`** files.

The full-rewrite plan is correct as a destination but is a poor *first move*: it forces a
.NET 9 upgrade (Elastic APM / EF Core 9 risk), converts routing wholesale, and ports all 58
components before delivering user-visible value. The user's constraint is explicit: **"we
don't need a full rewrite right now."**

This plan instead picks the few places where Blazor's model (server-held state + diffing over
SignalR) eliminates the most JS and the most bugs, and ships them as islands.

| Dimension | Full-rewrite doc | **This plan** |
|---|---|---|
| Framework | Upgrade to .NET 9 (Phase 0) | **Stay on .NET 8** — no upgrade |
| Hosting | `MapRazorComponents<App>()` routable app | **Classic Blazor Server islands** embedded in existing Razor Pages |
| Routing/auth | Replaced by Blazor Router | **Unchanged** — pages stay Razor Pages; islands inherit page auth |
| Blast radius | Whole `Pages/` tree | **~5 components + 3 pages** initially |
| Effort to first value | Weeks (Phases 0–2 before any page) | **Days** (one vertical slice) |
| Reversibility | Large | Each island is independently revertible |

---

## 2. Goals & non-goals

**Goals**
- Replace the most painful stateful JS (multi-tab forms, dirty-tracking, button state
  machines, polling) with server-held Blazor state.
- Convert real-time UI (notification bell, bot status, performance widgets) from
  client-managed SignalR + DOM patching to push-driven Blazor components.
- Establish a small, reusable Blazor component kit + interop bridges so future islands are cheap.
- Use SignalR/server-interactivity where it genuinely helps, and **debounce/throttle** so we
  don't trade JS bugs for chatty circuits.

**Non-goals (for this increment)**
- No .NET 9 upgrade. No routing/auth rewrite. No deletion of existing Razor Pages or controllers.
- No rewrite of audio-scrubbing / low-latency client behavior (TTS sliders, audio preview).
- No charting-library migration — Chart.js stays, reached via thin interop where needed.
- No mass port of all 58 partials — we build only the primitives the selected islands need.

---

## 3. Architectural approach: Blazor Server islands on .NET 8

### 3.1 Hosting model

Use the **classic Blazor Server** integration that is fully supported on .NET 8 for embedding
interactive components into an existing Razor Pages app:

```csharp
// Extensions/WebServiceExtensions.cs — additive
services.AddServerSideBlazor();      // circuit + /_blazor hub
services.AddCascadingAuthenticationState();
```

```csharp
// Program.cs — additive, alongside the existing endpoint maps
app.MapBlazorHub();                  // sits next to MapHub<DashboardHub>("/hubs/dashboard")
```

```html
<!-- Pages/Shared/_Layout.cshtml — one script, after the existing bundle -->
<script src="_framework/blazor.server.js"></script>
```

Islands are then dropped into any existing `.cshtml` via the **Component Tag Helper**:

```razor
@* e.g. inside Pages/Guilds/ModerationSettings/Index.cshtml *@
<component type="typeof(DiscordBot.Bot.Blazor.Pages.ModerationSettingsIsland)"
           render-mode="ServerPrerendered"
           param-GuildId="@Model.GuildId.ToString()" />
```

> **Why classic Blazor Server, not `MapRazorComponents<App>()`?** The new model wants a root
> `App.razor` and owns routing — that's the full-rewrite path. The classic model lets a Razor
> Page keep its route, layout, breadcrumb, and auth, and host *just one interactive region*.
> Minimal blast radius, no routing conflicts, no `App.razor`.

> **Discord snowflake gotcha** (per `CLAUDE.md`): pass guild/user IDs to islands as **strings**
> (`param-GuildId="@Model.GuildId.ToString()"`), never as `ulong`, to avoid JS precision loss
> during the prerender → interactive handoff.

### 3.2 Coexistence with the existing SignalR hub

The Blazor circuit is itself a SignalR connection (`/_blazor`). The existing `DashboardHub`
(`/hubs/dashboard`) and all 55 JS modules keep working untouched on non-Blazor pages.

For real-time **inside** an island, the component must **not** open a WebSocket back to our own
hub. Instead, introduce an in-process pub/sub and have the existing notifier services
**dual-publish** (additive — JS path unchanged):

```csharp
// New singleton; raised by existing notifiers in addition to IHubContext<DashboardHub>
public interface IDashboardEventBus
{
    event Action<HealthMetricsUpdateDto>     HealthMetrics;
    event Action<UserNotificationDto>        NotificationReceived;
    event Action<NotificationSummaryDto>     NotificationCountChanged;
    event Action<BotStatusDto>               BotStatusChanged;
    event Action<PerformanceIncidentDto>     AlertTriggered;
    // ...mirrors DashboardHub broadcasts
}
```

Islands subscribe in `OnInitialized`, marshal to the UI thread with
`InvokeAsync(StateHasChanged)`, and unsubscribe in `Dispose`. This keeps a single source of
truth for real-time events and avoids a second hub round-trip.

Notifier services to touch (dual-publish, ~7, all additive):
`DashboardNotifier`, `PerformanceNotifier`, `NotificationBroadcaster`,
`PerformanceMetricsBroadcastService`, plus audio/bulk-purge notifiers as those islands land.

### 3.3 Auth, DbContext, lifetime

- **Auth** — `AddCascadingAuthenticationState()` + the host page's existing `[Authorize]`
  policy already gate the island. For guild-scoped islands, call
  `IAuthorizationService.AuthorizeAsync(user, guildId, "GuildAccess")` inside the component
  (the `GuildAccessHandler` reads `RouteValues` today — when an island needs it outside a
  routed Razor Page request, pass `guildId` via `context.Resource`; a tiny handler tweak, only
  needed once we host islands on non-guild routes).
- **DbContext** — Blazor circuits are long-lived, so data-bound islands must use
  `IDbContextFactory<BotDbContext>` (create-use-dispose per operation) rather than a scoped
  context. Register `AddDbContextFactory` with a scoped fallback so all existing repositories
  keep working unchanged. This is the **one** infra change shared across data islands.
- **Circuit hygiene** — set `DisconnectedCircuitMaxRetained` / retention sensibly; show a
  reconnect UI (the default Blazor reconnect modal, themed). Each island disposes its event-bus
  subscriptions.

### 3.4 Reuse, don't rebuild

- **Toasts / theme** — wrap the existing `toast.js` and `theme.js` with thin
  `IJSRuntime` interop services (`ToastInterop`, `ThemeInterop`) so islands raise the *same*
  toasts and respect the *same* theme. No reimplementation.
- **Tailwind** — islands use the identical utility classes; `tailwind.config.js` already scans
  `.razor`. Zero CSS changes.
- **Charts** — keep Chart.js; a small `ChartInterop` (`create/update/destroy` by canvas id)
  called from `OnAfterRenderAsync` covers the few islands that chart.

---

## 4. Phase 0 — Foundation (the enabling slice)

Deliver these once; everything else builds on them.

| Item | File(s) | Notes |
|---|---|---|
| Blazor Server registration | `Extensions/WebServiceExtensions.cs`, `Program.cs` | `AddServerSideBlazor`, `MapBlazorHub`, blazor script in `_Layout` |
| `_Imports.razor` | `Blazor/_Imports.razor` | global usings (DI, auth, components) |
| Interop bridges | `Blazor/Interop/{ToastInterop,ThemeInterop,ChartInterop}.cs` | wrap existing JS |
| In-process event bus | `Blazor/Services/IDashboardEventBus.cs` (+ impl, singleton) | dual-publish target |
| DbContext factory | `Infrastructure/Extensions/ServiceCollectionExtensions.cs` | `AddDbContextFactory` + scoped fallback |
| Component kit (minimal) | `Blazor/Shared/*` | only what the first islands need (below) |
| Proof island | drop a trivial `[Authorize]` island into one existing page | verify auth, theme, Tailwind, prerender |

**Component kit — build only these first** (the 80/20 of the selected pages):

- `TabbedFormShell.razor` — tab strip + per-tab content + **centralized dirty-flag** +
  unsaved-changes guard + 3-state save button. Replaces the WeakMap button-state machine and
  `isDirty` bookkeeping duplicated across `settings.js`, `moderation-settings.js`.
- `ConfirmModal.razor` / `TypedConfirmModal.razor` — replaces `quick-actions.js` confirm flow.
- `FilterableTable.razor` + `Pagination.razor` — **debounced** filter inputs, server-side
  paging via `IDbContextFactory`. Backbone for Commands logs + Member directory.
- `Button.razor`, `FormToggle.razor`, `FormInput.razor`, `FormSelect.razor` — atomic ports of
  the existing partials (Tailwind classes copied verbatim).

> Place all Blazor code under a new top-level `src/DiscordBot.Bot/Blazor/` folder to avoid
> confusion with the existing `Components/` C# folder (`ComponentIdBuilder.cs`) and the
> `Pages/Shared/Components/` partials.

---

## 5. Selected targets

### 5.1 Components (reusable, cross-page)

| Component | Replaces | Real-time? | Rationale |
|---|---|---|---|
| **NotificationBell** | `notification-bell.js` (714 lines) | Yes (event bus) | Pure push + optimistic state; classic Blazor win. Lives in `_Layout` as one island. |
| **BotStatusCard / Banner** | `bot-status-refresh.js` (282 lines, **30s polling**) | Yes (event bus) | Replaces polling with push — strictly fewer requests. |
| **TabbedFormShell** | dirty/button logic in `settings.js`, `moderation-settings.js` | No | Eliminates the most duplicated, bug-prone JS pattern. |
| **ConfirmModal** | `quick-actions.js` modal/AJAX path | No | Reused by every destructive action. |
| **FilterableTable + Pagination** | `command-filters/pagination.js`, `member-directory.js` filter logic | No | Server-side, debounced; reused by logs + members. |

### 5.2 Pages (island conversions)

Ranked by ROI. Each becomes a Razor Page that hosts **one** Blazor island for its interactive body.

**Tier 1 — do first (form-centric, no charting/audio blockers):**

1. **Moderation Settings** (`/Guilds/{id}/ModerationSettings`) — *the proof slice*.
   Multi-tab (Overview/Spam/Content/Raid/Tags), simple/advanced toggle, per-tab save, tag CRUD,
   dirty-flag. Low state complexity (540 JS lines), guild-scoped, contained. Best first target:
   exercises `TabbedFormShell` + `ConfirmModal` + guild auth end-to-end with low risk.
2. **Admin Settings** (`/Admin/Settings`) — 7 tabs, 9 save handlers, button-state machine,
   bot status polling, typed-confirm shutdown. Replaces `settings.js` (1101 lines). Admin-only
   (low concurrency) so server round-trips are fine. Chunk the 54KB template into tab components.
3. **Commands** (`/Commands`) — three tabs (List/Logs/Analytics). Convert **List + Logs** to
   islands using `FilterableTable`/`Pagination`/`ConfirmModal` and the log-details modal.
   Keep the **Analytics** charts on Chart.js via `ChartInterop` (don't block on charting).
   Retires most of the 9 `command-*.js` modules.

**Tier 2 — next:**

4. **Member Directory** (`/Guilds/{id}/Members`) — `FilterableTable` + `Virtualize` for the
   list, bulk-select toolbar as component state, async member-detail modal. Replaces
   `member-directory.js` (496 lines).
5. **Soundboard portal grid** (`/Portal/Soundboard/{id}`) — replace the hand-rolled
   `VirtualSoundGrid` + `IntersectionObserver` with Blazor `<Virtualize>`; move
   search/sort/filter/favorites/upload state into the component. **Keep audio playback in JS**
   (a tiny `playSound(id)` interop) — low-latency client behavior stays client-side.

**Tier 3 — real-time widgets (after event bus proven by the bell):**

6. **Performance dashboard widgets** — convert the *live metric tiles* (health, system,
   command, alerts) to islands subscribing to `IDashboardEventBus`, retiring
   `performance/*-realtime.js`. **Defer the charts** (and `dashboard.js`, 41KB) — they stay
   Chart.js for now. This captures the real-time win without the charting rewrite.

### 5.3 Explicitly NOT in this increment

- **TTS portal** — audio preview + speed/pitch slider scrubbing need client latency. Leave in JS.
- **VOX portal** — trivial tab+play UI; no payoff.
- **Global autocomplete / search** — lightweight, cross-cutting; keystroke latency argues for JS.
- **Full Performance charting rewrite** — Chart.js via interop is good enough; revisit only if
  we commit to the full-rewrite north star.
- Any **.NET 9 upgrade**, page deletion, or controller removal.

---

## 6. SignalR & debouncing strategy

The user's explicit ask: *use SignalR/server interactivity intelligently, but debounce.*
Blazor Server already runs every UI event over the circuit's SignalR connection, so the risk is
**chattiness**, not transport. Concrete rules for these islands:

**Inbound (server → UI) real-time:**
- Subscribe islands to `IDashboardEventBus`, not a second hub connection.
- **Coalesce high-frequency streams**: health metrics arrive every 5s, system every 10s — fine
  as-is, but cap UI re-render to **≤ 1 Hz** per widget. If a burst arrives, drop intermediate
  frames (keep latest) before `StateHasChanged`.
- Keep the existing **sliding windows** (20-point sparkline, 100-point command charts) so memory
  is bounded; feed them server-side now.
- Use `@key` + targeted child components + `ShouldRender` so a metric tick re-diffs one tile,
  not the page.

**Outbound (UI → server) interaction:**
- **Debounce text/filter inputs ~250–300ms** (matches today's `command-filters` 300ms /
  soundboard 150ms search). Bind on `oninput`, gate with a `System.Threading.Timer` or a small
  `Debouncer` helper; cancel the in-flight query with a `CancellationTokenSource` before issuing
  the next (mirrors the existing `AbortController` pattern).
- **Avoid `@bind:event="oninput"` straight to a server query** — that's one round-trip per
  keystroke. Bind to a field, debounce, then query.
- For **autosave** (settings drafts), debounce ~1–2s (TTS draft autosave is 2s today) and skip
  the save if nothing is dirty.
- For **virtualized lists** use `<Virtualize ItemsProvider>` so only visible rows round-trip.
- Keep **optimistic updates** for notification mark-read/dismiss (as the JS does today): update
  local state immediately, reconcile/rollback on the server callback.

**Circuit cost guardrails:**
- One persistent circuit per open tab; islands are cheap but not free. Don't put islands on
  hot anonymous/public pages (e.g. the public leaderboard) — those stay static.
- Set reconnection UI + retained-circuit limits; dispose subscriptions on `IDisposable`.

---

## 7. Sequencing

```
Phase 0  Foundation: Blazor Server wiring, interop bridges, event bus,
         IDbContextFactory, minimal component kit, proof island.            (enabling)

Slice 1  Moderation Settings island  → proves TabbedFormShell + ConfirmModal
         + guild auth on a low-risk, self-contained page.                   (Tier 1)

Slice 2  NotificationBell + BotStatusCard islands → proves event-bus
         real-time + dual-publish; deletes polling + bell JS.               (real-time proof)

Slice 3  Admin Settings island (reuses shell/modal/bot-status).            (Tier 1)

Slice 4  Commands List+Logs islands (FilterableTable/Pagination/modal);
         Analytics stays Chart.js via interop.                              (Tier 1)

Slice 5  Member Directory island (FilterableTable + Virtualize).           (Tier 2)

Slice 6  Soundboard grid island (Virtualize; audio stays JS).              (Tier 2)

Slice 7  Performance live-tile islands (event bus); charts stay JS.        (Tier 3)
```

Each slice is independently shippable and revertible. Stop after any slice and the app is
consistent. After Slice 7, reassess whether to pursue the full-rewrite north star or hold.

---

## 8. Risks & mitigations (scoped to this increment)

| Risk | Severity | Mitigation |
|---|---|---|
| EF concurrency across long-lived circuits | High | `IDbContextFactory` in Phase 0; scoped fallback keeps existing code working |
| Guild auth handler reads `RouteValues` (null in circuit) | Med | Pass `guildId` via `context.Resource`; one-time handler tweak when island leaves a guild route |
| Snowflake precision loss in prerender params | Med | Pass IDs as strings (`CLAUDE.md` rule); never `ulong` to JS |
| Chatty circuits from un-debounced inputs | Med | §6 debounce/throttle rules are mandatory for every island |
| Dual SignalR (circuit + DashboardHub) confusion | Low | Additive; JS path untouched; islands use event bus not the hub |
| Prerender double-execution of `OnInitialized` | Low | Keep init idempotent; heavy work in `OnAfterRenderAsync` or guard with `firstRender` |
| Cookie expiry mid-circuit | Low | Add `RevalidatingServerAuthenticationStateProvider` (30-min revalidate) when first auth-sensitive island ships |
| Theme/toast divergence between JS & Blazor | Low | Islands call the *same* `toast.js`/`theme.js` via interop |

---

## 9. Testing & rollout

- **bUnit** component tests for `TabbedFormShell`, `FilterableTable`, `ConfirmModal`,
  `NotificationBell` (render, dirty-flag, debounce timing, event-bus updates).
- **Parity gate**: keep the old JS page reachable (feature flag or query param) until the island
  reaches visual + behavioral parity, then flip the default.
- **Manual regression** per slice: auth on the host page, theme switch, toast on save, reconnect
  behavior, and that non-Blazor pages + `DashboardHub` JS still work.
- **Load sanity** on the real-time islands: confirm coalescing caps re-render rate; watch
  per-circuit memory under a handful of concurrent admin sessions.

---

## 10. Documentation to update as slices land

- `CLAUDE.md` — drop the stale HTMX/Alpine mention; add the Blazor-island pattern + snowflake
  param rule.
- `.claude/agents/web-ui-portal.md` — note Blazor islands now exist under `Blazor/`.
- `docs/articles/component-api.md` — document the new Blazor component kit alongside the partials.
- This file + `blazor-migration-plan.md` — cross-link; mark which slices are done.
