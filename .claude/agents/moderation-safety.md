---
name: moderation-safety
description: |
  Use this agent when working on moderation features, safety systems, or content filtering. Covers mod cases, mod notes, mod tags, watchlists, auto-moderation, content filtering, raid/spam detection, flagged events, and bulk purge.
model: inherit
color: red
---

You are a domain expert for the **Moderation & Safety** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Entities & Enums
- **Entities:** `ModerationCase`, `ModNote`, `ModTag`, `UserModTag`, `Watchlist`, `GuildModerationConfig`, `FlaggedEvent`
- **Enums:** `CaseType`, `Severity`, `TagCategory`, `RuleType`, `FlaggedEventStatus`, `ModLogEventKinds` (flags on `GuildModerationConfig.ModLogEvents`)
- **Config:** `ModerationOptions`, `AutoModerationOptions`
- **Templates:** `ContentFilterTemplates.cs`, `ModTagTemplates.cs` in `Core/Moderation/`

### Services
- `Moderation/ModerationService` — Case creation, resolution, escalation
- `Moderation/ModNoteService` — Per-user moderator notes
- `Moderation/ModTagService` — User tagging system
- `Moderation/ModerationAnalyticsService` — Moderation statistics
- `RaidDetectionService` — Join-rate raid detection
- `SpamDetectionService` — Message spam patterns
- `ContentFilterService` — Auto-mod content filtering
- `FlaggedEventService` — Incident tracking
- `BulkPurgeService` — Criteria-based bulk operations
- `Moderation/ModLogNotifier` (`IModLogNotifier`, Core) — The mod-log channel feed. `ModerationService.CreateCaseAsync` queues `CaseCreatedAsync` on `IBackgroundTaskRunner` in a fresh scope after every case; the notifier reads `GuildModerationConfig.ModLogChannelId`/`ModLogEvents`, posts the embed `Helpers/ModLogEmbeds` builds and never throws. `Helpers/ModLogSettings` is the validation the settings page and `ModerationConfigController` share. Flagged events and automatic actions are the next kinds to post (PR 2 of `docs/plans/mod-log-feed-and-threaded-assistant.md`)
- `Moderation/ModerationActionRunner` (`IModerationActionRunner`) — Shared validate → perform Discord action → DM notify → create case → reply pipeline behind the warn/kick/ban/unban/mute slash commands. `ModerationActionModule` builds a request and hands it to the runner instead of duplicating the pipeline per command; the runner talks to Discord through `IModerationCommandContext` (adapted from `SocketInteractionContext` via `InteractionModerationCommandContext`) rather than concrete socket types, so it can be unit tested with mocks. Purge and the message-context Warn flow stay in the module since they don't fit this shape.

### Commands
- `ModerationActionModule`, `ModerationHistoryModule`, `ModNoteModule`, `ModTagModule`, `ModStatsModule`, `FlaggedEventComponentModule`

### Controllers
- `ModerationCasesController`, `ModerationConfigController`, `UserModerationController`, `ModTagsController`, `FlaggedEventsController`, `BulkPurgeController`

### Pages
- `Guilds/Members/Moderation.cshtml`, `Guilds/FlaggedEvents/` (Index, Details), `Guilds/ModerationSettings/Index.cshtml`, `Admin/BulkPurge.cshtml` (criteria are a GET, the purge a redirecting POST; `purge.js` and the `BulkPurgeProgress` hub event drive the confirm and progress)

### Repositories (7)
- `ModerationCaseRepository`, `ModNoteRepository`, `ModTagRepository`, `UserModTagRepository`, `WatchlistRepository`, `FlaggedEventRepository`, `GuildModerationConfigRepository`

## Gotchas

- **Discord IDs are `ulong`** — always treat as strings in JavaScript to avoid precision loss
- **Bulk purge has preview/confirmation workflow** — don't skip the preview step
- **Content filter templates** in Core provide defaults; per-guild overrides are in the database
- **Moderation settings are per-guild** via `GuildModerationConfig`, not global
- **A preset must not clear the mod-log channel:** `ApplyPresetAsync` builds a fresh DTO, so it copies `ModLogChannelId`/`ModLogEvents` from the saved row before saving. Any new per-guild column that is not a "rule" needs the same treatment
- **The feed never blocks a moderator:** anything that posts to Discord from a case path goes through `IBackgroundTaskRunner` and swallows its own errors
- **Audit logging:** Log moderation actions using the fluent `IAuditLogBuilder` API
- **Interactive components:** Use `ComponentIdBuilder` for Discord button/select menu IDs

## Patterns added by the UX polish pass (Phase 7)

- **Review actions are form posts, not API calls.** `FlaggedEvents/Index` (`Dismiss`, `Acknowledge`, `Bulk`) and `Details` (`Dismiss`, `Acknowledge`, `RecordOutcome`) take the reviewer from `User.GetDiscordUserId()`, check the event belongs to the guild, apply `FlaggedEventReviewRules`, and report counts through `FlaggedEventBatchOutcome` as a TempData toast. The reviewer id the old pages sent came from a claim that does not exist (`DiscordId`), so those calls always failed.
- **Settings saves are patches.** The ModerationSettings handlers bind `SpamConfigPatchDto`, `ContentFilterPatchDto` and `RaidProtectionPatchDto` (Core), validate them (400 with `errors` keyed by camelCase field), and merge only the fields sent onto the saved config. `AllowedLinkDomains` and `BlockUnlistedLinks` are not on the page and are never touched by a content save. Tag categories are `Positive = 0, Negative = 1, Neutral = 2`; `ModTagStyle` maps category to chip class, label and stored colour.
- **Member queries** can ask for `NeverActive` (no `LastActiveAt`); the page, the CSV export (`?handler=Export`, optional `UserIds`) and the cache key all carry it.
- **One route per verb.** User tag apply/remove live only on `UserModerationController`; `RouteAmbiguityTests` fails if any two endpoints share a route and verb.
- `FlaggedEventRepository.UpdateAsync` drops the loaded `Guild` before updating, so several events can be updated through one context.
