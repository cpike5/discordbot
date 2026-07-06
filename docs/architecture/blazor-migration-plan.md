# Blazor Server Migration Plan

> **Status (2026-07): superseded as the working plan.** The islands increment
> ([`blazor-modernization-selective-plan.md`](./blazor-modernization-selective-plan.md)) is
> complete, and the updated path to this document's end-state — revised for .NET 10 LTS, the
> shipped islands, and pages added since this was written — is
> [`blazor-completion-plan.md`](./blazor-completion-plan.md). This document remains the
> north-star reference for the full end-state rationale.

## Context

The Discord bot admin portal is built on Razor Pages with 63 vanilla JavaScript files (~25,500 lines) handling all client-side interactivity via `fetch()` calls to 29 API controllers and manual DOM manipulation. Despite documentation mentioning HTMX/Alpine.js, neither is actually used. The architecture works but the JS-heavy approach creates significant maintenance overhead and poor developer experience when adding new features.

This plan migrates the entire portal to Blazor Server on .NET 9 using an incremental, phased approach where Razor Pages and Blazor coexist throughout. The existing custom Tailwind design system is preserved. Chart.js is retained via JS interop. Portal pages migrate last with a redesign opportunity.

## Scope

| Category | Count |
|----------|-------|
| Razor PageModels | 68 |
| .cshtml files (total) | 154 |
| Shared components + ViewModels | 55 + 106 |
| JavaScript files | 63 (~25,500 lines) |
| API controllers | 29 |
| SignalR hub | 1 (DashboardHub, 1,380 lines, 20+ methods) |
| Server-side hub notifiers | 7 services |

## Key Decisions

- **Hosting model**: Blazor Server (Interactive Server rendering)
- **Target framework**: .NET 9
- **UI library**: Custom Tailwind components (port existing)
- **Charts**: Chart.js via JS interop
- **Portal pages**: Migrate last with redesign
- **DbContext strategy**: `AddDbContextFactory` with scoped fallback for backward compatibility
- **Auth pages**: Login, Logout, ExternalLogin, LinkDiscord stay as Razor Pages permanently (require HTTP redirects)
- **SignalR transition**: In-process `IDashboardEventBus` for Blazor components; existing JS hub path maintained during transition

---

## Phase 0: .NET 9 Upgrade

**Goal**: Upgrade all three projects from .NET 8 to .NET 9 with zero functional changes.

### Changes

**Project files** (TFM `net8.0` -> `net9.0`):
- `src/DiscordBot.Core/DiscordBot.Core.csproj`
- `src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj`
- `src/DiscordBot.Bot/DiscordBot.Bot.csproj`

**Docker**:
- `Dockerfile` -- base images `sdk:8.0` -> `sdk:9.0`, `aspnet:8.0` -> `aspnet:9.0`
- Create `global.json` to pin SDK version

**Package upgrades**:

| Package | From | To |
|---------|------|----|
| `AspNet.Security.OAuth.Discord` | 8.0.0 | 9.0.0 |
| `Microsoft.EntityFrameworkCore.*` | 8.x | 9.x |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 8.0.0 | 9.0.0 |
| `Microsoft.Extensions.Identity.Stores` | 8.0.0 | 9.0.0 |
| `Microsoft.Extensions.Hosting.Systemd` | 8.0.0 | 9.0.0 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.x | 9.x |
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.x | 9.x |
| `Elastic.Apm.*` | 1.29.0 | Latest compatible |
| `OpenTelemetry.*` | 1.14.0 | Latest 1.x |
| `Swashbuckle.AspNetCore` | 6.5.0 | Latest |
| `Serilog.AspNetCore` | 8.0.0 | Latest |

**Breaking change audit**:
- Verify `Npgsql.EnableLegacyTimestampBehavior` still works on EF Core 9
- Run both migration contexts (SQLite + PostgreSQL)
- Verify Elastic APM .NET 9 compatibility (highest risk package)

### Verification
- `dotnet build` / `dotnet test`
- Manual: login, navigate major pages, verify Docker build

---

## Phase 1: Blazor Foundation

**Goal**: Add Blazor Server infrastructure alongside Razor Pages. Both systems serve routes simultaneously. Prove with a test page.

**Depends on**: Phase 0

### 1.1 Service Registration

