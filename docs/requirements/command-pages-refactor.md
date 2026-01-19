# Requirements: Command Pages Refactoring

## Problem Statement
The command-related pages (/Commands, /CommandLogs, /CommandLogs/Analytics) currently lack visual cohesion and consistent navigation. Users must navigate between separate pages using browser back/forward buttons, and there's no unified entry point or shared filtering between related pages. This creates a disjointed experience compared to the polished Guild pages.

## Primary Purpose
Unify the three command pages into a single cohesive interface with tabbed navigation, shared date filtering, and seamless AJAX interactions - creating a professional admin experience that matches the quality of the Guild pages.

## Target Users
- **SuperAdmin**: Full access to all command management features
- **Admin**: Access to command logs and analytics for monitoring
- **Moderator**: May have read-only access to command logs

## Core Features (MVP)

### 1. Unified Layout with Tabbed Navigation
- **Hub Page**: `/Commands` becomes the main container page
- **Three Tabs**:
  - **Command List**: Shows all registered commands and modules (current /Commands content)
  - **Execution Logs**: Command execution history with filtering (current /CommandLogs content)
  - **Analytics**: Charts and statistics (current /CommandLogs/Analytics content)
- **Navigation Mode**: In-page tabs with URL hash persistence (#command-list, #execution-logs, #analytics)
- **Tab Styling**: Match Guild Details tab panel component (TabPanelViewModel)
- **No Badge Counts**: Keep tabs clean without count badges

### 2. Unified Header & Breadcrumb
- **Page Title**: "Commands" (static)
- **Subtitle**: Dynamic based on active tab:
  - Command List tab: "All registered slash commands"
  - Execution Logs tab: "Command execution history"
  - Analytics tab: "Usage statistics and performance"
- **Breadcrumb Navigation**: Dynamic path showing active tab
  - Command List: Home / Commands / Command List
  - Execution Logs: Home / Commands / Execution Logs
  - Analytics: Home / Commands / Analytics
- **Header Component**: Use similar structure to GuildHeaderViewModel
- **Breadcrumb Component**: Adapt GuildBreadcrumbViewModel pattern

### 3. Shared Date Range Filter Component
- **Applies To**: Execution Logs and Analytics tabs only (not Command List)
- **Filter Fields**:
  - Start Date (date picker)
  - End Date (date picker)
  - Guild/Server dropdown (All Servers or specific guild)
  - Quick Date Presets: Today | Last 7 Days | Last 30 Days
- **Default Behavior**: Last 7 days when first loading page
- **Persistence**: Filter values preserved via URL parameters when switching between Logs and Analytics tabs
- **Visual Design**: Collapsible filter panel (same as current implementation)
- **Active Filter Badge**: Show count of active filters in filter panel header

### 4. Execution Logs Tab - Additional Filters
In addition to shared date/guild filters:
- Search input (commands, users, servers)
- Command Name filter (autocomplete)
- Status filter (Success/Failed dropdown)
- "Clear Filters" button when filters are active
- Export CSV button (preserves current filters)

### 5. AJAX Interactions (Seamless UX)
- **Filter Form Submissions**: Apply filters without page reload
  - Update results dynamically
  - Update URL parameters
  - Show loading state during fetch
- **Pagination**: Load next/previous pages without reload
  - Update table/list content
  - Update URL with page number
  - Maintain scroll position or scroll to top
- **Tab Switching**: Content loads dynamically on first visit
  - Already handled by TabPanel component
  - Preserve tab state in URL hash
- **Module Collapse/Expand**: Already client-side, keep as-is

### 6. Command Log Details as Modal
- **Current Behavior**: Separate page at `/CommandLogs/Details/{id}`
- **New Behavior**: Open in modal/overlay when clicking "View" from logs table
- **Modal Content**:
  - Full command log details
  - Timestamp, user, guild, parameters, response time
  - Error messages if failed
  - Stack traces if applicable
- **Navigation**: Close modal returns to Logs tab with filters intact
- **URL**: Optional - update hash when modal opens (#execution-logs/details/{id})
- **Accessibility**: ESC key closes, focus trap, proper ARIA labels

### 7. Visual Consistency with Guild Pages
- **Spacing/Padding**: Match Guild page container widths, padding, vertical spacing
- **Card Styles**: Use same bg-bg-secondary, border-border-primary, rounded-lg
- **Color Scheme**:
  - Accent colors (accent-blue, accent-orange)
  - Status colors (success, warning, error, info)
  - Text hierarchy (text-primary, text-secondary, text-tertiary)
- **Empty States**: Use EmptyStateViewModel component
- **Badges**: Use BadgeViewModel component with consistent variants
- **Buttons**: Use btn-primary, btn-secondary classes
- **Typography**: Match heading sizes and font weights

## Technical Context

### Required Technologies
- **Backend**: ASP.NET Core Razor Pages (existing)
- **Frontend**: Vanilla JavaScript + Tailwind CSS (no framework requirement)
- **Components**: Existing ViewModels in `DiscordBot.Bot.ViewModels.Components`
- **Patterns**: Follow existing Guild page patterns

### Key Files to Reference
- [src\DiscordBot.Bot\Pages\Shared\_GuildLayout.cshtml](../../src/DiscordBot.Bot/Pages/Shared/_GuildLayout.cshtml) - Layout structure
- [src\DiscordBot.Bot\Pages\Shared\Components\_GuildBreadcrumb.cshtml](../../src/DiscordBot.Bot/Pages/Shared/Components/_GuildBreadcrumb.cshtml)
- [src\DiscordBot.Bot\Pages\Shared\Components\_GuildHeader.cshtml](../../src/DiscordBot.Bot/Pages/Shared/Components/_GuildHeader.cshtml)
- [src\DiscordBot.Bot\Pages\Shared\Components\_GuildNavBar.cshtml](../../src/DiscordBot.Bot/Pages/Shared/Components/_GuildNavBar.cshtml)
- [src\DiscordBot.Bot\Pages\Shared\Components\_TabPanel.cshtml](../../src/DiscordBot.Bot/Pages/Shared/Components/_TabPanel.cshtml) - Tab navigation
- [src\DiscordBot.Bot\Pages\Guilds\Details.cshtml](../../src/DiscordBot.Bot/Pages/Guilds/Details.cshtml) - In-page tabs example
- [src\DiscordBot.Bot\ViewModels\Components\TabPanelViewModel.cs](../../src/DiscordBot.Bot/ViewModels/Components/TabPanelViewModel.cs)

### Components to Create/Adapt
- **CommandBreadcrumbViewModel** - Adapt from GuildBreadcrumbViewModel
- **CommandHeaderViewModel** - Adapt from GuildHeaderViewModel
- **Shared Date Range Filter Partial** - Extract common filter UI
- **Command Log Details Modal** - New modal component

## Design Preferences

### Look and Feel
- Clean, professional admin interface
- Dark mode compatible (using design tokens)
- Responsive (mobile, tablet, desktop)
- Consistent with existing Guild pages aesthetic

### Reference Pages
- Guild Details page (`/Guilds/Details/{id}`) - Tab navigation pattern
- Guild pages - Overall styling and spacing
- Performance Dashboard (`/Admin/Performance`) - Tab-based navigation reference

## Data & Integration

### Data Sources
- Command metadata (from `InteractionService`)
- Command logs (from `CommandLog` entity)
- Analytics data (aggregated from `CommandLog`)
- Guild list (for filter dropdown)
- Available commands (for autocomplete)

### API Endpoints Needed
- `GET /api/commands/list` - Fetch command list (for tab content)
- `GET /api/commands/logs` - Fetch filtered command logs (AJAX pagination)
- `GET /api/commands/analytics` - Fetch analytics data with date/guild filters
- `GET /api/commands/log-details/{id}` - Fetch single log entry for modal
- `GET /api/autocomplete/commands` - Command name autocomplete (exists)

## Constraints

### Performance
- AJAX requests should respond within 500ms for good UX
- Pagination should load quickly (consider page size of 25-50 items)
- Tab content lazy-loaded on first visit

### Browser Compatibility
- Modern browsers (last 2 versions of Chrome, Firefox, Safari, Edge)
- Graceful degradation if JavaScript disabled (fall back to full page loads)

### Authorization
- Maintain existing authorization policies
- Commands page: Admin+ role required
- Logs/Analytics: Admin+ or appropriate guild access

## Future Features (Out of Current Scope)

### Phase 2 Enhancements
- Real-time command log updates via SignalR
- Command execution graphs/charts on Analytics tab
- Favorite/pin frequently used commands
- Export analytics as PDF report
- Command scheduling interface
- Bulk command operations (enable/disable multiple)

### Advanced Filtering
- Date range presets: This Week, This Month, Custom Range
- User filter (autocomplete)
- Response time range filter
- Multiple command name selection

### Performance Optimization
- Virtual scrolling for very long command lists
- Debounced search input
- Cache analytics data client-side

## Out of Scope

### Not Included
- Command editing/configuration (separate Settings page handles this)
- Permission management (handled in Admin/Users)
- Command creation wizard (commands defined in code)
- Mobile app (web interface only)
- Command execution from admin UI (Discord-only)

## Open Questions

### Resolved
- ✅ Navigation structure (in-page tabs)
- ✅ Filter persistence (URL parameters)
- ✅ AJAX scope (filters, pagination, tabs)
- ✅ Page structure (/Commands as hub)
- ✅ Header/breadcrumb design
- ✅ Tab badges (none)
- ✅ Default date filter (last 7 days)
- ✅ Details page (modal overlay)
- ✅ Visual consistency (match Guild pages completely)

### Still to Resolve
None at this time - all requirements captured

## Decisions Made

1. **Architecture Decision**: Use `/Commands` as the hub page with in-page tabs
   - **Rationale**: Maintains existing URL structure, avoids redirects, clear hierarchy

2. **Navigation Mode**: In-page tabs with URL hash persistence
   - **Rationale**: Seamless UX, shareable URLs, browser back/forward support

3. **Filter Persistence**: URL parameters for shared filters
   - **Rationale**: Shareable filtered views, bookmark-able, browser back support

4. **Default Date Range**: Last 7 days
   - **Rationale**: Reasonable default, prevents overwhelming users with all-time data

5. **Details Modal**: Use modal overlay instead of separate page
   - **Rationale**: Faster navigation, preserves filter context, modern UX pattern

6. **Visual Design**: Match Guild pages completely
   - **Rationale**: Consistency across admin interface, professional appearance

7. **AJAX All Actions**: Make all interactions seamless
   - **Rationale**: Modern admin UX expectation, faster perceived performance

## Implementation Notes

### PageModel Structure
```csharp
// Commands/Index.cshtml.cs
public class IndexModel : PageModel
{
    // Properties for all three tabs
    public CommandListViewModel CommandList { get; set; }
    public CommandLogsViewModel CommandLogs { get; set; }
    public CommandAnalyticsViewModel CommandAnalytics { get; set; }

    // Shared filter properties
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    // Active tab tracking
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "command-list";

    public async Task OnGetAsync()
    {
        // Set default date range if not provided
        if (!StartDate.HasValue && !EndDate.HasValue)
        {
            EndDate = DateTime.UtcNow.Date;
            StartDate = EndDate.Value.AddDays(-7);
        }

        // Load data based on active tab
        // Lazy-load via AJAX for better performance
    }
}
```

### URL Structure Examples
```
/Commands                           # Loads Command List tab (default)
/Commands#execution-logs            # Direct link to Execution Logs tab
/Commands#analytics                 # Direct link to Analytics tab
/Commands?StartDate=2024-01-01&GuildId=123#execution-logs  # With filters
```

### AJAX Response Format
```json
{
  "success": true,
  "html": "<div>...</div>",  // Rendered partial content
  "totalPages": 10,
  "currentPage": 2,
  "totalItems": 234
}
```

## Recommended Next Steps

1. **Create GitHub Issue** - Use `/create-issue` with this requirements doc
2. **Design Review** - Review with stakeholders if needed
3. **Create HTML Prototype** - Build static prototype in `/docs/prototypes/features/command-pages-refactor/`
4. **Implementation Plan** - Use systems-architect agent to break down into tasks
5. **Development** - Implement incrementally:
   - Phase 1: Unified layout with tabs (no AJAX)
   - Phase 2: Shared date filter component
   - Phase 3: AJAX interactions
   - Phase 4: Details modal
   - Phase 5: Polish and testing

## Success Criteria

### Must Have
- ✅ All three pages accessible via tabs on `/Commands`
- ✅ Breadcrumb updates based on active tab
- ✅ Date range filter shared between Logs and Analytics tabs
- ✅ Filter persistence via URL parameters
- ✅ AJAX filter submissions and pagination
- ✅ Command log details open in modal
- ✅ Visual consistency with Guild pages
- ✅ Responsive design (mobile, tablet, desktop)
- ✅ Existing functionality preserved (no regressions)

### Should Have
- Tab animations/transitions
- Loading states for AJAX operations
- Accessible keyboard navigation
- Error handling for failed AJAX requests

### Nice to Have
- Smooth scroll animations
- Filter preset saving (remember user preferences)
- Quick action buttons in modal

---

**Document Version**: 1.0
**Last Updated**: 2026-01-18
**Status**: Ready for Implementation Planning
