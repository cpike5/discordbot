# Authorization Policies

**Last Updated:** 2026-01-15
**Epic Reference:** [Epic 2: Authentication and Authorization](../archive/plans/epic-2-auth-architecture-plan.md) (archived)
**Related Issues:** #65 (Authorization Policies)
**Prerequisites:** [Identity Configuration](identity-configuration.md)

---

## Overview

The Discord Bot Management System implements a comprehensive role-based access control (RBAC) system that governs access to features and resources in the admin UI. The authorization system uses ASP.NET Core's policy-based authorization combined with custom authorization handlers to provide both global role-based access and guild-specific permissions.

### Key Features

- **Four-tier role hierarchy**: SuperAdmin → Admin → Moderator → Viewer
- **Policy-based authorization**: Declarative `[Authorize]` attributes on pages and controllers
- **Guild-specific access control**: Fine-grained permissions per Discord server
- **Custom tag helpers**: Conditional UI rendering based on policies and roles
- **Claims transformation**: Automatic enrichment of user identity with Discord data
- **Fallback policy**: All pages require authentication by default

### When to Use Each Authorization Mechanism

| Use Case | Mechanism | Example |
|----------|-----------|---------|
| Page-level protection | `[Authorize(Policy = "...")]` attribute | Restrict entire page to Admins |
| Conditional UI elements | `<authorize-view>` or `<require-role>` tag helpers | Show delete button only to SuperAdmins |
| Guild-specific pages | `[Authorize(Policy = "GuildAccess")]` | Guild settings page accessible only to linked users |
| Manual checks in code | Inject `IAuthorizationService` | Complex business logic with multiple conditions |

---

## Role Hierarchy

The system defines four hierarchical roles, where higher roles inherit permissions from lower roles. This cumulative permission model simplifies policy definition and ensures consistent access control.

### Role Definitions

| Role | Level | Description | Typical Use Cases |
|------|-------|-------------|-------------------|
| **SuperAdmin** | 4 | System owner with full access to all features | User management, system configuration, access all guilds |
| **Admin** | 3 | Guild administrator with full CRUD access | Guild management, bot control, configuration |
| **Moderator** | 2 | Limited admin with edit but no delete permissions | Edit guild settings, moderate content |
| **Viewer** | 1 | Read-only access to dashboards and logs | View dashboards, read logs, monitor bot status |

### Visual Hierarchy

```
SuperAdmin (Full System Access)
    ├── User management
    ├── System configuration
    ├── Access to all guilds (bypasses guild-specific permissions)
    └── All Admin permissions ↓

Admin (Guild Administration)
    ├── Guild CRUD operations
    ├── Bot control and settings
    ├── Cannot manage SuperAdmins
    └── All Moderator permissions ↓

Moderator (Limited Administration)
    ├── Edit guild settings
    ├── Cannot delete guilds
    ├── Cannot manage users
    └── All Viewer permissions ↓

Viewer (Read-Only Access)
    ├── View dashboards
    ├── Read logs
    └── Monitor bot status
```

### Role Constants

Role names are defined in the `Roles` class for compile-time safety:

```csharp
// Location: DiscordBot.Core/Authorization/Roles.cs
namespace DiscordBot.Core.Authorization;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { SuperAdmin, Admin, Moderator, Viewer };
}
```

**Usage:**
```csharp
using DiscordBot.Core.Authorization;

[Authorize(Roles = Roles.SuperAdmin)]
public class UserManagementModel : PageModel { }
```

---

## Authorization Policies

Policies are registered in `Program.cs` during application startup and provide a declarative way to enforce authorization requirements.

### Policy Definitions

#### RequireSuperAdmin

**Description:** Grants access only to SuperAdmin role. Use for system-level administration features.

**Configuration:**
```csharp
options.AddPolicy("RequireSuperAdmin", policy =>
    policy.RequireRole(Roles.SuperAdmin));
```

**Allowed Roles:** SuperAdmin only

**Use Cases:**
- User management (creating/deleting admin accounts)
- System configuration changes
- Role assignment and permission management
- Audit log access

**Example:**
```csharp
[Authorize(Policy = "RequireSuperAdmin")]
public class UserManagementModel : PageModel
{
    // Only SuperAdmins can access this page
}
```

---

#### RequireAdmin

**Description:** Grants access to SuperAdmin and Admin roles. Use for guild administration features.

**Configuration:**
```csharp
options.AddPolicy("RequireAdmin", policy =>
    policy.RequireRole(Roles.SuperAdmin, Roles.Admin));
```

**Allowed Roles:** SuperAdmin, Admin

**Use Cases:**
- Guild CRUD operations (create, read, update, delete guilds)
- Bot control (start, stop, restart)
- Configuration management
- Developer tools and component testing

**Example:**
```csharp
[Authorize(Policy = "RequireAdmin")]
public class ComponentsModel : PageModel
{
    // Admins and SuperAdmins can access component testing tools
}
```

---

#### RequireModerator