**Modify**: `src/DiscordBot.Bot/Extensions/WebServiceExtensions.cs`

Add to existing `AddWebServices()`:
```csharp
services.AddRazorComponents()
    .AddInteractiveServerComponents();
services.AddCascadingAuthenticationState();
```

### 1.2 Middleware Pipeline

**Modify**: `src/DiscordBot.Bot/Program.cs`

After `app.UseAuthorization()`:
```csharp
app.UseAntiforgery(); // Required by Blazor in .NET 9
```

After existing `app.MapRazorPages()` and `app.MapHub<DashboardHub>(...)`:
```csharp
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

`MapRazorPages()` stays -- it continues serving existing pages.

### 1.3 Create Blazor Root Files

| New File | Purpose |
|----------|---------|
| `src/DiscordBot.Bot/Components/App.razor` | Root HTML shell (`<html>`, `<HeadOutlet>`, `<Routes>`), theme FOUC prevention script, Tailwind CSS refs |
| `src/DiscordBot.Bot/Components/Routes.razor` | `<Router>` with `<AuthorizeRouteView>` wrapping |
| `src/DiscordBot.Bot/Components/_Imports.razor` | Global usings for all .razor files |
| `src/DiscordBot.Bot/Components/Layout/MainLayout.razor` | Primary layout (mirrors `_Layout.cshtml` structure) |
| `src/DiscordBot.Bot/Components/Layout/PortalLayout.razor` | Portal layout (mirrors `_PortalLayout.cshtml`) |
| `src/DiscordBot.Bot/Components/Layout/GuildLayout.razor` | Guild sub-layout with guild sidebar/breadcrumb |
| `src/DiscordBot.Bot/Components/Layout/RedirectToLogin.razor` | Handles unauthenticated users |

**Auth wrapping in Routes.razor**:
```razor
<CascadingAuthenticationState>
    <Router AppAssembly="typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)">
                <NotAuthorized><RedirectToLogin /></NotAuthorized>
            </AuthorizeRouteView>
        </Found>
    </Router>
</CascadingAuthenticationState>
```

### 1.4 IDbContextFactory Migration

**Modify**: `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

Replace `AddDbContext` with `AddDbContextFactory` (or `AddPooledDbContextFactory`) for both providers. Keep scoped fallback so all existing code works unchanged:

```csharp
// SQLite
services.AddPooledDbContextFactory<BotDbContext>((sp, options) => { /* same config */ });

// PostgreSQL - needs adapter wrapping IDbContextFactory<PostgresBotDbContext> as IDbContextFactory<BotDbContext>
services.AddPooledDbContextFactory<PostgresBotDbContext>((sp, options) => { /* same config */ });
```

`AddDbContextFactory` auto-registers a Scoped `DbContext` from the factory, so Identity services, repositories, and all existing code continues working. New Blazor components use `IDbContextFactory<BotDbContext>` directly.

**Fix direct DbContext consumers** (2 files):
- `src/DiscordBot.Bot/Services/SearchService.cs` (line 27)
- `src/DiscordBot.Bot/Controllers/PreviewController.cs` (line 24)

### 1.5 Guild Authorization Handler Refactor

**Modify**: `src/DiscordBot.Bot/Authorization/GuildAccessHandler.cs`, `GuildAccessAuthorizationHandler.cs`, `PortalGuildMemberAuthorizationHandler.cs`

Current handlers extract `guildId` from `HttpContext.Request.RouteValues`, which is null in Blazor circuits. Refactor to check `context.Resource` first (for Blazor callers), then fall back to `IHttpContextAccessor`:

```csharp
if (context.Resource is ulong resourceGuildId)
    guildId = resourceGuildId;
else
    // existing HttpContext extraction fallback
```

Blazor components call: `AuthorizationService.AuthorizeAsync(user, guildId, "GuildAccess")`

### 1.6 RevalidatingAuthenticationStateProvider

**Create**: `src/DiscordBot.Bot/Components/Services/RevalidatingAuthStateProvider.cs`

Blazor Server circuits can outlive cookie expiry. Implement a `RevalidatingServerAuthenticationStateProvider` that periodically validates the user still exists and is active (every 30 minutes).

### 1.7 Circuit-Scoped IP Service

**Create**: `src/DiscordBot.Bot/Components/Services/CircuitClientInfoService.cs`

