# Security Audit Report - Discord Bot Management System

**Audit Date:** 2026-01-14
**Audit Version:** 1.0
**Application Version:** v0.7.6-dev
**Auditor:** Security Specialist (Automated Audit)
**GitHub Issue:** #1010

---

## Executive Summary

This security audit examined the authentication and authorization controls of the Discord Bot Management System, focusing on API controllers, Razor Pages, SignalR hubs, Discord bot commands, and the underlying authorization infrastructure.

### Overall Risk Assessment: **LOW** *(Updated 2026-01-15)*

All identified security findings have been remediated. The application now has comprehensive authorization across all API controllers, Razor Pages, SignalR hubs, and Discord commands. The authorization infrastructure includes proper policies, role hierarchy, secure cookie configuration (SameSite=Strict), and separated health endpoints for load balancer compatibility.

### Key Findings Summary

| Severity | Count | Description |
|----------|-------|-------------|
| Critical | 1 | Bot restart/shutdown endpoints exposed without authorization |
| High | 2 | Guild data and command logs API endpoints missing authorization |
| Medium | 1 | Health endpoint exposes internal state without authentication |
| Low | 2 | Cookie SameSite policy and RequireGuildAccess policy inconsistency |
| Informational | 3 | Security best practices recommendations |

---

## Findings Table

| ID | Severity | Component | Finding | Status |
|----|----------|-----------|---------|--------|
| SEC-001 | Critical | BotController | Restart/Shutdown endpoints lack authorization | **Resolved** |
| SEC-002 | High | GuildsController | Entire controller lacks authorization attribute | **Resolved** |
| SEC-003 | High | CommandLogsController | Entire controller lacks authorization attribute | **Resolved** |
| SEC-004 | Medium | HealthController | No authorization on health endpoint | **Resolved** |
| SEC-005 | Low | IdentityServiceExtensions | Cookie SameSite set to Lax instead of Strict | **Resolved** |
| SEC-006 | Low | Razor Pages | RequireGuildAccess policy used inconsistently | **Resolved** |
| SEC-007 | Informational | Program.cs | Swagger UI enabled in all environments | **Resolved** |
| SEC-008 | Informational | appsettings.json | Discord token placeholder in config file | **Resolved** |
| SEC-009 | Informational | Portal Pages | AllowAnonymous with manual auth checks | **Documented** |

---

## Detailed Findings

### SEC-001: Bot Restart/Shutdown Endpoints Lack Authorization

**Severity:** Critical
**CWE:** CWE-862 (Missing Authorization)
**OWASP:** A01:2021 - Broken Access Control
**Location:** `src/DiscordBot.Bot/Controllers/BotController.cs` (Lines 192-232)

**Description:**
The `POST /api/bot/restart` and `POST /api/bot/shutdown` endpoints have no `[Authorize]` attribute, allowing any authenticated user (or potentially unauthenticated users depending on fallback policy behavior) to restart or shut down the bot service. These are destructive operations that should require SuperAdmin or Admin privileges.

**Vulnerable Code:**
```csharp
[HttpPost("restart")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> Restart(CancellationToken cancellationToken)
{
    _logger.LogWarning("Bot restart requested via API");
    // ... no authorization check
}

[HttpPost("shutdown")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
public async Task<IActionResult> Shutdown(CancellationToken cancellationToken)
{
    _logger.LogWarning("Bot shutdown requested via API");
    // ... no authorization check
}
```

**Impact:**
- Denial of Service: Any user can shut down the bot
- Service disruption for all guilds using the bot
- Potential for abuse by malicious actors

**Remediation:**
```csharp
[HttpPost("restart")]
[Authorize(Policy = "RequireAdmin")]  // Add this
[ProducesResponseType(StatusCodes.Status202Accepted)]
public async Task<IActionResult> Restart(...)

[HttpPost("shutdown")]
[Authorize(Policy = "RequireSuperAdmin")]  // Add this - shutdown should require highest privilege
[ProducesResponseType(StatusCodes.Status202Accepted)]
public async Task<IActionResult> Shutdown(...)
```

---

### SEC-002: GuildsController Missing Authorization

**Severity:** High
**CWE:** CWE-862 (Missing Authorization)
**OWASP:** A01:2021 - Broken Access Control
**Location:** `src/DiscordBot.Bot/Controllers/GuildsController.cs` (Lines 1-163)

**Description:**
The entire `GuildsController` lacks a class-level `[Authorize]` attribute. This exposes endpoints for reading, updating, and syncing guild data. The controller includes:
- `GET /api/guilds` - List all guilds
- `GET /api/guilds/{id}` - Get specific guild details
- `PUT /api/guilds/{id}` - Update guild settings
- `POST /api/guilds/{id}/sync` - Sync guild data from Discord

