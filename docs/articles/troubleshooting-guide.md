# Troubleshooting Guide

**Last Updated:** 2025-12-30
**Epic Reference:** [Epic #303: Documentation Overhaul for v0.4.0](https://github.com/cpike5/discordbot/issues/303)
**Related Issues:** #316

---

## Overview

This guide provides comprehensive troubleshooting procedures for common issues encountered when running the Discord Bot Management System. It covers bot connection problems, command registration issues, authentication failures, database errors, admin UI problems, and diagnostic procedures.

### Quick Diagnostics Checklist

Before diving into specific sections, run through this quick checklist:

- [ ] Bot token is configured via user secrets (`dotnet user-secrets list`)
- [ ] Discord Developer Portal has gateway intents enabled
- [ ] Database migrations have been applied (`dotnet ef database update`)
- [ ] .NET 8 SDK is installed (`dotnet --version`)
- [ ] Node.js is installed for Tailwind CSS build (`node --version`)
- [ ] Application builds successfully (`dotnet build`)
- [ ] Logs directory exists and is writable
- [ ] User has proper roles assigned (for admin UI access)

### Log Locations

All logs are written to these locations for troubleshooting:

| Log Type | Location | Purpose |
|----------|----------|---------|
| File Logs | `logs/discordbot-YYYY-MM-DD.log` | Rolling daily file logs |
| Console Output | Terminal/stdout | Real-time application logs |
| Seq Logs | `http://localhost:5341` (if configured) | Centralized structured logs |
| Windows Event Log | Event Viewer → Application | Windows service logs (systemd only) |

**Log Levels:**
- **Development:** Debug and above (verbose logging)
- **Production:** Warning and above (errors only)

---

## Bot Connection Issues

### Bot Doesn't Connect

**Symptoms:**
- Bot appears offline in Discord server
- Console shows "Failed to connect" or authentication errors
- Admin dashboard shows "Disconnected" status

**Solutions:**

#### 1. Verify Bot Token Configuration

```bash
cd src/DiscordBot.Bot
dotnet user-secrets list
```

Expected output should include:
```
Discord:Token = <your-bot-token-here>
```

If missing or incorrect:
```bash
dotnet user-secrets set "Discord:Token" "your-actual-bot-token"
```

**Important:** Tokens are long strings (59+ characters). Ensure you copied the entire token without truncation or extra spaces.

#### 2. Check Bot Token Validity

1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Select your application
3. Navigate to **Bot** section
4. If token is compromised, click **Reset Token**
5. Copy new token immediately (shown only once)
6. Update user secrets with new token

#### 3. Verify Gateway Intents

The application requires specific gateway intents enabled in the Discord Developer Portal:

**Required Intents:**
- **Server Members Intent** - Required for member join/leave events (welcome messages, member tracking)
- **Message Content Intent** - Required for message logging feature

**To Enable:**
1. Discord Developer Portal → Your Application → Bot
2. Scroll to **Privileged Gateway Intents**
3. Toggle ON the required intents
4. Click **Save Changes**
5. Restart the bot application

**Error Message if Missing:**
```
Discord.WebSocketClosedException: WebSocket connection was closed
CloseStatus: 4014
CloseDescription: Disallowed intent(s)
```

#### 4. Check Network Connectivity

Ensure the server can reach Discord's gateway:

```bash
# Test Discord gateway connectivity (Windows PowerShell)
Test-NetConnection gateway.discord.gg -Port 443

# Linux/macOS
curl -I https://gateway.discord.gg

# Check Discord API status
curl https://discordstatus.com
```

If connectivity fails:
- Check firewall rules (allow outbound HTTPS on port 443)
- Verify proxy settings if behind corporate network
- Check DNS resolution for `gateway.discord.gg`

#### 5. Review Application Logs

Check logs for specific connection errors:

```bash
# View today's log file
cat logs/discordbot-$(date +%Y-%m-%d).log

# Windows PowerShell
Get-Content logs/discordbot-$(Get-Date -Format yyyy-MM-dd).log -Tail 50

# Filter for errors only
grep "ERROR" logs/discordbot-*.log
```

Look for error messages indicating:
- Token authentication failures
- Gateway intent errors
- Network connection timeouts
- SSL/TLS certificate issues

---

### Bot Disconnects Frequently

**Symptoms:**
- Bot repeatedly connects and disconnects
- Gateway latency spikes before disconnections
- "Reconnecting" messages in console logs

**Solutions:**

#### 1. Check Network Stability

```bash
# Monitor gateway latency (Windows)
ping -t gateway.discord.gg

# Monitor gateway latency (Linux/macOS)
ping gateway.discord.gg

# Check for packet loss (should be 0%)
```

High latency (>300ms) or packet loss indicates network issues.

#### 2. Verify Server Resources

Bot disconnections can be caused by resource exhaustion:

```bash
# Check memory usage (Linux)
free -h

# Check CPU usage
top

# Windows Task Manager
taskmgr
```

**Resource Requirements:**
- Minimum RAM: 512MB
- Recommended RAM: 1GB+
- CPU: 1 core minimum

#### 3. Check Discord API Status

Discord gateway outages cause disconnections:

1. Visit https://discordstatus.com
2. Check for "API" or "Gateway" incidents
3. If Discord reports issues, wait for resolution

#### 4. Review Disconnect Logs

```bash
# Search for disconnect events
grep "Disconnected" logs/discordbot-*.log

# Check for rate limit errors
grep "429" logs/discordbot-*.log
```

**Common Disconnect Reasons:**
- Gateway reconnection timeouts
- Discord server restarts (normal, bot auto-reconnects)
- Rate limit violations (429 errors)
- Invalid session (bot auto-reconnects)

#### 5. Increase Gateway Timeout (Advanced)

If experiencing frequent timeout disconnections, adjust timeout in `DiscordServiceExtensions.cs`:

```csharp
// Current default is 30 seconds
config.DefaultTimeout = 60000; // Increase to 60 seconds
```

**Warning:** Only increase timeout if network latency is consistently high.

---

### Gateway Intents Errors

**Symptoms:**
- Error message: "Disallowed intent(s)"
- WebSocket close code 4014
- Bot immediately disconnects after connecting

**Solution:**

1. **Identify Required Intents:**
   - Server Members Intent (member events, welcome messages)
   - Message Content Intent (message logging)

2. **Enable in Developer Portal:**
   - Go to https://discord.com/developers/applications
   - Select your application
   - Bot → Privileged Gateway Intents
   - Enable required intents
   - Save Changes

3. **Verify Code Configuration:**

   Check `DiscordServiceExtensions.cs` for configured intents:
   ```csharp
   config.GatewayIntents =
       GatewayIntents.Guilds |
       GatewayIntents.GuildMembers |
       GatewayIntents.GuildMessages |
       GatewayIntents.MessageContent;
   ```

4. **Restart Application:**
   ```bash
   dotnet run --project src/DiscordBot.Bot
   ```

**Note:** Intents must be enabled in BOTH the Developer Portal AND the code. Missing either causes connection failure.

---

## Command Issues

### Commands Not Appearing

**Symptoms:**
- Slash commands don't appear in Discord when typing `/`
- Command list is empty in admin UI
- No autocomplete suggestions in Discord

**Solutions:**

#### 1. Understand Command Registration Delay

**Global Commands:** Take up to 1 hour to propagate across Discord
**Guild Commands:** Register instantly (recommended for development)

#### 2. Use Test Guild ID for Instant Registration

```bash
# Enable developer mode in Discord
# Right-click your server → Copy Server ID

cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:TestGuildId" "123456789012345678"
```

**After setting TestGuildId:**
- Commands register instantly in that guild
- No 1-hour propagation delay
- Ideal for development/testing

#### 3. Verify applications.commands Scope

The bot must be invited with the `applications.commands` OAuth scope:

1. Discord Developer Portal → OAuth2 → URL Generator
2. Select scopes:
   - ✓ `bot`
   - ✓ `applications.commands`
3. Generate new invite URL
4. Re-invite bot to server

**Check if scope is present:**
- Right-click bot in member list
- If "Add to Server" option exists, scope is missing

#### 4. Check Command Registration Logs

```bash
# View command registration output
dotnet run --project src/DiscordBot.Bot | grep "Registered"
```

Expected output:
```
[INFO] Registered 12 slash commands globally
[INFO] Command 'ping' registered successfully
```

#### 5. Manually Trigger Re-registration

If commands are stale or not updating:

```bash
# Delete user secrets for TestGuildId to force global registration
dotnet user-secrets remove "Discord:TestGuildId"

# Restart application
dotnet run --project src/DiscordBot.Bot
```

**Warning:** This triggers global registration with 1-hour delay.

#### 6. Check for Command Errors in Logs

```bash
# Search for command registration errors
grep "command" logs/discordbot-*.log -i | grep -i "error"
```

Common errors:
- Duplicate command names
- Invalid parameter types
- Missing command descriptions
- Circular dependencies in modules

---

### Commands Not Registering

**Symptoms:**
- Application starts but commands don't register
- "Failed to register commands" error in logs
- Commands list is empty in admin UI

**Solutions:**

#### 1. Check InteractionHandler Initialization

Review logs for InteractionHandler initialization:

```bash
grep "InteractionHandler" logs/discordbot-*.log
```

Expected output:
```
[INFO] InteractionHandler initialized
[INFO] Discovered 5 command modules
```

#### 2. Verify Command Module Discovery

Command modules must:
- Inherit from `InteractionModuleBase<SocketInteractionContext>`
- Be in the `DiscordBot.Bot.Commands` namespace
- Have `[SlashCommand]` attributes

**Check assembly scanning:**
```csharp
// InteractionHandler.cs
await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
```

#### 3. Check for Compilation Errors

```bash
dotnet build src/DiscordBot.Bot
```

Look for warnings about command modules or attributes.

#### 4. Review Discord API Permissions

Bot must have these permissions in Discord:
- `applications.commands` OAuth scope
- Server-level permission to create commands

**Verify in Discord:**
- Server Settings → Integrations
- Find your bot
- Ensure "Use Application Commands" is enabled

---

### Permission Denied Errors

**Symptoms:**
- "You do not have permission to use this command"
- Precondition attribute fails
- Command executes but returns permission error

**Solutions:**

#### 1. Check Command Preconditions

Commands may have preconditions that restrict access:

| Precondition | Required Permission |
|--------------|---------------------|
| `RequireAdminAttribute` | User must be guild admin or owner |
| `RequireOwnerAttribute` | User must be bot owner (configured in appsettings) |
| `RequireRatWatchEnabledAttribute` | Guild must have Rat Watch enabled |
| `RateLimitAttribute` | User must not exceed rate limit |

#### 2. Verify Discord Server Permissions

User must have these Discord permissions:
- Administrator (for admin commands)
- Manage Server (for configuration commands)

**Check in Discord:**
- Right-click user → Profile
- Review Roles section
- Ensure role has required permissions

#### 3. Check Bot Role Hierarchy

Bot cannot manage roles above its own role:

1. Server Settings → Roles
2. Ensure bot's role is high enough in hierarchy
3. Move bot role above roles it needs to manage

#### 4. Review Precondition Logs

```bash
grep "Precondition" logs/discordbot-*.log
```

Look for specific precondition failures and reasons.

#### 5. Test with Bot Owner

Configure bot owner in user secrets:

```bash
dotnet user-secrets set "Discord:OwnerId" "your-discord-user-id"
```

Bot owner bypasses most preconditions for testing.

---

### Rate Limiting Issues

**Symptoms:**
- Commands return "You are being rate limited"
- HTTP 429 errors in logs
- Commands work after waiting

**Solutions:**

#### 1. Understand Rate Limits

Discord enforces rate limits on API calls:
- Global rate limit: 50 requests per second
- Per-route rate limits: Varies by endpoint
- Command rate limit: Configured per-command via `RateLimitAttribute`

#### 2. Check Application Rate Limit Configuration

Default rate limits in `RateLimitAttribute`:
```csharp
[RateLimit(3, 60)] // 3 uses per 60 seconds per user
```

#### 3. Wait for Rate Limit Reset

Rate limits reset after the configured time window:
- Check `Retry-After` header in logs
- Wait specified duration before retrying

#### 4. Implement Retry Logic (Advanced)

For persistent rate limit issues, implement exponential backoff in API calls.

#### 5. Monitor Rate Limit Usage

```bash
# Check for 429 errors in logs
grep "429" logs/discordbot-*.log | wc -l
```

High count indicates excessive API usage. Review command usage patterns.

---

## Authentication Issues

### OAuth Redirect Failures

**Symptoms:**
- "Invalid redirect_uri" error during Discord OAuth
- Redirect after login fails
- User stuck on Discord authorization page

**Solutions:**

#### 1. Verify Redirect URI Configuration

Redirect URI must match EXACTLY in both places:

**Discord Developer Portal:**
```
https://localhost:5001/signin-discord
```

**Application Configuration:**
```csharp
// Configured automatically by ASP.NET Discord provider
// Default callback path: /signin-discord
```

**Common Mistakes:**
- Trailing slash: `https://localhost:5001/signin-discord/` (WRONG)
- Wrong protocol: `http://localhost:5001/signin-discord` (WRONG - must be HTTPS)
- Wrong port: `https://localhost:5000/signin-discord` (check your port)
- Case sensitivity: URLs are case-sensitive

#### 2. Configure All Environment Redirect URIs

Add redirect URIs for all environments:

**Development:**
```
https://localhost:5001/signin-discord
```

**Staging:**
```
https://staging.yourdomain.com/signin-discord
```

**Production:**
```
https://yourdomain.com/signin-discord
```

#### 3. Verify Client ID and Secret

```bash
cd src/DiscordBot.Bot
dotnet user-secrets list
```

Should show:
```
Discord:OAuth:ClientId = 123456789012345678
Discord:OAuth:ClientSecret = abcdef1234567890
```

If missing:
```bash
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
```

#### 4. Check Redirect URI in Logs

```bash
grep "redirect_uri" logs/discordbot-*.log
```

Compare logged URI with Developer Portal configuration.

#### 5. Clear Browser Cookies

Stale cookies can cause redirect issues:
- Clear all cookies for `localhost` or your domain
- Close browser completely
- Restart browser and try again

---

### Discord Login Not Working

**Symptoms:**
- "Login with Discord" button does nothing
- Redirect to Discord fails
- Error after clicking "Authorize" on Discord

**Solutions:**

#### 1. Verify OAuth Configuration is Present

Check that Discord OAuth is configured:

```bash
dotnet user-secrets list | grep OAuth
```

If missing, set up OAuth credentials:
```bash
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
```

#### 2. Check OAuth Provider Registration

Verify `Program.cs` has Discord OAuth configured:

```csharp
builder.Services.AddAuthentication()
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:OAuth:ClientId"];
        options.ClientSecret = builder.Configuration["Discord:OAuth:ClientSecret"];
        options.Scope.Add("identify");
        options.Scope.Add("email");
        options.Scope.Add("guilds");
        options.SaveTokens = true;
    });
```

#### 3. Verify Redirect URI is Correct

See [OAuth Redirect Failures](#oauth-redirect-failures) section above.

#### 4. Check Browser Console for Errors

1. Open browser developer tools (F12)
2. Go to Console tab
3. Click "Login with Discord"
4. Look for JavaScript errors or failed requests

Common errors:
- CORS errors (check redirect URI)
- Network errors (check Discord API status)
- Mixed content warnings (ensure HTTPS)

#### 5. Test OAuth Flow Manually

1. Navigate to Discord Developer Portal
2. OAuth2 → URL Generator
3. Select scopes: `identify`, `email`, `guilds`
4. Use generated URL to test OAuth flow
5. If manual test works, issue is in application configuration

---

### Token Refresh Failures

**Symptoms:**
- User logged out unexpectedly
- "Invalid token" errors after some time
- Session expires prematurely

**Solutions:**

#### 1. Check Cookie Expiration Settings

Review cookie configuration in `Program.cs`:

```csharp
options.ExpireTimeSpan = TimeSpan.FromDays(1); // Cookie lifetime
options.SlidingExpiration = true; // Renew on activity
```

**Sliding Expiration:**
- Cookie renews on each request
- User stays logged in as long as active
- Inactivity for `ExpireTimeSpan` duration logs out

#### 2. Verify Token Storage

OAuth tokens are stored in authentication cookie with `options.SaveTokens = true`.

Check if tokens are being saved:
```bash
grep "SaveTokens" src/DiscordBot.Bot/Program.cs
```

#### 3. Check for Clock Skew

Server and client clocks must be synchronized:

```bash
# Check system time (Linux)
timedatectl

# Check system time (Windows)
w32tm /query /status

# Sync time if needed
ntpdate pool.ntp.org  # Linux
w32tm /resync         # Windows
```

Clock skew >5 minutes causes token validation failures.

#### 4. Review Session Logs

```bash
grep "session" logs/discordbot-*.log -i
```

Look for session expiration or token refresh errors.

---

### Session Issues

**Symptoms:**
- User must log in repeatedly
- Session doesn't persist across browser restarts
- "Remember Me" doesn't work

**Solutions:**

#### 1. Check "Remember Me" Implementation

Verify login page sets persistent cookie when "Remember Me" is checked:

```csharp
var result = await _signInManager.PasswordSignInAsync(
    Input.Email,
    Input.Password,
    isPersistent: Input.RememberMe,  // This must be set
    lockoutOnFailure: true
);
```

#### 2. Verify Cookie Persistence Settings

```csharp
// In Program.cs
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax; // Must be Lax or None for cross-site
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Persistent cookie lifetime
    options.SlidingExpiration = true;
});
```

#### 3. Check Browser Cookie Settings

Browser must allow cookies:
- Enable cookies in browser settings
- Allow third-party cookies (if admin UI is on different domain)
- Whitelist your domain

#### 4. Verify HTTPS is Enforced

Cookies with `SecurePolicy = Always` only work over HTTPS:

```bash
# Check if app is running on HTTPS
curl -I https://localhost:5001
```

For production, ensure HTTPS is enforced:
```csharp
app.UseHttpsRedirection();
```

#### 5. Clear and Test

1. Clear all browser cookies
2. Restart browser
3. Log in with "Remember Me" checked
4. Close browser completely
5. Reopen browser and check if still logged in

---

## Database Issues

### Migration Failures

**Symptoms:**
- "Pending migrations" error on startup
- Database schema mismatch errors
- Entity Framework exceptions

**Solutions:**

#### 1. List Pending Migrations

```bash
dotnet ef migrations list --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

Output shows applied and pending migrations.

#### 2. Apply Pending Migrations

```bash
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

Expected output:
```
Applying migration '20250101000000_InitialCreate'.
Done.
```

#### 3. Reset Database (Development Only)

**WARNING:** This deletes all data.

```bash
# Drop database
dotnet ef database drop --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --force

# Recreate and apply migrations
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

#### 4. Check for Migration Conflicts

```bash
# View migration history
dotnet ef migrations list --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Check for duplicate or conflicting migrations
```

If multiple developers created migrations simultaneously, conflicts may exist.

#### 5. Review Migration Errors

```bash
grep "migration" logs/discordbot-*.log -i
```

Common errors:
- Column already exists
- Foreign key constraint violations
- Invalid column types
- Missing tables

#### 6. Manual Migration Repair (Advanced)

If migrations are corrupted:

```bash
# Remove last migration (if not applied)
dotnet ef migrations remove --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Recreate migration
dotnet ef migrations add FixedMigration --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Apply
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

---

### Connection Errors

**Symptoms:**
- "Unable to connect to database"
- Timeout errors during database operations
- SqlException or database provider errors

**Solutions:**

#### 1. Verify Connection String

**SQLite (Development):**

Connection string in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=discordbot.db"
  }
}
```

Check if database file exists:
```bash
ls -l discordbot.db  # Linux/macOS
dir discordbot.db    # Windows
```

**MySQL/PostgreSQL (Production):**

```bash
# Check connection string format
dotnet user-secrets list | grep ConnectionStrings
```

Example MySQL connection string:
```
Server=localhost;Database=discordbot;User=botuser;Password=yourpassword;
```

#### 2. Test Database Connectivity

**SQLite:**
```bash
# Check if file is readable/writable
touch discordbot.db
chmod 644 discordbot.db
```

**MySQL:**
```bash
mysql -h localhost -u botuser -p discordbot
```

**PostgreSQL:**
```bash
psql -h localhost -U botuser -d discordbot
```

If connection fails, database server may be down or credentials incorrect.

#### 3. Check Database Service Status

**MySQL:**
```bash
# Linux
systemctl status mysql

