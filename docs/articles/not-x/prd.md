# not-X — Product Requirements Document (PRD)

**Version:** 1.0
**Date:** 2026-04-01
**Status:** Draft
**Owner:** Bot Product

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Problem Statement](#problem-statement)
3. [Goals & Non-Goals](#goals--non-goals)
4. [Users & Stakeholders](#users--stakeholders)
5. [User Problems](#user-problems)
6. [Feature Requirements](#feature-requirements)
7. [Success Metrics](#success-metrics)
8. [Out of Scope](#out-of-scope)
9. [Dependencies & Risks](#dependencies--risks)
10. [Timeline Considerations](#timeline-considerations)

---

## Executive Summary

X (formerly Twitter) marks certain content as "sensitive" — typically adult, violent, or otherwise age-restricted media. Discord does not embed these links, leaving users with a bare URL and no preview. **not-X** is a bot feature that detects those unembeddable tweet links and automatically reconstructs the tweet content as a Discord-native embed, surfacing the text and images that the platform intentionally hides.

---

## Problem Statement

### The gap

When a user shares a link such as `https://x.com/handle/status/123` in Discord, Discord normally generates an inline preview (embed) via Twitter's oEmbed API. For content-sensitive tweets — where the account owner has flagged their media or where a tweet carries a content warning — Twitter returns no usable oEmbed data. Discord shows nothing. The conversation loses context, users must click out to X and are often met with a login wall or an age-verification prompt.

### Why it matters

- **Context loss**: Links shared without previews interrupt conversation flow. Other participants cannot see what is being discussed without leaving Discord.
- **Friction**: X increasingly gates sensitive content behind accounts. Many users lack accounts or are unwilling to log in.
- **Inconsistency**: Non-sensitive tweets embed richly; identical-format sensitive tweets embed as nothing, with no indication to users why.

---

## Goals & Non-Goals

### Goals

| # | Goal |
|---|------|
| G1 | Detect X/Twitter status URLs in guild messages in real time. |
| G2 | Fetch tweet content (text, images, metadata) via a reliable third-party API. |
| G3 | Post a Discord-native embed with tweet text and images when the original link would not embed. |
| G4 | Provide a right-click manual trigger so users can fetch previews for links the bot missed or for non-sensitive tweets. |
| G5 | Allow guild admins to enable/disable the feature and configure its behaviour per guild. |
| G6 | Impose no additional authentication burden on end users. |

### Non-Goals

| # | Non-Goal | Rationale |
|---|----------|-----------|
| NG1 | Replacing Discord's native embed for non-sensitive tweets. | Discord already handles those; duplicating would create noise. |
| NG2 | Archiving or storing tweet content in the bot's database. | Retention of third-party content raises legal concerns out of scope for this feature. |
| NG3 | Proxying or re-hosting video. | Video files are large and hosting introduces cost and ToS complexity. |
| NG4 | Supporting platforms other than X/Twitter. | This feature is scoped to X. Similar treatment of other platforms is a separate feature. |
| NG5 | Bypassing X's login wall for private accounts. | fxtwitter cannot access protected tweets; these are correctly not surfaced. |
| NG6 | Building our own tweet scraper or using the official X API. | Unnecessary infrastructure cost. fxtwitter is sufficient and well-maintained. |

---

## Users & Stakeholders

### Primary users

| Persona | Description | Interaction with not-X |
|---------|-------------|------------------------|
| **Discord guild member** | Regular user posting or reading messages in a guild. | Passively benefits from auto-posted previews; can use right-click manual trigger. |
| **Content sharer** | User who frequently posts X links in conversations. | Their links now produce previews for all participants. |
| **Guild admin** | User with `Manage Guild` permission who configures the bot. | Enables the feature, selects output channels, adjusts settings via `/notx` commands. |
| **Moderator** | User with moderation permissions. | Can use the right-click trigger to surface content for review without leaving Discord. |

### Stakeholders

- **Guild admins**: primary configurers; need clear, concise slash commands and status feedback.
- **Guild members**: passive beneficiaries; no action required.
- **Bot operator**: responsible for maintaining the fxtwitter HTTP client dependency and monitoring error rates.

---

## User Problems

### P1 — Broken previews disrupt conversation

**As a guild member**, when someone shares an X link to a sensitive tweet I have no context for what is being discussed without leaving Discord and logging in to X.

### P2 — Login walls create friction

**As a user without an X account** (or unwilling to log in), I am completely locked out of viewing content behind X's age/login gates, even when other guild members intend to share it openly.

### P3 — No visual indication that a preview failed

**As a guild member**, when I see a bare URL I cannot tell whether it's a sensitive tweet, a deleted tweet, or just a link with no preview — they all look identical.

### P4 — Bot misses links when it is restarted or newly added

**As a guild admin**, tweets posted while the bot was offline or before the feature was enabled are permanently missed by the automatic handler. There is no recovery path.

### P5 — Sensitive-only filter may be too strict for some guilds

**As a guild admin** of an NSFW-designated server, I want previews for all tweet links (not just flagged ones) so my community has a consistent experience regardless of the sender's X account settings.

---

## Feature Requirements

### FR-1: Automatic detection

- The bot monitors all new messages in enabled guilds.
- Any message containing one or more X/Twitter status URLs triggers the pipeline.
- URLs in both `twitter.com` and `x.com` domains are matched.
- A guild admin can restrict monitoring to specific channels.

### FR-2: Sensitive-content gating (default on)

- By default, the bot only posts a preview when the fetched tweet is flagged `possibly_sensitive`.
- Guild admins can disable this gate to preview all tweets regardless of sensitivity flag.

### FR-3: Embed posting

- The bot posts a Discord embed containing: tweet text, author name, @handle, author avatar, images, engagement statistics (likes, retweets, replies), and a link back to the original tweet.
- Up to 4 images are shown (matching Twitter's photo grid maximum).
- Video tweets show a thumbnail image and a note directing users to view the video on X.

### FR-4: Output channel routing

- By default, previews are posted in the same channel as the original message, as a reply.
- Guild admins can designate an alternative output channel (e.g. `#nsfw-previews`).
- When routing to a different channel, the embed footer includes a link back to the source message.

### FR-5: Manual trigger (right-click context menu)

- A **"Fetch Tweet"** option is available when right-clicking any message under the Apps submenu.
- The command extracts tweet URLs from that message and triggers the fetch/post pipeline.
- The `IsEnabled` and `SensitiveOnly` guild settings are bypassed for manual triggers; any guild member can fetch any tweet.
- The invoker receives an ephemeral confirmation listing each URL and whether a preview was posted.

### FR-6: Guild administration commands

- `/notx enable` and `/notx disable` to toggle the feature.
- `/notx status` to view current configuration (ephemeral).
- `/notx channel set/clear` to manage output channel routing.
- `/notx monitor add/remove/clear` to restrict monitored channels.
- `/notx sensitive-only` to toggle the sensitivity gate.
- All admin commands require `Manage Guild` permission.

### FR-7: Graceful degradation

- If the fxtwitter API is unavailable or returns an error, the bot logs the failure and does nothing — it does not post error messages to the channel.
- If a tweet is deleted or from a protected account, the bot silently skips it.

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Adoption** — guilds with feature enabled (90 days post-launch) | 20% of guilds that have used ≥1 other feature | Guild settings query |
| **Coverage** — % of sensitive tweet links that receive a preview within 10s | ≥ 95% (excluding fxtwitter outages) | Handler telemetry |
| **Manual trigger usage** | > 0 uses/week per active guild | Interaction telemetry |
| **Error rate** — failed fxtwitter fetches / total fetch attempts | < 5% (excluding tweet-not-found, protected account) | HTTP client metrics |
| **False positive rate** — non-sensitive previews posted when SensitiveOnly=true | 0% | Can be derived from `possibly_sensitive` flag logging |

---

## Out of Scope

- Video download or proxying.
- Storing tweet content or media in the bot's database.
- Previewing quote-tweets or reply threads inline (phase 2 candidate).
- Portal UI settings page (phase 2 candidate).
- `t.co` short-link resolution (phase 2; edge case).
- Support for any platform other than X/Twitter.
- Modifying or deleting the original message.

---

## Dependencies & Risks

| Item | Type | Description | Mitigation |
|------|------|-------------|------------|
| **fxtwitter API** | External dependency | Free community service; no SLA. Could go offline or change its API contract. | Log all non-200 responses; feature degrades gracefully to no-op. Monitor error rate in observability dashboard. |
| **X/Twitter policy changes** | External risk | X could change how `possibly_sensitive` is reported or further restrict oEmbed responses. | `SensitiveOnly` can be disabled per-guild if flagging behaviour changes. |
| **fxtwitter rate limits** | External risk | Undocumented; high-traffic guilds might hit limits. | Per-guild sliding-window rate limiter on fetch calls (phase 1 optional, phase 2 if observed). |
| **Discord embed limits** | Platform constraint | Max 10 embeds per message; 4096 char description limit per embed. | Enforced in `NotXEmbedBuilder`; tweet text truncated with `…` if needed. |
| **X ToS** | Legal/policy | Scraping or re-displaying X content may conflict with X's Terms of Service. | fxtwitter is the one interacting with X; the bot consumes fxtwitter's public JSON API. Guild operators accept responsibility for content posted in their servers. |

---

## Timeline Considerations

This is a self-contained feature with no hard external deadlines. Suggested phasing:

**Phase 1 (MVP):** Automatic handler, fxtwitter client, embed builder, guild settings entity + repository, `/notx` slash commands, right-click context menu command.

**Phase 2 (Polish):** Per-guild rate limiting on fxtwitter calls, `t.co` resolution, portal settings page, quote-tweet inlining, operational metrics counter.
