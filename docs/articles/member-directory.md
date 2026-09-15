# Member Directory

The Member Directory feature provides admins and moderators with a comprehensive, searchable interface for browsing and managing guild members. It offers advanced filtering, sorting, bulk selection, and CSV export capabilities for effective member management.

## Overview

The Member Directory is a full-featured member management interface accessible from the admin UI. It provides:

- **Advanced Search**: Search by username, display name, or user ID
- **Multi-criteria Filtering**: Filter by roles, join date, and activity status
- **Flexible Sorting**: Sort by username, join date, or last activity
- **Bulk Operations**: Select multiple members for bulk export
- **CSV Export**: Export filtered member lists or selected members to CSV
- **Real-time Data**: Member data synchronized from Discord with caching for performance
- **Responsive Design**: Desktop table view and mobile card layout

## Access Requirements

| Role | Access Level |
|------|-------------|
| SuperAdmin | Full access |
| Admin | Full access |
| Moderator | Read-only access |
| Viewer | No access |

**Policy:** `RequireModerator`

**Route:** `/Guilds/{guildId}/Members`

## User Interface

### Page Layout

The Member Directory page consists of the following sections:

1. **Breadcrumb Navigation**: Shows the path from Servers → Guild Details → Members
2. **Page Header**: Displays the page title with a badge showing total member count
3. **Export Button**: Quick access to export all filtered members to CSV
4. **Filter Panel**: Collapsible panel with search and filter controls
5. **Bulk Actions Toolbar**: Appears when members are selected (hidden by default)
6. **Member List**: Desktop table or mobile card layout showing member data
7. **Pagination Controls**: Navigate through pages and adjust page size
8. **Results Summary**: Shows current page range and total count

### Filter Panel

The filter panel is collapsible and shows an orange badge when filters are active. It provides the following controls:

#### Search
- **Label**: Search
- **Placeholder**: "Username, display name, or user ID..."
- **Behavior**: Searches across username, global display name, nickname, and user ID fields
- **Type**: Text input with search icon

#### Role Filter
- **Label**: Roles
- **Type**: Multi-select dropdown with checkboxes
- **Default**: "All roles"
- **Behavior**:
  - Shows role count when selected (e.g., "2 roles selected")
  - Displays role color dot next to role name
  - Filters members who have ALL selected roles (AND logic)
  - Excludes @everyone and managed roles

#### Join Date Range
- **Joined After**: Date picker for filtering members who joined after a specific date (inclusive)
- **Joined Before**: Date picker for filtering members who joined before a specific date (inclusive)
- **Type**: HTML5 date inputs
- **Format**: `yyyy-MM-dd`

#### Activity Filter
- **Label**: Activity
- **Type**: Dropdown select
- **Options**:
  - `All Members` - No activity filter
  - `Active Today` - Members active in the last 24 hours
  - `Active This Week` - Members active in the last 7 days
  - `Active This Month` - Members active in the last 30 days
  - `Inactive 7+ Days` - Members inactive for 7 or more days
  - `Inactive 30+ Days` - Members inactive for 30 or more days
  - `Never Messaged` - Members who have never sent a message

#### Sort Controls
- **Sort By**: Dropdown with options:
  - `Join Date` (default)
  - `Username`
  - `Last Active`
- **Order**: Dropdown with options:
  - `Ascending`
  - `Descending` (default)

#### Filter Actions
- **Apply Filters**: Primary button to submit filter form
- **Reset**: Secondary button to clear all filters and return to default view

### Member Display

#### Desktop Table View (md+ breakpoint)

The desktop view displays members in a responsive table with the following columns:

| Column | Content | Responsive |
|--------|---------|-----------|
| Checkbox | Selection checkbox for bulk actions | Always visible |
| Member | Avatar, display name, and @username | Always visible |
| Roles | Role badges (max 3 visible, +N more indicator) | Always visible |
| Joined | Join date formatted in user's timezone | Hidden on mobile (lg+) |
| Last Active | Relative time or "Never" | Hidden on tablet (xl+) |
| Actions | "View" link to open detail modal | Always visible |

**Member Avatar:**
- Display Discord avatar if available (CDN URL: `https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=80`)
- Fallback to gradient circle with initials if no avatar
- Size: 40x40px (w-10 h-10)

