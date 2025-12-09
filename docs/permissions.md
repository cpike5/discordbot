# Permissions System

This document provides comprehensive documentation on the precondition attribute system used for command authorization and access control in the Discord bot.

## Overview

The permissions system is built on Discord.NET's `PreconditionAttribute` framework, providing declarative permission checks for slash commands. Preconditions are evaluated before command execution and can prevent unauthorized access.

**Key Features:**
- Declarative authorization with attributes
- Guild-based permission checks
- Bot owner verification
- Rate limiting and throttling
- Automatic error handling and user feedback
- Extensible for custom permission logic

---

## Built-in Preconditions

### RequireAdminAttribute

The `RequireAdminAttribute` enforces that the user has Administrator permission in the guild where the command is executed.

**Namespace:** `DiscordBot.Bot.Preconditions`

**Inheritance:** `PreconditionAttribute` (Discord.Interactions)

#### Implementation

```csharp
using Discord;
using Discord.Interactions;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to have Administrator permission in the guild.
/// </summary>
public class RequireAdminAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user executing the command has Administrator permission.
    /// </summary>
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Check if the command is being used in a guild (not DM)
        if (context.Guild == null)
        {
            return Task.FromResult(
                PreconditionResult.FromError("This command can only be used in a guild (server).")
            );
        }

        // Cast the user to IGuildUser to access guild permissions
        if (context.User is not IGuildUser guildUser)
        {
            return Task.FromResult(
                PreconditionResult.FromError("Unable to retrieve guild user information.")
            );
        }

        // Check if the user has Administrator permission
        if (guildUser.GuildPermissions.Administrator)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(
            PreconditionResult.FromError("You must have Administrator permission to use this command.")
        );
    }
}
```

#### Usage

**Module-Level Application:**
```csharp
[RequireAdmin]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    // All commands in this module require admin permission

    [SlashCommand("status", "Display bot status")]
    public async Task StatusAsync()
    {
        // Command logic
    }
}
```

**Command-Level Application:**
```csharp
public class MixedModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Check bot latency")]
    public async Task PingAsync()
    {
        // Available to all users
    }

    [SlashCommand("config", "Configure bot settings")]
    [RequireAdmin]
    public async Task ConfigAsync()
    {
        // Only available to administrators
    }
}
```

#### Behavior

| Condition | Result |
|-----------|--------|
| User has Administrator permission in guild | ✅ Command executes |
| User lacks Administrator permission | ❌ Permission denied error |
| Command executed in DMs | ❌ "Can only be used in a guild" error |
| Unable to retrieve guild user | ❌ Internal error |

#### Error Messages

```
This command can only be used in a guild (server).
```
*Returned when command is used in DMs*

```
Unable to retrieve guild user information.
```
*Internal error casting user to IGuildUser*

```
You must have Administrator permission to use this command.
```
*User lacks the required permission*

---

### RequireOwnerAttribute

The `RequireOwnerAttribute` enforces that the user is the Discord application owner (bot owner).

**Namespace:** `DiscordBot.Bot.Preconditions`

**Inheritance:** `PreconditionAttribute` (Discord.Interactions)

#### Implementation

```csharp
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to be the bot owner.
/// </summary>
public class RequireOwnerAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user executing the command is the bot owner.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        if (context.Client is not DiscordSocketClient client)
        {
            return PreconditionResult.FromError("Unable to access Discord client.");
        }

        // Get the application info to retrieve the owner
        var application = await client.GetApplicationInfoAsync();

        // Check if the user is the application owner
        if (context.User.Id == application.Owner.Id)
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("This command can only be used by the bot owner.");
    }
}
```

#### Usage

```csharp
[SlashCommand("shutdown", "Gracefully shut down the bot")]
[RequireOwner]
public async Task ShutdownAsync()
{
    // Only the bot owner can execute this
    _lifetime.StopApplication();
}
```

#### Behavior

| Condition | Result |
|-----------|--------|
| User ID matches application owner ID | ✅ Command executes |
| User ID does not match owner ID | ❌ Permission denied error |
| Unable to access Discord client | ❌ Internal error |
| Unable to fetch application info | ❌ API error |

#### Error Messages

```
Unable to access Discord client.
```
*Internal error accessing the DiscordSocketClient*

```
This command can only be used by the bot owner.
```
*User is not the application owner*

#### Performance Considerations

