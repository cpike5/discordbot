# CLAUDE.md

Guidance for Claude Code when working with this Discord bot management system. See README.md for full documentation.

**Current version:** v1.0.1-dev. See [CLAUDE-REFERENCE.md](CLAUDE-REFERENCE.md) for comprehensive lookup tables.

## Quick Reference

```bash
# Build & Run
dotnet build
dotnet run --project src/DiscordBot.Bot
dotnet test

# Entity Framework — SQLite (--context required)
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext

# Entity Framework — PostgreSQL (--context required)
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext

# Data Migration (SQLite → PostgreSQL or vice versa)
dotnet run --project src/DiscordBot.Bot -- migrate-data --source "Data Source=data/discordbot.db" --target "Host=localhost;Database=discordbot;Username=discordbot;Password=changeme"

# Documentation
.\build-docs.ps1 -Serve  # Build and serve at http://localhost:8080
```

**User Secrets ID:** `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

## Architecture

Three-layer clean architecture: Core (domain) → Infrastructure (data) → Bot (UI/API)

| Location | Purpose |
|----------|---------|
| `src/DiscordBot.Core/` | Entities, interfaces, DTOs, enums, configuration |
| `src/DiscordBot.Infrastructure/` | EF Core DbContext, repositories, data access |
| `src/DiscordBot.Bot/` | Web API, Razor Pages, Discord bot, DI composition |

**Key patterns:** `DiscordSocketClient` singleton managed by `BotHostedService`; Repository pattern; Serilog logging; ASP.NET Core Identity with Discord OAuth; IOptions<T> for config.

For system-level understanding during major feature work, see `docs/architecture/`.

## Critical Gotchas

### JavaScript and Discord Snowflake IDs

**CRITICAL**: Discord IDs (`ulong` in C#) are 64-bit integers exceeding JavaScript's `Number.MAX_SAFE_INTEGER`. **Always treat Discord IDs as strings in JavaScript**:

```razor
<!-- WRONG - loses precision -->
window.guildId = @Model.GuildId;

<!-- CORRECT - preserves all digits -->
window.guildId = '@Model.GuildId';
```

### Configuration

- **Never commit tokens** - use User Secrets for `Discord:Token`, `Discord:OAuth:ClientId`, `Discord:OAuth:ClientSecret`, `Anthropic:ApiKey`, `AzureSpeech:SubscriptionKey`
- **Command propagation** - Without `Discord:TestGuildId`, global commands take up to 1 hour to appear
- **Discord terminology** - Use "guild" not "server" in URLs/code (Discord API convention)
- **Database provider** - Set `Database:Provider` to `Sqlite` or `PostgreSql` to explicitly select a provider; omit for auto-detection from the connection string (`Host=`/`Server=` → PostgreSQL, file-path `Data Source` → SQLite). Default is SQLite at `data/discordbot.db`.

### PostgreSQL

- **EF CLI requires `--context`** - Both `SqliteBotDbContext` and `PostgresBotDbContext` design-time factories exist; always pass `--context` to EF CLI commands (see Quick Reference above).
- **Separate migration sets** - SQLite migrations live in `Migrations/Sqlite/`, PostgreSQL in `Migrations/Postgresql/`.
- **Npgsql legacy timestamp** - `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` is applied at startup. Do not remove this switch; removing it causes `DateTime` write errors with `timestamp with time zone` columns.

### Audio Dependencies

FFmpeg required for audio features. On Windows, `libsodium.dll` and `opus.dll` must be in build output. See [audio-dependencies.md](docs/articles/audio-dependencies.md).

### VOX System

Half-Life style concatenated clip announcements. Three clip groups: **VOX** (scientist), **FVOX** (female scientist), **HGRUNT** (military radio).

**Configuration** (`appsettings.json` → `Vox` section):
```json
{
  "Vox": {
    "BasePath": "./sounds",
    "DefaultWordGapMs": 50,
    "MaxMessageWords": 50,
    "MaxMessageLength": 500
  }
}
```

**Service Architecture:**
- `IVoxClipLibrary` (Singleton) - Clip inventory, scanned at startup via `VoxClipLibraryInitializer`
- `IVoxConcatenationService` (Singleton) - FFmpeg-based audio joining with configurable silence gaps
- `IVoxService` (Scoped) - Orchestrates tokenization → clip lookup → concatenation → playback

**DI Registration:** `services.AddVox()` in `VoiceServiceExtensions.cs`

**Slash Commands:** `/vox`, `/fvox`, `/hgrunt` with `message` and optional `gap` (20-200ms) parameters. Rate limited: 5 per 10 seconds.

**Preconditions:** `[RequireGuildActive]`, `[RequireAudioEnabled]`, `[RequireVoiceChannel]`

## Key Documentation

Build and serve locally: `.\build-docs.ps1 -Serve`

| Doc | Purpose |
|-----|---------|
| [component-api.md](docs/articles/component-api.md) | Razor UI component library (Button, Badge, Card, FormInput, etc.) |
| [design-system.md](docs/articles/design-system.md) | UI tokens, color palette, component specs |
| [interactive-components.md](docs/articles/interactive-components.md) | Discord button/component patterns with `ComponentIdBuilder` |
| [identity-configuration.md](docs/articles/identity-configuration.md) | Authentication setup and troubleshooting |
| [authorization-policies.md](docs/articles/authorization-policies.md) | Role hierarchy (SuperAdmin > Admin > Moderator > Viewer) |
| [form-implementation-standards.md](docs/articles/form-implementation-standards.md) | Razor Pages form patterns and validation |
| [audit-log-system.md](docs/articles/audit-log-system.md) | Audit logging fluent builder API |
| [soundboard.md](docs/articles/soundboard.md) | Soundboard feature, playback, portal, API, export |
| [tts-support.md](docs/articles/tts-support.md) | Text-to-Speech with Azure Cognitive Services |
| [unified-now-playing.md](docs/articles/unified-now-playing.md) | Unified Now Playing component (SignalR, SSR, VoiceChannelPanel) |
| [voice-capability-system.md](docs/articles/voice-capability-system.md) | Voice capability-aware UI system |
| [voice-selector-spec.md](docs/articles/voice-selector-spec.md) | Voice selector component specification |
| [vox-system-spec.md](docs/articles/vox-system-spec.md) | VOX/FVOX/HGRUNT clip library architecture |
| [vox-ui-spec.md](docs/articles/vox-ui-spec.md) | VOX Portal UI/UX specification |
| [scheduled-messages.md](docs/articles/scheduled-messages.md) | Scheduled messages and cron expressions |
| [reminder-system.md](docs/articles/reminder-system.md) | Personal reminders with natural language parsing |
| [notification-system.md](docs/articles/notification-system.md) | User notification system |
| [database-schema.md](docs/articles/database-schema.md) | Entity relationships and schema |
| [testing-guide.md](docs/articles/testing-guide.md) | Testing patterns and fixtures |
| [docker-deployment.md](docs/articles/docker-deployment.md) | Docker and Docker Compose deployment guide |

## User/Guild Preview Popups

When displaying user/guild names/IDs, add hover preview support (loaded globally in `_Layout.cshtml`):

```razor
<!-- User preview -->
<span class="preview-trigger" data-preview-type="user"
      data-user-id="@item.UserId" data-context-guild-id="@Model.GuildId">@item.Username</span>

