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
```

## Configuration

Discord bot token must be configured via User Secrets (never commit tokens):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
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

- [docs/requirements.md](docs/requirements.md) - Technology stack and architecture requirements
- [docs/mvp-plan.md](docs/mvp-plan.md) - MVP implementation phases and file structure
- [docs/design-system.md](docs/design-system.md) - UI design tokens, color palette, component specs

## Discord.NET Specifics

- Using Discord.NET 3.18.0
- Slash commands only (no prefix commands)
- `InteractionHandler` discovers and registers command modules
- Command modules inherit from `InteractionModuleBase`
- Precondition attributes for permission checks (e.g., `RequireAdminAttribute`)

## Testing

- xUnit with FluentAssertions and Moq
- Test project references Core and Infrastructure
- Mock repositories for unit tests
