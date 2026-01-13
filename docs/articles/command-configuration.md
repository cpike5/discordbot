# Command Module Configuration

This document describes the Command Module Configuration feature, which allows administrators to enable or disable Discord slash command modules through the admin UI.

## Overview

The Command Module Configuration feature provides:
- Enable/disable slash command modules without code changes
- Persistence of module states across bot restarts
- Admin UI management through the Settings page
- Protection for core modules that cannot be disabled
- Restart notification when changes require a bot restart

## Accessing the Feature

**URL:** `/Admin/Settings` (Commands tab)

**Authorization:** RequireAdmin policy

Navigate to **Admin > Settings** in the admin UI, then select the **Commands** tab to view and manage command modules.

## Module Categories

Command modules are organized into the following categories:

### Admin Commands

Server administration and configuration commands.

| Module | Display Name | Commands |
|--------|--------------|----------|
| `AdminModule` | Admin | `/admin info`, `/admin kick`, `/admin ban` |
| `WelcomeModule` | Welcome | `/welcome setup/test/disable` |
| `ScheduleModule` | Scheduled Messages | `/schedule-message create/list/delete/edit` |

### Moderation Commands

User moderation and enforcement commands.

| Module | Display Name | Commands |
|--------|--------------|----------|
| `ModerationActionModule` | Moderation Actions | `/warn`, `/kick`, `/ban`, `/mute`, `/purge` |
| `ModerationHistoryModule` | Moderation History | `/mod-history` |
| `ModStatsModule` | Moderator Stats | `/mod-stats` |
| `ModNoteModule` | Mod Notes | `/mod-notes add/list/delete` |
| `ModTagModule` | Mod Tags | `/mod-tag add/remove/list` |
| `WatchlistModule` | Watchlist | `/watchlist add/remove/list` |
| `InvestigateModule` | Investigate | `/investigate` |

### Features Commands

Optional feature commands.

| Module | Display Name | Commands |
|--------|--------------|----------|
| `RatWatchModule` | Rat Watch | Rat Watch (context menu), `/rat-clear`, `/rat-stats`, `/rat-leaderboard`, `/rat-settings` |
| `ReminderModule` | Reminders | `/remind set/list/delete` |

### Audio Commands

Voice channel and audio playback commands.

| Module | Display Name | Commands |
|--------|--------------|----------|
| `TtsModule` | Text-to-Speech | `/tts <message> [voice]` |
| `SoundboardModule` | Soundboard | `/play <sound>`, `/sounds`, `/stop` |
| `VoiceModule` | Voice | `/join`, `/join-channel <channel>`, `/leave` |

### Utility Commands

Information and utility commands.

| Module | Display Name | Commands |
|--------|--------------|----------|
| `UtilityModule` | Utility | `/userinfo`, `/serverinfo`, `/roleinfo` |

### Core Commands (Always Enabled)

Essential bot commands that cannot be disabled.

| Module | Display Name | Commands |
|--------|--------------|----------|
| `GeneralModule` | General | `/ping` |
| `VerifyAccountModule` | Verify Account | `/verify` |
| `ConsentModule` | Consent | `/consent`, `/privacy` |

## Always-Enabled Modules

The following modules are marked as **Core** and cannot be disabled:

- **GeneralModule** - Basic bot commands like `/ping`
- **VerifyAccountModule** - Account verification commands
- **ConsentModule** - Privacy consent management commands

These modules are essential for basic bot operation and user privacy compliance. Attempting to disable them through the API will return an error.

## Making Changes

### Enabling/Disabling Modules

1. Navigate to **Admin > Settings > Commands**
2. Find the module you want to modify
3. Toggle the switch to enable or disable the module
4. Click **Save Changes**

### Restart Requirement

Most module configuration changes require a bot restart to take effect. This is because:
- Command modules are loaded during bot initialization
- Discord command registration happens at startup
- Disabling a module removes its commands from Discord

When changes are saved that require a restart:
1. A **yellow banner** appears at the top of the Settings page indicating "Restart Required"
2. The banner persists across page navigation until the bot is restarted
3. Navigate to **Admin > Bot Control** to restart the bot

