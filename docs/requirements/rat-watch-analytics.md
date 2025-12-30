# Rat Watch Analytics - Requirements Specification

**Date:** 2025-12-30
**Status:** Draft
**Related Epic:** #404 (Rat Watch)

---

## Executive Summary

Add comprehensive analytics capabilities to the Rat Watch feature, providing incident browsing, user metrics, time trends, and behavioral insights. The solution includes three tiers: admin/mod analytics (global and per-guild), and a public-facing leaderboard page for guild members.

---

## Problem Statement

Guild administrators and moderators need visibility into Rat Watch activity beyond the current management page. They want to:
- Browse and search historical incidents with rich filtering
- Track rat counts and accountability metrics by user
- Visualize trends over time to understand community behavior
- Access fun engagement stats for the community

---

## Target Users

| User Type | Access Level | Primary Use Cases |
|-----------|--------------|-------------------|
| **SuperAdmin/Admin** | Global + all guilds | Cross-guild analytics, system health monitoring |
| **Moderator** | Assigned guilds only | Guild-specific incident review, user behavior tracking |
| **Guild Member** | Public leaderboard only | View guild leaderboard, fun stats (if guild opts in) |

---

## Dashboards Overview

### 1. Global Analytics Dashboard

**URL:** `/Admin/RatWatch/Analytics`
**Authorization:** RequireSuperAdmin or RequireAdmin policy

**Purpose:** Cross-guild metrics for bot administrators.

**Features:**
- Total watches across all guilds
- Rats per guild comparison
- Global trends over time
- Most active guilds
- System-wide behavioral metrics

### 2. Guild Analytics Dashboard

**URL:** `/Guilds/RatWatch/{guildId}/Analytics`
**Authorization:** RequireAdmin or RequireModerator policy (guild-scoped)

**Purpose:** Deep-dive analytics for a specific guild.

**Features:**
- All features listed in Feature Specifications below
- Scoped to single guild
- Linked from main RatWatch management page

### 3. Public Leaderboard Page

**URL:** `/Guilds/{guildId}/Leaderboard`
**Authorization:** None (public URL), but guild must opt-in

**Purpose:** Fun, shareable leaderboard for guild members.

**Features:**
- Hall of Shame rankings
- Recent guilty verdicts
- Fun stats (streaks, vote margins, timing records)
- Shareable social-friendly URL

---

## Feature Specifications

### F1: Incident Browser

**Description:** Searchable, filterable list of all Rat Watch incidents.

**Filters:**
| Filter | Type | Options |
|--------|------|---------|
| Status | Multi-select | Pending, ClearedEarly, Voting, Guilty, NotGuilty, Expired, Cancelled |
| Date Range | Date picker | Start date, End date |
| Quick Presets | Buttons | Today, Last 7 Days, Last 30 Days |
| Accused User | Autocomplete | Search by username |
| Initiator User | Autocomplete | Search by username |
| Outcome | Dropdown | Guilty, Not Guilty, All |
| Vote Threshold | Number input | Min votes to include |
| Keyword | Text input | Search in custom messages |

**Saved Filter Presets:**
- Stored in browser localStorage
- User can save current filter as named preset
- Quick-select dropdown to apply saved presets

**Bulk Actions:**
- Export filtered results to CSV
- Cancel multiple pending watches (admin only)

**Incident Row Display:**
| Column | Content |
|--------|---------|
| Status | Badge with color-coded status |
| Accused | Username with avatar |
| Initiator | Username |
| Scheduled | Relative time + absolute on hover |
| Outcome | Vote tally (e.g., "5-2 Guilty") |
| Actions | View details, Cancel (if pending) |

**Incident Detail Modal:**
- Full incident information
- Link to original Discord message
- Vote breakdown (who voted what, if admin)
- Timeline of events (created, voting started, ended)

### F2: User Metrics

**Description:** Per-user statistics and rankings.

**Leaderboard View:**
| Rank | User | Rat Count | Accountability Score | Last Incident |
|------|------|-----------|---------------------|---------------|

