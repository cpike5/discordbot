# Commands Page

**Status:** Rebuilt in UX polish Phase 6 (replaces the Epic #1218 write-up, whose six cooperating
modules were removed). Last updated October 2026.

`/Commands` is one page with three tabs: **Command List**, **Execution Logs** and **Analytics**.
The URL is the page's state, one script owns it, and every tab shows loading, empty, error and
populated states.

## Files

| File | Job |
|---|---|
| `Pages/Commands/Index.cshtml(.cs)` | The page: breadcrumb, header, `_TabPanel` (`PersistenceMode.None`), the two filter forms, the three panels. Binds `StartDate`, `EndDate`, `GuildId`, `SearchTerm`, `CommandName`, `StatusFilter`, `ActiveTab` / `tab` so a URL renders its own form values. |
| `Pages/Commands/Tabs/_CommandListTab.cshtml` | The command list. Rendered by the page itself and by `GET /api/commands/list`. |
| `Pages/Commands/Tabs/_ExecutionLogsTab.cshtml` | Table (cards under `md`), `_Pagination`, filtered-empty state. Swapped into `[data-tab-content]`. |
| `Pages/Commands/Tabs/_AnalyticsTab.cshtml` | Stat cards, four chart canvases, chart data in a `<script type="application/json" data-commands-chart-data>` island. |
| `Controllers/CommandsApiController.cs` | The three tab partials plus `log-details/{id}`. Refusals are problem JSON, not HTML. |
| `wwwroot/js/commands-page.js` | The page controller (below). Pure helpers are exported for `__tests__/commands-page.test.js`. |
| `wwwroot/js/commands-charts.js` | Draws the analytics charts from the data island; series colours from `ChartTheme.colors()`. |
| `wwwroot/js/command-log-modal.js` | Fills the log details `quickActions` dialog (`_CommandLogDetailsModal.cshtml`). |
| `wwwroot/js/command-tabs.js` | Keeps the subtitle and breadcrumb in step with the tab. |
| `wwwroot/js/tab-panel.js` | The tab widget (shared). Ignores a URL hash that is not one of its own tab ids. |

`date-range-filter.js` is shared: the controller uses `DateRangeFilter.presetRange` / `detectPreset`.
Its older DOM helpers (`togglePanel`, `preserveHashAndSubmit`, `clearFiltersAndReload`,
`applyDefaultFilterIfNeeded`) are no longer called from this page.

## State lives in the query string

| Parameter | Meaning |
|---|---|
| `tab` | `command-list` (default, omitted), `execution-logs` or `analytics`. `logs` and `stats` are accepted; `ActiveTab` still works. An old `#analytics` link is read once and rewritten. |
| `StartDate`, `EndDate`, `GuildId` | Shared by Execution Logs and Analytics. |
| `CommandName`, `StatusFilter`, `SearchTerm` | Execution Logs only. `search` is an alias of `SearchTerm` (the Search page links with it). |
| `pageNumber` | Execution Logs page (never `page`: Razor Pages reserves it). |
| `q` | Command List search. |
| `log` | An open log details dialog. |

`GuildId` is a string in script, always (snowflakes exceed `Number.MAX_SAFE_INTEGER`). Refreshing,
sharing or pressing Back reproduces the view: filters apply as `pushState` entries, a tab switch
and the dialog use `replaceState`, and `popstate` reloads the page from its URL.

**Default range.** A bare Execution Logs view starts on "Last 7 days" (local calendar, written
into the URL). Any filter in the URL turns the default off, so a Search "View all N results" link
shows every match. Clear filters writes `StartDate=&EndDate=`, which also keeps the default off
after a refresh.

## Applying a filter

One path: `submit` (or a preset button, which fills the dates and calls `requestSubmit`) →
validate the range → write the URL → one `GET /api/commands/{logs|analytics}` with the controller's
own names (`startDate`, `endDate`, `guildId`, `searchTerm`, `commandName`, `statusFilter`,
`pageNumber`). The page resets to 1 on any filter change.

A load aborts the one before it, draws a skeleton only after 300 ms (`Skeleton.show`), and on
failure shows the server's message with **Retry** inside the tab, leaving the filters in place. A
refused request (4xx) has no Retry and puts the message next to the date fields.

**Date range rule** (client `validateRange`, server `CommandsApiController.ValidateDateRange`):
start not after end, at most 90 days. Shown inline under the date fields with
`aria-invalid` and focus on the first date.

## Pagination

`_Pagination` with `PageParameterName = "pageNumber"`. Its `BaseUrl` is built with `Url.Page`
from the request's filters, so every link is a real `/Commands?...` URL that works without script;
the page script intercepts plain clicks inside `[data-commands-pagination]`.

## Analytics

Zero commands in the range shows one empty state (or a filtered-empty state with Clear filters),
not four zero cards. The success rate keeps its tone colour but also says "Healthy", "Needs
attention" or "Failing". "Average response time" is the mean of the ten busiest commands'
averages and "Distinct commands" counts up to ten: the service only returns a top ten, and the
cards say so.

Known limits of the data (service, not page): success rate, top commands and response times use
the start date only, not the end date; top commands ignore the server filter.

## Log details

The dialog is `quickActions.openDialog` markup. `?log=<id>` deep-links to it. Moderators get an
"Open the full page" link; `CommandLogs/Details` takes `?returnUrl=` (checked by
`ReturnUrlHelper.Sanitize`) so Back returns to the view they came from. The full page is a
moderator area, so Search sends viewers to the dialog (`/Commands?tab=execution-logs&log=<id>`)
instead of a page that would answer "Access denied".

## Adding to the page

- **A filter on Execution Logs:** add the field to the form in `Index.cshtml`, its name to
  `TAB_FIELDS` / `API_NAMES` in `commands-page.js`, the bound parameter to the API action, and a
  test in `commands-page.test.js`.
- **A tab:** add it to the tab list, `TABS` / `API_ROUTES` in the script, a panel with a
  `[data-tab-content]` region, and an action in `CommandsApiController` that returns a partial.
