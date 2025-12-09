# Implementation Plan: Issue #65 - Authorization Policies

**Document Version:** 1.0
**Date:** 2025-12-09
**Issue Reference:** GitHub Issue #65
**Parent Epic:** #63 - Epic 2: Authentication and Authorization
**Prerequisites:** Issue #64 (Authentication Infrastructure) - COMPLETE

---

## 1. Requirement Summary

Implement a comprehensive role-based authorization system for the Discord bot admin UI. The system must:

1. Define four hierarchical roles: SuperAdmin, Admin, Moderator, Viewer
2. Create authorization policies that enforce role-based access control
3. Implement guild-specific authorization (admins can only manage their linked guilds)
4. Add role-based navigation visibility in the sidebar
5. Create authorization tag helpers for conditional UI rendering
6. Implement claims transformation for Discord-linked users
7. Update the AccessDenied page to use the shared layout
8. Document all authorization policies

---

## 2. Current State Analysis

### 2.1 Existing Infrastructure (from Issue #64)

| Component | Location | Status |
|-----------|----------|--------|
| `ApplicationUser` entity | `src/DiscordBot.Core/Entities/ApplicationUser.cs` | Complete |
| Role constants | `src/DiscordBot.Bot/Extensions/IdentitySeeder.cs` | Complete (nested class) |
| Role seeding | `src/DiscordBot.Bot/Extensions/IdentitySeeder.cs` | Complete |
| Identity configuration | `src/DiscordBot.Bot/Program.cs` | Complete |
| Cookie auth settings | `src/DiscordBot.Bot/Program.cs` | Complete |
| AccessDenied page | `src/DiscordBot.Bot/Pages/Account/AccessDenied.cshtml` | Exists (no layout) |
| Sidebar navigation | `src/DiscordBot.Bot/Pages/Shared/_Sidebar.cshtml` | No role visibility |
| ViewImports | `src/DiscordBot.Bot/Pages/_ViewImports.cshtml` | Basic setup |

### 2.2 Current Role Constants

The `IdentitySeeder.cs` already defines role constants:

```csharp
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Viewer = "Viewer";
}
```

**Issue:** These are nested inside `IdentitySeeder` and not easily accessible for `[Authorize]` attributes.

### 2.3 Missing Components

| Component | Required Action |
|-----------|-----------------|
| Authorization policies | Add to Program.cs |
| Role constants (public) | Extract to separate class |
| Guild-User linking entity | New entity for guild access |
| GuildAccessRequirement | New authorization requirement |
| GuildAccessHandler | Authorization handler implementation |
| Claims transformation | IClaimsTransformation implementation |
| Authorization tag helpers | New tag helpers |
| Role-based sidebar | Update _Sidebar.cshtml |
| AccessDenied with layout | Update AccessDenied.cshtml |

---

## 3. Architectural Considerations

### 3.1 Role Hierarchy Design

The role hierarchy follows a **cumulative permission model**:

```
SuperAdmin > Admin > Moderator > Viewer

SuperAdmin: Full system access + user management + system config
    |
    v
Admin: Guild CRUD + bot control + settings (cannot manage SuperAdmins)
    |
    v
Moderator: View all + edit guild settings (cannot delete)
    |
    v
Viewer: Read-only dashboards and logs
```

### 3.2 Authorization Policy Strategy

Policies should be named by the **minimum required role**:

| Policy Name | Allowed Roles | Use Case |
|-------------|---------------|----------|
| `RequireSuperAdmin` | SuperAdmin | User management, system config |
| `RequireAdmin` | SuperAdmin, Admin | Guild CRUD, bot control |
| `RequireModerator` | SuperAdmin, Admin, Moderator | Edit settings, moderate |
| `RequireViewer` | All roles | View dashboards, logs |
| `GuildAccess` | Custom handler | Guild-specific access |

### 3.3 Guild-Specific Authorization Design

To implement guild-specific authorization, we need:

1. **Data Model:** A linking table between `ApplicationUser` and `Guild`
2. **Authorization Requirement:** `GuildAccessRequirement` class
3. **Authorization Handler:** `GuildAccessAuthorizationHandler`
4. **Route Parameter Detection:** Extract guild ID from route

**Proposed Entity: `UserGuildAccess`**

```csharp
public class UserGuildAccess
{
    public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }

    public ulong GuildId { get; set; }
    public Guild Guild { get; set; }

    public GuildAccessLevel AccessLevel { get; set; }
    public DateTime GrantedAt { get; set; }
    public string? GrantedByUserId { get; set; }
}

public enum GuildAccessLevel
{
    Viewer = 0,
    Moderator = 1,
    Admin = 2,
    Owner = 3
}
```

### 3.4 Claims Transformation Strategy

Claims transformation will enrich the user's identity with:

1. Discord-specific claims (DiscordUserId, DiscordUsername)
2. Linked guild claims (list of guild IDs user can access)
3. Custom permissions derived from role + guild access

### 3.5 Integration Points

| Integration | Consideration |
|-------------|---------------|
| Sidebar | Must inject authorization service |
| Pages | Use `[Authorize(Policy = "...")]` attribute |
| API Controllers | Apply policies to API endpoints |
| Tag Helpers | Register in `_ViewImports.cshtml` |

---

## 4. Subagent Task Plan

### 4.1 dotnet-specialist Tasks

#### Task 2.2.1: Extract Role Constants to Public Class

**Description:** Move role constants from `IdentitySeeder.Roles` to a dedicated public class for easier access in `[Authorize]` attributes.

**File to Create:** `src/DiscordBot.Core/Authorization/Roles.cs`

```csharp
namespace DiscordBot.Core.Authorization;

/// <summary>
/// Defines role names used throughout the application.
/// </summary>
public static class Roles
{
    /// <summary>System owner with full access.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Guild administrator with full CRUD access.</summary>
    public const string Admin = "Admin";

    /// <summary>Limited admin with edit but no delete access.</summary>
    public const string Moderator = "Moderator";

    /// <summary>Read-only access to dashboards and logs.</summary>
    public const string Viewer = "Viewer";

    /// <summary>All roles for authorization policies.</summary>
    public static readonly string[] All = { SuperAdmin, Admin, Moderator, Viewer };
}
```

**File to Modify:** `src/DiscordBot.Bot/Extensions/IdentitySeeder.cs`
- Remove nested `Roles` class
- Import `DiscordBot.Core.Authorization.Roles`

**Acceptance Criteria:**
- [ ] `Roles` class is in Core project and publicly accessible
- [ ] `IdentitySeeder` uses the new `Roles` class
- [ ] Solution builds without errors

---

#### Task 2.2.2: Create Authorization Policies in Program.cs

**Description:** Add authorization policies to the service configuration.

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

**Code to Add (after Identity configuration, before `AddControllers`):**

