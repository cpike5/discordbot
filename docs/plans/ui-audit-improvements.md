# UI Audit Improvements: Implementation Plan

**Status:** implemented on branch `claude/modest-cray-86ztrk` (all nine items). Follow-ups are listed in § 13.
**Source:** UI audit of 2026-11-12, run against the app in offline mode on PostgreSQL with seeded
data (3 guilds, 40 members, 300 command logs, 12 sounds), signed in as SuperAdmin, at 1440px and
390px.
**Scope:** the nine improvements from that audit. Each is one PR with one concern, matching the
"small blast radius" rule in `CLAUDE.md`.

---

## 0. How to read this plan

Each work item has:

- **Today:** what the user sees, with the code that causes it.
- **Target:** the behaviour after the change, from the user's side.
- **Steps:** the changes in the order to make them.
- **Tests:** .NET tests (`tests/DiscordBot.Tests`) and JS tests
  (`src/DiscordBot.Bot/wwwroot/js/__tests__`, run with `npm test` in `src/DiscordBot.Bot`,
  using `fake-dom.js`).
- **Docs:** what to update (`CLAUDE.md` § Conventions: "update the docs you would have wanted").
- **Done when:** checks that can be run, mostly with the Playwright sweep in § 11.

### Corrections found while planning

Reading the code more closely changed three audit findings:

1. **Moderation Settings save button (audit item 8).** The audit said "Save overview" splits one
   form in two. It doesn't: the overview form holds only the Simple/Advanced mode, and protection
   level presets apply on their own after a confirmation (`ModerationSettings/Index.cshtml`,
   `overviewForm` and the `presetGroup` below it). The real problem is that nothing on screen
   says this. § 8 is rewritten to match.
2. **Member portal active tab (audit item 6).** The portal variant already has active-tab styles
   (`wwwroot/css/tab-panel.css:246`, ember text plus underline), and `ActiveTab = "soundboard"` is
   passed through. Yet the light-theme screenshot showed no difference. The cause isn't known
   yet, so § 6 starts with a reproduction step instead of a guessed fix.
3. **Landing page cards (audit item 9).** The cards use a CSS `fadeInUp` with delays of up to
   0.45s (`Landing.cshtml:43-55`). The blank band in the audit screenshot was most likely taken
   mid-animation, not a real bug. § 9 keeps one small change (no `opacity: 0` start under
   `prefers-reduced-motion`) and drops the rest.

---

## 1. Sequence

| Order | Item | Section | Effort | Depends on |
|---|---|---|---|---|
| 1 | Bot and guild status tell one story | § 2 | S | none |
| 2 | Finish the member portal | § 6 | S–M | none |
| 3 | Guild navigation without hidden tabs | § 3 | M | none |
| 4 | Live results in global search | § 4 | M | none |
| 5 | Tighten dense screens | § 8 | S | none |
| 6 | Visible search, consistent filters | § 7 | S–M | none |
| 7 | Guild switcher and recent guilds | § 5 | M | § 3 (shares the guild header) |
| 8 | Landing page refresh | § 9 | S | none |
| 9 | One page header, tab and width pattern | § 10 | M | §§ 3, 7 (settles their patterns first) |

Items 1–6 and 8 are independent and can run in parallel branches. Item 7 touches
`_GuildHeader.cshtml`, which item 3 leaves alone, but both change the top of every guild page, so
merge 3 first to avoid conflicts. Do item 9 last because it consolidates patterns that 3 and 7
define.

---

## 2. Bot and guild status tell one story (audit #1)

### Today

- **Dashboard guild rows** say `ONLINE` / `IDLE` / `OFFLINE`, but the value only means "ran a
  command today": `Pages/Index.cshtml.cs:328-332` sets `Online` when `commandsToday > 0`, without
  checking the bot connection. With the bot disconnected, "Pixel Pirates" still shows a green
  ONLINE badge under a red "Bot is Offline" banner. The tooltip text in
  `_ConnectedServersWidget.cshtml:11-14` ("Ran commands today") already admits what it measures.
- **Bot Performance** shows a green `OPERATIONAL` badge next to "Bot Health: Disconnected" and
  0.0% uptime. Both `PerformanceShellViewModel.OverallStatusText` (line 30) and
  `PerformanceOverviewViewModel.OverallStatusText` (line 127) fall back to "Operational" for any
  unknown status, and `PerformanceDashboardAggregator` computes `overallHealthStatus` without
  treating "disconnected" as unhealthy.
- **Commands (24h)** shows 38 on the dashboard (`DashboardStatsProvider`, from the command log
  table) but 0 on Performance (`PerformanceDashboardAggregator.cs:336`, which appears to read the
  in-memory command metrics that start empty on every process start).

### Target

- A guild row's badge describes activity, not connection: **Active today** (success) or **Quiet**
  (neutral). An inactive guild (bot left) keeps **Left** (muted).
