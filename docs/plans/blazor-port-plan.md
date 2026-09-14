# Blazor Port — High-Level Plan

> **Status:** Approved for planning; decisions in §8 settled 2026-09-10
> **Date:** 2026-09-10
> **Scope:** Port the whole web UI (admin portal, guild pages, public audio portal, account pages) from Razor Pages + per-page JavaScript to Blazor. Implement the design system as Blazor components. Remove the legacy Razor Pages, their JavaScript, and the controllers that only exist to serve that JavaScript.
> **Companion:** [`blazor-port-inventory.md`](blazor-port-inventory.md) — the page, component, JavaScript, controller and hosting survey this plan is built on.
> **Supersedes:** the islands-first plan removed in `dbd59ce` and the unmerged `claude/blazor-ui-remaining-work-rhesdp` branch (see §3).

---

## 1. Summary

The UI today is 69 routable Razor Pages, 92 partials (56 of them the shared component library), 3 layouts, ~29,700 lines of hand-written browser JavaScript across 71 files, one SignalR hub, and 36 REST controllers, half of which exist only to feed that JavaScript and most of the rest of which it also calls. The design system is already token-based (CSS custom properties in `site.css`, Tailwind mapped onto them), which is the one part of the stack that ports unchanged.

Recommendation in one paragraph: build a **Blazor Web App on Interactive Server**, hosted in the existing `DiscordBot.Bot` process, using **per-page interactivity** (static SSR shell, each admin page opts into `InteractiveServer`, live chrome as small interactive islands). Components call the existing Core service interfaces directly, so the JSON-for-JavaScript controller layer and the JavaScript SignalR client both retire. Chart.js, browser audio preview, and file upload with progress stay in three small JS interop modules. Account and OAuth pages become static SSR components plus minimal-API endpoints, following the .NET Blazor Identity pattern, so the OAuth flow stays an HTTP round trip. Migrate area by area, deleting each `.cshtml` in the same PR as its replacement, with bUnit tests from the first component and a Playwright smoke suite from the first page.

Rough size: 6 phases, roughly 45–60 PRs, on the order of 55–85 engineering days. The previous attempt estimated 51–75 days and stopped less than half way, so treat these as ranges, not commitments.

---

## 2. What exists today (short form)

Full tables are in the inventory. The shape that matters for planning:

| Surface | Count | Notes |
| --- | --- | --- |
| Routable pages | 74 | Account 8, Admin 22, Guild 32, Portal 3, Commands 2, misc 4 (Index, Landing, Search, Components), Error 3. Was 69 at the survey; §2.1 lists the five added on `main` since. |
| Complexity | S 22 / M 26 / L 21 | L = charts, SignalR, audio, or heavy JS. Soundboard admin page is effectively XL |
| Shared components | 57 partials + 58 view models | ~39 are pure markup; ~18 carry real behaviour in JS. `_CurrencyWalletPanel` arrived after the survey (§2.1). |
| Layouts | `_Layout`, `_GuildLayout` (nested), `_LayoutLanding`, `Portal/_PortalLayout` | Three audiences: staff, guild members, anonymous |
| JavaScript | 77 files, 31,552 lines | A: 33 files replaced outright; B: 20 need a thin shim; C: 14 must stay (charts, audio, upload); D: 10 obsolete plumbing + 9 orphaned dead files (4,284 lines nobody loads, deleted in Phase 0). The six files added on `main` since the survey (1,896 lines, §2.1) are all class A. |
| SignalR | 1 hub, 5 group families, 19 push events, 11 client-invoked methods, 7 broadcaster services | Broadcasters are UI-agnostic; the JS client is the only consumer |
| Controllers | 41 concrete | P (page-backing only) 23, X (real REST resource) 4, B (both) 14. The five added on `main` since the survey are P by consumer analysis; §2.1 flags the currency three for a decision at port time. |
| Tests | 5,051 unit (was 3,353 at the survey) + 595 bUnit + 5 Playwright; ~150 PageModel handler tests are invalidated by the port; controller tests survive only for controllers that survive | bUnit and Playwright projects exist since Phases 1–2 |
| Tailwind | `content` globs already include `Pages/**/*.razor` and `Components/**/*.razor` | Tokens are `var(--color-*)`; no hex in components |

Things the survey found that are not Blazor problems but will bite the port if left as-is:

- **Two guild-access authorization handlers** exist; the registered one (`GuildAccessHandler`) does a live gateway lookup, the documented and unit-tested one (`GuildAccessAuthorizationHandler`) is DB-backed and never registered.
- `AlertsController` and `BotController` (including `POST /api/bot/shutdown`) have no role policy, only "authenticated".
- **Two generations of the Performance dashboard** ship side by side: the unified `Admin/Performance/Index` with AJAX tabs and five legacy single-tab pages, each with its own realtime JS.
- `Admin/AuditLogs/Index` and `Admin/MessageLogs/Index` are redirect stubs to `Admin/Logs`.
- 27 guild page models each independently load the guild, 404 on null, and build breadcrumb/header/nav; half use `GuildPageModelBase.PopulateGuildLayout`, half hand-roll it.
- Three "save" patterns coexist: classic form POST + TempData, `?handler=X` fetch returning JSON, and direct REST calls.
- Two card components, two tab systems, two toggle implementations, two toast containers, two confirmation modals, two activity feeds, three breadcrumbs, and an orphaned `FilterPanelViewModel`.
- Bot status is refreshed by two pollers and a SignalR push at the same time.
- The app exits at startup without `Discord:Token`, so there is no way to run the web host for browser tests.
- Antiforgery tokens are rendered on 13 specific pages, not globally; `[ApiController]` endpoints get no antiforgery validation. No CSP or security headers exist.
- `docs/architecture/patterns.md` describes a richer `GuildPageModelBase` and `ApiControllerBase` than the code has; `design-system.md`'s theme section predates the Graphite v2 restyle.

### 2.1 Changes on `main` since the survey (rebased 2026-09-13)

The branch was rebased onto `main` at `5d33d4b` (71 commits since the survey commit `2a43628`). Nothing changed in the hosting model, SignalR surface, layouts or design tokens; everything that did change is listed here and folded into the phase tables below.

- **Five new Razor Pages**, all fetch-to-controller with no charts and no SignalR: `Admin/Currency` (SuperAdmin; modals for create/edit/deactivate/mint authorities; hosts the wallet panel with no currency selected), `Admin/LlmUsage` (Admin; server-rendered cost breakdowns with date/guild/mode filters plus a client-side per-user drill-down), and `Guilds/Currency/{Index,Details,Prices}` (`GuildPageModelBase`, `_GuildLayout`, `_TabPanel` on Index and Prices). Every write goes through the new controllers; none of them has a POST handler.
- **One new shared partial**, `_CurrencyWalletPanel` (`CurrencyWalletPanelViewModel`): holder list, paged ledger and the mint/fine/adjust modal for one currency, filled by `currency-wallets.js`, shared by `Admin/Currency` and `Guilds/Currency/Details`. Its Blazor equivalent lands with the Currency cluster (Phase 4h), not retroactively in Phase 2.
- **Six new JavaScript files** (1,896 lines): `currency/currency-{manage,wallets,prices,reconcile}.js`, `llm-models.js` (679 lines, the lazily initialised "AI Models" tab of `Admin/Settings`) and `llm-usage.js`. All talk to controllers through `window.ApiClient`; all are class A.
- **Five new controllers**: `CurrenciesController` (`api/guilds/{guildId}/currencies`, 10 endpoints), `WalletsController` (`api/currencies/{id}/wallets`, 5), `PricesController` (`api/guilds/{guildId}/prices`, 4), `LlmModelsController` (`api/admin/llm-models`, 4), `LlmUsageController` (`api/admin/llm-usage`, 2). Their only consumers are the new page scripts, so they are P-class today. The currency three are also the only write path for the ledger (mint, fine, adjust, reconcile) and enforce `ICurrencyAccessService`; when the cluster is ported, decide whether they stay as a real resource (X) or retire with the access checks moving into the component's service calls.
- **Existing pages that grew**: `Admin/Settings` gained the AI Models tab (catalog, allowlist, per-mode defaults); `Guilds/AssistantSettings` gained a Tool Access checklist with a "using the default set" banner; `Guilds/AssistantMetrics` gained three server-rendered tables (cost by user, tool usage, prompt surface), still no charts; `Portal/Soundboard` renders a price badge on priced sounds and treats a 402 from the play endpoint as a warning toast; `Guilds/AudioModerationLog` now uses `_Badge` for the feature column and shows "Unknown" for entries without a user.
- **Shared partial changes already reflected in the Blazor components**: `_Badge` gained `IsPill` (ported to `Badge`); `voice-channel-panel.js` now resets to the disconnected state after a successful leave instead of waiting for the SignalR event (ported to `VoiceChannelPanel`); `_FormSelect` fixed attribute encoding, which Blazor's attribute splatting already does.
- **Shell**: `_Sidebar` gained "LLM Usage" (Admin) and "Currency" (SuperAdmin) entries; `Login` lost its brand side panel and is a single centred card; `Landing` swapped the Claude tile for OpenRouter. Phase 3's `MainLayout` sidebar and the Phase 4c account pages port the current markup.
- **Solution**: `src/DiscordBot.Agents` (the extracted agent engine) and `tests/DiscordBot.Evals` are new projects; both were moved to .NET 10 in the rebase so the Phase 0 upgrade still covers the whole solution. CI now skips build and tests on documentation-only changes (`paths-ignore`), which applies to the E2E job too.