Capture client IP from the initial HTTP request (`HttpContext.Connection.RemoteIpAddress`) and store it for the circuit's lifetime. Audit logging can reference this for in-circuit actions.

### 1.8 Proof-of-Concept Test Page

**Create**: `src/DiscordBot.Bot/Components/Pages/BlazorTest.razor`

Minimal `@page "/blazor-test"` with `[Authorize(Policy = "RequireAdmin")]` that renders inside MainLayout, shows authenticated user info, injects a service, and validates Tailwind classes render correctly. Remove after verification.

### Verification
- `dotnet build` / `dotnet test`
- `/blazor-test` renders with correct layout, auth, and styling
- Existing Razor Pages (`/`, `/Admin/Settings`, etc.) still work
- Login/logout flow unchanged
- SignalR dashboard-hub still connects on existing pages

---

## Phase 2: Component Library

**Goal**: Port all 55 shared `.cshtml` components to `.razor` Blazor components. Create JS interop wrappers.

**Depends on**: Phase 1

### Directory Structure

```
src/DiscordBot.Bot/Components/
  App.razor, Routes.razor, _Imports.razor
  Layout/
    MainLayout.razor, PortalLayout.razor, GuildLayout.razor
  Shared/           # Component library (.razor files)
  Interop/          # JS interop C# services
  Pages/            # Migrated pages (later phases)
```

### Component Tiers

**Tier 1 -- Atomic/Pure Render (25 components, no JS, all parallelizable)**:
Badge, Button, Alert, Card, EnhancedCard, EmptyState, LoadingSpinner, Skeleton, SkeletonCard, StatusIndicator, StatusBadge, SeverityBadge, RuleTypeIcon, FormInput, FormSelect, FormToggle, Pagination, GuildBreadcrumb, CommandBreadcrumb, GuildHeader, CommandHeader, HeroMetricCard, DashboardWidget, RestartBanner, PageLoadingOverlay

