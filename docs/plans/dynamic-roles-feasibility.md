# Feasibility Assessment: Dynamic Roles via UI

## Context

We want to understand the effort, risks, and recommended approach for allowing SuperAdmins to create, delete, and assign custom roles through a web interface — replacing the current hardcoded 4-role system.

**Current state:** Four hardcoded roles (SuperAdmin > Admin > Moderator > Viewer) with hierarchy baked into policies, services, handlers, and views across ~81 files and ~188 role string references.

---

## Current Architecture Summary

### What Exists

| Component | Location | How Roles Are Used |
|-----------|----------|-------------------|
| **Role constants** | `Core/Authorization/Roles.cs` + `Bot/Extensions/IdentitySeeder.cs` (duplicate) | Static string constants: `SuperAdmin`, `Admin`, `Moderator`, `Viewer` |
| **Policy registration** | `Bot/Extensions/IdentityServiceExtensions.cs:151-191` | `RequireRole()` with hardcoded role lists per policy |
| **Authorization handlers** | `Bot/Authorization/GuildAccessAuthorizationHandler.cs`, `PortalGuildMemberAuthorizationHandler.cs` | `IsInRole("SuperAdmin")` / `IsInRole("Admin")` bypass checks |
| **Page/controller attributes** | 81 `.cshtml.cs` and controller files | `[Authorize(Policy = "RequireAdmin")]` etc. |
| **Tag helpers** | `Bot/TagHelpers/AuthorizeTagHelper.cs` | `<authorize policy="...">` and `if-role="..."` — already supports dynamic role names |
| **Sidebar navigation** | `Pages/Shared/_Sidebar.cshtml` | 6 hardcoded `<authorize policy="...">` checks |
| **User management service** | `Bot/Services/UserManagementService.cs` | Hardcoded `AllRoles` array, switch-based hierarchy in `GetAvailableRolesAsync()` |
| **User management UI** | `Pages/Admin/Users/Create.cshtml.cs`, `Edit.cshtml.cs` | Dropdown populated from `GetAvailableRolesAsync()` — already dynamic-capable |
| **Database** | Standard ASP.NET Identity | `AspNetRoles`, `AspNetUserRoles` tables — no custom role entity |
| **Guild access** | `Core/Entities/UserGuildAccess.cs` | Separate system with `GuildAccessLevel` enum (0-3), not tied to Identity roles |

### What's Well-Positioned for Dynamic Roles

- **ASP.NET Identity already supports arbitrary roles** — `AspNetRoles` table can hold any number of roles
- **Tag helpers** accept role names as strings at runtime
- **User management UI** already loads roles dynamically from a service method
- **Role seeding** is idempotent — new roles won't conflict with existing ones

### What's Tightly Coupled (The Hard Parts)

1. **Policy registration is static** — `AddAuthorization()` runs once at startup. New roles added at runtime won't be reflected in policies until app restart.

2. **Hierarchy is implicit** — expressed by listing roles in each policy (`RequireAdmin` = SuperAdmin + Admin). There's no "role level" or "role rank" concept in the database.

3. **~188 hardcoded role references** across the codebase — `IsInRole("SuperAdmin")`, switch statements on role names, etc.

4. **Authorization attributes are compile-time** — `[Authorize(Policy = "RequireAdmin")]` can't be changed at runtime.

---

## Effort Breakdown

### Phase 1: Foundation (Medium effort, ~1-2 weeks)

**Goal:** Make roles data-driven without changing authorization behavior.

| Task | Effort | Files |
|------|--------|-------|
| Add `RoleLevel` (int) column to `AspNetRoles` via custom `ApplicationRole : IdentityRole` | Small | Core entity, DbContext, migration |
| Consolidate duplicate `Roles` constants into one source | Small | `Roles.cs`, `IdentitySeeder.cs` |
| Seed existing 4 roles with level values (SuperAdmin=100, Admin=75, Moderator=50, Viewer=25) | Small | `IdentitySeeder.cs` |
| Replace hardcoded policy registration with a **custom `IAuthorizationHandler`** that checks role level dynamically | Medium | `IdentityServiceExtensions.cs`, new handler |

**Key design decision:** Instead of `RequireRole("SuperAdmin", "Admin")`, policies would use a `MinimumRoleLevelRequirement(75)` handler that checks if the user's highest role level meets the threshold. This makes policies work with any role that has the right level — no restart needed.

