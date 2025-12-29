# Connected Servers Widget - Function Specification

## 1. Feature Name
**Connected Servers Widget**

## 2. Purpose
Display a summary table of the bot's connected Discord servers on the dashboard, allowing administrators to quickly view server status, member counts, and daily command activity. This provides at-a-glance visibility into server health and engagement without navigating to a dedicated servers page.

## 3. Data Requirements

### Source Data
- **Guild Data**: From `IGuildService.GetAllGuildsAsync()` - provides server name, ID, member count, icon URL, and active status
- **Command Counts Per Guild**: Requires new service method or aggregation from `ICommandLogService` filtered by guild ID and today's date range

### Required Data Points Per Server
| Field | Source | Notes |
|-------|--------|-------|
| Server Name | `GuildDto.Name` | Display name |
| Server ID | `GuildDto.Id` | Discord snowflake ID |
| Icon URL | `GuildDto.IconUrl` | Nullable; generate initials avatar if null |
| Member Count | `GuildDto.MemberCount` | Nullable int from live Discord data |
| Is Active | `GuildDto.IsActive` | Boolean from database |
| Commands Today | New aggregation | Count of commands executed today for this guild |

### New Service Requirement
Add method to `ICommandLogService`:
```csharp
Task<IDictionary<ulong, int>> GetCommandCountsByGuildAsync(
    DateTime since,
    CancellationToken cancellationToken = default);
```
Returns a dictionary mapping guild ID to command count since the specified date.

## 4. ViewModel Properties

### ConnectedServersWidgetViewModel
```csharp
public class ConnectedServersWidgetViewModel
{
    public string Title { get; init; } = "Connected Servers";
    public string ViewAllUrl { get; init; } = "/Servers";
    public List<ConnectedServerItemViewModel> Servers { get; init; } = new();
    public int TotalServerCount { get; init; }
    public bool ShowViewAll => TotalServerCount > Servers.Count;
}
```

### ConnectedServerItemViewModel
```csharp
public class ConnectedServerItemViewModel
{
    public ulong Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public string Initials { get; init; } = string.Empty;
    public string AvatarGradient { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public ServerConnectionStatus Status { get; init; }
    public int CommandsToday { get; init; }
    public string DetailUrl { get; init; } = string.Empty;
}

public enum ServerConnectionStatus
{
    Online,   // Guild is active and bot connected
    Idle,     // Guild exists but low recent activity
    Offline   // Guild in database but bot not currently connected
}
```

## 5. Display Logic

### Server Count
- Display top 5 servers by default
- Sort by: Commands Today (descending), then by Member Count (descending)
- Show "View All" link if total servers exceed displayed count

### Status Mapping
| Condition | Status | Badge Color |
|-----------|--------|-------------|
| `IsActive == true` AND connected to Discord | Online | Success (green) |
| `IsActive == true` AND commands today == 0 | Idle | Warning (amber) |
| `IsActive == false` OR not in Discord cache | Offline | Gray |

### Avatar Generation
- If `IconUrl` is present: display Discord server icon
- If `IconUrl` is null: generate gradient avatar with initials
  - Initials: First letter of first two words, or first two letters if single word
  - Gradient: Hash server ID to select from predefined gradient palette

### Gradient Palette (for initials avatars)
```
from-purple-500 to-pink-500
from-blue-500 to-cyan-500
from-orange-500 to-red-500
from-green-500 to-emerald-500
from-indigo-500 to-purple-500
from-yellow-500 to-orange-500
```

## 6. Integration Point

### Page Location
- Dashboard (`Pages/Index.cshtml`)
- Position: After Hero Metrics Cards, in a 2-column grid layout
- Grid: `xl:col-span-2` (2/3 width) alongside Activity Timeline at `xl:col-span-1` (1/3 width)

### Layout Change Required
Replace current equal 2-column layout:
```html
<!-- Current: 2 equal columns -->
<div class="grid grid-cols-1 lg:grid-cols-2 gap-4 lg:gap-6 mb-8">
```

With 3-column grid:
```html
<!-- New: 2/3 + 1/3 layout -->
<div class="grid grid-cols-1 xl:grid-cols-3 gap-4 lg:gap-6 mb-8">
    <!-- Connected Servers: xl:col-span-2 -->
    <!-- Activity Timeline: xl:col-span-1 -->
</div>
```

### Index.cshtml.cs Changes
1. Add property: `public ConnectedServersWidgetViewModel ConnectedServers { get; private set; } = default!;`
2. Add builder method: `BuildConnectedServersWidget(IEnumerable<GuildDto> guilds, IDictionary<ulong, int> commandCounts)`
3. Call new service method in `OnGetAsync()` to fetch command counts by guild

## 7. Dependencies

### Existing Components
- `_Badge` partial view - for status badges
- Table styling from design system (see `docs/prototypes/components/data-display/tables.html`)

### New Components Required
- `_ConnectedServersWidget.cshtml` - partial view for the widget
- `ConnectedServersWidgetViewModel.cs` - ViewModel classes

### Services
- `IGuildService` (existing) - for guild data
- `ICommandLogService` (existing, needs extension) - for command counts by guild

### Partial View Location
`src/DiscordBot.Bot/Pages/Shared/Components/_ConnectedServersWidget.cshtml`

## 8. Acceptance Criteria

1. Widget displays in correct position (2/3 width column) after Hero Metrics
2. Shows top 5 servers sorted by commands today (desc), then members (desc)
3. Each row displays: avatar (icon or initials), name, ID, member count, status badge, commands today, actions button
4. Status badges correctly reflect server connection state with appropriate colors
5. "View All" link appears when more than 5 servers exist and navigates to `/Servers`
6. Initials avatars display with deterministic gradient based on server ID
7. Actions dropdown button is present (functionality deferred to future iteration)
8. Responsive: table scrolls horizontally on small screens
9. Hover state on rows matches design system (`hover:bg-bg-hover`)

## 9. Out of Scope (Future Iterations)

- Actions dropdown menu functionality (settings, kick bot, etc.)
- Real-time updates via SignalR
- Server search/filter within widget
- Expandable row details
- Sorting by clicking column headers

## 10. Prototype Reference

See: `docs/prototypes/features/dashboard-redesign/dashboard.html` lines 883-1011

## 11. Related Files

| File | Purpose |
|------|---------|
| `src/DiscordBot.Bot/Pages/Index.cshtml` | Dashboard page template |
| `src/DiscordBot.Bot/Pages/Index.cshtml.cs` | Dashboard page model |
| `src/DiscordBot.Core/DTOs/GuildDto.cs` | Guild data structure |
| `src/DiscordBot.Core/Interfaces/IGuildService.cs` | Guild service interface |
| `src/DiscordBot.Core/Interfaces/ICommandLogService.cs` | Command log service interface |
| `src/DiscordBot.Bot/ViewModels/Components/BadgeViewModel.cs` | Badge component model |
