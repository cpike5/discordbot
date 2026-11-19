# Slash Command Reduction: Follow-up Work

**Status:** Proposed
**Depends on:** PR #2000 (removal of portal-covered and low-value slash commands)

## Background

PR #2000 cut the bot's top-level slash commands from 51 to 25. The cut was made on three grounds:

- the command duplicated a portal page;
- the command was low value;
- for moderation, the command was not one of the actions themselves (ban, kick, mute, warn, purge, unban).

It left gaps and dead code that this spec closes. The work also finishes the reduction by merging near-duplicate commands, which takes the top level from 25 to about 18.

Each workstream below is one PR. They are independent unless noted.

| # | Workstream | Size | Priority |
|---|---|---|---|
| A | not-X portal settings page | Medium | **High.** not-X settings cannot be changed today |
| B | Remove the feature-request DM flow | Small | Medium. Dead code that is still registered |
| C | Portal parity gaps (schedule run-now, welcome test, watchlist page) | Small to medium | Medium |
| D | Merge near-duplicate commands | Medium | Low. Polish |
| E | Cleanup pass (unused services, docs audit) | Small | Low. Do last |

---

## A. not-X portal settings page

### Problem

`/notx` was the only way to edit `NotXGuildSettings`. Previews still run using each guild's stored row, and a guild that has no row cannot turn the feature on at all.

### Scope

A guild-scoped settings page at `/Guilds/NotX/{guildId}` that edits every field of `NotXGuildSettings`:

| Field | Control |
|---|---|
| `IsEnabled` | Toggle |
| `SensitiveOnly` | Toggle, labelled "Only preview sensitive tweets" |
| `HideSensitiveLabel` | Toggle |
| `OutputChannelId` | Channel select with a "Same channel as the link" option (null) |
| Monitored channels (`MonitoredChannelIdsJson`) | Multi-select of text channels. Leaving it empty means all channels |

### Design

- Copy `Pages/Guilds/Welcome.cshtml(.cs)`: it inherits `GuildPageModelBase` and uses a single-form POST (see `patterns.md`).
- Read and write through the existing `INotXService.GetOrCreateSettingsAsync` and `UpdateSettingsAsync`. No new service, entity or migration is needed.
- The channel lists come from the guild's text channels, the same way the Welcome page builds its channel select. Channel IDs are emitted as strings.
- Validate on the server that the output channel and every monitored channel belong to this guild. Drop unknown IDs rather than storing them.
- Write an audit log entry on save, using the same pattern the other guild settings pages follow.
- Add a "not-X" entry to `GuildNavigationConfig`, next to Welcome. Hide it when `NotX:Enabled` or `Features:NotXEnabled` is off, the way other feature-gated tabs are hidden.
- When the global switches are off, the page still renders but shows a banner saying previews are disabled bot-wide.
- Optional, only if the page needs it: a `NotXSettingsController` (`GET` and `PUT /api/guilds/{guildId}/notx`) on `ApiControllerBase`. Skip it if a plain form POST is enough.

### Acceptance criteria

- An admin can enable not-X, set the output channel, set the monitored channels and toggle both sensitivity flags, and the next tweet link in Discord follows the new settings.
- A guild with no settings row gets one on first save. Before that, the page shows the defaults (`IsEnabled=false`, `SensitiveOnly=true`).
- A non-admin, or an admin of a different guild, gets the standard guild-authorization result.
- `RouteSmokeTests` covers the new route. It uses `guildId` only, so the fixture handles it.

### Tests

- Page model tests: GET maps the entity, POST persists it, the POST rejects foreign channel IDs, an empty monitored list is stored as "all channels".
- `NotXService` round-trip of the monitored-channel JSON. Extend the existing tests if they don't already cover it.

### Docs

`ui-inventory.md` (new page), `feature-map.md` (the not-X row and its "cannot be changed" banner), `docs/articles/not-x/feature-reference.md` (replace the "no way to change settings" limitation with portal instructions), `configuration-guide.md` (drop the matching note).

