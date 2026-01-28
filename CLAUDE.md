# CLAUDE.md

Guidance for Claude Code when working with this Discord bot management system. See README.md for full documentation.

**Current version:** v0.17.0-dev. See [CLAUDE-REFERENCE.md](CLAUDE-REFERENCE.md) for comprehensive lookup tables.

## Quick Reference

```bash
# Build & Run
dotnet build
dotnet run --project src/DiscordBot.Bot
dotnet test

# Entity Framework
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

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

### Audio Dependencies

FFmpeg required for audio features. On Windows, `libsodium.dll` and `opus.dll` must be in build output. See [audio-dependencies.md](docs/articles/audio-dependencies.md).

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
| [database-schema.md](docs/articles/database-schema.md) | Entity relationships and schema |
| [testing-guide.md](docs/articles/testing-guide.md) | Testing patterns and fixtures |

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

**Services (500+ lines):** `RatWatch/RatWatchService.cs` (1,159), `UserManagementService.cs` (995), `SearchService.cs` (919), `PlaybackService.cs` (918), `UserDataExportService.cs` (762), `BotHostedService.cs` (739), `ScheduledMessageService.cs` (702), `TimeParsingService.cs` (598)

**Controllers (500+ lines):** `PerformanceMetricsController.cs` (1,173), `AnalyticsController.cs` (698), `PortalSoundboardController.cs` (652)

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

## Lookup Reference

For comprehensive tables (Configuration Options, UI Page Routes, Command Modules, Full Docs Index), see [CLAUDE-REFERENCE.md](CLAUDE-REFERENCE.md).

Generate/update reference: `/update-instructions tables`
