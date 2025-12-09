# Epic 2: Authentication and Authorization - Architectural Plan

**Document Version:** 1.0
**Date:** 2025-12-09
**Epic Reference:** GitHub Issue #63
**Related Issues:** #64, #65, #66, #90, #91, (NEW: Feature 2.4, Feature 2.5)

---

## Executive Summary

This document provides a comprehensive architectural review and implementation plan for Epic 2: Authentication and Authorization. The analysis includes:

1. Assessment of the current plan structure
2. Recommendations for ASP.NET Identity integration
3. Discord OAuth authentication strategy
4. Discord user linking approach
5. Detailed implementation roadmap

---

## 1. Current Plan Assessment

### 1.1 Epic 2 Structure Overview

The current Epic 2 is well-structured with three logical features:

| Issue | Feature | Description | Status |
|-------|---------|-------------|--------|
| #64 | 2.1: Authentication Infrastructure | Cookie-based auth, login/logout, password hashing | Open |
| #65 | 2.2: Authorization Policies | Role-based access control (Admin/Viewer) | Open |
| #66 | 2.3: User Management | Admin user CRUD operations | Open |

**Supporting UI Issues:**
- #90: Registration Page Prototype
- #91: Login Page Prototype

### 1.2 Strengths of Current Plan

1. **Clear Dependency Chain:** Features 2.1 -> 2.2 -> 2.3 creates logical progression
2. **Role-Based Access:** Admin and Viewer roles cover immediate needs
3. **Security Considerations:** CSRF protection, password masking mentioned
4. **UI Prototypes:** Parallel UI work planned (#90, #91)

### 1.3 Gaps and Concerns

1. **Missing Identity Framework Decision:** No explicit decision on using ASP.NET Identity
2. **No OAuth Strategy:** Discord OAuth not mentioned despite being a Discord bot management system
3. **User Entity Confusion:** Existing `User` entity is for Discord users, not application users
4. **No Discord Account Linking:** No plan to link admin users to their Discord accounts
5. **Missing Password Requirements:** No password complexity/policy specification
6. **Missing Security Hardening:** No account lockout, 2FA, or session management details
7. **Missing Refresh Token Strategy:** For long-lived sessions

---

## 2. ASP.NET Identity Recommendation

### 2.1 Should We Use ASP.NET Identity?

**Recommendation: YES - Use ASP.NET Identity Core**

### 2.2 Rationale

| Factor | Manual Implementation | ASP.NET Identity |
|--------|----------------------|------------------|
| Password Hashing | Must implement (PBKDF2, bcrypt) | Built-in (secure by default) |
| Account Lockout | Must implement | Built-in |
| Password Policies | Must implement | Configurable |
| Claims/Roles | Must implement | Built-in |
| Token Generation | Must implement | Built-in |
| Security Updates | Self-maintained | Microsoft-maintained |
| OAuth Integration | Complex custom work | Built-in support |
| Time to Implement | 2-3 weeks | 2-3 days |
| Audit/Compliance | Manual documentation | Industry standard |

### 2.3 Identity Implementation Approach

Since the project already has a custom `User` entity for Discord users, we need a **separate identity model**:

```
Existing Entity Structure:
  User (Discord users - tracks bot interactions)
    - Id (ulong - Discord snowflake)
    - Username (Discord username)
    - Discriminator
    - FirstSeenAt, LastSeenAt
    - CommandLogs (navigation)

New Identity Structure:
  ApplicationUser : IdentityUser (Admin UI users)
    - Id (GUID - Identity default)
    - Email
    - PasswordHash
    - DiscordUserId (nullable ulong - links to Discord)
    - DisplayName
    - IsActive
    - CreatedAt, LastLoginAt
    - Roles (via IdentityUserRole)
```

### 2.4 Separation of Concerns

```
Discord Bot Domain         |  Admin UI Domain
---------------------------|---------------------------
User entity                |  ApplicationUser entity
(Discord interactions)     |  (Web authentication)
                           |
Stored in: Users table     |  Stored in: AspNetUsers table
Primary Key: Discord ID    |  Primary Key: GUID
                           |
Purpose: Track bot usage   |  Purpose: Secure admin access
```

**Linking Strategy:** `ApplicationUser.DiscordUserId` references `User.Id` when an admin links their Discord account.

---

## 3. Discord OAuth Strategy

### 3.1 Why Discord OAuth?

For a Discord bot management system, Discord OAuth provides:

1. **Natural Fit:** Admins already have Discord accounts
2. **Identity Verification:** Proves ownership of Discord account
3. **Bot Permissions Context:** Can verify admin is guild owner/admin
4. **User Experience:** Familiar login flow for Discord users
5. **Reduced Friction:** No password to remember for Discord users

### 3.2 Authentication Flow Options

#### Option A: Discord OAuth as Primary (Recommended for this project)

```
[User] --> [Admin UI] --> [Discord OAuth] --> [Discord API]
                 |                                  |
                 |<---- Access Token + User Info <--|
                 |
                 v
         [Create/Link ApplicationUser]
                 |
                 v
         [Issue Session Cookie]
```

**Pros:**
- Most natural for Discord bot admins
- No password management needed
- Verifiable Discord identity

**Cons:**
- Requires Discord account
- Discord service dependency

#### Option B: Hybrid (Email/Password + Optional Discord OAuth)

```
Primary: Email/Password Authentication
Secondary: Link Discord account via OAuth

[User] --> [Login with Email/Password] --> [Session]
                      |
                      v (optional)
         [Link Discord Account via OAuth]
```

**Pros:**
- Flexible for non-Discord users
- Can still link Discord accounts
- Fallback if Discord is down

**Cons:**
- More complex implementation
- Password management required

#### Option C: Discord OAuth Only (Simplest)

Only allow Discord OAuth for authentication. No local accounts.

**Pros:**
- Simplest implementation
- Perfect Discord identity alignment

**Cons:**
- Cannot have bot admins without Discord accounts
- Single point of failure

### 3.3 Recommended Approach: Option B (Hybrid)

**Rationale:**
- Supports flexibility for service accounts or automated access
- Allows Discord linking for identity verification
- Enables checking if admin is guild owner
- Provides fallback authentication method

---

## 4. Discord User Linking Architecture

### 4.1 Data Model Changes

#### New Entity: ApplicationUser

```csharp
// Location: DiscordBot.Core/Entities/ApplicationUser.cs
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Optional link to Discord user account.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// Cached Discord username (updated on OAuth refresh).
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// Cached Discord avatar URL.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>
    /// Display name for the admin UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Account creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful login timestamp.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Navigation to linked Discord user (if exists in bot's user table).
    /// </summary>
    public User? LinkedDiscordUser { get; set; }
}
```

#### New Entity: DiscordOAuthToken

```csharp
// Location: DiscordBot.Core/Entities/DiscordOAuthToken.cs
public class DiscordOAuthToken
{
    public Guid Id { get; set; }

    /// <summary>
    /// The ApplicationUser who owns this token.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// Discord OAuth access token (encrypted at rest).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Discord OAuth refresh token (encrypted at rest).
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration time.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// OAuth scopes granted.
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// When the token was last refreshed.
    /// </summary>
    public DateTime LastRefreshedAt { get; set; }
}
```

### 4.2 Discord OAuth Configuration

#### Required Discord Application Setup

```
Discord Developer Portal Configuration:
1. Create OAuth2 Application (or use existing bot application)
2. Configure Redirect URIs:
   - Development: https://localhost:5001/signin-discord
   - Production: https://yourdomain.com/signin-discord
3. Note Client ID and Client Secret
4. Required Scopes: identify, guilds (optional: guilds.members.read)
```

#### OAuth Scopes Explanation

| Scope | Purpose |
|-------|---------|
| `identify` | Get Discord user ID, username, avatar |
| `guilds` | List guilds user is member of |
| `guilds.members.read` | Get user's role in specific guild |

### 4.3 Linking Flow Diagram

```
+----------------+     +------------------+     +---------------+
|  Admin UI      |     |  Discord OAuth   |     |  Discord API  |
|  (Login Page)  |     |  Endpoint        |     |               |
+-------+--------+     +--------+---------+     +-------+-------+
        |                       |                       |
        | 1. Click "Login with Discord"                 |
        |---------------------->|                       |
        |                       |                       |
        |                       | 2. Redirect to Discord|
        |                       |---------------------->|
        |                       |                       |
        |                       | 3. User authorizes    |
        |                       |<----------------------|
        |                       |                       |
        |                       | 4. Callback with code |
        |<----------------------|                       |
        |                       |                       |
        | 5. Exchange code for token                    |
        |---------------------------------------------->|
        |                       |                       |
        | 6. Get user info with token                   |
        |---------------------------------------------->|
        |                       |                       |
        | 7. Create/Update ApplicationUser              |
        | 8. Link DiscordUserId                         |
        | 9. Issue session cookie                       |
        |                       |                       |
```

---

## 5. Revised Feature Breakdown

### 5.1 Feature 2.1: Authentication Infrastructure (Revised)

**Issue #64 - Updated Scope:**

#### Tasks

| Task | Description | Subagent |
|------|-------------|----------|
| 2.1.1 | Add ASP.NET Identity packages to projects | dotnet-specialist |
| 2.1.2 | Create `ApplicationUser` entity extending `IdentityUser` | dotnet-specialist |
| 2.1.3 | Create `DiscordOAuthToken` entity | dotnet-specialist |
| 2.1.4 | Update `BotDbContext` to inherit from `IdentityDbContext<ApplicationUser>` | dotnet-specialist |
| 2.1.5 | Create EF Core migration for Identity tables | dotnet-specialist |
| 2.1.6 | Configure Identity services in `Program.cs` | dotnet-specialist |
| 2.1.7 | Configure cookie authentication options | dotnet-specialist |
| 2.1.8 | Configure Discord OAuth provider | dotnet-specialist |
| 2.1.9 | Create `Pages/Account/Login.cshtml` with email/password form | dotnet-specialist |
| 2.1.10 | Create `Pages/Account/Login.cshtml.cs` with OnPost handler | dotnet-specialist |
| 2.1.11 | Add "Login with Discord" OAuth button | dotnet-specialist |
| 2.1.12 | Create OAuth callback handler | dotnet-specialist |
| 2.1.13 | Create `Pages/Account/Logout.cshtml.cs` | dotnet-specialist |
| 2.1.14 | Implement Discord account linking service | dotnet-specialist |
| 2.1.15 | Create user secrets for Discord OAuth credentials | dotnet-specialist |
| 2.1.16 | Document Identity configuration | docs-writer |

#### Acceptance Criteria (Updated)

- [ ] Users can log in with email/password
- [ ] Users can log in with Discord OAuth
- [ ] Discord account is linked to ApplicationUser on OAuth login
- [ ] Logout clears session properly
- [ ] Remember me extends cookie lifetime
- [ ] Failed logins show appropriate errors
- [ ] Account lockout after 5 failed attempts
- [ ] CSRF protection on all forms
- [ ] Passwords meet complexity requirements (8+ chars, mixed case, number, symbol)

### 5.2 Feature 2.2: Authorization Policies (Revised)

**Issue #65 - Updated Scope:**

#### Roles Definition

| Role | Description | Capabilities |
|------|-------------|--------------|
| `SuperAdmin` | System owner | Full access, user management, system config |
| `Admin` | Guild administrator | Full CRUD, bot control, cannot manage SuperAdmins |
| `Moderator` | Limited admin | View all, edit guild settings, cannot delete |
| `Viewer` | Read-only access | View dashboards and logs only |

#### Tasks

| Task | Description | Subagent |
|------|-------------|----------|
| 2.2.1 | Define role constants and hierarchy | dotnet-specialist |
| 2.2.2 | Create authorization policies in `Program.cs` | dotnet-specialist |
| 2.2.3 | Create `[Authorize]` page filters | dotnet-specialist |
| 2.2.4 | Create `Pages/Account/AccessDenied.cshtml` (403 page) | dotnet-specialist |
| 2.2.5 | Implement claims transformation for Discord-linked users | dotnet-specialist |
| 2.2.6 | Add role-based navigation visibility | dotnet-specialist |
| 2.2.7 | Create authorization tag helpers for conditional UI | dotnet-specialist |
| 2.2.8 | Add guild-specific authorization (admin can only manage their guilds) | dotnet-specialist |
| 2.2.9 | Document authorization policies | docs-writer |

#### Authorization Policies

```csharp
// Policy definitions
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("SuperAdmin", "Admin"));

    options.AddPolicy("RequireModerator", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Moderator"));

    options.AddPolicy("RequireViewer", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Moderator", "Viewer"));

    options.AddPolicy("RequireSuperAdmin", policy =>
        policy.RequireRole("SuperAdmin"));

    // Guild-specific policy (user must be linked to guild via Discord)
    options.AddPolicy("GuildAccess", policy =>
        policy.Requirements.Add(new GuildAccessRequirement()));
});
```

### 5.3 Feature 2.3: User Management (Revised)

**Issue #66 - Updated Scope:**

#### Tasks

| Task | Description | Subagent |
|------|-------------|----------|
| 2.3.1 | Create `IUserManagementService` interface | dotnet-specialist |
| 2.3.2 | Implement `UserManagementService` | dotnet-specialist |
| 2.3.3 | Create `Pages/Admin/Users/Index.cshtml` - user list | dotnet-specialist |
| 2.3.4 | Create `Pages/Admin/Users/Create.cshtml` - invite user | dotnet-specialist |
| 2.3.5 | Create `Pages/Admin/Users/Edit.cshtml` - edit user | dotnet-specialist |
| 2.3.6 | Create `Pages/Admin/Users/Details.cshtml` - user profile | dotnet-specialist |
| 2.3.7 | Implement pagination and search | dotnet-specialist |
| 2.3.8 | Add role assignment dropdown | dotnet-specialist |
| 2.3.9 | Implement account enable/disable | dotnet-specialist |
| 2.3.10 | Implement password reset (admin-initiated) | dotnet-specialist |
| 2.3.11 | Add Discord account link/unlink | dotnet-specialist |
| 2.3.12 | Prevent self-deletion/demotion | dotnet-specialist |
| 2.3.13 | Add activity log for user actions | dotnet-specialist |
| 2.3.14 | Update user list prototype | html-prototyper |
| 2.3.15 | Document user management API | docs-writer |

---

## 6. New Feature: 2.4 - Discord OAuth Integration (Preferred Method)

**Suggested New Issue:**

### Feature 2.4: Discord OAuth Integration

#### Description

Implement Discord OAuth to allow administrators to link their Discord accounts, enabling identity verification and guild-based permissions.

#### Tasks

| Task | Description | Subagent |
|------|-------------|----------|
| 2.4.1 | Register Discord OAuth application | docs-writer |
| 2.4.2 | Configure `AspNet.Security.OAuth.Discord` package | dotnet-specialist |
| 2.4.3 | Create OAuth callback handler | dotnet-specialist |
| 2.4.4 | Implement token storage service | dotnet-specialist |
| 2.4.5 | Create Discord user info caching | dotnet-specialist |
| 2.4.6 | Implement guild membership verification | dotnet-specialist |
| 2.4.7 | Create `Pages/Account/LinkDiscord.cshtml` | dotnet-specialist |
| 2.4.8 | Create `Pages/Account/DiscordCallback.cshtml` | dotnet-specialist |
| 2.4.9 | Add Discord avatar display in UI | dotnet-specialist |
| 2.4.10 | Handle OAuth token refresh | dotnet-specialist |
| 2.4.11 | Document Discord OAuth setup | docs-writer |

#### Acceptance Criteria

- [ ] Users can link Discord account from profile
- [ ] Discord username and avatar displayed when linked
- [ ] OAuth tokens stored encrypted
- [ ] Tokens refresh automatically before expiry
- [ ] Users can unlink Discord account
- [ ] Guild membership can be verified for linked users

---

## 7. New Feature: 2.5 - Discord Bot Verification (Alternative Linking Method)

**Suggested New Issue:**

### Feature 2.5: Discord Bot Account Verification

#### Description

For users who create accounts via email/password and prefer not to use Discord OAuth, provide an alternative verification flow using the Discord bot itself. This allows users to link their Discord account without granting OAuth permissions.

#### Verification Flow

```
+------------------+     +------------------+     +------------------+
|  Web UI          |     |  Discord Bot     |     |  Database        |
|  (User Profile)  |     |  (/verify-acct)  |     |                  |
+--------+---------+     +--------+---------+     +--------+---------+
         |                        |                        |
         | 1. User clicks                                  |
         |    "Link via Bot"                               |
         |------------------------------------------------>|
         |                        |                        |
         |                        | 2. Store pending       |
         |                        |    verification record |
         |                        |    with user's email   |
         |<------------------------------------------------|
         |                        |                        |
         | 3. Display instructions:                        |
         |    "Run /verify-account                         |
         |     in Discord"                                 |
         |                        |                        |
         |                        | 4. User runs           |
         |                        |    /verify-account     |
         |                        |    command in Discord  |
         |                        |----------------------->|
         |                        |                        |
         |                        | 5. Generate unique     |
         |                        |    verification code   |
         |                        |    (e.g., "ABC-123")   |
         |                        |<-----------------------|
         |                        |                        |
         |                        | 6. DM user with code   |
         |                        |    (ephemeral/private) |
         |                        |                        |
         | 7. User enters code                             |
         |    in web UI                                    |
         |------------------------------------------------>|
         |                        |                        |
         |                        | 8. Validate code,      |
         |                        |    link Discord ID     |
         |                        |    to ApplicationUser  |
         |                        |----------------------->|
         |                        |                        |
         | 9. Show success:                                |
         |    "Discord account                             |
         |     linked!"                                    |
         |<------------------------------------------------|
```

#### Data Model

```csharp
// Location: DiscordBot.Core/Entities/VerificationCode.cs
public class VerificationCode
{
    public Guid Id { get; set; }

    /// <summary>
    /// The ApplicationUser requesting verification.
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicationUser { get; set; } = null!;

    /// <summary>
    /// The unique verification code (e.g., "ABC-123").
    /// Human-readable format for easy entry.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Discord user ID that generated this code.
    /// Set when user runs /verify-account command.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// When the code was generated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Code expiration (default: 15 minutes).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the code has been used.
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Verification status.
    /// </summary>
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
}

public enum VerificationStatus
{
    Pending,        // User initiated, waiting for Discord command
    CodeGenerated,  // User ran Discord command, code ready
    Completed,      // Successfully verified
    Expired,        // Code expired
    Failed          // Max attempts exceeded
}
```

#### Bot Command: /verify-account

```csharp
// Location: DiscordBot.Bot/Commands/VerifyAccountModule.cs

[SlashCommand("verify-account", "Generate a verification code to link your Discord account to the web UI")]
public async Task VerifyAccountAsync()
{
    // 1. Check if user already has a pending verification
    var existingCode = await _verificationService.GetPendingForDiscordUserAsync(Context.User.Id);

    if (existingCode != null)
    {
        // Return existing code
        await RespondAsync(
            $"🔐 **Your verification code:** `{existingCode.Code}`\n\n" +
            $"Enter this code in the web interface to complete linking.\n" +
            $"This code expires {TimestampTag.FromDateTime(existingCode.ExpiresAt, TimestampTagStyles.Relative)}.",
            ephemeral: true);
        return;
    }

    // 2. Generate new verification code
    var code = await _verificationService.GenerateCodeAsync(Context.User.Id);

    // 3. Respond with code (ephemeral - only visible to user)
    await RespondAsync(
        $"🔐 **Your verification code:** `{code.Code}`\n\n" +
        $"Enter this code in the web interface to link your Discord account.\n" +
        $"This code expires in 15 minutes.",
        ephemeral: true);
}
```

#### Tasks

| Task | Description | Subagent |
|------|-------------|----------|
| 2.5.1 | Create `VerificationCode` entity | dotnet-specialist |
| 2.5.2 | Add `VerificationCode` DbSet and migration | dotnet-specialist |
| 2.5.3 | Create `IVerificationService` interface | dotnet-specialist |
| 2.5.4 | Implement `VerificationService` with code generation | dotnet-specialist |
| 2.5.5 | Create `/verify-account` slash command module | dotnet-specialist |
| 2.5.6 | Add verification code entry to user profile page | dotnet-specialist |
| 2.5.7 | Implement code validation endpoint | dotnet-specialist |
| 2.5.8 | Add rate limiting (max 3 codes per hour) | dotnet-specialist |
| 2.5.9 | Create background job to clean expired codes | dotnet-specialist |
| 2.5.10 | Add verification status display in profile | dotnet-specialist |
| 2.5.11 | Write tests for verification flow | test-writer |
| 2.5.12 | Document verification process | docs-writer |

#### Acceptance Criteria

- [ ] User can initiate "Link via Discord Bot" from web profile
- [ ] Bot command `/verify-account` generates unique code (ephemeral response)
- [ ] Verification codes are 6-8 characters, human-readable format
- [ ] Codes expire after 15 minutes
- [ ] User can enter code in web UI to complete linking
- [ ] Successfully links Discord user ID to ApplicationUser
- [ ] Shows Discord username/avatar after linking
- [ ] Rate limited to prevent abuse (max 3 codes per hour per user)
- [ ] Expired/used codes are cleaned up automatically
- [ ] Clear error messages for invalid/expired codes

#### Security Considerations

1. **Code Format:** Use unambiguous characters (no 0/O, 1/I/l confusion)
   - Example format: `ABC-123` or `ABCD-1234`
   - Generated from charset: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`

2. **Rate Limiting:**
   - Max 3 verification codes per hour per web user
   - Max 10 code checks per hour per web user
   - Bot command has its own rate limit (existing `RateLimitAttribute`)

3. **Privacy:**
   - Bot response is ephemeral (only visible to user)
   - Never log verification codes
   - Codes hashed in database (optional, for extra security)

4. **Expiration:**
   - Codes expire after 15 minutes
   - Background job cleans expired codes hourly

---

## 8. Implementation Approach: Leveraging ASP.NET Identity

### Philosophy

We will use ASP.NET Identity's built-in infrastructure as much as possible, only creating custom Razor Pages for the UI itself. This means:

**What we USE from ASP.NET Identity (DO NOT reimplement):**
- `UserManager<ApplicationUser>` - user CRUD, password hashing, validation
- `SignInManager<ApplicationUser>` - login/logout, cookie management
- `RoleManager<IdentityRole>` - role CRUD operations
- Identity options - password policies, lockout settings
- Token generation - password reset, email confirmation tokens
- Claims transformation - built-in claims pipeline
- External authentication - OAuth provider integration

**What we CREATE ourselves (custom Razor Pages):**
- Login page UI (calls `SignInManager.PasswordSignInAsync`)
- Logout page UI (calls `SignInManager.SignOutAsync`)
- User management pages (uses `UserManager` methods)
- Access denied page (styled to match our design)
- Discord linking pages (uses OAuth + custom verification)

### Key Pattern: Thin Razor Pages

```csharp
// Example: Login.cshtml.cs - Uses SignInManager, doesn't reimplement auth
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public async Task<IActionResult> OnPostAsync()
    {
        // Let ASP.NET Identity do the heavy lifting
        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(returnUrl);

        if (result.IsLockedOut)
            return RedirectToPage("./Lockout");

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
```

---

## 9. Implementation Timeline

### Phase 1: Identity Foundation

```
- Add Identity packages to all projects
- Create ApplicationUser extending IdentityUser
- Update BotDbContext to inherit IdentityDbContext<ApplicationUser>
- Create Identity migration
- Configure Identity services with password/lockout policies
- Seed initial roles (SuperAdmin, Admin, Moderator, Viewer)
- Create custom Login/Logout Razor Pages (using SignInManager)
```

### Phase 2: Authorization

```
- Define authorization policies in Program.cs
- Add [Authorize] attributes to protected pages
- Create custom AccessDenied page
- Implement role-based navigation visibility
- Add claims transformation for Discord-linked users
```

### Phase 3: Discord OAuth

```
- Configure Discord OAuth provider
- Create OAuth callback handler
- Implement account linking (OAuth flow)
- Store Discord tokens (encrypted)
- Display Discord avatar when linked
```

### Phase 4: Discord Bot Verification

```
- Create VerificationCode entity and migration
- Implement IVerificationService
- Create /verify-account slash command
- Add verification code entry UI in profile
- Implement code validation endpoint
- Add cleanup job for expired codes
```

### Phase 5: User Management

```
- Create User List page (uses UserManager.Users)
- Create User Create page (uses UserManager.CreateAsync)
- Create User Edit page (uses UserManager.UpdateAsync)
- Implement role assignment (uses UserManager.AddToRoleAsync)
- Add account enable/disable toggle
- Implement password reset (uses UserManager.GeneratePasswordResetTokenAsync)
```

---

## 10. Package Dependencies

### Required NuGet Packages

```xml
<!-- DiscordBot.Core.csproj -->
<PackageReference Include="Microsoft.Extensions.Identity.Core" Version="8.0.0" />

<!-- DiscordBot.Infrastructure.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />

<!-- DiscordBot.Bot.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="8.0.0" />
<PackageReference Include="AspNet.Security.OAuth.Discord" Version="8.0.0" />
```

---

## 11. Security Considerations

### 9.1 Password Policy

```csharp
services.Configure<IdentityOptions>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
});
```

### 9.2 Cookie Configuration

```csharp
services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
```

### 9.3 OAuth Token Security

- Store tokens encrypted at rest using Data Protection API
- Implement token refresh before expiry
- Log OAuth events for audit trail
- Validate token scopes on each use

### 9.4 Audit Logging

Track authentication events:
- Login success/failure
- Logout
- Password change
- Role changes
- Account lockout
- Discord link/unlink

---

## 12. Migration Strategy

### 10.1 Database Migration

```bash
# Create Identity migration
dotnet ef migrations add AddIdentityTables \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot

# Apply migration
dotnet ef database update \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot
```

### 10.2 Initial Admin User Seeding

```csharp
// In Program.cs or a dedicated seeder
public static async Task SeedAdminUser(IServiceProvider services)
{
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // Create roles
    string[] roles = { "SuperAdmin", "Admin", "Moderator", "Viewer" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Create default admin (password from user secrets)
    var adminEmail = "admin@example.com";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            DisplayName = "System Administrator",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(admin, "CHANGE_ME_IMMEDIATELY!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }
    }
}
```

---

## 13. Subagent Task Assignments

### design-specialist

- Update design system with auth-specific components (login form states, OAuth buttons)
- Create Discord branding compliance specs for OAuth button
- Define user avatar display patterns

### html-prototyper

- Update Login Page Prototype (#91) with Discord OAuth button
- Update Registration Page Prototype (#90) if self-registration enabled
- Create User Management list/edit prototypes

### dotnet-specialist

All backend implementation as detailed in Features 2.1-2.4 task lists.

### docs-writer

- Document Identity configuration
- Document Discord OAuth setup process
- Document authorization policies
- Create admin user guide
- Update API documentation for auth endpoints

---

## 14. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Discord OAuth downtime | Low | Medium | Support email/password fallback |
| Token storage breach | Low | High | Encrypt tokens, rotate regularly |
| Session hijacking | Low | High | HttpOnly cookies, HTTPS only |
| Role escalation | Low | High | Server-side role validation |
| Brute force attacks | Medium | Medium | Account lockout, rate limiting |
| Forgot password abuse | Medium | Low | Rate limit reset requests |

---

## 15. Questions Answered

### Q: Are we using built-in ASP.NET Identity features?

**A: Yes.** ASP.NET Identity Core should be used for:
- User storage and management
- Password hashing and validation
- Account lockout
- Role management
- Claims-based authorization

### Q: Should we use ASP.NET Identity for User and Role management?

**A: Yes.** ASP.NET Identity provides:
- Secure, tested implementation
- Built-in role support via `IdentityRole`
- `UserManager<T>` and `RoleManager<T>` services
- Extensible through custom `IdentityUser` subclass

### Q: Can we add OAuth support (specifically Discord OAuth)?

**A: Yes.** Implementation path:
1. Install `AspNet.Security.OAuth.Discord` package
2. Configure in `Program.cs`
3. Create callback handler
4. Store tokens for API access

### Q: How can we link/authenticate a bot/application user to a Discord user?

**A: Via Discord OAuth linking:**
1. `ApplicationUser` has `DiscordUserId` property (nullable ulong)
2. When user completes Discord OAuth, extract Discord user ID from claims
3. Store Discord ID in `ApplicationUser.DiscordUserId`
4. Optionally foreign key to existing `User` table for cross-referencing bot interactions
5. Use linked Discord ID to verify guild membership/roles

---

## 16. Conclusion

Epic 2 provides a solid foundation but requires expansion to include:

1. **ASP.NET Identity integration** - Leverage `UserManager`, `SignInManager`, and `RoleManager` for all auth operations; only create custom Razor Page UIs
2. **Discord OAuth (Preferred)** - Primary method for Discord account linking with automatic user identity capture
3. **Discord Bot Verification (Alternative)** - For users who prefer not to use OAuth, link via `/verify-account` command and verification code
4. **Proper entity separation** - `ApplicationUser` (admin UI) vs `User` (Discord bot domain)
5. **Two Discord linking methods** - OAuth for seamless experience, bot verification for privacy-conscious users

### Updated Feature List

| Feature | Description | Linking Method |
|---------|-------------|----------------|
| 2.1 | Authentication Infrastructure | N/A (foundation) |
| 2.2 | Authorization Policies | N/A (foundation) |
| 2.3 | User Management | N/A (admin CRUD) |
| 2.4 | Discord OAuth Integration | **Preferred** - automatic linking |
| 2.5 | Discord Bot Verification | **Alternative** - manual code entry |

The hybrid approach provides maximum flexibility while maintaining security appropriate for a Discord bot management system.

---

## Appendix A: Updated Issue Descriptions

### Issue #64 - Feature 2.1: Authentication Infrastructure (Updated)

```markdown
## Description
Set up ASP.NET Identity with cookie-based authentication and Discord OAuth support.

## Parent Epic
#63 - Epic 2: Authentication and Authorization

## Acceptance Criteria
- [ ] ASP.NET Identity configured and migrations applied
- [ ] Login page with email/password form
- [ ] Discord OAuth "Login with Discord" button
- [ ] Logout functionality clears session
- [ ] Remember me option for persistent cookies
- [ ] Failed login attempts show appropriate error messages
- [ ] Account lockout after 5 failed attempts
- [ ] Password field masks input
- [ ] CSRF protection on all forms
- [ ] Password meets complexity requirements

## Dependencies
- Epic 1 complete (#57)

## Tasks
(See section 5.1 of architecture document)
```

### New Issue - Feature 2.4: Discord OAuth Integration

```markdown
## Description
Implement Discord OAuth to allow administrators to link their Discord accounts for identity verification and guild-based permissions.

## Parent Epic
#63 - Epic 2: Authentication and Authorization

## Acceptance Criteria
- [ ] Users can link Discord account from profile
- [ ] Discord username and avatar displayed when linked
- [ ] OAuth tokens stored encrypted at rest
- [ ] Tokens refresh automatically before expiry
- [ ] Users can unlink Discord account
- [ ] Guild membership can be verified for linked users
- [ ] Discord OAuth login creates/links ApplicationUser

## Dependencies
- Feature 2.1: Authentication Infrastructure (#64)

## Tasks
(See section 6 of architecture document)
```

---

*Document prepared by: Systems Architect Agent*
*Review status: Ready for stakeholder review*