**Display Name:**
- Shows nickname if set, otherwise global display name, otherwise username
- Font weight: medium
- Color: text-primary

**Username:**
- Shown as @username below display name
- Font: monospace
- Size: xs
- Color: text-tertiary

**Role Badges:**
- Display role name with role color as background
- Max 3 roles visible inline
- "+N" badge for remaining roles with hover tooltip showing all role names
- "No roles" text if member has no roles

**Dates:**
- Join date formatted using `data-utc` attribute with `data-format="date"`
- Last active formatted using `data-utc` attribute with `data-format="relative"`
- JavaScript handles timezone conversion and formatting

#### Mobile Card View (< md breakpoint)

Members are displayed as cards with:
- Checkbox, avatar, display name, and @username in header
- Roles section (max 5 visible, +N indicator)
- Stats grid showing join date and last active
- "View Details" button with eye icon

### Bulk Selection

#### Select All
- Checkbox in table header
- Selects/deselects all members on current page
- Updates bulk actions toolbar

#### Individual Selection
- Checkbox in each member row/card
- Updates selected count in bulk actions toolbar
- Shows/hides toolbar based on selection state

#### Bulk Actions Toolbar
- Appears when at least one member is selected
- Shows count of selected members
- **Deselect All**: Clears all selections
- **Export Selected**: Exports only selected members to CSV (passes user IDs as query params)

### Member Detail Modal

Clicking "View" on a member opens a modal with detailed information:

- **Basic Information**: User ID, account creation date, join date
- **Display Names**: Username, global display name, nickname
- **Avatar**: Full-size avatar image
- **Roles**: Complete list of all roles with colors
- **Activity**: Last active timestamp with relative time
- **Status**: Active/inactive indicator

As of the Blazor port (Phase 4 cluster 4d), the page is `Blazor/Pages/Guilds/Members/Index.razor` (`+ .razor.cs`), an interactive component - the modal is a `Modal` instance fed directly by `IGuildMemberService.GetMemberAsync` in-circuit, not an AJAX call against a REST endpoint. `GuildMembersController` (the former `/api/guilds/{guildId}/members*` endpoints) was retired with the port; `member-directory.js` and `Pages/Guilds/Members/_MemberDetailModal.cshtml` are gone.

### Pagination

The pagination component provides:
- First, previous, next, and last page buttons
- Current page indicator with page number buttons
- Page size selector (25, 50, 100 items per page)
- Total item count display
- Results summary (e.g., "Showing 1 to 25 of 150 members")

**Default Settings:**
- Page size: 25
- Sort by: Join Date (descending)

## Filtering and Search

### Search Behavior

The search term is applied to the following fields:
- **Username**: Discord username (without discriminator)
- **Global Display Name**: User's global display name set in Discord settings
- **Nickname**: Server-specific nickname
- **User ID**: Discord snowflake ID (exact match)

**Search Type:** Case-insensitive partial match (contains)

### Filter Combination

All active filters are combined with AND logic:
- Member must match search term (if provided)
- Member must have ALL selected roles (if roles selected)
- Member must have joined within the date range (if dates provided)
- Member must match activity criteria (if activity filter selected)

### Active Filter Indicator

When filters are active:
- Filter panel remains expanded
- Orange badge shows count of active filters
- Filter panel chevron points down
- Pagination and results reflect filtered data

## Sorting Options

Members can be sorted by the following fields:

| Sort Field | Description | Data Type |
|-----------|-------------|-----------|
| `JoinedAt` | Date member joined the guild | DateTime |
| `Username` | Discord username (alphabetical) | String |
| `LastActiveAt` | Most recent message activity | DateTime? |

**Default Sort:** `JoinedAt` descending (newest members first)

**Sort Order:**
- **Ascending**: A→Z, oldest→newest
- **Descending**: Z→A, newest→oldest

Members with null `LastActiveAt` are sorted to the end when sorting by last active.

## Export Functionality

### Export All (Filtered)

The "Export All (current filter, max 10k)" button builds the CSV in-circuit via
`IGuildMemberService.ExportMembersToCsvAsync` (current filters, no `UserIds`, `maxRows: 10000`)
and hands it to the browser with `BrowserInterop.DownloadFileAsync` - there is no longer a
`GET /Guilds/{guildId}/Members?handler=Export` link or redirect (that link pointed at a
nonexistent Razor Page handler even before the port) or a `GET /api/guilds/{guildId}/members/export`
REST round trip.

