# Implementation Plan: Issue #348 - Dashboard Real-time Updates Not Functioning

## 1. Requirement Summary

The SignalR backend infrastructure broadcasts events (`BotStatusUpdated`, `CommandExecuted`, `GuildActivity`, `StatsUpdated`) but the UI components do not receive updates because they lack the necessary data attributes and template elements that the JavaScript event handlers (`dashboard-realtime.js`) expect.

**Goal**: Bridge the gap between the existing JavaScript handlers and the Razor components by adding required DOM elements, data attributes, and templates.

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Status |
|-----------|----------|--------|
| `DashboardHub.cs` | Backend SignalR Hub | Working - broadcasts events |
| `DashboardUpdateService` | Backend service | Working - triggers broadcasts |
| `dashboard-hub.js` | `/wwwroot/js/` | Working - connection manager |
| `dashboard-realtime.js` | `/wwwroot/js/` | Working - event handlers exist but can't find DOM targets |
| `_ActivityFeedTimeline.cshtml` | Razor component | Missing template element and data attributes |
| `_BotStatusBanner.cshtml` | Razor component | Has some data attributes but missing `[data-bot-status-card]` |
| `_HeroMetricCard.cshtml` | Razor component | Missing data attributes for real-time updates |
| `_Navbar.cshtml` | Layout partial | Missing connection status indicator |

### 2.2 JavaScript Handler Expectations

The `dashboard-realtime.js` file (lines 54-66) caches these elements:

```javascript
elements = {
    connectionStatus: document.getElementById('connection-status'),
    botStatusCard: document.querySelector('[data-bot-status-card]'),
    activityFeed: document.getElementById('activity-feed'),
    activityItemTemplate: document.getElementById('activity-item-template'),
    pauseBtn: document.getElementById('pause-feed-btn'),
    pauseBtnText: document.getElementById('pause-btn-text'),
    pauseIcon: document.getElementById('pause-icon'),
    playIcon: document.getElementById('play-icon'),
    pausedIndicator: document.getElementById('feed-paused-indicator'),
    emptyState: document.getElementById('empty-state')
};
```

### 2.3 Data Attributes Expected by Handlers

**Bot Status Banner** (lines 94-100):
- `[data-bot-status-card]` - card container (root element)
- `[data-connection-state]` - connection state text
- `[data-latency]` - latency value
- `[data-uptime]` - uptime value
- `[data-guild-count]` - guild count value
- `[data-last-updated]` - last updated timestamp

**Hero Metrics Cards** (lines 145-158):
- `[data-total-commands]` - total commands count
- `[data-active-users]` - active users count
- `[data-messages-processed]` - messages processed count

**Activity Feed** (lines 180-213):
- `id="activity-feed"` - container for activity items
- `id="activity-item-template"` - `<template>` element with activity item structure
- Activity item structure must include:
  - `.activity-item` - item container
  - `.activity-timestamp` - timestamp element
  - `.activity-icon` - icon element
  - `.activity-description` - description element (accepts HTML)
  - `.activity-guild` - guild name element

**Connection Status** (lines 160-178):
- `id="connection-status"` - status indicator container
- `data-state` attribute for state management
- `.connection-text` - text element inside

### 2.4 CSS Animation Support

The CSS animations already exist in `site.css` (lines 804-846):
- `.card-update-pulse` - pulse animation for card updates
- `.activity-item-enter` - slide-in animation for new activity items
- Reduced motion preferences are respected

---

## 3. Subagent Task Plan

### 3.1 dotnet-specialist Tasks

#### Task 1: Update `_BotStatusBanner.cshtml`
**File**: `src/DiscordBot.Bot/Pages/Shared/Components/_BotStatusBanner.cshtml`

**Current State**: Has `data-bot-status-banner` but needs additional attributes.