- Makes an async API call to `GetApplicationInfoAsync()`
- Discord.NET caches the application info
- Avoid using on high-frequency commands if possible
- Consider caching owner ID if frequently checked

---

### RateLimitAttribute

The `RateLimitAttribute` implements rate limiting (throttling) to prevent command abuse and protect resources.

**Namespace:** `DiscordBot.Bot.Preconditions`

**Inheritance:** `PreconditionAttribute` (Discord.Interactions)

**Dependencies:** `DiscordBot.Core.Enums.RateLimitTarget`

#### Implementation

```csharp
using System.Collections.Concurrent;
using Discord;
using Discord.Interactions;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that applies rate limiting to commands.
/// </summary>
public class RateLimitAttribute : PreconditionAttribute
{
    private static readonly ConcurrentDictionary<string, List<DateTime>> _invocations = new();

    private readonly int _times;
    private readonly double _periodSeconds;
    private readonly RateLimitTarget _target;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitAttribute"/> class.
    /// </summary>
    /// <param name="times">The number of times the command can be invoked within the period.</param>
    /// <param name="periodSeconds">The period in seconds during which the limit applies.</param>
    /// <param name="target">The target scope for the rate limit (User, Guild, or Global).</param>
    public RateLimitAttribute(int times, double periodSeconds, RateLimitTarget target = RateLimitTarget.User)
    {
        _times = times;
        _periodSeconds = periodSeconds;
        _target = target;
    }

    /// <summary>
    /// Checks if the rate limit has been exceeded for this command invocation.
    /// </summary>
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var now = DateTime.UtcNow;
        var key = GetRateLimitKey(context, commandInfo);

        // Get or add the invocation list for this key
        var invocations = _invocations.GetOrAdd(key, _ => new List<DateTime>());

        lock (invocations)
        {
            // Remove old invocations outside the time window
            invocations.RemoveAll(time => (now - time).TotalSeconds > _periodSeconds);

            // Check if rate limit is exceeded
            if (invocations.Count >= _times)
            {
                var oldestInvocation = invocations.Min();
                var timeUntilReset = _periodSeconds - (now - oldestInvocation).TotalSeconds;

                return Task.FromResult(
                    PreconditionResult.FromError(
                        $"Rate limit exceeded. Please wait {timeUntilReset:F1} seconds before using this command again."
                    )
                );
            }

            // Add current invocation
            invocations.Add(now);
        }

        return Task.FromResult(PreconditionResult.FromSuccess());
    }

    /// <summary>
    /// Generates a unique key for rate limiting based on the target scope.
    /// </summary>
    private string GetRateLimitKey(IInteractionContext context, ICommandInfo commandInfo)
    {
        var commandName = commandInfo.Name;

        return _target switch
        {
            RateLimitTarget.User => $"user:{context.User.Id}:{commandName}",
            RateLimitTarget.Guild => $"guild:{context.Guild?.Id ?? 0}:{commandName}",
            RateLimitTarget.Global => $"global:{commandName}",
            _ => throw new InvalidOperationException($"Unknown rate limit target: {_target}")
        };
    }
}
```

#### RateLimitTarget Enum

```csharp
namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the target scope for rate limiting.
/// </summary>
public enum RateLimitTarget
{
    /// <summary>
    /// Rate limit applies per user across all guilds.
    /// </summary>
    User,

    /// <summary>
    /// Rate limit applies per guild (all users in the guild share the limit).
    /// </summary>
    Guild,

    /// <summary>
    /// Rate limit applies globally across all users and guilds.
    /// </summary>
    Global
}
```

#### Constructor Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `times` | int | Maximum invocations allowed within period | Required |
| `periodSeconds` | double | Time window in seconds | Required |
| `target` | RateLimitTarget | Scope of the rate limit | `User` |

#### Usage Examples

**Per-User Rate Limiting:**
```csharp
[RateLimit(3, 60.0, RateLimitTarget.User)]
[SlashCommand("search", "Search for content")]
public async Task SearchAsync(string query)
{
    // Each user can use this 3 times per 60 seconds
}
```

**Per-Guild Rate Limiting:**
```csharp
[RateLimit(10, 300.0, RateLimitTarget.Guild)]
[SlashCommand("announcement", "Make an announcement")]
public async Task AnnouncementAsync(string message)
{
    // Guild-wide limit: 10 uses per 5 minutes (shared by all users)
}
```