**Note:** Core modules do not require restart because they cannot be toggled.

## Default Configuration

By default, **all modules are enabled** when the bot starts for the first time. The system automatically creates database records for all known modules during initialization.

Default states:
- Admin modules: **Enabled**
- Moderation modules: **Enabled**
- Feature modules: **Enabled**
- Audio modules: **Enabled**
- Utility modules: **Enabled**
- Core modules: **Enabled** (cannot be changed)

## Database Entity

### CommandModuleConfiguration

Stores the enabled/disabled state for each command module.

| Property | Type | Description |
|----------|------|-------------|
| `ModuleName` | string | Primary key - module class name (e.g., "AdminModule") |
| `IsEnabled` | bool | Whether the module is enabled |
| `DisplayName` | string | User-friendly name for the admin UI |
| `Description` | string? | Optional description of the module |
| `Category` | string | Grouping category (Admin, Moderation, Features, Audio, Utility, Core) |
| `RequiresRestart` | bool | Whether changes require a bot restart |
| `LastModifiedAt` | DateTime | When the configuration was last changed |
| `LastModifiedBy` | string? | User ID who made the last change |

## Services

### ICommandModuleConfigurationService

Service for managing command module configuration with business logic.

| Method | Purpose |
|--------|---------|
| `GetAllModulesAsync()` | Get all module configurations |
| `GetModulesByCategoryAsync(category)` | Get modules for a specific category |
| `GetModuleAsync(moduleName)` | Get a single module configuration |
| `IsModuleEnabledAsync(moduleName)` | Check if a module is enabled |
| `SetModuleEnabledAsync(moduleName, isEnabled, userId)` | Update a single module's state |
| `UpdateModulesAsync(updates, userId)` | Batch update multiple modules |
| `SyncModulesAsync()` | Sync database with known modules |
| `IsRestartPending` | Whether a restart is needed |
| `ClearRestartPending()` | Clear the restart flag after restart |

### ICommandModuleConfigurationRepository

Repository for CRUD operations on `CommandModuleConfiguration` entities.

| Method | Purpose |
|--------|---------|
| `GetAllAsync()` | Get all configurations |
| `GetByNameAsync(moduleName)` | Get by module name |
| `GetByCategoryAsync(category)` | Get by category |
| `UpsertAsync(configuration)` | Insert or update a configuration |

## How It Works

### Startup Flow

1. `BotHostedService` starts the bot
2. `InteractionHandler.InitializeAsync()` is called
3. `SyncModulesAsync()` ensures all known modules have database records
4. For each discovered module type in the assembly:
   - Check if the module is enabled in the database
   - If enabled (or unconfigured), register it with Discord.NET
   - If disabled, skip registration
5. Enabled modules are registered with Discord

### Configuration Change Flow

1. Admin toggles a module in the Settings UI
2. Settings page calls `POST /Admin/Settings?handler=SaveCommandModules`
3. `CommandModuleConfigurationService.UpdateModulesAsync()` is called
4. Database is updated with new enabled states
5. If any changed module requires restart, `IsRestartPending` is set to true
6. UI shows the restart banner
7. Admin restarts bot via Bot Control page
8. On restart, new configuration takes effect

## Troubleshooting

### Commands not appearing after enabling a module

1. Ensure you saved changes (click "Save Changes")
2. Check if the restart banner is showing - if so, restart the bot
3. Wait up to an hour for global command propagation (or use `TestGuildId` for instant updates)

### Cannot disable a module

1. Check if the module is in the "Core" category - Core modules cannot be disabled
2. Verify you have Admin permissions

### Changes not persisting

1. Verify the save operation succeeded (check for error alerts)
2. Check database connectivity
3. Review logs for any errors during save

### Restart banner won't go away

The restart banner persists until the bot is actually restarted. It tracks that configuration changes were made that require a restart. Navigate to **Admin > Bot Control** and restart the bot.

## See Also

- [Settings Page](settings-page.md) - General settings page documentation
- [Commands Page](commands-page.md) - View registered commands and metadata
