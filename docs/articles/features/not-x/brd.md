# not-X — Business Requirements Document (BRD)

**Version:** 1.0
**Date:** 2026-04-01
**Status:** Draft
**Owner:** Bot Product

---

## Table of Contents

1. [Business Objectives](#business-objectives)
2. [Stakeholders](#stakeholders)
3. [Business Context](#business-context)
4. [Assumptions](#assumptions)
5. [Business Rules](#business-rules)
6. [Constraints](#constraints)
7. [Risk Register](#risk-register)
8. [Compliance & Legal Considerations](#compliance--legal-considerations)
9. [Acceptance Criteria (Business Level)](#acceptance-criteria-business-level)

---

## Business Objectives

| ID | Objective | Rationale |
|----|-----------|-----------|
| BO-1 | Increase the utility of the bot for guilds that share content from X/Twitter. | Content sharing is a core Discord activity; improving the experience increases bot engagement and retention. |
| BO-2 | Reduce friction for guild members encountering sensitive-content links. | Users who must leave Discord to view linked content represent a disruption in community engagement. |
| BO-3 | Provide guild admins granular control over the feature's behaviour. | Different guilds have different community standards; one-size-fits-all is insufficient. |
| BO-4 | Maintain the bot's reputation for reliability by failing silently. | Noisy error messages erode user trust more than silent no-ops. |

---

## Stakeholders

| Stakeholder | Role | Interest |
|-------------|------|----------|
| **Bot Operator** | Owner/deployer | Feature is maintainable, low operational burden, no ToS violations against the bot. |
| **Guild Admins** | Configurers | Simple setup, clear documentation, no surprises. |
| **Guild Members** | End users | Seamless content viewing; no additional steps required. |
| **fxtwitter Project** | External dependency | No unusual or abusive traffic patterns from this bot. |
| **X/Twitter** | Indirect party | Not a direct stakeholder; ToS implications noted in risk register. |

---

## Business Context

### Why not build this into the bot itself?

Directly fetching and parsing X/Twitter pages would require:

1. Maintaining a web scraper against a site that actively resists scraping.
2. Handling X's JavaScript-rendered content (requires a headless browser).
3. Dealing with X's login walls and authentication challenges.
4. Risk of IP bans against the bot's host.

fxtwitter is an established open-source proxy service purpose-built for this exact use case, with a stable JSON API. Consuming it as an external dependency is the correct build-vs-buy decision.

### Why "sensitive-only" by default?

Non-sensitive tweets already embed in Discord for most users. Generating a second embed for every tweet link would:

- Double the noise in channels that share many links.
- Provide no incremental value over Discord's built-in behaviour.
- Increase fxtwitter API call volume unnecessarily.

The default `SensitiveOnly = true` ensures the feature activates only where it adds value.

### Why allow manual trigger for any member (not just moderators)?

The Fetch Tweet context menu command is a convenience tool, not a privileged action. The content it surfaces would be publicly viewable to anyone who clicks the link anyway; the bot just removes friction. Restricting it to moderators would confuse users without improving safety.

---

## Assumptions

| ID | Assumption |
|----|------------|
| A-1 | fxtwitter's public JSON API (`api.fxtwitter.com`) remains available and free to access without authentication. |
| A-2 | The `possibly_sensitive` field in fxtwitter's response accurately reflects whether Discord would suppress a tweet's embed. |
| A-3 | Guild admins who enable the feature accept responsibility for the content that the bot surfaces in their channels under Discord's Community Guidelines. |
| A-4 | The bot's existing `MessageReceived` event handler infrastructure can support an additional handler without meaningful performance impact on typical guild message volumes. |
| A-5 | Discord's limit of 10 embeds per message will not change in a way that breaks the multi-image approach before the feature ships. |
| A-6 | Tweet text and metadata fetched via fxtwitter are accurate representations of the source tweet at the time of fetch. |

---

## Business Rules

| ID | Rule | Enforcement Point |
|----|------|-------------------|
| BR-1 | The feature must be explicitly enabled by a guild admin before it activates. It is **off by default**. | `NotXGuildSettings.IsEnabled` defaults to `false`; handler checks flag before processing. |
| BR-2 | The bot must not post a preview if the fxtwitter fetch fails for any reason. | `NotXService` treats all non-success results as no-ops. |
| BR-3 | The bot must not post a preview for a tweet from a protected/private account. | fxtwitter returns 401 for private accounts; treated as a no-op. |
| BR-4 | The bot must not modify, delete, or suppress the original message. | Not implemented; only `SendMessageAsync` is used. |
| BR-5 | The bot must not store tweet content (text, media) persistently in the database. | No tweet-related entities in the data model; embeds are fire-and-forget. |
| BR-6 | The bot must credit the original author in every embed. | `NotXEmbedBuilder` always includes author name and `@handle`. |
| BR-7 | The bot must include a direct link back to the original tweet in every embed. | Embed footer or title links back to the `tweet.url` from the fxtwitter response. |
| BR-8 | Admin commands (`/notx`) must require `Manage Guild` permission. | Discord.Net `[DefaultMemberPermissions(GuildPermission.ManageGuild)]` attribute on the command group. |
| BR-9 | Right-click "Fetch Tweet" response must be ephemeral (visible only to the invoker). | `DeferAsync(ephemeral: true)` and `FollowupAsync(ephemeral: true)` in command handler. |
| BR-10 | Per-guild settings must be isolated — changes in one guild must never affect another. | `GuildId` is the primary key of `NotXGuildSettings`; all queries are scoped by `GuildId`. |

---

## Constraints

### Technical Constraints

| Constraint | Impact |
|------------|--------|
| Discord enforces a maximum of 10 embeds per message. | Multi-image tweets are capped at 4 images (matching Twitter's photo grid), leaving headroom. |
| Discord embed description field maximum is 4096 characters. | Long tweet text must be truncated with `…` at 4096 chars. In practice tweets are ≤280 chars; this is a safety constraint only. |
| Discord does not support cross-channel `MessageReference` (replies). | When routing output to a different channel, the embed footer provides the back-link instead. |
| fxtwitter does not proxy video files. | Video tweets can only show a thumbnail; the full video requires visiting X. |
| The bot runs as a single process (or in a scaled-out stateless fashion). | In-memory deduplication caches (if added in phase 2) cannot be shared across instances without a distributed cache. |

### Operational Constraints

| Constraint | Impact |
|------------|--------|
| fxtwitter has no published SLA. | The bot must not degrade overall reliability when fxtwitter is unavailable. Silent failure is mandatory. |
| No X API key is required. | Avoids recurring API costs and credential management. Also means the bot cannot access features exclusive to authenticated API calls (e.g. private tweet previews). |

### Regulatory/Policy Constraints

| Constraint | Impact |
|------------|--------|
| Discord Community Guidelines prohibit hosting certain categories of content (CSAM, etc.). | The feature must not circumvent Discord's own content safety measures. Sensitive content flagged by X does not include illegal categories; the feature surfaces adult media, not prohibited content. |
| X Terms of Service restrict scraping. | The bot does not scrape X directly; it consumes fxtwitter's API, which is an independent service. Operational risk sits with fxtwitter. |

---

## Risk Register

| ID | Risk | Likelihood | Impact | Mitigation |
|----|------|-----------|--------|------------|
| R-1 | fxtwitter service goes offline. | Medium | Low (feature silently unavailable) | Structured log + metric counter for fetch failures; no user-visible impact. |
| R-2 | fxtwitter changes its API response schema. | Low | Medium (embeds show incomplete data or errors) | JSON deserialization should tolerate missing fields gracefully. Monitor error rates. |
| R-3 | X changes `possibly_sensitive` logic making it unreliable. | Low | Low (`SensitiveOnly` can be disabled guild-wide) | Documented in guild admin help text. |
| R-4 | A guild uses the feature to re-share content that violates Discord ToS. | Low | High (Discord enforcement against the guild/bot) | Bot does not alter content; responsibility lies with the guild admin who enabled the feature. Bot operator documents this in feature setup instructions. |
| R-5 | High-volume guild floods fxtwitter with requests. | Low | Medium (rate-limited or banned by fxtwitter) | Per-guild sliding window rate limiter (phase 2); log 429 responses and back off. |
| R-6 | Discord changes embed limits or context menu availability. | Very Low | Medium | Tracked as part of general Discord API compatibility monitoring. |

---

## Compliance & Legal Considerations

### Content responsibility

The bot fetches and re-displays public tweet content. Guild admins enable the feature knowing their community's context. Discord's own Community Guidelines apply to the guild, not to the bot in isolation. The bot operator should include language in the feature documentation noting that guild admins are responsible for ensuring the output is appropriate for their community.

### Data minimisation

The feature does not persist tweet content, user data, or author information. All data fetched from fxtwitter is used only to construct a Discord embed and is immediately discarded. No GDPR/privacy obligations arise from this feature beyond what is already handled by the bot's existing consent and message-logging frameworks.

### Attribution

Every embed includes the original author's name and handle and a direct link to the source tweet. This satisfies reasonable attribution expectations without reproducing X's entire user profile data.

---

## Acceptance Criteria (Business Level)

These are high-level business acceptance criteria. Technical acceptance criteria appear in the unit test constraints document.

| ID | Criterion |
|----|-----------|
| BAC-1 | A guild admin can enable the feature with a single command and see confirmation. |
| BAC-2 | After enabling, a sensitive tweet link posted in a monitored channel produces a Discord embed containing the tweet text and at least one image (if the tweet has images) within 10 seconds on a stable connection. |
| BAC-3 | A non-sensitive tweet in a guild with `SensitiveOnly=true` produces no embed and no bot message. |
| BAC-4 | A guild admin can route output to a dedicated channel; embeds appear there, not in the source channel. |
| BAC-5 | Right-clicking a message containing a tweet URL and selecting "Fetch Tweet" produces an embed in the channel regardless of `IsEnabled` status. |
| BAC-6 | The bot produces no error messages, notifications, or channel messages when fxtwitter is unreachable. |
| BAC-7 | Disabling the feature immediately stops automatic previews (subsequent messages are not processed). |
| BAC-8 | All admin command responses are ephemeral. |