**Description:** Grants access to SuperAdmin, Admin, and Moderator roles. Use for content moderation and settings editing.

**Configuration:**
```csharp
options.AddPolicy("RequireModerator", policy =>
    policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Moderator));
```

**Allowed Roles:** SuperAdmin, Admin, Moderator

**Use Cases:**
- Edit guild settings (but not delete)
- Content moderation actions
- Command configuration
- Log filtering and search

**Example:**
```csharp
[Authorize(Policy = "RequireModerator")]
public IActionResult OnPostUpdateSettings()
{
    // Moderators can update settings but typically cannot delete
}
```

---

#### RequireViewer

**Description:** Grants access to all authenticated users with any assigned role. Use for read-only features.

**Configuration:**
```csharp
options.AddPolicy("RequireViewer", policy =>
    policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Moderator, Roles.Viewer));
```

**Allowed Roles:** All roles (SuperAdmin, Admin, Moderator, Viewer)

**Use Cases:**
- Dashboard viewing
- Log reading (without filtering capabilities)
- Bot status monitoring
- Public documentation

**Example:**
```csharp
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    // All authenticated users with any role can view dashboard
}
```

---

#### GuildAccess

**Description:** Custom policy that enforces guild-specific access control. Users must have explicit access granted to a specific guild through the `UserGuildAccess` table. SuperAdmins automatically have access to all guilds.

**Configuration:**
```csharp
options.AddPolicy("GuildAccess", policy =>
    policy.Requirements.Add(new GuildAccessRequirement()));
```

**Authorization Logic:**
1. Check if user is SuperAdmin → Grant access to all guilds
2. Extract `guildId` from route parameters or query string
3. Look up `UserGuildAccess` record for user + guild
4. Verify user's access level meets minimum requirement
5. Grant or deny access accordingly

**Use Cases:**
- Guild-specific settings pages
- Per-guild command configuration
- Guild member management
- Guild-specific logs and analytics

**Example:**
```csharp
// Page route: /Guilds/{guildId}/Settings
[Authorize(Policy = "GuildAccess")]
public class GuildSettingsModel : PageModel
{
    public async Task<IActionResult> OnGetAsync(ulong guildId)
    {
        // User must have access to this specific guild
        // (SuperAdmins bypass this check)
    }
}
```

---

### Fallback Policy

**Description:** Default policy applied to all pages that don't have an explicit `[Authorize]` or `[AllowAnonymous]` attribute.

**Configuration:**
```csharp
options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
```

**Effect:** All pages require authentication by default. Use `[AllowAnonymous]` to explicitly allow unauthenticated access (e.g., login, register pages).

**Example:**
```csharp
// No [Authorize] attribute needed - fallback policy applies
public class DashboardModel : PageModel { }

// Explicitly allow anonymous access
[AllowAnonymous]
public class LoginModel : PageModel { }
```

---

## Using Authorization in Razor Pages

### Page-Level Authorization

Apply the `[Authorize]` attribute to the PageModel class to protect the entire page.

**Basic Role-Based Authorization:**
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages;

[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminPanelModel : PageModel
{
    public void OnGet()
    {
        // Only Admins and SuperAdmins can access
    }
}
```

**Policy-Based Authorization (Recommended):**
```csharp
using DiscordBot.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages;

[Authorize(Policy = "RequireAdmin")]
public class GuildManagementModel : PageModel
{
    public void OnGet()
    {
        // Uses RequireAdmin policy (SuperAdmin + Admin)
    }
}
```

### Handler-Level Authorization

Apply authorization to specific page handlers for finer-grained control.

```csharp
using DiscordBot.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds;

[Authorize(Policy = "RequireViewer")]
public class GuildDetailsModel : PageModel
{
    // All users can view
    public void OnGet(ulong guildId) { }

    // Only moderators can edit
    [Authorize(Policy = "RequireModerator")]
    public IActionResult OnPost(ulong guildId) { }

    // Only admins can delete
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult OnPostDelete(ulong guildId) { }
}
```

### Programmatic Authorization Checks

Inject `IAuthorizationService` to perform authorization checks in code.

```csharp
using DiscordBot.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages;

public class ConditionalActionModel : PageModel
{
    private readonly IAuthorizationService _authorizationService;

    public ConditionalActionModel(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, "RequireAdmin");

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        // Perform admin action
        return RedirectToPage("./Success");
    }
}
```

---

## Portal Pages: AllowAnonymous with Manual Authorization

Portal pages use a specialized authorization pattern that combines `[AllowAnonymous]` with manual authorization checks in the page handler. This pattern enables a landing page UX where unauthenticated users see a promotional page instead of being redirected to login.

### Why This Pattern?

Standard ASP.NET Core authorization redirects unauthenticated users to the login page. For portal pages, we want a different experience:

| User State | Standard Behavior | Portal Behavior |
|------------|-------------------|-----------------|
| Unauthenticated | Redirect to login | Show landing page with login CTA |
| Authenticated, not guild member | 403 Forbidden | 403 Forbidden |
| Authenticated, guild member | Show content | Show full portal |

### Security Considerations

> **⚠️ Important:** This pattern requires careful implementation. Unlike declarative `[Authorize]` attributes, manual authorization checks can be bypassed if not properly implemented in every handler.

**Risks:**
- Manual checks are more error-prone than declarative attributes
- New handlers (OnPostAsync, OnGetDownloadAsync, etc.) must include authorization checks
- Code reviews should verify authorization is enforced consistently

**Mitigations:**
1. Use `PortalPageModelBase` which provides standardized authorization via `CheckPortalAuthorizationAsync()`
2. All Portal pages must inherit from `PortalPageModelBase`
3. Every handler must call `CheckPortalAuthorizationAsync()` before accessing protected resources

### Implementation Pattern

Portal pages follow this standardized implementation:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Pages.Portal.Feature;

/// <summary>
/// Portal page with landing page UX for unauthenticated users.
/// </summary>
[AllowAnonymous]  // Allows unauthenticated access to show landing page
public class IndexModel : PortalPageModelBase
{
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        // 1. Perform authorization check FIRST
        var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "Feature", cancellationToken);

        // 2. Handle non-success results
        var actionResult = GetAuthResultAction(authResult);
        if (actionResult != null)
        {
            return actionResult;  // Returns NotFound or Forbid
        }

        // 3. For landing page, return early (no sensitive data loaded)
        if (authResult == PortalAuthResult.ShowLandingPage)
        {
            return Page();
        }

        // 4. User is authorized - load protected resources
        // ... load guild-specific data ...

        return Page();
    }
}
```

