# Discord Bot Management System

[![Version](https://img.shields.io/badge/version-v0.3.6-blue)](https://github.com/cpike5/discordbot/releases)
[![CI](https://github.com/cpike5/discordbot/actions/workflows/ci.yml/badge.svg)](https://github.com/cpike5/discordbot/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Discord.NET](https://img.shields.io/badge/Discord.NET-3.18.0-5865F2)](https://github.com/discord-net/Discord.Net)

A Discord bot built with .NET 8 and Discord.NET that provides a foundation for managing Discord servers through slash commands, a REST API, and a Razor Pages admin UI. The system combines a hosted Discord bot service with a Web API and admin dashboard for management and monitoring.

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
  - [Prerequisites](#prerequisites)
  - [Configuration](#configuration)
  - [Running the Bot](#running-the-bot)
- [Architecture](#architecture)
  - [Key Components](#key-components)
- [Commands](#commands)
- [Logging](#logging)
  - [Log Levels](#log-levels)
- [Development](#development)
  - [Adding New Commands](#adding-new-commands)
  - [Building and Testing](#building-and-testing)
  - [Adding Migrations](#adding-migrations)
- [Project Structure](#project-structure)
- [Configuration Reference](#configuration-reference)
  - [appsettings.json](#appsettingsjson)
  - [User Secrets](#user-secrets)
- [Dependencies](#dependencies)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)
- [Related Documentation](#related-documentation)

## Features

- Slash command framework with automatic discovery and registration
- Razor Pages admin UI with Tailwind CSS
- ASP.NET Core Identity with Discord OAuth integration
- Role-based authorization (SuperAdmin, Admin, Moderator, Viewer)
- User management dashboard with Discord account linking
- Structured logging with Serilog (console and rotating file outputs)
- Clean architecture with separation of concerns
- Graceful bot lifecycle management
- Swagger/OpenAPI documentation for REST API

## Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js (for Tailwind CSS build)
- Discord bot token ([Create a bot](https://discord.com/developers/applications))

### Configuration

1. **Set up Discord bot token** (required):

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Discord:Token" "your-bot-token-here"
```

2. **Optional: Configure test guild for faster command registration**:

```bash
dotnet user-secrets set "Discord:TestGuildId" "your-guild-id-here"
```

When `TestGuildId` is set, commands register instantly to that guild. Without it, commands register globally (can take up to 1 hour to propagate).

3. **Optional: Configure Discord OAuth for admin UI**:

```bash
dotnet user-secrets set "Discord:OAuth:ClientId" "your-client-id"
dotnet user-secrets set "Discord:OAuth:ClientSecret" "your-client-secret"
```

4. **Optional: Configure default admin user**:

```bash
dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:DefaultAdmin:Password" "InitialPassword123!"
```

### Running the Bot

From the solution root:

```bash
dotnet run --project src/DiscordBot.Bot
```

The bot will:
- Connect to Discord and appear online
- Register slash commands
- Log output to console and `logs/discordbot-YYYY-MM-DD.log`
- Start Web API and admin UI on `https://localhost:5001` (or `http://localhost:5000`)
- Swagger docs available at `/swagger`

To stop the bot gracefully, press `Ctrl+C`.

## Architecture

The solution follows a three-layer clean architecture pattern:

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Domain** | `DiscordBot.Core` | Entities, interfaces, DTOs, enums, authorization roles |
| **Infrastructure** | `DiscordBot.Infrastructure` | EF Core (with Identity), repositories, logging configuration |
| **Application** | `DiscordBot.Bot` | Web API controllers, Razor Pages admin UI, bot hosted service, command modules |

### Key Components

- **BotHostedService**: Manages Discord bot lifecycle (startup, login, shutdown)
- **InteractionHandler**: Discovers and registers slash command modules automatically
- **DiscordSocketClient**: Singleton client for Discord gateway communication
- **Command Modules**: Slash command implementations inheriting from `InteractionModuleBase`
- **Razor Pages**: Admin UI with Tailwind CSS for user/guild management
- **Identity System**: ASP.NET Core Identity with Discord OAuth external login

For detailed architecture documentation, see [docs/articles/architecture-history.md](docs/articles/architecture-history.md).

## Commands

### /ping

Check the bot's latency and responsiveness.

**Usage:** `/ping`

**Response:** Displays bot latency in milliseconds with timestamp.

### /admin

Server administration commands (requires admin permissions).

**Subcommands:**
- `/admin info` - Display server information
- `/admin kick <user> [reason]` - Kick a user from the server
- `/admin ban <user> [reason]` - Ban a user from the server

### /verify

Link your Discord account to a web admin account.

**Usage:** `/verify`

**Response:** Provides a verification code to enter in the admin UI under Account > Link Discord. See [bot-verification.md](docs/articles/bot-verification.md) for details.

## Logging

The bot uses Serilog for structured logging with two outputs:

1. **Console Sink**: Real-time output during development
   - Format: `[HH:mm:ss LEVEL] SourceContext: Message`
   - Log levels: Information and above (configurable)

2. **File Sink**: Rolling file logs for persistence
   - Location: `logs/discordbot-YYYY-MM-DD.log`
   - Retention: 7 days (configurable)
   - Format: ISO 8601 timestamp with full context

### Log Levels

Configure in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Information"
      }
    }
  }
}
```

## Development

### Adding New Commands

1. Create a new module class in `src/DiscordBot.Bot/Commands/`:

```csharp
using Discord.Interactions;

namespace DiscordBot.Bot.Commands;

public class MyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<MyModule> _logger;

    public MyModule(ILogger<MyModule> logger)
    {
        _logger = logger;
    }

    [SlashCommand("mycommand", "Description of my command")]
    public async Task MyCommandAsync()
    {
        _logger.LogInformation("MyCommand executed by {User}", Context.User.Username);
        await RespondAsync("Hello from my command!");
    }
}
```

2. The `InteractionHandler` will automatically discover and register the command on bot startup.

3. Restart the bot to apply changes.

### Building and Testing

```bash
# Build entire solution
dotnet build

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

### Adding Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot

# Apply migrations
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

## Project Structure

```
discordbot/
├── src/
│   ├── DiscordBot.Core/           # Domain layer (entities, interfaces, DTOs)
│   ├── DiscordBot.Infrastructure/ # EF Core DbContext, repositories, logging
│   └── DiscordBot.Bot/            # Application layer (API + bot + admin UI)
│       ├── Authorization/         # Custom authorization handlers
│       ├── Commands/              # Slash command modules
│       ├── Components/            # Interactive component utilities
│       ├── Controllers/           # REST API controllers
│       ├── Extensions/            # DI and service extensions
│       ├── Handlers/              # Interaction handlers
│       ├── Pages/                 # Razor Pages admin UI
│       │   ├── Account/           # Login, logout, OAuth pages
│       │   ├── Admin/Users/       # User management CRUD
│       │   └── Shared/            # Layouts, partials, components
│       ├── Preconditions/         # Permission check attributes
│       ├── Services/              # Bot hosted service, state management
│       ├── ViewModels/            # Page and component view models
│       └── Program.cs             # DI composition root
├── tests/
│   └── DiscordBot.Tests/          # Unit and integration tests
├── docs/                          # DocFX documentation
└── logs/                          # Log files (created at runtime)
```

## Configuration Reference

### appsettings.json

```json
{
  "Discord": {
    "Token": "",           // Set via user secrets
    "TestGuildId": null    // Optional: Guild ID for instant command registration
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

### User Secrets

User Secrets ID: `7b84433c-c2a8-46db-a8bf-58786ea4f28e`

Required secrets:
- `Discord:Token`: Your Discord bot token

Optional secrets:
- `Discord:TestGuildId`: Guild ID for development command testing
- `Discord:OAuth:ClientId`: Discord OAuth client ID for admin login
- `Discord:OAuth:ClientSecret`: Discord OAuth client secret
- `Identity:DefaultAdmin:Email`: Default admin user email
- `Identity:DefaultAdmin:Password`: Default admin user password

## Dependencies

### Core Packages

- **Discord.Net** (3.18.0): Discord API client and gateway
- **AspNet.Security.OAuth.Discord** (8.0.0): Discord OAuth authentication
- **Microsoft.AspNetCore.Identity.EntityFrameworkCore** (8.0.0): ASP.NET Core Identity
- **Serilog.AspNetCore** (8.0.0): Structured logging framework
- **Swashbuckle.AspNetCore** (6.5.0): Swagger/OpenAPI documentation

### Development

- **xUnit**: Test framework
- **FluentAssertions**: Assertion library
- **Moq**: Mocking framework
- **Tailwind CSS**: Utility-first CSS framework (built via npm)

## Security

- Never commit bot tokens to version control
- Use .NET User Secrets for local development
- Use environment variables or secret management services in production
- The `.gitignore` includes `appsettings.Development.json` and log files

## Troubleshooting

### Bot doesn't appear online

1. Check that the bot token is configured correctly
2. Verify the token is valid in Discord Developer Portal
3. Check logs for connection errors
4. Ensure bot has proper gateway intents enabled

### Commands not appearing

1. Check if `TestGuildId` is set in configuration
2. Without test guild, global commands take up to 1 hour to register
3. Check logs for command registration errors
4. Verify bot has `applications.commands` scope

### Permission errors

1. Ensure bot has necessary permissions in Discord server
2. Check role hierarchy (bot role must be high enough)
3. Review bot permissions in Discord Developer Portal

## Contributing

1. Create a feature branch from `main`
2. Make your changes
3. Add tests for new functionality
4. Submit a pull request

## License

This project is for educational and development purposes.

## Related Documentation

- [Architecture History](docs/articles/architecture-history.md) - Original implementation plan
- [Requirements](docs/articles/requirements.md) - Technology stack and specifications
- [Design System](docs/articles/design-system.md) - UI design tokens and components
- [API Endpoints](docs/articles/api-endpoints.md) - REST API documentation
- [Interactive Components](docs/articles/interactive-components.md) - Button/component patterns
- [Identity Configuration](docs/articles/identity-configuration.md) - Authentication setup
- [Authorization Policies](docs/articles/authorization-policies.md) - Role hierarchy and access control
- [User Management](docs/articles/user-management.md) - Admin UI user management
- [Bot Verification](docs/articles/bot-verification.md) - Discord account linking flow
- [Versioning Strategy](docs/articles/versioning-strategy.md) - SemVer versioning, CI/CD, release process
- [CLAUDE.md](CLAUDE.md) - Guidance for Claude Code AI assistant