---

## 3. The previous attempt, and what to keep from it

Four remote branches carry an earlier migration (`claude/bot-ui-blazor-plan-slot47`, `feature/Blazor-implementation`, `claude/blazor-modernization-handoff-hvw4au`, `claude/blazor-ui-remaining-work-rhesdp`). Verified against git:

- Only the Phase 0 foundation (two components, interop shim, service registration) ever reached `main`, via PR #1927. It was removed on 2026-09-09 in `dbd59ce` with the note "will be rescoped from scratch".
- The seven "island" slices in PR #1931 and everything after (an 8→10 framework upgrade, routed `App.razor`/layouts, ~33 shared components, 27 routed pages, 183 bUnit tests) live only on `rhesdp`. **They never merged.** The branch is 36 commits behind `main`.
- The Graphite v2 visual overhaul (`9df4fef`, 2026-09-02) landed after that branch point and restyled every component. All markup on the old branch was copied verbatim from pre-Graphite partials and is now visually stale. Graphite v2 is the design standard this port reproduces.

The old plan itself is not reused: it was poorly planned, islands-first, written against a different framework target, and its phase structure and estimates did not hold. This plan is built from a fresh survey of `main`. The old branch is only worth opening for a handful of concrete mechanisms and recorded gotchas, each of which should be re-derived critically against current code rather than cherry-picked (all paths on `rhesdp`):

| Asset | Why |
| --- | --- |
| `Blazor/Services/RevalidatingIdentityAuthenticationStateProvider.cs`, `CircuitClientInfoService.cs` | Small, correct, framework-version-agnostic. Circuits outlive the auth cookie; audit logging needs the client IP captured at circuit start. |
| `Blazor/Services/DashboardEventBus.cs` + interface | Per-handler exception isolation so one disposed circuit cannot break the publisher. |
| `Blazor/Common/Debouncer.cs` | Drop in as-is. |
| `Blazor/Interop/*.cs` + `wwwroot/js/blazor-interop.js` | The window-attached shim pattern. `toast.js`/`theme.js` declare top-level `const`s that `IJSRuntime` cannot reach. |
| `Blazor/Shared/TabbedFormShell`, `FilterableTable`, `ConfirmModal`, `Pagination` | Component **contracts** (`@typeparam TItem`, awaitable `ShowAsync() → Task<bool>` modals). Inner markup must be re-derived from current partials. |
| `App.razor`, `Routes.razor`, `Layout/GuildPageContext.cs`, `Layout/RedirectTo.razor` | Routing and cascading guild context wiring. |
| `tests/DiscordBot.ComponentTests/*` | bUnit project layout and the modal-await deadlock workaround. |
| Commit messages `f0fedc9..rhesdp` | Record of real bugs found: .NET 10 `blazor.server.js` initializer 404 on nested routes, the `discord:user_id` claim name, dead `OnPostSyncAsync`. |

Lessons carried into this plan (the full list is in the inventory): delete the `.cshtml` in the same commit as its replacement; add component tests with the first component, not as catch-up; keep charts, audio bytes and multi-MB uploads out of the circuit; snowflakes cross every boundary as strings; set an explicit trigger for removing any parity gate.

---

## 4. Target architecture

### 4.1 Hosting model

