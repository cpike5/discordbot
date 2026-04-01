# not-X — Unit Test Constraints

**Version:** 1.0
**Date:** 2026-04-01
**Status:** Draft

This document defines the test constraints, invariants, and edge cases that unit tests must cover for the not-X feature. No implementation code is included — this is a specification of *what* must be tested and *what must always be true*, not *how* to test it.

---

## Table of Contents

1. [TweetUrlExtractor](#tweeturlextractor)
2. [FxTwitterClient](#fxtwitterclient)
3. [NotXService.ProcessTweetAsync](#notxserviceprocesstweetasync)
4. [NotXEmbedBuilder](#notxembedbuilder)
5. [NotXGuildSettings](#notxguildsettings)
6. [NotXMessageHandler](#notxmessagehandler)
7. [NotX Context Menu Command](#notx-context-menu-command)
8. [Repository & Settings Service](#repository--settings-service)

---

## TweetUrlExtractor

This static helper extracts tweet URLs from arbitrary message text. It is the first line of defense — its correctness directly determines what the rest of the pipeline sees.

### Valid URL forms that MUST be detected

| Input | Expected tweet ID extracted |
|-------|-----------------------------|
| `https://twitter.com/user/status/1234567890` | `1234567890` |
| `https://x.com/user/status/1234567890` | `1234567890` |
| `http://twitter.com/user/status/1234567890` | `1234567890` |
| `https://www.twitter.com/user/status/1234567890` | `1234567890` |
| `https://www.x.com/user/status/1234567890` | `1234567890` |
| `https://mobile.twitter.com/user/status/1234567890` | `1234567890` |
| `https://mobile.x.com/user/status/1234567890` | `1234567890` |
| URL with query string: `?s=20` suffix | `1234567890` (query stripped from ID) |
| URL embedded mid-sentence: `"check this out https://x.com/a/status/99 lol"` | `99` |
| Two tweet URLs in one message | Both IDs returned |

### Cases that MUST NOT be extracted

| Input | Reason |
|-------|--------|
| `https://twitter.com/user` (no `/status/`) | Profile page, not a tweet |
| `https://x.com/home` | Home feed URL |
| `https://example.com/twitter.com/status/123` | Domain is not twitter.com or x.com |
| `https://t.co/abc123` | Short link — cannot extract ID without resolving (phase 2) |
| Empty string | No URLs present |
| Message with no URLs | No matches |
| Plain text containing the word "twitter" | Not a URL |

### Invariants

- Extracting from the same string twice must return the same result (pure function).
- Screen name (username) segment is captured alongside the tweet ID (needed for fxtwitter API call).
- Duplicate URLs in a single message must be deduplicated — the same tweet URL appearing twice returns one entry, not two.

---

## FxTwitterClient

This HTTP client wraps `api.fxtwitter.com`. Its contracts define what the rest of the system can depend on.

### Success path

- A well-formed API response with `code: 200` deserializes into a populated `FxTweetResult`.
- `FxTweetResult.TweetId`, `Author.Name`, `Author.ScreenName`, and `Text` are always non-null/non-empty when deserialization succeeds.
- `Media` may be `null` (tweet has no media); this is not an error.
- `Media.Photos` and `Media.Videos` may be empty lists; this is not an error.
- `Stats` fields (Replies, Retweets, Likes, Views) default to `0` if absent from the response — never throw on missing stat fields.

### Non-success HTTP status codes

| Status | Expected behaviour |
|--------|--------------------|
| 404 | Return `null` (tweet not found / deleted). Do not throw. |
| 401 / 403 | Return `null` (private account). Do not throw. |
| 429 | Return `null` and log a warning. Do not throw. |
| 500 / 502 / 503 | Return `null` and log a warning. Do not throw. |
| Network timeout | Return `null` and log a warning. Do not throw. |

### Edge cases

- Response body exceeding the configured size cap must be rejected (return `null`, log warning) — not parsed.
- Malformed JSON (unexpected schema change by fxtwitter) must not throw an unhandled exception; return `null` and log the deserialization error.
- A `code` field value other than 200 in an otherwise valid JSON body must be treated as a failure.
- The client must only ever connect to `api.fxtwitter.com` — the URL is not influenced by any external input.
- Concurrent calls for different tweets must not interfere with each other (no shared mutable state).

### Invariants

- Client is side-effect-free with respect to the database — it makes HTTP calls only.
- The same tweet ID fetched twice in rapid succession should produce equivalent results (no caching contract required, but no mutation of prior results either).

---

## NotXService.ProcessTweetAsync

This is the core orchestration method. Its decision logic is the most critical unit to test.

### Settings-gate decisions (auto path, `ignoreSettingsGate = false`)

| `IsEnabled` | `SensitiveOnly` | `possibly_sensitive` | `channelId` in monitored list | Expected outcome |
|-------------|-----------------|----------------------|-------------------------------|------------------|
| `false` | (any) | (any) | (any) | No post. Return `false`. |
| `true` | `true` | `false` | `true` or empty list | No post (tweet not sensitive). Return `false`. |
| `true` | `true` | `true` | `true` or empty list | Post embed. Return `true`. |
| `true` | `false` | `false` | `true` or empty list | Post embed. Return `true`. |
| `true` | `false` | `true` | `true` or empty list | Post embed. Return `true`. |
| `true` | `true` | `true` | Monitored list non-empty, channel NOT in list | No post. Return `false`. |
| `true` | `true` | `true` | Monitored list non-empty, channel IS in list | Post embed. Return `true`. |

### Settings-gate bypass (manual trigger, `ignoreSettingsGate = true`)

| `IsEnabled` | `SensitiveOnly` | `possibly_sensitive` | Expected outcome |
|-------------|-----------------|----------------------|------------------|
| `false` | `true` | `false` | Post embed. Return `true`. |
| `false` | `false` | `false` | Post embed. Return `true`. |
| `true` | `true` | `false` | Post embed. Return `true`. |

(Channel filter is also bypassed when `ignoreSettingsGate = true`.)

### Fetch failure handling

- If `FxTwitterClient.FetchAsync` returns `null` (for any reason), the method returns `false` and does not call `SendMessageAsync`.
- A `null` result must not throw; it is treated as a silent no-op.

### Output channel routing

- When `settings.OutputChannelId` is `null`, the embed is sent to `channelId` (the originating channel).
- When `settings.OutputChannelId` is set, the embed is sent to that channel, not `channelId`.
- `MessageReference` pointing to the source message is used only when posting to the originating channel.
- When posting to a different channel, a footer back-link is included and no `MessageReference` is set.

### Embed building is delegated

- `ProcessTweetAsync` does not construct embeds directly; it delegates to `NotXEmbedBuilder`.
- If `NotXEmbedBuilder` returns an empty array, no `SendMessageAsync` call is made.

---

## NotXEmbedBuilder

The embed builder converts a `FxTweetResult` into one or more Discord `Embed` objects.

### Embed count rules

| Tweet media | Expected embed count |
|-------------|----------------------|
| 0 photos, 0 videos | 1 (text-only) |
| 1 photo | 1 |
| 2 photos | 2 |
| 3 photos | 3 |
| 4 photos | 4 |
| 5+ photos | 4 (capped at Twitter grid maximum) |
| 1 video (no photos) | 1 (thumbnail + note) |
| 1 photo + 1 video | 2 (photo embed + video thumbnail embed) |

### Embed 0 (primary embed) invariants

- Always contains the tweet text in the description field.
- Always contains author name and `@handle`.
- Author avatar URL is set as thumbnail when available; omitted (not broken) when absent.
- Always contains a link to the original tweet (title link or footer link).
- Timestamp matches `FxTweetResult.CreatedAt`.
- Stats footer line is present (even if all stats are 0).

### Embeds 1–3 (additional image embeds) invariants

- Contain only the image; no duplicate text or author information.
- URL link matches the original tweet (so clicking each image still navigates to the tweet).

### Colour rules

| `PossiblySensitive` | `HideSensitiveLabel` | Expected colour | Expected label text |
|---------------------|----------------------|-----------------|---------------------|
| `false` | (any) | `#1D9BF0` (X blue) | None |
| `true` | `false` | `#FF6B6B` (red) | "🔞 Sensitive content" visible in footer |
| `true` | `true` | `#1D9BF0` (X blue) | No sensitive label |

### Text truncation

- If tweet text exceeds 4096 characters (edge case; standard tweets are ≤280 chars), description is truncated at 4093 characters and `…` is appended.
- Truncation must not split a Unicode surrogate pair.

### Null-safety invariants

- A `null` `Media` property must produce a valid text-only embed (not throw).
- A `null` or empty `Author.AvatarUrl` must produce a valid embed without a thumbnail (not throw).
- Empty `Text` (deleted tweet content, rare edge case from fxtwitter) must produce a valid embed with empty description (not throw).

---

## NotXGuildSettings

### JSON serialization round-trips

- `SetMonitoredChannelIds([])` followed by `GetMonitoredChannelIds()` returns an empty list (not `null`).
- `SetMonitoredChannelIds([123456789012345678UL, 987654321098765432UL])` followed by `GetMonitoredChannelIds()` returns both IDs with full 64-bit precision.
- Discord snowflake IDs (up to 18 decimal digits) must survive JSON round-trip without precision loss.
- `MonitoredChannelIdsJson = null` → `GetMonitoredChannelIds()` returns empty list (not throw).
- `MonitoredChannelIdsJson = ""` → `GetMonitoredChannelIds()` returns empty list (not throw).
- Malformed JSON in `MonitoredChannelIdsJson` → `GetMonitoredChannelIds()` returns empty list (not throw).

### Default value invariants

- A freshly constructed `NotXGuildSettings` has `IsEnabled = false`.
- A freshly constructed `NotXGuildSettings` has `SensitiveOnly = true`.
- A freshly constructed `NotXGuildSettings` has `HideSensitiveLabel = false`.
- A freshly constructed `NotXGuildSettings` has `OutputChannelId = null`.

---

## NotXMessageHandler

The handler is the entry point for `MessageReceived` events.

### Early-exit conditions (MUST produce no downstream calls)

| Condition | Must not call |
|-----------|---------------|
| Message author is a bot | `INotXService.ProcessTweetAsync` |
| Message is a DM (no guild context) | `INotXService.ProcessTweetAsync` |
| Message is not a `SocketUserMessage` | `INotXService.ProcessTweetAsync` |
| Message contains no tweet URLs | `INotXService.ProcessTweetAsync` |
| Guild settings not found in database | `INotXService.ProcessTweetAsync` (settings are loaded first, treated as disabled if null) |

### Normal processing invariants

- One call to `ProcessTweetAsync` per unique tweet URL in the message, not per message.
- Calls are sequential, not concurrent (to avoid Discord rate-limit pressure from a single message).
- An exception thrown by `ProcessTweetAsync` for one URL must not prevent processing subsequent URLs in the same message.
- An exception thrown by `ProcessTweetAsync` must not propagate to the Discord gateway event loop (must be caught and logged).

---

## NotX Context Menu Command

### "Fetch Tweet" command invariants

- Response is **always** deferred as ephemeral before any async work begins.
- If the target message contains no tweet URLs, the followup is an ephemeral warning embed — no embed is posted to the channel.
- If the target message contains tweet URLs, `ProcessTweetAsync` is called with `ignoreSettingsGate = true` for each.
- The followup message lists each URL and its individual result (success / failure).
- `IsEnabled = false` does not prevent the command from posting an embed (gate is bypassed).
- `SensitiveOnly = true` + non-sensitive tweet does not prevent the command from posting an embed (gate is bypassed).
- If fxtwitter returns `null` for a URL, the followup notes the failure for that URL; other URLs in the same message are still processed.
- Channel filter (`MonitoredChannelIds`) is also bypassed — the command works in any channel.
- Output channel routing from guild settings IS respected (if `OutputChannelId` is set, the embed still goes there).

---

## Repository & Settings Service

### `INotXGuildSettingsRepository.GetByGuildIdAsync`

- Returns `null` when no record exists for the given `guildId`.
- Returns the correct entity when a record exists.
- Does not throw for valid `guildId` values regardless of database state.

### `INotXGuildSettingsService` (upsert semantics)

- Enabling a feature on a guild with no existing record creates a new `NotXGuildSettings` with `IsEnabled = true`.
- Enabling a feature on a guild with an existing disabled record updates `IsEnabled = true` (does not create duplicate).
- Updating `SensitiveOnly` preserves all other fields.
- Updating `OutputChannelId` preserves all other fields.
- `UpdatedAt` is always set to a current UTC timestamp on any update.
- `CreatedAt` is set on insert and never updated on subsequent updates.
- Two concurrent enable calls for the same guild must not produce two records (upsert must be idempotent).