---

## B. Remove the feature-request DM flow

### Problem

Only `/feature-request` could start the AI requirements-gathering DM conversation. These pieces are still registered but can never be reached:

- `Services/FeatureRequests/FeatureRequestConversationService.cs` (singleton)
- `Handlers/FeatureRequestDmHandler.cs`: subscribed in `BotHostedService`, ahead of `DmAssistantMessageHandler`
- `Infrastructure/Services/FeatureRequests/FeatureRequestToolProvider.cs`
- the `submit_feature_request` `ToolCatalog` entry and `ToolScopes.FeatureRequests`
- `LlmMode.FeatureRequests` and `FeatureRequestsOptions.RequirementsGatheringModel`
- `PromptInjectionFilter` and `InputValidationService`: check first whether anything outside this flow uses them

### Decision

**Remove the flow (recommended).** Keep the `FeatureRequest` entity, its repository, `IFeatureRequestService`, the portal FeatureRequests pages and DocGen. Existing requests stay visible and triageable.

The alternative is to re-home intake as an assistant tool in DM scope, so users can say "I have a feature request" to the assistant. That is a feature in its own right; if it's wanted, write a separate spec.

### Scope

- Delete the conversation service, the DM handler and the tool provider, and remove their registrations in `FeatureRequestServiceExtensions`, `DiscordServiceExtensions` and `BotHostedService`.
- Remove the `submit_feature_request` catalogue entry and `docs/tools/` page if one exists. Remove `ToolScopes.FeatureRequests` if nothing else uses it. `ToolContractTests` must stay green.
- Remove `LlmMode.FeatureRequests` and its model-resolver branch, plus any `FeatureRequestsOptions` properties used only by the flow. Keep the options class if `FeatureRequests:Enabled` still gates the portal.
- Delete `FeatureRequestConversationServiceTests` and the DM handler tests.
- No schema change.

### Acceptance criteria

- The build is clean, and DMs to the bot reach the assistant handler with no feature-request interception.
- The portal FeatureRequests pages and DocGen are unchanged.

### Docs

Delete or archive `docs/articles/feature-request-command/`, and update `toc.yml`, `feature-map.md`, `service-catalog.md` and `configuration-guide.md` (options keys).

---

## C. Portal parity gaps

Three command-only actions have no portal button yet.

| Gap | Existing backend | Work |
|---|---|---|
| Run a scheduled message now (was `/schedule-run`) | `POST /api/.../scheduled-messages/{id}/execute` (`ScheduledMessagesController`) | Add a "Run now" row action on `Guilds/ScheduledMessages/Index`, with a confirmation modal, calling the existing endpoint. |
| Send a test welcome message (was `/welcome test`) | `WelcomeController` has `GET`, `PUT` and `POST preview`, but no send | Add `POST /api/guilds/{guildId}/welcome/test`, which sends the rendered message to the configured channel with the signed-in admin as the "member". Add a "Send test" button on `Guilds/Welcome`. |
| Watchlist UI (was `/watchlist`) | `WatchlistController` (`GET`, `POST`, `DELETE {userId}`) | Add a watchlist section to `Guilds/Members/Moderation`, or a "Watchlist" tab under moderation: list, add by user, remove. |

Each row can be its own small PR. Follow the "User Data in Markup and Scripts" rules in `patterns.md` (`data-*` attributes, `SafeHtml.escape`, snowflakes as strings). Update `api-endpoints.md` for the new welcome endpoint and `ui-inventory.md` for the new UI.

**Acceptance:** each action works from the portal, writes an audit log entry where the old command did, and is covered by page model or controller tests.

---

## D. Merge near-duplicate commands

Goal: about 18 top-level commands. Every change below renames or reshapes a public command, so ship all of them in **one release** with changelog notes.