- When the bot is not connected, the connected-servers card says so once in its header ("Bot
  offline: activity is from the log") and the badges stay activity-only. No guild row claims to
  be online.
- Performance's overall badge reads **Offline** (error style) whenever the gateway is not
  connected, whatever the other checks say. An unknown status shows **Unknown** (neutral), never
  "Operational".
- Both pages show the same "Commands (24h)" figure, taken from the command log.

### Steps

1. **Rename the enum to what it means.** In
   `ViewModels/Components/Enums/ServerConnectionStatus.cs` rename the type to
   `ServerActivityStatus` with values `ActiveToday`, `Quiet` and `Left`, and update the doc
   comments. Update `ConnectedServerItemViewModel.Status`, `Pages/Index.cshtml.cs:328-332`, the
   JSON at `Index.cshtml.cs:413-430` (`?handler=ConnectedServers`), `Components.cshtml.cs`, and
   any other reference (`grep -rn ServerConnectionStatus`).
2. **Display text.** In `_ConnectedServersWidget.cshtml`, replace `@server.Status` with a display
   switch ("Active today", "Quiet", "Left") so the enum name never reaches the page. Rename the
   column label from "Status" to "Activity". Update the `<template>` row at line 27 and
   `wwwroot/js/dashboard-actions.js`, which refills the rows from `?handler=ConnectedServers`
   after a sync, to use the same labels and badge classes. Have the JSON carry the display label
   and badge class so the label map lives only in C#.
3. **Bot-offline note.** Pass `BotStatusBanner.IsOnline` into the widget view model
   (`ConnectedServersWidgetViewModel` or a new `BotConnected` property) and render the one-line
   note in the card header when it is false. The dashboard hub's connection events already update
   the banner, so toggle the note from the same handler.
4. **Performance overall status.**
   - In `PerformanceDashboardAggregator`, before computing `overallHealthStatus`, return
     `"Offline"` when the gateway connection state is not `Connected` (the aggregator already
     reads bot health for the "Bot Health" card; reuse that value).
   - In both view models, add `"offline" => "Offline"` mapped to `health-status-error`, and change
     the default arm to `"Unknown"` / `health-status-neutral` (add that class to
     `performance-pages.css` if it doesn't exist, using `--color-text-secondary` on
     `--color-bg-tertiary`).
   - Remove the duplication: have `PerformanceOverviewViewModel` and `PerformanceShellViewModel`
     call one static helper (e.g. `HealthStatusDisplay.For(string)` returning `(Text, CssClass)`)
     in `ViewModels/Pages/`.
   - The live updater (`wwwroot/js/performance/`) has no copy of this mapping today; keep it
     that way by sending the display text with any status the hub pushes.
5. **One source for Commands (24h).** Confirm what `PerformanceDashboardAggregator.cs:320-340`
   reads. If it's the in-memory aggregator, take the 24h total from `ICommandLogService` (the
   same call `DashboardStatsProvider` uses) and keep the in-memory metrics for latency
   percentiles only. Label the card "Commands (24h)" on both pages.

### Tests

- `tests/DiscordBot.Tests/Bot/Pages/DashboardIndexModelTests.cs`: a guild with commands today is
  `ActiveToday`, one without is `Quiet`, an inactive one is `Left`, whatever the bot connection
  state is. The `ConnectedServers` JSON uses the new labels.
- `tests/DiscordBot.Tests/ViewModels/PerformanceStatusViewModelTests.cs`: `"offline"` →
  Offline/error; an unrecognised value → Unknown/neutral; existing cases unchanged.
- An aggregator test: disconnected gateway plus healthy everything else gives `"Offline"`.
- An aggregator test: the 24h command total equals the command log count for seeded rows.
- `wwwroot/js/__tests__/dashboard-actions.test.js`: a refreshed row renders "Active today" with
  `badge-success`.

### Docs

- `docs/architecture/ui-inventory.md`: the `/` row ("Connected Servers … Activity").
- `/Components` showcase: the connected-servers widget example uses the new states.

### Done when

- With the app in offline mode, no element on `/` contains the text "ONLINE"/"Online" next to a
  guild, and `/Admin/Performance` shows "Offline".
- `/` and `/Admin/Performance` show the same Commands (24h) number for the seeded data.

---

## 3. Guild navigation without hidden tabs (audit #2)

### Today

`Pages/Shared/_GuildLayout.cshtml:18-22` renders the 11 tabs from
`Configuration/GuildNavigationConfig.cs` through the shared `_TabPanel`. At 1440px the strip is
1,050px wide and its content 1,449px, so **Welcome, Assistant and Feature Requests** are off
screen. The only hint is the `.can-scroll-right` fade (`tab-panel.css:40`), which reads as a
clipped icon. Below `sm` a dropdown (`guild-nav.js`) works well.

### Target

- Desktop shows every tab that fits, in config order, and a final **More ▾** tab holding the
  rest. If the active page is in the overflow, the More tab shows as active and its label becomes
  "More: Welcome" so the user can see where they are.
- Resizing the window moves tabs in and out of More without a reload.
- Keyboard: the More button is in the tab order; Enter/Space/↓ opens it, arrow keys move through
  items, Escape closes and returns focus to the button.
- No horizontal scroll on the guild strip at any width ≥ `sm`.
- Mobile keeps today's dropdown.

### Steps

1. **Server-rendered fallback first.** In `_GuildLayout.cshtml`, render the full tab list as
   today plus a hidden `<div class="guild-nav-more">` containing a `<button aria-haspopup="menu"
   aria-expanded="false">More</button>` and an empty `<ul role="menu">`. Without JS the strip
   still scrolls, so nothing is lost.