**User Types:**
- **Most Watched:** Users put on watch most often
- **Most Active Accusers:** Users who create the most watches
- **Biggest Rats:** Users with most guilty verdicts

**Accountability Score:**
- Formula: `(EarlyCheckIns / TotalWatches) * 100`
- Displayed as percentage with color coding (green > 80%, yellow 50-80%, red < 50%)

**User Drill-Down Modal:**
Clicking a user opens a modal with:
- Total watches against them
- Guilty verdict count
- Early check-in count
- Accountability score
- Recent incidents list (last 10)
- Trend indicator (improving/declining)

### F3: Time Trends

**Description:** Visualizations of activity over time using Chart.js.

**Charts:**

1. **Watches Over Time** (Line chart)
   - X-axis: Date
   - Y-axis: Count
   - Series: Total watches, Guilty verdicts, Cleared early
   - Granularity: Daily for < 30 days, Weekly for > 30 days

2. **Activity Heatmap** (Grid visualization)
   - Rows: Days of week (Mon-Sun)
   - Columns: Hours of day (0-23)
   - Color intensity: Number of watches scheduled for that time
   - Purpose: Identify peak activity times

3. **Outcome Distribution** (Doughnut chart)
   - Segments: Guilty, Not Guilty, Cleared Early, Expired, Cancelled
   - Center: Total count

4. **Month-over-Month Comparison** (Bar chart)
   - Compare current period to previous period
   - Show percentage change

**Default Range:** Last 7 days

### F4: Behavioral Insights

**Description:** Derived metrics about community behavior.

**Metrics Cards:**

| Metric | Description | Calculation |
|--------|-------------|-------------|
| Early Check-in Rate | % of watches cleared before voting | `ClearedEarly / Total * 100` |
| Avg Voting Participation | Voters per watch | `TotalVotes / WatchesWithVoting` |
| Avg Vote Margin | How decisive verdicts are | `abs(GuiltyVotes - NotGuiltyVotes)` average |
| Most Common Duration | Popular scheduling times | Mode of time intervals |

**Fun Stats (for public leaderboard):**

| Stat | Description |
|------|-------------|
| Longest Guilty Streak | Most consecutive guilty verdicts (user) |
| Longest Clean Streak | Most watches cleared early in a row (user) |
| Biggest Landslide | Most lopsided vote ever (incident) |
| Closest Call | Smallest vote margin (incident) |
| Speed Demon | Fastest early check-in time (user + incident) |
| Last Second Larry | Check-in closest to deadline (user + incident) |

### F5: Summary Stats on Main Page

**Description:** Add summary statistics to existing `/Guilds/RatWatch/{guildId}` page.

**Stats Cards (4-column grid):**
1. **Total Watches** - All-time count with badge
2. **Active Watches** - Pending + Voting count
3. **Guilty Rate** - Percentage of completed watches resulting in guilty
4. **Early Check-in Rate** - Percentage cleared before voting

**Link:** "View Full Analytics" button to analytics page

### F6: Public Leaderboard Settings

**Description:** Guild opt-in for public leaderboard visibility.

**New Setting:** `GuildRatWatchSettings.PublicLeaderboardEnabled`
- Default: `false`
- Displayed in guild Rat Watch settings page
- Description: "Allow anyone with the link to view this guild's Rat Watch leaderboard"

**Privacy Considerations:**
- Only shows username (not user ID)
- No links to Discord profiles on public page
- No vote details (just tallies)
- Guild name visible

---

## Page Specifications

### P1: Guild Analytics Page

**Route:** `/Guilds/RatWatch/{guildId:long}/Analytics`
**File:** `src/DiscordBot.Bot/Pages/Guilds/RatWatchAnalytics.cshtml`
**Authorization:** `[Authorize(Policy = "RequireModeratorOrAbove")]`

