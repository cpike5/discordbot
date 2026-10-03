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
- **Page:** form posts re-render through `GuildPageModelBase.PopulateGuildLayout`; the preview is `wwwroot/js/discord-markdown.js` (escape first), the master switch uses `data-section-gate` (`section-gate.js`); a saved channel the bot cannot list stays selected (`FormFieldState.ChannelOptions`)

### Command Module Configuration
- `CommandModuleConfigurationService` — Per-guild enable/disable of command modules, role-based restrictions

### Pages
- `Guilds/` (Index, Details, Edit; Details widgets are `_DashboardWidget` + body partials in `Pages/Guilds/Widgets/`, each section loaded separately by `GuildDetailsAggregator` so one failure shows a retry state, not a 500), `Guilds/ModerationSettings/Index.cshtml`, `Guilds/AudioSettings/Index.cshtml`, `Guilds/Welcome.cshtml`, `Admin/Settings.cshtml`

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