2. **Priority-plus in `guild-nav.js`** (keep it out of the shared `tab-panel.js`, which other
   pages use as real tabs with `role="tab"` semantics; the guild strip is page navigation):
   - On load and on `ResizeObserver` of the container, measure each tab's width (cache it on
     first measure, before hiding anything), then show tabs left to right while
     `used + tab + moreButtonWidth <= available`.
   - Always keep the active tab visible if it would fit with More; if it doesn't, move it into
     More and mark More active.
   - Move overflowed links into the menu as `<li role="none"><a role="menuitem" href>` clones and
     hide the originals with `hidden`. Keep the originals in the DOM so the server markup stays
     the source of truth.
   - Turn off the scroll fade on this strip (`.guild-nav-container .tab-panel-container` →
     `overflow: visible`, no `::after`).
   - Respect `prefers-reduced-motion` for the menu's open transition.
3. **Menu behaviour.** Reuse the dropdown keyboard code already in `guild-nav.js` (Escape, click
   outside) and add arrow-key focus movement. Close on navigation.
4. **Styles** in `wwwroot/css/tab-panel.css` (or a new `guild-nav.css` imported by `site.css`):
   `.guild-nav-more-menu` uses the same surface, border and shadow tokens as the notification
   dropdown so it looks native in both themes.
5. **Optional, decide during review:** drop the per-tab icons above `lg` if the measurement still
   pushes three or more tabs into More at 1280px. Icons stay in the mobile dropdown and in More.

### Tests

- New `wwwroot/js/__tests__/guild-nav.test.js` with `fake-dom.js`: given widths
  `[100 × 11]` and an available width of 700, tabs 1–6 are visible and 7–11 are in the menu;
  when the active tab is index 9 it stays visible and index 6 moves out; Escape closes and
  restores focus. Give the measuring function an injectable width source so the test doesn't
  depend on layout.
- `Integration/RouteSmokeTests` already loads every guild route; no change needed.

### Docs

- `ui-inventory.md`: Guild Layout row ("priority-plus tabs with a More menu on desktop").
- `docs/articles/component-api.md` if it documents the guild nav.

### Done when

- Playwright at 1024, 1280, 1440 and 1920 wide: no guild strip has `scrollWidth >
  clientWidth`, and every one of the 11 links is reachable either in the strip or in More.
- On `/Guilds/Welcome/{id}` at 1280px, More shows "More: Welcome" with active styling.

---

## 4. Live results in global search (audit #3)

### Today

`_Navbar.cshtml:44-66` is a GET form to `/Search` with a `Ctrl K` hint. `search.js` focuses it on
`Ctrl/⌘ K` and shows recent searches, but typing shows nothing on desktop; mobile shows "Press
Enter to search for …" (`search.js:426`). `ISearchService.SearchAsync` already takes a
`SearchQueryDto` with `MaxResultsPerCategory` (default 5) and handles authorization per
category.

### Target

As the user types (from 2 characters, 200ms debounce):

1. **Pages:** sidebar destinations matching the text ("set" → Settings, "perf" → Bot
   Performance). These come from the links already rendered in the sidebar, so role filtering is
   free and needs no server call.
2. **Servers:** up to 5 guild matches (name or ID), each opening the guild overview.
3. **Commands:** up to 3 slash commands from the command list (name match), opening
   `/Commands?q=`.
4. A last row **"See all results for 'text'"** that submits the form as today.

Keyboard: ↓/↑ moves through rows, Enter opens the highlighted row (or submits the form when
none is highlighted), Escape closes. If exactly one guild matches and nothing is highlighted,
Enter opens that guild directly. Mobile overlay gets the same results list in place of the
"Press Enter" text.

### Steps

1. **Suggest endpoint.** Add `OnGetSuggestAsync(string q, CancellationToken ct)` to
   `Pages/Search.cshtml.cs` (page handler: `GET /Search?handler=Suggest&q=`). It returns JSON:

   ```json
   {
     "term": "pix",
     "guilds":   [{ "id": "123456789012345678", "name": "Pixel Pirates", "url": "/Guilds/Details/123…", "iconUrl": null }],
     "commands": [{ "name": "/play", "url": "/Commands?q=play" }]
   }
   ```

   - Call `_searchService.SearchAsync` with `MaxResultsPerCategory = 5`, limited to guilds and
     commands. If the service can't take a category set, call it once per category with
     `CategoryFilter` (two cheap queries) rather than changing the interface.
   - **Emit guild IDs as strings** (`CLAUDE.md` gotcha: snowflakes overflow JS numbers).
   - Return `400` with problem JSON for an empty or 1-character term, and cap `q` at 100
     characters.
   - Same `[Authorize]` as the page; the service already filters by what the user may see.