# Windows
Get-Service MySQL
```

**PostgreSQL:**
```bash
# Linux
systemctl status postgresql

# Windows
Get-Service PostgreSQL
```

Start service if stopped:
```bash
systemctl start mysql    # Linux
Start-Service MySQL      # Windows
```

#### 4. Verify Firewall Rules

Database server must accept connections on default ports:
- MySQL: 3306
- PostgreSQL: 5432

```bash
# Test port connectivity
telnet localhost 3306  # MySQL
telnet localhost 5432  # PostgreSQL

# Or using nc
nc -zv localhost 3306
```

#### 5. Review Database Logs

Check database server logs for connection rejections or authentication failures.

**MySQL:**
```bash
sudo tail -f /var/log/mysql/error.log
```

**PostgreSQL:**
```bash
sudo tail -f /var/log/postgresql/postgresql-*.log
```

---

### Performance Issues

**Symptoms:**
- Slow page load times
- Database queries taking several seconds
- High CPU usage
- Application becomes unresponsive

**Solutions:**

#### 1. Enable Query Logging

Add to `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

This logs all SQL queries with execution times.

#### 2. Identify Slow Queries

```bash
# Search for slow queries (>1000ms)
grep "Executed DbCommand" logs/discordbot-*.log | grep "ms]" | awk -F'[' '{print $NF}' | sort -n
```

