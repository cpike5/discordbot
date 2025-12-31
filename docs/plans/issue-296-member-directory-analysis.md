# Gap Analysis: Issue #296 - Member Directory Epic

**GitHub Issue:** #296 - Epic: Member Directory
**Milestone:** v0.5.0
**Created:** 2025-12-30
**Status:** Analysis Complete

---

## 1. Requirement Summary

The Member Directory is a searchable, filterable member list within the admin panel for managing and reviewing Discord server members. Key capabilities include:

- **Search & Filter:** Find members by username, display name, ID, role, join date, and activity
- **Member Detail View:** Profile info, server-specific data, activity summary, moderation integration
- **Actions:** View Discord profile, add mod notes, view mod history, export to CSV
- **API Endpoints:** RESTful API for member listing, detail retrieval, and export

---

## 2. Gap Analysis

### 2.1 Data Model Gaps

#### Missing Entities

The current database schema does not track guild membership. The existing `User` entity (line 6-42 in `User.cs`) only tracks:
- Discord user ID
- Username and discriminator
- First/last seen timestamps globally

**Required New Entities:**

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| `GuildMember` | Junction table for user-guild relationship | `GuildId`, `UserId`, `JoinedAt`, `Nickname`, `CachedRoles`, `LastActiveAt`, `LastCachedAt` |

**GuildMember Schema Proposal:**

```csharp
public class GuildMember
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? Nickname { get; set; }
    public string? CachedRolesJson { get; set; }  // JSON array of role IDs
    public DateTime? LastActiveAt { get; set; }    // Last message/command timestamp
    public DateTime LastCachedAt { get; set; }     // For cache invalidation
    public bool IsActive { get; set; } = true;     // Still in guild

    // Navigation
    public Guild Guild { get; set; }
    public User User { get; set; }
}
```

**Indexes Required:**
- `IX_GuildMembers_GuildId_LastActiveAt` - Activity sorting
- `IX_GuildMembers_GuildId_JoinedAt` - Join date sorting
- `IX_GuildMembers_GuildId_UserId` (Primary key composite)

#### User Entity Enhancements

The `User` entity needs additional fields for account age calculation:

```csharp
// Add to User entity
public DateTime? AccountCreatedAt { get; set; }  // Discord account creation (from snowflake)
public string? AvatarHash { get; set; }          // For avatar URL construction
public string? GlobalDisplayName { get; set; }   // Discord global display name
```

#### Activity Tracking Gap

The spec mentions "message count" and "last seen" but the current `MessageLog` table only logs messages when logging is enabled. Consider:

1. **MessageLog aggregation** - Calculate counts from existing logs (performance concern)
2. **Denormalized counters** - Add `MessageCount` to `GuildMember` (requires update triggers)
3. **Activity summary table** - Periodic aggregation job

**Recommendation:** Use denormalized counters with background sync for performance.

### 2.2 Moderation System Integration Gaps

