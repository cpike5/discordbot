---
title: Search System
description: Global search functionality across portal data and Discord logs
version: 1.0
lastUpdated: 2026-01-25
---

# Search System

The search system provides unified global search across all major data categories in the admin portal, including users, guilds, commands, audit logs, message logs, and more. Users can search from the header search bar or navigate to the dedicated search results page.

---

## Overview

The search system enables:

- **Unified Search** - Search across multiple categories simultaneously
- **Category Filtering** - Restrict search to specific data categories
- **Authorization-Aware** - Results respect user roles and permissions
- **Rich Results** - Results include icons, badges, timestamps, and metadata
- **Fast Lookup** - Indexed database queries for performance
- **Mobile-Friendly** - Mobile search overlay with type-ahead

---

## Searchable Content

The system indexes and searches the following content categories:

| Category | Searchable Fields | Result Details | Access |
|----------|------------------|----------------|--------|
| **Guilds** | Guild name, guild ID | Name, member count, icon | All authenticated users |
| **Users** | Username, display name, user ID | Username, avatar, roles | Admins only |
| **Commands** | Command name, description, module | Name, module, description | All users |
| **Command Logs** | Command name, parameters, result | Command, user, duration, status | All users |
| **Audit Logs** | Action, actor name, target, changes | Action type, actor, timestamp | Admins only |
| **Message Logs** | Message content, author, channel | Content snippet, author, timestamp | Admins only |
| **Pages** | Page title, description | Page title, URL, description | Based on page auth |
| **Reminders** | Reminder message | Message, user, scheduled time | Own reminders + admins |
| **Scheduled Messages** | Message content | Content snippet, channel, schedule | Per-guild admins |

---

## Search Service Architecture

### ISearchService Interface

The search functionality is provided by the `ISearchService` interface:

```csharp
public interface ISearchService
{
    /// <summary>
    /// Performs a unified search across all applicable categories.
    /// </summary>
    Task<UnifiedSearchResultDto> SearchAsync(
        SearchQueryDto query,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a search within a specific category.
    /// </summary>
    Task<SearchCategoryResult> SearchCategoryAsync(
        SearchCategory category,
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
```

**Implementation:** `SearchService` in `src/DiscordBot.Bot/Services/SearchService.cs` (749 lines)

### Service Registration

The service is registered in `Program.cs` as a scoped service:

```csharp
builder.Services.AddScoped<ISearchService, SearchService>();
```

### Search Categories

All searchable categories are defined in the `SearchCategory` enum:

```csharp
public enum SearchCategory
{
    Guilds,              // Discord servers
    Users,               // User accounts
    Commands,            // Registered slash commands
    CommandLogs,         // Command execution history
    AuditLogs,           // System audit trail
    MessageLogs,         // Discord message history
    Pages,               // Admin UI pages
    Reminders,           // User reminders
    ScheduledMessages    // Scheduled channel messages
}
```

---

## Using the Search

### Search Page

**URL:** `/Search?q={searchTerm}`

**Authorization:** RequireViewer policy (all authenticated users)

**Features:**
- Unified results across all categories
- Category-based filtering
- Result snippets with icons and badges
- Click-through navigation
- Empty state for no results

**Example:**
```
https://localhost:5001/Search?q=ban
```

### Header Search Bar

The global search bar is available in the navigation header:

**Keyboard Shortcut:** Ctrl+K (Windows/Linux) or Cmd+K (macOS)

**Features:**
- Type-ahead suggestions
- Mobile overlay support
- Quick navigation
- Category filtering

### Mobile Search

A mobile-optimized search overlay appears on smaller screens:

- Full-screen overlay layout
- Touch-friendly result cards
- Close button overlay
- Scroll-friendly results

---

## Search API Structure

### Request Format

The search uses a `SearchQueryDto` to specify search parameters:

```csharp
public class SearchQueryDto
{
    /// <summary>
    /// The search term to query across categories.
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Maximum results to return per category. Default is 5.
    /// </summary>
    public int MaxResultsPerCategory { get; set; } = 5;

    /// <summary>
    /// Optional filter to restrict search to a specific category.
    /// Null = search all categories.
    /// </summary>
    public SearchCategory? CategoryFilter { get; set; }
}
```

### Response Format

The API returns a `UnifiedSearchResultDto` containing results grouped by category:

```csharp
public class UnifiedSearchResultDto
{
    /// <summary>
    /// The original search term.
    /// </summary>
    public string SearchTerm { get; set; }

    /// <summary>
    /// Results for each category (Guilds, Users, Commands, etc.).
    /// </summary>
    public SearchCategoryResult Guilds { get; set; }
    public SearchCategoryResult Users { get; set; }
    public SearchCategoryResult Commands { get; set; }
    public SearchCategoryResult CommandLogs { get; set; }
    public SearchCategoryResult AuditLogs { get; set; }
    public SearchCategoryResult MessageLogs { get; set; }
    public SearchCategoryResult Pages { get; set; }
    public SearchCategoryResult Reminders { get; set; }
    public SearchCategoryResult ScheduledMessages { get; set; }

    /// <summary>
    /// True if any results were found across categories.
    /// </summary>
    public bool HasResults { get; }

    /// <summary>
    /// Total result count across all categories.
    /// </summary>
    public int TotalResultCount { get; }
}
```

### Category Result Format

Each category result is a `SearchCategoryResult`:

```csharp
public class SearchCategoryResult
{
    /// <summary>
    /// The category these results belong to.
    /// </summary>
    public SearchCategory Category { get; set; }

    /// <summary>
    /// Display name for UI rendering (e.g., "Users", "Commands").
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Search result items in this category.
    /// </summary>
    public List<SearchResultItemDto> Items { get; set; }

    /// <summary>
    /// Total count of all matches (may exceed Items.Count).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// True if more results exist beyond what's shown.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// URL to view all results in this category (if available).
    /// </summary>
    public string? ViewAllUrl { get; set; }
}
```

### Individual Result Format

Each search result is a `SearchResultItemDto`:

```csharp
public class SearchResultItemDto
{
    /// <summary>
    /// Unique identifier for this item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Primary title/name of the result.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Secondary text (e.g., description, member count).
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Longer description or content snippet.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Icon URL (discord avatar, guild icon, etc.).
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Badge text (e.g., status, role, count).
    /// </summary>
    public string? BadgeText { get; set; }

    /// <summary>
    /// Badge styling (e.g., "success", "warning", "danger").
    /// </summary>
    public string? BadgeVariant { get; set; }

    /// <summary>
    /// Navigation URL for this result.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Relevance score (0-100, higher = more relevant).
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Optional timestamp associated with result.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Additional metadata as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
```

---

## Search Results by Category

### Guilds Category

Searches Discord guild names and IDs.

**Fields Searched:** Guild name, guild ID

**Result Fields:**
- Title: Guild name
- Subtitle: Member count (e.g., "1,234 members")
- IconUrl: Guild icon
- Badge: "Active" or "Inactive"
- Url: `/Guilds/Details?id={guildId}`

**Example:**
```json
{
  "id": "123456789",
  "title": "My Discord Server",
  "subtitle": "1,234 members",
  "iconUrl": "https://cdn.discordapp.com/icons/...",
  "badgeText": "Active",
  "badgeVariant": "success",
  "url": "/Guilds/Details?id=123456789",
  "timestamp": "2026-01-10T14:30:00Z"
}
```

### Users Category

Searches user accounts by username, display name, and user ID. **Admin-only.**

**Fields Searched:** Username, display name, user ID

**Result Fields:**
- Title: Username
- Subtitle: Display name or role
- IconUrl: User avatar
- Badge: Role (SuperAdmin, Admin, etc.)
- Url: `/Admin/Users/Details?id={userId}`

### Commands Category

Searches registered slash commands by name and description.

**Fields Searched:** Command name, description, module name