### PortalPageModelBase

The base class (`src/DiscordBot.Bot/Pages/Portal/PortalPageModelBase.cs`) provides:

**Common Properties:**
- `GuildId`, `GuildName`, `GuildIconUrl` - Guild information for display
- `IsAuthenticated` - Whether user has logged in via Discord OAuth
- `IsAuthorized` - Whether authenticated user is a guild member
- `LoginUrl` - Pre-built login URL with return path

**Authorization Results:**
```csharp
protected enum PortalAuthResult
{
    GuildNotFound,      // Return NotFound()
    ShowLandingPage,    // User not authenticated - show landing
    NotGuildMember,     // Return Forbid()
    Authorized          // User can access protected content
}
```

**Helper Methods:**
- `CheckPortalAuthorizationAsync()` - Performs full authorization flow
- `GetAuthResultAction()` - Converts auth result to IActionResult
- `BuildVoiceChannelList()` - Helper for audio portal pages

### Authorization Flow

```
Request to /Portal/Feature/{guildId}
            │
            ▼
    ┌───────────────────┐
    │ Guild exists in   │──No──► Return NotFound()
    │ DB and Discord?   │
    └───────────────────┘
            │ Yes
            ▼
    ┌───────────────────┐
    │ User              │──No──► Return Page()
    │ authenticated?    │        (Landing page UX)
    └───────────────────┘
            │ Yes
            ▼
    ┌───────────────────┐
    │ User has linked   │──No──► Return Page()
    │ Discord account?  │        (Landing page UX)
    └───────────────────┘
            │ Yes
            ▼
    ┌───────────────────┐
    │ User is member    │──No──► Return Forbid()
    │ of guild?         │
    └───────────────────┘
            │ Yes
            ▼
    Load protected data and
    return Page() (Full portal)
```

### Affected Pages

Pages using this pattern:

| Page | Location | Feature |
|------|----------|---------|
| Soundboard Portal | `Pages/Portal/Soundboard/Index.cshtml.cs` | Guild member soundboard access |
| TTS Portal | `Pages/Portal/TTS/Index.cshtml.cs` | Guild member text-to-speech |
| Public Leaderboard | `Pages/Guilds/PublicLeaderboard.cshtml.cs` | Public Rat Watch leaderboard |

### Adding New Portal Pages

When creating a new portal page:

1. **Inherit from `PortalPageModelBase`:**
   ```csharp
   public class IndexModel : PortalPageModelBase
   ```

2. **Apply `[AllowAnonymous]` attribute:**
   ```csharp
   [AllowAnonymous]
   public class IndexModel : PortalPageModelBase
   ```

3. **Call `CheckPortalAuthorizationAsync()` in every handler:**
   ```csharp
   public async Task<IActionResult> OnGetAsync(ulong guildId, ...)
   {
       var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "FeatureName", cancellationToken);
       // Handle result...
   }

   public async Task<IActionResult> OnPostAsync(ulong guildId, ...)
   {
       var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "FeatureName", cancellationToken);
       // Handle result - POST handlers should NOT return landing page
       if (authResult != PortalAuthResult.Authorized)
       {
           return GetAuthResultAction(authResult) ?? Forbid();
       }
       // Process POST...
   }
   ```