**Global Rate Limiting:**
```csharp
[RateLimit(100, 3600.0, RateLimitTarget.Global)]
[SlashCommand("generate", "Generate AI content")]
public async Task GenerateAsync(string prompt)
{
    // Bot-wide limit: 100 uses per hour across all users and guilds
}
```

**Default Target (User):**
```csharp
[RateLimit(5, 120.0)]  // target defaults to User
[SlashCommand("info", "Get information")]
public async Task InfoAsync()
{
    // Each user can use this 5 times per 2 minutes
}
```

#### Behavior

| Scenario | Result |
|----------|--------|
| Invocations < limit | ✅ Command executes, invocation recorded |
| Invocations >= limit | ❌ Rate limit error with cooldown time |
| Time window expired | Old invocations removed, new invocation allowed |
| Bot restart | All rate limits cleared (in-memory storage) |

#### Rate Limit Keys

The attribute generates unique keys based on the target scope:

| Target | Key Format | Example |
|--------|-----------|---------|
| User | `user:{userId}:{commandName}` | `user:123456789:search` |
| Guild | `guild:{guildId}:{commandName}` | `guild:987654321:announcement` |
| Global | `global:{commandName}` | `global:generate` |

#### Error Messages

```
Rate limit exceeded. Please wait 42.3 seconds before using this command again.
```
*User/guild/global has exceeded the rate limit*

#### Thread Safety

- Uses `ConcurrentDictionary` for thread-safe invocation tracking
- Individual invocation lists protected by `lock` statements
- Safe for concurrent command executions

#### Storage Characteristics

- **In-Memory:** Rate limits stored in static concurrent dictionary
- **Non-Persistent:** Cleared on bot restart
- **Automatic Cleanup:** Expired invocations removed on each check
- **Memory Efficient:** Only tracks active time windows

#### Distributed Deployments

For multi-instance bot deployments, consider:
- Shared cache (Redis, Memcached)
- Database-backed rate limiting
- Distributed locking mechanisms

**Example Redis Implementation (pseudocode):**
```csharp
// Use Redis sorted sets with timestamp scores
var key = GetRateLimitKey(context, commandInfo);
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var windowStart = now - _periodSeconds;

// Remove old entries and count remaining
redis.ZRemRangeByScore(key, 0, windowStart);
var count = redis.ZCard(key);

if (count >= _times)
{
    // Rate limited
}
else
{
    redis.ZAdd(key, now, Guid.NewGuid().ToString());
    redis.Expire(key, _periodSeconds);
}
```

---

## Creating Custom Preconditions

### Basic Custom Precondition

```csharp
using Discord;
using Discord.Interactions;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Example custom precondition that requires a specific role.
/// </summary>
public class RequireRoleAttribute : PreconditionAttribute
{
    private readonly string _roleName;

    public RequireRoleAttribute(string roleName)
    {
        _roleName = roleName;
    }

    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        if (context.User is not IGuildUser guildUser)
        {
            return Task.FromResult(
                PreconditionResult.FromError("This command can only be used in a guild.")
            );
        }

        var hasRole = guildUser.Roles.Any(r => r.Name.Equals(_roleName, StringComparison.OrdinalIgnoreCase));

        if (hasRole)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(
            PreconditionResult.FromError($"You must have the '{_roleName}' role to use this command.")
        );
    }
}
```

**Usage:**
```csharp
[RequireRole("Moderator")]
[SlashCommand("warn", "Warn a user")]
public async Task WarnAsync(IUser user, string reason)
{
    // Command logic
}
```

---

### Precondition with Dependency Injection

```csharp
using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Custom precondition that checks if a user is banned from using commands.
/// Uses DI to access the user repository.
/// </summary>
public class RequireNotBannedAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Retrieve the user repository from DI
        var userRepository = services.GetService<IUserRepository>();

        if (userRepository == null)
        {
            return PreconditionResult.FromError("Unable to access user repository.");
        }

        // Check if user is banned
        var user = await userRepository.GetByIdAsync(context.User.Id);

        if (user != null && user.IsBanned)
        {
            return PreconditionResult.FromError("You are banned from using bot commands.");
        }

        return PreconditionResult.FromSuccess();
    }
}
```

**Usage:**
```csharp
[RequireNotBanned]
public class GameModule : InteractionModuleBase<SocketInteractionContext>
{
    // All commands check if user is banned before execution
}
```

---

### Async Precondition with Configuration