**Result Fields:**
- Title: Command name
- Subtitle: Module name
- Description: Command description
- Badge: Parameter count
- Url: `/Commands` (can be enhanced with fragment)

### Command Logs Category

Searches command execution history.

**Fields Searched:** Command name, executed by user, status

**Result Fields:**
- Title: Command name
- Subtitle: Executed by user
- Description: Duration and status
- Badge: Success/Error status
- Timestamp: Execution time
- Url: `/CommandLogs/{logId}`

### Audit Logs Category

Searches system audit trail. **Admin-only.**

**Fields Searched:** Action type, actor name, target entity

**Result Fields:**
- Title: Action description
- Subtitle: Actor (user who performed action)
- Description: Target and changes summary
- Badge: Action category
- Timestamp: Action time
- Url: `/Admin/AuditLogs/Details/{id}`

### Message Logs Category

Searches Discord message history. **Admin-only.**

**Fields Searched:** Message content, author, channel name

**Result Fields:**
- Title: First 50 characters of message
- Subtitle: Author and channel
- Description: Full message content
- Badge: Message type (normal, edited, etc.)
- Timestamp: Message time
- Url: `/Admin/MessageLogs/Details/{logId}`

### Pages Category

Searches admin UI pages (dynamic).

**Fields Searched:** Page title, page description

**Result Fields:**
- Title: Page title
- Subtitle: Page path
- Description: Page purpose
- Url: Actual page URL

**Examples:** Dashboard, Commands, Guild Details, Analytics, etc.

### Reminders Category

Searches user reminders. Users can only see their own reminders.

**Fields Searched:** Reminder message content

**Result Fields:**
- Title: Reminder message (truncated)
- Subtitle: Scheduled time
- Description: Full reminder message
- Badge: Status (Pending, Completed)
- Timestamp: Scheduled time
- Url: `/Guilds/{guildId}/Reminders`

### Scheduled Messages Category

Searches guild scheduled messages. **Guild admin only.**

**Fields Searched:** Message content

**Result Fields:**
- Title: First 50 characters of message
- Subtitle: Target channel
- Description: Full message content
- Badge: Status (Active, Inactive)
- Url: `/Guilds/ScheduledMessages/{guildId}`

---

## Search Features

### Relevance Scoring

Results are scored 0-100 based on match quality:

- **Exact matches** (title matches exactly): 100
- **Title contains search term**: 80-90
- **Partial matches**: 50-70
- **Related content**: 30-50

Results within a category are sorted by relevance score (highest first).

### Authorization Filtering

Search respects role-based authorization:

- **Admin Categories** (automatically hidden from non-admins):
  - Users
  - Audit Logs
  - Message Logs

- **Guild-Specific Content** (per-guild admin check):
  - Reminders
  - Scheduled Messages
  - Guild-specific audit entries

### Result Limiting

By default, search returns:

- **5 results per category** (configurable via `MaxResultsPerCategory`)
- **"HasMore" indicator** if more results exist
- **"ViewAllUrl"** to see complete category results

### Case-Insensitive Matching

All searches are case-insensitive for better usability.

---

## Integration Examples

### Using Search from a Service

```csharp
public class MyService
{
    private readonly ISearchService _searchService;

    public MyService(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task<SearchCategoryResult> FindUserAsync(
        string username,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        return await _searchService.SearchCategoryAsync(
            SearchCategory.Users,
            username,
            maxResults: 10,
            user,
            cancellationToken);
    }
}
```

### Using Unified Search

```csharp
var query = new SearchQueryDto
{
    SearchTerm = "ban",
    MaxResultsPerCategory = 5,
    CategoryFilter = null // Search all categories
};

var results = await _searchService.SearchAsync(query, User);

foreach (var category in results.GetType().GetProperties())
{
    var categoryResult = (SearchCategoryResult)category.GetValue(results);
    Console.WriteLine($"{categoryResult.DisplayName}: {categoryResult.Items.Count} results");
}
```

### Using Category-Specific Search

