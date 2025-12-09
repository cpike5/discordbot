# Identity Configuration

**Last Updated:** 2025-12-09
**Epic Reference:** [Epic 2: Authentication and Authorization](epic-2-auth-architecture-plan.md)
**Related Issues:** #64, #65, #66

---

## Overview

This document provides comprehensive guidance for configuring ASP.NET Identity with Discord OAuth integration for the Discord Bot Management System. The authentication system supports both traditional email/password authentication and Discord OAuth for seamless identity verification.

### Key Features

- ASP.NET Core Identity for user management and authentication
- Discord OAuth 2.0 for Discord account linking
- Cookie-based authentication with secure session management
- Role-based authorization (SuperAdmin, Admin, Moderator, Viewer)
- Password complexity requirements and account lockout
- CSRF protection on all authentication endpoints

### Architecture Overview

The system maintains two separate user entities:

| Entity | Purpose | Primary Key | Storage Table |
|--------|---------|-------------|---------------|
| `User` | Discord bot interactions | Discord Snowflake ID (ulong) | `Users` |
| `ApplicationUser` | Admin UI authentication | GUID | `AspNetUsers` |

**Linking:** `ApplicationUser.DiscordUserId` optionally references `User.Id` when a Discord account is linked.

---

## ApplicationUser Entity

The `ApplicationUser` entity extends `IdentityUser` with Discord-specific properties.

### Entity Definition

```csharp
// Location: DiscordBot.Core/Entities/ApplicationUser.cs
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Optional link to Discord user account.
    /// Populated when user completes Discord OAuth flow.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// Cached Discord username (updated on OAuth refresh).
    /// Format: "username" or "username#0000" for legacy discriminators
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// Cached Discord avatar URL.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>
    /// Display name for the admin UI.
    /// Defaults to email prefix or Discord username when linked.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the account is active.
    /// Inactive accounts cannot log in.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Account creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful login timestamp (UTC).
    /// Updated on each successful authentication.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Navigation to linked Discord user (if exists in bot's user table).
    /// </summary>
    public User? LinkedDiscordUser { get; set; }
}
```

### Property Details

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| `DiscordUserId` | `ulong?` | Yes | Discord snowflake ID when account is linked via OAuth |
| `DiscordUsername` | `string?` | Yes | Cached Discord username for display purposes |
| `DiscordAvatarUrl` | `string?` | Yes | Discord CDN avatar URL (e.g., `https://cdn.discordapp.com/avatars/...`) |
| `DisplayName` | `string` | No | Human-friendly name shown in UI (required, max 100 chars) |
| `IsActive` | `bool` | No | Account status flag (default: `true`) |
| `CreatedAt` | `DateTime` | No | Account creation timestamp |
| `LastLoginAt` | `DateTime?` | Yes | Most recent successful login timestamp |
| `LinkedDiscordUser` | `User?` | Yes | EF Core navigation property to bot's `User` entity |

---

## Roles and Authorization

### Role Hierarchy

The system defines four hierarchical roles:

| Role | Level | Description | Typical Use Case |
|------|-------|-------------|------------------|
| `SuperAdmin` | 1 | System owner with unrestricted access | Bot owner, system administrator |
| `Admin` | 2 | Guild administrator with full bot control | Discord server owner/administrator |
| `Moderator` | 3 | Limited administrative access | Discord server moderator |
| `Viewer` | 4 | Read-only access to dashboards | Auditor, read-only staff |

### Role Capabilities

#### SuperAdmin

- Full system access
- User management (create, edit, delete, assign roles)
- System configuration changes
- Can manage all guilds and users
- Cannot be locked out or demoted by other admins

#### Admin

- Full CRUD operations for assigned guilds
- Bot control (start, stop, restart)
- Guild settings management
- User management (except SuperAdmins)
- Cannot manage SuperAdmin accounts

#### Moderator

- View all dashboards and logs
- Edit guild settings
- Cannot delete resources
- Cannot manage users
- Cannot control bot lifecycle

#### Viewer

- Read-only access to dashboards
- View logs and statistics
- Cannot modify any resources
- Cannot access user management

### Authorization Policies

