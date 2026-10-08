# Enhancement Candidates — October 2026

> **Status:** Draft for discussion
> **Date:** 2026-10-08
> **Baseline:** v1.5.1-dev (after the UX polish plan, the agent groundwork overhaul, the LLM model catalog and usage ledger, and virtual currency)

A survey of what the bot does today, thirty candidate enhancements grounded in gaps the survey found, and a short-list of the fifteen worth doing first.

---

## 1. Where the project stands

**Shape.** One .NET 8 process: a Discord.NET bot, a Razor Pages admin portal with a member portal, REST controllers, SignalR hubs, and ~25 background services, over EF Core on PostgreSQL (SQLite still shipped, being phased out). Clean architecture in four projects (`Agents` → leaf engine, `Core` → contracts, `Infrastructure` → data and LLM providers, `Bot` → everything hosted). ~4,800 tests on a real Postgres, a route smoke test over every page, tool contract tests, and a model-backed eval suite that skips without a key.

**Feature streams and their maturity.**

| Stream | What exists | Depth |
| --- | --- | --- |
| Audio | Soundboard (queue, filters, favourites, categories, export/import, pricing), TTS (Azure, SSML, presets, history), VOX/FVOX/HGRUNT, unified now-playing, audio moderation log, member portal for all three | Deep. Most of the last two milestones went here |
| Moderation | warn/kick/ban/unban/mute/purge with DM notify and case numbers, notes, tags, watchlist, history, stats, investigation, automod (spam, content filter, raid), flagged-event review | Broad; a few loose ends (below) |
| Community | Reminders (DM, one-shot), Rat Watch (voting, leaderboard, analytics), virtual currency (ledger, wallets, mint authorities, hold/commit charge seam), scheduled messages (cron, plain text), Not-X previews, feature requests via DM conversation | Mixed: currency and Rat Watch are deep; reminders and scheduled messages are thin |
| Assistant | Guild `@mention` assistant (single-turn, consent-gated, per-guild tool allow-list), owner-only DM assistant (multi-turn, memory notes, skills, code execution, web fetch), model catalog and allowlist, per-mode defaults, usage ledger, prompt-surface measurement, evals | Engine is mature; the guild surface is deliberately narrow (documentation Q&A plus a few lookups) |
| Admin & platform | Identity + Discord OAuth, 4 fixed roles, guild access grants, audit log, command/message logging, performance dashboard and alerts, notifications inbox, global search, settings tabs, GDPR export/purge, PWA, health endpoint, Prometheus metrics | Mature |

**Gaps the survey turned up.** These are not opinions about what would be nice; they are things the code stores or documents but does not finish.

1. `ModerationCase.ExpiresAt` is written for temp bans and mutes, but nothing reads it back. Discord timeouts expire on their own; **bans do not**, so a "7-day ban" is permanent unless a moderator remembers.
2. The bot never subscribes to `MessageUpdated` or `MessageDeleted`. The message-logging docs promise "edit/delete history"; `MessageLog` has no edited or deleted columns and the handler list has nothing for it.
3. The guild assistant is single-turn: no follow-up questions, no thread context, and a skill costs a full round every time (which is why `docs/agents/skills/guild/` ships empty).
4. The guild assistant has read-only lookups only. The engine already has a `Mutation` seam with a refusal path and contract tests for it, but no guild tool uses it.
5. Reminders are DM-only, one-shot, with no snooze and no recurrence; a failed DM is silent (the docs call this out as a future enhancement).
6. Scheduled messages are a title and a text body. No embed, no attachment, no role mention, no template variables.
7. There is no mod-log channel. Every moderation action lands in the database and the portal, but a moderator in Discord has no feed.
8. Timezones are per guild (`GuildRatWatchSettings.Timezone`, default Eastern) and the time parser takes one; there is no per-user timezone, so "tomorrow 3pm" means the guild's 3pm, not the asker's.
9. Slash-command rate limits are compile-time attributes (`[RateLimit(5, 10)]`); an admin cannot tune them.
10. The REST API is cookie-authenticated only. There is no API key or token path for an external script, dashboard, or home-automation hook.
11. Virtual currency has exactly one priced feature (soundboard) and no way to earn units except a mint.
12. The welcome system sends a message and an embed but assigns no roles, despite the README's "automatic role assignment".
13. Nothing plays to a voice channel from anything but a file on disk, Azure TTS, or a VOX clip.
14. The ~276 closed feature issues and 9 open ones are almost all audio portal and VOX; the open set (`F29`–`F35`) is per-role audio access, quiet hours, voice previews, cross-feature search, scheduled TTS, per-user volume, soundboard paging.