### Export Selected (Bulk)

The "Export Selected" button in the bulk actions toolbar builds the CSV the same way, scoped to
the selected `UserIds`, and downloads it the same way - `GuildMembersController` (the former
`/api/guilds/{guildId}/members/export` endpoint) was retired with this port, since
`member-directory.js` was its only consumer.

### CSV Format

The exported CSV file contains the following columns:

| Column | Description | Format |
|--------|-------------|--------|
| UserId | Discord user snowflake ID | Numeric string |
| Username | Discord username | String |
| DisplayName | Effective display name (nickname > global > username) | String |
| Nickname | Server-specific nickname | String (empty if none) |
| GlobalDisplayName | Global display name | String (empty if none) |
| JoinedAt | Date joined the guild | ISO 8601 (UTC) |
| LastActiveAt | Last message activity | ISO 8601 (UTC) or empty |
| AccountCreatedAt | Discord account creation date | ISO 8601 (UTC) |
| Roles | Pipe-delimited list of role names | String (e.g., "Admin\|Moderator\|Member") |
| IsActive | Active status | Boolean (true/false) |

**File Naming:** `members-{guildId}-{timestamp}.csv`

**Timestamp Format:** `yyyyMMdd-HHmmss` (UTC)

**Example Filename:** `members-123456789012345678-20241208-153045.csv`

## Performance Considerations

### Caching

Member data is cached to reduce Discord API calls:
- **Cache Duration**: Configurable via `CachingOptions.MemberCacheDuration`
- **Default**: 15 minutes
- **Strategy**: Members are cached per guild after first fetch
- **Invalidation**: Cache expires after duration, or can be manually cleared

### Large Guilds

For guilds with many members:
- **Pagination**: Only requested page is loaded from database
- **Indexed Queries**: Database queries use indexes on `GuildId`, `UserId`, `JoinedAt`, and `LastActiveAt`
- **Lazy Avatar Loading**: Avatar images use `loading="lazy"` attribute
- **Export Limit**: CSV export is limited to 10,000 rows to prevent timeouts

### Database Queries

Member queries are optimized with:
- **Filtering**: Applied at database level via LINQ queries
- **Sorting**: Uses SQL ORDER BY for efficient sorting
- **Pagination**: Uses SKIP/TAKE for efficient paging
- **Role Joins**: Eager loads role data to avoid N+1 queries

## Service Integration

As of the Blazor port (Phase 4 cluster 4d) the page calls `IGuildMemberService` directly, in-circuit
- there is no longer a REST API surface for it. `GuildMembersController`
(`GET /api/guilds/{guildId}/members`, `GET /api/guilds/{guildId}/members/{userId}`,
`GET /api/guilds/{guildId}/members/export`, all `RequireAdmin`) was retired with this port; note
this page's own policy was always `RequireModerator`, one level below the controller's - calling
the service directly means this page's own policy now governs. See
[api-endpoints.md](api-endpoints.md) for the removal note.

## JavaScript Dependencies

None - as of the Blazor port (Phase 4 cluster 4d) the page is an interactive Blazor component
(`Blazor/Pages/Guilds/Members/Index.razor`); selection, the detail modal, filtering, and CSV
export are all component state and direct service calls. `wwwroot/js/member-directory.js` was
deleted with the port.

## Related Documentation

- [API Endpoints - Member Directory Endpoints](api-endpoints.md#member-directory-endpoints)
- [Design System](design-system.md) - UI component specifications
- [Authorization Policies](authorization-policies.md) - Role-based access control
- [Form Implementation Standards](form-implementation-standards.md) - Filter form patterns

## Future Enhancements

Planned enhancements for future releases (Phase 6):

- **Moderator Notes**: Add private notes to member profiles
- **Member Tags**: Custom tags for categorizing members
- **Activity History**: View message count and activity timeline
- **Bulk Actions**: Bulk role assignment, member tagging
- **Advanced Search**: Search by role combinations (OR logic), message count, activity patterns
- **Column Customization**: Show/hide columns, reorder columns
- **Export Options**: Additional export formats (JSON, Excel), custom column selection
- **Member Comparison**: Compare multiple member profiles side-by-side

These features are not yet implemented but are under consideration for future development.
