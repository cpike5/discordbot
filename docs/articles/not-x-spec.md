# not-X Feature Specification

**Version:** 1.0
**Date:** 2026-04-01
**Status:** Proposal
**Target Framework:** .NET 8, Discord.Net

---

## Table of Contents

1. [Overview](#overview)
2. [Problem Statement](#problem-statement)
3. [Architecture](#architecture)
4. [Data Model](#data-model)
5. [External API: fxtwitter](#external-api-fxtwitter)
6. [Handler & Service Design](#handler--service-design)
7. [Slash Commands](#slash-commands)
8. [Configuration](#configuration)
9. [Dependency Injection](#dependency-injection)
10. [Observability](#observability)
11. [Open Questions](#open-questions)

---

## Overview

**not-X** is a message monitoring feature that detects X/Twitter links posted in guild channels and, when those links point to content-sensitive (18+, media-flagged) tweets that Discord refuses to embed, automatically posts the tweet's text and images as a native Discord embed.

The name is a riff on "X (formerly Twitter)" — not-X surfaces what X won't show.

---

## Problem Statement

X/Twitter has account-level and tweet-level "sensitive media" flags. When a link to flagged content is posted in Discord:

- Discord receives no usable oEmbed data for sensitive tweets.
- The link renders as a bare URL with no preview.
- Users must click through to X and potentially log in before seeing the content.

### What "doesn't embed" means in practice

A tweet link will produce no Discord embed when any of the following is true:

| Condition | Source |
|-----------|--------|
| Tweet author's account has "Mark media as containing material that may be sensitive" enabled | Author account setting |
| Individual tweet is flagged with a content warning by the author | Per-tweet setting |
| Tweet contains media (photo/video) and the account is age-restricted | X policy |

not-X watches for these cases and fills in the missing preview.

---

## Architecture

```
MessageReceived event
        │
        ▼
┌─────────────────────────┐
│  NotXMessageHandler     │  Extracts tweet URLs from message text
│  (IDiscordEventHandler) │  Skips bots, checks guild settings
└────────────┬────────────┘
             │  tweet URL(s)
             ▼
┌─────────────────────────┐
│  INotXService           │  Orchestrates fetch + post decisions
│  NotXService            │
└────────────┬────────────┘
             │  tweet ID + username
             ▼
┌─────────────────────────┐
│  IFxTwitterClient       │  HTTP call to api.fxtwitter.com
│  FxTwitterClient        │  Returns FxTweetResult (text, media, flags)
└────────────┬────────────┘
             │  FxTweetResult
             ▼
┌─────────────────────────┐
│  NotXEmbedBuilder       │  Constructs Discord Embed(s) from tweet data
│  (internal helper)      │  Handles multi-image layout
└────────────┬────────────┘
             │  Embed[]
             ▼
      channel.SendMessageAsync
```

### Component responsibilities

| Component | Responsibility |
|-----------|----------------|
| `NotXMessageHandler` | Subscribe to `MessageReceived`; extract tweet URLs; delegate to service |
| `NotXService` | Load guild settings; decide whether to post; call fetch client; call channel |
| `FxTwitterClient` | HTTP GET to fxtwitter JSON API; deserialize response; SSRF guard |
| `NotXEmbedBuilder` | Map `FxTweetResult` → `Embed[]` respecting image layout constraints |
| `NotXGuildSettings` | Per-guild config entity (enabled flag, channel overrides) |
| `NotXCommandModule` | Admin slash commands to configure the feature |

---

## Data Model

### `NotXGuildSettings` entity

```csharp
public class NotXGuildSettings
{
    /// <summary>Primary key — same as the Discord guild ID.</summary>
    public ulong GuildId { get; set; }

    /// <summary>Feature kill-switch. Defaults to disabled.</summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// If set, tweet previews are posted to this channel instead of the
    /// originating channel.
    /// </summary>
    public ulong? OutputChannelId { get; set; }

    /// <summary>
    /// JSON-serialised ulong[] — only monitor these channels.
    /// Null / empty = monitor all channels.
    /// </summary>
    public string? MonitoredChannelIdsJson { get; set; }

    /// <summary>
    /// When true, only post previews when the tweet is flagged sensitive.
    /// When false, post previews for ALL tweet links (regardless of sensitivity).
    /// Defaults to true (the primary use-case).
    /// </summary>
    public bool SensitiveOnly { get; set; } = true;

    /// <summary>
    /// When true, suppress the "🔞 sensitive content" label on the embed.
    /// Useful for guilds that are already NSFW-designated.
    /// </summary>
    public bool HideSensitiveLabel { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guild? Guild { get; set; }

    public IReadOnlyList<ulong> GetMonitoredChannelIds()
    {
        if (string.IsNullOrEmpty(MonitoredChannelIdsJson)) return Array.Empty<ulong>();
        return JsonSerializer.Deserialize<ulong[]>(MonitoredChannelIdsJson) ?? Array.Empty<ulong>();
    }

    public void SetMonitoredChannelIds(IEnumerable<ulong> ids)
    {
        MonitoredChannelIdsJson = JsonSerializer.Serialize(ids.ToArray());
    }
}
```

### EF configuration highlights

```csharp
builder.HasKey(s => s.GuildId);
builder.Property(s => s.GuildId).HasConversion<long>().IsRequired();
builder.Property(s => s.OutputChannelId).HasConversion<long?>(); // nullable ulong
builder.Property(s => s.IsEnabled).HasDefaultValue(false);
builder.Property(s => s.SensitiveOnly).HasDefaultValue(true);
builder.Property(s => s.HideSensitiveLabel).HasDefaultValue(false);
builder.HasOne(s => s.Guild)
    .WithMany()
    .HasForeignKey(s => s.GuildId)
    .OnDelete(DeleteBehavior.Cascade);
```

Migrations go to both `Migrations/Sqlite/` and `Migrations/Postgresql/` per the project convention.

---

## External API: fxtwitter

[fxtwitter](https://github.com/FixTweet/FxTwitter) is an open-source proxy specifically built to fix Twitter embeds for messaging apps. It exposes a free, unauthenticated JSON API.

### Endpoint

```
GET https://api.fxtwitter.com/{screen_name}/status/{tweet_id}
```

Both `screen_name` and `tweet_id` can be extracted from any `twitter.com` or `x.com` status URL. `screen_name` can be any value (fxtwitter resolves by ID); passing the one from the URL avoids an extra redirect.

### Relevant response fields

```json
{
  "code": 200,
  "message": "OK",
  "tweet": {
    "id": "1234567890",
    "url": "https://twitter.com/user/status/1234567890",
    "text": "tweet body text",
    "created_at": "2025-01-01T00:00:00.000Z",
    "possibly_sensitive": true,
    "author": {
      "name": "Display Name",
      "screen_name": "handle",
      "avatar_url": "https://pbs.twimg.com/profile_images/..."
    },
    "media": {
      "photos": [
        {
          "url": "https://pbs.twimg.com/media/...",
          "width": 1200,
          "height": 675
        }
      ],
      "videos": [
        {
          "url": "https://video.twimg.com/...",
          "thumbnail_url": "https://pbs.twimg.com/...",
          "width": 1280,
          "height": 720
        }
      ]
    },
    "replies": 12,
    "retweets": 34,
    "likes": 56,
    "views": 7890
  }
}
```

Non-200 `code` values (404 tweet not found, 401 private account, 500 X-side error) are treated as non-actionable and logged at debug level.

### `FxTweetResult` model

```csharp
public record FxTweetResult(
    string TweetId,
    string TweetUrl,
    string Text,
    DateTimeOffset CreatedAt,
    bool PossiblySensitive,
    FxTweetAuthor Author,
    FxTweetMedia? Media,
    FxTweetStats Stats
);

public record FxTweetAuthor(string Name, string ScreenName, string AvatarUrl);

public record FxTweetMedia(
    IReadOnlyList<FxTweetPhoto> Photos,
    IReadOnlyList<FxTweetVideo> Videos
);

public record FxTweetPhoto(string Url, int Width, int Height);
public record FxTweetVideo(string ThumbnailUrl, int Width, int Height);

public record FxTweetStats(int Replies, int Retweets, int Likes, int Views);
```

### SSRF considerations

The fxtwitter hostname resolves to Cloudflare-owned IPs, not private ranges, so the existing `WebFetchToolProvider` SSRF checks serve as a reference but do not need to be ported verbatim. `FxTwitterClient` should:

1. Only ever connect to `api.fxtwitter.com` (hardcoded; never user-supplied).
2. Enforce a response size cap (e.g. 256 KB) — tweet JSON is tiny.
3. Set a request timeout (e.g. 5 s).

No general-purpose URL fetching is involved; the client is single-purpose.

---

## Handler & Service Design

### `NotXMessageHandler`

Registered on `_client.MessageReceived` in `BotHostedService`.

```
HandleMessageReceivedAsync(SocketMessage rawMessage)
  │
  ├─ skip: not SocketUserMessage, is bot, is DM
  ├─ skip: guild not available (IsDMChannel)
  ├─ extract tweet URLs from message.Content via regex
  ├─ skip: no tweet URLs found
  ├─ load NotXGuildSettings (via INotXService)
  ├─ skip: !settings.IsEnabled
  ├─ skip: channel not in MonitoredChannelIds (if list is non-empty)
  └─ foreach URL → await _notXService.ProcessTweetAsync(guildId, channelId, messageId, url)
```

**URL regex** (covers both domains, handles query strings and fragments):

```
(?:https?://)?(?:www\.)?(?:twitter\.com|x\.com)/(?:\w+)/status/(\d+)
```

Group 1 captures the tweet ID. The screen name is extracted from group preceding `/status/`.

Multiple tweet URLs in a single message are processed sequentially (not parallel) to avoid Discord rate-limit pressure.

### `NotXService`

```
ProcessTweetAsync(guildId, channelId, messageId, tweetUrl)
  │
  ├─ fetch FxTweetResult via IFxTwitterClient
  ├─ if fetch failed → log debug, return
  ├─ if settings.SensitiveOnly && !result.PossiblySensitive → return (not our job)
  ├─ build Embed[] via NotXEmbedBuilder
  ├─ resolve output channel:
  │     settings.OutputChannelId ?? originating channelId
  └─ channel.SendMessageAsync(embeds: embeds, messageReference: ref to original msg)
```

When `OutputChannelId` differs from the originating channel, the `messageReference` is omitted (cross-channel replies aren't supported by Discord) and the embed footer includes a link back to the original message.

### `NotXEmbedBuilder`

Discord embeds support one image per embed. To display multiple tweet images, the builder returns an array of embeds:

- **Embed 0**: tweet text, author info (name + avatar thumbnail), stats footer, first image (if any).
- **Embeds 1–3**: image-only embeds that visually stack in the same message.
- Maximum 4 images displayed (Discord's limit per message is 10 embeds, but 4 photos is the Twitter grid maximum).

Embed colour: `#1D9BF0` (X/Twitter blue) for non-sensitive tweets; `#FF6B6B` (red-ish) when `PossiblySensitive` is true and `HideSensitiveLabel` is false.

Footer text examples:
- `🐦 @handle · ♥ 1.2k · 🔁 340 · 💬 12`  (normal)
- `🔞 Sensitive content · @handle · ♥ 1.2k · 🔁 340`  (sensitive, label shown)

Videos: not downloadable via fxtwitter; the embed includes the thumbnail image and a note "▶ Video — click to view on X".

---

## Slash Commands

Module: `NotXCommandModule`
Group: `/notx` (no hyphen — Discord slash command names are alphanumeric + underscore only)

| Command | Parameters | Permission | Description |
|---------|-----------|------------|-------------|
| `/notx enable` | — | `ManageGuild` | Enable not-X for this guild |
| `/notx disable` | — | `ManageGuild` | Disable not-X for this guild |
| `/notx status` | — | `ManageGuild` | Show current settings (ephemeral embed) |
| `/notx channel set` | `channel` (TextChannel) | `ManageGuild` | Route previews to a specific channel |
| `/notx channel clear` | — | `ManageGuild` | Reset output to originating channel |
| `/notx monitor add` | `channel` (TextChannel) | `ManageGuild` | Add channel to monitor list |
| `/notx monitor remove` | `channel` (TextChannel) | `ManageGuild` | Remove channel from monitor list |
| `/notx monitor clear` | — | `ManageGuild` | Monitor all channels (clear the list) |
| `/notx sensitive-only` | `enabled` (bool) | `ManageGuild` | Toggle whether to only post for sensitive tweets |

All commands respond ephemerally.

### `/notx status` response example

```
not-X Settings
──────────────────────────────
Status:          ✅ Enabled
Sensitive only:  ✅ Yes (only posts when tweet is flagged)
Output channel:  #nsfw-previews
Monitored:       All channels
```

---

## Configuration

No `appsettings.json` additions are required for the core feature; behaviour is entirely guild-controlled via slash commands and the database.

Optional `appsettings.json` section for operator-level tuning:

```json
"NotX": {
  "RequestTimeoutSeconds": 5,
  "MaxResponseBytes": 262144,
  "UserAgent": "discordbot/1.0 (+https://github.com/cpike5/discordbot)"
}
```

Corresponding options class: `NotXOptions`.

---

## Dependency Injection

```csharp
// NotXExtensions.cs
public static IServiceCollection AddNotX(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.Configure<NotXOptions>(configuration.GetSection("NotX"));

    // Named HTTP client — single-purpose, points only at api.fxtwitter.com
    services.AddHttpClient("FxTwitter", client =>
    {
        client.BaseAddress = new Uri("https://api.fxtwitter.com/");
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.UserAgent
            .ParseAdd("discordbot/1.0");
    });

    services.AddScoped<INotXGuildSettingsRepository, NotXGuildSettingsRepository>();
    services.AddScoped<INotXGuildSettingsService, NotXGuildSettingsService>();
    services.AddScoped<IFxTwitterClient, FxTwitterClient>();
    services.AddScoped<INotXService, NotXService>();

    // Handler is a singleton (same lifetime as DiscordSocketClient)
    services.AddSingleton<NotXMessageHandler>();

    return services;
}
```

`BotHostedService` wires the handler:

```csharp
_client.MessageReceived += _notXMessageHandler.HandleMessageReceivedAsync;
```

---

## Observability

Following existing patterns:

- **Logging**: structured logs on every fetch attempt, post attempt, and skip decision (at appropriate levels — debug for skips, info for posts, warning for fetch failures).
- **Tracing**: a child `Activity` span from `BotActivitySource` wrapping the full `ProcessTweetAsync` call, tagged with `guild.id`, `tweet.id`, `tweet.sensitive`.
- **Metrics** (optional, phase 2): counter `notx_tweets_posted_total{guild, sensitive}` via OpenTelemetry.

No new background services are required — the handler is purely reactive.

---

## Open Questions

| # | Question | Options | Notes |
|---|----------|---------|-------|
| 1 | **Scope trigger**: should the feature trigger on *all* tweet links or only sensitive ones by default? | Default sensitive-only (safer); allow guild to opt into all-links mode | `SensitiveOnly` column handles this; question is just the default |
| 2 | **Video handling**: fxtwitter doesn't proxy video; show thumbnail + note, or skip video tweets? | Thumbnail + note is more informative | Linked above in embed builder section |
| 3 | **Rate limiting**: fxtwitter is a free community service — should we rate-limit calls per guild? | Simple per-guild sliding window (e.g. 10 tweets/minute) | Can reuse existing rate limiting patterns |
| 4 | **Repost suppression**: if the same tweet URL is posted multiple times in a channel, should subsequent posts be deduplicated? | Short-lived in-memory cache of (guildId, tweetId) tuples | Avoids spam when multiple users paste the same link |
| 5 | **Retweet / quote-tweet handling**: fxtwitter returns nested `quote` objects; should quoted tweets also be fetched and inlined? | Phase 2 enhancement | Not required for MVP |
| 6 | **Portal UI**: should there be a guild settings page for not-X? | Consistent with other features that have settings pages; phase 2 | Not required for MVP |
| 7 | **t.co short links**: messages may contain `t.co/...` redirect URLs instead of direct tweet links. Should the handler follow redirects? | Yes — one HEAD request to resolve; cache result | Adds one HTTP round-trip per short link; relatively uncommon in practice |