| Today | After | Notes |
|---|---|---|
| `/vox`, `/fvox`, `/hgrunt` | `/vox message [voice] [gap]`, where `voice` is a choice of `vox` (default), `fvox` or `hgrunt` | The three handlers already share a body. Keep the `VoxModule` name. |
| `/tts`, `/tts-styled` | `/tts message [voice] [preset]` | The `preset` option takes over `tts-styled`'s autocomplete. Choosing a preset overrides `voice`. Keep the existing rate limits. |
| `/join`, `/join-channel` | `/join [channel]` | No channel joins the caller's voice channel, as today. |
| `/consent *`, `/privacy *` | `/privacy consent-grant`, `consent-revoke`, `status`, `preview-delete`, `export-data`, `delete-data` | Fold `ConsentModule` into `PrivacyModule` and drop the `ConsentModule` row from `DefaultModules`. Keep `GrantedVia = "SlashCommand"`. The Core category means it can't be disabled. |
| `/rat-clear`, `/rat-stats`, `/rat-leaderboard` | `/rat clear`, `/rat stats`, `/rat leaderboard` | The Rat Watch context menu must stay outside the `[Group]` (context menus can't be grouped). Split it into `RatWatchContextMenuModule` and map it to `RatWatchModule`'s toggle. |

### Implementation notes

- **Rat Watch split.** `SlashCommandRegistrationService.ResolveCompanionParentModule` lost its explicit companion map in PR #2000. Bring back a small map for `RatWatchContextMenuModule` → `RatWatchModule`, or name the module so the `*ComponentModule` convention doesn't apply and give it its own `DefaultModules` row. A map entry is preferred, so the Commands tab shows one Rat Watch toggle. The `ratwatch:*` component and modal IDs must keep routing.
- **Stale commands.** Registration is a bulk overwrite, so old names disappear from Discord on the next startup. No manual cleanup is needed.
- **The assistant.** Check `docs/agents/*.md` and the assistant tools (`Services/LLM/Tools/`) for hard-coded command names (`/consent` is mentioned in `assistant-agent.md`).
- **Command analytics.** `CommandLog` rows keep the old names. Dashboards that group by command name show a break at the release; mention it in the changelog.

### Acceptance criteria

- Discord shows no more than about 18 top-level commands. Every behaviour listed above is reachable through its new shape.
- Module enable and disable on the Commands tab still governs each feature, including the Rat Watch context menu.
- Tests: update the module tests for the merged handlers, plus a registration test for the Rat Watch companion mapping.

### Docs

`README.md`, `command-configuration.md`, `feature-map.md`, the per-feature articles (soundboard, TTS, VOX, voice, privacy, rat-watch), and the release changelog.

---

## E. Cleanup pass (last)

- **Services left behind by PR #2000.** Find services whose only caller was a removed command module and delete them if nothing else uses them. Candidates: whatever backed `/investigate`, `/modstats` and `/modexport`. Check `service-catalog.md` against DI registrations. Leave anything the portal or the assistant uses.
- **`RequireOwnerAttribute`.** It now has no command users but stays, because `CommandMetadataService` maps it. Remove it only if the Commands page stops listing it.
- **Docs audit.** The docs subagent in PR #2000 did not fully audit `commands-page.md` and `troubleshooting-guide.md`. Sweep them for removed command names.
- **Lessons learned.** Add a note in `docs/lessons-learned/` if anything in this series fought back.

## Order

1. **A** first, because it restores a capability users lost.
2. **B** and **C** in parallel.
3. **D**, as a single release.
4. **E**, which closes the series.

## Open questions

1. Feature-request intake: is removing it (B) enough, or should it come back as an assistant DM tool?
2. `/privacy` consent subcommand names: `consent-grant` and `consent-revoke`, or a nested `/privacy consent grant|revoke` group? A nested group costs a sub-module (see the nested-group handling in `SlashCommandRegistrationService`). **Resolved (D):** flat names, `consent-grant` and `consent-revoke`, with consent status as `/privacy status`.
3. Watchlist UI: a section on the member moderation page, or its own tab?
