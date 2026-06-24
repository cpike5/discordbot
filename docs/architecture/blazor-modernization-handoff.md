# Blazor Modernization — Agent Handoff

> **Purpose.** Pick up the islands-first Blazor modernization (PR #1927) cleanly.
> Phase 0 (foundation) is done, verified, and pushed. Slice 1 is the next unit of
> work. The environment now provisions itself via a SessionStart hook, so you can
> build and test immediately.

**Branch:** `claude/bot-ui-blazor-plan-slot47`  ·  **PR:** #1927  ·  **Base:** `main`

---

## 1. TL;DR — where things stand

| Commit | What |
|---|---|
| `3895d4a` | The plan: `docs/architecture/blazor-modernization-selective-plan.md` |
| `bdaeb7c` | **Phase 0** — Blazor Server islands foundation (builds clean, 0 errors) |
| `bf688a8` | SessionStart hook that installs the .NET 8 toolchain on the web |

Nothing is half-finished. Phase 0 is a complete, self-contained increment. The
**FoundationProbe** island on `/Components` proves circuit + interop + component
parity end-to-end.

> **Slice 1 status: DONE.** `Bot/Blazor/Pages/ModerationSettingsIsland.razor` now owns the
> `/Guilds/{id}/ModerationSettings` interactive body, replacing
> `wwwroot/js/moderation-settings.js`. It added the reusable kit pieces `TabbedFormShell`,
> `ConfirmModal`, `SaveButton` (+ `TabDefinition`) under `Blazor/Shared/`, and a
> `setUnsavedGuard` helper in `blazor-interop.js`. The legacy JS UI stays reachable at
> `?legacy=true` (parity gate). Nested-route circuit start uses
> `Blazor.start({ configureSignalR: b => b.withUrl('/_blazor') })` instead of a global
> `<base href>` (which would break the layout's `#main-content` skip link). **Your job is
> now Slice 2** (NotificationBell + BotStatusCard — see §5 / plan §5.1).

Read these first (already written, don't redo):
- `docs/architecture/blazor-modernization-selective-plan.md` — the plan, phasing, debounce rules, risks.
- `docs/architecture/blazor-migration-plan.md` — the *maximalist* north-star (NOT what we're doing now).

---

## 2. Environment — it sets itself up

A SessionStart hook (`.claude/hooks/session-start.sh`, registered in
`.claude/settings.json`) runs automatically on web sessions and:
- installs `dotnet-sdk-8.0` from the **Ubuntu archive** (the Microsoft
  `dotnet-install` CDN, `builds.dotnet.microsoft.com`, is **blocked by egress
  policy — do not try to use it or route around it**);
- best-effort installs Node/npm + `npm install` for Tailwind (falls back to
  `SkipTailwind=true` if Node is missing);
- warms `dotnet restore`.

If you're in a fresh container and `dotnet` isn't found yet, the hook may still be
running (synchronous) or you can run it manually:
```bash
CLAUDE_CODE_REMOTE=true CLAUDE_PROJECT_DIR="$PWD" CLAUDE_ENV_FILE=/tmp/env.sh \
  ./.claude/hooks/session-start.sh
```

### Verified commands (use these to gate every slice)

```bash
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# Restore (once per session; the hook already does this)
dotnet restore DiscordBot.sln

# Build — full solution (Node present → Tailwind runs)
dotnet build DiscordBot.sln -c Debug --no-restore

# Build — Bot only, no Node needed (skips the npm/Tailwind MSBuild targets)
dotnet build src/DiscordBot.Bot/DiscordBot.Bot.csproj -c Debug --no-restore \
  -p:SkipTailwind=true -p:CI=true

# Test — scope to one class while iterating
dotnet test tests/DiscordBot.Tests/DiscordBot.Tests.csproj -c Debug --no-build \
  --filter "FullyQualifiedName~AuditLogsControllerTests"

# Lint — SDK formatter (CI has no separate linter; analyzers run during build)
dotnet format DiscordBot.sln --no-restore --include <files...> --verify-no-changes
```

Baselines on `bf688a8`: solution build **0 errors** (71 pre-existing warnings),
sample test class **22/22 passing**, `dotnet format` clean on the new files.

---

## 3. What Phase 0 added (inventory)

All new Blazor code lives under `src/DiscordBot.Bot/Blazor/` (a new top-level
folder — deliberately separate from the existing `Components/` C# folder and the
`Pages/Shared/Components/` partials).

| File | Role |
|---|---|
| `Blazor/_Imports.razor` | Global usings for `.razor` files |
| `Blazor/Interop/ToastInterop.cs` | Scoped service → existing `toast.js` |
| `Blazor/Interop/ThemeInterop.cs` | Scoped service → existing `theme.js` |
| `Blazor/Common/Debouncer.cs` | Server-side trailing-edge debounce (use for filter/search inputs) |
| `Blazor/Shared/UiButton.razor` | Blazor twin of `_Button.cshtml` (same Tailwind tokens) |
| `Blazor/Shared/UiToggle.razor` | Blazor twin of `_FormToggle.cshtml` (toggle CSS inlined) |
| `Blazor/Pages/FoundationProbe.razor` | PoC island; **delete once trusted** |
| `wwwroot/js/blazor-interop.js` | `window.blazorInterop` shim (see gotcha §6.1) |

Wiring changes:
- `Extensions/WebServiceExtensions.cs` — `AddServerSideBlazor()`,
  `AddCascadingAuthenticationState()`, registers `ToastInterop`/`ThemeInterop`.
- `Program.cs` — `app.MapBlazorHub()` next to `MapHub<DashboardHub>`.
- `Pages/Components.cshtml` — hosts the probe via `<component … render-mode="ServerPrerendered">`
  and loads `blazor-interop.js` + `~/_framework/blazor.server.js` in its Scripts section.

---

## 4. How an island is wired (the pattern to repeat)

1. Build the interactive component(s) under `Blazor/Pages/` (no `@page` directive —
   these are embedded, not routed). Reuse `Blazor/Shared/*` primitives.
2. Host it in the existing Razor Page's `.cshtml`:
   ```razor
   <component type="typeof(DiscordBot.Bot.Blazor.Pages.MyIsland)"
              render-mode="ServerPrerendered"
              param-GuildId="@Model.GuildId.ToString()" />   @* IDs as STRINGS — see §6.2 *@
   ```
3. In that page's `@section Scripts`, add (once per host page):
   ```html
   <script src="~/js/blazor-interop.js" asp-append-version="true"></script>
   <script src="~/_framework/blazor.server.js"></script>
   ```
4. The page keeps its route, layout, breadcrumb, and `[Authorize]` — the island
   inherits them. Auth inside the island: inject `AuthenticationStateProvider` or
   use `<AuthorizeView>` (cascading auth state is registered).
5. Data access: create a DI scope per operation and resolve the **existing**
   services (see §6.4) — do **not** rewire the global `DbContext`.

---

## 5. Slice 1: Moderation Settings — ✅ DONE (reference for the pattern)

> Completed. The build order, services, DTOs and gotchas below are kept as the worked
> example to copy for later slices. What shipped: `ModerationSettingsIsland` composing
> `TabbedFormShell` (5 tabs) + per-tab `SaveButton` + `ConfirmModal` (tag-delete &
> unsaved-switch guard), data via `IServiceScopeFactory`, hosted in `Index.cshtml` behind a
> `?legacy=true` parity gate. **Next: Slice 2** (NotificationBell + BotStatusCard — event
> bus + dual-publish, plan §3.2/§5.1).

**Target:** `/Guilds/{guildId:long}/ModerationSettings`
**Page:** `Pages/Guilds/ModerationSettings/Index.cshtml(.cs)`
**JS it replaces:** `wwwroot/js/moderation-settings.js` (540 lines)

Why this is the proof slice: self-contained, guild-scoped, low client-state
complexity, exercises the tab-form + confirm-modal + guild-auth path that the
later settings/commands slices all reuse.

### Shape of the page (already mapped)
- Tabs: **Overview, Spam, Content, Raid, Tags**; Simple/Advanced toggle per tab.
- Page handlers (reuse these via services, don't reinvent): `OnPostSaveOverviewAsync`,
  `OnPostSaveSpamAsync`, `OnPostSaveContentAsync`, `OnPostSaveRaidAsync`,
  `OnPostApplyPresetAsync`, `OnPostCreateTagAsync`, `OnPostDeleteTagAsync`,
  `OnPostImportTemplatesAsync`.
- Services injected by the page: `IGuildModerationConfigService` (`GetConfigAsync`,
  `UpdateConfigAsync`, `ApplyPresetAsync`), `IModTagService` (`GetGuildTagsAsync`,
  `CreateTagAsync`, `DeleteTagAsync`, `ImportTemplateTagsAsync`),
  `IGuildService`, `IFlaggedEventService`, `DiscordSocketClient`.
- DTOs to reuse (do not redefine): `GuildModerationConfigDto`, `OverviewUpdateDto`,
  `SpamDetectionConfigDto`, `ContentFilterConfigDto`, `RaidProtectionConfigDto`,
  `ApplyPresetDto`, `ModTagCreateDto`. Auth: `[Authorize(Policy="RequireAdmin")]`
  + `[Authorize(Policy="GuildAccess")]` (already on the page).

### Recommended build order
1. Add the reusable kit pieces this slice needs (these were deferred from Phase 0
   to land with their first consumer): `Blazor/Shared/TabbedFormShell.razor`
   (tab strip + centralized dirty-flag + unsaved-switch guard + 3-state save
   button) and `Blazor/Shared/ConfirmModal.razor` (+ `TypedConfirmModal` for the
   tag-delete confirm). Mirror the markup/classes in `_ConfirmationModal.cshtml`.
2. Build `Blazor/Pages/ModerationSettingsIsland.razor` composing the shell + one
   child component per tab. Load config in `OnInitializedAsync` via a scoped
   service call; save per-tab via the existing `IGuildModerationConfigService`.
   Debounce nothing here (form saves are explicit), but use the dirty-flag.
3. Replace the page body in `Index.cshtml` with the `<component>` host; add the two
   scripts to its Scripts section. Keep the page model's `OnGetAsync` for the
   guild header/breadcrumb/stats it already computes (the island only owns the
   interactive settings body).
4. Parity gate: keep the old JS-driven markup reachable behind a flag/query param
   until the island matches, then flip the default (plan §9).

### Gate before committing Slice 1
- `dotnet build DiscordBot.sln -c Debug` → 0 errors.
- `dotnet format … --verify-no-changes` clean on new files.
- Manually reason through: auth on the host page, theme, toast on save,
  reconnect, and that the page still renders its header/stats.

Then continue with the plan's slice order: **2** NotificationBell + BotStatusCard
(introduces the in-process event bus + notifier dual-publish — see plan §3.2/§5.1),
**3** Admin Settings, **4** Commands, **5** Member Directory, **6** Soundboard
grid, **7** Performance live tiles.

---

## 6. Gotchas & constraints (learned the hard way)

**6.1 Toast/theme are not on `window`.** `toast.js`/`theme.js` declare
`ToastManager`/`ThemeManager` as top-level `const`s, so they're in global lexical
scope but **not** properties of `window` — `IJSRuntime.InvokeVoidAsync("ToastManager.show")`
fails. `blazor-interop.js` is the window-attached shim that bridges them; it must
load **after** toast.js/theme.js (the layout renders those first, so putting it in
the page's `@section Scripts` is correct). Call toasts only from event handlers /
`OnAfterRenderAsync`, never during prerender (no JS runtime then).

**6.2 Discord snowflake IDs are strings in JS.** Per `CLAUDE.md`, pass guild/user
IDs to islands as strings (`param-GuildId="@Model.GuildId.ToString()"`), never as
`ulong`, to avoid precision loss in the prerender→interactive handoff. Parse back
to `ulong` inside the component.

**6.3 `blazor.server.js` is loaded per host page, not globally.** Keeps circuits
off pages without islands. Only add it to pages that host a `<component>`.

**6.4 Don't rewire the global `DbContext`.** Circuits are long-lived, so a
circuit-scoped `DbContext` would be a bug. The chosen low-blast-radius approach:
inject `IServiceScopeFactory`, and per operation do
`using var scope = factory.CreateScope();` then resolve the existing scoped
service/repository. This reuses all existing business logic and touches no DI
registration. (The plan mentions `IDbContextFactory` as an alternative — prefer
the scope-factory route unless a component needs raw `DbContext`.)

**6.5 Nested-route islands need a `<base href>`.** `/Components` is top-level so
`blazor.server.js` resolves `/_blazor` fine. For islands on nested routes (e.g.
`/Guilds/123/ModerationSettings`), add `<base href="~/" />` to the layout `<head>`
**or** configure the hub path explicitly, or the circuit negotiate URL resolves
wrong. Verify this when you host the first nested-route island (Slice 1).

**6.6 Scoped CSS bundle isn't linked.** The app has no prior `.razor`, so the
layout doesn't link `DiscordBot.Bot.styles.css`. `UiToggle` therefore **inlines**
its `<style>` (matching the partial) instead of using `UiToggle.razor.css`. Either
keep inlining component CSS, or add the bundle `<link>` to the layout if you adopt
CSS isolation broadly.

**6.7 Tailwind already globs `.razor`** (`tailwind.config.js` content includes
`./Components/**/*.{razor,cshtml}` and `./Pages/**/*.{razor,cshtml}`). New class
combos in `.razor` files are picked up on the next CSS rebuild (needs Node).

**6.8 Egress policy.** Microsoft download CDNs are blocked (403). Ubuntu archive,
npmjs, nuget.org all work through the proxy. Don't retry/route around 403/407 —
report blocked hosts.

---

## 7. Background research (don't re-run)

Four area maps were produced during planning and are the basis for the plan:
SignalR/real-time architecture, startup/DI wiring, the rendering/component stack,
and per-page Blazor-fit profiling. Their conclusions are folded into
`blazor-modernization-selective-plan.md`. The one SignalR hub is `DashboardHub`
(`/hubs/dashboard`); 7 notifier services push to it; for islands you'll add an
in-process `IDashboardEventBus` that those notifiers dual-publish to (Slice 2).

---

## 8. PR etiquette for this branch

- Commit per slice; end commit messages with the established trailers
  (`Co-Authored-By:` + `Claude-Session:`).
- Don't merge or change base. The PR (#1927) is open against `main`.
- The SessionStart hook only applies to sessions on this branch until #1927 merges
  into `main`.