4. **Never load sensitive data before authorization check:**
   ```csharp
   // ❌ WRONG - loads data before authorization
   public async Task<IActionResult> OnGetAsync(ulong guildId)
   {
       var sensitiveData = await _service.GetGuildDataAsync(guildId);
       var (authResult, _) = await CheckPortalAuthorizationAsync(guildId, "Feature");
       // ...
   }

   // ✓ CORRECT - authorization first, then load data
   public async Task<IActionResult> OnGetAsync(ulong guildId)
   {
       var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "Feature");
       if (authResult != PortalAuthResult.Authorized)
       {
           return GetAuthResultAction(authResult) ?? Page();
       }
       var sensitiveData = await _service.GetGuildDataAsync(guildId);
       // ...
   }
   ```

### Code Review Checklist

When reviewing portal pages, verify:

- [ ] Page inherits from `PortalPageModelBase`
- [ ] `[AllowAnonymous]` attribute is applied
- [ ] Every handler calls `CheckPortalAuthorizationAsync()` before accessing protected resources
- [ ] POST handlers do not return landing page for unauthenticated users (return Forbid instead)
- [ ] No sensitive data is loaded before authorization check completes
- [ ] `GetAuthResultAction()` result is checked and returned when non-null

---

## Using Tag Helpers

Tag helpers provide a declarative way to show or hide UI elements based on authorization policies or roles. They perform server-side authorization checks and completely remove unauthorized content from the HTML output.

### Available Tag Helpers

The system provides two tag helpers defined in `DiscordBot.Bot.TagHelpers`:

1. **`<authorize-view>`** - Policy-based or role-based conditional rendering
2. **`<require-role>`** - Role-based conditional rendering (simpler syntax)

### Registering Tag Helpers

Tag helpers are automatically registered in `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, DiscordBot.Bot
```

---

### authorize-view Tag Helper

Conditionally renders content based on an authorization policy or role(s).

**Attributes:**
- `policy` - Name of the authorization policy to check
- `roles` - Comma-separated list of roles (any match grants access)

**Behavior:**
- If user is not authenticated → Content is suppressed
- If user doesn't meet policy/role requirements → Content is suppressed
- If user meets requirements → Content is rendered
- The `<authorize-view>` tag itself is not rendered in the output

#### Policy-Based Examples

```cshtml
<!-- Show admin controls only to admins -->
<authorize-view policy="RequireAdmin">
    <div class="admin-controls">
        <button type="submit" class="btn-danger">Delete Guild</button>
        <a asp-page="./Settings">Configure Bot</a>
    </div>
</authorize-view>

<!-- Show user management link only to SuperAdmins -->
<authorize-view policy="RequireSuperAdmin">
    <li class="nav-item">
        <a asp-page="/Admin/Users">User Management</a>
    </li>
</authorize-view>

<!-- Show guild access controls for guild-specific pages -->
<authorize-view policy="GuildAccess">
    <section class="guild-settings">
        <h2>Guild Settings</h2>
        <p>You have access to manage this guild.</p>
    </section>
</authorize-view>
```

#### Role-Based Examples

```cshtml
<!-- Show content to SuperAdmins OR Admins -->
<authorize-view roles="SuperAdmin,Admin">
    <div class="alert alert-info">
        You have administrative privileges.
    </div>
</authorize-view>

<!-- Show content to any authenticated user (when no policy/roles specified) -->
<authorize-view>
    <p>Welcome, authenticated user!</p>
</authorize-view>
```

---

### require-role Tag Helper

Simpler tag helper specifically for role-based authorization. Use when you only need role checks without policy complexity.

**Attributes:**
- `roles` - Comma-separated list of roles (required, any match grants access)

**Behavior:**
- Same as `<authorize-view roles="...">` but more explicit naming
- The `<require-role>` tag itself is not rendered in the output

#### Examples

```cshtml
<!-- Show moderator tools to Moderators, Admins, and SuperAdmins -->
<require-role roles="Moderator,Admin,SuperAdmin">
    <div class="moderator-panel">
        <button class="btn-warning">Edit Settings</button>
    </div>
</require-role>

<!-- Show admin-only danger zone -->
<require-role roles="SuperAdmin,Admin">
    <section class="danger-zone">
        <h3>Danger Zone</h3>
        <button class="btn-danger">Delete Server</button>
    </section>
</require-role>

<!-- Show SuperAdmin-only system controls -->
<require-role roles="SuperAdmin">
    <a asp-page="/System/Configuration">System Configuration</a>
</require-role>
```

---

### Nested Tag Helpers

Tag helpers can be nested for complex authorization scenarios.

```cshtml
<!-- Outer: Require at least Viewer role -->
<authorize-view policy="RequireViewer">
    <div class="card">
        <h2>Guild Details</h2>
        <p>Name: @Model.Guild.Name</p>

        <!-- Inner: Show edit button only to Moderators+ -->
        <require-role roles="Moderator,Admin,SuperAdmin">
            <button class="btn-primary">Edit Guild</button>
        </require-role>

        <!-- Inner: Show delete button only to Admins+ -->
        <require-role roles="Admin,SuperAdmin">
            <button class="btn-danger">Delete Guild</button>
        </require-role>
    </div>
</authorize-view>
```