2. **Client** in `wwwroot/js/search.js`:
   - Build a `pages` index once from `document.querySelectorAll('.sidebar a[href]')`: label =
     link text, href = link href. Match by case-insensitive prefix of any word, then substring.
   - Debounced fetch through `ApiClient` (`api-client.js`) with an `AbortController`; a newer
     keystroke aborts the older request (the same approach `llm-usage.js` takes).
   - Render with `SafeHtml.escape` for every server string (`CLAUDE.md` gotcha on markup built in
     JS). Each row is an `<a>` in a `role="listbox"` container with `role="option"`, and the
     input gets `role="combobox"`, `aria-expanded`, `aria-controls` and `aria-activedescendant`.
   - States: loading (a thin progress line, not a spinner over the results), no matches ("No
     servers or commands match 'xyz'" plus the See all row), request failed (pages still shown,
     plus "Couldn't search servers. Press Enter for full search.").
   - Keep recent searches for the empty input.
   - Save a recent search when a suggestion is opened, as when the form is submitted.
3. **Mobile overlay:** render the same list into `#mobile-search-results`, sharing the render
   function.
4. **Styles:** extend the existing dropdown in `createDropdown()` (`search.js:116`) with group
   headings ("Pages", "Servers", "Commands") and a highlighted row style using
   `--color-bg-hover` plus a 2px left accent.

### Tests

- .NET: `tests/DiscordBot.Tests/Bot/Pages/SearchModelSuggestTests.cs`: returns guilds as string
  IDs; respects the service's authorization (mock returns nothing → empty arrays); 1-character
  term → 400; the term is trimmed and capped.
- JS: new `wwwroot/js/__tests__/search-suggest.test.js`: the pages index matches "perf" to Bot
  Performance; arrow keys and Enter pick the right href; a slow first response that arrives after
  a second one is ignored; server text with `<img onerror>` is escaped.

### Docs

- `ui-inventory.md`: `/Search` row and the navbar entry.
- `docs/articles/api-endpoints.md` if page handlers that return JSON are listed there (check
  first; the dashboard's `?handler=Stats` sets the precedent).

### Done when

- On desktop, typing "pix" shows "Pixel Pirates" under Servers within 500ms, and Enter opens
  `/Guilds/Details/123456789012345678`.
- Typing "sett" shows "Settings" under Pages without any network request (check in the Network
  log).
- Screen-reader smoke check: VoiceOver or NVDA announces the active option while arrowing.

---

## 5. Guild switcher and recent guilds (audit #4)

### Today

Moving from one guild's soundboard to another's is Servers → find the row → open → Audio. The
guild header (`_GuildHeader.cshtml`) shows the avatar and name as static text, and the sidebar
has no guild context. A `_GuildContextSelector` partial exists but is only used on `/Search`.

### Target

- **Switcher:** the guild name in the header becomes a button with a chevron. It opens a
  searchable list of the guilds the user can manage. Choosing one goes to **the same section** of
  that guild (Soundboard → Soundboard), or to its Overview if that section has no equivalent
  route (for example a details page with an entity ID).
- **Recent guilds:** the sidebar shows up to 3 recently opened guilds under Servers, each with its
  avatar and name, and the current one is highlighted. The mobile drawer shows the same list.
- A user with access to only one guild sees no chevron and no recent list.

### Steps

1. **Who sees which guilds.** Reuse the existing rule rather than writing a new one: the guild
   list should be what `GuildAccessAuthorizationHandler` would allow. Add
   `IGuildSwitcherService.GetAccessibleGuildsAsync(ClaimsPrincipal, CancellationToken)` (interface
   in Core under `Interfaces/`, implementation in `Bot/Services/`, registered in the matching
   `*ServiceExtensions.cs`). Admin+ gets all active guilds from `IGuildService`; other roles get
   their `UserGuildAccess` grants plus Discord-linked guilds, mirroring the handler's order.
   Cache per user for 60 seconds through `IMemoryCache` so a page load costs nothing extra
   most of the time.
2. **Section mapping.** Each `GuildNavigationConfig` entry already has an `Id` and a route. Add a
   `RouteTemplate` (e.g. `/Guilds/Soundboard/{guildId}`) to each entry if one isn't present. The
   switcher builds the target URL from the active tab's template; detail pages (IDs other than
   `guildId`) fall back to the Overview template. Put the decision in a small, testable static
   method: `GuildSwitchTarget.For(activeTabId, targetGuildId)`.
3. **Header markup.** In `_GuildHeader.cshtml`, when `GuildHeaderViewModel.SwitchableGuilds.Count
   > 1`, wrap the name in a `<button aria-haspopup="listbox">`. Render the list server-side inside
   a hidden popover (names and avatars are already known), with a filter input at the top
   when there are more than 8 guilds. Fill `SwitchableGuilds` in
   `GuildPageModelBase.PopulateGuildLayout` so every guild page and every failed POST gets it.
4. **Client** `wwwroot/js/guild-switcher.js`: open/close, filter as you type, arrow keys, Enter
   to go. Same keyboard pattern as § 3's More menu; share a tiny `menu-keyboard` helper if both
   land (`wwwroot/js/shared/`).
5. **Recent guilds:** on every guild page, `guild-switcher.js` writes `{id, name, iconUrl}` to
   `localStorage['recentGuilds']` (most recent first, max 3, IDs as strings). The sidebar
   (`Pages/Shared/_Sidebar.cshtml`, under the Servers link) has an empty
   `<ul data-recent-guilds>` that a small script fills on load. Wrap all storage access in
   try/catch; with storage blocked the list just stays empty. Drop entries whose IDs aren't in
   the switcher list on the current page, so a guild the user lost access to disappears.

