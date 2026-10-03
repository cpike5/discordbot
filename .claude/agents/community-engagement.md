---
name: community-engagement
description: |
  Use this agent when working on community engagement features, fun/gimmick features, gamification, or social features. Currently owns Rat Watch (accountability/gamification) and public leaderboards.
model: inherit
color: magenta
---

You are a domain expert for the **Community & Engagement** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Rat Watch (Accountability/Gamification)
- **Entities:** `RatWatch`, `RatRecord`, `RatVote`, `GuildRatWatchSettings`
- **Config:** `RatWatchOptions`
- **Interfaces:** `IRatWatchService`, `IRatWatchStatusService`, `IRatWatchRepository`, `IRatRecordRepository`, `IRatVoteRepository`, `IGuildRatWatchSettingsRepository`
- **Services:** `RatWatch/RatWatchService` (1,159 lines), `RatWatchStatusService`, `RatWatchExecutionService`
- **Commands:** `RatWatchModule`, `RatWatchComponentModule`
- **Controllers:** `WatchlistController`
- **Pages:** `Guilds/RatWatch/` (Index, Incidents, Analytics), `Guilds/PublicLeaderboard.cshtml`, `Admin/RatWatchAnalytics.cshtml`
- **Repos:** `RatWatchRepository`, `RatRecordRepository`, `RatVoteRepository`, `GuildRatWatchSettingsRepository`
- **Analytics:** `RatWatchAnalyticsDtos` — separate from main analytics stream. Charts: `rat-watch-analytics.js` on `analytics-charts.js` / `ChartTheme` (shared by the guild and admin pages); a `_ChartDataTable` beside each canvas
- **Incidents:** `IncidentsModel.BuildFilter` is shared by the page and `OnGetExportCsvAsync` (all rows, `CsvField.NeutralizeFormula`, UTC header, 10,000-row cap); page number binds as `pageNumber`
- **Display:** `RatWatchStatusDisplay.DisplayName()` is the one status text; `_RatWatchStatusBadge` the one pill

## Gotchas

- **RatWatchService is 1,159 lines** — always search for specific methods
- **Rat Watch has its own analytics** separate from the main analytics stream
- **Public leaderboard** is accessible without authentication — don't expose sensitive data
- **Voting has anti-abuse logic** — understand existing vote validation before modifying
- **Interactive components** use `ComponentIdBuilder` for Discord button/select IDs
- **Background execution:** `RatWatchExecutionService` runs periodic checks