<!-- Guild preview -->
<span class="preview-trigger" data-preview-type="guild"
      data-guild-id="@item.GuildId">@item.GuildName</span>
```

See implementations in Command Logs, Audit Logs, Member Directory, RatWatch, Reminders pages.

## Large Files Warning

Files exceeding standard read limits - search for specific methods instead of full read:

**Services (500+ lines):** `RatWatch/RatWatchService.cs` (1,159), `UserManagementService.cs` (995), `SearchService.cs` (919), `PlaybackService.cs` (918), `UserDataExportService.cs` (762), `BotHostedService.cs` (739), `ScheduledMessageService.cs` (702), `NotificationService.cs` (675), `Tts/VoiceCapabilityProvider.cs` (649), `Tts/SsmlValidator.cs` (631), `AlertMonitoringService.cs` (628), `PageMetadataService.cs` (609), `TimeParsingService.cs` (598), `ApiRequestTracker.cs` (584), `ConsentService.cs` (567), `AzureTtsService.cs` (527)

**Controllers (500+ lines):** `PerformanceMetricsController.cs` (1,173), `PortalTtsController.cs` (1,089), `AnalyticsController.cs` (698), `AlertsController.cs` (560), `PerformanceTabsController.cs` (553), `PortalVoxController.cs` (522)

**Documentation:** `api-endpoints.md`, `design-system.md`

## Common Issues

- **Commands not appearing**: Set `Discord:TestGuildId` in user secrets for instant registration
- **Bot doesn't connect**: Verify bot token and gateway intents in Discord Developer Portal
- **OAuth fails**: Check redirect URIs match environment (`https://localhost:5001/signin-discord` for dev)
- **Audio not playing**: Verify FFmpeg in PATH and libsodium/opus DLLs in output directory

## Development Endpoints

- Admin UI: `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger`
- Seq (optional): `http://localhost:5341`
- Elasticsearch (optional): `http://localhost:9200`
- Kibana (optional): `http://localhost:5601`
- Elastic APM (optional): `http://localhost:8200`

## Agent Definitions

Domain-expert agents live in `.claude/agents/`. Each agent owns a feature stream and carries domain-specific knowledge (key files, patterns, gotchas).

**Maintenance rule:** When completing feature work that adds new services, entities, repositories, or significantly changes patterns within a stream, update the relevant agent definition in `.claude/agents/` as part of the same work. Keep file paths, service inventories, and gotchas current.

| Agent | Stream |
|-------|--------|
| `moderation-safety` | Mod cases, notes, tags, watchlists, auto-mod, content filtering, raid/spam, flagged events |
| `audio-voice` | Soundboard, TTS (Azure), VOX system, playback, voice channel management |
| `ai-assistant` | Anthropic/Claude integration, agent runner, tool registry/providers, cost tracking |
| `scheduling-notifications` | Scheduled messages, reminders, notifications, time parsing |
| `analytics-observability` | Analytics, performance monitoring, alerting, health, SignalR, Serilog/OTel/APM |
| `user-identity` | Identity, Discord OAuth, consent/GDPR, data export/purge, verification, roles |
| `guild-configuration` | Guild settings, member sync, command module config, welcome system, IOptions |
| `community-engagement` | Rat Watch, public leaderboards, fun/nice-to-have features |
| `data-infrastructure` | EF Core, repositories, migrations, audit/message logging, search, caching, background services |
| `web-ui-portal` | Razor Pages, shared components, Tailwind/HTMX/Alpine.js, portal, design system, API controllers |

## Lookup Reference

For comprehensive tables (Configuration Options, UI Page Routes, Command Modules, Full Docs Index), see [CLAUDE-REFERENCE.md](CLAUDE-REFERENCE.md).

Generate/update reference: `/update-instructions tables`