### Tests

- `GuildSwitchTarget.For` unit tests: soundboard → soundboard; a details page → overview; an
  unknown tab → overview.
- `GuildSwitcherService` tests on PostgreSQL (`TestDbContextFactory`): an Admin sees all active
  guilds and no inactive ones; a Moderator with one `UserGuildAccess` grant sees one.
- JS tests for the recent list: order, de-duplication, cap of 3, storage throwing.
- `RouteSmokeTests` still covers every guild page.

### Docs

- `service-catalog.md` (new service), `feature-map.md` (Servers), `ui-inventory.md` (Guild
  Layout and sidebar), `patterns.md` if `GuildPageModelBase.PopulateGuildLayout` gains a
  documented responsibility.

### Done when

- From `/Guilds/Soundboard/{A}`, choosing guild B in the switcher lands on
  `/Guilds/Soundboard/{B}`.
- After visiting two guilds, both appear under Servers in the sidebar, most recent first, and
  survive a reload.
- A user with one guild sees a plain heading.

---

## 6. Finish the member portal (audit #6)

### Today

`/Portal/Soundboard/{id}`, `/Portal/TTS/{id}`, `/Portal/VOX/{id}` share
`Pages/Portal/_PortalLayout.cshtml` and `Shared/_PortalHeader.cshtml`.

- The active tab looked the same as the others in light theme (see § 0 correction 2).
- No account menu, no sign-out and no link back to the admin portal.
- The voice channel picker and sort control are native `<select>`s with browser styling
  (`Portal/Soundboard/Index.cshtml:1269` and the channel select).
- The guild avatar is a flat blue initial (`_PortalHeader.cshtml:59`, one letter) while the admin
  side uses a two-letter gradient avatar.
- On a 390px phone the sound grid is 2 columns of tall cards, so 6 sounds fit on screen.

### Target

- The current section is obvious in both themes: ember text, a 2px underline and
  `aria-current="page"`.
- Top right: the user's avatar opens a menu with their name, **Admin portal** (only for users
  with an admin role), **Theme**, and **Sign out**.
- Selects match the rest of the app.
- Guild avatar matches the admin side (two letters, same gradient, or the real icon).
- Phones show 3 columns of compact sound tiles (about 15 visible), with 44px play targets kept.

### Steps

1. **Reproduce the active-tab bug.** With Playwright on `/Portal/Soundboard/{id}` in light
   theme, read `getComputedStyle` of the active tab's colour and border. Then check whether
   `.active` is on the element (`_TabPanel.cshtml:22`) and which rule wins. Fix the actual cause
   (likely a light-theme override in `portal.css` or a specificity clash). Add
   `aria-current="page"` to the active link in page-navigation mode in `_TabPanel.cshtml`: it
   currently emits `aria-selected`, which is for tabs, not links.
2. **Account menu.** Add `Pages/Portal/Shared/_PortalUserMenu.cshtml`, rendered from
   `_PortalHeader.cshtml`. Data: display name and avatar from the user's claims (the same claims
   `_Navbar.cshtml` uses), and `CanOpenAdmin` from `IAuthorizationService` (`RequireViewer` or
   whichever is the lowest admin-portal policy). Sign out is a POST form to `/Account/Logout` with
   the antiforgery token. Reuse the admin navbar's dropdown script if it is self-contained;
   otherwise a small menu script that shares § 3's keyboard helper.
3. **Selects.** Replace the classes on the portal selects with the `form-select` styles used by
   `_FormSelect`, adjusted for the portal's spacing in `portal.css`. Keep them native elements
   (no custom listbox) for accessibility on phones.
4. **Avatar.** Use `TextDisplay.Initials(Model.GuildName)` (two letters) and the same avatar
   class choice as `Index.cshtml.cs:339` (`guild.Id % palette`). Move that palette into a shared
   static (`GuildAvatar.ClassFor(ulong)`) so both sides call one function.
5. **Phone grid.** In `portal.css` under `max-width: 480px`: `grid-template-columns: repeat(3,
   1fr)`, smaller card padding, play button 44px, name clamped to 2 lines, and the plays/duration
   meta on one line in `text-xs`. Check that the infinite-scroll sentinel
   (`portal-soundboard.js:94`) still fires.

### Tests

- .NET: a portal page test that the header renders the user menu, and the Admin portal link only
  for an admin.
- JS: if a new menu script is added, open/close/Escape tests.
- `RouteSmokeTests` already covers the three portal routes.

### Docs

- `ui-inventory.md`: Portal Pages section (user menu, 3-column phone grid).

### Done when

- Playwright, both themes: the active tab's computed `border-bottom-color` equals
  `--color-accent-orange`, and the other tabs' doesn't.
- At 390×844, at least 12 sound tiles are in the first viewport.
- Sign out from the portal ends on the signed-out page.

---

## 7. Visible search and consistent filters (audit #7)

### Today

- **Members** (`Guilds/Members/Index.cshtml:64`) hides search in a collapsed Filters panel; a
  lookup takes expand → type → Apply.