**Layout:**
```
+--------------------------------------------------+
| Header: "Rat Watch Analytics" + Guild Name       |
| [Back to Settings] [View Incidents]              |
+--------------------------------------------------+
| Filter Panel (collapsible)                       |
| - Date range, Quick presets, User filter         |
+--------------------------------------------------+
| Summary Stats Cards (4 columns)                  |
| [Total] [Guilty Rate] [Check-in Rate] [Avg Vote] |
+--------------------------------------------------+
| Charts Grid (2 columns)                          |
| [Watches Over Time]  [Outcome Distribution]      |
| [Activity Heatmap]   [Top Users]                 |
+--------------------------------------------------+
| Behavioral Insights Section                      |
| [Metric Cards Row]                               |
+--------------------------------------------------+
| User Leaderboards (tabs)                         |
| [Most Watched] [Top Accusers] [Biggest Rats]     |
+--------------------------------------------------+
```

### P2: Guild Incidents Page

**Route:** `/Guilds/RatWatch/{guildId:long}/Incidents`
**File:** `src/DiscordBot.Bot/Pages/Guilds/RatWatchIncidents.cshtml`
**Authorization:** `[Authorize(Policy = "RequireModeratorOrAbove")]`

**Layout:**
```
+--------------------------------------------------+
| Header: "Rat Watch Incidents" + Guild Name       |
| [Back to Analytics] [Export CSV]                 |
+--------------------------------------------------+
| Filter Panel (collapsible)                       |
| - Full filter set with saved presets             |
+--------------------------------------------------+
| Results Summary                                  |
| "Showing 1-25 of 142 incidents"                  |
+--------------------------------------------------+
| Incidents Table (paginated)                      |
| [Status] [Accused] [Initiator] [Scheduled] ...   |
+--------------------------------------------------+
| Pagination Controls                              |
+--------------------------------------------------+
```

### P3: Global Analytics Page

**Route:** `/Admin/RatWatch/Analytics`
**File:** `src/DiscordBot.Bot/Pages/Admin/RatWatchAnalytics.cshtml`
**Authorization:** `[Authorize(Policy = "RequireAdmin")]`

**Layout:**
```
+--------------------------------------------------+
| Header: "Global Rat Watch Analytics"             |
+--------------------------------------------------+
| Global Stats Cards (4 columns)                   |
| [Total Guilds] [Total Watches] [Active] [Today]  |
+--------------------------------------------------+
| Guild Filter Dropdown                            |
+--------------------------------------------------+
| Charts Grid (2 columns)                          |
| [Activity by Guild]   [Global Trends]            |
| [Outcome Distribution] [Most Active Guilds]      |
+--------------------------------------------------+
| Per-Guild Summary Table                          |
| [Guild] [Watches] [Guilty] [Rate] [Actions]      |
+--------------------------------------------------+
```

### P4: Public Leaderboard Page

**Route:** `/Guilds/{guildId:long}/Leaderboard`
**File:** `src/DiscordBot.Bot/Pages/Guilds/PublicLeaderboard.cshtml`
**Authorization:** None (public)

**Access Control:**
- Check `GuildRatWatchSettings.PublicLeaderboardEnabled`
- If disabled, show "This leaderboard is not public" message

**Layout:**
```
+--------------------------------------------------+
| Header: "Hall of Shame" + Guild Name             |
| [Guild Icon]                                     |
+--------------------------------------------------+
| Fun Stats Highlight Cards                        |
| [Longest Streak] [Biggest Landslide] [Speed]     |
+--------------------------------------------------+
| Leaderboard Table                                |
| [Rank] [User] [Rat Count] [Last Incident]        |
| (Top 25)                                         |
+--------------------------------------------------+
| Recent Incidents                                 |
| [Date] [User] [Outcome] [Vote Tally]             |
| (Last 10)                                        |
+--------------------------------------------------+
| Footer: "Powered by [Bot Name]"                  |
+--------------------------------------------------+
```

**Styling:**
- Dark theme friendly (matches Discord aesthetic)
- Responsive for mobile sharing
- Fun, playful design with rat emoji accents

---

## Data Requirements

### New DTOs