#### 3. Add Missing Indexes

Check for queries without indexes:
- Full table scans on large tables
- Queries filtering on non-indexed columns

Add indexes in `BotDbContext.cs`:
```csharp
modelBuilder.Entity<Guild>()
    .HasIndex(g => g.GuildId)
    .IsUnique();
```

Create migration and apply:
```bash
dotnet ef migrations add AddIndexes --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

#### 4. Optimize N+1 Query Problems

Use eager loading to avoid N+1 queries:

```csharp
// BAD: N+1 queries
var guilds = await _context.Guilds.ToListAsync();
foreach (var guild in guilds)
{
    var settings = await _context.GuildSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
}

// GOOD: Single query with join
var guilds = await _context.Guilds
    .Include(g => g.Settings)
    .ToListAsync();
```

#### 5. Implement Caching

Enable response caching for frequently accessed data:

```csharp
[ResponseCache(Duration = 60)] // Cache for 60 seconds
public async Task<IActionResult> GetGuilds()
{
    // ...
}
```

Or use memory cache:
```csharp
var guilds = await _cache.GetOrCreateAsync("all-guilds", async entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
    return await _context.Guilds.ToListAsync();
});
```

#### 6. Database Maintenance (Production)

**SQLite:**
```sql
VACUUM;  -- Reclaim space and optimize
ANALYZE; -- Update query planner statistics
```

**MySQL:**
```sql
OPTIMIZE TABLE Guilds;
ANALYZE TABLE Guilds;
```

**PostgreSQL:**
```sql
VACUUM ANALYZE;
```

---

## Admin UI Issues

### Page Not Loading

**Symptoms:**
- Blank page or loading spinner indefinitely
- HTTP 500 errors
- Page shows error message instead of content

**Solutions:**

#### 1. Check Browser Console

1. Open developer tools (F12)
2. Go to Console tab
3. Look for JavaScript errors

Common errors:
- `Uncaught ReferenceError` - Missing JavaScript dependency
- `404 Not Found` - Missing static file (CSS/JS)
- `Failed to fetch` - API endpoint not responding

#### 2. Verify Static Files Are Served

Check that Tailwind CSS and JavaScript files are built:

```bash
# Rebuild Tailwind CSS
cd src/DiscordBot.Bot
npm run build:css