Authorization policies are defined in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireSuperAdmin", policy =>
        policy.RequireRole("SuperAdmin"));

    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("SuperAdmin", "Admin"));

    options.AddPolicy("RequireModerator", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Moderator"));

    options.AddPolicy("RequireViewer", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Moderator", "Viewer"));

    // Guild-specific access (user must be linked to guild via Discord)
    options.AddPolicy("GuildAccess", policy =>
        policy.Requirements.Add(new GuildAccessRequirement()));
});
```

### Using Authorization Policies

#### In Razor Pages

```csharp
// Restrict entire page to Admins and above
[Authorize(Policy = "RequireAdmin")]
public class EditGuildModel : PageModel
{
    // ...
}
```

#### In Razor Page Markup

The application provides two custom tag helpers for conditional rendering:

**`<authorize>` Tag Helper** - For policy-based authorization:

```cshtml
@* Show element only to SuperAdmins *@
<authorize policy="RequireSuperAdmin">
    <a href="/Admin/Users">Manage Users</a>
</authorize>

@* Show element to Moderators and above *@
<authorize policy="RequireModerator">
    <button type="submit">Save Changes</button>
</authorize>

@* Show element only to non-authenticated users *@
<authorize negate="true">
    <a href="/Account/Login">Sign In</a>
</authorize>
```

**`if-role` Attribute Tag Helper** - For role-based visibility on any HTML element:

```cshtml
@* Element visible only to SuperAdmin and Admin roles *@
<div if-role="SuperAdmin,Admin">
    <p>This content is only visible to admins.</p>
</div>

@* Navigation link visible to moderators and above *@
<a href="/Servers" if-role="SuperAdmin,Admin,Moderator">Servers</a>
```

**Tag Helper Location:** `src/DiscordBot.Bot/TagHelpers/`

#### In Controllers (API)

```csharp
[Authorize(Policy = "RequireAdmin")]
[HttpPost("api/guilds/{guildId}/settings")]
public async Task<IActionResult> UpdateGuildSettings(ulong guildId, [FromBody] GuildSettingsDto settings)
{
    // Only Admins and SuperAdmins can access
}
```

---

## Password Requirements

Password policies are enforced through ASP.NET Identity configuration.

### Complexity Requirements

| Requirement | Value | Description |
|-------------|-------|-------------|
| Minimum Length | 8 characters | Passwords must be at least 8 characters long |
| Require Digit | Yes | Must contain at least one number (0-9) |
| Require Lowercase | Yes | Must contain at least one lowercase letter (a-z) |
| Require Uppercase | Yes | Must contain at least one uppercase letter (A-Z) |
| Require Non-Alphanumeric | Yes | Must contain at least one special character (!@#$%^&*) |
| Required Unique Characters | 4 | At least 4 distinct characters |

### Configuration

```csharp
// Location: DiscordBot.Bot/Program.cs
builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;

    // User settings
    options.User.RequireUniqueEmail = true;
});
```

### Example Valid Passwords

- `MyP@ssw0rd`
- `Str0ng!Pass`
- `Admin#2025`
- `B0t$ecure`

### Example Invalid Passwords

| Password | Reason |
|----------|--------|
| `password` | No uppercase, no digit, no special character |
| `Password` | No digit, no special character |
| `Password1` | No special character |
| `Pass1!` | Too short (less than 8 characters) |
| `AAAAA1!a` | Less than 4 unique characters |

---

## Account Lockout

Account lockout protects against brute-force password attacks.

### Lockout Configuration

| Setting | Value | Description |
|---------|-------|-------------|
| Max Failed Attempts | 5 | Number of consecutive failed login attempts before lockout |
| Lockout Duration | 15 minutes | How long the account remains locked |
| Lockout Enabled for New Users | Yes | New accounts are subject to lockout policy |

### Lockout Behavior

1. User enters incorrect password
2. Failed attempt counter increments
3. After 5 failed attempts, account is locked for 15 minutes
4. During lockout, login attempts return "Account locked" error
5. After 15 minutes, lockout automatically expires
6. Successful login resets failed attempt counter

### Configuration

```csharp
builder.Services.Configure<IdentityOptions>(options =>
{
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});
```

### Manual Lockout Reset

SuperAdmins can manually reset lockout via User Management UI:

```csharp
// Reset lockout programmatically
await userManager.SetLockoutEndDateAsync(user, null);
await userManager.ResetAccessFailedCountAsync(user);
```

---

## Cookie Settings

Authentication cookies are configured with security best practices.

### Cookie Configuration

