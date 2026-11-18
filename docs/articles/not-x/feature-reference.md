# not-X — Feature Reference

**Version:** 1.0
**Date:** 2026-04-01
**Audience:** Bot developers, power users, guild admins wanting deep understanding

---

## Table of Contents

1. [Overview](#overview)
2. [Component Map](#component-map)
3. [Data Flow: Automatic Path](#data-flow-automatic-path)
4. [Data Flow: Manual Trigger Path (removed)](#data-flow-manual-trigger-path)
5. [fxtwitter API Contract](#fxtwitter-api-contract)
6. [Guild Settings Reference](#guild-settings-reference)
7. [Slash Command Reference (removed)](#slash-command-reference)
8. [Context Menu Command Reference (removed)](#context-menu-command-reference)
9. [Embed Layout Reference](#embed-layout-reference)
10. [Decision Matrix](#decision-matrix)
11. [Error States & Degradation](#error-states--degradation)
12. [Configuration Reference](#configuration-reference)
13. [Observability Reference](#observability-reference)
14. [Known Limitations](#known-limitations)

---

## Overview

not-X surfaces X/Twitter tweet content in Discord when the platform's native embed is suppressed (typically for content marked sensitive/18+ by the tweet author). It operates automatically: it reacts to every `MessageReceived` event, scans for tweet URLs, fetches content via the fxtwitter JSON API, and posts embeds when the sensitivity gate passes.

> **Current limitation: per-guild settings cannot be changed.** The `/notx` slash commands and the "Fetch Tweet" context menu (`NotXCommandModule`, `NotXContextMenuModule`) were removed, and there is no portal page for `NotXGuildSettings` yet. The automatic handler still runs using whatever settings are already stored for each guild. A guild with no settings row (or `IsEnabled = false`) gets no previews, and there is currently no in-product way to change that.

The feature has no persistence footprint beyond the guild settings row — tweet content is never stored.

---

## Component Map

```
src/DiscordBot.Bot/
├── Handlers/
│   └── NotXMessageHandler.cs         MessageReceived event hook
├── Services/
│   └── NotXService.cs                orchestration; implements INotXService
├── Extensions/
│   └── NotXServiceExtensions.cs      AddNotX() DI registration
└── Services/
    └── NotX/
        ├── FxTwitterClient.cs        HTTP client; implements IFxTwitterClient
        ├── NotXEmbedBuilder.cs       Builds Discord embeds from tweet data
        ├── NotXService.cs            Orchestration; implements INotXService
        └── TweetUrlExtractor.cs      Static; used by the handler
├── Data/
│   ├── Repositories/
│   │   └── NotXGuildSettingsRepository.cs
│   └── Configurations/
│       └── NotXGuildSettingsConfiguration.cs

src/DiscordBot.Core/
├── Entities/
│   └── NotXGuildSettings.cs
├── Interfaces/
│   ├── IFxTwitterClient.cs
│   ├── INotXService.cs
│   └── INotXGuildSettingsRepository.cs
└── Models/
    └── FxTweetResult.cs              (record types: FxTweetResult, FxTweetAuthor, FxTweetMedia, etc.)
```

**Note:** `TweetUrlExtractor` is a static helper (not interface-backed) located alongside the other Not-X services in `Bot/Services/NotX/`.

---

## Data Flow: Automatic Path

```
1. Discord gateway fires MessageReceived
        │
2. NotXMessageHandler.HandleMessageReceivedAsync
   ├─ Is SocketUserMessage?                    NO → return
   ├─ Is author a bot?                         YES → return
   ├─ Has guild context?                        NO → return
   ├─ TweetUrlExtractor.Extract(content)
   └─ Any URLs found?                          NO → return
        │
        │  [foreach unique URL]
        ▼
3. NotXService.ProcessTweetAsync(guildId, channelId, messageId, url, ignoreSettingsGate: false)
   ├─ Load NotXGuildSettings from repository
   ├─ settings null or !IsEnabled?             YES → return false
   ├─ MonitoredChannelIds non-empty AND
   │  channelId not in list?                   YES → return false
   ├─ FxTwitterClient.FetchAsync(screenName, tweetId)
   ├─ result null?                             YES → return false (log warning if unexpected)
   ├─ SensitiveOnly AND !result.PossiblySensitive? YES → return false
   ├─ NotXEmbedBuilder.Build(result, settings)
   ├─ Resolve outputChannelId
   │   settings.OutputChannelId ?? channelId
   └─ channel.SendMessageAsync(embeds[], messageReference?)
        │
        ▼
4. Returns true
```

`MessageReference` is set (making the embed a reply) only when the output channel equals the originating channel.

---

## Data Flow: Manual Trigger Path

Removed. This path was the "Fetch Tweet" context menu, which called
`NotXService.ProcessTweetAsync(..., ignoreSettingsGate: true)`. The context menu no longer
exists, so nothing in the bot currently passes `ignoreSettingsGate: true` (the parameter is
still on `INotXService`).

---

## fxtwitter API Contract

### Request

```
GET https://api.fxtwitter.com/{screen_name}/status/{tweet_id}
```

`screen_name` is taken from the URL as posted by the user. fxtwitter resolves by tweet ID; the screen name only affects URL aesthetics and is not validated.

### Response structure (relevant fields)

```
{
  "code": 200,
  "message": "OK",
  "tweet": {
    "id": string,
    "url": string,
    "text": string,
    "created_at": ISO-8601 string,
    "possibly_sensitive": boolean,
    "author": {
      "name": string,
      "screen_name": string,
      "avatar_url": string | null
    },
    "media": {                        // null if tweet has no media
      "photos": [
        { "url": string, "width": int, "height": int }
      ],
      "videos": [
        { "url": string, "thumbnail_url": string, "width": int, "height": int }
      ]
    },
    "replies": int,
    "retweets": int,
    "likes": int,
    "views": int
  }
}
```

### Non-200 codes handled

| code | Meaning | Bot behaviour |
|------|---------|---------------|
| 200 | Success | Parse and post |
| 401 | Private account | Silent no-op |
| 404 | Tweet not found / deleted | Silent no-op |
| 429 | Rate limited | No-op + log Warning |
| 500 | fxtwitter internal error | No-op + log Warning |

### HTTP client configuration

```
Named client:   "FxTwitter"
Base address:   https://api.fxtwitter.com/
Timeout:        5 seconds (configurable via NotXOptions)
Max response:   256 KB (enforced before deserialization)
User-Agent:     DiscordBot/1.0 (+not-x)
```

---

## Guild Settings Reference

Table: `NotXGuildSettings`

| Column | Type | Default | Description |
|--------|------|---------|-------------|
| `GuildId` | `ulong` (stored as `long`) | — | Primary key; Discord guild snowflake ID |
| `IsEnabled` | `bool` | `false` | Master feature toggle |
| `OutputChannelId` | `ulong?` (stored as `long?`) | `null` | If set, route all previews here instead of originating channel |
| `MonitoredChannelIdsJson` | `string?` | `null` | JSON array of `ulong`; `null`/empty = monitor all channels |
| `SensitiveOnly` | `bool` | `true` | Only post when `possibly_sensitive = true`; `false` = post for all tweets |
| `HideSensitiveLabel` | `bool` | `false` | Suppress the 🔞 label on embeds for sensitive tweets |
| `CreatedAt` | `DateTime` (UTC) | — | Row creation timestamp |
| `UpdatedAt` | `DateTime` (UTC) | — | Last modification timestamp |

### Helper methods on `NotXGuildSettings`

| Method | Behaviour |
|--------|-----------|
| `GetMonitoredChannelIds()` | Deserializes `MonitoredChannelIdsJson`; returns empty list on null/empty/invalid JSON |
| `SetMonitoredChannelIds(IEnumerable<ulong>)` | Serializes to `MonitoredChannelIdsJson`; preserves full 64-bit precision |

---

## Slash Command Reference

Removed. The `/notx` group (`enable`, `disable`, `status`, `channel set|clear`,
`monitor add|remove|clear`, `sensitive-only`) was the only way to edit
`NotXGuildSettings`. There is no replacement yet; a portal settings page is planned
(see [Known Limitations](#known-limitations)).

## Context Menu Command Reference

Removed. The "Fetch Tweet" message context menu no longer exists.

---

## Embed Layout Reference

### Text-only tweet (no media)

```
┌─────────────────────────────────────────────────────────┐
│ 👤 Display Name (@handle)                    [avatar]   │
│                                                         │
│ Tweet text goes here. Up to 280 chars for standard     │
│ tweets; truncated at 4096 with … if needed.            │
│                                                         │
│ ─────────────────────────────────────────────────────── │
│ 🐦 @handle · ♥ 1.2k · 🔁 340 · 💬 12 · Jan 1, 2025   │
└─────────────────────────────────────────────────────────┘
```

Colour: `#1D9BF0` (X blue)

### Sensitive tweet, label shown (`HideSensitiveLabel = false`)

Same layout but:
- Colour: `#FF6B6B`
- Footer prepended with: `🔞 Sensitive content · @handle · …`

### Tweet with 3 photos

```
[Message]
 ├── Embed 0: tweet text + author + photo 1 (as embed image)
 ├── Embed 1: photo 2 only (image field, tweet URL as link)
 └── Embed 2: photo 3 only (image field, tweet URL as link)
```

Each image embed uses the same tweet URL so clicking navigates to the tweet.

### Video tweet

```
┌─────────────────────────────────────────────────────────┐
│ 👤 Display Name (@handle)                    [avatar]   │
│                                                         │
│ Tweet text goes here.                                   │
│                                                         │
│ [Video thumbnail image]                                 │
│                                                         │
│ ▶ Video — view on X                                     │
│ ─────────────────────────────────────────────────────── │
│ 🐦 @handle · ♥ … · 🔁 … · 💬 …                        │
└─────────────────────────────────────────────────────────┘
```

---

## Decision Matrix

Complete decision table for `ProcessTweetAsync` on the automatic path (`ignoreSettingsGate = false`):

| IsEnabled | Channel in MonitoredList (or list empty) | fxtwitter result | SensitiveOnly | possibly_sensitive | Action |
|-----------|------------------------------------------|------------------|---------------|--------------------|--------|
| false | — | — | — | — | **No-op** |
| true | false | — | — | — | **No-op** (channel filtered) |
| true | true | null (error/404/401) | — | — | **No-op** (log) |
| true | true | populated | true | false | **No-op** (not sensitive) |
| true | true | populated | true | true | **Post embed** |
| true | true | populated | false | false | **Post embed** |
| true | true | populated | false | true | **Post embed** |

On the manual path (`ignoreSettingsGate = true`), only the `fxtwitter result = null` row results in a no-op; all others post.

---

## Error States & Degradation

| Error | Visibility | Recovery |
|-------|------------|----------|
| fxtwitter timeout | None (logged at Warning) | Automatic on next message/trigger |
| fxtwitter 429 rate limit | None (logged at Warning) | Backoff implicit; no retry logic in phase 1 |
| fxtwitter 5xx | None (logged at Warning) | Automatic on next message/trigger |
| Tweet deleted (404) | None (logged at Debug) | Not recoverable; tweet is gone |
| Private account (401) | None (logged at Debug) | Not recoverable without account auth |
| JSON deserialization error | None (logged at Warning with raw body excerpt) | Indicates API schema change; requires code update |
| Output channel permission denied | N/A (the `/notx channel set` command that validated this was removed) | Fix the bot's channel permissions |
| Guild settings missing (null) | None (treated as disabled) | No way to create one currently (the `/notx enable` command was removed) |

The bot never posts an error message to any guild channel. All errors are internal.

---

## Configuration Reference

`appsettings.json` section `"NotX"` — all fields are optional:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `true` | Global kill switch for the whole feature — see below |
| `RequestTimeoutSeconds` | `int` | `5` | HTTP timeout for fxtwitter API calls |
| `MaxResponseBytes` | `int` | `262144` | Maximum response body size (bytes) before rejection |
| `UserAgent` | `string` | `DiscordBot/1.0 (+not-x)` | User-Agent header sent to fxtwitter |

Options class: `NotXOptions` (bound from `IOptions<NotXOptions>`).

### Disabling the feature

Setting `NotX:Enabled` to `false` (appsettings, an environment variable
`NotX__Enabled=false`, or user secrets) turns the feature off everywhere. Two gates
enforce it, so nothing can post while it is off. `SlashCommandRegistrationService` no
longer has any not-X handling, because there are no not-X commands left to deregister.

1. **Handler short-circuit.** `NotXMessageHandler` returns before inspecting any message, so
   no URL scanning or DI scope creation happens on the `MessageReceived` hot path.
2. **Service refusal.** `NotXService.ProcessTweetAsync` returns `false` before reading guild
   settings or calling fxtwitter. This outranks `ignoreSettingsGate`.

It is read through `IOptions<T>`, which this project does not reload, so a change needs a
process restart.

### Disabling from the admin portal

One portal switch exists alongside the config one:

- **Settings → Features → "not-X Tweet Previews"** writes `Features:NotXEnabled`. It takes
  effect on the next message with no restart, enforced by `NotXService.ProcessTweetAsync`.

There is no longer a Commands-tab toggle for not-X (the command module was removed).

`NotXGuildSettings` rows are left untouched, so each guild's own configuration returns
as it was when the feature is switched back on.

---

## Observability Reference

### Structured log events

| Level | Event | Key fields |
|-------|-------|------------|
| Debug | Tweet URL extracted from message | `guildId`, `channelId`, `messageId`, `tweetId` |
| Debug | Processing skipped — feature disabled | `guildId` |
| Debug | Processing skipped — channel not monitored | `guildId`, `channelId` |
| Debug | Processing skipped — tweet not sensitive | `guildId`, `tweetId` |
| Debug | Tweet not found or private account | `guildId`, `tweetId`, `httpStatus` |
| Information | Tweet embed posted | `guildId`, `tweetId`, `outputChannelId`, `possiblySensitive` |
| Warning | fxtwitter fetch failed | `guildId`, `tweetId`, `httpStatus`, `errorMessage` |
| Warning | Response body too large | `guildId`, `tweetId`, `responseBytes` |
| Warning | JSON deserialization failed | `guildId`, `tweetId`, `exception` |

### OpenTelemetry tracing

Activity: `notx.process_tweet`
Tags:
- `guild.id` — guild snowflake (string)
- `tweet.id` — tweet ID (string)
- `tweet.sensitive` — `possibly_sensitive` value (bool)
- `notx.outcome` — `posted` | `skipped_disabled` | `skipped_not_sensitive` | `skipped_channel` | `fetch_failed`

### Phase 2 metrics (not in MVP)

| Metric | Type | Labels |
|--------|------|--------|
| `notx_tweets_processed_total` | Counter | `guild_id`, `outcome` |
| `notx_fetch_duration_seconds` | Histogram | — |

---

## Known Limitations

| Limitation | Details | Phase |
|------------|---------|-------|
| **No video playback** | Videos show thumbnail + link only; fxtwitter does not proxy video files. | Not planned |
| **t.co short links not resolved** | URLs like `https://t.co/abc` in message text are not followed to discover their tweet destination. | Phase 2 |
| **No quote-tweet inlining** | Quoted tweets are not recursively fetched and inlined. | Phase 2 |
| **No in-memory deduplication** | If the same tweet URL is posted multiple times in a channel in quick succession, each message triggers an independent embed post. | Phase 2 |
| **Per-guild rate limiting absent** | High-volume guilds could stress fxtwitter; no backpressure mechanism in phase 1. | Phase 2 |
| **No way to change guild settings** | The `/notx` slash commands were removed and there is no web portal page for `NotXGuildSettings` yet, so per-guild settings cannot currently be edited. | Phase 2 |
| **Private/protected tweets** | fxtwitter cannot access protected accounts; these are permanently unavailable. | Won't fix |
| **No `possibly_sensitive` guarantee** | X's flagging is author-controlled. Authors can unflag content at will; this does not retroactively affect embeds already posted. | Won't fix |
| **Single process deduplication only** | If the bot is scaled horizontally, in-memory dedup caches (when added) will not be shared without a distributed cache layer. | Phase 2 |