| Decision | Choice | Why |
| --- | --- | --- |
| Blazor flavour | **Blazor Web App, Interactive Server**. No WebAssembly, no Auto. | Page models inject `DiscordSocketClient`, `IDiscordChannelResolver` and ~60 service interfaces directly. Server render keeps that; WASM would require an API for every one of them. The app is one process anyway. |
| Interactivity placement | **Per page/component.** `<Routes />` stays static SSR. Each admin/guild/portal page declares `@rendermode InteractiveServer`. Shell chrome (sidebar, navbar, breadcrumb) is static SSR; live chrome (notification bell, bot status, toast host) are small interactive islands inside the static layout. | Works identically on .NET 8 and 10. Account, error, landing and leaderboard pages need `HttpContext` and are simply static (no exclusion attribute needed). The static shell gets `HttpContext` for theme resolution. Islands on one page share one circuit and one scoped DI scope, so a page and the toast host share `IToastService`. |
| Prerender | Prerender on for pages (first paint fast); use `PersistentComponentState` where `OnInitializedAsync` does non-trivial work, so it does not run twice. | Standard double-render mitigation. |
| Framework | **Upgrade to .NET 10 LTS first** (Phase 0), then port. | .NET 8 leaves support in Nov 2026, two months out. The Ubuntu archive the remote session hook installs from has `dotnet-sdk-10.0`, so remote sessions keep working with a one-line hook change. The old branch did 8→10 cleanly. This is a separate PR with its own risk (EF Core 10, Npgsql, Elastic APM majors) and its own Postgres caveat. |
| Data access in components | Inject the Core service interfaces as today. Services and `BotDbContext` are scoped; a circuit is one scope. For long-lived pages that hit the database on every event, resolve per operation via `IServiceScopeFactory` (the old branch's proven pattern) rather than holding one `DbContext` for the life of the circuit. | Avoids the stale-DbContext-in-circuit problem. |
| Coexistence | `MapRazorComponents<App>()` alongside `MapRazorPages()` and `MapControllers()` until the last page moves. Route-conflict rule: delete the `.cshtml` in the same PR that adds its `.razor`, **and** sweep `asp-page`/`RedirectToPage`/`Url.Page` references to it in that same PR, extending `DeletedPagesGuardTests`'s `DeletedPageRoutes` list (§5 Phase 3 deviation (e)/(f)). | The app is buildable and shippable after every PR. |
| Folder | `src/DiscordBot.Bot/Blazor/` with `App.razor`, `Routes.razor`, `_Imports.razor`, `Layout/`, `Shared/` (design system), `Pages/` (mirrors today's `Pages/` tree), `Interop/`, `Services/`. Add `./Blazor/**/*.razor` to the Tailwind `content` globs. | Repo precedent; avoids the clash with the existing `Components/ComponentIdBuilder.cs` (Discord interaction IDs). |

### 4.2 Authentication and authorization

- **Account pages become static SSR Razor components**, following the .NET Blazor Identity template: `Login`, `Lockout`, `AccessDenied`, `Privacy`, `Profile`, `LinkDiscord`, `ExternalLogin` as static `.razor` with `[CascadingParameter] HttpContext`, plus minimal-API endpoints for `PerformExternalLogin` (the Discord `ChallengeResult`) and `Logout`. The Discord OAuth middleware callback, `SignInManager` cookie writes, the `?authError=discord_unavailable|discord_expired|discord_error` contract, `returnUrl` sanitising and the cookie paths in `IdentityConfigOptions` are preserved exactly. jQuery unobtrusive validation and `_ValidationScriptsPartial` go away; `EditForm` + `DataAnnotationsValidator` replace them.
- **Auth state in circuits:** register a `RevalidatingServerAuthenticationStateProvider` that re-checks user existence, lockout and security stamp every 30 minutes, because a circuit outlives the cookie and today's per-request `DiscordClaimsTransformation` no longer runs per interaction.
- **Role policies** (`RequireSuperAdmin/Admin/Moderator/Viewer`) stay; pages carry `[Authorize(Policy = ...)]` and `Routes.razor` uses `AuthorizeRouteView`. `<AuthorizeView Policy/Roles>` replaces the `<authorize>` and `<require-role>` tag helpers one for one.
- **Guild access** becomes resource-based: one `GuildAccess` handler evaluating `context.Resource is ulong guildId`, called from a single `GuildContextProvider` in `GuildLayout` (replacing 27 per-page loaders). Consolidate the two existing handlers first (Phase 0): **cached membership (`UserDiscordGuild` / `UserGuildAccess`) is checked first; on a cache miss the handler falls back to a live `DiscordSocketClient` lookup and refreshes the cache.** Discord Administrator permission for the Admin role is read from the live guild user when available, from the cached record otherwise.
- **Portal three-state UX** (anonymous → landing view, authenticated non-member → forbidden view, member → portal) is reproduced inside `PortalLayout` as explicit state branching, not `[Authorize]` redirects.
- **Antiforgery:** `UseAntiforgery()` after `UseAuthorization()`; static SSR `EditForm` gets tokens automatically. Interactive pages call services, not endpoints, so the fetch-with-token pattern disappears. The few endpoints that remain reachable from the browser (upload, downloads, streams) are GET or carry `[ValidateAntiForgeryToken]` with the token supplied via the interop upload module.

### 4.3 Real-time

Keep the seven broadcaster services untouched. Add an in-process `IDashboardEventBus` that every broadcaster dual-publishes to alongside `IHubContext<DashboardHub>`. Blazor components subscribe to the bus (with per-guild filtering mirroring today's `guild-{id}` / `guild-audio-{id}` groups) and call `InvokeAsync(StateHasChanged)`. Every component that subscribes debounces to ≤1 Hz re-render and unsubscribes in `Dispose`. The JS SignalR client, `dashboard-hub.js`, and every `*-realtime.js` are class D and disappear with their pages. Once no Razor Page loads `dashboard-hub.js`, `DashboardHub` and its endpoint are deleted (no external consumer exists; the 23 hub tests go with it) and the broadcasters publish to the bus only. Event names become C# record types instead of the 19 magic strings.

### 4.4 JavaScript that survives

Three purpose-built modules under `wwwroot/js/blazor/`, each with a thin C# wrapper in `Blazor/Interop/` that imports it as an `IJSObjectReference`, gates calls to `OnAfterRenderAsync`, and swallows `JSDisconnectedException` on dispose:

| Module | Contents | Replaces |
| --- | --- | --- |
| `charts.js` (~300–400 lines) | `create(canvas, config)`, `update(handle, datasets)`, `destroy(handle)`, Graphite theme defaults from `performance/components/chart-utils.js`. Chart.js vendored via npm and copied into `wwwroot/lib` at build instead of 13 per-page CDN tags. | All 14 class-C chart files and every inline `new Chart(...)` block |
| `audio.js` (~150–250 lines) | `playPreview(url)`, `stopPreview()`, `getDuration(file)`, drag-and-drop relay, XHR upload with progress callback to a `DotNetObjectReference`. | Audio and upload slivers of `portal-soundboard.js` and `portal-tts.js` |
| `browser.js` (~400–600 lines) | localStorage get/set, clipboard write, focus/scroll helpers, focus trap for modals, `beforeunload` dirty guard, `matchMedia`, IANA timezone detection, click-outside registration, textarea selection get/set for the emphasis toolbar. | Scattered helpers in `navigation.js`, `search.js`, `theme.js`, `timezone.js`, `settings.js`, `member-directory.js`, `date-range-filter.js` |

Plus `ssml-markers.js` (83 lines, pure functions) kept client-side for per-keystroke responsiveness in the Pro TTS composer, and `shared/keyboard-shortcuts.js` kept nearly as-is behind a `RegisterShortcut` interop call. Everything else in `wwwroot/js` is deleted with the page that loads it. The pre-paint theme script and the sidebar-collapse FOUC guard stay as inline scripts in `App.razor`'s head, since they must run before any framework boots.

### 4.5 Controllers and endpoints

- **Retire** the 18 P-class controllers as their consuming pages migrate: `AnalyticsController`, `AudioController`, `BulkPurgeController`, `CommandsApiController` (HTML partials), `ModTagsController`, `NotificationsController`, `PerformanceTabsController` (HTML partials), `PreviewController`, `SoundsController` (except download/export), `UserPreferencesController`, and the eight Portal soundboard/TTS controllers except the endpoints listed next.
- **Keep** endpoints a browser needs a URL for: `GET .../sounds/{id}/audio` (preview stream), `POST .../sounds` (upload, used by `audio.js`), `POST /api/portal/tts/{g}/preview` (blob), `GET .../members/export`, `GET /Admin/Logs?handler=Export` equivalents as minimal-API file endpoints, `GET /api/guilds/{g}/sounds/{id}/download` and `export`, plus a new `GET /api/portal/vox/{g}/clips/{id}/audio` clip stream for the VOX preview (decision 6).
- **Keep** the X-class REST resources that are documented as an API (`GuildsController`, `MessagesController`, `ScheduledMessagesController`, `WelcomeController`) and the B-class ones with real resource shape (`AuditLogs`, `FlaggedEvents`, `ModerationCases`, `ModerationConfig`, `Watchlist`, `UserModeration`, `GuildMembers`, `CommandLogs`, `PerformanceMetrics`, `Alerts`, `Bot`, `Theme`, `Autocomplete`, `PortalVox` for the endpoints the VOX page needs). Whether these stay long-term is a product question (nothing external consumes them today; all ride the Identity cookie). The port does not depend on removing them.
- Update `docs/articles/api-endpoints.md` (its "Authentication: None" statement is already stale).

### 4.6 Design system as Blazor components

Contract rules for every component in `Blazor/Shared/`:

1. Parameters map from today's view models; **slots become `RenderFragment`** (`HeaderContent`, `Body`, `Footer`, `HeaderActions`), never HTML strings. Every raw-JS-string callback (`OnClick`, `DismissCallback`, `OnRemove`, `OnModeChange`, `OnFormatChange`, ...) becomes `EventCallback`/`EventCallback<T>`; the two view models that regex-validate JS identifiers lose that code.
2. `[Parameter(CaptureUnmatchedValues = true)]` for pass-through attributes.
3. Discord IDs are `string` in any parameter that reaches markup or interop; `ulong` only inside C# logic.
4. Styling uses the existing token vocabulary (`bg-bg-primary`, `text-accent-orange`, `.btn-primary`, ...) so a future reskin is a token edit, not a component rewrite. Component-scoped rules that live today in inline `<style>` blocks move to `.razor.css` isolation. No hex values in components.
5. Icons: one `<Icon Path="..." />` (Heroicons paths as constants in `IconPaths`), replacing the mix of inline SVG and view-model path strings.
6. Every component ships with a bUnit test and an entry on the `/components` showcase page.

Consolidations (port one component, not two): `Card` + `EnhancedCard` → `Card` with `Accent`; `NavTabs` + `TabPanel` → `TabGroup` (modes: in-page and navigation; AJAX mode dropped); `ConfirmationModal` + `TypedConfirmationModal` → `ConfirmModal` with `RequiredText`; `ActivityFeed` + `ActivityFeedTimeline` → `ActivityFeed`; three breadcrumbs → `Breadcrumb`; two toggles → `Toggle`; two toast containers → `ToastHost`; `FilterPanelTagHelper` + orphan view model → `FilterPanel`; `HighlightTagHelper` → `Highlight`.

Tiers, in build order (details per component are in the inventory §2):

| Tier | Components | Interop |
| --- | --- | --- |
| 1 Primitives | Button, Badge, Alert, Card, Skeleton, SkeletonCard, LoadingSpinner, EmptyState, StatusIndicator, StatusBadge, SeverityBadge, RuleTypeIcon, Icon, HeroMetricCard, GuildStatsCard, DashboardWidget, Breadcrumb, PageHeader, GuildHeader, Pagination, Kbd | none |
| 2 Forms | FormField, TextInput, Select (with optgroups), Toggle, TextArea, SettingField, Autocomplete (native rewrite), FilterPanel, SortDropdown, DateRangeFilter | `browser.js` for localStorage on DateRangeFilter |
| 3 Navigation & overlays | TabGroup, Modal, ConfirmModal, ToastHost + `IToastService`, PreviewPopover (user/guild), LoadingOverlay + `ILoadingState`, GuildContextSelector, Highlight, RestartBanner | `browser.js` (focus trap, click-outside, positioning) |
| 4 Live widgets | BotStatusBanner/Card (one subscription, no pollers), ActivityFeed, ConnectionStatus, NotificationBell, QuickActionsCard, ConnectedServersWidget, AuditLogCard, RecentActivityCard (make its refresh button work), CommandStatsCard, Chart, VoiceChannelPanel | `charts.js`; event bus |
| 5 TTS | VoiceSelector, StyleSelector, PresetBar, ModeSwitcher, SsmlPreview, EmphasisToolbar, PauseModal | `browser.js` textarea selection + clipboard; `ssml-markers.js` |

### 4.7 Layouts

| Layout | Render mode | Replaces | Contents |
| --- | --- | --- | --- |
| `MainLayout` | static SSR + islands | `_Layout`, `_Navbar`, `_Sidebar`, `_MobileSearchOverlay`, root `_ToastContainer` | Sidebar with `<AuthorizeView>` gating, navbar with search and user menu, `NotificationBell` island, `ToastHost` island, `LoadingOverlay`, theme resolved server-side via `IThemeService` |
| `GuildLayout` | inherits `MainLayout` | `_GuildLayout`, `GuildNavBarHelper` | Resolves guild once, cascades `GuildContext` (guild DTO, permissions, active tab, feature flags), renders breadcrumb, header, `TabGroup` nav |
| `PortalLayout` | static SSR + islands | `Portal/_PortalLayout`, `_PortalHeader`, `_PortalLanding`, `_PortalUnauthorized` | Three-state gate, keyboard shortcuts, `ToastHost`, user preferences service |
| `LandingLayout` | static SSR | `_LayoutLanding` | Marketing shell, no theme injection |
| `EmptyLayout` | static SSR | `Layout = null` pages | Error pages, `PublicLeaderboard` |

Error handling: `UseExceptionHandler("/Error/500")` and `UseStatusCodePagesWithReExecute("/Error/{0}")` keep pointing at routes, now served by static SSR components; the 500 page reads `IExceptionHandlerPathFeature` from the cascaded `HttpContext`. Add an `ErrorBoundary` in `MainLayout` for in-circuit exceptions.

---

## 5. Phases

Each phase is several PRs. One concern per PR. CI must be green on every PR. A page's `.cshtml`, `.cshtml.cs`, its PageModel tests and its page-specific JS are deleted in the PR that adds the `.razor` replacement. Estimates are engineering days and include tests and docs.

### Phase 0 — Pre-flight (no Blazor yet) · 6–10 days · 6–8 PRs

Independent clean-ups that shrink the port and remove ambiguity. Each is its own PR and can merge in any order.

1. **.NET 10 LTS upgrade.** All four projects, package majors, `global.json`, CI `setup-dotnet`, Dockerfile base images, session-start hook (`dotnet-sdk-10.0` is in the Ubuntu archive). Verify both migration sets still apply. Flag that the test suite says nothing about Postgres.
2. **Delete the 9 orphaned JS files** (4,284 lines): `server-analytics.js`, `engagement-analytics.js`, `moderation-analytics.js`, `command-analytics.js`, `api-metrics-chart.js`, `command-error-handler.js`, `command-loading-states.js`, `performance-shell.js`, top-level `performance-tabs.js`.
3. **Authorization fixes.** Add role policies to `AlertsController` and `BotController`. Consolidate `GuildAccessHandler` / `GuildAccessAuthorizationHandler` into one registered, tested handler: cache first, live gateway lookup on miss, and accepting `context.Resource is ulong`. Update `authorization-policies.md`.
4. **Web-only startup mode** (for example `Discord:Enabled=false` or a `--web-only` switch) so the host can run without a bot token against a seeded SQLite database. Prerequisite for Playwright.
5. **Retire the five legacy Performance pages** (`SystemHealth`, `HealthMetrics`, `Commands`, `ApiMetrics`, `Alerts` as standalone routes) in favour of the unified `Index` with redirects, and turn the two `AuditLogs/MessageLogs` index redirect stubs into route redirects. Removes five L-rated pages and ~1,900 lines of realtime JS from the port.
6. **Collapse bot-status pollers** onto the SignalR push (deletes `bot-status-refresh.js` and the 5-second poll in `settings.js`).
7. **Design-system baseline.** Graphite v2 is the standard and no major design changes are planned until the port completes; any visual change in the meantime goes through `site.css` tokens only. Refresh the stale theme section of `design-system.md` to match `site.css` ("Graphite", not "Discord Dark").

### Phase 1 — Foundation · 5–8 days · 2–3 PRs

1. Hosting: `AddRazorComponents().AddInteractiveServerComponents()`, `MapRazorComponents<App>()`, `UseAntiforgery()`, `App.razor` (head with fonts, `app.css`, pre-paint theme script, sidebar FOUC guard, `blazor.web.js`), static `Routes.razor` with `AuthorizeRouteView` and `RedirectToLogin`, `_Imports.razor`, `Blazor/` tree, Tailwind glob. Circuit options: `MaximumReceiveMessageSize`, `DetailedErrors` in Development only, `DisconnectedCircuitMaxRetained`.
2. Auth plumbing: revalidating auth state provider, `CircuitClientInfoService` (IP/UA capture for audit logging), resource-based guild authorization from Phase 0.
3. Services: `IDashboardEventBus` with dual-publish from the 7 broadcasters; `IToastService`; `ILoadingState`; `IThemeInterop`; `Debouncer`; `GuildContext` types.
4. Interop: `charts.js`, `audio.js`, `browser.js` with C# wrappers; Chart.js vendored via npm.
5. Observability: a `CircuitHandler` that logs circuit open/close with correlation IDs and records circuit counts in the existing metrics; note in `docs/articles/metrics.md` that in-circuit interactions do not pass through `ApiMetricsMiddleware`.
6. Tests: `tests/DiscordBot.ComponentTests` (bUnit, xUnit, FluentAssertions, Moq) wired into `DiscordBot.sln` and CI; `tests/DiscordBot.E2E` (Playwright, Chromium is pre-installed in remote sessions) with one smoke test that boots the host in web-only mode, logs in with a seeded admin, and loads `/`.
7. Probe page: `/blazor-probe` (admin-only, deleted at the end of Phase 2) proving interactive events, auth state, toast, theme interop, an event-bus subscription and a chart render. The old branch found a real .NET 10 regression here (`blazor.server.js` resolving `_blazor/initializers` relative to nested routes); add a Playwright check that a nested route such as `/Guilds/1/members` boots its circuit.

**Deferred from Phase 1.** Three items step 3 lists landed later than planned, each because it
has no real caller until the phase that needs it: `IThemeInterop` moves to Phase 3, alongside
the shell — theme switching lives in the layout/`Profile`, so there is nothing in Phase 1 to
call it from. `GuildContext` types move to Phase 3 with `GuildLayout`, the component that
actually cascades a `GuildContext` — defining the type earlier would mean carrying an unused
shape through Phase 2. `PersistentComponentState` is applied per page as each page is ported in
Phase 4 rather than added to the Phase 1 foundation, since it needs a real `OnInitializedAsync`
worth protecting from a double run to be worth wiring up. Separately, the probe page's "publish
test event" button (step 7) is admin-only, matching the rest of `/admin/blazor-probe`, and is
deleted with the probe page itself at the end of Phase 2 — it is not a permanent piece of UI.

### Phase 2 — Design system as Blazor components · 10–14 days · 6–8 PRs (one per tier, tier 4 split)

Build Tiers 1–5 from §4.6 in order. Each PR: components, bUnit tests, `.razor.css` where needed, showcase entries. The last PR of the phase replaces `Pages/Components.cshtml` with a Blazor `/components` showcase (the existing page enumerates every variant and is the parity check) and deletes the `.cshtml`. Also in this phase: `docs/articles/component-api.md` gains a Blazor section per component (or a new `blazor-components.md`), `ui-inventory.md` starts a Blazor components table.

Exit criterion: every one of the 56 partials has a Blazor equivalent or a documented merge target; no Razor Page has changed yet. (`_CurrencyWalletPanel`, the 57th, arrived on `main` after this phase was built; it is page-specific and ports with cluster 4h.)

**Delivered.** 62 components across 7 groups at `Blazor/Shared/` (see the "Blazor Components"
table in `ui-inventory.md` and the "Status" section of `blazor-components.md` for the per-tier
breakdown and documented fidelity deviations); 566 bUnit tests in
`tests/DiscordBot.ComponentTests`, plus one Playwright test in `tests/DiscordBot.E2E` covering the
`/components` showcase page end to end. `Pages/Components.cshtml` is retired; `/components` is now
a permanent Blazor route. Superseding the "deleted at the end of Phase 2" note on the Phase 1
probe/smoke pages (`/blazor-smoke`, `/admin/blazor-smoke`, `/admin/blazor-probe`, deliverable 7
above): they are **retained** through Phase 2, since nothing in this phase needed to touch them,
and stay in place until Phase 4 replaces them with real nested pages that exercise the same
hosting-foundation guarantees (nested-route circuit boot, auth-in-circuit) as a side effect of
being real product pages, rather than being deleted outright at a phase boundary with nothing yet
to replace their coverage.

### Phase 3 — Shell and layouts · 4–6 days · 2 PRs

`MainLayout`, `GuildLayout` + `GuildContext`, `PortalLayout`, `LandingLayout`, `EmptyLayout`; error pages 403/404/500 and `Landing` as the first real static SSR pages (delete their `.cshtml`); `Search` with `Highlight` as the first interactive page (delete `.cshtml`, `HighlightTagHelper` and its tests). From here two shells exist in parallel (`_Layout.cshtml` for un-migrated pages, `MainLayout.razor` for migrated ones); keep the phase-4 clusters moving quickly so the window is short, and route any shell change through both until Phase 5. `MainLayout`'s sidebar ports the current `_Sidebar`, which since the survey also carries "LLM Usage" (Admin) and "Currency" (SuperAdmin); `Landing` ports the current markup (OpenRouter tile, not Claude).

**Delivered.** All five layouts at `Blazor/Layout/`: `MainLayout` (static SSR shell with
`ToastHost`/`LoadingOverlay` declared directly as `@rendermode InteractiveServer` islands, a
`NotificationBell` island inside `MainNavbar`, an `ErrorBoundary` around `@Body`, and
`wwwroot/js/blazor/shell.js` — a classic script delegating sidebar collapse/mobile-drawer,
user-menu and mobile-search open/close, plus `GuildLayout`'s mobile nav `<select>`, off
`data-shell-action` attributes so the handlers survive enhanced navigation); `GuildLayout`
(`@layout MainLayout`, no route parameter of its own — reads the guild id off
`NavigationManager.Uri` via `GuildRoutes.TryGetGuildId` and calls the same memoised
`IGuildContextProvider` a page under it calls — renders `Breadcrumb`/`GuildHeader`/`TabGroup
Mode=Navigation StyleVariant=Pills` for desktop plus a native mobile `<select
data-shell-action="navigate-select">`, no `tab-panel.js`/`guild-nav.js`); `PortalLayout` (**not**
nested under `MainLayout`; the three/four-state gate — `GuildNotFound` / `ShowLanding` /
`NotGuildMember` / `Authorized` — over the same `IPortalAccessService` outcome
`PortalPageModelBase` already used, via the new memoising `IPortalContextProvider`); `LandingLayout`
(no chrome, `/landing` only); and `EmptyLayout` (retained, now an opt-in layout for the error pages
rather than `Routes.razor`'s default — see below). `Routes.razor`'s `AuthorizeRouteView` now reads
`DefaultLayout="typeof(MainLayout)"` and `NotFoundPage="typeof(NotFound)"`, so a routed page gets
the admin shell unless it opts out, and the .NET 10 `Router` renders `Error/NotFound.razor` (a real
HTTP 404) for a signed-in visitor who hits an unmatched route. `Blazor/Guilds/GuildContext.cs` +
`IGuildContextProvider`/`GuildContextProvider` (scoped, memoised per guild id for the DI scope's
lifetime, `GuildAccess` via resource-based `IAuthorizationService.AuthorizeAsync(user, guildId,
"GuildAccess")`) replace the ~27 independent `GuildPageModelBase` loaders, paired with
`GuildPageBase` (sealed `OnInitializedAsync`/`OnParametersSetAsync`, `PersistentComponentState`
round trip across the prerender-to-circuit boundary) and `GuildContextGate` (loading/not-found/
forbidden/child-content states, overridable per page). `IPortalAccessService`/`PortalAccessResult`
are extracted from `Pages/Portal/PortalPageModelBase.CheckPortalAuthorizationAsync`, which now
delegates to the service (resolved from `HttpContext.RequestServices` rather than a constructor
change, so every derived Portal page model stays untouched); `IPortalContextProvider`/
`PortalContextProvider` (the same memoising-wrapper shape as `GuildContextProvider`) and
`PortalPageBase` (mirrors `GuildPageBase` exactly) sit on top of it. `IThemeInterop`, deferred from
Phase 1, landed and is wired into `/admin/blazor-probe`'s theme select (no navbar theme toggle
exists yet, so the probe is its only caller today). First real pages: `/landing` (static SSR,
`LandingLayout`, `[AllowAnonymous]`), `/Error/403|404|500` (static SSR, `EmptyLayout`,
`[AllowAnonymous]`), and `/Search` (the first interactive page — `Highlight`, `[SupplyParameterFromQuery]`
for `?q=`, `PersistentComponentState` keyed by search term). Deletions: `Pages/Landing.cshtml(.cs)`,
`Pages/Error/{403,404,500}.cshtml(.cs)`, `Pages/Search.cshtml(.cs)`,
`TagHelpers/HighlightTagHelper.cs` — `Pages/Shared/_LayoutLanding.cshtml` stays, since `Login` and
the rest of Account still use it until Phase 4c. Test totals: 706 bUnit (up from 566 at the end of
Phase 2), 15 Playwright (up from 6: the new scenarios are `MainLayout` shell chrome on the Phase
1/2 probe pages for a seeded SuperAdmin, `GuildProbe` rendering `GuildLayout` for a seeded guild and
its unknown-guild not-found state, `PortalProbe` in web-only mode showing guild-not-found, anonymous
`/landing` rendering its hero without a login redirect, the Blazor 404 for an unmatched route,
`/Error/403`/`/Error/500` rendering anonymously, `/Search` logged in showing the Pages section, a
short-query validation message, and a logout-from-shell regression check), 5,163 unit.

**Deviations and follow-ups.**

- (a) An anonymous request to an unmatched route is redirected to `/Account/Login` by the global
  `FallbackPolicy` (`IdentityServiceExtensions`, `RequireAuthenticatedUser`) before Blazor's router
  ever runs — `MapRazorComponents`'s fallback endpoint carries no derivable `[AllowAnonymous]`
  metadata for that policy to see. So `/Error/404` is only reached anonymously via
  `UseStatusCodePagesWithReExecute` for a non-Blazor 404 (an unmatched Razor Page/controller route),
  and by a signed-in visitor otherwise, for an unmatched Blazor route. Accepted for this admin app;
  revisit in Phase 6 alongside the security headers.
- (b) `MobileSearchOverlay` is a plain `GET /Search` form with no live results — the legacy
  `search.js` recent-searches/incremental-results panes are not reproduced; out of scope for this
  round per plan §5 Phase 3's own "`Search` ... as the first interactive page" scoping.
- (c) `Search`'s Command Log/Audit Log rows carry `data-utc`/`data-format` spans that
  `wwwroot/js/timezone.js` converts client-side under the legacy shell, but nothing loads that
  script (or an equivalent) under `MainLayout` yet, so those two columns render in UTC for now.
  Cluster 4a — the first cluster with a timestamped list (`Admin/AuditLogs/Details`,
  `Admin/MessageLogs/Details`, `CommandLogs/Details`) — adds the `browser.js` timezone conversion
  (or a `LocalTime` component) and `Search`'s rows pick it up at the same time.
- (d) Two more temporary probe pages join the Phase 1 ones (`/blazor-smoke`, `/admin/blazor-smoke`,
  `/admin/blazor-probe`): `/Guilds/{guildId:long}/blazor-probe` (`GuildProbe.razor`, retained until
  4b lands a real `GuildLayout` consumer) and `/Portal/{guildId:long}/blazor-probe`
  (`PortalProbe.razor`, retained until 4f lands a real `PortalLayout` consumer).
- (e) `tests/DiscordBot.Tests/Bot/Pages/DeletedPagesGuardTests.cs` scans `Pages/**/*.cshtml(.cs)`
  for `asp-page`/`RedirectToPage`/`Url.Page` references to a route this round deleted
  (`/Landing`, `/Search`, `/Error/403`, `/Error/404`, `/Error/500`, in its `DeletedPageRoutes`
  list) — added after the review found `Pages/Account/Logout.cshtml.cs` and
  `Pages/Shared/_Navbar.cshtml` still referencing `/Landing`/`/Search` after their `.cshtml` was
  deleted, silently breaking (an empty form action; an `InvalidOperationException` at request time)
  because the tag helper/MVC routing has no compile-time link to a page that no longer exists.
  Every Phase 4 cluster that deletes another `.cshtml` extends `DeletedPageRoutes` with its own
  route(s) rather than adding a new ad-hoc guard test.
- (f) The route-conflict rule in §4.1 now reads: a `.cshtml` is deleted in the same PR that adds
  its `.razor`, **and** every `asp-page`/`RedirectToPage`/`Url.Page` reference to it is swept in
  that same PR, extending `DeletedPagesGuardTests`'s `DeletedPageRoutes` list — the §4.1 table cell
  below is updated to match.
- (g) `PortalLayout` loads `shared/keyboard-shortcuts.js`/`user-preferences.js` (class B) via
  `<HeadContent>` for every non-`GuildNotFound` state, matching the legacy layout's "always
  loaded regardless of state" behavior — but neither script is wired to anything yet
  (`RegisterShortcut`/`UserPreferences.init(guildId)`); that wiring is Phase 4f's job, since no real
  Portal page exists yet to drive it.

### Phase 4 — Page migration by cluster · 25–40 days · 25–35 PRs

Order is by rising complexity so the component library hardens on easy pages first. Each PR migrates one cluster (2–6 pages), deletes the `.cshtml`s, their PageModel tests and page-specific JS, retires any P-class controller whose last consumer went, adds bUnit page tests and one Playwright smoke path, and updates `ui-inventory.md` and `feature-map.md`.

| Cluster | Pages | Notes |
| --- | --- | --- |
| 4a Simple admin | `Admin/Users` ×4, `Admin/AuditLogs/Details`, `Admin/MessageLogs/Details`, `CommandLogs/Details`, `Account/Profile`, `Account/AccessDenied`, `Account/Lockout` | **Done.** Classic forms → `EditForm`; TempData flash → `IToastService`. Client-side JSON download on AuditLogs details → `browser.js`. First cluster with a timestamped list (`AuditLogs`/`MessageLogs`/`CommandLogs` details) — adds the `browser.js` timezone conversion (or a `LocalTime` component) that `Search`'s own `data-utc` rows (§5 Phase 3 deviation (c)) pick up at the same time. |
| 4b Simple guild | `Guilds/Edit`, `Welcome`, `AssistantSettings`, `AssistantMetrics`, `FeatureRequests` ×2, `Reminders`, `ScheduledMessages` ×3, `AudioModerationLog`, `RatWatch/Index` | **Done.** First consumers of `GuildLayout`. ScheduledMessages needs timezone capture via `browser.js` and the live preview pane. Standardise on one pagination state type here. `AssistantSettings` now has a Tool Access checklist (with the "default set" banner) and `AssistantMetrics` three more server-rendered tables; both are still S/M with no charts. |
| 4c Account | `Login`, `ExternalLogin`, `LinkDiscord`, `Logout`, `Privacy` + minimal-API endpoints | Static SSR. Preserve the `?authError` contract, `returnUrl` sanitising and `OnRemoteFailure` redirect. Verify with Playwright against a stubbed OAuth provider or a manual checklist. Remove jQuery and `_ValidationScriptsPartial`. Fix the dead `LoginWith2fa` branch (either remove or leave a documented no-op). `Login` is now a single centred card (the brand side panel and its CSS were removed on `main`); port that composition. |
| 4d Lists and settings | `Guilds/Index`, `Guilds/Details`, `Members/Index` (+ detail modal), `Members/Moderation`, `FlaggedEvents` ×2, `ModerationSettings`, `AudioSettings`, `Admin/Logs` (unified; stubs become redirects), `Admin/Notifications`, `Admin/BulkPurge` (wire real progress from the event bus), `Admin/UserPurge`, `Admin/Settings`, `Admin/LlmUsage`, `RatWatch/Incidents` | Three save patterns collapse to component methods calling services. `Admin/Settings` and `ModerationSettings` get `TabGroup` + dirty tracking via `EditContext` + `beforeunload` guard. CSV exports become minimal-API GET endpoints. `Admin/Settings`'s AI Models tab (`llm-models.js`: lazily loaded OpenRouter catalog, allowlist, per-mode defaults) becomes its own component inside the settings `TabGroup`, loading on first activation and calling the catalog service directly; `LlmModelsController` retires with it. `Admin/LlmUsage`'s per-user drill-down (`llm-usage.js`) becomes a paged component call; `LlmUsageController` retires with it. |
| 4e Dashboards and charts | `Index` (home), `Commands` (three tabs in one component, filter state in the query string via `NavigationManager`), `Guilds/Analytics` ×3 (custom heatmap becomes a component), `RatWatch/Analytics`, `Admin/RatWatchAnalytics`, `Admin/Performance` (one page, six tabs, event-bus live tiles) | `Chart` component + `charts.js`. Retire `CommandsApiController` and `PerformanceTabsController` HTML endpoints, `AnalyticsController`. |
| 4f Audio | `Guilds/Soundboard`, `Guilds/TextToSpeech`, `Guilds/VOX`, `Portal/Soundboard`, `Portal/TTS`, `Portal/VOX` | Hardest cluster. `audio.js` for preview and upload; `VoiceChannelPanel` on the event bus; Tier 5 TTS components; `<Virtualize>` for the sound grid. VOX browser preview (a commented-out stub today) is implemented: add the clip stream endpoint and reuse `audio.js`. Portal pages keep the three-state gate and the stream/upload endpoints. The portal sound card now carries `Price`/`CurrencySymbol` and shows the price badge; the play action surfaces a charge refusal (402 from the charge seam) as a warning toast, not a failure. |
| 4g Public | `Guilds/PublicLeaderboard` | `EmptyLayout`, anonymous, its own three-state gate. |
| 4h Currency | `Admin/Currency`, `Guilds/Currency/Index`, `Guilds/Currency/Details`, `Guilds/Currency/Prices` + the `CurrencyWalletPanel` component | Added on `main` after the survey (§2.1). One cluster because the four pages share `_CurrencyWalletPanel` and 1,062 lines of class-A currency JS. Needs `GuildLayout`, `TabGroup` and `Modal`, so any time after 4d. `CurrencyWalletPanel` is the one new shared component: holder list, paged ledger, mint/fine/adjust modal, with `CanMint`/`CanFine`/`CanAdminister` from `ICurrencyAccessService`; `Admin/Currency` hosts it with no currency selected until one is picked. `Prices` passes the row's feature key (`CurrencyFeatureKeys.Soundboard(soundId)`) through untouched. Decide here whether `CurrenciesController`/`WalletsController`/`PricesController` stay as a real ledger API (X) or retire (P) with their access checks moving into the component's service calls; the reconcile check stays server-side either way. |

**Cluster 4a delivered.** Ten pages ported, all under `Blazor/Pages/`: `Account/AccessDenied.razor`,
`Account/Lockout.razor`, `Account/Profile.razor` (+ `.razor.cs`), `Admin/Users/Index.razor` (+
`.razor.cs`), `Admin/Users/Create.razor` (+ `.razor.cs`), `Admin/Users/Edit.razor` (+ `.razor.cs`),
`Admin/Users/Details.razor` (+ `.razor.cs`), `Admin/AuditLogs/Details.razor` (+ `.razor.cs` and
`.razor.css`), `Admin/MessageLogs/Details.razor` (+ `.razor.cs`), `CommandLogs/Details.razor` (+
`.razor.cs`). Each deleted its `.cshtml`/`.cshtml.cs` pair and PageModel tests; the three
now-orphaned `ViewModels/Pages/User{List,Form,Detail}ViewModel.cs` went with them. New shared
pieces: `Blazor/Shared/Primitives/LocalTime.razor` + `wwwroot/js/blazor/localtime.js` (a faithful,
idempotent port of `wwwroot/js/timezone.js`'s `convertDisplayTimes`/`initTimezoneFields`, loaded
from `App.razor` after `shell.js`) and two new `BrowserInterop` methods,
`ConvertLocalTimesAsync()` (re-scans the document for `<LocalTime>` markup after an interactive
re-render — a static page needs nothing, since the script's own `DOMContentLoaded`/`enhancedload`
hooks cover it) and `DownloadFileAsync()` (Blob + object-URL download, replacing the AuditLogs
Details page's inline "Export JSON" script; the JSON body is built server-side with
`System.Text.Json`). `Search`'s own `data-utc` rows (§5 Phase 3 deviation (c)) pick up the same
conversion in the same change. `Blazor/Common/LocalUrl.cs` (`IsLocal`) validates a
`[SupplyParameterFromQuery] returnUrl` is a same-origin relative path before it is rendered into an
`href` or passed to `NavigationManager.NavigateTo` — added after review found `AuditLogs/Details`
echoing `?returnUrl=` unvalidated (an open-redirect/`javascript:` URI risk), and reusable by any
later cluster that echoes a return URL. `tests/DiscordBot.Tests/Bot/Pages/DeletedPagesGuardTests.cs`
was extended twice: once for this cluster's ten new routes in `DeletedPageRoutes`, and once (review
finding, see below) to also catch a *relative* `RedirectToPage("./Leaf")`/`asp-page="Leaf"`
reference to a deleted page, scoped to that page's own former folder so it doesn't false-positive
on an unrelated same-named page elsewhere (`Guilds/Edit`, `ScheduledMessages/Edit`, etc.).
`PageMetadataService`'s four `Admin/Users/*` entries claimed `RequireSuperAdmin`; the actual
`[Authorize]` policy on the Razor Pages they described was always `RequireAdmin` — fixed as part of
the port. `UsersSearchProvider.ViewAllUrl` built `?search=` against a page that binds `SearchTerm`,
so "View all" from global search silently dropped the term; fixed to build `?SearchTerm=`.
`Admin/Users/Edit` now gates the whole form on `IUserManagementService.CanManageUserAsync` (an
Admin acting on a SuperAdmin, or any actor acting on themselves through this path, renders an
access-denied `EmptyState` instead of the form) — new enforcement versus the legacy `EditModel`,
which rendered the form unconditionally and relied only on `Details.cshtml` hiding the "Edit User"
link. Reset-password and unlink-Discord are similarly gated. Test totals: 784 bUnit (up from 706 at
the end of Phase 3), 20 Playwright (up from 15: new scenarios `Test_P`–`Test_T` cover `Profile`
render/theme-save, `AccessDenied`/`Lockout` anonymous rendering, an `Admin/Users`
create/edit/details round trip, `AuditLogs`/`MessageLogs`/`CommandLogs` details rendering for
seeded rows, and the AuditLogs Export JSON download), 5,167 unit (at the end of the cluster's fix
round).

**Deviations and rules.**

- (a) An interactive page's not-found state renders the design-system `EmptyState` component at
  HTTP 200, not a real 404 — once a Blazor circuit is live there is no way to set a response status
  code. First applied here (`Admin/AuditLogs/Details`, `Admin/MessageLogs/Details`,
  `Admin/Users/Edit`, `Admin/Users/Details` for an unresolvable id); the repo-wide convention every
  later interactive page's not-found state follows.
- (b) The interactive `EditForm` rule, as finally verified end to end against a real host (not
  assumed) and written up in `docs/lessons-learned/blazor-editform-formname-race.md`: `EditForm`
  always emits `method="post"` regardless of `FormName`; do not set `FormName` unless a static
  no-JS/prerender-window fallback is deliberately implemented end to end (antiforgery token,
  `[SupplyParameterFromForm]`, a way to tell a real postback from a fresh load) — this admin
  console has no such case today, so `Create.razor`/`Edit.razor` carry no `FormName`. Every
  submit/action button on a freshly rendered interactive page is disabled
  (`disabled="@(!RendererInfo.IsInteractive)"`, with a "Connecting…" hint) until the circuit
  actually attaches, closing the window where a click reaches no handler at all. `tests/DiscordBot.E2E/BrowserTests.cs`
  waits for that button to become enabled (`Expect(button).ToBeEnabledAsync()`) instead of a blind
  `page.WaitForTimeoutAsync`. This is the rule every later cluster's `EditForm`s follow.
- (c) `data-utc` conversion needs nothing on a static SSR page (the document-level
  `DOMContentLoaded`/`enhancedload` scan in `localtime.js` covers it for free); an interactive page
  calls `BrowserInterop.ConvertLocalTimesAsync()` from `OnAfterRenderAsync` whenever it renders new
  or changed `<LocalTime>` rows, since the browser never re-fires those document events for an
  in-circuit re-render.
- (d) `PreviewPopover` was **not** wired up for the Author/Guild preview popups on
  `Admin/MessageLogs/Details` — they render as plain text instead. No Blazor-callable preview
  service exists yet; the lookup logic lives only in `PreviewController`, called today by
  `preview-popup.js`. Out of scope for this cluster; a follow-up for cluster 4d, when the Members
  pages need the same preview.
- (e) Review caught one real regression from this cluster's own deletions: `Pages/Account/Login.cshtml.cs`
  and `Pages/Account/ExternalLogin.cshtml.cs` both sent a locked-out sign-in to
  `RedirectToPage("./Lockout")` — a relative reference that threw once `Lockout` became a Blazor
  page with no Razor Page route behind it. Fixed to a literal `Redirect("/Account/Lockout")`, and
  is exactly why `DeletedPagesGuardTests` now matches the relative forms too, not just the absolute
  `"/Route"` form.
- (f) A password reset's generated temporary password is shown exactly once, in a dismissible
  success banner on `Admin/Users/Edit` with a copy-to-clipboard button
  (`BrowserInterop.CopyToClipboardAsync`) — it is never put in a toast, never logged (only the
  fact that a reset happened is logged, not the value), and never persisted anywhere beyond the
  component's in-memory state for that render.

**Cluster 4b delivered.** Twelve pages ported, all under `Blazor/Pages/Guilds/`: `Edit.razor` (+
`.razor.cs`), `Welcome.razor` (+ `.razor.cs`), `AssistantSettings.razor` (+ `.razor.cs`),
`AssistantMetrics.razor` (+ `.razor.cs`), `AudioModerationLog/Index.razor` (+ `.razor.cs`),
`RatWatch/Index.razor` (+ `.razor.cs`), `FeatureRequests/Index.razor` and `.../Details.razor`
(each + `.razor.cs`), `Reminders/Index.razor` (+ `.razor.cs`), `ScheduledMessages/Index.razor`,
`.../Create.razor` and `.../Edit.razor` (each + `.razor.cs`, the latter two sharing
`ScheduledMessageInputModel.cs`/`ScheduledMessageForm.razor`) — the first real
`GuildLayout`/`GuildPageBase`/`GuildContextGate` consumers, replacing the Phase 3
`GuildProbe.razor` probe in that role (`Guilds/Edit` inherits its Playwright coverage as
`Test_U`/`Test_V`). `Blazor/Common/PagedQuery.cs` (`PageNumber`/`PageSize`/`SortBy`/
`SortDescending`, clamped, with a `FromQuery`/`ToQueryString` pair) plus
`Blazor/Shared/Navigation/Pagination.razor` in link mode becomes the one pagination pattern for a
guild list page, replacing what would otherwise have been six ad hoc paging implementations; query
names are `pageNumber`/`pageSize`/`sortBy`/`sortDescending`, with `FromQuery`'s `legacyPage`
parameter accepting the old `?page=` links (bookmarks, the `Guilds/Details` widget) without a
redirect. `ScheduledMessages/Create` and `.../Edit` detect the viewer's IANA zone once via
`BrowserInterop.GetTimeZoneAsync()` after first render and convert with the existing
`Core/Utilities/TimezoneHelper.cs` (`ConvertFromUtc` to prefill, `ConvertToUtc` on submit); a
chosen local time that falls in a DST spring-forward gap (`TimeZoneInfo.ConvertTimeToUtc` throwing
`ArgumentException` for an instant that never occurs, e.g. `America/Toronto` 2026-03-08 02:30) is
caught and surfaced as an inline validation message instead of crashing the circuit.
`GuildPageBase` (`Blazor/Guilds/`) gained `RequestLocalTimeScan()`/a virtual `OnAfterRenderAsync`
override — a page calls it at the end of every load/reload so the next render re-triggers
`BrowserInterop.ConvertLocalTimesAsync()`, since `localtime.js`'s document-level scan only ever
fires once; lifted out of what had been `Admin/Users/Index.razor.cs`'s own copy so every guild
list/detail page gets it for free. Review (`ae54ddb`, written up in
`docs/lessons-learned/scheduled-message-repeated-update-tracking.md`) found that a Blazor circuit's
DI scope — and therefore its `BotDbContext` — lives for the whole circuit, so a second
fetch-mutate-`Update()` of the same entity in one circuit collides with the first call's still-
tracked graph; the fix is `Blazor/Common/ScopedOperations.cs` (`IServiceScopeFactory.RunAsync`
one/two-service, action/func overloads), applied to every mutation handler and the reload that
follows it across both this cluster and 4a — 22 call sites across this cluster's 9 guild page
files (31 in total including the 3 `Admin/Users` files from 4a). Sweep:
`GuildNavigationConfig`'s `feature-requests` tab `UrlPattern` (`/Guilds/{guildId}/FeatureRequests`,
which never matched the real route either) fixed to `/Guilds/FeatureRequests/{guildId}`;
`RemindersSearchProvider`'s result `Url` (was building `/Guilds/{guildId}/Reminders` instead of
`/Guilds/Reminders/{guildId}`); `PageMetadataService` gains a Feature Requests entry;
`Guilds/Details.cshtml`'s three `Url.Page(...)` widget links become literal hrefs; extends
`DeletedPagesGuardTests.DeletedPageRoutes`. Deletions: the six legacy `.cshtml`/`.cshtml.cs` pairs
from the first PR and six more from the second, `GuildProbe.razor`, and two page-model test files
— `GuildProbeTests.cs` and `Reminders/IndexModelTests.cs` (RatWatch's own `IndexModelTests.cs`
also goes, its behaviour ported into the new page's bUnit suite). New lessons-learned notes:
`docs/lessons-learned/scheduled-message-repeated-update-tracking.md` (the per-operation-scopes
fix above) and `docs/lessons-learned/sqlite-migration-context-mismatch.md` (see the next section).
Test totals measured after the cluster's fix round: 853 bUnit, 5,204 unit, 23 Playwright (two of
the 4a-era `Test_I`/`Test_J` GuildProbe scenarios were retired and replaced by `Test_U`/`Test_V`,
and `Test_W`/`Test_X`/`Test_Y` cover the ScheduledMessages create/list/edit/delete round trip,
Reminders list/cancel, and FeatureRequests list/details/approve respectively).