| Setting | Value | Description |
|---------|-------|-------------|
| HttpOnly | `true` | Cookie not accessible via JavaScript (XSS protection) |
| Secure Policy | `Always` | Cookie only sent over HTTPS |
| SameSite Mode | `Strict` | Cookie not sent on cross-site requests (CSRF protection) |
| Expiration | 24 hours | Base cookie lifetime |
| Sliding Expiration | `true` | Cookie renewed on activity |

### Configuration

```csharp
builder.Services.ConfigureApplicationCookie(options =>
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

### Cookie Lifetime Behavior

#### Standard Session (No "Remember Me")

- User logs in without "Remember Me" checked
- Session cookie expires after 24 hours of inactivity
- Cookie is renewed (sliding) on each request within 24-hour window
- User remains logged in as long as they're active

#### Persistent Session ("Remember Me" Checked)

- User logs in with "Remember Me" checked
- Persistent cookie stored for extended period (e.g., 30 days)
- User remains logged in across browser sessions
- Cookie still subject to security policies (HttpOnly, Secure, SameSite)

---

## Discord OAuth Setup

### Prerequisites

1. Discord application created at https://discord.com/developers/applications
2. Bot already configured (same application can be used for OAuth)
3. Access to application's OAuth2 settings

### Step-by-Step Configuration

#### 1. Configure OAuth2 in Discord Developer Portal

1. Navigate to https://discord.com/developers/applications
2. Select your bot application
3. Go to **OAuth2** section in left sidebar
4. Click **Add Redirect** under "Redirects"

**Development Redirect URI:**
```
https://localhost:5001/signin-discord
```

**Production Redirect URI:**
```
https://yourdomain.com/signin-discord
```

5. Click **Save Changes**
6. Copy **Client ID** from "Client Information" section
7. Click **Reset Secret** to generate a new client secret
8. Copy **Client Secret** immediately (only shown once)

#### 2. Store Credentials in User Secrets

```bash
cd src/DiscordBot.Bot

# Set Discord OAuth credentials
dotnet user-secrets set "Discord:OAuth:ClientId" "YOUR_CLIENT_ID_HERE"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "YOUR_CLIENT_SECRET_HERE"
```

#### 3. Configure OAuth Provider in Application

The Discord OAuth provider is configured in `Program.cs`:

```csharp
builder.Services.AddAuthentication()
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:OAuth:ClientId"];
        options.ClientSecret = builder.Configuration["Discord:OAuth:ClientSecret"];
        options.Scope.Add("identify");
        options.Scope.Add("guilds");
        options.SaveTokens = true;
    });
```

### OAuth Scopes

The application requests the following OAuth scopes:

| Scope | Purpose | Required |
|-------|---------|----------|
| `identify` | Get Discord user ID, username, avatar | Yes |
| `guilds` | List guilds user is member of | Yes |
| `guilds.members.read` | Get user's role in specific guild | Optional (future) |

### OAuth Flow Diagram

```
+------------------+     +------------------+     +------------------+
|   User Browser   |     |   Admin UI       |     |   Discord API    |
+--------+---------+     +--------+---------+     +--------+---------+
         |                        |                        |
         | 1. Click "Login       |                        |
         |    with Discord"      |                        |
         |---------------------->|                        |
         |                        |                        |
         |                        | 2. Redirect to        |
         |                        |    Discord OAuth      |
         |<-----------------------|                        |
         |                        |                        |
         | 3. User authorizes    |                        |
         |    application        |                        |
         |--------------------------------------------->|
         |                        |                        |
         |                        | 4. Callback with      |
         |                        |    authorization code |
         |<-----------------------|                        |
         |                        |                        |
         |                        | 5. Exchange code      |
         |                        |    for access token   |
         |                        |---------------------->|
         |                        |                        |
         |                        | 6. Get user info      |
         |                        |    with token         |
         |                        |---------------------->|
         |                        |                        |
         |                        | 7. Create/update      |
         |                        |    ApplicationUser    |
         |                        | 8. Link DiscordUserId |
         |                        | 9. Issue cookie       |
         |                        |                        |
         | 10. Redirect to       |                        |
         |     dashboard         |                        |
         |<-----------------------|                        |