**Changes Required**:
1. Add `data-bot-status-card` to the root container (line 17 currently has `data-bot-status-banner`)
2. Add `[data-connection-state]` span inside the status badge
3. Add `[data-guild-count]` to the server count in summary text
4. Ensure `[data-uptime]` and `[data-latency]` are present (currently have `data-uptime` and `data-latency`)
5. Add `[data-last-updated]` element (hidden, used for tracking)

**Specific Changes**:
```html
<!-- Line 17: Change from -->
<div class="@bannerClass mb-8" data-bot-status-banner data-is-online="...">
<!-- To -->
<div class="@bannerClass mb-8" data-bot-status-card data-is-online="...">
```

Add connection state and guild count data attributes to existing elements.

#### Task 2: Update `_HeroMetricCard.cshtml`
**File**: `src/DiscordBot.Bot/Pages/Shared/Components/_HeroMetricCard.cshtml`

**Changes Required**:
1. Add support for `DataAttribute` property in the ViewModel
2. Add the data attribute to the value element based on card type

**ViewModel Update** (`HeroMetricCardViewModel.cs`):
```csharp
/// <summary>
/// Data attribute name for real-time updates (e.g., "data-total-commands").
/// </summary>
public string? DataAttribute { get; init; }
```

**Template Update** (line 73):
```html
<!-- Change from -->
<p class="mt-2 text-3xl font-bold text-text-primary">@Model.Value</p>
<!-- To -->
<p class="mt-2 text-3xl font-bold text-text-primary" @(Model.DataAttribute != null ? Model.DataAttribute : "")>@Model.Value</p>
```

#### Task 3: Update `_ActivityFeedTimeline.cshtml`
**File**: `src/DiscordBot.Bot/Pages/Shared/Components/_ActivityFeedTimeline.cshtml`

**Changes Required**:
1. Add `id="activity-feed"` to the timeline container
2. Add `<template id="activity-item-template">` element with required structure
3. Add `id="empty-state"` to the empty state container
4. Add pause/resume button with required IDs
5. Add paused indicator element

**Template Element Structure** (insert before the timeline div):
```html
<template id="activity-item-template">
    <div class="activity-item">
        <div class="bg-bg-primary/50 rounded-lg p-3">
            <div class="flex items-center gap-2 mb-1">
                <span class="activity-icon text-sm"></span>
                <span class="activity-timestamp text-xs text-text-tertiary font-mono"></span>
            </div>
            <p class="text-sm text-text-primary activity-description"></p>
            <p class="text-xs text-text-tertiary mt-1 activity-guild"></p>
        </div>
    </div>
</template>
```

**Pause Button Structure** (add to header):
```html
<button
    id="pause-feed-btn"
    type="button"
    class="btn-toggle"
    aria-pressed="false"
    aria-label="Pause activity feed"
>
    <svg id="pause-icon" class="w-4 h-4" ...><!-- pause icon --></svg>
    <svg id="play-icon" class="w-4 h-4 hidden" ...><!-- play icon --></svg>
    <span id="pause-btn-text">Pause</span>
</button>
```

**Paused Indicator** (add after pause button):
```html
<span id="feed-paused-indicator" class="hidden text-xs text-warning font-medium">
    Paused
</span>
```

#### Task 4: Update `_Navbar.cshtml`
**File**: `src/DiscordBot.Bot/Pages/Shared/_Navbar.cshtml`

**Changes Required**:
Add a connection status indicator element near the bot status indicator (lines 68-72).

**Add Connection Status Element**:
```html
<!-- Connection Status Indicator (visible when SignalR is active) -->
<div id="connection-status"
     class="hidden lg:flex items-center gap-1.5 px-2 py-1 rounded-full text-xs font-medium"
     data-state="disconnected">
    <span class="connection-dot w-2 h-2 rounded-full"></span>
    <span class="connection-text">Disconnected</span>
</div>
```

Add CSS for connection states if not already present (verify in `site.css`).