# Verify output file exists
ls -l wwwroot/css/site.css
```

#### 3. Check for Server-Side Errors

View application logs:

```bash
tail -f logs/discordbot-$(date +%Y-%m-%d).log
```

Look for:
- NullReferenceException
- Database connection errors
- Authorization failures

#### 4. Test Page Model Directly

```bash
# Check if razor page compiles
dotnet build src/DiscordBot.Bot
```

Look for compilation errors in `.cshtml` or `.cshtml.cs` files.

#### 5. Verify Authentication

If page requires authentication:
1. Ensure user is logged in
2. Check user has required role
3. Review authorization policy for page

Test with admin account to rule out authorization issues.

---

### Real-Time Updates Not Working (SignalR)

**Symptoms:**
- Bot status doesn't update in dashboard
- Guild updates don't appear automatically
- Connection indicator shows "disconnected"

**Solutions:**

#### 1. Check SignalR Connection

Open browser console and look for:
```
Connected to dashboard hub
Connection ID: abc123...
```

If missing, connection failed.

#### 2. Verify WebSocket Support

SignalR prefers WebSockets but falls back to other transports:

**Check negotiation:**
```bash
# Browser Network tab
# Look for /hubs/dashboard/negotiate request
# Should return JSON with available transports
```

#### 3. Test SignalR Hub Endpoint

```bash
# Test hub endpoint is accessible
curl -I https://localhost:5001/hubs/dashboard
```

Should return 101 Switching Protocols or 200 OK.

#### 4. Check for CORS Issues

If admin UI and API are on different domains:

Verify CORS is configured in `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminUI",
        builder => builder
            .WithOrigins("https://youradminui.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
```

#### 5. Review SignalR Logs

```bash
grep "SignalR" logs/discordbot-*.log
grep "DashboardHub" logs/discordbot-*.log
```

Look for:
- Connection failures
- Authentication failures
- Broadcast errors

#### 6. Verify Authentication Cookie

SignalR requires authentication cookie:
- User must be logged in
- Cookie must be sent with WebSocket upgrade request

Check browser developer tools → Application → Cookies.

#### 7. Test with Different Transport

Force Long Polling instead of WebSockets (for debugging):

```javascript
// In dashboard-hub.js
connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/dashboard', {
        transport: signalR.HttpTransportType.LongPolling
    })
    .build();