---

### Best Practices for Tag Helpers

1. **Server-side enforcement is required**: Tag helpers only hide UI elements. Always enforce authorization in page handlers with `[Authorize]` attributes.

2. **Use policies over role lists**: Prefer `<authorize-view policy="RequireAdmin">` over `<authorize-view roles="SuperAdmin,Admin">` for better maintainability.

3. **Performance considerations**: Tag helpers run on every page render. For frequently-checked permissions, consider caching authorization results in the PageModel.

4. **Accessibility**: When hiding interactive elements, ensure keyboard navigation and screen readers still provide a good experience.

5. **Provide feedback**: Consider showing disabled states instead of hiding elements when users should know the feature exists but is restricted.

```cshtml
<!-- Good: Show disabled state with explanation -->
@if (User.IsInRole("Admin"))
{
    <button type="submit" class="btn-danger">Delete</button>
}
else
{
    <button type="button" class="btn-secondary" disabled title="Requires Admin role">
        Delete (Admin Only)
    </button>
}
```

---

## Guild-Specific Authorization

Guild-specific authorization allows fine-grained access control per Discord server. This enables scenarios where different users have different permission levels for different guilds.

### How It Works

1. **User Authentication**: User logs in via ASP.NET Identity
2. **Guild Access Linking**: `UserGuildAccess` records are created linking users to guilds
3. **Authorization Check**: `GuildAccessAuthorizationHandler` verifies user has access to specific guild
4. **SuperAdmin Bypass**: SuperAdmins automatically have access to all guilds

### UserGuildAccess Entity

The `UserGuildAccess` entity represents a user's access to a specific guild.

**Location:** `DiscordBot.Core/Entities/UserGuildAccess.cs`

```csharp
public class UserGuildAccess
{
    /// <summary>The ApplicationUser ID (ASP.NET Core Identity user).</summary>
    public string ApplicationUserId { get; set; }

    /// <summary>Navigation property to the ApplicationUser.</summary>
    public ApplicationUser ApplicationUser { get; set; }

    /// <summary>The Guild ID (Discord snowflake).</summary>
    public ulong GuildId { get; set; }

    /// <summary>Navigation property to the Guild.</summary>
    public Guild Guild { get; set; }

    /// <summary>The user's access level for this guild.</summary>
    public GuildAccessLevel AccessLevel { get; set; }

    /// <summary>When the access was granted.</summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>Who granted the access (nullable for system-granted).</summary>
    public string? GrantedByUserId { get; set; }
}
```

**Database Schema:**
- **Primary Key**: Composite key (`ApplicationUserId`, `GuildId`)
- **Foreign Keys**:
  - `ApplicationUserId` → `AspNetUsers.Id` (Cascade delete)
  - `GuildId` → `Guilds.Id` (Cascade delete)
- **Indexes**: Automatically indexed on primary key for efficient lookups

---

### GuildAccessLevel Enum

Defines hierarchical access levels for guild-specific permissions.

**Location:** `DiscordBot.Core/Entities/UserGuildAccess.cs`

```csharp
public enum GuildAccessLevel
{
    /// <summary>Read-only access to guild data (dashboards, logs).</summary>
    Viewer = 0,

    /// <summary>Can edit guild settings but cannot delete or manage users.</summary>
    Moderator = 1,

    /// <summary>Full administrative access to the guild (CRUD, bot control).</summary>
    Admin = 2,

    /// <summary>Guild owner with all permissions (typically Discord server owner).</summary>
    Owner = 3
}
```

**Hierarchy:** Viewer < Moderator < Admin < Owner

**Note:** Guild-specific access levels are independent from application-wide roles. A user with `GuildAccessLevel.Admin` for a guild is not necessarily an `Admin` in the application role system.

---

### GuildAccessRequirement

Authorization requirement that specifies the minimum guild access level needed.

**Location:** `DiscordBot.Bot/Authorization/GuildAccessRequirement.cs`

```csharp
public class GuildAccessRequirement : IAuthorizationRequirement
{
    /// <summary>Minimum access level required. Defaults to Viewer.</summary>
    public GuildAccessLevel MinimumLevel { get; }

    public GuildAccessRequirement(GuildAccessLevel minimumLevel = GuildAccessLevel.Viewer)
    {
        MinimumLevel = minimumLevel;
    }
}
```

**Usage:**
```csharp
// In Program.cs policy configuration
options.AddPolicy("GuildAdmin", policy =>
    policy.Requirements.Add(new GuildAccessRequirement(GuildAccessLevel.Admin)));

// In Razor Page
[Authorize(Policy = "GuildAdmin")]
public class GuildSettingsModel : PageModel { }
```

---

### GuildAccessAuthorizationHandler

Custom authorization handler that enforces guild-specific access control.

