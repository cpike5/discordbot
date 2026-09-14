# UI Inventory

**Version:** 1.0
**Last Updated:** 2026-02-03
**Target Framework:** .NET 10 Razor Pages with Tailwind CSS

---

## Overview

This document provides a comprehensive inventory of all UI components, pages, and layouts in the Discord bot project. Use this as a quick reference to understand what UI building blocks exist without diving into the codebase.

For detailed component documentation, see [Component API Usage Guide](../articles/component-api.md).

---

## Blazor Routes (Phase 1, temporary — retained through Phase 3)

The Blazor port (`docs/plans/blazor-port-plan.md`) is under way alongside the Razor Pages
below; see "Blazor components" in `patterns.md`. These routes exist only to prove the
Phase 1 hosting foundation and are **not part of the permanent route surface** — but per
`blazor-port-plan.md` §5 Phase 2's "Delivered" note, they are **retained past Phase 2**
(superseding this table's earlier "deleted at the end of Phase 2" wording) since nothing in
Phase 2 needed to touch them; they are deleted once Phase 4 replaces them with real nested
pages that exercise the same hosting-foundation guarantees as a side effect of being real
product pages.

| Route | File | Purpose |
|-------|------|---------|
| `/blazor-smoke` | `Blazor/Pages/BlazorSmoke.razor` | Minimal smoke test: `RequireAdmin` auth on a routable component plus one interactive counter button. |
| `/admin/blazor-smoke` | `Blazor/Pages/BlazorSmoke.razor` | Second `@page` route on the same component as `/blazor-smoke`, guarding a known .NET 10 regression where `blazor.web.js` resolved `_blazor/initializers` relative to a nested path instead of the app base, 404ing and leaving the circuit dead — see `tests/DiscordBot.E2E`. Both the flat and nested Playwright checks run against this one component. |
| `/admin/blazor-probe` | `Blazor/Pages/Admin/BlazorProbe.razor` | Foundation probe: interactivity, cascading auth state, `IToastService`, `ILoadingState`, the `IDashboardEventBus` real-time subscription (debounced), `ChartInterop`, `BrowserInterop`/`CircuitClientInfoService`, and (Phase 3) `IThemeInterop` — a theme select bound to `IThemeService.GetActiveThemesAsync()` that applies the pick client-side via the interop, then persists it via `IThemeService.SetUserThemeAsync` (no navbar theme toggle exists yet, so this is the interop's only caller — see `docs/articles/blazor-interop.md`). Deliberately a nested route (`/admin/...`) rather than a top-level one, for the same `blazor.web.js` regression `/admin/blazor-smoke` guards. Its "publish test event" button is admin-only and stays retained with the rest of this page — see the note above. As of Phase 3, renders under `MainLayout` (see "Blazor Layouts" below), same as `/blazor-smoke`/`/admin/blazor-smoke`. |

## Blazor Routes (Phase 3, temporary — retained through Phase 4)

Same "prove the foundation, not part of the permanent route surface" role as the Phase 1 table
above, this time for `GuildLayout`/`PortalLayout` (plan §5 Phase 3). Each is deleted once its
own Phase 4 cluster (4b for guild pages, 4f for Portal) lands a real page that exercises the
same layout/context-resolution guarantees as a side effect of being a real product page.
`GuildProbe.razor` was retired in cluster 4b — `Guilds/Edit` (below) now proves `GuildLayout` +
`GuildPageBase` + `GuildContextGate` on a real, permanent route instead.

| Route | File | Purpose |
|-------|------|---------|
| `/Portal/{guildId:long}/blazor-probe` | `Blazor/Pages/Portal/PortalProbe.razor` | `[AllowAnonymous]` (Portal pages branch on `PortalAccessOutcome` themselves rather than gating the route), `@layout PortalLayout`, `@inherits PortalPageBase`. Renders the resolved `PortalAccessResult` (outcome, login URL, and — when present — the guild name/id/bot-online flag), proving `PortalLayout` + `PortalPageBase` + `IPortalContextProvider` render correctly together for a real, anonymous-reachable Portal route. |

## Blazor Routes (permanent)

Unlike the Phase 1 probe/smoke routes above, these routes are a permanent part of the app — each
replaces a Razor Page rather than proving the hosting foundation.

| Route | File | Purpose |
|-------|------|---------|
| `/components` | `Blazor/Pages/Components/ComponentsPage.razor` | Component showcase / design-system reference, `RequireAdmin`-gated. Replaces the former Razor Page `Pages/Components.cshtml` (route `/Components` — ASP.NET Core endpoint routing matches both case-insensitively, so the sidebar's existing link keeps resolving). Composes the six tier showcase sections (`Blazor/Pages/Components/Sections/*Showcase.razor`) behind an anchor nav. As of Phase 3, renders under `MainLayout` (see "Blazor Layouts" below), which now supplies the shared `ToastHost`/`LoadingOverlay` islands this page used to host itself. See "Blazor components" table below for every component it showcases. |
| `/landing` | `Blazor/Pages/Landing.razor` | Public marketing/landing page, `[AllowAnonymous]`, static SSR. Replaces `Pages/Landing.cshtml` + `LandingModel`. Renders under `LandingLayout` (see "Layouts" below); the two inline `@section Scripts` blocks the cshtml carried (hero parallax, scroll-spy nav highlight) move to the classic script `wwwroot/js/blazor/landing.js`. Also the target of Program.cs's unauthenticated `/` and `/Index` → `/landing` redirect middleware (unchanged). |
| `/Error/403` | `Blazor/Pages/Error/Forbidden.razor` | Access forbidden, `[AllowAnonymous]`, static SSR. Replaces `Pages/Error/403.cshtml` + `ForbiddenModel`; the authenticated/anonymous button branch now reads the cascading `Task<AuthenticationState>` instead of `User.Identity.IsAuthenticated`. |
| `/Error/404` | `Blazor/Pages/Error/NotFound.razor` | Page not found, `[AllowAnonymous]`, static SSR. Replaces `Pages/Error/404.cshtml` + `NotFoundModel`, fixing a fidelity bug: the requested-URL line now reads `IStatusCodeReExecuteFeature.OriginalPath`/`OriginalQueryString` when `UseStatusCodePagesWithReExecute` set them (falling back to `HttpContext.Request.Path` otherwise), rather than always showing `/Error/404` itself. Also `Routes.razor`'s `Router.NotFoundPage` target — the .NET 10 mechanism that renders this component (and returns a real HTTP 404) when no `@page` route matches, for a signed-in visitor. An anonymous visitor hitting an unmatched route never reaches it: `MapRazorComponents`'s generic "no match" fallback endpoint carries no derivable `[AllowAnonymous]` metadata, so `IdentityServiceExtensions`'s pre-existing global `FallbackPolicy` (`RequireAuthenticatedUser`) redirects to `/Account/Login` first — a known gap orthogonal to this page, left for a future PR/decision. |
| `/Error/500` | `Blazor/Pages/Error/ServerError.razor` | Server error, `[AllowAnonymous]`, static SSR. Replaces `Pages/Error/500.cshtml` + `ServerErrorModel`; `RequestId` is `[SupplyParameterFromQuery]` falling back to `HttpContext.TraceIdentifier`, and exception message/stack trace still gate on `IWebHostEnvironment.IsDevelopment()` via `IExceptionHandlerPathFeature`. Program.cs's `UseExceptionHandler("/Error/500")` reaches it unchanged. |


## Blazor Routes (Phase 3, permanent)

| Route | File | Purpose |
|-------|------|---------|
| `/Search` | `Blazor/Pages/Search.razor` (+ `Search.razor.cs`) | Unified search across guilds, command logs, users, commands, audit logs, message logs, pages, reminders and scheduled messages, `RequireViewer`-gated (admin-only categories additionally require `RequireAdmin`, checked via `IAuthorizationService` from the component). First interactive page ported off `Pages/Search.cshtml`/`SearchModel` (plan §5 Phase 3) — replaces it and the deleted `TagHelpers/HighlightTagHelper.cs` (now `Blazor/Shared/Primitives/Highlight.razor`) in the same change. Reads the term from `?q=` via `[SupplyParameterFromQuery]`, resolves once per term and persists the mapped `SearchResultsViewModel` across the prerender-to-circuit boundary with `PersistentComponentState` (same pattern as `GuildPageBase`, keyed by term instead of a guild id). The `DiscordSocketClient` guild-intersection `SearchModel` did directly is behind `IUserGuildSelectorService` (`Blazor/Services/`) instead, so it can be mocked in bUnit tests. Renders under `MainLayout`. Its Command Log/Audit Log rows now use `<LocalTime>` (Phase 4 cluster 4a, see below) instead of a raw `data-utc` span — `OnAfterRenderAsync` calls `BrowserInterop.ConvertLocalTimesAsync()` whenever results changed, since the browser's document-level scan never fires for an in-circuit re-render. |

## Blazor Routes (Phase 4, permanent)

| Route | File | Purpose |
| --- | --- | --- |
| `/Account/Profile` | `Blazor/Pages/Account/Profile.razor` (+ `Profile.razor.cs`) | Static SSR, `[Authorize]`, default `MainLayout`. Replaces `Pages/Account/Profile.cshtml` + `ProfileModel` (cluster 4a) — identity card, theme `<EditForm>` (the library's `Select<int?>` bound with an explicit `Name="Input.SelectedThemeId"` since it isn't `InputBase`-derived and so doesn't infer a posted field name), and the "Member Since"/"Last Login" fields now use `<LocalTime>`. The theme save posts, appends the `theme-preference` cookie via the cascaded `HttpContext`, then redirects to `?status=saved\|error` (`[SupplyParameterFromQuery]`) — no `TempData` in a static SSR page. |
| `/Account/AccessDenied` | `Blazor/Pages/Account/AccessDenied.razor` | Static SSR, `[AllowAnonymous]`, `@layout EmptyLayout`. Replaces `Pages/Account/AccessDenied.cshtml` + `AccessDeniedModel` — `[SupplyParameterFromQuery] ReturnUrl` shows the "Attempted URL" line; "Sign Out" is a plain `<form method="post" action="/Account/Logout">` with an explicit `<AntiforgeryToken />` (not an `EditForm`, so the token isn't automatic — contrast `Profile`). `IdentityConfigOptions.AccessDeniedPath` still points at this literal route. |
| `/Account/Lockout` | `Blazor/Pages/Account/Lockout.razor` | Static SSR, `[AllowAnonymous]`, `@layout EmptyLayout`. Replaces `Pages/Account/Lockout.cshtml` + `LockoutModel` — static copy, no code-behind logic to port. |
| `/Admin/AuditLogs/Details/{id:long}` | `Blazor/Pages/Admin/AuditLogs/Details.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, default `MainLayout`. Replaces `Pages/Admin/AuditLogs/Details.cshtml` + `DetailsModel` (cluster 4a) — actor/target/request-metadata cards, the details/raw-data panel (expand/collapse is now component state, no JS), related entries by correlation id, `<LocalTime>` throughout. Copy Entry ID and the JSON export (built server-side with `System.Text.Json`, handed to the browser via `BrowserInterop.DownloadFileAsync`) both replace inline JS. Not found renders an `EmptyState` at HTTP 200 (see the class remarks for why, over `NavigationManager.NotFound()`). |
| `/Admin/MessageLogs/Details/{id:long}` | `Blazor/Pages/Admin/MessageLogs/Details.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, default `MainLayout`. Replaces `Pages/Admin/MessageLogs/Details.cshtml` + `DetailsModel` (cluster 4a) — DM/Server badge, message info/author/location/content/metadata sections, `<LocalTime>` for the two timestamps. The legacy Author/Guild `preview-trigger` popups render as plain text (no Blazor-callable preview loader exists yet — see the class remarks). |
| `/CommandLogs/Details/{id:guid}` | `Blazor/Pages/CommandLogs/Details.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireModerator")]` (kept broader than the other two cluster 4a Details pages), default `MainLayout`. Replaces `Pages/CommandLogs/Details.cshtml` + `DetailsModel` (cluster 4a) — success/failed `Badge`, error `Alert`, basic/server/user info cards, command parameters `<pre>`, copy buttons via `BrowserInterop.CopyToClipboardAsync` + `IToastService.Success` toasts (replacing `ToastManager`). The guild link is fixed to `/Guilds/Details/{guildId}` (the legacy `asp-route-id` targeted a route template of `{guildId:long}`, a dead link — same fix `Search.razor.cs` applies). The sibling partial `Pages/CommandLogs/_CommandLogDetailsContent.cshtml` (used by `CommandLogs/Index`, cluster 4e) is untouched. |
| `/Guilds/Edit/{guildId:long}` | `Blazor/Pages/Guilds/Edit.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, `@layout GuildLayout`, `@inherits GuildPageBase` — the first real `GuildLayout`/`GuildPageBase`/`GuildContextGate` consumer (cluster 4b), replacing the retired `GuildProbe.razor` in that role. Replaces `Pages/Guilds/Edit.cshtml` + `EditModel`. Bot-active toggle plus a best-effort audio settings section (`IGuildAudioSettingsService` — hidden, not failed, if it throws), saved via `IGuildService.UpdateGuildAsync`/`IGuildAudioSettingsService.UpdateSettingsAsync`. Success toasts and navigates to `/Guilds/Details/{id}`; a failure at either save step sets an inline `Alert` and stays. |
| `/Guilds/Welcome/{guildId:long}` | `Blazor/Pages/Guilds/Welcome.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, `@layout GuildLayout`. Replaces `Pages/Guilds/Welcome.cshtml` + `WelcomeModel` — welcome-message config against `IWelcomeService`, with `IDiscordChannelResolver.GetTextChannels` for the channel dropdown (empty in web-only mode). The "channel required when enabled" rule is a manual check in the submit handler (not a data annotation, since it depends on another field) rather than `ModelState.AddModelError`. The live Discord-message preview is computed server-side in C# (HTML-encoding the message and guild name before token substitution, so neither can inject markup into the preview) instead of the legacy `updatePreview()` JS. Token-toolbar/token-table clicks insert at the raw `<textarea>`'s caret via `BrowserInterop.InsertAtSelectionAsync` (the shared `TextArea` component exposes no `ElementReference` to target). Success toasts and stays on the page. |
| `/Guilds/AssistantSettings/{guildId:long}` | `Blazor/Pages/Guilds/AssistantSettings.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, `@layout GuildLayout`. Replaces `Pages/Guilds/AssistantSettings.cshtml` + `AssistantSettingsModel` — enable toggle, channel allow-list, the Tool Access checklist (grouped by `ToolCatalog` category, with the "this server uses the default set" banner when nothing is saved), and the rate-limit override, saved via `IAssistantGuildSettingsService` with `ToolCatalog.NormalizeSelection` on save. A "View Metrics" action links to `AssistantMetrics` below (rendered as a page-level button, since `GuildLayout` owns the header and offers no per-page header-action slot). |
| `/Guilds/AssistantMetrics/{guildId:long}` | `Blazor/Pages/Guilds/AssistantMetrics.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, `@layout GuildLayout`, read-only. Replaces `Pages/Guilds/AssistantMetrics.cshtml` + `AssistantMetricsModel` — the same 30-day summary cards, daily breakdown table, **Tool Usage** table (`IAssistantInteractionLogRepository.GetToolUsageAsync` paired with `ToolCatalog` so an uncalled tool still gets a row), **Prompt Surface** panel (`IPromptSurfaceReporter`, null when the assistant isn't configured — the panel is omitted rather than the page failing), and **Cost by User** table (`ILlmUsageRepository.GetByUserAsync`, top 20) as the legacy page. This route matches none of `GuildNavigationConfig`'s tabs (the "assistant" tab points at `AssistantSettings`'s URL instead), so `GuildLayout` falls back to the guild's own name for the header/breadcrumb rather than the legacy page's custom "Home > Servers > Guild > Assistant > Metrics" trail — an accepted cluster 4b fidelity deviation. |
| `/Guilds/AudioModerationLog/{guildId:long}` | `Blazor/Pages/Guilds/AudioModerationLog/Index.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, `@layout GuildLayout`. Replaces `Pages/Guilds/AudioModerationLog/Index.cshtml` + `IndexModel` — a paginated, filterable (feature type/user id/date range) audio playback log, sorted `PlayedAt` descending, page size 25. Filters and paging live in component state seeded once from `[SupplyParameterFromQuery]` (a bookmarked URL still loads correctly) and reloaded imperatively by the filter/page-change handlers rather than through `OnParametersSetAsync`, which `GuildPageBase` seals to only react to a `GuildId` change. The feature-type query parameter is bound as `int?`, not the `AudioFeatureType?` enum directly — `QueryParameterValueSupplier` only recognizes a fixed set of primitive types and throws for an arbitrary enum — converted to the typed value on every seed. Renders the same audio-cluster sub-nav (Soundboard/TTS/VOX/Settings/Audio Log) as a page-navigation `TabGroup`; this route also matches none of `GuildNavigationConfig`'s top-level tabs (same deviation as `AssistantMetrics` above). |
| `/Guilds/RatWatch/{guildId:long}` | `Blazor/Pages/Guilds/RatWatch/Index.razor` (+ `.razor.cs`) | `@rendermode InteractiveServer`, `[Authorize(Policy = "RequireAdmin")]`, `@layout GuildLayout`. Replaces `Pages/Guilds/RatWatch/Index.cshtml` + `IndexModel` — settings card (display/edit toggle), Settings/Analytics/Incidents sub-nav (`TabGroup`, the latter two still Razor Pages), summary/analytics cards, watch stats + hall-of-shame leaderboard, and the recent-watches table. Cancel/end-vote are `ConfirmModal`-gated component methods calling `IRatWatchService.CancelWatchAsync`/`FinalizeVotingAsync` (replacing the legacy page's `OnPostCancelAsync`/`OnPostEndVoteAsync` handlers); the settings save reproduces the same timezone-non-blank/1-168-hour/1-60-minute validation `OnPostUpdateSettingsAsync` had. Reuses the legacy `RatWatchItemViewModel`/`RatLeaderboardEntryViewModel` (`ViewModels/Pages/RatWatchIndexViewModel.cs` — still alive, `Pages/Guilds/RatWatch/Incidents.cshtml.cs` also depends on them) for status/badge derivation rather than reimplementing it. |

Cluster 4a also adds `Blazor/Shared/Primitives/LocalTime.razor` (see "Blazor Components" below) and
`wwwroot/js/blazor/localtime.js` (a classic script, loaded from `App.razor` after `shell.js`,
porting `wwwroot/js/timezone.js`'s `convertDisplayTimes`/`initTimezoneFields`) — the timezone
conversion `Search`'s own `data-utc` rows were missing since Phase 3 (§5 Phase 3 deviation (c)).

The same cluster (4a "Simple admin") also ports the `Pages/Admin/Users/*.cshtml` Razor Pages below,
deleted in the same change along with their three now-orphaned
`ViewModels/Pages/User{List,Form,Detail}ViewModel.cs`. All four are `RequireAdmin`-gated (the four
`PageMetadataService` entries for these routes previously read `RequireSuperAdmin`, a pre-existing
mismatch with the actual `[Authorize]` policy on the Razor Page this port fixed in the same change)
and render under `MainLayout`. Query parameter names are unchanged from the legacy
`[BindProperty(SupportsGet = true)]`/handler-parameter names so existing plain-`href` builders
elsewhere (`_Sidebar.cshtml`, `Search.razor.cs`, `AuditLogListViewModel`) keep resolving without
edits — `UsersSearchProvider`'s own `ViewAllUrl` builder did *not* match (it built `?search=`
against a page that binds `SearchTerm`, a pre-existing bug predating this port that a later review
of this cluster caught and fixed). Timestamps (last login, member since, activity log entries)
render via `<LocalTime>`, same as the rest of this cluster.

| Route | File | Purpose |
| --- | --- | --- |
| `/Admin/Users` | `Blazor/Pages/Admin/Users/Index.razor` (+ `.razor.cs`) | Paginated, filterable (`SearchTerm`/`RoleFilter`/`ActiveFilter`/`DiscordLinkedFilter`/`pageNumber`, all `[SupplyParameterFromQuery]`) user list. Filters submit via `NavigationManager.NavigateTo` (no full page post); results persist across the prerender-to-circuit boundary with `PersistentComponentState`, keyed by the resolved query. Active/inactive toggle is a real `ConfirmModal`-gated action — the legacy `IndexModel.OnPostToggleActiveAsync` handler had no corresponding control in `Index.cshtml`, a dead-handler gap this port closes rather than reproduces. |
| `/Admin/Users/Create` | `Blazor/Pages/Admin/Users/Create.razor` (+ `.razor.cs`) | `EditForm` + `DataAnnotationsValidator` over a nested `Create.InputModel` (same data annotations as the legacy nested `CreateModel.InputModel`). On success, `IToastService.Success` then `NavigationManager.NavigateTo("/Admin/Users")` — the toast survives the same-circuit navigation. Service failure renders an inline `Alert`. |
| `/Admin/Users/Edit` | `Blazor/Pages/Admin/Users/Edit.razor` (+ `.razor.cs`), `?id=` | Same form shape as Create, `IsSelf`-gated (own role/active-status fields disabled, matching `EditModel`) and additionally gated on `IUserManagementService.CanManageUserAsync` (`CanAccessEdit`): a target the actor cannot manage (e.g. an Admin against a SuperAdmin) renders an access-denied `EmptyState` instead of the form — a review-flagged gap the legacy `EditModel` left open (it rendered unconditionally; only `Details.razor`'s "Edit User" link was hidden). Save reloads the model and stays on the page (toast, no navigation) — the faithful port of the legacy `RedirectToPage("Edit", new { id })` self-redirect. Reset-password and unlink-Discord are `ConfirmModal`-gated component methods calling `IUserManagementService` directly, each additionally hidden/refused unless `CanManageUserAsync` allows it (restoring the deleted `UserDetailViewModel.CanResetPassword`/`CanUnlinkDiscord` formulas); a reset's generated temporary password is shown once in a dismissible success banner with a `BrowserInterop.CopyToClipboardAsync` copy button — hand-rolled rather than through `<Alert>`, since its copy-button row is a block element that can't safely nest inside `<Alert>`'s `<p>`-wrapped `ChildContent` once server-prerendered markup round-trips through the browser's HTML parser. An unresolvable `?id=` renders the design-system "not found" `EmptyState` at HTTP 200 (a Blazor circuit can't set a status code once it's already serving the page) rather than a real 404 — a recorded fidelity deviation. |
| `/Admin/Users/Details` | `Blazor/Pages/Admin/Users/Details.razor` (+ `.razor.cs`), `?id=` | Read-only profile card (avatar/initials, role `Badge`, `StatusIndicator`, Discord link card or "not linked" state) plus a 20-row activity log (`Badge` per `UserActivityAction`, actor, details, timestamp) or an empty state, permission flags (`CanEdit` etc.) from `IUserManagementService.CanManageUserAsync`. Same unresolvable-`?id=` 200-with-`EmptyState` deviation as Edit. |

Cluster 4b ("Simple guild") ports the first pages under `GuildLayout` proper (the Phase 3 probe,
`Blazor/Pages/Guilds/GuildProbe.razor`, is retired by a sibling cluster in the same round). All six
below are `@inherits GuildPageBase`, render inside `GuildContextGate`, and use the new
`Blazor/Common/PagedQuery.cs` (`PageNumber`/`PageSize`/`SortBy`/`SortDescending`, clamped; a
`ToQueryString` helper) plus `Blazor/Shared/Navigation/Pagination.razor` in link mode
(`PageParameterName="pageNumber"`) for their list pages — each also accepts the legacy `?page=`
query name as a fallback so old bookmarks and the `Guilds/Details` widget links keep resolving.
`GuildNavigationConfig`'s `feature-requests` tab `UrlPattern` was `/Guilds/{guildId}/FeatureRequests`,
which never matched the real Razor Page route either — fixed to `/Guilds/FeatureRequests/{guildId}`
in the same change.

| Route | File | Purpose |
| --- | --- | --- |
| `/Guilds/FeatureRequests/{guildId:long}` | `Blazor/Pages/Guilds/FeatureRequests/Index.razor` (+ `.razor.cs`) | `RequireAdmin`. Replaces `Pages/Guilds/FeatureRequests/Index.cshtml` + `IndexModel`. Status filter (`Select`), desktop table + mobile cards, `Blazor/Common/FeatureRequestStatusDisplay.cs` for the status badge (was duplicated 3× across the legacy Index/Details pages), `<LocalTime>` for the submitted date. |
| `/Guilds/FeatureRequests/{guildId:long}/{id:guid}` | `Blazor/Pages/Guilds/FeatureRequests/Details.razor` (+ `.razor.cs`) | `RequireAdmin`. Replaces `Pages/Guilds/FeatureRequests/Details.cshtml` + `DetailsModel`. Approve/Reject are component methods calling `IFeatureRequestService.UpdateStatusAsync` with the reviewer id from the `discord:user_id` claim; the review-notes box and "Approve"/"Approve Anyway" wording are gated on status (Submitted/GeneratingDocs/DocsGenerated vs. DocGenFailed). `GatheredRequirements` deserializes to `Core.Models.FeatureRequests.GatheredRequirements` with a `<pre>` fallback. Not-found/guild-mismatch renders an `EmptyState` at HTTP 200. |
| `/Guilds/Reminders/{guildId:long}` | `Blazor/Pages/Guilds/Reminders/Index.razor` (+ `.razor.cs`) | Only the `GuildAccess` policy applies (no app role required — `[Authorize]` with no policy, enforced by `GuildContextProvider`/`GuildPageBase`, matching the legacy page's bare `[Authorize(Policy = "GuildAccess")]`). Replaces `Pages/Guilds/Reminders/Index.cshtml` + `IndexModel`. Four stat cards, status filter, `ConfirmModal`-gated cancel (Pending only). Per-row Discord user resolution goes through the new `IReminderUserResolver`/`DiscordReminderUserResolver` seam (`Bot/Services/Reminders/`) instead of a raw `DiscordSocketClient` lookup inline, so it's mockable in bUnit; falls back to `"Unknown ({id})"` exactly as the legacy inline lookup did. |
| `/Guilds/ScheduledMessages/{guildId:long}` | `Blazor/Pages/Guilds/ScheduledMessages/Index.razor` (+ `.razor.cs`) | `RequireAdmin`. Replaces `Pages/Guilds/ScheduledMessages/Index.cshtml` + `IndexModel`. Status badge/dot and schedule description come from `Blazor/Common/ScheduledMessageStatusDisplay.cs` (was duplicated between the list view model and the Edit page's own status helpers); toggle (pause/resume) and delete (`ConfirmModal`) are component methods. |
| `/Guilds/ScheduledMessages/Create/{guildId:long}` | `Blazor/Pages/Guilds/ScheduledMessages/Create.razor` (+ `.razor.cs`) | `RequireAdmin`. Replaces `Pages/Guilds/ScheduledMessages/Create.cshtml` + `CreateModel`. Shares `ScheduledMessageInputModel` and the `ScheduledMessageForm.razor` field markup with Edit. Defaults `NextExecutionAt` to local-now + 5 minutes (rounded up to the next 5-minute mark) once `BrowserInterop.GetTimeZoneAsync()` resolves the viewer's IANA zone; submits convert that local value to UTC via `TimezoneHelper.ConvertToUtc`. Cron is required (and validated via `IScheduledMessageService.ValidateCronExpressionAsync`) only when Frequency is Custom. |
| `/Guilds/ScheduledMessages/Edit/{guildId:long}/{id:guid}` | `Blazor/Pages/Guilds/ScheduledMessages/Edit.razor` (+ `.razor.cs`) | `RequireAdmin`. Replaces `Pages/Guilds/ScheduledMessages/Edit.cshtml` + `EditModel`. Same shared form as Create, prefilled from the stored UTC `NextExecutionAt` converted to the detected local zone (`TimezoneHelper.ConvertFromUtc`) rather than a client-side JS conversion; also adds status badge/Delete (`ConfirmModal`). The channel `Select`'s options always include the currently-selected channel even when `IDiscordChannelResolver.GetTextChannels` can't resolve it (no live gateway, or a deleted channel) — without that, the rendered `<select required>` matches no `<option>` and the browser's own HTML5 validation silently blocks the submit event from ever reaching Blazor. |

`ScheduledMessageForm.razor` (shared, not itself routable) holds the Create/Edit field markup: title/content/channel, the schedule-type radio cards, `<InputDate Type="InputDateType.DateTimeLocal">` for the next-run time with a `.timezone-indicator`-style caption, the cron field (Custom only), and a live Discord-styled message preview (HTML-encoded, newline → `<br>`, reactive on every keystroke — no JS needed since `TextArea`'s `@bind-Value` re-renders the component).

## Blazor Layouts

`Blazor/Layout/` (plan §4.7/§5 Phase 3). `Routes.razor`'s `DefaultLayout` is `MainLayout`, so a
routed Blazor page gets the admin shell unless it opts out: the error pages declare
`@layout EmptyLayout`, `/landing` declares `@layout LandingLayout`, guild pages declare
`@layout GuildLayout` (itself nested under `MainLayout`), and Portal pages declare
`@layout PortalLayout`, which is not nested under `MainLayout` (see that layout's own row below).

| Layout | Render mode | Replaces | Contents |
| --- | --- | --- | --- |
| `EmptyLayout` | static SSR | `Layout = null` pages | No chrome — the error pages (and later `PublicLeaderboard`) opt into it with `@layout EmptyLayout`. |
| `MainLayout` | static SSR + islands | `Pages/Shared/_Layout.cshtml` + `_Navbar.cshtml` + `_Sidebar.cshtml` + `_MobileSearchOverlay.cshtml` + root `_ToastContainer.cshtml` | `MainSidebar` (role-gated via `AuthorizeView Policy`, active-link state from `ShellNavigation`), `MainNavbar` (search form, user menu, `NotificationBell` island), `MobileSearchOverlay` (plain `GET /Search` form — no live recent/results panes yet, a recorded Phase 3 deviation from the legacy JS-driven overlay), an `ErrorBoundary` around `@Body`, and `ToastHost`/`LoadingOverlay` islands. Sidebar/navbar/mobile-search interactivity (collapse, drawer, user menu, Ctrl/Cmd+K) is `wwwroot/js/blazor/shell.js`, a classic script loaded after `blazor.web.js` in `App.razor`. See "Blazor Components" in `patterns.md` for the static-shell-plus-islands pattern. |
| `GuildLayout` | static SSR, nested `@layout MainLayout` | `Pages/Shared/_GuildLayout.cshtml` + `GuildNavBarHelper` | Resolves the guild once via `IGuildContextProvider` (memoised — see "GuildContext" in `patterns.md`) from the guild id in `NavigationManager.Uri` (`GuildRoutes.TryGetGuildId`). On `Ok`: `Breadcrumb`, `GuildHeader` (title = the active tab's label, or the guild name for Overview/no-match — no per-page title reaches this layout, a deliberate Phase 3 simplification), and the tab nav as `TabGroup Mode=Navigation StyleVariant=Pills` (desktop, `.hidden sm:block`) plus a native `<select data-shell-action="navigate-select">` (mobile, `.sm:hidden`) that navigates on `change` via a one-line addition to `shell.js` — chosen over a details/summary or a re-implemented dropdown menu since a native select needs no JS for keyboard/screen-reader accessibility and this layout has no `IJSRuntime` of its own. `NotFound`/`Forbidden` omit the breadcrumb/header/nav entirely and render `@Body` unchanged — the page's own `GuildContextGate` (a second, independent resolution served from the same memoised provider) shows the 404/403 content. No guild id in the route renders `@Body` with no chrome. No `tab-panel.js`/`guild-nav.js`. |
| `PortalLayout` | static SSR, **not** nested under `MainLayout` (the Portal is its own audience) | `Pages/Portal/_PortalLayout.cshtml` + `Shared/_PortalHeader.cshtml` + `Shared/_PortalLanding.cshtml` + `Shared/_PortalUnauthorized.cshtml` | Adds `portal.css` via `<HeadContent>` (`app.css`/`tab-panel.css` are already global via `App.razor`). Resolves the guild id from the Portal-shaped URI (`PortalRoutes.TryGetGuildId`) and calls `IPortalContextProvider` (memoised wrapper over the existing `IPortalAccessService` — see "Portal three-state gate" in `patterns.md`), then renders one of four states: `GuildNotFound` → the design-system `EmptyState` (same copy as `GuildContextGate`'s guild-not-found fragment); `ShowLanding` → the ported landing card with "Sign in with Discord" to `LoginUrl`; `NotGuildMember` → the ported unauthorized card; `Authorized` → the ported header (icon/name/online-offline badge/`TabGroup StyleVariant=Portal` Soundboard-TTS-VOX nav) + `@Body`. `shared/keyboard-shortcuts.js` and `user-preferences.js` are both loaded (blazor-port-inventory.md Part 4 classifies both **B**, shim-survives) as classic scripts, matching the legacy layout always loading them regardless of state; neither is wired to a `RegisterShortcut`/`init(guildId)` call yet — no real Portal page exists to drive them until Phase 4f. `api-client.js`/`toast.js` are not loaded (superseded by in-circuit service calls and `ToastHost`/`IToastService`). A `<ToastHost @rendermode="InteractiveServer" />` island renders unconditionally. |

`LandingLayout` (static SSR, replaces `_LayoutLanding.cshtml` for `/landing`; Login still uses the cshtml layout) landed with the landing page; `GuildLayout`/`PortalLayout` follow in the same phase.


Error-page "Go Back"/"Try Again" buttons are `<button data-error-action="back\|reload">` wired by
a delegated click listener in the classic script `wwwroot/js/blazor/error-pages.js`, rather than
the cshtml's inline `onclick`, so they survive the `script-src 'self'` CSP planned for Phase 6.

---

## Razor Page Routes

### Public/Landing Pages

| Route | File | Purpose |
|-------|------|---------|
| `/index` | `Pages/Index.cshtml` | Authenticated home/dashboard |

### Account Pages

| Route | File | Purpose |
|-------|------|---------|
| `/account/login` | `Pages/Account/Login.cshtml` | OAuth login with Discord |
| `/account/external-login` | `Pages/Account/ExternalLogin.cshtml` | External OAuth flow handler |
| `/account/link-discord` | `Pages/Account/LinkDiscord.cshtml` | Link Discord account to profile |
| `/account/logout` | `Pages/Account/Logout.cshtml` | Sign out handler |
| `/account/privacy` | `Pages/Account/Privacy.cshtml` | Privacy policy page |

`/account/access-denied`, `/account/lockout` and `/account/profile` moved to Blazor in Phase 4
cluster 4a — see "Blazor Routes (Phase 4, permanent)" above.

### Admin Pages

| Route | File | Purpose |
|-------|------|---------|
| `/admin/settings` | `Pages/Admin/Settings.cshtml` | Tabbed application settings (General, Features, Commands, Advanced, Bot Control, AI Models, Appearance for SuperAdmins). The **AI Models** tab (`ai-models-settings`, rendered by `wwwroot/js/llm-models.js`) has two parts: an **editable per-mode defaults panel** (guild assistant, DM assistant, feature requests) — one `<select>` per mode, options limited to enabled catalog models, with a source badge (DB/Config/Fallback), a status badge, and price/context detail; saved via its own `<form id="aiModelsDefaultsForm">` (kept out of `#settingsForm` so the catalog toolbar doesn't dirty-mark the page) posted to the standard `?handler=SaveCategory&category=AiModels` path — it *is* a `SettingCategory` (`SettingCategory.AiModels`), so audit logging and reset-to-default are the normal ones; and the **model catalog table** (search/vendor/enabled/available/tools filters, sortable columns, a per-model enable switch, "Refresh from OpenRouter"), which is not a `SettingCategory` and talks to `LlmModelsController` (`api/admin/llm-models`) directly, the same pattern as Bot Control/Appearance. Saving a default takes effect on the next message with no restart (`ILlmModelResolver` cache invalidated by `ISettingsService.SettingsChanged`). |
| `/admin/performance` | `Pages/Admin/Performance/Index.cshtml` | Performance metrics dashboard (tabbed) |
| `/admin/logs` | `Pages/Admin/Logs/Index.cshtml` | System logs viewer (Audit Logs and Message Logs tabs; `/admin/audit-logs` and `/admin/message-logs` permanently redirect here) |
| `/admin/notifications` | `Pages/Admin/Notifications/Index.cshtml` | Notification center |
| `/admin/bulk-purge` | `Pages/Admin/BulkPurge.cshtml` | Bulk user/data purge tool |
| `/admin/user-purge` | `Pages/Admin/UserPurge.cshtml` | User purge utility |
| `/admin/ratwatch-analytics` | `Pages/Admin/RatWatchAnalytics.cshtml` | RatWatch analytics dashboard |
| `/Admin/Currency` | `Pages/Admin/Currency/Index.cshtml` | Bot-wide currencies, including the credit that backs paid features: create, edit, deactivate, mint authorities, and the shared wallet/ledger panel across every guild. SuperAdmin only; sidebar entry "Currency" in the Administration group. |
| `/admin/llm-usage` | `Pages/Admin/LlmUsage.cshtml` | Portal-wide LLM token/cost usage dashboard — date-range/guild/mode filters, hero totals, breakdowns by user/model/mode/day (rendered server-side from `ILlmUsageRepository`), and a per-user drill-down of raw ledger rows fetched client-side (`wwwroot/js/llm-usage.js`) from `LlmUsageController` (`api/admin/llm-usage/records`). Sidebar entry "LLM Usage" in the Administration group. |

### Guild Pages (Per-Server Management)

Reminders, Scheduled Messages, and Feature Requests moved to Blazor in Phase 4 cluster 4b — see
"Blazor Routes (Phase 4, permanent)" above.

| Route | File | Purpose |
|-------|------|---------|
| `/guild/{guildId}` | `Pages/Guilds/Index.cshtml` | Guild overview/dashboard |
| `/guild/{guildId}/members` | `Pages/Guilds/Members/Index.cshtml` | Member directory |
| `/guild/{guildId}/members/moderation/{memberId}` | `Pages/Guilds/Members/Moderation.cshtml` | Member moderation actions |
| `/guild/{guildId}/members/{memberId}` | `Pages/Guilds/Members/_MemberDetailModal.cshtml` | Member detail popup |
| `/guild/{guildId}/moderation-settings` | `Pages/Guilds/ModerationSettings/Index.cshtml` | Moderation rules configuration |
| `/guild/{guildId}/analytics` | `Pages/Guilds/Analytics/Index.cshtml` | Analytics overview |
| `/guild/{guildId}/analytics/engagement` | `Pages/Guilds/Analytics/Engagement.cshtml` | Engagement metrics |
| `/guild/{guildId}/analytics/moderation` | `Pages/Guilds/Analytics/Moderation.cshtml` | Moderation analytics |
| `/guild/{guildId}/flagged-events` | `Pages/Guilds/FlaggedEvents/Index.cshtml` | Flagged events/alerts |
| `/guild/{guildId}/flagged-events/{id}` | `Pages/Guilds/FlaggedEvents/Details.cshtml` | Flagged event details |
| `/guild/{guildId}/soundboard` | `Pages/Guilds/Soundboard/Index.cshtml` | Soundboard management |
| `/guild/{guildId}/audio-settings` | `Pages/Guilds/AudioSettings/Index.cshtml` | Audio feature settings |
| `/guild/{guildId}/text-to-speech` | `Pages/Guilds/TextToSpeech/Index.cshtml` | TTS configuration |
| `/guild/{guildId}/vox` | `Pages/Guilds/VOX/Index.cshtml` | VOX clip management |
| `/guild/{guildId}/leaderboard` | `Pages/Guilds/PublicLeaderboard.cshtml` | Public member leaderboard |
| `/Guilds/{guildId}/Currency` | `Pages/Guilds/Currency/Index.cshtml` | Guild currencies: create, edit rules, deactivate, manage mint authorities. Server-rendered cards with holder/circulation/debtor totals; every write goes out through `CurrenciesController` from `wwwroot/js/currency/currency-manage.js`. Admin + `GuildAccess`. |
| `/Guilds/{guildId}/Currency/{currencyId}` | `Pages/Guilds/Currency/Details.cshtml` | One currency's wallets and ledger, with mint / fine / adjust and the reconcile check. Renders the shared `_CurrencyWalletPanel`, driven by `currency-wallets.js` (+ `currency-reconcile.js` for administrators). Moderator + `GuildAccess`; what the viewer may actually do comes from `ICurrencyAccessService`. |
| `/Guilds/{guildId}/Currency/Prices` | `Pages/Guilds/Currency/Prices.cshtml` | Soundboard prices: per-sound price, currency picker, exempt-role picker, plus a read-only list of prices that are not this guild's sounds. Rows are keyed by `CurrencyFeatureKeys.Soundboard(soundId)` and `currency-prices.js` sends that key back untouched. Admin + `GuildAccess`. |

`/guild/{guildId}/edit`, `/welcome`, `/assistant-settings`, `/assistant-metrics`,
`/Guilds/RatWatch/Index` and the (previously undocumented) audio moderation log moved to Blazor in
Phase 4 cluster 4b — see "Blazor Routes (Phase 4, permanent)" above.

### Commands Pages

| Route | File | Purpose |
|-------|------|---------|
| `/commands` | `Pages/Commands/Index.cshtml` | Command reference & documentation |
| `/command-logs` | `Pages/CommandLogs/Index.cshtml` | Command execution logs |

### Portal Pages (User Self-Service)

| Route | File | Purpose |
|-------|------|---------|
| `/portal/soundboard` | `Pages/Portal/Soundboard/Index.cshtml` | Public soundboard player |
| `/portal/tts` | `Pages/Portal/TTS/Index.cshtml` | Public TTS interface |
| `/portal/vox` | `Pages/Portal/VOX/Index.cshtml` | Public VOX clip player |

Error pages (`/error/403`, `/error/404`, `/error/500`) and the public landing page (`/`) moved to
Blazor — see "Blazor Routes (permanent)" above.

---

## Layouts

All layouts below are located in `Pages/Shared/`.

| Layout | File | Purpose | Used By |
|--------|------|---------|---------|
| **Main Layout** | `_Layout.cshtml` | Default authenticated layout with navbar, sidebar, footer | Most admin/guild pages |
| **Landing Layout** | `_LayoutLanding.cshtml` | Unauthenticated layout for public pages | Login pages only — the public landing page (`/landing`) moved to Blazor's own `LandingLayout` (below) |
| **Guild Layout** | `_GuildLayout.cshtml` | Guild-specific layout with guild header/context | Guild pages under `/guild/{guildId}/*` |

The Blazor tree has its own layouts alongside these — see "Blazor Layouts" above for the full set
(`MainLayout`, `GuildLayout`, `PortalLayout`, `LandingLayout`, `EmptyLayout`). `MainLayout`, not
`EmptyLayout`, is `Routes.razor`'s default now; `Blazor/Layout/EmptyLayout.razor` (no chrome) is an
opt-in layout for pages that declare `@layout EmptyLayout`, e.g. the error pages, and
`Blazor/Layout/LandingLayout.razor` (marketing shell for `/landing` — no chrome of its own either,
since `App.razor` already owns the document `<head>`/theme for the whole app) is likewise opt-in.

### Layout Components

| Component | File | Purpose |
|-----------|------|---------|
| Navbar | `_Navbar.cshtml` | Top navigation bar with user menu |
| Sidebar | `_Sidebar.cshtml` | Left sidebar with navigation (admin/authenticated) |
| Toast Container | `_ToastContainer.cshtml` | Global toast notification container |
| Mobile Search | `_MobileSearchOverlay.cshtml` | Mobile-friendly search overlay |
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
| **Form Input** | `_FormInput.cshtml` | Text input fields (text, email, password, search, url, tel) | `FormInputViewModel` |
| **Form Select** | `_FormSelect.cshtml` | Dropdown selection with option groups | `FormSelectViewModel` |
| **Form Toggle** | `_FormToggle.cshtml` | Toggle/checkbox switch control | `FormToggleViewModel` |
| **Autocomplete Input** | `_AutocompleteInput.cshtml` | Text input with autocomplete suggestions | `AutocompleteInputViewModel` |

### Layout & Container Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Card** | `_Card.cshtml` | Flexible container with header/body/footer | `CardViewModel` |
| **Enhanced Card** | `_EnhancedCard.cshtml` | Advanced card with additional styling options | `EnhancedCardViewModel` |
| **Guild Stats Card** | `_GuildStatsCard.cshtml` | Guild statistics display card | `GuildStatsCardViewModel` |
| **Hero Metric Card** | `_HeroMetricCard.cshtml` | Large metric/stat card for dashboards | `HeroMetricCardViewModel` |

### Navigation & Tabs

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **NavTabs** | `_NavTabs.cshtml` | Multi-tab navigation (page/in-page/AJAX modes) | `NavTabsViewModel` |
| **Tab Panel** | `_TabPanel.cshtml` | Individual tab content panel | `TabPanelViewModel` |
| **Guild Breadcrumb** | `_GuildBreadcrumb.cshtml` | Guild context breadcrumb | `GuildBreadcrumbViewModel` |
| **Command Breadcrumb** | `_CommandBreadcrumb.cshtml` | Command context breadcrumb | `CommandBreadcrumbViewModel` |

### Status & Indicators

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Status Indicator** | `_StatusIndicator.cshtml` | Online/offline/idle/busy status dot | `StatusIndicatorViewModel` |
| **Status Badge** | `_StatusBadge.cshtml` | Status displayed as badge | `StatusBadgeViewModel` |
| **Severity Badge** | `_SeverityBadge.cshtml` | Severity level indicator (error/warning/info) | `SeverityBadgeViewModel` |
| **Bot Status Card** | `_BotStatusCard.cshtml` | Bot online status display | `BotStatusCardViewModel` |
| **Bot Status Banner** | `_BotStatusBanner.cshtml` | Bot status banner for page top | `BotStatusBannerViewModel` |
| **Connection Status** | `_ConnectionStatus.cshtml` | WebSocket/API connection status | `ConnectionStatusViewModel` |
| **Restart Banner** | `_RestartBanner.cshtml` | Bot restart in-progress banner | `RestartBannerViewModel` |

### Data Display Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Badge** | `_Badge.cshtml` | Small labeled tag/status indicator; `IsPill = true` for a fully rounded pill | `BadgeViewModel` |
| **Rule Type Icon** | `_RuleTypeIcon.cshtml` | Rule type visual indicator | `RuleTypeIconViewModel` |
| **Pagination** | `_Pagination.cshtml` | Page navigation with first/prev/next/last | `PaginationViewModel` |
| **Activity Feed** | `_ActivityFeed.cshtml` | List of activity/event items | `ActivityFeedViewModel` |
| **Activity Feed Timeline** | `_ActivityFeedTimeline.cshtml` | Vertical timeline of activities | `ActivityFeedTimelineViewModel` |
| **Audit Log Card** | `_AuditLogCard.cshtml` | Audit log entry display card | `AuditLogCardViewModel` |
| **Recent Activity Card** | `_RecentActivityCard.cshtml` | Recent activity summary widget | `RecentActivityCardViewModel` |
| **Command Stats Card** | `_CommandStatsCard.cshtml` | Command execution statistics | `CommandStatsCardViewModel` |
| **Connected Servers Widget** | `_ConnectedServersWidget.cshtml` | List of connected Discord servers | `ConnectedServersWidgetViewModel` |

### Feedback Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Alert** | `_Alert.cshtml` | Info/success/warning/error message banner | `AlertViewModel` |
| **Button** | `_Button.cshtml` | Interactive button (primary/secondary/danger/ghost) | `ButtonViewModel` |
| **Loading Spinner** | `_LoadingSpinner.cshtml` | Loading indicator (simple/dots/pulse) | `LoadingSpinnerViewModel` |
| **Skeleton** | `_Skeleton.cshtml` | Content placeholder during loading | `SkeletonViewModel` |
| **Skeleton Card** | `_SkeletonCard.cshtml` | Card-shaped skeleton loader | `SkeletonCardViewModel` |
| **Page Loading Overlay** | `_PageLoadingOverlay.cshtml` | Full-page loading overlay with backdrop | `PageLoadingOverlayViewModel` |
| **Empty State** | `_EmptyState.cshtml` | No data/no results/error state display | `EmptyStateViewModel` |
| **Confirmation Modal** | `_ConfirmationModal.cshtml` | Confirmation dialog with yes/no actions | `ConfirmationModalViewModel` |
| **Typed Confirmation Modal** | `_TypedConfirmationModal.cshtml` | Enhanced confirmation requiring text input | `TypedConfirmationModalViewModel` |
| **Pause Modal** | `_PauseModal.cshtml` | Pause/resume action dialog | `PauseModalViewModel` |

### Specialized Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **Command Header** | `_CommandHeader.cshtml` | Command title/description header | `CommandHeaderViewModel` |
| **Command Log Details Modal** | `_CommandLogDetailsModal.cshtml` | Modal for command log entry details | `CommandLogDetailsModalViewModel` |
| **Dashboard Widget** | `_DashboardWidget.cshtml` | Generic dashboard widget container | `DashboardWidgetViewModel` |
| **Quick Actions Card** | `_QuickActionsCard.cshtml` | Card with action buttons/links | `QuickActionsCardViewModel` |
| **Guild Header** | `_GuildHeader.cshtml` | Guild name/icon header | `GuildHeaderViewModel` |
| **Voice Channel Panel** | `_VoiceChannelPanel.cshtml` | Voice channel list/control panel | `VoiceChannelPanelViewModel` |
| **Toast Container** | `_ToastContainer.cshtml` | Global toast notification area | `ToastContainerViewModel` |
| **Currency Wallet Panel** | `_CurrencyWalletPanel.cshtml` | Holder list, ledger with paging, and the mint / fine / adjust modal for one currency. Static markup filled by `currency-wallets.js`; the `CanMint` / `CanFine` / `CanAdminister` flags decide which actions are rendered at all. Shared by the guild currency detail page and `/Admin/Currency`. | `CurrencyWalletPanelViewModel` |

### TTS & Audio Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **SSML Preview** | `_SsmlPreview.cshtml` | SSML text preview and editor | `SsmlPreviewViewModel` |
| **Mode Switcher** | `_ModeSwitcher.cshtml` | Switch between text/SSML modes | `ModeSwitcherViewModel` |
| **Style Selector** | `_StyleSelector.cshtml` | TTS voice style selector | `StyleSelectorViewModel` |
| **Preset Bar** | `_PresetBar.cshtml` | Preset voice/style quick selector | `PresetBarViewModel` |
| **Emphasis Toolbar** | `_EmphasisToolbar.cshtml` | SSML emphasis/prosody editor toolbar | `EmphasisToolbarViewModel` |

### User/Guild Preview Components

| Component | File | Purpose | ViewModel |
|-----------|------|---------|-----------|
| **User Preview Popup** | `_UserPreviewPopup.cshtml` | User card preview (name, avatar, info) | `UserPreviewPopupViewModel` |
| **Guild Preview Popup** | `_GuildPreviewPopup.cshtml` | Guild card preview (name, icon, stats) | `GuildPreviewPopupViewModel` |
| **Preview Popup Loading** | `_PreviewPopupLoading.cshtml` | Loading state for preview popup | `PreviewPopupLoadingViewModel` |
| **Preview Popup Error** | `_PreviewPopupError.cshtml` | Error state for preview popup | `PreviewPopupErrorViewModel` |

---

## Blazor Components

The Phase 2 component library (`docs/plans/blazor-port-plan.md` §5 "Phase 2", complete), plus
additions from later phases, at `src/DiscordBot.Bot/Blazor/Shared/` — 63 components across 7
groups, each namespaced `DiscordBot.Bot.Blazor.Shared` regardless of which group subfolder it
lives in. See `docs/articles/blazor-components.md` for parameters, source partials, and documented
fidelity deviations per component, and the "Status" section there for what each tier delivered.
This table supersedes the "Reusable Components" partials above one entry at a time as their
consuming pages are ported in Phase 4 — until then both the partial and its Blazor equivalent
exist.

| Group | Components |
| --- | --- |
| **Icons** (1) | `Icon` (+ the `IconPaths` static class of named `d` path constants) |
| **Primitives** (18) | `Alert`, `Badge`, `Button`, `Card`, `DashboardWidget`, `EmptyState`, `GuildStatsCard`, `HeroMetricCard`, `Highlight`, `Kbd`, `LoadingSpinner`, `LocalTime`, `RuleTypeIcon`, `SeverityBadge`, `Skeleton`, `SkeletonCard`, `StatusBadge`, `StatusIndicator` |
| **Forms** (10) | `Autocomplete`, `DateRangeFilter`, `FilterPanel`, `FormField`, `Select`, `SettingField`, `SortDropdown`, `TextArea`, `TextInput`, `Toggle` |
| **Navigation** (7) | `Breadcrumb`, `GuildContextSelector`, `GuildHeader`, `PageHeader`, `Pagination`, `TabGroup`, `TabPanel` |
| **Overlays** (7) | `ConfirmModal`, `GuildPreviewPopoverContent`, `LoadingOverlay`, `Modal`, `PreviewPopover`, `ToastHost`, `UserPreviewPopoverContent` |
| **Widgets** (13) | `ActivityFeed`, `AuditLogCard`, `BotStatusBanner`, `BotStatusCard`, `Chart`, `CommandStatsCard`, `ConnectedServersWidget`, `ConnectionStatus`, `NotificationBell`, `QuickActionsCard`, `RecentActivityCard`, `RestartBanner`, `VoiceChannelPanel` |
| **Tts** (7) | `EmphasisToolbar`, `ModeSwitcher`, `PauseModal`, `PresetBar`, `SsmlPreview`, `StyleSelector`, `VoiceSelector` |

Every component has a bUnit test class under `tests/DiscordBot.ComponentTests/Blazor/Shared/`
(mirroring this same group structure); the full library plus the `/components` showcase page and
every ported page is 853 tests as of 2026-09-14, now that cluster 4b is complete (784 at the end of
cluster 4a, 706 at the end of Phase 3) (`docs/articles/testing-guide.md` "Component (bUnit) tests").

---

## Component Groups by Feature

### Admin Dashboard

**Pages:**
- `/admin/performance` - Main dashboard with tabbed metrics

**Components:**
- NavTabs (tabbed interface)
- HealthTab, HealthMetricsTab, OverviewTab
- ApiTab, ApiMetricsTab, CommandsTab
- Card, HeroMetricCard, GuildStatsCard

**Purpose:** System health, performance metrics, command stats, API usage monitoring

### User Management

Moved to Blazor in Phase 4 cluster 4a — see `/Admin/Users`, `/Admin/Users/Create`,
`/Admin/Users/Edit`, `/Admin/Users/Details` under "Blazor Routes (Phase 4, permanent)" above (now
`EditForm`/`ConfirmModal`/`LocalTime`-based, with `Edit`/`Details` gated on
`IUserManagementService.CanManageUserAsync`) rather than the `FormInput`/`FormSelect`/`FormToggle`
partials this section originally described.

**Purpose:** CRUD operations for system users

### Guild Management

**Pages:**
- `/guild/{guildId}` - Guild dashboard
- `/guild/{guildId}/members` - Member directory with filters
- `/guild/{guildId}/edit` - Guild settings
- `/guild/{guildId}/moderation-settings` - Moderation configuration
- `/guild/{guildId}/analytics/*` - Guild analytics with multiple views

**Components:**
- Guild layout with context
- NavTabs for multi-section pages
- FormInput, FormSelect (settings forms)
- Card, EnhancedCard
- StatusIndicator, StatusBadge
- EmptyState, LoadingSpinner
- Pagination (for member lists)
- ActivityFeed, AuditLogCard

**Purpose:** Per-server configuration and analytics

### Audio/Soundboard

**Pages:**
- `/guild/{guildId}/soundboard` - Soundboard manager
- `/guild/{guildId}/text-to-speech` - TTS settings
- `/guild/{guildId}/vox` - VOX clip library
- `/portal/soundboard` - Public soundboard
- `/portal/tts` - Public TTS player
- `/portal/vox` - Public VOX player

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
- `/admin/logs` - Unified log viewer (Audit Logs and Message Logs tabs; replaces the old `/admin/audit-logs` and `/admin/message-logs` list pages, which now redirect here)
- `/Admin/AuditLogs/Details/{id}` (Blazor, see "Blazor Routes (Phase 4, permanent)" above) - Audit log entry details
- `/Admin/MessageLogs/Details/{id}` (Blazor, see "Blazor Routes (Phase 4, permanent)" above) - Message details
- `/commands` - Command documentation
- `/command-logs` - Command execution logs
- `/CommandLogs/Details/{id}` (Blazor, see "Blazor Routes (Phase 4, permanent)" above) - Command log details
- `/guild/{guildId}/analytics/engagement` - Engagement metrics
- `/guild/{guildId}/analytics/moderation` - Moderation analytics
- `/guild/{guildId}/ratwatch` - RatWatch monitoring
- `/admin/ratwatch-analytics` - RatWatch analytics

**Components:**
- NavTabs (multi-view analytics)
- Card, HeroMetricCard, CommandStatsCard
- AuditLogCard
- Pagination
- EmptyState, LoadingSpinner
- StatusBadge, SeverityBadge, Badge
- ActivityFeed, ActivityFeedTimeline
- FilterPanel (date range, category filters)

**Purpose:** Activity tracking, metrics visualization, moderation logs

---

## Page Hierarchy Diagram

```
/
├── Landing (unauthenticated)
│
├── Account
│   ├── Login
│   ├── ExternalLogin
│   ├── LinkDiscord
│   ├── Logout
│   ├── Profile
│   ├── Privacy
│   ├── AccessDenied
│   └── Lockout
│
├── Index (authenticated home)
│
├── Admin (SuperAdmin role)
│   ├── Users
│   │   ├── Index (list)
│   │   ├── Create
│   │   ├── Edit
│   │   └── Details
│   ├── AuditLogs
│   │   └── Details (Index redirects to Admin/Logs)
│   ├── MessageLogs
│   │   └── Details (Index redirects to Admin/Logs)
│   ├── Performance (tabbed)
│   │   ├── Overview
│   │   ├── Health
│   │   ├── HealthMetrics
│   │   ├── API
│   │   ├── APIMetrics
│   │   └── Commands
│   ├── Logs
│   ├── Notifications
│   ├── BulkPurge
│   ├── UserPurge
│   ├── RatWatchAnalytics
│   └── LlmUsage
│
├── Guild/{guildId} (per-server pages)
│   ├── Index
│   ├── Welcome
│   ├── Edit
│   ├── Members
│   │   ├── Index
│   │   ├── Moderation
│   │   └── _MemberDetailModal
│   ├── ModerationSettings
│   ├── Reminders
│   ├── ScheduledMessages
│   │   ├── Index
│   │   ├── Create
│   │   └── Edit
│   ├── Analytics
│   │   ├── Index
│   │   ├── Engagement
│   │   └── Moderation
│   ├── FlaggedEvents
│   │   ├── Index
│   │   └── Details
│   ├── RatWatch
│   ├── AssistantSettings
│   ├── AssistantMetrics
│   ├── Soundboard
│   ├── AudioSettings
│   ├── TextToSpeech
│   ├── VOX
│   ├── PublicLeaderboard
│
├── Commands
│   ├── Index
│
├── CommandLogs
│   ├── Index
│   └── _CommandLogDetailsContent
│
├── Portal (public/user pages)
│   ├── Soundboard
│   ├── TTS
│   └── VOX
│
└── Error
    ├── 403
    ├── 404
    └── 500
```

---

## Layout Usage Map

| Layout | Routes | Key Feature |
|--------|--------|-------------|
| **_LayoutLanding** | `/account/login`, `/account/external-login`, `/account/link-discord` | Public pages with minimal chrome — `/landing` (what `/` redirects anonymous visitors to) moved to Blazor's own `LandingLayout`, see "Layouts" above |
| **_Layout** | Admin, Command, Home pages | Full nav + sidebar authenticated layout |
| **_GuildLayout** | All `/guild/{guildId}/*` routes | Guild context header + nav |
| **Portal (_ViewStart)** | `/portal/*` routes | Portal-specific initialization |

---

## Key Component Relationships

### Form Validation Flow

1. **FormInput/FormSelect** capture user input
2. **Button** (type="submit") submits the form
3. **Alert** (Variant=Error) displays server validation errors
4. **ValidationScriptsPartial** enables client-side validation

### Data Display Flow

1. **Pagination** divides data into pages
2. **NavTabs** or filter controls change data view
3. **Card** containers display individual items
4. **Badge/StatusIndicator** annotate items with metadata
5. **EmptyState** shows when no data available
6. **LoadingSpinner** indicates async operations

### Navigation Flow

1. **Navbar** provides top-level navigation
2. **Sidebar** provides admin/authenticated navigation
3. **NavTabs** handle section navigation within pages
4. **Breadcrumb** shows navigation context
5. **Button** with `OnClick` triggers page navigation

---

## Shared Utilities

### JavaScript Modules

| Module | Location | Purpose |
|--------|----------|---------|
| Filter Panel | `wwwroot/js/shared/filter-panel.js` | Collapsible filters + date presets |
| NavTabs | `wwwroot/js/shared/nav-tabs.js` | Tab switching (page/in-page/AJAX) |
| Toast System | `wwwroot/js/shared/toast.js` | Toast notifications API |
| Preview Popup | `wwwroot/js/shared/preview-popup.js` | User/guild preview cards |
| Currency Manage | `wwwroot/js/currency/currency-manage.js` | Currency create/edit form, deactivation, mint authority list (guild and bot-wide pages) |
| Currency Wallets | `wwwroot/js/currency/currency-wallets.js` | Holder list, ledger paging, and the mint / fine / adjust actions behind `_CurrencyWalletPanel` |
| Currency Prices | `wwwroot/js/currency/currency-prices.js` | Soundboard price rows, exempt roles, search/filter; sends back the feature key the row carries |
| Currency Reconcile | `wwwroot/js/currency/currency-reconcile.js` | Runs the cached-balance vs ledger-sum check on the currency detail page |

### CSS Framework

- **Tailwind CSS** for all styling
- **Design System tokens** in `design-system.md`
- **Color classes:** `bg-accent-orange`, `bg-bg-primary`, `text-text-secondary`, etc.
- **Spacing:** Tailwind standard scale (1 = 4px)

---

## Component Statistics

| Category | Count |
|----------|-------|
| Razor Pages | 60+ |
| Layout Templates | 3 |
| Reusable Components | 45+ |
| Form Components | 4 |
| Data Display Components | 12 |
| Feedback Components | 10 |
| Navigation Components | 4 |
| Specialized Components | 7+ |
| Preview/Popup Components | 4 |

---

## Navigation Reference

### For AI Agents (Claude)

When working on features, use these pages as entry points:

- **Need to add a form?** Look at `/Admin/Users/Create` or `/Guilds/Edit/{guildId}` (both Blazor, `EditForm`-based — see "Blazor Routes (Phase 4, permanent)" above)
- **Need to display a list?** Look at `/Admin/Users` (Blazor, `PagedQuery`-based) or `/admin/logs`
- **Need tabbed navigation?** See `/admin/performance` or `/guild/{guildId}/analytics`
- **Need modals/popups?** See `_MemberDetailModal.cshtml` or `_ConfirmationModal.cshtml`
- **Need real-time status?** See `_StatusIndicator` and `_BotStatusCard`
- **Need to show activity?** See `_ActivityFeed` and `_AuditLogCard`

---

## See Also

- **[Component API Usage Guide](../articles/component-api.md)** - Detailed component documentation with examples
- **[Design System](../articles/design-system.md)** - Color palette, typography, tokens
- **[Form Implementation Standards](../articles/form-implementation-standards.md)** - Form patterns and validation
- **[Authorization Policies](../articles/authorization-policies.md)** - Role-based access control
- **[Interactive Components](../articles/interactive-components.md)** - Discord button interactions

---

**Maintained by:** UI Development Team
**Last Updated:** 2026-02-03
**Status:** Complete
