# UX Polish Audit and Plan

**Status:** Done. All phases (0a through 16) landed; open items are listed in each phase's Done note.
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

**Done (Phase 0b).** Every listed site is fixed, plus the same patterns found elsewhere. Notes:

- `wwwroot/js/safe-html.js` adds `SafeHtml.escape` (quote-safe), loaded by both layouts. The ~20 per-file `escapeHtml` copies used the `textContent`/`innerHTML` trick, which leaves quotes alone; they now escape `"` and `'` too. Consolidating them onto `SafeHtml` is left for Phase 16.
- Beyond the audit list, the same inline-handler pattern was fixed in RatWatch Index (accused username), TextToSpeech admin (message text), VOX admin (clip names), ModerationSettings and Members/Moderation (tag names), and the Soundboard category list. The Welcome preview also inserted the guild name as HTML.
- ScheduledMessages Edit POST had the same missing guild check as Index delete/toggle; it now returns 404 too.
- `ReturnUrlHelper.Sanitize` now rejects non-local URLs, which fixes Login and ExternalLogin `LocalRedirect` throwing and the AuditLogs Details `href`. Logout checks too.
- CSV: `CsvField.NeutralizeFormula` covers the audit (Logs and AuditLogs), message and member exports; the RatWatch client export has the same guard.
- `EnableMemberPortal` enforcement stays with Phase 9 (D10), which owns the portal authorization handler.
- Tests: `Integration/SecurityHardeningTests` (stored hostile text on four pages, Sync All 403, cross-guild delete/toggle/edit 404, foreign return URLs) on the shared `TestHelpers/OfflineAppHost`. The client-rendered sites (Incidents modal, category list, previews, JS-created tags) were checked in a browser with hostile data seeded; no script ran.

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

**Done (Phase 1).** All listed changes landed; the acceptance checks pass on PostgreSQL in offline mode (Login double-click sends one POST; dropping the auth cookie and calling `ApiClient` or raw `fetch` shows one "session expired" toast with Sign in; error pages checked in both themes). Notes for later phases:

- **Toasts.** `toast.success/error/warning/info(msg, { title, duration, action, key })` is the API (`wwwroot/js/toast.js`, documented in `docs/articles/component-api.md`). The old shapes are aliases, so the ~190 existing call sites were not rewritten; move them to `toast.*` as each screen phase touches them. `Components/_ToastContainer` (the bottom variant, included by five pages, which duplicated the container id) is gone; the layouts own the container.
- **TempData bridge.** Every `[TempData] SuccessMessage` / `ErrorMessage` / `StatusMessage` producer moved to `TempData.Set*Toast`; the alert blocks that showed them are gone. Load failures are plain `ErrorMessage` page state with a non-dismissible `_Alert` (`GuildPageModelBase.ErrorMessage` is no longer `[TempData]`). Previously swallowed messages now show: Logs load failure, AuditLogs/Logs export failures, PublicLeaderboard load failure, Users/Edit reset-password and unlink errors, ScheduledMessages/Edit delete errors, Guilds/Details sync failures. Login and ExternalLogin keep their inline form error. Users/Edit's one-time password and Privacy's export link are `[TempData]` data shown once in a dismissible `_Alert`, because a toast closes too soon to copy them. `Integration/FeedbackPlumbingTests` covers redirect → toast → shown once.
- **Session expiry.** Script requests (`/api`, `/hubs`, `X-Requested-With`, JSON-only `Accept`) get 401/403 problem JSON (`HttpRequestExtensions.IsScriptRequest`). `ApiClient` sends `X-Requested-With`, and also watches same-origin responses to raw `fetch()`, so the 32 raw-`fetch` files get the toast without being rewritten. They still need moving to `ApiClient` for the error, timeout and pending handling (definition of done, item 3).
- **Error text.** `ApiClient` prefers `detail` for 4xx and ignores it for 5xx, because about 28 controller catch blocks put `ex.Message` in `detail` on a 500. A few return `ex.Message` with a 400 from a catch-all (`PortalTtsSynthesisController`, `PortalTtsPresetsController`, `PortalVoxController`, moderation controllers); those still reach users and belong to the phases that own those screens.
- **Submit guard.** `data-submit-guard` exists and is on the Login forms only. Each screen phase adds it to its POST forms (the purge pages in Phase 11 first). `LoadingManager.setButtonLoading` now keeps the button's icon slot and label.
- **Error pages.** One page, `Pages/Error/Index.cshtml` (`/Error/{statusCode}`), on `_ErrorLayout`. It handles every verb and ignores antiforgery, so the antiforgery 400 renders ("This page has expired", with Reload). Direct visits answer 200; re-executed errors keep the real status. Copy is in `ErrorPageModel.Describe`.
- **Found along the way.** Reminders bound its page number as `page`, which Razor Pages reserves for the page name: Cancel always threw and Previous/Next dropped the page number. Fixed (now `pageNumber`). ScheduledMessages, FeatureRequests, RatWatch and FlaggedEvents still use `asp-route-page` in their hand-rolled pagination, which URL generation overwrites with the page name, so their Previous/Next links always lead to page 1; they go when those screens move to `_Pagination` (Phases 7 and 8).

### Phase 2 — Tokens, CSS and theme (D4, D5)

- **Files:** `wwwroot/css/site.css`, `tailwind.config.js`, `wwwroot/css/moderation.css`, `performance-pages.css`, `portal.css`, `Pages/Shared/_Layout.cshtml`, `Pages/Shared/_Navbar.cshtml`, `Pages/Shared/_PwaHead.cshtml`, `wwwroot/js/theme.js`, new `wwwroot/js/chart-theme.js`.
- **Changes:** tune contrast tokens per D4; add `./**/*.cs` to Tailwind `content`; move `.status-badge`, `.severity-badge`, `.btn-error`/`.btn-danger` into `@layer components` with one definition each; fix invalid tokens (V-4); `color-scheme` and `theme-color` per theme; header theme toggle; OS preference when unset; theme labels; `viewport-fit=cover` with safe-area padding; 16px inputs and 44px targets under `pointer: coarse`; a `.row-actions` utility visible on `:focus-within` and `(hover: none)`; `chart-theme.js` reads tokens and redraws on `themechange`; cover remaining motion under reduced motion.
- **Acceptance:** automated contrast check passes both themes; FlaggedEvents badges styled; Guild badges purple; charts readable on Purple Dusk; toggle switches theme without reload.
- **Verify:** dashboard, FlaggedEvents and Performance in both themes at 1440; Reminders at 375 with touch emulation shows row actions.

**Done (Phase 2).** All listed changes landed; the acceptance checks pass on PostgreSQL in offline mode (dashboard, FlaggedEvents and Performance screenshotted in both themes at 1440; the toggle switches theme and recolours the Performance charts without a reload; Reminders at 375 with touch shows row actions and 16px inputs, and hides them on desktop until hover). Notes for later phases:

- **Inks and fills.** In the dark theme no single colour passes both as text on the surfaces and as a background under white text, so every accent and semantic colour now has an ink (`--color-success`) and a fill (`--color-success-fill`, `-fill-hover`, `-fill-active`). Tailwind `bg-*` resolves to the fill and every other utility to the ink, so the ~170 existing `bg-x text-white` chains became compliant without edits. Tints (`bg-x/10`, `--color-x-bg`, badge and alert backgrounds) are made from the fill. Text on warning is `text-on-warning` (`#1a1205`) in both themes; Purple Dusk's warning fill is now a light amber. The primary fill is `#c9501f` (D4). Purple Dusk's tertiary, placeholder and semantic inks were darkened too, since they failed on its surfaces.
- **Contrast check.** `tests/DiscordBot.Tests/Bot/Styles/DesignTokenContrastTests.cs` parses `site.css` and holds every text/surface, ink/surface, ink/tint and text/fill pairing at 4.5:1 in both themes. Hard-coded hex in pages, JS and charts is not covered; that stays with the screen phases (definition of done, item 8).
- **Theme (D5).** Layouts and the standalone Account pages and `PublicLeaderboard` put `theme-root` on `<html>` (`ThemeRootTagHelper`) and `<partial name="_ThemeHead" />` in `<head>`. With no saved choice (no stored user preference, no cookie) the page follows `prefers-color-scheme` before first paint and while open; the admin default theme now only applies when the browser states no preference or script is off. `IThemeService.GetCurrentThemeAsync` reports whether the theme was saved. A cookie now also applies to a signed-in user with no stored preference. `_ThemeToggle` is in the top bar and the portal header, and saves through the new `PUT/DELETE /api/theme/preference`, the endpoints the old `theme.js` was already calling. Themes are relabelled "Graphite (dark)" and "Purple Dusk (light)" by a data migration (both providers). `_LayoutLanding` is not themed yet (Phase 15). There is no "follow the system again" control in the UI: `ThemeManager.clearTheme(true)` and `DELETE /api/theme/preference` exist, and Profile (Phase 15) is the natural place for it.
- **Charts.** `chart-theme.js` (loaded by `_Layout`) sets `Chart.defaults` from the tokens, repaints each chart's own axis, grid, legend, title and tooltip colours as it is created and on `themechange`, and turns animation off under reduced motion. That makes the inline dark-hex charts' chrome readable on Purple Dusk; their series colours are still hard-coded (Phases 13 and 14). `Performance.ChartUtils` reads its colours from it.
- **Components.** `.status-badge`, `.severity-badge` and `.pulse-dot` have one definition in `site.css` (removed from `moderation.css`, `performance-pages.css` and `portal.css`); FlaggedEvents was rendering them unstyled because it never loaded `moderation.css`. `.btn-error` is an alias of `.btn-danger`; the purge pages' inline copies are gone. Notifications' "Delete selected" used `btn-error` with no definition and is now styled.
- **Invalid tokens (V-4).** Fixed VOX `accent-green`, ajax-sort `text-accent-red`, PublicLeaderboard `accent-primary`, RatWatch Analytics `rgb(var(--hex) / 1)`, the Connected Servers widget's composed classes (the Idle/Online badge had no background), `border-success-border/30`, and `ring-border-focus/NN` (`--color-border-focus` now has an RGB triplet). Variant-composed colour classes in `_QuickActionsCard`, the confirmation modals and `quick-actions.js` are safelisted.
- **Touch and motion.** `viewport-fit=cover` everywhere the theme head is, with `--safe-*` inset tokens applied to the top bar, drawer, page container, toasts, mobile search and portal body; `100dvh` alongside `100vh` in the shell. Under `pointer: coarse` text inputs are 16px and `.btn`, `.btn-sm`, icon buttons, nav items and close buttons are 44px. `.row-actions` replaced the inline hover-only rules on Reminders, ScheduledMessages and RatWatch and the `opacity-0 group-hover:opacity-100` reveals in Soundboard, TTS, `tts-page.js` and the voice queue. The 8 JS smooth scrolls respect reduced motion. CSS animations were already covered by the global reduced-motion rule.
- **Not verified:** the SQLite migration (nothing exercises the SQLite provider). Real iOS safe-area insets were not tested on a device.

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

**Done (Phase 3).** All listed changes landed; every primitive shows its states on `/Components` (both themes, 1440 and 375, no console errors); keyboard-only through a modal passes; Users/Edit Reset Password and Unlink, LinkDiscord Unlink and Settings reset each send one POST with no spurious error. Notes for later phases:

- **Modals.** `quick-actions.js` is the only modal layer: enter/exit motion, scroll lock, `inert` background, focus trap and return, stacking. Use `quickActions.openDialog(el)` / `closeDialog` for a page's own modal markup; cancel and backdrop elements carry `data-modal-dismiss`. `quick-actions.js` is frozen for the screen phases: report needed changes instead of editing it.
- **Confirm forms (B-8).** Confirm forms post over fetch to their own `form.action`; put the handler in the URL (`?handler=`, the partials do). Redirect answers are not followed as data; the page reloads once so TempData toasts show. Opt out with `data-submit-mode="navigate"`, or `data-custom-submit` for a page that submits itself. `quickActions.submitQuickAction` (dashboard cards) still has the old `.json()` bug: Phase 5.
- **Forms.** `_FormInput` takes autocomplete, inputmode, min/max/step, pattern and describedby; `.input-validation-error` is styled. New `_FormTextarea`, `_RadioCard` / `_RadioCardGroup` (sr-only inputs, visible focus) for Phase 8 and 11 radios. `_FormToggle` is `role=switch` and posts `false` when off (`PostsFalseWhenOff = false` for scripts that read `checked`). Legacy `.form-toggle*` CSS stays until Settings command modules, `llm-models.js` and Privacy move to the partial.
- **Unsaved changes.** `data-unsaved-changes` on a form opts in; `data-unsaved-dirty-on-load` on a form re-rendered after a failed POST; call `UnsavedChanges.markClean(form)` after a fetch save.
- **Loading and empty.** `Skeleton.show` (300 ms delay) and `EmptyState.error/filtered/empty` from script; `_SkeletonTable`, `_SkeletonLines` and `_EmptyState` (icon, action, heading level) from Razor. `_Pagination` handles disabled ends, `aria-current`, "No results", single page and past-the-end; it hides on one page without a count. `PageParameterName` still defaults to `page`, which Razor Pages reserves: pass `pageNumber`.
- **Open:** nav-tabs buttons have no visible focus indicator (Phase 6 owns tab scripts); `ShowCharacterCount` counts only at load; `_PortalLayout` lacks `quick-actions.js` (Phase 9).

### Phase 4 — Formatting, live status and nav chrome (D6, D7)

- **Files:** new `wwwroot/js/format.js`, new `Helpers/DisplayFormat.cs`, `wwwroot/js/timezone.js`, `wwwroot/js/date-range-filter.js`, `Pages/Shared/_Sidebar.cshtml`, `wwwroot/js/navigation.js`, `wwwroot/js/dashboard-hub.js`, `Pages/Shared/Components/_ConnectionStatus.cshtml`.
- **Changes:** `formatDate`, auto-refreshing `relativeTime`, `plural`, `number`, `duration`, `currency` in JS and C#, browser locale; `timezone.js` handles AJAX-inserted content; `date-range-filter.js` becomes the only preset helper; sidebar footer shows real bot status; global connection banner and stale badge with unlimited backoff after the fast retries; sidebar `inert` when closed and `aria-expanded` on its toggle; Escape only when the menu is open; `<main tabindex="-1">`; user menu becomes a disclosure.
- **Acceptance:** stopping the bot flips the sidebar footer and banner; killing SignalR shows "Reconnecting…" then recovers; no off-screen Tab stops at 375.
- **Verify:** keyboard run at 375; screenshot the offline state.