```csharp
// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    // Role-based policies
    options.AddPolicy("RequireSuperAdmin", policy =>
        policy.RequireRole(Roles.SuperAdmin));

    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole(Roles.SuperAdmin, Roles.Admin));

    options.AddPolicy("RequireModerator", policy =>
        policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Moderator));

    options.AddPolicy("RequireViewer", policy =>
        policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Moderator, Roles.Viewer));

    // Guild-specific policy (requires handler registration)
    options.AddPolicy("GuildAccess", policy =>
        policy.Requirements.Add(new GuildAccessRequirement()));

    // Default policy: require authenticated user
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

**Acceptance Criteria:**
- [ ] All five policies are defined
- [ ] FallbackPolicy requires authentication
- [ ] Application starts without errors

---

#### Task 2.2.3: Add [Authorize] Attributes to Protected Pages

**Description:** Apply authorization attributes to existing Razor pages based on their required access level.

**Pages to Modify:**

| Page | Policy | Rationale |
|------|--------|-----------|
| `Pages/Index.cshtml.cs` | `RequireViewer` | Dashboard - all authenticated users |
| `Pages/Components.cshtml.cs` | `RequireAdmin` | Developer tools - admins only |
| `Pages/Account/*.cshtml.cs` | `[AllowAnonymous]` | Login/logout pages |

**Example Implementation:**

```csharp
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Pages;

[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    // ...
}
```

**Note:** Pages for guild management (to be created later) should use `[Authorize(Policy = "GuildAccess")]`.

**Acceptance Criteria:**
- [ ] Index page requires Viewer policy
- [ ] Components page requires Admin policy
- [ ] Account pages allow anonymous access
- [ ] Unauthorized access redirects to login
- [ ] Forbidden access shows AccessDenied page

---

#### Task 2.2.4: Update AccessDenied Page to Use Layout

**Description:** Modify the AccessDenied page to use the shared layout for consistency with the rest of the admin UI.

**File to Modify:** `src/DiscordBot.Bot/Pages/Account/AccessDenied.cshtml`

**Changes:**
1. Remove `Layout = null`
2. Remove full HTML document structure
3. Keep only the content that goes inside the layout
4. Use consistent card/error styling from design system

**Target Structure:**

```cshtml
@page
@model AccessDeniedModel
@{
    ViewData["Title"] = "Access Denied";
}

<div class="max-w-2xl mx-auto">
    <!-- Error Card -->
    <div class="bg-bg-secondary border border-border-primary rounded-lg p-8 shadow-lg">
        <!-- Error Icon & Title -->
        <div class="text-center mb-6">
            <div class="inline-flex items-center justify-center w-16 h-16 bg-error rounded-xl mb-4">
                <svg class="w-10 h-10 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
                </svg>
            </div>
            <h1 class="text-2xl font-bold text-text-primary">Access Denied</h1>
            <p class="text-text-secondary mt-2">You don't have permission to access this resource</p>
        </div>

        <!-- Error Details -->
        <div class="mb-6 p-4 bg-error-bg border border-error-border rounded-lg">
            <p class="text-sm text-error">
                <strong>403 Forbidden:</strong> Access to this resource is restricted.
            </p>
        </div>

        <!-- Message -->
        <p class="text-sm text-text-primary mb-4">
            You do not have the necessary permissions to view this page or perform this action.
        </p>

        @if (!string.IsNullOrEmpty(Model.ReturnUrl))
        {
            <p class="text-xs text-text-tertiary mb-6 font-mono break-all">
                Attempted URL: @Model.ReturnUrl
            </p>
        }

        <p class="text-sm text-text-secondary mb-6">
            If you believe you should have access to this resource, please contact your administrator.
        </p>

        <!-- Actions -->
        <div class="flex gap-3">
            <a asp-page="/Index" class="flex-1 text-center py-2.5 px-4 text-sm font-semibold text-white bg-accent-orange border border-accent-orange rounded-md transition-colors hover:bg-accent-orange-hover hover:border-accent-orange-hover focus:outline-none focus-visible:ring-2 focus-visible:ring-accent-blue focus-visible:ring-offset-2 focus-visible:ring-offset-bg-secondary">
                Go to Dashboard
            </a>
            <form asp-page="/Account/Logout" asp-page-handler="Post" method="post" class="flex-1">
                <button type="submit" class="w-full text-center py-2.5 px-4 text-sm font-semibold text-text-primary bg-transparent border border-border-primary rounded-md transition-colors hover:bg-bg-hover focus:outline-none focus-visible:ring-2 focus-visible:ring-accent-blue focus-visible:ring-offset-2 focus-visible:ring-offset-bg-secondary">
                    Sign Out
                </button>
            </form>
        </div>
    </div>
</div>
```

**Acceptance Criteria:**
- [ ] AccessDenied page uses shared `_Layout.cshtml`
- [ ] Sidebar and navbar are visible on AccessDenied page
- [ ] Styling matches the design system
- [ ] Sign Out button correctly submits logout form

---

#### Task 2.2.5: Implement Claims Transformation for Discord-Linked Users

**Description:** Create an `IClaimsTransformation` implementation that enriches user claims with Discord-specific data.

**File to Create:** `src/DiscordBot.Bot/Authorization/DiscordClaimsTransformation.cs`

```csharp
using System.Security.Claims;
using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Transforms user claims to include Discord-specific information.
/// </summary>
public class DiscordClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DiscordClaimsTransformation> _logger;

    public DiscordClaimsTransformation(
        UserManager<ApplicationUser> userManager,
        ILogger<DiscordClaimsTransformation> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only transform if authenticated
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return principal;
        }

        // Check if claims already transformed (avoid duplicate work)
        if (principal.HasClaim(c => c.Type == "DiscordClaimsTransformed"))
        {
            return principal;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return principal;
        }

        var identity = (ClaimsIdentity)principal.Identity;

        // Add transformation marker
        identity.AddClaim(new Claim("DiscordClaimsTransformed", "true"));

        // Add Discord claims if user has linked Discord account
        if (user.DiscordUserId.HasValue)
        {
            identity.AddClaim(new Claim("DiscordUserId", user.DiscordUserId.Value.ToString()));

            if (!string.IsNullOrEmpty(user.DiscordUsername))
            {
                identity.AddClaim(new Claim("DiscordUsername", user.DiscordUsername));
            }

            identity.AddClaim(new Claim("HasLinkedDiscord", "true"));
        }

        // Add display name claim
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            identity.AddClaim(new Claim("DisplayName", user.DisplayName));
        }

        _logger.LogDebug("Claims transformed for user {UserId}, Discord linked: {HasDiscord}",
            userId, user.DiscordUserId.HasValue);

        return principal;
    }
}
```

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

Add service registration:

```csharp
// Register claims transformation
builder.Services.AddScoped<IClaimsTransformation, DiscordClaimsTransformation>();
```

**Acceptance Criteria:**
- [ ] Claims transformation is registered as scoped service
- [ ] Discord claims are added when user has linked Discord account
- [ ] Transformation only runs once per request (marker claim)
- [ ] No errors for users without linked Discord

---

#### Task 2.2.6: Add Role-Based Navigation Visibility to Sidebar

**Description:** Update the sidebar to show/hide navigation items based on user roles.

**File to Modify:** `src/DiscordBot.Bot/Pages/Shared/_Sidebar.cshtml`

**Implementation Approach:**

1. Inject `IAuthorizationService` at the top of the partial
2. Create helper methods or use inline authorization checks
3. Wrap navigation sections in role-based conditionals

**Code Changes:**

```cshtml
@inject Microsoft.AspNetCore.Authorization.IAuthorizationService AuthorizationService

<!-- Sidebar Navigation -->
<aside id="sidebar" class="...">
  <nav class="flex flex-col h-full p-4">
    <!-- Main Navigation (Viewer+) -->
    <div class="space-y-1">
      <a href="/" class="sidebar-link @(ViewContext.RouteData.Values["page"]?.ToString() == "/Index" ? "active" : "")">
        <!-- Dashboard icon -->
        Dashboard
      </a>

      <a href="#" class="sidebar-link">
        <!-- Servers icon -->
        Servers
        <span class="sidebar-badge">12</span>
      </a>

      <a href="#" class="sidebar-link">
        <!-- Commands icon -->
        Commands
      </a>

      <a href="#" class="sidebar-link">
        <!-- Logs icon -->
        Logs
        <span class="sidebar-badge-dot status-pulse"></span>
      </a>

      @if ((await AuthorizationService.AuthorizeAsync(User, "RequireAdmin")).Succeeded)
      {
        <a href="#" class="sidebar-link">
          <!-- Settings icon -->
          Settings
        </a>
      }
    </div>

    <!-- Admin Section (Admin+) -->
    @if ((await AuthorizationService.AuthorizeAsync(User, "RequireAdmin")).Succeeded)
    {
      <div class="my-4 border-t border-border-secondary"></div>
      <div class="space-y-1">
        <p class="sidebar-section">Administration</p>

        @if ((await AuthorizationService.AuthorizeAsync(User, "RequireSuperAdmin")).Succeeded)
        {
          <a href="#" class="sidebar-link">
            <!-- Users icon -->
            User Management
          </a>

          <a href="#" class="sidebar-link">
            <!-- Config icon -->
            System Config
          </a>
        }

        <a href="#" class="sidebar-link">
          <!-- Bot control icon -->
          Bot Control
        </a>
      </div>
    }

    <!-- Developer Tools (Admin+) -->
    @if ((await AuthorizationService.AuthorizeAsync(User, "RequireAdmin")).Succeeded)
    {
      <div class="my-4 border-t border-border-secondary"></div>
      <div class="space-y-1">
        <p class="sidebar-section">Developer</p>
        <a href="/Components" class="sidebar-link @(ViewContext.RouteData.Values["page"]?.ToString() == "/Components" ? "active" : "")">
          <!-- Components icon -->
          Components
        </a>
      </div>
    }

    <!-- Support Section (All users) -->
    <div class="my-4 border-t border-border-secondary"></div>
    <div class="space-y-1">
      <p class="sidebar-section">Support</p>
      <a href="#" class="sidebar-link">
        <!-- Docs icon -->
        Documentation
      </a>
      <a href="#" class="sidebar-link">
        <!-- Help icon -->
        Help & Support
      </a>
    </div>

    <!-- Bot Status Footer -->
    <div class="mt-auto pt-4 border-t border-border-secondary">
      <!-- Status content -->
    </div>
  </nav>
</aside>
```

**Acceptance Criteria:**
- [ ] Viewers see: Dashboard, Servers, Commands, Logs, Support
- [ ] Moderators see: Same as Viewers
- [ ] Admins see: + Settings, Developer section, Bot Control
- [ ] SuperAdmins see: + User Management, System Config

---

#### Task 2.2.7: Create Authorization Tag Helpers for Conditional UI Rendering

**Description:** Create custom tag helpers for cleaner authorization checks in views.

**File to Create:** `src/DiscordBot.Bot/TagHelpers/AuthorizeTagHelper.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Tag helper that conditionally renders content based on authorization policy.
/// </summary>
/// <example>
/// <authorize-view policy="RequireAdmin">
///     <p>Only visible to admins</p>
/// </authorize-view>
/// </example>
[HtmlTargetElement("authorize-view")]
public class AuthorizeViewTagHelper : TagHelper
{
    private readonly IAuthorizationService _authorizationService;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// The authorization policy to check.
    /// </summary>
    [HtmlAttributeName("policy")]
    public string? Policy { get; set; }

    /// <summary>
    /// Comma-separated list of roles (any match grants access).
    /// </summary>
    [HtmlAttributeName("roles")]
    public string? Roles { get; set; }

    public AuthorizeViewTagHelper(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // Don't render the tag itself

        var user = ViewContext.HttpContext.User;

        // Check if user is authenticated
        if (user?.Identity?.IsAuthenticated != true)
        {
            output.SuppressOutput();
            return;
        }

        bool isAuthorized = false;

        // Check policy if specified
        if (!string.IsNullOrEmpty(Policy))
        {
            var result = await _authorizationService.AuthorizeAsync(user, Policy);
            isAuthorized = result.Succeeded;
        }
        // Check roles if specified
        else if (!string.IsNullOrEmpty(Roles))
        {
            var roleList = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            isAuthorized = roleList.Any(role => user.IsInRole(role));
        }
        else
        {
            // No policy or roles specified, just require authentication
            isAuthorized = true;
        }

        if (!isAuthorized)
        {
            output.SuppressOutput();
        }
    }
}

/// <summary>
/// Tag helper that shows content only to specific roles.
/// </summary>
/// <example>
/// <require-role roles="SuperAdmin,Admin">
///     <button>Admin Action</button>
/// </require-role>
/// </example>
[HtmlTargetElement("require-role")]
public class RequireRoleTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Comma-separated list of roles (any match grants access).
    /// </summary>
    [HtmlAttributeName("roles")]
    public string Roles { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // Don't render the tag itself

        var user = ViewContext.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            output.SuppressOutput();
            return;
        }

        var roleList = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!roleList.Any(role => user.IsInRole(role)))
        {
            output.SuppressOutput();
        }
    }
}
```

**File to Modify:** `src/DiscordBot.Bot/Pages/_ViewImports.cshtml`

Add tag helper registration:

```cshtml
@addTagHelper *, DiscordBot.Bot
```

**Usage Examples:**

```cshtml
<!-- Policy-based -->
<authorize-view policy="RequireAdmin">
    <button class="btn-danger">Delete Server</button>
</authorize-view>

<!-- Role-based -->
<require-role roles="SuperAdmin">
    <a href="/Admin/Users">Manage Users</a>
</require-role>

<!-- Multiple roles -->
<require-role roles="SuperAdmin,Admin">
    <button>Admin Action</button>
</require-role>
```

**Acceptance Criteria:**
- [ ] Tag helpers are registered in ViewImports
- [ ] `<authorize-view policy="...">` works correctly
- [ ] `<require-role roles="...">` works correctly
- [ ] Content is hidden for unauthorized users
- [ ] No errors thrown for unauthenticated users

---

#### Task 2.2.8: Implement Guild-Specific Authorization Requirement

**Description:** Create the infrastructure for guild-specific authorization.

**Files to Create:**

1. **Entity:** `src/DiscordBot.Core/Entities/UserGuildAccess.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an ApplicationUser's access to a specific guild.
/// </summary>
public class UserGuildAccess
{
    /// <summary>
    /// The ApplicationUser ID.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation to the ApplicationUser.
    /// </summary>
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// The Guild ID (Discord snowflake).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Navigation to the Guild.
    /// </summary>
    public Guild Guild { get; set; } = null!;

    /// <summary>
    /// The user's access level for this guild.
    /// </summary>
    public GuildAccessLevel AccessLevel { get; set; } = GuildAccessLevel.Viewer;

    /// <summary>
    /// When access was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who granted the access (nullable for system-granted).
    /// </summary>
    public string? GrantedByUserId { get; set; }
}

/// <summary>
/// Access levels for guild-specific permissions.
/// </summary>
public enum GuildAccessLevel
{
    /// <summary>Read-only access to guild data.</summary>
    Viewer = 0,

    /// <summary>Can edit guild settings.</summary>
    Moderator = 1,

    /// <summary>Full admin access to the guild.</summary>
    Admin = 2,

    /// <summary>Guild owner with all permissions.</summary>
    Owner = 3
}
```

2. **Authorization Requirement:** `src/DiscordBot.Bot/Authorization/GuildAccessRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Authorization requirement for guild-specific access.
/// </summary>
public class GuildAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Minimum access level required. Defaults to Viewer.
    /// </summary>
    public GuildAccessLevel MinimumLevel { get; }

    public GuildAccessRequirement(GuildAccessLevel minimumLevel = GuildAccessLevel.Viewer)
    {
        MinimumLevel = minimumLevel;
    }
}
```

3. **Authorization Handler:** `src/DiscordBot.Bot/Authorization/GuildAccessAuthorizationHandler.cs`

```csharp
using System.Security.Claims;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Handles authorization for guild-specific access.
/// </summary>
public class GuildAccessAuthorizationHandler : AuthorizationHandler<GuildAccessRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GuildAccessAuthorizationHandler> _logger;

    public GuildAccessAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory,
        ILogger<GuildAccessAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GuildAccessRequirement requirement)
    {
        // SuperAdmins have access to all guilds
        if (context.User.IsInRole(Roles.SuperAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("GuildAccess check failed: No HTTP context available");
            return;
        }

        // Extract guild ID from route
        var guildIdString = httpContext.Request.RouteValues["guildId"]?.ToString()
            ?? httpContext.Request.Query["guildId"].FirstOrDefault();

        if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("GuildAccess check failed: No valid guildId in route or query");
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Check user's guild access
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var access = await dbContext.Set<UserGuildAccess>()
            .FirstOrDefaultAsync(a =>
                a.ApplicationUserId == userId &&
                a.GuildId == guildId);

        if (access != null && access.AccessLevel >= requirement.MinimumLevel)
        {
            _logger.LogDebug("GuildAccess granted for user {UserId} to guild {GuildId} with level {Level}",
                userId, guildId, access.AccessLevel);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("GuildAccess denied for user {UserId} to guild {GuildId}",
                userId, guildId);
        }
    }
}
```

4. **Update DbContext:** Add `UserGuildAccess` DbSet and configuration

**File to Modify:** `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`

```csharp
public DbSet<UserGuildAccess> UserGuildAccess => Set<UserGuildAccess>();

// In OnModelCreating:
modelBuilder.Entity<UserGuildAccess>(entity =>
{
    entity.HasKey(e => new { e.ApplicationUserId, e.GuildId });

    entity.HasOne(e => e.ApplicationUser)
        .WithMany()
        .HasForeignKey(e => e.ApplicationUserId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.Guild)
        .WithMany()
        .HasForeignKey(e => e.GuildId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.Property(e => e.AccessLevel)
        .HasConversion<int>();
});
```

5. **Register Handler:** Add to `Program.cs`

```csharp
// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, GuildAccessAuthorizationHandler>();
builder.Services.AddHttpContextAccessor();
```

6. **Create Migration:**

```bash
dotnet ef migrations add AddUserGuildAccess --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

**Acceptance Criteria:**
- [ ] `UserGuildAccess` entity created with proper relationships
- [ ] `GuildAccessRequirement` and `GuildAccessAuthorizationHandler` implemented
- [ ] Handler correctly extracts guild ID from route/query
- [ ] SuperAdmins bypass guild-specific checks
- [ ] EF Core migration created successfully

---

### 4.2 docs-writer Tasks

#### Task 2.2.9: Document Authorization Policies

**Description:** Create comprehensive documentation for the authorization system.

**File to Create:** `docs/articles/authorization-policies.md`

**Content Outline:**

```markdown
# Authorization Policies

## Overview
Describes the role-based access control system.

## Role Hierarchy
Table of roles and their capabilities.

## Authorization Policies
- RequireSuperAdmin
- RequireAdmin
- RequireModerator
- RequireViewer
- GuildAccess

## Using Authorization in Razor Pages
Code examples for [Authorize] attributes.

## Using Tag Helpers
Examples of <authorize-view> and <require-role>.

## Guild-Specific Authorization
How guild access works, UserGuildAccess entity.

## Claims Transformation
What claims are added and when.

## Troubleshooting
Common issues and solutions.
```

**Acceptance Criteria:**
- [ ] Documentation covers all roles and policies
- [ ] Code examples are accurate and tested
- [ ] Guild-specific authorization is explained
- [ ] Troubleshooting section included

---

## 5. Timeline / Dependency Map

```
Phase 1: Foundation (Can be parallelized)
├── Task 2.2.1: Extract Role Constants
├── Task 2.2.2: Create Authorization Policies
└── Task 2.2.4: Update AccessDenied Page

Phase 2: Core Implementation (Sequential after Phase 1)
├── Task 2.2.3: Add [Authorize] Attributes (depends on 2.2.1, 2.2.2)
├── Task 2.2.5: Claims Transformation (depends on 2.2.1)
└── Task 2.2.7: Authorization Tag Helpers (depends on 2.2.2)

Phase 3: Advanced Features (Sequential)
├── Task 2.2.8: Guild-Specific Authorization (depends on 2.2.2, 2.2.5)
└── Task 2.2.6: Role-Based Sidebar (depends on 2.2.2, 2.2.7)

Phase 4: Documentation (After all implementation)
└── Task 2.2.9: Document Authorization Policies
```

**Estimated Timeline:**

| Phase | Tasks | Duration |
|-------|-------|----------|
| Phase 1 | 2.2.1, 2.2.2, 2.2.4 | 1 day |
| Phase 2 | 2.2.3, 2.2.5, 2.2.7 | 1-2 days |
| Phase 3 | 2.2.6, 2.2.8 | 1-2 days |
| Phase 4 | 2.2.9 | 0.5 days |

**Total Estimated Duration:** 3.5-5.5 days

---

## 6. Acceptance Criteria Summary

### Functional Requirements

- [ ] Authorization policies defined in `Program.cs`
- [ ] SuperAdmin role can access all features
- [ ] Admin role can manage guilds and settings (not other admins)
- [ ] Moderator role has limited edit access
- [ ] Viewer role has read-only access
- [ ] Unauthorized access redirects to login
- [ ] Forbidden access shows 403 page (AccessDenied)
- [ ] Authorization enforced on page handlers via `[Authorize]`
- [ ] Role-based navigation visibility
- [ ] Guild-specific authorization (admin can only manage their linked guilds)

### Technical Requirements

- [ ] `Roles` class is publicly accessible in Core project
- [ ] All pages have appropriate `[Authorize]` attributes
- [ ] Claims transformation adds Discord-specific claims
- [ ] Tag helpers are registered and functional
- [ ] `UserGuildAccess` entity has proper EF Core configuration
- [ ] Database migration created and tested

### Documentation Requirements

- [ ] Authorization policies documented in `docs/articles/`
- [ ] Usage examples provided for tag helpers
- [ ] Troubleshooting guide included

---

## 7. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Policy name typos in attributes | Medium | High | Create compile-time constants for policy names |
| Performance impact of claims transformation | Low | Medium | Add caching, ensure transformation runs once |
| Guild access check performance | Medium | Medium | Index `UserGuildAccess` table properly |
| Authorization bypass bugs | Low | High | Write comprehensive unit/integration tests |
| UI confusion from hidden elements | Medium | Low | Show disabled states instead of hiding where appropriate |

### Security Considerations

1. **Always server-side:** Tag helpers hide UI elements but authorization is enforced server-side
2. **Principle of least privilege:** Default to Viewer access
3. **Audit logging:** Consider logging authorization failures
4. **Role assignment protection:** Prevent non-SuperAdmins from assigning SuperAdmin role

---

## 8. File Summary

### Files to Create

| File | Project | Description |
|------|---------|-------------|
| `Authorization/Roles.cs` | Core | Public role constants |
| `Authorization/DiscordClaimsTransformation.cs` | Bot | Claims transformation service |
| `Authorization/GuildAccessRequirement.cs` | Bot | Guild authorization requirement |
| `Authorization/GuildAccessAuthorizationHandler.cs` | Bot | Guild authorization handler |
| `TagHelpers/AuthorizeTagHelper.cs` | Bot | Authorization tag helpers |
| `Entities/UserGuildAccess.cs` | Core | Guild access linking entity |
| `articles/authorization-policies.md` | docs | Authorization documentation |

### Files to Modify

| File | Changes |
|------|---------|
| `Program.cs` | Add authorization policies, register handlers |
| `IdentitySeeder.cs` | Use new Roles class |
| `_ViewImports.cshtml` | Add tag helper registration |
| `_Sidebar.cshtml` | Add role-based visibility |
| `AccessDenied.cshtml` | Use shared layout |
| `Index.cshtml.cs` | Add [Authorize] attribute |
| `Components.cshtml.cs` | Add [Authorize] attribute |
| `BotDbContext.cs` | Add UserGuildAccess DbSet |

---

*Document prepared by: Systems Architect*
*Ready for implementation by: dotnet-specialist, docs-writer*