---

## 2. Thirty candidates

Grouped by stream. **Effort** is S (a day), M (a few days), L (a week or more). **Value** is the author's judgement of user impact per unit of effort, 1 to 5.

### Moderation

| # | Candidate | What it is | Effort | Value |
| --- | --- | --- | --- | --- |
| 1 | **Temp-ban and mute expiry service** | A `MonitoredBackgroundService` that sweeps `ModerationCase` rows with `ExpiresAt <= now` and no close, lifts the Discord ban, writes a closing case and audit row. Fixes gap 1 | S | 5 |
| 2 | **Mod-log channel feed** | A per-guild channel (in `GuildModerationConfig`) that receives an embed for every case, flagged event, and automod action, with a "View in portal" link. Fixes gap 7 | M | 5 |
| 3 | **Message edit and delete capture** | Subscribe to `MessageUpdated`/`MessageDeleted`; add `EditedAt`, `DeletedAt`, `PreviousContent` to `MessageLog`; show edit history in the message log viewer and the investigation page. Fixes gap 2 | M | 4 |
| 4 | **Escalation ladder** | Automod "N warnings in M days → mute for X, next → temp ban" expressed in `GuildModerationConfig` and evaluated by `ModerationActionRunner` after each case. Today every action is a separate human decision | M | 4 |
| 5 | **LLM-assisted flagged-event triage** | A background classifier on `ILlmClient` (cheapest enabled model) that scores a flagged event (spam, harassment, false positive) and suggests a disposition; a moderator still clicks. Surfaces in the flagged-event review page and as a DM assistant tool | M | 3 |
| 6 | **Appeal flow** | A `/appeal` command (works in DM after a ban) that opens a case thread in the mod-log channel and a review page in the portal | L | 2 |
| 7 | **Report message context menu** | "Report to moderators" on any message; creates a `FlaggedEvent` with the message context and pings the mod-log channel. Reuses the Rat Watch modal pattern | S | 4 |

### Community and engagement

| # | Candidate | What it is | Effort | Value |
| --- | --- | --- | --- | --- |
| 8 | **Reminders v2** | Recurring reminders (`every weekday 9am`), snooze buttons on delivery, channel delivery as an option, a retry with channel fallback on DM failure, and a portal page. Fixes gap 5 | M | 4 |
| 9 | **Per-user timezone** | A `/timezone` command and a portal setting stored in `UserPreference`; `TimeParsingService` and Rat Watch read it before the guild default. Fixes gap 8 | S | 4 |
| 10 | **Scheduled messages v2** | Embed builder (title, colour, image), file attachment, role/channel mention, template variables (`{{member_count}}`, `{{date}}`), preview in the portal. Fixes gap 6 | M | 4 |
| 11 | **Reaction and button roles** | Self-assignable roles via a bot-posted message with buttons or reactions; a portal page to compose the panel. Uses the existing `ComponentModule` and `ComponentIdBuilder` pattern | M | 5 |
| 12 | **Welcome auto-roles and onboarding** | Finish the README promise: assign roles on join, optional "press to verify" button, optional DM. Fixes gap 12 | S | 4 |
| 13 | **Polls** | `/poll` with up to ten options, a timed close, live counts via buttons, results posted and stored; a portal history page | M | 3 |
| 14 | **Activity rewards** | A background service that mints currency for activity already recorded in `UserActivityEvent` (messages, voice minutes, reactions), capped per day and configurable per guild. Gives the currency an income side. Fixes gap 11 | M | 4 |
| 15 | **Role shop** | Spend currency on a role for a period; a `PriceEntry` keyed `role:{roleId}` and a small expiry sweep. Pairs with 14 | M | 3 |
| 16 | **Starboard** | Messages with N ⭐ reposted to a board channel; the reaction event is already subscribed for analytics | S | 3 |
| 17 | **Discord scheduled-event helper** | Announce new guild events to a channel, remind RSVP'd members before start, and post a recap. Needs `GuildScheduledEvents` intent | M | 2 |
| 18 | **Birthdays** | `/birthday set`, a daily announcement, optional role for the day. Small, popular, cheap | S | 2 |