**Location:** `DiscordBot.Bot/Authorization/GuildAccessAuthorizationHandler.cs`

**Authorization Flow:**

1. **Check SuperAdmin**: If user is SuperAdmin → Grant access immediately
2. **Extract Guild ID**: Read `guildId` from route parameters or query string
3. **Validate Guild ID**: Ensure valid ulong Discord snowflake
4. **Query Database**: Look up `UserGuildAccess` for user + guild combination
5. **Compare Access Levels**: Verify user's access level >= required minimum level
6. **Grant or Deny**: Succeed or fail the authorization requirement

**Code Example:**
```csharp
protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    GuildAccessRequirement requirement)
{
    // SuperAdmins bypass guild-specific checks
    if (context.User.IsInRole(Roles.SuperAdmin))
    {
        context.Succeed(requirement);
        return;
    }

    // Extract guildId from route: /Guilds/{guildId}/Settings
    var guildIdString = _httpContext.Request.RouteValues["guildId"]?.ToString();
    if (!ulong.TryParse(guildIdString, out var guildId))
    {
        return; // Fail silently - no valid guild ID
    }

    // Check database for access grant
    var access = await _dbContext.Set<UserGuildAccess>()
        .FirstOrDefaultAsync(a =>
            a.ApplicationUserId == userId &&
            a.GuildId == guildId);

    if (access?.AccessLevel >= requirement.MinimumLevel)
    {
        context.Succeed(requirement);
    }
}
```

---

### Granting Guild Access Programmatically

To grant a user access to a specific guild, create a `UserGuildAccess` record.

**Example Service Method:**
```csharp
public async Task GrantGuildAccessAsync(
    string userId,
    ulong guildId,
    GuildAccessLevel accessLevel,
    string? grantedByUserId = null)
{
    var access = new UserGuildAccess
    {
        ApplicationUserId = userId,
        GuildId = guildId,
        AccessLevel = accessLevel,
        GrantedAt = DateTime.UtcNow,
        GrantedByUserId = grantedByUserId
    };

    _dbContext.UserGuildAccess.Add(access);
    await _dbContext.SaveChangesAsync();
}
```

**Example Page Handler:**
```csharp
[Authorize(Policy = "RequireSuperAdmin")]
public async Task<IActionResult> OnPostGrantAccessAsync(
    string userId, ulong guildId, GuildAccessLevel level)
{
    var access = new UserGuildAccess
    {
        ApplicationUserId = userId,
        GuildId = guildId,
        AccessLevel = level,
        GrantedAt = DateTime.UtcNow,
        GrantedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
    };

    await _dbContext.UserGuildAccess.AddAsync(access);
    await _dbContext.SaveChangesAsync();

    return RedirectToPage("./GuildPermissions", new { guildId });
}
```

---

### Route Parameter Detection

The `GuildAccessAuthorizationHandler` automatically detects guild IDs from:

1. **Route parameters**: `/Guilds/{guildId}/Settings`
2. **Query strings**: `/Guilds/Settings?guildId=123456789`

**Recommended Pattern:** Use route parameters for cleaner URLs and better SEO.

```csharp
// Razor Page route configuration
@page "/Guilds/{guildId:long}/Settings"
@model GuildSettingsModel

// Route parameter automatically available
public async Task<IActionResult> OnGetAsync(ulong guildId)
{
    // guildId is extracted and validated by handler
}
```

---

## Claims Transformation

Claims transformation enriches the user's identity with Discord-specific data during authentication. This process runs automatically on every request for authenticated users.

### DiscordClaimsTransformation

**Location:** `DiscordBot.Bot/Authorization/DiscordClaimsTransformation.cs`

**Purpose:** Add custom claims to user identity based on `ApplicationUser` data, particularly Discord account linkage information.

### Claims Added

The transformation adds the following custom claims when available:

| Claim Type | Description | Source | Example Value |
|------------|-------------|--------|---------------|
| `DiscordUserId` | Discord snowflake user ID | `ApplicationUser.DiscordUserId` | `"123456789012345678"` |
| `DiscordUsername` | Discord username | `ApplicationUser.DiscordUsername` | `"Username#1234"` |
| `HasLinkedDiscord` | Whether Discord is linked | Computed | `"true"` |
| `DisplayName` | User's display name | `ApplicationUser.DisplayName` | `"John Doe"` |
| `DiscordClaimsTransformed` | Transformation marker | Computed | `"true"` |

### Transformation Process

1. **Check Authentication**: Only transform authenticated users
2. **Check Marker**: Skip if already transformed (prevent duplicate work)
3. **Load User**: Query `ApplicationUser` by user ID from database
4. **Add Discord Claims**: If `DiscordUserId` is set, add Discord-related claims
5. **Add Display Name**: Add display name claim if available
6. **Set Marker**: Add transformation marker to prevent re-running

### Code Implementation