**Done (Phase 4).** All listed changes landed; killing the app shows the connection banner and a Stale badge, and it recovers by itself; an expired session ends retrying with "Signed out"; no off-screen Tab stops at 375; dates checked in en-US, en-GB and de-DE. Notes for later phases:

- **Formatting.** `format.js` (`window.Format`: `formatDate`, auto-refreshing `relativeTime` with the absolute time on hover/focus, `plural`, `number`, `duration`, `currency`) and `Helpers/DisplayFormat.cs` (`Time()` renders a `<time>`, plus `Iso`, `Plural`, `Number`, `Duration`, `Currency`) are the one formatter; API in component-api § Formatting. Use `Format.plural` instead of "N item(s)". Server dates go in `data-utc` via `DisplayFormat.Iso`, never `ToString("o")`; `timezone.js` converts AJAX-inserted content too. About 100 existing `data-utc` sites are untouched: move them to `DisplayFormat.Time` as each screen is reworked. Each relative time is a Tab stop; use it sparingly in long tables.
- **Date presets.** `DateRangeFilter.presetRange` / `detectPreset` / `applyPreset` are the only preset helpers (local dates). Six copies still use `toISOString()` (UTC, wrong in the evening): Admin Notifications and `RatWatchAnalytics` (Phase 12), `shared/filter-panel.js`, Analytics Engagement and Moderation, RatWatch Incidents (Phase 14).
- **Live status.** Hub states are `connecting | connected | reconnecting | disconnected`; a failed first attempt is `reconnecting`; 401/403 is terminal (`reason: 'auth'`). Retry is unlimited, so **pages that join hub groups must rejoin on the `reconnected` event** (Phases 5 and 13). Put `[data-stale-badge][hidden]` beside anything labelled "Live". The global `_ConnectionBanner` reports the hub; the sidebar footer reports the bot (in offline mode the hub is up and the bot is offline).
- **Chrome.** The closed mobile drawer is `inert`; Escape only acts when something is open; `<main tabindex="-1">` takes focus from the skip link; the user menu is a disclosure. The notification dropdown still has `role=menu` (Phase 12). Focus after AJAX swaps is per screen.
- **Phase 5:** `dashboard-realtime.js` still carries leftover connection handling; drop it.

### Phase 5 — Dashboard and realtime

- **Files:** `Pages/Index.cshtml(.cs)`, `wwwroot/js/dashboard-realtime.js`, `Services/DashboardUpdateService.cs`, `Core/DTOs/DashboardStatsDto.cs`, `Pages/Shared/Components/_ConnectedServersWidget.cshtml`, `wwwroot/js/quick-actions.js`.
- **Changes:** align the stats DTO with JS and broadcast it; accurate metric labels; Restart modal resets and shows "restarting → back online"; Sync All confirms and refreshes widgets; hide role-gated cards and collapse the grid (D8); card layout for the servers widget on mobile; relative times auto-refresh.
- **Acceptance:** live events render; metrics change after a command; a viewer sees no dead actions or holes.
- **Verify:** 320, 768, 1440 as admin and as viewer; hub offline then recovering.

**Done (Phase 5).** Live stats now broadcast and render; a Viewer sees no dead actions or holes; Restart and Sync All have pending, confirm and result states. Checked at 320/768/1440 as admin and Viewer in both themes, including the hub going down and recovering. Notes for later phases:

- `StatsUpdated` carries `DashboardStatsDto` camelCased (`totalServers`, `totalMembers`, `commandsLast24Hours`, `uptimePercent24Hours`, `timestamp`). `DashboardStatsProvider.Build` is the one definition; call `IDashboardStatsBroadcaster.NotifyChanged()` after anything that changes them (commands, guild join/leave and Sync All already do; it coalesces). A test pins the DTO names to `dashboard-stats.js`.
- Hero cards are Servers, Members, Commands (24h), Uptime (24h) — the old "Commands Today" was a rolling 24 h window (E-2).
- Role-gated cards are left out of the page model rather than hidden inside partials (D8); `_ConnectedServersWidget` has no role check of its own. A Viewer's grid collapses.
- Connected Servers switches between card and grid with a container query (40 rem), so it works in a half-width column. Raw IDs are behind a copy button (C-1).
- Dashboard script split: `dashboard-realtime.js` (feed, stats on `reconnected`), `dashboard-actions.js` (confirm follow-ups, server rows), `dashboard-stats.js` (field mapping). Connection state belongs to the layout banner. `BotStatus.watchRestart()` drives the "restarting → back online" banner. Offline-mode restart answers 409 with plain text.
- `quickActions.submitQuickAction` uses `ApiClient.requestRaw` with `redirect: 'manual'`; nothing on the dashboard calls it any more.
- `ApiClient` does not fetch HTML: refresh server-rendered regions by returning JSON and drawing from a `<template>`.
- Open: missed feed events are not backfilled after a reconnect; `navigation.js` still has the unused `toggleServerActionMenu` / `copyServerId` (Phase 16).

### Phase 6 — Commands, Search, CommandLogs

- **Files:** `Pages/Commands/*`, `wwwroot/js/command-*.js`, `wwwroot/js/tab-panel.js`, `Pages/Search.cshtml(.cs)`, `wwwroot/js/search.js`, `Pages/CommandLogs/Details.cshtml`, `Controllers/CommandsApiController.cs`.
- **Changes:** tab loader with delayed skeleton, abort, Retry that keeps filters and shows the server message; one submit path with correct field names; filters, page and tab in the query string; reset page on filter change; log modal doesn't hijack the tab hash and deep-links work; command-list search; date-range validation; Search renders its validation message and the true total; "View all" lands on the right tab with filters; role-aware results; Details back link keeps the user's place.
- **Acceptance:** refreshing any Commands URL restores the view; one request per Apply; a 90-day error shows inline.
- **Verify:** network panel on Apply; loading, empty, error and populated states at 320 and 1440.

**Done (Phase 6).** All listed changes landed; one request per Apply (network log); refreshing any Commands URL restores the view; a 90-day range shows inline; loading, empty, error and populated states checked at 1440 and 320, no horizontal overflow from 320 to 2560. Notes for later phases:

- **Commands.** `commands-page.js` replaces `command-tab-loader.js`, `command-filters.js`, `command-pagination.js` and `url-state.js`. The URL is the state (`tab`, `StartDate`, `EndDate`, `GuildId`, `CommandName`, `StatusFilter`, `SearchTerm`, `pageNumber`, `q`, `log`); Back and refresh restore it. Tab loads abort the previous request, show a skeleton after 300 ms, and offer Retry that keeps the filters and shows the server's message. The log modal is a `quickActions` dialog with a `?log=` deep link and no longer touches the hash. Command List has a debounced search (P-3). Zero commands shows one empty state; success rate reads Healthy / Needs attention / Failing; charts use `ChartTheme.colors()` (S-9).
- **`tab-panel.js`** ignores hashes that are not its own tab ids, so the skip link's `#main-content` no longer blanks every panel. Nav-tabs' focus ring was there but faded in through `transition: all`; it now shows at once.
- **`ApiClient.getHtml`** (and `responseType: 'html'`) fetches partial HTML; use it for tab and partial loads.
- **Search.** Validation renders; the count is the true total (C-6); "View all" lands on the right tab with filters (`q`); Viewers get the log dialog instead of a moderator-only page (N-4); the command-log search result URL was a 404 and is fixed. CommandLogs/Details takes a sanitised `?returnUrl=` for Back (N-3).
- **Found:** `CommandAnalyticsService` ran four queries in parallel over one scoped DbContext, so the Analytics tab failed intermittently; they now run in sequence. The service uses only the start date for some metrics and top commands ignore the server filter; the cards say so, the service is unchanged.
- **Open:** orphaned `command-analytics.js`, `command-error-handler.js`, `command-loading-states.js`, `command-stats-chart.js` (Phase 16); the mobile search overlay lacks dialog semantics.