### Audio

| # | Candidate | What it is | Effort | Value |
| --- | --- | --- | --- | --- |
| 19 | **Auto-TTS channel** | Mark a text channel so the bot reads new messages aloud in the author's voice channel (accessibility for members without a mic). Reuses `ITtsPlaybackService` and the per-user voice preference | M | 4 |
| 20 | **Temporary voice channels** | "Join to create" lobby that spawns a private channel owned by the joiner and deletes it when empty. `VoiceStateHandler` already tracks state | M | 4 |
| 21 | **Play from attachment or URL** | `/play` accepts an uploaded audio file or a direct audio URL (size- and type-limited, FFmpeg-transcoded through the existing cache) without first adding it to the library. Fixes gap 13 | S | 3 |
| 22 | **Quiet hours and per-role audio access** | Ship the two open backlog items `F29` and `F30` together: a per-guild schedule when audio refuses, and role lists per audio feature. Both are settings plus a precondition | M | 3 |

### Assistant

| # | Candidate | What it is | Effort | Value |
| --- | --- | --- | --- | --- |
| 23 | **Multi-turn guild assistant in threads** | A mention opens or continues a thread; the thread is the conversation key, with a sliding window like the DM assistant. Makes guild skills affordable (one load per thread) and fixes gap 3 | M | 5 |
| 24 | **Guild assistant action tools** | The first `Mutation` tools on the guild surface: `set_reminder`, `play_sound`, `create_poll`, `schedule_message` (draft, admin confirms via button). Behind the per-guild allow-list and off by default. Fixes gap 4 | M | 4 |
| 25 | **Moderator skill for the guild assistant** | Once 23 exists: a `docs/agents/skills/guild/moderation.md` with case lookups, watchlist and notes, visible only to callers holding the Moderator portal role | S | 3 |
| 26 | **Owner alerts to DM** | Performance alerts, failed background services, and failed scheduled messages delivered to the owner's DM through the existing DM assistant channel, with the alert as context so the next question can be "what happened?" | S | 4 |

### Platform and admin

| # | Candidate | What it is | Effort | Value |
| --- | --- | --- | --- | --- |
| 27 | **API tokens for the REST API** | Personal access tokens (hashed, scoped to a role and optional guild, revocable) accepted by a second authentication scheme on `/api/*`. Fixes gap 10 and unlocks external dashboards and automation | M | 4 |
| 28 | **Configurable command cooldowns** | Move `[RateLimit]` defaults into `CommandModuleConfiguration` so the Commands settings tab can tune per guild. Fixes gap 9 | S | 3 |
| 29 | **Guild configuration export and import** | One JSON bundle of a guild's settings (moderation, audio, TTS, welcome, assistant, scheduled messages, currencies, prices) for backup and cloning to a new server. Soundboard export is the model | M | 3 |
| 30 | **New-guild setup wizard** | When the bot joins a guild, the portal shows a checklist (features, channels, roles, consent copy) and the bot posts a one-time "set me up" DM to the inviter. Lowers the floor for a second operator | M | 3 |