```csharp
public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
{
    if (principal.Identity?.IsAuthenticated != true)
        return principal;

    // Skip if already transformed
    if (principal.HasClaim(c => c.Type == "DiscordClaimsTransformed"))
        return principal;

    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
        return principal;

    var identity = (ClaimsIdentity)principal.Identity;
    identity.AddClaim(new Claim("DiscordClaimsTransformed", "true"));

    // Add Discord claims if linked
    if (user.DiscordUserId.HasValue)
    {
        identity.AddClaim(new Claim("DiscordUserId", user.DiscordUserId.Value.ToString()));
        identity.AddClaim(new Claim("DiscordUsername", user.DiscordUsername ?? ""));
        identity.AddClaim(new Claim("HasLinkedDiscord", "true"));
    }

    if (!string.IsNullOrEmpty(user.DisplayName))
    {
        identity.AddClaim(new Claim("DisplayName", user.DisplayName));
    }

    return principal;
}
```

---

### Accessing Claims in Code

**In Razor Pages:**
```csharp
public class ProfileModel : PageModel
{
    public string? DiscordUsername { get; set; }
    public bool HasLinkedDiscord { get; set; }

    public void OnGet()
    {
        DiscordUsername = User.FindFirstValue("DiscordUsername");
        HasLinkedDiscord = User.HasClaim("HasLinkedDiscord", "true");
    }
}
```

**In Razor Views:**
```cshtml
@if (User.HasClaim("HasLinkedDiscord", "true"))
{
    <p>Discord Account: @User.FindFirstValue("DiscordUsername")</p>
    <p>Discord ID: @User.FindFirstValue("DiscordUserId")</p>
}
else
{
    <a asp-page="/Account/LinkDiscord">Link Discord Account</a>
}
```

**In Services:**
```csharp
public class UserService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public string GetCurrentDiscordId()
    {
        var user = _contextAccessor.HttpContext?.User;
        return user?.FindFirstValue("DiscordUserId") ?? string.Empty;
    }
}
```

---

### Performance Considerations

1. **Transformation Marker**: The `DiscordClaimsTransformed` claim prevents the transformation from running multiple times per request
2. **Database Query**: One database query per authenticated request to load `ApplicationUser`
3. **Caching**: Consider implementing claims caching for high-traffic scenarios

**Optimization Example:**
```csharp
// Use IMemoryCache to cache user data for short duration
public class CachedDiscordClaimsTransformation : IClaimsTransformation
{
    private readonly IMemoryCache _cache;
    private readonly UserManager<ApplicationUser> _userManager;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        var user = await _cache.GetOrCreateAsync($"user_{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _userManager.FindByIdAsync(userId);
        });

        // Transform claims...
    }
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### 1. Unauthorized Access - Redirects to Login Repeatedly

**Symptom:** User logs in successfully but is immediately redirected back to login page.

**Possible Causes:**
- Cookie authentication not properly configured
- User does not have any role assigned
- Fallback policy prevents access

**Solution:**
```bash
# Check user roles in database
SELECT u.UserName, r.Name as Role
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE u.Email = 'user@example.com';

# If user has no roles, assign one via UserManager
await _userManager.AddToRoleAsync(user, Roles.Viewer);
```

---

#### 2. Access Denied (403) - User Has Role But Cannot Access Page

**Symptom:** User has correct role but receives 403 Forbidden error.

**Possible Causes:**
- Policy name typo in `[Authorize]` attribute
- Role name mismatch (case-sensitive)
- Claims transformation not adding role claims

**Solution:**
```csharp
// Verify policy names match exactly
[Authorize(Policy = "RequireAdmin")] // Correct
[Authorize(Policy = "AdminOnly")]    // Wrong - policy doesn't exist

// Check role claims in debugger or add diagnostic page
public class DiagnosticsModel : PageModel
{
    public List<Claim> Claims { get; set; } = new();

    public void OnGet()
    {
        Claims = User.Claims.ToList();
        // Check for role claims: ClaimType = ClaimTypes.Role
    }
}
```

---

#### 3. Guild Access Always Denied

**Symptom:** User cannot access guild-specific pages even with correct access grants.

**Possible Causes:**
- No `UserGuildAccess` record exists
- Guild ID not being extracted from route
- SuperAdmin check not working

**Solution:**
```sql
-- Check if UserGuildAccess record exists
SELECT * FROM UserGuildAccess
WHERE ApplicationUserId = 'user-guid'
  AND GuildId = 123456789;

-- If missing, create one
INSERT INTO UserGuildAccess (ApplicationUserId, GuildId, AccessLevel, GrantedAt)
VALUES ('user-guid', 123456789, 2, GETUTCDATE());
```

**Debug Route Parameter Extraction:**
```csharp
// Add logging to see what's being extracted
_logger.LogInformation("Route values: {Values}",
    string.Join(", ", httpContext.Request.RouteValues.Select(kv => $"{kv.Key}={kv.Value}")));