#### Task 5: Update `Index.cshtml.cs` PageModel
**File**: `src/DiscordBot.Bot/Pages/Index.cshtml.cs`

**Changes Required**:
Update the HeroMetrics initialization to include DataAttribute values:

```csharp
// In the method that creates HeroMetrics
new HeroMetricCardViewModel
{
    Title = "Commands Today",
    DataAttribute = "data-total-commands",
    // ... other properties
},
new HeroMetricCardViewModel
{
    Title = "Active Users",
    DataAttribute = "data-active-users",
    // ... other properties
}
```

### 3.2 design-specialist Tasks

#### Task 1: Connection Status Indicator Styling
**File**: `src/DiscordBot.Bot/wwwroot/css/site.css`

Verify and enhance connection status CSS if needed:
```css
/* Connection Status States */
#connection-status[data-state="connected"] {
    @apply bg-success/10 text-success;
}
#connection-status[data-state="connected"] .connection-dot {
    @apply bg-success;
}
#connection-status[data-state="connecting"],
#connection-status[data-state="reconnecting"] {
    @apply bg-warning/10 text-warning;
}
#connection-status[data-state="connecting"] .connection-dot,
#connection-status[data-state="reconnecting"] .connection-dot {
    @apply bg-warning;
    animation: connection-blink 1s ease-in-out infinite;
}
#connection-status[data-state="disconnected"] {
    @apply bg-error/10 text-error;
}
#connection-status[data-state="disconnected"] .connection-dot {
    @apply bg-error;
}
```

### 3.3 docs-writer Tasks

#### Task 1: Update Documentation
**File**: `docs/articles/dashboard-realtime.md` (create if not exists)

Document the real-time update system:
- SignalR hub events
- Required data attributes
- JavaScript API (`DashboardRealtime.pause()`, `DashboardRealtime.resume()`)
- Troubleshooting guide

---

## 4. Timeline / Dependency Map

```
Phase 1: ViewModel Updates (Parallel)
├── Update HeroMetricCardViewModel.cs (add DataAttribute property)
└── No other dependencies

Phase 2: Razor Component Updates (Sequential after Phase 1)
├── Update _HeroMetricCard.cshtml (depends on ViewModel)
├── Update _BotStatusBanner.cshtml (independent)
├── Update _ActivityFeedTimeline.cshtml (independent)
└── Update _Navbar.cshtml (independent)

Phase 3: PageModel Updates (After Phase 2)
└── Update Index.cshtml.cs (add DataAttribute values to HeroMetrics)

Phase 4: CSS Verification (Parallel with Phase 2-3)
└── Verify/add connection status CSS in site.css

Phase 5: Testing & Documentation (After all phases)
├── Manual testing of real-time updates
└── Create/update documentation
```

**Estimated Effort**:
- Phase 1: 15 minutes
- Phase 2: 45 minutes
- Phase 3: 15 minutes
- Phase 4: 15 minutes
- Phase 5: 30 minutes
- **Total**: ~2 hours

---

## 5. Acceptance Criteria

### 5.1 Activity Feed Timeline
- [ ] Activity feed has `id="activity-feed"` on the container
- [ ] Template element `id="activity-item-template"` exists with correct structure
- [ ] Empty state has `id="empty-state"`
- [ ] Pause/Resume button works with correct IDs
- [ ] Paused indicator shows when feed is paused
- [ ] New items animate in from top when commands are executed
- [ ] Feed limits to 15 items maximum

### 5.2 Bot Status Banner
- [ ] Root element has `data-bot-status-card` attribute
- [ ] Connection state, latency, uptime, and guild count update in real-time
- [ ] Card shows pulse animation when updated

### 5.3 Hero Metrics Cards
- [ ] "Commands Today" card has `data-total-commands` attribute on value
- [ ] "Active Users" card has `data-active-users` attribute on value
- [ ] Values update when `StatsUpdated` event is received