**Vulnerable Code:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class GuildsController : ControllerBase  // Missing [Authorize]
{
    // All endpoints exposed without authorization
}
```

**Impact:**
- Information disclosure: Guild names, member counts, settings
- Unauthorized modification of guild settings
- Potential IDOR if guild IDs are predictable

**Remediation:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]  // Add class-level authorization
public class GuildsController : ControllerBase
{
    // Consider adding [Authorize(Policy = "GuildAccess")] on individual endpoints
}
```

---

### SEC-003: CommandLogsController Missing Authorization

**Severity:** High
**CWE:** CWE-862 (Missing Authorization)
**OWASP:** A01:2021 - Broken Access Control
**Location:** `src/DiscordBot.Bot/Controllers/CommandLogsController.cs` (Lines 1-225)

**Description:**
The `CommandLogsController` has no authorization, exposing sensitive command execution history and analytics:
- `GET /api/commandlogs` - Paginated command execution logs
- `GET /api/commandlogs/stats` - Command usage statistics
- `GET /api/commandlogs/analytics` - Comprehensive analytics data
- `GET /api/commandlogs/analytics/usage-over-time` - Usage trends
- `GET /api/commandlogs/analytics/success-rate` - Success/failure rates
- `GET /api/commandlogs/analytics/performance` - Performance metrics

**Vulnerable Code:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class CommandLogsController : ControllerBase  // Missing [Authorize]
{
    // All analytics endpoints exposed
}
```

**Impact:**
- Information disclosure: User IDs, usernames, command usage patterns
- Privacy concerns: Tracking user activity without authorization
- Potential for reconnaissance by attackers

**Remediation:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireModerator")]  // Add class-level authorization
public class CommandLogsController : ControllerBase
```

---

### SEC-004: HealthController Exposes Internal State

**Severity:** Medium
**CWE:** CWE-200 (Exposure of Sensitive Information)
**OWASP:** A01:2021 - Broken Access Control
**Location:** `src/DiscordBot.Bot/Controllers/HealthController.cs` (Lines 1-67)

**Description:**
The health endpoint exposes internal application state including database connectivity status, version information, and timestamp without any authentication.

**Vulnerable Code:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase  // No authorization
{
    [HttpGet]
    public async Task<ActionResult<HealthResponseDto>> GetHealth()
    {
        // Returns database status, version, etc.
    }
}
```

**Impact:**
- Information disclosure: Application version, database health
- Reconnaissance opportunity for attackers
- Potentially acceptable for load balancer health checks

**Remediation:**
Consider implementing two endpoints:
```csharp
// Public health check for load balancers (minimal info)
[HttpGet("live")]
[AllowAnonymous]
public IActionResult GetLiveness() => Ok();

// Detailed health for authenticated admins
[HttpGet]
[Authorize(Policy = "RequireViewer")]
public async Task<ActionResult<HealthResponseDto>> GetHealth()
```

---

### SEC-005: Cookie SameSite Policy Set to Lax

**Severity:** Low
**CWE:** CWE-1275 (Sensitive Cookie with Improper SameSite Attribute)
**OWASP:** A01:2021 - Broken Access Control
**Location:** `src/DiscordBot.Bot/Extensions/IdentityServiceExtensions.cs` (Line 65)

**Description:**
The authentication cookie uses `SameSite=Lax` instead of `SameSite=Strict`. While Lax provides reasonable protection, Strict offers stronger CSRF protection.

**Current Configuration:**
```csharp
options.Cookie.SameSite = SameSiteMode.Lax;
```

**Impact:**
- Reduced CSRF protection compared to Strict mode
- Cookies sent on top-level navigations from external sites

**Remediation:**
```csharp
options.Cookie.SameSite = SameSiteMode.Strict;
```

**Note:** Test thoroughly as Strict may affect OAuth redirect flows.

---

### SEC-006: RequireGuildAccess Policy Used Inconsistently

**Severity:** Low
**CWE:** CWE-863 (Incorrect Authorization)
**OWASP:** A01:2021 - Broken Access Control
**Location:** `src/DiscordBot.Bot/Pages/Guilds/Reminders/Index.cshtml.cs` (Line 15)

**Description:**
Only one Razor Page uses `RequireGuildAccess` policy, while others rely on role-based policies without guild-specific validation. This creates potential for horizontal privilege escalation where authenticated users might access data from guilds they don't belong to.

**Current Pattern:**
```csharp
// Reminders uses guild access
[Authorize(Policy = "RequireGuildAccess")]
public class IndexModel : PageModel  // Reminders