**Deviations and follow-ups.**

- (a) `AssistantMetrics`'s route matches none of `GuildNavigationConfig`'s tabs (the "assistant"
  tab points at `AssistantSettings`'s URL instead), so `GuildLayout` falls back to the guild's own
  name for the header/breadcrumb rather than the legacy page's custom "Home > Servers > Guild >
  Assistant > Metrics" trail — there is no separate "Metrics" breadcrumb entry. Accepted as a
  cluster 4b fidelity deviation.
- (b) `preview-trigger` hover previews are not wired in the Blazor shell. `Reminders/Index`,
  `RatWatch/Index` and `AudioModerationLog/Index` all carry the same `preview-trigger`
  class/`data-preview-*` attributes the legacy pages did, but `App.razor` loads only
  `blazor.web.js`, `shell.js` and `localtime.js` — never `wwwroot/js/preview-popup.js` — so the
  markup is inert; a Blazor-native `PreviewPopover` component exists (`Blazor/Shared/Overlays/`)
  but nothing in this cluster calls it. Left as a follow-up for cluster 4d, when the Members pages
  need the same preview.
- (c) `Repository<T>.UpdateAsync`'s `DbSet.Update(entity)` marks every `Include`d navigation as
  `Modified` too, not just the root entity — saving a `ScheduledMessage` (fetched with `.Include(s
  => s.Guild)`) also rewrites the `Guilds` row it came with, even though nothing about the guild
  changed. Per-operation scopes (deviation above) don't make this better or worse; it is
  pre-existing behaviour, tracked as a follow-up for a separate PR, not fixed as part of this one.
- (d) Discord-dependent data (channel lists, resolved usernames) renders empty, or as a raw
  Discord id, in the web-only E2E host used for Playwright (no gateway connection) — `Welcome`'s
  channel dropdown, `ScheduledMessages`' channel `Select`, and `Reminders`'/`RatWatch`'s/
  `AudioModerationLog`'s username columns all degrade this way. `tests/DiscordBot.E2E/BrowserTests.cs`
  therefore covers these pages' shells, navigation and validation, not the actual Discord content;
  the round-trip tests (`Test_W`/`Test_X`/`Test_Y`) seed rows directly via
  `BotHostFixture.DatabasePath` rather than through the Discord-backed create flow.

**Found during 4b: SQLite migrations never ran past January 2026 (upstream bug).**

Before this cluster's own work, `main`'s `AddInfrastructure` registered the **base**
`BotDbContext` for the SQLite provider, while every SQLite migration after
`20260127225612_AddSsmlSupportToGuildTtsSettings` carries `[DbContext(typeof(SqliteBotDbContext))]`
— the Sqlite/Postgres migration-set split (CLAUDE.md "Database and migrations"). EF Core's
migrator matches a migration to a context by exact runtime type, so `Program.cs`'s startup
`db.Database.MigrateAsync()`, which resolved the base type, silently stopped applying migrations
at 40 of 59 and returned successfully — no exception, no `PendingModelChangesWarning`, `/health`
green. **Every SQLite database created or upgraded since late January 2026 — the default provider
— has therefore been missing** `FeatureRequests`, `LlmUsageRecords`, `AudioPlaybackLogs`,
`UserPreferences`, `VoxMessageHistory`, `UserSoundFavorites`, the `EnabledTools`/`ToolNames`
columns, and the `Themes`/`PerformanceAlertConfigs` seed rows, among everything else added in the
last several months. It looked intermittent only because different pages touch different missing
tables — this cluster's own `FeatureRequests` Playwright test is what surfaced it.

What this branch does about it:

- **The registration fix.** `AddInfrastructure`'s SQLite branch now mirrors the Postgres branch:
  `AddBotDbContext<SqliteBotDbContext>(...)` plus a forwarding `AddScoped<BotDbContext>(sp =>
  sp.GetRequiredService<SqliteBotDbContext>())`.
- **`Infrastructure/Data/Migrations/SqliteLegacyHistoryRepair.cs`**, run from `Program.cs`
  immediately before `MigrateAsync`. Fixing the registration alone would brick every database
  already running under the bug: its `__EFMigrationsHistory` holds exactly the 40 legacy ids and
  none of the current lineage's, so the migrator would treat the whole re-baseline
  (`20260219205009_AddIsEnabledToGuildModerationConfig`, a from-scratch snapshot of the schema the
  legacy chain had already built) as pending and crash on `CREATE TABLE "ApplicationSettings"`
  against a table that already exists. The repair detects that pre-fix state, adds the one column
  the re-baseline actually introduces (`GuildModerationConfigs.IsEnabled`, defaulted to `1` so a
  guild that had moderation configured before the flag existed stays moderated), verifies every
  table/column the re-baseline declares already exists (reading the migration's own
  `UpOperations`, not a hand-copied list), and stamps the re-baseline as applied so the remaining
  migrations apply normally. It throws with recovery guidance (back up, then either
  `dotnet ef database update --context SqliteBotDbContext` or delete the file and let a fresh
  database be created) on a history that carries only *some* of the legacy ids; it logs a Warning
  and stands aside on a history with none of either lineage's ids. It is a no-op on a fresh
  database, an already-repaired one, and PostgreSQL.
- **Two idempotent seed migrations**, since the re-baseline recreated tables without their
  original seed data (not part of a model-diff snapshot):
  `Migrations/Sqlite/20260914060035_SeedDefaultThemesSqlite.cs` (the two default themes) and
  `Migrations/Sqlite/20260914065633_SeedPerformanceAlertConfigsSqlite.cs` (the eight default alert
  thresholds, copied row for row from `Migrations/Postgresql/20260219132220_InitialPostgresql.cs`).
  Both use `INSERT OR IGNORE` so they no-op on a database that already has the rows.
- **Tests.** `tests/DiscordBot.Tests/Infrastructure/Extensions/ServiceCollectionExtensionsTests.cs`'s
  `AddInfrastructure_WithSqliteConnectionString_ResolvesSqliteBotDbContext` now actually asserts
  `BeOfType<SqliteBotDbContext>()` (it asserted `BeOfType<BotDbContext>()` before, despite its own
  name). `tests/DiscordBot.Tests/Infrastructure/Data/SqliteLegacyHistoryRepairTests.cs` (13 test
  methods) builds the pre-fix on-disk state by migrating the base `BotDbContext` into a temp file
  and proves the fix against it: `MigrateAsync_OnLegacyDatabaseWithoutRepair_Throws` proves the
  crash without the repair; `RepairedDatabase_HasTheSameSchemaAsOneMigratedFromScratch` compares
  the repaired database against a from-scratch one object by object (`sqlite_master` plus `PRAGMA
  table_info`, including indexes and foreign keys); the rest cover idempotency, the fresh-database
  and empty-history no-ops, existing-data preservation, seed de-duplication, and the two refusal
  paths (partial legacy history, and a legacy history whose schema doesn't match the baseline).

**Recommendation for the user:** ship the registration fix, the repair, and the two seed
migrations to `main` as their own hotfix PR (and tag a release) ahead of, or independently from,
this port branch — every SQLite deployment is affected today, not just guild pages this port
happens to touch. PostgreSQL is untested for this change (CLAUDE.md: "There is no PostgreSQL test
path"), though the registration fix mirrors a pattern the Postgres branch already used and the two
seed migrations are SQLite-only by design (Postgres's initial migration already seeds both tables).
See `docs/lessons-learned/sqlite-migration-context-mismatch.md` for the full investigation and
`docs/articles/database-schema.md` for the upgrade note this needs.

### Phase 5 — Decommission · 4–6 days · 3–4 PRs

1. Remove `MapRazorPages()`, `AddRazorPages()`, the `Pages/` tree, `_ViewImports`/`_ViewStart`, `TagHelpers/`, `GuildPageModelBase`/`PaginatedPageModel`/`PortalPageModelBase`, `TempDataExtensions`, jQuery, `wwwroot/js` except `blazor/`, `shared/keyboard-shortcuts.js`, `ssml-markers.js`; `package.json` `test` script and `__tests__/api-client.test.js`.
2. Remove `DashboardHub`, `SignalRServiceExtensions`' hub mapping (keep `AddSignalR` only if Blazor needs the options), the JS SignalR CDN tag, and the retired P-class controllers (including `LlmModelsController` and `LlmUsageController` from 4d, and the currency three if 4h retired them); broadcasters publish to the bus only. Delete the corresponding tests.
3. View models: keep the types now used as component parameters (move to `Blazor/Shared/Models` or leave in `ViewModels/`), delete the rest with their tests.
4. Docs and agents: rewrite `docs/architecture/ui-inventory.md`, the Razor Pages, Guild Page Model Base and API Controller Base sections of `patterns.md`, `feature-map.md`, `service-catalog.md`, `component-api.md`, `api-endpoints.md`, `signalr-realtime.md`, `configuration-guide.md` (web-only mode, circuit options), `testing-guide.md` (bUnit, Playwright), `.claude/agents/web-ui-portal.md`, and `CLAUDE.md` (the "UI is Razor Pages, no Blazor" gotcha, build notes, test counts). Add `docs/lessons-learned/blazor-port.md`.

### Phase 6 — Hardening · 3–5 days · 2–3 PRs

- Security headers and a CSP (`script-src 'self'`, `connect-src 'self' wss:`, `frame-ancestors 'none'`, nonces for the two inline head scripts). Clean slate: nothing exists today.
- Circuit limits and rate limiting on the Blazor hub endpoint; measure memory per circuit on the audio and performance pages under a synthetic 50-circuit load.
- Accessibility pass on modals, tabs, popovers and toasts (focus order, `aria-*`, reduced-motion).
- Remove any parity gate left over, with the trigger written down in advance (for example "14 days default-on with no regression").

---

## 6. Testing strategy

| Layer | Tool | Rule |
| --- | --- | --- |
| Components | bUnit in `tests/DiscordBot.ComponentTests` | One test class per shared component from its first PR; page components get tests for each state (loading, empty, error, populated) with mocked services. Beware the modal-await deadlock pattern the old branch hit: hold un-awaited show/click tasks. |
| Services and controllers | existing xUnit project | Unchanged. The 150 PageModel tests are deleted with their pages, not rewritten; their behavioural coverage moves to component tests. |
| Browser | Playwright in `tests/DiscordBot.E2E` | Boots the host in web-only mode on SQLite in-memory with seeded admin, one happy path per migrated cluster (login, navigate, act, assert). Runs in CI after unit tests; also runnable in remote sessions (Chromium is pre-installed). |
| Real-time | bUnit + event bus | Publish a bus event, assert the component re-rendered; no SignalR in tests. |
| Visual | Playwright screenshots of `/components` per tier | Catches Tailwind purge regressions (a class only used in `.razor` vanishing from `app.css`). |

Background-service tests already have thread-pool starvation rules in CLAUDE.md; bUnit's renderer dispatcher adds another: never block on `InvokeAsync` inside a test.

---

## 7. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Design restyle mid-port invalidates markup | No major design work planned until the port completes; components use tokens only; showcase screenshots per tier |
| Long-lived branch drifts from `main` | No long-lived branch. Every phase is small PRs to `main`, app shippable after each |
| Two shells to maintain during Phase 4 | Keep 4a–4d short; any shell change is applied to both layouts in the same PR |
| Circuit memory: `DiscordSocketClient` and guild caches referenced from many components | Resolve per operation via `IServiceScopeFactory`, subscribe/unsubscribe in `Dispose`, measure in Phase 6 |
| Auth state stale inside a circuit (role change, ban) | Revalidating provider at 30 min; `Logout` forces circuit teardown |
| Framework upgrade breaks Postgres or Elastic APM | Separate Phase 0 PR; manual Postgres migration check; APM package major verified against the .NET 10 host |
| OAuth regression in 4c | Static SSR + endpoints exactly as the Identity template; Playwright against a stub provider; manual sign-in checklist in the PR |
| Tailwind purges classes only used in `.razor` | Glob added in Phase 1; showcase screenshot test |
| `.NET 10 blazor.server.js` initializer 404 on nested routes | Known from the old branch; Playwright nested-route boot check in Phase 1 |
| Upload of 10 MB sound files through the circuit | Never: `audio.js` posts to the retained upload endpoint with progress |
| Remote agent sessions cannot install the SDK | `dotnet-sdk-10.0` confirmed in the Ubuntu archive; update `.claude/hooks/session-start.sh` in the upgrade PR |

---

## 8. Decisions (settled 2026-09-10)

| # | Decision | Outcome | Effect on the plan |
| --- | --- | --- | --- |
| 1 | Upgrade to .NET 10 first | **Yes** | Phase 0 item 1 is the first PR; everything else targets `net10.0`. |
| 2 | Retire the five legacy Performance pages and the two redirect stubs | **Yes** | Phase 0 item 5; cluster 4e ports one Performance page, not six. |
| 3 | Guild-access semantics | **Cache first, live gateway lookup on miss** | Phase 0 item 3 consolidates onto one handler with that order; `GuildContextProvider` uses it. |
| 4 | Charting | **Chart.js via interop** for now; re-evaluate a native library later if needed | `charts.js` + `Chart` component in Phase 1/2; Chart.js vendored via npm. |
| 5 | Retire `DashboardHub` and page-backing controllers at the end | **Yes** | Phase 5 step 2 as written. |
| 6 | VOX browser preview | **Implement** | Cluster 4f adds a clip stream endpoint and wires preview through `audio.js`. |
| 7 | Design changes during the port | **None planned** until the migration completes; Graphite v2 is the standard | Phase 0 item 7 is a baseline, not a negotiation; components reproduce Graphite v2 exactly. |
| 8 | Folder | **`Blazor/`** | As written in §4.1. |

---

## 9. Out of scope

- Two-factor authentication UI (the `LoginWith2fa` redirect in `Login` points at a page that does not exist today; the port removes or documents the dead branch, it does not build 2FA).
- Any visual redesign. The port reproduces Graphite v2 as it is.
- Replacing Tailwind with CSS isolation wholesale. Isolation is used only for the component-scoped rules that are inline `<style>` blocks today.
- WebAssembly or offline support.
- Discord-side features (commands, voice, assistant) are untouched.

---

## 10. Definition of done

- `src/DiscordBot.Bot/Pages/` no longer exists; `MapRazorPages` is gone; `wwwroot/js` contains only the interop modules listed in §4.4.
- Every route in the inventory resolves to a Blazor component with the same authorization and, for the Portal, the same three-state behaviour.
- `dotnet test DiscordBot.sln` runs unit, component and (in CI) Playwright suites green.
- Docs in §5 Phase 5 step 4 updated; `CLAUDE.md` describes the Blazor UI.
- No parity gates remain.
