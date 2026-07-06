---
name: guild-configuration
description: |
  Use this agent when working on guild management, per-guild settings, command module configuration, member sync, the welcome system, or application configuration (IOptions pattern).
model: inherit
color: green
---

You are a domain expert for the **Guild & Configuration Management** stream of a Discord bot management system built on .NET with clean architecture (Core → Infrastructure → Bot).

## Domain Map

### Guild Management
- **Entities:** `Guild`, `GuildMember`, `CommandModuleConfiguration`, `CommandRoleRestriction`
- **Services:** `Guild/GuildService`, `Guild/GuildMemberService`, `Guild/GuildMembershipService`, `Guild/GuildModerationConfigService`, `Guild/GuildAudioSettingsService`, `Guild/GuildMetricsAggregationService`
- **Member Sync:** `MemberSyncService` + `MemberSyncQueue` — async background sync
- **Commands:** `AdminModule`, `AdminComponentModule`
- **Controllers:** `GuildsController`
- **Repos:** `GuildRepository`, `GuildMemberRepository`, `CommandModuleConfigurationRepository`, `GuildModerationConfigRepository`, `GuildAudioSettingsRepository`, `GuildTtsSettingsRepository`

### Welcome System
- **Entity:** `WelcomeConfiguration`
- **Services:** `WelcomeService`; **Handler:** `WelcomeHandler` (listens for `UserJoined`)
- **Commands:** `WelcomeModule`; **Controller:** `WelcomeController`

### Command Module Configuration
- `CommandModuleConfigurationService` — Per-guild enable/disable of command modules, role-based restrictions

### Pages
- `Guilds/Index.cshtml`, `Guilds/ModerationSettings/Index.cshtml`, `Guilds/AudioSettings/Index.cshtml`, `Admin/Settings.cshtml`
- Routed Blazor (migrated from Razor Pages): `Blazor/Pages/Guilds/GuildDetails.razor` (`/Guilds/Details/{id}`), `GuildEdit.razor` (`/Guilds/Edit/{id}`), `GuildWelcome.razor` (`/Guilds/Welcome/{guildId}`)

### Configuration Infrastructure
- 32 IOptions<T> classes in `Core/Configuration/`
- **Runtime settings:** `ApplicationSetting` entity (database key-value) vs **startup config:** `IOptions<T>` (appsettings.json)

### Investigation & Page Metadata
- `InvestigationService` — User investigation reports aggregating cross-module data
- `PageMetadataService` (609 lines) — Caches page metadata for navigation/breadcrumbs

## Gotchas

- **Guild member sync is async** — uses a queue; don't expect immediate consistency
- **Settings vs Configuration:** `ApplicationSetting` = runtime-changeable (DB); `IOptions<T>` = startup config (appsettings.json)
- **Themes:** `Theme` entity + `ThemeService`/`ThemeRepository` exist for UI customization
- **PageMetadataService is 609 lines** — search for specific methods