// Other guild pages use role-based only
[Authorize(Policy = "RequireAdmin")]
public class WelcomeModel : PageModel  // No guild membership check
```

**Impact:**
- Users with Admin role can potentially access any guild's data
- Inconsistent security model across guild pages

**Remediation:**
Consider applying `GuildAccess` policy consistently to all guild-scoped pages, or implement guild validation in each page handler.

---

### SEC-007: Swagger UI Enabled in All Environments

**Severity:** Informational
**CWE:** CWE-489 (Active Debug Code)
**Location:** `src/DiscordBot.Bot/Program.cs` (Lines 266-271)

**Description:**
Swagger UI is enabled in all environments, including production. While protected by authentication, exposing API documentation increases attack surface.

**Current Code:**
```csharp
// Enable Swagger in all environments for now (can be restricted to Development later)
app.UseSwagger();
app.UseSwaggerUI(c => ...);
```

**Remediation:**
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => ...);
}
```

---

### SEC-008: Discord Token Placeholder in Configuration

**Severity:** Informational
**CWE:** CWE-312 (Cleartext Storage of Sensitive Information)
**Location:** `src/DiscordBot.Bot/appsettings.json` (Line 113)

**Description:**
The Discord token has an empty placeholder in appsettings.json. While the actual token should be in user secrets, the presence of this field could encourage developers to store secrets in version control.

**Current Configuration:**
```json
"Discord": {
    "Token": "",  // Empty placeholder
```

**Remediation:**
Remove the Token field entirely from appsettings.json and document that it must be configured via user secrets only.

---

### SEC-009: Portal Pages Use AllowAnonymous with Manual Auth Checks

**Severity:** Informational
**CWE:** CWE-285 (Improper Authorization)
**Location:**
- `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml.cs`
- `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml.cs`
- `src/DiscordBot.Bot/Pages/Guilds/PublicLeaderboard.cshtml.cs`

**Description:**
Portal pages use `[AllowAnonymous]` attribute and implement manual authorization checks in the page handler. While this pattern works correctly (showing landing pages for unauthenticated users), it requires careful implementation to avoid authorization bypasses.

**Current Pattern:**
```csharp
[AllowAnonymous]
public class IndexModel : PortalPageModelBase
{
    public async Task<IActionResult> OnGetAsync(ulong guildId, ...)
    {
        // Manual auth check via CheckPortalAuthorizationAsync
    }
}
```

**Impact:**
- Manual auth checks are more error-prone than declarative attributes
- Requires consistent implementation across all portal pages

**Recommendation:**
The current implementation appears correct but should be reviewed whenever portal pages are modified. Consider documenting this pattern for future developers.

---

## OWASP Top 10 2021 Compliance Checklist

| Category | Status | Notes |
|----------|--------|-------|
| A01: Broken Access Control | **PASS** | All controllers now have proper authorization |
| A02: Cryptographic Failures | PASS | HTTPS enforced, secure cookie flags enabled |
| A03: Injection | PASS | Entity Framework with parameterized queries |
| A04: Insecure Design | PASS | Defense in depth with multiple authorization layers |
| A05: Security Misconfiguration | **PASS** | Swagger restricted to dev, secrets removed from config |
| A06: Vulnerable Components | NOT TESTED | Dependency scanning not performed |
| A07: Auth Failures | PASS | Strong password policies, account lockout enabled |
| A08: Software & Data Integrity | NOT TESTED | SBOM not reviewed |
| A09: Security Logging | PASS | Comprehensive Serilog logging with structured data |
| A10: SSRF | NOT TESTED | External URL handling not audited |

---

## Positive Findings

The following security controls are properly implemented:

### Authentication Infrastructure
- ASP.NET Core Identity with strong password requirements (8+ chars, mixed case, digits, special chars)
- Account lockout after 5 failed attempts (15-minute lockout)
- Secure cookie configuration (HttpOnly, Secure, SameSite)
- Discord OAuth with proper scope limiting (identify, email, guilds)
- Token refresh background service for OAuth tokens

### Authorization Framework
- Hierarchical role system: SuperAdmin > Admin > Moderator > Viewer
- Fallback policy requiring authentication by default
- Guild-specific authorization handler with membership validation
- Portal guild member authorization for public-facing features
- SuperAdmin bypass for guild-specific checks

