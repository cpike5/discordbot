# not-X — Feature Reference

**Version:** 1.0
**Date:** 2026-04-01
**Audience:** Bot developers, power users, guild admins wanting deep understanding

---

## Table of Contents

1. [Overview](#overview)
2. [Component Map](#component-map)
3. [Data Flow: Automatic Path](#data-flow-automatic-path)
4. [Data Flow: Manual Trigger Path](#data-flow-manual-trigger-path)
5. [fxtwitter API Contract](#fxtwitter-api-contract)
6. [Guild Settings Reference](#guild-settings-reference)
7. [Slash Command Reference](#slash-command-reference)
8. [Context Menu Command Reference](#context-menu-command-reference)
9. [Embed Layout Reference](#embed-layout-reference)
10. [Decision Matrix](#decision-matrix)
11. [Error States & Degradation](#error-states--degradation)
12. [Configuration Reference](#configuration-reference)
13. [Observability Reference](#observability-reference)
14. [Known Limitations](#known-limitations)

---

## Overview

not-X surfaces X/Twitter tweet content in Discord when the platform's native embed is suppressed (typically for content marked sensitive/18+ by the tweet author). It operates in two modes:

- **Automatic**: Reacts to every `MessageReceived` event, scans for tweet URLs, fetches content via the fxtwitter JSON API, and posts embeds when the sensitivity gate passes.
- **Manual**: A right-click "Fetch Tweet" context menu command that bypasses all guild-level gates and fetches on demand.

The feature has no persistence footprint beyond the guild settings row — tweet content is never stored.

---

## Component Map

```
src/DiscordBot.Bot/
├── Commands/
│   └── NotXCommandModule.cs          /notx slash group + [MessageCommand("Fetch Tweet")]
├── Handlers/
│   └── NotXMessageHandler.cs         MessageReceived event hook
├── Services/
│   └── NotXService.cs                orchestration; implements INotXService
└── Extensions/
    └── NotXExtensions.cs             AddNotX() DI registration

src/DiscordBot.Infrastructure/
├── Services/
│   └── Http/
│       └── FxTwitterClient.cs        HTTP client; implements IFxTwitterClient
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

Internal helper (not an interface-backed service):

```
src/DiscordBot.Bot/Utilities/
└── TweetUrlExtractor.cs              static; shared by handler and command module
```

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

```
1. User right-clicks message → Apps → "Fetch Tweet"
        │
2. NotXCommandModule.FetchTweetContextMenuAsync(IMessage message)
   ├─ DeferAsync(ephemeral: true)
   ├─ TweetUrlExtractor.Extract(message.Content)
   └─ Any URLs found?    NO → FollowupAsync("No Tweet Found", ephemeral)
        │
        │  [foreach unique URL]
        ▼
3. NotXService.ProcessTweetAsync(guildId, channelId, message.Id, url, ignoreSettingsGate: true)
   ├─ (IsEnabled check SKIPPED)
   ├─ (MonitoredChannelIds check SKIPPED)
   ├─ FxTwitterClient.FetchAsync(screenName, tweetId)
   ├─ result null?                             YES → record failure, continue to next URL
   ├─ (SensitiveOnly check SKIPPED)
   ├─ NotXEmbedBuilder.Build(result, settings)
   ├─ Resolve outputChannelId (settings.OutputChannelId is still respected)
   └─ channel.SendMessageAsync(embeds[])
        │
        ▼
4. FollowupAsync(results summary, ephemeral: true)
   e.g. "✅ Posted preview for <url>"
        "⚠️ Could not fetch <url>"
```

Guild settings are still loaded for the output channel routing step even in the manual path.

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

Command group: `/notx`
Required permission: `Manage Guild` (on all sub-commands)
All responses: ephemeral

| Command | Parameters | Effect |
|---------|-----------|--------|
| `/notx enable` | — | Sets `IsEnabled = true` |
| `/notx disable` | — | Sets `IsEnabled = false` |
| `/notx status` | — | Shows current guild settings (formatted embed) |
| `/notx channel set` | `channel` (TextChannel) | Sets `OutputChannelId`; validates bot has Send Messages + Embed Links in target |
| `/notx channel clear` | — | Sets `OutputChannelId = null` (back to originating channel) |
| `/notx monitor add` | `channel` (TextChannel) | Appends channel to `MonitoredChannelIds` |
| `/notx monitor remove` | `channel` (TextChannel) | Removes channel from `MonitoredChannelIds` |
| `/notx monitor clear` | — | Sets `MonitoredChannelIdsJson = null` (monitor all channels) |
| `/notx sensitive-only` | `enabled` (bool) | Sets `SensitiveOnly` |

### `/notx status` embed format

```
not-X Settings
────────────────────────────────
Status:             ✅ Enabled  /  ❌ Disabled
Sensitive only:     ✅ Yes (only posts flagged tweets)
                 /  ❌ No  (posts all tweets)
Output channel:     #channel-name  /  Same channel as link
Monitored:          #ch1, #ch2, #ch3  /  All channels
Sensitive label:    Shown  /  Hidden
```

---

## Context Menu Command Reference

Command name: **Fetch Tweet**
Appears under: Right-click message → Apps → Fetch Tweet
Permission: Any guild member (no restriction; Discord Integration settings can restrict per-guild)
Response: Always ephemeral

### Behaviour summary

1. Extracts tweet URLs from the target message content.
2. For each URL: calls `ProcessTweetAsync` with `ignoreSettingsGate: true`.
3. Respects `OutputChannelId` routing (embeds still go to the configured channel).
4. Reports per-URL results in the ephemeral followup.

### Ephemeral followup format

```
Fetch Tweet results:
✅ Posted preview for https://x.com/user/status/123
⚠️ Could not fetch https://x.com/user/status/456  (tweet may be deleted or private)
```

When no tweet URLs are found:

```
⚠️ No Tweet Found
That message doesn't appear to contain an X/Twitter link.
```

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
| Output channel permission denied | Ephemeral error at config time (channel set command) | Admin fixes channel permissions |
| Guild settings missing (null) | None (treated as disabled) | Resolved when admin runs `/notx enable` |

The bot never posts an error message to any guild channel. All errors are internal.

---

## Configuration Reference

`appsettings.json` section `"NotX"` — all fields are optional:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `RequestTimeoutSeconds` | `int` | `5` | HTTP timeout for fxtwitter API calls |
| `MaxResponseBytes` | `int` | `262144` | Maximum response body size (bytes) before rejection |
| `UserAgent` | `string` | `DiscordBot/1.0 (+not-x)` | User-Agent header sent to fxtwitter |

Options class: `NotXOptions` (bound from `IOptions<NotXOptions>`).

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
| **No portal settings UI** | Guild settings are managed via slash commands only; no web portal page. | Phase 2 |
| **Private/protected tweets** | fxtwitter cannot access protected accounts; these are permanently unavailable. | Won't fix |
| **No `possibly_sensitive` guarantee** | X's flagging is author-controlled. Authors can unflag content at will; this does not retroactively affect embeds already posted. | Won't fix |
| **Single process deduplication only** | If the bot is scaled horizontally, in-memory dedup caches (when added) will not be shared without a distributed cache layer. | Phase 2 |
