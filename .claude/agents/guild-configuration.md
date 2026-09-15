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
- **Member directory page:** `Blazor/Pages/Guilds/Members/Index.razor` (`RequireModerator`, filters/bulk-select/CSV export), ported off Razor Pages in Phase 4 cluster 4d — calls `IGuildMemberService` directly; `GuildMembersController` (the former `/api/guilds/{guildId}/members*` REST surface, `RequireAdmin`) retired with it, since `wwwroot/js/member-directory.js` was its only consumer.
- **Preview popups:** `IPreviewService` (`Services/Preview/PreviewService.cs`) — user/guild hover-popup lookups from the Discord cache, added in cluster 4d; `PreviewController` (`api/preview/*`) is now a thin wrapper over it for the still-legacy `preview-popup.js` surface, and `Blazor/Shared/Overlays/UserPreview.razor`/`GuildPreview.razor` call it directly in-circuit.

### Welcome System
- **Entity:** `WelcomeConfiguration`
- **Services:** `WelcomeService`; **Handler:** `WelcomeHandler` (listens for `UserJoined`)
- **Commands:** `WelcomeModule`; **Controller:** `WelcomeController`

### Command Module Configuration
- `CommandModuleConfigurationService` — Per-guild enable/disable of command modules, role-based restrictions

### Pages
- `Guilds/ModerationSettings/Index.cshtml`, `Guilds/AudioSettings/Index.cshtml`, `Admin/Settings.cshtml` (still Razor Pages); `Blazor/Pages/Guilds/Edit.razor` and `Blazor/Pages/Guilds/Welcome.razor` (Phase 4 cluster 4b), `Blazor/Pages/Guilds/{Index,Details}.razor` (the top-level guild list and per-guild dashboard, Phase 4 cluster 4d)

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