```csharp
using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Services;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that checks against a configurable list of allowed guilds.
/// </summary>
public class RequireAllowedGuildAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Retrieve configuration from DI
        var config = services.GetService<IOptions<BotConfiguration>>()?.Value;

        if (config == null)
        {
            return Task.FromResult(
                PreconditionResult.FromError("Unable to access bot configuration.")
            );
        }

        if (context.Guild == null)
        {
            return Task.FromResult(
                PreconditionResult.FromError("This command can only be used in a guild.")
            );
        }

        // Check if guild is in allowed list (example: config.AllowedGuildIds)
        if (config.AllowedGuildIds?.Contains(context.Guild.Id) == true)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(
            PreconditionResult.FromError("This command is not available in this guild.")
        );
    }
}
```

---

### Combining Multiple Preconditions

Preconditions can be stacked to create complex permission requirements:

```csharp
[RequireAdmin]
[RequireNotBanned]
[RateLimit(3, 60.0)]
[SlashCommand("purge", "Delete messages")]
public async Task PurgeAsync(int count)
{
    // Must be admin, not banned, and rate limited to 3 per minute
}
```

**Evaluation Order:**
1. Preconditions are evaluated in the order they are declared
2. All preconditions must succeed for command to execute
3. First failure short-circuits evaluation and returns error

---

## Permission Error Handling

### Automatic Error Responses

The `InteractionHandler` automatically handles permission errors and provides user feedback:

```csharp
// In OnSlashCommandExecutedAsync
if (result.Error == InteractionCommandError.UnmetPrecondition)
{
    var embed = new EmbedBuilder()
        .WithTitle("Permission Denied")
        .WithDescription(errorMessage ?? "You do not have permission to use this command.")
        .WithColor(Color.Red)
        .WithFooter($"Correlation ID: {correlationId}")
        .WithCurrentTimestamp()
        .Build();

    await context.Interaction.RespondAsync(embed: embed, ephemeral: true);
}
```

**Properties:**
- Ephemeral (only visible to the user)
- Red color for error indication
- Includes correlation ID for support
- Contains the specific error message from the precondition

---

### Custom Error Handling in Preconditions

Preconditions can return detailed error messages:

```csharp
public override Task<PreconditionResult> CheckRequirementsAsync(...)
{
    if (!someCondition)
    {
        return Task.FromResult(
            PreconditionResult.FromError("Detailed error message explaining why the check failed.")
        );
    }

    return Task.FromResult(PreconditionResult.FromSuccess());
}
```

---

## Best Practices

### 1. Choose the Right Precondition Level

**Module-Level:**
```csharp
[RequireAdmin]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    // All commands require admin
}
```
- Use when all commands in a module share the same permission requirement
- Reduces code duplication
- Clear intent for the entire module

**Command-Level:**
```csharp
[SlashCommand("config", "Configure settings")]
[RequireAdmin]
public async Task ConfigAsync()
{
    // Only this command requires admin
}
```
- Use when only specific commands need restrictions
- Allows mixing public and restricted commands in the same module

---

### 2. Provide Clear Error Messages

**Good:**
```csharp
PreconditionResult.FromError("You must have the 'Moderator' role to use this command.")
```

**Better:**
```csharp
PreconditionResult.FromError(
    "You must have the 'Moderator' role to use this command. " +
    "Contact a server administrator if you believe this is an error."
)
```

---

### 3. Use Appropriate Rate Limit Targets

| Command Type | Recommended Target | Reasoning |
|-------------|-------------------|-----------|
| General information | `User` | Prevents individual spam |
| Guild configuration | `Guild` | Protects guild resources |
| External API calls | `Global` | Respects API quotas |
| Expensive operations | `Global` | Protects bot resources |

---

### 4. Handle DM Context Gracefully

Always check for guild context when using guild-specific permissions:

```csharp
if (context.Guild == null)
{
    return Task.FromResult(
        PreconditionResult.FromError("This command can only be used in a guild (server).")
    );
}
```

---

### 5. Consider Performance

**Async Operations:**
- Minimize async API calls in preconditions
- Cache frequently accessed data
- Consider impact on command execution time

**Example - Caching Owner ID:**
```csharp
private static ulong? _cachedOwnerId;

public override async Task<PreconditionResult> CheckRequirementsAsync(...)
{
    if (_cachedOwnerId == null)
    {
        var application = await client.GetApplicationInfoAsync();
        _cachedOwnerId = application.Owner.Id;
    }

    if (context.User.Id == _cachedOwnerId)
    {
        return PreconditionResult.FromSuccess();
    }

    // ...
}
```