```csharp
// Analytics summary
public record RatWatchAnalyticsSummaryDto
{
    public int TotalWatches { get; init; }
    public int ActiveWatches { get; init; }
    public int GuiltyCount { get; init; }
    public int ClearedEarlyCount { get; init; }
    public double GuiltyRate { get; init; }
    public double EarlyCheckInRate { get; init; }
    public double AvgVotingParticipation { get; init; }
    public double AvgVoteMargin { get; init; }
}

// Time series data point
public record RatWatchTimeSeriesDto
{
    public DateTime Date { get; init; }
    public int TotalCount { get; init; }
    public int GuiltyCount { get; init; }
    public int ClearedCount { get; init; }
}

// User metrics
public record RatWatchUserMetricsDto
{
    public ulong UserId { get; init; }
    public string Username { get; init; }
    public int WatchesAgainst { get; init; }
    public int GuiltyCount { get; init; }
    public int EarlyCheckInCount { get; init; }
    public double AccountabilityScore { get; init; }
    public DateTime? LastIncidentDate { get; init; }
}

// Fun stats
public record RatWatchFunStatsDto
{
    public UserStreakDto? LongestGuiltyStreak { get; init; }
    public UserStreakDto? LongestCleanStreak { get; init; }
    public IncidentHighlightDto? BiggestLandslide { get; init; }
    public IncidentHighlightDto? ClosestCall { get; init; }
    public IncidentHighlightDto? FastestCheckIn { get; init; }
    public IncidentHighlightDto? LatestCheckIn { get; init; }
}

public record UserStreakDto
{
    public ulong UserId { get; init; }
    public string Username { get; init; }
    public int StreakCount { get; init; }
}

public record IncidentHighlightDto
{
    public Guid WatchId { get; init; }
    public ulong UserId { get; init; }
    public string Username { get; init; }
    public string Description { get; init; } // e.g., "10-1 Guilty" or "Cleared in 23 seconds"
    public DateTime Date { get; init; }
}
```

### New Repository Methods

```csharp
// IRatWatchRepository additions
Task<RatWatchAnalyticsSummaryDto> GetAnalyticsSummaryAsync(
    ulong? guildId,
    DateTime? startDate,
    DateTime? endDate,
    CancellationToken ct = default);

Task<IEnumerable<RatWatchTimeSeriesDto>> GetTimeSeriesAsync(
    ulong? guildId,
    DateTime startDate,
    DateTime endDate,
    CancellationToken ct = default);

Task<IEnumerable<(int DayOfWeek, int Hour, int Count)>> GetActivityHeatmapAsync(
    ulong guildId,
    DateTime startDate,
    DateTime endDate,
    CancellationToken ct = default);

// IRatRecordRepository additions
Task<IEnumerable<RatWatchUserMetricsDto>> GetUserMetricsAsync(
    ulong guildId,
    string sortBy, // "watched", "accusers", "guilty"
    int limit,
    CancellationToken ct = default);

Task<RatWatchFunStatsDto> GetFunStatsAsync(
    ulong guildId,
    CancellationToken ct = default);
```

### Database Changes

**New Column:** `GuildRatWatchSettings.PublicLeaderboardEnabled`
- Type: `bool`
- Default: `false`

**Migration:** `AddPublicLeaderboardSetting`

---

## Technical Specifications

### Chart.js Integration

- Use Chart.js 4.x (already in project)
- Create `rat-watch-analytics.js` for chart initialization
- Embed data via `<script type="application/json">` pattern (matches Command Analytics)
- Support dark mode via CSS variables

### Filter Preset Storage

```javascript
// localStorage structure
{
  "ratwatch-filter-presets": [
    {
      "name": "Recent Guilty",
      "filters": {
        "startDate": "-7d",
        "status": ["Guilty"],
        "minVotes": 3
      }
    }
  ]
}
```

### Export to CSV

- Client-side generation using JavaScript
- Include: Date, Accused, Initiator, Status, Votes For, Votes Against, Custom Message
- Filename: `ratwatch-incidents-{guildId}-{date}.csv`

