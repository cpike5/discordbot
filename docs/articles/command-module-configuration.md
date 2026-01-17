# Command Module Configuration

Dynamically enable and disable command modules without restarting the bot (Issue #1082).

**Status:** Implemented
**Version:** v0.11.0+

**Note:** This is a technical specification document. For comprehensive feature documentation including UI workflows and troubleshooting, see [Command Configuration](command-configuration.md).

---

## Table of Contents

- [Overview](#overview)
- [Configuration Options](#configuration-options)
- [Enable/Disable Modules](#enabledisable-modules)
- [API Reference](#api-reference)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

---

## Overview

The command module configuration feature allows administrators to dynamically enable or disable Discord bot command modules without restarting the application. This is useful for:

- Temporarily disabling features during maintenance
- Controlling feature access at runtime
- A/B testing new commands
- Responding to abuse or moderation needs

### Key Features

- Dynamic enable/disable without restart
- Per-guild module configuration support
- Global and guild-level override capability
- Real-time command visibility updates
- Audit logging of configuration changes

---

## Configuration Options

### Global Module Configuration

Global module settings are configured in `appsettings.json` under the `CommandModules` section:

```json
{
  "CommandModules": {
    "DefaultEnabled": true,
    "Modules": {
      "AdminModule": {
        "Enabled": true,
        "Description": "Bot administration commands"
      },
      "RatWatchModule": {
        "Enabled": true,
        "Description": "Rat Watch accountability feature"
      },
      "ScheduleModule": {
        "Enabled": true,
        "Description": "Scheduled message management"
      },
      "WelcomeModule": {
        "Enabled": true,
        "Description": "Welcome message configuration"
      }
    }
  }
}
```

### Configuration Options Class

```csharp
public class CommandModuleOptions
{
    /// <summary>
    /// Default enabled state for modules not explicitly configured.
    /// Default: true
    /// </summary>
    public bool DefaultEnabled { get; set; } = true;

    /// <summary>
    /// Module-specific configurations keyed by module name.
    /// </summary>
    public Dictionary<string, ModuleConfig> Modules { get; set; } = new();
}

public class ModuleConfig
{
    /// <summary>
    /// Whether this module is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Human-readable description of the module.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional guild-specific overrides.
    /// Key: guild ID, Value: enabled state
    /// </summary>
    public Dictionary<ulong, bool> GuildOverrides { get; set; } = new();
}
```

---

## Enable/Disable Modules

### Via Admin API

The `/Admin/CommandModules` endpoint provides REST API access to module configuration:

#### GET - List All Modules

```http
GET /api/command-modules
Authorization: Bearer {token}
```

**Response:**
```json
{
  "modules": [
    {
      "name": "AdminModule",
      "enabled": true,
      "description": "Bot administration commands",
      "globallyEnabled": true,
      "commandCount": 3
    },
    {
      "name": "RatWatchModule",
      "enabled": true,
      "description": "Rat Watch accountability feature",
      "globallyEnabled": true,
      "commandCount": 5
    }
  ]
}
```

#### PUT - Update Module State

```http
PUT /api/command-modules/{moduleName}
Authorization: Bearer {token}
Content-Type: application/json

{
  "enabled": false,
  "reason": "Maintenance"
}
```

**Response:**
```json
{
  "name": "ScheduleModule",
  "enabled": false,
  "updatedAt": "2025-01-17T10:30:00Z",
  "updatedBy": "admin@example.com"
}
```

#### PUT - Update Guild-Level Override

```http
PUT /api/command-modules/{moduleName}/guild/{guildId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "enabled": true
}
```

### Via Admin UI

The admin dashboard provides a GUI for managing module configuration:

1. Navigate to `/Admin/CommandModules`
2. View list of all available command modules
3. Click module name to expand details
4. Toggle "Enabled" switch to enable/disable
5. Optional: Set guild-specific overrides
6. Changes apply immediately

### Programmatically

```csharp
public class CommandModuleService
{
    private readonly ICommandModuleRepository _repository;

    // Get module state
    public async Task<ModuleConfig> GetModuleConfigAsync(string moduleName)
    {
        return await _repository.GetModuleConfigAsync(moduleName);
    }

    // Enable module globally
    public async Task EnableModuleAsync(string moduleName, string updatedBy)
    {
        var config = await _repository.GetModuleConfigAsync(moduleName);
        config.Enabled = true;
        await _repository.UpdateModuleConfigAsync(config);

        // Audit log
        await _auditLog.LogAsync(
            new AuditLogEntry
            {
                Category = AuditCategory.CommandManagement,
                Action = "Enable",
                Description = $"Module {moduleName} enabled",
                PerformedBy = updatedBy
            });
    }

    // Disable module globally
    public async Task DisableModuleAsync(string moduleName, string reason, string updatedBy)
    {
        var config = await _repository.GetModuleConfigAsync(moduleName);
        config.Enabled = false;
        await _repository.UpdateModuleConfigAsync(config);

        await _auditLog.LogAsync(
            new AuditLogEntry
            {
                Category = AuditCategory.CommandManagement,
                Action = "Disable",
                Description = $"Module {moduleName} disabled. Reason: {reason}",
                PerformedBy = updatedBy
            });
    }

    // Set guild-level override
    public async Task SetGuildOverrideAsync(
        string moduleName,
        ulong guildId,
        bool enabled,
        string updatedBy)
    {
        var config = await _repository.GetModuleConfigAsync(moduleName);
        config.GuildOverrides[guildId] = enabled;
        await _repository.UpdateModuleConfigAsync(config);

        await _auditLog.LogAsync(
            new AuditLogEntry
            {
                Category = AuditCategory.CommandManagement,
                Action = "Override",
                Description = $"Guild override for {moduleName}: {(enabled ? "enabled" : "disabled")}",
                GuildId = guildId,
                PerformedBy = updatedBy
            });
    }
}
```

---

## API Reference

### CommandModuleService

Central service for managing command module configuration.

```csharp
public interface ICommandModuleService
{
    /// <summary>
    /// Get configuration for a specific module.
    /// </summary>
    Task<ModuleConfig?> GetModuleConfigAsync(string moduleName);

    /// <summary>
    /// Get all module configurations.
    /// </summary>
    Task<IEnumerable<ModuleConfig>> GetAllModulesAsync();

    /// <summary>
    /// Check if a module is enabled globally or for a specific guild.
    /// </summary>
    Task<bool> IsModuleEnabledAsync(string moduleName, ulong? guildId = null);

    /// <summary>
    /// Enable a module globally.
    /// </summary>
    Task EnableModuleAsync(string moduleName, string updatedBy);

    /// <summary>
    /// Disable a module globally.
    /// </summary>
    Task DisableModuleAsync(string moduleName, string reason, string updatedBy);

    /// <summary>
    /// Set guild-specific override for a module.
    /// </summary>
    Task SetGuildOverrideAsync(
        string moduleName,
        ulong guildId,
        bool enabled,
        string updatedBy);

    /// <summary>
    /// Remove guild-specific override (revert to global setting).
    /// </summary>
    Task RemoveGuildOverrideAsync(string moduleName, ulong guildId, string updatedBy);
}
```

### CommandModuleRepository

Data access for module configuration.

```csharp
public interface ICommandModuleRepository
{
    Task<ModuleConfig?> GetModuleConfigAsync(string moduleName);
    Task<IEnumerable<ModuleConfig>> GetAllAsync();
    Task UpdateModuleConfigAsync(ModuleConfig config);
    Task DeleteModuleConfigAsync(string moduleName);
}
```

---

## Examples

### Example 1: Disable Scheduled Messages Module During Maintenance

```csharp
var moduleService = serviceProvider.GetRequiredService<ICommandModuleService>();

// Disable globally
await moduleService.DisableModuleAsync(
    moduleName: "ScheduleModule",
    reason: "Database maintenance window",
    updatedBy: "admin@example.com");

// Scheduled messages commands won't appear for any user
// After maintenance, re-enable:
await moduleService.EnableModuleAsync(
    moduleName: "ScheduleModule",
    updatedBy: "admin@example.com");
```

### Example 2: Enable Feature for Specific Guild Only

```csharp
// Keep RatWatch disabled globally (still testing)
await moduleService.DisableModuleAsync(
    moduleName: "RatWatchModule",
    reason: "Feature under development",
    updatedBy: "admin@example.com");

// But enable for specific test guild
await moduleService.SetGuildOverrideAsync(
    moduleName: "RatWatchModule",
    guildId: 123456789,  // Test guild
    enabled: true,
    updatedBy: "admin@example.com");

// Users in guild 123456789 can use RatWatch commands
// Users in other guilds cannot
```

### Example 3: Check Module Status Before Executing Command

In a command precondition:

```csharp
public class RequireModuleEnabledAttribute : PreconditionAttribute
{
    private readonly string _moduleName;

    public RequireModuleEnabledAttribute(string moduleName)
    {
        _moduleName = moduleName;
    }

    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var moduleService = services.GetRequiredService<ICommandModuleService>();

        var isEnabled = await moduleService.IsModuleEnabledAsync(
            _moduleName,
            context.Guild?.Id);

        if (!isEnabled)
        {
            return PreconditionResult.FromError(
                $"The {_moduleName} is currently disabled.");
        }

        return PreconditionResult.FromSuccess();
    }
}

// Usage in command:
[SlashCommand("schedule-list", "List scheduled messages")]
[RequireModuleEnabled("ScheduleModule")]
public async Task ListAsync()
{
    // Command implementation
}
```

---

## Module List Reference

### Available Modules for Configuration

| Module Name | Default | Commands |
|-------------|---------|----------|
| `AdminModule` | Enabled | `/status`, `/guilds`, `/shutdown` |
| `GeneralModule` | Enabled | `/ping` |
| `RatWatchModule` | Enabled | Rat Watch (context menu), `/rat-*` commands |
| `ScheduleModule` | Enabled | `/schedule-*` commands |
| `WelcomeModule` | Enabled | `/welcome` subcommands |
| `ReminderModule` | Enabled | `/remind` subcommands |
| `UtilityModule` | Enabled | `/userinfo`, `/serverinfo`, `/roleinfo` |
| `ModerationActionModule` | Enabled | `/warn`, `/kick`, `/ban`, `/mute`, `/purge` |
| `ModerationHistoryModule` | Enabled | `/mod-history` |
| `ModStatsModule` | Enabled | `/mod-stats` |
| `ModNoteModule` | Enabled | `/mod-notes` subcommands |
| `ModTagModule` | Enabled | `/mod-tag` subcommands |
| `WatchlistModule` | Enabled | `/watchlist` subcommands |
| `InvestigateModule` | Enabled | `/investigate` |
| `ConsentModule` | Enabled | `/consent` subcommands |
| `PrivacyModule` | Enabled | `/privacy` subcommands |
| `TtsModule` | Enabled | `/tts` command |
| `SoundboardModule` | Enabled | `/play`, `/sounds`, `/stop` |
| `VoiceModule` | Enabled | `/join`, `/leave` |
| `VerifyAccountModule` | Enabled | `/verify-account` |

---

## Troubleshooting

### Module Remains Disabled After Restart

**Problem:** You disabled a module, but after restarting the bot, it's enabled again.

**Cause:** The disable setting was only in memory, not persisted to the database.

**Solution:**
1. Verify the module configuration is saved to the database
2. Check database connectivity
3. View audit logs to confirm the disable action was recorded

```sql
-- Check module configuration in database
SELECT * FROM CommandModuleConfigurations WHERE ModuleName = 'ScheduleModule';

-- Check audit log
SELECT * FROM AuditLogs
WHERE Category = 'CommandManagement'
AND Description LIKE '%ScheduleModule%'
ORDER BY CreatedAt DESC;
```

### Module Not Appearing in Admin UI

**Problem:** A module name doesn't appear in the command modules admin page.

**Causes:**
1. Module not registered in DI container
2. Module name doesn't match class name
3. Module not yet discovered by InteractionHandler

**Solutions:**
1. Ensure module is added to DI during startup
2. Verify exact module name matches the class name (case-sensitive)
3. Restart bot to trigger module discovery

### Guild Override Not Working

**Problem:** You set a guild-specific override, but the module state doesn't change for that guild.

**Cause:**
- Override is stored but not being checked during command validation
- Cache is stale

**Solution:**
1. Clear the command cache: Restart bot or run cache clear endpoint
2. Verify override is saved in database:
```sql
SELECT * FROM CommandModuleOverrides
WHERE ModuleName = 'RatWatchModule'
AND GuildId = 123456789;
```

### Permission Denied on Module Configuration

**Problem:** Getting 403 Forbidden when trying to update module configuration via API.

**Causes:**
1. User doesn't have Admin role
2. Authorization policy not configured

**Solution:**
1. Verify user has Admin or SuperAdmin role
2. Check authorization policy on CommandModuleController:
```csharp
[ApiController]
[Route("api/command-modules")]
[Authorize(Policy = "RequireAdmin")]  // Requires Admin+
public class CommandModuleController : ControllerBase
{
    // ...
}
```

---

## Related Documentation

- [Authorization Policies](authorization-policies.md)
- [Audit Log System](audit-log-system.md)
- [Settings Page](settings-page.md)
- [Command Configuration](command-configuration.md)

---

**Last Updated:** January 17, 2025
**Feature Version:** 1.0 (Issue #1082)