---

## 3. Short-list: the fifteen to do first

Ranked. The ordering favours (a) finishing things the code already half-promises, (b) features that make an existing deep investment pay off, and (c) breadth for everyday members rather than more depth for the audio portal, which has had two milestones in a row.

| Rank | # | Candidate | Why it is on the list |
| --- | --- | --- | --- |
| 1 | 1 | Temp-ban and mute expiry service | A correctness bug with user-visible consequences: a "7-day ban" never lifts. One service, one sweep, existing audit hooks |
| 2 | 2 | Mod-log channel feed | The single most-expected feature of a moderation bot that this one lacks. Every case already has the data; this is an embed and a channel ID |
| 3 | 23 | Multi-turn guild assistant in threads | The agent engine is the most-invested part of the codebase and the guild surface uses almost none of it. Threads unlock follow-ups, skills, and action tools in one move |
| 4 | 11 | Reaction and button roles | Table-stakes for community servers; exactly the component pattern the repo already has |
| 5 | 8 | Reminders v2 | The most-used member feature is the thinnest; the docs themselves list the fallback as a to-do |
| 6 | 9 | Per-user timezone | Small, and it makes reminders, Rat Watch and scheduled-message previews right for everyone outside the guild's default zone. Do it before 8 so recurring reminders are correct from day one |
| 7 | 3 | Message edit and delete capture | Documented as existing; it is not. Investigation and moderation both want it |
| 8 | 14 | Activity rewards | Currency has a full ledger and a charge seam but no way for an ordinary member to earn. Without this the currency stays an admin toy |
| 9 | 24 | Guild assistant action tools | The `Mutation` seam, refusal path and contract tests are built and unused on the guild surface. Start with `set_reminder` and `play_sound`; both have services and no side effects beyond their feature |
| 10 | 10 | Scheduled messages v2 | Embeds and template variables are what announcement channels actually need; plain text is why admins keep posting by hand |
| 11 | 27 | API tokens | Makes the REST API usable beyond the portal. Low risk if tokens inherit the issuing user's role and guild grants |
| 12 | 19 | Auto-TTS channel | A real accessibility feature with every piece already built (TTS playback, user voice preference, channel settings) |
| 13 | 20 | Temporary voice channels | Common ask, small surface, and the voice-state handler already exists |
| 14 | 26 | Owner alerts to DM | Cheap, and it turns the performance dashboard from something to check into something that comes to you |
| 15 | 12 | Welcome auto-roles | Closes a README promise; small |

**Left off the short-list and why.** Polls, starboard, birthdays and the event helper (13, 16, 17, 18) are good but generic; any bot does them, and none exercises something this codebase is uniquely good at. The appeal flow (6) is large for the number of servers that will use it. LLM triage (5) should wait until the mod-log feed (2) gives it a place to show up. Quiet hours and per-role audio access (22) are already tracked as `F29`/`F30` and can ride the backlog. Export/import (29) and the setup wizard (30) matter once a second operator runs the bot; today there is one.

**Suggested sequencing.** Three batches, each one PR-sized concern at a time per `CLAUDE.md`:

1. *Finish what is promised* — 1, 12, 3, 9. Small, independent, all correctness.
2. *Moderation and community breadth* — 2, 11, 8, 10, 7 (the report context menu is a natural companion to the mod-log feed).
3. *Make the investments pay* — 23, then 24 and 25; 14 then 15; 27; 19 and 20; 26.

Each short-listed item is medium or smaller and should get a spec in `docs/specs/` before code, per the conventions. Items 23 and 24 touch the assistant surface and need an eval case each in `tests/DiscordBot.Evals`; items 1, 3, 8, 10, 11, 14, 27 add or change entities and so ship both migrations and a `data-model.md` update.
