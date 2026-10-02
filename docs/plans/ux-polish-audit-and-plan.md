# UX Polish Audit and Plan

**Status:** In progress. Phase 0a done; next is Phase 0b.
**Date:** 2026-10-02
**Scope:** The admin web portal (Razor Pages), the member portal (Soundboard, TTS, VOX), and the shared layout, components, CSS and JS behind them.

This is the output of a read-only UX audit. The app works, but most features stop at the happy path. Loading, empty, error, pending and success states are missing; feedback is inconsistent; shared components exist but are not used. This document records what the audit found, the decisions taken, and a phased plan that agents can implement one session at a time.

## How the audit was done

- Five agents read every page, page model, partial and JS module, split by area: foundation, guild management, audio/portal/currency/analytics, admin, and dashboard/account/commands. About 230 findings came back.
- A sixth agent ran the app offline in headless Chromium against PostgreSQL. It checked 36 routes, 4 widths (320, 768, 1440, 2560), both themes, keyboard runs, form submits and reduced motion.
- Two more agents tried to disprove the 43 highest-severity claims. 40 held up as stated, 3 were partly true, none were refuted.

**Markers:** ✔ = re-verified in code by a second agent. ◉ = seen in the running app. No marker = read in code by one agent. *(inference)* = needs a quick browser check before acting on it.

Paths are relative to `src/DiscordBot.Bot/` unless they start with `docs/`. Line numbers are as of the audit date and will drift.

## Summary

The foundation is better than the app feels:

- Colour tokens are CSS variables in `wwwroot/css/site.css`, used about 2,600 times.
- Focus rings are global, reduced motion is honoured, and every page sets a `<title>`.
- Good shared pieces exist: `quickActions.confirm`, `ApiClient`, `tab-panel.js`, `autocomplete.js`, `ToastManager`.

Screens mostly don't use them. The top five problems:

1. **Broken flows from hand-built URLs and drifted JS/server contracts.** Two admin pages always 500 ◉. A successful guild save ends on an error page ✔. Create User fails silently with default settings ◉. Self-edit always fails ✔. The dashboard activity feed throws on every event ✔.
2. **The UI states things that are false.** "Bot Online" in the sidebar is hard-coded ✔◉. "Live" badges sit on data that never updates. The voice panel reports "Connected" when only SignalR is connected ✔. Load errors are swallowed and shown as "no data yet" ✔.
3. **No pending state or double-submit guard on about 50 actions,** including Login (double click counts twice toward lockout ✔) and the purge pages (F5 re-runs a purge). An expired session makes `/api` calls receive the login page HTML as data ✔◉.
4. **Fragmented and partly broken components.** 4 modal implementations plus about 12 hand-rolled ones, 7 tab systems, 3 toggles, 0 uses of the skeleton partials. `_Button` and `_FormInput` mangle extra attributes ✔. A wrong jQuery integrity hash disables client validation on 6 forms ◉.
5. **Touch, keyboard and contrast gaps.** Row actions are hover-only on 7+ screens ✔. Some radios are `display:none` ✔. `text-tertiary` is 3.3:1 and white on the primary button is 3.46:1 ✔. The closed mobile sidebar still takes 17 Tab stops ◉.

## Decisions

Taken with the product owner on 2026-10-02. Implementing agents should treat these as settled.

