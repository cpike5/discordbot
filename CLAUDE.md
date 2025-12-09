# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Discord bot management system built with .NET 8 and Discord.NET. Combines a Discord bot hosted service with a Web API for management, plus a future Razor Pages admin UI.

## Build & Run Commands

```bash
# Build entire solution
dotnet build

# Run the bot (from solution root)
dotnet run --project src/DiscordBot.Bot

# Run tests
dotnet test

# Run single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Add EF Core migration
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Apply migrations
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Build documentation
dotnet tool restore                    # First time: restore DocFX tool
.\build-docs.ps1                       # Windows: build docs
.\build-docs.ps1 -Serve                # Windows: build and serve locally at http://localhost:8080
./build-docs.sh                        # Linux/macOS: build docs
./build-docs.sh --serve                # Linux/macOS: build and serve locally
```

## Configuration

Discord bot token must be configured via User Secrets (never commit tokens):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id"  # Optional: instant command registration
```

UserSecretsId: `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

## Architecture

Three-layer clean architecture:

| Layer | Project | Purpose |
|-------|---------|---------|
| Domain | `DiscordBot.Core` | Entities, interfaces, DTOs, enums |
| Infrastructure | `DiscordBot.Infrastructure` | EF Core DbContext, repositories, Serilog config |
| Application | `DiscordBot.Bot` | Web API controllers, bot hosted service, command modules, DI composition |

**Key patterns:**
- `DiscordSocketClient` registered as singleton, managed by `BotHostedService`
- Repository pattern with interfaces in Core, implementations in Infrastructure
- Serilog as logging provider, inject `ILogger<T>` via DI
- SQLite for dev/test, MSSQL/MySQL/PostgreSQL for production

## Key Documentation

Reference these docs for detailed specifications:

- [docs/articles/requirements.md](docs/articles/requirements.md) - Technology stack and architecture requirements
- [docs/articles/mvp-plan.md](docs/articles/mvp-plan.md) - MVP implementation phases and file structure
- [docs/articles/design-system.md](docs/articles/design-system.md) - UI design tokens, color palette, component specs
- [docs/articles/api-endpoints.md](docs/articles/api-endpoints.md) - REST API documentation
- [docs/articles/interactive-components.md](docs/articles/interactive-components.md) - Button/component patterns

Build and serve documentation locally with `.\build-docs.ps1 -Serve` to view the full documentation site.

## Discord.NET Specifics

- Using Discord.NET 3.18.0
- Slash commands only (no prefix commands)
- `InteractionHandler` discovers and registers command modules from assembly
- Command modules inherit from `InteractionModuleBase<SocketInteractionContext>`
- Precondition attributes for permission checks: `RequireAdminAttribute`, `RequireOwnerAttribute`, `RateLimitAttribute`

**Interactive Components Pattern:**
- Use `ComponentIdBuilder` to create custom IDs: `{handler}:{action}:{userId}:{correlationId}:{data}`
- Store component state via `IInteractionStateService` (15-min default expiry)
- Component handlers go in separate `*ComponentModule` classes using `[ComponentInteraction]` attribute

**Adding a New Command:**
1. Create module in `Commands/` inheriting from `InteractionModuleBase<SocketInteractionContext>`
2. Use `[SlashCommand("name", "description")]` attribute on methods
3. Inject dependencies via constructor (logger, services, etc.)
4. If using buttons/components, create separate component handler module

## Testing

- xUnit with FluentAssertions and Moq
- Test project references Core and Infrastructure
- Mock repositories for unit tests