```

If Long Polling works but WebSockets don't, issue is with WebSocket support (firewall, proxy, etc.).

---

### Permission/Authorization Errors

**Symptoms:**
- "Access Denied" page when accessing admin pages
- HTTP 403 Forbidden errors
- Pages require login despite being logged in

**Solutions:**

#### 1. Verify User Roles

Check user's assigned roles:

```sql
-- Query database for user roles
SELECT u.Email, r.Name as Role
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE u.Email = 'user@example.com';
```

#### 2. Check Authorization Policy

Review authorization policy for the page:

```csharp
[Authorize(Policy = "RequireAdmin")]  // Page requires Admin role or higher
public class EditGuildModel : PageModel
```

**Policy Hierarchy:**
- `RequireSuperAdmin` - SuperAdmin only
- `RequireAdmin` - SuperAdmin or Admin
- `RequireModerator` - SuperAdmin, Admin, or Moderator
- `RequireViewer` - All roles (SuperAdmin, Admin, Moderator, Viewer)

#### 3. Assign Appropriate Role

Use admin UI (if you have SuperAdmin access) or database:

```sql
-- Add user to Admin role
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM AspNetUsers u, AspNetRoles r
WHERE u.Email = 'user@example.com' AND r.Name = 'Admin';
```

#### 4. Check Guild-Specific Access

Some pages require guild membership validation:

```csharp
[Authorize(Policy = "GuildAccess")]  // User must be member of guild
```

Verify user is a member of the Discord guild.

#### 5. Review Authorization Logs

```bash
grep "Authorization" logs/discordbot-*.log
grep "Access denied" logs/discordbot-*.log
```

Look for:
- Policy evaluation failures
- Missing roles
- Guild access validation failures

See [Authorization Policies](authorization-policies.md) for complete role documentation.

---

## Logging & Diagnostics

### How to Enable Verbose Logging

**Development Environment:**

Verbose logging is enabled by default in `appsettings.Development.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

