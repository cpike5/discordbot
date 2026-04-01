# not-X — User Stories

**Version:** 1.0
**Date:** 2026-04-01
**Status:** Draft

---

## Table of Contents

1. [Personas](#personas)
2. [Epic: Automatic Tweet Preview](#epic-automatic-tweet-preview)
3. [Epic: Manual Trigger](#epic-manual-trigger)
4. [Epic: Guild Configuration](#epic-guild-configuration)
5. [Epic: Output Channel Routing](#epic-output-channel-routing)
6. [Epic: Operational Reliability](#epic-operational-reliability)
7. [Story Map Summary](#story-map-summary)

---

## Personas

| Persona | Description |
|---------|-------------|
| **Alex** | Regular guild member; shares X links frequently in conversation. Not technical. |
| **Jordan** | Guild member who does not have an X/Twitter account. Relies entirely on in-Discord context. |
| **Sam** | Guild admin; manages the bot and configures features. Comfortable with slash commands. |
| **Riley** | Moderator; reviews content shared in the guild. Uses right-click tools regularly. |
| **Bot Operator** | Deployer and maintainer of the bot; monitors logs and error rates. |

---

## Epic: Automatic Tweet Preview

### US-001 — Sensitive tweet auto-preview

**As Alex**, I want the bot to automatically post the contents of sensitive X/Twitter links I share,
**so that** my guild mates can see what I'm talking about without leaving Discord or logging in to X.

**Acceptance Criteria:**

- [ ] When Alex posts a message containing an `x.com/handle/status/ID` or `twitter.com/handle/status/ID` URL in a guild where not-X is enabled, the bot posts an embed within ~10 seconds.
- [ ] The embed contains: the tweet's text, the author's display name, the author's `@handle`, and any images attached to the tweet.
- [ ] The embed includes a link back to the original tweet on X.
- [ ] If the tweet has no images, the embed contains only the text and author info (no broken image placeholder).
- [ ] The embed is posted as a reply to Alex's original message (in the same channel).

---

### US-002 — Multiple tweet URLs in one message

**As Alex**, I want all X links in a single message to get previews,
**so that** I can share threads or collections of tweets without losing context.

**Acceptance Criteria:**

- [ ] If Alex's message contains two or more tweet URLs, each is processed independently.
- [ ] A separate embed (or embed group) is posted for each tweet.
- [ ] If one URL fails to fetch, the others are still attempted.
- [ ] The bot does not post duplicate embeds if the same URL appears twice in one message.

---

### US-003 — Non-sensitive tweet is not duplicated

**As a guild member**, I want the bot to leave normal (non-flagged) tweets alone,
**so that** channels are not flooded with duplicate embeds for tweets that already preview fine.

**Acceptance Criteria:**

- [ ] In a guild with `SensitiveOnly = true` (the default), a tweet where `possibly_sensitive = false` produces no bot response.
- [ ] Discord's native embed (if present) is unaffected.
- [ ] No bot message appears in the channel.

---

### US-004 — Multi-image tweet

**As Jordan**, I want to see all images from a multi-photo tweet,
**so that** I get the full context without needing to visit X.

**Acceptance Criteria:**

- [ ] A tweet with 4 photos results in 4 images displayed in Discord (one per embed, stacked in the same message).
- [ ] A tweet with 1 photo results in 1 image in the embed.
- [ ] Images are displayed in their original upload order.
- [ ] A maximum of 4 images are shown even if fxtwitter returns more.

---

### US-005 — Video tweet handling

**As Jordan**, I want at least a thumbnail when a tweet contains a video,
**so that** I know what the tweet is about even if I cannot watch the video in-app.

**Acceptance Criteria:**

- [ ] A tweet containing a video shows the video's thumbnail image in the embed.
- [ ] The embed includes a note indicating the content is a video (e.g. "▶ Video — view on X").
- [ ] The embed links to the original tweet where the video can be watched.
- [ ] No attempt is made to download or re-host the video.

---

### US-006 — All-tweets mode for NSFW guilds

**As Sam (admin of a guild designated NSFW)**, I want previews for all tweet links, not just sensitive-flagged ones,
**so that** my community has a consistent link preview experience regardless of the sender's account settings.

**Acceptance Criteria:**

- [ ] Sam can run `/notx sensitive-only false` to disable the sensitivity gate.
- [ ] After doing so, all tweet links in monitored channels produce embeds regardless of `possibly_sensitive` value.
- [ ] Sam can re-enable the gate with `/notx sensitive-only true`.
- [ ] The current state is visible in `/notx status`.

---

## Epic: Manual Trigger

### US-007 — Right-click fetch for missed tweets

**As Jordan**, I want to manually request a tweet preview for a link that was posted before the bot was enabled,
**so that** I can get context for older messages without navigating to X.

**Acceptance Criteria:**

- [ ] Jordan can right-click any message → Apps → "Fetch Tweet".
- [ ] If the message contains a tweet URL, an embed is posted in the same channel within ~10 seconds.
- [ ] Jordan receives an ephemeral confirmation message telling them which URLs were processed and whether the preview was posted.
- [ ] The `IsEnabled` and `SensitiveOnly` guild settings do not block the manual trigger.
- [ ] If the message contains no tweet URLs, Jordan receives an ephemeral "No Tweet Found" message.

---

### US-008 — Right-click fetch for multiple URLs

**As Jordan**, I want all tweet links in a right-clicked message to be fetched,
**so that** I don't have to trigger the command multiple times for a message with several links.

**Acceptance Criteria:**

- [ ] All tweet URLs extracted from the target message are fetched in sequence.
- [ ] The ephemeral confirmation lists each URL and its individual result (success / failed to fetch / no tweet found at URL).

---

### US-009 — Right-click produces no public noise on failure

**As a guild member watching the channel**, I want the channel to stay clean if Jordan's right-click fetch fails,
**so that** failed fetch attempts do not pollute the conversation.

**Acceptance Criteria:**

- [ ] If fxtwitter returns an error for a manual trigger, only Jordan sees the failure (ephemeral message).
- [ ] No message is posted to the channel.
- [ ] The channel only receives output when there is actual content to display.

---

## Epic: Guild Configuration

### US-010 — Enable the feature

**As Sam**, I want to enable not-X for my guild with a single command,
**so that** I can start benefiting from tweet previews without complex setup.

**Acceptance Criteria:**

- [ ] `/notx enable` enables the feature.
- [ ] Sam receives an ephemeral confirmation that the feature is now enabled.
- [ ] The feature is disabled by default; running `/notx enable` is required before any automatic processing begins.

---

### US-011 — Disable the feature

**As Sam**, I want to disable not-X immediately if members complain,
**so that** I have quick control over the bot's behaviour.

**Acceptance Criteria:**

- [ ] `/notx disable` disables the feature.
- [ ] Messages received after the command is executed are not processed.
- [ ] Sam receives an ephemeral confirmation.

---

### US-012 — View current configuration

**As Sam**, I want to see the current not-X configuration at a glance,
**so that** I don't have to remember what I last set.

**Acceptance Criteria:**

- [ ] `/notx status` responds with an ephemeral embed showing: enabled/disabled state, `SensitiveOnly` value, output channel (if set), and monitored channel list (or "All channels").
- [ ] The response is always ephemeral.

---

### US-013 — Restrict monitored channels

**As Sam**, I want not-X to only monitor specific channels,
**so that** tweet preview noise is limited to relevant areas of the server (e.g. `#media-links`).

**Acceptance Criteria:**

- [ ] `/notx monitor add #channel` adds a channel to the monitored list.
- [ ] `/notx monitor remove #channel` removes a channel from the list.
- [ ] `/notx monitor clear` resets to monitoring all channels.
- [ ] When the monitored list is non-empty, tweets in non-listed channels produce no embed.
- [ ] When the monitored list is empty, all channels are monitored.
- [ ] `/notx status` correctly reflects the current monitored list.

---

## Epic: Output Channel Routing

### US-014 — Route previews to a dedicated channel

**As Sam**, I want tweet previews to appear in a dedicated `#link-previews` channel rather than cluttering the source channel,
**so that** the main conversation channels remain clean.

**Acceptance Criteria:**

- [ ] `/notx channel set #link-previews` designates that channel as the output for all previews.
- [ ] After setting this, embeds appear in `#link-previews`, not in the channel where the link was shared.
- [ ] The embed in `#link-previews` includes a reference to the source channel and message (e.g. "Shared in #general").
- [ ] `/notx channel clear` resets output to the originating channel.
- [ ] `/notx status` shows the configured output channel.

---

### US-015 — Output channel must be accessible to the bot

**As Sam**, I want to receive a clear error if I configure an output channel the bot cannot post in,
**so that** I can fix permissions before previews are silently lost.

**Acceptance Criteria:**

- [ ] When Sam sets an output channel, the bot verifies it can send messages there.
- [ ] If the bot lacks `Send Messages` or `Embed Links` permission in the target channel, Sam receives an ephemeral error explaining the issue.
- [ ] The output channel setting is not saved if the permission check fails.

---

## Epic: Operational Reliability

### US-016 — Silent failure on API outage

**As a guild member**, I want the bot to stay quiet when it cannot fetch a tweet,
**so that** API outages or deleted tweets don't produce confusing error messages in my channels.

**Acceptance Criteria:**

- [ ] If fxtwitter is unreachable (timeout, 5xx), no message is posted to the channel.
- [ ] If a tweet is deleted (404) or from a private account (401/403), no message is posted.
- [ ] The failure is logged internally for the bot operator to review.

---

### US-017 — Bot operator can monitor fetch error rates

**As the Bot Operator**, I want structured logs and metrics for not-X activity,
**so that** I can detect fxtwitter outages or API changes before users notice.

**Acceptance Criteria:**

- [ ] Every fetch attempt is logged with: guild ID, tweet ID, HTTP status code, and outcome (posted / skipped / error).
- [ ] A log entry at `Information` level is written for each embed successfully posted.
- [ ] A log entry at `Warning` level is written for each unexpected fetch failure (non-404/401/403 errors).
- [ ] (Phase 2) An OpenTelemetry counter `notx_tweets_posted_total` is incremented on each successful post.

---

## Story Map Summary

| Priority | Story | Epic |
|----------|-------|------|
| Must Have | US-001 Sensitive tweet auto-preview | Automatic Preview |
| Must Have | US-003 Non-sensitive tweet not duplicated | Automatic Preview |
| Must Have | US-004 Multi-image tweet | Automatic Preview |
| Must Have | US-007 Right-click fetch for missed tweets | Manual Trigger |
| Must Have | US-009 No public noise on failure | Manual Trigger |
| Must Have | US-010 Enable feature | Guild Config |
| Must Have | US-011 Disable feature | Guild Config |
| Must Have | US-012 View config status | Guild Config |
| Must Have | US-016 Silent failure on API outage | Reliability |
| Should Have | US-002 Multiple URLs in one message | Automatic Preview |
| Should Have | US-005 Video tweet handling | Automatic Preview |
| Should Have | US-006 All-tweets mode | Automatic Preview |
| Should Have | US-008 Right-click multiple URLs | Manual Trigger |
| Should Have | US-013 Restrict monitored channels | Guild Config |
| Should Have | US-014 Route to dedicated channel | Output Routing |
| Should Have | US-015 Permission check on output channel | Output Routing |
| Could Have | US-017 Bot operator metrics | Reliability |