### Phase 2: Management UI (Medium effort, ~1-2 weeks)

**Goal:** SuperAdmin interface to create/edit/delete roles.

| Task | Effort | Files |
|------|--------|-------|
| Role management page (CRUD) | Medium | New Razor Page or Blazor component |
| Role form: name, description, level (numeric), color/icon | Small | New view model, form |
| Protect built-in roles from deletion (SuperAdmin, Viewer at minimum) | Small | Service validation |
| Update user management UI to show all roles including custom | Small | Already dynamic, minor changes |

### Phase 3: Refactor Role Checks (High effort, ~3-4 weeks)

**Goal:** Replace hardcoded role name checks with level-based checks.

| Task | Effort | Files |
|------|--------|-------|
| Replace `IsInRole("SuperAdmin")` bypass checks in authorization handlers | Medium | 3-4 handler files |
| Replace `IsInRole()` checks in services (NotificationService, UserManagementService, etc.) | Medium | 5-10 service files |
| Update sidebar/navigation to use level-based policies | Small | `_Sidebar.cshtml` |
| Audit and update all 81 page-level `[Authorize]` attributes | High | 81 files (but mostly mechanical) |
| Update `GuildAccessLevel` enum to reference role levels or keep separate | Medium | Authorization handlers |

### Phase 4: Advanced Features (Optional, ~2-3 weeks)

| Task | Effort |
|------|--------|
| Granular permission assignments per role (e.g., "can manage scheduled messages") | High — needs new `RolePermission` table and permission checks |
| Per-guild role overrides (different role meanings per guild) | Very High — fundamental architecture change |
| Role assignment audit logging | Small — extend existing audit system |

---

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Breaking existing authorization** during refactor | High | Comprehensive test coverage before refactoring; feature flag to toggle old vs new system |
| **SuperAdmin lockout** if role level misconfigured | High | Hardcode SuperAdmin as immutable; always grant level 100 |
| **Performance** — role level lookups on every request | Low | Cache role levels in claims at login time |
| **Migration complexity** — dual database (SQLite + PostgreSQL) | Medium | Must create migrations for both providers |
| **Stale policies after role changes** | Medium | Level-based handler resolves at runtime, not startup |

---

## Recommended Approach

**Phase 1 + Phase 2 first** (2-4 weeks), then assess Phase 3.

The key architectural change is replacing the static policy model with a **level-based authorization handler**:

```
Current:  [Authorize(Policy = "RequireAdmin")] → checks if user has "SuperAdmin" OR "Admin" role
Proposed: [Authorize(Policy = "RequireAdmin")] → checks if user's highest role level >= 75
```

This means:
- Existing `[Authorize]` attributes **don't need to change** in Phase 1
- Custom roles with level 75+ automatically get "Admin" access
- Custom roles with level 50-74 automatically get "Moderator" access
- The UI just needs to let SuperAdmins set the level when creating a role

Phase 3 (refactoring hardcoded `IsInRole` calls) can happen incrementally — each service/handler can be migrated independently.

---

## Alternative Considered: Permission-Based (PBAC)

A full permission-based system (e.g., `can_view_logs`, `can_edit_guild_settings`) would be more flexible but requires:
- New `Permission` and `RolePermission` tables
- Replacing every `[Authorize(Policy = "RequireAdmin")]` with `[Authorize(Policy = "CanEditGuildSettings")]`
- Building a full permission matrix UI
- **Estimated effort: 10+ weeks**

Not recommended as an initial approach. The level-based system gets 80% of the value at 20% of the cost. PBAC can be layered on later if needed.

---

## Summary

| Question | Answer |
|----------|--------|
| **Can we add custom roles today without code changes?** | No — roles are hardcoded in policies and services |
| **What's the minimum viable change?** | Phase 1: `ApplicationRole` entity with level + dynamic authorization handler (~1-2 weeks) |
| **What's the full effort for UI-managed dynamic roles?** | Phases 1-3: ~6-8 weeks total |
| **Biggest risk?** | Breaking existing authorization during the Phase 3 refactor |
| **Recommended first step?** | Phase 1 foundation + Phase 2 management UI, then incremental Phase 3 |