### 5.4 Connection Status Indicator
- [ ] Element with `id="connection-status"` exists in navbar
- [ ] Shows "Connected" (green) when SignalR is connected
- [ ] Shows "Connecting/Reconnecting" (yellow) during connection attempts
- [ ] Shows "Disconnected" (red) when connection fails
- [ ] Text updates via `.connection-text` class

### 5.5 General
- [ ] No console errors related to missing elements
- [ ] Animations respect `prefers-reduced-motion` setting
- [ ] Graceful degradation when SignalR is unavailable
- [ ] All timestamps display in user's local timezone

---

## 6. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing static rendering | High | Ensure all data attributes are additive, don't remove existing attributes |
| JavaScript errors if elements missing | Medium | Add null checks in JS (already present in `dashboard-realtime.js`) |
| Template element browser compatibility | Low | `<template>` is supported in all modern browsers |
| CSS specificity conflicts | Low | Use ID selectors for connection status to ensure priority |
| SignalR connection failures | Medium | Keep existing fallback behavior (page shows static data on load) |

---

## 7. Navigation Integration Checklist

**Not applicable** - This issue does not introduce new pages. It only modifies existing dashboard components.

---

## 8. Date/Time Handling

- Activity feed timestamps are received from the server in UTC
- The `dashboard-realtime.js` `formatTimestamp()` function (line 269) converts to local time
- Display format: `HH:MM:SS` (24-hour format)
- Relative timestamps ("2 min ago") are calculated client-side

---

## 9. File Reference Summary

### Files to Modify

| File | Type | Changes |
|------|------|---------|
| `src/DiscordBot.Bot/ViewModels/Components/HeroMetricCardViewModel.cs` | ViewModel | Add `DataAttribute` property |
| `src/DiscordBot.Bot/Pages/Shared/Components/_HeroMetricCard.cshtml` | Razor | Add data attribute rendering |
| `src/DiscordBot.Bot/Pages/Shared/Components/_BotStatusBanner.cshtml` | Razor | Change `data-bot-status-banner` to `data-bot-status-card`, add `data-connection-state` |
| `src/DiscordBot.Bot/Pages/Shared/Components/_ActivityFeedTimeline.cshtml` | Razor | Add template, IDs, pause button, paused indicator |
| `src/DiscordBot.Bot/Pages/Shared/_Navbar.cshtml` | Razor | Add connection status indicator |
| `src/DiscordBot.Bot/Pages/Index.cshtml.cs` | PageModel | Add DataAttribute values to HeroMetrics |
| `src/DiscordBot.Bot/wwwroot/css/site.css` | CSS | Verify/add connection status styles |

### Reference Files (No Changes Needed)

| File | Purpose |
|------|---------|
| `src/DiscordBot.Bot/wwwroot/js/dashboard-hub.js` | SignalR connection manager (working) |
| `src/DiscordBot.Bot/wwwroot/js/dashboard-realtime.js` | Event handlers (working, defines expected DOM structure) |
| `docs/prototypes/features/dashboard-redesign/dashboard.html` | Reference prototype |
| `src/DiscordBot.Bot/Pages/Index.cshtml` | Dashboard page (verify script includes) |

---

## 10. Implementation Notes

### 10.1 Backward Compatibility
All changes are additive. The components will continue to work for static server-side rendering even if JavaScript fails to load.

### 10.2 Testing Approach
1. Start the bot locally
2. Open browser developer tools console
3. Navigate to dashboard
4. Verify "[DashboardRealtime] Initializing..." message appears
5. Execute a slash command in Discord
6. Verify activity appears in the feed
7. Test pause/resume functionality
8. Test connection status indicator by stopping/starting the bot

### 10.3 Debug Logging
The JavaScript handlers include console logging:
- `[DashboardHub]` prefix for connection events
- `[DashboardRealtime]` prefix for update events

Enable these logs to troubleshoot any remaining issues.
