# Security & Authorization Documentation Consolidation Plan

## Context

The security/role/authorization documentation is spread across 6+ files with significant duplication (role hierarchy defined in 3 places, password requirements in 2, role capabilities in 2). Some important systems have no documentation at all (portal auth flow, guild access levels). This plan consolidates and fills gaps.

---

## Current State

| Document | Lines | Focus |
|----------|-------|-------|
| `authorization-policies.md` | ~1,500 | ASP.NET policies, tag helpers, RBAC |
| `identity-configuration.md` | ~876 | OAuth, Identity, passwords, cookies, entity schema |
| `permissions.md` | ~988 | Discord bot command preconditions (separate system) |
| `bot-verification.md` | ~649 | Alternative code-based account linking |
| `user-management.md` | ~1,143 | Admin UI for managing users/roles |
| `consent-privacy.md` | ~2,500 | GDPR, consent tracking, data lifecycle |

**Key problems:**
- Role hierarchy defined in 3 docs (`authorization-policies`, `identity-configuration`, `user-management`)
- Password requirements in 2 docs
- Role capabilities/permissions matrix in 2 docs
- Portal authorization (`PortalGuildMember` policy) has no dedicated documentation
- `UserGuildAccess` / `GuildAccessLevel` system is undocumented
- `permissions.md` name is misleading — it covers Discord bot preconditions, not web permissions
- Cross-reference format varies (some "Related Documentation" sections, some inline, some missing)

---

## Proposed Structure

### Merge: `authorization-policies.md` + `identity-configuration.md` → `authentication-and-authorization.md`

These two docs share the most overlap and cover the same system (ASP.NET Core Identity + authorization).

**New structure:**

```
# Authentication and Authorization

## Overview
  - Architecture (ApplicationUser vs User entities, linking)
  - Role hierarchy (SINGLE definition — SuperAdmin > Admin > Moderator > Viewer)
  - Role capabilities matrix (SINGLE definition)

## Authentication
  - ASP.NET Identity setup
  - ApplicationUser entity schema
  - Password requirements & lockout
  - Cookie settings
  - Discord OAuth setup (step-by-step, scopes, flow diagram)
  - Discord account linking process

## Authorization Policies
  - Policy definitions (RequireSuperAdmin, RequireAdmin, etc.)
  - Policy-based authorization on pages/controllers
  - Guild access policy (GuildAccessRequirement + handler)
  - Portal authorization (PortalGuildMember policy)  ← NEW
  - UserGuildAccess / GuildAccessLevel system  ← NEW

## Tag Helpers
  - <authorize> / <authorize-view> tag helper
  - if-role attribute tag helper
  - Usage examples

## Authorization in Code
  - IAuthorizationService manual checks
  - Claims transformation (DiscordClaimsTransformation)
  - Authorization handler patterns

## Initial Setup
  - Database migration
  - Role & admin user seeding
  - Production checklist

## Troubleshooting
  - Merged from both docs (deduplicated)

## API Reference
  - UserManager, SignInManager, RoleManager operations
```

**What moves where:**
- Role hierarchy → single definition in Overview
- Password requirements → Authentication section
- OAuth setup → Authentication section
- Policy definitions → Authorization Policies section
- Tag helpers → own section (from authorization-policies.md)
- Troubleshooting → merged and deduplicated
- API reference → kept from identity-configuration.md

**New content to add:**
- Portal authorization flow section (from code: `PortalGuildMemberAuthorizationHandler`, `PortalPageModelBase`)
- Guild access documentation (`UserGuildAccess` entity, `GuildAccessLevel` enum, `GuildAccessAuthorizationHandler`)
- Three authorization worlds summary: web roles vs guild access vs portal membership

### Rename: `permissions.md` → `bot-command-permissions.md`

No content changes — just rename to distinguish from web authorization. This doc covers Discord.NET `PreconditionAttribute` (RequireAdmin, RequireOwner, RateLimit), which is a completely separate system from ASP.NET authorization.

### Keep as-is:
- **`bot-verification.md`** — focused feature doc, no overlap
- **`user-management.md`** — remove duplicated role hierarchy/capabilities, replace with cross-reference to `authentication-and-authorization.md`
- **`consent-privacy.md`** — separate concern (GDPR), no changes needed

### Cross-reference updates:

| File | Change |
|------|--------|
| `user-management.md` | Replace role hierarchy section with link to new doc |
| `user-management.md` | Replace password requirements with link to new doc |
| `bot-verification.md` | Update link from `identity-configuration.md` → `authentication-and-authorization.md` |
| `api-endpoints.md` | Add link to portal authorization section |
| `admin-commands.md` | Add link to `bot-command-permissions.md` |
| `settings-page.md` | Add link to authorization policies section |
| `docs/index.md` | Update navigation links |
| `CLAUDE-REFERENCE.md` | Update doc reference table |
| `.claude/agents/` | Update any agent definitions referencing old doc names |

### Delete after merge:
- `authorization-policies.md`
- `identity-configuration.md`

---

## Estimated Effort

| Task | Effort |
|------|--------|
| Write `authentication-and-authorization.md` (merge + new portal/guild content) | Medium — mostly reorganizing, ~200 lines of new content |
| Rename `permissions.md` → `bot-command-permissions.md` | Trivial |
| Trim duplicated sections from `user-management.md` | Small |
| Update cross-references across ~8 files | Small |
| Delete old files | Trivial |
| **Total** | ~Half a day of focused work |

---

## Verification

- All links in docs resolve correctly (no broken cross-references)
- `CLAUDE-REFERENCE.md` doc table reflects new names
- No duplicated role hierarchy or password requirement definitions remain
- Portal auth flow is documented for the first time
- Guild access system is documented for the first time