```csharp
var results = await _searchService.SearchCategoryAsync(
    SearchCategory.Commands,
    "soundboard",
    maxResults: 20,
    User);

if (results.HasMore)
{
    Console.WriteLine($"{results.TotalCount - results.Items.Count} more results available");
    Console.WriteLine($"View all: {results.ViewAllUrl}");
}
```

---

## Search UI Components

### Global Search Bar

Located in the main navigation header:

```html
<input type="text" placeholder="Search... (Ctrl+K)"
       id="global-search-input"
       aria-label="Global search">
```

**Features:**
- Keyboard shortcut (Ctrl+K / Cmd+K)
- Type-ahead suggestions
- Navigate to results page on Enter
- Mobile overlay support

### Mobile Search Overlay

On smaller screens, search opens a full-screen overlay:

```html
<div id="mobile-search-overlay" class="search-overlay">
  <div class="search-overlay-content">
    <input type="text" placeholder="Search..." autofocus>
    <div class="results-list">
      <!-- Results rendered here -->
    </div>
  </div>
</div>
```

### Search Results Page

The dedicated results page at `/Search?q=...` displays:

- **Search Term Display** - "Results for: {term}"
- **Category Sections** - Each category shown as collapsible/expandable group
- **Result Cards** - Rich cards with icon, title, subtitle, description, badge
- **View More Links** - "View all X results" for each category
- **Empty State** - Helpful message when no results found
- **Sorting** - Results within category sorted by relevance

---

## Performance Considerations

### Database Queries

Each category uses optimized database queries:

- **Indexed fields** - Search queries use database indexes on common fields
- **Limited result sets** - Default 5 results per category reduces query cost
- **Authorization filters** - Applied at query level for efficiency
- **Case-insensitive indexes** - Database provides case-insensitive matching

### Query Optimization Tips

When adding new searchable content:

1. **Add database indexes** on frequently searched fields
2. **Use parameterized queries** to prevent SQL injection
3. **Limit text length** in result descriptions (e.g., 200 characters)
4. **Implement pagination** for large result sets
5. **Cache common searches** if appropriate

### Search Latency

Typical search latency:

- **Local development**: 50-200ms
- **Production**: 100-500ms
- **With many results**: Up to 1000ms

Factors affecting latency:

- Database size
- Search term specificity
- Number of categories searched
- Network latency (if using external search service)

---

## Troubleshooting

### No Results Found

**Check:**
1. Verify search term spelling
2. Confirm object exists in system
3. Check user authorization level
4. Try searching with different terms
5. Check if content category is enabled

### Slow Search Performance

**Solutions:**
1. Verify database indexes exist
2. Check database query execution plans
3. Reduce `MaxResultsPerCategory` if needed
4. Upgrade database hardware if needed
5. Consider denormalizing frequently searched fields

### Missing Results Category

**Check:**
1. Is the user authorized for that category?
2. Is the category filter set correctly?
3. Is the content category populated with data?
4. Check service registration in Program.cs

### Authorization Issues

If you can't see certain results:

1. **Users, Audit Logs, Message Logs** - Require Admin role
2. **Guild-specific content** - Requires Admin role in that guild
3. **Reminders** - Can only see own reminders (unless Admin)
4. Contact an administrator if access needed

---

## Future Enhancements

**Potential improvements** (not in current MVP):

- Elasticsearch integration for full-text search
- Search suggestions/autocomplete API
- Advanced filters (date range, type selectors)
- Saved searches
- Search history
- Custom search operators (field-specific search: `user:john`)
- Fuzzy matching for typos
- Search analytics and trending searches
- Scheduled search reports

---

## References

- **Service Implementation:** `SearchService.cs` (749 lines)
- **Service Interface:** `ISearchService.cs`
- **Search Page:** `/Search.cshtml` and `Search.cshtml.cs`
- **DTOs:** `SearchDtos.cs` and `SearchCategory.cs` enum
- **Authorization Policies:** See [Authorization Policies](authorization-policies.md)