| # | Topic | Decision |
|---|---|---|
| D1 | Feedback | Toast for transient action results. `_Alert` for persistent page-level state (load failure, degraded, validation summary). Inline text for field errors. Error toasts persist until dismissed; repeats are deduped. Revive the TempData→toast bridge (`Extensions/TempDataExtensions.cs`, `_ToastContainer`'s `Toast*` keys) and migrate every `Error` / `ErrorMessage` / `SuccessMessage` producer to it. |
| D2 | Modals | Extend the `quickActions` dynamic API (`wwwroot/js/quick-actions.js`); do not rewrite on `<dialog>`. Add enter/exit motion via class toggles. Typed confirmation for bulk or irreversible actions (purge, Delete All, delete account); plain confirm for single-row deletes. |
| D3 | Component layer | CSS component classes (`.btn`, `.card`, `.form-*`, `.alert`, `.toggle`) are canonical. Razor partials are thin wrappers over them, like `_Badge`. |
| D4 | Contrast | Tune tokens; keep white button text. Darken the primary fill toward the pressed shade `#c9501f` (4.52:1). Text on warning is always dark (`#1a1205`, as at `site.css:1045`). Lighten `text-tertiary` until it passes AA on `bg-secondary`. Check both themes. |
| D5 | Theme | Add a header theme toggle. Follow `prefers-color-scheme` only when the user has no saved choice. Label themes "(dark)" / "(light)". No third theme for now. |
| D6 | Time | User-local time in the browser's locale and 12/24h preference (remove hard-coded `en-US`). Relative time for recency, with the absolute time on hover or focus. CSV exports stay in UTC with "UTC" in the column header. |
| D7 | Terms | "Server" in user-facing copy, "guild" in code, routes and APIs. "Sign in" / "Sign out". |
| D8 | Role-gated nav | Hide what the user cannot use. Collapse any resulting layout gap. |
| D9 | Flagged events | Relabel "Take Action" as "Record outcome" now. Enforcing actions through Discord is a feature and needs a spec in `docs/specs/` first. |
| D10 | Portal access | The voice panel on portal pages uses the portal's own endpoints (`Controllers/PortalSoundboardPlaybackController.cs:190,254`), extended if needed. Do not grant Discord members the Viewer role. `EnableMemberPortal` gates the portal independently of `AudioEnabled` (issue #947). |
| D11 | Placeholders | Wire it if a server endpoint exists (TTS Preview, Import Templates). Otherwise remove it: VOX admin Play, TTS Replay and Avg Latency, Logs "Application" tab, Engagement placeholder cards, member-modal analytics, Landing screenshot boxes. GitHub badges link to the repo or go. |
| D12 | Performance pages | Retire the 5 standalone pages; redirect to the tab shell's `?tab=`. Update `Services/Performance/AlertIncidentManager.cs:194`. "Live" only on tabs subscribed to a hub group; elsewhere "Updated X min ago". |
| D13 | Mobile tiers | Member portal: mobile-first. Guild moderation screens (Members, FlaggedEvents, Reminders, ScheduledMessages, RatWatch): card layouts under `md`. Admin system pages: no horizontal page overflow; tables scroll inside their container. |
| D14 | Notifications | Suppress the bot-disconnected alert in offline mode. Raise the 480MB memory threshold (baseline is about 624MB) or make it relative. Show the default window as a clearable "Last 7 days" chip. |
| D15 | Portal in development | Add a Development / OfflineMode-only path that links the seeded default admin to a seeded guild membership, so the portal can be tested and screenshotted. It must not be reachable in production. |
| D16 | SQLite | PostgreSQL is the standard. SQLite is being phased out: do not fix SQLite-only failures; removing SQLite is a separate PR outside this plan. Run all UX verification on PostgreSQL. |

## Security and correctness issues found along the way

Not UX, but fix first (Phase 0b).

- **Stored XSS against moderators** ✔: `Pages/Guilds/RatWatch/Incidents.cshtml:636-650` writes nickname and custom message into `innerHTML`. Any guild member can plant it.
- **Sound names reach inline JS or attributes** ✔: `Pages/Guilds/Soundboard/_SoundsList.cshtml:76,94`; `wwwroot/js/portal-soundboard.js:353,375` (`escapeHtml` does not escape quotes). Members can upload sounds.
- **`Html.Raw` inside `onclick`** ✔: `Pages/Guilds/ScheduledMessages/Index.cshtml:170`.
- Reported but not re-verified, same class: `Pages/Guilds/AudioSettings/Index.cshtml:427,845`; `Pages/Guilds/Soundboard/Index.cshtml:538-562`; `wwwroot/js/user-moderation-profile.js` (`addTag`); `wwwroot/js/moderation-settings.js` (`createTag`); `Pages/Guilds/Reminders/Index.cshtml:234`; message previews via `innerHTML` in ScheduledMessages Create/Edit and Welcome; `wwwroot/js/portal-vox.js:972-976` (self-XSS ✔).
- **Authorisation:** "Sync All Guilds" has no role check (`Pages/Index.cshtml.cs:427`). ScheduledMessages Index delete/toggle don't check the message belongs to the guild. `EnableMemberPortal` is never enforced ✔ (`Authorization/PortalGuildMemberAuthorizationHandler.cs:92-101`).
- **Other:** `returnUrl` written into `href` unsanitised (`Pages/Admin/AuditLogs/Details.cshtml`). CSV exports have no formula-injection guard (`Pages/Admin/Logs/Index.cshtml.cs:408-414`, RatWatch Incidents). `LocalRedirect` on Login throws on a non-local `ReturnUrl`.

## Foundation

**Tokens.** `site.css` is the Tailwind input; `app.css` is generated and gitignored. Two themes: Graphite (dark, default) and Purple Dusk (light). Adoption in Razor is excellent; hard-coded colour sits in charts (about 80 literals) and Discord-message replicas.

Token problems:

- Contrast failures ✔ (Graphite): `text-tertiary` `#6d6a66` on bg-primary 3.51:1, on bg-secondary 3.28:1; placeholder 2.76:1; white on primary `#e6602b` 3.46:1, on its hover 2.94:1; white on warning 2.11:1, on success 2.36:1, on accent-blue 3.09:1. `docs/articles/design-system.md` claims AA.
- `tailwind.config.js` does not scan `.cs`, but C# builds class strings, so `bg-accent-purple` is never generated ✔.
- Invalid names: `accent-green`, `text-accent-red`, `rgb(var(--hex) / 1)` at `Pages/Guilds/RatWatch/Analytics.cshtml:670` ✔, `bg-border-primary/50`.
- No `color-scheme`; `prefers-color-scheme` ignored ◉; no theme toggle in the chrome.

**Shared components.**

| Concern | What exists | State |
|---|---|---|
| Buttons | `.btn` (92 uses), `_Button` (4), ~47 hand-rolled chains | `_Button` encodes extra attributes wrongly ✔. `.btn-error` only inline in purge pages |
| Cards | `.card`, `_Card`, `_EnhancedCard` | 1 real use; 333 hand-rolled `bg-bg-secondary border … rounded-lg` chains |
| Modals | `_ConfirmationModal`, `_TypedConfirmationModal`, `_PauseModal`, `quickActions` API | ~12 hand-rolled. None animate or lock scroll. Static-modal AJAX broken. Typed modal depends on `settings.js` |
| Tabs | `tab-panel.js` (best), `nav-tabs.js`, `command-tabs.js`, `performance-tabs.js`, 3 hand-rolled | Hand-rolled ones have static `aria-selected` |
| Forms | `_FormInput`, `_FormSelect`, `_FormToggle`, `_SettingField` | `_FormInput` in 4 files vs 216 raw inputs. No autocomplete/inputmode/min/max. No error styling |
| Empty / loading | `_EmptyState` (29 pages), `_Skeleton`, `_SkeletonCard`, `_LoadingSpinner`, `LoadingManager` | ~54 hand-rolled empty states. Skeletons unused. 31 hand-rolled spinners |
| Feedback | `ToastManager`, TempData bridge, `_Alert` | 4 toast signatures. Bridge has 0 producers. `_Alert` dismiss dead |
| Pagination | `_Pagination` (9 pages) | ~6 hand-rolled. Disabled Prev/Next still links. "Showing 1-0 of 0" |
| Page shell | `.page-container`, `.page-header` | `_GuildLayout.cshtml:9` adds a second gutter. Content edge x=292 vs x=324 ◉ |
| Dead code | 13 unreferenced partials, ~3,000 lines of orphaned JS | Includes `command-loading-states.js`, token-based analytics modules, `performance-tabs.js` |

**Reference patterns to copy.**

| Pattern | Where |
|---|---|
| Dialogs | `wwwroot/js/quick-actions.js:279-623` (focus trap and return verified ◉) |
| Tabs | `wwwroot/js/tab-panel.js` |
| Combobox | `wwwroot/js/autocomplete.js` |
| Tab loader | `wwwroot/js/performance/dashboard.js` (delayed skeleton, abort, Retry panel) |
| Save button | `wwwroot/js/settings.js:99-147` (Saving → Saved → Retry) |
| In-place updates | `wwwroot/js/currency/currency-prices.js` |
| Filters | `Pages/Guilds/AudioModerationLog/Index.cshtml`, `Pages/Admin/Logs/Tabs/_AuditTab.cshtml` |
| Accessible portal | `Pages/Portal/VOX/Index.cshtml` with `wwwroot/js/portal-vox.js` |
| OAuth error copy | `Pages/Account/Login.cshtml.cs:116-130` |
| Typed confirmation | `Pages/Account/Privacy.cshtml:376-415` |
| Chart colours from tokens | `wwwroot/js/moderation-analytics.js:8-21` |
| Optimistic update | `wwwroot/js/notification-bell.js:329-350` |
| Local-date presets | `wwwroot/js/date-range-filter.js:75-120` |

`docs/layout-polish-plan.md` holds an earlier audit with open items (script `defer`, self-hosting CDN files). Fold them in rather than duplicating.

## Findings by category

Severity H/M/L. Effort S (under 2h), M (half to one day), L (several days). "Sys" = systemic.

### Broken flows

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| B-1 ◉ | `Pages/Admin/AuditLogs/Index.cshtml.cs:79`, `Pages/Admin/MessageLogs/Index.cshtml.cs:82` | `RedirectToPage("/Admin/Logs")` should be `/Admin/Logs/Index`; every visit 500s. Dashboard "View all audit logs" lands there | H | S |
| B-2 ✔ | `Pages/Guilds/Edit.cshtml.cs:170` | Redirect passes `id`, Details needs `guildId`; save succeeds then errors | H | S |
| B-3 ✔ | `Configuration/GuildNavigationConfig.cs:124`; `Pages/Guilds/FeatureRequests/Details.cshtml.cs:73`; `Pages/Guilds/FlaggedEvents/Details.cshtml.cs:108` | Feature Requests URL wrong; tab 404s on every guild page | H | S |
| B-4 ✔ | `Pages/Guilds/Index.cshtml:267` | Mobile card URL is null; tapping reloads the list | H | S |
| B-5 ✔ | `Pages/Guilds/Details.cshtml.cs:277,284` | Header Sync is `#`; Edit Settings uses `?id=` and 404s | H | S |
| B-6 ◉ | `Pages/Admin/Users/Create.cshtml` | Welcome-email checkbox has no `value`, posts `on`; model invalid, silent failure, passwords and role cleared | H | S |
| B-7 ✔ | `Pages/Admin/Users/Edit.cshtml:134-141`, `Services/UserManagementService.cs:318` | Self-edit always fails. Disabled role select posts nothing, so fixing only IsActive would demote you to Viewer | H | S |
| B-8 ✔ Sys | `wwwroot/js/quick-actions.js:30-35,151-165` | Static confirm modal ignores `form.action` and calls `json()` on a redirect. Breaks Users/Edit Reset Password and Unlink, LinkDiscord Unlink (succeeds, shows error), Settings reset (two requests, spurious error) | H | M |
| B-9 ✔ | `Pages/Admin/Logs/Tabs/_MessagesTab.cshtml:50` | Export points at a page that doesn't exist; real endpoint is `api/messages/export` | H | S |
| B-10 ✔ | `Pages/Admin/Logs/Index.cshtml.cs:121-134` | Only the `?tab=` tab loads; client tab switch shows "No audit logs yet" | H | M |
| B-11 ✔ | `Pages/Account/ExternalLogin.cshtml.cs:52` | `ErrorMessage` not `[TempData]`; all Discord-login errors lost on redirect | H | S |
| B-12 ✔ | `Pages/Account/Logout.cshtml(.cs)`, `Pages/Account/Privacy.cshtml.cs:347`, `Pages/Account/AccessDenied.cshtml:136` | GET shows "logged out" without signing out; delete-data flow and AccessDenied "Sign out" end there | H | S |
| B-13 ✔ | `wwwroot/js/dashboard-realtime.js:226`, `Pages/Shared/Components/_ActivityFeedTimeline.cshtml` | Template lacks `.activity-icon`; every live event throws | H | S |
| B-14 ✔ | `Services/Search/CommandLogsSearchProvider.cs:87` and 9 other sites | `?tab=logs` not read and tab id is `execution-logs`; links open the wrong tab | H | S |
| B-15 ✔ | `Pages/Guilds/Members/Index.cshtml:209,319`, `Pages/Guilds/FlaggedEvents/Index.cshtml:154,208` | Desktop and mobile checkboxes both counted; select-all doubles, bulk sends ids twice, failures invisible | H | S |
| B-16 ✔ | `wwwroot/js/portal-soundboard.js:902` | `dataset =` throws in strict mode; Preview does nothing | H | S |
| B-17 ✔ | `wwwroot/js/portal-tts.js:462-493` | Textarea cleared before send; failure loses the message | H | S |
| B-18 ✔ | `wwwroot/js/voice-channel-panel.js:127-153,509` | Hub connection shown as voice "Connected"; portal guards bypassed | H | S |
| B-19 ✔ | `wwwroot/js/voice-channel-panel.js:203-304`, `Controllers/AudioController.cs:14` | Portal members without an Identity role get 403 on Join/Leave/Stop (see D10) | H | S–M |
| B-20 ✔ | `Pages/Guilds/VOX/Index.cshtml:366-372` | Play only logs to console (remove per D11) | H | S |
| B-21 | `Pages/Guilds/ScheduledMessages/Edit.cshtml:511,541-550` | Time shifts by UTC offset after each failed POST; Delete unreachable | H | S |
| B-22 | `Pages/Guilds/TextToSpeech/Index.cshtml:180`, `.cshtml.cs:434` | Send ignores on-screen voice settings; Pro-mode SSML never sent | H | M |
| B-23 ✔ | `wwwroot/js/moderation-settings.js:113-132`, `:352-358` | Preset radio overwrites all rules instantly; Import Templates is a "not implemented" alert | H | S |
| B-24 ◉ | `Pages/Shared/_ValidationScriptsPartial.cshtml` | Wrong jQuery integrity hash; no client validation on 6 forms | H | S |
| B-25 ◉ | `Pages/Index.cshtml`, `Pages/Admin/Performance/*`, `Pages/Guilds/Index.cshtml`, `Pages/CommandLogs/Details.cshtml` | `dashboard-hub.js`, `toast.js` and SignalR included twice (one `@latest`); "redeclared" SyntaxErrors | M | S |
| B-26 | `Pages/Guilds/Members/Index.cshtml.cs:162,228` | Export CSV handler missing; "Never messaged" filter no-op | M | S |
| B-27 | `wwwroot/js/currency/currency-wallets.js:355` | Mint enabled with no currency; posts to `/api/currencies/null/mint` | M | S |
| B-28 | `wwwroot/js/ajax-sort.js:60-93`, `Pages/Guilds/Soundboard/Index.cshtml:424` | Sort wipes category dropdowns; Back does nothing | M | S |
| B-29 ◉ | `Pages/Guilds/AssistantMetrics.cshtml.cs` | 500 when no OpenRouter key; should say "Assistant not configured" | M | S |
| B-30 | `Pages/Guilds/FlaggedEvents/Details.cshtml:180-190` | "Ban User" only records text (relabel per D9) | H | S |

### Missing states

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| S-1 ✔◉ | `Pages/Shared/_Sidebar.cshtml:202-210` | "Bot Online" static; contradicts dashboard "Bot is Offline" | H | S |
| S-2 ✔ | `wwwroot/js/bot-status-refresh.js:132,251` | Banner selector mismatch; never flips offline | H | S |
| S-3 | `wwwroot/js/dashboard-realtime.js:152-165`, `Core/DTOs/DashboardStatsDto.cs`, `Services/DashboardUpdateService.cs` | Field names differ, broadcast never called; hero metrics frozen | M | M |
| S-4 ◉ Sys | `Pages/Admin/Performance/Index.cshtml:24`, `wwwroot/js/performance/dashboard.js:20`, `wwwroot/js/performance/alerts-realtime.js:424-489`, `wwwroot/js/dashboard-hub.js:50-84` | Static "Live"; 5-minute tab cache with no age; grey dot still says Live; hub stops after 5 retries silently; `_ConnectionStatus` unused | H | M |
| S-5 ✔ Sys | `Pages/Admin/Logs`, `Pages/Admin/AuditLogs`, `Pages/Guilds/PublicLeaderboard.cshtml.cs:242`, `Pages/Admin/RatWatchAnalytics.cshtml.cs:179`, `Pages/Guilds/Soundboard/Index.cshtml.cs:172`, `wwwroot/js/settings.js:378` | Load failures shown as empty, zeros, or console only | H | M |
| S-6 Sys | `wwwroot/js/command-tab-loader.js:117-229`, LlmUsage drill-down | No loading state; stale content left in place | M | M |
| S-7 Sys | `wwwroot/js/command-tab-loader.js:156-219`, `wwwroot/js/performance/components/chart-utils.js`, `wwwroot/js/ajax-sort.js:75-104` | Errors replace the UI (including filters) with generic or raw text; no Retry | M | M |
| S-8 ◉ | `Pages/Admin/Users/Index.cshtml:221`, `_MessagesTab.cshtml:265`, Notifications, FlaggedEvents, Portal Soundboard | Empty states ignore active filters | M | S |
| S-9 ◉ | AssistantMetrics, Commands analytics, `wwwroot/js/performance/tabs/overview.js:31-38` | Zero data shown red, or green ↑ on 0% uptime; fake response-time chart; hard-coded trends | M | S |
| S-10 ◉ | Notification alerts | Fresh install raises 2 critical alerts with raw titles (see D14) | M | S |
| S-11 Sys | `wwwroot/js/currency/currency-manage.js:117-150`, `wwwroot/js/notification-history.js`, `wwwroot/js/settings.js` | Toast then immediate reload; confirmation never seen | M | S |
| S-12 ◉ | `wwwroot/js/portal-tts.js:541`, VOX Play, Logs Application tab | Disabled with no reason | M | S |
| S-13 ◉ | TTS, Engagement, member modal, Landing | Placeholder UI shipped (see D11) | M | S |
| S-14 ◉✔ | `Program.cs:260`, `Pages/Error/*` | Only 403/404/500; 400/405/429/503 blank. 404 shows "/Error/404" as the URL. Standalone, unthemed, no `<main>`, false "team notified" | H | S–M |

### Interaction feedback

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| I-1 ✔◉ Sys | `wwwroot/js/login.js`, `Pages/Admin/BulkPurge.cshtml.cs:94-107`, `Pages/Admin/UserPurge.cshtml.cs`, ModerationSettings, `wwwroot/js/user-moderation-profile.js`, AudioSettings, portal play, ~30 more | No pending state or double-submit guard; purges have no PRG (F5 re-runs) | H | M |
| I-2 ✔ | Presets, FeatureRequests Reject, revoke mint authority, TTS/VOX history delete, Acknowledge All, Notifications Delete All | Destructive with no confirm; Delete All ignores filters while harmless Mark-all-read asks | H | S |
| I-3 | `Pages/Guilds/Soundboard/Index.cshtml:593-713`, `wwwroot/js/portal-soundboard.js:922`, `Pages/Shared/Components/_PresetBar.cshtml:439,628` | Native `alert` / `confirm` / `prompt`; `_PortalLayout` lacks `quick-actions.js` | M | S |
| I-4 Sys | `wwwroot/js/api-client.js:68-81`, ~14 call sites | Shows `message`, drops actionable `detail`; raw "HTTP 500", "undefined" | M | S |
| I-5 ✔◉ | `Extensions/IdentityServiceExtensions.cs:75-86`, `wwwroot/js/api-client.js` | Expired session: `/api` 302s to login HTML which is treated as data; 32 files use raw `fetch` | H | M |
| I-6 Sys | `wwwroot/js/toast.js`, `wwwroot/js/notification-bell.js:317`, `wwwroot/js/tts-page.js`, `_SsmlPreview` | 4 toast signatures; nonexistent `showToast` silences 6 confirmations; hover-only pause; no dedupe; 80px offset under a 56px nav | M | M |
| I-7 | `Pages/Shared/Components/_Alert.cshtml:30-43` | Dismiss ✕ dead on 21 alerts | M | S |
| I-8 | Notifications, Currency, Settings, `wwwroot/js/guild-sync.js`, `wwwroot/js/user-moderation-profile.js` | Full reload as update; scroll, selection, tab lost | M | M |
| I-9 | `site.css:605,636`, Landing tiles | Hover lift on non-clickable cards | L | S |

### Motion

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| Mo-1 | All modals; `.dropdown-menu` (`site.css:1182`) | No enter/exit animation; `display:none` defeats transitions | L | M |
| Mo-2 ◉ | 8 JS smooth scrolls, PublicLeaderboard wiggle, portal pulses, `livePulse` | Escape the (otherwise working) reduced-motion rule | L | S |
| Mo-3 | Alerts Save button, `[data-utc]` spans, ajax-sort, dashboard widgets | Layout shift | L | S |
| Mo-4 | CSS | `transition: all` ×25; duplicated keyframes | L | S |

### Visual consistency

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| V-1 ✔◉ | `site.css:70-118,434` | Contrast tokens fail AA (see Foundation, D4) | H | S |
| V-2 ✔ | `wwwroot/css/moderation.css`, `performance-pages.css`, `portal.css` | FlaggedEvents badges unstyled; `.status-badge` defined 3 ways; `.btn-error` only inline | M | S |
| V-3 ✔ | `tailwind.config.js:8-13` | `.cs` not scanned; Guild badges have no background | M | S |
| V-4 ✔ | VOX `accent-green`, `ajax-sort.js:130`, `RatWatch/Analytics.cshtml:670`, `_ConnectedServersWidget.cshtml:73` | Invalid tokens; uncoloured badges, invisible active tab | M | S |
| V-5 ◉ | `Pages/Guilds/Analytics/*` inline scripts, `wwwroot/js/rat-watch-analytics.js`, `chart-utils.js`, `command-stats-chart.js` | Dark-only hex; no `themechange` redraw; token modules unused | M | M |
| V-6 ◉ Sys | See component table; `Pages/Shared/_GuildLayout.cshtml:9` | Parallel variants of every component; inconsistent shell | M | L |
| V-7 | `Pages/Admin/MessageLogs/Details.cshtml`, Members/Moderation, Error/404, Engagement | Red for "No"/zero; white on warning 2.1:1 | M | S |
| V-8 | `quick-actions.js:290-297`, `settings.js:85-143`, `voice-channel-panel.js:524` | Default Tailwind palette in JS | L | S |
| V-9 ◉ | `_Layout.cshtml`, `theme.js`, 8 standalone pages | No toggle, OS preference ignored, static `theme-color`, standalone pages unthemed (see D5) | M | M |

### Copy

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| C-1 Sys | FeatureRequests, Currency, AssistantMetrics, Guilds/Details, LinkDiscord, AudioModerationLog, `_ConnectedServersWidget` | Raw Discord IDs as main text or typed input | M | M |
| C-2 Sys | BulkPurge, RatWatch, LlmUsage, Moderation, Privacy, Audit, alerts | Enum and key names shown to users | M | S |
| C-3 Sys | ~30 sites | No pluralisation ("1 servers", "event(s)") | L | S |
| C-4 | Throughout | Mixed guild/server, sign in/log in (see D7) | L | S |
| C-5 Sys | 9+ relative-time functions, 13 date formats, `timezone.js:44-68`, `_ActivityFeedTimeline.cshtml:59`, 5 UTC date-preset copies | Inconsistent dates and numbers; "Today" wrong in the local evening | M | M |
| C-6 | Search count, Settings "No changes" in a success banner, Lockout "15 minutes", AccessDenied "403 Forbidden", LinkDiscord code length | Misleading copy | M | S |
| C-7 | `Pages/Guilds/TextToSpeech/Index.cshtml.cs:557`, CommandLogs, FeatureRequests `DocGenError`, chart errors | Raw exception text | M | S |

### Forms

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| F-1 ✔ | `Pages/Shared/Components/_Button.cshtml:52`, `_FormInput.cshtml:119` | Extra attributes HTML-encoded; breaks Alerts ids, `style`, decimal `step`. Copy `_FormSelect.cshtml:56` | H | S |
| F-2 | `ViewModels/Components/FormInputViewModel.cs`, Users/Create, Login | No autocomplete/inputmode/min/max; Create User lacks `new-password`; Login uses `email` not `username` | M | M |
| F-3 Sys | Users/Create, ModerationSettings, AudioSettings `parseInt\|\|0`, `performance/tabs/alerts.js:31` | Errors not on the field; invalid values saved silently | M | M |
| F-4 ◉ | All forms | Focus never moves to the first error | M | S |
| F-5 | Welcome, AssistantSettings, ScheduledMessages/Create, Users/Create ◉, Search | Input or layout lost after failed submit | M | S |
| F-6 Sys | `moderation-settings.js`, `settings.js`; none on Edit, Welcome, AssistantSettings, ScheduledMessages, AudioSettings | Unsaved-changes protection missing or wrong | M | M |
| F-7 ✔ | ScheduledMessages radios, BulkPurge radios, AudioSettings role picker, preset radios | Not keyboard-reachable or no focus ring | H | S |
| F-8 ◉ | Users filters, Notifications, AudioSettings, TTS, VOX, toggles, Privacy | Unlabelled controls (~29) | M | S |
| F-9 | `site.css:328,723`, numeric inputs | 14px inputs zoom on iOS; no `inputmode` | M | S |
| F-10 | `Pages/Shared/Components/_FormToggle.cshtml` | No `role=switch`; unchecked posts nothing | L | S |

### Navigation

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| N-1 Sys | Root cause of B-1–B-5, B-14 | Hand-built URL strings; need `Url.Page`/`asp-page` plus a route smoke test | H | M |
| N-2 | `settings.js:481`, `command-filters.js:87-123`, `command-pagination.js`, `tab-panel.js:354`, ModerationSettings, LLM model filters | State not in URL or wrong; Commands Apply sends two fetches with wrong field names | M | M |
| N-3 | MessageLogs/AuditLogs Details, CommandLogs Details | Back loses filters or lands on the wrong tab | M | S |
| N-4 | `ViewModels/Components/GuildNavBarHelper.cs:23`, dashboard quick actions, Connected Servers widget, Search | Role-gated items shown, ending in Access Denied (see D8) | M | M |
| N-5 ◉ | Landing, AudioSettings/Soundboard, ModerationSettings | Wrong page titles | L | S |
| N-6 | Users/Edit, AuditLogs/Details | No breadcrumb or back link | L | S |
| N-7 | 5 standalone Performance pages, `AlertIncidentManager.cs:194` | Two parallel UIs (see D12) | M | L |
| N-8 | ScheduledMessages, `_Pagination` | Empty last page "Showing 21 to 20"; disabled links active | M | S |
| N-9 ◉ | `<main>`, AJAX swaps | Focus not managed after skip link or content swap | L | S |

### Responsive and touch

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| R-1 ✔ Sys | Reminders, ScheduledMessages, RatWatch, Guilds Soundboard/TTS, portal delete, voice skip, PresetBar | Hover-only actions in `min-w-[800–900px]` tables | H | S–M |
| R-2 ◉ | Notifications (466px at 320), Performance/Commands, Settings, Landing; Notifications clipped at 1440 | Horizontal overflow | M | S |
| R-3 | Connected Servers widget, Currency prices, Moderation analytics, FlaggedEvents/FeatureRequests `overflow-hidden` | No card layout; clipped tables | M | M |
| R-4 | `.btn` 38px, `.btn-sm` 30px, pagination 32px, VOX history 20px, voice Stop 24px | Tap targets under 44px | M | S |
| R-5 | `_Layout.cshtml:5`, `_PwaHead.cshtml`, VOX play bar | No `viewport-fit=cover`, no safe-area insets, `100vh` ×11 | M | S |
| R-6 ◉ | `_MessagesTab.cshtml:246`, `MessageLogs/Details.cshtml:173`, Reminders, Performance KPI | Long strings truncated, overflowing, or broken mid-word | M | S |
| R-7 | Portal Soundboard/TTS/VOX | Sidebar before content on phones; voice chip stale; dropdown clipped; VOX tile tap raises keyboard | H | M |
| R-8 ◉ | `Pages/Landing.cshtml:177` | No login link below 1024px | H | S |

### Accessibility

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| A-1 ◉ | `_Sidebar.cshtml`, `navigation.js` | Closed drawer has 17 focusable off-screen links | M | S |
| A-2 ✔ | `wwwroot/js/navigation.js:277-287` | Escape anywhere focuses the user menu | M | S |
| A-3 Sys | ~12 hand-rolled modals | No focus trap/return, no scroll lock | M | M |
| A-4 | Members/Moderation, ModerationSettings, Settings, RatWatch analytics, Performance container | Wrong tab roles/state; whole panel re-announced | M | M |
| A-5 | `_Alert.cshtml:12`, `toast.js`, voice panel, `_PageLoadingOverlay.cshtml` | Live regions wrong (assertive on load, double, missing, stuck busy) | M | S |
| A-6 | Status dots, vote tallies, escalation letters, heatmap, 22 canvases | Colour-only meaning; no chart text alternative | M | M |
| A-7 | Tag chips, LlmUsage rows, VOX replay, role picker, TTS header, Welcome token rows, `tr role=link` | Mouse-only custom widgets | M | S |
| A-8 ◉ | User menu, notification dropdown, recent-search listbox | Menu/listbox semantics without keyboard behaviour | L | S |
| A-9 ◉ | Login, 5 pages, Landing, error pages, Members/Moderation | Missing h1, heading skips, no `<main>`, two h1s | L | S |
| A-10 | TTS, VOX | No `dir="auto"`; Enter ignores IME composition | L | S |

### Perceived performance

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| P-1 | `_Layout.cshtml:76-117` | ~14 blocking scripts; 55 CDN tags without SRI; unused Chart.js on dashboard | M | M |
| P-2 *(inference)* | `wwwroot/js/portal-soundboard.js:68-133` | Lazy-load observer may not fire inside the scroll container; >40 sounds may never load | H? | M |
| P-3 | Commands search, Currency holder search | Not debounced or not wired | L | S |
| P-4 | `Pages/Admin/Logs/Index.cshtml.cs:366`, RatWatch CSV | Export loads all rows into memory, or only one page | M | S |
| P-5 | Currency wallets, Reminders user lookups | N+1 loads | L | M |

### Edge-case data

| ID | Location | Problem | Sev | Eff |
|---|---|---|---|---|
| E-1 | `_GuildHeader.cshtml:88`, Profile, Guilds/Index, FeatureRequests | String slicing splits emoji | L | S |
| E-2 | `Pages/Guilds/Members/Moderation.cshtml.cs:115`, dashboard Active Users | Fabricated or mislabelled data | M | S |
| E-3 | Role chips | White text on light role colours | L | S |
| E-4 | Welcome deleted channel, `RatWatch/Index.cshtml:88-93` timezone | Unknown stored values reset and overwritten on save | M | S |
| E-5 ◉ | `?page=-5`, `pageSize=100000`, standalone Performance `Hours` | Bad query params accepted silently | L | S |
| E-6 | `wwwroot/js/portal-soundboard.js:448` | Blocked localStorage leaves grid on skeleton | L | S |

## Screen inventory

| Screen | Main gaps |
|---|---|
| Dashboard `/` | Broken live layer, duplicate scripts, Restart modal stuck, role holes, no reconnect indicator |
| Landing | No mobile login, placeholder boxes, fake badges, stale "coming soon", doubled title, no `<main>`, unthemed |
| Search | Validation never rendered, misleading count, "View all" wrong tab, viewers shown logs they can't open |
| Commands | No loading/retry, errors wipe filters, two fetches per Apply, URL drift, log modal collapses page, charts unthemed |
| CommandLogs/Details | Wrong back target, duplicate `toast.js`, unlabelled copy buttons, raw errors |
| Account/Login | No pending state, no focus on error, wrong autocomplete, no h1 |
| ExternalLogin / LinkDiscord | Errors lost; unlink shows error but succeeds; no pending states; raw IDs |
| Logout / AccessDenied / Lockout | GET "logged out"; GET sign-out link; hard-coded duration; jargon; unthemed |
| Privacy / Profile | Export link as text; unnamed toggles; delete flow ends on false logout; "1 minutes"; UTC dates |
| Guilds/Index | Dead mobile card, plurals, duplicate "Showing", silent sync failure |
| Guilds/Details | Dead Sync, 404 Edit Settings, HTML built in C#, no widget retry |
| Guilds/Edit | Save errors after success, audio section silently hidden, no pending or unsaved warning |
| Welcome / AssistantSettings | Layout lost on failed POST, raw-HTML preview, fake-disabled overlays, raw tool IDs |
| AssistantMetrics | 500 without key, red 0.0%, raw IDs, stale copy |
| Members / Members/Moderation | Double-count checkboxes, dead export, broken filters, mouse-only chips, fabricated date |
| FlaggedEvents | Unstyled badges, forced 30-day filter, duplicate bulk posts, misleading "Ban", lost success message |
| ModerationSettings | Instant presets, wrong dirty flag, dead Import, no pending state, unnamed toggles |
| Reminders / ScheduledMessages | Hover-only actions, inline-JS injection, hand-rolled modals, time shift, unreachable delete |
| FeatureRequests | Tab 404, unconfirmed approve/reject, raw snowflakes, raw doc-gen error |
| PublicLeaderboard | Error shown as empty, bare 404/403, false "real-time" |
| Guilds Soundboard | Native alerts, sort breaks dropdowns, blind upload, hover-only, injection, no admin Stop |
| Guilds TextToSpeech | Send ignores sliders, SSML dropped, placeholders, silent toasts |
| Guilds VOX | Dead Play, fake disabled controls, invalid colour, hand-rolled pager |
| AudioSettings | Member-portal toggle not enforced, 4 stateless Saves, silent defaults, mouse-only role picker |
| AudioModerationLog | Best in scope; invalid user filter ignored, raw IDs |
| Portal Soundboard | Preview throws, possible lazy-load stall, no play pending, hover-only delete, native confirm, injection |
| Portal TTS | Failed send loses text, hard-coded limit, unexplained disabled Send, clipped dropdown |
| Portal VOX | Tile tap raises keyboard, replay not keyboard-reachable, 20px buttons, no safe-area |
| Guild Currency | Raw IDs, toast lost to reload, unconfirmed revoke, Mint with no currency, hand-rolled modals |
| Guild RatWatch | Hover-only actions, timezone overwrite, XSS in Incidents, CSV one page, UTC presets, invisible active tab |
| Guild Analytics | Inline dark-hex charts, duplicated UTC date filter, funnel breaks at 320, colour-only letters |
| Admin/Settings | Tab not in URL, Save posts all tabs, Save All skips 3, double reset, silent poll, 320 overflow |
| BulkPurge / UserPurge | No PRG, no progress, hidden radios, enum names, inline `.btn-error` |
| Admin/LlmUsage | Mouse-only rows, no drill-down loading, UTC preset mismatch, raw enums |
| Admin/RatWatchAnalytics | Error shown as zeros, dark-only CDN charts |
| Admin/Currency | Mint with no currency, untrapped modals, lost toast, unconfirmed revoke |
| Admin Logs / AuditLogs / MessageLogs | Index pages 500, hidden tab empty, dead export, swallowed errors, Back loses filters |
| Admin/Notifications | Reload per action, default filter counted as active, inverted confirms, unlabelled checkboxes, overflow |
| Admin/Users | Create fails silently, self-edit fails, Reset/Unlink broken, wrong autocomplete, unused delete/lock |
| Admin/Performance | Static "Live", stale cache, fake chart, `_Button` id bug, two parallel UIs |
| Error 403/404/500 | Only 3 codes, wrong URL shown, unthemed, no `<main>`, false "team notified" |
| Components showcase | Reference only; shows components pages don't use |

## Phased plan

Each phase is one agent session. Phases 0–4 are prerequisites for the screen phases. Within the screen phases, order is by user impact; they can run in parallel once Phase 4 lands, as long as two agents don't touch the same files.

All runtime verification uses PostgreSQL (D16) with `Discord:OfflineMode=true`.

### Phase 0a — Fix broken flows

- **Files:** `Pages/Admin/AuditLogs/Index.cshtml.cs`, `Pages/Admin/MessageLogs/Index.cshtml.cs`, `Pages/Guilds/Edit.cshtml.cs`, `Pages/Guilds/Index.cshtml`, `Pages/Guilds/Details.cshtml.cs`, `Configuration/GuildNavigationConfig.cs`, `Pages/Guilds/FeatureRequests/Details.cshtml.cs`, `Pages/Guilds/FlaggedEvents/Details.cshtml(.cs)`, `Pages/Admin/Users/Create.cshtml`, `Pages/Admin/Users/Edit.cshtml(.cs)`, `Pages/Account/ExternalLogin.cshtml.cs`, `Pages/Account/Logout.cshtml.cs`, `Pages/Account/AccessDenied.cshtml`, `Pages/Account/Privacy.cshtml`, `Pages/Admin/Logs/Tabs/_MessagesTab.cshtml`, `Pages/Commands/Index.cshtml.cs`, `Pages/Shared/_ValidationScriptsPartial.cshtml`, `Pages/Shared/Components/_Button.cshtml`, `Pages/Shared/Components/_FormInput.cshtml`, `Pages/Shared/Components/_ActivityFeedTimeline.cshtml`, `wwwroot/js/bot-status-refresh.js`, `wwwroot/js/portal-soundboard.js`, `wwwroot/js/portal-tts.js`, pages with duplicate script includes, `CLAUDE.md`, `DiscordBot.Bot.csproj`.
- **Changes:**
  - B-1 to B-5: use `Url.Page` / `asp-page` / `RedirectToPage` with the right page names and route values.
  - B-6: `value="true"` on the welcome-email checkbox.
  - B-7: don't post `IsActive` or `Role` for self-edit; the service keeps current values when they're absent.
  - B-11: `[TempData]` on ExternalLogin `ErrorMessage`.
  - B-12: Logout GET signs out (or renders a POST form that auto-submits); Privacy delete and AccessDenied "Sign out" actually sign out.
  - B-9: point Messages export at `api/messages/export` with the current filters.
  - B-24: correct the jQuery integrity hash.
  - F-1: `_Button` and `_FormInput` use `Html.Raw` with `HtmlEncode` per attribute, space-separated, as `_FormSelect` does.
  - B-13 and S-2: add `.activity-icon` to the template (or null-guard), fix the banner selector.
  - B-25: remove duplicate `dashboard-hub.js`, `toast.js` and SignalR includes; pin versions.
  - B-16: `previewAudio.dataset.soundId = …`.
  - B-17: keep the text until the send succeeds; honour `isSending`.
  - B-14: Commands accepts `tab` as an alias of `ActiveTab` and maps `logs` → `execution-logs`; `search` → `SearchTerm`.
  - B-30 / D9: relabel "Take Action" as "Record outcome" with matching confirm copy.
  - D16: CLAUDE.md states that local runs and UI verification use PostgreSQL, that SQLite is being phased out, and that there is no checked-in CSS. The Bot project build fails with a clear message when `SkipTailwind=true` and `wwwroot/css/app.css` is missing.
- **Acceptance:** `/Admin/AuditLogs` and `/Admin/MessageLogs` redirect to the right tab; guild Edit save lands on Details; Feature Requests tab opens; mobile guild card opens Details; Create User works with defaults; an admin can change their own display name and keeps their role; ExternalLogin errors show on Login; Logout signs out; Messages export downloads; zero console errors on `/`, `/Guilds` and the Performance pages; Alerts "Acknowledge All" and "Save" respond; search "View all" opens Execution Logs.
- **Verify:** add a test that requests every `@page` route and every `GuildNavigationConfig` URL and asserts no 5xx and no 404 for valid ids. Collect console errors across the route list. Screenshot Create User success and self-edit success.

**Done (Phase 0a).** All listed changes landed and the acceptance checks pass on PostgreSQL in offline mode. Notes for later phases:

- The route smoke test is `tests/DiscordBot.Tests/Integration/RouteSmokeTests.cs`. It boots the real app, signs in, and sweeps every page route and guild nav URL. Its `KnownFailures` list holds B-29 (AssistantMetrics 500 without an OpenRouter key, Phase 8) and the three portal pages, which 404 offline because the guild is never in the Discord client (D15, Phase 9). Remove each entry with its fix; the test fails if a listed route starts passing.
- Two extra console-error sources were fixed to meet "zero console errors": concurrent `DashboardHub.connect()` calls from the layout and page scripts (now share one attempt), and `preview-popup.js` calling `closest()` on the document.
- The Guilds/Details header Sync button posts a form (`HeaderAction.IsPost`). A failed sync now shows an error alert instead of nothing.
- Privacy delete-data signs the user out on the server before redirecting.
- With `SkipTailwind=true` and no `app.css` the build fails. `AllowMissingTailwindCss=true` opts out for compile-and-test-only environments; the SessionStart hook sets it when Node is unavailable.

### Phase 0b — Security fixes

- **Files:** every site under "Security and correctness issues".
- **Changes:** replace `innerHTML` and inline `on*` handlers carrying user data with `data-*` attributes and `textContent`, or a quote-safe `escapeHtml`. Role check on Sync All. Guild-ownership check on ScheduledMessages delete/toggle. Local-only `returnUrl`. Prefix CSV cells starting with `= + - @`. Safe `LocalRedirect` on Login.
- **Acceptance:** tests show names containing `'`, `"`, `<img onerror>` render inert on each page; non-admins get 403 on Sync All; cross-guild delete returns 404.
- **Verify:** seed a sound named `x" onmouseover="alert(1)` and a RatWatch message `<img src=x onerror=alert(1)>`; open both pages; nothing fires.

### Phase 1 — Request and feedback plumbing (D1)

- **Files:** `Extensions/IdentityServiceExtensions.cs`, `wwwroot/js/api-client.js`, `wwwroot/js/toast.js`, `Extensions/TempDataExtensions.cs`, `Pages/Shared/Components/_ToastContainer.cshtml`, `Pages/Shared/Components/_Alert.cshtml`, `wwwroot/js/loading-manager.js`, `Pages/Error/*`, `Program.cs`.
- **Changes:**
  - 401/403 JSON for `/api` and XHR instead of redirects.
  - `ApiClient`: detect `redirected` and 401 → "Session expired — sign in again" action toast; prefer `detail` over `message`; normalise network failures; add a timeout; never return HTML as data.
  - One toast API `toast.success/error/info(msg, { action })`; alias the old signatures; dedupe; pause on focus and touch; fix offset.
  - TempData→toast bridge used everywhere; migrate `Error` / `ErrorMessage` / `SuccessMessage` producers.
  - `_Alert` dismiss works without a callback; static banners use `role=status`.
  - `LoadingManager`: global `data-submit-guard` (disable, spinner, `aria-busy`, re-enable on `pageshow`); keep button icons.
  - One error layout; add 400 (session timed out, reload), 405, 429, 503; show the original path; apply theme; `<main>`; remove "team notified".
- **Acceptance:** expired-cookie fetch shows the session toast; every previously swallowed TempData message appears; double-clicking Login sends one POST; `/Error/400` renders themed.
- **Verify:** delete the auth cookie and trigger a fetch; Slow 3G double-submit on Login; screenshot each error page in both themes.

### Phase 2 — Tokens, CSS and theme (D4, D5)

- **Files:** `wwwroot/css/site.css`, `tailwind.config.js`, `wwwroot/css/moderation.css`, `performance-pages.css`, `portal.css`, `Pages/Shared/_Layout.cshtml`, `Pages/Shared/_Navbar.cshtml`, `Pages/Shared/_PwaHead.cshtml`, `wwwroot/js/theme.js`, new `wwwroot/js/chart-theme.js`.
- **Changes:** tune contrast tokens per D4; add `./**/*.cs` to Tailwind `content`; move `.status-badge`, `.severity-badge`, `.btn-error`/`.btn-danger` into `@layer components` with one definition each; fix invalid tokens (V-4); `color-scheme` and `theme-color` per theme; header theme toggle; OS preference when unset; theme labels; `viewport-fit=cover` with safe-area padding; 16px inputs and 44px targets under `pointer: coarse`; a `.row-actions` utility visible on `:focus-within` and `(hover: none)`; `chart-theme.js` reads tokens and redraws on `themechange`; cover remaining motion under reduced motion.
- **Acceptance:** automated contrast check passes both themes; FlaggedEvents badges styled; Guild badges purple; charts readable on Purple Dusk; toggle switches theme without reload.
- **Verify:** dashboard, FlaggedEvents and Performance in both themes at 1440; Reminders at 375 with touch emulation shows row actions.

### Phase 3 — Interaction primitives (D2, D3)

- **Files:** `wwwroot/js/quick-actions.js`, `Pages/Shared/Components/_ConfirmationModal.cshtml`, `_TypedConfirmationModal.cshtml`, `_FormInput.cshtml`, `ViewModels/Components/FormInputViewModel.cs`, `_FormToggle.cshtml`, `_Pagination.cshtml`, `_EmptyState.cshtml`, `_Skeleton*.cshtml`, new `wwwroot/js/empty-state.js`, `skeleton.js`, `unsaved-changes.js`, `Pages/Components.cshtml`, `docs/articles/component-api.md`.
- **Changes:**
  - `quickActions`: enter/exit motion, scroll lock, `inert` background, stacking; static-modal AJAX honours `form.action` and handles redirects (fixes B-8 at the root); `_TypedConfirmationModal` no longer needs `settings.js`.
  - `_FormInput`: autocomplete, inputmode, min, max, step, describedby; `.input-validation-error` styling.
  - New `_FormTextarea` and radio-card partials (`sr-only` inputs, visible focus).
  - `_FormToggle`: `role=switch`, hidden false value.
  - `_Pagination`: disabled spans, `aria-current`, labels, correct empty and single-page states.
  - `_EmptyState`: configurable icon and action; JS twin.
  - Skeleton helpers.
  - `unsaved-changes.js`: per-form dirty tracking with `beforeunload`.
- **Acceptance:** every primitive shows all states on `/Components`; keyboard-only through a modal; unit tests for the dirty tracker; Users/Edit Reset Password works.
- **Verify:** Tab through `/Components` at 1440 and 375; screenshot each state.

### Phase 4 — Formatting, live status and nav chrome (D6, D7)

- **Files:** new `wwwroot/js/format.js`, new `Helpers/DisplayFormat.cs`, `wwwroot/js/timezone.js`, `wwwroot/js/date-range-filter.js`, `Pages/Shared/_Sidebar.cshtml`, `wwwroot/js/navigation.js`, `wwwroot/js/dashboard-hub.js`, `Pages/Shared/Components/_ConnectionStatus.cshtml`.
- **Changes:** `formatDate`, auto-refreshing `relativeTime`, `plural`, `number`, `duration`, `currency` in JS and C#, browser locale; `timezone.js` handles AJAX-inserted content; `date-range-filter.js` becomes the only preset helper; sidebar footer shows real bot status; global connection banner and stale badge with unlimited backoff after the fast retries; sidebar `inert` when closed and `aria-expanded` on its toggle; Escape only when the menu is open; `<main tabindex="-1">`; user menu becomes a disclosure.
- **Acceptance:** stopping the bot flips the sidebar footer and banner; killing SignalR shows "Reconnecting…" then recovers; no off-screen Tab stops at 375.
- **Verify:** keyboard run at 375; screenshot the offline state.

### Phase 5 — Dashboard and realtime

- **Files:** `Pages/Index.cshtml(.cs)`, `wwwroot/js/dashboard-realtime.js`, `Services/DashboardUpdateService.cs`, `Core/DTOs/DashboardStatsDto.cs`, `Pages/Shared/Components/_ConnectedServersWidget.cshtml`, `wwwroot/js/quick-actions.js`.
- **Changes:** align the stats DTO with JS and broadcast it; accurate metric labels; Restart modal resets and shows "restarting → back online"; Sync All confirms and refreshes widgets; hide role-gated cards and collapse the grid (D8); card layout for the servers widget on mobile; relative times auto-refresh.
- **Acceptance:** live events render; metrics change after a command; a viewer sees no dead actions or holes.
- **Verify:** 320, 768, 1440 as admin and as viewer; hub offline then recovering.

### Phase 6 — Commands, Search, CommandLogs

- **Files:** `Pages/Commands/*`, `wwwroot/js/command-*.js`, `wwwroot/js/tab-panel.js`, `Pages/Search.cshtml(.cs)`, `wwwroot/js/search.js`, `Pages/CommandLogs/Details.cshtml`, `Controllers/CommandsApiController.cs`.
- **Changes:** tab loader with delayed skeleton, abort, Retry that keeps filters and shows the server message; one submit path with correct field names; filters, page and tab in the query string; reset page on filter change; log modal doesn't hijack the tab hash and deep-links work; command-list search; date-range validation; Search renders its validation message and the true total; "View all" lands on the right tab with filters; role-aware results; Details back link keeps the user's place.
- **Acceptance:** refreshing any Commands URL restores the view; one request per Apply; a 90-day error shows inline.
- **Verify:** network panel on Apply; loading, empty, error and populated states at 320 and 1440.

### Phase 7 — Guild moderation screens

- **Files:** `Pages/Guilds/Members/*`, `Pages/Guilds/FlaggedEvents/*`, `Pages/Guilds/ModerationSettings/*`, `wwwroot/js/member-directory.js`, `user-moderation-profile.js`, `moderation-settings.js`.
- **Changes:** one checkbox set per row; bulk actions report partial failures; real export and "Never messaged" filter; tabs on `tab-panel.js`; moderation profile on `ApiClient` with pending states and no reloads; keyboard tag chips; FlaggedEvents success toasts, actions for acknowledged events, user-history links; ModerationSettings confirms presets, per-tab dirty tracking, a real `<form>` with validation, sends only edited fields, wires Import Templates (D11); card layouts under `md` (D13).
- **Acceptance:** select-all on 25 rows reads 25; preset change asks first; unsaved warning fires once and correctly.
- **Verify:** keyboard-only ModerationSettings at 1440; FlaggedEvents and Members at 320.

### Phase 8 — Guild configuration and scheduling

- **Files:** `Pages/Guilds/Index`, `Details`, `Edit`, `Welcome`, `AssistantSettings`, `AssistantMetrics`, `Reminders/*`, `ScheduledMessages/*`, `FeatureRequests/*`, `PublicLeaderboard`.
- **Changes:** failed POSTs repopulate the layout via `PopulateGuildLayout`; fix ScheduledMessages time shift and reachable delete; card layouts under `md` for Reminders and ScheduledMessages; radios via the Phase 3 partial; shared confirms; previews escape HTML and render Discord markdown consistently; real disabled sections (`inert` plus explanation); show a deleted saved channel; AssistantMetrics "not configured" and "no data yet"; FeatureRequests confirm-with-reason on reject, resolved usernames, doc-gen retry; PublicLeaderboard real error state; unsaved-changes on every form.
- **Acceptance:** each form survives a failed submit with input and chrome intact; every row action reachable by touch and keyboard.
- **Verify:** at 320, submit each form invalid and screenshot.

### Phase 9 — Member portal, mobile first (D10, D13, D15)

- **Files:** `Pages/Portal/*`, `Pages/Shared/_PortalLayout.cshtml`, `Pages/Shared/Components/_VoiceChannelPanel.cshtml`, `_VoiceSelector.cshtml`, `_PresetBar.cshtml`, `wwwroot/js/voice-channel-panel.js`, `portal-*.js`, `voice-selector.js`, `Controllers/Portal*`, `Authorization/PortalGuildMemberAuthorizationHandler.cs`, a development-only sign-in path.
- **Changes:** development/offline-only seeded member sign-in (D15) first; voice panel reflects real bot voice state and calls portal endpoints (D10); `EnableMemberPortal` enforced with a friendly "portal disabled" page; sticky voice bar above content on mobile and a live mobile chip; play pending and queued feedback; lazy-load observer rooted on the grid; delete visible on touch via shared confirm; limits and counts from the server; TTS guarded Enter, `dir="auto"`, unclipped voice dropdown, wired Preview (D11); VOX no auto-focus on coarse pointers, keyboard replay, 44px history buttons; safe-area padding.
- **Acceptance:** at 375 with touch, a member can join, play, queue and delete with no keyboard pop-up or hidden controls; a failed TTS send keeps the text; disabling the portal blocks members.
- **Verify:** sign in as the seeded member; screenshots at 320, 375, 768.

### Phase 10 — Audio admin

- **Files:** `Pages/Guilds/Soundboard/*`, `TextToSpeech/*`, `VOX/*`, `AudioSettings/*`, `AudioModerationLog/*`, `wwwroot/js/tts-page.js`, `ajax-sort.js`, `Pages/Shared/Components/_SsmlPreview.cshtml`.
- **Changes:** toasts and confirms instead of native dialogs; sort keeps dropdowns; upload with client checks, progress, and correct single/multi copy; admin now-playing and Stop; TTS Send uses on-screen settings (or labels them defaults) and posts SSML; remove TTS Replay, Avg Latency and VOX Play (D11); AudioSettings single Save with dirty state, validated numbers, keyboard role picker, responsive rows; validate the AudioModerationLog user filter.
- **Acceptance:** no native dialogs; every control has success, failure and pending states.
- **Verify:** keyboard through AudioSettings; 320 screenshots.

### Phase 11 — Admin users, settings, purge

- **Files:** `Pages/Admin/Users/*`, `Pages/Admin/Settings.cshtml(.cs)`, `wwwroot/js/settings.js`, `wwwroot/js/llm-models.js`, `Pages/Admin/BulkPurge.cshtml(.cs)`, `Pages/Admin/UserPurge.cshtml(.cs)`, `Pages/Shared/Components/_RestartBanner.cshtml`.
- **Changes:** Users: `new-password`, role kept after failed submit, field-level identity errors, filter-aware empty state, delete/lock UI where allowed, copyable one-time reset password. Settings: `?category=` in URL, per-tab Save of only that tab, correct Save All and dirty tracking, labelled tab ids and arrow keys, `_Alert` reuse, honest "no changes", visible status-poll failure, no 320 overflow. Purge: PRG, progress, keyboard radios, friendly names, username in the confirm.
- **Acceptance:** F5 after purge doesn't re-run; Settings reload keeps the tab.
- **Verify:** keyboard-only Create User; Settings at 320.

### Phase 12 — Logs, notifications, LLM usage, currency (D14)

- **Files:** `Pages/Admin/Logs/*`, `AuditLogs/*`, `MessageLogs/*`, `Notifications/*`, `LlmUsage.cshtml(.cs)`, `Currency/*`, `Pages/Guilds/Currency/*`, `wwwroot/js/notification-history.js`, `llm-usage.js`, `wwwroot/js/currency/*`, alert threshold configuration.
- **Changes:** Logs tabs load on demand; errors rendered; Details keep `returnUrl`; long content wraps with an expander; neutral boolean colours; streamed or capped export with UTC headers (D6). Notifications: in-place updates, "Last 7 days" chip, confirm only destructive actions, Delete All respects filters, labelled checkboxes, no overflow at 320 or 1440; suppress offline disconnect alert and fix the memory threshold. LlmUsage: keyboard rows, drill-down loading/error, sequenced requests, local dates. Currency: user pickers instead of raw IDs, confirm on revoke, no reload, shared modals, scroll to ledger, integer validation, Mint disabled until a currency is chosen.
- **Verify:** empty, filtered-empty, error and populated states for each list at 320 and 1440.

### Phase 13 — Performance consolidation (D12)

- **Files:** `Pages/Admin/Performance/*`, `wwwroot/js/performance/*`, `performance-tabs.js`, `performance-shell.js`, `api-metrics-chart.js`, `Services/Performance/AlertIncidentManager.cs`.
- **Changes:** redirect the 5 standalone pages to `?tab=`; update notification links; delete orphaned JS; "Live" only on hub-subscribed tabs, "Updated X min ago" elsewhere; remove the fake chart and hard-coded trends; clamp `hours`; validate thresholds (warning < critical) and keep edits across tab switches; charts via `chart-theme.js` with text alternatives and empty states; `aria-live` only on a status line; fix KPI word-break.
- **Verify:** both themes; hub disconnect; fresh-install empty state.

### Phase 14 — Analytics and charts

- **Files:** `Pages/Guilds/Analytics/*`, `Pages/Guilds/RatWatch/*`, `Pages/Admin/RatWatchAnalytics.cshtml`, `wwwroot/js/*analytics*.js`, `wwwroot/js/rat-watch-analytics.js`, `filter-panel.js`.
- **Changes:** use the token-based analytics modules or delete them in favour of `chart-theme.js`; one date-filter partial on `date-range-filter.js`; funnel works at 320; text labels for escalation letters and vote tallies; data-table alternatives; RatWatch valid active-tab style, touch-visible actions, timezone select keeps unknown values, CSV exports all rows, enum display names.
- **Verify:** Purple Dusk screenshots; 320; one chart through a screen reader.

### Phase 15 — Account, landing, error copy

- **Files:** `Pages/Account/*`, `Pages/Landing.cshtml`, `Pages/Shared/_LayoutLanding.cshtml`, `wwwroot/js/login.js`.
- **Changes:** Login pending state, focus on error, `autocomplete="username"`, an h1; LinkDiscord pending states, code-length copy, expiry countdown; Lockout duration from config; Privacy real download button, labelled toggles, no scroll loss; Profile plurals and local time; Landing mobile login, no placeholder boxes, real or no badges, current feature list, `<main>`, meta description, theme.
- **Verify:** Landing at 320, 768, 1440, 2560; Login keyboard-only.

### Phase 16 — Long-tail sweep

- **Changes:** pluralisation (~30 sites); raw IDs replaced by names with the ID copyable; enum display names via a C# helper; "server" in copy (D7); remove `console.log`s, dead partials, `Pages/Index.cshtml.cs.bak` / `.temp`, unused scripts; update `docs/articles/design-system.md`, `docs/architecture/ui-inventory.md`, `.claude/agents/web-ui-portal.md`.
- **Acceptance:** grep counts for `(s)`, `[..2]`, `onclick='`, `alert(`, `console.log` reach zero or are justified in a comment.

## Definition of done for UI work

Every UI task, not just these phases, must meet this before it is pushed.

1. **States:** every data region shows loading (skeleton if over 300ms), empty, filtered-empty with "Clear filters", error with Retry and plain-language text, and success. Screenshot each.
2. **Actions:** every mutating control has a pending state, cannot be double-submitted, and gives feedback that survives (no toast-then-reload). Destructive actions use the shared confirm; typed confirm when bulk or irreversible.
3. **Shared pieces first:** shared modal, toast, `ApiClient`, `_FormInput` / `_Pagination` / `_EmptyState`, `tab-panel.js`, `format.js`. No raw `fetch`, no native `alert` / `confirm` / `prompt`, no hand-rolled modal, tabs or pagination.
4. **No user data in markup by hand:** no `innerHTML` or inline `on*` with user strings; use `data-*` and `textContent`.
5. **Routes:** links via `Url.Page` / `asp-page`; the route smoke test passes; filters, tab and page live in the query string; Back restores them.
6. **Forms:** every control labelled; correct `type`, `autocomplete`, `inputmode`; errors on the field; focus moves to the first error; input survives a failed submit; unsaved edits warn.
7. **Copy:** no raw IDs, enum names, exception text or "HTTP 500"; plurals, dates, numbers and durations through the shared formatters; "server" and "sign in" consistently.
8. **Tokens only:** no hex, `rgb()` or default-palette classes in new code (Discord-replica areas excepted with a comment); charts via `chart-theme.js`.
9. **Accessibility:** keyboard-only run passes (everything reachable, visible focus, Escape closes overlays and focus returns); icon buttons named; async results in a live region; nothing colour-only; one h1.
10. **Responsive:** 320, 768, 1440, 2560 with no horizontal page scroll; wide tables get cards (per D13) or scroll in their container; long strings wrap; no hover-only actions; 44px targets on coarse pointers.
11. **Both themes and reduced motion** checked.
12. **Zero console errors** on touched pages; no duplicate script includes.
13. **Docs:** `ui-inventory.md`, `component-api.md` and the Components showcase updated when a component changes.