**Production Environment:**

To temporarily enable verbose logging in production:

1. Edit `appsettings.Production.json`:
   ```json
   {
     "Serilog": {
       "MinimumLevel": {
         "Default": "Debug"
       }
     }
   }
   ```

2. Restart application

3. **IMPORTANT:** Revert to Warning level after debugging (Debug logs are very verbose)

**Enable Specific Namespace Logging:**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "DiscordBot.Bot.Handlers": "Debug",
        "DiscordBot.Bot.Commands": "Debug"
      }
    }
  }
}
```

---

### Reading Log Files

**Log File Location:**
```bash
logs/discordbot-YYYY-MM-DD.log
```

**View Today's Logs:**
```bash
# Linux/macOS
tail -f logs/discordbot-$(date +%Y-%m-%d).log

# Windows PowerShell
Get-Content logs/discordbot-$(Get-Date -Format yyyy-MM-dd).log -Tail 50 -Wait
```

**Filter by Log Level:**
```bash
# Errors only
grep "ERROR" logs/discordbot-*.log

# Warnings and errors
grep -E "WARN|ERROR" logs/discordbot-*.log

# Specific keyword
grep "Discord" logs/discordbot-*.log
```

**View Logs from Specific Date:**
```bash
cat logs/discordbot-2025-12-30.log
```

**Search for Exception Stack Traces:**
```bash
grep -A 10 "Exception" logs/discordbot-*.log
```

**Count Error Frequency:**
```bash
grep "ERROR" logs/discordbot-*.log | wc -l
```

---

### Using Seq for Log Analysis

Seq provides powerful structured log querying. See [Log Aggregation](log-aggregation.md) for full setup.

#### Starting Seq Locally

```bash
docker run -d \
  --name seq \
  -p 5341:80 \
  -e ACCEPT_EULA=Y \
  -v seq-data:/data \
  datalust/seq:latest