### Phase 7 — Guild moderation screens

- **Files:** `Pages/Guilds/Members/*`, `Pages/Guilds/FlaggedEvents/*`, `Pages/Guilds/ModerationSettings/*`, `wwwroot/js/member-directory.js`, `user-moderation-profile.js`, `moderation-settings.js`.
- **Changes:** one checkbox set per row; bulk actions report partial failures; real export and "Never messaged" filter; tabs on `tab-panel.js`; moderation profile on `ApiClient` with pending states and no reloads; keyboard tag chips; FlaggedEvents success toasts, actions for acknowledged events, user-history links; ModerationSettings confirms presets, per-tab dirty tracking, a real `<form>` with validation, sends only edited fields, wires Import Templates (D11); card layouts under `md` (D13).
- **Acceptance:** select-all on 25 rows reads 25; preset change asks first; unsaved warning fires once and correctly.
- **Verify:** keyboard-only ModerationSettings at 1440; FlaggedEvents and Members at 320.

**Done (Phase 7).** All listed changes landed; select-all on 25 Members rows reads 25 at 1440 and at 320 with touch; presets ask first; the unsaved-changes warning fires once and only when dirty; Members and FlaggedEvents have card layouts at 320. Notes for later phases:

- **Selection.** `bulk-selection.js` (`BulkSelection`: `data-select-item`, `data-select-all`, `data-bulk-toolbar`) keys by row id and mirrors table and card checkboxes (B-15). Use it for any page that renders both.
- **Members.** Real CSV export (current filters or the selection, UTC headers) and a real "Never messaged" filter (`NeverActive`) (B-26). Filter-aware empty states; the forced 30-day window on FlaggedEvents is gone (S-8). The profile's join date is the stored value, else Discord's, else the snowflake's date (E-2).
- **FlaggedEvents.** `_Pagination` with `pageNumber`. Review actions are confirmed form posts with TempData toasts; partial failures are counted; the reviewer comes from `User.GetDiscordUserId()` (the old claim did not exist). `FlaggedEventReviewRules` and `FlaggedEventBatchOutcome` hold the rules and messages. Acknowledged events can still be dismissed or given an outcome.
- **ModerationSettings.** Tabs on `_TabPanel` with the tab in the hash (A-4); one `data-unsaved-changes` form per tab with a dirty dot; saves send only changed fields as patches (`*PatchDto`, 400 with per-field `errors`); presets confirm; Import Templates is wired (D11).
- **Tags.** Chips have real remove buttons (A-7). `ModTagStyle` maps category to class, label and colour (Positive 0, Negative 1, Neutral 2).
- **Bugs fixed along the way:** two controllers owned `users/{id}/tags/{name}`, so every profile tag add/remove was a 500 (`RouteAmbiguityTests` now fails on any duplicate route and verb); the tag colour select sent the wrong categories; a content save wiped the link allow-list settings; bulk-updating events through one context threw; "False Positives" was always 0.
- A form that sets an explicit `action` needs `asp-antiforgery="true"` or the post is a 400.
- **Open:** `FlaggedEventsController` still takes `ReviewerId` in the body (derive it from claims); `tab-panel.js` hides every panel when the hash isn't a tab id (Phase 6); light theme was checked on the profile, Details and Settings, not on Members or FlaggedEvents.

### Phase 8 — Guild configuration and scheduling

- **Files:** `Pages/Guilds/Index`, `Details`, `Edit`, `Welcome`, `AssistantSettings`, `AssistantMetrics`, `Reminders/*`, `ScheduledMessages/*`, `FeatureRequests/*`, `PublicLeaderboard`.
- **Changes:** failed POSTs repopulate the layout via `PopulateGuildLayout`; fix ScheduledMessages time shift and reachable delete; card layouts under `md` for Reminders and ScheduledMessages; radios via the Phase 3 partial; shared confirms; previews escape HTML and render Discord markdown consistently; real disabled sections (`inert` plus explanation); show a deleted saved channel; AssistantMetrics "not configured" and "no data yet"; FeatureRequests confirm-with-reason on reject, resolved usernames, doc-gen retry; PublicLeaderboard real error state; unsaved-changes on every form.
- **Acceptance:** each form survives a failed submit with input and chrome intact; every row action reachable by touch and keyboard.
- **Verify:** at 320, submit each form invalid and screenshot.

**Done (Phase 8).** All listed changes landed; every form survives a failed submit at 320 (touch, both themes) with input and chrome intact and the first error focused; row actions work by touch and keyboard; `RouteSmokeTests.KnownFailures` is empty (the mechanism stays). Notes for later phases:

- **ScheduledMessages (B-21).** Create and Edit share `_MessageEditor`. The next-run field converts from UTC only on Edit's first render, so a failed POST no longer shifts the time; Delete is a confirmed form beside Save.
- **AssistantMetrics (B-29).** Reads `IAssistantTelemetryReader`, registered without an API key; shows "Assistant not configured", dashes for no data (S-9) and a retry state (S-5).
- **Failed POSTs (F-5).** Welcome, AssistantSettings, ScheduledMessages, FeatureRequests and AssistantMetrics are on `GuildPageModelBase`; every failed POST goes through `PopulateGuildLayout` and takes `guildId` from the route. `_FormSelect` sets `aria-invalid` and links its error.
- **Forms (F-6, F-7).** `data-unsaved-changes`, `data-submit-guard` and dirty-on-load on all five forms; a `<form>` writes `data-unsaved-dirty-on-load="true|false"` (`unsaved-changes.js` treats `"false"` as clean). Schedule type is `_RadioCardGroup`; toggles are `_FormToggle`.
- **Lists (R-1, D13).** Reminders, ScheduledMessages and FeatureRequests: table from `md`, cards below, `.row-action-btn` (44px on touch), confirmed cancel/delete forms (`confirm-forms.js`), `_Pagination` with `pageNumber`. Reminders resolves names with one lookup per page.
- **Helpers.** `discord-markdown.js` (escape first, then Discord markdown; Welcome and ScheduledMessages previews), `section-gate.js` (a switched-off section is `inert` with its reason beside it), `form-errors.js` (focus the first invalid field), `FormFieldState`, `UserDisplay` (maps `Unknown#id` to a readable name). API in component-api § Settings Form Helpers. `form-errors.js` overlaps Phase 11's `form-focus.js`; keep one (Phase 16).
- **Welcome / channels (E-4).** A saved channel the bot can't see stays selected with a warning.
- **FeatureRequests.** Reject needs a reason (dialog and server); names instead of IDs (C-1); "Queue documentation again" (`IFeatureRequestService.RequeueDocGenAsync`); no raw error text (C-7).
- **PublicLeaderboard.** In-page 404, 403 and 503 states; load failure offers Try again (S-5); relative times; the animation respects reduced motion; no "real-time" claim.
- **Guilds/Index and Details.** Per-row Sync updates the row in place (the page had no antiforgery token, so Sync never worked); Sync all is a confirmed form; plurals. Details widgets are `_DashboardWidget` with body partials (no HTML built in C#); `GuildDetailsAggregator` loads each section separately, so one failure shows a retry widget. Retry still reloads the page.
- **Found (data loss):** if audio settings failed to load, Guilds/Edit saved defaults over the real values; it now saves audio only when they loaded (`AudioSettingsLoaded`) and warns otherwise.

### Phase 9 — Member portal, mobile first (D10, D13, D15)

- **Files:** `Pages/Portal/*`, `Pages/Shared/_PortalLayout.cshtml`, `Pages/Shared/Components/_VoiceChannelPanel.cshtml`, `_VoiceSelector.cshtml`, `_PresetBar.cshtml`, `wwwroot/js/voice-channel-panel.js`, `portal-*.js`, `voice-selector.js`, `Controllers/Portal*`, `Authorization/PortalGuildMemberAuthorizationHandler.cs`, a development-only sign-in path.
- **Changes:** development/offline-only seeded member sign-in (D15) first; voice panel reflects real bot voice state and calls portal endpoints (D10); `EnableMemberPortal` enforced with a friendly "portal disabled" page; sticky voice bar above content on mobile and a live mobile chip; play pending and queued feedback; lazy-load observer rooted on the grid; delete visible on touch via shared confirm; limits and counts from the server; TTS guarded Enter, `dir="auto"`, unclipped voice dropdown, wired Preview (D11); VOX no auto-focus on coarse pointers, keyboard replay, 44px history buttons; safe-area padding.
- **Acceptance:** at 375 with touch, a member can join, play, queue and delete with no keyboard pop-up or hidden controls; a failed TTS send keeps the text; disabling the portal blocks members.
- **Verify:** sign in as the seeded member; screenshots at 320, 375, 768.

**Done (Phase 9).** All listed changes landed; signed in as the seeded member at 320, 375 and 768 with touch (and 1440), both themes, on all three portal pages: no console or HTTP errors, no horizontal overflow, no tap target under 44px. Audio cannot play offline, so success and queued states were checked against mocked endpoint answers. Notes for later phases:

- **Development portal (D15).** `DevelopmentPortal.IsEnabled` requires both the Development environment and `Discord:OfflineMode`; the registration and the seeder each check it. The seeder links the default admin to the seeded guilds and creates a role-less `portal-member@example.com` with the default admin's password. `OfflineAppHost.CreatePortalMemberClientAsync` gives tests that member. `RouteSmokeTests.KnownFailures` now holds only B-29. The offline directory treats the two fake users as members of every guild in the database.
- **Seam.** Portal pages, the authorization handler and the portal playback controllers use `IPortalGuildDirectory`, not `DiscordSocketClient`.
- **Member portal switch (#947).** `EnableMemberPortal` is enforced on every `PortalGuildMember` route (pages and API, including `UserPreferencesController`), independently of `AudioEnabled` and before the admin bypass. Pages render `_PortalDisabled` (200); the API answers 403 "Portal disabled". A guild with only audio off opens the portal with a notice.
- **Voice panel (D10, B-18, B-19).** `VoiceChannelPanelViewModel.ApiBase` selects the portal endpoints; "Connected" means the bot is in voice. `window.VoiceChannelPanel` exposes `refresh`, `notePlaying`, `reveal`, `getState` and a `voicepanel:change` event. Members cannot use the dashboard hub (it requires the Viewer role), so portal pages leave the hub scripts out for them and poll `/status` every 5 s. Soundboard `status` returns `memberCount` and `queueLength`; `play` returns `wasQueued` and `queuePosition`.
- **Mobile (R-7, R-4, R-5).** Under 1024px the sidebar is `display: contents`, so the voice bar comes first and sticks; touch targets are 44px; safe-area insets and `dvh` apply. `_PortalLayout` carries `quick-actions.js`; native dialogs are gone (I-3).
- **Screens.** Soundboard: lazy-load rooted on the grid (P-2), blocked storage handled (E-6), play pending and queued feedback, delete visible on touch. TTS: server limits, `dir="auto"`, IME-safe Enter, preset name dialog, custom presets load again (wrong container id). VOX: no keyboard pop-up on tile tap, keyboard replay, 44px history buttons. Portal 400s no longer return exception text (C-7); copy says "server".
- **Open:** a hub policy that admits guild members (with a membership check in `JoinGuildAudioGroup`) would replace polling; the panel's Stop and "playing" state come from the soundboard `PlaybackService`, so TTS and VOX playback may not show; `_ModeSwitcher` (also on the admin TTS page) now guards localStorage.

### Phase 10 — Audio admin

- **Files:** `Pages/Guilds/Soundboard/*`, `TextToSpeech/*`, `VOX/*`, `AudioSettings/*`, `AudioModerationLog/*`, `wwwroot/js/tts-page.js`, `ajax-sort.js`, `Pages/Shared/Components/_SsmlPreview.cshtml`.
- **Changes:** toasts and confirms instead of native dialogs; sort keeps dropdowns; upload with client checks, progress, and correct single/multi copy; admin now-playing and Stop; TTS Send uses on-screen settings (or labels them defaults) and posts SSML; remove TTS Replay, Avg Latency and VOX Play (D11); AudioSettings single Save with dirty state, validated numbers, keyboard role picker, responsive rows; validate the AudioModerationLog user filter.
- **Acceptance:** no native dialogs; every control has success, failure and pending states.
- **Verify:** keyboard through AudioSettings; 320 screenshots.

**Done (Phase 10).** All listed changes landed; no native dialogs remain on the five audio admin pages; checked at 320 and 1440 in both themes, with a keyboard run through Audio Settings and mocked endpoints for the send, preview, upload and sort failure states. Real playback cannot run offline: the now-playing section and Stop render, playback itself was not exercised. Notes for later phases:

- **Admin pages must not call `/api/portal/*`.** Those endpoints refuse everyone, administrators included, while `EnableMemberPortal` is off. Soundboard categories and TTS Preview are admin page handlers now; `build-ssml` and `validate-ssml` are `[AllowAnonymous]` and safe to reuse. Phase 9's `_PresetBar` custom-preset Save still posts to the portal endpoint on the admin TTS page and fails while the portal is off (open).
- **Soundboard.** Categories render on the server, so a sort keeps them (B-28); `ajax-sort.js` keeps the old list on failure with a Retry toast and restores the sort on Back/Forward (`AjaxSort.configure`, `ajaxsort:loaded`; `_SortDropdown.setSelected`). Upload takes several files, checks type, size and free slots first, and shows per-file progress (`XMLHttpRequest`, because `ApiClient` has no progress events). Now-playing and Stop show for admins. Category assignment never saved (the old category was written back); fixed here and in `PortalSoundboardCategoriesController`.
- **TTS.** Send and Preview use the on-screen voice settings and post SSML in Pro mode (B-22); the save button is "Save as server defaults". Replay, Avg Latency and VOX Play are removed (D11, B-20). Upstream failures answer 503 with plain text (C-7). Enter sends, IME-safe.
- **Audio Settings.** One form, one sticky Save, field-keyed `errors` in a 400, `UnsavedChanges.markClean` after save or reset; whole numbers or a field error (F-3); role pickers are a native `<details>` with checkboxes (A-7); rows stack under `sm`.
- **Audio Log** refuses an unreadable user or reversed dates instead of listing everything, and accepts a pasted `<@id>`.
- `LoadingManager` is a top-level `const`, not on `window`.
- **Open:** `SoundsController` download fails with a relative `SoundboardOptions.BasePath` (`PhysicalFileResult` needs an absolute path); `ajax-sort.js` still uses `fetch()` (move to `ApiClient.getHtml` from Phase 6).

### Phase 11 — Admin users, settings, purge

- **Files:** `Pages/Admin/Users/*`, `Pages/Admin/Settings.cshtml(.cs)`, `wwwroot/js/settings.js`, `wwwroot/js/llm-models.js`, `Pages/Admin/BulkPurge.cshtml(.cs)`, `Pages/Admin/UserPurge.cshtml(.cs)`, `Pages/Shared/Components/_RestartBanner.cshtml`.
- **Changes:** Users: `new-password`, role kept after failed submit, field-level identity errors, filter-aware empty state, delete/lock UI where allowed, copyable one-time reset password. Settings: `?category=` in URL, per-tab Save of only that tab, correct Save All and dirty tracking, labelled tab ids and arrow keys, `_Alert` reuse, honest "no changes", visible status-poll failure, no 320 overflow. Purge: PRG, progress, keyboard radios, friendly names, username in the confirm.
- **Acceptance:** F5 after purge doesn't re-run; Settings reload keeps the tab.
- **Verify:** keyboard-only Create User; Settings at 320.

**Done (Phase 11).** All listed changes landed; F5 after either purge sends nothing; Settings reload keeps the tab and arrow/Home keys switch tabs; keyboard-only Create User at 1440; Settings at 320 (all tabs) has no horizontal overflow; both themes. Restart and Shutdown were exercised against a stubbed status endpoint, not real posts. Notes for later phases:

- **Purge (I-1, F-7, C-2).** BulkPurge preview is a GET (`?Preview=true&EntityType=…`); both purges POST then redirect, and the result shows once from TempData. The typed confirm (`purge.js`) submits with `requestSubmit()` so `data-submit-guard` still applies; a progress panel follows the `BulkPurgeProgress` hub event. Radios are `_RadioCardGroup`. `Helpers/PurgeDisplay.cs` gives plain names (a seed for Phase 16's enum display names). UserPurge shows the Discord username and still asks for the user ID.
- **Users.** `new-password`; role and welcome choice survive a failed submit; identity errors land on their field (`FieldForError`); `form-focus.js` moves focus to the first error (shared, documented in component-api); filter-aware empty state; "Locked out" badge; Disable/Enable with a confirm. The user service has no delete or unlock, so neither is offered (follow-up for the identity stream). Users/Edit has a breadcrumb and a Copy button for the one-time password.
- **Settings.** `_TabPanel` with `?category=` (`SettingsModel.ResolveTab`); one `<form data-settings-form data-settings-handler data-unsaved-changes>` per tab, saved by `settings.js`; Save sends only that tab and the server scopes keys to the category; Save all saves the dirty tabs; the save JSON carries `changeCount`, so an unchanged save says "Nothing changed" (C-6). The restart banner appears in place (`_RestartBanner` is `@model bool`, always rendered); restart follows `BotStatus.watchRestart`; a failed status poll is visible with "Try now". Resets redirect with a toast.
- **Toggles.** Settings command modules and the LLM model catalog use the `.toggle` partial markup; only Privacy (Phase 15) still uses the legacy `.form-toggle*` CSS.
- **Open:** `OnPostSaveAllAsync` / `SaveAllAsync` are unused by the UI and still save unscoped (remove in Phase 16).

### Phase 12 — Logs, notifications, LLM usage, currency (D14)

- **Files:** `Pages/Admin/Logs/*`, `AuditLogs/*`, `MessageLogs/*`, `Notifications/*`, `LlmUsage.cshtml(.cs)`, `Currency/*`, `Pages/Guilds/Currency/*`, `wwwroot/js/notification-history.js`, `llm-usage.js`, `wwwroot/js/currency/*`, alert threshold configuration.
- **Changes:** Logs tabs load on demand; errors rendered; Details keep `returnUrl`; long content wraps with an expander; neutral boolean colours; streamed or capped export with UTC headers (D6). Notifications: in-place updates, "Last 7 days" chip, confirm only destructive actions, Delete All respects filters, labelled checkboxes, no overflow at 320 or 1440; suppress offline disconnect alert and fix the memory threshold. LlmUsage: keyboard rows, drill-down loading/error, sequenced requests, local dates. Currency: user pickers instead of raw IDs, confirm on revoke, no reload, shared modals, scroll to ledger, integer validation, Mint disabled until a currency is chosen.
- **Verify:** empty, filtered-empty, error and populated states for each list at 320 and 1440.

**Done (Phase 12).** All listed changes landed; empty, filtered-empty, error and populated states checked for each list at 320 and 1440 in both themes, with keyboard runs through the bell, LLM rows and dialogs. Notes for later phases:

- **Logs.** Logs tabs are page navigation (`?tab=`) and only the active tab reads its data (B-10); a failed read shows an error with Retry, not an empty table (S-5). The "Application" tab is gone (D11). Details pages keep `returnUrl`, so Back lands on the right tab with its filters (N-3). Long messages clamp with "Show more" (R-6). The audit export streams in pages and is capped at 10,000 rows (file name says `-first-10000` when cut); time columns say UTC (P-4, D6). The old audit export ignored its filters (wrong parameter names) and `message-logs.js` never ran its channel clearing; both fixed.
- **Notifications.** Actions update in place (I-8). "Last 7 days" is a clearable chip (`?AllTime=true`). Mark all read no longer asks; Delete all is a typed confirm and deletes only what the filters show (`INotificationWriter.DeleteMatchingAsync`). The bell is a disclosure with a labelled region, not a `role=menu` (A-8).
- **Alerts (D14, S-10).** The `bot_disconnected` alert is skipped in offline mode; alert titles use display names; memory thresholds default to 1024/1536 MB (data migration `RaiseMemoryAlertDefaults`, only rows still at the old defaults). The notification default window is UTC days while presets are local days, so they can disagree by a day in the evening.
- **LlmUsage.** Rows are buttons (A-7); the drill-down has loading, empty and error states, aborts older requests and drops stale responses; dates are the viewer's local days (`UserTimezone`).
- **Currency.** All modals are `quickActions` dialogs; create, edit and deactivate patch the page from `_CurrencyCard` / `_CurrencyRow` and a `<template>`; members are picked by name, roles from a select (C-1); revoke confirms; Mint is disabled until a currency is chosen (B-27); amounts are whole numbers. Offline, `IDiscordUserResolver` returns `Unknown#…`, so holders show "Unknown user" (follow-up: fall back to the `Users` table).
- **Found:** `LedgerRepository` transactions failed under Npgsql's retry strategy (mint, fine and adjust answered 500 on PostgreSQL); fixed separately on this branch.
- `AutocompleteManager` is a bare global, not on `window`.

### Phase 13 — Performance consolidation (D12)

- **Files:** `Pages/Admin/Performance/*`, `wwwroot/js/performance/*`, `performance-tabs.js`, `performance-shell.js`, `api-metrics-chart.js`, `Services/Performance/AlertIncidentManager.cs`.
- **Changes:** redirect the 5 standalone pages to `?tab=`; update notification links; delete orphaned JS; "Live" only on hub-subscribed tabs, "Updated X min ago" elsewhere; remove the fake chart and hard-coded trends; clamp `hours`; validate thresholds (warning < critical) and keep edits across tab switches; charts via `chart-theme.js` with text alternatives and empty states; `aria-live` only on a status line; fix KPI word-break.
- **Verify:** both themes; hub disconnect; fresh-install empty state.

**Done (Phase 13).** All listed changes landed; all six tabs in both themes at 320 and 1440 with no horizontal scroll and no console errors; killing the app shows Paused and Stale, then Live again within seconds with pushes resumed (the hub group is rejoined); fresh-install empty states and seeded data both render. Notes for later phases:

- **One UI (D12).** The five standalone pages are redirect stubs (`PerformanceTabRedirectModel`, 302 to `/Admin/Performance?tab=…` with a clamped `hours`). Build links with `PerformanceDashboardTabs.TabUrl`, never the old routes; alert notifications and search use `?tab=`. Tab and range live in the query string; an old `#hash` still opens the right tab. Deleted: `performance-tabs.js` (a duplicate of `dashboard.js`), `performance-shell.js`/`.css`, `api-metrics-chart.js`, the four `*-realtime.js` files, `timestamp-utils.js` and two orphan tab partials. `dashboard.js` stays as the content loader; `tab-panel.js` drives the tablist.
- **Live status (S-4).** "Live" only on tabs that declare `live = { group, events, snapshot }` in `tabs/*.js` (overview, health, system, alerts); `live.js` joins one hub group per tab, leaves it on switch and rejoins on `connected`/`reconnected`. Other tabs show "Updated X ago"; the tab cache shows its age and has Refresh; the chip reads "Paused" with `[data-stale-badge]` while the hub is down.
- **No invented data (S-9).** The response-time and error-rate "trends" were flat copies of one value (there is no per-bucket series); they are now per-command comparisons. Hard-coded trend arrows, "Normal" badges, dead Quick Actions and the "Commands Today" label are gone; `LoadFailed` gives an error state instead of zeros (S-5).
- **Validation.** `hours` is clamped in the handler and the aggregator (E-5); alert thresholds are validated in the browser and in `PerformanceAlertService.ValidateThresholds` (warning < critical, message on the field, F-3); edits survive tab switches and warn on leave; a missing config is a 404.
- **Accessibility.** The only live region is an sr-only status line (A-5); every chart has a summary name and a hidden data table (`ChartUtils.describeChart`), gauges are labelled, charts have empty and error states (A-6); KPI values no longer split mid-word (R-6).
- **Charts.** `ChartUtils` gained `createChart`, `describeChart`, `describeGauge`, `showChartEmpty`, `showChartError(id, msg, onRetry)`, `clearChartState`, `updateGauge` and `themeColors` on datasets. `chart-theme.js` now redraws with `update('resize')`, so single-colour datasets recolour on a theme switch.
- **Open:** `PerformanceTabsController` (`api/performance/tabs`) is unused and its partial paths would not resolve (remove in Phase 16); `CommandPerformanceViewModel.*Trend` members are unused; Commands tables still show raw IDs and "Guild" headers (Phase 16); a fresh install shows 0.0% uptime because no-history and zero look the same.

### Phase 14 — Analytics and charts

- **Files:** `Pages/Guilds/Analytics/*`, `Pages/Guilds/RatWatch/*`, `Pages/Admin/RatWatchAnalytics.cshtml`, `wwwroot/js/*analytics*.js`, `wwwroot/js/rat-watch-analytics.js`, `filter-panel.js`.
- **Changes:** use the token-based analytics modules or delete them in favour of `chart-theme.js`; one date-filter partial on `date-range-filter.js`; funnel works at 320; text labels for escalation letters and vote tallies; data-table alternatives; RatWatch valid active-tab style, touch-visible actions, timezone select keeps unknown values, CSV exports all rows, enum display names.
- **Verify:** Purple Dusk screenshots; 320; one chart through a screen reader.

**Done (Phase 14).** All listed changes landed; all seven chart pages screenshotted in Purple Dusk and Graphite at 1440, light at 320 and 768 with no horizontal scroll; one chart's text alternative checked in the accessibility tree (an `img` plus a named table). Notes for later phases:

- **Charts (V-5).** Server, moderation, engagement and Rat Watch analytics are on `ChartTheme.colors()` through `analytics-charts.js` (`create`, one recolour listener, token-class heatmap, local-day labels). The unused token modules and `command-analytics.js` are gone; all inline Analytics scripts are gone. Pages that set `Chart.defaults.color` undid chart-theme on Purple Dusk; removed. A bar dataset with a single colour string did not recolour on a theme switch (`update('none')` keeps resolved options); use per-bar arrays (`AnalyticsCharts.each`) or rely on the chart-theme fix from Phase 13.
- **Text alternatives (A-6).** `_ChartDataTable` is a visually hidden table beside every chart and heatmap, linked by `aria-describedby`. Escalation steps name their action, vote tallies read "N guilty / N not guilty", Rat Watch status is `_RatWatchStatusBadge` (dot plus words). A `<table class="sr-only">` can widen the page: wrap it in a div, and make an `overflow-x-auto` scroller `relative`.
- **One date filter.** `_DateRangeFilter` (+ `DateRangeFilterViewModel`) on `DateRangeFilter.presetRange` (local dates) is the only date-range filter: Analytics (three pages), Rat Watch Analytics and Incidents, Admin RatWatchAnalytics. `filter-panel.js` grows to content height and is `inert` when collapsed.
- **Errors (S-5).** All five analytics pages show an error with "Try again" instead of zeros.
- **Rat Watch.** `tab-panel.css` was never loaded on its pages (no active-tab style); leaderboards use `_TabPanel`; confirms through `quickActions.confirm`; an unknown stored time zone stays selected (E-4); `_Pagination` on `pageNumber`; the Incidents modal and raw `fetch` moved to `quickActions.openDialog` + `ApiClient`; CSV exports every filtered row (10,000 cap, formula guard, UTC header) (P-4); `status.DisplayName()` (`EnumDisplayExtensions`) is the one status text (C-2); cards under `md`.
- **Engagement.** Placeholder cards removed (D11); the funnel is text over proportional bars and works at 320.
- **Open (pre-existing data):** the Engagement funnel is zeros from the service; Rat Watch Index's Votes column is "--" because `GetByGuildAsync` doesn't fill vote counts.

### Phase 15 — Account, landing, error copy

- **Files:** `Pages/Account/*`, `Pages/Landing.cshtml`, `Pages/Shared/_LayoutLanding.cshtml`, `wwwroot/js/login.js`.
- **Changes:** Login pending state, focus on error, `autocomplete="username"`, an h1; LinkDiscord pending states, code-length copy, expiry countdown; Lockout duration from config; Privacy real download button, labelled toggles, no scroll loss; Profile plurals and local time; Landing mobile login, no placeholder boxes, real or no badges, current feature list, `<main>`, meta description, theme.
- **Verify:** Landing at 320, 768, 1440, 2560; Login keyboard-only.

**Done (Phase 15).** All listed changes landed; Landing at 320, 768, 1440 and 2560 in both themes (no horizontal scroll, one h1, Sign in and the toggle visible); Login keyboard-only, with one POST on a double click; LinkDiscord pending, expiry and wrong-length states at 375; Privacy and Profile at 320. Notes for later phases:

- **Landing.** `_LayoutLanding` has `theme-root`, `_ThemeHead`, `theme.js`, `viewport-fit`, a meta description and one title; Sign in and the theme toggle show at every width (R-8); `<main>` and a skip link; placeholder screenshots, "Coming soon" and the fake Stars/Forks/MIT badges are gone (D11) in favour of real Source, Issues and Releases links; tokens only; no hover lift on static cards.
- **Login.** One h1, `autocomplete="username"`, server-set `aria-invalid`, focus to the first error (`form-focus.js`); errors shown once. The error alert used to fade out after 0.3 s and stay invisible (animation lacked `forwards`).
- **LinkDiscord.** Code length from `Verification:CodeLength`; a wrong-length code doesn't use up an attempt; an expiry countdown with "Start again"; Copy ID buttons instead of raw IDs (C-1).
- **Lockout / AccessDenied / ExternalLogin.** Lockout duration from `IdentityOptions`; no "403 Forbidden"; Sign out is a POST; "Sign in"/"Sign out" (D7); `<main>` everywhere.
- **Privacy.** Consent switches are `_FormToggle`; after a change the page returns to that row; the export is a Download button; delete uses `ApiClient` with a pending state; dates through `DisplayFormat.Time`. The legacy `.form-toggle*` CSS is deleted (Privacy was its last user).
- **Profile.** Theme choice starts with "Match my system", which clears the saved choice and cookie; plain-word source; local dates.
- **Open:** data exports are written to `wwwroot/exports/{discordId}/{guid}.zip` and served by static files without authentication (only the GUID protects them); "server" wording outside the touched pages (Phase 16); `design-system.md`'s old toggle section (Phase 16).

### Review fixes (Phases 3–15)

Each group of phases had an independent review before the next began. Fixes that landed beyond the phases' own scope:

- **Ledger transactions** retry as one unit under Npgsql's `EnableRetryOnFailure` (mint, fine and adjust answered 500 on PostgreSQL); a retry no longer reports its own write as a duplicate. Tests run with the app's retry strategy.
- **Development portal (D15) seed** is skipped when the admin already has a real Discord link; with the portal off, startup clears the fake Discord IDs and locks the seeded member.
- **Notifications "Delete all"** deletes only notifications created before the page rendered (`before`, required).
- **FlaggedEvents** reviewer comes from the session; an admin without a Discord link is refused instead of being recorded as 0.
- **Authorization:** Guilds/Edit routes on `{guildId}` and `GuildAccessHandler` no longer falls back to `?guildId=` (an Admin could edit another guild); per-row guild Sync checks guild access; an Admin can no longer disable a SuperAdmin; non-SuperAdmins cannot save or reset the Appearance settings category; the public leaderboard no longer shows a guild's name when its board isn't public; currency mint grants refuse @everyone and managed roles.
- **Data exports** were written under `wwwroot/exports` and served as public static files (anyone with the URL could download a user's full personal-data archive, and it outlived "Delete my data"). They now live in `data/exports` outside the web root, are served only to their owner by `Privacy?handler=DownloadExport&id=…` (`no-store`), are deleted by the user purge, and expire through the new `UserDataExportCleanupService` (cleanup existed but was never called).
- **Rat Watch** Cancel and End vote check the watch belongs to the guild; settings reject an unknown time zone unless it is the stored value. Performance redirect stubs answer 302, not 301. Live tabs show "Paused" when a hub group join fails.
- **Smaller:** Logs page-size selector works and is clamped; ScheduledMessages refuses a time inside a daylight-saving gap; Commands analytics honour the end date; one auto-leave limit (60 min) for Guilds/Edit and Audio Settings; Pro-mode SSML enforces the configured limits; `form-errors.js` merged into `form-focus.js` (`FormFocus`, `data-focus-first-error`).

### Phase 16 — Long-tail sweep

- **Changes:** pluralisation (~30 sites); raw IDs replaced by names with the ID copyable; enum display names via a C# helper; "server" in copy (D7); remove `console.log`s, dead partials, `Pages/Index.cshtml.cs.bak` / `.temp`, unused scripts; update `docs/articles/design-system.md`, `docs/architecture/ui-inventory.md`, `.claude/agents/web-ui-portal.md`.
- **Acceptance:** grep counts for `(s)`, `[..2]`, `onclick='`, `alert(`, `console.log` reach zero or are justified in a comment.

**Done (Phase 16).** All listed changes landed. In `Pages` and `wwwroot/js` there are no `console.log`, native `alert`/`confirm`/`prompt` calls or inline `onclick='` handlers; the three `(s)` hits are JavaScript parameter names; seven slice patterns remain (GUID hex and ASCII ids), each with a comment. A Release build passes the full suite (5,651 .NET, 355 JS). Notes:

- **Enum names (C-2).** `value.DisplayName()` (`Core/Extensions/EnumDisplayExtensions`) is the one way to show an enum; add `[Display(Name = …)]` only when the words differ from the member name (a test fails on run-together names). `DisplayNameFor<T>(string)` is for DTOs that carry the name as a string. `RatWatchStatusDisplay` is gone; `PurgeDisplay` keeps only the user-purge count labels. Option values, API fields and exports keep the enum name or number.
- **Names (C-1).** `UserDisplay.Name` everywhere; `DiscordUserResolver` falls back to the stored username in `Users` before `Unknown#id`; the public leaderboard resolves through the same path in one batch. IDs moved to `title` or "(ID …)" text; a global copy-ID control does not exist yet.
- **Text.** `TextDisplay` (C#) and `Format.initials` / `Format.truncate` (JS) cut by grapheme (E-1). `SafeHtml.escape` is the one escaper (14 copies removed; `discord-markdown.js` keeps its own because it also runs under node). Plurals via `DisplayFormat.Plural` / `Format.plural` (C-3); "server" and "sign in" in copy, Performance and Logs columns included (D7, C-4).
- **Admin TTS presets.** `GuildTtsPresetsController` (`api/guilds/{guildId}/tts/presets/custom`, `RequireAdmin` + `GuildAccess`, antiforgery on writes) shares `CustomTtsPresetService` with the portal; `PresetBarViewModel.CustomPresetsUrl` points the bar at it, so admin presets work while the member portal is off. `ajax-sort.js` uses `ApiClient.getHtml`.
- **Dead code removed.** Ten unreferenced partials and their view models (the audit counted 13; Phase 13 had already removed some), three orphaned command scripts, the dashboard's unused Chart.js include, `Index.cshtml.cs.bak` and `.temp`, the `navigation.js` server-action menu and its CSS, `PerformanceTabsController`, `CommandPerformanceViewModel.*Trend` and `FormatTrend`, the `.metric-trend*` and `.card-enhanced` CSS, and the `sw.js` `/exports/` bypass (`CACHE_VERSION` v3). `_Card` and `_SkeletonCard` are rendered only on `/Components` and stay as documented primitives.
- **Docs.** `design-system.md` v2.1 rewrites the toggle, badge, alert and status sections and adds row actions, dialogs and toasts, empty/loading/error states, formatting and charts; the Buttons, Cards, Inputs and Typography code samples still carry old hex (`site.css` is the source of truth). `ui-inventory.md` routes match the `@page` directives; `feature-map.md` and `service-catalog.md` cover the services added in Phases 5–15; docs that named deleted scripts are fixed. When you add a component, add it to `ui-inventory.md` and the `/Components` showcase.
- **Open (for a later pass):** search results still show "User ID" / "Channel ID" subtitles; Logs CSV writes raw action and category names; moderation and Rat Watch analytics resolve names without the `Users` fallback; about 100 `data-utc` sites could move to `DisplayFormat.Time`; design-system code samples for buttons, cards, inputs and typography still carry old hex.

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