---

### 6. Log Permission Checks

Add logging to custom preconditions for debugging and auditing:

```csharp
public override Task<PreconditionResult> CheckRequirementsAsync(
    IInteractionContext context,
    ICommandInfo commandInfo,
    IServiceProvider services)
{
    var logger = services.GetService<ILogger<RequireRoleAttribute>>();

    logger?.LogDebug(
        "Checking role requirement '{RoleName}' for user {UserId} in command {CommandName}",
        _roleName,
        context.User.Id,
        commandInfo.Name
    );

    // Permission check logic...
}
```

---

### 7. Test Edge Cases

Ensure preconditions handle:
- DM vs. guild context
- Missing services from DI
- Null reference scenarios
- Concurrent invocations (for rate limiting)
- Bot restarts (for stateful checks)

---

## Configuration Reference

### appsettings.json

```json
{
  "Discord": {
    "DefaultRateLimitInvokes": 3,
    "DefaultRateLimitPeriodSeconds": 60.0,
    "AdditionalOwnerIds": []
  }
}
```

### BotConfiguration Class

```csharp
public class BotConfiguration
{
    /// <summary>
    /// Default number of invocations allowed within the rate limit period.
    /// </summary>
    public int DefaultRateLimitInvokes { get; set; } = 3;

    /// <summary>
    /// Default rate limit period in seconds.
    /// </summary>
    public double DefaultRateLimitPeriodSeconds { get; set; } = 60.0;

    /// <summary>
    /// Additional user IDs that should be treated as bot owners.
    /// </summary>
    public List<ulong> AdditionalOwnerIds { get; set; } = new();
}
```

---

## Troubleshooting

### Precondition Not Executing

**Symptom:** Permission checks appear to be skipped

**Possible Causes:**
1. Attribute not applied to command or module
2. Attribute applied to wrong method (not a slash command)
3. Exception thrown in precondition (silently caught)

**Solutions:**
- Verify attribute is present with `[RequireAdmin]` or custom attribute
- Ensure method has `[SlashCommand]` attribute
- Add logging to precondition to verify it's being called

---

### Permission Denied Despite Having Permission

**Symptom:** User with correct permission receives denial

**Possible Causes:**
1. Checking wrong permission (e.g., `ManageGuild` instead of `Administrator`)
2. Permission inherited from role, not applied directly
3. Bot cache out of sync with Discord

**Solutions:**
- Verify exact permission being checked in precondition code
- Test with user who has permission directly assigned
- Restart bot to refresh cache

---

### Rate Limit Not Working

**Symptom:** Users can spam commands despite rate limit

**Possible Causes:**
1. Rate limit attribute not applied
2. Bot restarted (in-memory limits cleared)
3. Different command names (limits are per-command)
4. Key generation logic issue

**Solutions:**
- Verify `[RateLimit(...)]` attribute is present
- Rate limits are in-memory and don't persist across restarts
- Check that command name is consistent
- Add debug logging to `GetRateLimitKey` method

---

### Dependency Injection Not Working in Precondition

**Symptom:** `GetService<T>()` returns null in custom precondition

**Possible Causes:**
1. Service not registered in DI container
2. Wrong service lifetime (trying to get scoped service from singleton)
3. Service provider not passed correctly

**Solutions:**
- Verify service is registered in `Program.cs` or `Startup.cs`
- Create a scope if accessing scoped services: `services.CreateScope()`
- Use constructor injection in the attribute if needed (advanced)

---

## Related Documentation

- [Admin Commands](admin-commands.md) - Command reference and usage examples
- [Database Schema](database-schema.md) - CommandLog entity for audit tracking
- [Configuration Guide](../CLAUDE.md#configuration) - Bot configuration and user secrets

---

## Examples Repository

### Complete Custom Precondition Examples

See the following files in the source code for reference implementations:

| File | Purpose |
|------|---------|
| `src/DiscordBot.Bot/Preconditions/RequireAdminAttribute.cs` | Guild Administrator check |
| `src/DiscordBot.Bot/Preconditions/RequireOwnerAttribute.cs` | Bot owner verification |
| `src/DiscordBot.Bot/Preconditions/RateLimitAttribute.cs` | Rate limiting implementation |

---

*Document Version: 1.0*
*Last Updated: December 2025*
*Status: Phase 3 Implementation Complete*