The epic references integration with Moderation System (#291), but those entities don't exist yet:

| Entity from #291 | Status | Member Directory Usage |
|------------------|--------|------------------------|
| `ModNote` | Not implemented | "Add Mod Note" action |
| `ModTag` | Not implemented | Display tags on member cards |
| `UserModTag` | Not implemented | Tag assignments |
| `ModerationCase` | Not implemented | "View Mod History" action |
| `Watchlist` | Not implemented | Watchlist indicator |

**Decision Required:**
- Implement Member Directory with stubs for moderation features?
- Wait for Moderation System implementation?
- Implement minimal moderation entities now?

### 2.3 API Design Gaps

#### Missing Query Parameters

The spec proposes `/api/guilds/{guildId}/members` but lacks query parameter details:

```
GET /api/guilds/{guildId}/members
    ?search=username      # Search term
    ?roleId=12345         # Filter by role
    ?joinedAfter=date     # Join date filter
    ?joinedBefore=date
    ?lastActiveAfter=date # Activity filter
    ?lastActiveBefore=date
    ?hasMessaged=true     # Never messaged filter
    ?sortBy=joinDate|username|lastActive|roleCount
    ?sortOrder=asc|desc
    ?page=1
    ?pageSize=50
```

#### Response Format Not Specified

Need to define DTOs:

```csharp
public class GuildMemberDto
{
    public ulong UserId { get; set; }
    public string Username { get; set; }
    public string? GlobalDisplayName { get; set; }
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime AccountCreatedAt { get; set; }
    public int MessageCount { get; set; }
    public List<GuildRoleDto> Roles { get; set; }
    public List<string>? ModTags { get; set; }  // From Moderation System
    public bool IsOnWatchlist { get; set; }
}

public class GuildRoleDto
{
    public ulong Id { get; set; }
    public string Name { get; set; }
    public string? Color { get; set; }  // Hex color
    public int Position { get; set; }
}
```

#### Pagination Response

Follow existing pattern from `PaginatedResponseDto<T>`:

```csharp
public class PaginatedResponseDto<T>
{
    public IReadOnlyList<T> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
```

### 2.4 Caching Strategy Gap

The spec mentions "merge/cache strategy for performance" but provides no details. Critical concerns:

1. **Discord API Rate Limits:** 50 requests/second, 10k/10 minutes
2. **Large Guilds:** 10k+ members common, 100k+ possible
3. **Data Freshness:** How stale is acceptable?

**Proposed Strategy:**

| Data Type | Cache Duration | Refresh Trigger |
|-----------|---------------|-----------------|
| Member list (basic) | 5 minutes | On-demand refresh |
| Member roles | 5 minutes | Role change event |
| Member detail | 1 minute | On-access |
| Activity metrics | Real-time | Event-driven |

### 2.5 Authorization Gap

The spec doesn't define who can access the Member Directory:

**Proposed Authorization:**

| Action | Required Role | Notes |
|--------|--------------|-------|
| View member list | Moderator | Read-only access |
| View member detail | Moderator | Includes activity summary |
| Add mod note | Moderator | Requires Moderation System |
| Export members | Admin | Data export privilege |
| View mod history | Moderator | Requires Moderation System |

Should use existing `GuildAccess` policy for guild-specific access control.

### 2.6 Navigation Integration Gap

The spec doesn't define where Member Directory fits in navigation.

**Proposed Location:**

- Route: `/Guilds/Members/{guildId:long}` or `/Guilds/{guildId:long}/Members`
- Navigation: Add link to Guild Details page (like Scheduled Messages, Rat Watch)
- Breadcrumb: Home > Guilds > [Guild Name] > Members

**Files to Update:**
- `Pages/Guilds/Details.cshtml` - Add "Members" link to guild actions
- Sidebar/NavMenu if exists at guild level

---

## 3. Technical Considerations

### 3.1 Discord API Rate Limits

Discord REST API has strict rate limits:
- **Global:** 50 requests/second
- **Per-route:** Varies (GET /guilds/{id}/members is 10/10s)
- **Large guilds:** Must use pagination (max 1000/request)

**Mitigation Strategies:**

1. **Gateway Events:** Subscribe to `GuildMemberAdd`, `GuildMemberRemove`, `GuildMemberUpdate`
2. **Incremental Sync:** Only fetch changed members since last sync
3. **Lazy Loading:** Fetch member details on-demand, not bulk
4. **Cache Invalidation:** Event-driven instead of TTL-based

### 3.2 Large Guild Handling

For guilds with 10,000+ members:

| Challenge | Solution |
|-----------|----------|
| Initial fetch timeout | Paginated background job |
| Memory pressure | Stream processing, don't load all |
| Database insert performance | Bulk upsert with batching |
| UI responsiveness | Virtual scrolling, server-side pagination |
| Search performance | Database indexes, consider full-text search |

**Proposed Sync Strategy:**

```
1. On bot join/startup: Queue full member sync job
2. Sync job: Paginate through Discord API (1000/request)
3. Batch upsert: 500 members per database transaction
4. Event listener: Real-time updates for joins/leaves/updates
5. Periodic reconciliation: Daily job to catch missed events
```

### 3.3 Search Performance

**Database Search Approach:**

For guilds under 50k members, database search is sufficient:

```sql
CREATE INDEX IX_GuildMembers_GuildId_Username
    ON GuildMembers (GuildId, Username);

-- Partial match search
SELECT * FROM GuildMembers
WHERE GuildId = @guildId
  AND (Username LIKE @search + '%' OR Nickname LIKE @search + '%')
ORDER BY Username
LIMIT 50;
```

**Full-Text Search (Future):**

For larger deployments, consider:
- PostgreSQL: Built-in full-text search
- SQLite: FTS5 extension
- External: Elasticsearch/Meilisearch

### 3.4 Data Freshness vs. Performance

**Hybrid Approach:**

| View | Data Source | Refresh |
|------|-------------|---------|
| List view | Cached database | Stale OK (5 min) |
| Detail view | Live Discord API | Real-time |
| Role list | Cached + event updates | Event-driven |
| Activity data | Database only | Real-time |

### 3.5 CSV Export Considerations

- **Row limit:** Consider 10k row limit for exports
- **Async generation:** Large exports via background job
- **Data included:** Define columns explicitly
- **Privacy:** Exclude sensitive moderation notes?

---

## 4. Recommended Sub-Issues

### Phase 1: Data Layer Foundation

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #A | task | Create GuildMember entity and migration | Add `GuildMember` entity, EF configuration, database migration | None |
| #B | task | Enhance User entity with Discord metadata | Add `AccountCreatedAt`, `AvatarHash`, `GlobalDisplayName` to User | None |
| #C | task | Implement member sync background service | Background job to sync guild members on bot start, periodic reconciliation | #A |
| #D | task | Add Discord gateway event handlers for member updates | Handle `GuildMemberAdd`, `GuildMemberRemove`, `GuildMemberUpdate` events | #A |

**Labels:** `component:infrastructure`, `type:task`, `priority:high`

### Phase 2: Service Layer

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #E | task | Create IGuildMemberService interface | Define service contract for member operations | #A |
| #F | task | Implement GuildMemberService | Search, filter, detail retrieval, caching logic | #E |
| #G | task | Create member query DTOs | `GuildMemberQueryDto`, `GuildMemberDto`, `GuildRoleDto` | #E |
| #H | task | Implement member caching layer | IMemoryCache-based caching with event invalidation | #F |

**Labels:** `component:bot`, `type:task`, `priority:high`

### Phase 3: API Layer

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #I | feature | Add GET /api/guilds/{guildId}/members endpoint | Paginated member list with filtering | #F, #G |
| #J | feature | Add GET /api/guilds/{guildId}/members/{userId} endpoint | Member detail view | #F |
| #K | feature | Add GET /api/guilds/{guildId}/members/export endpoint | CSV export with row limits | #F |
| #L | task | Add API documentation for member endpoints | Swagger/OpenAPI annotations | #I, #J, #K |

**Labels:** `component:api`, `type:feature`, `priority:medium`

### Phase 4: Admin UI - List View

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #M | feature | Create Member Directory page prototype | HTML prototype for list view with filters | None (parallel) |
| #N | feature | Create Members/Index Razor Page | Guild member list page with pagination | #I, #M |
| #O | task | Implement member search and filter UI | Sidebar/dropdown filters, search input | #N |
| #P | task | Add bulk selection functionality | Checkbox selection for bulk actions | #N |
| #Q | task | Add navigation link to Member Directory | Update Guild Details page with Members link | #N |

**Labels:** `component:ui`, `type:feature`, `priority:medium`

### Phase 5: Admin UI - Detail View

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #R | feature | Create member detail modal/page prototype | HTML prototype for member detail view | None (parallel) |
| #S | feature | Create Members/Details Razor Page or modal | Member profile, activity, server info | #J, #R |
| #T | task | Add activity summary section | Message count, last seen, command history | #S |
| #U | task | Add Discord profile link | Quick link to Discord profile | #S |

**Labels:** `component:ui`, `type:feature`, `priority:medium`

### Phase 6: Moderation Integration (Deferred)

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #V | feature | Add mod tags display to member cards | Show tags on list and detail views | #291 (Moderation System) |
| #W | feature | Add mod note quick action | Create note from member detail | #291 |
| #X | feature | Add mod history link | Navigate to user's moderation history | #291 |
| #Y | feature | Add watchlist indicator | Visual indicator for watched users | #291 |

**Labels:** `component:ui`, `depends:moderation-system`, `priority:low`

### Phase 7: Export & Polish

| Issue | Type | Title | Description | Dependencies |
|-------|------|-------|-------------|--------------|
| #Z | feature | Implement CSV export from UI | Export button with progress feedback | #K, #P |
| #AA | task | Add loading states and error handling | Skeleton loading, error boundaries | All UI issues |
| #AB | task | Performance optimization for large guilds | Virtual scrolling, query optimization | #N |
| #AC | task | Create Member Directory documentation | User guide and API documentation | All |

**Labels:** `type:task`, `priority:low`

---

## 5. Implementation Risks

### 5.1 Critical Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Discord API rate limit exceeded during sync | Members not visible | Medium | Implement backoff, prioritize active guilds |
| Large guild causes timeout | Page unresponsive | High | Server-side pagination, lazy loading |
| Cache invalidation race conditions | Stale data displayed | Medium | Event sourcing, optimistic locking |

### 5.2 Design Decisions Required

| Decision | Options | Recommendation | Rationale |
|----------|---------|----------------|-----------|
| Member detail: Modal vs. Page | Modal / Full page | Modal with "Open in new tab" | Faster navigation, consistent with other features |
| Activity data source | MessageLog aggregation / Denormalized counters | Denormalized with background sync | Performance critical for large guilds |
| Moderation integration timing | Wait for #291 / Stub now | Stub interfaces now, implement later | Avoid blocking, maintain clean architecture |
| Role display format | List / Grouped / Colored pills | Colored pills (top 5) | Visual, compact, Discord-like |
| Export format | CSV only / CSV+JSON | CSV only (MVP) | Simplicity, Excel compatibility |

### 5.3 Dependencies on Other Epics

| Epic | Dependency Type | Impact |
|------|----------------|--------|
| #291 Moderation System | Soft dependency | Mod notes, tags, history features blocked |
| None | - | Core directory features can proceed independently |

### 5.4 Technical Debt Considerations

- **User entity changes** may require migration of existing data
- **MessageLog queries** for activity summary may need optimization
- **Gateway event handling** adds complexity to bot service

---

## 6. Timeline Estimate

| Phase | Duration | Dependencies | Parallel Work |
|-------|----------|--------------|---------------|
| Phase 1: Data Layer | 3-4 days | None | Prototype (Phase 4) |
| Phase 2: Service Layer | 2-3 days | Phase 1 | Prototype (Phase 4/5) |
| Phase 3: API Layer | 2 days | Phase 2 | - |
| Phase 4: List View UI | 3-4 days | Phase 3 | - |
| Phase 5: Detail View UI | 2-3 days | Phase 4 | - |
| Phase 6: Moderation | Deferred | #291 | - |
| Phase 7: Polish | 2-3 days | All above | - |

**Total Estimate:** 14-19 days (excluding Phase 6)

---

## 7. Acceptance Criteria Summary

### Must Have (MVP)

- [ ] View paginated member list for a guild
- [ ] Search members by username, display name, or ID
- [ ] Filter by role, join date range, activity
- [ ] Sort by join date, username, last active
- [ ] View member detail (profile, roles, activity)
- [ ] Navigate to Member Directory from Guild Details
- [ ] Proper authorization (Moderator+ required)
- [ ] Timestamps displayed in user's local timezone

### Should Have

- [ ] Bulk selection of members
- [ ] CSV export of member list
- [ ] Role colored pills display
- [ ] Activity summary (message count, last seen)
- [ ] Mobile-responsive layout

### Could Have (Deferred to Phase 6)

- [ ] Mod tags displayed on member cards
- [ ] Quick add mod note action
- [ ] Link to mod history
- [ ] Watchlist indicator

---

## 8. Navigation Integration Checklist

- **Route:** `/Guilds/Members/{guildId:long}`
- **Parent Page:** Guild Details (`/Guilds/Details?id={guildId}`)
- **Navigation Update Required:**
  - Add "Members" card/link on Guild Details page (similar to Scheduled Messages, Rat Watch sections)
  - Follow existing pattern from `Details.cshtml` lines 150-190
- **Breadcrumb:** Home > Guilds > [Guild Name] > Members
- **Page Title:** `{GuildName} - Members`

---

## 9. Timezone Handling

Per project standards:
- All timestamps stored in UTC in database
- Display layer converts to user's local timezone using `timezone-utils.js`
- Apply to: `JoinedAt`, `LastActiveAt`, `AccountCreatedAt`
- Format: Consistent with existing pages (e.g., "Dec 30, 2025 3:45 PM")

---

## 10. Files to Create/Modify

### New Files

| Path | Type | Description |
|------|------|-------------|
| `src/DiscordBot.Core/Entities/GuildMember.cs` | Entity | Guild member junction table |
| `src/DiscordBot.Infrastructure/Data/Configurations/GuildMemberConfiguration.cs` | Config | EF Core configuration |
| `src/DiscordBot.Core/Interfaces/IGuildMemberRepository.cs` | Interface | Repository contract |
| `src/DiscordBot.Core/Interfaces/IGuildMemberService.cs` | Interface | Service contract |
| `src/DiscordBot.Infrastructure/Repositories/GuildMemberRepository.cs` | Repository | Data access |
| `src/DiscordBot.Bot/Services/GuildMemberService.cs` | Service | Business logic |
| `src/DiscordBot.Bot/Controllers/MembersController.cs` | Controller | REST API |
| `src/DiscordBot.Core/DTOs/GuildMemberDto.cs` | DTO | API response model |
| `src/DiscordBot.Core/DTOs/GuildMemberQueryDto.cs` | DTO | Query parameters |
| `src/DiscordBot.Bot/Pages/Guilds/Members/Index.cshtml(.cs)` | Page | List view |
| `src/DiscordBot.Bot/Pages/Guilds/Members/Details.cshtml(.cs)` | Page | Detail view (or modal) |
| `src/DiscordBot.Bot/ViewModels/Pages/GuildMemberListViewModel.cs` | ViewModel | List page model |
| `src/DiscordBot.Bot/ViewModels/Pages/GuildMemberDetailViewModel.cs` | ViewModel | Detail view model |
| `src/DiscordBot.Bot/BackgroundServices/MemberSyncService.cs` | Service | Background sync job |
| `docs/prototypes/features/member-directory/index.html` | Prototype | List view prototype |
| `docs/prototypes/features/member-directory/detail.html` | Prototype | Detail modal prototype |

### Modified Files

| Path | Change |
|------|--------|
| `src/DiscordBot.Core/Entities/User.cs` | Add `AccountCreatedAt`, `AvatarHash`, `GlobalDisplayName` |
| `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` | Add `DbSet<GuildMember>` |
| `src/DiscordBot.Bot/Pages/Guilds/Details.cshtml` | Add Members navigation link |
| `src/DiscordBot.Bot/Extensions/ServiceCollectionExtensions.cs` | Register new services |
| `docs/articles/api-endpoints.md` | Document new endpoints |
| `docs/articles/database-schema.md` | Document GuildMember table |

---

*Document Version: 1.0*
*Author: Systems Architect*
*Last Updated: 2025-12-30*