**Tier 2 -- Interactive (9 components, need JS interop or C# event handling)**:
ConfirmationModal, TypedConfirmationModal, PauseModal, TabPanel, NavTabs, AutocompleteInput, SortDropdown, PresetBar, ToastContainer

**Tier 3 -- Complex/Stateful (22 components, SignalR or multi-component)**:
ActivityFeed, ActivityFeedTimeline, BotStatusCard, BotStatusBanner, ConnectionStatus, ConnectedServersWidget, GuildStatsCard, CommandStatsCard, QuickActionsCard, RecentActivityCard, AuditLogCard, CommandLogDetailsModal, VoiceChannelPanel, VoiceSelector, ModeSwitcher, StyleSelector, EmphasisToolbar, SsmlPreview, UserPreviewPopup, GuildPreviewPopup, PreviewPopupLoading, PreviewPopupError

### Porting Pattern

| Razor Partial | Blazor |
|---------------|--------|
| ViewModel properties | `[Parameter]` properties in `@code {}` |
| `Html.Raw(content)` | `@((MarkupString)content)` |
| `onclick="..."` strings | `@onclick="Handler"` with `EventCallback` |
| `AdditionalAttributes` dict | `@attributes="AdditionalAttributes"` |
| `@model BadgeViewModel` | `[Parameter] public string Text { get; set; }` (flat params for simple) |
| Tailwind classes | Identical -- no change |

### JS Interop Services

**Create in `src/DiscordBot.Bot/Components/Interop/`**:

| Service | Purpose |
|---------|---------|
| `ToastInterop.cs` | Wraps existing `toast.js` |
| `ThemeInterop.cs` | Wraps existing `theme.js` |
| `ChartJsInterop.cs` | New `blazor-chart-interop.js` for Chart.js lifecycle |
| `TimezoneInterop.cs` | Wraps existing `timezone.js` |
| `ClipboardInterop.cs` | Clipboard API |
| `NavigationInterop.cs` | Sidebar toggle, scroll |

**Create**: `src/DiscordBot.Bot/wwwroot/js/blazor-chart-interop.js` -- exposes `create`, `update`, `destroy` for Chart.js instances by canvas ID.

### Verification
- Visual regression: compare old partial vs new Blazor component side-by-side
- All Tier 1 components render correctly with Tailwind
- Toast interop shows notifications from Blazor
- Chart.js interop renders a test chart in `OnAfterRenderAsync`

---

## Phase 3: Simple Pages

**Goal**: Migrate error pages, static pages, and read-only detail views.

**Depends on**: Phase 2 (Tier 1 components)

### Error Pages

| Current | New | Route |
|---------|-----|-------|
| `Pages/Error/403.cshtml` | `Components/Pages/Error/Error403.razor` | `/Error/403` |
| `Pages/Error/404.cshtml` | `Components/Pages/Error/Error404.razor` | `/Error/404` |
| `Pages/Error/500.cshtml` | `Components/Pages/Error/Error500.razor` | `/Error/500` |

All `[AllowAnonymous]`. Verify `UseStatusCodePagesWithReExecute("/Error/{0}")` routes to Blazor pages.

### Account Pages (partial)

**Stay as Razor Pages permanently** (require `HttpContext`):
- `Login`, `Logout`, `ExternalLogin`, `LinkDiscord`, `Lockout`

**Migrate to Blazor**:
- `AccessDenied` -> `Components/Pages/Account/AccessDenied.razor`
- `Privacy` -> `Components/Pages/Account/Privacy.razor`
- `Profile` -> `Components/Pages/Account/Profile.razor` (evaluate if form POSTs to Identity require keeping as Razor)

### Detail/View Pages (read-only)

- `Admin/AuditLogs/Details` -> `Components/Pages/Admin/AuditLogDetails.razor`
- `Admin/MessageLogs/Details` -> `Components/Pages/Admin/MessageLogDetails.razor`
- `Admin/Users/Details` -> `Components/Pages/Admin/Users/UserDetails.razor`
- `CommandLogs/Details` -> `Components/Pages/Commands/CommandLogDetails.razor`
- `Guilds/Details` -> `Components/Pages/Guilds/GuildDetails.razor`
- `Guilds/FlaggedEvents/Details` -> `Components/Pages/Guilds/FlaggedEventDetails.razor`

### Verification
- Error routes work via status code middleware
- Account flow (login -> dashboard -> logout) works
- Detail pages display correct data
- Authorization policies enforce correctly on Blazor pages

---

## Phase 4: Form & List Pages

**Goal**: Migrate all CRUD, settings, configuration, and filtered list pages.

**Depends on**: Phase 3

### Form Pattern

Replace Razor Pages `OnPostAsync` with Blazor `EditForm`:
```razor
<EditForm Model="model" OnValidSubmit="HandleSubmit" FormName="editGuild">
    <DataAnnotationsValidator />
    <FormInput @bind-Value="model.Name" Label="Name" />
    <Button Type="submit">Save</Button>
</EditForm>
```

### Batch 1 -- Simple CRUD Forms

- `Admin/Users/Create`, `Admin/Users/Edit`
- `Guilds/Edit`
- `Guilds/ScheduledMessages/Create`, `Guilds/ScheduledMessages/Edit`
- `Guilds/Welcome`

### Batch 2 -- Settings/Config Pages (tabbed forms)

- `Admin/Settings` (replaces 1,100-line `settings.js`)
- `Guilds/AudioSettings`
- `Guilds/AssistantSettings`
- `Guilds/ModerationSettings`

### Batch 3 -- List Pages with Filters

- `Guilds/Index`, `Admin/Users/Index`
- `Admin/Notifications`
- `Admin/Logs`, `Admin/AuditLogs/Index`, `Admin/MessageLogs/Index`
- `Guilds/ScheduledMessages/Index`, `Guilds/Reminders/Index`
- `Guilds/FlaggedEvents/Index`
- `Admin/BulkPurge`, `Admin/UserPurge`
- `Search`

### API Controller Strategy

Do NOT remove controllers during this phase. Blazor components call services directly (bypassing controllers). Controllers marked for removal in Phase 8.

### Verification
- All forms submit with validation
- Toast notifications appear on success/error
- List filtering, sorting, pagination work without full page reload
- No disposed-context errors under concurrent use

---

## Phase 5: Real-Time Pages

**Goal**: Migrate SignalR-dependent pages. Introduce in-process event bus for Blazor components.

**Depends on**: Phase 4, Phase 2 Tier 3 components

### 5.1 In-Process Event Bus

Blazor Server runs on the server -- it shouldn't connect to its own SignalR hub over WebSocket. Instead:

**Create**: `src/DiscordBot.Bot/Components/Services/IDashboardEventBus.cs` + implementation (Singleton)

Events mirror `DashboardHub` broadcasts: `OnBotStatusChanged`, `OnPerformanceUpdate`, `OnAudioStatusChanged`, `OnNotificationReceived`, etc.

**Modify** the 7 notifier services to dual-publish:
1. Continue broadcasting via `IHubContext<DashboardHub>` (for remaining JS clients)
2. Also raise events on `IDashboardEventBus` (for Blazor components)

Files to modify:
- `src/DiscordBot.Bot/Services/DashboardNotifier.cs`
- `src/DiscordBot.Bot/Services/AudioNotifier.cs`
- `src/DiscordBot.Bot/Services/PerformanceNotifier.cs`
- `src/DiscordBot.Bot/Services/DashboardUpdateService.cs`
- `src/DiscordBot.Bot/Services/PerformanceMetricsBroadcastService.cs`
- `src/DiscordBot.Bot/Services/BulkPurgeService.cs`
- `src/DiscordBot.Bot/Services/NotificationService.cs`

### 5.2 Pages

**Dashboard** (`@page "/"`):
- Subscribes to `IDashboardEventBus` for live updates
- Uses: BotStatusCard, ConnectionStatus, GuildStatsCard, CommandStatsCard, ActivityFeed, DashboardWidget
- Replaces: `dashboard-realtime.js`, `bot-status-refresh.js`, `command-stats-chart.js`

**Performance Dashboard** (6 pages under `/Admin/Performance/*`):
- `PerformanceDashboard.razor`, `HealthMetrics.razor`, `CommandPerformance.razor`, `SystemHealth.razor`, `ApiMetrics.razor`, `PerformanceAlerts.razor`
- Chart.js via `ChartJsInterop`, real-time via event bus
- Replaces: 12 JS files in `wwwroot/js/performance/`

**Notification Bell** (global, in `MainLayout.razor`):
- `NotificationBell.razor` subscribes to event bus
- Replaces: `notification-bell.js` (714 lines)

**Voice Channel Panel** (shared component):
- `VoiceChannelPanel.razor` subscribes to `OnAudioStatusChanged`
- Replaces: `voice-channel-panel.js` (656 lines)

### Verification
- Dashboard shows live bot status without page refresh
- Performance charts update in real-time
- Notification bell reflects new notifications instantly
- Voice channel panel shows playback state changes
- Un-migrated Razor Pages still get SignalR updates via JS

---

## Phase 6: Complex Feature Pages

**Goal**: Migrate feature-rich pages with complex state, filtering, charts, and multi-component composition.

**Depends on**: Phase 5

### Pages

**Commands** (`@page "/Commands"`) -- Very Complex:
- Decompose into: `CommandList.razor`, `CommandLogList.razor`, `CommandAnalytics.razor`
- Replaces: 8 JS files (`command-tabs.js`, `command-filters.js`, `command-analytics.js`, etc.)

**Member Directory** (`@page "/Guilds/{GuildId:long}/Members"`) -- Complex:
- Search, role/status filtering, pagination via direct service calls
- Replaces: `member-directory.js` (496 lines)

**Guild Analytics** (3 pages) -- Complex:
- `GuildAnalytics.razor`, `Engagement.razor`, `ModerationAnalytics.razor`
- Chart.js via interop

**Guild Audio Pages** -- Very Complex:
- `Soundboard.razor` -- sound CRUD, file upload, voice panel, playback controls
- `TextToSpeech.razor` -- voice selector, SSML editor, mode switcher, preview
- `VOX.razor` -- clip selection, concatenation controls
- Shared: VoiceChannelPanel, VoiceSelector, ModeSwitcher, StyleSelector components

**RatWatch** (3 guild pages + 1 admin page):
- Index, Incidents, Analytics (guild), RatWatchAnalytics (admin)

**Remaining**:
- `Guilds/AssistantMetrics` (charts)
- `Guilds/PublicLeaderboard` (`[AllowAnonymous]`)

### Verification
- Charts display and update correctly
- Filters and pagination work without full page reload
- Tab navigation is native Blazor (no JS)
- Voice channel integration works on audio pages
- File upload works on Soundboard

---

## Phase 7: Portal Redesign

**Goal**: Migrate the 3 public-facing Portal pages with a redesign opportunity.

**Depends on**: Phase 6 (voice components)

### Pages

| New | Route | Layout |
|-----|-------|--------|
| `Components/Pages/Portal/SoundboardPortal.razor` | `/Portal/Soundboard/{GuildId:long}` | `PortalLayout` |
| `Components/Pages/Portal/TtsPortal.razor` | `/Portal/TTS/{GuildId:long}` | `PortalLayout` |
| `Components/Pages/Portal/VoxPortal.razor` | `/Portal/VOX/{GuildId:long}` | `PortalLayout` |

Auth: `[Authorize(Policy = "PortalGuildMember")]` with `<AuthorizeView>` for authenticated/anonymous split (currently `PortalPageModelBase` shows landing page for anonymous users).

### Redesign Opportunities
- Real-time playback status via `IDashboardEventBus` (currently polled)
- Improved mobile responsiveness
- Better voice channel selection UX
- Sound favorites, recently played, search improvements
- Smoother audio playback controls

### Verification
- Portal accessible with Discord OAuth (not admin login)
- Audio playback works end-to-end
- Anonymous landing page renders for unauthenticated users
- Mobile layout functional
- `PortalGuildMember` policy enforces guild membership

---

## Phase 8: Cleanup

**Goal**: Remove all legacy Razor Pages infrastructure, dead JS, and redundant controllers.

**Depends on**: All previous phases verified

### 8.1 Delete Razor Pages

Delete entire `Pages/` directory EXCEPT pages that must stay permanently:
- `Pages/Account/Login.cshtml` + `.cshtml.cs`
- `Pages/Account/Logout.cshtml` + `.cshtml.cs`
- `Pages/Account/ExternalLogin.cshtml` + `.cshtml.cs`
- `Pages/Account/LinkDiscord.cshtml` + `.cshtml.cs`
- `Pages/Account/Lockout.cshtml` + `.cshtml.cs`
- `Pages/_ViewImports.cshtml` (for the remaining pages)

Delete: `ViewModels/Components/` directory (replaced by `[Parameter]` properties)

### 8.2 Delete JavaScript

Remove from `wwwroot/js/` all files except:
- `toast.js` (used via `ToastInterop`)
- `theme.js` (used via `ThemeInterop`)
- `timezone.js` (used via `TimezoneInterop`)
- `blazor-chart-interop.js` (new, created in Phase 2)
- `login.js` (if still needed by remaining Login Razor Page)

That's ~58 JS files removed (~24,000+ lines).

### 8.3 Delete Redundant API Controllers

Remove controllers that only served admin page AJAX (25 controllers). Keep:
- `PortalTtsController`, `PortalSoundboardController`, `PortalVoxController` (evaluate: may also be removable if Portal Blazor pages call services directly)
- `ThemeController`

### 8.4 Remove SignalR JS Path

- Remove `signalr.min.js` CDN reference from remaining layouts
- Remove `DashboardHub.connect()` auto-connection JS
- `DashboardHub.cs` stays (server-side notifiers still publish via `IHubContext`; useful for external clients)

### 8.5 Remove Tag Helpers

Delete:
- `src/DiscordBot.Bot/TagHelpers/AuthorizeTagHelper.cs` (replaced by `<AuthorizeView>`)
- `src/DiscordBot.Bot/TagHelpers/FilterPanelTagHelper.cs`
- `src/DiscordBot.Bot/TagHelpers/HighlightTagHelper.cs`

### 8.6 Update DI/Middleware

- Keep `AddRazorPages()` (needed for 5 remaining auth pages)
- Keep `AddControllers()` (if any API controllers remain)
- `MapRazorComponents<App>()` is the primary routing

### Verification
- Full regression test of all routes
- `dotnet build` clean, `dotnet test` passes
- Docker build and deploy works
- No 404s, no console JS errors
- Performance baseline comparison

---

## Cross-Cutting Concerns

### Authentication Flow (Final State)

| Flow | Technology | Permanent? |
|------|-----------|------------|
| Login form + Discord OAuth | Razor Pages | Yes -- requires HTTP redirects |
| Logout | Razor Pages | Yes -- requires cookie clear |
| OAuth callback (`/signin-discord`) | Middleware | Yes -- not a page |
| Admin authorization | Blazor `[Authorize]` + `<AuthorizeView>` | Yes |
| Guild access | Resource-based authorization via refactored handlers | Yes |
| Session validation | `RevalidatingAuthenticationStateProvider` | Yes |

### Claims Staleness

`DiscordClaimsTransformation` runs once per circuit. If a user links/unlinks Discord mid-session, claims are stale until page refresh. Document this as a known limitation or implement a circuit-refresh mechanism.

### SQLite vs PostgreSQL

SQLite's single-writer limitation is more exposed under Blazor Server (concurrent circuits = concurrent writes). Document PostgreSQL as recommended for multi-user deployments.

### Global JS Transition Timeline

| JS File | Replaced In | Replacement |
|---------|-------------|-------------|
| `dashboard-hub.js` | Phase 5 | `IDashboardEventBus` |
| `notification-bell.js` | Phase 5 | `NotificationBell.razor` |
| `navigation.js` | Phase 1 | `MainLayout.razor` |
| `toast.js` | Phase 2 | `ToastInterop.cs` (wraps JS) |
| `quick-actions.js` | Phase 4 | `ConfirmationModal.razor` |
| `loading-manager.js` | Phase 2 | `LoadingSpinner.razor` |
| `timezone.js` | Phase 2 | `TimezoneInterop.cs` (wraps JS) |
| `bot-status-refresh.js` | Phase 5 | `BotStatusCard.razor` + event bus |
| `search.js` | Phase 4 | Blazor search component |
| `preview-popup.js` | Phase 6 | `PreviewPopup.razor` |
| `theme.js` | Phase 2 | `ThemeInterop.cs` (wraps JS) |

---

## Risk Register

| Risk | Severity | Mitigation |
|------|----------|------------|
| Elastic APM .NET 9 compatibility | High | Test early in Phase 0; fallback to disabling APM |
| EF Core concurrency in Blazor circuits | High | `IDbContextFactory` in Phase 1; scoped fallback for backward compat |
| Guild auth handlers depend on HttpContext | High | Refactor to resource-based auth in Phase 1 |
| Cookie expiry in active circuits | Medium | `RevalidatingAuthenticationStateProvider` in Phase 1 |
| SignalR dual-path during transition | Medium | Additive (event bus doesn't break JS path) |
| Chart.js interop timing | Medium | `OnAfterRenderAsync` + debounce |
| Phase 8 Razor Pages removal breaks auth | Medium | Keep 5 auth pages; test exhaustively |
| Portal Soundboard inline JS (1,500+ lines) | Medium | Biggest single-page migration; plan extra time |
| Claims staleness after Discord link changes | Low | Document; consider circuit-refresh |
| Route conflicts (Razor + Blazor same route) | Low | Delete Razor Page before creating Blazor equivalent |

---

## Estimated Effort

| Phase | Effort | Notes |
|-------|--------|-------|
| 0: .NET 9 Upgrade | 1-2 days | Package compat is main risk |
| 1: Blazor Foundation | 3-4 days | Auth refactors add time |
| 2: Component Library | 5-8 days | Tier 1 highly parallelizable |
| 3: Simple Pages | 3-4 days | Low risk, all parallelizable |
| 4: Form & List Pages | 8-12 days | Batches parallelizable |
| 5: Real-Time Pages | 8-10 days | Event bus is critical path |
| 6: Complex Feature Pages | 12-18 days | Audio pages are hardest |
| 7: Portal Redesign | 8-12 days | Includes design work |
| 8: Cleanup | 3-5 days | High regression risk |
| **Total** | **51-75 days** | **~13-15 weeks (1 dev)** |

With 2 developers working in parallel: ~10-12 weeks.

---

## Documentation Updates

Update these docs after migration:
- `CLAUDE.md` -- remove HTMX/Alpine.js references, add Blazor patterns
- `docs/articles/component-api.md` -- rewrite for Blazor component library
- `docs/articles/design-system.md` -- update component usage examples
- `docs/articles/form-implementation-standards.md` -- rewrite for `EditForm`
- `docs/architecture/` -- update UI layer documentation
- `.claude/agents/web-ui-portal.md` -- update for Blazor