```

---

#### 4. Tag Helpers Not Hiding Content

**Symptom:** `<authorize-view>` or `<require-role>` showing content to unauthorized users.

**Possible Causes:**
- Tag helpers not registered in `_ViewImports.cshtml`
- Incorrect policy/role names
- Server-side authorization not enforced

**Solution:**
```cshtml
<!-- Verify tag helpers are registered in _ViewImports.cshtml -->
@addTagHelper *, DiscordBot.Bot

<!-- Check for typos in policy names -->
<authorize-view policy="RequireAdmin"> <!-- Correct -->
<authorize-view policy="AdminOnly">    <!-- Wrong -->

<!-- Always enforce server-side authorization -->
[Authorize(Policy = "RequireAdmin")] // Required!
public class SecurePageModel : PageModel { }
```

---

#### 5. Claims Transformation Not Running

**Symptom:** Discord claims are missing even though user has linked Discord account.

**Possible Causes:**
- `IClaimsTransformation` not registered
- Transformation registered as wrong scope (should be Scoped)
- Database query failing silently

**Solution:**
```csharp
// Verify registration in Program.cs
builder.Services.AddScoped<IClaimsTransformation, DiscordClaimsTransformation>();

// Add logging to transformation class
_logger.LogInformation("Transforming claims for user {UserId}", userId);

// Check if user actually has DiscordUserId set
SELECT Id, Email, DiscordUserId, DiscordUsername
FROM AspNetUsers
WHERE Id = 'user-guid';
```

---

### Verifying User Roles

**Via Database Query:**
```sql
SELECT
    u.Id,
    u.UserName,
    u.Email,
    r.Name as Role
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
ORDER BY u.Email, r.Name;
```

**Via Diagnostic Page:**
```csharp
[Authorize(Policy = "RequireSuperAdmin")]
public class UserRolesModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public List<(string Email, List<string> Roles)> UsersWithRoles { get; set; }

    public async Task OnGetAsync()
    {
        var users = _userManager.Users.ToList();
        UsersWithRoles = new();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            UsersWithRoles.Add((user.Email!, roles.ToList()));
        }
    }
}
```

---

### Debugging Authorization Failures

**Enable Detailed Authorization Logging:**
```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.Authorization": "Debug"
    }
  }
}
```

**Check Authorization Failure Reasons:**
```csharp
public class DebugAuthModel : PageModel
{
    private readonly IAuthorizationService _authorizationService;

    public async Task<IActionResult> OnGetAsync()
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, "RequireAdmin");

        if (!authResult.Succeeded)
        {
            foreach (var failure in authResult.Failure?.FailureReasons ?? [])
            {
                _logger.LogWarning("Authorization failed: {Reason}", failure.Message);
            }

            return Forbid();
        }

        return Page();
    }
}
```

---

### Testing Authorization Policies

**Unit Testing Authorization Handlers:**
```csharp
public class GuildAccessAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_SuperAdmin_GrantsAccess()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, Roles.SuperAdmin)
        }, "Test"));

        var requirement = new GuildAccessRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        var handler = CreateHandler();
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }
}
```

---

## Related Documentation

- [Identity Configuration](identity-configuration.md) - ASP.NET Identity setup and Discord OAuth
- [Epic 2: Authentication and Authorization](../archive/plans/epic-2-auth-architecture-plan.md) - Overall architecture plan (archived)
- [Database Schema](database-schema.md) - Entity relationships and table structures
- [Razor Components](razor-components.md) - UI component patterns

---

## File Locations

### Core Authorization Files

| File | Location | Description |
|------|----------|-------------|
| Role constants | `src/DiscordBot.Core/Authorization/Roles.cs` | Role name definitions |
| UserGuildAccess entity | `src/DiscordBot.Core/Entities/UserGuildAccess.cs` | Guild access linking table |
| GuildAccessRequirement | `src/DiscordBot.Bot/Authorization/GuildAccessRequirement.cs` | Authorization requirement |
| GuildAccessHandler | `src/DiscordBot.Bot/Authorization/GuildAccessAuthorizationHandler.cs` | Authorization handler |
| DiscordClaimsTransformation | `src/DiscordBot.Bot/Authorization/DiscordClaimsTransformation.cs` | Claims enrichment |
| AuthorizeViewTagHelper | `src/DiscordBot.Bot/TagHelpers/AuthorizeTagHelper.cs` | Tag helpers |
| PortalPageModelBase | `src/DiscordBot.Bot/Pages/Portal/PortalPageModelBase.cs` | Portal authorization base class |

### Configuration Files

| File | Location | Purpose |
|------|----------|---------|
| Policy registration | `src/DiscordBot.Bot/Program.cs` | Service configuration |
| Tag helper registration | `src/DiscordBot.Bot/Pages/_ViewImports.cshtml` | View imports |

---

**Last Updated:** 2026-01-15
**Maintained By:** Documentation Team
**Related Issues:** [#65 - Authorization Policies](https://github.com/cpike5/discordbot/issues/65), [#1135 - Document AllowAnonymous pattern](https://github.com/cpike5/discordbot/issues/1135)