```

### Discord Account Linking Process

When a user logs in via Discord OAuth:

1. **New User:** `ApplicationUser` is created with Discord information populated
2. **Existing User (Email/Password):** User can link Discord from profile page
3. **Returning Discord User:** Existing `ApplicationUser` is updated with latest Discord info

**Data Stored:**
- `DiscordUserId`: Discord snowflake ID
- `DiscordUsername`: Current Discord username
- `DiscordAvatarUrl`: Avatar URL (refreshed on each login)
- OAuth tokens (encrypted) for API access

---

## Initial Configuration

### Database Migration

After configuring Identity, create and apply migrations:

```bash
# Generate migration
dotnet ef migrations add AddIdentityTables \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot

# Apply migration
dotnet ef database update \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot
```

### Seed Initial Data

The application automatically seeds roles and an initial admin user on first run.

#### Roles Created

- SuperAdmin
- Admin
- Moderator
- Viewer

#### Default Admin User

If configured via user secrets:

```bash
dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:DefaultAdmin:Password" "InitialPassword123!"
```

**Default credentials (if not configured):**
- Email: `admin@example.com`
- Password: `Admin@123456` (CHANGE IMMEDIATELY)
- Role: `SuperAdmin`

**Security Warning:** Change the default admin password immediately after first login.

### Seeding Logic

```csharp
// Location: DiscordBot.Bot/Data/IdentitySeeder.cs
public static async Task SeedAsync(IServiceProvider services)
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

    // Create default admin
    var adminEmail = configuration["Identity:DefaultAdmin:Email"] ?? "admin@example.com";
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

        var password = configuration["Identity:DefaultAdmin:Password"] ?? "Admin@123456";
        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }
    }
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: "Invalid redirect_uri" error during Discord OAuth

**Cause:** Redirect URI in application doesn't match Discord Developer Portal configuration.

**Solution:**
1. Verify redirect URI in Discord Developer Portal exactly matches application configuration
2. Check for trailing slashes (should not have trailing slash)
3. Ensure protocol is `https` (not `http`)
4. For development, make sure using `https://localhost:5001/signin-discord`

#### Issue: "The client secret is invalid" error

**Cause:** Client secret in user secrets doesn't match Discord application.

**Solution:**
1. Go to Discord Developer Portal > OAuth2
2. Reset client secret
3. Copy new secret immediately (only shown once)
4. Update user secrets:
   ```bash
   dotnet user-secrets set "Discord:OAuth:ClientSecret" "NEW_SECRET_HERE"
   ```

#### Issue: Account locked after failed login attempts

**Cause:** Too many failed login attempts triggered account lockout.

**Solution:**
- **Wait 15 minutes** for automatic lockout expiration
- **Or** contact SuperAdmin to manually reset lockout via User Management UI

#### Issue: Password doesn't meet complexity requirements

**Cause:** Password doesn't satisfy all requirements (length, uppercase, lowercase, digit, special character).