```

Access Seq UI at: `http://localhost:5341`

#### Common Seq Queries

**All Errors:**
```
@Level = 'Error'
```

**Errors for Specific Guild:**
```
GuildId = 123456789012345678 and @Level = 'Error'
```

**Track Command Execution:**
```
CorrelationId = 'abc123def456'
```

**Find Slow Operations:**
```
ExecutionTimeMs > 1000
```

**Exception Stack Traces:**
```
Exception is not null
```

**Recent Warnings and Errors:**
```
@Level in ['Warning', 'Error'] and @Timestamp > Now() - 1h
```

See [Log Aggregation - Querying Logs](log-aggregation.md#querying-logs-in-seq) for advanced query syntax.

---

## Common Error Messages

### "The client secret is invalid"

**Cause:** Discord OAuth client secret is incorrect or expired

**Solution:**
1. Go to Discord Developer Portal → OAuth2 → General
2. Click "Reset Secret"
3. Copy new secret immediately
4. Update user secrets:
   ```bash
   dotnet user-secrets set "Discord:OAuth:ClientSecret" "new-secret-here"
   ```
5. Restart application

See [Discord Login Not Working](#discord-login-not-working)

---

### "Invalid redirect_uri"

**Cause:** OAuth redirect URI doesn't match Developer Portal configuration

**Solution:**

Ensure exact match:
- Developer Portal: `https://localhost:5001/signin-discord`
- No trailing slashes
- Correct protocol (HTTPS)
- Correct port number

See [OAuth Redirect Failures](#oauth-redirect-failures)

---

### "Disallowed intent(s)"

**Cause:** Gateway intent enabled in code but not in Developer Portal

**Solution:**
1. Discord Developer Portal → Bot → Privileged Gateway Intents
2. Enable required intents (Server Members, Message Content)
3. Save Changes
4. Restart bot

See [Gateway Intents Errors](#gateway-intents-errors)

---

### "A database operation failed while processing the request"

**Cause:** Database migration not applied or database connection issue

**Solution:**

```bash
# Apply pending migrations
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# If that fails, check connection string
dotnet user-secrets list | grep ConnectionStrings
```

See [Migration Failures](#migration-failures)

---

### "WebSocket connection was closed (CloseStatus: 4014)"

**Cause:** Bot requests gateway intents not enabled in Developer Portal

**Solution:**

Enable intents:
1. Discord Developer Portal → Bot
2. Privileged Gateway Intents → Enable Server Members Intent
3. Save Changes
4. Restart bot

See [Gateway Intents Errors](#gateway-intents-errors)

---

### "Unable to resolve service for type 'Microsoft.EntityFrameworkCore.DbContext'"

**Cause:** Database context not registered in dependency injection

**Solution:**

Verify `Program.cs` has:
```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

Check `InfrastructureServiceExtensions.cs` registers `BotDbContext`.

---

### "CSRF token validation failed"

**Cause:** Anti-forgery token mismatch

**Solution:**
1. Clear browser cookies
2. Ensure forms include `@Html.AntiForgeryToken()`
3. Verify `[ValidateAntiForgeryToken]` on POST handlers
4. Check session hasn't expired

See [Identity Configuration - CSRF Validation Failed](identity-configuration.md#troubleshooting)

---

### "Account locked"

**Cause:** Too many failed login attempts (5 by default)

**Solution:**
- Wait 15 minutes for automatic lockout expiration
- Or contact SuperAdmin to reset lockout via User Management

See [Identity Configuration - Account Lockout](identity-configuration.md#account-lockout)

---

### "Tailwind CSS not building"

**Cause:** Node.js not installed or npm dependencies missing

**Solution:**

```bash
# Verify Node.js is installed
node --version
npm --version

# Install dependencies
cd src/DiscordBot.Bot
npm install

# Rebuild CSS
npm run build:css

# Verify output
ls -l wwwroot/css/site.css
```

---

### "Failed to bind to address"

**Cause:** Port already in use or permission denied

**Solution:**

```bash
# Check what's using the port (Linux)
sudo lsof -i :5001

# Check what's using the port (Windows)
netstat -ano | findstr :5001

# Kill the process or change port in launchSettings.json
```

---

## Related Documentation

- [Discord Bot Setup Guide](discord-bot-setup.md) - Initial bot configuration
- [Identity Configuration](identity-configuration.md) - Authentication and authorization
- [Log Aggregation](log-aggregation.md) - Seq centralized logging setup
- [SignalR Real-Time Dashboard](signalr-realtime.md) - Real-time update troubleshooting
- [API Endpoints](api-endpoints.md) - REST API documentation
- [Environment Configuration](environment-configuration.md) - Configuration management

---

## Getting Additional Help

If you encounter issues not covered in this guide:

1. **Check Application Logs:**
   ```bash
   tail -f logs/discordbot-$(date +%Y-%m-%d).log
   ```

2. **Enable Verbose Logging:**
   Set `MinimumLevel.Default = "Debug"` in appsettings

3. **Use Seq for Analysis:**
   Query structured logs at `http://localhost:5341`

4. **Check Discord API Status:**
   Visit https://discordstatus.com

5. **Review GitHub Issues:**
   Search existing issues for similar problems

6. **Check Dependencies:**
   ```bash
   dotnet --version  # Should be 8.0+
   node --version    # Should be 16.0+
   ```

7. **Verify Configuration:**
   ```bash
   dotnet user-secrets list
   ```

---

**Version:** 1.0
**Last Updated:** 2025-12-30
**Issue Reference:** #316