---

## Navigation Updates

### Sidebar Changes
None required - pages accessed from Guild context.

### Guild Details Page
Add "Rat Watch" section with:
- Link to `/Guilds/RatWatch/{id}` (Settings)
- Link to `/Guilds/RatWatch/{id}/Analytics` (Analytics)

### RatWatch Settings Page
Add navigation tabs:
- Settings (current)
- Analytics (new)
- Incidents (new)

Add summary stats cards at top.

### Admin Sidebar
Add under "Analytics" section:
- "Rat Watch Analytics" link to `/Admin/RatWatch/Analytics`

---

## Files to Create

| Layer | File | Purpose |
|-------|------|---------|
| Core | `DTOs/RatWatchAnalyticsDtos.cs` | Analytics DTOs |
| Infrastructure | `Repositories/RatWatchRepository.cs` | Add analytics query methods |
| Infrastructure | `Repositories/RatRecordRepository.cs` | Add metrics query methods |
| Bot | `Pages/Guilds/RatWatchAnalytics.cshtml` | Guild analytics page |
| Bot | `Pages/Guilds/RatWatchAnalytics.cshtml.cs` | Guild analytics PageModel |
| Bot | `Pages/Guilds/RatWatchIncidents.cshtml` | Incidents browser page |
| Bot | `Pages/Guilds/RatWatchIncidents.cshtml.cs` | Incidents browser PageModel |
| Bot | `Pages/Admin/RatWatchAnalytics.cshtml` | Global analytics page |
| Bot | `Pages/Admin/RatWatchAnalytics.cshtml.cs` | Global analytics PageModel |
| Bot | `Pages/Guilds/PublicLeaderboard.cshtml` | Public leaderboard page |
| Bot | `Pages/Guilds/PublicLeaderboard.cshtml.cs` | Public leaderboard PageModel |
| Bot | `ViewModels/Pages/RatWatchAnalyticsViewModel.cs` | Analytics view model |
| Bot | `ViewModels/Pages/RatWatchIncidentsViewModel.cs` | Incidents view model |
| Bot | `wwwroot/js/rat-watch-analytics.js` | Chart.js initialization |

## Files to Modify

| File | Changes |
|------|---------|
| `GuildRatWatchSettings.cs` | Add `PublicLeaderboardEnabled` property |
| `GuildRatWatchSettingsConfiguration.cs` | Configure new column |
| `IRatWatchRepository.cs` | Add analytics query method signatures |
| `IRatRecordRepository.cs` | Add metrics query method signatures |
| `Pages/Guilds/RatWatch.cshtml` | Add summary stats, navigation tabs |
| `Pages/Shared/_Sidebar.cshtml` | Add global analytics link (Admin section) |
| `CLAUDE.md` | Add new routes to documentation |

---

## Open Questions

1. **Caching:** Should analytics queries be cached? If so, what invalidation strategy?
2. **Real-time updates:** Should the analytics page auto-refresh, or only on manual refresh?
3. **Export formats:** CSV only, or also JSON/Excel?
4. **Mobile optimization:** Priority for responsive design on public leaderboard?

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Chart.js for visualizations | Already in tech stack, consistent with Command Analytics |
| localStorage for filter presets | Simpler than database, no auth needed |
| 7-day default date range | Focuses on recent activity, faster queries |
| Opt-in public leaderboards | Respects guild privacy preferences |
| Modal for user drill-down | Keeps users on page, faster UX |
| Separate analytics page + summary on main | Best of both worlds - quick glance and deep dive |

---

## Recommended Next Steps

1. **Design Phase:** Create HTML prototypes for each page layout
2. **Database:** Add migration for `PublicLeaderboardEnabled`
3. **Backend:** Implement repository analytics methods
4. **Frontend:** Build pages starting with Guild Analytics
5. **Testing:** Unit tests for analytics calculations
6. **Documentation:** Update feature docs with analytics usage

---

*End of Requirements Specification*