- **Command logs** are collapsed with a "2 ACTIVE" chip that doesn't say which two.
- **Analytics** opens expanded; **Servers** shows filters inline.
- All of these use `wwwroot/js/shared/filter-panel.js` (`data-filter-panel`).

### Target

One rule for every list page:

- The **search box** is always visible above the list and filters as you type (300ms debounce),
  updating the query string so Back and refresh keep it.
- **Other filters** stay in the collapsible panel, collapsed by default.
- **Active filters** show as removable chips between the panel and the list ("Role: Moderator
  ×", "Joined after: Nov 1 ×"), plus "Clear all". The "N ACTIVE" chip on the panel header stays
  as a summary.
- A date-range page (Analytics) shows its date range as a chip too and starts collapsed.

### Steps

1. **Chips partial.** Add `Pages/Shared/Components/_ActiveFilterChips.cshtml` with a
   `ActiveFilterChipsViewModel { IReadOnlyList<(string Label, string RemoveUrl)> Chips; string
   ClearAllUrl; }`. Each page builds `RemoveUrl` by taking the current query and dropping that
   key (a helper on `PaginatedPageModel` or a static `QueryStringHelper.Without(Request,
   key)`), and resets `pageNumber` to 1.
2. **Search outside the panel.** Add a `Search` slot to the filter panel layout so pages place
   the search input in a bar above the panel. In `filter-panel.js`, add `data-filter-live` on an
   input: on debounced input it updates the URL and does a GET for that page. Members and
   command logs are server-rendered, so the simplest correct version is a full navigation with
   `location.assign`, keeping focus and caret by restoring from the query on load
   (`autofocus` + `setSelectionRange`). A partial-reload version can come later if the reload
   feels slow.
3. **Apply to pages,** one commit each so review is easy: Members, Commands › Execution Logs,
   Admin › Users (already has inline search; just add chips), Notifications, LLM Usage,
   Rat Watch Incidents, Analytics (collapsed by default plus a date chip).
4. **Panel default.** In `filter-panel.js` the panel opens by default only when the page asks
   (`data-filter-open`); remove any page that opens it unconditionally.

### Tests

- JS: extend `filter-panel.test.js`: live input updates `location` after the debounce and not
  before; chips' remove links drop exactly one key and reset paging.
- .NET: `QueryStringHelper.Without` unit tests (keeps repeated keys it wasn't asked to drop,
  removes `pageNumber`).
- `IndexModelExportTests` for Members: export still uses the current filters.

### Docs

- `ui-inventory.md` rows for each touched page; `docs/articles/component-api.md` for the new
  partial and `data-filter-live`; `/Components` showcase gets a chips example.

### Done when

- On Members, typing "user1" in the visible search shows Member 1 and Members 10–19 without
  opening the panel, and the URL has `?SearchTerm=user1`.
- On command logs, the two default filters appear as named chips, and removing one updates the
  list.

---

## 8. Tighten dense screens (audit #8)

### Today and target, per screen

| Screen | Today | Target |
|---|---|---|
| Members table (`Guilds/Members/Index.cshtml:206+`) | `px-6 py-4` cells and a 40px avatar with name and handle stacked, so about 80px rows; 40 members make a 2,650px page | `py-2.5` cells and a 32px avatar with name and handle on one line from `lg`, about 52px rows |
| Dashboard hero cards on phones (`Index.cshtml:31`) | `grid-cols-1` below `sm`, so 4 full-width cards fill 1.2 screens | `grid-cols-2` from 0px, a smaller value font under `sm`, and the icon hidden under `sm` |
| Soundboard category per row (`_SoundsList.cshtml:51`) | Tiny native select, `text-xs`, `py-1` | `form-select form-select-sm`, at least 32px tall (44px on touch, as `.row-action-btn` already does) |
| Moderation Settings overview (see § 0 correction 1) | "Save overview" sits between the mode cards and the protection level cards, which apply on their own; nothing says so | "Save overview" stays under the mode cards but is hidden until the mode changes (driven by the form's existing `data-unsaved-changes` dirty state), and a note under "Protection level" says "Choosing a level applies it straight away, after you confirm." |

### Steps

1. Add a `.table-compact` modifier in `site.css` (cell padding, avatar size) rather than editing
   each page's utility classes, and apply it to Members first. Then check Command logs,
   Reminders, Scheduled Messages, and Admin › Users; apply where rows are over 60px.
2. Hero grid: change the grid classes in `Index.cshtml:31` and add a `.hero-card` small-screen
   rule in `site.css` for font size and icon visibility. Check 320px: two cards must still fit
   without truncating "COMMANDS (24H)"; if not, shorten the label to "Cmds (24h)" under `sm`
   only, through a `data-short-label`.
3. Soundboard select: swap classes in `_SoundsList.cshtml`; `soundboard-admin.js` binds by
   `.category-assign-select`, so keep that class.
4. Moderation overview: markup change plus a small toggle in `moderation-settings.js` that
   listens for the unsaved-changes event on `overviewForm`.

### Tests

- JS: `moderation-settings` test (create if none) that "Save overview" appears only after the
  mode changes.
- Visual check with the § 11 sweep: Members page height at 1440px with 40 members drops below
  2,000px; at 390px the four hero cards fit in the first viewport.

### Docs

- `docs/articles/design-system.md`: document `.table-compact`.
- `ui-inventory.md`: Moderation Settings row (save appears on change).

---

## 9. Landing page refresh (audit #9)

### Today

`Pages/Landing.cshtml`: the hero reads "A Discord bot that does a bunch of stuff", the only button
is "View on GitHub", and "Sign in" is a small ghost button in the nav. "Built with" and Quick
Start say **.NET 8** (lines 281 and 357) while the project targets .NET 10. Feature cards start
at `opacity: 0` and fade in (§ 0 correction 3).

### Target

- Hero: one plain sentence on what it does ("Moderation, soundboard, text-to-speech and an AI
  assistant for your Discord server, managed from your browser."), a primary **Sign in** button
  and a secondary **View on GitHub**.
- A screenshot of the dashboard under the hero, themed: light and dark images swapped with
  `<picture>` and `prefers-color-scheme`, with `loading="lazy"`, explicit width and height, and
  real alt text.
- Version strings say .NET 10, taken from one place.
- Under `prefers-reduced-motion: reduce`, cards render visible with no animation.

### Steps

1. Copy changes in `Landing.cshtml`; Sign in links to `/Account/Login` (Discord sign-in is the
   first option there).
2. Screenshots: capture `/` in both themes at 1440×900 with the § 11 script and seeded data,
   saved as `wwwroot/images/landing/dashboard-light.webp` and `-dark.webp` (aim for under 150 KB
   each). Note in the PR how to regenerate them.
3. .NET version: render from `Environment.Version.Major` in `Landing.cshtml.cs` (or a constant
   next to the version in `Directory.Build.props` if one exists), so it doesn't go stale again.
4. In the page's `<style>`, wrap the `.feature-card` animation in
   `@@media (prefers-reduced-motion: no-preference)` so the default is visible.

### Tests

- `tests/DiscordBot.Tests/Integration/AccountAndLandingPageTests.cs` still passes; add an
  assertion that the page contains `.NET 10` (or the runtime major), not `.NET 8`, and that the
  hero has a link to `/Account/Login`.

### Docs

- `ui-inventory.md` `/landing` row.

### Done when

- Playwright with `reducedMotion: 'reduce'`: every feature card has computed `opacity: 1`
  immediately after load.
- No "NET 8" text on the page.

---

## 10. One page header, tab and width pattern (audit #5)

### Today

- **Headers:** Dashboard, Servers, Logs, Notifications and LLM Usage use `.page-header` with an
  eyebrow; Commands uses `_CommandBreadcrumb` + `_CommandHeader`; Performance has its own
  breadcrumb; Settings has a bare `<h1>`; guild pages use `_GuildBreadcrumb` + `_GuildHeader`.
- **Widths:** Servers, Commands, Users and guild pages wrap in `max-w-7xl mx-auto px-4 sm:px-6
  lg:px-8 py-8`; Dashboard, Settings, Performance, Logs, Notifications and LLM Usage don't, so
  content shifts sideways between pages.
- **Tabs:** pills in a card (guild, Settings), plain underline (Commands), underline with icons
  (Performance), underline in the portal.

### Target

- **One header partial** `_PageHeader.cshtml` with `PageHeaderViewModel { Breadcrumbs, Eyebrow?,
  Title, Subtitle?, Actions, Badge? }`. Top-level pages show a breadcrumb only when they're more
  than one level deep; the eyebrow names the sidebar group ("Overview", "Administration").
  `_GuildHeader` and `_CommandHeader` become thin wrappers that build a `PageHeaderViewModel`.
- **One width:** the content container moves into `_Layout.cshtml` (`<main class="page-container">`
  with the `max-w-7xl` rules), and pages drop their own wrappers. Pages that need full width (for
  example wide tables in Logs) opt out with `ViewData["FullWidth"] = true`.
- **Two tab levels:**
  - Primary navigation between pages of a section: pill style (as the guild strip today).
  - Secondary tabs within a page: underline style (as Commands today).
  Settings' tabs become underline (they are in-page sections); Performance's become pills (they
  are pages, `?tab=`), or stay underline if review agrees they are in-page. Record the decision
  in `design-system.md`.

### Steps

1. Write `_PageHeader.cshtml` and its view model; add it to `/Components` with every
   combination.
2. Move the container into `_Layout.cshtml` behind a `ViewData` switch; first make it opt-in
   (`ViewData["Contained"] = true`) and convert pages one at a time so each diff is small, then
   flip the default and delete the opt-in in a last commit.
3. Convert headers page by page (Dashboard, Servers, Commands, Performance, Settings, Users,
   Logs, Notifications, LLM Usage, Currency, Bulk Purge, User Purge, Rat Watch Analytics), then
   the guild and command wrappers.
4. Tabs: change the `StyleVariant` passed to `_TabPanel` on Settings and Performance (and
   `nav-tabs.css` users) per the decision above. Delete CSS variants no page uses any more
   (`grep` the variant name first).
5. Remove the now-unused header partials if nothing else references them.

### Tests

- `RouteSmokeTests` covers every page.
- Add one Razor rendering test for `_PageHeader` (breadcrumb omitted at depth 1, actions
  rendered, title in a single `<h1>`).
- § 11 sweep: compare the left edge of the `<h1>` across all top-level pages at 1440px; it should
  be the same x for every page.

### Docs

- `docs/articles/design-system.md` (header and tab rules), `component-api.md` (`_PageHeader`),
  `ui-inventory.md` (Layouts section), `patterns.md` if page scaffolding is described there.

### Done when

- At 1440px, every top-level page's `<h1>` starts at the same x and every page has exactly one
  `<h1>`.
- No page uses the Settings-style pill tabs for in-page sections.

---

## 11. Verification for every PR

Every PR is checked the same way the audit was run, so before/after can be compared.

1. **Build and tests:** `dotnet build DiscordBot.sln`, `dotnet test DiscordBot.sln` (needs
   PostgreSQL, as in `CLAUDE.md`), and `npm test` in `src/DiscordBot.Bot`.
2. **Run offline on PostgreSQL** with a seeded admin:

   ```bash
   ConnectionStrings__DefaultConnection="Host=localhost;Database=discordbot_ui;Username=postgres;Password=postgres" \
   Discord__OfflineMode=true ElasticApm__Enabled=false \
   Identity__DefaultAdmin__Email=admin@example.com Identity__DefaultAdmin__Password='Audit-Pass-123!' \
   dotnet run --project src/DiscordBot.Bot
   ```

3. **Seed** the same data as the audit (3 guilds, one with a long name; 40 members; 300 command
   logs with some failures; 8 reminders; 3 scheduled messages; 12 sounds; 6 notifications). Keep
   the SQL as `scripts/dev/seed-ui-audit.sql` (a new top-level `scripts/` folder; the repo has
   none today) so every PR uses identical data; it inserts into
   `Guilds`, `GuildAudioSettings`, `Users`, `GuildMembers`, `CommandLogs`, `Reminders`,
   `ScheduledMessages`, `Sounds` and `UserNotifications`.
4. **Screenshot sweep:** a Playwright script at `scripts/dev/ui-sweep.js` that signs in, visits
   the 30 audit routes at 1440×900 and 390×844 in both themes, records status codes, console
   errors and `scrollWidth - clientWidth`, and writes screenshots into a folder named on the
   command line. Attach before/after shots of the touched screens to each PR.
5. **Accessibility smoke check** on touched pages: keyboard-only pass (Tab, Shift+Tab, Enter,
   Escape, arrows in menus) and `axe-core` from a CDN in the sweep script, failing on new
   serious or critical violations.

Items 3 and 4 are worth landing as the first commit of PR 1, since every later PR uses them.

---

## 12. Open decisions

These don't block starting. Each has a recommended default that the plan assumes.

| # | Question | Recommended default |
|---|---|---|
| D1 | Per-guild badge wording (§ 2) | "Active today" / "Quiet" / "Left" |
| D2 | Should the guild strip drop icons above `lg` (§ 3)? | Keep icons; decide after measuring with More in place |
| D3 | Suggest endpoint as a page handler or an API controller (§ 4)? | Page handler on `/Search`, as the dashboard already does |
| D4 | Recent guilds stored per browser or per account (§ 5)? | Per browser (`localStorage`); per account needs a table and isn't worth it yet |
| D5 | Performance tabs: pages (pills) or in-page (underline) (§ 10)? | Pills, since each tab has its own `?tab=` URL and loads its own data |
| D6 | Landing screenshot: real capture or illustration (§ 9)? | Real capture from the seeded dev data |

---

## 13. Out of scope

- Moderator, Viewer and member roles were not audited; each PR should still check the touched
  page with a Moderator (`OfflineAppHost.CreateSignedInClientAsync` shows how to create one).
- SQLite: nothing here touches the schema or migrations.
- Live voice state, real member counts and SignalR updates need a connected bot. § 2's hub
  changes must be checked against a real gateway connection before release.

### Follow-ups found during implementation

- **Guild access in full search.** `GuildsSearchProvider` calls `IGuildService.GetGuildsAsync` without the user, so `/Search` lists every guild to any signed-in user. The navbar suggestions and the guild switcher filter through `IAuthorizationService` with `GuildAccessRequirement`; the full search should too.
- **Dead authorization handler.** `GuildAccessAuthorizationHandler` is never registered; `Bot/Authorization/GuildAccessHandler.cs` is the one that runs, and it ignores `UserGuildAccess` grants. Decide which rules are intended.
- **Landing screenshot** was captured in offline mode, so it shows "Bot is Offline" and zero members. Recapture against a connected bot or replace with an illustration (D6).
- **Pre-existing axe findings** from the sweep: mobile `.topbar-brand` link name, colour contrast on the connection chip and Performance status cards, unfocusable scroll regions (Logs table, activity feed, System Health), portal VOX panel ARIA, Portal Soundboard nested interactive controls, Soundboard `#file-input` label.
- **Console 404s** on guild Details, Members and Reminders pages during the sweep; not yet traced.
- **Headers not converted** to `_PageHeader`: Search, Users Details/Create/Edit, Command log Details, Audit and Message log Details, Account/Profile.
- **Not verified:** a real gateway connection (§ 2 hub changes), Moderator/Viewer passes in a browser, keyboard-only and screen-reader passes.