**Solution:**
Create password with:
- At least 8 characters
- At least 1 uppercase letter
- At least 1 lowercase letter
- At least 1 digit
- At least 1 special character (!@#$%^&*)

**Example valid password:** `MyP@ssw0rd`

#### Issue: CSRF validation failed

**Cause:** Anti-forgery token mismatch (form token doesn't match cookie token).

**Solution:**
1. Clear browser cookies and try again
2. Ensure forms include `@Html.AntiForgeryToken()` in markup
3. Verify `[ValidateAntiForgeryToken]` attribute on POST handlers
4. Check for expired sessions (cookie timeout)

#### Issue: "Not authorized" error when accessing page

**Cause:** User's role doesn't have access to requested resource.

**Solution:**
1. Verify user is logged in (check for redirect to login page)
2. Contact administrator to verify role assignment
3. Check authorization policy requirements for the page
4. Review [Authorization Policies](#authorization-policies) section

#### Issue: Discord username not showing after linking

**Cause:** OAuth token may have expired or been revoked.

**Solution:**
1. Unlink Discord account from profile
2. Re-link using "Login with Discord" or "Link Discord Account"
3. Verify OAuth scopes include `identify`

#### Issue: Can't create user with existing email

**Cause:** ASP.NET Identity requires unique emails.

**Solution:**
- Use a different email address
- Or delete/disable the existing user account first

---

## Security Best Practices

### For Administrators

1. **Change Default Password:** Immediately change the default admin password after first login
2. **Use Strong Passwords:** Follow password complexity requirements
3. **Enable Discord Linking:** Link Discord account for additional identity verification
4. **Monitor Login Activity:** Review LastLoginAt timestamps regularly
5. **Disable Inactive Accounts:** Set `IsActive = false` for unused accounts
6. **Limit SuperAdmin Role:** Only assign SuperAdmin to trusted system owners
7. **Regular Audits:** Periodically review user roles and permissions

### For Developers

1. **Never Commit Secrets:** Always use User Secrets for sensitive configuration
2. **HTTPS Only:** Enforce HTTPS in production (cookies use `SecurePolicy.Always`)
3. **Validate Input:** Use ASP.NET model validation on all forms
4. **CSRF Protection:** Include anti-forgery tokens on all POST forms
5. **Rate Limiting:** Implement rate limiting on authentication endpoints
6. **Audit Logging:** Log all authentication events (login, logout, role changes)
7. **Encrypt OAuth Tokens:** Store Discord OAuth tokens encrypted at rest
8. **Token Refresh:** Implement automatic OAuth token refresh before expiry

### Production Checklist

- [ ] Changed default admin password
- [ ] Configured Discord OAuth with production redirect URI
- [ ] User secrets configured (not in appsettings.json)
- [ ] HTTPS enforced (HTTP Strict Transport Security enabled)
- [ ] Cookie settings use `Secure` and `HttpOnly`
- [ ] Account lockout enabled
- [ ] Password complexity requirements enforced
- [ ] CSRF protection on all forms
- [ ] Audit logging configured
- [ ] Database migration applied
- [ ] Initial roles seeded

---

## API Reference

### Key Identity Services

#### UserManager<ApplicationUser>

Primary service for user management operations.

```csharp
// Inject in constructor
public class UserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserManagementService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }
}
```

**Common Operations:**

```csharp
// Create user
var user = new ApplicationUser { UserName = email, Email = email };
var result = await _userManager.CreateAsync(user, password);

// Find user by email
var user = await _userManager.FindByEmailAsync(email);

// Find user by ID
var user = await _userManager.FindByIdAsync(userId);

// Update user
user.DisplayName = "New Name";
await _userManager.UpdateAsync(user);

// Delete user
await _userManager.DeleteAsync(user);

// Check password
var isValid = await _userManager.CheckPasswordAsync(user, password);

// Change password
await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

// Reset password
var token = await _userManager.GeneratePasswordResetTokenAsync(user);
await _userManager.ResetPasswordAsync(user, token, newPassword);

// Add to role
await _userManager.AddToRoleAsync(user, "Admin");

// Remove from role
await _userManager.RemoveFromRoleAsync(user, "Admin");

// Get user roles
var roles = await _userManager.GetRolesAsync(user);

// Check if in role
var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

// Reset lockout
await _userManager.SetLockoutEndDateAsync(user, null);
await _userManager.ResetAccessFailedCountAsync(user);
```

#### SignInManager<ApplicationUser>

Service for authentication operations.

```csharp
// Inject in Razor Page or controller
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }
}
```

**Common Operations:**

```csharp
// Sign in with password
var result = await _signInManager.PasswordSignInAsync(
    email, password, isPersistent: rememberMe, lockoutOnFailure: true);

if (result.Succeeded) { /* Login successful */ }
if (result.IsLockedOut) { /* Account locked */ }
if (result.RequiresTwoFactor) { /* 2FA required */ }

// Sign out
await _signInManager.SignOutAsync();

// Check if signed in
var isSignedIn = _signInManager.IsSignedIn(User);

// Get current user
var user = await _userManager.GetUserAsync(User);
```

#### RoleManager<IdentityRole>

Service for role management.

```csharp
// Create role
if (!await _roleManager.RoleExistsAsync("CustomRole"))
{
    await _roleManager.CreateAsync(new IdentityRole("CustomRole"));
}

// Delete role
var role = await _roleManager.FindByNameAsync("CustomRole");
await _roleManager.DeleteAsync(role);

// Get all roles
var roles = _roleManager.Roles.ToList();
```

---

## Related Documentation

- [Epic 2: Authentication and Authorization Architecture Plan](epic-2-auth-architecture-plan.md)
- [API Endpoints](api-endpoints.md) - REST API authentication endpoints
- [Requirements](requirements.md) - Overall system requirements
- [Database Schema](database-schema.md) - Identity table structure

---

## Changelog

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.1 | 2025-12-09 | Claude Code | Added tag helper documentation for Issue #65 |
| 1.0 | 2025-12-09 | docs-writer | Initial documentation for Issue #64 |

---

**Document Status:** Draft
**Review Required:** Yes
**Stakeholders:** Development Team, Security Team, Operations Team