### Secured Components
| Component | Authorization |
|-----------|--------------|
| DashboardHub (SignalR) | RequireViewer |
| BulkPurgeController | RequireSuperAdmin |
| MessagesController | RequireAdmin |
| WelcomeController | RequireAdmin |
| ScheduledMessagesController | RequireAdmin |
| AuditLogsController | RequireSuperAdmin |
| All Admin Razor Pages | Appropriate role policies |
| Discord Commands | Precondition attributes |

### Discord Bot Command Security
- Admin commands require `[RequireAdmin]` precondition
- Moderation commands require `[RequireModerator]`
- Permission-specific commands (ban, kick) require Discord permissions
- Feature flags (RatWatch, Audio, TTS) require feature-enabled checks

---

## Recommendations Summary

### Immediate Actions (Critical/High)

1. **SEC-001:** Add `[Authorize(Policy = "RequireSuperAdmin")]` to `BotController.Shutdown()` and `[Authorize(Policy = "RequireAdmin")]` to `BotController.Restart()`

2. **SEC-002:** Add `[Authorize(Policy = "RequireAdmin")]` class-level attribute to `GuildsController`

3. **SEC-003:** Add `[Authorize(Policy = "RequireModerator")]` class-level attribute to `CommandLogsController`

### Short-Term Actions (Medium)

4. **SEC-004:** Implement separate public liveness probe and authenticated health endpoint

5. **API Documentation:** Update API endpoint documentation to reflect actual authentication requirements (contradicts "Authentication: None (MVP)" statement)

### Long-Term Actions (Low/Informational)

6. **SEC-005:** Evaluate changing SameSite to Strict after testing OAuth flows

7. **SEC-006:** Implement consistent guild access validation across all guild-scoped pages

8. **SEC-007:** Restrict Swagger UI to development environment only

9. **SEC-008:** Remove token placeholder from appsettings.json

10. **Dependency Scanning:** Implement automated vulnerability scanning for NuGet packages

11. **Security Headers:** Consider adding security headers middleware (CSP, X-Frame-Options, etc.)

---

## Appendix A: Files Audited

### API Controllers (26 total)
| Controller | Authorization | Status |
|------------|--------------|--------|
| AlertsController | Mixed per-endpoint | OK |
| AnalyticsController | RequireViewer | OK |
| AudioController | RequireViewer | OK |
| AuditLogsController | RequireSuperAdmin | OK |
| AutocompleteController | RequireViewer | OK |
| BotController | RequireAdmin (restart), RequireSuperAdmin (shutdown) | OK |
| BulkPurgeController | RequireSuperAdmin | OK |
| CommandLogsController | RequireModerator | OK |
| FlaggedEventsController | RequireAdmin | OK |
| GuildMembersController | RequireAdmin | OK |
| GuildsController | RequireAdmin | OK |
| HealthController | RequireViewer (detailed), AllowAnonymous (live/ready) | OK |
| MessagesController | RequireAdmin | OK |
| ModerationCasesController | RequireAdmin | OK |
| ModerationConfigController | RequireAdmin | OK |
| ModTagsController | RequireAdmin | OK |
| PerformanceMetricsController | RequireViewer | OK |
| PerformanceTabsController | RequireViewer | OK |
| PortalSoundboardController | PortalGuildMember | OK |
| PortalTtsController | PortalGuildMember | OK |
| PreviewController | RequireViewer | OK |
| ScheduledMessagesController | RequireAdmin | OK |
| SoundsController | RequireViewer | OK |
| UserModerationController | RequireAdmin | OK |
| WatchlistController | RequireAdmin | OK |
| WelcomeController | RequireAdmin | OK |

### Razor Pages (62 total)
All pages have appropriate `[Authorize]` or `[AllowAnonymous]` attributes. See grep results for complete list.

### SignalR Hubs (1 total)
| Hub | Authorization | Status |
|-----|--------------|--------|
| DashboardHub | RequireViewer | OK |

### Discord Command Modules
All command modules have appropriate precondition attributes where required.

---

## Appendix B: Authorization Policies

Defined in `src/DiscordBot.Bot/Extensions/IdentityServiceExtensions.cs`:

```csharp
// Hierarchical role policies
RequireSuperAdmin   -> SuperAdmin only
RequireAdmin        -> SuperAdmin, Admin
RequireModerator    -> SuperAdmin, Admin, Moderator
RequireViewer       -> SuperAdmin, Admin, Moderator, Viewer

// Special policies
GuildAccess         -> Requires guild membership verification
PortalGuildMember   -> Requires Discord OAuth and guild membership (no role check)

// Fallback
FallbackPolicy      -> RequireAuthenticatedUser (applies to all pages by default)
```

---

*Report generated by Security Audit Tool v1.0*
